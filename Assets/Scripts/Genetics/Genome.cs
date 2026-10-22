using System;
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

        public static Genome CreateRandom()
        {
            return new Genome
            {
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
