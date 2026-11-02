namespace FishBreeder.Core
{
    /// <summary>
    /// The message log in the bottom-left corner: the newest messages, oldest first, the latest
    /// in bold. Its box (fb-log in FishBreeder.uss) keeps them to its height, so a long message
    /// never runs off the edge.
    /// </summary>
    public static class GameLogPanel
    {
        /// <summary>The newest messages (at most <paramref name="maxEntries"/>) into the box being filled.</summary>
        public static void Fill(UiFrame ui, int maxEntries)
        {
            var entries = GameLog.Entries;
            for (int i = UnityEngine.Mathf.Max(0, entries.Count - maxEntries); i < entries.Count; i++) ui.Label(Line(entries, i), "fb-text fb-log__line");
        }

        static string Line(System.Collections.Generic.IReadOnlyList<string> entries, int i)
            => i == entries.Count - 1 ? $"<b>{entries[i]}</b>" : $"<color=#bfe3e3>{entries[i]}</color>";
    }
}
