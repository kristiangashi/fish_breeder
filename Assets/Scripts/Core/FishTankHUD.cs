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
    /// Placeholder IMGUI for looking into one tank: the tank's water, equipment, heater, decorations
    /// and size on the left, a status window next to the selected fish (the camera follows it), and
    /// the breeding bar when two fish are picked. Left/right arrows or the little buttons beside the
    /// fish step through the fish in the tank. While the shop is open everything is view-only.
    /// Replace with a real UI later.
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
        Rect leftPanel, fishWindow, breedBar, logPanel, stepLeft, stepRight;
        string priceText = "";
        Fish priceTextFor;
        Vector2 panelScroll;
        bool showDecorations;
        Func<StoreTank, int, bool> upgradeTank;

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
            if (Input.GetKeyDown(KeyCode.RightArrow)) Cycle(1);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Cycle(-1);
        }

        void OnGUI()
        {
            if (tank == null) return;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            // Runs down to the log, so eggs, pregnancies and breeding lines never push the buttons off the bottom.
            leftPanel = new Rect(10f, 10f, 270f, Mathf.Max(520f, height - 170f));
            logPanel = new Rect(10f, height - 150f, 460f, 140f);
            breedBar = selected.Count == 2 ? new Rect(width * 0.5f - 210f, height - 70f, 420f, 60f) : Rect.zero;
            fishWindow = Rect.zero;
            stepLeft = stepRight = Rect.zero;

            DrawTankPanel();
            if (selected.Count > 0)
            {
                DrawStepButtons(Focus, scale, height);
                DrawFishWindow(Focus, scale, width, height);
            }
            if (selected.Count == 2) DrawBreedBar();
            DrawLog();
            DrawSelectionMarkers(scale, height);
            HandleClick();
        }

        // ------------------------------------------------------------ tank panel

        void DrawTankPanel()
        {
            GUILayout.BeginArea(leftPanel, GUI.skin.box);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            var size = tank.SizeInfo;
            GUILayout.Label($"<b>{storeTank.Title}</b>   {Economy.Format(Economy.Money)}", Rich());
            if (ReadOnly) GUILayout.Label("<color=#ffd27f>The shop is open: you can look, but tank work waits until closing.</color>", Rich());
            GUILayout.Label($"{size.name} tank. Fish: {tank.Residents.Count} / {tank.Capacity}" + (tank.Corpses.Count > 0 ? $"   <color=#ff8080>Dead: {tank.Corpses.Count}</color>" : ""), Rich());
            foreach (var clutch in EggClutch.All)
                if (clutch != null && clutch.Tank == tank)
                    GUILayout.Label($"<color=#ffe9a8>Eggs: {clutch.Count} {clutch.Label}, hatch in {BreedingSystem.Steps(clutch.StepsToHatch)}</color>", Rich());
            foreach (var fish in tank.Residents)
                if (fish.IsPregnant)
                    GUILayout.Label($"<color=#ffb3d9>Pregnant: {fish.FishName} ({fish.Species}), due in {BreedingSystem.Steps(fish.StepsToBirth)}</color>", Rich());

            foreach (var line in BreedingSystem.Readiness(tank))
                GUILayout.Label($"<color=#c9d7e8>{line}</color>", Rich());

            Bar("Water quality", tank.WaterQuality, true);
            Bar("Algae", tank.Algae, false);
            Bar("Oxygen", tank.Oxygen, true);
            GUILayout.Label($"Temperature: {tank.Temperature:0.#} C" + (tank.HasHeater ? "  (heater)" : "  (room temperature)"));
            GUILayout.Label($"pH: {tank.Ph:0.0}    Water: {(tank.IsSaltwater ? "salt" : "fresh")} ({tank.Salinity:0} ppt)");
            if (tank.HasPests) GUILayout.Label("<color=#ff8080>Pests! Treat the water.</color>", Rich());
            var equipment = new List<string>();
            if (tank.HasFilter) equipment.Add("filter");
            if (tank.HasHeater) equipment.Add("heater");
            if (tank.HasBubbler) equipment.Add("bubbler");
            GUILayout.Label("Equipment: " + (equipment.Count > 0 ? string.Join(", ", equipment) : "none (buy at the supplier computer)"));

            GUI.enabled = !ReadOnly;
            if (tank.HasHeater) DrawHeater();
            if (GUILayout.Button($"Feed flakes ({Economy.FoodPortions(FoodType.Flakes)} left)") && Economy.TryUseFood(FoodType.Flakes)) storeTank.Feeding.Feed(FoodType.Flakes, 8);
            if (Economy.FoodPortions(FoodType.Premium) > 0 && GUILayout.Button($"Feed premium ({Economy.FoodPortions(FoodType.Premium)} left)") && Economy.TryUseFood(FoodType.Premium)) storeTank.Feeding.Feed(FoodType.Premium, 8);
            if (GUILayout.Button($"Wipe tank ({tank.WipesLeft} wipes left)")) tank.Clean();
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
            if (tank.Residents.Count == 0 && tank.Corpses.Count == 0)
            {
                var supplier = Balance.Data.supplier;
                int salt = supplier.marineSaltPrice;
                string label = tank.IsSaltwater ? $"Change to fresh water ({Economy.Format(salt)})" : $"Change to salt water ({Economy.Format(salt)})";
                if (!tank.IsSaltwater && !Progression.IsUnlocked(XpTrack.Fishkeeping, supplier.marineSaltLevel))
                    GUILayout.Label($"<i>Salt water tanks: {Progression.LockText(XpTrack.Fishkeeping, supplier.marineSaltLevel)}</i>", Rich());
                else if (GUILayout.Button(label) && Economy.TrySpend(salt, "changing a tank's water")) tank.SetSaltwater(!tank.IsSaltwater);
            }
            DrawDecorations();
            DrawUpgrade();
            GUI.enabled = true;
#if UNITY_EDITOR
            if (!ReadOnly && GUILayout.Button("Dirty the water (dev)")) tank.DevMakeDirty();
            if (!ReadOnly && GUILayout.Button("Grow every fish up (dev)"))
                foreach (var fish in tank.Residents) fish.DevGrowUp();
#endif
            GUILayout.EndScrollView();
            if (onBack != null && GUILayout.Button("Back to store (Esc)")) onBack();
            GUILayout.EndArea();
        }

        /// <summary>Heater thermostat: a slider, the range the fish here need, and a one-click best setting.</summary>
        void DrawHeater()
        {
            var shared = tank.SharedTemperatureRange;
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
                string need = shared.HasValue
                    ? $"Fish here are happy at {shared.Value.x:0}-{shared.Value.y:0} C."
                    : "<color=#ffd27f>These fish don't share a temperature: some will be uncomfortable.</color>";
                GUILayout.Label($"<size=11>{need}</size>", Rich());
                float best = tank.SuggestedTemperature;
                if (!Mathf.Approximately(best, tank.HeaterSetting) && GUILayout.Button($"Set to {best:0} C for these fish")) tank.SetHeater(best);
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

        void DrawUpgrade()
        {
            var sizes = Balance.Data.store.tankSizes;
            int next = tank.SizeIndex + 1;
            if (next >= sizes.Count || upgradeTank == null) return;
            var info = sizes[next];
            GUILayout.Space(2f);
            if (!Progression.IsUnlocked(XpTrack.Fishkeeping, info.unlockLevel))
            {
                GUILayout.Label($"<i>{info.name} tank ({info.capacity} fish): {Progression.LockText(XpTrack.Fishkeeping, info.unlockLevel)}</i>", Rich());
                return;
            }
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && Economy.CanAfford(info.upgradePrice);
            if (GUILayout.Button($"Upgrade to {info.name.ToLower()} tank ({info.capacity} fish)  {Economy.Format(info.upgradePrice)}")) upgradeTank(storeTank, next);
            GUI.enabled = wasEnabled;
        }

        static void Bar(string label, float value, bool highIsGood)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Rich(), GUILayout.Width(95f));
            var rect = GUILayoutUtility.GetRect(120f, 14f, GUILayout.ExpandWidth(true));
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
            const float w = 300f, h = 530f;
            var screen = cam.WorldToScreenPoint(fish.transform.position);
            var anchor = new Vector2(screen.x / scale, height - screen.y / scale);
            float x = anchor.x + 90f;
            if (x + w > width - 10f) x = anchor.x - 90f - w;
            x = Mathf.Clamp(x, leftPanel.xMax + 10f, width - w - 10f);
            float y = Mathf.Clamp(anchor.y - h * 0.4f, 10f, height - h - 80f);
            fishWindow = new Rect(x, y, w, h);

            GUILayout.BeginArea(fishWindow, UiStyles.Window);
            var species = fish.Species;
            var p = fish.Phenotype;
            string sex = fish.Sex == Sex.Male ? "male" : "female";
            GUILayout.Label($"<size=15><b>{fish.FishName}</b></size>  <i>{sex}</i>", Rich());
            GUILayout.Label($"{species.displayName}  ({species.rarity} species, {p.RarityLabel} looks)");
            GUILayout.Label($"{fish.Stage}, {fish.AgeDays:0.#} days old, generation {fish.Generation}");
            if (!string.IsNullOrEmpty(fish.ParentNames)) GUILayout.Label($"Parents: {fish.ParentNames}");
            if (fish.IsPregnant)
                GUILayout.Label($"<color=#ffb3d9><b>Pregnant</b> by {fish.UnbornFather}: about {fish.UnbornCount} fry due in {BreedingSystem.Steps(fish.StepsToBirth)}</color>", Rich());

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
                GUILayout.EndArea();
                return;
            }

            var report = fish.Health.Report();
            GUILayout.Space(4f);
            Bar("<b>Health</b>", report.total, true);
            Penalty("Hunger", report.hunger, $"{fish.Hunger * 100f:0}% hungry" + (species.needsPremiumFood ? ", needs premium food" : ""));
            Penalty("Illness", report.illness, fish.Health.IsSick ? $"{fish.Health.Disease.displayName} ({fish.Health.Severity * 100f:0}%)" : "healthy");
            Penalty("Comfort", report.comfort, $"comfort {report.comfortLevel * 100f:0}%");
            Penalty("Water", report.environment, report.environmentMismatch < 0.05f ? "suits it" : "not right for it");
            if (report.oldAge > 0f) Penalty("Old age", report.oldAge, $"past {species.lifespanDays:0} days");
            if (report.deadTankmates > 0f) Penalty("Dead fish", report.deadTankmates, $"{tank.Corpses.Count} in the tank");

            GUILayout.Label($"Needs: {species.tempMin:0}-{species.tempMax:0} C, pH {species.phMin:0.0}-{species.phMax:0.0}, {(species.IsSaltwater ? "salt" : "fresh")} water" +
                            (species.schoolSize > 0 ? $", {species.schoolSize}+ of its kind" : ""));
            if (!fish.IsPregnant)
            {
                string notReady = BreedingSystem.WhyNotReady(fish, tank);
                string how = species.reproduction == Reproduction.Livebearer ? "livebearer" : "egg-layer";
                GUILayout.Label(notReady == null
                    ? $"<color=#9fe39f>Ready to breed</color> ({how}): needs a {(fish.Sex == Sex.Male ? "female" : "male")} {species.displayName} in the tank."
                    : $"Breeding ({how}): {notReady.Replace(fish.FishName + " ", "")}", Rich());
            }

            GUILayout.Space(4f);
            GUILayout.Label($"Appraised value: <b>~{Economy.Format(fish.AppraisedValue)}</b>", Rich());
            if (storeTank.Role == TankRole.ForSale) DrawPrice(fish);

            GUI.enabled = !ReadOnly;
            if (fish.Health.IsSick && !Progression.IsUnlocked(XpTrack.Fishkeeping, Balance.Data.supplier.medicineLevel))
                GUILayout.Label($"<i>Medicine: {Progression.LockText(XpTrack.Fishkeeping, Balance.Data.supplier.medicineLevel)}</i>", Rich());
            else if (fish.Health.IsSick)
            {
                int cost = Balance.Data.supplier.medicinePrice;
                GUILayout.Label($"Medicine ({Economy.Format(cost)} a dose):");
                GUILayout.BeginHorizontal();
                foreach (Medicine medicine in new[] { Medicine.Antiviral, Medicine.Antibacterial, Medicine.Antiparasitic })
                    if (GUILayout.Button(medicine.ToString()) && Economy.TrySpend(cost, $"{medicine.ToString().ToLower()} for {fish.FishName}")) fish.Health.Treat(medicine);
                GUILayout.EndHorizontal();
            }

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
                GUI.enabled = wasEnabled && !other.Tank.IsFull;
                if (GUILayout.Button(other.Title, GUILayout.Width(138f)) && fish.MoveTo(other.Tank))
                {
                    selected.Remove(fish);
                    GameLog.Add($"Moved {fish.FishName} to {other.Title}.");
                }
                GUI.enabled = wasEnabled;
                column++;
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (GUILayout.Button("Close (Esc)")) selected.Clear();
            GUILayout.EndArea();
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
            if (fish.Price <= 0) GUILayout.Label("<i>No price: customers can't buy it.</i>", Rich());
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
            GUILayout.Label(penalty < 0.02f ? detail : $"{detail}  <color={color}>-{penalty * 100f:0}</color>", Rich());
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------ breeding

        void DrawBreedBar()
        {
            GUILayout.BeginArea(breedBar, GUI.skin.box);
            var a = selected[0];
            var b = selected[1];
            if (storeTank.Role != TankRole.Breeder)
            {
                GUILayout.Label($"{a.FishName} and {b.FishName}: move them to a breeder tank to pair them up. Compatible pairs in other tanks sometimes breed by chance at each time step.");
            }
            else
            {
                bool ok = storeTank.Breeding.CanBreed(a, b, out var reason) && !ReadOnly;
                GUI.enabled = ok;
                if (GUILayout.Button($"Breed {a.FishName} and {b.FishName}")) storeTank.Breeding.Breed(a, b);
                GUI.enabled = true;
                if (!string.IsNullOrEmpty(reason)) GUILayout.Label($"<i>{reason}</i>", Rich());
            }
            GUILayout.EndArea();
        }

        void DrawLog()
        {
            GUILayout.BeginArea(logPanel, GUI.skin.box);
            var entries = GameLog.Entries;
            for (int i = Mathf.Max(0, entries.Count - 6); i < entries.Count; i++)
                GUILayout.Label(entries[i]);
            GUILayout.EndArea();
        }

        void DrawSelectionMarkers(float scale, float guiHeight)
        {
            if (cam == null) return;
            foreach (var fish in selected)
            {
                var screen = cam.WorldToScreenPoint(fish.transform.position + Vector3.up * 0.08f);
                if (screen.z < 0f) continue;
                var gui = new Vector2(screen.x / scale, guiHeight - screen.y / scale);
                GUI.Label(new Rect(gui.x - 60f, gui.y - 22f, 120f, 22f), $"v {fish.FishName}", CenteredLabel());
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

        static GUIStyle richLabel, centeredLabel, stepStyle;

        static GUIStyle StepStyle()
        {
            if (stepStyle == null) stepStyle = new GUIStyle(GUI.skin.button) { richText = true, fontSize = 18 };
            return stepStyle;
        }

        static GUIStyle Rich()
        {
            if (richLabel == null) richLabel = new GUIStyle(GUI.skin.label) { richText = true };
            return richLabel;
        }

        static GUIStyle CenteredLabel()
        {
            if (centeredLabel == null) centeredLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            return centeredLabel;
        }
    }
}
