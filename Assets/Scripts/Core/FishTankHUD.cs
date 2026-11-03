using System;
using System.Collections.Generic;
using FishBreeder.Breeding;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.Health;
using FishBreeder.Store;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishBreeder.Core
{
    /// <summary>
    /// The screen for looking into one tank: the tank's water, equipment, heater, decorations
    /// and size on the left, a status window next to the selected fish (the camera follows it).
    /// With a fish selected, left/right arrows or the little buttons beside it step through the
    /// fish in the tank; with none selected they move to the next tank, as do the buttons at the
    /// bottom. While the shop is open everything is view-only. UI Toolkit: the layout is
    /// Resources/UI/TankHud.uxml, styled in FishBreeder.uss.
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
        string priceText = "";
        Fish priceTextFor;
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
        public bool MouseOverMenu => enabled && tank != null && UiPanels.PointerOverUi;

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
            // Clicks in the water: picking fish, or picking up decorations and the background while arranging.
            if (tank != null && cam != null && !HandleArranging() && Arranging == ArrangeMode.None) HandleClick();
            if (tank == null || UiPanels.Typing) return;
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
                // The panel is 300 wide from 10 in, plus a little breathing room, on a screen 900 units tall.
                return Mathf.Clamp01(320f * Screen.height / UiPanels.ReferenceHeight / Screen.width);
            }
        }

        // ------------------------------------------------------------ screen (UI Toolkit)
        //
        // Resources/UI/TankHud.uxml: the tank menu on the left, the picker beside it, the fish
        // window, the bar with the tank buttons, the log, the level details and the marks over fish.
        // How they look is in FishBreeder.uss; this code fills them each frame.

        UIDocument hudDocument;
        VisualElement hudRoot, markersView, panelView, pickerView, fishView, tankBarView, logView;
        Label levelPopupView;
        UiFrame ui = new UiFrame();
        Button levelHelp;
        VisualElement levelBadge;
        ScrollView panelScrollView, fishScrollView;

        void Awake()
        {
            hudDocument = UiPanels.Open("Tank HUD", "TankHud", transform, 6);
            hudRoot = hudDocument.rootVisualElement;
            markersView = hudRoot.Q("markers");
            panelView = hudRoot.Q("tank-panel");
            pickerView = hudRoot.Q("picker");
            fishView = hudRoot.Q("fish-window");
            tankBarView = hudRoot.Q("tank-bar");
            logView = hudRoot.Q("log");
            GameLogView.MakeClickable(logView);
            levelPopupView = hudRoot.Q<Label>("level-popup");
            UiPanels.Show(hudRoot, false);
        }

        void OnEnable()
        {
            if (hudRoot != null) UiPanels.Show(hudRoot, true);
        }

        void OnDisable()
        {
            if (hudRoot != null) UiPanels.Show(hudRoot, false);
        }

        void OnDestroy()
        {
            if (hudDocument != null) Destroy(hudDocument.gameObject);
        }

        /// <summary>The panel's size in UI units (900 tall).</summary>
        Vector2 PanelSize => hudRoot.panel != null ? hudRoot.panel.visualTree.layout.size : new Vector2(1600f, 900f);

        /// <summary>Where a point in the tank is on the screen; false when it's behind the camera.</summary>
        bool ToPanel(Vector3 world, out Vector2 point)
        {
            var screen = cam.WorldToScreenPoint(world);
            point = UiPanels.ScreenToPanel(hudRoot, screen);
            return screen.z > 0f;
        }

        Vector2 MouseOnPanel => UiPanels.ScreenToPanel(hudRoot, Input.mousePosition);

        /// <summary>After the camera has moved, so the marks over fish sit on this frame's view.</summary>
        void LateUpdate()
        {
            if (tank == null || cam == null) return;
            bool watching = ViewMode.Watching;
            UiPanels.Show(hudRoot, true);
            Fill(panelView, !watching, DrawTankPanel);
            pickerShown = !watching && tab == TankTab.Tank && Arranging == ArrangeMode.None ? picker : Picker.None;
            Fill(pickerView, pickerShown != Picker.None, DrawPicker);
            bool fishShown = !watching && Focus != null;
            Fill(fishView, fishShown, () => DrawFishWindow(Focus));
            Fill(tankBarView, !watching, DrawTankBar);
            Fill(logView, !watching, () => GameLogPanel.Fill(ui, 6));
            Fill(markersView, !watching, DrawMarkers);
            DrawLevelPopup(!watching);
            PlaceTankBar();
        }

        void Fill(VisualElement part, bool show, Action draw)
        {
            UiPanels.Show(part, show);
            ui.Begin(part);
            if (show) draw();
            ui.End();
            if (show && part.childCount == 0) UiPanels.Show(part, false);
        }

        /// <summary>Beside the log when there's room; in a narrow window, just above it instead.</summary>
        void PlaceTankBar()
        {
            var size = PanelSize;
            float logWidth = Mathf.Min(460f, size.x - 20f);
            logView.style.width = logWidth;
            float barWidth = Mathf.Min(460f, size.x - 20f);
            float barX = Mathf.Max(10f + logWidth + 10f, size.x * 0.5f - barWidth * 0.5f);
            if (barX + barWidth <= size.x - 10f)
            {
                tankBarView.style.left = barX;
                tankBarView.style.width = barWidth;
                tankBarView.style.bottom = 10f;
            }
            else
            {
                barX = Mathf.Max(320f, size.x - 10f - barWidth);
                tankBarView.style.left = barX;
                tankBarView.style.width = size.x - 10f - barX;
                tankBarView.style.bottom = 156f;
            }
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
        /// needs doing. The aquarium's level sits in a circle by the title on every tab: click it, or
        /// the "?" beside it, for what makes it up.
        /// </summary>
        bool renaming;
        string renameText = "";
        TankTab scrolledTab;

        void DrawTankPanel()
        {
            ui.BeginRow("fb-row fb-tank-title");
            if (renaming)
            {
                renameText = ui.TextField(renameText, 28, "fb-field fb-grow");
                if (ui.Button("OK", "fb-button fb-button--tiny")) { storeTank.Rename(renameText); renaming = false; }
            }
            else
            {
                ui.Label($"<b>{storeTank.Title}</b>", "fb-text fb-tank-title__name");
                if (!ReadOnly && ui.Button("✎", "fb-button fb-button--tiny", true, "Rename this aquarium")) { renaming = true; renameText = storeTank.Title; }
            }
            ui.Label($"  {Economy.Format(Economy.Money)}", "fb-text fb-header-text");
            ui.Flex();
            DrawLevelBadge();
            if (ui.Button("?", "fb-button fb-button--tiny", true, "What the aquarium's level means")) showLevelInfo = !showLevelInfo;
            levelHelp = (Button)ui.Last;
            ui.Pop();
            if (Arranging != ArrangeMode.None)
            {
                DrawArrangePanel();
                return;
            }
            if (ReadOnly) ui.Label("<color=#ffd27f>The shop is open: you can look, but tank work waits until closing.</color>");
            if ((int)tab >= TabCount) tab = TankTab.Tank;
            var labels = new string[TabCount];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = TabNames[i] + (TabNeedsYou((TankTab)i) ? $" <color={UiTheme.CoralHex}>●</color>" : "");
            tab = (TankTab)ui.Tabs((int)tab, labels, "fb-tabs fb-tank-tabs", "fb-button fb-tab fb-tank-tab");
            panelScrollView = ui.BeginScroll("fb-scroll fb-window__body");
            if (scrolledTab != tab)
            {
                scrolledTab = tab;
                panelScrollView.scrollOffset = Vector2.zero;
            }
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
            ui.Pop();
            ui.BeginRow("fb-row fb-pair");
            if (ui.Button("Journal (J)", "fb-button fb-grow")) JournalView.Toggle();
            if (ui.Button(ResearchView.ButtonLabel(), "fb-button fb-grow")) ResearchView.Toggle();
            ui.Pop();
            ui.BeginRow("fb-row fb-pair");
            if (ui.Button(Perks.TotalAvailable > 0 ? $"Perks (P) ●{Perks.TotalAvailable}" : "Perks (P)", "fb-button fb-grow")) PerksView.Toggle();
            if (ui.Button("Watch (V)", "fb-button fb-grow")) ViewMode.Set(true);
            ui.Pop();
            ui.BeginRow("fb-row fb-pair");
            if (ui.Button("Finances (F)", "fb-button fb-grow")) FinancesView.Toggle();
            if (ui.Button("Reputation (Y)", "fb-button fb-grow")) ReputationView.Toggle();
            ui.Pop();
            if (onBack != null && ui.Button("Back to store (Esc)")) onBack();
        }

        // ------------------------------------------------------------ aquarium level

        bool showLevelInfo;

        /// <summary>The aquarium's level in a circle: coral when fish here need a higher one.</summary>
        void DrawLevelBadge()
        {
            int level = TankQuality.Level(tank);
            var color = FishWantingHigherLevel() > 0 ? UiTheme.Coral : level >= TankQuality.MaxLevel ? UiTheme.Sun : UiTheme.Aqua;
            if (ui.Button($"<b>{level}</b>", "fb-button fb-level-badge")) showLevelInfo = !showLevelInfo;
            levelBadge = ui.Last;
            levelBadge.style.backgroundColor = color;
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
        /// outside the panel, opened and closed by clicking the circle or the "?".
        /// </summary>
        void DrawLevelPopup(bool menus)
        {
            bool show = menus && showLevelInfo && Arranging == ArrangeMode.None && levelHelp != null && levelHelp.panel != null;
            UiPanels.Show(levelPopupView, show);
            if (!show) return;
            levelPopupView.text = LevelInfoText();
            var anchor = levelHelp.worldBound;
            var size = PanelSize;
            const float boxWidth = 250f;
            float x = anchor.xMax + 8f;
            // Clear of the picker when one is open beside the panel.
            if (UiPanels.Showing(pickerView)) x = Mathf.Max(x, pickerView.worldBound.xMax + 8f);
            if (x + boxWidth > size.x - 8f) x = Mathf.Max(8f, anchor.x - boxWidth);
            float h = levelPopupView.resolvedStyle.height;
            levelPopupView.style.left = x;
            levelPopupView.style.top = Mathf.Clamp(anchor.y, 8f, Mathf.Max(8f, size.y - (float.IsNaN(h) ? 0f : h) - 8f));
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
            if (panelScrollView != null) panelScrollView.scrollOffset = Vector2.zero;
        }

        void DrawConfirm()
        {
            if (confirmText == null) return;
            ui.BeginCard(false, "fb-card fb-card--column fb-warning-box");
            ui.Label($"<color={UiTheme.SunHex}><b>Are you sure?</b></color>\n<size=11>{confirmText}</size>");
            ui.BeginRow("fb-row fb-pair");
            if (ui.Button("Do it anyway", "fb-button fb-button--primary fb-grow", !ReadOnly))
            {
                var action = confirmAction;
                confirmText = null;
                confirmAction = null;
                action?.Invoke();
            }
            if (ui.Button("Cancel", "fb-button fb-grow"))
            {
                confirmText = null;
                confirmAction = null;
            }
            ui.Pop();
            ui.Pop();
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
            bool can = !ReadOnly;
            DrawFeedButtons(can);
            DrawCleaning(can);
            DrawTreatWater(can);
            if (tank.Corpses.Count > 0 && ui.Button($"Remove dead fish ({tank.Corpses.Count})", "fb-button", can))
            {
                int count = tank.Corpses.Count;
                foreach (var corpse in new List<Fish>(tank.Corpses)) corpse.RemoveFromTank();
                Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpRemoveDeadFish * count);
                GameLog.Add("You netted out the dead fish.");
            }
            DrawSickList();
            DrawMedicine(can);
            DrawRepairs(can);
            if (tank.HasHeater)
            {
                DrawHeater(can);
                DrawTemperature();
            }

            ui.Label("TANK", "fb-heading");
            DrawTankSpecs();
        }

        /// <summary>Fish, eggs, water, temperature and water type.</summary>
        void DrawTankSpecs()
        {
            DrawFishCount(tank.IsWallAquarium ? tank.SizeInfo.name : tank.SizeInfo.name + " tank");
            int unhappy = FishWantingHigherLevel();
            if (unhappy > 0)
                ui.Label($"<color={UiTheme.CoralHex}>{unhappy} fish need a higher aquarium level (see the circle by the title).</color>", "fb-small");
            int eggs = EggClutch.EggsIn(tank);
            int pregnant = 0;
            foreach (var fish in tank.Residents) if (fish.IsPregnant) pregnant++;
            if (eggs > 0 || pregnant > 0)
                ui.Label($"<color=#ffe9a8>{(eggs > 0 ? $"{eggs} eggs" : "")}{(eggs > 0 && pregnant > 0 ? ", " : "")}{(pregnant > 0 ? $"{pregnant} pregnant" : "")} (see Breeding)</color>");
            Bar("Water quality", tank.WaterQuality, true);
            DrawAlgae();
            Bar("Oxygen", tank.Oxygen, true);
            if (!tank.HasHeater) DrawTemperature();
            ui.Label($"Water: {(tank.IsSaltwater ? "salt" : "fresh")} ({tank.Salinity:0} ppt)");
            if (tank.HasPests) ui.Label("<color=#ff8080>Pests! Treat the water (above).</color>");
        }

        /// <summary>Clean tank (a little algae, a few times a session) and deep clean (nearly all of it, a bloom too, but fish can get hurt).</summary>
        void DrawCleaning(bool can)
        {
            var rules = Balance.Data.algae;
            float removed = rules.cleanAlgaeRemoved * (1f + rules.cleanBonusPerFishkeepingLevel * (Progression.Fishkeeping - 1)) * Perks.More(PerkEffect.StrongerWipes);
            int price = rules.deepCleanPrice;
            float risk = Tank.DeepCleanInjuryChance;
            ui.BeginRow("fb-row fb-pair");
            if (ui.Button($"Clean tank ({tank.CleansLeft} left)", "fb-button fb-grow", can && !tank.InBloom && tank.CleansLeft > 0,
                    tank.InBloom ? "No use against an algal bloom: it needs a deep clean" : $"Scrubs off {removed * 100f:0.#}% algae and freshens the water"))
                tank.Clean();
            if (ui.Button($"Deep clean ({tank.DeepCleansLeft} left)" + (price > 0 ? $"  {Economy.Format(price)}" : ""), "fb-button fb-grow",
                    can && tank.DeepCleansLeft > 0 && Economy.CanAfford(price), $"Nets the fish out and scrubs the tank down: clears nearly all the algae (and a bloom), but each fish has a {risk * 100f:0}% chance of getting hurt"))
                Confirm(tank.Residents.Count > 0 ? $"Each fish has a {risk * 100f:0}% chance of getting hurt in a deep clean (torn fins, lost scales, shock and worse). It clears nearly all the algae{(tank.InBloom ? " and the bloom" : "")}." : null,
                    () => tank.DeepClean());
            ui.Pop();
            if (tank.InBloom) ui.Label($"<color={UiTheme.CoralHex}><b>Algal bloom!</b> It chokes every fish and keeps spreading. Only a deep clean clears it.</color>", "fb-small");
        }

        /// <summary>The algae bar and what's driving it: its target and what makes it, where it's heading, the filter's fish limit and the feeding algae.</summary>
        void DrawAlgae()
        {
            var a = Balance.Data.algae;
            Bar("Algae", tank.Algae, false, $"How much of the tank is covered in algae. It climbs to its target: {a.naturalAlgae * 100f:0}% on its own, more for fish, plants and decorations, less for algae eaters. A filter that handles the fish holds it at the target; otherwise it keeps creeping up. Each feeding adds a little that goes again over the next time steps. At {a.bloomAbove * 100f:0}% the tank blooms, and only a deep clean clears it.");
            string Pct(float v) => $"{v * 100f:0.#}%";
            float target = tank.AlgaeTarget, settled = tank.Algae - tank.FeedingAlgae;
            var parts = new List<string>();
            if (tank.InBloom) parts.Add($"<color={UiTheme.CoralHex}>algal bloom</color>");
            else if (settled < target - 0.005f) parts.Add($"climbing to {Pct(target)}");
            else if (tank.AlgaeHeld) parts.Add(settled > target + 0.005f ? $"falling to {Pct(target)}" : $"steady at {Pct(target)}");
            else parts.Add($"<color={UiTheme.CoralHex}>past its {Pct(target)} target, creeping up</color>");
            int load = Mathf.RoundToInt(tank.FishLoad);
            if (!tank.HasFilter) parts.Add($"no filter ({load} fish)");
            else if (!tank.FilterWorking) parts.Add($"<color={UiTheme.CoralHex}>filter broken</color>");
            else
            {
                int cap = tank.FilterFishCapacity;
                parts.Add(load > cap ? $"<color={UiTheme.CoralHex}>filter for {cap} fish, {load} here</color>" : $"filter for {cap} fish, {load} here");
            }
            if (tank.FeedingAlgae > 0.0005f) parts.Add($"feeding +{Pct(tank.FeedingAlgae)} (gone in {tank.FeedingAlgaeSteps} step{(tank.FeedingAlgaeSteps == 1 ? "" : "s")})");
            ui.Label(string.Join(" · ", parts), "fb-small");
            var made = new List<string> { $"{Pct(a.naturalAlgae)} on its own" };
            if (tank.FishAlgae > 0.0005f) made.Add($"fish +{Pct(tank.FishAlgae)}");
            if (tank.PlantAlgae > 0.0005f) made.Add($"plants +{Pct(tank.PlantAlgae)}");
            if (tank.DecorationAlgae > 0.0005f) made.Add($"decorations +{Pct(tank.DecorationAlgae)}");
            if (tank.EaterAlgae > 0.0005f) made.Add($"algae eaters -{Pct(tank.EaterAlgae)}");
            ui.Label("Target: " + string.Join(", ", made) + $" · {tank.AlgaeEaterCount} of {tank.AlgaeEatersNeeded} algae eaters", "fb-small");
        }

        void DrawTemperature()
        {
            var liked = KnownTemperatureRange(out bool allKnown);
            ui.Label($"Temperature: {tank.Temperature:0.#} C" + (tank.HeaterWorking ? "  (heater)" : tank.HasHeater ? "  <color=#ff8080>(heater broken)</color>" : "  (room temperature)"));
            ui.Temperature(tank.Temperature, liked);
        }

        /// <summary>Water conditioner, bought and poured in one go: resets the pH and clears pests.</summary>
        void DrawTreatWater(bool can)
        {
            var supplier = Balance.Data.supplier;
            if (!Progression.IsUnlocked(XpTrack.Fishkeeping, supplier.waterConditionerLevel))
            {
                ui.Label($"<i>Water treatment: {Progression.LockText(XpTrack.Fishkeeping, supplier.waterConditionerLevel)}</i>", "fb-small");
                return;
            }
            int price = WaterTreatmentPrice;
            ui.BeginRow();
            if (ui.Button($"Treat water  {Economy.Format(price)}", "fb-button fb-grow", can && Economy.CanAfford(price), "Water conditioner: resets the pH and gets rid of snails and worms")
                && Economy.TrySpend(price, $"water conditioner for {storeTank.Title}", MoneyKind.Care))
                tank.TreatWater();
            if (Research.CanReadPh) ui.Label($"pH {tank.Ph:0.0}", "fb-text fb-value");
            ui.Pop();
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
                    string eggs = clutch.Count == 1 ? "Fish egg" : $"{clutch.Count} fish eggs";
                    if (ui.Button($"<size=11>{eggs}, I wonder what's inside!   hatch in {BreedingSystem.Steps(clutch.StepsToHatch)}</size>", "fb-button fb-stock-row", true, "Go to the eggs"))
                    {
                        selected.Clear();
                        ZoomPoint = tank.Clamp(clutch.transform.position, 0.12f, 0.08f);
                    }
                }
            foreach (var fish in tank.Residents)
                if (fish.IsPregnant)
                {
                    any = true;
                    ui.Label($"<color=#ffb3d9>Pregnant: {fish.FishName} ({fish.SpeciesLabel}), due in {BreedingSystem.Steps(fish.StepsToBirth)}</color>");
                }
            if (any) ui.Space(4f);
            DrawStockBook();
            var lines = BreedingSystem.Readiness(tank);
            foreach (var line in lines) ui.Label($"<color=#c9d7e8>{line}</color>");
            if (lines.Count == 0 && !any) ui.Label("<i>No fish here to breed.</i>");
            ui.Label("Fish pair up on their own at each time step when they're ready; a breeder tank gives them the best chance.", "fb-small fb-gap-above");
        }

        /// <summary>
        /// The stock book (a Breeding perk): every fish in the tank by kind, then sex and age, each
        /// row a button that picks the fish like clicking it in the water.
        /// </summary>
        void DrawStockBook()
        {
            ui.Label("<b>Stock book</b>");
            if (Perks.Count(PerkEffect.StockBook) == 0 && !DevMode.On)
            {
                ui.Label("<i>The Stock book perk (Breeding, tier 1) lists every fish here by kind and age, and lets you pick them from the list.</i>", "fb-small");
                ui.Space(4f);
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
                ui.Label("<i>No fish in this tank.</i>", "fb-small");
                ui.Space(4f);
                return;
            }
            foreach (var pair in groups)
            {
                var list = pair.Value;
                // Adults, then juveniles, then fry; within those by sex; then biggest first.
                list.Sort((a, b) => a.Stage != b.Stage ? b.Stage.CompareTo(a.Stage) : a.Sex != b.Sex ? b.Sex.CompareTo(a.Sex) : a.Size != b.Size ? b.Size.CompareTo(a.Size) : b.VisualLength.CompareTo(a.VisualLength));
                int males = 0, females = 0, adults = 0;
                foreach (var fish in list)
                {
                    if (fish.Stage == LifeStage.Adult) adults++;
                    if (fish.Stage == LifeStage.Fry) continue;
                    if (fish.Sex == Sex.Male) males++;
                    else females++;
                }
                ui.Label($"<b>{pair.Key}</b> <size=11>x{list.Count}: {males}♂ {females}♀, {adults} adult{(adults == 1 ? "" : "s")}</size>");
                foreach (var fish in list)
                {
                    string sex = fish.Stage == LifeStage.Fry ? "?" : fish.Sex == Sex.Male ? "♂" : "♀";
                    string sick = fish.Health.IsSick ? " <color=#ff8080>+</color>" : "";
                    string size = fish.Stage == LifeStage.Fry ? "" : $", {fish.Size.ToString().ToLower()}";
                    string label = $"<size=11>{sex} {fish.FishName}{sick}   {fish.Stage.ToString().ToLower()}{size}, {fish.AgeDays:0.#} days</size>";
                    if (ui.Button(label, selected.Contains(fish) ? "fb-button fb-stock-row fb-button--primary" : "fb-button fb-stock-row")) ToggleSelect(fish);
                }
            }
            ui.Space(6f);
        }

        /// <summary>The tank itself: its size, water type, decorations, background and type.</summary>
        void DrawTankTab()
        {
            bool can = !ReadOnly;
            DrawUpgrade(can);
            // The water can only change in an empty tank: no fish, fry, eggs or dead fish.
            int eggs = EggClutch.EggsIn(tank);
            if (tank.Residents.Count > 0 || tank.Corpses.Count > 0 || eggs > 0)
                ui.Label($"<i>Water: {(tank.IsSaltwater ? "salt" : "fresh")}. To change it, move every fish, fry and egg out of the tank first{(tank.Corpses.Count > 0 ? " and clear the dead fish" : "")}.</i>", "fb-small");
            else
            {
                var supplier = Balance.Data.supplier;
                int salt = Perks.Supplier(supplier.marineSaltPrice);
                string label = tank.IsSaltwater ? $"Change to fresh water ({Economy.Format(salt)})" : $"Change to salt water ({Economy.Format(salt)})";
                if (!tank.IsSaltwater && !Progression.IsUnlocked(XpTrack.Fishkeeping, supplier.marineSaltLevel))
                    ui.Label($"<i>Salt water tanks: {Progression.LockText(XpTrack.Fishkeeping, supplier.marineSaltLevel)}</i>");
                else if (ui.Button(label, "fb-button", can) && Economy.TrySpend(salt, "changing a tank's water", MoneyKind.Care)) tank.SetSaltwater(!tank.IsSaltwater);
            }
            DrawDecorations(can);
            DrawBackgroundSetup(can);
            DrawTankType(can);
#if UNITY_EDITOR
            if (!ReadOnly && ui.Button("Dirty the water (dev)")) tank.DevMakeDirty();
            if (!ReadOnly && ui.Button("Grow every fish up (dev)"))
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
            ui.Label("<color=#ffd27f>Developer tools: everything here is free and instant.</color>", "fb-small");
            ui.BeginRow("fb-row fb-pair");
            if (ui.Button("Clean water", "fb-button fb-grow")) tank.DevMakeClean();
            if (ui.Button("Dirty water", "fb-button fb-grow")) tank.DevMakeDirty();
            ui.Pop();
            ui.BeginRow("fb-row fb-wrap fb-pair");
            if (ui.Button("Algae 45%", "fb-button fb-button--small")) tank.DevSetAlgae(0.45f, false);
            if (ui.Button("Algal bloom", "fb-button fb-button--small")) tank.DevSetAlgae(0.55f, true);
            ui.Pop();
            ui.BeginRow("fb-row fb-wrap fb-pair");
            if (ui.Button("Grow all up", "fb-button fb-button--small"))
                foreach (var fish in new List<Fish>(tank.Residents)) fish.DevGrowUp();
            if (ui.Button("Grow as big as the tank allows", "fb-button fb-button--small"))
                foreach (var fish in new List<Fish>(tank.Residents)) fish.DevGrowWithRoom();
            if (ui.Button("Remove all fish", "fb-button fb-button--small"))
            {
                foreach (var fish in new List<Fish>(tank.Residents)) fish.RemoveFromTank();
                foreach (var corpse in new List<Fish>(tank.Corpses)) corpse.RemoveFromTank();
                selected.Clear();
            }
            ui.Pop();

            ui.Label("<b>Equipment</b>", "fb-text fb-gap-above");
            foreach (var kind in Equipment.Kinds)
            {
                var item = tank.GetEquipment(kind);
                int current = item != null ? item.tier : -1;
                ui.BeginRow("fb-row fb-wrap");
                ui.Label(kind.ToString(), "fb-text fb-dev-label");
                if (ui.Button("None", current < 0 ? "fb-button fb-button--tiny fb-button--primary" : "fb-button fb-button--tiny", true, "Take it out")) tank.DevRemoveEquipment(kind);
                var tiers = Equipment.Tiers(kind);
                var fits = Equipment.TiersFor(kind, tank);
                for (int n = 0; n < fits.Count; n++)
                {
                    int t = fits[n];
                    if (ui.Button((n + 1).ToString(), current == t ? "fb-button fb-button--tiny fb-button--primary" : "fb-button fb-button--tiny", true, tiers[t].name)) tank.Install(kind, t);
                }
                ui.Pop();
            }
            // Always there, so the rows below don't jump when the pointer moves on and off a button.
            ui.Label(ui.Tooltip, "fb-tooltip-line");

            var decor = tank.Decor;
            if (ui.Button($"{(devShowDecor ? "▾" : "▸")} <b>Decorations</b>  <size=11>{(decor != null ? $"{decor.Count} / {decor.Capacity}" : "")}</size>", "fb-button fb-fold")) devShowDecor = !devShowDecor;
            if (devShowDecor && decor != null)
            {
                var ids = decor.Ids;
                for (int i = 0; i < ids.Count; i++)
                {
                    var placed = TankDecor.Find(ids[i]);
                    ui.BeginRow();
                    ui.Label(placed != null ? placed.name : ids[i], "fb-text fb-grow");
                    bool remove = ui.Button("x", "fb-button fb-button--tiny");
                    ui.Pop();
                    if (remove)
                    {
                        decor.RemoveAt(i);
                        break;
                    }
                }
                ui.Label("Add:", "fb-small");
                foreach (var item in SupplierCatalog.Decorations)
                    if (ui.Button($"+ {item.name}{(item.plant ? " (plant)" : "")}", "fb-button fb-button--small", !decor.IsFull)) decor.Add(item.id);
            }

            if (ui.Button($"{(devShowFish ? "▾" : "▸")} <b>Add fish</b>  <size=11>tiny {tank.TinyCount} / {tank.Capacity}, small {tank.GroupCount(SizeGroup.Small)} / {tank.GroupCapacity(SizeGroup.Small)}, medium {tank.GroupCount(SizeGroup.Medium)} / {tank.GroupCapacity(SizeGroup.Medium)}, large {tank.GroupCount(SizeGroup.Large)} / {tank.GroupCapacity(SizeGroup.Large)}</size>", "fb-button fb-fold")) devShowFish = !devShowFish;
            if (!devShowFish) return;
            string heading = null;
            foreach (var species in Species.ByWaterSizeName())
            {
                if (species.WaterSizeHeading != heading)
                {
                    heading = species.WaterSizeHeading;
                    ui.Label($"<b>{heading}</b>", "fb-small fb-gap-above");
                }
                ui.BeginRow();
                ui.Label($"<size=11>{species.displayName}</size>", "fb-text fb-grow");
                if (ui.Button("+1", "fb-button fb-button--tiny", true, "One adult")) DevSpawn(species, 1f, null);
                if (ui.Button("+pair", "fb-button fb-button--tiny", true, "A grown male and female"))
                {
                    DevSpawn(species, 1f, Sex.Male);
                    DevSpawn(species, 1f, Sex.Female);
                }
                if (ui.Button("+5 fry", "fb-button fb-button--tiny", true, "Five fry of this kind (bred, so you can tell what they are)"))
                    for (int i = 0; i < 5; i++) DevSpawn(species, 0.05f, null);
                ui.Pop();
            }
        }

        void DevSpawn(Species species, float growth, Sex? sex)
        {
            // Generation 2 counts as bred here, so fry show what they are straight away.
            var fish = FishFactory.Spawn(Genome.CreateRandom(species), growth < 1f ? 2 : 1, growth, tank, tank.RandomFreePoint(0.1f));
            if (sex.HasValue) fish.DevSetSex(sex.Value);
        }

        /// <summary>
        /// How many fish live here against the tank's limit. Going over the limit is allowed, up to
        /// the hard limit, but makes the water dirty faster, spreads illness and stresses every fish.
        /// </summary>
        void DrawFishCount(string sizeName)
        {
            int count = tank.TinyCount;
            string dead = tank.Corpses.Count > 0 ? $"   <color=#ff8080>Dead: {tank.Corpses.Count}</color>" : "";
            if (tank.IsExhibit)
            {
                // An exhibit counts its fish by size, each against its own limit.
                var parts = new List<string>();
                foreach (var size in new[] { AquariumSize.Small, AquariumSize.Medium, AquariumSize.Large })
                {
                    int have = PublicAquarium.CountOfSize(tank, size), limit = PublicAquarium.ExhibitLimit(tank, size);
                    parts.Add($"<color={(have >= limit ? "#ffd27f" : "#ffffff")}>{PublicAquarium.SizeName(size)}: {have} / {limit}</color>");
                }
                ui.Label($"{sizeName}. <b>{string.Join("   ", parts)}</b>{dead}");
                ui.Label("No tiny tank fish here: small (hybrid), medium and large kinds, each size with its own limit.", "fb-small");
                return;
            }
            if (tank.IsHolding)
            {
                int finds = tank.Residents.Count + EggClutch.EggsIn(tank);
                ui.Label($"{sizeName}. <color={(finds >= tank.Capacity ? "#ffd27f" : "#ffffff")}><b>Finds: {finds} / {tank.Capacity}</b></color>{dead}");
                ui.Label($"For the fish the seller finds diving. Raise them here, then put them in the {PublicAquarium.RoomName.ToLower()}, sell them on the fish market or let them go.", "fb-small");
                return;
            }
            // Small, medium and large fish have their own limits; each shows where they can live or already do.
            string groups = "";
            foreach (var group in new[] { SizeGroup.Small, SizeGroup.Medium, SizeGroup.Large })
            {
                int have = tank.GroupCount(group), limit = tank.GroupCapacity(group);
                if (have == 0 && (limit == 0 || !tank.IsWallAquarium)) continue;
                groups += $"   <color={(have >= limit ? "#ffd27f" : "#ffffff")}>{group}: {have} / {limit}</color>";
            }
            dead = groups + dead;
            if (tank.IsOvercrowded)
            {
                ui.Label($"{sizeName}. <color=#ff8080><b>Fish: {count} / {tank.Capacity}</b></color>{dead}");
                ui.Label($"<color=#ff9a8a>Overcrowded by {tank.OverLimit}: the water fouls faster, illness spreads and every fish here is stressed. Move some out.</color>", "fb-small");
            }
            else
            {
                string color = count == tank.Capacity ? "#ffd27f" : "#ffffff";
                ui.Label($"{sizeName}. <color={color}><b>Fish: {count} / {tank.Capacity}</b></color>{dead}");
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
        void DrawFeedButtons(bool can)
        {
            bool any = false;
            foreach (FoodType food in Enum.GetValues(typeof(FoodType)))
            {
                int left = Economy.FoodPortions(food);
                if (left <= 0) continue;
                any = true;
                var info = FoodPellet.Info(food);
                string name = info != null ? info.name : food.ToString();
                if (ui.Button($"Feed {name.ToLower()} ({left} left)", "fb-button", can) && Economy.TryUseFood(food)) storeTank.Feeding.Feed(food, 8);
            }
            if (!any) ui.Label("<color=#ff9a8a>No food! Order some from the Supplier menu (B).</color>");
        }

        // ------------------------------------------------------------ upgrades

        readonly bool[] showModels = new bool[3];

        /// <summary>
        /// The tank's filter, heater and bubbler: what's in now (or a warning about having none),
        /// and, clicked open, every model this tank can take. Models not unlocked yet are greyed
        /// out. Not every tank takes the same models: the wall aquarium needs its own.
        /// </summary>
        void DrawUpgradesTab()
        {
            ui.Label("Equipment wears out: the longer it runs, the likelier it breaks down at a time step. A broken one does nothing until it's repaired (Care tab). Better models work better, break less and raise the aquarium's quality, but cost more to fix.", "fb-small");
            foreach (var kind in new[] { EquipmentKind.Heater, EquipmentKind.Filter, EquipmentKind.Bubbler })
                DrawEquipmentKind(kind, !ReadOnly);
        }

        void DrawEquipmentKind(EquipmentKind kind, bool can)
        {
            int k = (int)kind;
            var item = tank.GetEquipment(kind);
            string title = kind == EquipmentKind.Filter ? "Filter" : kind == EquipmentKind.Heater ? "Heater" : "Bubbler";
            string now = item == null ? $"<color={UiTheme.CoralHex}>No {title.ToLower()}</color>" : item.Name + $" <size=11>({item.AgeText}, +{item.AgeBreakChance * 100f:0.#}% break chance from age)</size>" + (item.broken ? " <color=#ff8080>(broken)</color>" : "");
            if (ui.Button($"{(showModels[k] ? "▾" : "▸")} <b>{title}:</b> {now}", "fb-button fb-equipment-header")) showModels[k] = !showModels[k];
            if (item == null) ui.Label($"<color=#ffb0a0>{MissingWarning(kind)}</color>", "fb-small");
            else if (item.broken) ui.Label($"<color=#ff8080>{item.fault}. Repair it on the Care tab ({Economy.Format(item.RepairPrice)}).</color>", "fb-small");
            else ui.Label($"{EffectText(kind, item.Info)}. {item.BreakChance * 100f:0.#}% chance of breaking per time step, {item.AgeBreakChance * 100f:0.#}% of it from age.", "fb-small");
            if (showModels[k])
            {
                var tiers = Equipment.Tiers(kind);
                var fits = Equipment.TiersFor(kind, tank);
                if (fits.Count == 0) ui.Label("<i>No models for this tank yet.</i>", "fb-small");
                foreach (int tier in fits) DrawModelRow(kind, tier, tiers[tier], item, can);
                if (tank.IsWallAquarium) ui.Label("<i>The wall aquarium is far too big for ordinary equipment: it takes plant-room systems of its own.</i>", "fb-small");
            }
            ui.Space(6f);
        }

        /// <summary>What having none of this kind of equipment risks.</summary>
        string MissingWarning(EquipmentKind kind)
        {
            switch (kind)
            {
                case EquipmentKind.Filter:
                    return "Without a filter, fish waste builds up fast: the water gets dirty, algae climbs with every fish and fish fall ill.";
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
                case EquipmentKind.Filter: return $"Leaves {info.effect * 100f:0}% of the waste and stops algae at its target for up to {info.fishCapacity} fish";
                case EquipmentKind.Heater: return info.effect > 0.05f ? $"Holds the setting within {info.effect:0.#} C" : "Holds the exact setting";
                default: return $"Air for {info.effect:0} fish";
            }
        }

        /// <summary>One model: its picture, what it does, and the button to put it in (greyed out until it's unlocked).</summary>
        void DrawModelRow(EquipmentKind kind, int tier, EquipmentTierBalance info, InstalledEquipment installed, bool can)
        {
            bool unlocked = Progression.IsUnlocked(XpTrack.Fishkeeping, info.unlockLevel);
            bool same = installed != null && installed.tier == tier;
            var quality = Balance.Data.quality;
            ui.BeginCard(false, unlocked ? "fb-card fb-model" : "fb-card fb-model fb-faded");
            Picture(ItemPreviews.Equipment(kind, tier), 40f, 40f, false);
            ui.BeginColumn("fb-column fb-grow");
            ui.Label($"<b>{info.name}</b>" + (same ? $"  <color={UiTheme.LeafHex}>(in now)</color>" : ""));
            ui.Label($"{info.blurb} {EffectText(kind, info)}. Breaks {info.breakChance * 100f:0.#}% of time steps when new. Quality {quality.equipmentPoints + quality.pointsPerEquipmentTier * tier:0}.", "fb-small fb-text--tiny");
            int price = Perks.Supplier(info.price);
            if (!unlocked) ui.Label($"<color=#ffd27f>Needs {Progression.LockText(XpTrack.Fishkeeping, info.unlockLevel)}</color>", "fb-small");
            else
            {
                string label = installed == null ? $"Install  {Economy.Format(price)}" : same ? $"Replace with new  {Economy.Format(price)}" : $"Swap to this  {Economy.Format(price)}";
                if (ui.Button(label, "fb-button fb-button--small", can && Economy.CanAfford(price)))
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
            }
            ui.Pop();
            ui.Pop();
        }

        /// <summary>A picture on a dark rounded square.</summary>
        void Picture(Texture picture, float width, float height, bool crop)
        {
            var box = ui.Begin("picture", "fb-picture");
            box.style.width = width;
            box.style.height = height;
            var image = ui.Image(picture, width, height);
            image.scaleMode = crop ? ScaleMode.ScaleAndCrop : ScaleMode.ScaleToFit;
            UiPanels.Show(image, picture != null);
            ui.Pop();
        }

        /// <summary>A repair button for each broken piece of equipment.</summary>
        void DrawRepairs(bool can)
        {
            foreach (var kind in Equipment.Kinds)
            {
                var item = tank.GetEquipment(kind);
                if (item == null || !item.broken) continue;
                ui.Label($"<color=#ff8080><b>{item.Name}: broken!</b></color>\n<size=11>{item.fault}.</size>");
                if (ui.Button($"Repair it  {Economy.Format(item.RepairPrice)}", "fb-button", can && Economy.CanAfford(item.RepairPrice))) tank.Repair(kind);
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
            ui.BeginRow();
            ui.Badge(sick.Count == 1 ? "+ 1 SICK FISH" : $"+ {sick.Count} SICK FISH", SickRed);
            ui.Pop();
            foreach (var line in sick) ui.Label($"<color=#ff9a9a>{line}</color>");
            ui.Label("Dose the tank with medicine (Care), or move them to quarantine.", "fb-small");
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
        void DrawMedicine(bool can)
        {
            var supplier = Balance.Data.supplier;
            if (tank.Residents.Count == 0) return;
            if (!Progression.IsUnlocked(XpTrack.Fishkeeping, supplier.medicineLevel))
            {
                ui.Label($"<i>Medicine: {Progression.LockText(XpTrack.Fishkeeping, supplier.medicineLevel)}</i>");
                return;
            }
            int sick = 0;
            foreach (var fish in tank.Residents) if (fish.Health.IsSick) sick++;
            ui.Label($"Medicine for the whole tank ({Economy.Format(Perks.Supplier(supplier.medicinePrice))} a dose)" + (sick > 0 ? $": <color=#ff8080>{sick} sick</color>" : ": no one is sick"));
            ui.BeginRow("fb-row fb-pair");
            foreach (Medicine medicine in new[] { Medicine.Antiviral, Medicine.Antibacterial, Medicine.Antiparasitic })
                if (ui.Button(medicine.ToString(), "fb-button fb-button--small fb-grow", can)) DoseTank(medicine);
            ui.Pop();
        }

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
            if (previous != null && ui.Button($"< {previous.Title}", "fb-button fb-tank-bar__side")) switchTank(-1);
            ui.Flex();
            if (ui.Button(TopView ? "Front view (T)" : "Top view (T)", "fb-button", true, TopView ? "Look in through the front glass" : "Look straight down into the tank")) ToggleTopView();
            ui.Flex();
            if (next != null && ui.Button($"{next.Title} >", "fb-button fb-tank-bar__side")) switchTank(1);
        }

        /// <summary>Heater thermostat: a slider, the range the fish here need, and a one-click best setting.</summary>
        void DrawHeater(bool can)
        {
            ui.Label($"<b>Heater: {tank.HeaterSetting:0} C</b>", "fb-text fb-gap-above");
            ui.BeginRow();
            if (ui.Button("-", "fb-button fb-step", can)) tank.SetHeater(tank.HeaterSetting - 1f);
            float setting = ui.Slider(tank.HeaterSetting, 18f, 32f, "fb-slider", can);
            if (!Mathf.Approximately(Mathf.Round(setting), tank.HeaterSetting)) tank.SetHeater(setting);
            if (ui.Button("+", "fb-button fb-step", can)) tank.SetHeater(tank.HeaterSetting + 1f);
            ui.Pop();
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
                if (need != null) ui.Label(need, "fb-small");
                if (known.HasValue)
                {
                    float best = Mathf.Round((known.Value.x + known.Value.y) * 0.5f);
                    if (!Mathf.Approximately(best, tank.HeaterSetting) && ui.Button($"Set to {best:0} C for these fish", "fb-button", can)) tank.SetHeater(best);
                }
            }
        }

        /// <summary>The decorations: a button that opens the picker beside the panel, and one to move them about.</summary>
        void DrawDecorations(bool can)
        {
            var decor = tank.Decor;
            if (decor == null) return;
            ui.BeginRow("fb-row fb-pair");
            if (ui.Button($"Decorations  {decor.Count} / {decor.Capacity}", pickerShown == Picker.Decorations ? "fb-button fb-button--primary fb-grow" : "fb-button fb-grow", can)) TogglePicker(Picker.Decorations);
            if (decor.Count > 0 && ui.Button("Move", "fb-button", can, "Move decorations (top view)")) StartArranging(ArrangeMode.Decorations);
            ui.Pop();
        }

        /// <summary>The background: a button that opens the picker beside the panel, and one to adjust the picture.</summary>
        void DrawBackgroundSetup(bool can)
        {
            var background = TankBackgrounds.Find(tank.BackgroundId);
            ui.BeginRow("fb-row fb-pair");
            if (ui.Button(background != null ? $"Background: {background.name}" : "Background: none", pickerShown == Picker.Background ? "fb-button fb-button--primary fb-grow fb-button--wrap" : "fb-button fb-grow fb-button--wrap", can)) TogglePicker(Picker.Background);
            if (background != null && ui.Button("Adjust", "fb-button", can, "Slide and zoom the picture")) StartArranging(ArrangeMode.Background);
            ui.Pop();
            // A wall aquarium's sides run deep into the wall and show from the room: they take a picture too.
            if (!tank.IsWallAquarium) return;
            var sides = TankBackgrounds.Find(tank.SideBackgroundId);
            if (ui.Button(sides != null ? $"Sides: {sides.name}" : "Sides: none", pickerShown == Picker.Sides ? "fb-button fb-button--primary fb-button--wrap" : "fb-button fb-button--wrap", can)) TogglePicker(Picker.Sides);
        }

        // ------------------------------------------------------------ decoration and background pickers

        enum Picker { None, Decorations, Background, Sides }
        // Asked for, and showing (only on the Tank tab, and not while arranging).
        Picker picker, pickerShown;
        ScrollView pickerScrollView;
        Picker scrolledPicker;

        void TogglePicker(Picker which)
        {
            picker = picker == which ? Picker.None : which;
        }

        /// <summary>The open picker, as a window beside the tank panel (only on the Tank tab).</summary>
        void DrawPicker()
        {
            ui.BeginRow("fb-line");
            ui.Label(pickerShown == Picker.Decorations ? "<b>Your decorations</b>" : pickerShown == Picker.Sides ? "<b>Backgrounds for the sides</b>" : "<b>Your backgrounds</b>", "fb-text fb-grow");
            if (ui.Button("✕", "fb-button fb-button--tiny")) picker = Picker.None;
            ui.Pop();
            DrawConfirm();
            pickerScrollView = ui.BeginScroll("fb-scroll fb-window__body");
            if (scrolledPicker != pickerShown)
            {
                scrolledPicker = pickerShown;
                pickerScrollView.scrollOffset = Vector2.zero;
            }
            bool can = !ReadOnly;
            if (pickerShown == Picker.Decorations) DrawDecorationPicker(can);
            else if (pickerShown == Picker.Sides) DrawSideBackgroundPicker(can);
            else DrawBackgroundPicker(can);
            ui.Pop();
        }

        /// <summary>Decorations in stock to put in, then the ones in this tank to take out (back into stock).</summary>
        void DrawDecorationPicker(bool can)
        {
            var decor = tank.Decor;
            if (decor == null) return;
            bool any = false;
            foreach (var pair in new List<KeyValuePair<string, int>>(Inventory.AllDecorations))
            {
                any = true;
                var item = TankDecor.Find(pair.Key);
                string name = item != null ? item.name : pair.Key;
                bool fits = item == null || item.FitsTankSize(tank.SizeIndex);
                if (PickerRow(ItemPreviews.Decoration(pair.Key), 40f, $"{name}  <size=11>x{pair.Value}</size>{LikedBy(pair.Key)}", !fits ? "Too big" : decor.IsFull ? "Full" : "Put in", can && fits && !decor.IsFull)
                    && Inventory.TakeDecoration(pair.Key))
                {
                    if (decor.Add(pair.Key)) GameLog.Add($"Put a {name.ToLower()} in {storeTank.Title}.");
                    else Inventory.AddDecoration(pair.Key);
                }
            }
            if (!any) ui.Label("<i>None in stock. Buy them from the Supplier menu (B).</i>", "fb-small");

            var ids = decor.Ids;
            if (ids.Count == 0) return;
            ui.Label("<b>In this tank</b>", "fb-small fb-gap-above");
            for (int i = 0; i < ids.Count; i++)
            {
                var item = TankDecor.Find(ids[i]);
                string name = item != null ? item.name : ids[i];
                if (!PickerRow(ItemPreviews.Decoration(ids[i]), 28f, $"<size=12>{name}</size>{LikedBy(ids[i])}", "Take out", can)) continue;
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
        void DrawBackgroundPicker(bool can)
        {
            var background = TankBackgrounds.Find(tank.BackgroundId);
            if (background != null && PickerRow(TankBackgrounds.Texture(background.id), 40f, $"{background.name}  <size=11>(up now)</size>", "Take down", can))
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
                if (item == null || item.wallAquariumOnly && !tank.IsWallAquarium) continue;
                any = true;
                if (!PickerRow(TankBackgrounds.Texture(pair.Key), 40f, $"{item.name}  <size=11>x{pair.Value}</size>", background != null ? "Swap" : "Put up", can)
                    || !Inventory.TakeBackground(pair.Key)) continue;
                if (background != null) Inventory.AddBackground(background.id);
                tank.SetBackground(pair.Key);
                GameLog.Add($"Put the {item.name.ToLower()} background up in {storeTank.Title}.");
            }
            if (!any) ui.Label("<i>None in stock. Buy them from the Supplier menu (B).</i>", "fb-small");
        }

        /// <summary>
        /// A wall aquarium's sides: one picture from stock covers both side glasses. The one up now
        /// (to take down, back into stock), then the ones in stock to put up or swap in.
        /// </summary>
        void DrawSideBackgroundPicker(bool can)
        {
            ui.Label("<i>One picture covers both side glasses.</i>", "fb-small");
            var sides = TankBackgrounds.Find(tank.SideBackgroundId);
            if (sides != null && PickerRow(TankBackgrounds.Texture(sides.id), 40f, $"{sides.name}  <size=11>(up now)</size>", "Take down", can))
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
                if (!PickerRow(TankBackgrounds.Texture(pair.Key), 40f, $"{item.name}  <size=11>x{pair.Value}</size>", sides != null ? "Swap" : "Put up", can)
                    || !Inventory.TakeBackground(pair.Key)) continue;
                if (sides != null) Inventory.AddBackground(sides.id);
                tank.SetSideBackground(pair.Key);
                GameLog.Add($"Put the {item.name.ToLower()} picture up on the sides of {storeTank.Title}.");
            }
            if (!any) ui.Label("<i>None in stock. Buy them from the Supplier menu (B).</i>", "fb-small");
        }

        /// <summary>One row of a picker: a picture, a name and a button. True when the button is clicked.</summary>
        bool PickerRow(Texture picture, float size, string label, string button, bool enabled)
        {
            ui.BeginRow("fb-row fb-picker-row");
            Picture(picture, size * 1.4f, size, true);
            ui.Label(label, "fb-text fb-grow fb-picker-row__name");
            bool clicked = ui.Button(button, "fb-button fb-button--small fb-picker-row__button", enabled);
            ui.Pop();
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
        void DrawTankType(bool can)
        {
            if (tankStore == null) return;
            ui.Label($"Tank type: <b>{StoreTank.RoleName(storeTank.Role)}</b>", "fb-text fb-gap-above");
            foreach (var role in StoreLayout.OtherRoles(storeTank))
            {
                var reason = tankStore.WhyCantChangeRole(storeTank, role);
                if (ui.Button($"Make it a {StoreTank.RoleName(role).ToLower()} tank  {Economy.Format(StoreLayout.ChangeRolePrice)}", "fb-button fb-button--wrap", can && reason == null)) tankStore.TryChangeRole(storeTank, role);
                if (reason != null && reason != StoreLayout.NoMoney) ui.Label($"<color=#ffd27f>{reason}</color>", "fb-small");
            }
            ui.Label(RoleHint(storeTank.Role), "fb-small");
        }

        static string RoleHint(TankRole role)
        {
            switch (role)
            {
                case TankRole.ForSale: return "Customers buy the fish in for sale tanks.";
                case TankRole.Display: return "Display tanks aren't for sale: they're there to wow customers.";
                case TankRole.Breeder: return "Pairs breed best in breeder tanks.";
                case TankRole.Exhibit: return "An exhibit in the public aquarium: visitors pay to see it, and nothing in it is sold. The better it looks, the more people come and the more a ticket is worth.";
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

        const float DecorSizeStep = TankDecor.SizeStep;

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
                ui.Label("MOVE DECORATIONS", "fb-heading");
                ui.Label("Seen from above. Drag a decoration to move it. Q and E, the mouse wheel or the buttons turn the one you last moved." + (tank.IsWallAquarium ? " Here in the wall aquarium, - and + make it smaller or bigger." : "") + " Nothing can go past the glass.");
                string held = decor != null ? decor.IdAt(heldPiece) : null;
                var item = held != null ? TankDecor.Find(held) : null;
                ui.Label(item != null ? $"Holding: <b>{item.name}</b>" : "<i>Pick one up to move or turn it.</i>", "fb-text fb-gap-above");
                ui.BeginRow("fb-row fb-pair");
                if (ui.Button("Turn left (Q)", "fb-button fb-grow", held != null)) decor.Turn(heldPiece, -15f);
                if (ui.Button("Turn right (E)", "fb-button fb-grow", held != null)) decor.Turn(heldPiece, 15f);
                ui.Pop();
                if (tank.IsWallAquarium)
                {
                    // Only the wall aquarium is big enough for pieces of different sizes.
                    ui.Label(held != null ? $"Size: <b>{Mathf.RoundToInt(decor.SizeAt(heldPiece) * 100f)}%</b>" : "Size: <i>pick one up first</i>");
                    ui.BeginRow("fb-row fb-pair");
                    if (ui.Button("Smaller (-)", "fb-button fb-grow", held != null)) decor.Resize(heldPiece, 1f / DecorSizeStep);
                    if (ui.Button("Bigger (+)", "fb-button fb-grow", held != null)) decor.Resize(heldPiece, DecorSizeStep);
                    if (ui.Button("100%", "fb-button", held != null)) decor.Resize(heldPiece, 1f / Mathf.Max(0.01f, decor.SizeAt(heldPiece)));
                    ui.Pop();
                }
                if (ui.Button("Back to automatic spots", "fb-button", decor != null && decor.MovedCount > 0))
                {
                    decor.ResetPlacement();
                    heldPiece = -1;
                }
            }
            else
            {
                ui.Label("ADJUST THE BACKGROUND", "fb-heading");
                ui.Label("Drag the picture to slide it, and zoom with the mouse wheel or the slider. It always covers the whole back glass.");
                ui.BeginRow("fb-row fb-gap-above");
                ui.Label("Zoom", "fb-text fb-header-text");
                float zoom = ui.Slider(tank.BackgroundZoom, 1f, Tank.MaxBackgroundZoom);
                if (!Mathf.Approximately(zoom, tank.BackgroundZoom)) tank.SetBackgroundCrop(tank.BackgroundCenter, zoom);
                ui.Label($"{tank.BackgroundZoom:0.0}x", "fb-text fb-value");
                ui.Pop();
                if (ui.Button("Centre it, as wide as it fits")) tank.SetBackgroundCrop(new Vector2(0.5f, 0.5f), 1f);
            }
            ui.Flex();
            if (ui.Button("Done (Esc)", "fb-button fb-button--primary")) StopArranging();
        }

        /// <summary>
        /// Mouse work while arranging: picking up, dragging and turning decorations on the floor of
        /// the tank, or dragging and zooming the picture on the back glass. True if the mouse was used.
        /// </summary>
        bool HandleArranging()
        {
            if (Arranging == ArrangeMode.None) return false;
            bool overMenu = UiPanels.PointerOverUi;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            float wheel = Input.mouseScrollDelta.y;
            if (Arranging == ArrangeMode.Decorations)
            {
                var decor = tank.Decor;
                if (decor == null) return false;
                if (!Input.GetMouseButton(0)) dragPiece = -1;
                var floor = new Plane(Vector3.up, tank.transform.position);
                if (!floor.Raycast(ray, out float hitAt)) return false;
                var point = ray.GetPoint(hitAt);
                if (Input.GetMouseButtonDown(0) && !overMenu)
                {
                    int index = -1;
                    var hits = Physics.RaycastAll(ray, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
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
                    return true;
                }
                if (Input.GetMouseButton(0) && dragPiece >= 0)
                {
                    decor.MoveTo(dragPiece, point + dragOffset);
                    return true;
                }
                // Wheel down turns clockwise, as it always has.
                if (wheel != 0f && heldPiece >= 0 && !overMenu)
                {
                    decor.Turn(heldPiece, wheel < 0f ? 15f : -15f);
                    return true;
                }
                return false;
            }

            // The background: points on the back glass.
            if (!Input.GetMouseButton(0)) draggingPicture = false;
            var size = tank.InnerSize;
            var glass = new Plane(tank.Front, tank.InnerBounds.center - tank.Front * (size.z * 0.5f));
            if (!glass.Raycast(ray, out float at)) return false;
            var onGlass = ray.GetPoint(at);
            if (Input.GetMouseButtonDown(0) && !overMenu)
            {
                draggingPicture = true;
                lastDragPoint = onGlass;
                return true;
            }
            if (Input.GetMouseButton(0) && draggingPicture)
            {
                var moved = onGlass - lastDragPoint;
                lastDragPoint = onGlass;
                tank.DragBackground(new Vector2(Vector3.Dot(moved, tank.transform.right), moved.y));
                return true;
            }
            if (wheel != 0f && !overMenu)
            {
                tank.SetBackgroundCrop(tank.BackgroundCenter, tank.BackgroundZoom * (wheel < 0f ? 0.85f : 1.18f));
                return true;
            }
            return false;
        }

        /// <summary>The next size up: what it holds, which fish need it, and the button once the quality is there.</summary>
        void DrawUpgrade(bool can)
        {
            int next = tank.SizeIndex + 1;
            var info = StoreLayout.SizeInfo(tank, next);
            if (info == null || upgradeTank == null) return;
            bool wall = tank.IsWallAquarium;
            int step = next - Tank.WallSize;
            ui.Label(tank.IsExhibit
                ? $"{info.name}: raised to {info.height:0.0#} m tall, room for {PublicAquarium.ExhibitLimitAt(step, AquariumSize.Small)} small, {PublicAquarium.ExhibitLimitAt(step, AquariumSize.Medium)} medium and {PublicAquarium.ExhibitLimitAt(step, AquariumSize.Large)} large fish, {info.decorationSlots} decorations."
                : wall
                ? $"{info.name}: raised to {info.height:0.0#} m tall, {Tank.LimitsText(info)}, {info.decorationSlots} decorations."
                : tank.IsHolding ? $"{info.name} tank: room for {ScubaDives.Data.holdingFishPerSize[Mathf.Min(next, ScubaDives.Data.holdingFishPerSize.Count - 1)]} of the seller's finds."
                : $"{info.name} tank: {Tank.CapacityOf(info)} fish, {info.decorationSlots} decorations.", "fb-small");
            var reason = StoreLayout.WhyCantUpgrade(storeTank, next);
            if (reason != null && reason != StoreLayout.NoMoney)
            {
                ui.Label($"<color=#ffd27f>{reason}</color>", "fb-small");
                return;
            }
            int price = Perks.Upgrade(info.upgradePrice);
            if (ui.Button(wall ? $"Raise to {info.height:0.0#} m  {Economy.Format(price)}" : $"Upgrade to {info.name.ToLower()} tank  {Economy.Format(price)}", "fb-button fb-button--wrap", can && reason == null)) upgradeTank(storeTank, next);
        }

        /// <summary>A label, a bar coloured from red (bad) to green (good) and the percentage.</summary>
        void Bar(string label, float value, bool highIsGood, string tooltip = null)
        {
            ui.BeginRow("fb-row fb-stat");
            ui.Label(label, "fb-text fb-stat__label");
            float good = highIsGood ? value : 1f - value;
            ui.Bar(value, Color.Lerp(new Color(0.9f, 0.3f, 0.25f), new Color(0.35f, 0.85f, 0.4f), good), "fb-bar fb-stat__bar", tooltip);
            ui.Label($"{value * 100f:0}%", "fb-text fb-stat__value");
            ui.Pop();
        }

        // ------------------------------------------------------------ fish window

        /// <summary>Whether the selected fish's health and comfort breakdowns are open (needs the health check-up research).</summary>
        bool healthOpen, comfortOpen;
        Fish fishScrollFor;

        /// <summary>
        /// The fish window: who it is (sex, age and size), its health and comfort with which way
        /// each is going, its price, and a link to its kind's page in the journal, where everything
        /// about the species lives. With the health check-up research the arrows open up to show why.
        /// It sits beside the fish, clear of the tank menu.
        /// </summary>
        void DrawFishWindow(Fish fish)
        {
            var size = PanelSize;
            const float w = 320f;
            float h = Mathf.Clamp(size.y - 110f, 300f, healthOpen || comfortOpen ? 560f : 420f);
            ToPanel(fish.transform.position, out var anchor);
            float x = anchor.x + 90f;
            if (x + w > size.x - 10f) x = anchor.x - 90f - w;
            x = Mathf.Clamp(x, 320f, Mathf.Max(320f, size.x - w - 10f));
            float y = Mathf.Clamp(anchor.y - h * 0.4f, 10f, Mathf.Max(10f, size.y - h - 70f));
            fishView.style.left = x;
            fishView.style.top = y;
            fishView.style.height = h;

            DrawFishHeader(fish);

            if (fish.IsDead)
            {
                ui.Label($"<color=#ff8080><b>Dead</b> ({fish.CauseOfDeath})</color>");
                ui.Label("Dead fish slowly make the rest of the tank sick and upset customers.");
                if (ui.Button("Remove from the tank", "fb-button", !ReadOnly))
                {
                    selected.Remove(fish);
                    fish.RemoveFromTank();
                    Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpRemoveDeadFish);
                }
                ui.Flex();
                if (ui.Button("Close (Esc)")) selected.Clear();
                return;
            }

            // The five things about the fish scroll if the breakdowns are open; the move buttons stay put.
            fishScrollView = ui.BeginScroll("fb-scroll fb-window__body");
            if (fishScrollFor != fish)
            {
                fishScrollFor = fish;
                fishScrollView.scrollOffset = Vector2.zero;
            }
            DrawFishHealth(fish);
            DrawFishComfort(fish);
            DrawFishPrice(fish);
            ui.Space(6f);
            if (fish.IdentityHidden)
                ui.Button("Journal entry", "fb-button", false, "Too small to tell what it is yet: it shows its colours when it grows into a juvenile");
            else if (ui.Button($"Journal entry: {fish.Species.displayName}", "fb-button fb-button--wrap")) JournalView.Open(fish.Species.id);
#if UNITY_EDITOR
            if (!ReadOnly && ui.Button("Next illness (dev)")) fish.Health.DevNextIllness();
#endif
            ui.Pop();

            DrawMoveButtons(fish);
            if (ui.Button("Close (Esc)")) selected.Clear();
        }

        /// <summary>Its name, then sex, age (stage and days) and size as bold coloured badges.</summary>
        void DrawFishHeader(Fish fish)
        {
            ui.Label($"<size=17><b>{fish.FishName}</b></size>  <size=11>{(fish.IdentityHidden ? "mystery fry" : fish.SpeciesLabel)}</size>");
            ui.BeginRow("fb-row fb-wrap");
            if (fish.Stage == LifeStage.Fry) ui.Badge("Sex: ?", new Color(0.4f, 0.42f, 0.46f));
            else if (fish.Sex == Sex.Male) ui.Badge("♂ Male", new Color(0.2f, 0.45f, 0.85f));
            else ui.Badge("♀ Female", new Color(0.85f, 0.3f, 0.6f));
            string age = $" · {fish.AgeDays:0.#} DAYS";
            switch (fish.Stage)
            {
                case LifeStage.Adult: ui.Badge("ADULT" + age, new Color(0.2f, 0.6f, 0.3f)); break;
                case LifeStage.Juvenile: ui.Badge("JUVENILE" + age, new Color(0.75f, 0.55f, 0.15f)); break;
                default: ui.Badge("FRY" + age, new Color(0.45f, 0.47f, 0.5f)); break;
            }
            if (fish.Stage != LifeStage.Fry) ui.Badge(fish.Size.ToString().ToUpper(), new Color(0.3f, 0.45f, 0.55f));
            ui.Pop();
            if (fish.IsPregnant)
                ui.Label($"<color=#ffb3d9><b>Pregnant</b>: about {fish.UnbornCount} fry due in {BreedingSystem.Steps(fish.StepsToBirth)}</color>", "fb-small");
        }

        /// <summary>
        /// The health bar with an arrow for where it's heading at the next time step. Once the health
        /// check-up research is done the arrow opens up every factor behind it.
        /// </summary>
        void DrawFishHealth(Fish fish)
        {
            var report = fish.Health.Report();
            float now = fish.Health.Current;
            float change = report.total - now;
            float next = Mathf.MoveTowards(now, report.total, Balance.Data.health.healthChangePerStep);
            int trend = Mathf.Abs(change) < 0.01f ? 0 : change > 0f ? 1 : -1;
            string why = trend == 0 ? "Steady: health only changes at time steps (closing up and overnight)"
                : $"{(trend > 0 ? "Rising" : "Falling")} to {next * 100f:0}% at the next time step" + (Mathf.Abs(next - report.total) > 0.01f ? $" (heading for {report.total * 100f:0}%)" : "");
            TrendBar("<b>Health</b>", now, trend, why, ref healthOpen);
            if (fish.Health.IsSick)
            {
                var disease = fish.Health.Disease;
                bool known = Research.Has(ResearchUnlock.Pathology);
                ui.Label($"<b>Sick: {disease.displayName}</b>" + (known ? $"  <size=11>({fish.Health.Severity * 100f:0}% bad), cure: {CureText(disease)}</size>" : "  <size=11>treat the tank on the Care tab</size>"), "fb-text fb-sick-box");
                DrawEuthanize(fish);
            }
            if (!healthOpen || !Research.Has(ResearchUnlock.HealthDetails)) return;

            var species = fish.Species;
            ui.Label($"<color={(trend > 0 ? "#9fe39f" : trend < 0 ? "#ff9a8a" : "#d8dde4")}>{why}.</color>", "fb-small");
            Penalty("Hunger", report.hunger, $"{fish.Hunger * 100f:0}% hungry");
            Penalty("Illness", report.illness, fish.Health.IsSick ? $"{fish.Health.Disease.displayName} ({fish.Health.Severity * 100f:0}%)" : "healthy");
            Penalty("Water", report.environment, report.environmentMismatch < 0.01f ? "suits it" : "not right for it");
            foreach (var note in FishHealth.EnvironmentNotes(species, tank)) SubLine(note, 0f, true);
            Penalty("Comfort", report.comfort, $"{report.comfortLevel * 100f:0}% comfortable");
            if (report.overcrowding > 0f) Penalty("Crowding", report.overcrowding, $"{tank.OverLimit} fish over the tank's limit");
            if (report.injury > 0f) Penalty("Wounds", report.injury, string.IsNullOrEmpty(fish.Health.InjuryCause) ? "healing" : fish.Health.InjuryCause);
            if (report.medicineDamage > 0f) Penalty("Medicine", report.medicineDamage, $"overdosed {fish.Health.MedicineDamage}x" + (fish.Health.Infertile ? ", can't breed" : ""));
            if (report.companionship > 0f) ui.Label($"<color=#9fe39f>Good company  +{report.companionship * 100f:0}</color>");
            if (report.oldAge > 0f) Penalty("Old age", report.oldAge, Research.Knows(species, ResearchFact.Lifespan) ? $"past {species.lifespanDays:0} days" : "getting old");
            if (report.deadTankmates > 0f) Penalty("Dead fish", report.deadTankmates, $"{tank.Corpses.Count} in the tank");
        }

        /// <summary>The comfort bar with an arrow for how it has changed since the last time step; with the research it opens up what's behind it.</summary>
        void DrawFishComfort(Fish fish)
        {
            float now = fish.Health.Report().comfortLevel, before = fish.Health.ComfortBefore;
            int trend = before < 0f || Mathf.Abs(now - before) < 0.01f ? 0 : now > before ? 1 : -1;
            string why = trend == 0 ? "No change since the last time step" : $"{(trend > 0 ? "Up" : "Down")} from {before * 100f:0}% at the last time step";
            TrendBar("<b>Comfort</b>", now, trend, why, ref comfortOpen);
            if (!comfortOpen || !Research.Has(ResearchUnlock.HealthDetails)) return;
            ui.Label($"{why}.", "fb-small");
            foreach (var note in fish.Health.ComfortNotes()) SubLine(note.text, note.delta, false);
        }

        /// <summary>
        /// The price the player set (editable in a For Sale tank). Seller's finds and exhibit fish
        /// get the fish market and release buttons instead.
        /// </summary>
        void DrawFishPrice(Fish fish)
        {
            ui.Space(6f);
            if (tank.IsHolding || tank.IsExhibit) DrawAquariumFish(fish);
            else if (storeTank.Role == TankRole.ForSale) DrawPrice(fish);
            else ui.Label(fish.Price > 0 ? $"Price: <b>{Economy.Format(fish.Price)}</b>  <size=11>(not for sale in this tank)</size>" : "Price: <size=11><i>not for sale; set one in a For Sale tank</i></size>");
        }

        /// <summary>
        /// A bar with a small up or down arrow after it. Clicking the arrow opens what it means
        /// underneath: with the health check-up research every factor behind it, before that just
        /// which way it's going.
        /// </summary>
        void TrendBar(string label, float value, int trend, string tooltip, ref bool open)
        {
            ui.BeginRow("fb-row fb-trend");
            Bar(label, value, true);
            string arrow = trend > 0 ? "<color=#9fe39f>▲</color>" : trend < 0 ? "<color=#ff9a8a>▼</color>" : "<color=#9aa3ad>–</color>";
            if (ui.Button($"{arrow}<size=10>{(open ? " ▴" : " ▾")}</size>", "fb-button fb-button--tiny fb-trend__arrow")) open = !open;
            ui.Pop();
            // Opened before the research: which way it is going, and that the research tells why.
            if (open && !Research.Has(ResearchUnlock.HealthDetails))
                ui.Label($"{tooltip}. Research health check-ups to see why.", "fb-small");
        }

        Fish releaseAsk;

        /// <summary>
        /// Where this kind may live in the public aquarium, and for the seller's finds in a holding
        /// tank and fish in an exhibit: sell it on the fish market, or let it go back to the wild.
        /// </summary>
        void DrawAquariumFish(Fish fish)
        {
            // A mystery find can still be sold or let go, but where it may live would give it away.
            var size = fish.IdentityHidden ? AquariumSize.None : PublicAquarium.SizeClass(fish.Species);
            if (!fish.IdentityHidden && (size != AquariumSize.None || tank.IsExhibit)) ui.Label(PublicAquarium.WhereItLives(fish), "fb-small");
            if (!tank.IsHolding && !tank.IsExhibit) return;
            bool can = !ReadOnly;
            ui.BeginRow("fb-row fb-wrap fb-pair");
            int price = PublicAquarium.MarketPrice(fish);
            if (ui.Button($"Sell on the market  {Economy.Format(price)}", "fb-button fb-grow", can, "The fish market pays at once, a little under its appraised value"))
            {
                var next = NextLivingFish(fish);
                selected.Remove(fish);
                PublicAquarium.SellOnMarket(fish);
                if (next != null && next != fish) Select(next);
            }
            if (releaseAsk != fish && ui.Button("Release...", "fb-button", can, "Let it go back to the wild: no money, a little Fishkeeping XP")) releaseAsk = fish;
            ui.Pop();
            if (releaseAsk == fish)
            {
                ui.Label($"Let {fish.FishName} go back to the wild? It's gone for good.", "fb-small");
                ui.BeginRow("fb-row fb-pair");
                if (ui.Button("Yes, let it go", "fb-button fb-button--primary fb-grow", can))
                {
                    var next = NextLivingFish(fish);
                    selected.Remove(fish);
                    releaseAsk = null;
                    PublicAquarium.Release(fish);
                    if (next != null && next != fish) Select(next);
                }
                if (ui.Button("Cancel", "fb-button fb-grow")) releaseAsk = null;
                ui.Pop();
            }
        }

        Fish euthanizeAsk;

        /// <summary>For a sick fish: put it to sleep humanely (asks once to make sure).</summary>
        void DrawEuthanize(Fish fish)
        {
            bool can = !ReadOnly;
            if (euthanizeAsk != fish)
            {
                if (ui.Button("Euthanize...", "fb-button", can, "Put it to sleep humanely, so it stops suffering and can't pass its illness on")) euthanizeAsk = fish;
                return;
            }
            ui.Label($"Put {fish.FishName} to sleep? It's taken out of the tank straight away. This can't be undone.", "fb-small");
            ui.BeginRow("fb-row fb-pair");
            if (ui.Button("Yes, put it to sleep", "fb-button fb-button--primary fb-grow", can))
            {
                var next = NextLivingFish(fish);
                selected.Remove(fish);
                euthanizeAsk = null;
                fish.PutToSleep();
                if (next != null && next != fish) Select(next);
            }
            if (ui.Button("Cancel", "fb-button fb-grow")) euthanizeAsk = null;
            ui.Pop();
        }

        /// <summary>Move buttons for every other tank, with how full each is; afterwards the next fish here is picked.</summary>
        void DrawMoveButtons(Fish fish)
        {
            bool can = !ReadOnly;
            ui.Label("Move to:", "fb-text fb-gap-above");
            ui.BeginRow("fb-row fb-wrap fb-move-buttons");
            foreach (var other in allTanks)
            {
                if (other == storeTank) continue;
                var t = other.Tank;
                string why = t.WhyNotFor(fish);
                t.CountFor(fish.Species, out int have, out int limit);
                // A full tank shows its count; any other reason shows why.
                string count = why == null ? $"{have}/{limit}" : why == "full" || why.StartsWith("no room") ? $"<color=#ff9a8a>{have}/{limit}</color>" : $"<color=#ff9a8a>{why}</color>";
                if (ui.Button($"{other.Title} ({count})", "fb-button fb-button--small fb-move-button", can && why == null))
                {
                    var next = NextLivingFish(fish);
                    if (fish.MoveTo(t))
                    {
                        selected.Remove(fish);
                        GameLog.Add($"Moved {fish.FishName} to {other.Title}." + (t.IsOvercrowded ? $" {other.Title} is over its limit now!" : ""));
                        if (next != null) Select(next);
                    }
                }
            }
            ui.Pop();
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

        /// <summary>An indented line under a heading; with a delta it shows the comfort it adds or takes.</summary>
        void SubLine(string text, float delta, bool bad)
        {
            string color = bad || delta < -0.001f ? "#ffb0a0" : delta > 0.001f ? "#b5ecb5" : "#d8dde4";
            bool number = Mathf.Abs(delta) > 0.001f && Research.Has(ResearchUnlock.HealthDetails);
            ui.BeginRow("fb-line fb-subline");
            ui.Label($"<size=12><color={color}>• {text}</color></size>", "fb-text fb-grow");
            ui.Label(number ? $"<size=12><color={color}>{(delta > 0f ? "+" : "-")}{Mathf.Abs(delta) * 100f:0}</color></size>" : "", "fb-text fb-value");
            ui.Pop();
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
            bool can = !ReadOnly;
            ui.BeginRow("fb-row fb-price-row");
            ui.Label("Price: $", "fb-text fb-header-text");
            var field = ui.TextField(priceText, 6, "fb-field fb-price-field");
            ui.Last.SetEnabled(can);
            if (can) priceText = field;
            if (int.TryParse(priceText, out var typed) && typed != fish.Price) fish.Price = typed;
            if (ui.Button("-5", "fb-button fb-step", can)) SetPrice(fish, fish.Price - 5);
            if (ui.Button("+5", "fb-button fb-step", can)) SetPrice(fish, fish.Price + 5);
            if (ui.Button("Appraised", "fb-button fb-button--small", can)) SetPrice(fish, fish.AppraisedValue);
            ui.Pop();
            if (fish.Stage == LifeStage.Fry) ui.Label("<i>Too young to sell: customers only buy juveniles and adults.</i>");
            else if (fish.Price <= 0) ui.Label("<i>No price: customers can't buy it.</i>");
        }

        void SetPrice(Fish fish, int price)
        {
            fish.Price = price;
            priceText = fish.Price.ToString();
        }

        void Penalty(string label, float penalty, string detail)
        {
            var color = penalty < 0.02f ? "#9fe39f" : penalty < 0.15f ? "#ffd27f" : "#ff8080";
            ui.BeginRow("fb-row fb-penalty");
            ui.Label($"<color={color}>{label}</color>", "fb-text fb-penalty__label");
            ui.Label(penalty < 0.02f || !Research.Has(ResearchUnlock.HealthDetails) ? detail : $"{detail}  <color={color}>-{penalty * 100f:0}</color>", "fb-text fb-grow");
            ui.Pop();
        }

        // ------------------------------------------------------------ marks over the fish

        /// <summary>
        /// Over the water: little arrow buttons either side of the picked fish to step through the
        /// tank, its name bobbing over it, a pulsing red "+" with the illness over every sick fish,
        /// and a box round the decoration being moved.
        /// </summary>
        void DrawMarkers()
        {
            var focus = Focus;
            if (focus != null && ToPanel(focus.transform.position, out var at))
            {
                if (ui.Button("<b><</b>", "fb-button fb-step-arrow")) Cycle(-1);
                Place(ui.Last, at.x - 78f, at.y - 16f);
                if (ui.Button("<b>></b>", "fb-button fb-step-arrow")) Cycle(1);
                Place(ui.Last, at.x + 44f, at.y - 16f);
            }
            foreach (var fish in tank.Residents)
            {
                if (fish.IsDead || !fish.Health.IsSick || selected.Contains(fish)) continue;
                if (!ToPanel(fish.transform.position + Vector3.up * 0.05f, out var p)) continue;
                float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f + fish.GetInstanceID());
                var mark = ui.Label("+", "fb-sick-mark");
                float size = 20f * pulse;
                mark.style.width = size;
                mark.style.height = size;
                Place(mark, p.x - size * 0.5f, p.y - size - 6f);
                Place(ui.Label(fish.Health.Disease.displayName, "fb-sick-name"), p.x, p.y - 6f);
            }
            foreach (var fish in selected)
            {
                if (!ToPanel(fish.transform.position + Vector3.up * 0.08f, out var p)) continue;
                float bob = Mathf.Sin(Time.unscaledTime * 4f) * 3f;
                Place(ui.Label($"▼ {fish.FishName}", "fb-selected-name"), p.x, p.y - 22f + bob);
            }
            DrawHeldMarker();
        }

        static void Place(VisualElement element, float x, float y)
        {
            element.style.left = x;
            element.style.top = y;
        }

        /// <summary>A box round the decoration being moved, so it's clear which one is held.</summary>
        void DrawHeldMarker()
        {
            if (Arranging != ArrangeMode.Decorations || heldPiece < 0 || tank.Decor == null) return;
            var b = tank.Decor.BoundsAt(heldPiece);
            if (b.size == Vector3.zero) return;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                if (!ToPanel(corner, out var p)) return;
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }
            var box = ui.Begin("held", "fb-held-mark");
            box.pickingMode = PickingMode.Ignore;
            Place(box, minX - 4f, minY - 4f);
            box.style.width = maxX - minX + 8f;
            box.style.height = maxY - minY + 8f;
            ui.Pop();
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

        /// <summary>A left click in the water (not on a menu): picks the fish there, or zooms in on the spot.</summary>
        void HandleClick()
        {
            if (!Input.GetMouseButtonDown(0) || cam == null || UiPanels.PointerOverUi || FxOverlay.MouseOverCard) return;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            Fish fish = null;
            if (Physics.Raycast(ray, out var hit, 50f)) fish = hit.collider.GetComponentInParent<Fish>();
            if (fish == null || fish.Tank != tank) fish = NearestFishOnScreen(Input.mousePosition);
            if (fish != null)
            {
                ToggleSelect(fish);
                return;
            }
            // Empty water: zoom in on that spot, as close as following a fish. Anywhere else zooms back out.
            selected.Clear();
            ZoomPoint = WaterPoint(ray);
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
    }
}
