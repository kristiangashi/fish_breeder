using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The menus' buttons, labels, toggles and sliders. Each one lays out exactly like its
    /// GUILayout twin, then goes through <see cref="UiLayout.Item"/> under a name made from its
    /// window and text, so the menu layout editor (F7) can move and resize every piece of every
    /// menu. The Rect versions do the same for pieces placed by hand.
    /// </summary>
    public static class Ui
    {
        static Rect Piece(string kind, string text, Rect rect) => UiLayout.Active ? UiLayout.Item(UiLayout.AutoId(kind, text), rect) : rect;
        static Rect Piece(string kind, GUIContent content, Rect rect) => UiLayout.Active ? UiLayout.Item(UiLayout.AutoId(kind, Text(content)), rect) : rect;
        static string Text(GUIContent content) => content == null ? null : !string.IsNullOrEmpty(content.text) ? content.text : content.tooltip;

        // ------------------------------------------------------------ labels

        public static void Label(string text, params GUILayoutOption[] options) => Label(new GUIContent(text), GUI.skin.label, options);
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] options) => Label(new GUIContent(text), style, options);
        public static void Label(GUIContent content, params GUILayoutOption[] options) => Label(content, GUI.skin.label, options);
        public static void Label(Texture image, params GUILayoutOption[] options) => Label(new GUIContent(image), GUI.skin.label, options);
        public static void Label(Texture image, GUIStyle style, params GUILayoutOption[] options) => Label(new GUIContent(image), style, options);

        public static void Label(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            var rect = Piece("Label", content, GUILayoutUtility.GetRect(content, style, options));
            GUI.Label(rect, content, style);
        }

        /// <summary>A label whose text keeps changing (a tooltip line, a total), under a fixed name.</summary>
        public static void NamedLabel(string name, string text, GUIStyle style, params GUILayoutOption[] options)
        {
            var content = new GUIContent(text);
            var rect = Piece(name, name, GUILayoutUtility.GetRect(content, style, options));
            GUI.Label(rect, content, style);
        }

        public static void Label(Rect rect, string text, GUIStyle style) => GUI.Label(Piece("Label", text, rect), text, style);
        public static void Label(Rect rect, GUIContent content, GUIStyle style) => GUI.Label(Piece("Label", content, rect), content, style);
        public static void Box(Rect rect, string text, GUIStyle style) => GUI.Box(Piece("Box", text, rect), text, style);
        public static void Box(Rect rect, GUIContent content, GUIStyle style) => GUI.Box(Piece("Box", content, rect), content, style);

        // ------------------------------------------------------------ buttons

        public static bool Button(string text, params GUILayoutOption[] options) => Button(new GUIContent(text), GUI.skin.button, options);
        public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options) => Button(new GUIContent(text), style, options);
        public static bool Button(GUIContent content, params GUILayoutOption[] options) => Button(content, GUI.skin.button, options);
        public static bool Button(Texture image, params GUILayoutOption[] options) => Button(new GUIContent(image), GUI.skin.button, options);
        public static bool Button(Texture image, GUIStyle style, params GUILayoutOption[] options) => Button(new GUIContent(image), style, options);

        public static bool Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            var rect = Piece("Button", content, GUILayoutUtility.GetRect(content, style, options));
            return GUI.Button(rect, content, style);
        }

        public static bool Button(Rect rect, string text) => GUI.Button(Piece("Button", text, rect), text);
        public static bool Button(Rect rect, string text, GUIStyle style) => GUI.Button(Piece("Button", text, rect), text, style);
        public static bool Button(Rect rect, GUIContent content, GUIStyle style) => GUI.Button(Piece("Button", content, rect), content, style);

        // ------------------------------------------------------------ toggles, sliders, fields

        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) => Toggle(value, new GUIContent(text), GUI.skin.toggle, options);
        public static bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options) => Toggle(value, new GUIContent(text), style, options);
        public static bool Toggle(bool value, GUIContent content, params GUILayoutOption[] options) => Toggle(value, content, GUI.skin.toggle, options);

        public static bool Toggle(bool value, GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            var rect = Piece("Toggle", content, GUILayoutUtility.GetRect(content, style, options));
            return GUI.Toggle(rect, value, content, style);
        }

        static readonly GUIContent sliderSize = new GUIContent("mmmm");

        public static float HorizontalSlider(float value, float left, float right, params GUILayoutOption[] options)
        {
            var rect = Piece("Slider", (string)null, GUILayoutUtility.GetRect(sliderSize, GUI.skin.horizontalSlider, options));
            return GUI.HorizontalSlider(rect, value, left, right);
        }

        public static string TextField(string text, int maxLength, params GUILayoutOption[] options)
        {
            var rect = Piece("Text field", (string)null, GUILayoutUtility.GetRect(new GUIContent(text), GUI.skin.textField, options));
            return GUI.TextField(rect, text, maxLength);
        }

        public static int Toolbar(int selected, string[] texts, params GUILayoutOption[] options) => SelectionGrid(selected, texts, texts.Length, GUI.skin.button, options);
        public static int SelectionGrid(int selected, string[] texts, int xCount, params GUILayoutOption[] options) => SelectionGrid(selected, texts, xCount, GUI.skin.button, options);

        /// <summary>A grid of buttons (tabs), sized like GUILayout's: every cell as big as the biggest label.</summary>
        public static int SelectionGrid(int selected, string[] texts, int xCount, GUIStyle style, params GUILayoutOption[] options)
        {
            xCount = Mathf.Max(1, xCount);
            float cellW = 0f, cellH = 0f;
            foreach (var text in texts)
            {
                var size = style.CalcSize(new GUIContent(text));
                cellW = Mathf.Max(cellW, size.x);
                cellH = Mathf.Max(cellH, size.y);
            }
            int rows = Mathf.Max(1, Mathf.CeilToInt(texts.Length / (float)xCount));
            float spacing = Mathf.Max(style.margin.left, style.margin.right);
            float width = cellW * xCount + spacing * (xCount - 1);
            float height = cellH * rows + Mathf.Max(style.margin.top, style.margin.bottom) * (rows - 1);
            var rect = Piece("Tabs", string.Join(" ", texts), GUILayoutUtility.GetRect(width, height, style, options));
            return GUI.SelectionGrid(rect, selected, texts, xCount, style);
        }

        // ------------------------------------------------------------ bars and pictures

        /// <summary>A layout slot for a bar, picture or card drawn by hand, named so the editor can move it.</summary>
        public static Rect Slot(string name, float width, float height, params GUILayoutOption[] options) =>
            Piece(name, name, GUILayoutUtility.GetRect(width, height, options));
    }
}
