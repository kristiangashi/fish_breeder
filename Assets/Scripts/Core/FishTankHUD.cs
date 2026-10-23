using System;
using System.Collections.Generic;
using FishBreeder.Feeding;
using FishBreeder.Health;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Placeholder IMGUI for looking into one tank: the tank's water and equipment on the left,
    /// a status window next to the clicked fish, and the breeding bar when two fish are picked.
    /// While the shop is open everything is view-only. Replace with a real UI later.
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
        Rect leftPanel, fishWindow, breedBar, logPanel;
        string priceText = "";
        Fish priceTextFor;

        bool ReadOnly => shopIsOpen != null && shopIsOpen();

        public void Initialize(StoreTank current, IReadOnlyList<StoreTank> tanks, Camera camera, Action back, Func<bool> isShopOpen)
        {
            storeTank = current;
            tank = current.Tank;
            allTanks = tanks;
            cam = camera;
            onBack = back;
            shopIsOpen = isShopOpen;
            selected.Clear();
        }

        void Update() => selected.RemoveAll(f => f == null || f.Tank != tank);

        void OnGUI()
        {
            if (tank == null) return;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            leftPanel = new Rect(10f, 10f, 270f, 520f);
            logPanel = new Rect(10f, height - 150f, 460f, 140f);
            breedBar = selected.Count == 2 ? new Rect(width * 0.5f - 210f, height - 70f, 420f, 60f) : Rect.zero;
            fishWindow = Rect.zero;

            DrawTankPanel();
            if (selected.Count > 0) DrawFishWindow(selected[selected.Count - 1], scale, width, height);
            if (selected.Count == 2) DrawBreedBar();
            DrawLog();
            DrawSelectionMarkers(scale, height);
            HandleClick();
        }

        // ------------------------------------------------------------ tank panel

        void DrawTankPanel()
        {
            GUILayout.BeginArea(leftPanel, GUI.skin.box);
            GUILayout.Label($"<b>{storeTank.Title}</b>   {Economy.Format(Economy.Money)}", Rich());
            if (ReadOnly) GUILayout.Label("<color=#ffd27f>The shop is open: you can look, but tank work waits until closing.</color>", Rich());
            GUILayout.Label($"Fish: {tank.Residents.Count} / {GameConfig.Active.maxFishPerTank}" + (tank.Corpses.Count > 0 ? $"   <color=#ff8080>Dead: {tank.Corpses.Count}</color>" : ""), Rich());
            int eggs = EggClutch.EggsIn(tank);
            if (eggs > 0) GUILayout.Label($"Eggs: {eggs} (hatch at the next time step)");

            Bar("Water quality", tank.WaterQuality, true);
            Bar("Algae", tank.Algae, false);
            Bar("Oxygen", tank.Oxygen, true);
            GUILayout.Label($"Temperature: {tank.Temperature:0.#} C" + (tank.HasHeater ? $"  (heater set to {tank.HeaterSetting:0} C)" : "  (room temperature)"));
            GUILayout.Label($"pH: {tank.Ph:0.0}    Water: {(tank.IsSaltwater ? "salt" : "fresh")} ({tank.Salinity:0} ppt)");
            GUILayout.Label($"Decorations: {tank.DecorCount}{(tank.HasPlants ? ", with plants" : "")}" + (tank.HasPests ? "   <color=#ff8080>Pests!</color>" : ""), Rich());
            var equipment = new List<string>();
            if (tank.HasFilter) equipment.Add("filter");
            if (tank.HasHeater) equipment.Add("heater");
            if (tank.HasBubbler) equipment.Add("bubbler");
            GUILayout.Label("Equipment: " + (equipment.Count > 0 ? string.Join(", ", equipment) : "none (buy at the supplier computer)"));

            GUI.enabled = !ReadOnly;
            if (tank.HasHeater)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Heater:");
                if (GUILayout.Button("-", GUILayout.Width(28f))) tank.SetHeater(tank.HeaterSetting - 1f);
                GUILayout.Label($"{tank.HeaterSetting:0} C", GUILayout.Width(40f));
                if (GUILayout.Button("+", GUILayout.Width(28f))) tank.SetHeater(tank.HeaterSetting + 1f);
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button($"Feed flakes ({Economy.FoodPortions(FoodType.Flakes)} left)") && Economy.TryUseFood(FoodType.Flakes)) storeTank.Feeding.Feed(FoodType.Flakes, 8);
            if (GUILayout.Button($"Feed premium ({Economy.FoodPortions(FoodType.Premium)} left)") && Economy.TryUseFood(FoodType.Premium)) storeTank.Feeding.Feed(FoodType.Premium, 8);
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
            if (tank.Residents.Count == 0 && tank.Corpses.Count == 0)
            {
                int salt = Balance.Data.supplier.marineSaltPrice;
                string label = tank.IsSaltwater ? $"Change to fresh water ({Economy.Format(salt)})" : $"Change to salt water ({Economy.Format(salt)})";
                if (GUILayout.Button(label) && Economy.TrySpend(salt, "changing a tank's water")) tank.SetSaltwater(!tank.IsSaltwater);
            }
            if (tank.Corpses.Count > 0 && GUILayout.Button($"Remove dead fish ({tank.Corpses.Count})"))
            {
                foreach (var corpse in new List<Fish>(tank.Corpses)) corpse.RemoveFromTank();
                GameLog.Add("You netted out the dead fish.");
            }
            GUI.enabled = true;
            if (onBack != null && GUILayout.Button("Back to store (Esc)")) onBack();
            GUILayout.EndArea();
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
            const float w = 300f, h = 470f;
            var screen = cam.WorldToScreenPoint(fish.transform.position);
            var anchor = new Vector2(screen.x / scale, height - screen.y / scale);
            float x = anchor.x + 40f;
            if (x + w > width - 10f) x = anchor.x - 40f - w;
            x = Mathf.Clamp(x, leftPanel.xMax + 10f, width - w - 10f);
            float y = Mathf.Clamp(anchor.y - h * 0.4f, 10f, height - h - 80f);
            fishWindow = new Rect(x, y, w, h);

            GUILayout.BeginArea(fishWindow, GUI.skin.box);
            var species = fish.Species;
            var p = fish.Phenotype;
            string sex = fish.Sex == Sex.Male ? "male" : "female";
            GUILayout.Label($"<size=15><b>{fish.FishName}</b></size>  <i>{sex}</i>", Rich());
            GUILayout.Label($"{species.displayName}  ({species.rarity} species, {p.RarityLabel} looks)");
            GUILayout.Label($"{fish.Stage}, {fish.AgeDays:0.#} days old, generation {fish.Generation}");
            if (!string.IsNullOrEmpty(fish.ParentNames)) GUILayout.Label($"Parents: {fish.ParentNames}");

            if (fish.IsDead)
            {
                GUILayout.Label($"<color=#ff8080><b>Dead</b> ({fish.CauseOfDeath})</color>", Rich());
                GUILayout.Label("Dead fish slowly make the rest of the tank sick and upset customers.");
                GUI.enabled = !ReadOnly;
                if (GUILayout.Button("Remove from the tank"))
                {
                    selected.Remove(fish);
                    fish.RemoveFromTank();
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

            GUILayout.Space(4f);
            GUILayout.Label($"Appraised value: <b>~{Economy.Format(fish.AppraisedValue)}</b>", Rich());
            if (storeTank.Role == TankRole.ForSale) DrawPrice(fish);

            GUI.enabled = !ReadOnly;
            if (fish.Health.IsSick)
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
            if (GUILayout.Button("Close")) selected.Clear();
            GUILayout.EndArea();
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
                GUILayout.Label($"{a.FishName} and {b.FishName}: move them to a breeder tank to pair them up. (Pairs in other tanks sometimes breed by chance.)");
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
            if (leftPanel.Contains(m) || fishWindow.Contains(m) || breedBar.Contains(m) || logPanel.Contains(m)) return;

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
            float best = Mathf.Max(28f, Screen.height * 0.04f);
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

        static GUIStyle richLabel, centeredLabel;

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
