using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.People;
using FishBreeder.Shop;
using UnityEngine;
using UnityEngine.UIElements;

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
    /// The screen is UI Toolkit: Resources/UI/StoreHud.uxml for the bars and labels, Window.uxml for
    /// the windows, both styled in FishBreeder.uss.
    /// </summary>
    public class StoreView : MonoBehaviour
    {
        enum Panel { None, Register, Supplier, Unlock, BuyTank, DayReport, Owner }

        static readonly Vector3 StoreViewDirection = new Vector3(0f, 0.62f, -0.78f).normalized;
        static readonly Color StoreBackground = new Color(0.42f, 0.78f, 0.86f);
        static readonly string[] SupplierTabs = { "Eggs", "Fry", "Decorations", "Food", "Equipment", "Any fish (dev)", "Dives" };
        const int EggsTab = 0, FryTab = 1, DecorTab = 2, FoodTab = 3, EquipmentTab = 4, DevFryTab = 5, DivesTab = 6;
        // The fish seller sells the fish (and dives for the aquarium's); the supplier menu (top bar) everything else.
#if UNITY_EDITOR
        static readonly int[] FishSellerTabs = { EggsTab, FryTab, DivesTab, DevFryTab };
#else
        static readonly int[] FishSellerTabs = { EggsTab, FryTab, DivesTab };
#endif
        // Equipment is bought from each aquarium's Upgrades tab, since what fits depends on the tank.
        static readonly int[] SupplierMenuTabs = { DecorTab, FoodTab };
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
#if UNITY_EDITOR
        int devGesture = -1;
#endif
        float panX;
        /// <summary>How far north (into the screen) the store view has been slid; 0 looks at the shop floor.</summary>
        float panZ;
        float distance = 15f;
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
            UpdateNightLook(dt);
            FishSeller.ShopOpen = shopDay.IsOpen;
            // Gone home for the night, or out on a dive until the next time step.
            FishSeller.Away = shopDay.Phase == DayPhase.Evening || ScubaDives.Out;
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
            bool escape = Input.GetKeyDown(KeyCode.Escape) && !PauseMenu.JustClosed;
            if (Input.GetKeyDown(KeyCode.V) && !UiPanels.Typing && panel != Panel.DayReport) ToggleWatch();
            if (Input.GetKeyDown(KeyCode.B) && !UiPanels.Typing && !shopDay.IsOpen) ToggleSupplier();
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
            if (escape && ResearchView.IsOpen)
            {
                ResearchView.Close();
                escape = false;
            }
            if (escape && ReputationView.IsOpen)
            {
                ReputationView.Close();
                escape = false;
            }
            if (escape && FinancesView.IsOpen)
            {
                FinancesView.Close();
                escape = false;
            }
            if (escape && AquariumView.IsOpen)
            {
                AquariumView.Close();
                escape = false;
            }
            // A right click zooms out one level too: from a fish or a close-up to the tank, from a
            // wheel zoom to the normal view, from the tank or a person to the store.
            bool zoomOut = Input.GetMouseButtonDown(1) && !AnyWindowOpen && panel != Panel.DayReport && !ViewMode.Watching;
            if (focused != null && focused.Tank == null) ExitTank();
            else if (focused != null && (escape || zoomOut)) StepBackInTank();
            else if (focused == null && (escape || zoomOut) && following >= 0) following = -1;
            // With nothing left to step back from, Esc opens the pause menu.
            else if (focused == null && escape && panel == Panel.None && following < 0) PauseMenu.Open();
            else if (focused == null && escape && panel != Panel.DayReport && panel != Panel.Owner) panel = Panel.None;
            if (focused == null && panel == Panel.None && Input.GetKeyDown(KeyCode.C) && !UiPanels.Typing) following++;
            var person = focused == null ? FollowedPerson() : null;
            // In a public aquarium exhibit everything round the tank goes black (which covers the top view too).
            bool exhibit = focused != null && focused.Role == TankRole.Exhibit;
            HideAroundExhibit(exhibit ? focused : null);
            HideOverTank(!exhibit && focused != null && (hud.TopView || hud.Arranging == FishTankHUD.ArrangeMode.Decorations) ? focused : null);
            if (exhibit) cam.backgroundColor = Color.black;
            if (focused != null)
            {
                // The scroll wheel zooms in and out (arranging uses it to turn pieces; over a menu it scrolls the menu).
                float wheel = Input.mouseScrollDelta.y;
                if (wheel != 0f && hud.Arranging == FishTankHUD.ArrangeMode.None && !hud.MouseOverMenu && !AnyWindowOpen && panel == Panel.None)
                    hud.ZoomWheel(wheel);
                if (hud.Arranging == FishTankHUD.ArrangeMode.Decorations) GetTopPose(focused, null, null, Vector2.zero, 1f, cam, hud.MenuCover, out targetPosition, out targetRotation);
                else if (hud.TopView) GetTopPose(focused, hud.Focus, hud.ZoomPoint, hud.ViewPan, hud.ViewZoom, cam, hud.MenuCover, out targetPosition, out targetRotation);
                else GetTankPose(focused, hud.Focus, hud.ZoomPoint, hud.ViewPan, hud.ViewZoom, cam, hud.MenuCover, out targetPosition, out targetRotation);
            }
            else if (person != null)
            {
#if UNITY_EDITOR
                // G plays the person's gestures one after another, for checking them.
                if (Input.GetKeyDown(KeyCode.G) && !UiPanels.Typing)
                {
                    var personRig = person.GetComponentInChildren<CrewRig>();
                    int count = System.Enum.GetValues(typeof(Gesture)).Length;
                    devGesture = (devGesture + 1) % count;
                    if (personRig != null) personRig.Play((Gesture)devGesture, cam.transform.position);
                    GameLog.Add($"Gesture (dev): {(Gesture)devGesture}");
                }
                // H: slow motion while checking them.
                if (Input.GetKeyDown(KeyCode.H) && !shopDay.IsOpen) Time.timeScale = Time.timeScale < 1f ? 1f : 0.2f;
#endif
                // Close up on one of the shop's people, from the store camera's side: the shopkeeper and
                // the fish seller face it, and customers turn round to it when something wows them.
                var look = person.position + Vector3.up * 0.85f;
                targetPosition = look + StoreViewDirection * 3.4f;
                targetRotation = Quaternion.LookRotation(look - targetPosition);
            }
            else
            {
                if (panel == Panel.None && !AnyWindowOpen) HandleStoreCameraInput(dt);
                GetStorePose(out targetPosition, out targetRotation);
            }
            HandleStoreClick();
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

        /// <summary>
        /// Esc or a right click in a tank: stop arranging, else leave a close-up for the whole tank
        /// (at the wheel zoom it had), else put the wheel zoom back to normal, else go back to the store.
        /// </summary>
        void StepBackInTank()
        {
            if (hud.Arranging != FishTankHUD.ArrangeMode.None) hud.StopArranging();
            else if (hud.CloseUp) hud.ClearSelection();
            else if (!hud.ResetWheelZoom()) ExitTank();
        }

        static bool AnyWindowOpen => JournalView.IsOpen || PerksView.IsOpen || ResearchView.IsOpen || ReputationView.IsOpen || FinancesView.IsOpen || AquariumView.IsOpen;

        bool AquariumBuilt => store.AquariumSection != null && store.AquariumSection.unlocked;

        /// <summary>The person the people camera follows, or null (stepping past the last one turns it off).</summary>
        Transform FollowedPerson()
        {
            if (following < 0) return null;
            var people = new List<Transform>();
            if (Shopkeeper.Instance != null) people.Add(Shopkeeper.Instance.transform);
            if (FishSeller.Instance != null && !FishSeller.Away) people.Add(FishSeller.Instance.transform);
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
            if (UiPanels.Typing) return;
            float pan = 0f, up = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) pan -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) pan += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) up += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) up -= 1f;
            panX += pan * 7f * dt;
            panZ += up * 7f * dt;
            if (Input.GetMouseButton(1))
            {
                panX -= Input.GetAxis("Mouse X") * 0.25f;
                panZ -= Input.GetAxis("Mouse Y") * 0.25f;
            }
            distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 1.2f, 7f, 26f);

            // The breeding rooms are to the west; you can look one section past the open ones, to see what's for sale.
            float maxX = Mathf.Min(store.UnlockedCount, store.Sections.Count - 1) * StoreLayout.SectionWidth;
            panX = Mathf.Clamp(panX, StoreLayout.BigRoomCenterX, maxX);
            // Down to the street out front, up past the back wall to the parking lot behind the breeding room.
            panZ = Mathf.Clamp(panZ, -2.5f, StoreLayout.ParkingLotView.z - StoreLookZ);
        }

        /// <summary>Where the store view looks before it's slid north: a little in front of the middle, so the pavement shows.</summary>
        const float StoreLookZ = -1.5f;

        /// <summary>Slid north past the back wall, the view tips down more steeply so it can see over the wall into the parking lot.</summary>
        static readonly Vector3 LotViewDirection = new Vector3(0f, 0.8f, -0.6f).normalized;

        void GetStorePose(out Vector3 position, out Quaternion rotation)
        {
            var lookAt = new Vector3(panX, 0.8f, StoreLookZ + panZ);
            float steep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(StoreLayout.SectionDepth * 0.5f - 1.5f, StoreLayout.ParkingLotView.z, lookAt.z));
            position = lookAt + Vector3.Slerp(StoreViewDirection, LotViewDirection, steep) * distance;
            rotation = Quaternion.LookRotation(lookAt - position);
        }

        /// <summary>Slides the store view over the fish seller in the parking lot.</summary>
        void LookAtParkingLot()
        {
            panX = StoreLayout.ParkingLotView.x;
            panZ = StoreLayout.ParkingLotView.z - StoreLookZ;
        }

        /// <summary>
        /// The whole tank fills the view (bigger tanks are seen from further back). With a fish
        /// selected the camera follows it at half the distance, keeping it centred; a clicked spot of
        /// empty water (<paramref name="zoom"/>) is looked at the same way.
        /// While the tank menu covers the left of the screen (<paramref name="menuCover"/>, a share of
        /// its width), the view slides over so the tank sits in the middle of the space right of
        /// the menu, as far as it can without the tank going off the right edge (backing off a
        /// little to make room when it's tight).
        /// </summary>
        static void GetTankPose(StoreTank tank, Fish follow, Vector3? zoom, Vector2 pan, float wheelZoom, Camera cam, float menuCover, out Vector3 position, out Quaternion rotation)
        {
            var bounds = tank.Tank.InnerBounds;
            var size = tank.Tank.InnerSize;
            var front = tank.Tank.Front;
            float back = Mathf.Max(1.9f, 1.9f * size.x / 1.6f, size.y * 2.1f);
            // Half the view's width one metre away.
            float halfWidth = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
            // A wall aquarium is framed just wide enough to fit, so the camera stays in the room.
            if (size.x > 4f && halfWidth > 1e-4f) back = Mathf.Max(1.9f, (size.x * 0.5f + 0.4f) / halfWidth + size.z * 0.5f);
            if ((follow != null && follow.Tank == tank.Tank) || zoom.HasValue)
            {
                // A spot slid to with the keys wins over the fish (clicking a fish follows it again).
                var target = zoom.HasValue ? zoom.Value : follow.transform.position;
                // Close-ups are as close in a wall aquarium as in a grand tank.
                float near = Mathf.Min(back, 3.6f) * 0.5f;
                // How far the target is behind the front glass (and a little room), where the camera has to stop.
                float glass = Vector3.Dot(bounds.center - target, front) + size.z * 0.5f + 0.12f;
                // The wheel zooms the stretch in front of the glass, so it works however deep the tank
                // is (in a deep one the target can be metres behind the glass).
                float away = glass + Mathf.Max(near - glass, near * 0.5f) * wheelZoom;
                position = target + front * away + Vector3.up * 0.05f;
                rotation = Quaternion.LookRotation(target - position);
                // Keep the fish (or spot) in the middle of the uncovered part.
                position -= rotation * Vector3.right * (menuCover * away * halfWidth);
                return;
            }
            back = Mathf.Max(back * wheelZoom, size.z * 0.5f + 0.25f);
            var center = bounds.center;
            float shift = 0f;
            if (menuCover > 0f && halfWidth > 1e-4f)
            {
                // The tank's middle goes to the middle of the uncovered part, if its right side (the
                // front glass plus its frame, about 3 cm) can stay on screen.
                float halfTank = size.x * 0.5f + 0.03f;
                float frontGlass = size.z * 0.5f;
                float wanted = menuCover * back * halfWidth;
                float most = 0.97f * (back - frontGlass) * halfWidth - halfTank;
                if (most < wanted && menuCover < 0.9f)
                {
                    // Step back a little (at most 15%) to make room beside the menu.
                    float needed = (halfTank + 0.97f * frontGlass * halfWidth) / ((0.97f - menuCover) * halfWidth);
                    back = Mathf.Min(needed, back * 1.15f);
                    wanted = menuCover * back * halfWidth;
                    most = 0.97f * (back - frontGlass) * halfWidth - halfTank;
                }
                shift = Mathf.Max(0f, Mathf.Min(wanted, most));
            }
            position = center + front * back + Vector3.up * 0.1f;
            rotation = Quaternion.LookRotation(center - position);
            position -= rotation * Vector3.right * shift;
            // Slid along with WASD: the same view, moved sideways and up or down.
            position += tank.Tank.transform.right * pan.x + Vector3.up * pan.y;
        }

        /// <summary>
        /// Straight down onto the tank, its back at the top of the screen, high enough to see the
        /// whole floor inside the glass beside the menu: for moving decorations about, and the tank
        /// view's top view (T). There the wheel zooms, WASD slides the view about, and a selected
        /// fish (or a clicked spot) is looked down on from closer, as the front view does.
        /// </summary>
        static void GetTopPose(StoreTank tank, Fish follow, Vector3? zoom, Vector2 pan, float wheelZoom, Camera cam, float menuCover, out Vector3 position, out Quaternion rotation)
        {
            var size = tank.Tank.InnerSize;
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = tanV * cam.aspect;
            float open = Mathf.Clamp(1f - menuCover, 0.3f, 1f);
            // Fitted at the rim, the nearest part to the camera.
            float above = Mathf.Max((size.x * 0.5f + 0.12f) / (halfWidth * open), (size.z * 0.5f + 0.12f) / tanV);
            var top = tank.Tank.InnerBounds.center + Vector3.up * size.y * 0.5f;
            rotation = Quaternion.LookRotation(Vector3.down, tank.transform.forward);
            if ((follow != null && follow.Tank == tank.Tank) || zoom.HasValue)
            {
                var target = zoom.HasValue ? zoom.Value : follow.transform.position;
                // As close as a front close-up, but never down inside the water.
                float height = Mathf.Max(top.y - target.y + 0.2f, Mathf.Min(above, 3.6f) * 0.5f * wheelZoom + (top.y - target.y));
                position = new Vector3(target.x, target.y + height, target.z);
                position -= rotation * Vector3.right * (menuCover * height * halfWidth);
                return;
            }
            above = Mathf.Max(above * wheelZoom, 0.3f);
            position = top + Vector3.up * above;
            position += tank.transform.right * pan.x + tank.transform.forward * pan.y;
            // The tank's middle goes to the middle of the part of the screen right of the menu.
            position -= rotation * Vector3.right * (menuCover * above * halfWidth);
        }

        /// <summary>Parts of the store above the tank being looked down into, hidden while looking (see <see cref="HideOverTank"/>).</summary>
        readonly List<Renderer> hiddenOverTank = new List<Renderer>();
        StoreTank hiddenFor;

        /// <summary>
        /// Looking straight down into a tank, nothing of the store may be in the way: the housing
        /// over the back of a wall aquarium, the wall it's set into, a stripe along that wall. Every
        /// part of the store that rises above the sand within the tank's outline is hidden until
        /// the view leaves (null shows them again).
        /// </summary>
        void HideOverTank(StoreTank tank)
        {
            if (hiddenFor == tank && !hiddenOverTank.Exists(r => r == null)) return;
            foreach (var r in hiddenOverTank) if (r != null) r.enabled = true;
            hiddenOverTank.Clear();
            hiddenFor = tank;
            if (tank == null || store == null) return;
            var b = tank.Tank.InnerBounds;
            foreach (var r in store.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r.transform.IsChildOf(tank.transform)) continue;
                var rb = r.bounds;
                // Anything rising above the sand inside the tank's outline (by more than a few centimetres).
                if (rb.max.y <= b.min.y + 0.05f) continue;
                if (rb.min.x >= b.max.x - 0.05f || rb.max.x <= b.min.x + 0.05f || rb.min.z >= b.max.z - 0.05f || rb.max.z <= b.min.z + 0.05f) continue;
                r.enabled = false;
                hiddenOverTank.Add(r);
            }
        }

        /// <summary>The store, other tanks' fish and people, hidden while looking into an exhibit (see <see cref="HideAroundExhibit"/>).</summary>
        readonly List<Renderer> hiddenAround = new List<Renderer>();
        StoreTank darkFor;
        float darkRescan;

        /// <summary>
        /// Looking into one of the public aquarium's exhibits, it's like a real aquarium: only the
        /// lit tank in the dark. Everything of the store but the tank itself, fish in other tanks
        /// and people are hidden, and the camera clears to black, until the view leaves (null
        /// shows them again). Rescanned now and then for fish and people that turn up.
        /// </summary>
        void HideAroundExhibit(StoreTank tank)
        {
            darkRescan -= Time.unscaledDeltaTime;
            if (darkFor == tank && (tank == null || darkRescan > 0f)) return;
            darkRescan = 0.5f;
            if (darkFor != tank)
            {
                foreach (var r in hiddenAround) if (r != null) r.enabled = true;
                hiddenAround.Clear();
                darkFor = tank;
                if (tank == null)
                {
                    ApplyStoreLook();
                    return;
                }
            }
            foreach (var r in store.GetComponentsInChildren<Renderer>()) HideForExhibit(r, tank);
            foreach (var other in store.Tanks)
            {
                if (other == tank) continue;
                foreach (var fish in other.Tank.Residents) if (fish != null) foreach (var r in fish.GetComponentsInChildren<Renderer>()) HideForExhibit(r, tank);
                foreach (var fish in other.Tank.Corpses) if (fish != null) foreach (var r in fish.GetComponentsInChildren<Renderer>()) HideForExhibit(r, tank);
            }
            foreach (var person in Customer.All) if (person != null) foreach (var r in person.GetComponentsInChildren<Renderer>()) HideForExhibit(r, tank);
        }

        void HideForExhibit(Renderer r, StoreTank tank)
        {
            if (!r.enabled || r.transform.IsChildOf(tank.transform)) return;
            r.enabled = false;
            hiddenAround.Add(r);
        }

        void EnterTank(StoreTank tank)
        {
            panel = Panel.None;
            if (focused == tank) return;
            focused = tank;
            Tank.Focused = tank.Tank;
            hud.Initialize(tank, store.Tanks, cam, ExitTank, () => shopDay.IsOpen, UpgradeTank);
            hud.SetTankNavigation(SwitchTank, NeighbourTank);
            hud.SetStore(store);
            hud.enabled = true;
        }

        /// <summary>Tanks in walking order: back room first, then round the walls of each section in turn.</summary>
        List<StoreTank> TanksInOrder()
        {
            var list = new List<StoreTank>(store.Tanks);
            list.Sort((a, b) => store.SlotOrder(a.Slot).CompareTo(store.SlotOrder(b.Slot)));
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
                var at = focused.transform.position;
                if (focused.Slot != null && focused.Slot.holding) panX = StoreLayout.BigRoomCenterX;
                else panX = focused.InBackRoom ? StoreLayout.BackRoomCenterX : Mathf.Round(at.x / StoreLayout.SectionWidth) * StoreLayout.SectionWidth;
                // Back out over the north end of the East wing for a tank up there.
                panZ = at.z > StoreLayout.SectionDepth * 0.5f ? at.z - 2f - StoreLookZ : 0f;
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
            cam.backgroundColor = Color.Lerp(StoreBackground, NightBackground, night);
        }

        // ---------------------------------------------------------------- evening light

        static readonly Color NightBackground = new Color(0.03f, 0.05f, 0.14f);
        static readonly Color NightSun = new Color(0.45f, 0.55f, 1f);
        Light sun;
        float sunIntensity = -1f, reflectionIntensity;
        Color sunColor;
        UnityEngine.Rendering.SphericalHarmonicsL2 ambientProbe;
        /// <summary>0 in daylight, 1 at night: the evening fades in after closing and out again in the morning.</summary>
        float night;

        /// <summary>
        /// In the evening the sky outside goes dark blue, the sun becomes cool moonlight and the room
        /// dims. Morning brings the day back.
        /// </summary>
        void UpdateNightLook(float dt)
        {
            if (sun == null)
            {
                sun = RenderSettings.sun;
                if (sun == null)
                    foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (light.type == LightType.Directional && light.name != "Studio light") { sun = light; break; }
                if (sun == null) return;
                sunIntensity = sun.intensity;
                sunColor = sun.color;
                ambientProbe = RenderSettings.ambientProbe;
                reflectionIntensity = RenderSettings.reflectionIntensity;
            }
            float target = shopDay.Phase == DayPhase.Evening ? 1f : 0f;
            if (Mathf.Approximately(night, target) && Time.frameCount > 2) return;
            night = Mathf.MoveTowards(night, target, dt / 2.5f);
            float eased = night * night * (3f - 2f * night);
            sun.intensity = Mathf.Lerp(sunIntensity, sunIntensity * 0.3f, eased);
            sun.color = Color.Lerp(sunColor, NightSun, eased);
            RenderSettings.ambientProbe = ambientProbe * Mathf.Lerp(1f, 0.4f, eased);
            RenderSettings.reflectionIntensity = Mathf.Lerp(reflectionIntensity, reflectionIntensity * 0.35f, eased);
            cam.backgroundColor = Color.Lerp(StoreBackground, NightBackground, eased);
        }

        // ---------------------------------------------------------------- screen (UI Toolkit)
        //
        // The bars, to-do bar, milestones, log and labels over the store are one screen,
        // Resources/UI/StoreHud.uxml; the register, fish seller, supplier, buy-a-tank, unlock,
        // day report and shopkeeper windows share one window (Window.uxml, named per window so
        // FishBreeder.uss sizes each), and the "are you sure" question has its own on top.

        UIDocument hudDocument;
        VisualElement hudRoot, worldView, topBarView, statusBarView, underBars, todoView, milestonesView, clockView, bannersView, logView, watchView;
        UiFrame ui = new UiFrame();
        UiWindow window, confirm;

        void Awake()
        {
            hudDocument = UiPanels.Open("Store HUD", "StoreHud", transform, 5);
            hudRoot = hudDocument.rootVisualElement;
            worldView = hudRoot.Q("world");
            topBarView = hudRoot.Q("top-bar");
            statusBarView = hudRoot.Q("status-bar");
            underBars = hudRoot.Q("under-bars");
            todoView = hudRoot.Q("todo");
            milestonesView = hudRoot.Q("milestones");
            clockView = hudRoot.Q("clock");
            bannersView = hudRoot.Q("banners");
            logView = hudRoot.Q("log");
            watchView = hudRoot.Q("watch");
            // A click outside a window closes it, except the day report and the shopkeeper pick, which need their buttons.
            window = new UiWindow(transform, "store-window", "", 20, true, () =>
            {
                if (panel != Panel.DayReport && panel != Panel.Owner) panel = Panel.None;
            });
            UiPanels.Show(window.CloseButton, false);
            confirm = new UiWindow(transform, "confirm-window", "", 30, true, null);
            UiPanels.Show(confirm.CloseButton, false);
        }

        void OnDisable()
        {
            UiPanels.Show(hudRoot, false);
            window?.Show(false);
            confirm?.Show(false);
        }

        void OnDestroy()
        {
            if (hudDocument != null) Destroy(hudDocument.gameObject);
            window?.Destroy();
            confirm?.Destroy();
        }

        /// <summary>After the camera has moved, so the labels over the store sit on this frame's view.</summary>
        void LateUpdate()
        {
            if (store == null || cam == null) return;
            UiPanels.Show(hudRoot, true);
            bool watching = ViewMode.Watching, inTank = focused != null;
            bool storeView = !watching && !inTank;

            Fill(watchView, watching, DrawWatchHint);
            UiPanels.Show(underBars, !watching);
            Fill(topBarView, storeView, DrawTopBar);
            Fill(statusBarView, storeView, DrawStatusBar);
            Fill(clockView, !watching && inTank && shopDay.IsOpen, DrawShopClock);
            todoView.EnableInClassList("fb-todo--tank", inTank);
            Fill(todoView, !watching && (inTank || panel == Panel.None), DrawToDoBar);
            Fill(milestonesView, storeView && panel == Panel.None, DrawMilestones);
            bannersView.style.top = inTank ? 140f : 200f;
            Fill(bannersView, !watching, DrawLevelUps);
            Fill(logView, storeView, () => GameLogPanel.Fill(ui, 4));
            Fill(worldView, storeView, DrawWorld);

            bool showWindow = storeView && panel != Panel.None;
            window.Show(showWindow);
            if (showWindow) DrawWindow();
            bool asking = storeView && pendingStep != PendingStep.None;
            confirm.Show(asking);
            if (asking) DrawStepConfirm();
        }

        /// <summary>Shows or hides a part of the screen, filling it this frame when it shows.</summary>
        void Fill(VisualElement part, bool show, System.Action draw)
        {
            UiPanels.Show(part, show);
            ui.Begin(part);
            if (show) draw();
            ui.End();
            // Nothing drawn (an empty log, no milestones): the part takes no room.
            if (show && part.childCount == 0) UiPanels.Show(part, false);
        }

        void DrawTopBar()
        {
            // Money rolls up or down to its new value, flashing green or coral.
            string moneyColor = moneyFlash > 0.01f ? ColorHex(Color.Lerp(UiTheme.Sand, UiTheme.Leaf, moneyFlash))
                : moneyFlash < -0.01f ? ColorHex(Color.Lerp(UiTheme.Sand, UiTheme.Coral, -moneyFlash)) : UiTheme.SandHex;
            string moneyText = DevMode.On ? $"<size=14><color={UiTheme.SunHex}>DEV MODE</color></size>" : $"<color={moneyColor}>{Economy.Format(Mathf.RoundToInt(shownMoney))}</color>";
            if (ui.Button($"<size=17><b>{moneyText}</b></size>", "fb-button fb-flat fb-money", true, "Finances (F)")) FinancesView.Toggle();
            var money = ui.Last;
            if (coinBurst && money.worldBound.width > 0f)
            {
                coinBurst = false;
                var at = money.worldBound;
                Fx.ScreenBurst(new Vector2(at.x + 40f, at.center.y), UiTheme.Sun, 14, false, 0.8f);
            }
            ui.Label($"<b>Day {Economy.Day}</b>", "fb-text fb-bar-item");
            if (ui.Button($"<color={UiTheme.SunHex}>{Stars(Economy.Reputation * 5f)}</color>", "fb-button fb-flat", true, "Reputation (Y)")) ReputationView.Toggle();
            ui.Label(FoodSummary(), "fb-text fb-bar-item");
            if (shopDay.IsOpen)
            {
                var today = shopDay.Today;
                var aquarium = PublicAquarium.Today;
                string tickets = aquarium != null ? $"   Aquarium {aquarium.tickets} tickets +{Economy.Format(aquarium.earnings)}" : "";
                ui.Label($"<color={UiTheme.LeafHex}><b>OPEN</b></color> {Clock(shopDay.TimeLeft)}   Visitors {today.visitors}   Sold {today.fishSold}   +{Economy.Format(today.earnings)}{tickets}", "fb-text fb-bar-item");
                ui.Flex();
                DrawDevControls();
                return;
            }
            int setup = Economy.DaysUntilOpening;
            bool morning = shopDay.Phase == DayPhase.Morning;
            string phase = morning ? $"<color={UiTheme.SunHex}><b>MORNING</b></color>" : $"<color={UiTheme.HibiscusHex}><b>EVENING</b></color>";
            if (morning && setup > 0) phase += $"  <size=12>opening in {setup} day{(setup == 1 ? "" : "s")}</size>";
            ui.Label(phase, "fb-text fb-bar-item");
            ui.Flex();
            if (ui.Button("Supplier (B)", "fb-button fb-bar-button")) ToggleSupplier();
            if (AquariumBuilt && ui.Button($"Aquarium <color={UiTheme.SunHex}>{PublicAquarium.Stars:0.0}★</color>", "fb-button fb-bar-button", true, "The public aquarium: ticket price, estimate, booth upgrades, rating (U)")) AquariumView.Toggle();
            if (ui.Button("Journal (J)", "fb-button fb-bar-button")) JournalView.Toggle();
            if (ui.Button(ResearchView.ButtonLabel(), "fb-button fb-bar-button")) ResearchView.Toggle();
            if (ui.Button(PerksButtonLabel(), "fb-button fb-bar-button")) PerksView.Toggle();
            if (ui.Button("Watch (V)", "fb-button fb-bar-button")) ToggleWatch();
            DrawPhaseButtons(false);
        }

        /// <summary>
        /// What the player can do next: in the morning open the shop or keep it closed (only "let the
        /// day pass" during the setup days); in the evening go to sleep.
        /// </summary>
        void DrawPhaseButtons(bool wide)
        {
            string plain = wide ? "fb-button fb-grow" : "fb-button fb-bar-button";
            string primary = plain + " fb-button--primary";
            if (shopDay.Phase == DayPhase.Evening)
            {
                if (ui.Button("Go to sleep", primary, true, "The night passes: fish grow, eggs hatch, babies are born, water gets dirtier.")) AskStep(PendingStep.Sleep);
                return;
            }
            if (shopDay.Phase != DayPhase.Morning) return;
            if (!Economy.CanOpenShop)
            {
                if (ui.Button("Let the day pass", primary, true, "Time passes until evening, with the shop shut: fish grow, eggs hatch, water gets dirtier.")) AskStep(PendingStep.PassDay);
                return;
            }
            if (ui.Button("Stay closed", plain, true, "Let the day pass without opening: no customers, no sales.")) AskStep(PendingStep.StayClosed);
            if (ui.Button("Open Shop", primary, true, "Nothing changes in the tanks until you close: time passes while customers are in."))
            {
                panel = Panel.None;
                shopDay.Open();
            }
        }

        enum PendingStep { None, Sleep, PassDay, StayClosed }
        PendingStep pendingStep;
        int pendingChores;

        /// <summary>
        /// Asks before time moves on: always for staying closed, and for sleeping or letting the day
        /// pass only when chores are still waiting.
        /// </summary>
        void AskStep(PendingStep step)
        {
            int chores = new ToDoList().Build(store.Tanks);
            if (step != PendingStep.StayClosed && chores == 0)
            {
                if (step == PendingStep.Sleep) Sleep();
                else StayClosed();
                return;
            }
            pendingStep = step;
            pendingChores = chores;
        }

        void DrawStepConfirm()
        {
            confirm.Title.text = pendingStep == PendingStep.StayClosed ? "Stay closed today?" : "Chores not finished";
            var frame = confirm.Frame;
            confirm.Fill(null, () =>
            {
                string text = pendingStep == PendingStep.StayClosed ? "The shop stays shut all day: no customers, no sales and no reviews, but the fish still grow and the water still gets dirtier." : "";
                if (pendingChores > 0)
                    text += (text.Length > 0 ? "\n\n" : "") + $"<color=#ffd27f>{(pendingChores == 1 ? "One chore is" : $"{pendingChores} chores are")} still waiting: hungry, sick or dead fish, dirty water or broken equipment.</color>";
                frame.Label(text);
            }, () =>
            {
                if (frame.Button("Go back")) pendingStep = PendingStep.None;
                if (frame.Button(pendingStep == PendingStep.StayClosed ? "Stay closed" : "Go anyway", "fb-button fb-button--primary"))
                {
                    var step = pendingStep;
                    pendingStep = PendingStep.None;
                    if (step == PendingStep.Sleep) Sleep();
                    else StayClosed();
                }
            });
        }

        void StayClosed()
        {
            panel = Panel.None;
            shopDay.StayClosed();
        }

        void Sleep()
        {
            panel = Panel.None;
            todo.Dismiss();
            shopDay.GoToSleep();
        }

        static string PerksButtonLabel() => Perks.TotalAvailable > 0 ? $"Perks (P) <color={UiTheme.SunHex}>●{Perks.TotalAvailable}</color>" : "Perks (P)";

        static string Stars(float stars)
        {
            int full = Mathf.Clamp(Mathf.RoundToInt(stars), 0, 5);
            return new string('★', full) + "<color=#5f8a92>" + new string('★', 5 - full) + "</color>";
        }

        static string ColorHex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

        /// <summary>The three levels with their XP bars, and today's goals.</summary>
        void DrawStatusBar()
        {
            LevelMeter(XpTrack.Fishkeeping);
            LevelMeter(XpTrack.Shopkeeping);
            LevelMeter(XpTrack.Research);
            if (Perks.TotalAvailable > 0)
            {
                // A pulsing reminder while level-up points wait to be spent.
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                if (ui.Button($"★ {Perks.TotalAvailable} perk point{(Perks.TotalAvailable == 1 ? "" : "s")}!", "fb-button fb-button--primary fb-bar-button")) PerksView.Open();
                ui.Last.style.backgroundColor = Color.Lerp(new Color(0.98f, 0.47f, 0.36f), UiTheme.Sun, pulse * 0.6f);
            }
            var goals = new List<string>();
            foreach (var goal in Progression.Goals)
                goals.Add(goal.Done ? $"<color={UiTheme.LeafHex}>{goal.Describe()} ✔</color>" : $"{goal.Describe()} <b>{goal.progress}/{goal.target}</b>");
            ui.Label($"<b><color={UiTheme.SunHex}>Today:</color></b> " + (goals.Count > 0 ? string.Join("   ", goals) : "no goals"), "fb-text fb-goals");
        }

        /// <summary>A level, its XP bar and the XP numbers, side by side on one line.</summary>
        void LevelMeter(XpTrack track)
        {
            int level = Progression.Level(track);
            string name = Progression.TrackName(track);
            ui.BeginRow("fb-row fb-meter");
            ui.Label($"<b>{name} {level}</b>", "fb-text fb-meter__name");
            float fill = Progression.IsMaxLevel(track) ? 1f : Progression.Xp(track) / (float)Progression.XpToNext(level);
            var color = track == XpTrack.Fishkeeping ? UiTheme.Aqua : track == XpTrack.Shopkeeping ? UiTheme.Sun : UiTheme.Hibiscus;
            ui.Bar(fill, color, "fb-bar fb-meter__bar");
            ui.Label(Progression.IsMaxLevel(track) ? "max" : $"{Progression.Xp(track)}/{Progression.XpToNext(level)}", "fb-text fb-meter__xp");
            ui.Pop();
        }

        /// <summary>The next few milestones down the right side, with progress and what each pays.</summary>
        void DrawMilestones()
        {
            var upcoming = Milestones.Upcoming(3);
            if (upcoming.Count == 0) return;
            ui.BeginRow("fb-line");
            ui.Label("MILESTONES", "fb-heading fb-grow");
            ui.Label($"{Milestones.DoneCount}/{Milestones.Total}", "fb-small fb-value");
            ui.Pop();
            foreach (var milestone in upcoming)
            {
                int have = Mathf.Min(Milestones.Progress(milestone.goal), milestone.target);
                ui.Line($"<b>{milestone.title}</b>", $"<color={UiTheme.LeafHex}><b>+{Economy.Format(milestone.reward)}</b></color>");
                ui.Label(milestone.Describe(), "fb-small");
                ui.Bar(have / (float)Mathf.Max(1, milestone.target), UiTheme.Sun, "fb-bar fb-bar--thin");
            }
        }

        void ToggleWatch()
        {
            ViewMode.Toggle();
            if (ViewMode.Watching && panel != Panel.DayReport) panel = Panel.None;
        }

        /// <summary>In watch mode: just a hint at first, and a small button to bring the menus back when the mouse moves.</summary>
        void DrawWatchHint()
        {
            float since = Time.unscaledTime - ViewMode.Since;
            if (since < 3.5f)
            {
                ui.Label("Watch mode. Press <b>V</b> or <b>Esc</b> to bring the menus back.", "fb-world-label fb-watch-hint").style.opacity = Mathf.Clamp01(3.5f - since);
            }
            if (Time.unscaledTime - mouseMovedAt < 2.5f || since < 3.5f)
                if (ui.Button("Show menus (V)", "fb-button fb-watch-button")) ViewMode.Set(false);
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
            if (Economy.GrandmaNote != null) subtitle += $". {Economy.GrandmaNote}!";
            Fx.Day($"Day {Economy.Day}", subtitle);
        }

        /// <summary>"Food 40": feedings in stock across every food, red when you've run out.</summary>
        static string FoodSummary()
        {
            int total = 0;
            foreach (FoodType food in System.Enum.GetValues(typeof(FoodType))) total += Economy.FoodPortions(food);
            return total > 0 ? $"Food: {total} feedings" : "<color=#ff8080>Food: none!</color>";
        }

        void DrawLevelUps()
        {
            foreach (var levelUp in Progression.LevelUps)
            {
                float alpha = Mathf.Clamp01(6f - levelUp.age) * Mathf.Clamp01(levelUp.age * 4f);
                float drop = (1f - Mathf.Clamp01(levelUp.age * 3f)) * -40f;
                string unlocks = levelUp.unlocks.Count > 0 ? $"\n<color={UiTheme.AquaHex}>Unlocked:</color> " + string.Join(", ", levelUp.unlocks) : "";
                var banner = ui.Label($"<size=24><b><color={UiTheme.SunHex}>★ {Progression.TrackName(levelUp.track)} level {levelUp.level}! ★</color></b></size>{unlocks}", "fb-banner");
                banner.style.opacity = alpha;
                banner.style.translate = new Translate(0f, drop);
                var at = banner.worldBound;
                if (at.width > 0f && celebratedLevelUps.Add(levelUp)) Fx.ScreenBurst(at.center, null, 40, false, 1.4f);
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
            hud.ShowCare();
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
                    ui.Label("<b><color=#9fe39f>All caught up!</color></b> Every problem from closing is dealt with.", "fb-banner fb-banner--small");
                else if (!shopDay.IsOpen && todo.Count > 0 && todo.Remaining > 0)
                {
                    ui.BeginRow("fb-panel fb-row fb-todo__paused");
                    ui.Label(todo.Remaining == 1 ? "One thing still needs seeing to." : $"{todo.Remaining} things still need seeing to.", "fb-text fb-grow");
                    if (ui.Button("Resume", "fb-button fb-button--gap") && todo.Next()) GoToCurrent();
                    if (ui.Button("Dismiss")) todo.Dismiss();
                    ui.Pop();
                }
                return;
            }

            bool resolved = item.Resolved;
            ui.BeginColumn("fb-panel fb-column");
            ui.Line($"<b>To do {todo.Index + 1} of {todo.Count}</b>   {item.tank.Title}", resolved ? "<color=#9fe39f><b>Done!</b></color>" : "");
            ui.Label(resolved ? $"<color=#9fe39f>{item.title}</color>" : $"<b>{item.title}</b> {item.hint}");
            ui.BeginRow("fb-row fb-wrap");
            if (ui.Button("< Back", "fb-button fb-button--gap") && todo.Previous()) GoToCurrent();
            if (ui.Button("Take me there", "fb-button fb-button--gap")) GoToCurrent();
            if (ui.Button(resolved ? "Next >" : "Skip >", "fb-button fb-button--gap")) NextToDo();
            ui.Flex();
            if (ui.Button("Stop for now")) todo.Pause();
            ui.Pop();
            ui.Pop();
        }

        void DrawShopClock()
        {
            ui.Label($"<color={UiTheme.LeafHex}><b>OPEN</b></color> {Clock(shopDay.TimeLeft)}", "fb-text fb-bar-item fb-grow");
            DrawDevControls();
        }

        void DrawDevControls()
        {
            foreach (var speed in Speeds)
            {
                bool on = Mathf.Approximately(shopDay.SpeedMultiplier, speed);
                if (ui.Button($"{speed:0}x", on ? "fb-button fb-tab fb-tab--on fb-speed" : "fb-button fb-tab fb-speed") && !on) shopDay.SetSpeed(speed);
            }
            if (ui.Button("Skip day (K)", "fb-button fb-bar-button")) shopDay.SkipRestOfDay();
        }

        /// <summary>
        /// Keyboard versions of the dev controls: 1/2/3 set 1x/5x/20x and K skips the rest of the day
        /// (with the shop shut, K lets the day pass or goes to sleep).
        /// In the editor and development builds F8 runs the UI check; in the editor F9 puts the next
        /// equipment model in the tank you're looking at. F10 turns dev mode on and off (editor and
        /// development builds).
        /// </summary>
        void HandleDevKeys()
        {
            if (Input.GetKeyDown(KeyCode.F10)) DevMode.Toggle();
            if ((Application.isEditor || Debug.isDebugBuild) && Input.GetKeyDown(KeyCode.F8) && !UiToolkitCheck.Sweeping) RunUiCheck();
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F9)) CycleEquipment();
#endif
            if (!shopDay.IsOpen)
            {
                // K also moves a closed day on: morning to evening, evening to the next morning.
                if ((DevMode.On || Application.isEditor) && Input.GetKeyDown(KeyCode.K) && panel != Panel.DayReport && !UiPanels.Typing)
                {
                    if (shopDay.Phase == DayPhase.Evening) Sleep();
                    else if (shopDay.Phase == DayPhase.Morning) StayClosed();
                }
                return;
            }
            for (int i = 0; i < Speeds.Length && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) shopDay.SetSpeed(Speeds[i]);
            if (Input.GetKeyDown(KeyCode.K)) shopDay.SkipRestOfDay();
        }

