using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Every window and scroll list goes through here, so text can't run off an edge unnoticed.
    /// <see cref="Begin"/>/<see cref="End"/> wrap a GUILayout area and measure what its content
    /// needed each frame, so a window can grow to fit (<see cref="Height"/>). Scroll lists keep their
    /// content to the width that's really visible (minus the scrollbar, when there is one), so wrapped
    /// text never slides under the bar or past the edge. Fixed-size labels can go through
    /// <see cref="Label"/>. Anything that still doesn't fit is recorded in <see cref="Problems"/> and,
    /// in the editor, logged once as a "[UI overflow]" warning naming the window. The store's UI check
    /// (F8 in the editor) opens every window and tab in turn and reports what overflowed.
    /// </summary>
    public static class UiCheck
    {
        struct Open
        {
            public string id;
            public Rect rect;
            public RectOffset padding;
        }

        static readonly Stack<Open> open = new Stack<Open>();
        static readonly Dictionary<string, Vector2> measured = new Dictionary<string, Vector2>();
        static readonly Dictionary<string, float> viewWidths = new Dictionary<string, float>();
        static readonly Dictionary<string, bool> scrollBars = new Dictionary<string, bool>();
        static readonly HashSet<string> reported = new HashSet<string>();
        // Repaints in a row each id has overflowed. Windows that grow to fit are a frame behind
        // their content, so only an overflow that lasts a few frames counts.
        static readonly Dictionary<string, int> overflowing = new Dictionary<string, int>();
        const int FramesToCount = 3;

        /// <summary>Everything that overflowed since the last <see cref="ClearProblems"/>, one line each.</summary>
        public static readonly List<string> Problems = new List<string>();

        /// <summary>Counts every window and list drawn, for the UI check's report.</summary>
        public static readonly HashSet<string> Seen = new HashSet<string>();

        public static void ClearProblems()
        {
            Problems.Clear();
            reported.Clear();
            Seen.Clear();
        }

        /// <summary>Starts a window: a GUILayout area whose content is measured.</summary>
        public static void Begin(string id, Rect rect, GUIStyle style = null)
        {
            if (style != null) GUILayout.BeginArea(rect, style);
            else GUILayout.BeginArea(rect);
            open.Push(new Open { id = id, rect = rect, padding = style != null ? style.padding : new RectOffset() });
            GUILayout.BeginVertical();
        }

        public static void End()
        {
            GUILayout.EndVertical();
            var o = open.Pop();
            if (Event.current.type == EventType.Repaint)
            {
                var content = GUILayoutUtility.GetLastRect();
                var need = new Vector2(content.xMax + o.padding.right, content.yMax + o.padding.bottom);
                measured[o.id] = need;
                Seen.Add(o.id);
                bool tall = need.y > o.rect.height + 1.5f, wide = need.x > o.rect.width + 1.5f;
                if (Lasting(o.id, tall || wide))
                    Report(o.id, tall ? $"needs {need.y:0} high, has {o.rect.height:0}" : $"needs {need.x:0} wide, has {o.rect.width:0}");
            }
            GUILayout.EndArea();
        }

        /// <summary>How tall the window's content was last frame (with padding), or the fallback before it's been drawn.</summary>
        public static float Height(string id, float fallback) => measured.TryGetValue(id, out var size) ? size.y : fallback;

        /// <summary>
        /// Starts a vertical scroll list. Its content is laid out at exactly the visible width, less
        /// the scrollbar when the content is taller than the list, so nothing hides under the bar.
        /// </summary>
        public static Vector2 BeginScroll(string id, Vector2 scroll, params GUILayoutOption[] options)
        {
            bool bar = scrollBars.TryGetValue(id, out var b) && b;
            scroll = GUILayout.BeginScrollView(scroll, false, bar, GUIStyle.none, GUI.skin.verticalScrollbar, options);
            open.Push(new Open { id = id });
            if (viewWidths.TryGetValue(id, out var width) && width > 20f)
                GUILayout.BeginVertical(GUILayout.Width(width - (bar ? BarWidth : 0f)));
            else GUILayout.BeginVertical();
            return scroll;
        }

        public static void EndScroll()
        {
            GUILayout.EndVertical();
            var o = open.Pop();
            bool repaint = Event.current.type == EventType.Repaint;
            var content = repaint ? GUILayoutUtility.GetLastRect() : default;
            GUILayout.EndScrollView();
            if (!repaint) return;
            var view = GUILayoutUtility.GetLastRect();
            Seen.Add(o.id);
            bool hadBar = scrollBars.TryGetValue(o.id, out var b) && b;
            // A little give either way, so the bar doesn't flicker on and off at the edge.
            bool bar = hadBar ? content.height > view.height - 24f : content.height > view.height + 1f;
            scrollBars[o.id] = bar;
            bool settled = viewWidths.TryGetValue(o.id, out var oldWidth) && Mathf.Abs(oldWidth - view.width) < 0.5f && bar == hadBar;
            viewWidths[o.id] = view.width;
            float visible = view.width - (bar ? BarWidth : 0f);
            if (Lasting(o.id, settled && content.width > visible + 1.5f)) Report(o.id, $"content {content.width:0} wide, only {visible:0} visible");
        }

        static float BarWidth
        {
            get
            {
                var style = GUI.skin.verticalScrollbar;
                return (style.fixedWidth > 0f ? style.fixedWidth : 12f) + style.margin.horizontal + 2f;
            }
        }

        /// <summary>A label in a fixed rect; records it if the text doesn't fit.</summary>
        public static void Label(string id, Rect rect, GUIContent content, GUIStyle style)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Seen.Add(id);
                if (style.wordWrap)
                {
                    float h = style.CalcHeight(content, rect.width);
                    if (h > rect.height + 1.5f) Report(id, $"text needs {h:0} high, has {rect.height:0}");
                }
                else
                {
                    var size = style.CalcSize(content);
                    if (size.x > rect.width + 1.5f) Report(id, $"text needs {size.x:0} wide, has {rect.width:0}");
                }
            }
            GUI.Label(rect, content, style);
        }

        static bool Lasting(string id, bool over)
        {
            int frames = over && overflowing.TryGetValue(id, out var n) ? n + 1 : over ? 1 : 0;
            overflowing[id] = frames;
            return frames >= FramesToCount;
        }

        static void Report(string id, string what)
        {
            if (!reported.Add(id)) return;
            string line = $"{id}: {what} (screen {Screen.width}x{Screen.height})";
            Problems.Add(line);
#if UNITY_EDITOR
            Debug.LogWarning("[UI overflow] " + line);
#endif
        }
    }
}
