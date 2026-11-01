using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// What the UI checks found. <see cref="UiToolkitCheck"/> walks the screens and records here
    /// anything off the screen, poking out of its box or with text too big for it; each problem is
    /// kept once and, in the editor, logged as a "[UI overflow]" warning. F8 in the menus and the
    /// store runs the full check at five screen sizes.
    /// </summary>
    public static class UiCheck
    {
        static readonly HashSet<string> reported = new HashSet<string>();

        /// <summary>Everything that overflowed since the last <see cref="ClearProblems"/>, one line each.</summary>
        public static readonly List<string> Problems = new List<string>();

        /// <summary>Every screen checked, for the report.</summary>
        public static readonly HashSet<string> Seen = new HashSet<string>();

        public static void ClearProblems()
        {
            Problems.Clear();
            reported.Clear();
            Seen.Clear();
        }

        /// <summary>Records a problem once per id.</summary>
        public static void Record(string id, string line)
        {
            if (!reported.Add(id)) return;
            Problems.Add(line);
#if UNITY_EDITOR
            Debug.LogWarning("[UI overflow] " + line);
#endif
        }
    }
}
