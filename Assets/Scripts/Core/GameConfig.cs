using System.Collections.Generic;
using FishBreeder.Health;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// All tuning numbers in one asset. Create one via Assets > Create > Fish Breeder > Game Config
    /// and assign it to GameBootstrap; otherwise a default instance is used.
    /// </summary>
    [CreateAssetMenu(menuName = "Fish Breeder/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Hunger and growth (age never kills a fish)")]
        [Tooltip("Hunger gained per second. 1 = completely starving.")]
        public float hungerPerSecond = 1f / 120f;
        [Tooltip("Seconds of being fed for a fry to become an adult.")]
        public float secondsToAdult = 180f;
        [Range(0f, 1f)] public float starvingThreshold = 0.8f;

        [Header("Feeding and disease")]
        [Tooltip("Base chance that a single meal makes a fish sick.")]
        [Range(0f, 1f)] public float baseInfectionChancePerMeal = 0.04f;
        [Tooltip("How much a fully hardy fish (hardiness gene = 1) reduces infection chance.")]
        [Range(0f, 1f)] public float hardinessProtection = 0.6f;
        [Tooltip("Infection chance multiplier for a meal eaten while starving.")]
        public float starvingInfectionMultiplier = 1.5f;
        [Tooltip("Infection chance multiplier at 0% water quality (1 at 100%).")]
        public float dirtyWaterInfectionMultiplier = 3f;

        [Header("Water")]
        public float pelletRotSeconds = 20f;
        public float waterQualityLossPerRottenPellet = 0.03f;
        [Tooltip("Water quality lost per fish per second (waste).")]
        public float bioloadPerFishPerSecond = 0.0002f;

        [Header("Breeding")]
        public float breedingCooldownSeconds = 60f;
        [Range(0f, 1f)] public float mutationChance = 0.05f;
        [Range(0f, 1f)] public float mutationStrength = 0.15f;
        public int minFryPerBreeding = 1;
        public int maxFryPerBreeding = 3;
        public int maxFishPerTank = 24;

        [Header("Diseases")]
        public List<DiseaseDefinition> diseases = DiseaseDefinition.CreateDefaults();

        static GameConfig active;

        public static GameConfig Active
        {
            get
            {
                if (active == null) active = CreateInstance<GameConfig>();
                return active;
            }
            set => active = value;
        }
    }
}
