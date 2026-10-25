using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.People;
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
    /// the player through every problem in the tanks one at a time. Each day the player either opens
    /// the shop or keeps it closed (the only choice during the first setup days). Milestones show
    /// down the right side, and V hides everything for watch mode.
    /// Placeholder IMGUI like the tank HUD, in the ocean theme (see <see cref="UiTheme"/>).
    /// </summary>
    public class StoreView : MonoBehaviour
    {
        enum Panel { None, Register, Supplier, Unlock, BuyTank, DayReport, Owner }

        static readonly Vector3 StoreViewDirection = new Vector3(0f, 0.62f, -0.78f).normalized;
        static readonly Color StoreBackground = new Color(0.42f, 0.78f, 0.86f);
#if UNITY_EDITOR
        static readonly string[] SupplierTabs = { "Eggs", "Fry", "Decorations", "Food", "Equipment and care", "Any fish (dev)" };
#else
        static readonly string[] SupplierTabs = { "Eggs", "Fry", "Decorations", "Food", "Equipment and care" };
#endif
        const int EggsTab = 0, FryTab = 1, DecorTab = 2, FoodTab = 3, EquipmentTab = 4, DevFryTab = 5;
        // The fish seller sells the fish; the supplier computer everything else.
#if UNITY_EDITOR
        static readonly int[] FishSellerTabs = { EggsTab, FryTab, DevFryTab };
#else
        static readonly int[] FishSellerTabs = { EggsTab, FryTab };
#endif
        static readonly int[] ComputerTabs = { DecorTab, FoodTab, EquipmentTab };
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
        bool atFishSeller;
        /// <summary>Who the people camera (C) is following: -1 off, 0 the shopkeeper, 1 the fish seller, then customers.</summary>
        int following = -1;
        Vector2 supplierScroll;
        float panX;
        float distance = 15f;
        Rect panelRect, topBar, statusBar, todoBar, milestoneRect, watchButton;
        /// <summary>Height the to-do bar needed last frame (its hint text wraps on narrow screens).</summary>
        float todoHeight = 110f;
        readonly ToDoList todo = new ToDoList();
        float todoFinishedTime = -1f;
        int lastDay = 1;
        float shownMoney, moneyFlash;
        int lastMoney;
        float milestoneTimer;
        Vector3 lastMouse;
        float mouseMovedAt;
        int levelUpsSeen;

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
            lastDay = Economy.Day;
            shownMoney = lastMoney = Economy.Money;
            if (!ShopOwner.Chosen) panel = Panel.Owner;
        }

        void Update()
        {
            if (store == null || cam == null) return;
            float dt = Time.unscaledDeltaTime;

            HandleDevKeys();
            FishSeller.ShopOpen = shopDay.IsOpen;
            foreach (var levelUp in Progression.LevelUps) levelUp.age += dt;
            Progression.LevelUps.RemoveAll(l => l.age > 6f);
            UpdateMoney(dt);
            UpdateDay();
            milestoneTimer -= dt;
            if (milestoneTimer <= 0f)
            {
                milestoneTimer = 0.5f;
                var tanks = new List<Tank>();
                foreach (var storeTank in store.Tanks) tanks.Add(storeTank.Tank);
                Milestones.Tick(tanks);
            }
            if (Input.mousePosition != lastMouse)
            {
                lastMouse = Input.mousePosition;
                mouseMovedAt = Time.unscaledTime;
            }

            if (shopDay.LastReport != null && panel != Panel.DayReport)
            {
                if (focused != null) ExitTank();
                todo.Dismiss();
                panel = Panel.DayReport;
            }
            UpdateToDo(dt);
            Fx.Hold = panel == Panel.DayReport;

            Vector3 targetPosition;
            Quaternion targetRotation;
            // Esc steps back one level: from a fish to its tank, from the tank to the store.
            bool escape = Input.GetKeyDown(KeyCode.Escape);
            if (Input.GetKeyDown(KeyCode.V) && GUIUtility.keyboardControl == 0 && panel != Panel.DayReport) ToggleWatch();
            if (escape && ViewMode.Watching)
            {
                ViewMode.Set(false);
                escape = false;
            }
            if (escape && JournalView.IsOpen)
            {
                JournalView.Close();
                escape = false;
            }
            if (escape && PerksView.IsOpen)
            {
                PerksView.Close();
                escape = false;
            }
            if (focused != null && focused.Tank == null) ExitTank();
            else if (focused != null && escape)
            {
                if (hud.HasSelection) hud.ClearSelection();
                else ExitTank();
            }
            else if (focused == null && escape && following >= 0) following = -1;
            else if (focused == null && escape && panel != Panel.DayReport && panel != Panel.Owner) panel = Panel.None;
            if (focused == null && panel == Panel.None && Input.GetKeyDown(KeyCode.C) && GUIUtility.keyboardControl == 0) following++;
            var person = focused == null ? FollowedPerson() : null;
            if (focused != null)
            {
                GetTankPose(focused, hud.Focus, out targetPosition, out targetRotation);
            }
            else if (person != null)
            {
                // Close up on one of the shop's people, from in front and a little to the side.
                var forward = person.forward;
                forward.y = 0f;
                forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.back;
                var side = Vector3.Cross(Vector3.up, forward);
                targetPosition = person.position + Vector3.up * 2f + forward * 2.6f + side * 0.8f;
                targetRotation = Quaternion.LookRotation(person.position + Vector3.up * 0.8f - targetPosition);
            }
            else
            {
                if (panel == Panel.None && !JournalView.IsOpen && !PerksView.IsOpen) HandleStoreCameraInput(dt);
                GetStorePose(out targetPosition, out targetRotation);
            }
            if (ViewMode.Watching)
            {
                // A slow, gentle drift so watching feels alive.
                float t = Time.unscaledTime;
                float reach = focused != null ? 0.06f : 0.35f;
                targetPosition += targetRotation * new Vector3(Mathf.Sin(t * 0.13f) * reach * 2f, Mathf.Sin(t * 0.21f) * reach, Mathf.Sin(t * 0.09f) * reach);
            }

            float blend = 1f - Mathf.Exp(-6f * dt);
            cam.transform.SetPositionAndRotation(
                Vector3.Lerp(cam.transform.position, targetPosition, blend),
                Quaternion.Slerp(cam.transform.rotation, targetRotation, blend));
        }

        /// <summary>The person the people camera follows, or null (stepping past the last one turns it off).</summary>
        Transform FollowedPerson()
        {
            if (following < 0) return null;
            var people = new List<Transform>();
            if (Shopkeeper.Instance != null) people.Add(Shopkeeper.Instance.transform);
            if (FishSeller.Instance != null) people.Add(FishSeller.Instance.transform);
            foreach (var customer in Customer.All) if (customer != null) people.Add(customer.transform);
            if (following >= people.Count)
            {
                following = -1;
                return null;
            }
            return people[following];
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
            hud.SetTankNavigation(SwitchTank, NeighbourTank);
            hud.enabled = true;
        }

        /// <summary>Tanks in walking order: back room first, then the shop floor west to east, back row before front.</summary>
        List<StoreTank> TanksInOrder()
        {
            var list = new List<StoreTank>(store.Tanks);
            list.Sort((a, b) =>
            {
                var pa = a.transform.position;
                var pb = b.transform.position;
                if (a.InBackRoom != b.InBackRoom) return a.InBackRoom ? -1 : 1;
                int bySection = Mathf.FloorToInt((pa.x + 6f) / StoreLayout.SectionWidth).CompareTo(Mathf.FloorToInt((pb.x + 6f) / StoreLayout.SectionWidth));
                if (bySection != 0) return bySection;
                if (!Mathf.Approximately(pa.z, pb.z)) return pb.z.CompareTo(pa.z);
                return pa.x.CompareTo(pb.x);
            });
            return list;
        }

        /// <summary>The tank before (-1) or after (+1) the one being looked at, wrapping round.</summary>
        StoreTank NeighbourTank(int direction)
        {
            if (focused == null) return null;
            var list = TanksInOrder();
            if (list.Count < 2) return null;
            int index = list.IndexOf(focused);
            return list[((index + direction) % list.Count + list.Count) % list.Count];
        }

        void SwitchTank(int direction)
        {
            var next = NeighbourTank(direction);
            if (next != null) EnterTank(next);
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
            UiTheme.Apply();
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            if (ViewMode.Watching)
            {
                DrawWatchHint(width, height);
                return;
            }

            if (focused != null)
            {
                // In the tank view the tank HUD owns the screen; just show the shop clock if open,
                // the to-do bar and level-ups.
                if (shopDay.IsOpen) DrawShopClock(new Rect(width - 330f, 10f, 320f, 38f));
                todoBar = new Rect(Mathf.Max(300f, width * 0.5f - 280f), 10f, Mathf.Max(420f, Mathf.Min(560f, width - 650f)), todoHeight);
                DrawToDoBar();
                DrawLevelUps(width, 140f);
                return;
            }

            topBar = new Rect(10f, 8f, width - 20f, 42f);
            statusBar = new Rect(10f, 54f, width - 20f, 32f);
            DrawTopBar();
            DrawStatusBar();
            if (panel == Panel.None)
            {
                if (following < 0) DrawWorldLabels(scale, height);
                DrawCustomerBubbles(scale, height);
            }
            DrawMoneyPopups(scale, height);
            DrawLog(height);
            todoBar = new Rect(width * 0.5f - 280f, 94f, 560f, todoHeight);
            milestoneRect = Rect.zero;
            if (panel == Panel.None)
            {
                DrawToDoBar();
                DrawMilestones(width);
            }
            DrawLevelUps(width, 200f);

            switch (panel)
            {
                case Panel.Register: panelRect = Centered(width, height, 500f, 560f); DrawRegister(); break;
                case Panel.Supplier: panelRect = Centered(width, height, 760f, Mathf.Min(700f, height - 100f)); DrawSupplier(); break;
                case Panel.Unlock: panelRect = Centered(width, height, 380f, 170f); DrawUnlock(); break;
                case Panel.BuyTank: panelRect = Centered(width, height, 440f, 250f); DrawBuyTank(); break;
                case Panel.DayReport: panelRect = Centered(width, height, 460f, 360f); DrawDayReport(); break;
                case Panel.Owner: panelRect = Centered(width, height, 480f, 400f); DrawOwnerPick(); break;
            }

            HandleClick();
        }

        static Rect Centered(float width, float height, float w, float h) => new Rect((width - w) * 0.5f, (height - h) * 0.5f, w, h);

        void DrawTopBar()
        {
            GUILayout.BeginArea(topBar, UiTheme.Bar);
            GUILayout.BeginHorizontal();
            // Money rolls up or down to its new value, flashing green or coral.
            string moneyColor = moneyFlash > 0.01f ? ColorHex(Color.Lerp(UiTheme.Sand, UiTheme.Leaf, moneyFlash))
                : moneyFlash < -0.01f ? ColorHex(Color.Lerp(UiTheme.Sand, UiTheme.Coral, -moneyFlash)) : UiTheme.SandHex;
            GUILayout.Label($"<size=17><b><color={moneyColor}>{Economy.Format(Mathf.RoundToInt(shownMoney))}</color></b></size>", Rich(), GUILayout.Width(96f));
            if (Event.current.type == EventType.Repaint && coinBurst)
            {
                coinBurst = false;
                var last = GUILayoutUtility.GetLastRect();
                Fx.ScreenBurst(new Vector2(topBar.x + last.x + 40f, topBar.y + last.center.y), UiTheme.Sun, 14, false, 0.8f);
            }
            GUILayout.Label($"<b>Day {Economy.Day}</b>", Rich(), GUILayout.Width(58f));
            GUILayout.Label($"<color={UiTheme.SunHex}>{Stars(Economy.Reputation * 5f)}</color>", Rich(), GUILayout.Width(78f));
            GUILayout.Label(FoodSummary(), Rich(), GUILayout.Width(118f));
            if (shopDay.IsOpen)
            {
                var today = shopDay.Today;
                GUILayout.Label($"<color={UiTheme.LeafHex}><b>OPEN</b></color> {Clock(shopDay.TimeLeft)}   Visitors {today.visitors}   Sold {today.fishSold}   +{Economy.Format(today.earnings)}", Rich());
                GUILayout.FlexibleSpace();
                DrawDevControls();
            }
            else
            {
                int setup = Economy.DaysUntilOpening;
                GUILayout.Label(setup > 0
                    ? $"<color={UiTheme.SunHex}><b>Grand opening in {setup} day{(setup == 1 ? "" : "s")}</b></color>"
                    : $"<color={UiTheme.CoralHex}><b>CLOSED</b></color>  <size=12>tank work and ordering</size>", Rich());
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Journal (J)", GUILayout.Width(92f))) JournalView.Toggle();
                if (GUILayout.Button(PerksButtonLabel(), PerkButtonStyle(), GUILayout.Width(Perks.TotalAvailable > 0 ? 104f : 84f))) PerksView.Toggle();
                if (GUILayout.Button("Watch (V)", GUILayout.Width(84f))) ToggleWatch();
                if (setup > 0)
                {
                    if (GUILayout.Button(new GUIContent("End the day", "Time passes: fish grow, eggs hatch, water gets dirtier."), UiTheme.Primary, GUILayout.Width(118f))) shopDay.StayClosed();
                }
                else
                {
                    if (GUILayout.Button(new GUIContent("Stay closed", "Let the day pass without opening: no customers, no sales."), GUILayout.Width(100f))) shopDay.StayClosed();
                    if (GUILayout.Button("Open Shop", UiTheme.Primary, GUILayout.Width(110f))) shopDay.Open();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static string PerksButtonLabel() => Perks.TotalAvailable > 0 ? $"Perks (P) <color={UiTheme.SunHex}>●{Perks.TotalAvailable}</color>" : "Perks (P)";

        static GUIStyle perkButton;
        static GUIStyle PerkButtonStyle() => perkButton != null ? perkButton : perkButton = new GUIStyle(GUI.skin.button) { richText = true };

        static string Stars(float stars)
        {
            int full = Mathf.Clamp(Mathf.RoundToInt(stars), 0, 5);
            return new string('★', full) + "<color=#5f8a92>" + new string('★', 5 - full) + "</color>";
        }

        static string ColorHex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

        /// <summary>The three levels with their XP bars, and today's goals.</summary>
        void DrawStatusBar()
        {
            GUILayout.BeginArea(statusBar, UiTheme.Bar);
            GUILayout.BeginHorizontal();
            LevelMeter(XpTrack.Fishkeeping);
            GUILayout.Space(10f);
            LevelMeter(XpTrack.Shopkeeping);
            GUILayout.Space(10f);
            LevelMeter(XpTrack.Research);
            GUILayout.Space(10f);
            if (Perks.TotalAvailable > 0)
            {
                // A pulsing reminder while level-up points wait to be spent.
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                var old = GUI.color;
                GUI.color = Color.Lerp(Color.white, UiTheme.Sun, pulse);
                if (GUILayout.Button($"★ {Perks.TotalAvailable} perk point{(Perks.TotalAvailable == 1 ? "" : "s")}!", UiTheme.Primary, GUILayout.Width(132f))) PerksView.Open();
                GUI.color = old;
                GUILayout.Space(10f);
            }
            var goals = new List<string>();
            foreach (var goal in Progression.Goals)
                goals.Add(goal.Done ? $"<color={UiTheme.LeafHex}>{goal.Describe()} ✔</color>" : $"{goal.Describe()} <b>{goal.progress}/{goal.target}</b>");
            GUILayout.Label($"<b><color={UiTheme.SunHex}>Today:</color></b> " + (goals.Count > 0 ? string.Join("   ", goals) : "no goals"), Rich());
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static void LevelMeter(XpTrack track)
        {
            int level = Progression.Level(track);
            GUILayout.Label($"<b>{Progression.TrackName(track)} {level}</b>", Rich(), GUILayout.Width(104f));
            var rect = GUILayoutUtility.GetRect(84f, 12f, GUILayout.Width(84f));
            rect.y += 6f;
            rect.height = 10f;
            float fill = Progression.IsMaxLevel(track) ? 1f : Progression.Xp(track) / (float)Progression.XpToNext(level);
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0.05f, 0.1f, 0.6f), 0f, 5f);
            var color = track == XpTrack.Fishkeeping ? UiTheme.Aqua : track == XpTrack.Shopkeeping ? UiTheme.Sun : UiTheme.Hibiscus;
            if (fill > 0.001f)
                GUI.DrawTexture(new Rect(rect.x, rect.y, Mathf.Max(10f, rect.width * Mathf.Clamp01(fill)), rect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 5f);
            GUILayout.Label(Progression.IsMaxLevel(track) ? "<size=11>max</size>" : $"<size=11>{Progression.Xp(track)}/{Progression.XpToNext(level)}</size>", Rich(), GUILayout.Width(58f));
        }

        /// <summary>The next few milestones down the right side, with progress and what each pays.</summary>
        void DrawMilestones(float width)
        {
            var upcoming = Milestones.Upcoming(3);
            if (upcoming.Count == 0) return;
            float h = 38f + upcoming.Count * 58f;
            milestoneRect = new Rect(width - 290f, 94f, 280f, h);
            GUILayout.BeginArea(milestoneRect, UiStyles.Window);
            GUILayout.BeginHorizontal();
            GUILayout.Label("MILESTONES", UiTheme.Heading);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<size=11>{Milestones.DoneCount}/{Milestones.Total}</size>", Rich());
            GUILayout.EndHorizontal();
            foreach (var milestone in upcoming)
            {
                int have = Mathf.Min(Milestones.Progress(milestone.goal), milestone.target);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{milestone.title}</b>", Rich());
                GUILayout.FlexibleSpace();
                GUILayout.Label($"<color={UiTheme.LeafHex}><b>+{Economy.Format(milestone.reward)}</b></color>", Rich());
                GUILayout.EndHorizontal();
                GUILayout.Label($"<size=11>{milestone.Describe()}</size>", Rich());
                var bar = GUILayoutUtility.GetRect(200f, 7f, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0.05f, 0.1f, 0.6f), 0f, 3f);
                float fill = have / (float)Mathf.Max(1, milestone.target);
                if (fill > 0f) GUI.DrawTexture(new Rect(bar.x, bar.y, Mathf.Max(7f, bar.width * fill), bar.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, UiTheme.Sun, 0f, 3f);
                GUILayout.Space(2f);
            }
            GUILayout.EndArea();
        }

        void ToggleWatch()
        {
            ViewMode.Toggle();
            if (ViewMode.Watching && panel != Panel.DayReport) panel = Panel.None;
        }

        /// <summary>In watch mode: just a hint at first, and a small button to bring the menus back when the mouse moves.</summary>
        void DrawWatchHint(float width, float height)
        {
            float since = Time.unscaledTime - ViewMode.Since;
            if (since < 3.5f)
            {
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(3.5f - since));
                var hint = new Rect(width * 0.5f - 230f, height - 60f, 460f, 36f);
                GUI.Box(hint, "Watch mode. Press <b>V</b> or <b>Esc</b> to bring the menus back.", WorldLabelStyle());
                GUI.color = old;
            }
            if (Time.unscaledTime - mouseMovedAt < 2.5f || since < 3.5f)
            {
                watchButton = new Rect(width - 150f, 12f, 138f, 32f);
                if (GUI.Button(watchButton, "Show menus (V)")) ViewMode.Set(false);
            }
        }

        void UpdateMoney(float dt)
        {
            int money = Economy.Money;
            if (money != lastMoney)
            {
                moneyFlash = money > lastMoney ? 1f : -1f;
                if (money > lastMoney) coinBurst = true;
                lastMoney = money;
            }
            float gap = Mathf.Abs(money - shownMoney);
            shownMoney = Mathf.MoveTowards(shownMoney, money, Mathf.Max(40f, gap * 3f) * dt);
            moneyFlash = Mathf.MoveTowards(moneyFlash, 0f, dt * 0.8f);
        }

        /// <summary>When the calendar moves on, the screen washes over with the new day.</summary>
        void UpdateDay()
        {
            if (Economy.Day == lastDay) return;
            lastDay = Economy.Day;
            int setup = Economy.DaysUntilOpening;
            string subtitle = setup > 1 ? $"{setup} days until the grand opening"
                : setup == 1 ? "Tomorrow is the grand opening!"
                : Economy.Day == Balance.Data.shop.setupDays + 1 ? "Grand opening day! Open the shop when you're ready."
                : "A new day at the shop";
            Fx.Day($"Day {Economy.Day}", subtitle);
        }

        /// <summary>"Food 40": feedings in stock across every food, red when you've run out.</summary>
        static string FoodSummary()
        {
            int total = 0;
            foreach (FoodType food in System.Enum.GetValues(typeof(FoodType))) total += Economy.FoodPortions(food);
            return total > 0 ? $"Food: {total} feedings" : "<color=#ff8080>Food: none!</color>";
        }

        void DrawLevelUps(float width, float y)
        {
            foreach (var levelUp in Progression.LevelUps)
            {
                float alpha = Mathf.Clamp01(6f - levelUp.age) * Mathf.Clamp01(levelUp.age * 4f);
                float drop = (1f - Mathf.Clamp01(levelUp.age * 3f)) * -40f;
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                string unlocks = levelUp.unlocks.Count > 0 ? $"\n<color={UiTheme.AquaHex}>Unlocked:</color> " + string.Join(", ", levelUp.unlocks) : "";
                var content = new GUIContent($"<size=24><b><color={UiTheme.SunHex}>★ {Progression.TrackName(levelUp.track)} level {levelUp.level}! ★</color></b></size>{unlocks}");
                float w = Mathf.Min(620f, width - 40f);
                float h = Banner().CalcHeight(content, w);
                var rect = new Rect((width - w) * 0.5f, y + drop, w, h);
                GUI.Box(rect, content, Banner());
                GUI.color = old;
                if (celebratedLevelUps.Add(levelUp)) Fx.ScreenBurst(rect.center, null, 40, false, 1.4f);
                y += h + 6f;
            }
            celebratedLevelUps.RemoveWhere(l => !Progression.LevelUps.Contains(l));
        }

        readonly HashSet<LevelUp> celebratedLevelUps = new HashSet<LevelUp>();
        bool coinBurst;

        // ---------------------------------------------------------------- to-do list after closing

        void StartToDo()
        {
            if (todo.Build(store.Tanks) == 0)
            {
                GameLog.Add("Every tank looks fine. Nothing urgent to do.");
                return;
            }
            GameLog.Add(todo.Count == 1 ? "One thing needs seeing to after closing." : $"{todo.Count} things need seeing to after closing.");
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
                    GUILayout.Label(todo.Remaining == 1 ? "One thing still needs seeing to." : $"{todo.Remaining} things still need seeing to.", Rich());
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

            bool resolved = item.Resolved;
            string body = resolved ? $"<color=#9fe39f>{item.title}</color>" : $"<b>{item.title}</b> {item.hint}";
            // Header, wrapped text and the button row, plus the window's padding.
            var window = UiStyles.Window;
            float inner = todoBar.width - window.padding.horizontal;
            todoHeight = window.padding.vertical + 26f + Rich().CalcHeight(new GUIContent(body), inner) + 40f;
            todoBar.height = todoHeight;
            GUILayout.BeginArea(todoBar, window);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>To do {todo.Index + 1} of {todo.Count}</b>   {item.tank.Title}", Rich());
            GUILayout.FlexibleSpace();
            if (resolved) GUILayout.Label("<color=#9fe39f><b>Done!</b></color>", Rich());
            GUILayout.EndHorizontal();
            GUILayout.Label(body, Rich());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Back", GUILayout.Width(74f)) && todo.Previous()) GoToCurrent();
            if (GUILayout.Button("Take me there", GUILayout.Width(120f))) GoToCurrent();
            if (GUILayout.Button(resolved ? "Next >" : "Skip >", GUILayout.Width(74f))) NextToDo();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Stop for now", GUILayout.Width(110f))) todo.Pause();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawShopClock(Rect rect)
        {
            GUILayout.BeginArea(rect, UiTheme.Bar);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color={UiTheme.LeafHex}><b>OPEN</b></color> {Clock(shopDay.TimeLeft)}", Rich());
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
            var rect = new Rect(10f, height - 112f, 560f, 102f);
            GUILayout.BeginArea(rect, UiTheme.Bar);
            for (int i = Mathf.Max(0, entries.Count - 4); i < entries.Count; i++)
                GUILayout.Label(i == entries.Count - 1 ? $"<b>{entries[i]}</b>" : $"<color=#bfe3e3>{entries[i]}</color>", Rich());
            GUILayout.EndArea();
        }

        void DrawWorldLabels(float scale, float height)
        {
            foreach (var tank in store.Tanks)
            {
                var t = tank.Tank;
                int eggs = EggClutch.EggsIn(t);
                string count = $"{t.Residents.Count}/{t.Capacity} fish";
                string contents = (t.IsOvercrowded ? $"<color=#ff8080>{count}</color>" : count) + (eggs > 0 ? $", {eggs} eggs" : "");
                int sick = 0;
                foreach (var fish in t.Residents) if (!fish.IsDead && fish.Health.IsSick) sick++;
                string sickLine = sick > 0 ? $"\n<color=#ff6b6b><b>+ {sick} sick</b></color>" : "";
                string warning = sickLine + (t.IsOvercrowded ? "\n<color=#ff8080>overcrowded!</color>" : t.Corpses.Count > 0 ? "\n<color=#ff8080>dead fish!</color>" : t.WaterHealth < Balance.Data.shop.grossWaterQuality ? "\n<color=#ffd27f>dirty water</color>" : "");
                WorldLabel(t.InnerBounds.center + Vector3.up * 0.65f, $"<b>{tank.Title}</b>\n{contents}{warning}", scale, height);
            }
            WorldLabel(store.RegisterPoint, "<b>Register</b>", scale, height);
            WorldLabel(store.ComputerPoint, "<b>Supplier computer</b>\nfood, decorations, equipment", scale, height);
            WorldLabel(store.FishSellerPoint, "<b>Fish seller</b>\negg and fry boxes", scale, height);
            WorldLabel(store.BackRoomLabelPoint, "<b>Back room</b>\nstaff only: breeding and quarantine", scale, height);

            if (!shopDay.IsOpen)
                foreach (var slot in store.Slots)
                    if (slot.IsOpen && slot.tank == null)
                        WorldLabel(new Vector3(slot.position.x, 0.3f, slot.position.z), slot.backRoom ? "+ Breeder or quarantine tank" : "+ Tank spot", scale, height);

            foreach (var section in store.Sections)
                if (!section.unlocked)
                {
                    int level = StoreLayout.SectionLevel(section);
                    string requirement = Progression.IsUnlocked(XpTrack.Shopkeeping, level) ? $"Unlock for {Economy.Format(section.Price)}" : $"Needs {Progression.LockText(XpTrack.Shopkeeping, level)}";
                    WorldLabel(section.LabelPoint, $"<b>{section.name}</b> (locked)\n{requirement}", scale, height);
                }
        }

        void WorldLabel(Vector3 world, string text, float scale, float height)
        {
            if (!ToGui(world, scale, height, out var gui)) return;
            var content = new GUIContent(text);
            var size = WorldLabelStyle().CalcSize(content);
            GUI.Label(new Rect(gui.x - size.x * 0.5f, gui.y - size.y, size.x, size.y), content, WorldLabelStyle());
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
                float bounce = 1f + 0.6f * Mathf.Clamp01(1f - popup.age * 5f);
                var text = $"<size={Mathf.RoundToInt(24 * bounce)}><b>+{Economy.Format(popup.amount)}</b></size>";
                var rect = new Rect(gui.x - 60f, gui.y - 20f, 120f, 30f);
                var old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, alpha * 0.6f);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, Centered());
                GUI.color = new Color(UiTheme.Sun.r, UiTheme.Sun.g, UiTheme.Sun.b, alpha);
                GUI.Label(rect, text, Centered());
                GUI.color = old;
            }
        }

        // ---------------------------------------------------------------- windows

        void DrawRegister()
        {
            GUILayout.BeginArea(panelRect, UiStyles.Window);
            GUILayout.Label("Register", UiTheme.Title);
            GUILayout.Label($"Balance: <b>{Economy.Format(Economy.Money)}</b>     Day {Economy.Day}     Reputation {Economy.Reputation * 100f:0}%", Rich());
            if (shopDay.IsOpen)
            {
                GUILayout.Label($"Open for another {Clock(shopDay.TimeLeft)}. About {shopDay.CustomersPerHour:0} customers an hour.");
                if (GUILayout.Button("Close early")) shopDay.Close();
            }
            else if (!Economy.CanOpenShop)
            {
                int setup = Economy.DaysUntilOpening;
                GUILayout.Label($"The grand opening is in <b>{setup} day{(setup == 1 ? "" : "s")}</b>. Until then, raise fish to sell: each day you end lets time pass (eggs hatch, fish grow, water gets dirtier).", Rich());
                if (GUILayout.Button("End the day", UiTheme.Primary))
                {
                    panel = Panel.None;
                    shopDay.StayClosed();
                }
            }
            else
            {
                GUILayout.Label($"Opening moves time on: eggs hatch, fish age and get hungry, water gets dirtier, and illness, breeding and deaths are rolled. The day lasts {Clock(Balance.Data.shop.dayLengthSeconds)}. Or keep the shop closed today: time still passes, but nobody comes in.");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Stay closed today"))
                {
                    panel = Panel.None;
                    shopDay.StayClosed();
                }
                if (GUILayout.Button("Open Shop", UiTheme.Primary))
                {
                    panel = Panel.None;
                    shopDay.Open();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            GUILayout.Label("<b>Recent transactions</b>", Rich());
            var ledger = Economy.Ledger;
            if (ledger.Count == 0) GUILayout.Label("<i>Nothing yet.</i>", Rich());
            for (int i = ledger.Count - 1; i >= Mathf.Max(0, ledger.Count - 9); i--)
                GUILayout.Label(ledger[i]);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Shopkeeper:", GUILayout.Width(90f));
            if (GUILayout.Toggle(ShopOwner.Look == OwnerLook.Woman, "Woman", GUI.skin.button) && ShopOwner.Look != OwnerLook.Woman) ShopOwner.Look = OwnerLook.Woman;
            if (GUILayout.Toggle(ShopOwner.Look == OwnerLook.Man, "Man", GUI.skin.button) && ShopOwner.Look != OwnerLook.Man) ShopOwner.Look = OwnerLook.Man;
            GUILayout.EndHorizontal();
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
            if (!Economy.CanOpenShop && GUILayout.Button("Skip the setup days (dev)")) Economy.DevSkipSetup();
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
            if (report.stayedClosed)
            {
                GUILayout.Label($"Day {report.day}: the shop stayed closed", UiTheme.Title);
                int setup = Economy.DaysUntilOpening;
                GUILayout.Label(setup > 1 ? $"<color={UiTheme.SunHex}>{setup} days until the grand opening.</color> Keep raising fish to sell."
                    : setup == 1 ? $"<color={UiTheme.SunHex}>Tomorrow is the grand opening!</color> Make sure the for sale tank has some juveniles or adults."
                    : Economy.Day == Balance.Data.shop.setupDays + 1 ? $"<color={UiTheme.SunHex}>Today is the grand opening!</color> Open the shop when you're ready."
                    : "No customers today, so no sales and no reviews.", Rich());
            }
            else
            {
                GUILayout.Label($"Day {report.day} is over", UiTheme.Title);
                GUILayout.Label($"Visitors: {report.visitors}     Fish sold: {report.fishSold}     Earned: <b><color={UiTheme.LeafHex}>{Economy.Format(report.earnings)}</color></b>", Rich());
                GUILayout.Label($"Reactions: {report.happy} happy, {report.grossed} grossed out, {report.sad} sad, {report.mad} angry about prices", Rich());
                GUILayout.Label($"Reviews: <size=18><color={UiTheme.SunHex}>{Stars(report.stars)}</color></size> ({report.stars:0.#} / 5)   Reputation {(report.reputationChange >= 0f ? "+" : "")}{report.reputationChange * 100f:0}%", Rich());
                GUILayout.Label($"<i>{report.review}</i>", Rich());
            }
            GUILayout.Space(6f);
            GUILayout.Label(report.stayedClosed ? "IN THE TANKS" : "OVERNIGHT IN THE TANKS", UiTheme.Heading);
            GUILayout.Label(report.overnight, Rich());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back to work", UiTheme.Primary, GUILayout.Height(34f)))
            {
                shopDay.LastReport = null;
                panel = Panel.None;
                StartToDo();
            }
            GUILayout.EndArea();
        }

        void OpenSupplier(bool fishSeller)
        {
            atFishSeller = fishSeller;
            panel = Panel.Supplier;
            if (fishSeller && FishSeller.Instance != null) FishSeller.Instance.Greet();
        }

        /// <summary>The first time the game runs: who runs the shop. It can be changed at the register.</summary>
        void DrawOwnerPick()
        {
            GUILayout.BeginArea(panelRect, UiStyles.Window);
            GUILayout.Label("Who runs the shop?", UiTheme.Title);
            GUILayout.Label("Pick your shopkeeper. You can change this any time at the register.", Rich());
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            OwnerCard(OwnerLook.Woman, CrewModels.OwnerWoman, "Her");
            GUILayout.Space(24f);
            OwnerCard(OwnerLook.Man, CrewModels.OwnerMan, "Him");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        void OwnerCard(OwnerLook look, string modelId, string label)
        {
            GUILayout.BeginVertical(UiTheme.Card, GUILayout.Width(170f));
            var rect = GUILayoutUtility.GetRect(150f, 220f, GUILayout.Width(150f), GUILayout.Height(220f));
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.2f, 0.24f, 0.3f, 0.9f), 0f, 6f);
            var photo = ItemPreviews.Person(modelId);
            if (photo != null) GUI.DrawTexture(rect, photo, ScaleMode.ScaleToFit);
            if (GUILayout.Button(label, UiTheme.Primary, GUILayout.Height(34f)))
            {
                ShopOwner.Look = look;
                panel = Panel.None;
            }
            GUILayout.EndVertical();
        }

        void DrawSupplier()
        {
            GUILayout.BeginArea(panelRect, UiStyles.Window);
            GUILayout.BeginHorizontal();
            GUILayout.Label(atFishSeller ? "Fish seller" : "Supplier computer", UiTheme.Title);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Fishkeeping {Progression.Fishkeeping}   Shopkeeping {Progression.Shopkeeping}   Balance: <b>{Economy.Format(Economy.Money)}</b>", Rich());
            GUILayout.EndHorizontal();
            if (shopDay.IsOpen)
            {
                GUILayout.Label(atFishSeller ? "The fish seller only sells while the shop is closed." : "The supplier only delivers while the shop is closed.");
                if (GUILayout.Button("Close")) panel = Panel.None;
                GUILayout.EndArea();
                return;
            }
            var tabs = atFishSeller ? FishSellerTabs : ComputerTabs;
            var tabNames = new string[tabs.Length];
            for (int i = 0; i < tabs.Length; i++) tabNames[i] = SupplierTabs[tabs[i]];
            int shown = Mathf.Max(0, System.Array.IndexOf(tabs, supplierTab));
            shown = Mathf.Clamp(GUILayout.Toolbar(shown, tabNames), 0, tabs.Length - 1);
            supplierTab = tabs[shown];
            GUILayout.Label(atFishSeller ? "<size=11><i>Food, decorations and equipment are on the supplier computer.</i></size>" : "<size=11><i>Egg and fry boxes are at the fish seller's stall.</i></size>", Rich());
            GUILayout.Space(2f);

            if (supplierTab != FoodTab) DrawDeliverTo();

            supplierScroll = GUILayout.BeginScrollView(supplierScroll);
            switch (supplierTab)
            {
                case EggsTab: DrawEggOffers(false); break;
                case FryTab: DrawEggOffers(true); break;
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
            bool fish = supplierTab == EggsTab || supplierTab == FryTab || supplierTab == DevFryTab;
            GUILayout.Label(fish ? "Deliver to:" : supplierTab == DecorTab ? "Put in:" : "Install in:");
            var tanks = store.Tanks;
            var names = new string[tanks.Count];
            int current = 0;
            for (int i = 0; i < tanks.Count; i++)
            {
                var tank = tanks[i].Tank;
                names[i] = fish ? $"{tanks[i].Title} ({FreeSpace(tanks[i])} free)"
                    : supplierTab == DecorTab ? $"{tanks[i].Title} ({tank.DecorCount}/{tank.Decor.Capacity})"
                    : tanks[i].Title;
                if (tanks[i] == orderTarget) current = i;
            }
            current = GUILayout.SelectionGrid(current, names, 3);
            if (tanks.Count > 0) orderTarget = tanks[current];
            GUILayout.Space(4f);
        }

        // Orders only fill a tank up to its limit; moving or breeding fish can push it over.
        static int FreeSpace(StoreTank tank) => Mathf.Max(0, tank.Tank.FreeSpaceBeforeLimit - EggClutch.EggsIn(tank.Tank));

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

        /// <summary>
        /// Egg boxes or fry boxes. Both roll their fish the same way; eggs hatch at the next time
        /// step and only egg-laying fish come as eggs, while fry arrive straight away and can be any
        /// fish. Either way, the fry are tiny grey fish until they grow enough to tell what they are.
        /// </summary>
        void DrawEggOffers(bool fry)
        {
            GUILayout.Label(fry
                ? "<i>Boxes of mixed fry, delivered right away. Any kind of fish can come as fry, but they're all tiny grey fish: you find out what each one is when it grows into a juvenile. Each extra rare fish in one box is harder to get than the last.</i>"
                : "<i>Boxes of mixed eggs. They hatch at the next time step (when the shop opens or closes). Only egg-laying fish come as eggs: livebearers like guppies and mollies come as fry. Each extra rare fish in one box is harder to get than the last.</i>", Rich());
            int free = orderTarget != null ? FreeSpace(orderTarget) : 0;
            int level = Progression.Fishkeeping;
            bool eggLayersOnly = !fry;
            foreach (var tier in EggBoxes.Tiers)
            {
                var normal = EggBoxes.Pool(tier.normalMin, tier.normalMax, level, eggLayersOnly);
                var rare = EggBoxes.Pool(tier.rarePool, tier.rarePool, level, eggLayersOnly);
                bool unlocked = Progression.IsUnlocked(XpTrack.Fishkeeping, tier.unlockLevel);
                bool anyFish = normal.Count + rare.Count > 0 || !unlocked;
                string details = tier.blurb;
                if (unlocked && normal.Count + rare.Count == 0) details += "\n<color=#ffd27f>No fish of this kind in the game yet.</color>";
                else if (unlocked)
                {
                    var odds = EggBoxes.AtLeastRareOdds(tier);
                    var oddsText = new List<string>();
                    for (int k = 0; k < odds.Length && k < 3; k++) oddsText.Add($"{k + 1}+ {Percent(odds[k])}");
                    details += $"\n{tier.eggs} {(fry ? "fry" : "eggs")}. Rare fish ({tier.rarePool}): " + (rare.Count > 0 ? string.Join(", ", oddsText) : RareUnlockText(tier, eggLayersOnly));
                }
                int price = Perks.Supplier(fry ? tier.fryPrice : tier.price);
                string what = fry ? "fry" : "eggs";
                string label = free >= tier.eggs ? $"Buy {tier.eggs} {what}  {Economy.Format(price)}" : $"Needs {tier.eggs} free spaces";
                bool canBuy = anyFish && free >= tier.eggs && Economy.CanAfford(price) && orderTarget != null;
                var picture = fry ? ItemPreviews.FryBox(tier.color) : ItemPreviews.EggBox(tier.color, tier.speckled);
                if (OfferRow(picture, fry ? EggBoxes.FryName(tier) : tier.name, details, XpTrack.Fishkeeping, tier.unlockLevel, label, canBuy))
                {
                    if (fry) OrderFryBox(tier);
                    else OrderEggs(tier);
                }
                if (unlocked && normal.Count + rare.Count > 0) DrawPoolPictures(normal, rare);
            }
        }

        static string Percent(float chance) => chance < 0.01f ? "<1%" : $"{chance * 100f:0}%";

        /// <summary>When the first species of the tier's rare pool unlocks, for boxes that can't hatch one yet.</summary>
        static string RareUnlockText(EggTierBalance tier, bool eggLayersOnly)
        {
            int first = int.MaxValue;
            foreach (var species in Species.All)
                if (species.rarity == tier.rarePool && (!eggLayersOnly || species.reproduction == Reproduction.EggLayer)) first = Mathf.Min(first, species.unlockLevel);
            return first == int.MaxValue ? "none in the game yet" : $"from Fishkeeping level {first}";
        }

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
            if (!Economy.TrySpend(Perks.Supplier(tier.price), tier.name.ToLower())) return;
            var eggs = EggBoxes.Roll(tier, Progression.Fishkeeping, true);
            EggClutch.Lay(orderTarget.Tank, tier, eggs);
            if (FishSeller.Instance != null) FishSeller.Instance.Sold();
            GameLog.Add($"{tier.eggs} {tier.name.ToLower()} delivered to {orderTarget.Title}. They hatch at the next time step.");
        }

        void OrderFryBox(EggTierBalance tier)
        {
            if (!Economy.TrySpend(Perks.Supplier(tier.fryPrice), EggBoxes.FryName(tier).ToLower())) return;
            var tank = orderTarget.Tank;
            foreach (var genome in EggBoxes.Roll(tier, Progression.Fishkeeping, false))
                FishFactory.Spawn(genome, 1, 0.05f, tank, tank.RandomFreePoint(0.1f));
            if (FishSeller.Instance != null) FishSeller.Instance.Sold();
            GameLog.Add($"{tier.eggs} {EggBoxes.FryName(tier).ToLower()} delivered to {orderTarget.Title}. You'll see what they are when they grow.");
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
                int price = Perks.Decoration(item.price);
                string label = full ? "No room in this tank" : $"Add  {Economy.Format(price)}";
                if (OfferRow(ItemPreviews.Decoration(item.id), item.name, details, XpTrack.Shopkeeping, item.unlockLevel, label, !full && Economy.CanAfford(price))
                    && Economy.TrySpend(price, $"a {item.name.ToLower()} for {orderTarget.Title}"))
                {
                    if (tank.Decor.Add(item.id))
                    {
                        GameLog.Add($"Added a {item.name.ToLower()} to {orderTarget.Title}.");
                        Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpBuyDecoration);
                        Progression.Count(GoalKind.BuyDecorations);
                    }
                    else Economy.Earn(price, "refund: no room for a decoration");
                }
            }
        }

        void DrawFoodOffers()
        {
            foreach (var offer in SupplierCatalog.Food)
            {
                var tint = offer.color;
                string details = $"{offer.blurb}\nYou have {Economy.FoodPortions(offer.type)} feedings.";
                int price = Perks.Supplier(offer.price);
                if (OfferRow(ItemPreviews.Filler(ItemPreviews.Icon.Food, tint), offer.name, details, XpTrack.Fishkeeping, offer.unlockLevel,
                        $"{offer.feedings} feedings  {Economy.Format(price)}", Economy.CanAfford(price))
                    && Economy.TrySpend(price, offer.name.ToLower()))
                {
                    Economy.AddFood(offer.type, offer.feedings);
                    GameLog.Add($"Bought {offer.name.ToLower()}: {offer.feedings} more feedings.");
                }
            }
        }

        void DrawEquipmentOffers()
        {
            var supplier = SupplierCatalog.Equipment;
            var tank = orderTarget != null ? orderTarget.Tank : null;
            string where = orderTarget != null ? orderTarget.Title : "a tank";
            GUILayout.Label("<i>Equipment wears out: the longer it runs, the likelier it breaks down at a time step. A broken one does nothing until you fix it with a repair kit. Better models work better and break less.</i>", Rich());

            EquipmentSection(EquipmentKind.Filter, ItemPreviews.Icon.Filter, "Filters", "Cuts how fast waste dirties the water.", tank, where,
                info => $"Leaves {info.effect * 100f:0}% of the waste");
            EquipmentSection(EquipmentKind.Heater, ItemPreviews.Icon.Heater, "Heaters", $"Holds the water at the temperature you set in the tank view. Without one the water is room temperature ({GameConfig.Active.roomTemperature:0} C).", tank, where,
                info => info.effect > 0.05f ? $"Holds the setting within {info.effect:0.#} C" : "Holds the exact setting");
            EquipmentSection(EquipmentKind.Bubbler, ItemPreviews.Icon.Bubbler, "Bubblers", $"Air for the fish. Without one, a tank has air for {GameConfig.Active.fishPerTankWithoutBubbler} fish.", tank, where,
                info => $"Air for {info.effect:0} fish");

            GUILayout.Label("<b>Care</b>", Rich());
            int repairPrice = Perks.Supplier(supplier.repairKitPrice);
            int conditionerPrice = Perks.Supplier(supplier.waterConditionerPrice);
            string repairDetails = $"One kit fixes one broken filter, heater or bubbler. Repairs take off only a little wear, so old equipment keeps breaking more often.\nYou have {Economy.RepairKits} kits.";
            if (OfferRow(ItemPreviews.Filler(ItemPreviews.Icon.Repair, new Color(0.85f, 0.6f, 0.25f)), "Repair kits", repairDetails, XpTrack.Fishkeeping, supplier.repairKitLevel,
                    $"{supplier.repairKitsPerPack} kits  {Economy.Format(repairPrice)}", Economy.CanAfford(repairPrice))
                && Economy.TrySpend(repairPrice, "repair kits"))
                Economy.RepairKits += supplier.repairKitsPerPack;

            string details = $"Resets the pH and gets rid of snails and worms. Use it from the tank view.\nYou have {Economy.WaterConditionerDoses} doses.";
            if (OfferRow(ItemPreviews.Filler(ItemPreviews.Icon.Conditioner, new Color(0.35f, 0.65f, 0.85f)), "Water conditioner", details, XpTrack.Fishkeeping, supplier.waterConditionerLevel,
                    $"{supplier.waterConditionerDoses} doses  {Economy.Format(conditionerPrice)}", Economy.CanAfford(conditionerPrice))
                && Economy.TrySpend(conditionerPrice, "water conditioner"))
                Economy.WaterConditionerDoses += supplier.waterConditionerDoses;
        }

        /// <summary>All the models of one kind of equipment, and what the chosen tank has now.</summary>
        void EquipmentSection(EquipmentKind kind, ItemPreviews.Icon icon, string title, string blurb, Tank tank, string where, System.Func<EquipmentTierBalance, string> effect)
        {
            var installed = tank != null ? tank.GetEquipment(kind) : null;
            string now = installed == null ? "none"
                : installed.broken ? $"{installed.Name} <color=#ff8080>(broken)</color>"
                : $"{installed.Name} ({installed.BreakChance * 100f:0}% chance of breaking per time step)";
            GUILayout.Label($"<b>{title}</b>  <size=11>{blurb} {where}: {now}</size>", Rich());
            var tiers = Equipment.Tiers(kind);
            for (int i = 0; i < tiers.Count; i++)
            {
                var info = tiers[i];
                int tier = i;
                bool same = installed != null && installed.tier == i;
                int price = Perks.Supplier(info.price);
                string label = installed == null ? $"Install  {Economy.Format(price)}"
                    : same ? $"Replace with new  {Economy.Format(price)}"
                    : $"Swap to this  {Economy.Format(price)}";
                string details = $"{info.blurb} {effect(info)}. Breaks {info.breakChance * 100f:0.#}% of time steps when new, up to {info.maxBreakChance * 100f:0}% when old.";
                var tint = Color.Lerp(new Color(0.55f, 0.6f, 0.68f), new Color(0.95f, 0.8f, 0.35f), tiers.Count > 1 ? i / (float)(tiers.Count - 1) : 0f);
                if (OfferRow(ItemPreviews.Filler(icon, tint), info.name, details, XpTrack.Fishkeeping, info.unlockLevel, label,
                        tank != null && Economy.CanAfford(price), 56f)
                    && Economy.TrySpend(price, $"a {info.name.ToLower()} for {where}"))
                {
                    tank.Install(kind, tier);
                    Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpBuyEquipment);
                    Milestones.Add(MilestoneKind.BuyEquipment);
                    GameLog.Add($"{info.name} installed in {where}.");
                }
            }
            GUILayout.Space(6f);
        }

        /// <summary>Developer list: any species as a single fry, ignoring levels, for testing.</summary>
        void DrawDevFryOffers()
        {
            GUILayout.Label("<i>Developer only: buy any species as a fry, ignoring levels.</i>", Rich());
            int free = orderTarget != null ? FreeSpace(orderTarget) : 0;
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Random mix</b>  <size=11>A free adult with the body, fins and tail of three random species, to see that any fins fit any body. Only compatible pairs can breed mixes in game.</size>", Rich());
            GUI.enabled = free >= 1;
            if (GUILayout.Button("Random mix", GUILayout.Width(120f), GUILayout.Height(32f))) OrderRandomMix();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            foreach (var species in Species.All)
            {
                string livebearer = species.reproduction == Reproduction.Livebearer ? ", livebearer" : "";
                string details = $"{species.rarity}, {Category(species.category)}{livebearer}, {species.tempMin:0}-{species.tempMax:0} C, Fishkeeping {species.unlockLevel}";
                if (OfferRow(ItemPreviews.Fish(species), species.displayName, details, XpTrack.Fishkeeping, 0, $"1 fry  {Economy.Format(species.fryPrice)}",
                        free >= 1 && Economy.CanAfford(species.fryPrice), 48f))
                    OrderFry(species);
            }
        }

        void OrderRandomMix()
        {
            var all = Species.All;
            var genome = Genome.CreateRandom(all[GameRandom.Range(0, all.Count)]);
            string fins = all[GameRandom.Range(0, all.Count)].id;
            string tail = GameRandom.Chance(0.5f) ? fins : all[GameRandom.Range(0, all.Count)].id;
            genome.finSpecies = fins == genome.species ? "" : fins;
            genome.tailSpecies = tail == genome.species ? "" : tail;
            var tank = orderTarget.Tank;
            var fish = FishFactory.Spawn(genome, 1, 1f, tank, tank.RandomFreePoint(0.1f));
            GameLog.Add($"{fish.FishName} the {fish.SpeciesLabel} arrived in {orderTarget.Title}.");
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
            GUILayout.Label($"Unlock the {section.name}?", UiTheme.Title);
            GUILayout.Label("Opens up room for 6 more display or for sale tanks.");
            var next = store.NextLockedSection;
            int level = StoreLayout.SectionLevel(section);
            bool levelOk = Progression.IsUnlocked(XpTrack.Shopkeeping, level);
            if (shopDay.IsOpen) GUILayout.Label("<i>Building work has to wait until the shop is closed.</i>", Rich());
            else if (next != section) GUILayout.Label($"<i>Unlock the {next.name} first.</i>", Rich());
            else if (!levelOk) GUILayout.Label($"<i>Needs {Progression.LockText(XpTrack.Shopkeeping, level)}.</i>", Rich());
            else if (!Economy.CanAfford(section.Price)) GUILayout.Label($"<i>You need {Economy.Format(section.Price - Economy.Money)} more.</i>", Rich());
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUI.enabled = !shopDay.IsOpen && next == section && levelOk && Economy.CanAfford(section.Price);
            if (GUILayout.Button($"Unlock for {Economy.Format(section.Price)}") && store.TryUnlock(section)) panel = Panel.None;
            GUI.enabled = true;
            if (GUILayout.Button("Cancel")) panel = Panel.None;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static readonly TankRole[] BuyableRoles = { TankRole.ForSale, TankRole.Display, TankRole.Breeder, TankRole.Quarantine };

        void DrawBuyTank()
        {
            GUILayout.BeginArea(panelRect, UiStyles.Window);
            GUILayout.Label(pendingSlot.backRoom ? "Buy a tank for the back room" : "Buy a tank for this spot", UiTheme.Title);
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
            if (topBar.Contains(e.mousePosition) || statusBar.Contains(e.mousePosition) || milestoneRect.Contains(e.mousePosition)) return;
            if (todo.Count > 0 && todoBar.Contains(e.mousePosition)) return;
            if (panel != Panel.None)
            {
                // Clicking outside a window closes it (except the day report, which needs its button).
                if (!panelRect.Contains(e.mousePosition) && panel != Panel.DayReport && panel != Panel.Owner) panel = Panel.None;
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
                case StoreClickable.Kind.Computer: OpenSupplier(false); break;
                case StoreClickable.Kind.FishSeller: OpenSupplier(true); break;
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

        static GUIStyle WorldLabelStyle()
        {
            if (label == null) label = new GUIStyle(UiTheme.Bar) { richText = true, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(10, 10, 4, 5), wordWrap = false };
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
