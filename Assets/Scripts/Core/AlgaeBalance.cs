using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// How algae works. Every tank has an algae target, the share of the tank it settles at: 10%
    /// with nothing in it, more for every fish, plant and decoration, less for every algae eater.
    /// Algae climbs quickly up to the target. Past it, a filter made for the tank's fish holds it
    /// there; with no filter, or more fish than the filter handles, it keeps creeping up slowly.
    /// Each feeding adds a little algae at once that goes again one feeding's worth per time step.
    /// The moment algae reaches the bloom level the tank blooms, and only a deep clean clears it.
    /// Algae eaters live on the algae: they need a little of it and get sick on too much.
    /// </summary>
    [Serializable]
    public class AlgaeBalance
    {
        [Header("The algae target (share of the tank covered)")]
        [Tooltip("Where an empty tank's algae settles (0.1 = 10%). The game never says why.")]
        [Range(0f, 1f)] public float naturalAlgae = 0.1f;
        [Tooltip("How much each tiny fish raises the target in the standard tank (0.01 = 1%, so 10 fish make 10%). A tank with a bigger fish limit gets less per fish, in proportion. Algae eaters don't count.")]
        public float fishAlgae = 0.01f;
        [Tooltip("How many tiny fish each fish of a size group counts as, for algae and filters (tiny, small, medium, large). In the public aquarium's exhibits and holding tanks every fish counts as one.")]
        public float tinyFishLoad = 1f, smallFishLoad = 2f, mediumFishLoad = 3f, largeFishLoad = 6f;
        [Tooltip("How much each decoration that isn't a plant raises the target (too many decorations mean more algae). Plants have their own amount (Eggs, decor & quality tab).")]
        public float decorationAlgae = 0.01f;
        [Tooltip("How much each algae eater lowers the target in the standard tank (0.05 = 5%). Bigger tanks get less per eater, like the fish. A tank of only algae eaters ends up with too little for them, so they need wafers.")]
        public float eaterAlgae = 0.05f;

        [Header("How fast it moves")]
        [Tooltip("Below the target, each time step algae climbs this share of the way to it...")]
        [Range(0f, 1f)] public float riseShare = 0.3f;
        [Tooltip("...but at least the first number and at most the second.")]
        public float minRise = 0.02f, maxRise = 0.1f;
        [Tooltip("Past the target with no working filter, or more fish than the filter handles, algae creeps up this much each time step (much slower than below it).")]
        public float overTargetRise = 0.01f;
        [Tooltip("Above the target with a filter that handles the fish (after algae wafers, a new filter, more eaters or fewer fish), algae falls this share of the way back each time step, at least the second number.")]
        [Range(0f, 1f)] public float fallShare = 0.4f;
        public float minFall = 0.02f;

        [Header("Feeding")]
        [Tooltip("Algae each feeding adds at once (0.02 = 2%). One feeding's worth goes again at each time step, so two feedings take two time steps to go. Algae wafers add their own amount and stay.")]
        public float feedAlgae = 0.02f;

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
        [Tooltip("The moment a tank's algae reaches this (feeding included), it blooms. There's no undoing it: only a deep clean clears a bloom.")]
        [Range(0f, 1f)] public float bloomAbove = 0.6f;
        [Tooltip("Algae a bloom adds the moment it starts (0.35 = 35%).")]
        public float bloomJump = 0.35f;
        [Tooltip("Algae a bloom adds every time step until a deep clean (0.1 = 10%).")]
        public float bloomGrowth = 0.1f;
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
