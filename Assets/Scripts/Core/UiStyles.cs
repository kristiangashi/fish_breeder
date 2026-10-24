using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>Shared styles for the placeholder IMGUI screens, in the ocean theme (see <see cref="UiTheme"/>).</summary>
    public static class UiStyles
    {
        static GUIStyle window;

        /// <summary>A near-opaque lagoon window with an aqua rim, so text stays readable over the store.</summary>
        public static GUIStyle Window
        {
            get
            {
                if (window == null || window.normal.background == null)
                {
                    var skin = UiTheme.Skin;
                    window = new GUIStyle(skin.box) { richText = true };
                    window.normal.background = skin.window.normal.background;
                    window.border = skin.window.border;
                    window.padding = new RectOffset(14, 14, 12, 12);
                }
                return window;
            }
        }

        /// <summary>Forgets cached styles after the theme is rebuilt.</summary>
        public static void Rebuild() => window = null;
    }
}
