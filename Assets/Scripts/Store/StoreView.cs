using FishBreeder.Core;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.Shop;
using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>
    /// The store screen: an overhead view of the shop with labels over everything clickable, the
    /// Open Shop controls, customers' thought bubbles and sale popups, and the register, supplier,
    /// unlock, buy-a-tank and end-of-day windows. Clicking a tank flies the camera in and hands over
    /// to <see cref="FishTankHUD"/>; Esc or "Back to store" flies back out.
    /// Placeholder IMGUI like the tank HUD, to be replaced with a real UI later.
    /// </summary>
    public class StoreView : MonoBehaviour
    {
        enum Panel { None, Register, Supplier, Unlock, BuyTank, DayReport }

        static readonly Vector3 StoreViewDirection = new Vector3(0f, 0.62f, -0.78f).normalized;
        static readonly Color StoreBackground = new Color(0.17f, 0.19f, 0.23f);
        static readonly string[] SupplierTabs = { "Fish", "Food", "Equipment and care" };
        static readonly float[] Speeds = { 1f, 5f, 20f };

        StoreLayout store;
        ShopDay shopDay;
        FishTankHUD hud;
        UnderwaterFilter underwater;
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

        public void Initialize(StoreLayout layout, ShopDay day, FishTankHUD tankHud, Camera camera)
        {
            store = layout;
            shopDay = day;
            hud = tankHud;
            cam = camera;
            hud.enabled = false;
            underwater = cam.GetComponent<UnderwaterFilter>();
            if (underwater == null) underwater = cam.gameObject.AddComponent<UnderwaterFilter>();
            underwater.enabled = false;
            ApplyStoreLook();
            GetStorePose(out var position, out var rotation);
            cam.transform.SetPositionAndRotation(position, rotation);
        }

        void Update()
        {
            if (store == null || cam == null) return;
            float dt = Time.unscaledDeltaTime;

            HandleDevKeys();

            if (shopDay.LastReport != null && panel != Panel.DayReport)
            {
                if (focused != null) ExitTank();
                panel = Panel.DayReport;
            }

            Vector3 targetPosition;
            Quaternion targetRotation;
            if (focused != null && focused.Tank == null) ExitTank();
            if (focused != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) ExitTank();
                GetTankPose(focused, out targetPosition, out targetRotation);
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.Escape) && panel != Panel.DayReport) panel = Panel.None;
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
            hud.Initialize(tank, store.Tanks, cam, ExitTank, () => shopDay.IsOpen);
            hud.enabled = true;
            underwater.enabled = true;
        }

        void ExitTank()
        {
            if (focused != null) panX = Mathf.Round(focused.transform.position.x / StoreLayout.SectionWidth) * StoreLayout.SectionWidth;
            focused = null;
            Tank.Focused = null;
            hud.enabled = false;
            underwater.enabled = false;
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
                // In the tank view the tank HUD owns the screen; just show the shop clock if open.
                if (shopDay.IsOpen) DrawShopClock(new Rect(width - 330f, 10f, 320f, 34f));
                return;
            }

            topBar = new Rect(10f, 10f, width - 20f, 34f);
            DrawTopBar();
            if (panel == Panel.None)
            {
                DrawWorldLabels(scale, height);
                DrawCustomerBubbles(scale, height);
            }
            DrawMoneyPopups(scale, height);
            DrawLog(height);

            switch (panel)
            {
                case Panel.Register: panelRect = Centered(width, height, 460f, 500f); DrawRegister(); break;
                case Panel.Supplier: panelRect = Centered(width, height, 680f, 620f); DrawSupplier(); break;
                case Panel.Unlock: panelRect = Centered(width, height, 380f, 170f); DrawUnlock(); break;
                case Panel.BuyTank: panelRect = Centered(width, height, 420f, 230f); DrawBuyTank(); break;
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

            if (!shopDay.IsOpen)
                foreach (var slot in store.Slots)
                    if (slot.section.unlocked && slot.tank == null)
                        WorldLabel(new Vector3(slot.position.x, 0.3f, slot.position.z), "+ Tank spot", scale, height);

            foreach (var section in store.Sections)
                if (!section.unlocked)
                    WorldLabel(section.LabelPoint, $"<b>{section.name}</b> (locked)\nUnlock for {Economy.Format(section.price)}", scale, height);
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
            GUILayout.BeginArea(panelRect, GUI.skin.box);
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
            if (GUILayout.Button("Close")) panel = Panel.None;
            GUILayout.EndArea();
        }

        void DrawDayReport()
        {
            var report = shopDay.LastReport;
            GUILayout.BeginArea(panelRect, GUI.skin.box);
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
            }
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
            if (shopDay.IsOpen)
            {
                GUILayout.Label("The supplier only delivers while the shop is closed.");
                if (GUILayout.Button("Close")) panel = Panel.None;
                GUILayout.EndArea();
                return;
            }
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
            GUILayout.Label("<i>Eggs are cheaper but hatch at the next time step (when the shop opens or closes). Fry arrive right away.</i>", Rich());
            int free = orderTarget != null ? FreeSpace(orderTarget) : 0;
            foreach (var species in SupplierCatalog.Fish)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{species.displayName}</b>\n<size=10>{species.rarity}, {Category(species.category)}, {species.tempMin:0}-{species.tempMax:0} C{(species.IsSaltwater ? ", salt water" : "")}</size>", Rich(), GUILayout.Width(250f));
                GUI.enabled = free >= species.clutchSize && Economy.CanAfford(species.clutchPrice);
                if (GUILayout.Button($"{species.clutchSize} eggs  {Economy.Format(species.clutchPrice)}", GUILayout.Width(150f), GUILayout.Height(32f)))
                    OrderEggs(species);
                GUI.enabled = free >= 1 && Economy.CanAfford(species.fryPrice);
                if (GUILayout.Button($"1 fry  {Economy.Format(species.fryPrice)}", GUILayout.Width(120f), GUILayout.Height(32f)))
                    OrderFry(species);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
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

        void OrderEggs(Species species)
        {
            if (!Economy.TrySpend(species.clutchPrice, $"{species.clutchSize} {species} eggs")) return;
            EggClutch.Lay(orderTarget.Tank, species, species.clutchSize);
            GameLog.Add($"{species.clutchSize} {species} eggs delivered to {orderTarget.Title}. They hatch at the next time step.");
        }

        void OrderFry(Species species)
        {
            if (!Economy.TrySpend(species.fryPrice, $"a {species} fry")) return;
            var tank = orderTarget.Tank;
            var fish = FishFactory.Spawn(Genome.CreateRandom(species), 1, 0.05f, tank, tank.RandomFreePoint(0.1f));
            GameLog.Add($"{fish.FishName} the {species} fry arrived in {orderTarget.Title}.");
        }

        void DrawFoodOffers()
        {
            foreach (var offer in SupplierCatalog.Food)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"<b>{offer.name}</b>   (you have {Economy.FoodPortions(offer.type)} feedings)", Rich());
                GUILayout.Label(offer.blurb);
                GUI.enabled = Economy.CanAfford(offer.price);
                if (GUILayout.Button($"Buy {offer.feedings} feedings  {Economy.Format(offer.price)}") && Economy.TrySpend(offer.price, offer.name.ToLower()))
                {
                    Economy.AddFood(offer.type, offer.feedings);
                    GameLog.Add($"Bought {offer.name.ToLower()}: {offer.feedings} more feedings.");
                }
                GUI.enabled = true;
                GUILayout.EndVertical();
            }
        }

        void DrawEquipmentOffers()
        {
            var prices = SupplierCatalog.Equipment;
            var tank = orderTarget != null ? orderTarget.Tank : null;
            string where = orderTarget != null ? orderTarget.Title : "a tank";

            EquipmentRow("Filter", "Cuts how fast waste dirties the water, so the tank needs wiping less often.", prices.filterPrice, tank != null && tank.HasFilter, where, () => tank.InstallFilter());
            EquipmentRow("Heater", "Holds the water at the temperature you set (tropical fish need 24 C or more). Room temperature is " + $"{GameConfig.Active.roomTemperature:0} C.", prices.heaterPrice, tank != null && tank.HasHeater, where, () => tank.InstallHeater());
            EquipmentRow("Bubbler", $"Adds oxygen, so the tank can hold more than {GameConfig.Active.fishPerTankWithoutBubbler} fish comfortably.", prices.bubblerPrice, tank != null && tank.HasBubbler, where, () => tank.InstallBubbler());

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<b>Water conditioner</b>   (you have {Economy.WaterConditionerDoses} doses)", Rich());
            GUILayout.Label("Resets the pH and gets rid of snails and worms. Use it from the tank view.");
            GUI.enabled = Economy.CanAfford(prices.waterConditionerPrice);
            if (GUILayout.Button($"Buy {prices.waterConditionerDoses} doses  {Economy.Format(prices.waterConditionerPrice)}") && Economy.TrySpend(prices.waterConditionerPrice, "water conditioner"))
                Economy.WaterConditionerDoses += prices.waterConditionerDoses;
            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        void EquipmentRow(string name, string blurb, int price, bool installed, string where, System.Action install)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<b>{name}</b>", Rich());
            GUILayout.Label(blurb);
            GUI.enabled = orderTarget != null && !installed && Economy.CanAfford(price);
            string label = installed ? $"{where} already has one" : $"Install in {where}  {Economy.Format(price)}";
            if (GUILayout.Button(label) && Economy.TrySpend(price, $"a {name.ToLower()} for {where}"))
            {
                install();
                GameLog.Add($"{name} installed in {where}.");
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        void DrawUnlock()
        {
            var section = pendingSection;
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label($"<size=16><b>Unlock the {section.name}?</b></size>", Rich());
            GUILayout.Label("Opens up room for 6 more tanks.");
            var next = store.NextLockedSection;
            if (shopDay.IsOpen) GUILayout.Label("<i>Building work has to wait until the shop is closed.</i>", Rich());
            else if (next != section) GUILayout.Label($"<i>Unlock the {next.name} first.</i>", Rich());
            else if (!Economy.CanAfford(section.price)) GUILayout.Label($"<i>You need {Economy.Format(section.price - Economy.Money)} more.</i>", Rich());
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUI.enabled = !shopDay.IsOpen && next == section && Economy.CanAfford(section.price);
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
                GUI.enabled = !shopDay.IsOpen && store.CanBuyTank(pendingSlot, role, out _);
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

        static GUIStyle rich, label, button, centered;

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
