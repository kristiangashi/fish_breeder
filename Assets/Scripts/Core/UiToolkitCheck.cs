using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishBreeder.Core
{
    /// <summary>
    /// The overflow check for every screen.
    /// <see cref="Check"/> walks everything showing and records, in <see cref="UiCheck.Problems"/>:
    /// anything partly off the screen, anything poking out of the box it sits in, and text that needs
    /// more room than its box gives it. Inside a scroll list only the sides count, since the list
    /// scrolls up and down.
    ///
    /// <see cref="Sweep"/> lays each page of a screen out at five screen sizes, from 1280x720 to a
    /// 3440x1440 ultrawide, checks it and saves a picture of each (UiCheck/ next to Assets in the
    /// editor, the save folder's UiCheck in a build). The menus run it with F8 in the editor and in
    /// development builds. Elements with the class "fb-no-check" (the main menu's bubbles) are skipped.
    /// </summary>
    public static class UiToolkitCheck
    {
        public const string SkipClass = "fb-no-check";
        const float Give = 1.5f;

        public static readonly Vector2Int[] SweepSizes =
        {
            new Vector2Int(1280, 720), new Vector2Int(1366, 768), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3440, 1440),
        };

        public static string PictureFolder => Application.isEditor
            ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "UiCheck")
            : Path.Combine(Application.persistentDataPath, "UiCheck");

        /// <summary>True while a sweep has the screens drawing off screen.</summary>
        public static bool Sweeping { get; private set; }

        /// <summary>
        /// Checks everything showing under <paramref name="root"/> (null: every UI Toolkit screen
        /// showing); returns how many problems it found.
        /// </summary>
        public static int Check(VisualElement root, string screen)
        {
            if (root == null)
            {
                int all = 0;
                foreach (var showing in UiPanels.ShowingRoots()) all += Check(showing, screen);
                return all;
            }
            if (root.panel == null) return 0;
            var panel = root.panel.visualTree.worldBound;
            string size = $"{panel.width:0}x{panel.height:0} UI units";
            int found = 0;
            Walk(root, screen, size, panel, null, ref found);
            UiCheck.Seen.Add(screen);
            return found;
        }

        static void Walk(VisualElement e, string screen, string size, Rect panel, Rect? scrollSides, ref int found)
        {
            var style = e.resolvedStyle;
            if (style.display == DisplayStyle.None || e.ClassListContains(SkipClass)) return;
            var r = e.worldBound;
            bool visible = style.visibility == Visibility.Visible && style.opacity > 0.01f && r.width > 0.5f && r.height > 0.5f;
            if (visible)
            {
                string what = null;
                var parent = e.hierarchy.parent;
                if (scrollSides.HasValue)
                {
                    var sides = scrollSides.Value;
                    if (r.xMin < sides.xMin - Give || r.xMax > sides.xMax + Give) what = $"runs {Mathf.Max(sides.xMin - r.xMin, r.xMax - sides.xMax):0} past the side of its scroll list";
                }
                else if (r.xMin < panel.xMin - Give || r.yMin < panel.yMin - Give || r.xMax > panel.xMax + Give || r.yMax > panel.yMax + Give)
                    what = $"runs off the screen ({r.width:0}x{r.height:0} at {r.x:0},{r.y:0})";
                if (what == null && parent != null && style.position == Position.Relative && !IsScrollInside(parent))
                {
                    var p = parent.worldBound;
                    if (r.xMin < p.xMin - Give || r.xMax > p.xMax + Give || r.yMin < p.yMin - Give || r.yMax > p.yMax + Give)
                        what = $"pokes out of {Name(parent)} ({r.width:0}x{r.height:0} in {p.width:0}x{p.height:0})";
                }
                if (what == null && e is TextElement text && !string.IsNullOrEmpty(text.text)) what = TextProblem(text);
                if (what != null)
                {
                    found++;
                    string id = $"{screen} / {Where(e)}";
                    UiCheck.Record(id, $"{id}: {what} ({size})");
                }
            }
            // A scroll list's content may run past its top and bottom, not its sides.
            if (e.ClassListContains(ScrollView.viewportUssClassName)) scrollSides = r;
            foreach (var child in e.hierarchy.Children()) Walk(child, screen, size, panel, scrollSides, ref found);
        }

        static bool IsScrollInside(VisualElement parent) =>
            parent.ClassListContains(ScrollView.viewportUssClassName) || parent.ClassListContains(ScrollView.contentUssClassName);

        static string TextProblem(TextElement text)
        {
            var box = text.contentRect;
            bool wraps = text.resolvedStyle.whiteSpace == WhiteSpace.Normal || text.resolvedStyle.whiteSpace == WhiteSpace.PreWrap;
            if (wraps)
            {
                var need = text.MeasureTextSize(text.text, box.width + Give, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined);
                if (need.y > box.height + Give) return $"text needs {need.y:0} high, has {box.height:0}";
            }
            else
            {
                var need = text.MeasureTextSize(text.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
                if (need.x > box.width + Give) return $"text needs {need.x:0} wide, has {box.width:0}";
                if (need.y > box.height + Give) return $"text needs {need.y:0} high, has {box.height:0}";
            }
            return null;
        }

        static string Name(VisualElement e)
        {
            if (!string.IsNullOrEmpty(e.name)) return e.name;
            foreach (var c in e.GetClasses()) return "." + c;
            return e.GetType().Name;
        }

        /// <summary>The element's named ancestors and its own name or text, like "load-page > save-list > load".</summary>
        static string Where(VisualElement e)
        {
            var parts = new List<string>();
            if (e is TextElement t && !string.IsNullOrEmpty(t.text))
            {
                string plain = System.Text.RegularExpressions.Regex.Replace(t.text, "<[^>]+>", "").Replace('\n', ' ');
                parts.Add($"\"{(plain.Length > 32 ? plain.Substring(0, 32) + "…" : plain)}\"");
            }
            else parts.Add(Name(e));
            for (var p = e.hierarchy.parent; p != null && parts.Count < 4; p = p.hierarchy.parent)
                if (!string.IsNullOrEmpty(p.name) && !p.name.StartsWith("unity-")) parts.Insert(0, p.name);
            return string.Join(" > ", parts);
        }

        /// <summary>
        /// Shows each page at each screen size in <see cref="SweepSizes"/>, checks it and saves a
        /// picture. <paramref name="root"/> is the screen's root element; <paramref name="done"/> puts
        /// the screen back as it was.
        /// </summary>
        public static IEnumerator Sweep(string screen, VisualElement root, IList<KeyValuePair<string, Action>> pages, Action done)
        {
            if (Sweeping) yield break;
            Sweeping = true;
            UiCheck.ClearProblems();
            var settings = UiPanels.Settings;
            bool clear = settings.clearColor;
            var clearValue = settings.colorClearValue;
            settings.clearColor = true;
            settings.colorClearValue = UiTheme.Abyss;
            string folder = PictureFolder;
            Directory.CreateDirectory(folder);
            int pictures = 0;
            try
            {
                foreach (var size in SweepSizes)
                {
                    var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32) { name = "UI check" };
                    target.Create();
                    settings.targetTexture = target;
                    foreach (var page in pages)
                    {
                        page.Value();
                        // A few frames for the new size and page to lay out and draw.
                        for (int i = 0; i < 3; i++) yield return null;
                        yield return new WaitForEndOfFrame();
                        Check(root, $"{screen}: {page.Key} at {size.x}x{size.y}");
                        Picture(target, Path.Combine(folder, FileName($"{screen} - {page.Key} - {size.x}x{size.y}.png")));
                        pictures++;
                    }
                    settings.targetTexture = null;
                    target.Release();
                    UnityEngine.Object.Destroy(target);
                }
            }
            finally
            {
                settings.targetTexture = null;
                settings.clearColor = clear;
                settings.colorClearValue = clearValue;
                Sweeping = false;
                done?.Invoke();
            }
            int problems = UiCheck.Problems.Count;
            Debug.Log($"[UI check] {screen}: {pages.Count} pages at {SweepSizes.Length} screen sizes, {problems} problem{(problems == 1 ? "" : "s")}. {pictures} pictures in {folder}"
                + (problems > 0 ? "\n" + string.Join("\n", UiCheck.Problems) : ""));
        }

        static string FileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        static void Picture(RenderTexture source, string path)
        {
            var old = RenderTexture.active;
            RenderTexture.active = source;
            var picture = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            picture.Apply();
            RenderTexture.active = old;
            File.WriteAllBytes(path, picture.EncodeToPNG());
            UnityEngine.Object.Destroy(picture);
        }
    }
}
