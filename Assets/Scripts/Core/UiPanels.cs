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
    /// units tall, so a wide screen gets more
    /// room across and nothing ever gets taller than it is. The game's font is Segoe UI taken from
    /// the system at runtime; where that's missing, Unity's default is used.
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
            // The layout's root covers the whole screen, so its pieces can place themselves on it,
            // but only what's drawn on it takes clicks (see PointerOverUi).
            root.style.position = Position.Absolute;
            root.style.left = root.style.top = root.style.right = root.style.bottom = 0f;
            root.pickingMode = PickingMode.Ignore;
            var gameFont = Font;
            if (gameFont != null) root.style.unityFontDefinition = FontDefinition.FromSDFFont(gameFont);
            root.RegisterCallback<PointerMoveEvent>(TrackTooltip, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerLeaveEvent>(_ => HoverTooltip = "");
            documents.Add(document);
            return document;
        }

        static readonly List<UIDocument> documents = new List<UIDocument>();

        /// <summary>Every UI Toolkit screen showing now (for the UI check).</summary>
        public static List<VisualElement> ShowingRoots()
        {
            documents.RemoveAll(d => d == null);
            var roots = new List<VisualElement>();
            foreach (var document in documents)
                if (document.isActiveAndEnabled && document.rootVisualElement != null && Showing(document.rootVisualElement)) roots.Add(document.rootVisualElement);
            return roots;
        }

        /// <summary>Where a screen pixel (origin bottom left, like Input.mousePosition) falls on an element's panel.</summary>
        public static Vector2 ScreenToPanel(VisualElement element, Vector3 screen)
        {
            var size = element.panel != null ? element.panel.visualTree.layout.size : new Vector2(Screen.width, Screen.height);
            return new Vector2(screen.x / Mathf.Max(1, Screen.width) * size.x, (1f - screen.y / Mathf.Max(1, Screen.height)) * size.y);
        }

        /// <summary>The tooltip of the element under the pointer (or of the nearest parent that has one), or "".</summary>
        public static string HoverTooltip { get; private set; } = "";

        static void TrackTooltip(PointerMoveEvent e)
        {
            string tip = "";
            for (var element = e.target as VisualElement; element != null; element = element.hierarchy.parent)
                if (!string.IsNullOrEmpty(element.tooltip))
                {
                    tip = element.tooltip;
                    break;
                }
            HoverTooltip = tip;
        }

        /// <summary>
        /// The mouse is over something drawn by a UI Toolkit screen (a window, bar or button), so a
        /// click there is for the menu, not the store or tank behind it.
        /// </summary>
        public static bool PointerOverUi
        {
            get
            {
                var mouse = Input.mousePosition;
                var screen = new Vector2(mouse.x, Screen.height - mouse.y);
                documents.RemoveAll(d => d == null);
                foreach (var document in documents)
                {
                    var panel = document.rootVisualElement?.panel;
                    if (panel == null || !Showing(document.rootVisualElement)) continue;
                    if (panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, screen)) != null) return true;
                }
                return false;
            }
        }

        /// <summary>Someone is typing in a text box (old or new UI), so single-key shortcuts should wait.</summary>
        public static bool Typing
        {
            get
            {
                if (GUIUtility.keyboardControl != 0) return true;
                foreach (var document in documents)
                {
                    var focused = document != null ? document.rootVisualElement?.focusController?.focusedElement as VisualElement : null;
                    if (focused is TextField || focused?.GetFirstAncestorOfType<TextField>() != null) return true;
                }
                return false;
            }
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
