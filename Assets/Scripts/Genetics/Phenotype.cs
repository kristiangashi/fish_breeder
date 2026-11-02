using UnityEngine;

namespace FishBreeder.Genetics
{
    /// <summary>
    /// What a genome looks like and how it behaves, in game units.
    /// Visuals, swimming and health read from this, never from raw alleles.
    /// </summary>
    public readonly struct Phenotype
    {
        /// <summary>The body's species: it decides needs, behaviour and how the young are born.</summary>
        public readonly Species species;
        /// <summary>Where the fins and tail come from; the same as <see cref="species"/> unless cross-bred.</summary>
        public readonly Species finSpecies;
        public readonly Species tailSpecies;
        public readonly Color bodyColor;
        public readonly Color patternColor;
        public readonly Color finColor;
        public readonly PatternType pattern;
        public readonly float patternScale;
        public readonly float patternOffset;
        public readonly FinShape finShape;
        public readonly TailShape tailShape;
        public readonly float finSize;
        /// <summary>Adult length in metres, nose to tail tip.</summary>
        public readonly float lengthMeters;
        /// <summary>Body height as a fraction of length.</summary>
        public readonly float heightRatio;
        public readonly float hardiness;
        public readonly float swimSpeed;

        public Phenotype(Genome g)
        {
            species = g.Species;
            finSpecies = Species.Get(g.FinSpecies) ?? species;
            tailSpecies = Species.Get(g.TailSpecies) ?? species;
            float saturation = Mathf.Lerp(0.35f, 1f, g.bodySaturation.Expressed);
            float brightness = Mathf.Lerp(0.45f, 1f, g.bodyBrightness.Expressed);
            bodyColor = Color.HSVToRGB(g.bodyHue.Expressed, saturation, brightness);
            // Pattern contrasts with the body: dark body gets light markings and vice versa.
            patternColor = Color.HSVToRGB(g.patternHue.Expressed, Mathf.Lerp(0.5f, 1f, saturation), 1.25f - brightness);
            finColor = Color.Lerp(bodyColor, patternColor, 0.5f);

            pattern = (PatternType)g.pattern.Expressed;
            patternScale = g.patternScale.Expressed;
            patternOffset = g.patternOffset.Expressed;

            finShape = (FinShape)g.finShape.Expressed;
            tailShape = (TailShape)g.tailShape.Expressed;
            finSize = Mathf.Lerp(0.6f, 1.6f, g.finSize.Expressed);
            lengthMeters = Mathf.Lerp(species.minLength, species.maxLength, g.bodyLength.Expressed);
            heightRatio = Mathf.Lerp(0.25f, 0.55f, g.bodyHeight.Expressed);

            hardiness = g.hardiness.Expressed;
            swimSpeed = g.swimSpeed.Expressed;
        }

        public bool IsHybrid => finSpecies != species || tailSpecies != species;

        /// <summary>"Comet Goldfish", or the cross-bred look's name.</summary>
        public string SpeciesName => CrossBreeding.Name(species, finSpecies, tailSpecies);

        /// <summary>Sum of how recessive the visible discrete traits are. Higher is rarer.</summary>
        public int RarityScore => (int)finShape + (int)tailShape + (int)pattern;

        public string RarityLabel
        {
            get
            {
                int score = RarityScore;
                if (score >= 9) return "Exotic";
                if (score >= 6) return "Rare";
                if (score >= 3) return "Uncommon";
                return "Common";
            }
        }

        public string Describe()
        {
            return $"{SpeciesName}, {pattern} body, {finShape} fins, {tailShape} tail, {lengthMeters * 100f:0} cm";
        }
    }
}
