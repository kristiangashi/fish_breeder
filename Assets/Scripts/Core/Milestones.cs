using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>What a milestone counts.</summary>
    public enum MilestoneKind
    {
        GetFish, KeepFish, DiscoverSpecies, GrowUp, FeedFish, CleanTank, HatchEggs, BreedFish, LearnFact,
        BuyEquipment, BuyDecoration, OpenShop, SellFish, EarnMoney, HappyCustomers, FishkeepingLevel,
        CureFish, RepairEquipment, TreatWater,
    }

    /// <summary>One milestone: reach <see cref="target"/> of <see cref="goal"/> for a cash (and XP) reward.</summary>
    [Serializable]
    public class MilestoneBalance
    {
        public string title;
        [Tooltip("{0} is replaced by the target.")]
        public string text;
        public MilestoneKind goal;
        public int target = 1;
        [Tooltip("Cash paid when it's reached.")]
        public int reward = 50;
        public int xp = 10;
        public XpTrack track;

        public string Describe() => string.Format(text ?? "", target);

        public static List<MilestoneBalance> CreateDefaults() => new List<MilestoneBalance>
        {
            Make("First fish", "Get your first fish", MilestoneKind.GetFish, 1, 50, 10, XpTrack.Fishkeeping),
            Make("Dinner time", "Feed a tank", MilestoneKind.FeedFish, 1, 25, 5, XpTrack.Fishkeeping),
            Make("Squeaky clean", "Wipe a tank", MilestoneKind.CleanTank, 1, 25, 5, XpTrack.Fishkeeping),
            Make("All grown up", "Raise a fish to adulthood", MilestoneKind.GrowUp, 1, 75, 15, XpTrack.Fishkeeping),
            Make("Gearing up", "Install a filter, heater or bubbler", MilestoneKind.BuyEquipment, 1, 50, 10, XpTrack.Fishkeeping),
            Make("Bright idea", "Learn something new about a fish", MilestoneKind.LearnFact, 1, 40, 10, XpTrack.Research),
            Make("Hatchlings", "Hatch some eggs", MilestoneKind.HatchEggs, 1, 60, 10, XpTrack.Fishkeeping),
            Make("Collector", "Discover {0} kinds of fish", MilestoneKind.DiscoverSpecies, 3, 100, 20, XpTrack.Research),
            Make("A full house", "Keep {0} fish at once", MilestoneKind.KeepFish, 10, 100, 20, XpTrack.Fishkeeping),
            Make("Proud parent", "Breed your own fry", MilestoneKind.BreedFish, 1, 150, 25, XpTrack.Fishkeeping),
            Make("Fishkeeper", "Reach Fishkeeping level {0}", MilestoneKind.FishkeepingLevel, 3, 100, 0, XpTrack.Fishkeeping),
            Make("Grand opening", "Open the shop for the first time", MilestoneKind.OpenShop, 1, 100, 20, XpTrack.Shopkeeping),
            Make("First sale", "Sell a fish", MilestoneKind.SellFish, 1, 75, 15, XpTrack.Shopkeeping),
            Make("Service with a smile", "Make {0} customers happy", MilestoneKind.HappyCustomers, 10, 75, 15, XpTrack.Shopkeeping),
            Make("Pretty tanks", "Buy {0} decorations", MilestoneKind.BuyDecoration, 3, 60, 10, XpTrack.Shopkeeping),
            Make("Money maker", "Earn {0} dollars from sales", MilestoneKind.EarnMoney, 500, 200, 30, XpTrack.Shopkeeping),
            Make("Naturalist", "Discover {0} kinds of fish", MilestoneKind.DiscoverSpecies, 6, 250, 40, XpTrack.Research),
        };

        static MilestoneBalance Make(string title, string text, MilestoneKind goal, int target, int reward, int xp, XpTrack track)
            => new MilestoneBalance { title = title, text = text, goal = goal, target = target, reward = reward, xp = xp, track = track };
    }

    /// <summary>
    /// Early goals that pay out the shop's first real money. Everything that counts towards them
    /// reports here (most of it through <see cref="Progression.Count"/>); each one reached pays its
    /// reward, gives its XP and gets a celebration. They're all tracked at once, and the next few
    /// unfinished ones are shown in the store.
    /// </summary>
    public static class Milestones
    {
        static readonly Dictionary<MilestoneKind, int> progress = new Dictionary<MilestoneKind, int>();
        static readonly HashSet<string> done = new HashSet<string>();

        static List<MilestoneBalance> All => Balance.Data.milestones;

        public static int DoneCount => done.Count;
        public static int Total => All.Count;

        public static void Reset()
        {
            progress.Clear();
            done.Clear();
        }

        public static int Progress(MilestoneKind kind) => progress.TryGetValue(kind, out var n) ? n : 0;

        public static bool IsDone(MilestoneBalance milestone) => milestone != null && done.Contains(milestone.title);

        /// <summary>Something that counts happened <paramref name="amount"/> times.</summary>
        public static void Add(MilestoneKind kind, int amount = 1)
        {
            if (amount <= 0) return;
            progress[kind] = Progress(kind) + amount;
            Research.OnEvent(kind, amount);
            Check();
        }

        /// <summary>A running total (fish kept, species found, level): only ever goes up.</summary>
        public static void Report(MilestoneKind kind, int value)
        {
            Research.OnTotal(kind, value);
            if (value <= Progress(kind)) return;
            progress[kind] = value;
            Check();
        }

        /// <summary>Daily-goal events count towards the matching milestones too.</summary>
        public static void FromGoal(GoalKind kind, int amount)
        {
            switch (kind)
            {
                case GoalKind.BreedFish: Add(MilestoneKind.BreedFish, amount); break;
                case GoalKind.HatchEggs: Add(MilestoneKind.HatchEggs, amount); break;
                case GoalKind.FeedTanks: Add(MilestoneKind.FeedFish, amount); break;
                case GoalKind.CleanTanks: Add(MilestoneKind.CleanTank, amount); break;
                case GoalKind.SellFish: Add(MilestoneKind.SellFish, amount); break;
                case GoalKind.HappyCustomers: Add(MilestoneKind.HappyCustomers, amount); break;
                case GoalKind.EarnMoney: Add(MilestoneKind.EarnMoney, amount); break;
                case GoalKind.BuyDecorations: Add(MilestoneKind.BuyDecoration, amount); break;
            }
        }

        /// <summary>Checks the running totals that aren't events: fish kept and Fishkeeping level.</summary>
        public static void Tick(IEnumerable<Tank> tanks)
        {
            int living = 0;
            foreach (var tank in tanks)
                if (tank != null) living += tank.Residents.Count;
            Report(MilestoneKind.KeepFish, living);
            Report(MilestoneKind.FishkeepingLevel, Progression.Fishkeeping);
        }

        /// <summary>The next unfinished milestones, in list order.</summary>
        public static List<MilestoneBalance> Upcoming(int count)
        {
            var list = new List<MilestoneBalance>();
            foreach (var milestone in All)
            {
                if (milestone == null || IsDone(milestone)) continue;
                list.Add(milestone);
                if (list.Count >= count) break;
            }
            return list;
        }

        static void Check()
        {
            foreach (var milestone in All)
            {
                if (milestone == null || IsDone(milestone) || Progress(milestone.goal) < milestone.target) continue;
                done.Add(milestone.title);
                if (milestone.reward > 0) Economy.Earn(milestone.reward, $"milestone: {milestone.title}", MoneyKind.Milestones);
                if (milestone.xp > 0) Progression.Award(milestone.track, milestone.xp);
                GameLog.Add($"Milestone reached: {milestone.title}! +{Economy.Format(milestone.reward)}" + (milestone.xp > 0 ? $" and {milestone.xp} {Progression.TrackName(milestone.track)} XP." : "."));
                Fx.Milestone(milestone);
            }
        }
    }
}
