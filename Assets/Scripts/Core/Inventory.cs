using System.Collections.Generic;

namespace FishBreeder.Core
{
    /// <summary>
    /// Decorations and background pictures bought from the supplier menu and not in a tank yet.
    /// They're put in (and taken back out) from each aquarium's Tank tab, so three plants bought at
    /// once can go one in one tank and two in another. Taking something out of a tank puts it back here.
    /// </summary>
    public static class Inventory
    {
        static readonly Dictionary<string, int> decorations = new Dictionary<string, int>();
        static readonly Dictionary<string, int> backgrounds = new Dictionary<string, int>();

        public static void Reset()
        {
            decorations.Clear();
            backgrounds.Clear();
        }

        public static int Decorations(string id) => id != null && decorations.TryGetValue(id, out var n) ? n : 0;
        public static int Backgrounds(string id) => id != null && backgrounds.TryGetValue(id, out var n) ? n : 0;

        public static void AddDecoration(string id, int count = 1) => Add(decorations, id, count);
        public static void AddBackground(string id, int count = 1) => Add(backgrounds, id, count);

        /// <summary>Takes one out to put in a tank (false if there are none).</summary>
        public static bool TakeDecoration(string id) => Take(decorations, id);
        public static bool TakeBackground(string id) => Take(backgrounds, id);

        /// <summary>Every decoration on hand, by id, in the order they were first bought.</summary>
        public static IEnumerable<KeyValuePair<string, int>> AllDecorations
        {
            get
            {
                foreach (var pair in decorations) if (pair.Value > 0) yield return pair;
            }
        }

        public static IEnumerable<KeyValuePair<string, int>> AllBackgrounds
        {
            get
            {
                foreach (var pair in backgrounds) if (pair.Value > 0) yield return pair;
            }
        }

        public static int DecorationCount
        {
            get
            {
                int total = 0;
                foreach (var pair in decorations) total += pair.Value;
                return total;
            }
        }

        static void Add(Dictionary<string, int> items, string id, int count)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return;
            items[id] = (items.TryGetValue(id, out var n) ? n : 0) + count;
        }

        static bool Take(Dictionary<string, int> items, string id)
        {
            if (id == null || !items.TryGetValue(id, out var n) || n <= 0) return false;
            items[id] = n - 1;
            return true;
        }
    }
}
