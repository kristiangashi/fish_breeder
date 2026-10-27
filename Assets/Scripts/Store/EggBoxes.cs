using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>
    /// The supplier's egg and fry boxes. A box holds a few eggs (egg-laying fish only) or fry (any
    /// fish) of mixed species. Each tier has a hatch table (set in the balance console's Eggs tab):
    /// every fish it can hatch, with a relative chance and the Fishkeeping level and day from which
    /// it can turn up. Each egg picks from the fish available right now by those chances; fish of
    /// the tier's rare rarity or rarer get less likely for every rare one already in the box
    /// (the tier's rare falloff), so each extra rare fish in a box is harder to get than the one
    /// before. The seller only packs fish that will live in the tank the box is for.
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

        /// <summary>The day used for "from day" in hatch tables (dev mode opens everything).</summary>
        static int Today => DevMode.On ? int.MaxValue : Economy.Day;

        public static bool IsRare(EggTierBalance tier, Species species) => species.rarity >= tier.rarePool;

        static readonly Dictionary<string, int> raised = new Dictionary<string, int>();

        public static void Reset() => raised.Clear();

        /// <summary>How many fish of this species the player has raised to adulthood this game.</summary>
        public static int Raised(string speciesId) => speciesId != null && raised.TryGetValue(speciesId, out var n) ? n : 0;

        /// <summary>The species a box's goal counts ("raise N of these to adulthood").</summary>
        public static List<Species> GoalSpecies(EggTierBalance tier)
        {
            var list = new List<Species>();
            if (tier == null || string.IsNullOrEmpty(tier.raiseSpecies)) return list;
            foreach (var id in tier.raiseSpecies.Split(','))
            {
                var species = Species.Find(id.Trim());
                if (species != null) list.Add(species);
            }
            return list;
        }

        public static bool HasGoal(EggTierBalance tier) => tier != null && tier.raiseCount > 0 && GoalSpecies(tier).Count > 0;

        /// <summary>Fish raised so far towards a box's goal.</summary>
        public static int GoalProgress(EggTierBalance tier)
        {
            int n = 0;
            foreach (var species in GoalSpecies(tier)) n += Raised(species.id);
            return n;
        }

        /// <summary>True when the box has no goal, or its goal is reached (dev mode opens everything).</summary>
        public static bool GoalMet(EggTierBalance tier) => !HasGoal(tier) || DevMode.On || GoalProgress(tier) >= tier.raiseCount;

        /// <summary>"Raise 50 Neon Tetras or Tiger Barbs to adulthood (12/50)".</summary>
        public static string GoalText(EggTierBalance tier)
        {
            var names = new List<string>();
            foreach (var species in GoalSpecies(tier)) names.Add(species.displayName + "s");
            string who = names.Count <= 1 ? string.Join("", names) : string.Join(", ", names.GetRange(0, names.Count - 1)) + " or " + names[names.Count - 1];
            return $"Raise {tier.raiseCount} {who} to adulthood ({Mathf.Min(GoalProgress(tier), tier.raiseCount)}/{tier.raiseCount})";
        }

        /// <summary>A fish grew up: counts towards box goals, and announces a box that just opened.</summary>
        public static void RecordGrownUp(Species species)
        {
            if (species == null) return;
            var before = new List<EggTierBalance>();
            foreach (var tier in Tiers) if (HasGoal(tier) && GoalProgress(tier) < tier.raiseCount) before.Add(tier);
            raised[species.id] = Raised(species.id) + 1;
            foreach (var tier in before)
                if (GoalProgress(tier) >= tier.raiseCount)
                {
                    GameLog.Add($"You've raised {tier.raiseCount} fish for the fish seller's goal: {tier.name.ToLower()} are now at his van!");
                    Fx.Card("NEW EGG BOX", tier.name, tier.blurb, "At the fish seller's van", UiTheme.Aqua);
                }
        }

        /// <summary>
        /// Whether the player can get this fish at all right now: false for fish that only come in
        /// a box whose goal isn't reached yet (so shoppers don't ask for them).
        /// </summary>
        public static bool Obtainable(Species species)
        {
            if (species == null || string.IsNullOrEmpty(species.onlyInBox)) return true;
            foreach (var tier in Tiers) if (tier != null && tier.id == species.onlyInBox) return GoalMet(tier);
            return false;
        }

        /// <summary>One fish a box can hatch right now, with its chance weight.</summary>
        public struct Entry
        {
            public Species species;
            public float weight;
            public bool rare;
        }

        /// <summary>
        /// What a box of this tier can hatch at this level and day: egg-layers only for egg boxes,
        /// and only fish that will live in a tank of <paramref name="tankSize"/>.
        /// </summary>
        public static List<Entry> Available(EggTierBalance tier, int fishkeepingLevel, bool eggLayersOnly, int tankSize = int.MaxValue)
        {
            var list = new List<Entry>();
            if (tier.hatch == null) return list;
            int day = Today;
            foreach (var row in tier.hatch)
            {
                if (row == null || row.weight <= 0f || row.fromLevel > fishkeepingLevel || row.fromDay > day) continue;
                var species = Species.Find(row.species);
                if (species == null || species.minTankSize > tankSize || (eggLayersOnly && species.reproduction != Reproduction.EggLayer)) continue;
                list.Add(new Entry { species = species, weight = row.weight, rare = IsRare(tier, species) });
            }
            return list;
        }

        /// <summary>The species in <see cref="Available"/>, the rare ones or the others.</summary>
        public static List<Species> Pool(EggTierBalance tier, bool rare, int fishkeepingLevel, bool eggLayersOnly, int tankSize = int.MaxValue)
        {
            var pool = new List<Species>();
            foreach (var entry in Available(tier, fishkeepingLevel, eggLayersOnly, tankSize))
                if (entry.rare == rare) pool.Add(entry.species);
            return pool;
        }

        /// <summary>The fish in one box, only of kinds that will live in a tank of <paramref name="tankSize"/>.</summary>
        public static List<Genome> Roll(EggTierBalance tier, int fishkeepingLevel, bool eggLayersOnly, int tankSize = int.MaxValue)
        {
            var entries = Available(tier, fishkeepingLevel, eggLayersOnly, tankSize);
            var eggs = new List<Genome>();
            if (entries.Count == 0) return eggs;
            int rares = 0;
            for (int i = 0; i < tier.eggs; i++)
            {
                float rareFactor = Mathf.Pow(Mathf.Clamp01(tier.rareFalloff), rares);
                float total = 0f;
                foreach (var e in entries) total += e.weight * (e.rare ? rareFactor : 1f);
                if (total <= 0f) break;
                float roll = GameRandom.Range(0f, total);
                var pick = entries[entries.Count - 1];
                foreach (var e in entries)
                {
                    roll -= e.weight * (e.rare ? rareFactor : 1f);
                    if (roll <= 0f) { pick = e; break; }
                }
                if (pick.rare) rares++;
                eggs.Add(Genome.CreateRandom(pick.species));
            }
            return eggs;
        }

        /// <summary>Chance one egg hatches into each species right now, before any rare fish is in the box (sums to 1).</summary>
        public static Dictionary<Species, float> FirstEggOdds(EggTierBalance tier, int fishkeepingLevel, bool eggLayersOnly, int tankSize = int.MaxValue)
        {
            var odds = new Dictionary<Species, float>();
            var entries = Available(tier, fishkeepingLevel, eggLayersOnly, tankSize);
            float total = 0f;
            foreach (var e in entries) total += e.weight;
            if (total > 0f) foreach (var e in entries) odds[e.species] = e.weight / total;
            return odds;
        }

        /// <summary>
        /// Chance of at least 1, 2, 3... rare fish in one box (index 0 = at least one), from the
        /// same rule <see cref="Roll"/> uses. Shown in the supplier so the odds are clear.
        /// </summary>
        public static float[] AtLeastRareOdds(EggTierBalance tier, int fishkeepingLevel, bool eggLayersOnly, int tankSize = int.MaxValue)
        {
            float normal = 0f, rare = 0f;
            foreach (var e in Available(tier, fishkeepingLevel, eggLayersOnly, tankSize))
                if (e.rare) rare += e.weight;
                else normal += e.weight;
            // exactly[k] = chance of exactly k rare eggs so far.
            var exactly = new float[tier.eggs + 1];
            exactly[0] = 1f;
            for (int i = 0; i < tier.eggs; i++)
            {
                var next = new float[tier.eggs + 1];
                for (int k = 0; k <= i; k++)
                {
                    float r = rare * Mathf.Pow(Mathf.Clamp01(tier.rareFalloff), k);
                    float chance = r + normal > 0f ? r / (r + normal) : 0f;
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

        /// <summary>When the first rare fish of this box can hatch, for boxes that can't hatch one yet.</summary>
        public static string RareUnlockText(EggTierBalance tier, bool eggLayersOnly)
        {
            int level = int.MaxValue, day = int.MaxValue;
            if (tier.hatch != null)
                foreach (var row in tier.hatch)
                {
                    var species = row != null && row.weight > 0f ? Species.Find(row.species) : null;
                    if (species == null || !IsRare(tier, species) || (eggLayersOnly && species.reproduction != Reproduction.EggLayer)) continue;
                    if (row.fromLevel < level || (row.fromLevel == level && row.fromDay < day)) { level = row.fromLevel; day = row.fromDay; }
                }
            if (level == int.MaxValue) return "none in this box";
            return day > Economy.Day ? $"from Fishkeeping level {level}, day {day}" : $"from Fishkeeping level {level}";
        }
    }
}
