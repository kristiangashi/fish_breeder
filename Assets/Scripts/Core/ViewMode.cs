using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Watch mode: every menu, label and window hides so you can just look at the aquarium. V (or
    /// the Watch button) toggles it; Esc also leaves it. The camera drifts gently while watching.
    /// </summary>
    public static class ViewMode
    {
        public static bool Watching { get; private set; }
        /// <summary>Real time when watch mode last changed, for the fading hint.</summary>
        public static float Since { get; private set; }

        public static void Set(bool watching)
        {
            if (Watching == watching) return;
            Watching = watching;
            Since = Time.unscaledTime;
            if (watching) JournalView.Close();
        }

        public static void Toggle() => Set(!Watching);
    }
}
