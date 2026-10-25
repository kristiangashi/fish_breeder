using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Health
{
    public enum Medicine { None, Antiviral, Antibacterial, Antiparasitic }

    /// <summary>How an illness shows on the fish. Auto picks from the disease id.</summary>
    public enum SymptomLook { Auto, Spots, FinRot, Listing, Fever, None }

    /// <summary>
    /// One illness a fish can catch. Illness lowers health; a fish only dies from the death roll at
    /// a time step, which gets likelier as health drops. All rates are per time step.
    /// </summary>
    [Serializable]
    public class DiseaseDefinition
    {
        public string id;
        public string displayName;
        [TextArea] public string description;

        [Tooltip("Relative chance of this disease being the one caught.")]
        public float weight = 1f;
        [Tooltip("Severity gained per time step (0..1). Hardy fish progress up to 50% slower.")]
        public float progressionPerStep = 0.2f;
        [Tooltip("Share of the illness health weight it takes at full severity (0..1).")]
        [Range(0f, 1f)] public float lethality = 0.7f;
        [Tooltip("Chance per time step of getting better on its own, scaled by hardiness.")]
        [Range(0f, 1f)] public float recoveryChancePerStep = 0.1f;
        [Tooltip("Chance per time step of passing it to each tank mate. 0 = not contagious.")]
        [Range(0f, 1f)] public float contagionChancePerStep;
        [Range(0f, 1f)] public float swimSpeedMultiplier = 0.7f;
        [Tooltip("Body tint at full severity.")]
        public Color symptomTint = Color.white;
        public Medicine cure = Medicine.None;
        [Tooltip("How it shows on the fish: white spots, ragged fins, floating on its side, or a pale feverish shiver. Auto picks from the id.")]
        public SymptomLook look = SymptomLook.Auto;

        public SymptomLook Look
        {
            get
            {
                if (look != SymptomLook.Auto) return look;
                switch (id)
                {
                    case "ich": return SymptomLook.Spots;
                    case "fin_rot": return SymptomLook.FinRot;
                    case "swim_bladder": return SymptomLook.Listing;
                    case "aqua_flu": return SymptomLook.Fever;
                    default: return SymptomLook.Spots;
                }
            }
        }

        public static List<DiseaseDefinition> CreateDefaults()
        {
            return new List<DiseaseDefinition>
            {
                new DiseaseDefinition
                {
                    id = "aqua_flu", displayName = "Aqua Flu", description = "A fast virus that spreads to tank mates.",
                    weight = 1f, progressionPerStep = 0.3f, lethality = 0.9f, recoveryChancePerStep = 0.08f, contagionChancePerStep = 0.25f,
                    swimSpeedMultiplier = 0.6f, symptomTint = new Color(0.75f, 0.85f, 1f), cure = Medicine.Antiviral,
                },
                new DiseaseDefinition
                {
                    id = "fin_rot", displayName = "Fin Rot", description = "Bacteria from spoiled food and dirty water eat away at the fins.",
                    weight = 1.5f, progressionPerStep = 0.2f, lethality = 0.7f, recoveryChancePerStep = 0.1f,
                    swimSpeedMultiplier = 0.8f, symptomTint = new Color(0.8f, 0.7f, 0.55f), cure = Medicine.Antibacterial,
                },
                new DiseaseDefinition
                {
                    id = "ich", displayName = "White Spot", description = "A parasite that slowly hops between fish.",
                    weight = 1.2f, progressionPerStep = 0.25f, lethality = 0.8f, recoveryChancePerStep = 0.05f, contagionChancePerStep = 0.15f,
                    swimSpeedMultiplier = 0.75f, symptomTint = new Color(0.95f, 0.95f, 0.9f), cure = Medicine.Antiparasitic,
                },
                new DiseaseDefinition
                {
                    id = "swim_bladder", displayName = "Swim Bladder", description = "Overeating trouble. Rarely deadly and usually passes on its own.",
                    weight = 0.8f, progressionPerStep = 0.15f, lethality = 0.3f, recoveryChancePerStep = 0.4f,
                    swimSpeedMultiplier = 0.4f, symptomTint = Color.white, cure = Medicine.None,
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
