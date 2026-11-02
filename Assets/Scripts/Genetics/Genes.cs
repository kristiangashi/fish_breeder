using System;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder.Genetics
{
    // Discrete traits are ordered from most dominant to most recessive.
    // The lower value always wins, so the last entries are the rare ones.
    public enum FinShape { Round, Fan, Spiky, Veil, Ribbon }
    public enum TailShape { Fork, Round, Lyre, Sword, Veil }
    public enum PatternType { Solid, Striped, Spotted, Marbled }

    /// <summary>Two co-dominant alleles in 0..1; the fish shows their average.</summary>
    [Serializable]
    public struct ContinuousGene
    {
        [Range(0f, 1f)] public float a;
        [Range(0f, 1f)] public float b;

        public ContinuousGene(float a, float b)
        {
            this.a = Mathf.Clamp01(a);
            this.b = Mathf.Clamp01(b);
        }

        public float Expressed => (a + b) * 0.5f;

        public float RandomAllele() => GameRandom.Chance(0.5f) ? a : b;

        public static ContinuousGene Random() => new ContinuousGene(GameRandom.Value, GameRandom.Value);
    }

    /// <summary>
    /// Two hue alleles on the colour wheel (0..1). Expressed as the circular mean,
    /// so red x magenta gives a reddish-pink instead of wrapping through green.
    /// </summary>
    [Serializable]
    public struct HueGene
    {
        [Range(0f, 1f)] public float a;
        [Range(0f, 1f)] public float b;

        public HueGene(float a, float b)
        {
            this.a = Mathf.Repeat(a, 1f);
            this.b = Mathf.Repeat(b, 1f);
        }

        public float Expressed
        {
            get
            {
                float halfway = Mathf.DeltaAngle(a * 360f, b * 360f) / 360f * 0.5f;
                return Mathf.Repeat(a + halfway, 1f);
            }
        }

        public float RandomAllele() => GameRandom.Chance(0.5f) ? a : b;

        public static HueGene Random() => new HueGene(GameRandom.Value, GameRandom.Value);
    }

    /// <summary>Discrete trait with simple dominance: the lower allele is expressed.</summary>
    [Serializable]
    public struct DiscreteGene
    {
        public int a;
        public int b;

        public DiscreteGene(int a, int b)
        {
            this.a = a;
            this.b = b;
        }

        public int Expressed => Mathf.Min(a, b);

        public bool IsHomozygous => a == b;

        public int RandomAllele() => GameRandom.Chance(0.5f) ? a : b;

        /// <summary>
        /// Random gene for wild fish. Each step towards the recessive end is half as likely,
        /// so rare alleles exist in the starter population but rarely show.
        /// </summary>
        public static DiscreteGene Random(int variantCount)
        {
            return new DiscreteGene(WildAllele(variantCount), WildAllele(variantCount));
        }

        public static int WildAllele(int variantCount)
        {
            int allele = 0;
            while (allele < variantCount - 1 && GameRandom.Chance(0.4f)) allele++;
            return allele;
        }
    }

    public static class TraitCounts
    {
        public static readonly int FinShapes = Enum.GetValues(typeof(FinShape)).Length;
        public static readonly int TailShapes = Enum.GetValues(typeof(TailShape)).Length;
        public static readonly int Patterns = Enum.GetValues(typeof(PatternType)).Length;
    }
}
