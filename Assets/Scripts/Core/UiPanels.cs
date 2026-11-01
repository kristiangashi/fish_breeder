using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace FishBreeder.Core
{
    /// <summary>
    /// Shows UI Toolkit screens. Their layouts are UXML files in Resources/UI (open them in Window >
    /// UI Toolkit > UI Builder to move or restyle things) and the look is Resources/UI/FishBreeder.uss.
    /// Code only finds elements by name, fills in text and reacts to clicks.
    ///
    /// Every screen shares one set of panel settings: the UI scales so the screen is always 900
    /// units tall (the same scale the IMGUI menus use at 900 px and up), so a wide screen gets more
    /// room across and nothing ever gets taller than it is. The game's font is Segoe UI taken from
    /// the system at runtime, like the IMGUI screens; where that's missing, Unity's default is used.
    /// </summary>
    public static class UiPanels
    {
        public const float ReferenceHeight = 900f;

        static PanelSettings settings;
        static FontAsset font;
        static bool triedFont;

        public static PanelSettings Settings
        {
            get
            {
                if (settings != null) return settings;
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "Fish Breeder UI";
                settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/FishBreederTheme");
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = new Vector2Int(1600, (int)ReferenceHeight);
                settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                settings.match = 1f;
                return settings;
            }
        }

        /// <summary>The layout Resources/UI/<paramref name="layout"/>.uxml, to instantiate pieces from.</summary>
        public static VisualTreeAsset Layout(string layout)
        {
            var asset = Resources.Load<VisualTreeAsset>("UI/" + layout);
            if (asset == null) Debug.LogError($"[UI] Resources/UI/{layout}.uxml is missing.");
            return asset;
        }

        /// <summary>
        /// Shows a UXML layout on its own child object of <paramref name="parent"/>. Higher
        /// <paramref name="order"/> draws on top.
        /// </summary>
        public static UIDocument Open(string name, string layout, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = Settings;
            document.visualTreeAsset = Layout(layout);
            document.sortingOrder = order;
            go.SetActive(true);
            var root = document.rootVisualElement;
            // The layout's root covers the whole screen, so its pieces can place themselves on it.
            root.style.position = Position.Absolute;
            root.style.left = root.style.top = root.style.right = root.style.bottom = 0f;
            var gameFont = Font;
            if (gameFont != null) root.style.unityFontDefinition = FontDefinition.FromSDFFont(gameFont);
            return document;
        }

        static FontAsset Font
        {
            get
            {
                if (triedFont) return font;
                triedFont = true;
                try
                {
                    font = FontAsset.CreateFontAsset("Segoe UI", "Regular");
                    // Symbols the main font lacks (arrows, stars) come from Segoe UI Symbol.
                    var symbols = font != null ? FontAsset.CreateFontAsset("Segoe UI Symbol", "Regular") : null;
                    if (symbols != null) font.fallbackFontAssetTable = new List<FontAsset> { symbols };
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[UI] Couldn't load Segoe UI, using Unity's default font: " + e.Message);
                    font = null;
                }
                return font;
            }
        }

        public static void Show(VisualElement element, bool show)
        {
            if (element != null) element.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static bool Showing(VisualElement element) => element != null && element.resolvedStyle.display != DisplayStyle.None;

        /// <summary>A top-to-bottom colour fade, for backgrounds (USS has no gradients).</summary>
        public static Texture2D Gradient(Color top, Color bottom)
        {
            const int steps = 64;
            var texture = new Texture2D(1, steps, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < steps; i++) texture.SetPixel(0, i, Color.Lerp(bottom, top, i / (steps - 1f)));
            texture.Apply();
            return texture;
        }
    }
}
