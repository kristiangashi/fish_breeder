using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Hand-tuned nudges for the menus. Every window (anything drawn through <see cref="UiCheck"/>)
    /// and every named item (<see cref="Item"/>, like the XP bars) can be moved and resized by a
    /// saved offset, kept in Resources/UiLayout.json so builds ship with it.
    /// The layout editor (F7, in the editor and in dev mode) shows an outline round each of them:
    /// drag a window by its name tag (or hold Ctrl and drag anywhere in it), drag the corner square
    /// to resize it, drag an item to move it (Shift-drag resizes it), and right-click a tag or item
    /// to put it back where the code puts it. Every change is saved straight away.
    /// Drags snap: an edge or middle that comes near another window's (or, for a piece, another
    /// piece's in the same window) lines up with it and shows a guide line, and otherwise the
    /// corner snaps to an 8 px grid. Hold Alt to drag freely. The arrow keys nudge the last thing
    /// touched by a pixel (Shift: 8), Ctrl with the arrows resizes it.
    /// </summary>
    public static class UiLayout
    {
        [Serializable]
        class Entry
        {
            public string id;
            public float x, y, width, height;
        }

        [Serializable]
        class SavedLayout
        {
            public List<Entry> entries = new List<Entry>();
        }

        const string ResourceName = "UiLayout";
        static Dictionary<string, Rect> offsets;

        /// <summary>The layout editor is showing (F7).</summary>
        public static bool Editing { get; set; }
        /// <summary>Items (bars and labels inside windows) get outlines too, not just windows.</summary>
        public static bool ShowItems = true;

        static string dragging;
        static bool resizing;
        static Vector2 dragFrom;
        static Rect dragStart;

        /// <summary>The last window or item touched, shown in the editor's bar.</summary>
        public static string LastTouched { get; private set; }

        /// <summary>Drags line up with other edges and the grid (hold Alt to drag freely).</summary>
        public static bool Snap = true;
        /// <summary>The editor draws the grid over the screen.</summary>
        public static bool ShowGrid = true;
        public const float GridSize = 8f;
        // How close (in GUI pixels) an edge has to come to another to line up with it.
        const float EdgeReach = 6f;

        // Where every window and item was last drawn, so drags can line up with them. Windows are
        // in screen GUI space (parent null), items in their window's space.
        struct Drawn
        {
            public Rect rect;
            public string parent;
            public int frame;
        }
        static readonly Dictionary<string, Drawn> drawn = new Dictionary<string, Drawn>();
        static string currentWindow;
        // The guide lines for the drag going on, in the dragged thing's space.
        static readonly List<Rect> guides = new List<Rect>();

        /// <summary>Where the last touched window or item is now (in its own space), for the editor's bar.</summary>
        public static Rect? LastRect => LastTouched != null && drawn.TryGetValue(LastTouched, out var d) ? d.rect : (Rect?)null;

        static Dictionary<string, Rect> Offsets
        {
            get
            {
                if (offsets == null) Load();
                return offsets;
            }
        }

        static void Load()
        {
            offsets = new Dictionary<string, Rect>();
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return;
            try
            {
                var saved = JsonUtility.FromJson<SavedLayout>(asset.text);
                if (saved?.entries == null) return;
                foreach (var entry in saved.entries)
                    if (!string.IsNullOrEmpty(entry.id)) offsets[entry.id] = new Rect(entry.x, entry.y, entry.width, entry.height);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FishBreeder] Couldn't read UiLayout.json: " + e.Message);
            }
        }

        static void Save()
        {
#if UNITY_EDITOR
            var saved = new SavedLayout();
            var ids = new List<string>(offsets.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                var r = offsets[id];
                if (r == Rect.zero) continue;
                saved.entries.Add(new Entry { id = id, x = Round(r.x), y = Round(r.y), width = Round(r.width), height = Round(r.height) });
            }
            string path = System.IO.Path.Combine(Application.dataPath, "Resources", ResourceName + ".json");
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(saved, true));
            UnityEditor.AssetDatabase.ImportAsset("Assets/Resources/" + ResourceName + ".json");
