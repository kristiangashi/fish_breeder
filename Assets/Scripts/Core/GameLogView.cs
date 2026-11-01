using UnityEngine;
using UnityEngine.UIElements;

namespace FishBreeder.Core
{
    /// <summary>
    /// The message history: clicking the log in the bottom left corner opens the last 30 messages
    /// in a window that scrolls, newest at the bottom. Esc or a click outside closes it. Drawn in
    /// the #log-window (Window.uxml, styled in FishBreeder.uss).
    /// </summary>
    public class GameLogView : MonoBehaviour
    {
        const int Shown = 30;

        static bool open, scrollToEnd;

        UiWindow window;
        UiFrame ui;

        public static bool IsOpen => open;

        public static void Open()
        {
            open = true;
            scrollToEnd = true;
        }

        public static void Close() => open = false;

        /// <summary>Makes a log box open the history when clicked.</summary>
        public static void MakeClickable(VisualElement log)
        {
            log.pickingMode = PickingMode.Position;
            log.tooltip = "Click to see the last 30 messages";
            log.AddToClassList("fb-log--clickable");
            log.RegisterCallback<PointerDownEvent>(_ => Open());
        }

        void Awake()
        {
            open = false;
            window = new UiWindow(transform, "log-window", "Messages", 15, true, Close);
            ui = window.Frame;
        }

        void OnDestroy() => window?.Destroy();

        void OnDisable() => window?.Show(false);

        void Update()
        {
            bool show = open && !ViewMode.Watching;
            window.Show(show);
            if (show) window.Fill(null, DrawBody);
        }

        void DrawBody()
        {
            var entries = GameLog.Entries;
            ui.Label($"The last {Mathf.Min(Shown, entries.Count)} messages, newest at the bottom.", "fb-small");
            var scroll = ui.BeginScroll("fb-scroll fb-window__body");
            if (entries.Count == 0) ui.Label("<i>Nothing yet.</i>");
            for (int i = Mathf.Max(0, entries.Count - Shown); i < entries.Count; i++)
                ui.Label(i == entries.Count - 1 ? $"<b>{entries[i]}</b>" : entries[i], "fb-text fb-log-entry");
            ui.Pop();
            if (scrollToEnd && scroll.contentContainer.layout.height > 0f)
            {
                scrollToEnd = false;
                scroll.scrollOffset = new Vector2(0f, float.MaxValue);
            }
        }
    }
}
