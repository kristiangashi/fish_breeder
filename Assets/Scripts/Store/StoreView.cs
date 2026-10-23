using FishBreeder.Core;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>
    /// The closed-shop screen: an overhead view of the store, labels over everything clickable,
    /// and the register, supplier, unlock and buy-a-tank windows. Clicking a tank flies the camera
    /// in and hands over to <see cref="FishTankHUD"/>; Esc or "Back to store" flies back out.
    /// Placeholder IMGUI like the tank HUD, to be replaced with a real UI later.
    /// </summary>
    public class StoreView : MonoBehaviour
    {
        enum Panel { None, Register, Supplier, Unlock, BuyTank }

        static readonly Vector3 StoreViewDirection = new Vector3(0f, 0.62f, -0.78f).normalized;
        static readonly Color StoreBackground = new Color(0.17f, 0.19f, 0.23f);
        static readonly string[] SupplierTabs = { "Fish", "Food", "Equipment" };

        StoreLayout store;
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
        float distance = 10f;
        Rect panelRect, topBar;

        public void Initialize(StoreLayout layout, FishTankHUD tankHud, Camera camera)
        {
            store = layout;
            hud = tankHud;
            cam = camera;
            hud.enabled = false;
            ApplyStoreLook();
            GetStorePose(out var position, out var rotation);
            cam.transform.SetPositionAndRotation(position, rotation);
        }

        void Update()
        {
            if (store == null || cam == null) return;
            float dt = Time.unscaledDeltaTime;

            Vector3 targetPosition;
            Quaternion targetRotation;
            if (focused != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) ExitTank();
                GetTankPose(focused, out targetPosition, out targetRotation);
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.Escape)) panel = Panel.None;
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
            distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 0.8f, 5f, 16f);

            // You can look one section past the open ones, to see what's for sale.
            float maxX = Mathf.Min(store.UnlockedCount, store.Sections.Count - 1) * StoreLayout.SectionWidth;
            panX = Mathf.Clamp(panX, -1f, maxX);
        }

        void GetStorePose(out Vector3 position, out Quaternion rotation)
        {
            var lookAt = new Vector3(panX, 0.8f, 0.3f);
            position = lookAt + StoreViewDirection * distance;
            rotation = Quaternion.LookRotation(lookAt - position);
        }

        /// <summary>Same framing the original single-tank view used.</summary>
        static void GetTankPose(StoreTank tank, out Vector3 position, out Quaternion rotation)
        {
            var center = tank.Tank.InnerBounds.center;
            position = center + new Vector3(0f, 0.1f, -1.9f);
            rotation = Quaternion.LookRotation(center - position);
        }

        void EnterTank(StoreTank tank)
        {
            panel = Panel.None;
            focused = tank;
            Tank.Focused = tank.Tank;
            hud.Initialize(tank.Tank, tank.Feeding, tank.Breeding, cam, tank.Title, ExitTank);
            hud.enabled = true;
        }

        void ExitTank()
        {
            if (focused != null) panX = Mathf.Round(focused.transform.position.x / StoreLayout.SectionWidth) * StoreLayout.SectionWidth;
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
            if (store == null || focused != null) return;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            topBar = new Rect(10f, 10f, width - 20f, 30f);
            DrawTopBar();
            if (panel == Panel.None) DrawWorldLabels(scale, height);
            DrawLog(height);

            switch (panel)
            {
                case Panel.Register: panelRect = Centered(width, height, 440f, 470f); DrawRegister(); break;
                case Panel.Supplier: panelRect = Centered(width, height, 600f, 600f); DrawSupplier(); break;
                case Panel.Unlock: panelRect = Centered(width, height, 380f, 170f); DrawUnlock(); break;
                case Panel.BuyTank: panelRect = Centered(width, height, 400f, 230f); DrawBuyTank(); break;
            }

            HandleClick();
        }

        static Rect Centered(float width, float height, float w, float h) => new Rect((width - w) * 0.5f, (height - h) * 0.5f, w, h);

        void DrawTopBar()
        {
            GUILayout.BeginArea(topBar, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{Economy.Format(Economy.Money)}</b>", Rich());
            GUILayout.Label($"Day {Economy.Day}  (shop closed)");
            GUILayout.Label($"Flakes: {Economy.FoodPortions(FoodType.Flakes)}   Premium: {Economy.FoodPortions(FoodType.Premium)}");
            GUILayout.FlexibleSpace();
            GUILayout.Label("<i>Click a tank to look inside. A/D or right-drag to pan, scroll to zoom.</i>", Rich());
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawLog(float height)
        {
            var entries = GameLog.Entries;
            if (entries.Count == 0) return;
            var rect = new Rect(10f, height - 110f, 520f, 100f);
            GUILayout.BeginArea(rect, GUI.skin.box);
            for (int i = Mathf.Max(0, entries.Count - 4); i < entries.Count; i++)
                GUILayout.Label(entries[i]);
            GUILayout.EndArea();
        }

        void DrawWorldLabels(float scale, float height)
        {
            foreach (var tank in store.Tanks)
            {
                int eggs = EggClutch.EggsIn(tank.Tank);
                string contents = $"{tank.Tank.Residents.Count} fish" + (eggs > 0 ? $", {eggs} eggs" : "");
                WorldLabel(tank.Tank.InnerBounds.center + Vector3.up * 0.65f, $"<b>{tank.Title}</b>\n{contents}", scale, height);
            }
            WorldLabel(store.RegisterPoint, "<b>Register</b>", scale, height);
            WorldLabel(store.ComputerPoint, "<b>Supplier computer</b>", scale, height);

            foreach (var slot in store.Slots)
                if (slot.section.unlocked && slot.tank == null)
                    WorldLabel(new Vector3(slot.position.x, 0.3f, slot.position.z), "+ Tank spot", scale, height);

            foreach (var section in store.Sections)
                if (!section.unlocked)
                    WorldLabel(section.LabelPoint, $"<b>{section.name}</b> (locked)\nUnlock for {Economy.Format(section.price)}", scale, height);
        }

        void WorldLabel(Vector3 world, string text, float scale, float height)
        {
            var screen = cam.WorldToScreenPoint(world);
            if (screen.z < 0f) return;
            var content = new GUIContent(text);
            var size = Label().CalcSize(content);
            var gui = new Vector2(screen.x / scale, height - screen.y / scale);
            GUI.Label(new Rect(gui.x - size.x * 0.5f, gui.y - size.y, size.x, size.y), content, Label());
        }

        void DrawRegister()
        {
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("<size=16><b>Register</b></size>", Rich());
            GUILayout.Label($"Balance: <b>{Economy.Format(Economy.Money)}</b>     Day {Economy.Day}", Rich());
            GUILayout.Label("The shop is closed. Opening the shop to customers comes in a later update.");
            GUI.enabled = false;
            GUILayout.Button("Open Shop (coming soon)");
            GUI.enabled = true;

            GUILayout.Space(6f);
            GUILayout.Label("<b>Recent transactions</b>", Rich());
            var ledger = Economy.Ledger;
            if (ledger.Count == 0) GUILayout.Label("<i>Nothing yet.</i>", Rich());
            for (int i = ledger.Count - 1; i >= Mathf.Max(0, ledger.Count - 8); i--)
                GUILayout.Label(ledger[i]);

            GUILayout.FlexibleSpace();
            GUILayout.Label("<b>Developer</b>", Rich());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+$1,000 (dev)")) Economy.Earn(1000, "developer cash");
            if (GUILayout.Button("Skip to next day (dev)")) Economy.AdvanceDay();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Close")) panel = Panel.None;
            GUILayout.EndArea();
        }

        void DrawSupplier()
        {
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=16><b>Supplier</b></size>", Rich());
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Balance: <b>{Economy.Format(Economy.Money)}</b>", Rich());
            GUILayout.EndHorizontal();
            supplierTab = GUILayout.Toolbar(supplierTab, SupplierTabs);
            GUILayout.Space(4f);

            if (supplierTab != 1) DrawDeliverTo();

            supplierScroll = GUILayout.BeginScrollView(supplierScroll);
            if (supplierTab == 0) DrawFishOffers();
            else if (supplierTab == 1) DrawFoodOffers();
            else DrawEquipmentOffers();
            GUILayout.EndScrollView();

            if (GUILayout.Button("Close")) panel = Panel.None;
            GUILayout.EndArea();
        }

        void DrawDeliverTo()
        {
            if (orderTarget == null && store.Tanks.Count > 0) orderTarget = store.Tanks[0];
            GUILayout.Label(supplierTab == 0 ? "Deliver to:" : "Install in:");
            var tanks = store.Tanks;
            var names = new string[tanks.Count];
            int current = 0;
            for (int i = 0; i < tanks.Count; i++)
            {
                names[i] = supplierTab == 0 ? $"{tanks[i].Title} ({FreeSpace(tanks[i])} free)" : tanks[i].Title;
                if (tanks[i] == orderTarget) current = i;
            }
            current = GUILayout.SelectionGrid(current, names, 3);
            if (tanks.Count > 0) orderTarget = tanks[current];
            GUILayout.Space(4f);
        }

        static int FreeSpace(StoreTank tank) => Mathf.Max(0, tank.Tank.FreeSpace - EggClutch.EggsIn(tank.Tank));

        void DrawFishOffers()
        {
            GUILayout.Label("<i>Eggs are cheaper but only hatch when the next day starts. Fry arrive right away.</i>", Rich());
            int free = orderTarget != null ? FreeSpace(orderTarget) : 0;
            foreach (var offer in SupplierCatalog.Fish)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(offer.species.displayName, GUILayout.Width(170f));
                GUI.enabled = free >= offer.clutchSize && Economy.CanAfford(offer.clutchPrice);
                if (GUILayout.Button($"{offer.clutchSize} eggs  {Economy.Format(offer.clutchPrice)}", GUILayout.Width(170f)))
                    OrderEggs(offer);
                GUI.enabled = free >= 1 && Economy.CanAfford(offer.fryPrice);
                if (GUILayout.Button($"1 fry  {Economy.Format(offer.fryPrice)}", GUILayout.Width(140f)))
                    OrderFry(offer);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        void OrderEggs(SupplierCatalog.FishOffer offer)
        {
            if (!Economy.TrySpend(offer.clutchPrice, $"{offer.clutchSize} {offer.species} eggs")) return;
            EggClutch.Lay(orderTarget.Tank, offer.species, offer.clutchSize);
            GameLog.Add($"{offer.clutchSize} {offer.species} eggs delivered to {orderTarget.Title}. They hatch when the next day starts.");
        }

        void OrderFry(SupplierCatalog.FishOffer offer)
        {
            if (!Economy.TrySpend(offer.fryPrice, $"a {offer.species} fry")) return;
            var tank = orderTarget.Tank;
            var fish = FishFactory.Spawn(Genome.CreateRandom(offer.species), 1, 0.05f, tank, tank.RandomFreePoint(0.1f));
            GameLog.Add($"{fish.FishName} the {offer.species} fry arrived in {orderTarget.Title}.");
        }

        void DrawFoodOffers()
        {
            foreach (var offer in SupplierCatalog.Food)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"<b>{offer.name}</b>   (you have {Economy.FoodPortions(offer.type)} feedings)", Rich());
                GUILayout.Label(offer.blurb);
                GUI.enabled = Economy.CanAfford(offer.price);
                if (GUILayout.Button($"Buy {offer.portions} feedings  {Economy.Format(offer.price)}"))
                {
                    if (Economy.TrySpend(offer.price, offer.name.ToLower()))
                    {
                        Economy.AddFood(offer.type, offer.portions);
                        GameLog.Add($"Bought a {offer.name.ToLower()}: {offer.portions} more feedings.");
                    }
                }
                GUI.enabled = true;
                GUILayout.EndVertical();
            }
        }

        void DrawEquipmentOffers()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Filter</b>", Rich());
            GUILayout.Label("Cuts how fast fish waste dirties the water, so the tank needs cleaning less often.");
            bool installed = orderTarget != null && orderTarget.Tank.HasFilter;
            GUI.enabled = orderTarget != null && !installed && Economy.CanAfford(SupplierCatalog.FilterPrice);
            string label = installed ? $"{orderTarget.Title} already has a filter" : $"Install in {(orderTarget != null ? orderTarget.Title : "a tank")}  {Economy.Format(SupplierCatalog.FilterPrice)}";
            if (GUILayout.Button(label) && Economy.TrySpend(SupplierCatalog.FilterPrice, $"a filter for {orderTarget.Title}"))
            {
                orderTarget.Tank.InstallFilter();
                GameLog.Add($"Filter installed in {orderTarget.Title}.");
            }
            GUI.enabled = true;
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Heater</b> and <b>Bubbler</b>  <i>(coming soon: they need water temperature and oxygen first)</i>", Rich());
            GUILayout.EndVertical();
        }

        void DrawUnlock()
        {
            var section = pendingSection;
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label($"<size=16><b>Unlock the {section.name}?</b></size>", Rich());
            GUILayout.Label("Opens up room for 6 more tanks.");
            var next = store.NextLockedSection;
            if (next != section) GUILayout.Label($"<i>Unlock the {next.name} first.</i>", Rich());
            else if (!Economy.CanAfford(section.price)) GUILayout.Label($"<i>You need {Economy.Format(section.price - Economy.Money)} more.</i>", Rich());
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUI.enabled = next == section && Economy.CanAfford(section.price);
            if (GUILayout.Button($"Unlock for {Economy.Format(section.price)}") && store.TryUnlock(section)) panel = Panel.None;
            GUI.enabled = true;
            if (GUILayout.Button("Cancel")) panel = Panel.None;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static readonly TankRole[] BuyableRoles = { TankRole.Breeder, TankRole.ForSale, TankRole.Display, TankRole.Quarantine };

        void DrawBuyTank()
        {
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("<size=16><b>Buy a tank for this spot</b></size>", Rich());
            foreach (var role in BuyableRoles)
            {
                bool ok = store.CanBuyTank(pendingSlot, role, out _);
                GUI.enabled = ok;
                string text = $"{StoreTank.RoleName(role)} tank   {Economy.Format(SupplierCatalog.TankPrice(role))}   ({store.CountTanks(role)}/{SupplierCatalog.TankLimit(role)} owned)";
                if (GUILayout.Button(text) && store.TryBuyTank(pendingSlot, role) != null) panel = Panel.None;
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
            if (topBar.Contains(e.mousePosition)) return;
            if (panel != Panel.None)
            {
                // Clicking outside a window closes it.
                if (!panelRect.Contains(e.mousePosition)) panel = Panel.None;
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

        static GUIStyle rich, label;

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
    }
}
