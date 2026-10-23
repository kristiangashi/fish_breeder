using System;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// General game rules. Lives in the balance file under "rules" (see <see cref="Balance"/>),
    /// so it is edited in the balance console like everything else.
    /// Time-based numbers are per time step: one step happens when the shop opens and one when it closes.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        [Header("Hunger and growth")]
        [Range(0f, 1f)] public float starvingThreshold = 0.8f;

        [Header("Feeding and disease")]
        [Tooltip("Base chance that a single meal makes a fish sick.")]
        [Range(0f, 1f)] public float baseInfectionChancePerMeal = 0.02f;
        [Tooltip("How much a fully hardy fish (hardiness gene = 1) reduces infection chance.")]
        [Range(0f, 1f)] public float hardinessProtection = 0.6f;
        [Tooltip("Infection chance multiplier for a meal eaten while starving.")]
        public float starvingInfectionMultiplier = 1.5f;
        [Tooltip("Infection chance multiplier at 0% water quality (1 at 100%).")]
        public float dirtyWaterInfectionMultiplier = 3f;

        [Header("Water")]
        public float pelletRotSeconds = 20f;
        public float waterQualityLossPerRottenPellet = 0.01f;
        [Tooltip("Room temperature in C: tank temperature without a heater.")]
        public float roomTemperature = 22f;
        public float startingPh = 7.2f;
        [Tooltip("Fish a tank holds comfortably without a bubbler before oxygen runs low.")]
        public int fishPerTankWithoutBubbler = 8;

        [Header("Breeding")]
        [Tooltip("Time steps a fish rests after breeding.")]
        public int breedingCooldownSteps = 2;
        [Range(0f, 1f)] public float mutationChance = 0.05f;
        [Range(0f, 1f)] public float mutationStrength = 0.15f;
        public int maxFishPerTank = 24;

        /// <summary>The rules from the loaded balance file.</summary>
        public static GameConfig Active => Balance.Data.rules;
    }
}