#endif
        }

        static float Round(float v) => Mathf.Round(v);

        static Rect Apply(string id, Rect rect)
        {
            if (!Offsets.TryGetValue(id, out var o)) return rect;
            return new Rect(rect.x + o.x, rect.y + o.y, Mathf.Max(8f, rect.width + o.width), Mathf.Max(4f, rect.height + o.height));
        }

        /// <summary>Puts one window or item back where the code puts it.</summary>
        public static void Reset(string id)
        {
            if (Offsets.Remove(id)) Save();
        }

        public static void ResetAll()
        {
            Offsets.Clear();
            Save();
        }

        public static int Count => Offsets.Count;

        // ------------------------------------------------------------ windows

        /// <summary>
        /// A window's rect with its saved nudge. While editing, its name tag and corner take mouse
        /// drags (call before the window's content, in the window's parent space).
        /// </summary>
        public static Rect Window(string id, Rect rect)
        {
            var moved = Apply(id, rect);
            if (!Editing) return moved;
            var e = Event.current;
            var tag = TagRect(id, moved);
            var corner = new Rect(moved.xMax - 12f, moved.yMax - 12f, 12f, 12f);
            if (e.type == EventType.MouseDown)
            {
                if (e.button == 1 && tag.Contains(e.mousePosition))
                {
                    Reset(id);
                    LastTouched = id;
                    e.Use();
                    return rect;
                }
                if (e.button == 0 && (tag.Contains(e.mousePosition) || corner.Contains(e.mousePosition) || (e.control && moved.Contains(e.mousePosition))))
                    StartDrag(id, corner.Contains(e.mousePosition), e);
            }
            HandleDrag(id, rect, null, e);
            moved = Apply(id, rect);
            Record(id, moved, null);
            currentWindow = id;
            return moved;
        }

        /// <summary>Draws the window's outline, name tag and corner while editing (call after the window).</summary>
        public static void WindowOutline(string id, Rect moved)
        {
            if (currentWindow == id) currentWindow = null;
            if (!Editing || Event.current.type != EventType.Repaint) return;
            var color = dragging == id ? UiTheme.Sun : new Color(1f, 0.45f, 0.6f, 0.9f);
            Outline(moved, color, 2f);
            var tag = TagRect(id, moved);
            GUI.DrawTexture(tag, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(color.r, color.g, color.b, 0.85f), 0f, 3f);
            GUI.Label(tag, id, TagStyle());
            GUI.DrawTexture(new Rect(moved.xMax - 12f, moved.yMax - 12f, 12f, 12f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 2f);
            if (dragging == id) DrawGuides();
        }

        static Rect TagRect(string id, Rect window)
        {
            float w = Mathf.Min(window.width, TagStyle().CalcSize(new GUIContent(id)).x + 8f);
            return new Rect(window.x, window.y, w, 16f);
        }

        // ------------------------------------------------------------ items

        /// <summary>
        /// A named piece inside a window (an XP bar, a label) with its saved nudge. While editing
        /// (with items shown) it gets a thin outline and takes drags itself.
        /// </summary>
        public static Rect Item(string id, Rect rect)
        {
            var moved = Apply(id, rect);
            if (!Editing || !ShowItems) return moved;
            string parent = currentWindow ?? "";
            var e = Event.current;
            if (e.type == EventType.MouseDown && moved.Contains(e.mousePosition) && !e.control)
            {
                if (e.button == 1)
                {
                    Reset(id);
                    LastTouched = id;
                    e.Use();
                    return rect;
                }
                if (e.button == 0) StartDrag(id, e.shift, e);
            }
            HandleDrag(id, rect, parent, e);
            moved = Apply(id, rect);
            Record(id, moved, parent);
            if (e.type == EventType.Repaint)
            {
                var r = new Rect(moved.x - 1f, moved.y - 1f, moved.width + 2f, moved.height + 2f);
                Outline(r, dragging == id ? UiTheme.Sun : new Color(0.4f, 1f, 0.9f, 0.85f), 1f);
                if (dragging == id) DrawGuides();
            }
            return moved;
        }

        // ------------------------------------------------------------ dragging

        static void StartDrag(string id, bool resize, Event e)
        {
            dragging = id;
            resizing = resize;
            dragFrom = e.mousePosition;
            dragStart = Offsets.TryGetValue(id, out var o) ? o : Rect.zero;
            LastTouched = id;
            e.Use();
        }

        static void HandleDrag(string id, Rect code, string parent, Event e)
        {
            if (dragging != id) return;
            if (e.type == EventType.MouseDrag)
            {
                var d = e.mousePosition - dragFrom;
                // Whole pixels, so text stays crisp.
                d = new Vector2(Mathf.Round(d.x), Mathf.Round(d.y));
                var offset = resizing
                    ? new Rect(dragStart.x, dragStart.y, dragStart.width + d.x, dragStart.height + d.y)
                    : new Rect(dragStart.x + d.x, dragStart.y + d.y, dragStart.width, dragStart.height);
                guides.Clear();
                if (Snap && !e.alt) offset = Snapped(id, code, offset, parent);
                Offsets[id] = offset;
                e.Use();
            }
            else if (e.rawType == EventType.MouseUp)
            {
                dragging = null;
                guides.Clear();
                Save();
                e.Use();
            }
        }

        static void Record(string id, Rect rect, string parent)
        {
            if (Event.current.type != EventType.Repaint) return;
            drawn[id] = new Drawn { rect = rect, parent = parent, frame = Time.frameCount };
        }

        /// <summary>
        /// The offset moved so the dragged rect lines up: with the nearest edge or middle of another
        /// window (or piece in the same window) within reach, else its corner on the grid. Moving
        /// lines up the left, middle and right (top, middle and bottom); resizing only the far edges.
        /// </summary>
        static Rect Snapped(string id, Rect code, Rect offset, string parent)
        {
            var r = new Rect(code.x + offset.x, code.y + offset.y, code.width + offset.width, code.height + offset.height);
            var others = new List<Rect>();
            foreach (var pair in drawn)
                if (pair.Key != id && pair.Value.parent == parent && pair.Value.frame >= Time.frameCount - 3)
                    others.Add(pair.Value.rect);

            float[] xs = resizing ? new[] { r.xMax } : new[] { r.xMin, r.center.x, r.xMax };
            float[] ys = resizing ? new[] { r.yMax } : new[] { r.yMin, r.center.y, r.yMax };
            float dx = Nearest(xs, others, true, out var lineX, out var otherX);
            float dy = Nearest(ys, others, false, out var lineY, out var otherY);
            if (float.IsNaN(dx)) dx = resizing ? Grid(r.xMax) - r.xMax : Grid(r.xMin) - r.xMin;
            if (float.IsNaN(dy)) dy = resizing ? Grid(r.yMax) - r.yMax : Grid(r.yMin) - r.yMin;

            var after = resizing ? new Rect(r.x, r.y, r.width + dx, r.height + dy) : new Rect(r.x + dx, r.y + dy, r.width, r.height);
            if (!float.IsNaN(lineX))
            {
                float top = Mathf.Min(after.yMin, otherX.yMin), bottom = Mathf.Max(after.yMax, otherX.yMax);
                guides.Add(new Rect(lineX, top - 6f, 1f, bottom - top + 12f));
            }
            if (!float.IsNaN(lineY))
            {
                float left = Mathf.Min(after.xMin, otherY.xMin), right = Mathf.Max(after.xMax, otherY.xMax);
                guides.Add(new Rect(left - 6f, lineY, right - left + 12f, 1f));
            }
            return resizing
                ? new Rect(offset.x, offset.y, offset.width + dx, offset.height + dy)
                : new Rect(offset.x + dx, offset.y + dy, offset.width, offset.height);
        }

        /// <summary>The smallest shift (within reach) that puts one of these lines on another rect's edge or middle, or NaN.</summary>
        static float Nearest(float[] mine, List<Rect> others, bool across, out float line, out Rect other)
        {
            float best = float.NaN;
            line = float.NaN;
            other = default;
            foreach (var o in others)
            {
                var theirs = across ? new[] { o.xMin, o.center.x, o.xMax } : new[] { o.yMin, o.center.y, o.yMax };
                foreach (var m in mine)
                    foreach (var t in theirs)
                    {
                        float shift = t - m;
                        if (Mathf.Abs(shift) > EdgeReach || (!float.IsNaN(best) && Mathf.Abs(shift) >= Mathf.Abs(best))) continue;
                        best = shift;
                        line = t;
                        other = o;
                    }
            }
            return float.IsNaN(best) ? best : Mathf.Round(best);
        }

        static float Grid(float v) => Mathf.Round(v / GridSize) * GridSize;

        static void DrawGuides()
        {
            foreach (var g in guides)
                GUI.DrawTexture(g, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(1f, 0.25f, 0.85f, 1f), 0f, 0f);
        }

        /// <summary>Moves (or, with resize, grows) the last touched window or item by whole pixels.</summary>
        public static void Nudge(Vector2 by, bool resize)
        {
            if (LastTouched == null) return;
            var o = Offsets.TryGetValue(LastTouched, out var old) ? old : Rect.zero;
            Offsets[LastTouched] = resize ? new Rect(o.x, o.y, o.width + by.x, o.height + by.y) : new Rect(o.x + by.x, o.y + by.y, o.width, o.height);
            Save();
        }

        /// <summary>Lets go of a drag that lost its window (it closed while dragging).</summary>
        public static void EndStrayDrag()
        {
            if (dragging == null) return;
            dragging = null;
            Save();
        }

        static void Outline(Rect r, Color color, float t)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
        }

        static GUIStyle tagStyle;
        static GUIStyle TagStyle() => tagStyle != null ? tagStyle : tagStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleLeft, padding = new RectOffset(4, 2, 0, 0), margin = new RectOffset(),
            normal = { textColor = new Color(0.05f, 0.1f, 0.15f) },
        };
    }

    /// <summary>
    /// The layout editor's switch and help bar: F7 (in the editor, or in dev mode) turns it on and
    /// off. See <see cref="UiLayout"/>.
    /// </summary>
    public class UiLayoutEditor : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F7) && DevMode.Available) UiLayout.Editing = !UiLayout.Editing;
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) UiLayout.EndStrayDrag();
        }

        void OnGUI()
        {
            if (!UiLayout.Editing) return;
            UiTheme.Apply();
            // Drawn after (over) the game's menus.
            GUI.depth = -100;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;
            var e = Event.current;
            if (e.type == EventType.KeyDown && UiLayout.LastTouched != null)
            {
                float step = e.shift ? UiLayout.GridSize : 1f;
                var by = e.keyCode == KeyCode.LeftArrow ? new Vector2(-step, 0f) : e.keyCode == KeyCode.RightArrow ? new Vector2(step, 0f)
                    : e.keyCode == KeyCode.UpArrow ? new Vector2(0f, -step) : e.keyCode == KeyCode.DownArrow ? new Vector2(0f, step) : Vector2.zero;
                if (by != Vector2.zero)
                {
                    UiLayout.Nudge(by, e.control);
                    e.Use();
                }
            }
            if (UiLayout.ShowGrid && e.type == EventType.Repaint) DrawGrid(width, height);

            var bar = new Rect(Mathf.Max(10f, width * 0.5f - 340f), height - 100f, Mathf.Min(680f, width - 20f), 90f);
            GUILayout.BeginArea(bar, UiStyles.Window);
            GUILayout.Label("<b>Menu layout editor (F7 to close)</b>  <size=11>Drag a window's pink name tag to move it (or Ctrl-drag anywhere in it), its corner square to resize. Drag a teal outline to move that piece, Shift-drag to resize. Edges snap to other edges (pink guide line) or the grid; hold Alt to drag freely. Arrow keys nudge the last one 1 px (Shift: 8, Ctrl: resize). Right-click resets. Saved as you go.</size>", Rich());
            GUILayout.BeginHorizontal();
            UiLayout.ShowItems = GUILayout.Toggle(UiLayout.ShowItems, " Pieces", GUILayout.Width(70f));
            UiLayout.Snap = GUILayout.Toggle(UiLayout.Snap, " Snap", GUILayout.Width(62f));
            UiLayout.ShowGrid = GUILayout.Toggle(UiLayout.ShowGrid, " Grid", GUILayout.Width(58f));
            GUILayout.FlexibleSpace();
            var r = UiLayout.LastRect;
            string last = UiLayout.LastTouched == null ? "" : $", last: {UiLayout.LastTouched}" + (r.HasValue ? $"  ({r.Value.x:0}, {r.Value.y:0}  {r.Value.width:0} x {r.Value.height:0})" : "");
            GUILayout.Label($"<size=11>{UiLayout.Count} nudged{last}</size>", Rich());
            if (GUILayout.Button("Reset all", GUILayout.Width(80f))) UiLayout.ResetAll();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>Faint lines every 8 px, stronger every 32, so things can be lined up by eye too.</summary>
        static void DrawGrid(float width, float height)
        {
            var fine = new Color(1f, 1f, 1f, 0.05f);
            var major = new Color(1f, 1f, 1f, 0.13f);
            float g = UiLayout.GridSize;
            for (int i = 0; i * g <= width; i++)
                GUI.DrawTexture(new Rect(i * g, 0f, 1f, height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, i % 4 == 0 ? major : fine, 0f, 0f);
            for (int i = 0; i * g <= height; i++)
                GUI.DrawTexture(new Rect(0f, i * g, width, 1f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, i % 4 == 0 ? major : fine, 0f, 0f);
        }

        static GUIStyle rich;
        static GUIStyle Rich() => rich != null ? rich : rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
    }
}
