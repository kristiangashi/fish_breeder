using FishBreeder.Core;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder.Breeding
{
    /// <summary>
    /// Pure genetics: each child gene takes one random allele from each parent,
    /// with a small chance of mutation. No Unity objects involved, so it is easy to test.
    /// </summary>
    public static class BreedingService
    {
        public static Genome Cross(Genome mother, Genome father, float mutationChance, float mutationStrength)
        {
            var m = new Mutator(mutationChance, mutationStrength);
            return new Genome
            {
                bodyHue = m.Inherit(mother.bodyHue, father.bodyHue),
                bodySaturation = m.Inherit(mother.bodySaturation, father.bodySaturation),
                bodyBrightness = m.Inherit(mother.bodyBrightness, father.bodyBrightness),
                patternHue = m.Inherit(mother.patternHue, father.patternHue),
                pattern = m.Inherit(mother.pattern, father.pattern, TraitCounts.Patterns),
                patternScale = m.Inherit(mother.patternScale, father.patternScale),
                patternOffset = m.Inherit(mother.patternOffset, father.patternOffset),
                finShape = m.Inherit(mother.finShape, father.finShape, TraitCounts.FinShapes),
                tailShape = m.Inherit(mother.tailShape, father.tailShape, TraitCounts.TailShapes),
                finSize = m.Inherit(mother.finSize, father.finSize),
                bodyLength = m.Inherit(mother.bodyLength, father.bodyLength),
                bodyHeight = m.Inherit(mother.bodyHeight, father.bodyHeight),
                hardiness = m.Inherit(mother.hardiness, father.hardiness),
                swimSpeed = m.Inherit(mother.swimSpeed, father.swimSpeed),
            };
        }

        readonly struct Mutator
        {
            readonly float chance;
            readonly float strength;

            public Mutator(float chance, float strength)
            {
                this.chance = chance;
                this.strength = strength;
            }

            public ContinuousGene Inherit(ContinuousGene mother, ContinuousGene father)
            {
                return new ContinuousGene(Mutate(mother.RandomAllele()), Mutate(father.RandomAllele()));
            }

            public HueGene Inherit(HueGene mother, HueGene father)
            {
                // HueGene wraps values, so a mutation past 1 continues around the colour wheel.
                return new HueGene(MutateUnclamped(mother.RandomAllele()), MutateUnclamped(father.RandomAllele()));
            }

            public DiscreteGene Inherit(DiscreteGene mother, DiscreteGene father, int variantCount)
            {
                return new DiscreteGene(Mutate(mother.RandomAllele(), variantCount), Mutate(father.RandomAllele(), variantCount));
            }

            float Mutate(float allele) => Mathf.Clamp01(MutateUnclamped(allele));

            float MutateUnclamped(float allele)
            {
                return GameRandom.Chance(chance) ? allele + GameRandom.Range(-strength, strength) : allele;
            }

            int Mutate(int allele, int variantCount)
            {
                return GameRandom.Chance(chance) ? GameRandom.Range(0, variantCount) : allele;
            }
        }
    }
}
