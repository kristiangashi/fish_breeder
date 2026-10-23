using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>Shared styles for the placeholder IMGUI screens.</summary>
    public static class UiStyles
    {
        static GUIStyle window;
        static Texture2D windowBackground;

        /// <summary>A near-opaque dark box for pop-up windows, so text stays readable over the store.</summary>
        public static GUIStyle Window
        {
            get
            {
                if (window == null || windowBackground == null)
                {
                    windowBackground = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                    windowBackground.SetPixel(0, 0, new Color(0.11f, 0.12f, 0.14f, 0.95f));
                    windowBackground.Apply();
                    window = new GUIStyle(GUI.skin.box);
                    window.normal.background = windowBackground;
                    window.border = new RectOffset(0, 0, 0, 0);
                }
                return window;
            }
        }
    }
}
