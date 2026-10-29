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
            HandleDrag(id, e);
            return Apply(id, rect);
        }

        /// <summary>Draws the window's outline, name tag and corner while editing (call after the window).</summary>
        public static void WindowOutline(string id, Rect moved)
        {
            if (!Editing || Event.current.type != EventType.Repaint) return;
            var color = dragging == id ? UiTheme.Sun : new Color(1f, 0.45f, 0.6f, 0.9f);
            Outline(moved, color, 2f);
            var tag = TagRect(id, moved);
            GUI.DrawTexture(tag, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(color.r, color.g, color.b, 0.85f), 0f, 3f);
            GUI.Label(tag, id, TagStyle());
            GUI.DrawTexture(new Rect(moved.xMax - 12f, moved.yMax - 12f, 12f, 12f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 2f);
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
            HandleDrag(id, e);
            moved = Apply(id, rect);
            if (e.type == EventType.Repaint)
            {
                var r = new Rect(moved.x - 1f, moved.y - 1f, moved.width + 2f, moved.height + 2f);
                Outline(r, dragging == id ? UiTheme.Sun : new Color(0.4f, 1f, 0.9f, 0.85f), 1f);
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

        static void HandleDrag(string id, Event e)
        {
            if (dragging != id) return;
            if (e.type == EventType.MouseDrag)
            {
                var d = e.mousePosition - dragFrom;
                // Whole pixels, so text stays crisp.
                d = new Vector2(Mathf.Round(d.x), Mathf.Round(d.y));
                Offsets[id] = resizing
                    ? new Rect(dragStart.x, dragStart.y, dragStart.width + d.x, dragStart.height + d.y)
                    : new Rect(dragStart.x + d.x, dragStart.y + d.y, dragStart.width, dragStart.height);
                e.Use();
            }
            else if (e.rawType == EventType.MouseUp)
            {
                dragging = null;
                Save();
                e.Use();
            }
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
            var bar = new Rect(Mathf.Max(10f, width * 0.5f - 330f), height - 92f, Mathf.Min(660f, width - 20f), 82f);
            GUILayout.BeginArea(bar, UiStyles.Window);
            GUILayout.Label("<b>Menu layout editor (F7 to close)</b>  <size=11>Drag a window's pink name tag to move it (or Ctrl-drag anywhere in it), its corner square to resize. Drag a teal outline to move that piece, Shift-drag to resize. Right-click a tag or piece to reset it. Saved as you go.</size>", Rich());
            GUILayout.BeginHorizontal();
            UiLayout.ShowItems = GUILayout.Toggle(UiLayout.ShowItems, " Show pieces inside windows");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<size=11>{UiLayout.Count} nudged{(UiLayout.LastTouched != null ? ", last: " + UiLayout.LastTouched : "")}</size>", Rich());
            if (GUILayout.Button("Reset all", GUILayout.Width(80f))) UiLayout.ResetAll();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static GUIStyle rich;
        static GUIStyle Rich() => rich != null ? rich : rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
    }
}
