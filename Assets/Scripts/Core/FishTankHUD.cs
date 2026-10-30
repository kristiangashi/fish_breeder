using System;
using System.Collections.Generic;
using FishBreeder.Breeding;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.Health;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// IMGUI for looking into one tank: the tank's water, equipment, heater, decorations
    /// and size on the left, a status window next to the selected fish (the camera follows it), and
    /// the breeding bar when two fish are picked. With a fish selected, left/right arrows or the
    /// little buttons beside it step through the fish in the tank; with none selected they move to
    /// the next tank, as do the buttons at the bottom. While the shop is open everything is view-only.
    /// </summary>
    public class FishTankHUD : MonoBehaviour
    {
        StoreTank storeTank;
        Tank tank;
        IReadOnlyList<StoreTank> allTanks;
        Camera cam;
        Action onBack;
        Func<bool> shopIsOpen;
        readonly List<Fish> selected = new List<Fish>();
        Rect leftPanel, fishWindow, breedBar, logPanel, stepLeft, stepRight, tankBar;
        string priceText = "";
        Fish priceTextFor;
        Vector2 panelScroll, fishScroll;
        Fish fishScrollFor;
        Func<StoreTank, int, bool> upgradeTank;
        Action<int> switchTank;
        Func<int, StoreTank> neighbourTank;

        /// <summary>The fish the camera follows: the last one selected.</summary>
        public Fish Focus => selected.Count > 0 ? selected[selected.Count - 1] : null;
        public bool HasSelection => selected.Count > 0;
        public StoreTank Current => storeTank;
        /// <summary>A spot in the water the camera zooms in on (clicked with no fish there), or null.</summary>
        public Vector3? ZoomPoint { get; private set; }
        /// <summary>How far WASD has slid the whole-tank view (metres along the glass and up), without zooming in.</summary>
        public Vector2 ViewPan { get; private set; }
        /// <summary>The scroll wheel's zoom: the camera's distance as a share of the normal view (below 1 is closer).</summary>
        public float WheelZoom { get; private set; } = 1f;
        /// <summary>
        /// The scroll wheel's zoom in a close-up (a picked fish or a clicked spot), kept apart from the
        /// whole-tank view's: how far the camera stands out from the glass, as a share of the normal
        /// close-up (below 1 is closer). Leaving the close-up puts it back to 1.
        /// </summary>
        public float CloseZoom { get; private set; } = 1f;
        /// <summary>Whether the view is a close-up on a fish or a clicked spot, not the whole tank.</summary>
        public bool CloseUp => HasSelection || ZoomPoint.HasValue;
        /// <summary>The zoom the camera uses now: the close-up's own, or the whole-tank view's.</summary>
        public float ViewZoom => CloseUp ? CloseZoom : WheelZoom;
        /// <summary>Looking straight down into the tank instead of through the front glass (T). It stays on when switching tanks.</summary>
        public bool TopView { get; private set; }

        public void ToggleTopView()
        {
            TopView = !TopView;
            ViewPan = Vector2.zero;
            WheelZoom = 1f;
        }
        public const float MinWheelZoom = 0.3f, MaxWheelZoom = 1.5f;
        public const float MinCloseZoom = 0.1f, MaxCloseZoom = 3f;

        /// <summary>Lets go of the picked fish and the clicked spot: back to the whole tank, at the wheel zoom it had.</summary>
        public void ClearSelection()
        {
            selected.Clear();
            ZoomPoint = null;
            CloseZoom = 1f;
        }

        /// <summary>
        /// Scroll wheel: each notch moves the camera 15% closer (up) or further (down), within the
        /// limits of the view it's in (a close-up has its own).
        /// </summary>
        public void ZoomWheel(float notches)
        {
            float step = Mathf.Pow(0.85f, notches);
            if (CloseUp) CloseZoom = Mathf.Clamp(CloseZoom * step, MinCloseZoom, MaxCloseZoom);
            else WheelZoom = Mathf.Clamp(WheelZoom * step, MinWheelZoom, MaxWheelZoom);
        }

        /// <summary>Back to the normal distance; false if it already was.</summary>
        public bool ResetWheelZoom()
        {
            if (Mathf.Abs(WheelZoom - 1f) < 0.01f) return false;
            WheelZoom = 1f;
            return true;
        }

        /// <summary>Whether the mouse is over one of the tank menus (so the wheel scrolls the menu, not the view).</summary>
        public bool MouseOverMenu
        {
            get
            {
                if (!enabled || tank == null) return false;
                float scale = Mathf.Max(1f, Screen.height / 900f);
                var m = new Vector2(Input.mousePosition.x / scale, (Screen.height - Input.mousePosition.y) / scale);
                return leftPanel.Contains(m) || fishWindow.Contains(m) || breedBar.Contains(m) || logPanel.Contains(m) || tankBar.Contains(m)
                    || pickerRect.Contains(m) || levelPopup.Contains(m);
            }
        }

        public void Select(Fish fish)
        {
            selected.Clear();
            ZoomPoint = null;
            if (fish != null && fish.Tank == tank) selected.Add(fish);
        }

        /// <summary>Steps to the next (+1) or previous (-1) fish in the tank, living first, then dead.</summary>
        public void Cycle(int direction)
        {
            var all = new List<Fish>(tank.Residents);
            all.AddRange(tank.Corpses);
            all.RemoveAll(f => f == null);
            if (all.Count == 0) return;
            int index = Focus != null ? all.IndexOf(Focus) : -1;
            index = index < 0 ? (direction > 0 ? 0 : all.Count - 1) : (index + direction + all.Count) % all.Count;
            Select(all[index]);
        }

        bool ReadOnly => shopIsOpen != null && shopIsOpen();

        /// <summary>Lets the arrow keys and the buttons at the bottom move to the previous (-1) or next (+1) tank.</summary>
        public void SetTankNavigation(Action<int> switchTo, Func<int, StoreTank> neighbour)
        {
            switchTank = switchTo;
            neighbourTank = neighbour;
        }

        public void Initialize(StoreTank current, IReadOnlyList<StoreTank> tanks, Camera camera, Action back, Func<bool> isShopOpen, Func<StoreTank, int, bool> upgrade)
        {
            upgradeTank = upgrade;
            storeTank = current;
            tank = current.Tank;
            allTanks = tanks;
            cam = camera;
            onBack = back;
            shopIsOpen = isShopOpen;
            selected.Clear();
            ZoomPoint = null;
            ViewPan = Vector2.zero;
            WheelZoom = CloseZoom = 1f;
            Arranging = ArrangeMode.None;
            picker = Picker.None;
            showLevelInfo = false;
            heldPiece = dragPiece = -1;
            confirmText = null;
            confirmAction = null;
        }

        void Update()
        {
            selected.RemoveAll(f => f == null || f.Tank != tank);
            // However the close-up ended (a click outside the water, the fish moved away), the next one starts at its normal distance.
            if (!CloseUp) CloseZoom = 1f;
            if (tank == null || GUIUtility.keyboardControl != 0 || UiLayout.Editing) return;
            if (Arranging == ArrangeMode.Decorations && heldPiece >= 0 && tank.Decor != null)
            {
                if (Input.GetKeyDown(KeyCode.Q)) tank.Decor.Turn(heldPiece, -15f);
                if (Input.GetKeyDown(KeyCode.E)) tank.Decor.Turn(heldPiece, 15f);
                if (tank.IsWallAquarium)
                {
                    if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) tank.Decor.Resize(heldPiece, 1f / DecorSizeStep);
                    if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) tank.Decor.Resize(heldPiece, DecorSizeStep);
                }
            }
            if (Arranging != ArrangeMode.None) return;
            if (Input.GetKeyDown(KeyCode.T)) ToggleTopView();
            SlideZoom(Time.unscaledDeltaTime);
            int step = Input.GetKeyDown(KeyCode.RightArrow) ? 1 : Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 0;
            if (step == 0 || ZoomPoint.HasValue) return;
            if (HasSelection) Cycle(step);
            else switchTank?.Invoke(step);
        }

        /// <summary>
        /// WASD (and the arrow keys once zoomed in on a spot) slide the close-up view about the
        /// tank; while following a fish, they let go of it and slide from where it is. From the
        /// whole-tank view, WASD slides that view along the glass and up and down, at the same
        /// distance, as far as the tank's edges.
        /// </summary>
        void SlideZoom(float dt)
        {
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;
            if (ZoomPoint.HasValue)
            {
                if (Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
                if (Input.GetKey(KeyCode.RightArrow)) x += 1f;
                if (Input.GetKey(KeyCode.UpArrow)) y += 1f;
                if (Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            }
            if (x == 0f && y == 0f) return;
            if (!ZoomPoint.HasValue && Focus == null)
            {
                var size = tank.InnerSize;
                float pace = Mathf.Max(0.6f, size.x * 0.3f);
                var pan = ViewPan + new Vector2(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(y, -1f, 1f)) * pace * dt;
                // From above, up the screen is towards the back of the tank.
                float reachY = TopView ? size.z * 0.5f : size.y * 0.5f;
                ViewPan = new Vector2(Mathf.Clamp(pan.x, -size.x * 0.5f, size.x * 0.5f), Mathf.Clamp(pan.y, -reachY, reachY));
                return;
            }
            var from = ZoomPoint ?? Focus.transform.position;
            // About a third of the view's width a second, whatever the tank's size.
            float speed = Mathf.Max(0.5f, Mathf.Min(tank.InnerSize.x, 3f) * 0.35f);
            var move = (tank.transform.right * Mathf.Clamp(x, -1f, 1f) + (TopView ? tank.transform.forward : Vector3.up) * Mathf.Clamp(y, -1f, 1f)) * speed * dt;
            ZoomPoint = tank.Clamp(from + move, 0.12f, 0.08f);
        }

        /// <summary>Share of the screen's width, from the left, that the tank menu covers (0 when the menus are hidden).</summary>
        public float MenuCover
        {
            get
            {
                if (!enabled || tank == null || ViewMode.Watching || Screen.width <= 0) return 0f;
                float scale = Mathf.Max(1f, Screen.height / 900f);
                // The panel is 300 wide from 10 in (see OnGUI), plus a little breathing room.
                return Mathf.Clamp01(320f * scale / Screen.width);
            }
        }

        void OnGUI()
        {
            if (tank == null) return;
            UiTheme.Apply();
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            if (ViewMode.Watching)
            {
                // No menus: clicking a fish still follows it (the corner is the "Show menus" button).
                leftPanel = fishWindow = breedBar = logPanel = stepLeft = stepRight = tankBar = Rect.zero;
                leftPanel = new Rect(width - 170f, 0f, 170f, 60f);
                pickerRect = levelPopup = Rect.zero;
                HandleClick();
                return;
            }

            // Runs down to the log, so eggs, pregnancies and breeding lines never push the buttons off the bottom.
            leftPanel = new Rect(10f, 10f, 300f, Mathf.Min(height - 20f, Mathf.Max(520f, height - 170f)));
            logPanel = new Rect(10f, height - 150f, Mathf.Min(460f, width - 20f), 140f);
            breedBar = selected.Count == 2 ? new Rect(Mathf.Max(10f, width * 0.5f - 210f), height - 120f, Mathf.Min(420f, width - 20f), 60f) : Rect.zero;
            // Beside the log when there's room; in a narrow window, just above it instead.
            float barWidth = Mathf.Min(460f, width - 20f);
            float barX = Mathf.Max(logPanel.xMax + 10f, width * 0.5f - barWidth * 0.5f);
            if (barX + barWidth <= width - 10f) tankBar = new Rect(barX, height - 50f, barWidth, 40f);
            else
            {
                barX = Mathf.Max(leftPanel.xMax + 10f, width - 10f - barWidth);
                tankBar = new Rect(barX, logPanel.y - 46f, width - 10f - barX, 40f);
            }
            fishWindow = Rect.zero;
            stepLeft = stepRight = Rect.zero;

            DrawTankPanel();
            DrawPicker(height);
            if (selected.Count > 0)
            {
                DrawStepButtons(Focus, scale, height);
                DrawFishWindow(Focus, scale, width, height);
            }
            if (selected.Count == 2) DrawBreedBar();
            DrawTankBar();
            DrawLog();
            DrawSickMarkers(scale, height);
            DrawSelectionMarkers(scale, height);
            DrawHeldMarker(scale, height);
            DrawLevelPopup(width, height);
            if (!HandleArranging() && Arranging == ArrangeMode.None) HandleClick();
        }

        // ------------------------------------------------------------ tank panel

        enum TankTab { Care, Tank, Upgrades, Breeding, Dev }

        static readonly string[] TabNames = { "Care", "Tank", "Upgrades", "Breeding", "Dev" };
        TankTab tab;

        /// <summary>The Dev tab shows in the editor, and in dev mode (F10) in a development build.</summary>
        static bool ShowsDevTab => Application.isEditor || DevMode.On;

        /// <summary>Opens the tank on a tab (the to-do list opens on Care).</summary>
        public void ShowCare() => tab = TankTab.Care;

        public int TabCount => ShowsDevTab ? TabNames.Length : TabNames.Length - 1;

        public void ShowTab(int index) => tab = (TankTab)Mathf.Clamp(index, 0, TabCount - 1);

        /// <summary>
        /// The tank panel, in four tabs: Care (the daily jobs: feeding, wiping, water treatment,
        /// medicine, repairs, the heater's setting, then the tank's water and fish), Tank (size,
        /// water type, decorations, background and tank type), Upgrades (filter, heater and bubbler)
        /// and Breeding (eggs, pregnancies and who's ready). A dot on a tab means something there
        /// needs doing. The aquarium's level sits in a circle by the title on every tab: hover over
        /// it, or click the "?" beside it, for what makes it up.
        /// </summary>
        void DrawTankPanel()
        {
            UiCheck.Begin("Tank panel", leftPanel, UiStyles.Window);
            GUILayout.BeginHorizontal();
            Ui.Label($"<b>{storeTank.Title}</b>   {Economy.Format(Economy.Money)}", Rich());
            GUILayout.FlexibleSpace();
            DrawLevelBadge();
            if (Ui.Button(new GUIContent("?", "What the aquarium's level means"), SmallButton(), GUILayout.Width(22f), GUILayout.Height(22f))) showLevelInfo = !showLevelInfo;
            if (Event.current.type == EventType.Repaint)
            {
                // The details pop out beside this, outside the panel, so nothing in it moves.
                var help = GUILayoutUtility.GetLastRect();
                levelHover |= help.Contains(Event.current.mousePosition);
                levelAnchor = UiCheck.CurrentWindow.position + new Vector2(help.xMax, help.y);
            }
            GUILayout.EndHorizontal();
            if (Arranging != ArrangeMode.None)
            {
                DrawArrangePanel();
                UiCheck.End();
                return;
            }
            if (ReadOnly) Ui.Label("<color=#ffd27f>The shop is open: you can look, but tank work waits until closing.</color>", Rich());
            if ((int)tab >= TabCount) tab = TankTab.Tank;
            var labels = new string[TabCount];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = TabNames[i] + (TabNeedsYou((TankTab)i) ? $" <color={UiTheme.CoralHex}>●</color>" : "");
            tab = (TankTab)Ui.SelectionGrid((int)tab, labels, labels.Length, TabStyle());
            GUILayout.Space(4f);
            panelScroll = UiCheck.BeginScroll("Tank panel: " + TabNames[(int)tab], panelScroll);
            // A waiting warning comes first (in the picker instead, when one is open).
            if (pickerShown == Picker.None) DrawConfirm();
            switch (tab)
            {
                case TankTab.Care: DrawCareTab(); break;
                case TankTab.Tank: DrawTankTab(); break;
                case TankTab.Upgrades: DrawUpgradesTab(); break;
                case TankTab.Breeding: DrawBreedingTab(); break;
                default: DrawDevTab(); break;
            }
            UiCheck.EndScroll();
            GUILayout.BeginHorizontal();
            if (Ui.Button("Journal (J)")) JournalView.Toggle();
            if (Ui.Button(ResearchView.ButtonLabel(), RichButton())) ResearchView.Toggle();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Ui.Button(Perks.TotalAvailable > 0 ? $"Perks (P) ●{Perks.TotalAvailable}" : "Perks (P)")) PerksView.Toggle();
            if (Ui.Button("Watch (V)")) ViewMode.Set(true);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Ui.Button("Finances (F)")) FinancesView.Toggle();
            if (Ui.Button("Reputation (Y)")) ReputationView.Toggle();
            GUILayout.EndHorizontal();
            if (onBack != null && Ui.Button("Back to store (Esc)")) onBack();
            UiCheck.End();
        }

        // ------------------------------------------------------------ aquarium level

        bool showLevelInfo, levelHover;
        // The "?" button's top right, in the menus' space, and the details box beside it.
        Vector2 levelAnchor;
        Rect levelPopup;

        /// <summary>The aquarium's level in a circle: coral when fish here need a higher one.</summary>
        void DrawLevelBadge()
        {
            int level = TankQuality.Level(tank);
            var rect = Ui.Slot("Level badge", 22f, 22f, GUILayout.Width(22f), GUILayout.Height(22f));
            var color = FishWantingHigherLevel() > 0 ? UiTheme.Coral : level >= TankQuality.MaxLevel ? UiTheme.Sun : UiTheme.Aqua;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 11f);
            GUI.Label(rect, $"<b>{level}</b>", BadgeText());
            if (Event.current.type == EventType.Repaint) levelHover = rect.Contains(Event.current.mousePosition);
        }

        /// <summary>How many fish here need a higher aquarium level than this one has.</summary>
        int FishWantingHigherLevel()
        {
            int level = TankQuality.Level(tank), count = 0;
            foreach (var fish in tank.Residents)
                if (fish != null && !fish.IsDead && TankQuality.LevelNeeded(fish) > level && TankQuality.TooSmallFor(fish.MinTankSize, tank) == null) count++;
            return count;
        }

        /// <summary>
        /// The level details in a little box popping out beside the "?", over everything and
        /// outside the panel: while the mouse is on the circle or the "?", or pinned by clicking "?".
        /// </summary>
        void DrawLevelPopup(float width, float height)
        {
            if (!(showLevelInfo || levelHover) || Arranging != ArrangeMode.None)
            {
                levelPopup = Rect.zero;
                return;
            }
            var anchor = levelAnchor;
            var content = new GUIContent(LevelInfoText());
            const float boxWidth = 250f;
            float boxHeight = HoverStyle().CalcHeight(content, boxWidth);
            float x = anchor.x + 8f;
            // Clear of the picker when one is open beside the panel.
            if (pickerRect.width > 0f && pickerRect.Contains(new Vector2(pickerRect.x + 1f, anchor.y + 1f))) x = pickerRect.xMax + 8f;
            if (x + boxWidth > width - 8f) x = Mathf.Max(8f, anchor.x - boxWidth);
            float y = Mathf.Clamp(anchor.y, 8f, Mathf.Max(8f, height - boxHeight - 8f));
            levelPopup = new Rect(x, y, boxWidth, boxHeight);
            if (Event.current.type == EventType.Repaint) GUI.Box(levelPopup, content, HoverStyle());
        }

        /// <summary>The level, the quality score and what it's made of, the next level and who wants more.</summary>
        string LevelInfoText()
        {
            int quality = tank.Quality, level = TankQuality.LevelFor(quality);
            var text = new System.Text.StringBuilder();
            text.Append($"<b>Level {level}</b>  <size=11>quality {quality}");
            if (level < TankQuality.MaxLevel) text.Append($", level {level + 1} at {TankQuality.QualityForLevel(level + 1)}");
            text.Append("</size>\n");
            var bits = new List<string>();
            foreach (var part in TankQuality.Breakdown(tank)) bits.Add($"{part.label} +{part.points}");
            text.Append("<size=11>" + (bits.Count > 0 ? string.Join(", ", bits) : "Raise it with equipment, decorations and a background.") + "</size>");
            var sizes = Balance.Data.store.tankSizes;
            for (int n = 2; n <= TankQuality.MaxLevel; n++)
            {
                string size = sizes != null && n - 1 < sizes.Count ? sizes[n - 1].name : "";
                text.Append($"\n<size=11>Level {n}: {size.ToLower()} tanks and the fish that need one</size>");
            }
            int unhappy = FishWantingHigherLevel();
            if (unhappy > 0) text.Append($"\n<color={UiTheme.CoralHex}><size=11>{unhappy} fish here want a higher level.</size></color>");
            return text.ToString();
        }

        static GUIStyle badgeText, hoverStyle;
        static GUIStyle BadgeText() => badgeText != null ? badgeText : badgeText = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 13, padding = new RectOffset(), margin = new RectOffset(), normal = { textColor = new Color(0.04f, 0.12f, 0.18f) } };
        static GUIStyle HoverStyle() => hoverStyle != null ? hoverStyle : hoverStyle = new GUIStyle(UiStyles.Window) { richText = true, wordWrap = true, alignment = TextAnchor.UpperLeft, fontSize = 12, padding = new RectOffset(8, 8, 6, 6) };

        // ------------------------------------------------------------ warnings before a change

        string confirmText;
        Action confirmAction;

        /// <summary>Does it straight away, or first shows the warning with buttons to go ahead or not.</summary>
        void Confirm(string warning, Action action)
        {
            if (string.IsNullOrEmpty(warning))
            {
                action();
                return;
            }
            confirmText = warning;
            confirmAction = action;
            panelScroll = Vector2.zero;
        }

        void DrawConfirm()
        {
            if (confirmText == null) return;
            GUILayout.BeginVertical(GUI.skin.box);
            Ui.Label($"<color={UiTheme.SunHex}><b>Are you sure?</b></color>\n<size=11>{confirmText}</size>", Rich());
            GUILayout.BeginHorizontal();
            bool wasEnabled = GUI.enabled;
            GUI.enabled = !ReadOnly;
            if (Ui.Button("Do it anyway", UiTheme.Primary))
            {
                var action = confirmAction;
                confirmText = null;
                confirmAction = null;
                action?.Invoke();
            }
            GUI.enabled = wasEnabled;
            if (Ui.Button("Cancel"))
            {
                confirmText = null;
                confirmAction = null;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        bool TabNeedsYou(TankTab which)
        {
            switch (which)
            {
                case TankTab.Care:
                    if (tank.Corpses.Count > 0 || tank.HasPests || tank.WaterHealth < Balance.Data.shop.grossWaterQuality) return true;
                    foreach (var kind in Equipment.Kinds)
                    {
                        var item = tank.GetEquipment(kind);
                        if (item != null && item.broken) return true;
                    }
                    foreach (var fish in tank.Residents)
                        if (!fish.IsDead && (fish.Health.IsSick || fish.Hunger >= Balance.Data.notifications.hungryAbove)) return true;
                    return false;
                case TankTab.Breeding:
                    foreach (var line in BreedingSystem.Readiness(tank))
                        if (line.Contains("ready to breed")) return true;
                    return false;
                default: return false;
            }
        }

        /// <summary>The daily jobs first, then how the tank and its water are doing.</summary>
        void DrawCareTab()
        {
            GUI.enabled = !ReadOnly;
            DrawFeedButtons();
            if (Ui.Button($"Wipe tank ({tank.WipesLeft} left)")) tank.Clean();
            DrawTreatWater();
            if (tank.Corpses.Count > 0 && Ui.Button($"Remove dead fish ({tank.Corpses.Count})"))
            {
                int count = tank.Corpses.Count;
                foreach (var corpse in new List<Fish>(tank.Corpses)) corpse.RemoveFromTank();
                Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpRemoveDeadFish * count);
                GameLog.Add("You netted out the dead fish.");
            }
            DrawSickList();
            DrawMedicine();
            DrawRepairs();
            if (tank.HasHeater) DrawHeater();
            GUI.enabled = true;

            GUILayout.Space(8f);
            Ui.Label("TANK", UiTheme.Heading);
            DrawTankSpecs();
        }

        /// <summary>Fish, eggs, water, temperature and water type.</summary>
        void DrawTankSpecs()
        {
            DrawFishCount(tank.IsWallAquarium ? tank.SizeInfo.name : tank.SizeInfo.name + " tank");
            int unhappy = FishWantingHigherLevel();
            if (unhappy > 0)
                Ui.Label($"<size=11><color={UiTheme.CoralHex}>{unhappy} fish need a higher aquarium level (see the circle by the title).</color></size>", Rich());
            int eggs = EggClutch.EggsIn(tank);
            int pregnant = 0;
            foreach (var fish in tank.Residents) if (fish.IsPregnant) pregnant++;
            if (eggs > 0 || pregnant > 0)
                Ui.Label($"<color=#ffe9a8>{(eggs > 0 ? $"{eggs} eggs" : "")}{(eggs > 0 && pregnant > 0 ? ", " : "")}{(pregnant > 0 ? $"{pregnant} pregnant" : "")} (see Breeding)</color>", Rich());
            Bar("Water quality", tank.WaterQuality, true);
            Bar("Algae", tank.Algae, false);
            Bar("Oxygen", tank.Oxygen, true);
            var liked = KnownTemperatureRange(out bool allKnown);
            Ui.Label($"Temperature: {tank.Temperature:0.#} C" + (tank.HeaterWorking ? "  (heater)" : tank.HasHeater ? "  <color=#ff8080>(heater broken)</color>" : "  (room temperature)"), Rich());
            UiGauges.Temperature(tank.Temperature, liked);
            Ui.Label((Research.CanReadPh ? $"pH: {tank.Ph:0.0}    " : "") + $"Water: {(tank.IsSaltwater ? "salt" : "fresh")} ({tank.Salinity:0} ppt)", Rich());
            if (tank.HasPests) Ui.Label("<color=#ff8080>Pests! Treat the water (above).</color>", Rich());
        }

        /// <summary>Water conditioner, bought and poured in one go: resets the pH and clears pests.</summary>
        void DrawTreatWater()
        {
            var supplier = Balance.Data.supplier;
            if (!Progression.IsUnlocked(XpTrack.Fishkeeping, supplier.waterConditionerLevel))
            {
                Ui.Label($"<size=11><i>Water treatment: {Progression.LockText(XpTrack.Fishkeeping, supplier.waterConditionerLevel)}</i></size>", Rich());
                return;
            }
            int price = WaterTreatmentPrice;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && Economy.CanAfford(price);
            if (Ui.Button(new GUIContent($"Treat water  {Economy.Format(price)}", "Water conditioner: resets the pH and gets rid of snails and worms"))
                && Economy.TrySpend(price, $"water conditioner for {storeTank.Title}", MoneyKind.Care))
                tank.TreatWater();
            GUI.enabled = wasEnabled;
        }

        /// <summary>What one water treatment costs, after supplier perks.</summary>
        public static int WaterTreatmentPrice => Perks.Supplier(Balance.Data.supplier.waterTreatmentPrice);

        void DrawBreedingTab()
        {
            bool any = false;
            foreach (var clutch in EggClutch.All)
                if (clutch != null && clutch.Tank == tank)
                {
                    any = true;
                    Ui.Label($"<color=#ffe9a8>Eggs: {clutch.Count} {clutch.Label}, hatch in {BreedingSystem.Steps(clutch.StepsToHatch)}</color>", Rich());
                }
            foreach (var fish in tank.Residents)
                if (fish.IsPregnant)
                {
                    any = true;
                    Ui.Label($"<color=#ffb3d9>Pregnant: {fish.FishName} ({fish.SpeciesLabel}), due in {BreedingSystem.Steps(fish.StepsToBirth)}</color>", Rich());
                }
            if (any) GUILayout.Space(4f);
            DrawStockBook();
            var lines = BreedingSystem.Readiness(tank);
            foreach (var line in lines) Ui.Label($"<color=#c9d7e8>{line}</color>", Rich());
            if (lines.Count == 0 && !any) Ui.Label("<i>No fish here to breed.</i>", Rich());
            GUILayout.Space(4f);
            Ui.Label(storeTank.Role == TankRole.Breeder
                ? "<size=11>Pick a male and a female to pair them up.</size>"
                : "<size=11>Pairs in other tanks only breed now and then by chance; a breeder tank in the breeding room lets you pair them.</size>", Rich());
        }

        /// <summary>
        /// The stock book (a Breeding perk): every fish in the tank by kind, then sex and age, each
        /// row a button that picks the fish like clicking it in the water.
        /// </summary>
        void DrawStockBook()
        {
            Ui.Label("<b>Stock book</b>", Rich());
            if (Perks.Count(PerkEffect.StockBook) == 0 && !DevMode.On)
            {
                Ui.Label("<size=11><i>The Stock book perk (Breeding, tier 1) lists every fish here by kind and age, and lets you pick them from the list.</i></size>", Rich());
                GUILayout.Space(4f);
                return;
            }
            var groups = new SortedDictionary<string, List<Fish>>();
            foreach (var fish in tank.Residents)
            {
                if (fish.IsDead) continue;
                string kind = fish.IdentityHidden ? "Mystery fry" : fish.SpeciesLabel;
                if (!groups.TryGetValue(kind, out var list)) groups[kind] = list = new List<Fish>();
                list.Add(fish);
            }
            if (groups.Count == 0)
            {
                Ui.Label("<size=11><i>No fish in this tank.</i></size>", Rich());
                GUILayout.Space(4f);
                return;
            }
            foreach (var pair in groups)
            {
                var list = pair.Value;
                list.Sort((a, b) => a.Sex != b.Sex ? b.Sex.CompareTo(a.Sex) : b.AgeDays.CompareTo(a.AgeDays));
                int males = 0, females = 0, adults = 0;
                foreach (var fish in list)
                {
                    if (fish.Stage == LifeStage.Adult) adults++;
                    if (fish.Stage == LifeStage.Fry) continue;
                    if (fish.Sex == Sex.Male) males++;
                    else females++;
                }
                Ui.Label($"<b>{pair.Key}</b> <size=11>x{list.Count}: {males}♂ {females}♀, {adults} adult{(adults == 1 ? "" : "s")}</size>", Rich());
                foreach (var fish in list)
                {
                    string sex = fish.Stage == LifeStage.Fry ? "?" : fish.Sex == Sex.Male ? "♂" : "♀";
                    string sick = fish.Health.IsSick ? " <color=#ff8080>+</color>" : "";
                    string label = $"<size=11>{sex} {fish.FishName}{sick}   {fish.Stage.ToString().ToLower()}, {fish.AgeDays:0.#} days</size>";
                    if (Ui.Button(label, selected.Contains(fish) ? StockRowSelected() : StockRow())) ToggleSelect(fish);
                }
            }
            GUILayout.Space(6f);
        }

        static GUIStyle stockRow, stockRowSelected;
        static GUIStyle StockRow() => stockRow != null ? stockRow : stockRow = new GUIStyle(GUI.skin.button) { richText = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 4, 2, 3) };
        static GUIStyle StockRowSelected() => stockRowSelected != null ? stockRowSelected : stockRowSelected = new GUIStyle(UiTheme.Primary) { richText = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 4, 2, 3) };

        /// <summary>The tank itself: its size, water type, decorations, background and type.</summary>
        void DrawTankTab()
        {
            GUI.enabled = !ReadOnly;
            DrawUpgrade();
            if (tank.Residents.Count == 0 && tank.Corpses.Count == 0)
            {
                var supplier = Balance.Data.supplier;
                int salt = Perks.Supplier(supplier.marineSaltPrice);
                string label = tank.IsSaltwater ? $"Change to fresh water ({Economy.Format(salt)})" : $"Change to salt water ({Economy.Format(salt)})";
                if (!tank.IsSaltwater && !Progression.IsUnlocked(XpTrack.Fishkeeping, supplier.marineSaltLevel))
                    Ui.Label($"<i>Salt water tanks: {Progression.LockText(XpTrack.Fishkeeping, supplier.marineSaltLevel)}</i>", Rich());
                else if (Ui.Button(label) && Economy.TrySpend(salt, "changing a tank's water", MoneyKind.Care)) tank.SetSaltwater(!tank.IsSaltwater);
            }
            DrawDecorations();
            DrawBackgroundSetup();
            DrawTankType();
            GUI.enabled = true;
#if UNITY_EDITOR
            if (!ReadOnly && Ui.Button("Dirty the water (dev)")) tank.DevMakeDirty();
            if (!ReadOnly && Ui.Button("Grow every fish up (dev)"))
                foreach (var fish in tank.Residents) fish.DevGrowUp();
#endif
        }

        bool devShowFish = true, devShowDecor;

        /// <summary>
        /// Developer: set the tank up directly, free. Water, equipment models (or none), any
        /// decoration in or out, and any fish (adult, a male and female pair, or a handful of fry).
        /// </summary>
        void DrawDevTab()
        {
            Ui.Label("<size=11><color=#ffd27f>Developer tools: everything here is free and instant.</color></size>", Rich());
            GUILayout.BeginHorizontal();
            if (Ui.Button("Clean water")) tank.DevMakeClean();
            if (Ui.Button("Dirty water")) tank.DevMakeDirty();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Ui.Button("Grow all up"))
                foreach (var fish in new List<Fish>(tank.Residents)) fish.DevGrowUp();
            if (Ui.Button("Remove all fish"))
            {
                foreach (var fish in new List<Fish>(tank.Residents)) fish.RemoveFromTank();
                foreach (var corpse in new List<Fish>(tank.Corpses)) corpse.RemoveFromTank();
                selected.Clear();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            Ui.Label("<b>Equipment</b>", Rich());
            foreach (var kind in Equipment.Kinds)
            {
                var item = tank.GetEquipment(kind);
                int current = item != null ? item.tier : -1;
                GUILayout.BeginHorizontal();
                Ui.Label(kind.ToString(), GUILayout.Width(56f));
                if (Ui.Button(new GUIContent("None", "Take it out"), current < 0 ? DevPrimary() : DevButton(), GUILayout.Width(44f))) tank.DevRemoveEquipment(kind);
                var tiers = Equipment.Tiers(kind);
                var fits = Equipment.TiersFor(kind, tank);
                for (int n = 0; n < fits.Count; n++)
                {
                    int t = fits[n];
                    if (Ui.Button(new GUIContent((n + 1).ToString(), tiers[t].name), current == t ? DevPrimary() : DevButton(), GUILayout.Width(28f))) tank.Install(kind, t);
                }
                GUILayout.EndHorizontal();
            }
            // Always there (tooltips only exist while repainting): a label that came and went would upset the layout.
            Ui.NamedLabel("Tooltip line", $"<size=11>{GUI.tooltip}</size>", Rich(), GUILayout.Height(18f));

            GUILayout.Space(4f);
            var decor = tank.Decor;
            // Folding opens or shuts from the next frame, so this one's layout stays as it was measured.
            bool showDecor = devShowDecor;
            if (Ui.Button($"{(devShowDecor ? "▾" : "▸")} <b>Decorations</b>  <size=11>{(decor != null ? $"{decor.Count} / {decor.Capacity}" : "")}</size>", SectionHeader())) devShowDecor = !devShowDecor;
            if (showDecor && decor != null)
            {
                var ids = decor.Ids;
                for (int i = 0; i < ids.Count; i++)
                {
                    var placed = TankDecor.Find(ids[i]);
                    GUILayout.BeginHorizontal();
                    Ui.Label(placed != null ? placed.name : ids[i]);
                    if (Ui.Button("x", GUILayout.Width(26f)))
                    {
                        decor.RemoveAt(i);
                        GUILayout.EndHorizontal();
                        break;
                    }
                    GUILayout.EndHorizontal();
                }
                Ui.Label("<size=11>Add:</size>", Rich());
                foreach (var item in SupplierCatalog.Decorations)
                {
                    GUI.enabled = !decor.IsFull;
                    if (Ui.Button($"+ {item.name}{(item.plant ? " (plant)" : "")}")) decor.Add(item.id);
                    GUI.enabled = true;
                }
            }

            GUILayout.Space(4f);
            bool showFish = devShowFish;
            if (Ui.Button($"{(devShowFish ? "▾" : "▸")} <b>Add fish</b>  <size=11>{tank.SmallCount} / {tank.Capacity}, medium {tank.MediumCount} / {tank.MediumCapacity}</size>", SectionHeader())) devShowFish = !devShowFish;
            if (!showFish) return;
            foreach (var species in Species.All)
            {
                GUILayout.BeginHorizontal();
                Ui.Label($"<size=11>{species.displayName}</size>", Rich(), GUILayout.Width(100f));
                if (Ui.Button(new GUIContent("+1", "One adult"), DevButton(), GUILayout.Width(30f))) DevSpawn(species, 1f, null);
                if (Ui.Button(new GUIContent("+pair", "A grown male and female"), DevButton(), GUILayout.Width(40f)))
                {
                    DevSpawn(species, 1f, Sex.Male);
                    DevSpawn(species, 1f, Sex.Female);
                }
                if (Ui.Button(new GUIContent("+5 fry", "Five fry of this kind (bred, so you can tell what they are)"), DevButton(), GUILayout.Width(50f)))
                    for (int i = 0; i < 5; i++) DevSpawn(species, 0.05f, null);
                GUILayout.EndHorizontal();
            }
        }

        void DevSpawn(Species species, float growth, Sex? sex)
        {
            // Generation 2 counts as bred here, so fry show what they are straight away.
            var fish = FishFactory.Spawn(Genome.CreateRandom(species), growth < 1f ? 2 : 1, growth, tank, tank.RandomFreePoint(0.1f));
            if (sex.HasValue) fish.DevSetSex(sex.Value);
        }

        // Small-padding buttons so the dev rows fit the panel.
        static GUIStyle devButton, devPrimary;
        static GUIStyle DevButton() => devButton != null ? devButton : devButton = new GUIStyle(GUI.skin.button) { fontSize = 11, padding = new RectOffset(1, 1, 3, 4) };
        static GUIStyle DevPrimary() => devPrimary != null ? devPrimary : devPrimary = new GUIStyle(UiTheme.Primary) { fontSize = 11, padding = new RectOffset(1, 1, 3, 4) };

        // A clickable heading that folds a section open or shut.
        static GUIStyle sectionHeader;
        static GUIStyle SectionHeader() => sectionHeader != null ? sectionHeader : sectionHeader = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleLeft };

        static GUIStyle tabStyle, narrowTabStyle;
        // Five tabs (with Dev) need a slightly smaller font to fit the panel.
        GUIStyle TabStyle() => TabCount > 4
            ? (narrowTabStyle != null ? narrowTabStyle : narrowTabStyle = new GUIStyle(GUI.skin.button) { richText = true, fontSize = 11, padding = new RectOffset(2, 2, 4, 5) })
            : (tabStyle != null ? tabStyle : tabStyle = new GUIStyle(GUI.skin.button) { richText = true, fontSize = 12, padding = new RectOffset(3, 3, 4, 5) });

        /// <summary>
        /// How many fish live here against the tank's limit. Going over the limit is allowed, up to
        /// the hard limit, but makes the water dirty faster, spreads illness and stresses every fish.
        /// </summary>
        void DrawFishCount(string sizeName)
        {
            int count = tank.SmallCount;
            string dead = tank.Corpses.Count > 0 ? $"   <color=#ff8080>Dead: {tank.Corpses.Count}</color>" : "";
            // Medium fish have their own limit; it shows where they can live or already do.
            int medium = tank.MediumCount;
            if (medium > 0 || tank.IsWallAquarium)
            {
                string mediumColor = medium >= tank.MediumCapacity ? "#ffd27f" : "#ffffff";
                dead = $"   <color={mediumColor}>Medium: {medium} / {tank.MediumCapacity}</color>" + dead;
            }
            if (tank.IsOvercrowded)
            {
                Ui.Label($"{sizeName}. <color=#ff8080><b>Fish: {count} / {tank.Capacity}</b></color>{dead}", Rich());
                Ui.Label($"<size=11><color=#ff9a8a>Overcrowded by {tank.OverLimit}: the water fouls faster, illness spreads and every fish here is stressed. Move some out.</color></size>", Rich());
            }
            else
            {
                string color = count == tank.Capacity ? "#ffd27f" : "#ffffff";
                Ui.Label($"{sizeName}. <color={color}><b>Fish: {count} / {tank.Capacity}</b></color>{dead}", Rich());
            }
        }

        /// <summary>
        /// The temperature range every fish here likes, counting only species whose range research has
        /// found. Null when nothing is known or the known ranges don't overlap.
        /// </summary>
        Vector2? KnownTemperatureRange(out bool allKnown)
        {
            allKnown = true;
            float low = float.MinValue, high = float.MaxValue;
            bool any = false;
            foreach (var fish in tank.Residents)
            {
                var species = fish.Species;
                if (fish.IdentityHidden || !Research.Knows(species, ResearchFact.Temperature))
                {
                    allKnown = false;
                    continue;
                }
                any = true;
                low = Mathf.Max(low, species.tempMin);
                high = Mathf.Min(high, species.tempMax);
            }
            if (!any || low > high) return null;
            return new Vector2(low, high);
        }

        /// <summary>One button per food you own. Fish eat any food, but each species has favourites.</summary>
        void DrawFeedButtons()
        {
            bool any = false;
            foreach (FoodType food in Enum.GetValues(typeof(FoodType)))
            {
                int left = Economy.FoodPortions(food);
                if (left <= 0) continue;
                any = true;
                var info = FoodPellet.Info(food);
                string name = info != null ? info.name : food.ToString();
                if (Ui.Button($"Feed {name.ToLower()} ({left} left)") && Economy.TryUseFood(food)) storeTank.Feeding.Feed(food, 8);
            }
            if (!any) Ui.Label("<color=#ff9a8a>No food! Order some at the supplier computer.</color>", Rich());
        }

        // ------------------------------------------------------------ upgrades

        readonly bool[] showModels = new bool[3];
        readonly bool[] modelsShown = new bool[3];

        /// <summary>
        /// The tank's filter, heater and bubbler: what's in now (or a warning about having none),
        /// and, clicked open, every model this tank can take. Models not unlocked yet are greyed
        /// out. Not every tank takes the same models: the wall aquarium needs its own.
        /// </summary>
        void DrawUpgradesTab()
        {
            if (Event.current.type == EventType.Layout)
                for (int i = 0; i < showModels.Length; i++) modelsShown[i] = showModels[i];
            GUI.enabled = !ReadOnly;
            Ui.Label("<size=11>Equipment wears out: the longer it runs, the likelier it breaks down at a time step. A broken one does nothing until it's repaired (Care tab). Better models work better, break less and raise the aquarium's quality, but cost more to fix.</size>", Rich());
            GUILayout.Space(4f);
            foreach (var kind in new[] { EquipmentKind.Heater, EquipmentKind.Filter, EquipmentKind.Bubbler })
                DrawEquipmentKind(kind);
            GUI.enabled = true;
        }

        void DrawEquipmentKind(EquipmentKind kind)
        {
            int k = (int)kind;
            var item = tank.GetEquipment(kind);
            string title = kind == EquipmentKind.Filter ? "Filter" : kind == EquipmentKind.Heater ? "Heater" : "Bubbler";
            string now = item == null ? $"<color={UiTheme.CoralHex}>No {title.ToLower()}</color>" : item.Name + (item.broken ? " <color=#ff8080>(broken)</color>" : "");
            bool wasEnabled = GUI.enabled;
            GUI.enabled = true;
            if (Ui.Button($"{(modelsShown[k] ? "▾" : "▸")} <b>{title}:</b> {now}", EquipmentHeader())) showModels[k] = !showModels[k];
            GUI.enabled = wasEnabled;
            if (item == null) Ui.Label($"<size=11><color=#ffb0a0>{MissingWarning(kind)}</color></size>", Rich());
            else if (item.broken) Ui.Label($"<size=11><color=#ff8080>{item.fault}. Repair it on the Care tab ({Economy.Format(item.RepairPrice)}).</color></size>", Rich());
            else Ui.Label($"<size=11>{EffectText(kind, item.Info)}. {item.BreakChance * 100f:0}% chance of breaking per time step.</size>", Rich());
            if (modelsShown[k])
            {
                var tiers = Equipment.Tiers(kind);
                var fits = Equipment.TiersFor(kind, tank);
                if (fits.Count == 0) Ui.Label("<size=11><i>No models for this tank yet.</i></size>", Rich());
                foreach (int tier in fits) DrawModelRow(kind, tier, tiers[tier], item);
                if (tank.IsWallAquarium) Ui.Label("<size=11><i>The wall aquarium is far too big for ordinary equipment: it takes plant-room systems of its own.</i></size>", Rich());
            }
            GUILayout.Space(6f);
        }

        /// <summary>What having none of this kind of equipment risks.</summary>
        string MissingWarning(EquipmentKind kind)
        {
            switch (kind)
            {
                case EquipmentKind.Filter:
                    return "Without a filter, fish waste builds up fast: the water gets dirty, algae grows and fish fall ill.";
                case EquipmentKind.Heater:
                    bool tropical = false;
                    foreach (var fish in tank.Residents)
                        if (fish != null && !fish.IsDead && fish.Species.tempMin > GameConfig.Active.roomTemperature) tropical = true;
                    return $"Without a heater the water sits at room temperature ({GameConfig.Active.roomTemperature:0} C). Tropical fish get cold, stressed and sick" + (tropical ? ", and some fish here already want it warmer." : "; cold-water fish like goldfish are fine.");
                default:
                    return $"Without a bubbler there's only enough air for {GameConfig.Active.fishPerTankWithoutBubbler} fish. Any more and they run short of oxygen, gasp and get sick.";
            }
        }

        static string EffectText(EquipmentKind kind, EquipmentTierBalance info)
        {
            if (info == null) return "";
            switch (kind)
            {
                case EquipmentKind.Filter: return $"Leaves {info.effect * 100f:0}% of the waste";
                case EquipmentKind.Heater: return info.effect > 0.05f ? $"Holds the setting within {info.effect:0.#} C" : "Holds the exact setting";
                default: return $"Air for {info.effect:0} fish";
            }
        }

        /// <summary>One model: its picture, what it does, and the button to put it in (greyed out until it's unlocked).</summary>
        void DrawModelRow(EquipmentKind kind, int tier, EquipmentTierBalance info, InstalledEquipment installed)
        {
            bool unlocked = Progression.IsUnlocked(XpTrack.Fishkeeping, info.unlockLevel);
            bool same = installed != null && installed.tier == tier;
            var quality = Balance.Data.quality;
            var old = GUI.color;
            if (!unlocked) GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.65f);
            GUILayout.BeginHorizontal(GUI.skin.box);
            var pictureRect = Ui.Slot("Picture", 40f, 40f, GUILayout.Width(40f), GUILayout.Height(40f));
            GUI.DrawTexture(pictureRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.2f, 0.24f, 0.3f, 0.9f), 0f, 4f);
            var picture = ItemPreviews.Equipment(kind, tier);
            if (picture != null) GUI.DrawTexture(pictureRect, picture, ScaleMode.ScaleToFit);
            GUILayout.BeginVertical();
            Ui.Label($"<b>{info.name}</b>" + (same ? $"  <color={UiTheme.LeafHex}>(in now)</color>" : ""), Rich());
            Ui.Label($"<size=10>{info.blurb} {EffectText(kind, info)}. Breaks {info.breakChance * 100f:0.#}% of time steps when new. Quality {quality.equipmentPoints + quality.pointsPerEquipmentTier * tier:0}.</size>", Rich());
            int price = Perks.Supplier(info.price);
            if (!unlocked) Ui.Label($"<size=11><color=#ffd27f>Needs {Progression.LockText(XpTrack.Fishkeeping, info.unlockLevel)}</color></size>", Rich());
            else
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && Economy.CanAfford(price);
                string label = installed == null ? $"Install  {Economy.Format(price)}" : same ? $"Replace with new  {Economy.Format(price)}" : $"Swap to this  {Economy.Format(price)}";
                if (Ui.Button(label, SmallButton()))
                {
                    string where = storeTank.Title;
                    Confirm(TankQuality.WarnIfDrops(tank, TankQuality.WhatIf.Equip(kind, tier), $"Swapping to the {info.name.ToLower()}"), () =>
                    {
                        if (!Economy.TrySpend(price, $"a {info.name.ToLower()} for {where}", MoneyKind.Equipment)) return;
                        tank.Install(kind, tier);
                        Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpBuyEquipment);
                        Milestones.Add(MilestoneKind.BuyEquipment);
                        GameLog.Add($"{info.name} installed in {where}.");
                    });
                }
                GUI.enabled = wasEnabled;
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUI.color = old;
        }

        static GUIStyle equipmentHeader;
        static GUIStyle EquipmentHeader() => equipmentHeader != null ? equipmentHeader : equipmentHeader = new GUIStyle(GUI.skin.button) { richText = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 6, 5, 6) };

        /// <summary>A repair button for each broken piece of equipment.</summary>
        void DrawRepairs()
        {
            foreach (var kind in Equipment.Kinds)
            {
                var item = tank.GetEquipment(kind);
                if (item == null || !item.broken) continue;
                Ui.Label($"<color=#ff8080><b>{item.Name}: broken!</b></color>\n<size=11>{item.fault}.</size>", Rich());
                GUI.enabled = !ReadOnly && Economy.CanAfford(item.RepairPrice);
                if (Ui.Button($"Repair it  {Economy.Format(item.RepairPrice)}")) tank.Repair(kind);
                GUI.enabled = !ReadOnly;
            }
        }

        /// <summary>A red warning box listing every sick fish.</summary>
        void DrawSickList()
        {
            var sick = new List<string>();
            foreach (var fish in tank.Residents)
                if (!fish.IsDead && fish.Health.IsSick)
                    sick.Add($"{fish.FishName}: {fish.Health.Disease.displayName}" + (Research.Has(ResearchUnlock.Pathology) ? $" ({fish.Health.Severity * 100f:0}%)" : ""));
            if (sick.Count == 0) return;
            UiGauges.Badge(sick.Count == 1 ? "+ 1 SICK FISH" : $"+ {sick.Count} SICK FISH", SickRed);
            foreach (var line in sick) Ui.Label($"<color=#ff9a9a>{line}</color>", Rich());
            Ui.Label("<size=11>Dose the tank with medicine (Care), or move them to quarantine.</size>", Rich());
        }

        static readonly Color SickRed = new Color(0.85f, 0.18f, 0.2f);

        /// <summary>The cure a disease needs, in words.</summary>
        static string CureText(DiseaseDefinition disease)
        {
            return disease.cure == Medicine.None ? "no medicine: it usually passes with rest and light feeding" : $"{disease.cure.ToString().ToLower()} medicine";
        }

        /// <summary>
        /// Medicine goes into the whole tank: it cures every fish with the illness it treats, but
        /// stresses the rest, and too many doses close together damage fish for good.
        /// </summary>
        void DrawMedicine()
        {
            var supplier = Balance.Data.supplier;
            if (tank.Residents.Count == 0) return;
            if (!Progression.IsUnlocked(XpTrack.Fishkeeping, supplier.medicineLevel))
            {
                Ui.Label($"<i>Medicine: {Progression.LockText(XpTrack.Fishkeeping, supplier.medicineLevel)}</i>", Rich());
                return;
            }
            int sick = 0;
            foreach (var fish in tank.Residents) if (fish.Health.IsSick) sick++;
            Ui.Label($"Medicine for the whole tank ({Economy.Format(Perks.Supplier(supplier.medicinePrice))} a dose)" + (sick > 0 ? $": <color=#ff8080>{sick} sick</color>" : ": no one is sick"), Rich());
            GUILayout.BeginHorizontal();
            foreach (Medicine medicine in new[] { Medicine.Antiviral, Medicine.Antibacterial, Medicine.Antiparasitic })
                if (Ui.Button(medicine.ToString(), SmallButton(), GUILayout.MinWidth(50f))) DoseTank(medicine);
            GUILayout.EndHorizontal();
        }

        static GUIStyle smallButton;
        static GUIStyle SmallButton() => smallButton != null ? smallButton : smallButton = new GUIStyle(GUI.skin.button) { fontSize = 11, padding = new RectOffset(4, 4, 3, 3) };

        void DoseTank(Medicine medicine)
        {
            int price = Perks.Supplier(Balance.Data.supplier.medicinePrice);
            if (!Economy.TrySpend(price, $"{medicine.ToString().ToLower()} for {storeTank.Title}", MoneyKind.Care)) return;
            int cured = 0, stressed = 0;
            foreach (var fish in new List<Fish>(tank.Residents))
                if (fish.Health.ReceiveMedicine(medicine) == FishHealth.MedicineResult.Cured) cured++;
                else stressed++;
            if (cured > 0) Milestones.Add(MilestoneKind.CureFish, cured);
            GameLog.Add($"{medicine} went into {storeTank.Title}: " + (cured > 0 ? $"{cured} cured" : "nobody cured") + (stressed > 0 ? $", {stressed} fish stressed by medicine they didn't need." : "."));
        }

        /// <summary>Buttons to step to the previous and next tank (the arrow keys do the same when no fish is picked).</summary>
        void DrawTankBar()
        {
            var previous = neighbourTank != null && switchTank != null ? neighbourTank(-1) : null;
            var next = neighbourTank != null && switchTank != null ? neighbourTank(1) : null;
            UiCheck.Begin("Tank switch bar", tankBar);
            GUILayout.BeginHorizontal();
            if (previous != null && Ui.Button($"< {previous.Title}", GUILayout.Height(30f))) switchTank(-1);
            GUILayout.FlexibleSpace();
            if (Ui.Button(new GUIContent(TopView ? "Front view (T)" : "Top view (T)", TopView ? "Look in through the front glass" : "Look straight down into the tank"), GUILayout.Height(30f))) ToggleTopView();
            GUILayout.FlexibleSpace();
            if (next != null && Ui.Button($"{next.Title} >", GUILayout.Height(30f))) switchTank(1);
            GUILayout.EndHorizontal();
            UiCheck.End();
        }

        /// <summary>Heater thermostat: a slider, the range the fish here need, and a one-click best setting.</summary>
        void DrawHeater()
        {
            GUILayout.Space(2f);
            Ui.Label($"<b>Heater: {tank.HeaterSetting:0} C</b>", Rich());
            GUILayout.BeginHorizontal();
            if (Ui.Button("-", GUILayout.Width(26f))) tank.SetHeater(tank.HeaterSetting - 1f);
            float setting = Ui.HorizontalSlider(tank.HeaterSetting, 18f, 32f, GUILayout.ExpandWidth(true));
            if (!Mathf.Approximately(Mathf.Round(setting), tank.HeaterSetting)) tank.SetHeater(setting);
            if (Ui.Button("+", GUILayout.Width(26f))) tank.SetHeater(tank.HeaterSetting + 1f);
            GUILayout.EndHorizontal();
            if (tank.Residents.Count > 0)
            {
                bool allKnown;
                var known = KnownTemperatureRange(out allKnown);
                bool anyKnown = known.HasValue;
                if (!anyKnown)
                    foreach (var fish in tank.Residents)
                        if (!fish.IdentityHidden && Research.Knows(fish.Species, ResearchFact.Temperature)) anyKnown = true;
                string need;
                if (known.HasValue) need = $"Fish here are happy at {known.Value.x:0}-{known.Value.y:0} C" + (allKnown ? "." : ", as far as you know.");
                else if (anyKnown) need = "<color=#ffd27f>These fish don't share a temperature: some will be uncomfortable.</color>";
                else need = null;
                if (need != null) Ui.Label($"<size=11>{need}</size>", Rich());
                if (known.HasValue)
                {
                    float best = Mathf.Round((known.Value.x + known.Value.y) * 0.5f);
                    if (!Mathf.Approximately(best, tank.HeaterSetting) && Ui.Button($"Set to {best:0} C for these fish")) tank.SetHeater(best);
                }
            }
        }

        /// <summary>The decorations: a button that opens the picker beside the panel, and one to move them about.</summary>
        void DrawDecorations()
        {
            var decor = tank.Decor;
            if (decor == null) return;
            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            if (Ui.Button($"Decorations  {decor.Count} / {decor.Capacity}", pickerShown == Picker.Decorations ? UiTheme.Primary : GUI.skin.button)) TogglePicker(Picker.Decorations);
            if (decor.Count > 0 && Ui.Button(new GUIContent("Move", "Move decorations (top view)"), GUILayout.Width(64f))) StartArranging(ArrangeMode.Decorations);
            GUILayout.EndHorizontal();
        }

        /// <summary>The background: a button that opens the picker beside the panel, and one to adjust the picture.</summary>
        void DrawBackgroundSetup()
        {
            var background = TankBackgrounds.Find(tank.BackgroundId);
            GUILayout.BeginHorizontal();
            if (Ui.Button(background != null ? $"Background: {background.name}" : "Background: none", pickerShown == Picker.Background ? UiTheme.Primary : GUI.skin.button)) TogglePicker(Picker.Background);
            if (background != null && Ui.Button(new GUIContent("Adjust", "Slide and zoom the picture"), GUILayout.Width(64f))) StartArranging(ArrangeMode.Background);
            GUILayout.EndHorizontal();
            // A wall aquarium's sides run deep into the wall and show from the room: they take a picture too.
            if (!tank.IsWallAquarium) return;
            var sides = TankBackgrounds.Find(tank.SideBackgroundId);
            if (Ui.Button(sides != null ? $"Sides: {sides.name}" : "Sides: none", pickerShown == Picker.Sides ? UiTheme.Primary : GUI.skin.button)) TogglePicker(Picker.Sides);
        }

        // ------------------------------------------------------------ decoration and background pickers

        enum Picker { None, Decorations, Background, Sides }
        // Asked for, and showing (changed only on Layout, so each frame's layout matches).
        Picker picker, pickerShown;
        Vector2 pickerScroll;
        Rect pickerRect;

        void TogglePicker(Picker which)
        {
            picker = picker == which ? Picker.None : which;
            pickerScroll = Vector2.zero;
        }

        /// <summary>The open picker, as a window beside the tank panel (only on the Tank tab).</summary>
        void DrawPicker(float height)
        {
            if (Event.current.type == EventType.Layout) pickerShown = tab == TankTab.Tank && Arranging == ArrangeMode.None ? picker : Picker.None;
            if (pickerShown == Picker.None)
            {
                pickerRect = Rect.zero;
                return;
            }
            string id = pickerShown == Picker.Decorations ? "Decoration picker" : pickerShown == Picker.Sides ? "Side background picker" : "Background picker";
            float want = UiCheck.Height(id, 320f);
            pickerRect = new Rect(leftPanel.xMax + 8f, leftPanel.y, 270f, Mathf.Clamp(want, 120f, Mathf.Max(120f, height - leftPanel.y - 160f)));
            UiCheck.Begin(id, pickerRect, UiStyles.Window);
            GUILayout.BeginHorizontal();
            Ui.Label(pickerShown == Picker.Decorations ? "<b>Your decorations</b>" : pickerShown == Picker.Sides ? "<b>Backgrounds for the sides</b>" : "<b>Your backgrounds</b>", Rich());
            GUILayout.FlexibleSpace();
            if (Ui.Button("✕", SmallButton(), GUILayout.Width(24f))) picker = Picker.None;
            GUILayout.EndHorizontal();
            DrawConfirm();
            pickerScroll = UiCheck.BeginScroll(id + ": list", pickerScroll);
            GUI.enabled = !ReadOnly;
            if (pickerShown == Picker.Decorations) DrawDecorationPicker();
            else if (pickerShown == Picker.Sides) DrawSideBackgroundPicker();
            else DrawBackgroundPicker();
            GUI.enabled = true;
            UiCheck.EndScroll();
            UiCheck.End();
        }

        /// <summary>Decorations in stock to put in, then the ones in this tank to take out (back into stock).</summary>
        void DrawDecorationPicker()
        {
            var decor = tank.Decor;
            if (decor == null) return;
            bool any = false;
            foreach (var pair in new List<KeyValuePair<string, int>>(Inventory.AllDecorations))
            {
                any = true;
                var item = TankDecor.Find(pair.Key);
                string name = item != null ? item.name : pair.Key;
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && !decor.IsFull;
                if (PickerRow(ItemPreviews.Decoration(pair.Key), 40f, $"{name}  <size=11>x{pair.Value}</size>{LikedBy(pair.Key)}", decor.IsFull ? "Full" : "Put in")
                    && Inventory.TakeDecoration(pair.Key))
                {
                    if (decor.Add(pair.Key)) GameLog.Add($"Put a {name.ToLower()} in {storeTank.Title}.");
                    else Inventory.AddDecoration(pair.Key);
                }
                GUI.enabled = wasEnabled;
            }
            if (!any) Ui.Label("<size=11><i>None in stock. Buy them at the supplier computer.</i></size>", Rich());

            var ids = decor.Ids;
            if (ids.Count == 0) return;
            GUILayout.Space(6f);
            Ui.Label("<size=11><b>In this tank</b></size>", Rich());
            for (int i = 0; i < ids.Count; i++)
            {
                var item = TankDecor.Find(ids[i]);
                string name = item != null ? item.name : ids[i];
                if (!PickerRow(ItemPreviews.Decoration(ids[i]), 28f, $"<size=12>{name}</size>{LikedBy(ids[i])}", "Take out")) continue;
                int index = i;
                string id = ids[i], lower = name.ToLower();
                Confirm(TankQuality.WarnIfDrops(tank, TankQuality.WhatIf.RemoveDecoration(index), $"Taking out the {lower}"), () =>
                {
                    if (index >= decor.Count || decor.IdAt(index) != id) return;
                    decor.RemoveAt(index);
                    Inventory.AddDecoration(id);
                    GameLog.Add($"Took the {lower} out of {storeTank.Title}. It's back in your stock.");
                });
            }
        }

        /// <summary>The picture up now (to take down, back into stock), then the ones in stock to put up.</summary>
        void DrawBackgroundPicker()
        {
            var background = TankBackgrounds.Find(tank.BackgroundId);
            if (background != null && PickerRow(TankBackgrounds.Texture(background.id), 40f, $"{background.name}  <size=11>(up now)</size>", "Take down"))
            {
                string id = background.id, name = background.name.ToLower();
                Confirm(TankQuality.WarnIfDrops(tank, TankQuality.WhatIf.RemoveBackground, $"Taking down the {name} background"), () =>
                {
                    if (tank.BackgroundId != id) return;
                    tank.SetBackground("");
                    Inventory.AddBackground(id);
                    GameLog.Add($"Took the {name} background down in {storeTank.Title}. It's back in your stock.");
                });
            }
            bool any = false;
            foreach (var pair in new List<KeyValuePair<string, int>>(Inventory.AllBackgrounds))
            {
                var item = TankBackgrounds.Find(pair.Key);
                if (item == null) continue;
                any = true;
                if (!PickerRow(TankBackgrounds.Texture(pair.Key), 40f, $"{item.name}  <size=11>x{pair.Value}</size>", background != null ? "Swap" : "Put up")
                    || !Inventory.TakeBackground(pair.Key)) continue;
                if (background != null) Inventory.AddBackground(background.id);
                tank.SetBackground(pair.Key);
                GameLog.Add($"Put the {item.name.ToLower()} background up in {storeTank.Title}.");
            }
            if (!any) Ui.Label("<size=11><i>None in stock. Buy them at the supplier computer.</i></size>", Rich());
        }

        /// <summary>
        /// A wall aquarium's sides: one picture from stock covers both side glasses. The one up now
        /// (to take down, back into stock), then the ones in stock to put up or swap in.
        /// </summary>
        void DrawSideBackgroundPicker()
        {
            Ui.Label("<size=11><i>One picture covers both side glasses.</i></size>", Rich());
            var sides = TankBackgrounds.Find(tank.SideBackgroundId);
            if (sides != null && PickerRow(TankBackgrounds.Texture(sides.id), 40f, $"{sides.name}  <size=11>(up now)</size>", "Take down"))
            {
                Inventory.AddBackground(sides.id);
                tank.SetSideBackground("");
                GameLog.Add($"Took the {sides.name.ToLower()} pictures off the sides of {storeTank.Title}. It's back in your stock.");
            }
            bool any = false;
            foreach (var pair in new List<KeyValuePair<string, int>>(Inventory.AllBackgrounds))
            {
                var item = TankBackgrounds.Find(pair.Key);
                if (item == null) continue;
                any = true;
                if (!PickerRow(TankBackgrounds.Texture(pair.Key), 40f, $"{item.name}  <size=11>x{pair.Value}</size>", sides != null ? "Swap" : "Put up")
                    || !Inventory.TakeBackground(pair.Key)) continue;
                if (sides != null) Inventory.AddBackground(sides.id);
                tank.SetSideBackground(pair.Key);
                GameLog.Add($"Put the {item.name.ToLower()} picture up on the sides of {storeTank.Title}.");
            }
            if (!any) Ui.Label("<size=11><i>None in stock. Buy them at the supplier computer.</i></size>", Rich());
        }

        /// <summary>One row of a picker: a picture, a name and a button. True when the button is clicked.</summary>
        bool PickerRow(Texture picture, float size, string label, string button)
        {
            GUILayout.BeginHorizontal();
            var rect = Ui.Slot("Picture", size * 1.4f, size, GUILayout.Width(size * 1.4f), GUILayout.Height(size));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.2f, 0.24f, 0.3f, 0.9f), 0f, 4f);
                if (picture != null) GUI.DrawTexture(rect, picture, ScaleMode.ScaleAndCrop);
            }
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            Ui.Label(label, Rich());
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(68f));
            GUILayout.FlexibleSpace();
            bool clicked = Ui.Button(button, SmallButton());
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            return clicked;
        }

        /// <summary>A second line naming the fish here that like it, once the plants research says who likes what.</summary>
        string LikedBy(string decorationId)
        {
            var kinds = new List<string>();
            foreach (var fish in tank.Residents)
            {
                if (fish == null || fish.IsDead || fish.IdentityHidden || !fish.Species.LikesDecoration(decorationId)) continue;
                if (!Research.Knows(fish.Species, ResearchFact.Plants)) continue;
                string name = Research.Plural(fish.Species);
                if (!kinds.Contains(name)) kinds.Add(name);
            }
            if (kinds.Count == 0) return "";
            return $"\n<color={UiTheme.LeafHex}><size=10>{string.Join(", ", kinds.ToArray())} like it</size></color>";
        }

        /// <summary>Turning the tank into the other type for its room (for sale and display, or breeder and quarantine), for a fee.</summary>
        void DrawTankType()
        {
            if (tankStore == null) return;
            GUILayout.Space(2f);
            Ui.Label($"Tank type: <b>{StoreTank.RoleName(storeTank.Role)}</b>", Rich());
            bool wasEnabled = GUI.enabled;
            foreach (var role in StoreLayout.OtherRoles(storeTank))
            {
                var reason = tankStore.WhyCantChangeRole(storeTank, role);
                GUI.enabled = wasEnabled && reason == null;
                if (Ui.Button($"Make it a {StoreTank.RoleName(role).ToLower()} tank  {Economy.Format(StoreLayout.ChangeRolePrice)}")) tankStore.TryChangeRole(storeTank, role);
                GUI.enabled = wasEnabled;
                if (reason != null && reason != StoreLayout.NoMoney) Ui.Label($"<size=11><color=#ffd27f>{reason}</color></size>", Rich());
            }
            Ui.Label($"<size=11>{RoleHint(storeTank.Role)}</size>", Rich());
        }

        static string RoleHint(TankRole role)
        {
            switch (role)
            {
                case TankRole.ForSale: return "Customers buy the fish in for sale tanks.";
                case TankRole.Display: return "Display tanks aren't for sale: they're there to wow customers.";
                case TankRole.Breeder: return "Pairs breed best in breeder tanks.";
                default: return "Sick fish get better apart in quarantine.";
            }
        }

        StoreLayout tankStore;
        /// <summary>The store the tanks stand in, for changing a tank's type.</summary>
        public void SetStore(StoreLayout layout) => tankStore = layout;

        // ------------------------------------------------------------ arranging decorations and the background

        public enum ArrangeMode { None, Decorations, Background }

        /// <summary>Moving decorations about (seen from above) or sliding the background picture.</summary>
        public ArrangeMode Arranging { get; private set; }
        int heldPiece = -1, dragPiece = -1;
        Vector3 dragOffset, lastDragPoint;
        bool draggingPicture;

        void StartArranging(ArrangeMode mode)
        {
            if (ReadOnly) return;
            selected.Clear();
            ZoomPoint = null;
            Arranging = mode;
            heldPiece = dragPiece = -1;
            draggingPicture = false;
            picker = Picker.None;
        }

        /// <summary>Each Smaller or Bigger press changes a wall aquarium decoration's size by this factor.</summary>
        const float DecorSizeStep = 1.15f;

        public void StopArranging()
        {
            Arranging = ArrangeMode.None;
            heldPiece = dragPiece = -1;
            draggingPicture = false;
        }

        void DrawArrangePanel()
        {
            if (Arranging == ArrangeMode.Decorations)
            {
                var decor = tank.Decor;
                Ui.Label("MOVE DECORATIONS", UiTheme.Heading);
                Ui.Label("Seen from above. Drag a decoration to move it. Q and E, the mouse wheel or the buttons turn the one you last moved." + (tank.IsWallAquarium ? " Here in the wall aquarium, - and + make it smaller or bigger." : "") + " Nothing can go past the glass.", Rich());
                GUILayout.Space(4f);
                string held = decor != null ? decor.IdAt(heldPiece) : null;
                var item = held != null ? TankDecor.Find(held) : null;
                Ui.Label(item != null ? $"Holding: <b>{item.name}</b>" : "<i>Pick one up to move or turn it.</i>", Rich());
                GUILayout.BeginHorizontal();
                GUI.enabled = held != null;
                if (Ui.Button("Turn left (Q)")) decor.Turn(heldPiece, -15f);
                if (Ui.Button("Turn right (E)")) decor.Turn(heldPiece, 15f);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                if (tank.IsWallAquarium)
                {
                    // Only the wall aquarium is big enough for pieces of different sizes.
                    GUILayout.Space(2f);
                    Ui.Label(held != null ? $"Size: <b>{Mathf.RoundToInt(decor.SizeAt(heldPiece) * 100f)}%</b>" : "Size: <i>pick one up first</i>", Rich());
                    GUILayout.BeginHorizontal();
                    GUI.enabled = held != null;
                    if (Ui.Button("Smaller (-)")) decor.Resize(heldPiece, 1f / DecorSizeStep);
                    if (Ui.Button("Bigger (+)")) decor.Resize(heldPiece, DecorSizeStep);
                    if (Ui.Button("100%", GUILayout.Width(48f))) decor.Resize(heldPiece, 1f / Mathf.Max(0.01f, decor.SizeAt(heldPiece)));
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
                GUI.enabled = decor != null && decor.MovedCount > 0;
                if (Ui.Button("Back to automatic spots"))
                {
                    decor.ResetPlacement();
                    heldPiece = -1;
                }
                GUI.enabled = true;
            }
            else
            {
                Ui.Label("ADJUST THE BACKGROUND", UiTheme.Heading);
                Ui.Label("Drag the picture to slide it, and zoom with the mouse wheel or the slider. It always covers the whole back glass.", Rich());
                GUILayout.Space(4f);
                GUILayout.BeginHorizontal();
                Ui.Label("Zoom", GUILayout.Width(44f));
                float zoom = Ui.HorizontalSlider(tank.BackgroundZoom, 1f, Tank.MaxBackgroundZoom, GUILayout.ExpandWidth(true));
                if (!Mathf.Approximately(zoom, tank.BackgroundZoom)) tank.SetBackgroundCrop(tank.BackgroundCenter, zoom);
                Ui.Label($"{tank.BackgroundZoom:0.0}x", GUILayout.Width(36f));
                GUILayout.EndHorizontal();
                if (Ui.Button("Centre it, as wide as it fits")) tank.SetBackgroundCrop(new Vector2(0.5f, 0.5f), 1f);
            }
            GUILayout.FlexibleSpace();
            if (Ui.Button("Done (Esc)", UiTheme.Primary, GUILayout.Height(30f))) StopArranging();
        }

        /// <summary>
        /// Mouse work while arranging: picking up, dragging and turning decorations on the floor of
        /// the tank, or dragging and zooming the picture on the back glass. True if it used the event.
        /// </summary>
        bool HandleArranging()
        {
            if (Arranging == ArrangeMode.None) return false;
            var e = Event.current;
            var m = e.mousePosition;
            bool overMenu = leftPanel.Contains(m) || logPanel.Contains(m) || tankBar.Contains(m) || pickerRect.Contains(m);
            var ray = RayAt(m);
            if (Arranging == ArrangeMode.Decorations)
            {
                var decor = tank.Decor;
                if (decor == null) return false;
                var floor = new Plane(Vector3.up, tank.transform.position);
                if (!floor.Raycast(ray, out float hitAt)) return false;
                var point = ray.GetPoint(hitAt);
                if (e.type == EventType.MouseDown && e.button == 0 && !overMenu)
                {
                    int index = -1;
                    var hits = Physics.RaycastAll(ray, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (var hit in hits)
                    {
                        index = decor.IndexOf(hit.collider);
                        if (index >= 0) break;
                    }
                    if (index < 0) index = decor.Nearest(point, Mathf.Max(0.2f, tank.InnerSize.y * 0.25f));
                    heldPiece = dragPiece = index;
                    if (index >= 0)
                    {
                        var c = decor.BoundsAt(index).center;
                        dragOffset = new Vector3(c.x - point.x, 0f, c.z - point.z);
                    }
                    e.Use();
                    return true;
                }
                if (e.type == EventType.MouseDrag && dragPiece >= 0)
                {
                    decor.MoveTo(dragPiece, point + dragOffset);
                    e.Use();
                    return true;
                }
                if (e.rawType == EventType.MouseUp) dragPiece = -1;
                if (e.type == EventType.ScrollWheel && heldPiece >= 0 && !overMenu)
                {
                    decor.Turn(heldPiece, e.delta.y > 0f ? 15f : -15f);
                    e.Use();
                    return true;
                }
                return false;
            }

            // The background: points on the back glass.
            var size = tank.InnerSize;
            var glass = new Plane(tank.Front, tank.InnerBounds.center - tank.Front * (size.z * 0.5f));
            if (!glass.Raycast(ray, out float at)) return false;
            var onGlass = ray.GetPoint(at);
            if (e.type == EventType.MouseDown && e.button == 0 && !overMenu)
            {
                draggingPicture = true;
                lastDragPoint = onGlass;
                e.Use();
                return true;
            }
            if (e.type == EventType.MouseDrag && draggingPicture)
            {
                var moved = onGlass - lastDragPoint;
                lastDragPoint = onGlass;
                tank.DragBackground(new Vector2(Vector3.Dot(moved, tank.transform.right), moved.y));
                e.Use();
                return true;
            }
            if (e.rawType == EventType.MouseUp) draggingPicture = false;
            if (e.type == EventType.ScrollWheel && !overMenu)
            {
                tank.SetBackgroundCrop(tank.BackgroundCenter, tank.BackgroundZoom * (e.delta.y > 0f ? 0.85f : 1.18f));
                e.Use();
                return true;
            }
            return false;
        }

        Ray RayAt(Vector2 guiPoint)
        {
            var screen = GUI.matrix.MultiplyPoint3x4(guiPoint);
            return cam.ScreenPointToRay(new Vector3(screen.x, Screen.height - screen.y, 0f));
        }

        /// <summary>A box round the decoration being moved, so it's clear which one is held.</summary>
        void DrawHeldMarker(float scale, float guiHeight)
        {
            if (Arranging != ArrangeMode.Decorations || heldPiece < 0 || tank.Decor == null || cam == null) return;
            var b = tank.Decor.BoundsAt(heldPiece);
            if (b.size == Vector3.zero) return;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = cam.WorldToScreenPoint(corner);
                if (p.z <= 0f) return;
                minX = Mathf.Min(minX, p.x / scale);
                maxX = Mathf.Max(maxX, p.x / scale);
                minY = Mathf.Min(minY, guiHeight - p.y / scale);
                maxY = Mathf.Max(maxY, guiHeight - p.y / scale);
            }
            var r = new Rect(minX - 4f, minY - 4f, maxX - minX + 8f, maxY - minY + 8f);
            var c = UiTheme.Sun;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, c, 0f, 0f);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, c, 0f, 0f);
            GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, c, 0f, 0f);
            GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, c, 0f, 0f);
        }

        /// <summary>The next size up: what it holds, which fish need it, and the button once the quality is there.</summary>
        void DrawUpgrade()
        {
            int next = tank.SizeIndex + 1;
            var info = StoreLayout.SizeInfo(tank, next);
            if (info == null || upgradeTank == null) return;
            bool wall = tank.IsWallAquarium;
            Ui.Label(wall
                ? $"<size=11>{info.name}: raised to {info.height:0.0#} m tall, {Tank.CapacityOf(info)} fish and {Tank.MediumCapacityOf(info)} medium fish, {info.decorationSlots} decorations.</size>"
                : $"<size=11>{info.name} tank: {Tank.CapacityOf(info)} fish, {info.decorationSlots} decorations.</size>", Rich());
            var reason = StoreLayout.WhyCantUpgrade(storeTank, next);
            if (reason != null && reason != StoreLayout.NoMoney)
            {
                Ui.Label($"<size=11><color=#ffd27f>{reason}</color></size>", Rich());
                return;
            }
            bool wasEnabled = GUI.enabled;
            int price = Perks.Upgrade(info.upgradePrice);
            GUI.enabled = wasEnabled && reason == null;
            if (Ui.Button(wall ? $"Raise to {info.height:0.0#} m  {Economy.Format(price)}" : $"Upgrade to {info.name.ToLower()} tank  {Economy.Format(price)}")) upgradeTank(storeTank, next);
            GUI.enabled = wasEnabled;
        }

        /// <summary>A bar of this height in the slot beside a label, centred on the label's text so the two line up.</summary>
        static Rect BarBeside(Rect label, Rect slot, float height)
        {
            float middle = label.height > 1f ? label.center.y : slot.center.y;
            return new Rect(slot.x, middle - height * 0.5f, slot.width, height);
        }

        static void Bar(string label, float value, bool highIsGood)
        {
            GUILayout.BeginHorizontal();
            Ui.Label(label, Rich(), GUILayout.Width(95f));
            var rect = UiLayout.Item("Tank panel: " + label.Replace("<b>", "").Replace("</b>", "") + " bar", BarBeside(GUILayoutUtility.GetLastRect(), GUILayoutUtility.GetRect(60f, 14f, GUILayout.ExpandWidth(true)), 12f));
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.35f), 0f, 0f);
            float good = highIsGood ? value : 1f - value;
            var fill = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height);
            GUI.DrawTexture(fill, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.Lerp(new Color(0.9f, 0.3f, 0.25f), new Color(0.35f, 0.85f, 0.4f), good), 0f, 0f);
            Ui.Label($"{value * 100f:0}%", GUILayout.Width(40f));
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------ fish window

        void DrawFishWindow(Fish fish, float scale, float width, float height)
        {
            const float w = 320f;
            float h = Mathf.Clamp(height - 110f, 300f, 560f);
            var screen = cam.WorldToScreenPoint(fish.transform.position);
            var anchor = new Vector2(screen.x / scale, height - screen.y / scale);
            float x = anchor.x + 90f;
            if (x + w > width - 10f) x = anchor.x - 90f - w;
            x = Mathf.Clamp(x, leftPanel.xMax + 10f, width - w - 10f);
            float y = Mathf.Clamp(anchor.y - h * 0.4f, 10f, height - h - 70f);
            fishWindow = new Rect(x, y, w, h);
            if (fishScrollFor != fish)
            {
                fishScrollFor = fish;
                fishScroll = Vector2.zero;
            }

            UiCheck.Begin("Fish window", fishWindow, UiStyles.Window);
            DrawFishHeader(fish);

            if (fish.IsDead)
            {
                Ui.Label($"<color=#ff8080><b>Dead</b> ({fish.CauseOfDeath})</color>", Rich());
                Ui.Label("Dead fish slowly make the rest of the tank sick and upset customers.");
                GUI.enabled = !ReadOnly;
                if (Ui.Button("Remove from the tank"))
                {
                    selected.Remove(fish);
                    fish.RemoveFromTank();
                    Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpRemoveDeadFish);
                }
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
                if (Ui.Button("Close (Esc)")) selected.Clear();
                UiCheck.End();
                return;
            }

            // Everything about the fish scrolls; the header and the move buttons stay put.
            fishScroll = UiCheck.BeginScroll("Fish window: details", fishScroll);
            DrawFishHealth(fish);
            DrawFishNeeds(fish);
            DrawFishBreeding(fish);

            Section("Value");
            Ui.Label($"Appraised value: <b>~{Economy.Format(fish.AppraisedValue)}</b>", Rich());
            if (!fish.IdentityHidden)
                Ui.Label($"Attractiveness: <b>{fish.AttractivenessRating}</b>/100  <color={UiTheme.SunHex}>{AttractivenessStars(fish)}</color>\n<size=11>Eye-catching fish bring people in, get looked at more and sell more easily.</size>", Rich());
            if (storeTank.Role == TankRole.ForSale) DrawPrice(fish);
            if (fish.Health.IsSick)
            {
                Ui.Label($"<size=11>Treat the tank with medicine on the Care tab. It goes to every fish in the water.</size>", Rich());
                DrawEuthanize(fish);
            }
