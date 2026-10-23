using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.Shop;
using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>
    /// The store screen: an overhead view of the shop with labels over everything clickable, the
    /// Open Shop controls, levels and daily goals, customers' thought bubbles and sale popups, and
    /// the register, supplier, unlock, buy-a-tank and end-of-day windows. Clicking a tank flies the
    /// camera in and hands over to <see cref="FishTankHUD"/>; selecting a fish there makes the camera
    /// follow it. Esc steps back out: fish, then tank, then store. After closing, the to-do bar takes
    /// the player through every problem in the tanks one at a time.
    /// Placeholder IMGUI like the tank HUD, to be replaced with a real UI later.
    /// </summary>
    public class StoreView : MonoBehaviour
    {
        enum Panel { None, Register, Supplier, Unlock, BuyTank, DayReport }

        static readonly Vector3 StoreViewDirection = new Vector3(0f, 0.62f, -0.78f).normalized;
        static readonly Color StoreBackground = new Color(0.17f, 0.19f, 0.23f);
#if UNITY_EDITOR
        static readonly string[] SupplierTabs = { "Eggs", "Decorations", "Food", "Equipment and care", "Fry (dev)" };
#else
        static readonly string[] SupplierTabs = { "Eggs", "Decorations", "Food", "Equipment and care" };
#endif
        const int EggsTab = 0, DecorTab = 1, FoodTab = 2, EquipmentTab = 3, DevFryTab = 4;
        static readonly float[] Speeds = { 1f, 5f, 20f };

        StoreLayout store;
        ShopDay shopDay;
        FishTankHUD hud;
        Camera cam;
        StoreTank focused;
        Panel panel;
        StoreLayout.Section pendingSection;
        StoreLayout.Slot pendingSlot;
        StoreTank orderTarget;
        int supplierTab;
        Vector2 supplierScroll;
        float panX;
        float distance = 15f;
        Rect panelRect, topBar, statusBar, todoBar;
        readonly ToDoList todo = new ToDoList();
        float todoFinishedTime = -1f;

        public void Initialize(StoreLayout layout, ShopDay day, FishTankHUD tankHud, Camera camera)
        {
            store = layout;
            shopDay = day;
            hud = tankHud;
            cam = camera;
            hud.enabled = false;
            // The water's murk measures how much water each pixel looks through.
            cam.depthTextureMode |= DepthTextureMode.Depth;
            ApplyStoreLook();
            GetStorePose(out var position, out var rotation);
            cam.transform.SetPositionAndRotation(position, rotation);
        }

        void Update()
        {
            if (store == null || cam == null) return;
            float dt = Time.unscaledDeltaTime;

            HandleDevKeys();
            foreach (var levelUp in Progression.LevelUps) levelUp.age += dt;
            Progression.LevelUps.RemoveAll(l => l.age > 6f);

            if (shopDay.LastReport != null && panel != Panel.DayReport)
            {
                if (focused != null) ExitTank();
                todo.Dismiss();
                panel = Panel.DayReport;
            }
            UpdateToDo(dt);

            Vector3 targetPosition;
            Quaternion targetRotation;
            // Esc steps back one level: from a fish to its tank, from the tank to the store.
            bool escape = Input.GetKeyDown(KeyCode.Escape);
            if (focused != null && focused.Tank == null) ExitTank();
            else if (focused != null && escape)
            {
                if (hud.HasSelection) hud.ClearSelection();
                else ExitTank();
            }
            else if (focused == null && escape && panel != Panel.DayReport) panel = Panel.None;
            if (focused != null)
            {
                GetTankPose(focused, hud.Focus, out targetPosition, out targetRotation);
            }
            else
            {
                if (panel == Panel.None) HandleStoreCameraInput(dt);
                GetStorePose(out targetPosition, out targetRotation);
            }

            float blend = 1f - Mathf.Exp(-6f * dt);
            cam.transform.SetPositionAndRotation(
                Vector3.Lerp(cam.transform.position, targetPosition, blend),
                Quaternion.Slerp(cam.transform.rotation, targetRotation, blend));
        }

        void HandleStoreCameraInput(float dt)
        {
            float pan = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) pan -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) pan += 1f;
            panX += pan * 7f * dt;
            if (Input.GetMouseButton(1)) panX -= Input.GetAxis("Mouse X") * 0.25f;
            distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 1.2f, 7f, 26f);

            // The back room is to the west; you can look one section past the open ones, to see what's for sale.
            float maxX = Mathf.Min(store.UnlockedCount, store.Sections.Count - 1) * StoreLayout.SectionWidth;
            panX = Mathf.Clamp(panX, StoreLayout.BackRoomCenterX, maxX);
        }

        void GetStorePose(out Vector3 position, out Quaternion rotation)
        {
            var lookAt = new Vector3(panX, 0.8f, 0.3f);
            position = lookAt + StoreViewDirection * distance;
            rotation = Quaternion.LookRotation(lookAt - position);
        }

        /// <summary>
        /// The whole tank fills the view (bigger tanks are seen from further back). With a fish
        /// selected the camera follows it at half the distance, keeping it centred.
        /// </summary>
        static void GetTankPose(StoreTank tank, Fish follow, out Vector3 position, out Quaternion rotation)
        {
            var bounds = tank.Tank.InnerBounds;
            float back = Mathf.Max(1.9f, 1.9f * bounds.size.x / 1.6f, bounds.size.y * 2.1f);
            if (follow != null && follow.Tank == tank.Tank)
            {
                var target = follow.transform.position;
                position = target + new Vector3(0f, 0.05f, -back * 0.5f);
                rotation = Quaternion.LookRotation(target - position);
                return;
            }
            var center = bounds.center;
            position = center + new Vector3(0f, 0.1f, -back);
            rotation = Quaternion.LookRotation(center - position);
        }

        void EnterTank(StoreTank tank)
        {
            panel = Panel.None;
            if (focused == tank) return;
            focused = tank;
            Tank.Focused = tank.Tank;
            hud.Initialize(tank, store.Tanks, cam, ExitTank, () => shopDay.IsOpen, UpgradeTank);
            hud.enabled = true;
        }

        bool UpgradeTank(StoreTank tank, int size) => !shopDay.IsOpen && store.TryUpgradeTank(tank, size);

        void ExitTank()
        {
            if (focused != null)
            {
                float x = focused.transform.position.x;
                panX = focused.InBackRoom ? StoreLayout.BackRoomCenterX : Mathf.Round(x / StoreLayout.SectionWidth) * StoreLayout.SectionWidth;
            }
            focused = null;
            Tank.Focused = null;
            hud.enabled = false;
            ApplyStoreLook();
        }

        void ApplyStoreLook()
        {
            RenderSettings.fog = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = StoreBackground;
        }

        // ---------------------------------------------------------------- GUI

        void OnGUI()
        {
            if (store == null) return;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            if (focused != null)
            {
                // In the tank view the tank HUD owns the screen; just show the shop clock if open,
                // the to-do bar and level-ups.
                if (shopDay.IsOpen) DrawShopClock(new Rect(width - 330f, 10f, 320f, 34f));
                todoBar = new Rect(Mathf.Max(300f, width * 0.5f - 280f), 10f, Mathf.Min(560f, width - 650f), 92f);
                DrawToDoBar();
                DrawLevelUps(width, 140f);
                return;
            }

            topBar = new Rect(10f, 10f, width - 20f, 34f);
            statusBar = new Rect(10f, 48f, width - 20f, 30f);
            DrawTopBar();
            DrawStatusBar();
            if (panel == Panel.None)
            {
                DrawWorldLabels(scale, height);
                DrawCustomerBubbles(scale, height);
            }
            DrawMoneyPopups(scale, height);
            DrawLog(height);
            todoBar = new Rect(width * 0.5f - 280f, 84f, 560f, 92f);
            if (panel == Panel.None) DrawToDoBar();
            DrawLevelUps(width, 190f);

            switch (panel)
            {
                case Panel.Register: panelRect = Centered(width, height, 500f, 560f); DrawRegister(); break;
                case Panel.Supplier: panelRect = Centered(width, height, 760f, Mathf.Min(700f, height - 100f)); DrawSupplier(); break;
                case Panel.Unlock: panelRect = Centered(width, height, 380f, 170f); DrawUnlock(); break;
                case Panel.BuyTank: panelRect = Centered(width, height, 440f, 250f); DrawBuyTank(); break;
                case Panel.DayReport: panelRect = Centered(width, height, 460f, 360f); DrawDayReport(); break;
            }

            HandleClick();
        }

        static Rect Centered(float width, float height, float w, float h) => new Rect((width - w) * 0.5f, (height - h) * 0.5f, w, h);

        void DrawTopBar()
        {
            GUILayout.BeginArea(topBar, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{Economy.Format(Economy.Money)}</b>", Rich(), GUILayout.Width(90f));
            GUILayout.Label($"Day {Economy.Day}", GUILayout.Width(55f));
            GUILayout.Label($"Reputation {Economy.Reputation * 100f:0}%", GUILayout.Width(115f));
            GUILayout.Label($"Flakes {Economy.FoodPortions(FoodType.Flakes)}  Premium {Economy.FoodPortions(FoodType.Premium)}", GUILayout.Width(170f));
            if (shopDay.IsOpen)
            {
                var today = shopDay.Today;
                GUILayout.Label($"<color=#9fe39f><b>OPEN</b></color>  {Clock(shopDay.TimeLeft)} left   Visitors {today.visitors}   Sold {today.fishSold}   +{Economy.Format(today.earnings)}", Rich());
                GUILayout.FlexibleSpace();
                DrawDevControls();
            }
            else
            {
                GUILayout.Label("<color=#ffd27f><b>CLOSED</b></color>  Tank work, ordering and pricing happen now.", Rich());
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("<b>Open Shop</b>", Button(), GUILayout.Width(120f))) shopDay.Open();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>Both levels with their XP bars, and today's goals.</summary>
        void DrawStatusBar()
        {
            GUILayout.BeginArea(statusBar, GUI.skin.box);
            GUILayout.BeginHorizontal();
            LevelMeter(XpTrack.Fishkeeping);
            GUILayout.Space(14f);
            LevelMeter(XpTrack.Shopkeeping);
            GUILayout.Space(14f);
            var goals = new List<string>();
            foreach (var goal in Progression.Goals)
                goals.Add(goal.Done ? $"<color=#9fe39f>{goal.Describe()} (done)</color>" : $"{goal.Describe()} <b>{goal.progress}/{goal.target}</b>");
            GUILayout.Label("<b>Today:</b> " + (goals.Count > 0 ? string.Join("   ", goals) : "no goals"), Rich());
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static void LevelMeter(XpTrack track)
        {
            int level = Progression.Level(track);
            GUILayout.Label($"<b>{Progression.TrackName(track)} {level}</b>", Rich(), GUILayout.Width(track == XpTrack.Fishkeeping ? 105f : 110f));
            var rect = GUILayoutUtility.GetRect(110f, 12f, GUILayout.Width(110f));
            rect.y += 5f;
            float fill = Progression.IsMaxLevel(track) ? 1f : Progression.Xp(track) / (float)Progression.XpToNext(level);
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.4f), 0f, 0f);
            var color = track == XpTrack.Fishkeeping ? new Color(0.35f, 0.7f, 0.95f) : new Color(0.95f, 0.75f, 0.3f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
            GUILayout.Label(Progression.IsMaxLevel(track) ? "max" : $"{Progression.Xp(track)}/{Progression.XpToNext(level)}", GUILayout.Width(70f));
        }

        void DrawLevelUps(float width, float y)
        {
            foreach (var levelUp in Progression.LevelUps)
            {
                float alpha = Mathf.Clamp01(6f - levelUp.age) * Mathf.Clamp01(levelUp.age * 4f);
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                string unlocks = levelUp.unlocks.Count > 0 ? "\nUnlocked: " + string.Join(", ", levelUp.unlocks) : "";
                var content = new GUIContent($"<size=22><b>{Progression.TrackName(levelUp.track)} level {levelUp.level}!</b></size>{unlocks}");
                float w = Mathf.Min(620f, width - 40f);
                float h = Banner().CalcHeight(content, w);
                GUI.Box(new Rect((width - w) * 0.5f, y, w, h), content, Banner());
                GUI.color = old;
                y += h + 6f;
            }
        }

        // ---------------------------------------------------------------- to-do list after closing

        void StartToDo()
        {
            if (todo.Build(store.Tanks) == 0)
            {
                GameLog.Add("Every tank looks fine. Nothing urgent to do.");
                return;
            }
            GameLog.Add($"{todo.Count} things need seeing to after closing.");
            if (Balance.Data.notifications.startAutomatically && todo.Next()) GoToCurrent();
        }

        void GoToCurrent()
        {
            var item = todo.Current;
            if (item == null || item.tank == null) return;
            EnterTank(item.tank);
            if (item.fish != null) hud.Select(item.fish);
            else hud.ClearSelection();
        }

        void NextToDo()
        {
            if (todo.Next()) GoToCurrent();
            else
            {
                GameLog.Add("All caught up!");
                todoFinishedTime = Time.unscaledTime;
            }
        }

        /// <summary>Moves on by itself a moment after the current problem is dealt with.</summary>
        void UpdateToDo(float dt)
        {
            var item = todo.Current;
            if (item == null || shopDay.IsOpen) return;
            if (!item.Resolved)
            {
                todo.ResolvedFor = 0f;
                return;
            }
            todo.ResolvedFor += dt;
            float wait = Balance.Data.notifications.autoAdvanceSeconds;
            if (wait >= 0f && todo.ResolvedFor >= wait) NextToDo();
        }

        void DrawToDoBar()
        {
            var item = todo.Current;
            if (item == null)
            {
                if (todoFinishedTime >= 0f && Time.unscaledTime - todoFinishedTime < 3f)
                {
                    var done = new Rect(todoBar.x, todoBar.y, todoBar.width, 34f);
                    GUI.Box(done, "<b><color=#9fe39f>All caught up!</color></b> Every problem from closing is dealt with.", Banner());
                }
                else if (!shopDay.IsOpen && todo.Count > 0 && todo.Remaining > 0)
                {
                    GUILayout.BeginArea(new Rect(todoBar.x, todoBar.y, todoBar.width, 34f), UiStyles.Window);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{todo.Remaining} things still need seeing to.", Rich());
                    if (GUILayout.Button("Resume", GUILayout.Width(90f)))
                    {
                        if (todo.Next()) GoToCurrent();
                    }
                    if (GUILayout.Button("Dismiss", GUILayout.Width(80f))) todo.Dismiss();
                    GUILayout.EndHorizontal();
                    GUILayout.EndArea();
                }
                return;
            }

            GUILayout.BeginArea(todoBar, UiStyles.Window);
            bool resolved = item.Resolved;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>To do {todo.Index + 1} of {todo.Count}</b>   {item.tank.Title}", Rich());
            GUILayout.FlexibleSpace();
            if (resolved) GUILayout.Label("<color=#9fe39f><b>Done!</b></color>", Rich());
            GUILayout.EndHorizontal();
            GUILayout.Label(resolved ? $"<color=#9fe39f>{item.title}</color>" : $"<b>{item.title}</b> {item.hint}", Rich());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Back", GUILayout.Width(80f)) && todo.Previous()) GoToCurrent();
            if (GUILayout.Button("Take me there", GUILayout.Width(120f))) GoToCurrent();
            if (GUILayout.Button(resolved ? "Next >" : "Skip >", GUILayout.Width(80f))) NextToDo();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Stop for now", GUILayout.Width(110f))) todo.Pause();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawShopClock(Rect rect)
        {
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color=#9fe39f><b>OPEN</b></color> {Clock(shopDay.TimeLeft)}", Rich());
            DrawDevControls();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawDevControls()
        {
            foreach (var speed in Speeds)
            {
                bool on = Mathf.Approximately(shopDay.SpeedMultiplier, speed);
                if (GUILayout.Toggle(on, $"{speed:0}x", GUI.skin.button, GUILayout.Width(36f)) && !on) shopDay.SetSpeed(speed);
            }
            if (GUILayout.Button("Skip day (K)", GUILayout.Width(110f))) shopDay.SkipRestOfDay();
        }

        /// <summary>Keyboard versions of the dev controls: 1/2/3 set 1x/5x/20x and K skips the rest of the day.</summary>
        void HandleDevKeys()
        {
            if (!shopDay.IsOpen) return;
            for (int i = 0; i < Speeds.Length && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) shopDay.SetSpeed(Speeds[i]);
            if (Input.GetKeyDown(KeyCode.K)) shopDay.SkipRestOfDay();
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        void DrawLog(float height)
        {
            var entries = GameLog.Entries;
            if (entries.Count == 0) return;
            var rect = new Rect(10f, height - 110f, 560f, 100f);
            GUILayout.BeginArea(rect, GUI.skin.box);
            for (int i = Mathf.Max(0, entries.Count - 4); i < entries.Count; i++)
                GUILayout.Label(entries[i]);
            GUILayout.EndArea();
        }

        void DrawWorldLabels(float scale, float height)
        {
            foreach (var tank in store.Tanks)
            {
                var t = tank.Tank;
                int eggs = EggClutch.EggsIn(t);
                string contents = $"{t.Residents.Count} fish" + (eggs > 0 ? $", {eggs} eggs" : "");
                string warning = t.Corpses.Count > 0 ? "\n<color=#ff8080>dead fish!</color>" : t.WaterHealth < Balance.Data.shop.grossWaterQuality ? "\n<color=#ffd27f>dirty water</color>" : "";
                WorldLabel(t.InnerBounds.center + Vector3.up * 0.65f, $"<b>{tank.Title}</b>\n{contents}{warning}", scale, height);
            }
            WorldLabel(store.RegisterPoint, "<b>Register</b>", scale, height);
            WorldLabel(store.ComputerPoint, "<b>Supplier computer</b>", scale, height);
            WorldLabel(store.BackRoomLabelPoint, "<b>Back room</b>\nstaff only: breeding and quarantine", scale, height);

            if (!shopDay.IsOpen)
                foreach (var slot in store.Slots)
                    if (slot.IsOpen && slot.tank == null)
                        WorldLabel(new Vector3(slot.position.x, 0.3f, slot.position.z), slot.backRoom ? "+ Breeder or quarantine tank" : "+ Tank spot", scale, height);

            foreach (var section in store.Sections)
                if (!section.unlocked)
                {
                    int level = StoreLayout.SectionLevel(section);
                    string requirement = Progression.IsUnlocked(XpTrack.Shopkeeping, level) ? $"Unlock for {Economy.Format(section.price)}" : $"Needs {Progression.LockText(XpTrack.Shopkeeping, level)}";
                    WorldLabel(section.LabelPoint, $"<b>{section.name}</b> (locked)\n{requirement}", scale, height);
                }
        }

        void WorldLabel(Vector3 world, string text, float scale, float height)
        {
            if (!ToGui(world, scale, height, out var gui)) return;
            var content = new GUIContent(text);
            var size = Label().CalcSize(content);
            GUI.Label(new Rect(gui.x - size.x * 0.5f, gui.y - size.y, size.x, size.y), content, Label());
        }

        bool ToGui(Vector3 world, float scale, float height, out Vector2 gui)
        {
            var screen = cam.WorldToScreenPoint(world);
            gui = new Vector2(screen.x / scale, height - screen.y / scale);
            return screen.z > 0f;
        }

        void DrawCustomerBubbles(float scale, float height)
        {
            foreach (var customer in shopDay.Customers)
            {
                if (customer == null || customer.Mood == Mood.None) continue;
                if (!ToGui(customer.HeadPoint, scale, height, out var gui)) continue;
                float pop = Mathf.Clamp01((3.5f - customer.MoodTimeLeft) * 6f);
                float size = 54f * Mathf.Lerp(0.5f, 1f, pop);
                var bubbleRect = new Rect(gui.x - size * 0.5f, gui.y - size, size, size);
                GUI.DrawTexture(bubbleRect, MoodIcons.Bubble);
                float face = size * 0.62f;
                var faceRect = new Rect(gui.x - face * 0.5f, bubbleRect.y + size * 0.1f, face, face);
                GUI.DrawTexture(faceRect, MoodIcons.Get(customer.Mood));
                if (customer.Mood == Mood.Mad)
                    GUI.Label(new Rect(faceRect.xMax - 8f, faceRect.y - 8f, 24f, 24f), "<color=#2e9e3e><size=18><b>$</b></size></color>", Rich());
            }
        }

        void DrawMoneyPopups(float scale, float height)
        {
            foreach (var popup in shopDay.Popups)
            {
                if (!ToGui(popup.position + Vector3.up * (0.3f + popup.age * 0.35f), scale, height, out var gui)) continue;
                float alpha = Mathf.Clamp01(2.2f - popup.age);
                var text = $"<size=22><b>+{Economy.Format(popup.amount)}</b></size>";
                var rect = new Rect(gui.x - 60f, gui.y - 20f, 120f, 30f);
                var old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, alpha * 0.6f);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, Centered());
                GUI.color = new Color(0.45f, 1f, 0.45f, alpha);
                GUI.Label(rect, text, Centered());
                GUI.color = old;
            }
        }

        // ---------------------------------------------------------------- windows

        void DrawRegister()
        {
            GUILayout.BeginArea(panelRect, UiStyles.Window);
            GUILayout.Label("<size=16><b>Register</b></size>", Rich());
            GUILayout.Label($"Balance: <b>{Economy.Format(Economy.Money)}</b>     Day {Economy.Day}     Reputation {Economy.Reputation * 100f:0}%", Rich());
            if (shopDay.IsOpen)
            {
                GUILayout.Label($"Open for another {Clock(shopDay.TimeLeft)}. About {shopDay.CustomersPerHour:0} customers an hour.");
                if (GUILayout.Button("Close early")) shopDay.Close();
            }
            else
            {
                GUILayout.Label($"Opening moves time on: eggs hatch, fish age and get hungry, water gets dirtier, and illness, breeding and deaths are rolled. The day lasts {Clock(Balance.Data.shop.dayLengthSeconds)}.");
                if (GUILayout.Button("<b>Open Shop</b>", Button()))
                {
                    panel = Panel.None;
                    shopDay.Open();
                }
            }

            GUILayout.Space(6f);
            GUILayout.Label("<b>Recent transactions</b>", Rich());
            var ledger = Economy.Ledger;
            if (ledger.Count == 0) GUILayout.Label("<i>Nothing yet.</i>", Rich());
            for (int i = ledger.Count - 1; i >= Mathf.Max(0, ledger.Count - 9); i--)
                GUILayout.Label(ledger[i]);

            GUILayout.FlexibleSpace();
            GUILayout.Label("<b>Developer</b>", Rich());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+$1,000 (dev)")) Economy.Earn(1000, "developer cash");
            if (shopDay.IsOpen && GUILayout.Button("Skip rest of day (dev)")) shopDay.SkipRestOfDay();
            GUILayout.EndHorizontal();
#if UNITY_EDITOR
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+500 Fishkeeping XP (dev)")) Progression.DevAward(XpTrack.Fishkeeping, 500);
            if (GUILayout.Button("+500 Shopkeeping XP (dev)")) Progression.DevAward(XpTrack.Shopkeeping, 500);
            GUILayout.EndHorizontal();
#endif
            if (GUILayout.Button("Close")) panel = Panel.None;
            GUILayout.EndArea();
        }

        void DrawDayReport()
        {
            var report = shopDay.LastReport;
            GUILayout.BeginArea(panelRect, UiStyles.Window);
            if (report == null)
            {
                panel = Panel.None;
                GUILayout.EndArea();
                return;
            }
            GUILayout.Label($"<size=16><b>Day {report.day} is over</b></size>", Rich());
            GUILayout.Label($"Visitors: {report.visitors}     Fish sold: {report.fishSold}     Earned: <b>{Economy.Format(report.earnings)}</b>", Rich());
            GUILayout.Label($"Reactions: {report.happy} happy, {report.grossed} grossed out, {report.sad} sad, {report.mad} angry about prices");
            string stars = new string('*', Mathf.FloorToInt(report.stars)) + (report.stars % 1f > 0f ? "1/2" : "");
            GUILayout.Label($"Reviews: <b>{stars}</b> ({report.stars:0.#} / 5)   Reputation {(report.reputationChange >= 0f ? "+" : "")}{report.reputationChange * 100f:0}%", Rich());
            GUILayout.Label(report.review);
            GUILayout.Space(6f);
            GUILayout.Label("<b>Overnight in the tanks</b>", Rich());
            GUILayout.Label(report.overnight);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back to work"))
            {
                shopDay.LastReport = null;
                panel = Panel.None;
                StartToDo();
            }
            GUILayout.EndArea();
        }

        void DrawSupplier()
        {
            GUILayout.BeginArea(panelRect, UiStyles.Window);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=16><b>Supplier</b></size>", Rich());
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Fishkeeping {Progression.Fishkeeping}   Shopkeeping {Progression.Shopkeeping}   Balance: <b>{Economy.Format(Economy.Money)}</b>", Rich());
            GUILayout.EndHorizontal();
            if (shopDay.IsOpen)
            {
                GUILayout.Label("The supplier only delivers while the shop is closed.");
                if (GUILayout.Button("Close")) panel = Panel.None;
                GUILayout.EndArea();
                return;
            }
            supplierTab = Mathf.Clamp(GUILayout.Toolbar(supplierTab, SupplierTabs), 0, SupplierTabs.Length - 1);
            GUILayout.Space(4f);

            if (supplierTab != FoodTab) DrawDeliverTo();

            supplierScroll = GUILayout.BeginScrollView(supplierScroll);
            switch (supplierTab)
            {
                case EggsTab: DrawEggOffers(); break;
                case DecorTab: DrawDecorationOffers(); break;
                case FoodTab: DrawFoodOffers(); break;
                case EquipmentTab: DrawEquipmentOffers(); break;
                default: DrawDevFryOffers(); break;
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button("Close")) panel = Panel.None;
            GUILayout.EndArea();
        }

        void DrawDeliverTo()
        {
            if (orderTarget == null && store.Tanks.Count > 0) orderTarget = store.Tanks[0];
            GUILayout.Label(supplierTab == EggsTab || supplierTab == DevFryTab ? "Deliver to:" : supplierTab == DecorTab ? "Put in:" : "Install in:");
            var tanks = store.Tanks;
            var names = new string[tanks.Count];
            int current = 0;
            for (int i = 0; i < tanks.Count; i++)
            {
                var tank = tanks[i].Tank;
                names[i] = supplierTab == EggsTab || supplierTab == DevFryTab ? $"{tanks[i].Title} ({FreeSpace(tanks[i])} free)"
                    : supplierTab == DecorTab ? $"{tanks[i].Title} ({tank.DecorCount}/{tank.Decor.Capacity})"
                    : tanks[i].Title;
                if (tanks[i] == orderTarget) current = i;
            }
            current = GUILayout.SelectionGrid(current, names, 3);
            if (tanks.Count > 0) orderTarget = tanks[current];
            GUILayout.Space(4f);
        }

        static int FreeSpace(StoreTank tank) => Mathf.Max(0, tank.Tank.FreeSpace - EggClutch.EggsIn(tank.Tank));

        /// <summary>
        /// One thing for sale: its picture (a photo of the 3D design, or a drawn filler), name and
        /// details, and a buy button, or what level unlocks it.
        /// </summary>
        bool OfferRow(Texture2D picture, string title, string details, XpTrack track, int level, string buyLabel, bool canBuy, float pictureSize = 72f)
        {
            bool unlocked = Progression.IsUnlocked(track, level);
            bool bought = false;
            GUILayout.BeginHorizontal(GUI.skin.box);
            var pictureRect = GUILayoutUtility.GetRect(pictureSize, pictureSize, GUILayout.Width(pictureSize), GUILayout.Height(pictureSize));
            var old = GUI.color;
            if (!unlocked) GUI.color = new Color(0.45f, 0.45f, 0.45f, 0.8f);
            GUI.DrawTexture(pictureRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.2f, 0.24f, 0.3f, 0.9f), 0f, 4f);
            if (picture != null) GUI.DrawTexture(pictureRect, picture, ScaleMode.ScaleToFit);
            GUI.color = old;
            GUILayout.BeginVertical();
            GUILayout.Label($"<b>{title}</b>", Rich());
            if (!string.IsNullOrEmpty(details)) GUILayout.Label($"<size=11>{details}</size>", Rich());
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(170f));
            GUILayout.FlexibleSpace();
            if (!unlocked) GUILayout.Label($"<color=#ffd27f>Needs {Progression.LockText(track, level)}</color>", Rich());
            else
            {
                GUI.enabled = canBuy;
                bought = GUILayout.Button(buyLabel, GUILayout.Height(34f));
                GUI.enabled = true;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            return bought && unlocked;
        }

        void DrawEggOffers()
        {
            GUILayout.Label("<i>Boxes of mixed eggs. They hatch at the next time step (when the shop opens or closes). Rarer boxes and a higher Fishkeeping level bring rarer fish; each extra rare fish in one box is harder to get than the last.</i>", Rich());
            int free = orderTarget != null ? FreeSpace(orderTarget) : 0;
            int level = Progression.Fishkeeping;
            foreach (var tier in EggBoxes.Tiers)
            {
                var normal = EggBoxes.Pool(tier.normalMin, tier.normalMax, level);
                var rare = EggBoxes.Pool(tier.rarePool, tier.rarePool, level);
                bool unlocked = Progression.IsUnlocked(XpTrack.Fishkeeping, tier.unlockLevel);
                bool anyFish = normal.Count + rare.Count > 0 || !unlocked;
                string details = tier.blurb;
                if (unlocked && normal.Count + rare.Count == 0) details += "\n<color=#ffd27f>No fish of this kind in the game yet.</color>";
                else if (unlocked)
                {
                    var odds = EggBoxes.AtLeastRareOdds(tier);
                    var oddsText = new List<string>();
                    for (int k = 0; k < odds.Length && k < 3; k++) oddsText.Add($"{k + 1}+ {Percent(odds[k])}");
                    details += $"\n{tier.eggs} eggs. Rare fish ({tier.rarePool}): " + (rare.Count > 0 ? string.Join(", ", oddsText) : "none unlocked yet");
                }
                string label = free >= tier.eggs ? $"Buy {tier.eggs} eggs  {Economy.Format(tier.price)}" : $"Needs {tier.eggs} free spaces";
                bool canBuy = anyFish && free >= tier.eggs && Economy.CanAfford(tier.price) && orderTarget != null;
                if (OfferRow(ItemPreviews.EggBox(tier.color, tier.speckled), tier.name, details, XpTrack.Fishkeeping, tier.unlockLevel, label, canBuy))
                    OrderEggs(tier);
                if (unlocked && normal.Count + rare.Count > 0) DrawPoolPictures(normal, rare);
            }
        }

        static string Percent(float chance) => chance < 0.01f ? "<1%" : $"{chance * 100f:0}%";

        /// <summary>Little pictures of the fish a box can hatch at the player's level (rare ones marked).</summary>
        void DrawPoolPictures(List<Species> normal, List<Species> rare)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(80f);
            GUILayout.Label("<size=11>Can hatch:</size>", Rich(), GUILayout.Width(62f));
            int shown = 0;
            foreach (var list in new[] { normal, rare })
                foreach (var species in list)
                {
                    if (shown++ >= 12) break;
                    var rect = GUILayoutUtility.GetRect(40f, 40f, GUILayout.Width(40f), GUILayout.Height(40f));
                    GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                        list == rare ? new Color(0.45f, 0.35f, 0.15f, 0.9f) : new Color(0.2f, 0.24f, 0.3f, 0.9f), 0f, 4f);
                    var photo = ItemPreviews.Fish(species);
                    if (photo != null) GUI.DrawTexture(rect, photo, ScaleMode.ScaleToFit);
                    GUI.Label(rect, new GUIContent("", $"{species.displayName} ({species.rarity})"));
                }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(GUI.tooltip)) GUILayout.Label($"<size=11>{GUI.tooltip}</size>", Rich());
        }

        void OrderEggs(EggTierBalance tier)
        {
            if (!Economy.TrySpend(tier.price, tier.name.ToLower())) return;
            var eggs = EggBoxes.Roll(tier, Progression.Fishkeeping);
            EggClutch.Lay(orderTarget.Tank, tier, eggs);
            GameLog.Add($"{tier.eggs} {tier.name.ToLower()} delivered to {orderTarget.Title}. They hatch at the next time step.");
        }

        void DrawDecorationOffers()
        {
            GUILayout.Label("<i>Decorations draw more customers and make fish comfortable (most like 3 to 10 per tank), but too many speed up algae. Plants count as plants for fish that love them.</i>", Rich());
            var tank = orderTarget != null ? orderTarget.Tank : null;
            foreach (var item in SupplierCatalog.Decorations)
            {
                int inTank = 0;
                if (tank != null) foreach (var id in tank.Decor.Ids) if (id == item.id) inTank++;
                string details = (item.plant ? "Plant. " : "") + (item.back ? "Goes along the back." : "Goes near the front.") + (inTank > 0 ? $"  {inTank} in this tank." : "");
                bool full = tank == null || tank.Decor.IsFull;
                string label = full ? "No room in this tank" : $"Add  {Economy.Format(item.price)}";
                if (OfferRow(ItemPreviews.Decoration(item.id), item.name, details, XpTrack.Shopkeeping, item.unlockLevel, label, !full && Economy.CanAfford(item.price))
                    && Economy.TrySpend(item.price, $"a {item.name.ToLower()} for {orderTarget.Title}"))
                {
                    if (tank.Decor.Add(item.id))
                    {
                        GameLog.Add($"Added a {item.name.ToLower()} to {orderTarget.Title}.");
                        Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpBuyDecoration);
                        Progression.Count(GoalKind.BuyDecorations);
                    }
                    else Economy.Earn(item.price, "refund: no room for a decoration");
                }
            }
        }

        void DrawFoodOffers()
        {
            foreach (var offer in SupplierCatalog.Food)
            {
                var tint = offer.type == FoodType.Premium ? new Color(0.9f, 0.5f, 0.2f) : new Color(0.65f, 0.5f, 0.3f);
                string details = $"{offer.blurb}\nYou have {Economy.FoodPortions(offer.type)} feedings.";
                if (OfferRow(ItemPreviews.Filler(ItemPreviews.Icon.Food, tint), offer.name, details, XpTrack.Fishkeeping, offer.unlockLevel,
                        $"{offer.feedings} feedings  {Economy.Format(offer.price)}", Economy.CanAfford(offer.price))
                    && Economy.TrySpend(offer.price, offer.name.ToLower()))
                {
                    Economy.AddFood(offer.type, offer.feedings);
                    GameLog.Add($"Bought {offer.name.ToLower()}: {offer.feedings} more feedings.");
                }
            }
        }

        void DrawEquipmentOffers()
        {
            var prices = SupplierCatalog.Equipment;
            var tank = orderTarget != null ? orderTarget.Tank : null;
            string where = orderTarget != null ? orderTarget.Title : "a tank";

            EquipmentRow(ItemPreviews.Icon.Filter, "Filter", "Cuts how fast waste dirties the water, so the tank needs wiping less often.", prices.filterPrice, prices.filterLevel, tank != null && tank.HasFilter, where, () => tank.InstallFilter());
            EquipmentRow(ItemPreviews.Icon.Heater, "Heater", $"Holds the water at a temperature you set in the tank view (tropical fish need 24 C or more). Room temperature is {GameConfig.Active.roomTemperature:0} C.", prices.heaterPrice, prices.heaterLevel, tank != null && tank.HasHeater, where, () => tank.InstallHeater());
            EquipmentRow(ItemPreviews.Icon.Bubbler, "Bubbler", $"Adds oxygen, so the tank can hold more than {GameConfig.Active.fishPerTankWithoutBubbler} fish comfortably.", prices.bubblerPrice, prices.bubblerLevel, tank != null && tank.HasBubbler, where, () => tank.InstallBubbler());

            string details = $"Resets the pH and gets rid of snails and worms. Use it from the tank view.\nYou have {Economy.WaterConditionerDoses} doses.";
            if (OfferRow(ItemPreviews.Filler(ItemPreviews.Icon.Conditioner, new Color(0.35f, 0.65f, 0.85f)), "Water conditioner", details, XpTrack.Fishkeeping, prices.waterConditionerLevel,
                    $"{prices.waterConditionerDoses} doses  {Economy.Format(prices.waterConditionerPrice)}", Economy.CanAfford(prices.waterConditionerPrice))
                && Economy.TrySpend(prices.waterConditionerPrice, "water conditioner"))
                Economy.WaterConditionerDoses += prices.waterConditionerDoses;
        }

        void EquipmentRow(ItemPreviews.Icon icon, string name, string blurb, int price, int level, bool installed, string where, System.Action install)
        {
            string label = installed ? "Already has one" : $"Install  {Economy.Format(price)}";
            if (OfferRow(ItemPreviews.Filler(icon, new Color(0.55f, 0.6f, 0.68f)), name, blurb, XpTrack.Fishkeeping, level, label, orderTarget != null && !installed && Economy.CanAfford(price))
                && Economy.TrySpend(price, $"a {name.ToLower()} for {where}"))
            {
                install();
                Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpBuyEquipment);
                GameLog.Add($"{name} installed in {where}.");
            }
        }

        /// <summary>Developer list: any species as a single fry, ignoring levels, for testing.</summary>
        void DrawDevFryOffers()
        {
            GUILayout.Label("<i>Developer only: buy any species as a fry, ignoring levels.</i>", Rich());
            int free = orderTarget != null ? FreeSpace(orderTarget) : 0;
            foreach (var species in Species.All)
            {
                string livebearer = species.reproduction == Reproduction.Livebearer ? ", livebearer" : "";
                string details = $"{species.rarity}, {Category(species.category)}{livebearer}, {species.tempMin:0}-{species.tempMax:0} C, Fishkeeping {species.unlockLevel}";
                if (OfferRow(ItemPreviews.Fish(species), species.displayName, details, XpTrack.Fishkeeping, 0, $"1 fry  {Economy.Format(species.fryPrice)}",
                        free >= 1 && Economy.CanAfford(species.fryPrice), 48f))
                    OrderFry(species);
            }
        }

        static string Category(FishCategory category)
        {
            switch (category)
            {
                case FishCategory.FreshwaterCold: return "cold fresh water";
                case FishCategory.FreshwaterTropical: return "tropical fresh water";
                case FishCategory.SaltwaterCold: return "cold salt water";
                case FishCategory.SaltwaterTropical: return "tropical salt water";
                case FishCategory.BottomFeeder: return "bottom feeder";
                default: return "shellfish";
            }
        }

        void OrderFry(Species species)
        {
            if (!Economy.TrySpend(species.fryPrice, $"a {species} fry")) return;
            var tank = orderTarget.Tank;
            var fish = FishFactory.Spawn(Genome.CreateRandom(species), 1, 0.05f, tank, tank.RandomFreePoint(0.1f));
            GameLog.Add($"{fish.FishName} the {species} fry arrived in {orderTarget.Title}.");
        }

        void DrawUnlock()
        {
            var section = pendingSection;
            GUILayout.BeginArea(panelRect, UiStyles.Window);
            GUILayout.Label($"<size=16><b>Unlock the {section.name}?</b></size>", Rich());
            GUILayout.Label("Opens up room for 6 more display or for sale tanks.");
            var next = store.NextLockedSection;
            int level = StoreLayout.SectionLevel(section);
            bool levelOk = Progression.IsUnlocked(XpTrack.Shopkeeping, level);
            if (shopDay.IsOpen) GUILayout.Label("<i>Building work has to wait until the shop is closed.</i>", Rich());
            else if (next != section) GUILayout.Label($"<i>Unlock the {next.name} first.</i>", Rich());
            else if (!levelOk) GUILayout.Label($"<i>Needs {Progression.LockText(XpTrack.Shopkeeping, level)}.</i>", Rich());
            else if (!Economy.CanAfford(section.price)) GUILayout.Label($"<i>You need {Economy.Format(section.price - Economy.Money)} more.</i>", Rich());
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUI.enabled = !shopDay.IsOpen && next == section && levelOk && Economy.CanAfford(section.price);
            if (GUILayout.Button($"Unlock for {Economy.Format(section.price)}") && store.TryUnlock(section)) panel = Panel.None;
            GUI.enabled = true;
            if (GUILayout.Button("Cancel")) panel = Panel.None;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static readonly TankRole[] BuyableRoles = { TankRole.ForSale, TankRole.Display, TankRole.Breeder, TankRole.Quarantine };

        void DrawBuyTank()
        {
            GUILayout.BeginArea(panelRect, UiStyles.Window);
            GUILayout.Label(pendingSlot.backRoom ? "<size=16><b>Buy a tank for the back room</b></size>" : "<size=16><b>Buy a tank for this spot</b></size>", Rich());
            GUILayout.Label(pendingSlot.backRoom
                ? "<i>Customers never come back here: it's for breeding and for keeping sick fish apart.</i>"
                : "<i>Customers browse these. Breeder and quarantine tanks go in the back room.</i>", Rich());
            foreach (var role in BuyableRoles)
            {
                if (!pendingSlot.Allows(role)) continue;
                SupplierCatalog.TankLevel(role, out var track, out var level);
                GUI.enabled = !shopDay.IsOpen && store.CanBuyTank(pendingSlot, role, out _);
                string text = Progression.IsUnlocked(track, level)
                    ? $"{StoreTank.RoleName(role)} tank   {Economy.Format(SupplierCatalog.TankPrice(role))}   ({store.CountTanks(role)}/{SupplierCatalog.TankLimit(role)} owned)"
                    : $"{StoreTank.RoleName(role)} tank: needs {Progression.LockText(track, level)}";
                if (GUILayout.Button(text, GUILayout.Height(30f)) && store.TryBuyTank(pendingSlot, role) != null) panel = Panel.None;
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel")) panel = Panel.None;
            GUILayout.EndArea();
        }

        void HandleClick()
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0) return;
            if (topBar.Contains(e.mousePosition) || statusBar.Contains(e.mousePosition)) return;
            if (todo.Count > 0 && todoBar.Contains(e.mousePosition)) return;
            if (panel != Panel.None)
            {
                // Clicking outside a window closes it (except the day report, which needs its button).
                if (!panelRect.Contains(e.mousePosition) && panel != Panel.DayReport) panel = Panel.None;
                e.Use();
                return;
            }

            var screenPoint = GUI.matrix.MultiplyPoint3x4(e.mousePosition);
            var ray = cam.ScreenPointToRay(new Vector3(screenPoint.x, Screen.height - screenPoint.y, 0f));
            if (!Physics.Raycast(ray, out var hit, 100f, StoreClickable.Mask, QueryTriggerInteraction.Collide)) return;
            var clickable = hit.collider.GetComponent<StoreClickable>();
            if (clickable == null) return;
            e.Use();

            switch (clickable.kind)
            {
                case StoreClickable.Kind.Tank: EnterTank(clickable.tank); break;
                case StoreClickable.Kind.Register: panel = Panel.Register; break;
                case StoreClickable.Kind.Computer: panel = Panel.Supplier; break;
                case StoreClickable.Kind.EmptySlot: pendingSlot = clickable.slot; panel = Panel.BuyTank; break;
                case StoreClickable.Kind.LockedSection: pendingSection = clickable.section; panel = Panel.Unlock; break;
            }
        }

        static GUIStyle rich, label, button, centered, banner;

        static GUIStyle Banner()
        {
            if (banner == null)
            {
                banner = new GUIStyle(UiStyles.Window) { richText = true, alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 14 };
                banner.normal.textColor = Color.white;
                banner.padding = new RectOffset(12, 12, 8, 8);
            }
            return banner;
        }

        static GUIStyle Rich()
        {
            if (rich == null) rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            return rich;
        }

        static GUIStyle Label()
        {
            if (label == null) label = new GUIStyle(GUI.skin.box) { richText = true, alignment = TextAnchor.MiddleCenter };
            return label;
        }

        static GUIStyle Button()
        {
            if (button == null) button = new GUIStyle(GUI.skin.button) { richText = true };
            return button;
        }

        static GUIStyle Centered()
        {
            if (centered == null) centered = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter };
            return centered;
        }
    }
}
