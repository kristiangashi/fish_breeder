using System;
using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Feeding;

namespace FishBreeder.Genetics
{
    public enum SwimStyle
    {
        /// <summary>Pottering around open water at its preferred depth.</summary>
        Midwater,
        /// <summary>Lives on the sand: hops, roots for food, rests on its fins, dashes up for air.</summary>
        Bottom,
    }

    /// <summary>
    /// Where the fish lives in real life: fresh or salt water, cold or tropical. Only fish in the
    /// same category can ever cross-breed. (BottomFeeder and Shellfish are kept so older balance
    /// files still load; no fish uses them now.)
    /// </summary>
    public enum FishCategory { FreshwaterCold, FreshwaterTropical, SaltwaterCold, SaltwaterTropical, BottomFeeder, Shellfish }

    /// <summary>Livebearers get pregnant and later give birth to fry; egg-layers lay eggs that hatch a few time steps later. The two never cross.</summary>
    public enum Reproduction { EggLayer, Livebearer }

    public enum SpeciesRarity { Common, Uncommon, Rare, Exotic, Legendary, Ancient }

    /// <summary>
    /// A kind of fish and all its balancing numbers. Species is fixed for life and passed from
    /// mother to fry. Genes then vary the individual within the species' ranges.
    /// The list lives in the balance file (see <see cref="Balance"/>), editable in the balance console.
    /// Small species are drawn a little larger than life so they are easy to see in the tank.
    /// </summary>
    [Serializable]
    public class Species
    {
        public string id;
        public string displayName;
        public FishCategory category;
        public Reproduction reproduction;
        public SpeciesRarity rarity;
        /// <summary>The real fish's family, e.g. "Cichlids (Cichlidae)".</summary>
        public string family = "";
        /// <summary>The real fish's Latin name.</summary>
        public string scientificName = "";
        /// <summary>Fishkeeping level from which it can hatch from the supplier's egg boxes.</summary>
        public int unlockLevel = 1;
        /// <summary>Id of the only egg/fry box this fish comes in (empty = any box whose rarity settings include it).</summary>
        public string onlyInBox = "";

        // Money
        /// <summary>What a healthy, common-looking adult is worth. Fry and juveniles are worth less.</summary>
        public int baseValue = 20;
        /// <summary>Price of one fry in the developer fry list (players buy egg boxes).</summary>
        public int fryPrice = 12;

        // Body and swimming
        /// <summary>Adult length range in metres, nose to tail tip.</summary>
        public float minLength = 0.08f, maxLength = 0.12f;
        /// <summary>Swimming speed relative to a goldfish, in body lengths per second.</summary>
        public float speed = 1f;
        /// <summary>Preferred depth band: 0 = the sand, 1 = the surface.</summary>
        public float depthMin = 0.2f, depthMax = 0.8f;
        public SwimStyle style;
        /// <summary>
        /// Where the body starts to wiggle when swimming, as a fraction of its length from the nose
        /// (0 = the whole fish snakes, 0.5 = the front half stays stiff and only the back swings).
        /// The wiggle grows over the next <see cref="BendRamp"/> of the body to full strength.
        /// </summary>
        public float bendStart = 0.2f;
        /// <summary>How big small, medium and large fish of this kind look, as a share of the species' length (medium is normally 1).</summary>
        public float sizeSmall = 0.85f, sizeMedium = 1f, sizeLarge = 1.18f;

        /// <summary>How much of the body (fraction of its length) the wiggle takes to reach full strength after <see cref="bendStart"/>.</summary>
        public const float BendRamp = 0.3f;

        // Needs
        public float tempMin = 20f, tempMax = 26f;
        public float phMin = 6.5f, phMax = 7.5f;
        /// <summary>Parts per thousand: 0 is fresh water, about 35 is sea water.</summary>
        public float salinityMin, salinityMax = 2f;
        /// <summary>Days without food before it is starving.</summary>
        public float hungerToleranceDays = 3f;
        /// <summary>Foods it loves: more filling, and a meal of one cheers it up. It eats anything, though.</summary>
        public List<FoodType> favoriteFoods = new List<FoodType>();
        public bool likesPlants = true;
        /// <summary>Feels unsafe with fewer than this many of its own kind in the tank (0 = happy alone).</summary>
        public int schoolSize;
        /// <summary>Smallest tank it will live in, as an index into the tank sizes (0 = any tank, 1 = Large, 2 = Grand).</summary>
        public int minTankSize;
        /// <summary>Decoration ids it likes to stay close to (hiding in plants, sheltering by rocks), and is a little happier for.</summary>
        public List<string> likedDecorations = new List<string>();
        /// <summary>Roams the whole tank rather than keeping near decorations: longer trips, fewer stops to nose around.</summary>
        public bool roams;

