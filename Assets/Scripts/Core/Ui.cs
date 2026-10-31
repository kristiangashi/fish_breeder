using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The menus' buttons, labels, toggles and sliders. Each one lays out exactly like its
    /// GUILayout twin, then goes through <see cref="UiLayout.Item"/> under a name made from its
    /// window and text, so the menu layout editor (F7) can move and resize every piece of every
    /// menu. The Rect versions do the same for pieces placed by hand. Labels, buttons and toggles
    /// can also be given new text or hidden in the editor (a hidden one takes no room). While the
    /// editor is open nothing here reacts: buttons don't click, toggles and tabs don't change, so
    /// pieces can be picked and dragged without setting anything off.
    /// </summary>
    public static class Ui
    {
        static Rect Piece(string kind, string text, Rect rect) => UiLayout.Active ? UiLayout.Item(UiLayout.AutoId(kind, text), rect) : rect;
        static Rect Piece(string id, Rect rect) => id != null ? UiLayout.Item(id, rect) : rect;
        static string Text(GUIContent content) => content == null ? null : !string.IsNullOrEmpty(content.text) ? content.text : content.tooltip;

        /// <summary>
        /// Names the piece and swaps in its new text from the editor. False when it's hidden: draw
        /// nothing (and, in a layout, take no room).
        /// </summary>
        static bool Prepare(string kind, ref GUIContent content, out string id)
        {
            id = null;
            if (!UiLayout.Active) return true;
            id = UiLayout.AutoId(kind, Text(content));
            return UiLayout.Content(id, ref content);
        }

        /// <summary>Hidden pieces show faintly while the editor is open, so they can be brought back.</summary>
        static Color Fade(string id)
        {
            var old = GUI.color;
            if (id != null && UiLayout.Editing && UiLayout.IsHidden(id)) GUI.color = new Color(old.r, old.g, old.b, old.a * 0.3f);
            return old;
        }

        // ------------------------------------------------------------ labels

        public static void Label(string text, params GUILayoutOption[] options) => Label(new GUIContent(text), GUI.skin.label, options);
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] options) => Label(new GUIContent(text), style, options);
        public static void Label(GUIContent content, params GUILayoutOption[] options) => Label(content, GUI.skin.label, options);
        public static void Label(Texture image, params GUILayoutOption[] options) => Label(new GUIContent(image), GUI.skin.label, options);
        public static void Label(Texture image, GUIStyle style, params GUILayoutOption[] options) => Label(new GUIContent(image), style, options);

        public static void Label(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            if (!Prepare("Label", ref content, out var id)) return;
            var rect = Piece(id, GUILayoutUtility.GetRect(content, style, options));
            var old = Fade(id);
            GUI.Label(rect, content, style);
            GUI.color = old;
        }

        /// <summary>A label whose text keeps changing (a tooltip line, a total), under a fixed name.</summary>
        public static void NamedLabel(string name, string text, GUIStyle style, params GUILayoutOption[] options)
        {
            var content = new GUIContent(text);
            string id = null;
            if (UiLayout.Active)
            {
                id = UiLayout.AutoId(name, name);
                if (!UiLayout.Content(id, ref content)) return;
            }
            var rect = Piece(id, GUILayoutUtility.GetRect(content, style, options));
            var old = Fade(id);
            GUI.Label(rect, content, style);
            GUI.color = old;
        }

        public static void Label(Rect rect, string text, GUIStyle style) => Label(rect, new GUIContent(text), style);
        public static void Box(Rect rect, string text, GUIStyle style) => Box(rect, new GUIContent(text), style);

        public static void Label(Rect rect, GUIContent content, GUIStyle style)
        {
            if (!Prepare("Label", ref content, out var id)) return;
            var old = Fade(id);
            GUI.Label(Piece(id, rect), content, style);
            GUI.color = old;
        }

        public static void Box(Rect rect, GUIContent content, GUIStyle style)
        {
            if (!Prepare("Box", ref content, out var id)) return;
            var old = Fade(id);
            GUI.Box(Piece(id, rect), content, style);
            GUI.color = old;
        }

        // ------------------------------------------------------------ buttons

        public static bool Button(string text, params GUILayoutOption[] options) => Button(new GUIContent(text), GUI.skin.button, options);
        public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options) => Button(new GUIContent(text), style, options);
        public static bool Button(GUIContent content, params GUILayoutOption[] options) => Button(content, GUI.skin.button, options);
        public static bool Button(Texture image, params GUILayoutOption[] options) => Button(new GUIContent(image), GUI.skin.button, options);
        public static bool Button(Texture image, GUIStyle style, params GUILayoutOption[] options) => Button(new GUIContent(image), style, options);

        public static bool Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            if (!Prepare("Button", ref content, out var id)) return false;
            var rect = Piece(id, GUILayoutUtility.GetRect(content, style, options));
            var old = Fade(id);
            bool clicked = GUI.Button(rect, content, style);
            GUI.color = old;
            return clicked && !UiLayout.Editing;
        }

        public static bool Button(Rect rect, string text) => Button(rect, new GUIContent(text), GUI.skin.button);
        public static bool Button(Rect rect, string text, GUIStyle style) => Button(rect, new GUIContent(text), style);

        public static bool Button(Rect rect, GUIContent content, GUIStyle style)
        {
            if (!Prepare("Button", ref content, out var id)) return false;
            var old = Fade(id);
            bool clicked = GUI.Button(Piece(id, rect), content, style);
            GUI.color = old;
            return clicked && !UiLayout.Editing;
        }

        // ------------------------------------------------------------ toggles, sliders, fields

        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) => Toggle(value, new GUIContent(text), GUI.skin.toggle, options);
        public static bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options) => Toggle(value, new GUIContent(text), style, options);
        public static bool Toggle(bool value, GUIContent content, params GUILayoutOption[] options) => Toggle(value, content, GUI.skin.toggle, options);

        public static bool Toggle(bool value, GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            if (!Prepare("Toggle", ref content, out var id)) return value;
            var rect = Piece(id, GUILayoutUtility.GetRect(content, style, options));
            var old = Fade(id);
            bool now = GUI.Toggle(rect, value, content, style);
            GUI.color = old;
            return UiLayout.Editing ? value : now;
        }

        static readonly GUIContent sliderSize = new GUIContent("mmmm");

        public static float HorizontalSlider(float value, float left, float right, params GUILayoutOption[] options)
        {
            var rect = Piece("Slider", (string)null, GUILayoutUtility.GetRect(sliderSize, GUI.skin.horizontalSlider, options));
            float now = GUI.HorizontalSlider(rect, value, left, right);
            return UiLayout.Editing ? value : now;
        }

        public static string TextField(string text, int maxLength, params GUILayoutOption[] options)
        {
            var rect = Piece("Text field", (string)null, GUILayoutUtility.GetRect(new GUIContent(text), GUI.skin.textField, options));
            // Drawn without taking the keyboard while editing, so the editor's keys still work.
            if (UiLayout.Editing)
            {
                GUI.Label(rect, text, GUI.skin.textField);
                return text;
            }
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
            int now = GUI.SelectionGrid(rect, selected, texts, xCount, style);
            return UiLayout.Editing ? selected : now;
        }

        // ------------------------------------------------------------ bars and pictures

        /// <summary>A layout slot for a bar, picture or card drawn by hand, named so the editor can move it.</summary>
        public static Rect Slot(string name, float width, float height, params GUILayoutOption[] options) =>
            Piece(name, name, GUILayoutUtility.GetRect(width, height, options));
    }
}
