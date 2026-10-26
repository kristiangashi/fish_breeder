using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>
    /// The supplier's egg and fry boxes. A box holds a few eggs (egg-laying fish only) or fry (any
    /// fish) of mixed species. Each egg comes from the
    /// tier's normal pool, or from its rare pool with a chance that is multiplied by the tier's
    /// falloff for every rare egg already rolled, so each extra rare fish in a box is harder to get
    /// than the one before. Only species unlocked at the player's Fishkeeping level can hatch.
    /// </summary>
    public static class EggBoxes
    {
        public static List<EggTierBalance> Tiers => Balance.Data.supplier.eggTiers;

        /// <summary>"Simple fry" for "Simple eggs": the same tier sold as fry.</summary>
        public static string FryName(EggTierBalance tier)
        {
            string name = tier.name ?? "";
            return name.EndsWith(" eggs") ? name.Substring(0, name.Length - 5) + " fry" : name + " (fry)";
        }

        /// <summary>The fish in one box, only of kinds that will live in a tank of <paramref name="tankSize"/>.</summary>
        public static List<Genome> Roll(EggTierBalance tier, int fishkeepingLevel, bool eggLayersOnly, int tankSize = int.MaxValue)
        {
            var normal = Pool(tier.normalMin, tier.normalMax, fishkeepingLevel, eggLayersOnly, tankSize);
            var rare = Pool(tier.rarePool, tier.rarePool, fishkeepingLevel, eggLayersOnly, tankSize);
            var eggs = new List<Genome>();
            int rares = 0;
            for (int i = 0; i < tier.eggs; i++)
            {
                float chance = tier.rareChance * Mathf.Pow(tier.rareFalloff, rares);
                Species species;
                if (rare.Count > 0 && (normal.Count == 0 || GameRandom.Chance(chance)))
                {
                    species = rare[GameRandom.Range(0, rare.Count)];
                    rares++;
                }
                else species = PickWeighted(normal, tier.normalMin, tier.normalRarityFalloff);
                if (species == null && normal.Count == 0 && rare.Count == 0) break;
                if (species == null) species = Species.Get(null);
                eggs.Add(Genome.CreateRandom(species));
            }
            return eggs;
        }

        /// <summary>
        /// Species of these rarities that can come in a box at this level (only egg-layers for egg
        /// boxes), for a tank of this size: the seller won't put fish in a tank too small for them.
        /// </summary>
        public static List<Species> Pool(SpeciesRarity min, SpeciesRarity max, int fishkeepingLevel, bool eggLayersOnly = false, int tankSize = int.MaxValue)
        {
            var pool = new List<Species>();
            foreach (var species in Species.All)
                if (species.rarity >= min && species.rarity <= max && species.unlockLevel <= fishkeepingLevel && species.minTankSize <= tankSize
                    && (!eggLayersOnly || species.reproduction == Reproduction.EggLayer)) pool.Add(species);
            return pool;
        }

        static Species PickWeighted(List<Species> pool, SpeciesRarity min, float falloff)
        {
            if (pool.Count == 0) return null;
            float total = 0f;
            foreach (var species in pool) total += Weight(species, min, falloff);
            float roll = GameRandom.Range(0f, total);
            foreach (var species in pool)
            {
                roll -= Weight(species, min, falloff);
                if (roll <= 0f) return species;
            }
            return pool[pool.Count - 1];
        }

        static float Weight(Species species, SpeciesRarity min, float falloff) => Mathf.Pow(Mathf.Max(0.001f, falloff), species.rarity - min);

        /// <summary>
        /// Chance of at least 1, 2, 3... rare fish in one box (index 0 = at least one), from the
        /// same rule <see cref="Roll"/> uses. Shown in the supplier so the odds are clear.
        /// </summary>
        public static float[] AtLeastRareOdds(EggTierBalance tier)
        {
            // exactly[k] = chance of exactly k rare eggs so far.
            var exactly = new float[tier.eggs + 1];
            exactly[0] = 1f;
            for (int i = 0; i < tier.eggs; i++)
            {
                var next = new float[tier.eggs + 1];
                for (int k = 0; k <= i; k++)
                {
                    float chance = Mathf.Clamp01(tier.rareChance * Mathf.Pow(tier.rareFalloff, k));
                    next[k] += exactly[k] * (1f - chance);
                    next[k + 1] += exactly[k] * chance;
                }
                exactly = next;
            }
            var atLeast = new float[tier.eggs];
            for (int k = tier.eggs; k >= 1; k--)
                atLeast[k - 1] = exactly[k] + (k < tier.eggs ? atLeast[k] : 0f);
            return atLeast;
        }
    }
}