#if UNITY_EDITOR
            if (!ReadOnly && !fish.IsDead && Ui.Button("Next illness (dev)")) fish.Health.DevNextIllness();
#endif
            UiCheck.EndScroll();

            DrawMoveButtons(fish);
            if (Ui.Button("Close (Esc)")) selected.Clear();
            UiCheck.End();
        }

        /// <summary>Name, then sex and age as bold coloured badges so they stand out, then the species.</summary>
        void DrawFishHeader(Fish fish)
        {
            var species = fish.Species;
            Ui.Label($"<size=17><b>{fish.FishName}</b></size>", Rich());
            GUILayout.BeginHorizontal();
            if (fish.Stage == LifeStage.Fry) UiGauges.Badge("Sex: ?", new Color(0.4f, 0.42f, 0.46f));
            else if (fish.Sex == Sex.Male) UiGauges.Badge("♂ Male", new Color(0.2f, 0.45f, 0.85f));
            else UiGauges.Badge("♀ Female", new Color(0.85f, 0.3f, 0.6f));
            switch (fish.Stage)
            {
                case LifeStage.Adult: UiGauges.Badge("ADULT", new Color(0.2f, 0.6f, 0.3f)); break;
                case LifeStage.Juvenile: UiGauges.Badge("JUVENILE", new Color(0.75f, 0.55f, 0.15f)); break;
                default: UiGauges.Badge("FRY", new Color(0.45f, 0.47f, 0.5f)); break;
            }
            if (fish.Stage != LifeStage.Fry) UiGauges.Badge(fish.Size.ToString().ToUpper(), new Color(0.3f, 0.45f, 0.55f));
            if (fish.Health.IsSick && !fish.IsDead) UiGauges.Badge("+ SICK", Color.Lerp(SickRed, new Color(1f, 0.45f, 0.4f), Mathf.PingPong(Time.unscaledTime * 1.5f, 1f)));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (fish.Health.IsSick && !fish.IsDead)
            {
                var disease = fish.Health.Disease;
                GUILayout.BeginVertical(SickBox());
                bool known = Research.Has(ResearchUnlock.Pathology);
                Ui.Label($"<b>{disease.displayName}</b>" + (known ? $"  <size=11>({fish.Health.Severity * 100f:0}% bad)</size>" : ""), Rich());
                if (!string.IsNullOrEmpty(disease.description)) Ui.Label($"<size=11>{disease.description}</size>", Rich());
                if (known) Ui.Label($"<size=11>Cure: {CureText(disease)}.</size>", Rich());
                GUILayout.EndVertical();
            }
            if (fish.IdentityHidden) Ui.Label("<b>Mystery fry</b>: too small to tell what it is yet. It shows its colours when it grows into a juvenile.", Rich());
            else
            {
                var look = fish.Phenotype;
                if (look.IsHybrid)
                {
                    Ui.Label($"<b>{fish.SpeciesLabel}</b>  <size=11>(cross-bred, {look.RarityLabel} looks)</size>", Rich());
                    Ui.Label($"<size=11>Body: {species.displayName} ({species.rarity}), fins: {look.finSpecies.displayName}, tail: {look.tailSpecies.displayName}. It lives like a {species.displayName}.</size>", Rich());
                }
                else Ui.Label($"<b>{species.displayName}</b>  <size=11>({species.rarity} species, {look.RarityLabel} looks)</size>", Rich());
                if (!string.IsNullOrEmpty(species.family)) Ui.Label($"<size=11><i>{species.scientificName}</i>, {species.family}</size>", Rich());
            }
            string growing = fish.Stage == LifeStage.Adult ? "" : $", grown up in about {Mathf.Max(0.5f, (1f - fish.Growth) * species.daysToAdult):0.#} days";
            Ui.Label($"<size=11>{fish.AgeDays:0.#} days old{growing}, generation {fish.Generation}</size>", Rich());
            if (!string.IsNullOrEmpty(fish.ParentNames)) Ui.Label($"<size=11>Parents: {fish.ParentNames}</size>", Rich());
            if (fish.IsPregnant)
                Ui.Label($"<color=#ffb3d9><b>Pregnant</b> by {fish.UnbornFather}: about {fish.UnbornCount} fry due in {BreedingSystem.Steps(fish.StepsToBirth)}</color>", Rich());
        }

        /// <summary>
        /// Health now, where it is heading at the next time step, and every factor as its own line
        /// with the details underneath, one per line.
        /// </summary>
        void DrawFishHealth(Fish fish)
        {
            var species = fish.Species;
            var report = fish.Health.Report();
            float now = fish.Health.Current;
            Section("Health");
            Bar("<b>Health</b>", now, true);
            float change = report.total - now;
            if (Mathf.Abs(change) >= 0.01f)
            {
                float next = Mathf.MoveTowards(now, report.total, Balance.Data.health.healthChangePerStep);
                Ui.Label($"<size=11><color={(change > 0f ? "#9fe39f" : "#ff9a8a")}>{(change > 0f ? "Rising" : "Falling")} to {next * 100f:0}% at the next time step</color>" +
                                (Mathf.Abs(next - report.total) > 0.01f ? $" (heading for {report.total * 100f:0}%)" : "") + "</size>", Rich());
            }
            else Ui.Label("<size=11>Steady: health only changes at time steps (closing up and overnight).</size>", Rich());

            Penalty("Hunger", report.hunger, $"{fish.Hunger * 100f:0}% hungry");
            Penalty("Illness", report.illness, fish.Health.IsSick ? $"{fish.Health.Disease.displayName} ({fish.Health.Severity * 100f:0}%)" : "healthy");
            Penalty("Water", report.environment, report.environmentMismatch < 0.01f ? "suits it" : "not right for it");
            foreach (var note in FishHealth.EnvironmentNotes(species, tank)) SubLine(note, 0f, true);
            Penalty("Comfort", report.comfort, $"{report.comfortLevel * 100f:0}% comfortable");
            foreach (var note in fish.Health.ComfortNotes()) SubLine(note.text, note.delta, false);
            if (report.overcrowding > 0f) Penalty("Crowding", report.overcrowding, $"{tank.OverLimit} fish over the tank's limit");
            if (report.injury > 0f) Penalty("Wounds", report.injury, string.IsNullOrEmpty(fish.Health.InjuryCause) ? "healing" : fish.Health.InjuryCause);
            if (report.medicineDamage > 0f) Penalty("Medicine", report.medicineDamage, $"overdosed {fish.Health.MedicineDamage}x" + (fish.Health.Infertile ? ", can't breed" : ""));
            if (report.companionship > 0f) Ui.Label("<color=#9fe39f>Good company</color>" + (Research.Has(ResearchUnlock.HealthDetails) ? $"<color=#9fe39f>  +{report.companionship * 100f:0}</color>" : ""), Rich());
            if (report.oldAge > 0f) Penalty("Old age", report.oldAge, Research.Knows(species, ResearchFact.Lifespan) ? $"past {species.lifespanDays:0} days" : "getting old");
            if (report.deadTankmates > 0f) Penalty("Dead fish", report.deadTankmates, $"{tank.Corpses.Count} in the tank");
        }

        /// <summary>What the species needs, as far as research has found out: gauges for temperature and pH, then the rest.</summary>
        void DrawFishNeeds(Fish fish)
        {
            if (fish.IdentityHidden) return;
            var species = fish.Species;
            Section("What it needs");
            bool tempKnown = Research.Knows(species, ResearchFact.Temperature);
            Ui.Label("Temperature" + (tempKnown ? $": likes {species.tempMin:0}-{species.tempMax:0} C" : ""), Rich());
            UiGauges.Temperature(tank.Temperature, tempKnown ? new Vector2(species.tempMin, species.tempMax) : (Vector2?)null);
            if (Research.CanReadPh)
            {
                bool phKnown = Research.Knows(species, ResearchFact.Ph);
                Ui.Label("pH" + (phKnown ? $": likes {species.phMin:0.0}-{species.phMax:0.0}" : ""), Rich());
                UiGauges.Ph(tank.Ph, phKnown ? new Vector2(species.phMin, species.phMax) : (Vector2?)null);
            }
            SubLine(species.IsSaltwater ? "Salt water" : species.salinityMax > 5f ? "Fresh or slightly salty water" : "Fresh water", 0f, false);
            if (Research.Knows(species, ResearchFact.School))
                SubLine(species.schoolSize > 0 ? $"Wants {species.schoolSize}+ of its own kind" : "Happy without others of its kind", 0f, false);
            if (Research.Knows(species, ResearchFact.Plants))
                SubLine(species.likesPlants ? "Loves plants" : "Doesn't care for plants", 0f, false);
            var known = new List<string>();
            foreach (var food in species.favoriteFoods)
                if (Research.KnowsFavourite(species, food)) known.Add(FoodPellet.Name(food));
            if (known.Count > 0) SubLine($"Favourite food: {string.Join(", ", known)}" + (known.Count < species.favoriteFoods.Count ? " (maybe more)" : ""), 0f, false);
            if (Ui.Button($"Journal: {species.displayName}")) JournalView.Open(species.id);
        }

        void DrawFishBreeding(Fish fish)
        {
            if (fish.IsPregnant || fish.IdentityHidden) return;
            var species = fish.Species;
            Section("Breeding");
            string notReady = BreedingSystem.WhyNotReady(fish, tank);
            string how = Research.Knows(species, ResearchFact.Breeding) ? (species.reproduction == Reproduction.Livebearer ? " (livebearer)" : " (egg-layer)") : "";
            Ui.Label(notReady == null
                ? $"<color=#9fe39f>Ready to breed</color>{how}: needs a {(fish.Sex == Sex.Male ? "female" : "male")} {species.displayName} in the tank."
                : Research.Has(ResearchUnlock.Breeding) ? $"Not ready{how}: {notReady.Replace(fish.FishName + " ", "")}" : "Not ready to breed.", Rich());
        }

        /// <summary>Move buttons for every other tank, with how full each is; afterwards the next fish here is picked.</summary>
        Fish euthanizeAsk;

        /// <summary>For a sick fish: put it to sleep humanely (asks once to make sure).</summary>
        void DrawEuthanize(Fish fish)
        {
            GUI.enabled = !ReadOnly;
            if (euthanizeAsk != fish)
            {
                if (Ui.Button(new GUIContent("Euthanize...", "Put it to sleep humanely, so it stops suffering and can't pass its illness on"))) euthanizeAsk = fish;
            }
            else
            {
                Ui.Label($"<size=11>Put {fish.FishName} to sleep? It's taken out of the tank straight away. This can't be undone.</size>", Rich());
                GUILayout.BeginHorizontal();
                if (Ui.Button("Yes, put it to sleep", UiTheme.Primary))
                {
                    var next = NextLivingFish(fish);
                    selected.Remove(fish);
                    euthanizeAsk = null;
                    fish.PutToSleep();
                    if (next != null && next != fish) Select(next);
                }
                if (Ui.Button("Cancel")) euthanizeAsk = null;
                GUILayout.EndHorizontal();
            }
            GUI.enabled = true;
        }

        void DrawMoveButtons(Fish fish)
        {
            GUI.enabled = !ReadOnly;
            Ui.Label("Move to:");
            GUILayout.BeginHorizontal();
            int column = 0;
            foreach (var other in allTanks)
            {
                if (other == storeTank) continue;
                if (column == 2)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    column = 0;
                }
                bool wasEnabled = GUI.enabled;
                var t = other.Tank;
                bool tooSmall = fish.MinTankSize > t.SizeIndex;
                bool medium = fish.Species.IsMedium;
                GUI.enabled = wasEnabled && t.HasRoomFor(fish.Species) && !tooSmall;
                int have = medium ? t.MediumCount : t.SmallCount, limit = medium ? t.MediumCapacity : t.Capacity;
                string count = tooSmall ? "<color=#ff9a8a>too small</color>" : have >= limit ? $"<color=#ff9a8a>{have}/{limit}</color>" : $"{have}/{limit}";
                if (Ui.Button($"{other.Title} ({count})", RichButton(), GUILayout.Width(146f)))
                {
                    var next = NextLivingFish(fish);
                    if (fish.MoveTo(t))
                    {
                        selected.Remove(fish);
                        GameLog.Add($"Moved {fish.FishName} to {other.Title}." + (t.IsOvercrowded ? $" {other.Title} is over its limit now!" : ""));
                        if (next != null) Select(next);
                    }
                }
                GUI.enabled = wasEnabled;
                column++;
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        /// <summary>The living fish after this one in the tank (wrapping round), or null if it's the only one.</summary>
        Fish NextLivingFish(Fish fish)
        {
            var living = tank.Residents;
            int index = -1;
            for (int i = 0; i < living.Count; i++) if (living[i] == fish) index = i;
            if (living.Count < 2 || index < 0) return null;
            return living[(index + 1) % living.Count];
        }

        static void Section(string title)
        {
            GUILayout.Space(6f);
            Ui.Label($"<b><color={UiTheme.SunHex}>{title.ToUpper()}</color></b>", Rich());
        }

        /// <summary>An indented line under a heading; with a delta it shows the comfort it adds or takes.</summary>
        static void SubLine(string text, float delta, bool bad)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(14f);
            string color = bad || delta < -0.001f ? "#ffb0a0" : delta > 0.001f ? "#b5ecb5" : "#d8dde4";
            Ui.Label($"<size=12><color={color}>• {text}</color></size>", Rich());
            if (Mathf.Abs(delta) > 0.001f && Research.Has(ResearchUnlock.HealthDetails))
            {
                Ui.Label($"<size=12><color={color}>{(delta > 0f ? "+" : "-")}{Mathf.Abs(delta) * 100f:0}</color></size>", Rich(), GUILayout.Width(34f));
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>Little arrow buttons either side of the selected fish to step through the tank.</summary>
        void DrawStepButtons(Fish fish, float scale, float height)
        {
            if (fish == null || cam == null) return;
            var screen = cam.WorldToScreenPoint(fish.transform.position);
            if (screen.z <= 0f) return;
            var gui = new Vector2(screen.x / scale, height - screen.y / scale);
            stepLeft = new Rect(gui.x - 78f, gui.y - 16f, 34f, 32f);
            stepRight = new Rect(gui.x + 44f, gui.y - 16f, 34f, 32f);
            if (Ui.Button(stepLeft, "<b><</b>", StepStyle())) Cycle(-1);
            if (Ui.Button(stepRight, "<b>></b>", StepStyle())) Cycle(1);
        }

        /// <summary>Attractiveness as five stars, the empty ones dimmed.</summary>
        public static string AttractivenessStars(Fish fish)
        {
            int full = Mathf.Clamp(Mathf.RoundToInt(fish.Attractiveness * 5f), 0, 5);
            return new string('★', full) + "<color=#5f8a92>" + new string('★', 5 - full) + "</color>";
        }

        void DrawPrice(Fish fish)
        {
            if (priceTextFor != fish)
            {
                priceTextFor = fish;
                priceText = fish.Price.ToString();
            }
            GUI.enabled = !ReadOnly;
            GUILayout.BeginHorizontal();
            Ui.Label("Price: $", GUILayout.Width(52f));
            priceText = Ui.TextField(priceText, 6, GUILayout.Width(60f));
            if (int.TryParse(priceText, out var typed) && typed != fish.Price) fish.Price = typed;
            if (Ui.Button("-5", GUILayout.Width(34f))) SetPrice(fish, fish.Price - 5);
            if (Ui.Button("+5", GUILayout.Width(34f))) SetPrice(fish, fish.Price + 5);
            if (Ui.Button("Appraised")) SetPrice(fish, fish.AppraisedValue);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (fish.Stage == LifeStage.Fry) Ui.Label("<i>Too young to sell: customers only buy juveniles and adults.</i>", Rich());
            else if (fish.Price <= 0) Ui.Label("<i>No price: customers can't buy it.</i>", Rich());
        }

        void SetPrice(Fish fish, int price)
        {
            fish.Price = price;
            priceText = fish.Price.ToString();
        }

        static void Penalty(string label, float penalty, string detail)
        {
            GUILayout.BeginHorizontal();
            var color = penalty < 0.02f ? "#9fe39f" : penalty < 0.15f ? "#ffd27f" : "#ff8080";
            Ui.Label($"<color={color}>{label}</color>", Rich(), GUILayout.Width(80f));
            Ui.Label(penalty < 0.02f || !Research.Has(ResearchUnlock.HealthDetails) ? detail : $"{detail}  <color={color}>-{penalty * 100f:0}</color>", Rich());
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------ breeding

        void DrawBreedBar()
        {
            UiCheck.Begin("Breed bar", breedBar, UiStyles.Window);
            var a = selected[0];
            var b = selected[1];
            if (storeTank.Role != TankRole.Breeder)
            {
                Ui.Label($"{a.FishName} and {b.FishName}: move them to a breeder tank to pair them up. Compatible pairs in other tanks sometimes breed by chance at each time step.", Rich());
            }
            else
            {
                bool ok = storeTank.Breeding.CanBreed(a, b, out var reason) && !ReadOnly;
                GUI.enabled = ok;
                if (Ui.Button($"♥ Breed {a.FishName} and {b.FishName}", UiTheme.Primary)) storeTank.Breeding.Breed(a, b);
                GUI.enabled = true;
                if (!string.IsNullOrEmpty(reason)) Ui.Label($"<i>{reason}</i>", Rich());
            }
            UiCheck.End();
        }

        void DrawLog() => GameLogPanel.Draw("Tank log", logPanel, 6);

        static GUIStyle sickBox;

        /// <summary>Dark red panel behind the illness details in the fish window.</summary>
        static GUIStyle SickBox()
        {
            if (sickBox != null && sickBox.normal.background != null) return sickBox;
            var background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            background.SetPixel(0, 0, new Color(0.45f, 0.08f, 0.1f, 0.9f));
            background.Apply();
            sickBox = new GUIStyle { padding = new RectOffset(8, 8, 6, 6), margin = new RectOffset(0, 0, 4, 4) };
            sickBox.normal.background = background;
            return sickBox;
        }

        /// <summary>A pulsing red "+" bubble over every sick fish in the tank, with what it has.</summary>
        void DrawSickMarkers(float scale, float guiHeight)
        {
            if (cam == null) return;
            foreach (var fish in tank.Residents)
            {
                if (fish.IsDead || !fish.Health.IsSick || selected.Contains(fish)) continue;
                var screen = cam.WorldToScreenPoint(fish.transform.position + Vector3.up * 0.05f);
                if (screen.z < 0f) continue;
                var gui = new Vector2(screen.x / scale, guiHeight - screen.y / scale);
                float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f + fish.GetInstanceID());
                float size = 20f * pulse;
                var old = GUI.color;
                GUI.color = SickRed;
                GUI.DrawTexture(new Rect(gui.x - size * 0.5f, gui.y - size - 6f, size, size), UiTheme.Circle);
                GUI.color = Color.white;
                GUI.Label(new Rect(gui.x - 15f, gui.y - size - 7f - (size - 20f) * 0.5f, 30f, 22f), "+", CenteredLabel());
                GUI.color = new Color(1f, 0.75f, 0.75f);
                var name = new GUIContent($"<size=11>{fish.Health.Disease.displayName}</size>");
                float nameWidth = CenteredRich().CalcSize(name).x + 4f;
                GUI.Label(new Rect(gui.x - nameWidth * 0.5f, gui.y - 6f, nameWidth, 18f), name, CenteredRich());
                GUI.color = old;
            }
        }

        static GUIStyle centeredRich;
        static GUIStyle CenteredRich() => centeredRich != null ? centeredRich : centeredRich = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };

        void DrawSelectionMarkers(float scale, float guiHeight)
        {
            if (cam == null) return;
            foreach (var fish in selected)
            {
                var screen = cam.WorldToScreenPoint(fish.transform.position + Vector3.up * 0.08f);
                if (screen.z < 0f) continue;
                var gui = new Vector2(screen.x / scale, guiHeight - screen.y / scale);
                float bob = Mathf.Sin(Time.unscaledTime * 4f) * 3f;
                var old = GUI.color;
                var label = new GUIContent($"▼ {fish.FishName}");
                float w = CenteredLabel().CalcSize(label).x + 4f;
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                GUI.Label(new Rect(gui.x - w * 0.5f + 1f, gui.y - 21f + bob, w, 22f), label, CenteredLabel());
                GUI.color = UiTheme.Sun;
                GUI.Label(new Rect(gui.x - w * 0.5f, gui.y - 22f + bob, w, 22f), label, CenteredLabel());
                GUI.color = old;
            }
        }

        /// <summary>Picks a fish (two make a breeding pair), or lets go of it if it was picked.</summary>
        void ToggleSelect(Fish fish)
        {
            ZoomPoint = null;
            if (selected.Contains(fish)) selected.Remove(fish);
            else
            {
                if (selected.Count == 2 || fish.IsDead || (selected.Count == 1 && selected[0].IsDead)) selected.Clear();
                selected.Add(fish);
            }
        }

        void HandleClick()
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || cam == null || UiLayout.Editing) return;
            var m = e.mousePosition;
            if (leftPanel.Contains(m) || fishWindow.Contains(m) || breedBar.Contains(m) || logPanel.Contains(m) || stepLeft.Contains(m) || stepRight.Contains(m)
                || pickerRect.Contains(m) || levelPopup.Contains(m)) return;

            // IMGUI coordinates are top-left based and scaled; convert back to screen pixels.
            var guiToScreen = GUI.matrix.MultiplyPoint3x4(m);
            var ray = cam.ScreenPointToRay(new Vector3(guiToScreen.x, Screen.height - guiToScreen.y, 0f));
            Fish fish = null;
            if (Physics.Raycast(ray, out var hit, 50f)) fish = hit.collider.GetComponentInParent<Fish>();
            if (fish == null || fish.Tank != tank) fish = NearestFishOnScreen(new Vector2(guiToScreen.x, Screen.height - guiToScreen.y));
            if (fish != null)
            {
                ToggleSelect(fish);
                e.Use();
                return;
            }
            // Empty water: zoom in on that spot, as close as following a fish. Anywhere else zooms back out.
            selected.Clear();
            ZoomPoint = WaterPoint(ray);
            e.Use();
        }

        /// <summary>Where a click ray meets the water, on the upright plane through the tank's middle (null if it misses the water).</summary>
        Vector3? WaterPoint(Ray ray)
        {
            var bounds = tank.InnerBounds;
            var plane = new Plane(tank.Front, bounds.center);
            if (!plane.Raycast(ray, out float distance)) return null;
            var point = ray.GetPoint(distance);
            bounds.Expand(0.04f);
            if (!bounds.Contains(point)) return null;
            return tank.Clamp(point, 0.12f, 0.08f);
        }

        /// <summary>Small fish and fry are hard to hit with a ray, so a click near one on screen also selects it.</summary>
        Fish NearestFishOnScreen(Vector2 screenPoint)
        {
            float best = Mathf.Max(40f, Screen.height * 0.06f);
            Fish nearest = null;
            foreach (var list in new[] { tank.Residents, tank.Corpses })
            {
                foreach (var f in list)
                {
                    if (f == null) continue;
                    var p = cam.WorldToScreenPoint(f.transform.position);
                    if (p.z <= 0f) continue;
                    float d = Vector2.Distance(screenPoint, new Vector2(p.x, p.y));
                    if (d < best) { best = d; nearest = f; }
                }
            }
            return nearest;
        }

        static GUIStyle richLabel, centeredLabel, stepStyle, richButton;

        static GUIStyle RichButton()
        {
            if (richButton == null) richButton = new GUIStyle(GUI.skin.button) { richText = true };
            return richButton;
        }

        static GUIStyle StepStyle()
        {
            if (stepStyle == null) stepStyle = new GUIStyle(GUI.skin.button) { richText = true, fontSize = 18 };
            return stepStyle;
        }

        static GUIStyle Rich()
        {
            if (richLabel == null) richLabel = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            return richLabel;
        }

        static GUIStyle CenteredLabel()
        {
            if (centeredLabel == null) centeredLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            return centeredLabel;
        }
    }
}
