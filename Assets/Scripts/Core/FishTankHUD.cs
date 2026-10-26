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
        bool showDecorations;
        Func<StoreTank, int, bool> upgradeTank;
        Action<int> switchTank;
        Func<int, StoreTank> neighbourTank;

        /// <summary>The fish the camera follows: the last one selected.</summary>
        public Fish Focus => selected.Count > 0 ? selected[selected.Count - 1] : null;
        public bool HasSelection => selected.Count > 0;
        public StoreTank Current => storeTank;

        public void ClearSelection() => selected.Clear();

        public void Select(Fish fish)
        {
            selected.Clear();
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
        }

        void Update()
        {
            selected.RemoveAll(f => f == null || f.Tank != tank);
            if (tank == null) return;
            int step = Input.GetKeyDown(KeyCode.RightArrow) ? 1 : Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 0;
            if (step == 0) return;
            if (HasSelection) Cycle(step);
            else switchTank?.Invoke(step);
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
                HandleClick();
                return;
            }

            // Runs down to the log, so eggs, pregnancies and breeding lines never push the buttons off the bottom.
            leftPanel = new Rect(10f, 10f, 300f, Mathf.Max(520f, height - 170f));
            logPanel = new Rect(10f, height - 150f, 460f, 140f);
            breedBar = selected.Count == 2 ? new Rect(width * 0.5f - 210f, height - 120f, 420f, 60f) : Rect.zero;
            tankBar = new Rect(Mathf.Max(logPanel.xMax + 10f, width * 0.5f - 230f), height - 50f, 460f, 40f);
            fishWindow = Rect.zero;
            stepLeft = stepRight = Rect.zero;

            DrawTankPanel();
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
            HandleClick();
        }

        // ------------------------------------------------------------ tank panel

        enum TankTab { Tank, Care, Breeding, Setup }

        static readonly string[] TabNames = { "Tank", "Care", "Breeding", "Setup" };
        TankTab tab;

        /// <summary>Opens the tank on a tab (the to-do list opens on Care).</summary>
        public void ShowCare() => tab = TankTab.Care;

        public int TabCount => TabNames.Length;

        public void ShowTab(int index) => tab = (TankTab)Mathf.Clamp(index, 0, TabNames.Length - 1);

        /// <summary>
        /// The tank panel, in four tabs: Tank (fish count, water, temperature), Care (feeding,
        /// wiping, water treatment, medicine, repairs, the heater), Breeding (eggs, pregnancies and
        /// who's ready) and Setup (equipment, decorations, size and water type). A dot on a tab means
        /// something there needs doing.
        /// </summary>
        void DrawTankPanel()
        {
            UiCheck.Begin("Tank panel", leftPanel, UiStyles.Window);
            GUILayout.Label($"<b>{storeTank.Title}</b>   {Economy.Format(Economy.Money)}", Rich());
            if (ReadOnly) GUILayout.Label("<color=#ffd27f>The shop is open: you can look, but tank work waits until closing.</color>", Rich());
            var labels = new string[TabNames.Length];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = TabNames[i] + (TabNeedsYou((TankTab)i) ? $" <color={UiTheme.CoralHex}>●</color>" : "");
            tab = (TankTab)GUILayout.SelectionGrid((int)tab, labels, labels.Length, TabStyle());
            GUILayout.Space(4f);
            panelScroll = UiCheck.BeginScroll("Tank panel: " + TabNames[(int)tab], panelScroll);
            switch (tab)
            {
                case TankTab.Tank: DrawTankTab(); break;
                case TankTab.Care: DrawCareTab(); break;
                case TankTab.Breeding: DrawBreedingTab(); break;
                default: DrawSetupTab(); break;
            }
            UiCheck.EndScroll();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Journal (J)")) JournalView.Toggle();
            if (GUILayout.Button(ResearchView.ButtonLabel(), RichButton())) ResearchView.Toggle();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Perks.TotalAvailable > 0 ? $"Perks (P) ●{Perks.TotalAvailable}" : "Perks (P)")) PerksView.Toggle();
            if (GUILayout.Button("Watch (V)")) ViewMode.Set(true);
            GUILayout.EndHorizontal();
            if (onBack != null && GUILayout.Button("Back to store (Esc)")) onBack();
            UiCheck.End();
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

        void DrawTankTab()
        {
            DrawFishCount(tank.SizeInfo.name);
            GUILayout.Label($"<size=11>Aquarium quality {tank.Quality} (see Setup)</size>", Rich());
            DrawSickList();
            int eggs = EggClutch.EggsIn(tank);
            int pregnant = 0;
            foreach (var fish in tank.Residents) if (fish.IsPregnant) pregnant++;
            if (eggs > 0 || pregnant > 0)
                GUILayout.Label($"<color=#ffe9a8>{(eggs > 0 ? $"{eggs} eggs" : "")}{(eggs > 0 && pregnant > 0 ? ", " : "")}{(pregnant > 0 ? $"{pregnant} pregnant" : "")} (see Breeding)</color>", Rich());
            Bar("Water quality", tank.WaterQuality, true);
            Bar("Algae", tank.Algae, false);
            Bar("Oxygen", tank.Oxygen, true);
            var liked = KnownTemperatureRange(out bool allKnown);
            GUILayout.Label($"Temperature: {tank.Temperature:0.#} C" + (tank.HeaterWorking ? "  (heater)" : tank.HasHeater ? "  <color=#ff8080>(heater broken)</color>" : "  (room temperature)"), Rich());
            UiGauges.Temperature(tank.Temperature, liked);
            GUILayout.Label((Research.CanReadPh ? $"pH: {tank.Ph:0.0}    " : "") + $"Water: {(tank.IsSaltwater ? "salt" : "fresh")} ({tank.Salinity:0} ppt)", Rich());
            if (tank.HasPests) GUILayout.Label("<color=#ff8080>Pests! Treat the water (Care).</color>", Rich());
        }

        void DrawCareTab()
        {
            GUI.enabled = !ReadOnly;
            DrawFeedButtons();
            if (GUILayout.Button($"Wipe tank ({tank.WipesLeft} left)")) tank.Clean();
            if (GUILayout.Button($"Treat water ({Economy.WaterConditionerDoses} doses)"))
            {
                if (Economy.WaterConditionerDoses > 0)
                {
                    Economy.WaterConditionerDoses--;
                    tank.TreatWater();
                }
                else GameLog.Add("No water conditioner. Order some at the supplier computer.");
            }
            if (tank.Corpses.Count > 0 && GUILayout.Button($"Remove dead fish ({tank.Corpses.Count})"))
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
        }

        void DrawBreedingTab()
        {
            bool any = false;
            foreach (var clutch in EggClutch.All)
                if (clutch != null && clutch.Tank == tank)
                {
                    any = true;
                    GUILayout.Label($"<color=#ffe9a8>Eggs: {clutch.Count} {clutch.Label}, hatch in {BreedingSystem.Steps(clutch.StepsToHatch)}</color>", Rich());
                }
            foreach (var fish in tank.Residents)
                if (fish.IsPregnant)
                {
                    any = true;
                    GUILayout.Label($"<color=#ffb3d9>Pregnant: {fish.FishName} ({fish.SpeciesLabel}), due in {BreedingSystem.Steps(fish.StepsToBirth)}</color>", Rich());
                }
            if (any) GUILayout.Space(4f);
            var lines = BreedingSystem.Readiness(tank);
            foreach (var line in lines) GUILayout.Label($"<color=#c9d7e8>{line}</color>", Rich());
            if (lines.Count == 0 && !any) GUILayout.Label("<i>No fish here to breed.</i>", Rich());
            GUILayout.Space(4f);
            GUILayout.Label(storeTank.Role == TankRole.Breeder
                ? "<size=11>Pick a male and a female to pair them up.</size>"
                : "<size=11>Pairs in other tanks only breed now and then by chance; a breeder tank in the back room lets you pair them.</size>", Rich());
        }

        void DrawSetupTab()
        {
            GUI.enabled = !ReadOnly;
            DrawQuality();
            DrawUpgrade();
            DrawEquipment();
            if (tank.Residents.Count == 0 && tank.Corpses.Count == 0)
            {
                var supplier = Balance.Data.supplier;
                int salt = Perks.Supplier(supplier.marineSaltPrice);
                string label = tank.IsSaltwater ? $"Change to fresh water ({Economy.Format(salt)})" : $"Change to salt water ({Economy.Format(salt)})";
                if (!tank.IsSaltwater && !Progression.IsUnlocked(XpTrack.Fishkeeping, supplier.marineSaltLevel))
                    GUILayout.Label($"<i>Salt water tanks: {Progression.LockText(XpTrack.Fishkeeping, supplier.marineSaltLevel)}</i>", Rich());
                else if (GUILayout.Button(label) && Economy.TrySpend(salt, "changing a tank's water")) tank.SetSaltwater(!tank.IsSaltwater);
            }
            DrawDecorations();
            GUI.enabled = true;
#if UNITY_EDITOR
            if (!ReadOnly && GUILayout.Button("Dirty the water (dev)")) tank.DevMakeDirty();
            if (!ReadOnly && GUILayout.Button("Grow every fish up (dev)"))
                foreach (var fish in tank.Residents) fish.DevGrowUp();
#endif
        }

        static GUIStyle tabStyle;
        static GUIStyle TabStyle() => tabStyle != null ? tabStyle : tabStyle = new GUIStyle(GUI.skin.button) { richText = true, fontSize = 12, padding = new RectOffset(3, 3, 4, 5) };

        /// <summary>
        /// How many fish live here against the tank's limit. Going over the limit is allowed, up to
        /// the hard limit, but makes the water dirty faster, spreads illness and stresses every fish.
        /// </summary>
        void DrawFishCount(string sizeName)
        {
            int count = tank.Residents.Count;
            string dead = tank.Corpses.Count > 0 ? $"   <color=#ff8080>Dead: {tank.Corpses.Count}</color>" : "";
            if (tank.IsOvercrowded)
            {
                GUILayout.Label($"{sizeName} tank. <color=#ff8080><b>Fish: {count} / {tank.Capacity}</b></color>{dead}", Rich());
                GUILayout.Label($"<size=11><color=#ff9a8a>Overcrowded by {tank.OverLimit}: the water fouls faster, illness spreads and every fish here is stressed. Move some out.</color></size>", Rich());
            }
            else
            {
                string color = count == tank.Capacity ? "#ffd27f" : "#ffffff";
                GUILayout.Label($"{sizeName} tank. <color={color}><b>Fish: {count} / {tank.Capacity}</b></color>{dead}", Rich());
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
                if (GUILayout.Button($"Feed {name.ToLower()} ({left} left)") && Economy.TryUseFood(food)) storeTank.Feeding.Feed(food, 8);
            }
            if (!any) GUILayout.Label("<color=#ff9a8a>No food! Order some at the supplier computer.</color>", Rich());
        }

        /// <summary>Each piece of equipment: its model and how worn it is (repairs are on the Care tab).</summary>
        void DrawEquipment()
        {
            bool any = false;
            foreach (var kind in Equipment.Kinds)
            {
                var item = tank.GetEquipment(kind);
                if (item == null) continue;
                any = true;
                if (item.broken) GUILayout.Label($"<color=#ff8080><b>{item.Name}: broken!</b></color> <size=11>Repair it on the Care tab.</size>", Rich());
                else GUILayout.Label($"{item.Name}: <size=11>{item.BreakChance * 100f:0}% chance to break per step</size>", Rich());
            }
            if (!any) GUILayout.Label("Equipment: none (buy at the supplier computer)", Rich());
        }

        /// <summary>A repair button for each broken piece of equipment.</summary>
        void DrawRepairs()
        {
            foreach (var kind in Equipment.Kinds)
            {
                var item = tank.GetEquipment(kind);
                if (item == null || !item.broken) continue;
                GUILayout.Label($"<color=#ff8080><b>{item.Name}: broken!</b></color>", Rich());
                if (GUILayout.Button($"Repair ({Economy.RepairKits} repair kits)")) tank.Repair(kind);
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
            foreach (var line in sick) GUILayout.Label($"<color=#ff9a9a>{line}</color>", Rich());
            GUILayout.Label("<size=11>Dose the tank with medicine (Care), or move them to quarantine.</size>", Rich());
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
                GUILayout.Label($"<i>Medicine: {Progression.LockText(XpTrack.Fishkeeping, supplier.medicineLevel)}</i>", Rich());
                return;
            }
            int sick = 0;
            foreach (var fish in tank.Residents) if (fish.Health.IsSick) sick++;
            GUILayout.Label($"Medicine for the whole tank ({Economy.Format(Perks.Supplier(supplier.medicinePrice))} a dose)" + (sick > 0 ? $": <color=#ff8080>{sick} sick</color>" : ": no one is sick"), Rich());
            GUILayout.BeginHorizontal();
            foreach (Medicine medicine in new[] { Medicine.Antiviral, Medicine.Antibacterial, Medicine.Antiparasitic })
                if (GUILayout.Button(medicine.ToString(), SmallButton(), GUILayout.MinWidth(50f))) DoseTank(medicine);
            GUILayout.EndHorizontal();
        }

        static GUIStyle smallButton;
        static GUIStyle SmallButton() => smallButton != null ? smallButton : smallButton = new GUIStyle(GUI.skin.button) { fontSize = 11, padding = new RectOffset(4, 4, 3, 3) };

        void DoseTank(Medicine medicine)
        {
            int price = Perks.Supplier(Balance.Data.supplier.medicinePrice);
            if (!Economy.TrySpend(price, $"{medicine.ToString().ToLower()} for {storeTank.Title}")) return;
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
            if (neighbourTank == null || switchTank == null) return;
            var previous = neighbourTank(-1);
            var next = neighbourTank(1);
            if (previous == null && next == null) return;
            UiCheck.Begin("Tank switch bar", tankBar);
            GUILayout.BeginHorizontal();
            if (previous != null && GUILayout.Button($"< {previous.Title}", GUILayout.Height(30f))) switchTank(-1);
            GUILayout.FlexibleSpace();
            if (next != null && GUILayout.Button($"{next.Title} >", GUILayout.Height(30f))) switchTank(1);
            GUILayout.EndHorizontal();
            UiCheck.End();
        }

        /// <summary>Heater thermostat: a slider, the range the fish here need, and a one-click best setting.</summary>
        void DrawHeater()
        {
            GUILayout.Space(2f);
            GUILayout.Label($"<b>Heater: {tank.HeaterSetting:0} C</b>", Rich());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", GUILayout.Width(26f))) tank.SetHeater(tank.HeaterSetting - 1f);
            float setting = GUILayout.HorizontalSlider(tank.HeaterSetting, 18f, 32f, GUILayout.ExpandWidth(true));
            if (!Mathf.Approximately(Mathf.Round(setting), tank.HeaterSetting)) tank.SetHeater(setting);
            if (GUILayout.Button("+", GUILayout.Width(26f))) tank.SetHeater(tank.HeaterSetting + 1f);
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
                if (need != null) GUILayout.Label($"<size=11>{need}</size>", Rich());
                if (known.HasValue)
                {
                    float best = Mathf.Round((known.Value.x + known.Value.y) * 0.5f);
                    if (!Mathf.Approximately(best, tank.HeaterSetting) && GUILayout.Button($"Set to {best:0} C for these fish")) tank.SetHeater(best);
                }
            }
        }

        void DrawDecorations()
        {
            var decor = tank.Decor;
            if (decor == null) return;
            GUILayout.Space(2f);
            showDecorations = GUILayout.Toggle(showDecorations, $" Decorations: {decor.Count} / {decor.Capacity}{(tank.HasPlants ? ", with plants" : "")}");
            if (!showDecorations) return;
            var ids = decor.Ids;
            if (ids.Count == 0) GUILayout.Label("<i>None. Buy some at the supplier computer.</i>", Rich());
            float sellBack = Balance.Data.supplier.decorationSellBack;
            for (int i = 0; i < ids.Count; i++)
            {
                var item = TankDecor.Find(ids[i]);
                int refund = item != null ? Mathf.RoundToInt(item.price * sellBack) : 0;
                GUILayout.BeginHorizontal();
                GUILayout.Label(item != null ? item.name : ids[i], GUILayout.Width(140f));
                if (GUILayout.Button(refund > 0 ? $"Remove +{Economy.Format(refund)}" : "Remove"))
                {
                    decor.RemoveAt(i);
                    if (refund > 0) Economy.Earn(refund, $"sold back a {(item != null ? item.name.ToLower() : "decoration")}");
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>The tank's quality score, what it's made of, and how far it is from the next size.</summary>
        void DrawQuality()
        {
            int quality = tank.Quality;
            var sizes = Balance.Data.store.tankSizes;
            int next = tank.SizeIndex + 1;
            int need = next < sizes.Count ? sizes[next].qualityNeeded : 0;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Aquarium quality <b>{quality}</b>" + (need > 0 ? $" / {need}" : ""), Rich(), GUILayout.Width(150f));
            if (need > 0)
            {
                var rect = GUILayoutUtility.GetRect(40f, 14f, GUILayout.ExpandWidth(true));
                rect.y += 4f;
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.35f), 0f, 0f);
                bool ready = quality >= need;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(quality / (float)need), rect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                    ready ? new Color(0.35f, 0.85f, 0.4f) : UiTheme.Aqua, 0f, 0f);
            }
            GUILayout.EndHorizontal();
            var parts = TankQuality.Breakdown(tank);
            var bits = new List<string>();
            foreach (var part in parts) bits.Add($"{part.label} +{part.points}");
            GUILayout.Label($"<size=11>{(bits.Count > 0 ? string.Join(",  ", bits) : "Nothing yet. Equipment (better models count more) and decorations (plants and variety count extra) raise it.")}</size>", Rich());
        }

        /// <summary>The next size up: what it holds, which fish need it, and the button once the quality is there.</summary>
        void DrawUpgrade()
        {
            var sizes = Balance.Data.store.tankSizes;
            int next = tank.SizeIndex + 1;
            if (next >= sizes.Count || upgradeTank == null) return;
            var info = sizes[next];
            var kinds = TankQuality.KindsNeeding(next);
            GUILayout.Label($"<size=11>A {info.name.ToLower()} tank holds {Tank.CapacityOf(info)} fish and {info.decorationSlots} decorations" + (kinds != null ? $", and can keep {kinds}." : "."), Rich());
            var reason = StoreLayout.WhyCantUpgrade(storeTank, next);
            if (reason != null && reason != StoreLayout.NoMoney)
            {
                GUILayout.Label($"<size=11><color=#ffd27f>{reason}</color></size>", Rich());
                return;
            }
            bool wasEnabled = GUI.enabled;
            int price = Perks.Upgrade(info.upgradePrice);
            GUI.enabled = wasEnabled && reason == null;
            if (GUILayout.Button($"Upgrade to {info.name.ToLower()} tank  {Economy.Format(price)}")) upgradeTank(storeTank, next);
            GUI.enabled = wasEnabled;
        }

        static void Bar(string label, float value, bool highIsGood)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Rich(), GUILayout.Width(95f));
            var rect = GUILayoutUtility.GetRect(60f, 14f, GUILayout.ExpandWidth(true));
            rect.y += 4f;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.35f), 0f, 0f);
            float good = highIsGood ? value : 1f - value;
            var fill = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height);
            GUI.DrawTexture(fill, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.Lerp(new Color(0.9f, 0.3f, 0.25f), new Color(0.35f, 0.85f, 0.4f), good), 0f, 0f);
            GUILayout.Label($"{value * 100f:0}%", GUILayout.Width(40f));
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
                GUILayout.Label($"<color=#ff8080><b>Dead</b> ({fish.CauseOfDeath})</color>", Rich());
                GUILayout.Label("Dead fish slowly make the rest of the tank sick and upset customers.");
                GUI.enabled = !ReadOnly;
                if (GUILayout.Button("Remove from the tank"))
                {
                    selected.Remove(fish);
                    fish.RemoveFromTank();
                    Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpRemoveDeadFish);
                }
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close (Esc)")) selected.Clear();
                UiCheck.End();
                return;
            }

            // Everything about the fish scrolls; the header and the move buttons stay put.
            fishScroll = UiCheck.BeginScroll("Fish window: details", fishScroll);
            DrawFishHealth(fish);
            DrawFishNeeds(fish);
            DrawFishBreeding(fish);

            Section("Value");
            GUILayout.Label($"Appraised value: <b>~{Economy.Format(fish.AppraisedValue)}</b>", Rich());
            if (storeTank.Role == TankRole.ForSale) DrawPrice(fish);
            if (fish.Health.IsSick)
                GUILayout.Label($"<size=11>Treat the tank with medicine on the Care tab. It goes to every fish in the water.</size>", Rich());
