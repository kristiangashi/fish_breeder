using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Dev mode, for trying things out: everything is unlocked whatever your levels, and money
    /// never runs out (buying costs nothing). F10 turns it on and off; it stays on between plays.
    /// Only in the editor and development builds.
    /// </summary>
    public static class DevMode
    {
        const string PrefsKey = "FishBreeder.DevMode";
        static bool loaded, on;

        public static bool Available => Application.isEditor || Debug.isDebugBuild;

        public static bool On
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    on = Available && PlayerPrefs.GetInt(PrefsKey, 0) == 1;
                }
                return on;
            }
        }

        public static void Toggle()
        {
            if (!Available) return;
            on = !On;
            PlayerPrefs.SetInt(PrefsKey, on ? 1 : 0);
            GameLog.Add(on ? "Dev mode on: everything is unlocked and buying is free. F10 turns it off." : "Dev mode off.");
        }
    }
}
