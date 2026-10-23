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

    /// <summary>Breeding groups: only fish in the same category can ever cross-breed.</summary>
    public enum FishCategory { FreshwaterCold, FreshwaterTropical, SaltwaterCold, SaltwaterTropical, BottomFeeder, Shellfish }

    /// <summary>Livebearers get pregnant and later give birth to fry; egg-layers lay eggs that hatch a few time steps later. The two never cross.</summary>
    public enum Reproduction { EggLayer, Livebearer }

    public enum SpeciesRarity { Common, Uncommon, Rare, Exotic, Legendary }

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
        /// <summary>Fishkeeping level needed to buy it (levels are not in the game yet).</summary>
        public int unlockLevel;

        // Money
        /// <summary>What a healthy, common-looking adult is worth. Fry and juveniles are worth less.</summary>
        public int baseValue = 20;
        public int clutchPrice = 20;
        public int clutchSize = 4;
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
        /// <summary>Species ids that stress it when they share a tank (fin nippers, predators, bullies).</summary>
        public string[] incompatibleWith = new string[0];

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

        public bool Dislikes(Species other)
        {
            if (other == null || incompatibleWith == null) return false;
            foreach (var id in incompatibleWith)
                if (id == other.id) return true;
            return false;
        }

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

        static Species Make(string id, string name, FishCategory category, SpeciesRarity rarity, int value, int clutchPrice, int clutchSize, int fryPrice,
            float minLength, float maxLength, float speed, float depthMin, float depthMax,
            float tempMin, float tempMax, float phMin, float phMax, float hungerDays, int school, params string[] incompatible)
        {
            return new Species
            {
                id = id, displayName = name, category = category, rarity = rarity,
                baseValue = value, clutchPrice = clutchPrice, clutchSize = clutchSize, fryPrice = fryPrice,
                minLength = minLength, maxLength = maxLength, speed = speed, depthMin = depthMin, depthMax = depthMax,
                tempMin = tempMin, tempMax = tempMax, phMin = phMin, phMax = phMax,
                hungerToleranceDays = hungerDays, schoolSize = school, incompatibleWith = incompatible,
            };
        }

        /// <summary>Defaults used when there is no balance file yet. Roughly true to the real fish.</summary>
        public static List<Species> CreateDefaults()
        {
            var list = new List<Species>
            {
                Make("CometGoldfish", "Comet Goldfish", FishCategory.FreshwaterCold, SpeciesRarity.Common, 20, 20, 4, 12, 0.1f, 0.2f, 1f, 0.15f, 0.85f, 10f, 24f, 7f, 8f, 3f, 0),
                Make("FancyGoldfish", "Fancy Goldfish", FishCategory.FreshwaterCold, SpeciesRarity.Uncommon, 45, 40, 3, 25, 0.1f, 0.14f, 0.7f, 0.15f, 0.8f, 18f, 24f, 7f, 8f, 3f, 0, "TigerBarb"),
                Make("Angelfish", "Angelfish", FishCategory.FreshwaterTropical, SpeciesRarity.Rare, 70, 60, 3, 35, 0.12f, 0.15f, 0.6f, 0.35f, 0.85f, 24f, 30f, 6f, 7.5f, 3f, 0, "TigerBarb", "NeonTetra"),
                Make("Betta", "Betta", FishCategory.FreshwaterTropical, SpeciesRarity.Rare, 55, 50, 3, 30, 0.08f, 0.1f, 0.6f, 0.45f, 0.95f, 24f, 30f, 6.5f, 7.5f, 4f, 0, "TigerBarb", "Guppy"),
                Make("Clownfish", "Clownfish", FishCategory.SaltwaterTropical, SpeciesRarity.Exotic, 110, 90, 3, 55, 0.08f, 0.1f, 0.9f, 0.25f, 0.75f, 24f, 28f, 7.8f, 8.4f, 3f, 0),
                Make("Guppy", "Guppy", FishCategory.FreshwaterTropical, SpeciesRarity.Common, 12, 15, 5, 8, 0.06f, 0.08f, 1.1f, 0.45f, 0.95f, 22f, 28f, 7f, 8f, 2f, 0, "Betta"),
                Make("TigerBarb", "Tiger Barb", FishCategory.FreshwaterTropical, SpeciesRarity.Uncommon, 25, 25, 4, 14, 0.07f, 0.09f, 1.3f, 0.2f, 0.7f, 22f, 27f, 6f, 7.5f, 2f, 4, "Angelfish", "Betta", "FancyGoldfish"),
                Make("NeonTetra", "Neon Tetra", FishCategory.FreshwaterTropical, SpeciesRarity.Common, 14, 20, 5, 10, 0.06f, 0.07f, 1.2f, 0.3f, 0.7f, 21f, 27f, 5.5f, 7f, 2f, 5, "Angelfish"),
                Make("ZebraDanio", "Zebra Danio", FishCategory.FreshwaterTropical, SpeciesRarity.Common, 12, 15, 5, 8, 0.06f, 0.07f, 1.5f, 0.6f, 0.95f, 18f, 26f, 6.5f, 7.5f, 2f, 4),
                Make("Corydoras", "Corydoras Catfish", FishCategory.BottomFeeder, SpeciesRarity.Uncommon, 35, 35, 4, 20, 0.07f, 0.08f, 1f, 0f, 0.1f, 22f, 26f, 6f, 7.5f, 3f, 3),
                Make("RedTailShark", "Red-tailed Black Shark", FishCategory.FreshwaterTropical, SpeciesRarity.Uncommon, 40, 40, 3, 22, 0.1f, 0.12f, 1.1f, 0.05f, 0.45f, 22f, 27f, 6.5f, 7.5f, 3f, 0, "SiameseAlgaeEater", "Corydoras"),
                Make("SiameseAlgaeEater", "Siamese Algae Eater", FishCategory.FreshwaterTropical, SpeciesRarity.Common, 15, 18, 4, 9, 0.1f, 0.14f, 1.3f, 0.05f, 0.6f, 23f, 27f, 6.5f, 8f, 4f, 0),
                Make("SilverDollar", "Red Hook Silver Dollar", FishCategory.FreshwaterTropical, SpeciesRarity.Uncommon, 35, 35, 4, 18, 0.12f, 0.15f, 1f, 0.3f, 0.8f, 24f, 28f, 5.5f, 7.5f, 3f, 4),
                Make("BlackMolly", "Black Molly", FishCategory.FreshwaterTropical, SpeciesRarity.Common, 14, 18, 5, 9, 0.07f, 0.09f, 1f, 0.3f, 0.9f, 22f, 28f, 7.5f, 8.5f, 2f, 0),
                Make("Platy", "Tuxedo Platy", FishCategory.FreshwaterTropical, SpeciesRarity.Common, 12, 15, 5, 8, 0.05f, 0.07f, 1.1f, 0.3f, 0.9f, 20f, 26f, 7f, 8f, 2f, 0),
                Make("Oscar", "Oscar", FishCategory.FreshwaterTropical, SpeciesRarity.Rare, 90, 80, 3, 45, 0.16f, 0.2f, 0.8f, 0.2f, 0.8f, 23f, 28f, 6f, 7.5f, 3f, 0, "Guppy", "NeonTetra", "ZebraDanio", "Platy", "BlackMolly", "Betta"),
                Make("GreenTerror", "Green Terror", FishCategory.FreshwaterTropical, SpeciesRarity.Rare, 65, 60, 3, 35, 0.12f, 0.16f, 0.9f, 0.2f, 0.7f, 22f, 26f, 6.5f, 8f, 3f, 0, "Angelfish", "NeonTetra", "Guppy", "Platy", "BlueAcara"),
                Make("BlueAcara", "Electric Blue Acara", FishCategory.FreshwaterTropical, SpeciesRarity.Rare, 60, 55, 3, 32, 0.1f, 0.13f, 0.9f, 0.2f, 0.7f, 23f, 28f, 6f, 7.5f, 3f, 0, "GreenTerror", "GoldenMbuna"),
                Make("GoldenMbuna", "Golden Mbuna", FishCategory.FreshwaterTropical, SpeciesRarity.Uncommon, 30, 30, 4, 16, 0.08f, 0.1f, 1.2f, 0.1f, 0.6f, 24f, 28f, 7.5f, 8.5f, 2f, 0, "Angelfish", "Betta", "Guppy", "BlueAcara"),
            };

            foreach (var s in list)
            {
                if (s.category == FishCategory.SaltwaterTropical) { s.salinityMin = 30f; s.salinityMax = 36f; }
                if (s.id == "Corydoras") s.style = SwimStyle.Bottom;
                if (s.id == "Guppy") { s.reproduction = Reproduction.Livebearer; s.litterMin = 3; s.litterMax = 6; }
                if (s.id == "Platy") { s.reproduction = Reproduction.Livebearer; s.litterMin = 3; s.litterMax = 6; }
                if (s.id == "BlackMolly") { s.reproduction = Reproduction.Livebearer; s.litterMin = 4; s.litterMax = 8; s.gestationSteps = 5; s.salinityMax = 10f; }
                if (s.id == "SiameseAlgaeEater") s.eatsAlgae = true;
                if (s.id == "Oscar") s.needsPremiumFood = true;
                if (s.id == "SilverDollar" || s.id == "Oscar" || s.id == "GoldenMbuna" || s.id == "GreenTerror") s.likesPlants = false;
                if (s.id == "Betta" || s.id == "Angelfish" || s.id == "Clownfish") s.needsPremiumFood = true;
                if (s.id == "Clownfish" || s.id == "Betta") s.likesPlants = false;
                if (s.category == FishCategory.FreshwaterCold) { s.lifespanDays = 120f; s.daysToAdult = 5f; }
                if (s.rarity >= SpeciesRarity.Rare) s.daysToAdult = 6f;
            }
            return list;
        }
    }
}
