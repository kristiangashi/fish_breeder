using System.Collections.Generic;

namespace FishBreeder.Genetics
{
    public enum SwimStyle
    {
        /// <summary>Pottering around open water at its preferred depth.</summary>
        Midwater,
        /// <summary>Lives on the sand: hops, roots for food, rests on its fins, dashes up for air.</summary>
        Bottom,
    }

    /// <summary>
    /// A kind of fish. Species is fixed for life and passed from mother to fry; only fish of the
    /// same species can breed. Genes then vary the individual within the species' ranges.
    /// Small species are drawn a little larger than life so they are easy to see in the tank.
    /// </summary>
    public sealed class Species
    {
        public readonly string id;
        public readonly string displayName;
        /// <summary>Adult length range in metres, nose to tail tip.</summary>
        public readonly float minLength, maxLength;
        /// <summary>Swimming speed relative to a goldfish, in body lengths per second.</summary>
        public readonly float speed;
        /// <summary>Preferred depth band: 0 = the sand, 1 = the surface.</summary>
        public readonly float depthMin, depthMax;
        public readonly SwimStyle style;

        Species(string id, string displayName, float minLength, float maxLength, float speed,
            float depthMin, float depthMax, SwimStyle style = SwimStyle.Midwater)
        {
            this.id = id;
            this.displayName = displayName;
            this.minLength = minLength;
            this.maxLength = maxLength;
            this.speed = speed;
            this.depthMin = depthMin;
            this.depthMax = depthMax;
            this.style = style;
        }

        public static readonly Species CometGoldfish = new Species("CometGoldfish", "Comet Goldfish", 0.1f, 0.2f, 1f, 0.15f, 0.85f);

        public static readonly IReadOnlyList<Species> All = new[]
        {
            CometGoldfish,
            new Species("FancyGoldfish", "Fancy Goldfish", 0.1f, 0.14f, 0.7f, 0.15f, 0.8f),
            new Species("Angelfish", "Angelfish", 0.12f, 0.15f, 0.6f, 0.35f, 0.85f),
            new Species("Betta", "Betta", 0.08f, 0.095f, 0.6f, 0.45f, 0.95f),
            new Species("Clownfish", "Clownfish", 0.08f, 0.1f, 0.9f, 0.25f, 0.75f),
            new Species("Guppy", "Guppy", 0.06f, 0.075f, 1.1f, 0.45f, 0.95f),
            new Species("TigerBarb", "Tiger Barb", 0.07f, 0.085f, 1.3f, 0.2f, 0.7f),
            new Species("NeonTetra", "Neon Tetra", 0.055f, 0.065f, 1.2f, 0.3f, 0.7f),
            new Species("ZebraDanio", "Zebra Danio", 0.06f, 0.07f, 1.5f, 0.6f, 0.95f),
            new Species("Corydoras", "Corydoras Catfish", 0.065f, 0.08f, 1f, 0f, 0.1f, SwimStyle.Bottom),
        };

        /// <summary>The species with this id; Comet Goldfish for unknown or empty ids (older saves).</summary>
        public static Species Get(string id)
        {
            if (!string.IsNullOrEmpty(id))
                foreach (var species in All)
                    if (species.id == id) return species;
            return CometGoldfish;
        }

        public override string ToString() => displayName;
    }
}
