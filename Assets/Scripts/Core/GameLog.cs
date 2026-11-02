using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Player-facing event feed ("Bubbles caught Aqua Flu"). The HUD reads from it.
    /// </summary>
    public static partial class GameLog
    {
        const int MaxEntries = 50;
        static readonly List<string> entries = new List<string>();

        public static IReadOnlyList<string> Entries => entries;
        public static event Action<string> Added;

        public static void Add(string message)
        {
            entries.Add(message);
            if (entries.Count > MaxEntries) entries.RemoveAt(0);
            Debug.Log($"[FishBreeder] {message}");
            Added?.Invoke(message);
        }

        public static void Clear() => entries.Clear();
    }
}
