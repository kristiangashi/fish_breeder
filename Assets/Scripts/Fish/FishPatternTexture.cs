using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Paints the body texture from the phenotype: base colour with a lighter belly,
    /// plus stripes, spots or marbling. Works on the placeholder body's UV layout.
    /// </summary>
    public static class FishPatternTexture
    {
        public static Texture2D Generate(Phenotype p, int width = 128, int height = 64)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = "FishPattern",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            float frequency = Mathf.Lerp(3f, 9f, p.patternScale);
            float seed = p.patternOffset * 100f;
            var back = p.bodyColor * 0.75f;
            var belly = Color.Lerp(p.bodyColor, Color.white, 0.45f);
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                float bellyAmount = 1f - Mathf.Abs(v - 0.5f) * 2f;
                for (int x = 0; x < width; x++)
                {
                    float u = x / (width - 1f);
                    var color = Color.Lerp(back, belly, bellyAmount);
                    float mark = PatternMask(p.pattern, u, v, frequency, seed) * (1f - bellyAmount * 0.6f);
                    color = Color.Lerp(color, p.patternColor, mark);
                    color.a = 1f;
                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        static float PatternMask(PatternType pattern, float u, float v, float frequency, float seed)
        {
            switch (pattern)
            {
                case PatternType.Striped:
                    return Step(0.25f, 0.45f, Mathf.Sin((u * frequency + seed) * Mathf.PI * 2f + Mathf.Sin(v * Mathf.PI * 2f) * 0.6f));
                case PatternType.Spotted:
                    return Step(0.62f, 0.68f, Mathf.PerlinNoise(u * frequency * 2f + seed, v * frequency + seed));
                case PatternType.Marbled:
                    float n = Mathf.PerlinNoise(u * frequency + seed, v * frequency * 0.6f + seed);
                    return 1f - Step(0.02f, 0.08f, Mathf.Abs(n - 0.5f));
                default:
                    return 0f;
            }
        }

        static float Step(float from, float to, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, x));
    }
}
