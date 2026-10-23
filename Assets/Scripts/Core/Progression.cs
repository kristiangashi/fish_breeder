using System.Collections.Generic;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>The two experience tracks. Fishkeeping comes from caring for fish, Shopkeeping from running the shop.</summary>
    public enum XpTrack { Fishkeeping, Shopkeeping }

    /// <summary>Things a daily goal can ask for.</summary>
    public enum GoalKind { BreedFish, HatchEggs, FeedTanks, CleanTanks, SellFish, HappyCustomers, EarnMoney, BuyDecorations }

    /// <summary>One goal for today: do something a number of times for a big XP reward.</summary>
    public class DailyGoal
    {
        public GoalKind kind;
        public XpTrack track;
        public string text;
        public int target;
        public int progress;
        public int xp;
        public bool Done => progress >= target;
        public string Describe() => string.Format(text, target);
    }

    /// <summary>A level just reached, for the banner that announces it.</summary>
    public class LevelUp
    {
        public XpTrack track;
        public int level;
        public List<string> unlocks;
        public float age;
    }

    /// <summary>
    /// Experience, levels and daily goals. Each track has its own XP and level (1 up to the
    /// balance's max level). Almost everything in the shop is locked behind one of the two levels:
    /// Fishkeeping unlocks equipment, food, egg boxes, rarer fish and tank sizes; Shopkeeping
    /// unlocks decorations, display and for sale tanks and new parts of the store.
    /// </summary>
    public static class Progression
    {
        static readonly int[] xp = new int[2];
        static readonly int[] level = { 1, 1 };
        static readonly List<DailyGoal> goals = new List<DailyGoal>();
        static readonly List<LevelUp> levelUps = new List<LevelUp>();
        static GoalKind? lastFishGoal, lastShopGoal;

        public static IReadOnlyList<DailyGoal> Goals => goals;
        /// <summary>Recent level-ups, newest last. The store view shows and ages them.</summary>
        public static List<LevelUp> LevelUps => levelUps;

        static ProgressionBalance Data => Balance.Data.progression;

        public static int Level(XpTrack track) => level[(int)track];
        public static int Xp(XpTrack track) => xp[(int)track];
        public static int Fishkeeping => Level(XpTrack.Fishkeeping);
        public static int Shopkeeping => Level(XpTrack.Shopkeeping);

        public static void Reset()
        {
            xp[0] = xp[1] = 0;
            level[0] = level[1] = 1;
            levelUps.Clear();
            lastFishGoal = lastShopGoal = null;
            NewDailyGoals();
        }

        /// <summary>XP needed to go from <paramref name="fromLevel"/> to the next level.</summary>
        public static int XpToNext(int fromLevel) => Mathf.Max(1, Mathf.RoundToInt(Data.xpForLevel2 * Mathf.Pow(Data.xpGrowthPerLevel, fromLevel - 1)));

        public static bool IsMaxLevel(XpTrack track) => Level(track) >= Data.maxLevel;

        public static bool IsUnlocked(XpTrack track, int requiredLevel) => Level(track) >= requiredLevel;

        public static string TrackName(XpTrack track) => track.ToString();

        /// <summary>"Fishkeeping 5": the level something needs, for locked buttons.</summary>
        public static string LockText(XpTrack track, int requiredLevel) => $"{TrackName(track)} level {requiredLevel}";

        public static void Award(XpTrack track, int amount, string why = null)
        {
            if (amount <= 0) return;
            int t = (int)track;
            if (level[t] >= Data.maxLevel) return;
            xp[t] += amount;
            while (level[t] < Data.maxLevel && xp[t] >= XpToNext(level[t]))
            {
                xp[t] -= XpToNext(level[t]);
                level[t]++;
                var unlocks = UnlocksAt(track, level[t]);
                levelUps.Add(new LevelUp { track = track, level = level[t], unlocks = unlocks });
                GameLog.Add($"{TrackName(track)} level {level[t]}!" + (unlocks.Count > 0 ? $" Unlocked: {string.Join(", ", unlocks)}." : ""));
            }
            if (level[t] >= Data.maxLevel) xp[t] = 0;
        }

        /// <summary>Counts towards today's goals; finishing one awards its XP.</summary>
        public static void Count(GoalKind kind, int amount = 1)
        {
            if (amount <= 0) return;
            foreach (var goal in goals)
            {
                if (goal.kind != kind || goal.Done) continue;
                goal.progress = Mathf.Min(goal.target, goal.progress + amount);
                if (!goal.Done) continue;
                GameLog.Add($"Daily goal done: {goal.Describe()}! +{goal.xp} {TrackName(goal.track)} XP.");
                Award(goal.track, goal.xp);
            }
        }

        /// <summary>A new day: one fresh goal per track, never the same kind twice in a row.</summary>
        public static void NewDailyGoals()
        {
            goals.Clear();
            var fish = PickGoal(XpTrack.Fishkeeping, lastFishGoal);
            var shop = PickGoal(XpTrack.Shopkeeping, lastShopGoal);
            if (fish != null) { goals.Add(fish); lastFishGoal = fish.kind; }
            if (shop != null) { goals.Add(shop); lastShopGoal = shop.kind; }
        }

        static DailyGoal PickGoal(XpTrack track, GoalKind? last)
        {
            var options = new List<DailyGoalBalance>();
            foreach (var goal in Data.dailyGoals)
                if (goal != null && goal.track == track) options.Add(goal);
            if (options.Count > 1 && last.HasValue) options.RemoveAll(g => g.kind == last.Value);
            if (options.Count == 0) return null;
            var pick = options[GameRandom.Range(0, options.Count)];
            int lvl = Level(track);
            return new DailyGoal
            {
                kind = pick.kind,
                track = track,
                text = string.IsNullOrEmpty(pick.text) ? pick.kind + " x{0}" : pick.text,
                target = Mathf.Max(1, Mathf.RoundToInt(pick.baseTarget * (1f + Data.goalTargetGrowthPerLevel * (lvl - 1)))),
                xp = Mathf.RoundToInt(Data.dailyGoalXp + Data.dailyGoalXpPerLevel * lvl),
            };
        }

        /// <summary>XP for a fish bred in the shop: more for rarer species and rarer looks.</summary>
        public static int BredFishXp(int baseXp, Species species, string looks)
        {
            float multiplier = 1f + Data.xpRarityMultiplier * (species != null ? (int)species.rarity : 0);
            int bonus = looks == "Exotic" ? Data.xpExoticLooks : looks == "Rare" ? Data.xpRareLooks : looks == "Uncommon" ? Data.xpUncommonLooks : 0;
            return Mathf.RoundToInt(baseXp * multiplier) + bonus;
        }

        /// <summary>Everything that becomes available at exactly this level of this track.</summary>
        public static List<string> UnlocksAt(XpTrack track, int lvl)
        {
            var list = new List<string>();
            var balance = Balance.Data;
            var store = balance.store;
            var supplier = balance.supplier;
            if (track == XpTrack.Fishkeeping)
            {
                foreach (var tier in supplier.eggTiers) if (tier.unlockLevel == lvl) list.Add(tier.name);
                foreach (var size in store.tankSizes) if (size.unlockLevel == lvl && size.upgradePrice > 0) list.Add($"{size.name} tanks");
                foreach (var food in supplier.food) if (food.unlockLevel == lvl) list.Add(food.name);
                foreach (var tiers in new[] { supplier.filters, supplier.heaters, supplier.bubblers })
                    foreach (var model in tiers) if (model.unlockLevel == lvl) list.Add(model.name.ToLower() + "s");
                if (supplier.repairKitLevel == lvl) list.Add("repair kits");
                if (supplier.waterConditionerLevel == lvl) list.Add("water conditioner");
                if (supplier.medicineLevel == lvl) list.Add("medicine");
                if (supplier.marineSaltLevel == lvl) list.Add("salt water tanks");
                if (store.breederTankLevel == lvl) list.Add("more breeder tanks");
                if (store.quarantineTankLevel == lvl) list.Add("a quarantine tank");
                var fish = new List<string>();
                foreach (var species in balance.species) if (species.unlockLevel == lvl && lvl > 1) fish.Add(species.displayName);
                if (fish.Count > 0) list.Add("new fish in egg boxes (" + string.Join(", ", fish) + ")");
            }
            else
            {
                foreach (var decoration in supplier.decorations) if (decoration.unlockLevel == lvl) list.Add(decoration.name);
                foreach (var section in store.sections) if (section.unlockLevel == lvl && section.price > 0) list.Add($"the {section.name}");
                if (store.forSaleTankLevel == lvl) list.Add("more for sale tanks");
                if (store.displayTankLevel == lvl) list.Add("more display tanks");
            }
            return list;
        }

#if UNITY_EDITOR
        /// <summary>Developer shortcut for testing locks.</summary>
        public static void DevAward(XpTrack track, int amount) => Award(track, amount);
#endif
    }
}
