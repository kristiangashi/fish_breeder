using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The message log in the bottom-left corner. It shows the newest messages that fit, measured
    /// line by line with their wrapping, and grows upward from the bottom of its space, so a long
    /// message never runs off the edge.
    /// </summary>
    public static class GameLogPanel
    {
        /// <summary>Draws up to <paramref name="maxEntries"/> of the newest messages inside <paramref name="space"/>, sitting on its bottom edge.</summary>
        public static void Draw(string id, Rect space, int maxEntries)
        {
            var entries = GameLog.Entries;
            if (entries.Count == 0) return;
            var box = UiTheme.Bar;
            float inner = space.width - box.padding.horizontal;
            float used = box.padding.vertical;
            int first = entries.Count;
            while (first > 0 && entries.Count - first < maxEntries)
            {
                float h = Style().CalcHeight(new GUIContent(Line(entries, first - 1)), inner);
                if (used + h > space.height && first < entries.Count) break;
                used += h;
                first--;
            }
            var rect = new Rect(space.x, space.yMax - Mathf.Min(used, space.height), space.width, Mathf.Min(used, space.height));
            UiCheck.Begin(id, rect, box);
            for (int i = first; i < entries.Count; i++) Ui.Label(Line(entries, i), Style());
            UiCheck.End();
        }

        static string Line(System.Collections.Generic.IReadOnlyList<string> entries, int i)
            => i == entries.Count - 1 ? $"<b>{entries[i]}</b>" : $"<color=#bfe3e3>{entries[i]}</color>";

        static GUIStyle style;
        static GUIStyle Style() => style != null ? style : style = new GUIStyle(UiTheme.Skin.label) { richText = true, wordWrap = true, margin = new RectOffset(0, 0, 0, 0), padding = new RectOffset(0, 0, 1, 1) };
    }
}