#if UNITY_EDITOR
        /// <summary>Editor only: installs the next model of every kind of equipment in the focused tank, to see each design.</summary>
        void CycleEquipment()
        {
            var tank = Tank.Focused;
            if (tank == null) return;
            var current = tank.GetEquipment(EquipmentKind.Filter);
            int tier = current == null ? 0 : (current.tier + 1) % Mathf.Max(1, Equipment.Tiers(EquipmentKind.Filter).Count);
            foreach (var kind in Equipment.Kinds) tank.Install(kind, tier);
            GameLog.Add($"(Editor) Equipment model {tier + 1} in {tank.Title}.");
        }
#endif

        /// <summary>
        /// The UI check (F8): shows every window, every supplier tab, both day reports, the journal,
        /// perks and research, and every tab of every tank with a fish picked, at each of the
        /// <see cref="UiToolkitCheck.SweepSizes"/>, saves a picture of each and lists what doesn't fit.
        /// </summary>
        public Coroutine RunUiCheck()
        {
            if (focused != null) ExitTank();
            following = -1;
            GameLog.Add("UI check (dev): showing every window at five screen sizes...");
            var pages = new List<KeyValuePair<string, System.Action>>();
            void Page(string name, System.Action show) => pages.Add(new KeyValuePair<string, System.Action>(name, show));
            void Reset()
            {
                panel = Panel.None;
                JournalView.Close();
                PerksView.Close();
                ResearchView.Close();
                ReputationView.Close();
                FinancesView.Close();
                AquariumView.Close();
                if (focused != null) ExitTank();
            }
            var savedReport = shopDay.LastReport;
            Page("Store", Reset);
            Page("Register", () => { Reset(); panel = Panel.Register; });
            if (!shopDay.IsOpen)
                foreach (bool seller in new[] { false, true })
                    foreach (int tab in seller ? FishSellerTabs : SupplierMenuTabs)
                        Page($"{(seller ? "Fish seller" : "Supplier")} {SupplierTabs[tab]}", () => { Reset(); atFishSeller = seller; supplierTab = tab; panel = Panel.Supplier; });
            Page("Shopkeeper", () => { Reset(); panel = Panel.Owner; });
            foreach (bool morning in new[] { false, true })
                Page(morning ? "Morning report" : "Day report", () =>
                {
                    Reset();
                    shopDay.LastReport = new DayReport
                    {
                        day = Economy.Day, morning = morning, visitors = 12, fishSold = 4, earnings = 260, happy = 6, grossed = 1, sad = 1, mad = 2, stars = 3.5f,
                        reviews = new List<string> { "Nice fish, but the prices are ridiculous.", "Lots of tanks, hardly any fish to buy.", "Watched a big fish bully the little ones. Horrible." },
                        tanks = "3 eggs hatched, 5 fry born, a new pregnancy or clutch of eggs, 2 fish fell ill, 1 died, a fish was eaten by a tank mate, 2 fish were bitten or nipped.",
                    };
                    panel = Panel.DayReport;
                });
            foreach (var slot in store.Slots)
                if (slot.IsOpen && slot.tank == null)
                {
                    var open = slot;
                    Page("Buy a tank", () => { Reset(); shopDay.LastReport = savedReport; pendingSlot = open; panel = Panel.BuyTank; });
                    break;
                }
            if (store.NextLockedSection != null)
                Page("Unlock", () => { Reset(); pendingSection = store.NextLockedSection; panel = Panel.Unlock; });
            Page("Stay closed?", () => { Reset(); pendingStep = PendingStep.StayClosed; pendingChores = 2; });
            Page("Journal", () => { Reset(); pendingStep = PendingStep.None; if (Species.All.Count > 0) JournalView.Open(Species.All[0].id); });
            Page("Perks", () => { Reset(); PerksView.Open(); });
            Page("Research", () => { Reset(); ResearchView.Open(); });
            Page("Reputation", () => { Reset(); ReputationView.Open(); });
            Page("Finances", () => { Reset(); FinancesView.Open(); });
            foreach (var tank in new List<StoreTank>(store.Tanks))
            {
                for (int i = 0; i < hud.TabCount; i++)
                {
                    int tab = i;
                    Page($"{tank.Title} tab {tab + 1}", () => { Reset(); EnterTank(tank); hud.ClearSelection(); hud.ShowTab(tab); });
                }
                if (tank.Tank.Residents.Count > 0)
                    Page($"{tank.Title} fish", () => { Reset(); EnterTank(tank); hud.ShowTab(0); if (tank.Tank.Residents.Count > 0) hud.Select(tank.Tank.Residents[0]); });
            }
            return StartCoroutine(UiToolkitCheck.Sweep("Store", null, pages, () =>
            {
                Reset();
                shopDay.LastReport = savedReport;
                if (savedReport != null) panel = Panel.DayReport;
                pendingStep = PendingStep.None;
                string summary = $"UI check (dev): {UiCheck.Problems.Count} problem{(UiCheck.Problems.Count == 1 ? "" : "s")}. Pictures are in {UiToolkitCheck.PictureFolder}.";
                GameLog.Add(summary + (UiCheck.Problems.Count > 0 ? " See the Console." : ""));
            }));
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        // ---------------------------------------------------------------- over the store

        /// <summary>Labels over the tanks and everything else clickable, shoppers' thought bubbles and sale popups.</summary>
        void DrawWorld()
        {
            if (panel == Panel.None)
            {
                if (following < 0) DrawWorldLabels();
                DrawCustomerBubbles();
            }
            DrawMoneyPopups();
        }

        void DrawWorldLabels()
        {
            foreach (var tank in store.Tanks)
            {
                var t = tank.Tank;
                int eggs = EggClutch.EggsIn(t);
                string count = $"{t.SmallCount}/{t.Capacity} fish" + (t.MediumCount > 0 ? $", {t.MediumCount}/{t.MediumCapacity} medium" : "");
                string contents = (t.IsOvercrowded ? $"<color=#ff8080>{count}</color>" : count) + (eggs > 0 ? $", {eggs} eggs" : "");
                int sick = 0;
                foreach (var fish in t.Residents) if (!fish.IsDead && fish.Health.IsSick) sick++;
                string sickLine = sick > 0 ? $"\n<color=#ff6b6b><b>+ {sick} sick</b></color>" : "";
                string warning = sickLine + (t.IsOvercrowded ? "\n<color=#ff8080>overcrowded!</color>" : t.Corpses.Count > 0 ? "\n<color=#ff8080>dead fish!</color>" : t.WaterHealth < Balance.Data.shop.grossWaterQuality ? "\n<color=#ffd27f>dirty water</color>" : "");
                WorldLabel(t.InnerBounds.center + Vector3.up * 0.65f, $"<b>{tank.Title}</b>\n{contents}{warning}");
            }
            WorldLabel(store.RegisterPoint, "<b>Register</b>");
            if (AquariumBuilt)
            {
                var why = PublicAquarium.WhyClosed(store);
                string state = shopDay.IsOpen && PublicAquarium.Today != null ? $"open, {PublicAquarium.Today.tickets} tickets sold" : why != null ? $"<color=#ffd27f>{why}</color>" : "opens with the shop";
                if (ClickableLabel(store.BoothLabelPoint, $"<b>Ticket booth</b>  {Economy.Format(PublicAquarium.TicketPrice)}\n<color={UiTheme.SunHex}>{ReputationView.Stars(PublicAquarium.Stars)}</color>  <size=11>{state}</size>")) AquariumView.Open();
                if (panZ >= 4f) WorldLabel(store.ExpansionPoint, "<b>Future expansion</b>\n<size=11>kept free for later</size>");
            }
            // The seller is behind the breeding room's back wall: only labelled once the view is out there.
            if (panZ >= 4f) WorldLabel(store.FishSellerPoint, ScubaDives.Out ? "<b>Fish seller's van</b>\nout diving, back at the next time step" : FishSeller.Away ? "<b>Fish seller's van</b>\ngone home, back in the morning" : "<b>Fish seller's van</b>\negg and fry boxes, dives");
            // The van is out of sight behind the breeding room: a sign over the back door slides the view there.
            if (panZ < 4f && WorldButton(store.BackDoorSign, FishSeller.Away ? "Parking lot ▲" : "Fish seller's van ▲  <size=11>(W)</size>", "fb-button fb-world-button")) LookAtParkingLot();

            if (!shopDay.IsOpen)
                foreach (var slot in store.Slots)
                    if (slot.IsOpen && slot.tank == null && ClickableLabel(slot.FloorFront + Vector3.up * 0.3f, slot.Exhibit ? $"+ {PublicAquarium.ClimateName(slot.exhibit)} aquarium\n{Economy.Format(StoreLayout.TankPrice(slot, TankRole.Exhibit))}" : slot.wall ? $"+ Wall aquarium\n{Economy.Format(StoreLayout.TankPrice(slot, TankRole.Display))}" : slot.holding ? $"+ Holding tank\n{Economy.Format(StoreLayout.TankPrice(slot, TankRole.Holding))}" : slot.backRoom ? "+ Breeder or quarantine tank" : "+ Tank spot"))
                    {
                        pendingSlot = slot;
                        panel = Panel.BuyTank;
                    }

            foreach (var section in store.Sections)
                if (!section.unlocked)
                {
                    var why = store.WhyLocked(section);
                    string requirement = why == null ? $"Unlock for {Economy.Format(section.Price)}"
                        : section == store.NextLockedSection ? why : $"{StoreLayout.SectionStars(section):0.0} stars and {Economy.Format(section.Price)}";
                    if (ClickableLabel(section.LabelPoint, $"<b>{section.name}</b> (locked)\n{requirement}"))
                    {
                        pendingSection = section;
                        panel = Panel.Unlock;
                    }
                }
        }

        /// <summary>Where a point in the store is on the screen; false when it's behind the camera.</summary>
        bool ToPanel(Vector3 world, out Vector2 point)
        {
            var screen = cam.WorldToScreenPoint(world);
            point = UiPanels.ScreenToPanel(hudRoot, screen);
            return screen.z > 0f;
        }

        /// <summary>Puts an element with its bottom middle on a point in the store (hidden when that's behind the camera).</summary>
        void Pin(VisualElement element, Vector3 world, float lift = 0f)
        {
            bool visible = ToPanel(world, out var point);
            UiPanels.Show(element, visible);
            element.style.left = point.x;
            element.style.top = point.y - lift;
        }

        void WorldLabel(Vector3 world, string text) => Pin(ui.Label(text, "fb-world-label"), world);

        /// <summary>A label over the store that's also a button: the floor spot under it isn't the only thing to click.</summary>
        bool ClickableLabel(Vector3 world, string text) => WorldButton(world, text, "fb-button fb-world-label fb-world-label--button");

        bool WorldButton(Vector3 world, string text, string classes)
        {
            bool clicked = ui.Button(text, classes);
            Pin(ui.Last, world);
            return clicked;
        }

        void DrawCustomerBubbles()
        {
            // Shoppers and aquarium visitors alike.
            foreach (var customer in Customer.All)
            {
                if (customer == null || customer.Mood == Mood.None) continue;
                float pop = Mathf.Clamp01((3.5f - customer.MoodTimeLeft) * 6f);
                float size = 54f * Mathf.Lerp(0.5f, 1f, pop);
                var bubble = ui.Begin("bubble", "fb-mood");
                Pin(bubble, customer.HeadPoint);
                bubble.style.width = size;
                bubble.style.height = size;
                bubble.style.backgroundImage = MoodIcons.Bubble;
                var face = ui.Image(MoodIcons.Get(customer.Mood), size * 0.62f, size * 0.62f, "fb-mood__face");
                face.style.top = size * 0.1f;
                if (customer.Mood == Mood.Mad) ui.Label("<b>$</b>", "fb-mood__money");
                ui.Pop();
            }
        }

        void DrawMoneyPopups()
        {
            foreach (var popup in shopDay.Popups)
            {
                float alpha = Mathf.Clamp01(2.2f - popup.age);
                float bounce = 1f + 0.6f * Mathf.Clamp01(1f - popup.age * 5f);
                var label = ui.Label($"<b>+{Economy.Format(popup.amount)}</b>", "fb-money-popup");
                label.style.fontSize = Mathf.RoundToInt(24 * bounce);
                label.style.opacity = alpha;
                Pin(label, popup.position + Vector3.up * (0.3f + popup.age * 0.35f), -15f);
            }
        }

        /// <summary>A left click on the store (not on a menu): a tank, the register, the fish seller and so on.</summary>
        void HandleStoreClick()
        {
            if (!Input.GetMouseButtonDown(0) || focused != null || ViewMode.Watching || panel != Panel.None || pendingStep != PendingStep.None) return;
            if (UiPanels.PointerOverUi || FxOverlay.MouseOverCard) return;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 100f, StoreClickable.Mask, QueryTriggerInteraction.Collide)) return;
            var clickable = hit.collider.GetComponent<StoreClickable>();
            if (clickable == null) return;
            switch (clickable.kind)
            {
                case StoreClickable.Kind.Tank: EnterTank(clickable.tank); break;
                case StoreClickable.Kind.Register: panel = Panel.Register; break;
                case StoreClickable.Kind.FishSeller: OpenSupplier(true); break;
                case StoreClickable.Kind.TicketBooth: AquariumView.Open(); break;
                case StoreClickable.Kind.EmptySlot: pendingSlot = clickable.slot; panel = Panel.BuyTank; break;
                case StoreClickable.Kind.LockedSection: pendingSection = clickable.section; panel = Panel.Unlock; break;
            }
        }

        // ---------------------------------------------------------------- windows

        void DrawWindow()
        {
            switch (panel)
            {
                case Panel.Register: Open("register-window", "Register"); window.Fill(null, DrawRegister, CloseButton); break;
                case Panel.Supplier: Open("supplier-window", atFishSeller ? "Fish seller" : "Supplier"); window.Fill(SupplierHeader, DrawSupplier, CloseButton); break;
                case Panel.Unlock: Open("unlock-window", pendingSection.IsAquarium ? $"Build the {pendingSection.name.ToLower()}?" : $"Unlock the {pendingSection.name}?"); window.Fill(null, DrawUnlock, UnlockButtons); break;
                case Panel.BuyTank: Open("buy-tank-window", BuyTankTitle()); window.Fill(null, DrawBuyTank, () => { if (w.Button("Cancel")) panel = Panel.None; }); break;
                case Panel.DayReport: Open("day-report-window", DayReportTitle()); window.Fill(null, DrawDayReport, DayReportButton); break;
                case Panel.Owner: Open("owner-window", "Who runs the shop?"); window.Fill(null, DrawOwnerPick); break;
            }
        }

        /// <summary>Names the shared window for this panel (its USS id sets its size) and gives it its title.</summary>
        void Open(string name, string title)
        {
            if (window.Box.name != name)
            {
                window.Box.name = name;
                // A different window: start its parts afresh (scrolled to the top).
                window.Body.Clear();
                window.Footer.Clear();
            }
            window.Title.text = title;
        }

        /// <summary>The frame that fills the window (the to-do bar and labels have their own, <see cref="ui"/>).</summary>
        UiFrame w => window.Frame;

        void CloseButton()
        {
            if (w.Button("Close")) panel = Panel.None;
        }

        void DrawRegister()
        {
            w.Label($"Balance: <b>{Economy.Format(Economy.Money)}</b>     Day {Economy.Day}     Reputation {Economy.Reputation * 100f:0}%");
            if (shopDay.IsOpen)
            {
                w.Label($"Open for another {Clock(shopDay.TimeLeft)}. About {shopDay.CustomersPerHour:0} customers an hour. Time passes while the shop is open; what happens in the tanks shows when you close.");
                if (w.Button("Close early")) shopDay.Close();
            }
            else
            {
                if (shopDay.Phase == DayPhase.Evening)
                    w.Label("Evening. See to anything that happened today, settle the fish in, then go to sleep: the night passes and a new day begins.");
                else if (!Economy.CanOpenShop)
                {
                    int setup = Economy.DaysUntilOpening;
                    w.Label($"Morning. The grand opening is in <b>{setup} day{(setup == 1 ? "" : "s")}</b>. Until then, raise fish to sell: let the day pass, then settle the fish in for the night.");
                }
                else
                    w.Label($"Morning. Get the tanks ready, then open: nothing changes in the tanks between now and opening. Time passes while the shop is open (the day lasts {Clock(Balance.Data.shop.dayLengthSeconds)}), and you see what happened when you close. Or keep the shop closed today: the day still passes, but nobody comes in.");
                w.BeginRow("fb-row fb-gap-row");
                DrawPhaseButtonsInto(w);
                w.Pop();
            }

            w.Label("<b>Recent transactions</b>", "fb-text fb-gap-above");
            var ledger = Economy.Ledger;
            if (ledger.Count == 0) w.Label("<i>Nothing yet.</i>");
            for (int i = ledger.Count - 1; i >= Mathf.Max(0, ledger.Count - 7); i--)
                w.Label(ledger[i]);

            w.BeginRow("fb-row fb-gap-above");
            w.Label("Shopkeeper:", "fb-text fb-header-text");
            if (w.Button("Woman", ShopOwner.Look == OwnerLook.Woman ? "fb-button fb-tab fb-tab--on" : "fb-button fb-tab")) ShopOwner.Look = OwnerLook.Woman;
            if (w.Button("Man", ShopOwner.Look == OwnerLook.Man ? "fb-button fb-tab fb-tab--on" : "fb-button fb-tab")) ShopOwner.Look = OwnerLook.Man;
            w.Pop();
            w.Label("<b>Developer</b>", "fb-text fb-gap-above");
            w.BeginRow("fb-row fb-wrap");
            if (w.Button("+$1,000 (dev)", "fb-button fb-button--gap")) Economy.Earn(1000, "developer cash");
            if (shopDay.IsOpen && w.Button("Skip rest of day (dev)", "fb-button fb-button--gap")) shopDay.SkipRestOfDay();
#if UNITY_EDITOR
            if (w.Button("+500 Fishkeeping XP (dev)", "fb-button fb-button--gap")) Progression.DevAward(XpTrack.Fishkeeping, 500);
            if (w.Button("+500 Shopkeeping XP (dev)", "fb-button fb-button--gap")) Progression.DevAward(XpTrack.Shopkeeping, 500);
            if (!Economy.CanOpenShop && w.Button("Skip the setup days (dev)", "fb-button fb-button--gap")) Economy.DevSkipSetup();
#endif
            w.Pop();
        }

        /// <summary>The phase buttons, wide, in the register (drawn with the window's frame there).</summary>
        void DrawPhaseButtonsInto(UiFrame frame)
        {
            var bar = ui;
            ui = frame;
            DrawPhaseButtons(true);
            ui = bar;
        }

        string DayReportTitle()
        {
            var report = shopDay.LastReport;
            if (report == null) return "";
            if (report.morning) return $"Good morning! Day {report.day}";
            if (report.stayedClosed) return $"Day {report.day}: the shop stayed closed";
            return $"Day {report.day} is over";
        }

        void DrawDayReport()
        {
            var report = shopDay.LastReport;
            if (report == null)
            {
                panel = Panel.None;
                return;
            }
            if (report.morning)
            {
                int setup = Economy.DaysUntilOpening;
                w.Label(setup > 1 ? $"<color={UiTheme.SunHex}>{setup} days until the grand opening.</color> Feed and clean, then let the day pass."
                    : setup == 1 ? $"<color={UiTheme.SunHex}>Tomorrow is the grand opening!</color> Make sure the for sale tank has some juveniles or adults."
                    : Economy.Day == Balance.Data.shop.setupDays + 1 ? $"<color={UiTheme.SunHex}>Today is the grand opening!</color> Get the tanks ready, then open the shop."
                    : "Get the tanks ready, then open the shop. Nothing changes in the tanks until you open.");
            }
            else if (report.stayedClosed)
                w.Label(Economy.CanOpenShop ? "No customers today, so no sales and no reviews." : "Another day of getting ready. See to the fish, then go to sleep.");
            else
            {
                w.Label($"Visitors: {report.visitors}     Fish sold: {report.fishSold}     Earned: <b><color={UiTheme.LeafHex}>{Economy.Format(report.earnings)}</color></b>");
                w.Label($"Reactions: {report.happy} happy, {report.grossed} grossed out, {report.sad} upset, {report.mad} angry about prices");
                w.Label($"Reviews: <size=18><color={UiTheme.SunHex}>{Stars(report.stars)}</color></size> ({report.stars:0.#} / 5)   Reputation {(report.reputationChange >= 0f ? "+" : "")}{report.reputationChange * 5f:0.00} stars (now {StoreLayout.Stars:0.0})");
                if (report.visitors == 0) w.Label("<i>Nobody came in today. More tanks, especially display tanks, bring in more people.</i>");
                foreach (var review in report.reviews) w.Label($"<i>\"{review}\"</i>");
                w.Label("See what's helping and hurting your reputation: click the stars or press Y.", "fb-small");
                DrawAquariumReport(report.aquarium);
            }
            w.Label(report.morning ? "OVERNIGHT IN THE TANKS" : "TODAY IN THE TANKS", "fb-heading");
            w.Label(report.tanks);
            if (!report.morning) w.Label("When you're done, go to sleep (top bar or register) to let the night pass.", "fb-small");
        }

        void DayReportButton()
        {
            if (w.Button("Back to work", "fb-button fb-button--primary fb-grow"))
            {
                shopDay.LastReport = null;
                panel = Panel.None;
                StartToDo();
            }
        }

        /// <summary>The public aquarium's part of the day report: tickets, people turned away, the rating and its reviews.</summary>
        void DrawAquariumReport(AquariumReport aquarium)
        {
            if (aquarium == null) return;
            w.Label(PublicAquarium.RoomName.ToUpper(), "fb-heading");
            w.Label($"Visitors: {aquarium.visitors}     Tickets: {aquarium.tickets}     Earned: <b><color={UiTheme.LeafHex}>{Economy.Format(aquarium.earnings)}</color></b>     Turned away at the price: {aquarium.turnedAway}");
            float stars = Mathf.Round(Mathf.Lerp(1f, 5f, aquarium.AverageScore) * 2f) / 2f;
            w.Label($"Visits: <size=18><color={UiTheme.SunHex}>{Stars(stars)}</color></size> ({aquarium.happy} happy, {aquarium.unhappy} disappointed)   Rating {(aquarium.ratingChange >= 0f ? "+" : "")}{aquarium.ratingChange * 5f:0.00} stars (now {PublicAquarium.Stars:0.0})");
            foreach (var review in aquarium.reviews) w.Label($"<i>\"{review}\"</i>");
            w.Label("Ticket price, estimate and booth upgrades: click the ticket booth or press U.", "fb-small");
        }

        /// <summary>The supplier menu (food, decorations, backgrounds): the Supplier button or B, shop closed only.</summary>
        void ToggleSupplier()
        {
            if (panel == Panel.Supplier && !atFishSeller) panel = Panel.None;
            else if (panel == Panel.None || panel == Panel.Supplier) OpenSupplier(false);
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
            w.Label("Pick your shopkeeper. You can change this any time at the register.");
            w.BeginRow("fb-row fb-center-row fb-gap-above");
            OwnerCard(OwnerLook.Woman, CrewModels.OwnerWoman, "Her");
            OwnerCard(OwnerLook.Man, CrewModels.OwnerMan, "Him");
            w.Pop();
        }

        void OwnerCard(OwnerLook look, string modelId, string label)
        {
            w.BeginCard(false, "fb-card fb-card--column fb-owner-card");
            w.Begin("picture", "fb-owner-card__picture");
            var photo = ItemPreviews.Person(modelId);
            UiPanels.Show(w.Image(photo, 150f, 220f), photo != null);
            w.Pop();
            if (w.Button(label, "fb-button fb-button--primary"))
            {
                ShopOwner.Look = look;
                panel = Panel.None;
            }
            w.Pop();
        }

        void SupplierHeader()
        {
            w.Flex();
            w.Label($"Fishkeeping {Progression.Fishkeeping}   Shopkeeping {Progression.Shopkeeping}   Balance: <b>{Economy.Format(Economy.Money)}</b>", "fb-text fb-header-text");
        }

        void DrawSupplier()
        {
            if (shopDay.IsOpen)
            {
                w.Label(atFishSeller ? "The fish seller only sells while the shop is closed." : "The supplier only delivers while the shop is closed.");
                return;
            }
            var tabs = atFishSeller ? FishSellerTabs : SupplierMenuTabs;
            var tabNames = new string[tabs.Length];
            for (int i = 0; i < tabs.Length; i++) tabNames[i] = SupplierTabs[tabs[i]];
            int shown = Mathf.Max(0, System.Array.IndexOf(tabs, supplierTab));
            int picked = Mathf.Clamp(w.Tabs(shown, tabNames), 0, tabs.Length - 1);
            supplierTab = tabs[picked];
            w.Label(atFishSeller ? "<i>Food and decorations are on the supplier menu: the Supplier (B) button up top.</i>" : "<i>Egg and fry boxes are at the fish seller's stall. Equipment is bought from each aquarium's Upgrades tab.</i>", "fb-small");
            if (atFishSeller && ScubaDives.Out)
                w.Label($"<color={UiTheme.SunHex}>The fish seller is out diving. He's back at the next time step.</color>");
            else if (atFishSeller && FishSeller.Away)
                w.Label($"<color={UiTheme.SunHex}>The fish seller has gone home for the night. He's back in the morning.</color>");

            if (supplierTab == EggsTab || supplierTab == FryTab || supplierTab == DevFryTab) DrawDeliverTo();

            var scroll = w.BeginScroll("fb-scroll fb-window__body");
            // A different tab starts at the top.
            if (picked != shown) scroll.scrollOffset = Vector2.zero;
            switch (supplierTab)
            {
                case EggsTab: DrawEggOffers(false); break;
                case FryTab: DrawEggOffers(true); break;
                case DecorTab: DrawDecorationOffers(); break;
                case FoodTab: DrawFoodOffers(); break;
                case DivesTab: DrawDives(); break;
                default: DrawDevFryOffers(); break;
            }
            w.Pop();
        }

        void DrawDeliverTo()
        {
            // The public aquarium's exhibits and the holding tanks never take boxes: little fish can't
            // live in the exhibits, and the holding tanks are for the seller's finds.
            var tanks = new List<StoreTank>();
            foreach (var tank in store.Tanks) if (tank.Role != TankRole.Exhibit && tank.Role != TankRole.Holding) tanks.Add(tank);
            if ((orderTarget == null || !tanks.Contains(orderTarget)) && tanks.Count > 0) orderTarget = tanks[0];
            w.Label("Deliver to:");
            w.BeginRow("fb-row fb-wrap fb-grid3");
            foreach (var tank in tanks)
                if (w.Button($"{tank.Title} ({FreeSpace(tank)} free)", tank == orderTarget ? "fb-button fb-tab fb-tab--on" : "fb-button fb-tab")) orderTarget = tank;
            w.Pop();
        }

        // Orders only fill a tank up to its limit; moving or breeding fish can push it over.
        static int FreeSpace(StoreTank tank) => Mathf.Max(0, tank.Tank.FreeSpaceBeforeLimit - EggClutch.EggsIn(tank.Tank, false));

        /// <summary>
        /// The seller's dives: book one (he's gone until the next time step and comes back with an
        /// egg or fry for a holding tank), keep him diving, see his past finds, and upgrade his gear.
        /// </summary>
        void DrawDives()
        {
            var d = ScubaDives.Data;
            w.Label($"<i>Some fish only live in the {PublicAquarium.RoomName.ToLower()}, and nobody sells them: the fish seller dives for them. Each dive he saves one egg or fry and brings it back at the next time step, for a holding tank in the big breeding room. He won't go while every holding tank is full. The rarer the fish is in the wild, the rarer the find; better gear finds rarer fish.</i>");
            if (!ScubaDives.Unlocked)
            {
                w.Label($"<color=#ffd27f>Dives open at {Progression.LockText(XpTrack.Fishkeeping, d.unlockLevel)}.</color>");
                return;
            }
            var holding = ScubaDives.HoldingTanks(store.Tanks);
            if (holding.Count == 0) w.Label($"<color=#ffd27f>No holding tanks yet: click a spot in the big breeding room to buy one ({Economy.Format(StoreLayout.TankPrice(null, TankRole.Holding))}).</color>");
            else
            {
                var lines = new List<string>();
                foreach (var tank in holding) lines.Add($"{tank.Title}: {tank.Tank.Capacity - ScubaDives.RoomIn(tank.Tank)} / {tank.Tank.Capacity}");
                w.Label("Holding tanks: " + string.Join("   ", lines));
            }
            w.BeginCard();
            if (ScubaDives.Out)
            {
                w.Label("<b>He's out diving.</b> He'll be back at the next time step (when the shop closes, or overnight).");
                if (w.Button($"Call it off (get the {Economy.Format(ScubaDives.Fee)} back)")) ScubaDives.CancelDive();
            }
            else
            {
                var why = ScubaDives.WhyCantDive(store.Tanks);
                if (w.Button($"Send him diving  {Economy.Format(ScubaDives.Fee)}", "fb-button fb-button--primary", why == null)) ScubaDives.TryDive(store.Tanks);
                if (why != null) w.Label($"<color=#ffd27f>{why}</color>", "fb-small");
            }
            ScubaDives.KeepDiving = w.Toggle(ScubaDives.KeepDiving, $"Keep diving: he goes again after each dive while a holding tank has room ({Economy.Format(ScubaDives.Fee)} each time)");
            if (!string.IsNullOrEmpty(ScubaDives.LastFind)) w.Label($"Last find: {ScubaDives.LastFind}", "fb-small");
            w.Pop();

            // One picture for each kind he has ever brought back (once it's grown enough to tell),
            // so the player can see what he's capable of. The odds stay hidden.
            w.Section("PAST FINDS");
            var past = ScubaDives.PastFinds();
            if (past.Count == 0) w.Label("<i>Nothing yet. What he brings back shows here once it has grown enough to tell what it is.</i>", "fb-small");
            else
            {
                w.BeginRow("fb-row fb-wrap");
                foreach (var species in past) Icon(ItemPreviews.Fish(species), 48f, species.displayName, false);
                w.Pop();
                w.Label(w.Tooltip, "fb-tooltip-line");
            }

            w.Section("HIS GEAR");
            var next = ScubaDives.NextGear;
            w.Label(ScubaDives.GearLevel == 0 ? "Rented gear for now." : $"He dives with his {d.gear[ScubaDives.GearLevel - 1].name.ToLower()}.");
            if (next != null)
            {
                int price = ScubaDives.GearPrice(next);
                var why = ScubaDives.WhyCantUpgradeGear();
                if (OfferRow(null, next.name, next.blurb, XpTrack.Fishkeeping, next.level, $"Buy {Economy.Format(price)}", why == null, 48f)) ScubaDives.TryUpgradeGear();
            }
            else w.Label($"<color={UiTheme.LeafHex}>He has the best gear there is.</color>");
        }

        /// <summary>A small picture on a dark square (a gold one for rare fish), named under the pointer.</summary>
        void Icon(Texture2D picture, float size, string tooltip, bool rare)
        {
            var box = w.Begin("icon", rare ? "fb-icon fb-icon--rare" : "fb-icon");
            box.tooltip = tooltip;
            box.style.width = size;
            box.style.height = size;
            UiPanels.Show(w.Image(picture, size, size), picture != null);
            w.Pop();
        }

        /// <summary>
        /// One thing for sale: its picture (a photo of the 3D design, or a drawn filler), name and
        /// details, and a buy button, or what level unlocks it.
        /// </summary>
        bool OfferRow(Texture2D picture, string title, string details, XpTrack track, int level, string buyLabel, bool canBuy, float pictureSize = 72f)
        {
            bool unlocked = Progression.IsUnlocked(track, level);
            bool bought = false;
            w.BeginCard(false, "fb-card fb-offer");
            var box = w.Begin("picture", unlocked ? "fb-offer__picture" : "fb-offer__picture fb-offer__picture--locked");
            box.style.width = pictureSize;
            box.style.height = pictureSize;
            UiPanels.Show(w.Image(picture, pictureSize, pictureSize), picture != null);
            if (picture == null) w.Label("<b>?</b>", "fb-offer__mystery");
            w.Pop();
            w.BeginColumn("fb-column fb-grow fb-offer__text");
            w.Label($"<b>{title}</b>");
            if (!string.IsNullOrEmpty(details)) w.Label(details, "fb-small fb-small--bright");
            w.Pop();
            w.BeginColumn("fb-column fb-offer__buy");
            if (!unlocked) w.Label($"<color=#ffd27f>Needs {Progression.LockText(track, level)}</color>");
            else bought = w.Button(buyLabel, "fb-button fb-button--wrap", canBuy);
            w.Pop();
            w.Pop();
            return bought && unlocked;
        }

        /// <summary>
        /// Egg boxes or fry boxes. Both roll their fish the same way; eggs hatch at the next time
        /// step and only egg-laying fish come as eggs, while fry arrive straight away and can be any
        /// fish. Either way, the fry are tiny grey fish until they grow enough to tell what they are.
        /// </summary>
        void DrawEggOffers(bool fry)
        {
            w.Label(fry
                ? "<i>Boxes of mixed fry, delivered right away. Any kind of fish can come as fry, but they're all tiny grey fish: you find out what each one is when it grows into a juvenile. Each extra rare fish in one box is harder to get than the last.</i>"
                : "<i>Boxes of mixed eggs. They hatch at the next time step (when the shop closes, or overnight). Only egg-laying fish come as eggs: livebearers like guppies and mollies come as fry. Each extra rare fish in one box is harder to get than the last.</i>");
            int free = orderTarget != null ? FreeSpace(orderTarget) : 0;
            int level = Progression.UnlockLevel(XpTrack.Fishkeeping);
            bool eggLayersOnly = !fry;
            int tankSize = orderTarget != null ? orderTarget.Tank.SizeIndex : 0;
            DrawBiggerTankFish(tankSize);
            foreach (var tier in EggBoxes.Tiers)
            {
                var normal = EggBoxes.Pool(tier, false, level, eggLayersOnly, tankSize);
                var rare = EggBoxes.Pool(tier, true, level, eggLayersOnly, tankSize);
                bool unlocked = Progression.IsUnlocked(XpTrack.Fishkeeping, tier.unlockLevel);
                bool anyFish = normal.Count + rare.Count > 0 || !unlocked;
                string details = tier.blurb;
                if (unlocked && normal.Count + rare.Count == 0)
                    details += EggBoxes.Available(tier, level, eggLayersOnly).Count > 0
                        ? "\n<color=#ffd27f>Every fish in this box needs a bigger tank than this one.</color>"
                        : "\n<color=#ffd27f>No fish of this kind in the game yet.</color>";
                else if (unlocked)
                {
                    var odds = EggBoxes.AtLeastRareOdds(tier, level, eggLayersOnly, tankSize);
                    var oddsText = new List<string>();
                    for (int k = 0; k < odds.Length && k < 3; k++) oddsText.Add($"{k + 1}+ {Percent(odds[k])}");
                    details += $"\n{tier.eggs} {(fry ? "fry" : "eggs")}. Rare fish ({tier.rarePool}): " + (rare.Count > 0 ? string.Join(", ", oddsText)
                        : EggBoxes.Pool(tier, true, level, eggLayersOnly).Count > 0 ? "only for a bigger tank" : EggBoxes.RareUnlockText(tier, eggLayersOnly));
                }
                int price = Perks.Supplier(fry ? tier.fryPrice : tier.price);
                string what = fry ? "fry" : "eggs";
                // Some boxes open with a goal instead of a level (wild tetras: raise neons or tiger barbs).
                bool goalMet = EggBoxes.GoalMet(tier);
                if (EggBoxes.HasGoal(tier))
                    details += goalMet ? $"\n<color={UiTheme.AquaHex}>Unlocked: you raised {tier.raiseCount} fish for the fish seller.</color>"
                        : $"\n<color=#ffd27f>To unlock: {EggBoxes.GoalText(tier)}.</color>";
                // A box that opens with a goal stays a mystery until then: no fish, only how to open it.
                bool mystery = EggBoxes.HasGoal(tier) && !goalMet;
                if (mystery) details = $"<color=#ffd27f>To unlock: {EggBoxes.GoalText(tier)}.</color>";
                bool away = FishSeller.Away;
                string label = away ? (ScubaDives.Out ? "Out diving" : "Back in the morning") : !goalMet ? $"Raise {tier.raiseCount} fish first" : free >= tier.eggs ? $"Buy {tier.eggs} {what}  {Economy.Format(price)}" : $"Needs {tier.eggs} free spaces";
                bool canBuy = !away && goalMet && anyFish && free >= tier.eggs && Economy.CanAfford(price) && orderTarget != null;
                var picture = mystery ? null : fry ? ItemPreviews.FryBox(tier.color) : ItemPreviews.EggBox(tier.color, tier.speckled);
                if (OfferRow(picture, mystery ? "?" : fry ? EggBoxes.FryName(tier) : tier.name, details, XpTrack.Fishkeeping, tier.unlockLevel, label, canBuy))
                {
                    if (fry) OrderFryBox(tier);
                    else OrderEggs(tier);
                }
                if (!mystery && unlocked && normal.Count + rare.Count > 0) DrawPoolPictures(normal, rare, EggBoxes.FirstEggOdds(tier, level, eggLayersOnly, tankSize));
            }
        }

        static string Percent(float chance) => chance < 0.01f ? "<1%" : $"{chance * 100f:0}%";

        /// <summary>Which fish the seller keeps back for bigger tanks than the one being ordered for.</summary>
        void DrawBiggerTankFish(int tankSize)
        {
            var sizes = Balance.Data.store.tankSizes;
            var lines = new List<string>();
            for (int size = tankSize + 1; size < sizes.Count; size++)
            {
                var kinds = TankQuality.KindsNeeding(size);
                if (kinds != null) lines.Add($"{sizes[size].name} tank: {kinds}");
            }
            if (lines.Count == 0) return;
            w.Label($"<color={UiTheme.SunHex}>Some fish need more room than a {TankQuality.SizeName(tankSize).ToLower()} tank, so they only come for bigger ones. Raise a tank's quality to upgrade it.</color>\n" + string.Join("\n", lines), "fb-small fb-small--bright");
        }

        /// <summary>Little pictures of the fish a box can hatch at the player's level (rare ones marked).</summary>
        void DrawPoolPictures(List<Species> normal, List<Species> rare, Dictionary<Species, float> odds)
        {
            w.BeginRow("fb-row fb-wrap fb-pool");
            w.Label("Can hatch:", "fb-small fb-pool__label");
            int shown = 0;
            foreach (var list in new[] { normal, rare })
                foreach (var species in list)
                {
                    if (shown++ >= 12) break;
                    string chance = odds.TryGetValue(species, out var p) ? $", about {Percent(p)} of eggs" : "";
                    Icon(ItemPreviews.Fish(species), 40f, $"{species.displayName} ({species.rarity}{chance})", list == rare);
                }
            w.Pop();
            // Always there, so the list below doesn't jump when the pointer moves on and off a picture.
            w.Label(w.Tooltip, "fb-tooltip-line fb-pool__tip");
        }

        void OrderEggs(EggTierBalance tier)
        {
            if (!Economy.TrySpend(Perks.Supplier(tier.price), tier.name.ToLower(), MoneyKind.FishAndEggs)) return;
            var target = orderTarget.Tank;
            var eggs = EggBoxes.Roll(tier, Progression.UnlockLevel(XpTrack.Fishkeeping), true, target.SizeIndex, target.MediumFreeSpace - EggClutch.EggsIn(target, true));
            EggClutch.Lay(orderTarget.Tank, tier, eggs);
            if (FishSeller.Instance != null) FishSeller.Instance.Sold();
            GameLog.Add($"{tier.eggs} {tier.name.ToLower()} delivered to {orderTarget.Title}. They hatch at the next time step.");
        }

        void OrderFryBox(EggTierBalance tier)
        {
            if (!Economy.TrySpend(Perks.Supplier(tier.fryPrice), EggBoxes.FryName(tier).ToLower(), MoneyKind.FishAndEggs)) return;
            var tank = orderTarget.Tank;
            foreach (var genome in EggBoxes.Roll(tier, Progression.UnlockLevel(XpTrack.Fishkeeping), false, tank.SizeIndex, tank.MediumFreeSpace - EggClutch.EggsIn(tank, true)))
                FishFactory.Spawn(genome, 1, 0.05f, tank, tank.RandomFreePoint(0.1f));
            if (FishSeller.Instance != null) FishSeller.Instance.Sold();
            GameLog.Add($"{tier.eggs} {EggBoxes.FryName(tier).ToLower()} delivered to {orderTarget.Title}. You'll see what they are when they grow.");
        }

        /// <summary>
        /// Decorations and background pictures go into your stock when bought, not straight into a
        /// tank: put them in (and take them out again) from each aquarium's Tank tab.
        /// </summary>
        void DrawDecorationOffers()
        {
            w.Label("<i>Bought decorations wait in your stock until you put them in an aquarium (its Tank tab), so you can buy a few and share them out. Decorations draw more customers and make fish comfortable (most like 3 to 10 per tank), but too many speed up algae. Some fish like certain decorations and stay close to them.</i>");
            var quality = Balance.Data.quality;
            foreach (var item in SupplierCatalog.Decorations)
            {
                int have = Inventory.Decorations(item.id), placed = PlacedCount(item.id);
                float points = quality.decorationPoints + (item.plant ? quality.plantPoints : 0f);
                string details = (item.plant ? "Plant. " : "") + (item.back ? "Goes along the back." : "Goes near the front.") + $"  Quality +{points:0} (+{quality.varietyPoints:0} if it's a new kind for the tank)."
                                 + $"\nYou have <b>{have}</b> in stock" + (placed > 0 ? $", {placed} in aquariums." : ".");
                int price = Perks.Decoration(item.price);
                if (OfferRow(ItemPreviews.Decoration(item.id), item.name, details, item.UnlockTrack, item.unlockLevel, $"Buy  {Economy.Format(price)}", Economy.CanAfford(price))
                    && Economy.TrySpend(price, $"a {item.name.ToLower()}", MoneyKind.Decorations))
                {
                    Inventory.AddDecoration(item.id);
                    GameLog.Add($"Bought a {item.name.ToLower()}. It's in your stock: put it in an aquarium from its Tank tab.");
                    Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpBuyDecoration);
                    Progression.Count(GoalKind.BuyDecorations);
                }
            }
            DrawBackgroundOffers();
        }

        int PlacedCount(string decorationId)
        {
            int count = 0;
            foreach (var tank in store.Tanks)
                foreach (var id in tank.Tank.Decor.Ids)
                    if (id == decorationId) count++;
            return count;
        }

        /// <summary>Pictures for the back glass, bought into your stock like decorations; each one goes up in one tank.</summary>
        void DrawBackgroundOffers()
        {
            w.Label("<b>Backgrounds</b>", "fb-text fb-gap-above");
            w.Label($"<i>A picture for the back glass. Put it up from an aquarium's Tank tab; a tank shows one at a time, and a wall aquarium can take a second one on its sides. Quality +{Balance.Data.quality.backgroundPoints:0}.</i>");
            foreach (var item in TankBackgrounds.All)
            {
                if (item == null) continue;
                int have = Inventory.Backgrounds(item.id), up = 0;
                foreach (var tank in store.Tanks)
                {
                    if (tank.Tank.BackgroundId == item.id) up++;
                    if (tank.Tank.SideBackgroundId == item.id) up++;
                }
                int price = Perks.Decoration(item.price);
                string details = item.blurb + (item.wallAquariumOnly ? " <b>Wall aquariums only.</b>" : "") + $"\nYou have <b>{have}</b> in stock" + (up > 0 ? $", {up} up in aquariums." : ".");
                if (OfferRow(TankBackgrounds.Texture(item.id), item.name, details, XpTrack.Shopkeeping, item.unlockLevel, $"Buy  {Economy.Format(price)}", Economy.CanAfford(price))
                    && Economy.TrySpend(price, $"the {item.name.ToLower()} background", MoneyKind.Decorations))
                {
                    Inventory.AddBackground(item.id);
                    GameLog.Add($"Bought the {item.name.ToLower()} background. Put it up from an aquarium's Tank tab.");
                    Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpBuyDecoration);
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
                    && Economy.TrySpend(price, offer.name.ToLower(), MoneyKind.Food))
                {
                    Economy.AddFood(offer.type, offer.feedings);
                    GameLog.Add($"Bought {offer.name.ToLower()}: {offer.feedings} more feedings.");
                }
            }
        }

        /// <summary>Developer list: any species as a single fry, ignoring levels, for testing.</summary>
        void DrawDevFryOffers()
        {
            w.Label("<i>Developer only: buy any species as a fry, ignoring levels.</i>");
            int free = orderTarget != null ? FreeSpace(orderTarget) : 0;
            w.BeginCard(false, "fb-card");
            w.Label("<b>Random mix</b>  <size=11>A free adult with the body, fins and tail of three random species, to see that any fins fit any body. Only compatible pairs can breed mixes in game.</size>", "fb-text fb-grow");
            if (w.Button("Random mix", "fb-button", free >= 1)) OrderRandomMix();
            w.Pop();
            foreach (var species in Species.All)
            {
                string livebearer = species.reproduction == Reproduction.Livebearer ? ", livebearer" : "";
                string details = $"{species.rarity}, {Category(species.category)}{livebearer}, {species.tempMin:0}-{species.tempMax:0} C, Fishkeeping {species.unlockLevel}";
                if (OfferRow(ItemPreviews.Fish(species), species.displayName, details, XpTrack.Fishkeeping, 0, $"1 fry  {Economy.Format(species.fryPrice)}",
                        (species.IsMedium ? (orderTarget != null ? orderTarget.Tank.MediumFreeSpace : 0) : free) >= 1 && Economy.CanAfford(species.fryPrice), 48f))
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
            if (!Economy.TrySpend(species.fryPrice, $"a {species} fry", MoneyKind.FishAndEggs)) return;
            var tank = orderTarget.Tank;
            var fish = FishFactory.Spawn(Genome.CreateRandom(species), 1, 0.05f, tank, tank.RandomFreePoint(0.1f));
            GameLog.Add($"{fish.FishName} the {species} fry arrived in {orderTarget.Title}.");
        }

        void DrawUnlock()
        {
            var section = pendingSection;
            w.Label(section.IsAquarium
                ? "A public aquarium next door, with its own entrance and ticket booth: people pay to see its two big exhibits (temperate and tropical). Good exhibits bring good ratings and more visitors. It opens once your shop's reputation is high enough (see Reputation, Y)."
                : "Opens up room for more display or for sale tanks. Bigger rooms open as your reputation grows (see Reputation, Y).");
            var why = store.WhyLocked(section);
            if (shopDay.IsOpen) w.Label("<i>Building work has to wait until the shop is closed.</i>");
            else if (why != null) w.Label($"<i>{why}</i>");
            else if (!Economy.CanAfford(section.Price)) w.Label($"<i>You need {Economy.Format(section.Price - Economy.Money)} more.</i>");
        }

        void UnlockButtons()
        {
            var section = pendingSection;
            bool can = !shopDay.IsOpen && store.WhyLocked(section) == null && Economy.CanAfford(section.Price);
            if (w.Button($"Unlock for {Economy.Format(section.Price)}", "fb-button fb-button--primary", can) && store.TryUnlock(section)) panel = Panel.None;
            if (w.Button("Cancel")) panel = Panel.None;
        }

        static readonly TankRole[] BuyableRoles = { TankRole.ForSale, TankRole.Display, TankRole.Breeder, TankRole.Quarantine, TankRole.Exhibit, TankRole.Holding };

        string BuyTankTitle() => pendingSlot.Exhibit ? $"The {PublicAquarium.ClimateName(pendingSlot.exhibit).ToLower()} aquarium" : pendingSlot.wall ? "A wall aquarium" : pendingSlot.holding ? "A holding tank" : pendingSlot.backRoom ? "Buy a tank for the breeding room" : "Buy a tank for this spot";

        void DrawBuyTank()
        {
            var wall = Balance.Data.store.wallAquarium;
            w.Label(pendingSlot.Exhibit
                ? $"<i>One of the {PublicAquarium.RoomName.ToLower()}'s two exhibits: a tank along this whole wall ({wall.width:0.#} m long, {wall.depth:0.#} m deep), set into it and reaching out behind it, for {(pendingSlot.exhibit == ExhibitClimate.Temperate ? "cold-water fish only (goldfish and their kin)" : "warm-water fish only")}. Only fish that grow big live here: it holds {PublicAquarium.ExhibitLimitAt(0, AquariumSize.Small)} small, {PublicAquarium.ExhibitLimitAt(0, AquariumSize.Medium)} medium and {PublicAquarium.ExhibitLimitAt(0, AquariumSize.Large)} large fish and {wall.decorationSlots} decorations, more once it's raised taller. Visitors pay to see it, and love variety; nothing in it is sold.</i>"
                : pendingSlot.wall
                ? $"<i>One tank along this whole wall ({wall.width:0.#} m long, {wall.depth:0.#} m deep), set into it and reaching out behind it. It holds {Tank.CapacityOf(wall)} fish (or {Tank.MediumCapacityOf(wall)} of the big medium kinds) and {wall.decorationSlots} decorations: four grand tanks' worth, and it can be raised taller later.</i>"
                : pendingSlot.holding
                ? $"<i>A small tank for the fish the seller finds diving, which only live in the {PublicAquarium.RoomName.ToLower()}. It keeps {ScubaDives.Data.holdingFishPerSize[0]} of them (more when it's upgraded) until they go to the aquarium, the fish market or back to the wild. He only dives while a holding tank has room.</i>"
                : pendingSlot.backRoom
                ? "<i>Customers never come back here: it's for breeding and for keeping sick fish apart.</i>"
                : "<i>Customers browse these. Breeder and quarantine tanks go in the breeding room.</i>");
            foreach (var role in BuyableRoles)
            {
                if (!pendingSlot.Allows(role)) continue;
                StoreLayout.TankLevel(pendingSlot, role, out var track, out var level);
                bool can = !shopDay.IsOpen && store.CanBuyTank(pendingSlot, role, out _);
                string kind = pendingSlot.Exhibit ? $"{PublicAquarium.ClimateName(pendingSlot.exhibit)} aquarium" : pendingSlot.wall ? $"Wall aquarium ({StoreTank.RoleName(role).ToLower()})" : $"{StoreTank.RoleName(role)} tank";
                string text = Progression.IsUnlocked(track, level)
                    ? pendingSlot.Exhibit ? $"{kind}   {Economy.Format(StoreLayout.TankPrice(pendingSlot, role))}" : $"{kind}   {Economy.Format(StoreLayout.TankPrice(pendingSlot, role))}   ({store.CountTanks(role)}/{SupplierCatalog.TankLimit(role)} {StoreTank.RoleName(role).ToLower()} owned)"
                    : $"{kind}: needs {Progression.LockText(track, level)}";
                if (w.Button(text, "fb-button fb-button--wrap", can) && store.TryBuyTank(pendingSlot, role) != null) panel = Panel.None;
            }
        }
    }
}
