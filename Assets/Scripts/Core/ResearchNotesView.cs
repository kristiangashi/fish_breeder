using System.Collections.Generic;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The research notebook: every species you have kept on the left, and on the right a page of
    /// what you have learned about the picked one. Facts unlock as the species earns research points
    /// (time spent in your tanks times your research speed); the rest show as ??? with how far off
    /// they are. Favourite foods and how species get on are found by feeding and by keeping adults
    /// together. R opens and closes it; Esc closes it (handled by the store view, which owns Esc).
    /// Placeholder IMGUI like the other screens.
    /// </summary>
    public class ResearchNotesView : MonoBehaviour
    {
        static ResearchNotesView instance;
        static bool open;
        static string page;

        Vector2 listScroll, pageScroll;
        Rect window;

        public static bool IsOpen => open;

        public static void Open(string speciesId)
        {
            open = true;
            if (!string.IsNullOrEmpty(speciesId)) page = speciesId;
        }

        public static void Toggle()
        {
            if (open) Close();
            else Open(null);
        }

        public static void Close() => open = false;

        void Awake()
        {
            instance = this;
            open = false;
            page = null;
        }

        void OnDestroy()
        {
            if (instance == this) open = false;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.R) && GUIUtility.keyboardControl == 0) Toggle();
        }

        void OnGUI()
        {
            if (!open) return;
            GUI.depth = -10;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            float w = Mathf.Min(780f, width - 40f);
            float h = Mathf.Min(600f, height - 60f);
            window = new Rect((width - w) * 0.5f, (height - h) * 0.5f, w, h);

            var studied = Research.Studied;
            if (page == null && studied.Count > 0) page = studied[0];

            GUILayout.BeginArea(window, UiStyles.Window);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<size=18><b>Research notes</b></size>   Research level {Progression.Level(XpTrack.Research)}, speed <b>x{Research.Speed:0.0#}</b>", Rich());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close (R / Esc)", GUILayout.Width(120f))) Close();
            GUILayout.EndHorizontal();
            GUILayout.Label("<size=11>Every fish you keep teaches you about its species at each time step. A higher Research level learns faster.</size>", Rich());

            GUILayout.BeginHorizontal();
            DrawList(studied);
            GUILayout.Space(8f);
            DrawPage(page != null ? FindStudied(page) : null);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // The notebook is modal: clicks outside it close it instead of reaching the store behind.
            var e = Event.current;
            if (e.type == EventType.MouseDown)
            {
                if (!window.Contains(e.mousePosition)) Close();
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && window.Contains(e.mousePosition)) e.Use();
        }

        static Species FindStudied(string id)
        {
            foreach (var species in Species.All)
                if (species.id == id) return species;
            return null;
        }

        void DrawList(IReadOnlyList<string> studied)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(210f));
            listScroll = GUILayout.BeginScrollView(listScroll);
            if (studied.Count == 0) GUILayout.Label("<i>Nothing yet. Keep some fish and open and close the shop.</i>", Rich());
            foreach (var id in studied)
            {
                var species = FindStudied(id);
                if (species == null) continue;
                int known = KnownFacts(species), total = Balance.Data.research.facts.Count;
                string label = (id == page ? "<b>" : "") + species.displayName + (id == page ? "</b>" : "") + $"\n<size=10>{known}/{total} facts, {Research.Points(species):0} points</size>";
                if (GUILayout.Button(label, ListButton())) page = id;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        static int KnownFacts(Species species)
        {
            int n = 0;
            foreach (var entry in Balance.Data.research.facts)
                if (entry != null && Research.Knows(species, entry.fact)) n++;
            return n;
        }

        void DrawPage(Species species)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            pageScroll = GUILayout.BeginScrollView(pageScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            if (species == null)
            {
                GUILayout.Label("Pick a species on the left.");
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label($"<size=17><b>{species.displayName}</b></size>", Rich());
            if (!string.IsNullOrEmpty(species.scientificName)) GUILayout.Label($"<i>{species.scientificName}</i>, {species.family}", Rich());
            GUILayout.Label($"{species.rarity} species, {(species.IsSaltwater ? "salt water" : "fresh water")}.", Rich());
            float points = Research.Points(species);
            var next = Research.NextFact(species);
            GUILayout.Label($"Research points: <b>{points:0.#}</b>" + (next.HasValue
                ? $"   Next: {Research.FactName(next.Value)} at {Research.PointsNeeded(next.Value):0}"
                : "   Everything known."), Rich());
            if (next.HasValue) ProgressBar(Mathf.Clamp01(points / Mathf.Max(1f, Research.PointsNeeded(next.Value))));

            Section("Temperature");
            if (Research.Knows(species, ResearchFact.Temperature))
            {
                GUILayout.Label($"Likes {species.tempMin:0}-{species.tempMax:0} C.", Rich());
                RangeGauge(true, species.tempMin, species.tempMax);
            }
            else Unknown(species, ResearchFact.Temperature);

            Section("pH");
            if (!Research.CanReadPh)
                GUILayout.Label($"<i>You can't measure pH yet. Reach Research level {Balance.Data.research.phReadingLevel} to learn what it is and read it.</i>", Rich());
            else
            {
                GUILayout.Label("<size=11>pH measures how acidic (below 7) or alkaline (above 7) the water is. Dirty water drifts acidic; water conditioner sets it back to neutral.</size>", Rich());
                if (Research.Knows(species, ResearchFact.Ph))
                {
                    GUILayout.Label($"Likes pH {species.phMin:0.0}-{species.phMax:0.0}.", Rich());
                    RangeGauge(false, species.phMin, species.phMax);
                }
                else Unknown(species, ResearchFact.Ph);
            }

            Section("Water");
            Line(species.IsSaltwater ? $"Salt water ({species.salinityMin:0}-{species.salinityMax:0} ppt)." : species.salinityMax > 5f ? $"Fresh or slightly salty water (up to {species.salinityMax:0} ppt)." : "Fresh water.");

            Section("Plants");
            if (Research.Knows(species, ResearchFact.Plants)) Line(species.likesPlants ? "Loves plants in the tank." : "Doesn't care about plants.");
            else Unknown(species, ResearchFact.Plants);

            Section("Company");
            if (Research.Knows(species, ResearchFact.School)) Line(species.schoolSize > 0 ? $"Feels safe with {species.schoolSize} or more of its own kind." : "Happy on its own.");
            else Unknown(species, ResearchFact.School);

            Section("Breeding");
            if (Research.Knows(species, ResearchFact.Breeding))
            {
                Line(species.reproduction == Reproduction.Livebearer
                    ? $"Livebearer: gives birth to {species.litterMin}-{species.litterMax} fry about {species.gestationSteps * 0.5f:0.#} days after mating."
                    : $"Egg-layer: {species.litterMin}-{species.litterMax} eggs that hatch in about {species.eggHatchSteps * 0.5f:0.#} days.");
                Line($"Grows up in about {species.daysToAdult:0.#} days.");
            }
            else Unknown(species, ResearchFact.Breeding);

            Section("Lifespan");
            if (Research.Knows(species, ResearchFact.Lifespan)) Line($"Starts to slow down with age after about {species.lifespanDays:0} days.");
            else Unknown(species, ResearchFact.Lifespan);

            Section("Favourite foods");
            int foundFoods = 0;
            foreach (var food in species.favoriteFoods)
                if (Research.KnowsFavourite(species, food))
                {
                    Line($"Loves {FoodPellet.Name(food)}.");
                    foundFoods++;
                }
            if (foundFoods < species.favoriteFoods.Count)
                GUILayout.Label("<i>" + (foundFoods == 0 ? "Unknown. " : "There may be more. ") + "Feed it different foods: a fish enjoying a favourite may give it away. It eats any food, though.</i>", Rich());

            Section("Tank mates");
            int foundMates = 0;
            foreach (var other in Species.All)
            {
                if (!Research.KnowsRelationship(species, other)) continue;
                Line(Research.DescribeRelationship(species, other));
                foundMates++;
            }
            if (foundMates == 0) GUILayout.Label("<i>Unknown. Grow two kinds of fish to adulthood in the same tank and you may see how they get on.</i>", Rich());
            else GUILayout.Label("<size=11><i>Keep adults together to discover more.</i></size>", Rich());

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        static void Unknown(Species species, ResearchFact fact)
        {
            float needed = Research.PointsNeeded(fact);
            float have = Research.Points(species);
            string why = have >= needed ? "" : $" ({needed - have:0.#} more research points)";
            GUILayout.Label($"<color=#8f96a0>???{why}</color>", Rich());
        }

        static void RangeGauge(bool temperature, float min, float max)
        {
            // No water to point at on a notebook page, so the arrow marks the middle of the liked range.
            float mid = (min + max) * 0.5f;
            if (temperature) UiGauges.Temperature(mid, new Vector2(min, max));
            else UiGauges.Ph(mid, new Vector2(min, max));
        }

        static void ProgressBar(float fill)
        {
            var rect = GUILayoutUtility.GetRect(200f, 8f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.4f), 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * fill, rect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.6f, 0.45f, 0.9f), 0f, 0f);
        }

        static void Section(string title)
        {
            GUILayout.Space(6f);
            GUILayout.Label($"<b><color=#c3b0f0>{title.ToUpper()}</color></b>", Rich());
        }

        static void Line(string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            GUILayout.Label($"• {text}", Rich());
            GUILayout.EndHorizontal();
        }

        static GUIStyle rich, listButton;

        static GUIStyle Rich() => rich != null ? rich : rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };

        static GUIStyle ListButton() => listButton != null ? listButton : listButton = new GUIStyle(GUI.skin.button) { richText = true, alignment = TextAnchor.MiddleLeft, wordWrap = true };
    }
}
