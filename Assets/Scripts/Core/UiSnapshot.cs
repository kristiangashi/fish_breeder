#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishBreeder.Core
{
    /// <summary>
    /// Editor only (F6 while playing): saves every screen showing right now, exactly as it is, as a
    /// UXML file in Assets/UI Previews, so it can be opened in UI Builder. Most screens are filled
    /// by code each frame, so their own UXML files are nearly empty boxes; a preview shows the real
    /// thing with the same classes. Changing a class's look in FishBreeder.uss (UI Builder's
    /// StyleSheets panel) changes the game; moving or editing the preview's own elements doesn't.
    /// </summary>
    public static class UiSnapshot
    {
        public const string Folder = "Assets/UI Previews";

        public static void SaveAll()
        {
            Directory.CreateDirectory(Folder);
            int saved = 0;
            foreach (var root in UiPanels.ShowingRoots())
            {
                var size = root.panel.visualTree.layout.size;
                var xml = new StringBuilder();
                xml.AppendLine("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" editor-extension-mode=\"False\">");
                xml.AppendLine("    <Style src=\"project://database/Assets/Resources/UI/FishBreeder.uss\" />");
                // The screen's size at the moment of the snapshot (it's always 900 tall).
                xml.AppendLine($"    <ui:VisualElement name=\"screen\" style=\"width: {size.x:0}px; height: {size.y:0}px; flex-shrink: 0;\">");
                foreach (var child in root.Children()) Write(xml, child, 2);
                xml.AppendLine("    </ui:VisualElement>");
                xml.AppendLine("</ui:UXML>");
                string name = root.name.Replace("-container", "");
                foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                File.WriteAllText(Path.Combine(Folder, name + ".uxml"), xml.ToString());
                saved++;
            }
            UnityEditor.AssetDatabase.Refresh();
            GameLog.Add($"UI previews (editor): {saved} screen{(saved == 1 ? "" : "s")} saved to {Folder}. Open them in UI Builder.");
        }

        static void Write(StringBuilder xml, VisualElement e, int depth)
        {
            string tag = e is Button ? "ui:Button" : e is Label ? "ui:Label" : e is ScrollView ? "ui:ScrollView" : e is Image ? "ui:Image"
                : e is Toggle ? "ui:Toggle" : e is Slider ? "ui:Slider" : e is TextField ? "ui:TextField" : "ui:VisualElement";
            var line = new StringBuilder(new string(' ', depth * 4)).Append('<').Append(tag);
            if (!string.IsNullOrEmpty(e.name) && !e.name.StartsWith("unity-")) line.Append($" name=\"{Escape(e.name)}\"");
            if (e is TextElement text && !(e is Toggle) && !string.IsNullOrEmpty(text.text)) line.Append($" text=\"{Escape(text.text)}\"");
            if (e is Toggle toggle && !string.IsNullOrEmpty(toggle.text)) line.Append($" text=\"{Escape(toggle.text)}\"");
            if (e.pickingMode == PickingMode.Ignore) line.Append(" picking-mode=\"Ignore\"");
            var classes = new StringBuilder();
            foreach (var c in e.GetClasses())
                if (!c.StartsWith("unity-")) classes.Append(classes.Length > 0 ? " " : "").Append(c);
            if (classes.Length > 0) line.Append($" class=\"{classes}\"");
            string style = InlineStyle(e);
            if (style.Length > 0) line.Append($" style=\"{style}\"");
            // Controls draw their own insides; scroll lists keep their content in a container.
            var children = e is ScrollView scroll ? scroll.contentContainer : e is Toggle || e is Slider || e is TextField ? null : e;
            if (children == null || children.childCount == 0)
            {
                xml.Append(line).AppendLine(" />");
                return;
            }
            xml.Append(line).AppendLine(">");
            foreach (var child in children.Children()) Write(xml, child, depth + 1);
            xml.Append(' ', depth * 4).Append("</").Append(tag).AppendLine(">");
        }

        /// <summary>The styles code set on the element (positions, sizes, colours), as USS.</summary>
        static string InlineStyle(VisualElement e)
        {
            var s = e.style;
            var parts = new StringBuilder();
            void Add(string property, string value) => parts.Append(property).Append(": ").Append(value).Append("; ");
            void Size(string property, StyleLength length)
            {
                if (length.keyword != StyleKeyword.Undefined) return;
                var v = length.value;
                Add(property, v.unit == LengthUnit.Percent ? $"{v.value:0.##}%" : $"{v.value:0.##}px");
            }
            void Colour(string property, StyleColor color)
            {
                if (color.keyword != StyleKeyword.Undefined) return;
                var c = color.value;
                Add(property, $"rgba({c.r * 255f:0}, {c.g * 255f:0}, {c.b * 255f:0}, {c.a:0.##})");
            }
            if (s.position.keyword == StyleKeyword.Undefined && s.position.value == Position.Absolute) Add("position", "absolute");
            Size("left", s.left);
            Size("top", s.top);
            Size("right", s.right);
            Size("bottom", s.bottom);
            Size("width", s.width);
            Size("height", s.height);
            if (s.display.keyword == StyleKeyword.Undefined && s.display.value == DisplayStyle.None) Add("display", "none");
            if (s.opacity.keyword == StyleKeyword.Undefined) Add("opacity", $"{s.opacity.value:0.##}");
            if (s.fontSize.keyword == StyleKeyword.Undefined) Add("font-size", $"{s.fontSize.value.value:0}px");
            Colour("background-color", s.backgroundColor);
            Colour("color", s.color);
            return parts.ToString().Trim();
        }

        static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("\n", "&#10;");
    }
}
#endif
