using System;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder.Genetics
{
    /// <summary>
    /// The full DNA of a fish. Every gene carries two alleles, one from each parent.
    /// All fields are value types, so a shallow copy is a full copy.
    /// </summary>
    [Serializable]
    public class Genome
    {
        [Tooltip("Species id (see Species). Fixed for life and inherited from the mother.")]
        public string species;
        [Tooltip("Species id of the fins when they come from a cross-bred ancestor; empty = same as the body.")]
        public string finSpecies = "";
        [Tooltip("Species id of the tail when it comes from a cross-bred ancestor; empty = same as the body.")]
        public string tailSpecies = "";

        [Header("Colour")]
        public HueGene bodyHue;
        public ContinuousGene bodySaturation;
        public ContinuousGene bodyBrightness;
        public HueGene patternHue;

        [Header("Pattern")]
        public DiscreteGene pattern;
        public ContinuousGene patternScale;
        public ContinuousGene patternOffset;

        [Header("Shape")]
        public DiscreteGene finShape;
        public DiscreteGene tailShape;
        public ContinuousGene finSize;
        public ContinuousGene bodyLength;
        public ContinuousGene bodyHeight;

        [Header("Hidden")]
        [Tooltip("Resistance to disease from food and other fish.")]
        public ContinuousGene hardiness;
        public ContinuousGene swimSpeed;

        public Species Species => Species.Get(species);
        public string FinSpecies => string.IsNullOrEmpty(finSpecies) ? species : finSpecies;
        public string TailSpecies => string.IsNullOrEmpty(tailSpecies) ? species : tailSpecies;
        /// <summary>True when the fins or tail come from another species (a cross-bred fish).</summary>
        public bool IsHybrid => FinSpecies != species || TailSpecies != species;

        /// <summary>A random wild fish of a random species.</summary>
        public static Genome CreateRandom() => CreateRandom(Species.All[GameRandom.Range(0, Species.All.Count)]);

        /// <summary>A random wild fish of the given species.</summary>
        public static Genome CreateRandom(Species kind)
        {
            return new Genome
            {
                species = kind.id,
                bodyHue = HueGene.Random(),
                bodySaturation = ContinuousGene.Random(),
                bodyBrightness = ContinuousGene.Random(),
                patternHue = HueGene.Random(),
                pattern = DiscreteGene.Random(TraitCounts.Patterns),
                patternScale = ContinuousGene.Random(),
                patternOffset = ContinuousGene.Random(),
                finShape = DiscreteGene.Random(TraitCounts.FinShapes),
                tailShape = DiscreteGene.Random(TraitCounts.TailShapes),
                finSize = ContinuousGene.Random(),
                bodyLength = ContinuousGene.Random(),
                bodyHeight = ContinuousGene.Random(),
                hardiness = ContinuousGene.Random(),
                swimSpeed = ContinuousGene.Random(),
            };
        }

        public Genome Clone() => (Genome)MemberwiseClone();

        public Phenotype Express() => new Phenotype(this);

        public string ToJson() => JsonUtility.ToJson(this);

        public static Genome FromJson(string json) => JsonUtility.FromJson<Genome>(json);
    }
}
