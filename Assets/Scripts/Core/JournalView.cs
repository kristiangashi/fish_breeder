using System.Collections.Generic;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.Shop;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The fish journal. The index shows every fish in the game, grouped by where it lives (cold
    /// fresh water, tropical fresh water, salt water): ones you've discovered show their picture and
    /// name, the rest are dark silhouettes with a question mark. Clicking a discovered fish opens
    /// its profile: what it is, and everything research has taught you about it (facts unlock as
    /// the species earns research points; the rest show as ??? with how far off they are). J or R
    /// opens and closes it; Esc closes it (handled by the store view, which owns Esc).
    /// Placeholder IMGUI like the other screens.
    /// </summary>
    public class JournalView : MonoBehaviour
    {
        static bool open;
        static string page;

        Vector2 indexScroll, pageScroll;
        Rect window;

        public static bool IsOpen => open;

        /// <summary>Opens the journal on a species' profile, or on the index with null.</summary>
        public static void Open(string speciesId)
        {
            open = true;
            page = speciesId;
            if (speciesId != null) Journal.MarkSeen(Species.Get(speciesId));
        }

        public static void Toggle()
        {
            if (open) Close();
            else Open(null);
        }

        public static void Close() => open = false;

        void Awake()
        {
            open = false;
            page = null;
        }

        void Update()
        {
            if (ViewMode.Watching || GUIUtility.keyboardControl != 0) return;
            if (Input.GetKeyDown(KeyCode.J)) Toggle();
        }

        void OnGUI()
        {
            if (!open || ViewMode.Watching) return;
            UiTheme.Apply();
            GUI.depth = -10;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            float w = Mathf.Min(900f, width - 40f);
            float h = Mathf.Min(640f, height - 60f);
            window = new Rect((width - w) * 0.5f, (height - h) * 0.5f, w, h);

            // Dim the store behind, like a book opened on the counter.
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.01f, 0.06f, 0.1f, 0.45f), 0f, 0f);

            UiCheck.Begin("Journal", window, UiStyles.Window);
            var profile = page != null ? FindSpecies(page) : null;
            if (profile != null && !Journal.IsDiscovered(profile)) profile = null;
            DrawHeader(profile);
            if (profile == null) DrawIndex(w);
            else DrawProfile(profile);
            UiCheck.End();

            // The journal is modal: clicks outside it close it instead of reaching the store behind.
            var e = Event.current;
            if (e.type == EventType.MouseDown)
            {
                if (!window.Contains(e.mousePosition)) Close();
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && window.Contains(e.mousePosition)) e.Use();
        }

        void DrawHeader(Species profile)
        {
            GUILayout.BeginHorizontal();
            if (profile != null && GUILayout.Button("< All fish", GUILayout.Width(90f))) page = null;
            GUILayout.Label("Fish journal", UiTheme.Title);
            GUILayout.Space(10f);
            GUILayout.BeginVertical();
            GUILayout.Space(6f);
            GUILayout.Label($"<b>{Journal.Count}</b> of {Species.All.Count} discovered   ·   Research level {Progression.Level(XpTrack.Research)}, speed <b>x{Research.Speed:0.0#}</b>", Rich());
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close (J / Esc)", GUILayout.Width(120f))) Close();
            GUILayout.EndHorizontal();
        }

        static Species FindSpecies(string id)
        {
            foreach (var species in Species.All)
                if (species.id == id) return species;
            return null;
        }

        // ------------------------------------------------------------ index

        void DrawIndex(float windowWidth)
        {
            GUILayout.Label("<size=12>Every kind of fish you can find. New ones join the journal the first time you can tell what they are.</size>", Rich());
            indexScroll = UiCheck.BeginScroll("Journal: index", indexScroll);
            const float cardW = 128f, cardH = 132f;
            int perRow = Mathf.Max(2, Mathf.FloorToInt((windowWidth - 50f) / (cardW + 8f)));
            foreach (var group in Journal.Groups)
            {
                var list = Journal.SpeciesIn(group);
                if (list.Count == 0) continue;
                int found = 0;
                foreach (var species in list) if (Journal.IsDiscovered(species)) found++;
                GUILayout.Space(6f);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{group.title.ToUpper()}", UiTheme.Heading, GUILayout.ExpandWidth(false));
                GUILayout.Label($"<size=11>{found}/{list.Count}  ·  {group.blurb}</size>", Rich());
                GUILayout.EndHorizontal();
                for (int i = 0; i < list.Count; i += perRow)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < Mathf.Min(list.Count, i + perRow); j++) DrawCard(list[j], cardW, cardH);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }
            UiCheck.EndScroll();
        }

        void DrawCard(Species species, float w, float h)
        {
            bool known = Journal.IsDiscovered(species);
            var rect = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
            rect.width -= 8f;
            bool hover = rect.Contains(Event.current.mousePosition);
            GUI.Box(rect, GUIContent.none, known && hover ? UiTheme.CardSelected : UiTheme.Card);

            var pictureRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 46f);
            GUI.DrawTexture(pictureRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                known ? new Color(0.06f, 0.45f, 0.55f, 0.7f) : new Color(0.01f, 0.08f, 0.12f, 0.8f), 0f, 8f);
            var photo = ItemPreviews.Fish(species);
            var old = GUI.color;
            if (photo != null)
            {
                float bob = known && hover ? Mathf.Sin(Time.unscaledTime * 4f) * 2f : 0f;
                GUI.color = known ? Color.white : new Color(0.02f, 0.05f, 0.08f, 0.95f);
                GUI.DrawTexture(new Rect(pictureRect.x + 4f, pictureRect.y + 4f + bob, pictureRect.width - 8f, pictureRect.height - 8f), photo, ScaleMode.ScaleToFit);
            }
            GUI.color = old;
            if (!known) GUI.Label(pictureRect, $"<size=40><b><color={UiTheme.AquaHex}>?</color></b></size>", Centered());

            var nameRect = new Rect(rect.x + 4f, pictureRect.yMax + 2f, rect.width - 8f, 20f);
            // Long names step down a size or two so they fit the card.
            var name = new GUIContent(known ? $"<b>{species.displayName}</b>" : "<b>???</b>");
            for (int size = 12; size >= 9 && Centered().CalcSize(name).x > nameRect.width; size--)
                name = new GUIContent($"<size={size}><b>{species.displayName}</b></size>");
            UiCheck.Label("Journal: card name", nameRect, name, Centered());
            var subRect = new Rect(rect.x + 4f, nameRect.yMax - 2f, rect.width - 8f, 16f);
            GUI.Label(subRect, known ? $"<size=10><color={RarityHex(species.rarity)}>{species.rarity}</color></size>"
                : $"<size=10><color=#8fb3bd>{(string.IsNullOrEmpty(species.onlyInBox) ? $"Fishkeeping {species.unlockLevel}+" : BoxName(species.onlyInBox))}</color></size>", Centered());

            if (known && Journal.IsNew(species))
            {
                var tagRect = new Rect(rect.xMax - 42f, rect.y + 4f, 38f, 16f);
                var tagOld = GUI.backgroundColor;
                GUI.backgroundColor = UiTheme.Coral;
                GUI.Label(tagRect, "NEW", UiTheme.Tag);
                GUI.backgroundColor = tagOld;
            }

            if (known && GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                page = species.id;
                pageScroll = Vector2.zero;
                Journal.MarkSeen(species);
            }
        }

        /// <summary>The name of the egg box a fish only comes in, e.g. "Wild tetra eggs".</summary>
        static string BoxName(string id)
        {
            foreach (var tier in EggBoxes.Tiers) if (tier != null && tier.id == id) return tier.name;
            return id;
        }

        static string RarityHex(SpeciesRarity rarity)
        {
            switch (rarity)
            {
                case SpeciesRarity.Common: return "#d6e6e8";
                case SpeciesRarity.Uncommon: return UiTheme.LeafHex;
                case SpeciesRarity.Rare: return "#7fc8ff";
                case SpeciesRarity.Exotic: return "#d39bff";
                case SpeciesRarity.Legendary: return UiTheme.SunHex;
                default: return UiTheme.CoralHex;
            }
        }

        // ------------------------------------------------------------ profile

        void DrawProfile(Species species)
        {
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();

            // Left: the fish itself.
            GUILayout.BeginVertical(GUILayout.Width(250f));
            var pictureRect = GUILayoutUtility.GetRect(250f, 170f, GUILayout.Width(250f), GUILayout.Height(170f));
            GUI.DrawTexture(pictureRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.06f, 0.45f, 0.55f, 0.75f), 0f, 14f);
            var photo = ItemPreviews.Fish(species);
            if (photo != null)
            {
                float bob = Mathf.Sin(Time.unscaledTime * 2f) * 4f;
                GUI.DrawTexture(new Rect(pictureRect.x + 8f, pictureRect.y + 8f + bob, pictureRect.width - 16f, pictureRect.height - 16f), photo, ScaleMode.ScaleToFit);
            }
            GUILayout.Label($"<size=20><b><color={UiTheme.SunHex}>{species.displayName}</color></b></size>", Rich());
            if (!string.IsNullOrEmpty(species.scientificName)) GUILayout.Label($"<i>{species.scientificName}</i>", Rich());
            if (!string.IsNullOrEmpty(species.family)) GUILayout.Label($"<size=12>{species.family}</size>", Rich());
            GUILayout.Label($"<color={RarityHex(species.rarity)}><b>{species.rarity}</b></color> species  ·  {Where(species)}", Rich());
            int kept = 0;
            foreach (var tank in FindObjectsByType<Tank>(FindObjectsSortMode.None))
                foreach (var fish in tank.Residents)
                    if (!fish.IdentityHidden && fish.Species == species) kept++;
            GUILayout.Label(kept > 0 ? $"You have <b>{kept}</b> in your tanks." : "<i>None in your tanks right now.</i>", Rich());
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            var neighbours = DiscoveredOrder();
            int index = neighbours.IndexOf(species);
            GUI.enabled = neighbours.Count > 1;
            if (GUILayout.Button("< Prev")) GoTo(neighbours[(index - 1 + neighbours.Count) % neighbours.Count]);
            if (GUILayout.Button("Next >")) GoTo(neighbours[(index + 1) % neighbours.Count]);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(12f);
            DrawFacts(species);
            GUILayout.EndHorizontal();
        }

        void GoTo(Species species)
        {
            page = species.id;
            pageScroll = Vector2.zero;
            Journal.MarkSeen(species);
        }

        static List<Species> DiscoveredOrder()
        {
            var list = new List<Species>();
            foreach (var group in Journal.Groups)
                foreach (var species in Journal.SpeciesIn(group))
                    if (Journal.IsDiscovered(species)) list.Add(species);
            return list;
        }

        static string Where(Species species)
        {
            switch (species.category)
            {
                case FishCategory.FreshwaterCold: return "cold fresh water";
                case FishCategory.SaltwaterCold: return "cold salt water";
                case FishCategory.SaltwaterTropical: return "tropical salt water";
                default: return "tropical fresh water";
            }
        }

        /// <summary>Research notes: what's been learned so far, and how far off the rest is.</summary>
        void DrawFacts(Species species)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            pageScroll = UiCheck.BeginScroll("Journal: research notes", pageScroll);
            DrawPrices(species);
            GUILayout.Space(8f);
            GUILayout.Label("RESEARCH NOTES", UiTheme.Heading);
            GUILayout.Label("<size=11>Research projects (R) teach you more about every fish you've discovered.</size>", Rich());

            Section("Temperature");
            if (Research.Knows(species, ResearchFact.Temperature))
            {
                GUILayout.Label($"Likes {species.tempMin:0}-{species.tempMax:0} C.", Rich());
                RangeGauge(true, species.tempMin, species.tempMax);
            }
            else Unknown(species, ResearchFact.Temperature);

            Section("pH");
            if (!Research.CanReadPh) Unknown(species, ResearchFact.Ph);
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
            if (foundFoods == 0) Unknown(species, ResearchFact.Temperature);
            else if (foundFoods < species.favoriteFoods.Count) GUILayout.Label("<size=11><i>There may be more.</i></size>", Rich());

            Section("Tank mates");
            int foundMates = 0;
            foreach (var other in Species.All)
            {
                if (!Research.KnowsRelationship(species, other)) continue;
                Line(Research.DescribeRelationship(species, other));
                foundMates++;
            }
            if (foundMates == 0) Unknown(species, ResearchFact.Temperature);

            UiCheck.EndScroll();
            GUILayout.EndVertical();
        }

        // ------------------------------------------------------------ prices

        /// <summary>Percent of the appraised price that "Price them all" sets.</summary>
        static int markup = 100;
        static ShopDay shopDay;

        static bool ShopOpen
        {
            get
            {
                if (shopDay == null) shopDay = FindAnyObjectByType<ShopDay>();
                return shopDay != null && shopDay.IsOpen;
            }
        }

        /// <summary>
        /// What this kind of fish is worth, and every one you own with its appraised value. Fish in
        /// a For Sale tank can be priced from here, one by one or all at once.
        /// </summary>
        void DrawPrices(Species species)
        {
            GUILayout.Label("PRICES", UiTheme.Heading);
            int adult = CrossBreeding.BaseValue(species, species, species);
            GUILayout.Label($"<size=12>A healthy one with plain looks is appraised at <b>{Economy.Format(adult)}</b> grown up, about {Economy.Format(Mathf.Max(1, Mathf.RoundToInt(adult * 0.6f)))} as a juvenile and {Economy.Format(Mathf.Max(1, Mathf.RoundToInt(adult * 0.3f)))} as fry. Uncommon to exotic looks are worth 1.5x to 4x; poor health brings it down.</size>", Rich());

            var owned = new List<KeyValuePair<Fish, StoreTank>>();
            foreach (var storeTank in FindObjectsByType<StoreTank>(FindObjectsSortMode.None))
                if (storeTank.Tank != null)
                    foreach (var fish in storeTank.Tank.Residents)
                        if (!fish.IsDead && !fish.IdentityHidden && fish.Species == species) owned.Add(new KeyValuePair<Fish, StoreTank>(fish, storeTank));
            if (owned.Count == 0) return;
            owned.Sort((a, b) => b.Key.AppraisedValue.CompareTo(a.Key.AppraisedValue));

            bool locked = ShopOpen;
            int forSale = 0;
            foreach (var pair in owned) if (pair.Value.Role == TankRole.ForSale && pair.Key.Stage != LifeStage.Fry) forSale++;
            GUI.enabled = !locked;
            if (forSale > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<size=12>Price all {forSale} for sale at</size>", Rich(), GUILayout.ExpandWidth(false));
                if (GUILayout.Button("-", GUILayout.Width(26f))) markup = Mathf.Max(10, markup - 10);
                GUILayout.Label($"<b>{markup}%</b>", Rich(), GUILayout.Width(44f));
                if (GUILayout.Button("+", GUILayout.Width(26f))) markup = Mathf.Min(300, markup + 10);
                GUILayout.Label("<size=12>of appraised</size>", Rich(), GUILayout.ExpandWidth(false));
                if (GUILayout.Button("Apply", GUILayout.Width(64f)))
                    foreach (var pair in owned)
                        if (pair.Value.Role == TankRole.ForSale && pair.Key.Stage != LifeStage.Fry)
                            pair.Key.Price = Mathf.Max(1, Mathf.RoundToInt(pair.Key.AppraisedValue * markup / 100f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            if (locked) GUILayout.Label("<size=11><i>Prices are set while the shop is closed.</i></size>", Rich());

            foreach (var pair in owned)
            {
                var fish = pair.Key;
                bool sellable = pair.Value.Role == TankRole.ForSale && fish.Stage != LifeStage.Fry;
                GUILayout.BeginHorizontal(UiTheme.Card);
                GUILayout.Label($"<b>{fish.FishName}</b>\n<size=11>{pair.Value.Title} · {fish.Stage.ToString().ToLower()}{(fish.Health.IsSick ? " · <color=#ff8080>sick</color>" : "")}</size>", Rich(), GUILayout.Width(170f));
                GUILayout.Label($"<size=11>appraised</size>\n<b>{Economy.Format(fish.AppraisedValue)}</b>", Rich(), GUILayout.Width(74f));
                if (sellable)
                {
                    string color = fish.Price <= 0 ? UiTheme.CoralHex : fish.Price > fish.AppraisedValue * 1.3f ? UiTheme.SunHex : "#ffffff";
                    GUILayout.Label($"<size=11>price</size>\n<b><color={color}>{(fish.Price > 0 ? Economy.Format(fish.Price) : "none")}</color></b>", Rich(), GUILayout.Width(64f));
                    if (GUILayout.Button("-5", GUILayout.Width(34f))) fish.Price = Mathf.Max(1, fish.Price - 5);
                    if (GUILayout.Button("+5", GUILayout.Width(34f))) fish.Price += 5;
                    if (GUILayout.Button("Appraised", GUILayout.Width(84f))) fish.Price = fish.AppraisedValue;
                }
                else GUILayout.Label(fish.Stage == LifeStage.Fry ? "<size=11><i>Too young to sell.</i></size>" : "<size=11><i>Not in a For Sale tank.</i></size>", Rich());
                GUILayout.EndHorizontal();
            }
            GUI.enabled = true;
        }

        static void Unknown(Species species, ResearchFact fact) => GUILayout.Label("<color=#8fb3bd>???</color>", Rich());

        static void RangeGauge(bool temperature, float min, float max)
        {
            // No water to point at on a journal page, so the arrow marks the middle of the liked range.
            float mid = (min + max) * 0.5f;
            if (temperature) UiGauges.Temperature(mid, new Vector2(min, max));
            else UiGauges.Ph(mid, new Vector2(min, max));
        }

        static void ProgressBar(float fill)
        {
            var rect = GUILayoutUtility.GetRect(200f, 8f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0.05f, 0.1f, 0.6f), 0f, 4f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, Mathf.Max(8f, rect.width * fill), rect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, UiTheme.Aqua, 0f, 4f);
        }

        static void Section(string title)
        {
            GUILayout.Space(4f);
            GUILayout.Label(title.ToUpper(), UiTheme.Heading);
        }

        static void Line(string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            GUILayout.Label($"• {text}", Rich());
            GUILayout.EndHorizontal();
        }

        static GUIStyle rich, centered;

        static GUIStyle Rich() => rich != null ? rich : rich = new GUIStyle(UiTheme.Skin.label) { richText = true, wordWrap = true };

        static GUIStyle Centered() => centered != null ? centered : centered = new GUIStyle(UiTheme.Skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Clip };
    }
}
