using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// How algae works. The fish in a tank set how much algae it can reach (its algae maximum):
    /// more fish for the tank's size, more algae. Algae climbs towards that maximum a little at
    /// every time step rather than all at once. A filter holds it at a steady level, but only for
    /// as many fish as it's made for; algae eaters take a share off the maximum. Past the bloom
    /// level a tank can burst into an algal bloom that only a deep clean clears. Algae eaters
    /// live on the algae: they need a little of it and get sick on too much.
    /// </summary>
    [Serializable]
    public class AlgaeBalance
    {
        [Header("How much algae a tank can reach")]
        [Tooltip("Algae maximum each tiny fish adds in the standard tank with no filter and no algae eaters: 0.04 makes 5 fish reach 20%. A tank with a bigger fish limit gets less per fish, in proportion.")]
        public float algaePerFish = 0.04f;
        [Tooltip("How many tiny fish each fish of a size group counts as for algae (tiny, small, medium, large). Algae eaters never count. In the public aquarium's exhibits and holding tanks every fish counts as one.")]
        public float tinyFishLoad = 1f, smallFishLoad = 2f, mediumFishLoad = 3f, largeFishLoad = 6f;
        [Tooltip("Share of the algae maximum a tank's full set of algae eaters takes off (the number each tank size needs is on the Aquariums tab). 1/3 turns a filter's 30% into 20%.")]
        [Range(0f, 1f)] public float eaterReduction = 0.3333f;
        [Tooltip("More algae eaters than the tank needs keep taking a share off, up to this.")]
        [Range(0f, 1f)] public float maxEaterReduction = 0.6f;

        [Header("How fast it gets there")]
        [Tooltip("Each time step algae climbs this share of the way to its maximum (so it takes a few days to get there).")]
        [Range(0f, 1f)] public float riseShare = 0.3f;
        [Tooltip("...but always at least this much while it's below the maximum, and never more than the second number (so even a crowded tank takes a few days to get bad).")]
        public float minRise = 0.02f, maxRise = 0.1f;
        [Tooltip("Each decoration makes algae climb this much faster (0.02 = 2%). Too many decorations speed algae.")]
        public float risePerDecoration = 0.02f;
        [Tooltip("Above its maximum (after algae wafers, or a new filter or eaters), algae falls this share of the way back each time step.")]
        [Range(0f, 1f)] public float fallShare = 0.4f;
        public float minFall = 0.02f;

        [Header("How it looks and what customers think")]
        [Tooltip("The water starts turning green at this much algae, and gets much greener from the second number.")]
        [Range(0f, 1f)] public float greenStart = 0.15f, greenMore = 0.3f;
        [Tooltip("With the tank's full set of algae eaters cleaning the glass, the water stays clear until this much algae (and gets much greener 15% later).")]
        [Range(0f, 1f)] public float greenStartWithEaters = 0.3f;
        [Tooltip("Customers start minding algae at this level, and mind it a lot more from the second.")]
        [Range(0f, 1f)] public float bothersAbove = 0.4f, bothersMoreAbove = 0.5f;
        [Tooltip("Chance a customer looking at the tank is put off by its algae, between the two levels above and past the second.")]
        [Range(0f, 1f)] public float bothersChance = 0.35f, bothersMoreChance = 0.85f;

        [Header("Algal bloom")]
        [Tooltip("At this much algae a tank risks an algal bloom at every time step.")]
        [Range(0f, 1f)] public float bloomAbove = 0.5f;
        [Tooltip("Chance of a bloom at a time step right at the bloom level, plus the second number for every 10% more algae.")]
        [Range(0f, 1f)] public float bloomChance = 0.15f, bloomChancePer10 = 0.15f;
        [Tooltip("Algae a bloom adds the moment it starts.")]
        public float bloomJump = 0.35f;
        [Tooltip("Chance each fish dies right away when a bloom starts, and the chance a survivor falls ill.")]
        [Range(0f, 1f)] public float bloomDeathChance = 0.12f, bloomIllnessChance = 0.5f;
        [Tooltip("How badly a bloom's choked water stresses every fish while it lasts (environment, 0..1). Only a deep clean ends it.")]
        [Range(0f, 1f)] public float bloomStress = 0.5f;

        [Header("Clean tank")]
        [Tooltip("Cleans each tank gets per closed-shop session (more with the Tireless perk).")]
        public int cleansPerSession = 2;
        [Tooltip("One more clean per session every this many Fishkeeping levels (0 = never).")]
        public int extraCleanEveryFishkeepingLevels;
        [Tooltip("Algae one clean takes off at Fishkeeping level 1 (more with the Elbow grease perk).")]
        public float cleanAlgaeRemoved = 0.05f;
        [Tooltip("Each Fishkeeping level makes a clean this much more effective (0.02 = +2%).")]
        public float cleanBonusPerFishkeepingLevel = 0.02f;
        [Tooltip("Water quality one clean gives back.")]
        public float cleanQualityRestored = 0.2f;

        [Header("Deep clean")]
        [Tooltip("Deep cleans each tank gets per closed-shop session.")]
        public int deepCleansPerSession = 1;
        public int deepCleanPrice;
        [Tooltip("Algae left after a deep clean. It ends an algal bloom and gives the water back this much quality.")]
        [Range(0f, 1f)] public float deepCleanAlgaeLeft = 0.02f;
        public float deepCleanQualityRestored = 0.5f;
        [Tooltip("Chance each fish gets hurt in a deep clean at Fishkeeping level 1, how much less per level, and the least it gets to.")]
        [Range(0f, 1f)] public float deepCleanInjuryChance = 0.3f;
        public float deepCleanInjuryLessPerLevel = 0.004f;
        [Range(0f, 1f)] public float deepCleanMinInjuryChance = 0.08f;
        [Tooltip("What can happen to a fish in a deep clean: one is picked by weight. Amount is how much it hurts (0..1, wounds heal a little every time step).")]
        public List<InjuryBalance> deepCleanInjuries = InjuryBalance.DeepCleanDefaults();

        [Header("Algae eaters")]
        [Tooltip("Algae one feeding of algae wafers adds to the tank, for algae eaters to graze.")]
        public float waferAlgae = 0.1f;
        [Tooltip("Algae eaters without their own numbers (Species tab) get a least algae between these two (each species its own, picked from its name)...")]
        [Range(0f, 1f)] public float eaterMinFrom = 0.1f, eaterMinTo = 0.16f;
        [Tooltip("...and get sick above an algae level between these two.")]
        [Range(0f, 1f)] public float eaterMaxFrom = 0.42f, eaterMaxTo = 0.5f;
        [Tooltip("Too much algae for an algae eater: illness chance multiplied by this, and this much stress (environment) for every 10% over its limit.")]
        public float eaterOverIllness = 3f, eaterOverStressPer10 = 0.25f;

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
