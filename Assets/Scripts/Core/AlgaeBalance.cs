using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The four moods a tank's algae can be in, from none to an algal bloom. The player only
    /// ever sees the stage, never a number.
    /// </summary>
    public enum AlgaeStage { Clear, HealthyGreen, Overgrown, Bloom }

    /// <summary>
    /// How algae works. Every tank has an algae load (what feeds algae: its fish, feedings, plants
    /// and decorations, and a little on its own) and an algae capacity (what holds algae back: the
    /// tank itself, its filter and its algae eaters), both counted in tiny fish. Load over capacity
    /// picks the stage the tank is heading for, and at each time step the tank moves one stage
    /// towards it (or stays). Healthy green is good for fish; overgrown harms them; a bloom harms
    /// them badly and only a deep clean ends it. A clean takes the tank down a stage.
    /// </summary>
    [Serializable]
    public class AlgaeBalance
    {
        [Header("Algae load: what feeds algae (in tiny fish)")]
        [Tooltip("Load every tank has on its own, even empty, in the standard tank (bigger tanks get more, by their fish limit). The game never says why.")]
        public float naturalLoad = 2f;
        [Tooltip("How many tiny fish each fish of a size group counts as, for algae and filters (tiny, small, medium, large). Algae eaters don't count. In the public aquarium's exhibits and holding tanks every fish counts as one.")]
        public float tinyFishLoad = 1f, smallFishLoad = 2f, mediumFishLoad = 3f, largeFishLoad = 6f;
        [Tooltip("Load from one feeding a day (not algae wafers): it counts half at each of the next two time steps, so one feeding a day adds this all the time and three feedings add three times as much.")]
        public float feedingLoad = 2f;
        [Tooltip("Load from one feeding of algae wafers, counted the same way.")]
        public float waferLoad = 1f;
        [Tooltip("Load from each decoration that isn't a plant (too many decorations mean more algae). Plants have their own number (Algae load, Eggs, decor & quality tab).")]
        public float decorationLoad = 0.5f;

        [Header("Algae capacity: what holds it back (in tiny fish)")]
        [Tooltip("Capacity of the standard tank on its own, with no filter or algae eaters (bigger tanks get more, by their fish limit).")]
        public float tankCapacity = 6f;
        [Tooltip("A working filter adds its Fish it handles (Equipment & repairs tab, with the Steady filters perk) times this.")]
        public float filterCapacityShare = 0.75f;
        [Tooltip("Capacity each algae eater adds (whatever the tank's size). A tank of only algae eaters ends up clear, so they need wafers.")]
        public float eaterCapacity = 4f;
        [Tooltip("Capacity added for every Fishkeeping level past 1 (0 = none: only gear and eaters help).")]
        public float capacityPerFishkeepingLevel;

        [Header("Stages: load divided by capacity")]
        [Tooltip("Below this the tank heads for Clear; from here it heads for Healthy green...")]
        public float healthyFrom = 0.25f;
        [Tooltip("...from here for Overgrown...")]
        public float overgrownFrom = 1f;
        [Tooltip("...and from here for an algal bloom. A tank moves one stage a time step, so it is overgrown for at least one time step first.")]
        public float bloomFrom = 2f;

        [Header("Algae and fish health (not algae eaters)")]
        [Tooltip("Health a fish gains while its tank is Healthy green (0.05 = +5).")]
        public float healthyAlgaeBonus = 0.05f;
        [Tooltip("Health a fish loses while its tank is Overgrown (0.08 = -8).")]
        public float overgrownHealthLoss = 0.08f;
        [Tooltip("Health a fish loses during an algal bloom (on top of the bloom's stress).")]
        public float bloomHealthLoss = 0.16f;

        [Header("How it looks and what customers think")]
        [Tooltip("How much of the glass algae covers at each stage, growing in from the edges and corners (Clear, Healthy green, Overgrown, Bloom). Looks only.")]
        [Range(0f, 1f)] public float clearGlass = 0f, healthyGlass = 0.12f, overgrownGlass = 0.45f, bloomGlass = 0.88f;
        [Tooltip("Chance a customer looking at an overgrown tank is put off by the algae, and at a tank in bloom.")]
        [Range(0f, 1f)] public float overgrownBotherChance = 0.35f, bloomBotherChance = 0.85f;
        [Tooltip("How much the algae spoils the look of the water for customers and the public aquarium (0..1), overgrown and in bloom.")]
        [Range(0f, 1f)] public float overgrownNuisance = 0.15f, bloomNuisance = 0.85f;

        [Header("Algal bloom")]
        [Tooltip("Chance each fish dies right away when a bloom starts, and the chance a survivor falls ill.")]
        [Range(0f, 1f)] public float bloomDeathChance = 0.12f, bloomIllnessChance = 0.5f;
        [Tooltip("How badly a bloom's choked water stresses every fish while it lasts (environment, 0..1). Only a deep clean ends it.")]
        [Range(0f, 1f)] public float bloomStress = 0.5f;

        [Header("Clean tank")]
        [Tooltip("Cleans each tank gets per closed-shop session (more with the Tireless perk).")]
        public int cleansPerSession = 2;
        [Tooltip("One more clean per session every this many Fishkeeping levels (0 = never).")]
        public int extraCleanEveryFishkeepingLevels;
        [Tooltip("Stages one clean takes the algae down (no use against a bloom).")]
        public int cleanStages = 1;
        [Tooltip("A clean never takes the algae below Healthy green, so cleaning for the water doesn't scrub off the algae that's good for fish (a Clear tank stays Clear).")]
        public bool cleanLeavesHealthy = true;
        [Tooltip("Water quality one clean gives back (more with the Elbow grease perk).")]
        public float cleanQualityRestored = 0.2f;
        [Tooltip("Each Fishkeeping level makes a clean give back this much more water quality (0 = the same at every level, so the filter, not the level, decides how many fish a tank can keep).")]
        public float cleanQualityBonusPerFishkeepingLevel;

        [Header("Deep clean")]
        [Tooltip("Deep cleans each tank gets per closed-shop session.")]
        public int deepCleansPerSession = 1;
        public int deepCleanPrice;
        [Tooltip("A deep clean takes the algae to Clear (it ends a bloom too) and gives the water back this much quality.")]
        public float deepCleanQualityRestored = 0.5f;
        [Tooltip("Chance each fish gets hurt in a deep clean at Fishkeeping level 1, how much less per level, and the least it gets to.")]
        [Range(0f, 1f)] public float deepCleanInjuryChance = 0.3f;
        public float deepCleanInjuryLessPerLevel = 0.004f;
        [Range(0f, 1f)] public float deepCleanMinInjuryChance = 0.08f;
        [Tooltip("What can happen to a fish in a deep clean: one is picked by weight. Amount is how much it hurts (0..1, wounds heal a little every time step).")]
        public List<InjuryBalance> deepCleanInjuries = InjuryBalance.DeepCleanDefaults();

        [Header("Algae eaters")]
        [Tooltip("Algae eaters graze when the tank is Healthy green or Overgrown, or had algae wafers in the last two time steps; a Clear tank leaves them hungry. In a bloom they get sick: illness chance times this, and this much stress (environment).")]
        public float eaterBloomIllness = 3f, eaterBloomStress = 0.5f;

        /// <summary>How many tiny fish one fish of this size group counts as.</summary>
        public float LoadOf(Genetics.SizeGroup group)
        {
            switch (group)
            {
                case Genetics.SizeGroup.Small: return smallFishLoad;
                case Genetics.SizeGroup.Medium: return mediumFishLoad;
                case Genetics.SizeGroup.Large: return largeFishLoad;
                default: return tinyFishLoad;
            }
        }
    }

    /// <summary>One kind of injury a fish can get, with how much it hurts and how often it comes up.</summary>
    [Serializable]
    public class InjuryBalance
    {
        [Tooltip("What happened, as the game says it (\"a torn fin from the net\").")]
        public string text;
        [Range(0f, 1f)] public float amount = 0.25f;
        public float weight = 1f;

        static InjuryBalance Make(string text, float amount, float weight = 1f) => new InjuryBalance { text = text, amount = amount, weight = weight };

        public static List<InjuryBalance> DeepCleanDefaults() => new List<InjuryBalance>
        {
            Make("a torn fin from the net", 0.25f, 1.5f),
            Make("lost scales in the net", 0.15f, 1.5f),
            Make("scraped by the algae scraper", 0.2f),
            Make("bruised against the glass", 0.2f),
            Make("shock from the big water change", 0.3f),
            Make("gill burns from the cleaning chemicals", 0.35f, 0.6f),
            Make("jumped out of the holding bucket", 0.45f, 0.4f),
        };

        /// <summary>One injury picked by weight, or null from an empty list.</summary>
        public static InjuryBalance Pick(List<InjuryBalance> list)
        {
            if (list == null || list.Count == 0) return null;
            float total = 0f;
            foreach (var item in list) if (item != null) total += Mathf.Max(0f, item.weight);
            float roll = GameRandom.Value * total;
            foreach (var item in list)
            {
                if (item == null) continue;
                roll -= Mathf.Max(0f, item.weight);
                if (roll <= 0f) return item;
            }
            return list[list.Count - 1];
        }
    }
}
