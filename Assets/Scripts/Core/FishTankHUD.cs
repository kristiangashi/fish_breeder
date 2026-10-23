using System.Collections.Generic;
using FishBreeder.Breeding;
using FishBreeder.Feeding;
using FishBreeder.Health;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Throwaway IMGUI interface so the systems are playable right away.
    /// Replace with a proper UI Toolkit or uGUI screen later.
    /// Click a fish to select it; select two adults to breed them.
    /// </summary>
    public class FishTankHUD : MonoBehaviour
    {
        Tank tank;
        FeedingSystem feeding;
        BreedingSystem breeding;
        Camera cam;
        readonly List<Fish> selected = new List<Fish>();
        Rect leftPanel, rightPanel, logPanel;

        public void Initialize(Tank t, FeedingSystem f, BreedingSystem b, Camera c)
        {
            tank = t;
            feeding = f;
            breeding = b;
            cam = c;
        }

        void Update() => selected.RemoveAll(f => f == null || f.IsDead);

        void OnGUI()
        {
            if (tank == null) return;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            leftPanel = new Rect(10f, 10f, 230f, 250f);
            rightPanel = new Rect(width - 330f, 10f, 320f, 420f);
            logPanel = new Rect(10f, height - 190f, 460f, 180f);

            DrawTankPanel();
            DrawSelectionPanel();
            DrawLog();
            DrawSelectionMarkers(scale, height);
            HandleClick();
        }

        void DrawTankPanel()
        {
            GUILayout.BeginArea(leftPanel, GUI.skin.box);
            GUILayout.Label("<b>Fish Breeder</b>", RichLabel());
            GUILayout.Label($"Fish: {tank.Residents.Count} / {GameConfig.Active.maxFishPerTank}");
            GUILayout.Label($"Water quality: {tank.WaterQuality * 100f:0}%  (illness x{tank.InfectionMultiplier:0.0})");
            if (GUILayout.Button("Feed flakes (cheap, risky)")) feeding.Feed(FoodType.Flakes, 8);
            if (GUILayout.Button("Feed premium (safer)")) feeding.Feed(FoodType.Premium, 8);
            if (GUILayout.Button("Clean tank")) tank.Clean();
            if (GUILayout.Button("Add wild fish") && !tank.IsFull)
                FishFactory.Spawn(Genetics.Genome.CreateRandom(), 1, 1f, tank, tank.RandomFreePoint(0.1f));
            GUILayout.EndArea();
        }

        void DrawSelectionPanel()
        {
            GUILayout.BeginArea(rightPanel, GUI.skin.box);
            if (selected.Count == 0)
            {
                GUILayout.Label("Click a fish to inspect it.\nClick a second fish to pick a breeding partner.");
                GUILayout.EndArea();
                return;
            }

            foreach (var fish in selected) DrawFishInfo(fish);

            if (selected.Count == 2)
            {
                bool ok = breeding.CanBreed(selected[0], selected[1], out var reason);
                GUI.enabled = ok;
                if (GUILayout.Button("Breed these two")) breeding.Breed(selected[0], selected[1]);
                GUI.enabled = true;
                if (!ok) GUILayout.Label($"<i>{reason}</i>", RichLabel());
            }

            if (GUILayout.Button("Clear selection")) selected.Clear();
            GUILayout.EndArea();
        }

        void DrawFishInfo(Fish fish)
        {
            var p = fish.Phenotype;
            GUILayout.Label($"<b>{fish.FishName}</b>  ({p.RarityLabel}, gen {fish.Generation})", RichLabel());
            GUILayout.Label($"{fish.Stage}  -  {p.Describe()}");
            if (!string.IsNullOrEmpty(fish.ParentNames)) GUILayout.Label($"Parents: {fish.ParentNames}");
            GUILayout.Label($"Hunger: {fish.Hunger * 100f:0}%   Hardiness: {p.hardiness * 100f:0}%");

            if (fish.Health.IsSick)
            {
                GUILayout.Label($"<color=#ff8080>Sick: {fish.Health.Disease.displayName} ({fish.Health.Severity * 100f:0}%)</color>", RichLabel());
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Antiviral")) fish.Health.Treat(Medicine.Antiviral);
                if (GUILayout.Button("Antibacterial")) fish.Health.Treat(Medicine.Antibacterial);
                if (GUILayout.Button("Antiparasitic")) fish.Health.Treat(Medicine.Antiparasitic);
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("Healthy");
            }
            GUILayout.Space(8f);
        }

        void DrawLog()
        {
            GUILayout.BeginArea(logPanel, GUI.skin.box);
            var entries = GameLog.Entries;
            for (int i = Mathf.Max(0, entries.Count - 8); i < entries.Count; i++)
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
            if (leftPanel.Contains(e.mousePosition) || rightPanel.Contains(e.mousePosition) || logPanel.Contains(e.mousePosition)) return;

            // IMGUI coordinates are top-left based and scaled; convert back to screen pixels.
            var guiToScreen = GUI.matrix.MultiplyPoint3x4(e.mousePosition);
            var ray = cam.ScreenPointToRay(new Vector3(guiToScreen.x, Screen.height - guiToScreen.y, 0f));
            if (Physics.Raycast(ray, out var hit, 50f))
            {
                var fish = hit.collider.GetComponentInParent<Fish>();
                if (fish != null && !fish.IsDead)
                {
                    if (selected.Contains(fish)) selected.Remove(fish);
                    else
                    {
                        if (selected.Count == 2) selected.RemoveAt(0);
                        selected.Add(fish);
                    }
                    e.Use();
                    return;
                }
            }
            selected.Clear();
            e.Use();
        }

        static GUIStyle richLabel, centeredLabel;

        static GUIStyle RichLabel()
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
