using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishBreeder.Core
{
    /// <summary>
    /// A game window on its own UI Toolkit panel, built from Resources/UI/Window.uxml: a title, a
    /// header strip for tabs and buttons, the close button, a body and a footer. Each window is named
    /// (its USS id, like #perks-window), so FishBreeder.uss can give it its own size. A modal window
    /// dims the store behind it, and a click on the dimmed part closes it. Views fill the header,
    /// body and footer each frame through <see cref="Frame"/>.
    /// </summary>
    public class UiWindow
    {
        public readonly UIDocument Document;
        public readonly VisualElement Root, Overlay, Box, HeaderContent, Body, Footer;
        public readonly Label Title;
        public readonly Button CloseButton;
        public readonly UiFrame Frame = new UiFrame();
        public readonly string Name;

        /// <summary>Every window made, for the UI check (F8).</summary>
        public static readonly System.Collections.Generic.List<UiWindow> All = new System.Collections.Generic.List<UiWindow>();

        public static UiWindow Find(string name)
        {
            All.RemoveAll(w => w.Document == null);
            return All.Find(w => w.Name == name);
        }

        public UiWindow(Transform parent, string name, string title, int order, bool modal, Action close)
        {
            Name = name;
            Document = UiPanels.Open(title, "Window", parent, order);
            Root = Document.rootVisualElement;
            Overlay = Root.Q("overlay");
            Box = Root.Q("window");
            Box.name = name;
            Title = Root.Q<Label>("title");
            Title.text = title;
            HeaderContent = Root.Q("header-content");
            Body = Root.Q("body");
            Footer = Root.Q("footer");
            CloseButton = Root.Q<Button>("close");
            CloseButton.clicked += () => close?.Invoke();
            if (modal)
            {
                Overlay.AddToClassList("fb-dim");
                Overlay.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.target == Overlay && !UiToolkitCheck.Sweeping) close?.Invoke();
                });
            }
            else Overlay.pickingMode = PickingMode.Ignore;
            Show(false);
            All.Add(this);
        }

        /// <summary>Fills the header strip, body and footer for this frame (any can be null). An empty footer takes no room.</summary>
        public void Fill(Action header, Action body, Action footer = null)
        {
            Frame.Begin(HeaderContent);
            header?.Invoke();
            Frame.End();
            Frame.Begin(Body);
            body?.Invoke();
            Frame.End();
            Frame.Begin(Footer);
            footer?.Invoke();
            Frame.End();
            UiPanels.Show(Footer, Footer.childCount > 0);
        }

        public bool Showing => UiPanels.Showing(Root);

        public void Show(bool show) => UiPanels.Show(Root, show);

        public void Destroy()
        {
            if (Document != null) UnityEngine.Object.Destroy(Document.gameObject);
        }
    }
}
