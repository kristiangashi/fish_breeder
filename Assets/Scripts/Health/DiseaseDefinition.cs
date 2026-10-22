using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Health
{
    public enum Medicine { None, Antiviral, Antibacterial, Antiparasitic }

    /// <summary>
    /// One illness a fish can catch. Fish never die of age; diseases like these are
    /// the only way they die, and every one of them is a dice roll the player can influence.
    /// </summary>
    [Serializable]
    public class DiseaseDefinition
    {
        public string id;
        public string displayName;
        [TextArea] public string description;

        [Tooltip("Relative chance of this disease being the one caught from a bad meal.")]
        public float weight = 1f;
        [Tooltip("Severity gained per second (0..1). Hardy fish progress up to 50% slower.")]
        public float progressionPerSecond = 0.01f;
        [Tooltip("Death rate per second at full severity. Scales with severity squared.")]
        public float lethalityPerSecond = 0.02f;
        [Tooltip("Natural recovery rate per second, scaled by hardiness.")]
        public float recoveryPerSecond = 0.004f;
        [Tooltip("Rate at which a nearby healthy fish catches it. 0 = not contagious.")]
        public float contagionPerSecond;
        public float contagionRadius = 0.3f;
        [Range(0f, 1f)] public float swimSpeedMultiplier = 0.7f;
        [Tooltip("Body tint at full severity.")]
        public Color symptomTint = Color.white;
        public Medicine cure = Medicine.None;

        public static List<DiseaseDefinition> CreateDefaults()
        {
            return new List<DiseaseDefinition>
            {
                new DiseaseDefinition
                {
                    id = "aqua_flu",
                    displayName = "Aqua Flu",
                    description = "A fast virus that spreads to nearby fish.",
                    weight = 1f,
                    progressionPerSecond = 0.012f,
                    lethalityPerSecond = 0.03f,
                    recoveryPerSecond = 0.003f,
                    contagionPerSecond = 0.02f,
                    contagionRadius = 0.35f,
                    swimSpeedMultiplier = 0.6f,
                    symptomTint = new Color(0.75f, 0.85f, 1f),
                    cure = Medicine.Antiviral,
                },
                new DiseaseDefinition
                {
                    id = "fin_rot",
                    displayName = "Fin Rot",
                    description = "Bacteria from spoiled food eat away at the fins.",
                    weight = 1.5f,
                    progressionPerSecond = 0.008f,
                    lethalityPerSecond = 0.015f,
                    recoveryPerSecond = 0.004f,
                    swimSpeedMultiplier = 0.8f,
                    symptomTint = new Color(0.8f, 0.7f, 0.55f),
                    cure = Medicine.Antibacterial,
                },
                new DiseaseDefinition
                {
                    id = "ich",
                    displayName = "White Spot",
                    description = "A parasite that slowly hops between fish.",
                    weight = 1.2f,
                    progressionPerSecond = 0.01f,
                    lethalityPerSecond = 0.02f,
                    recoveryPerSecond = 0.002f,
                    contagionPerSecond = 0.01f,
                    contagionRadius = 0.25f,
                    swimSpeedMultiplier = 0.75f,
                    symptomTint = new Color(0.95f, 0.95f, 0.9f),
                    cure = Medicine.Antiparasitic,
                },
                new DiseaseDefinition
                {
                    id = "swim_bladder",
                    displayName = "Swim Bladder",
                    description = "Overeating trouble. Rarely deadly and usually passes on its own.",
                    weight = 0.8f,
                    progressionPerSecond = 0.006f,
                    lethalityPerSecond = 0.006f,
                    recoveryPerSecond = 0.02f,
                    swimSpeedMultiplier = 0.4f,
                    symptomTint = Color.white,
                    cure = Medicine.None,
                },
            };
        }

        public static DiseaseDefinition PickWeighted(IReadOnlyList<DiseaseDefinition> diseases, float roll01)
        {
            if (diseases == null || diseases.Count == 0) return null;
            float total = 0f;
            foreach (var d in diseases) total += Mathf.Max(0f, d.weight);
            float pick = roll01 * total;
            foreach (var d in diseases)
            {
                pick -= Mathf.Max(0f, d.weight);
                if (pick <= 0f) return d;
            }
            return diseases[diseases.Count - 1];
        }
    }
}
