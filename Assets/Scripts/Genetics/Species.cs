using System;
using System.Collections.Generic;
using FishBreeder.Core;

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

        // Needs
        public float tempMin = 20f, tempMax = 26f;
        public float phMin = 6.5f, phMax = 7.5f;
        /// <summary>Parts per thousand: 0 is fresh water, about 35 is sea water.</summary>
        public float salinityMin, salinityMax = 2f;
        /// <summary>Days without food before it is starving.</summary>
        public float hungerToleranceDays = 3f;
        /// <summary>Flakes only half fill it; it needs premium food to thrive.</summary>
        public bool needsPremiumFood;
        public bool likesPlants = true;
        /// <summary>Feels unsafe with fewer than this many of its own kind in the tank (0 = happy alone).</summary>
        public int schoolSize;

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

        const string Carps = "Carps and minnows (Cyprinidae)";
        const string Danios = "Danios (Danionidae)";
        const string Cichlids = "Cichlids (Cichlidae)";
        const string Livebearers = "Livebearers (Poeciliidae)";

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
                if (s.id == "Oscar") s.needsPremiumFood = true;
                if (s.id == "SilverDollar" || s.id == "Oscar" || s.id == "GoldenMbuna" || s.id == "GreenTerror") s.likesPlants = false;
                if (s.id == "Betta" || s.id == "Angelfish" || s.id == "Clownfish") s.needsPremiumFood = true;
                if (s.id == "Clownfish") s.likesPlants = false;
                if (s.category == FishCategory.FreshwaterCold) { s.lifespanDays = 120f; s.daysToAdult = 5f; }
                if (s.rarity >= SpeciesRarity.Rare) s.daysToAdult = 6f;
            }
            return list;
        }
    }
}
