using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The game's colours, for code that picks one (bars, badges, text colour codes) and for the
    /// screens' stylesheet, Resources/UI/FishBreeder.uss, whose variables match: deep lagoon,
    /// bright aqua, turquoise, coral for the big actions, sunny yellow and warm sand. Also the soft
    /// dot and bubble pictures the effects use.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Abyss = new Color(0.02f, 0.11f, 0.17f);
        public static readonly Color Deep = new Color(0.03f, 0.2f, 0.29f);
        public static readonly Color Lagoon = new Color(0.04f, 0.36f, 0.44f);
        public static readonly Color Turquoise = new Color(0.1f, 0.62f, 0.64f);
        public static readonly Color Aqua = new Color(0.36f, 0.9f, 0.85f);
        public static readonly Color Coral = new Color(1f, 0.46f, 0.37f);
        public static readonly Color Sun = new Color(1f, 0.83f, 0.38f);
        public static readonly Color Sand = new Color(1f, 0.96f, 0.87f);
        public static readonly Color Leaf = new Color(0.42f, 0.86f, 0.5f);
        public static readonly Color Hibiscus = new Color(1f, 0.42f, 0.66f);

        /// <summary>Rich-text colour codes for headings and highlights.</summary>
        public const string SunHex = "#ffd361", AquaHex = "#7ff0e4", CoralHex = "#ff8a73", LeafHex = "#8ff0a0", SandHex = "#fff4dc", HibiscusHex = "#ff6ba8";

        static Texture2D circle, ring;

        /// <summary>A soft white dot, tinted by whoever draws it, for bubbles and confetti.</summary>
        public static Texture2D Circle => circle != null ? circle : circle = MakeCircle(32, false);

        /// <summary>A bubble: a thin bright rim with a highlight.</summary>
        public static Texture2D Bubble => ring != null ? ring : ring = MakeCircle(32, true);

        static Texture2D MakeCircle(int size, bool bubble, Color? tint = null)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var color = tint ?? Color.white;
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float inside = Mathf.Clamp01(r - d);
                    float a;
                    if (bubble)
                    {
                        // Clear middle, bright rim, and a highlight up and to the left.
                        float rim = Mathf.Clamp01(1f - Mathf.Abs(d - (r - 2.5f)) / 2f);
                        float shine = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(dx, dy), new Vector2(-r * 0.35f, r * 0.35f)) / (r * 0.28f));
                        a = Mathf.Max(0.12f * inside, Mathf.Max(rim, shine)) * inside;
                    }
                    else a = inside;
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, a));
                }
            texture.Apply();
            return texture;
        }
    }
}
