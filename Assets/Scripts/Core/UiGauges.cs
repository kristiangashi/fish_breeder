using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Small IMGUI read-outs shared by the tank view and the research notes: a coloured scale (cold
    /// blue to hot red, or acid red to alkaline purple) with the range a fish likes outlined on it and
    /// an arrow where the water is now, and little coloured badges for things like sex and age.
    /// </summary>
    public static class UiGauges
    {
        public const float TempMin = 10f, TempMax = 34f;
        public const float PhMin = 5f, PhMax = 9.5f;

        static Texture2D heatScale, phScale;
        static GUIStyle arrowStyle, tickStyle;
        static readonly Dictionary<Color, GUIStyle> badges = new Dictionary<Color, GUIStyle>();

        static Texture2D HeatScale => heatScale != null ? heatScale : heatScale = Gradient("HeatScale",
            new Color(0.2f, 0.35f, 0.95f), new Color(0.2f, 0.75f, 0.95f), new Color(0.35f, 0.85f, 0.4f), new Color(0.98f, 0.7f, 0.2f), new Color(0.9f, 0.2f, 0.15f));

        static Texture2D PhScale => phScale != null ? phScale : phScale = Gradient("PhScale",
            new Color(0.9f, 0.25f, 0.2f), new Color(0.95f, 0.8f, 0.25f), new Color(0.35f, 0.8f, 0.35f), new Color(0.2f, 0.65f, 0.75f), new Color(0.5f, 0.3f, 0.8f));

        /// <summary>Temperature scale with the water at <paramref name="celsius"/> and, when known, the range the fish likes.</summary>
        public static void Temperature(float celsius, Vector2? liked)
        {
            Gauge(HeatScale, TempMin, TempMax, celsius, liked, $"{celsius:0.#} C", "cold", "hot");
        }

        /// <summary>pH scale: acidic (low) on the left, alkaline (high) on the right, neutral 7 in the middle-ish.</summary>
        public static void Ph(float ph, Vector2? liked)
        {
            Gauge(PhScale, PhMin, PhMax, ph, liked, $"pH {ph:0.0}", "acidic", "alkaline");
        }

        static void Gauge(Texture2D scale, float min, float max, float value, Vector2? liked, string valueText, string lowWord, string highWord)
        {
            var rect = Ui.Slot("Gauge", 140f, 34f, GUILayout.ExpandWidth(true));
            var bar = new Rect(rect.x + 2f, rect.y + 14f, rect.width - 4f, 10f);
            GUI.DrawTexture(bar, scale, ScaleMode.StretchToFill);

            if (liked.HasValue)
            {
                float x0 = Mathf.Lerp(bar.x, bar.xMax, Mathf.InverseLerp(min, max, liked.Value.x));
                float x1 = Mathf.Lerp(bar.x, bar.xMax, Mathf.InverseLerp(min, max, liked.Value.y));
                var band = new Rect(x0, bar.y - 3f, Mathf.Max(2f, x1 - x0), bar.height + 6f);
                GUI.DrawTexture(band, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(1f, 1f, 1f, 0.25f), 0f, 0f);
                Outline(band, Color.white);
            }

            float t = Mathf.InverseLerp(min, max, value);
            float x = Mathf.Lerp(bar.x, bar.xMax, t);
            GUI.DrawTexture(new Rect(x - 1f, bar.y - 2f, 2f, bar.height + 4f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.white, 0f, 0f);
            bool inside = liked.HasValue && value >= liked.Value.x && value <= liked.Value.y;
            string color = !liked.HasValue ? "#ffffff" : inside ? "#9fe39f" : "#ff9a8a";
            float labelX = Mathf.Clamp(x - 40f, rect.x, rect.xMax - 80f);
            Ui.Label(new Rect(labelX, rect.y - 3f, 80f, 18f), $"<color={color}><b>▼ {valueText}</b></color>", ArrowStyle);
            Ui.Label(new Rect(bar.x, bar.yMax, 60f, 14f), lowWord, TickStyle);
            Ui.Label(new Rect(bar.xMax - 60f, bar.yMax, 60f, 14f), highWord, RightTick);
        }

        static void Outline(Rect r, Color color)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
            GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
        }

        /// <summary>A rounded-looking coloured tag with bold text, for things that should stand out (sex, age).</summary>
        public static void Badge(string text, Color color)
        {
            if (!badges.TryGetValue(color, out var style) || style.normal.background == null)
            {
                var background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                background.SetPixel(0, 0, color);
                background.Apply();
                style = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    fontStyle = FontStyle.Bold,
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(8, 8, 2, 2),
                    margin = new RectOffset(0, 6, 2, 2),
                };
                style.normal.background = background;
                style.normal.textColor = Color.white;
                badges[color] = style;
            }
            Ui.Label(text, style, GUILayout.ExpandWidth(false));
        }

        static GUIStyle ArrowStyle => arrowStyle != null ? arrowStyle : arrowStyle = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.UpperCenter, fontSize = 11, padding = new RectOffset(0, 0, 0, 0) };
        static GUIStyle TickStyle => tickStyle != null ? tickStyle : tickStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10, padding = new RectOffset(0, 0, 0, 0), normal = { textColor = new Color(0.75f, 0.78f, 0.82f) } };
        static GUIStyle rightTick;
        static GUIStyle RightTick => rightTick != null ? rightTick : rightTick = new GUIStyle(TickStyle) { alignment = TextAnchor.UpperRight };

        static Texture2D Gradient(string name, params Color[] stops)
        {
            const int width = 128;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int i = 0; i < width; i++)
            {
                float t = i / (width - 1f) * (stops.Length - 1);
                int a = Mathf.Min(stops.Length - 2, Mathf.FloorToInt(t));
                texture.SetPixel(i, 0, Color.Lerp(stops[a], stops[a + 1], t - a));
            }
            texture.Apply();
            return texture;
        }
    }
}
