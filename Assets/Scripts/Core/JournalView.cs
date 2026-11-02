using System.Collections.Generic;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.Shop;
using FishBreeder.Store;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishBreeder.Core
{
    /// <summary>
    /// The fish journal. The index shows every fish in the game, grouped by where it lives (cold
    /// fresh water, tropical fresh water, salt water): ones you've discovered show their picture and
    /// name, the rest are dark silhouettes with a question mark. Clicking a discovered fish opens
    /// its profile: what it is, and everything research has taught you about it (facts unlock as
    /// the species earns research points; the rest show as ??? with how far off they are). J or R
    /// opens and closes it; Esc closes it (handled by the store view, which owns Esc). Drawn in the
    /// #journal-window (Window.uxml, styled in FishBreeder.uss).
    /// </summary>
    public class JournalView : MonoBehaviour
    {
        static bool open;
        static string page;

        UiWindow window;
        UiFrame ui;
        ScrollView pageScroll;

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
            window = new UiWindow(transform, "journal-window", "Fish journal", 10, true, Close);
            window.CloseButton.text = "Close (J / Esc)";
            ui = window.Frame;
        }

        void OnDestroy() => window?.Destroy();

        void OnDisable() => window?.Show(false);

        void Update()
        {
            if (!ViewMode.Watching && !UiPanels.Typing && Input.GetKeyDown(KeyCode.J)) Toggle();
            bool show = open && !ViewMode.Watching;
            window.Show(show);
            if (!show) return;
            var profile = page != null ? FindSpecies(page) : null;
            if (profile != null && !Journal.IsDiscovered(profile)) profile = null;
            window.Fill(() => DrawHeader(profile), () =>
            {
                if (profile == null) DrawIndex();
                else DrawProfile(profile);
            });
        }

        void DrawHeader(Species profile)
        {
            ui.Label($"<b>{Journal.Count}</b> of {Species.All.Count} discovered   ·   Research level {Progression.Level(XpTrack.Research)}, speed <b>x{Research.Speed:0.0#}</b>", "fb-text fb-header-text");
            ui.Flex();
            if (profile != null && ui.Button("< All fish")) page = null;
        }

        static Species FindSpecies(string id)
        {
            foreach (var species in Species.All)
                if (species.id == id) return species;
            return null;
        }

        // ------------------------------------------------------------ index

        void DrawIndex()
        {
            ui.Label("Every kind of fish you can find. New ones join the journal the first time you can tell what they are.", "fb-text fb-text--small");
            ui.BeginScroll("fb-scroll fb-window__body");
            foreach (var group in Journal.Groups)
            {
                var list = Journal.SpeciesIn(group);
                if (list.Count == 0) continue;
                int found = 0;
                foreach (var species in list) if (Journal.IsDiscovered(species)) found++;
                ui.BeginRow("fb-row fb-journal-group");
                ui.Label(group.title.ToUpper(), "fb-heading fb-header-text");
                ui.Label($"{found}/{list.Count}  ·  {group.blurb}", "fb-small fb-grow");
                ui.Pop();
                ui.BeginRow("fb-row fb-wrap fb-journal-grid");
                foreach (var species in list) DrawCard(species);
                ui.Pop();
            }
            ui.Pop();
        }

        void DrawCard(Species species)
        {
            bool known = Journal.IsDiscovered(species);
            if (ui.BeginButton(known ? "fb-journal-card" : "fb-journal-card fb-journal-card--unknown", known))
            {
                page = species.id;
                if (pageScroll != null) pageScroll.scrollOffset = Vector2.zero;
                Journal.MarkSeen(species);
            }
            ui.Begin("picture", "fb-journal-card__picture");
            var photo = ItemPreviews.Fish(species);
            var image = ui.Image(photo, 100f, 80f, "fb-image fb-journal-card__photo");
            image.tintColor = known ? Color.white : new Color(0.02f, 0.05f, 0.08f, 0.95f);
            UiPanels.Show(image, photo != null);
            if (!known) ui.Label($"<b>?</b>", "fb-journal-card__question");
            if (known && Journal.IsNew(species)) ui.Badge("NEW", UiTheme.Coral, "fb-badge fb-journal-card__new");
            ui.Pop();
            // Long names step down a size or two so they fit the card.
            string name = known ? species.displayName : "???";
            int size = name.Length > 22 ? 9 : name.Length > 19 ? 10 : name.Length > 16 ? 11 : 12;
            ui.Label($"<size={size}><b>{name}</b></size>", "fb-journal-card__name");
            ui.Label(known ? $"<color={RarityHex(species.rarity)}>{species.rarity}</color>"
                : $"<color=#8fb3bd>{(species.eastWingOnly ? "Found diving" : string.IsNullOrEmpty(species.onlyInBox) ? $"Fishkeeping {species.unlockLevel}+" : BoxName(species.onlyInBox))}</color>", "fb-journal-card__sub");
            ui.Pop();
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
            ui.BeginRow("fb-row fb-journal-profile");

            // Left: the fish itself.
            ui.BeginColumn("fb-column fb-journal-profile__left");
            ui.Begin("picture", "fb-journal-profile__picture");
            var photo = ItemPreviews.Fish(species);
            var image = ui.Image(photo, 234f, 154f, "fb-image fb-journal-profile__photo");
            image.style.translate = new Translate(0f, Mathf.Sin(Time.unscaledTime * 2f) * 4f);
            UiPanels.Show(image, photo != null);
            ui.Pop();
            ui.Label($"<size=20><b><color={UiTheme.SunHex}>{species.displayName}</color></b></size>");
            if (!string.IsNullOrEmpty(species.scientificName)) ui.Label($"<i>{species.scientificName}</i>");
            if (!string.IsNullOrEmpty(species.family)) ui.Label(species.family, "fb-text fb-text--small");
            ui.Label($"<color={RarityHex(species.rarity)}><b>{species.rarity}</b></color> species  ·  {Where(species)}");
            int kept = 0;
            foreach (var tank in FindObjectsByType<Tank>(FindObjectsSortMode.None))
                foreach (var fish in tank.Residents)
                    if (!fish.IdentityHidden && fish.Species == species) kept++;
            ui.Label(kept > 0 ? $"You have <b>{kept}</b> in your tanks." : "<i>None in your tanks right now.</i>");
            ui.Flex();
            ui.BeginRow();
            var neighbours = DiscoveredOrder();
            int index = neighbours.IndexOf(species);
            bool more = neighbours.Count > 1;
            if (ui.Button("< Prev", "fb-button fb-button--gap", more)) GoTo(neighbours[(index - 1 + neighbours.Count) % neighbours.Count]);
            if (ui.Button("Next >", "fb-button", more)) GoTo(neighbours[(index + 1) % neighbours.Count]);
            ui.Pop();
            ui.Pop();

            DrawFacts(species);
            ui.Pop();
        }

        void GoTo(Species species)
        {
            page = species.id;
            if (pageScroll != null) pageScroll.scrollOffset = Vector2.zero;
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
            pageScroll = ui.BeginScroll("fb-scroll fb-journal-profile__facts");
            DrawPrices(species);
            ui.Label("RESEARCH NOTES", "fb-heading");
            ui.Label("Research projects (R) teach you more about every fish you've discovered.", "fb-small");

            Section("Temperature");
            if (Research.Knows(species, ResearchFact.Temperature))
            {
                ui.Label($"Likes {species.tempMin:0}-{species.tempMax:0} C.");
                // No water to point at on a journal page, so the arrow marks the middle of the liked range.
                ui.Temperature((species.tempMin + species.tempMax) * 0.5f, new Vector2(species.tempMin, species.tempMax));
            }
            else Unknown();

            Section("pH");
            if (!Research.CanReadPh) Unknown();
            else
            {
                ui.Label("pH measures how acidic (below 7) or alkaline (above 7) the water is. Dirty water drifts acidic; water conditioner sets it back to neutral.", "fb-small");
                if (Research.Knows(species, ResearchFact.Ph))
                {
                    ui.Label($"Likes pH {species.phMin:0.0}-{species.phMax:0.0}.");
                    ui.Ph((species.phMin + species.phMax) * 0.5f, new Vector2(species.phMin, species.phMax));
                }
                else Unknown();
            }

            Section("Water");
            Line(species.IsSaltwater ? $"Salt water ({species.salinityMin:0}-{species.salinityMax:0} ppt)." : species.salinityMax > 5f ? $"Fresh or slightly salty water (up to {species.salinityMax:0} ppt)." : "Fresh water.");

            Section("Plants");
            if (Research.Knows(species, ResearchFact.Plants)) Line(species.likesPlants ? "Loves plants in the tank." : "Doesn't care about plants.");
            else Unknown();

            Section("Company");
            if (Research.Knows(species, ResearchFact.School)) Line(species.schoolSize > 0 ? $"Feels safe with {species.schoolSize} or more of its own kind." : "Happy on its own.");
            else Unknown();

            Section("Breeding");
            if (Research.Knows(species, ResearchFact.Breeding))
            {
                Line(species.reproduction == Reproduction.Livebearer
                    ? $"Livebearer: gives birth to {species.litterMin}-{species.litterMax} fry about {species.gestationSteps * 0.5f:0.#} days after mating."
                    : $"Egg-layer: {species.litterMin}-{species.litterMax} eggs that hatch in about {species.eggHatchSteps * 0.5f:0.#} days.");
                Line($"Grows up in about {species.daysToAdult:0.#} days.");
            }
            else Unknown();

            Section("Lifespan");
            if (Research.Knows(species, ResearchFact.Lifespan)) Line($"Starts to slow down with age after about {species.lifespanDays:0} days.");
            else Unknown();

            if (species.eatsAlgae)
            {
                // How algae eaters work: they're told apart from the start, no research needed.
                var algae = Balance.Data.algae;
                Section("Algae eater");
                Line("Lives on the algae in its tank and never eats flakes or any other food.");
                Line($"Needs at least {species.AlgaeMin * 100f:0}% algae to graze, or it goes hungry: algae wafers add {algae.waferAlgae * 100f:0}% for it. A Java moss rock keeps some algae for it too.");
                Line($"Falls sick above {species.AlgaeMax * 100f:0}% algae: too much is as bad for it as too little.");
                var covers = new List<string>();
                foreach (var size in Balance.Data.store.tankSizes)
                    if (size != null) covers.Add($"{size.algaeEaterFish} in a {size.name.ToLowerInvariant()} tank");
                Line($"Each one covers a few fish' worth of algae on top of the filter ({string.Join(", ", covers)}), and keeps the water clear for longer. While the filter and algae eaters cover every fish, algae stays at its soft cap instead of creeping past it.");
            }

            Section("Favourite foods");
            int foundFoods = 0;
            foreach (var food in species.favoriteFoods)
                if (Research.KnowsFavourite(species, food))
                {
                    Line($"Loves {FoodPellet.Name(food)}.");
                    foundFoods++;
                }
            if (foundFoods == 0) Unknown();
            else if (foundFoods < species.favoriteFoods.Count) ui.Label("<i>There may be more.</i>", "fb-small");

            Section("Tank mates");
            int foundMates = 0;
            foreach (var other in Species.All)
            {
                if (!Research.KnowsRelationship(species, other)) continue;
                Line(Research.DescribeRelationship(species, other));
                foundMates++;
            }
            if (foundMates == 0) Unknown();
            ui.Pop();
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
        /// The appraisal for this kind of fish, given once (and once more for each cross of it you
        /// own), then every one you own. Fish in a For Sale tank can be priced from here, one by one
        /// or all at once.
        /// </summary>
        void DrawPrices(Species species)
        {
            ui.Label("PRICES", "fb-heading");
            ui.Label($"Appraised at {AppraisalText(species, species, species)}. Customers judge each fish for themselves: good looks and good health sell for more.", "fb-text fb-text--small");

            var owned = new List<KeyValuePair<Fish, StoreTank>>();
            foreach (var storeTank in FindObjectsByType<StoreTank>(FindObjectsSortMode.None))
                if (storeTank.Tank != null)
                    foreach (var fish in storeTank.Tank.Residents)
                        if (!fish.IsDead && !fish.IdentityHidden && fish.Species == species) owned.Add(new KeyValuePair<Fish, StoreTank>(fish, storeTank));
            if (owned.Count == 0) return;
            // Pure ones first, then each cross together, oldest first within each.
            owned.Sort((a, b) =>
            {
                int kind = string.CompareOrdinal(KindKey(a.Key), KindKey(b.Key));
                return kind != 0 ? kind : b.Key.AgeDays.CompareTo(a.Key.AgeDays);
            });

            bool can = !ShopOpen;
            int forSale = 0;
            foreach (var pair in owned) if (pair.Value.Role == TankRole.ForSale && pair.Key.Stage != LifeStage.Fry) forSale++;
            if (forSale > 0)
            {
                ui.BeginRow("fb-row fb-wrap");
                ui.Label($"Price all {forSale} for sale at", "fb-text fb-text--small fb-header-text");
                if (ui.Button("-", "fb-button fb-step", can)) markup = Mathf.Max(10, markup - 10);
                ui.Label($"<b>{markup}%</b>", "fb-text fb-step-value fb-step-value--narrow");
                if (ui.Button("+", "fb-button fb-step", can)) markup = Mathf.Min(300, markup + 10);
                ui.Label("of appraised", "fb-text fb-text--small fb-header-text");
                if (ui.Button("Apply", "fb-button fb-button--small", can))
                    foreach (var pair in owned)
                        if (pair.Value.Role == TankRole.ForSale && pair.Key.Stage != LifeStage.Fry)
                            pair.Key.Price = Mathf.Max(1, Mathf.RoundToInt(pair.Key.AppraisedPrice * markup / 100f));
                ui.Pop();
            }
            if (!can) ui.Label("<i>Prices are set while the shop is closed.</i>", "fb-small");

            string lastKind = KindKey(owned[0].Key);
            foreach (var pair in owned)
            {
                var fish = pair.Key;
                // A cross of this fish gets its own appraisal, once, above the ones you own.
                string kindKey = KindKey(fish);
                if (kindKey != lastKind)
                {
                    lastKind = kindKey;
                    var look = fish.Phenotype;
                    ui.Label($"<b>{fish.SpeciesLabel}</b> <size=11>(fins: {look.finSpecies.displayName}, tail: {look.tailSpecies.displayName})</size>\n<size=12>Appraised at {AppraisalText(look.species, look.finSpecies, look.tailSpecies)}.</size>", "fb-text fb-cross");
                }
                bool sellable = pair.Value.Role == TankRole.ForSale && fish.Stage != LifeStage.Fry;
                ui.BeginCard(false, "fb-card");
                ui.Label($"<b>{fish.FishName}</b>\n<size=11>{pair.Value.Title} · {fish.Stage.ToString().ToLower()}{(fish.Health.IsSick ? " · <color=#ff8080>sick</color>" : "")}</size>", "fb-text fb-grow");
                if (sellable)
                {
                    string color = fish.Price <= 0 ? UiTheme.CoralHex : fish.Price > fish.AppraisedPrice * 1.3f ? UiTheme.SunHex : "#ffffff";
                    ui.Label($"<size=11>price</size>\n<b><color={color}>{(fish.Price > 0 ? Economy.Format(fish.Price) : "none")}</color></b>", "fb-text fb-step-value fb-step-value--narrow");
                    if (ui.Button("-5", "fb-button fb-step", can)) fish.Price = Mathf.Max(1, fish.Price - 5);
                    if (ui.Button("+5", "fb-button fb-step", can)) fish.Price += 5;
                    if (ui.Button("Appraised", "fb-button fb-button--small", can)) fish.Price = fish.AppraisedPrice;
                }
                else ui.Label(fish.Stage == LifeStage.Fry ? "<i>Too young to sell.</i>" : "<i>Not in a For Sale tank.</i>", "fb-small fb-value");
                ui.Pop();
            }
        }

        /// <summary>Sorts the pure fish first ("" key), then each cross by its fins and tail.</summary>
        static string KindKey(Fish fish)
        {
            var look = fish.Phenotype;
            return look.IsHybrid ? $"{look.finSpecies.id}/{look.tailSpecies.id}" : "";
        }

        /// <summary>"$40 grown up, $24 as a juvenile and $12 as fry".</summary>
        static string AppraisalText(Species body, Species fins, Species tail) =>
            $"<b>{Economy.Format(Fish.Appraise(body, fins, tail, LifeStage.Adult))}</b> grown up, {Economy.Format(Fish.Appraise(body, fins, tail, LifeStage.Juvenile))} as a juvenile and {Economy.Format(Fish.Appraise(body, fins, tail, LifeStage.Fry))} as fry";

        void Unknown() => ui.Label("<color=#8fb3bd>???</color>");

        void Section(string title) => ui.Label(title.ToUpper(), "fb-heading");

        void Line(string text) => ui.Label($"• {text}", "fb-text fb-bullet");
    }
}