        // Life
        public float daysToAdult = 4f;
        /// <summary>After this many days old age slowly lowers health.</summary>
        public float lifespanDays = 90f;
        public int litterMin = 3, litterMax = 5;
        /// <summary>Livebearers: time steps (half days) from getting pregnant to giving birth.</summary>
        public int gestationSteps = 4;
        /// <summary>Egg-layers: time steps (half days) from laying to hatching.</summary>
        public int eggHatchSteps = 2;
        /// <summary>Grazes algae off the glass and decorations at every time step.</summary>
        public bool eatsAlgae;

        public bool IsSaltwater => category == FishCategory.SaltwaterCold || category == FishCategory.SaltwaterTropical;

        public static IReadOnlyList<Species> All => Balance.Data.species;

        public static Species CometGoldfish => Get("CometGoldfish");

        /// <summary>The species with this id; the first species for unknown or empty ids (older saves).</summary>
        public static Species Get(string id)
        {
            var all = All;
            if (!string.IsNullOrEmpty(id))
                foreach (var species in all)
                    if (species.id == id) return species;
            return all.Count > 0 ? all[0] : null;
        }

        /// <summary>The species with this id, or null.</summary>
        public static Species Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var species in All)
                if (species.id == id) return species;
            return null;
        }

        public override string ToString() => displayName;

        static Species Make(string id, string name, string scientific, string family, FishCategory category, SpeciesRarity rarity, int value, int fryPrice,
            float minLength, float maxLength, float speed, float depthMin, float depthMax,
            float tempMin, float tempMax, float phMin, float phMax, float hungerDays, int school)
        {
            return new Species
            {
                id = id, displayName = name, scientificName = scientific, family = family, category = category, rarity = rarity,
                baseValue = value, fryPrice = fryPrice,
                minLength = minLength, maxLength = maxLength, speed = speed, depthMin = depthMin, depthMax = depthMax,
                tempMin = tempMin, tempMax = tempMax, phMin = phMin, phMax = phMax,
                hungerToleranceDays = hungerDays, schoolSize = school,
            };
        }

        /// <summary>
        /// What each fish is fed in good care sheets: goldfish and big cichlids on sinking pellets
        /// and bloodworms, small community fish on flakes and brine shrimp, grazers and bottom
        /// feeders on algae wafers, mbuna on plant food only (meaty food bloats them).
        /// </summary>
        static readonly Dictionary<string, FoodType[]> Favourites = new Dictionary<string, FoodType[]>
        {
            { "CometGoldfish", new[] { FoodType.Premium, FoodType.Bloodworms } },
            { "FancyGoldfish", new[] { FoodType.Premium, FoodType.BrineShrimp } },
            { "Angelfish", new[] { FoodType.Bloodworms, FoodType.BrineShrimp } },
            { "Betta", new[] { FoodType.Bloodworms, FoodType.Premium } },
            { "Clownfish", new[] { FoodType.BrineShrimp, FoodType.Premium } },
            { "Guppy", new[] { FoodType.Flakes, FoodType.BrineShrimp } },
            { "TigerBarb", new[] { FoodType.Flakes, FoodType.Bloodworms } },
            { "NeonTetra", new[] { FoodType.Flakes, FoodType.BrineShrimp } },
            { "ZebraDanio", new[] { FoodType.Flakes, FoodType.BrineShrimp } },
            { "Corydoras", new[] { FoodType.AlgaeWafers, FoodType.Bloodworms } },
            { "RedTailShark", new[] { FoodType.AlgaeWafers, FoodType.Bloodworms } },
            { "SiameseAlgaeEater", new[] { FoodType.AlgaeWafers } },
            { "SilverDollar", new[] { FoodType.AlgaeWafers, FoodType.Flakes } },
            { "BlackMolly", new[] { FoodType.AlgaeWafers, FoodType.Flakes } },
            { "Platy", new[] { FoodType.Flakes, FoodType.AlgaeWafers } },
            { "Oscar", new[] { FoodType.Premium, FoodType.Bloodworms } },
            { "CrimsonJewelCichlid", new[] { FoodType.Premium, FoodType.Bloodworms } },
            { "SilverFlamefin", new[] { FoodType.AlgaeWafers, FoodType.Flakes } },
            { "GreenTerror", new[] { FoodType.Premium, FoodType.Bloodworms } },
            { "BlueAcara", new[] { FoodType.Bloodworms, FoodType.Premium } },
            { "GoldenMbuna", new[] { FoodType.AlgaeWafers, FoodType.Flakes } },
            { "BloodfinTetra", new[] { FoodType.Flakes, FoodType.BrineShrimp } },
            { "SerpaeTetra", new[] { FoodType.Flakes, FoodType.Bloodworms } },
            { "XRayTetra", new[] { FoodType.Flakes, FoodType.BrineShrimp } },
            { "BlackSkirtTetra", new[] { FoodType.Flakes, FoodType.Bloodworms } },
            { "RedeyeTetra", new[] { FoodType.Flakes, FoodType.Bloodworms } },
            { "LemonTetra", new[] { FoodType.Flakes, FoodType.BrineShrimp } },
            { "BleedingHeartTetra", new[] { FoodType.Bloodworms, FoodType.Flakes } },
            { "BuenosAiresTetra", new[] { FoodType.AlgaeWafers, FoodType.Flakes } },
            { "EmperorTetra", new[] { FoodType.BrineShrimp, FoodType.Flakes } },
            { "FlameTetra", new[] { FoodType.Flakes, FoodType.BrineShrimp } },
            { "CongoTetra", new[] { FoodType.Flakes, FoodType.Bloodworms } },
            { "PenguinTetra", new[] { FoodType.Flakes, FoodType.BrineShrimp } },
            { "GlowlightTetra", new[] { FoodType.Flakes, FoodType.BrineShrimp } },
            { "BucktoothTetra", new[] { FoodType.Bloodworms, FoodType.Premium } },
            { "RubyTetra", new[] { FoodType.BrineShrimp, FoodType.Flakes } },
            { "SilvertipTetra", new[] { FoodType.Flakes, FoodType.BrineShrimp } },
            { "RummyNoseTetra", new[] { FoodType.Flakes, FoodType.BrineShrimp } },
        };

        /// <summary>
        /// Fish that need more room than a standard tank: tall angelfish, big shoaling silver
        /// dollars, territorial sharks and cichlids want a Large tank, and the oscar (which grows
        /// huge and messy) a Grand one.
        /// </summary>
        /// <summary>
        /// Who keeps close to what, from how the real fish live: tetras and bettas shelter among
        /// plants, cichlids and sharks claim a rock or root, corydoras rest under driftwood. Active
        /// open-water swimmers (danios, barbs, silver dollars, goldfish) roam instead.
        /// </summary>
        static readonly Dictionary<string, string[]> LikedDecorations = new Dictionary<string, string[]>
        {
            { "Angelfish", new[] { "TallPlant", "Plant" } }, { "Betta", new[] { "Plant", "FernRock" } }, { "Clownfish", new[] { "Seaweed", "Clam" } },
            { "NeonTetra", new[] { "Plant", "TallPlant" } }, { "Corydoras", new[] { "Driftwood" } }, { "RedTailShark", new[] { "Rock", "Driftwood" } },
            { "SiameseAlgaeEater", new[] { "Driftwood", "Rock" } }, { "Platy", new[] { "Plant", "Seaweed" } }, { "Oscar", new[] { "Rock" } },
            { "GreenTerror", new[] { "Rock" } }, { "BlueAcara", new[] { "Rock", "Driftwood" } }, { "GoldenMbuna", new[] { "Rock", "FernRock" } },
            { "FancyGoldfish", new[] { "Plant" } },
            { "SerpaeTetra", new[] { "Plant", "Driftwood" } }, { "XRayTetra", new[] { "Plant" } }, { "BlackSkirtTetra", new[] { "Plant", "TallPlant" } },
            { "RedeyeTetra", new[] { "Plant" } }, { "LemonTetra", new[] { "TallPlant", "Plant" } }, { "BleedingHeartTetra", new[] { "TallPlant", "Driftwood" } },
            { "EmperorTetra", new[] { "Plant", "Driftwood" } }, { "FlameTetra", new[] { "Plant" } }, { "PenguinTetra", new[] { "TallPlant" } },
            { "GlowlightTetra", new[] { "Plant", "TallPlant" } }, { "RubyTetra", new[] { "Plant", "FernRock" } }, { "RummyNoseTetra", new[] { "Plant", "Driftwood" } },
            { "SilvertipTetra", new[] { "Plant" } }, { "CongoTetra", new[] { "TallPlant" } },
            { "CrimsonJewelCichlid", new[] { "Rock", "Driftwood" } },
        };

        static readonly HashSet<string> Roamers = new HashSet<string>
        {
            "CometGoldfish", "Guppy", "TigerBarb", "ZebraDanio", "SilverDollar", "BlackMolly", "BloodfinTetra", "BuenosAiresTetra", "BucktoothTetra",
            "SilverFlamefin",
        };

        static void SetHabits(Species s)
        {
            s.likedDecorations = LikedDecorations.TryGetValue(s.id, out var liked) ? new List<string>(liked) : new List<string>();
            s.roams = Roamers.Contains(s.id);
        }

        /// <summary>Whether it likes to stay near this decoration.</summary>
        public bool LikesDecoration(string id) => likedDecorations != null && likedDecorations.Contains(id);

        static readonly Dictionary<string, int> MinTankSizes = new Dictionary<string, int>
        {
            { "Angelfish", 1 }, { "SilverDollar", 1 }, { "RedTailShark", 1 }, { "GreenTerror", 1 }, { "BlueAcara", 1 }, { "Oscar", 2 },
            { "CongoTetra", 1 }, { "BucktoothTetra", 1 },
            { "CrimsonJewelCichlid", Tank.WallSize }, { "SilverFlamefin", Tank.WallSize },
        };

        /// <summary>
        /// Where each body starts to wiggle (see <see cref="bendStart"/>): deep, short bodies
        /// (goldfish, angelfish, silver dollars, cichlids) swim with the back half; slim fish
        /// (danios, tetras, sharks) bend from just behind the head. Others use 0.2.
        /// </summary>
        static readonly Dictionary<string, float> BendStarts = new Dictionary<string, float>
        {
            { "CometGoldfish", 0.45f }, { "FancyGoldfish", 0.55f }, { "Angelfish", 0.55f }, { "SilverDollar", 0.55f },
            { "Oscar", 0.48f }, { "GreenTerror", 0.43f }, { "BlueAcara", 0.43f }, { "Betta", 0.25f }, { "Clownfish", 0.35f },
            { "BlackMolly", 0.33f }, { "Platy", 0.33f }, { "GoldenMbuna", 0.3f }, { "Guppy", 0.23f }, { "TigerBarb", 0.3f },
            { "NeonTetra", 0.2f }, { "ZebraDanio", 0.15f }, { "Corydoras", 0.38f }, { "RedTailShark", 0.18f }, { "SiameseAlgaeEater", 0.15f },
            { "BlackSkirtTetra", 0.35f }, { "BleedingHeartTetra", 0.33f }, { "RedeyeTetra", 0.3f }, { "SerpaeTetra", 0.28f }, { "FlameTetra", 0.28f },
            { "LemonTetra", 0.28f }, { "XRayTetra", 0.23f }, { "EmperorTetra", 0.25f }, { "CongoTetra", 0.23f }, { "BucktoothTetra", 0.2f },
            { "CrimsonJewelCichlid", 0.5f }, { "SilverFlamefin", 0.35f },
        };

        /// <summary>The default <see cref="bendStart"/> for a species id.</summary>
        public static float DefaultBendStart(string id) => id != null && BendStarts.TryGetValue(id, out var b) ? b : 0.2f;

        const string Carps = "Carps and minnows (Cyprinidae)";
        const string Danios = "Danios (Danionidae)";
        const string Cichlids = "Cichlids (Cichlidae)";
        const string Livebearers = "Livebearers (Poeciliidae)";
        const string Tetras = "Tetras (Characidae)";

        /// <summary>Id of the egg box the wild tetras come in.</summary>
        public const string WildTetraBox = "WildTetra";

        /// <summary>
        /// Defaults used when there is no balance file yet. Temperatures and pH are the ranges
        /// fishkeeping care sheets give for each fish (their comfortable range, not the extremes
        /// they can survive), and the families and water types are the real fish's.
        /// </summary>
        public static List<Species> CreateDefaults()
        {
            var tropical = FishCategory.FreshwaterTropical;
            var list = new List<Species>
            {
                Make("CometGoldfish", "Comet Goldfish", "Carassius auratus", Carps, FishCategory.FreshwaterCold, SpeciesRarity.Common, 20, 12, 0.1f, 0.2f, 1f, 0.15f, 0.85f, 15f, 22f, 7f, 8f, 3f, 0),
                Make("FancyGoldfish", "Fantail Goldfish", "Carassius auratus", Carps, FishCategory.FreshwaterCold, SpeciesRarity.Uncommon, 45, 25, 0.1f, 0.14f, 0.7f, 0.15f, 0.8f, 18f, 23f, 7f, 8f, 3f, 0),
                Make("Angelfish", "Angelfish", "Pterophyllum scalare", Cichlids, tropical, SpeciesRarity.Rare, 70, 35, 0.12f, 0.15f, 0.6f, 0.35f, 0.85f, 25f, 29f, 6f, 7.5f, 3f, 0),
                Make("Betta", "Betta", "Betta splendens", "Gouramis (Osphronemidae)", tropical, SpeciesRarity.Rare, 55, 30, 0.08f, 0.1f, 0.6f, 0.45f, 0.95f, 25f, 28f, 6.5f, 7.5f, 4f, 0),
                Make("Clownfish", "Clownfish", "Amphiprion ocellaris", "Damselfish (Pomacentridae)", FishCategory.SaltwaterTropical, SpeciesRarity.Exotic, 110, 55, 0.08f, 0.1f, 0.9f, 0.25f, 0.75f, 24f, 27f, 8f, 8.4f, 3f, 0),
                Make("Guppy", "Guppy", "Poecilia reticulata", Livebearers, tropical, SpeciesRarity.Common, 12, 8, 0.06f, 0.08f, 1.1f, 0.45f, 0.95f, 23f, 27f, 7f, 8f, 2f, 0),
                Make("TigerBarb", "Tiger Barb", "Puntigrus tetrazona", Carps, tropical, SpeciesRarity.Uncommon, 25, 14, 0.07f, 0.09f, 1.3f, 0.2f, 0.7f, 23f, 27f, 6f, 7.5f, 2f, 6),
                Make("NeonTetra", "Neon Tetra", "Paracheirodon innesi", "Tetras (Characidae)", tropical, SpeciesRarity.Common, 14, 10, 0.06f, 0.07f, 1.2f, 0.3f, 0.7f, 21f, 25f, 5.5f, 7f, 2f, 6),
                Make("ZebraDanio", "Zebra Danio", "Danio rerio", Danios, tropical, SpeciesRarity.Common, 12, 8, 0.06f, 0.07f, 1.5f, 0.6f, 0.95f, 18f, 25f, 6.5f, 7.5f, 2f, 5),
                Make("Corydoras", "Corydoras Catfish", "Corydoras aeneus", "Armoured catfish (Callichthyidae)", tropical, SpeciesRarity.Uncommon, 35, 20, 0.07f, 0.08f, 1f, 0f, 0.1f, 22f, 26f, 6f, 7.5f, 3f, 5),
                Make("RedTailShark", "Red-tailed Black Shark", "Epalzeorhynchos bicolor", Carps, tropical, SpeciesRarity.Uncommon, 40, 22, 0.1f, 0.12f, 1.1f, 0.05f, 0.45f, 24f, 27f, 6.5f, 7.5f, 3f, 0),
                Make("SiameseAlgaeEater", "Siamese Algae Eater", "Crossocheilus oblongus", Carps, tropical, SpeciesRarity.Common, 15, 9, 0.1f, 0.14f, 1.3f, 0.05f, 0.6f, 24f, 27f, 6.5f, 7.5f, 4f, 0),
                Make("SilverDollar", "Red Hook Silver Dollar", "Myloplus rubripinnis", "Pacus and piranhas (Serrasalmidae)", tropical, SpeciesRarity.Uncommon, 35, 18, 0.12f, 0.15f, 1f, 0.3f, 0.8f, 24f, 28f, 5.5f, 7f, 3f, 4),
                Make("BlackMolly", "Black Molly", "Poecilia sphenops", Livebearers, tropical, SpeciesRarity.Common, 14, 9, 0.07f, 0.09f, 1f, 0.3f, 0.9f, 24f, 28f, 7.5f, 8.5f, 2f, 0),
                Make("Platy", "Tuxedo Platy", "Xiphophorus maculatus", Livebearers, tropical, SpeciesRarity.Common, 12, 8, 0.05f, 0.07f, 1.1f, 0.3f, 0.9f, 21f, 25f, 7f, 8.2f, 2f, 0),
                Make("Oscar", "Oscar", "Astronotus ocellatus", Cichlids, tropical, SpeciesRarity.Rare, 90, 45, 0.16f, 0.2f, 0.8f, 0.2f, 0.8f, 24f, 27f, 6f, 7.5f, 3f, 0),
                Make("GreenTerror", "Green Terror", "Andinoacara rivulatus", Cichlids, tropical, SpeciesRarity.Rare, 65, 35, 0.12f, 0.16f, 0.9f, 0.2f, 0.7f, 21f, 25f, 6.5f, 8f, 3f, 0),
                Make("BlueAcara", "Electric Blue Acara", "Andinoacara pulcher", Cichlids, tropical, SpeciesRarity.Rare, 60, 32, 0.1f, 0.13f, 0.9f, 0.2f, 0.7f, 23f, 27f, 6.5f, 7.5f, 3f, 0),
                Make("GoldenMbuna", "Golden Mbuna", "Melanochromis auratus", Cichlids, tropical, SpeciesRarity.Uncommon, 30, 16, 0.08f, 0.1f, 1.2f, 0.1f, 0.6f, 23f, 27f, 7.7f, 8.6f, 2f, 0),
            };

            // Fishkeeping levels: common fish from the start, the others spread over the levels
            // where their egg box tier opens, so each level or two adds a new fish.
            var nextLevel = new Dictionary<SpeciesRarity, int> { { SpeciesRarity.Common, 1 }, { SpeciesRarity.Uncommon, 2 }, { SpeciesRarity.Rare, 5 }, { SpeciesRarity.Exotic, 15 }, { SpeciesRarity.Legendary, 30 }, { SpeciesRarity.Ancient, 45 } };
            foreach (var s in list)
            {
                s.unlockLevel = nextLevel[s.rarity];
                if (s.rarity != SpeciesRarity.Common) nextLevel[s.rarity]++;
                if (s.category == FishCategory.SaltwaterTropical) { s.salinityMin = 32f; s.salinityMax = 35f; }
                if (s.id == "Corydoras") s.style = SwimStyle.Bottom;
                if (s.id == "Guppy") { s.reproduction = Reproduction.Livebearer; s.litterMin = 3; s.litterMax = 6; }
                if (s.id == "Platy") { s.reproduction = Reproduction.Livebearer; s.litterMin = 3; s.litterMax = 6; }
                if (s.id == "BlackMolly") { s.reproduction = Reproduction.Livebearer; s.litterMin = 4; s.litterMax = 8; s.gestationSteps = 5; s.salinityMax = 10f; }
                if (s.id == "SiameseAlgaeEater") s.eatsAlgae = true;
                if (s.id == "SilverDollar" || s.id == "Oscar" || s.id == "GoldenMbuna" || s.id == "GreenTerror") s.likesPlants = false;
                if (s.id == "Clownfish") s.likesPlants = false;
                if (s.category == FishCategory.FreshwaterCold) { s.lifespanDays = 120f; s.daysToAdult = 5f; }
                if (s.rarity >= SpeciesRarity.Rare) s.daysToAdult = 6f;
                if (Favourites.TryGetValue(s.id, out var foods)) s.favoriteFoods = new List<FoodType>(foods);
                if (MinTankSizes.TryGetValue(s.id, out var size)) s.minTankSize = size;
            }
            list.AddRange(WildTetras());
            list.AddRange(BigFish());
            foreach (var s in list)
            {
                s.bendStart = DefaultBendStart(s.id);
                SetHabits(s);
            }
            return list;
        }

        /// <summary>
        /// The wild tetras: they only come in wild tetra eggs, which open once you've raised enough
        /// neon tetras or tiger barbs (see the egg box's goal). Real care-sheet ranges; Congo
        /// tetras (from Africa) and the big, scale-eating bucktooth want a Large tank.
        /// </summary>
        /// <summary>
        /// The big fish (2026-10-30), about five times the size of an Oscar, so they only live in the
        /// wall aquarium. The Crimson Jewel is a flowerhorn-type cichlid (hot, hard water, bossy);
        /// the Silver Flamefin is a tinfoil-barb-type schooling carp that roams the whole tank.
        /// </summary>
        static List<Species> BigFish()
        {
            var tropical = FishCategory.FreshwaterTropical;
            var x = SpeciesRarity.Exotic;
            var list = new List<Species>
            {
                Make("CrimsonJewelCichlid", "Crimson Jewel Cichlid", "Cichlid hybrid (flowerhorn type)", Cichlids, tropical, x, 600, 300, 0.9f, 1.05f, 0.5f, 0.2f, 0.75f, 26f, 30f, 7f, 8f, 4f, 0),
                Make("SilverFlamefin", "Silver Flamefin", "Barbonymus schwanenfeldii", Carps, tropical, x, 450, 220, 0.85f, 1f, 0.6f, 0.2f, 0.8f, 22f, 26f, 6.5f, 7.5f, 4f, 5),
            };
            foreach (var s in list)
            {
                s.unlockLevel = s.id == "SilverFlamefin" ? 16 : 20;
                s.daysToAdult = 8f;
                s.litterMin = 3; s.litterMax = 6;
                if (s.id == "CrimsonJewelCichlid") s.likesPlants = false;
                if (Favourites.TryGetValue(s.id, out var foods)) s.favoriteFoods = new List<FoodType>(foods);
                if (MinTankSizes.TryGetValue(s.id, out var size)) s.minTankSize = size;
            }
            return list;
        }

        static List<Species> WildTetras()
        {
            var tropical = FishCategory.FreshwaterTropical;
            var u = SpeciesRarity.Uncommon; var r = SpeciesRarity.Rare; var x = SpeciesRarity.Exotic;
            var list = new List<Species>
            {
                Make("BloodfinTetra", "Bloodfin Tetra", "Aphyocharax anisitsi", Tetras, tropical, u, 32, 18, 0.065f, 0.08f, 1.4f, 0.45f, 0.95f, 18f, 28f, 6f, 8f, 2f, 6),
                Make("SerpaeTetra", "Serpae Tetra", "Hyphessobrycon eques", Tetras, tropical, u, 30, 16, 0.065f, 0.075f, 1.3f, 0.3f, 0.8f, 22f, 27f, 5f, 7.5f, 2f, 6),
                Make("XRayTetra", "X-Ray Tetra", "Pristella maxillaris", Tetras, tropical, u, 35, 18, 0.065f, 0.08f, 1.2f, 0.3f, 0.8f, 24f, 28f, 6f, 8f, 2f, 6),
                Make("BlackSkirtTetra", "Black Skirt Tetra", "Gymnocorymbus ternetzi", Tetras, tropical, u, 30, 16, 0.07f, 0.085f, 1f, 0.3f, 0.8f, 20f, 26f, 6f, 7.5f, 3f, 5),
                Make("RedeyeTetra", "Redeye Tetra", "Moenkhausia sanctaefilomenae", Tetras, tropical, r, 55, 28, 0.08f, 0.1f, 1.1f, 0.3f, 0.8f, 22f, 28f, 5.5f, 8.5f, 3f, 5),
                Make("LemonTetra", "Lemon Tetra", "Hyphessobrycon pulchripinnis", Tetras, tropical, u, 35, 18, 0.065f, 0.08f, 1.1f, 0.3f, 0.8f, 22f, 28f, 5.5f, 8f, 2f, 6),
                Make("BleedingHeartTetra", "Bleeding Heart Tetra", "Hyphessobrycon erythrostigma", Tetras, tropical, r, 70, 35, 0.08f, 0.1f, 0.9f, 0.3f, 0.75f, 22f, 28f, 6f, 7.5f, 3f, 5),
                Make("BuenosAiresTetra", "Buenos Aires Tetra", "Psalidodon anisitsi", Tetras, tropical, u, 30, 16, 0.08f, 0.1f, 1.3f, 0.3f, 0.85f, 18f, 28f, 5.8f, 8.5f, 3f, 6),
                Make("EmperorTetra", "Emperor Tetra", "Nematobrycon palmeri", Tetras, tropical, r, 80, 40, 0.065f, 0.08f, 1f, 0.3f, 0.8f, 23f, 27f, 5f, 7.8f, 2f, 5),
                Make("FlameTetra", "Flame Tetra", "Hyphessobrycon flammeus", Tetras, tropical, r, 60, 30, 0.055f, 0.065f, 1.2f, 0.3f, 0.8f, 22f, 28f, 6f, 7.5f, 2f, 6),
                Make("CongoTetra", "Congo Tetra", "Phenacogrammus interruptus", "African tetras (Alestidae)", tropical, x, 140, 70, 0.09f, 0.11f, 1f, 0.35f, 0.85f, 24f, 27f, 6f, 7.5f, 3f, 6),
                Make("PenguinTetra", "Penguin Tetra", "Thayeria boehlkei", Tetras, tropical, u, 35, 18, 0.07f, 0.085f, 1.2f, 0.5f, 0.95f, 22f, 28f, 5.8f, 8.5f, 2f, 6),
                Make("GlowlightTetra", "Glowlight Tetra", "Hemigrammus erythrozonus", Tetras, tropical, u, 38, 20, 0.055f, 0.065f, 1.2f, 0.3f, 0.75f, 24f, 28f, 5.5f, 7.5f, 2f, 6),
                Make("BucktoothTetra", "Bucktooth Tetra", "Exodon paradoxus", Tetras, tropical, x, 150, 75, 0.12f, 0.15f, 1.5f, 0.3f, 0.85f, 23f, 28f, 5.5f, 7.5f, 3f, 8),
                Make("RubyTetra", "Ruby Tetra", "Axelrodia riesei", Tetras, tropical, x, 160, 80, 0.045f, 0.055f, 1.1f, 0.3f, 0.75f, 23f, 27f, 5f, 6.5f, 2f, 8),
                Make("SilvertipTetra", "Silvertip Tetra", "Hasemania nana", Tetras, tropical, r, 55, 28, 0.065f, 0.075f, 1.4f, 0.3f, 0.8f, 22f, 28f, 6f, 8f, 2f, 6),
                Make("RummyNoseTetra", "Rummy Nose Tetra", "Hemigrammus rhodostomus", Tetras, tropical, r, 70, 35, 0.065f, 0.075f, 1.3f, 0.3f, 0.75f, 24f, 27f, 5.5f, 7f, 2f, 8),
            };
            foreach (var s in list)
            {
                s.onlyInBox = WildTetraBox;
                s.unlockLevel = 1;
                s.daysToAdult = s.rarity == SpeciesRarity.Exotic ? 6f : s.rarity == SpeciesRarity.Rare ? 5f : 4.5f;
                s.litterMin = 4; s.litterMax = 8;
                if (s.id == "BuenosAiresTetra" || s.id == "BucktoothTetra") s.likesPlants = false;
                if (Favourites.TryGetValue(s.id, out var foods)) s.favoriteFoods = new List<FoodType>(foods);
                if (MinTankSizes.TryGetValue(s.id, out var size)) s.minTankSize = size;
            }
            return list;
        }
    }
}
