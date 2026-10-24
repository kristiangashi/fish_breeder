using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The game's look for every IMGUI screen: deep lagoon panels with a bright aqua rim, turquoise
    /// buttons, coral for the big actions, sunny yellow headings and warm sand-coloured text, all
    /// drawn into small rounded textures at runtime (no art assets needed). Call
    /// <see cref="Apply"/> at the top of every OnGUI, because Unity resets the skin for each one.
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
        public const string SunHex = "#ffd361", AquaHex = "#7ff0e4", CoralHex = "#ff8a73", LeafHex = "#8ff0a0", SandHex = "#fff4dc";

        static GUISkin skin;
        static Font font;
        static GUIStyle primary, title, heading, card, cardSelected, tag, bar;
        static Texture2D circle, ring;

        /// <summary>Sets the themed skin for this OnGUI call (building it the first time).</summary>
        public static void Apply()
        {
            if (skin == null || skin.box.normal.background == null) Build();
            GUI.skin = skin;
        }

        public static GUISkin Skin
        {
            get
            {
                if (skin == null || skin.box.normal.background == null) Build();
                return skin;
            }
        }

        /// <summary>Coral button for the main action on a screen (Open Shop, Buy, Back to work).</summary>
        public static GUIStyle Primary => Ensure(ref primary, () =>
        {
            var style = new GUIStyle(Skin.button) { fontStyle = FontStyle.Bold, fontSize = 14, richText = true };
            SetStates(style, new Color(1f, 0.55f, 0.42f), new Color(0.92f, 0.36f, 0.3f), new Color(1f, 0.8f, 0.6f), 9);
            style.hover.background = Rounded(new Color(1f, 0.66f, 0.5f), new Color(1f, 0.45f, 0.36f), new Color(1f, 0.9f, 0.75f), 9, 2);
            style.active.background = Rounded(new Color(0.85f, 0.35f, 0.28f), new Color(0.75f, 0.27f, 0.22f), new Color(1f, 0.8f, 0.6f), 9, 2);
            return style;
        });

        /// <summary>Big window title in sunny yellow.</summary>
        public static GUIStyle Title => Ensure(ref title, () =>
        {
            var style = new GUIStyle(Skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, richText = true, wordWrap = false };
            style.normal.textColor = Sun;
            return style;
        });

        /// <summary>Section heading inside a window.</summary>
        public static GUIStyle Heading => Ensure(ref heading, () =>
        {
            var style = new GUIStyle(Skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, richText = true };
            style.normal.textColor = Aqua;
            style.margin = new RectOffset(4, 4, 8, 2);
            return style;
        });

        /// <summary>A lighter tile inside a window (shop offers, journal cards).</summary>
        public static GUIStyle Card => Ensure(ref card, () =>
        {
            var style = new GUIStyle(Skin.box) { richText = true, alignment = TextAnchor.UpperLeft };
            style.normal.background = Rounded(new Color(0.07f, 0.33f, 0.42f, 0.95f), new Color(0.05f, 0.26f, 0.35f, 0.95f), new Color(0.25f, 0.6f, 0.62f), 8, 1);
            style.hover.background = Rounded(new Color(0.1f, 0.42f, 0.5f, 0.95f), new Color(0.07f, 0.33f, 0.42f, 0.95f), Aqua, 8, 1);
            style.padding = new RectOffset(8, 8, 6, 6);
            return style;
        });

        public static GUIStyle CardSelected => Ensure(ref cardSelected, () =>
        {
            var style = new GUIStyle(Card);
            style.normal.background = Rounded(new Color(0.12f, 0.5f, 0.55f, 0.97f), new Color(0.08f, 0.4f, 0.47f, 0.97f), Sun, 8, 2);
            return style;
        });

        /// <summary>Thin rounded strip, for bars at the top and bottom of the screen.</summary>
        public static GUIStyle Bar => Ensure(ref bar, () =>
        {
            var style = new GUIStyle(Skin.box) { richText = true, alignment = TextAnchor.MiddleLeft };
            style.normal.background = Rounded(new Color(0.04f, 0.3f, 0.4f, 0.93f), new Color(0.02f, 0.18f, 0.27f, 0.93f), new Color(0.3f, 0.78f, 0.76f), 10, 1);
            style.padding = new RectOffset(10, 10, 4, 4);
            return style;
        });

        /// <summary>Pill-shaped tag (NEW, rarity, OPEN).</summary>
        public static GUIStyle Tag => Ensure(ref tag, () =>
        {
            var style = new GUIStyle(Skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true, padding = new RectOffset(6, 6, 1, 1) };
            style.normal.background = Rounded(Color.white, Color.white, Color.white, 7, 0);
            style.normal.textColor = Abyss;
            return style;
        });

        /// <summary>A soft white dot, tinted with GUI.color, for bubbles and confetti.</summary>
        public static Texture2D Circle => circle != null ? circle : circle = MakeCircle(32, false);

        /// <summary>A bubble: a thin bright rim with a highlight.</summary>
        public static Texture2D Bubble => ring != null ? ring : ring = MakeCircle(32, true);

        /// <summary>Draws a vertical gradient rectangle (used for full-screen washes).</summary>
        public static void Gradient(Rect rect, Color top, Color bottom)
        {
            const int steps = 24;
            for (int i = 0; i < steps; i++)
            {
                var color = Color.Lerp(top, bottom, i / (steps - 1f));
                GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height * i / steps, rect.width, rect.height / steps + 1f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 0f);
            }
        }

        static GUIStyle Ensure(ref GUIStyle style, System.Func<GUIStyle> make)
        {
            if (skin == null || skin.box.normal.background == null) Build();
            if (style == null) style = make();
            return style;
        }

        static void Build()
        {
            skin = Object.Instantiate(GUI.skin);
            skin.hideFlags = HideFlags.HideAndDontSave;
            primary = title = heading = card = cardSelected = tag = bar = null;
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Segoe UI Symbol", "Trebuchet MS", "Verdana", "Arial" }, 14);
            if (font != null) skin.font = font;

            var text = Sand;
            foreach (var style in new[] { skin.label, skin.box, skin.button, skin.toggle, skin.textField, skin.textArea, skin.window })
            {
                style.normal.textColor = text;
                style.hover.textColor = Color.white;
                style.active.textColor = Color.white;
                style.focused.textColor = Color.white;
                style.onNormal.textColor = text;
                style.onHover.textColor = Color.white;
                style.onActive.textColor = Color.white;
                style.fontSize = 13;
                style.richText = true;
            }

            skin.box.normal.background = Rounded(new Color(0.04f, 0.27f, 0.36f, 0.9f), new Color(0.02f, 0.15f, 0.23f, 0.9f), new Color(0.28f, 0.72f, 0.72f), 9, 1);
            skin.box.border = new RectOffset(10, 10, 10, 10);
            skin.box.padding = new RectOffset(8, 8, 6, 6);

            skin.window.normal.background = Rounded(new Color(0.04f, 0.3f, 0.4f, 0.97f), new Color(0.02f, 0.14f, 0.22f, 0.97f), Aqua, 12, 2);
            skin.window.onNormal.background = skin.window.normal.background;
            skin.window.border = new RectOffset(13, 13, 13, 13);

            var button = skin.button;
            SetStates(button, new Color(0.13f, 0.62f, 0.65f), new Color(0.06f, 0.43f, 0.5f), new Color(0.45f, 0.9f, 0.85f), 8);
            button.hover.background = Rounded(new Color(0.2f, 0.75f, 0.76f), new Color(0.1f, 0.55f, 0.6f), new Color(0.7f, 1f, 0.95f), 8, 1);
            button.active.background = Rounded(new Color(0.05f, 0.38f, 0.44f), new Color(0.04f, 0.3f, 0.36f), Aqua, 8, 1);
            // Toggled buttons (toolbars, speed buttons) turn sunny.
            button.onNormal.background = Rounded(new Color(1f, 0.78f, 0.38f), new Color(0.95f, 0.6f, 0.25f), new Color(1f, 0.92f, 0.7f), 8, 1);
            button.onHover.background = button.onNormal.background;
            button.onActive.background = button.onNormal.background;
            button.onNormal.textColor = Abyss;
            button.onHover.textColor = Abyss;
            button.onActive.textColor = Abyss;
            button.border = new RectOffset(9, 9, 9, 9);
            button.padding = new RectOffset(8, 8, 4, 5);
            button.fontStyle = FontStyle.Bold;

            foreach (var name in new[] { "ButtonLeft", "ButtonMid", "ButtonRight" })
            {
                var style = skin.FindStyle(name);
                if (style == null) continue;
                CopyStates(button, style);
            }

            skin.textField.normal.background = Rounded(new Color(0.01f, 0.1f, 0.16f, 0.95f), new Color(0.01f, 0.1f, 0.16f, 0.95f), new Color(0.25f, 0.6f, 0.62f), 6, 1);
            skin.textField.focused.background = Rounded(new Color(0.02f, 0.14f, 0.2f, 0.95f), new Color(0.02f, 0.14f, 0.2f, 0.95f), Sun, 6, 1);
            skin.textField.hover.background = skin.textField.normal.background;
            skin.textField.border = new RectOffset(7, 7, 7, 7);
            skin.settings.cursorColor = Sun;
            skin.settings.selectionColor = new Color(0.3f, 0.8f, 0.8f, 0.5f);

            skin.verticalScrollbar.normal.background = Rounded(new Color(0f, 0.08f, 0.12f, 0.6f), new Color(0f, 0.08f, 0.12f, 0.6f), new Color(0f, 0f, 0f, 0f), 5, 0);
            skin.verticalScrollbar.border = new RectOffset(5, 5, 5, 5);
            skin.verticalScrollbar.fixedWidth = 10f;
            skin.verticalScrollbarThumb.normal.background = Rounded(new Color(0.4f, 0.9f, 0.85f), new Color(0.2f, 0.7f, 0.72f), new Color(0f, 0f, 0f, 0f), 5, 0);
            skin.verticalScrollbarThumb.border = new RectOffset(5, 5, 5, 5);
            skin.verticalScrollbarThumb.fixedWidth = 10f;
            skin.horizontalScrollbar.normal.background = skin.verticalScrollbar.normal.background;
            skin.horizontalScrollbarThumb.normal.background = skin.verticalScrollbarThumb.normal.background;

            skin.horizontalSlider.normal.background = Rounded(new Color(0f, 0.08f, 0.12f, 0.8f), new Color(0f, 0.08f, 0.12f, 0.8f), new Color(0.25f, 0.6f, 0.62f), 4, 1);
            skin.horizontalSlider.border = new RectOffset(5, 5, 4, 4);
            skin.horizontalSlider.fixedHeight = 8f;
            skin.horizontalSlider.margin = new RectOffset(4, 4, 9, 6);
            skin.horizontalSliderThumb.normal.background = MakeCircle(24, false, Coral);
            skin.horizontalSliderThumb.hover.background = MakeCircle(24, false, new Color(1f, 0.65f, 0.5f));
            skin.horizontalSliderThumb.active.background = skin.horizontalSliderThumb.hover.background;
            skin.horizontalSliderThumb.fixedWidth = 16f;
            skin.horizontalSliderThumb.fixedHeight = 16f;
            skin.horizontalSliderThumb.border = new RectOffset(0, 0, 0, 0);
            skin.horizontalSliderThumb.margin = new RectOffset(0, 0, -4, 0);

            skin.toggle.normal.background = Rounded(new Color(0.01f, 0.1f, 0.16f), new Color(0.01f, 0.1f, 0.16f), new Color(0.35f, 0.8f, 0.78f), 4, 1);
            skin.toggle.hover.background = Rounded(new Color(0.03f, 0.18f, 0.25f), new Color(0.03f, 0.18f, 0.25f), Aqua, 4, 1);
            skin.toggle.onNormal.background = Rounded(Leaf, new Color(0.25f, 0.7f, 0.4f), new Color(0.8f, 1f, 0.85f), 4, 1);
            skin.toggle.onHover.background = skin.toggle.onNormal.background;
            skin.toggle.active.background = skin.toggle.hover.background;
            skin.toggle.onActive.background = skin.toggle.onNormal.background;
            skin.toggle.border = new RectOffset(5, 5, 5, 5);
            skin.toggle.overflow = new RectOffset(0, 0, -3, -3);

            UiStyles.Rebuild();
        }

        static void SetStates(GUIStyle style, Color top, Color bottom, Color rim, int radius)
        {
            style.normal.background = Rounded(top, bottom, rim, radius, 1);
            style.focused.background = style.normal.background;
            style.border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1);
        }

        static void CopyStates(GUIStyle from, GUIStyle to)
        {
            to.normal.background = from.normal.background;
            to.hover.background = from.hover.background;
            to.active.background = from.active.background;
            to.onNormal.background = from.onNormal.background;
            to.onHover.background = from.onHover.background;
            to.onActive.background = from.onActive.background;
            to.normal.textColor = from.normal.textColor;
            to.hover.textColor = from.hover.textColor;
            to.onNormal.textColor = from.onNormal.textColor;
            to.onHover.textColor = from.onHover.textColor;
            to.onActive.textColor = from.onActive.textColor;
            to.border = from.border;
            to.padding = from.padding;
            to.fontStyle = from.fontStyle;
            to.fontSize = from.fontSize;
            to.richText = true;
        }

        /// <summary>
        /// A small rounded rectangle with a vertical gradient fill and a rim, used as a 9-sliced
        /// background (the corners keep their shape, the middle stretches).
        /// </summary>
        public static Texture2D Rounded(Color top, Color bottom, Color rim, int radius, int rimWidth)
        {
            int size = radius * 2 + 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                // Texture rows go bottom-up; the gradient runs top to bottom on screen.
                var fill = Color.Lerp(bottom, top, y / (size - 1f));
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float edge = radius - Mathf.Sqrt(dx * dx + dy * dy);
                    if (dx == 0f && dy == 0f) edge = Mathf.Min(Mathf.Min(x + 0.5f, size - x - 0.5f), Mathf.Min(y + 0.5f, size - y - 0.5f));
                    float alpha = Mathf.Clamp01(edge + 0.5f);
                    var color = rimWidth > 0 && edge < rimWidth ? rim : fill;
                    if (rimWidth > 0 && edge >= rimWidth && edge < rimWidth + 1f) color = Color.Lerp(rim, fill, edge - rimWidth);
                    color.a *= alpha;
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

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
