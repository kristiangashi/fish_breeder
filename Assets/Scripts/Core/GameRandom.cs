using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Single source of randomness for gameplay so a run can be reproduced with a seed.
    /// </summary>
    public static class GameRandom
    {
        static System.Random rng = new System.Random();

        public static void Seed(int seed) => rng = new System.Random(seed);

        public static float Value => (float)rng.NextDouble();

        public static float Range(float min, float max) => min + (max - min) * Value;

        /// <summary>Integer in [min, maxExclusive).</summary>
        public static int Range(int min, int maxExclusive) => rng.Next(min, maxExclusive);

        public static bool Chance(float probability) => Value < probability;

        /// <summary>
        /// Rolls an event that happens at <paramref name="ratePerSecond"/> on average,
        /// independent of frame rate.
        /// </summary>
        public static bool ChancePerSecond(float ratePerSecond, float deltaTime)
        {
            if (ratePerSecond <= 0f) return false;
            return Value < 1f - Mathf.Exp(-ratePerSecond * deltaTime);
        }

        public static Vector3 InsideBounds(Bounds bounds)
        {
            return new Vector3(
                Range(bounds.min.x, bounds.max.x),
                Range(bounds.min.y, bounds.max.y),
                Range(bounds.min.z, bounds.max.z));
        }
    }
}