#if UNITY_EDITOR
            if (!ReadOnly && !fish.IsDead && GUILayout.Button("Next illness (dev)")) fish.Health.DevNextIllness();
#endif
            UiCheck.EndScroll();

            DrawMoveButtons(fish);
            if (GUILayout.Button("Close (Esc)")) selected.Clear();
            UiCheck.End();
        }

        /// <summary>Name, then sex and age as bold coloured badges so they stand out, then the species.</summary>
        void DrawFishHeader(Fish fish)
        {
            var species = fish.Species;
            GUILayout.Label($"<size=17><b>{fish.FishName}</b></size>", Rich());
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
            if (fish.Health.IsSick && !fish.IsDead) UiGauges.Badge("+ SICK", Color.Lerp(SickRed, new Color(1f, 0.45f, 0.4f), Mathf.PingPong(Time.unscaledTime * 1.5f, 1f)));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (fish.Health.IsSick && !fish.IsDead)
            {
                var disease = fish.Health.Disease;
                GUILayout.BeginVertical(SickBox());
                bool known = Research.Has(ResearchUnlock.Pathology);
                GUILayout.Label($"<b>{disease.displayName}</b>" + (known ? $"  <size=11>({fish.Health.Severity * 100f:0}% bad)</size>" : ""), Rich());
                if (!string.IsNullOrEmpty(disease.description)) GUILayout.Label($"<size=11>{disease.description}</size>", Rich());
                if (known) GUILayout.Label($"<size=11>Cure: {CureText(disease)}.</size>", Rich());
                GUILayout.EndVertical();
            }
            if (fish.IdentityHidden) GUILayout.Label("<b>Mystery fry</b>: too small to tell what it is yet. It shows its colours when it grows into a juvenile.", Rich());
            else
            {
                var look = fish.Phenotype;
                if (look.IsHybrid)
                {
                    GUILayout.Label($"<b>{fish.SpeciesLabel}</b>  <size=11>(cross-bred, {look.RarityLabel} looks)</size>", Rich());
                    GUILayout.Label($"<size=11>Body: {species.displayName} ({species.rarity}), fins: {look.finSpecies.displayName}, tail: {look.tailSpecies.displayName}. It lives like a {species.displayName}.</size>", Rich());
                }
                else GUILayout.Label($"<b>{species.displayName}</b>  <size=11>({species.rarity} species, {look.RarityLabel} looks)</size>", Rich());
                if (!string.IsNullOrEmpty(species.family)) GUILayout.Label($"<size=11><i>{species.scientificName}</i>, {species.family}</size>", Rich());
            }
            string growing = fish.Stage == LifeStage.Adult ? "" : $", grown up in about {Mathf.Max(0.5f, (1f - fish.Growth) * species.daysToAdult):0.#} days";
            GUILayout.Label($"<size=11>{fish.AgeDays:0.#} days old{growing}, generation {fish.Generation}</size>", Rich());
            if (!string.IsNullOrEmpty(fish.ParentNames)) GUILayout.Label($"<size=11>Parents: {fish.ParentNames}</size>", Rich());
            if (fish.IsPregnant)
                GUILayout.Label($"<color=#ffb3d9><b>Pregnant</b> by {fish.UnbornFather}: about {fish.UnbornCount} fry due in {BreedingSystem.Steps(fish.StepsToBirth)}</color>", Rich());
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
                GUILayout.Label($"<size=11><color={(change > 0f ? "#9fe39f" : "#ff9a8a")}>{(change > 0f ? "Rising" : "Falling")} to {next * 100f:0}% at the next time step</color>" +
                                (Mathf.Abs(next - report.total) > 0.01f ? $" (heading for {report.total * 100f:0}%)" : "") + "</size>", Rich());
            }
            else GUILayout.Label("<size=11>Steady: health only changes at time steps (closing up and overnight).</size>", Rich());

            Penalty("Hunger", report.hunger, $"{fish.Hunger * 100f:0}% hungry");
            Penalty("Illness", report.illness, fish.Health.IsSick ? $"{fish.Health.Disease.displayName} ({fish.Health.Severity * 100f:0}%)" : "healthy");
            Penalty("Water", report.environment, report.environmentMismatch < 0.01f ? "suits it" : "not right for it");
            foreach (var note in FishHealth.EnvironmentNotes(species, tank)) SubLine(note, 0f, true);
            Penalty("Comfort", report.comfort, $"{report.comfortLevel * 100f:0}% comfortable");
            foreach (var note in fish.Health.ComfortNotes()) SubLine(note.text, note.delta, false);
            if (report.overcrowding > 0f) Penalty("Crowding", report.overcrowding, $"{tank.OverLimit} fish over the tank's limit");
            if (report.injury > 0f) Penalty("Wounds", report.injury, string.IsNullOrEmpty(fish.Health.InjuryCause) ? "healing" : fish.Health.InjuryCause);
            if (report.medicineDamage > 0f) Penalty("Medicine", report.medicineDamage, $"overdosed {fish.Health.MedicineDamage}x" + (fish.Health.Infertile ? ", can't breed" : ""));
            if (report.companionship > 0f) GUILayout.Label("<color=#9fe39f>Good company</color>" + (Research.Has(ResearchUnlock.HealthDetails) ? $"<color=#9fe39f>  +{report.companionship * 100f:0}</color>" : ""), Rich());
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
            GUILayout.Label("Temperature" + (tempKnown ? $": likes {species.tempMin:0}-{species.tempMax:0} C" : ""), Rich());
            UiGauges.Temperature(tank.Temperature, tempKnown ? new Vector2(species.tempMin, species.tempMax) : (Vector2?)null);
            if (Research.CanReadPh)
            {
                bool phKnown = Research.Knows(species, ResearchFact.Ph);
                GUILayout.Label("pH" + (phKnown ? $": likes {species.phMin:0.0}-{species.phMax:0.0}" : ""), Rich());
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
            if (GUILayout.Button($"Journal: {species.displayName}")) JournalView.Open(species.id);
        }

        void DrawFishBreeding(Fish fish)
        {
            if (fish.IsPregnant || fish.IdentityHidden) return;
            var species = fish.Species;
            Section("Breeding");
            string notReady = BreedingSystem.WhyNotReady(fish, tank);
            string how = Research.Knows(species, ResearchFact.Breeding) ? (species.reproduction == Reproduction.Livebearer ? " (livebearer)" : " (egg-layer)") : "";
            GUILayout.Label(notReady == null
                ? $"<color=#9fe39f>Ready to breed</color>{how}: needs a {(fish.Sex == Sex.Male ? "female" : "male")} {species.displayName} in the tank."
                : Research.Has(ResearchUnlock.Breeding) ? $"Not ready{how}: {notReady.Replace(fish.FishName + " ", "")}" : "Not ready to breed.", Rich());
        }

        /// <summary>Move buttons for every other tank, with how full each is; afterwards the next fish here is picked.</summary>
        void DrawMoveButtons(Fish fish)
        {
            GUI.enabled = !ReadOnly;
            GUILayout.Label("Move to:");
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
                GUI.enabled = wasEnabled && !t.IsFull && !tooSmall;
                string count = tooSmall ? "<color=#ff9a8a>too small</color>" : t.Residents.Count >= t.Capacity ? $"<color=#ff9a8a>{t.Residents.Count}/{t.Capacity}</color>" : $"{t.Residents.Count}/{t.Capacity}";
                if (GUILayout.Button($"{other.Title} ({count})", RichButton(), GUILayout.Width(146f)))
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
            GUILayout.Label($"<b><color={UiTheme.SunHex}>{title.ToUpper()}</color></b>", Rich());
        }

        /// <summary>An indented line under a heading; with a delta it shows the comfort it adds or takes.</summary>
        static void SubLine(string text, float delta, bool bad)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(14f);
            string color = bad || delta < -0.001f ? "#ffb0a0" : delta > 0.001f ? "#b5ecb5" : "#d8dde4";
            GUILayout.Label($"<size=12><color={color}>• {text}</color></size>", Rich());
            if (Mathf.Abs(delta) > 0.001f && Research.Has(ResearchUnlock.HealthDetails))
            {
                GUILayout.Label($"<size=12><color={color}>{(delta > 0f ? "+" : "-")}{Mathf.Abs(delta) * 100f:0}</color></size>", Rich(), GUILayout.Width(34f));
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
            if (GUI.Button(stepLeft, "<b><</b>", StepStyle())) Cycle(-1);
            if (GUI.Button(stepRight, "<b>></b>", StepStyle())) Cycle(1);
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
            GUILayout.Label("Price: $", GUILayout.Width(52f));
            priceText = GUILayout.TextField(priceText, 6, GUILayout.Width(60f));
            if (int.TryParse(priceText, out var typed) && typed != fish.Price) fish.Price = typed;
            if (GUILayout.Button("-5", GUILayout.Width(34f))) SetPrice(fish, fish.Price - 5);
            if (GUILayout.Button("+5", GUILayout.Width(34f))) SetPrice(fish, fish.Price + 5);
            if (GUILayout.Button("Appraised")) SetPrice(fish, fish.AppraisedValue);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (fish.Stage == LifeStage.Fry) GUILayout.Label("<i>Too young to sell: customers only buy juveniles and adults.</i>", Rich());
            else if (fish.Price <= 0) GUILayout.Label("<i>No price: customers can't buy it.</i>", Rich());
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
            GUILayout.Label($"<color={color}>{label}</color>", Rich(), GUILayout.Width(80f));
            GUILayout.Label(penalty < 0.02f || !Research.Has(ResearchUnlock.HealthDetails) ? detail : $"{detail}  <color={color}>-{penalty * 100f:0}</color>", Rich());
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
                GUILayout.Label($"{a.FishName} and {b.FishName}: move them to a breeder tank to pair them up. Compatible pairs in other tanks sometimes breed by chance at each time step.", Rich());
            }
            else
            {
                bool ok = storeTank.Breeding.CanBreed(a, b, out var reason) && !ReadOnly;
                GUI.enabled = ok;
                if (GUILayout.Button($"♥ Breed {a.FishName} and {b.FishName}", UiTheme.Primary)) storeTank.Breeding.Breed(a, b);
                GUI.enabled = true;
                if (!string.IsNullOrEmpty(reason)) GUILayout.Label($"<i>{reason}</i>", Rich());
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

        void HandleClick()
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || cam == null) return;
            var m = e.mousePosition;
            if (leftPanel.Contains(m) || fishWindow.Contains(m) || breedBar.Contains(m) || logPanel.Contains(m) || stepLeft.Contains(m) || stepRight.Contains(m)) return;

            // IMGUI coordinates are top-left based and scaled; convert back to screen pixels.
            var guiToScreen = GUI.matrix.MultiplyPoint3x4(m);
            var ray = cam.ScreenPointToRay(new Vector3(guiToScreen.x, Screen.height - guiToScreen.y, 0f));
            Fish fish = null;
            if (Physics.Raycast(ray, out var hit, 50f)) fish = hit.collider.GetComponentInParent<Fish>();
            if (fish == null || fish.Tank != tank) fish = NearestFishOnScreen(new Vector2(guiToScreen.x, Screen.height - guiToScreen.y));
            if (fish != null)
            {
                if (selected.Contains(fish)) selected.Remove(fish);
                else
                {
                    if (selected.Count == 2 || fish.IsDead || (selected.Count == 1 && selected[0].IsDead)) selected.Clear();
                    selected.Add(fish);
                }
                e.Use();
                return;
            }
            selected.Clear();
            e.Use();
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
