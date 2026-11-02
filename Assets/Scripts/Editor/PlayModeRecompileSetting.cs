using UnityEditor;
using UnityEngine;

namespace FishBreeder.EditorTools
{
    /// <summary>
    /// The game isn't built to survive a script reload in the middle of Play mode (it fills the
    /// console with errors), so script changes made while playing wait until Play is stopped.
    /// Sets Unity's "Script Changes While Playing" preference to "Recompile After Finished Playing".
    /// </summary>
    [InitializeOnLoad]
    static class PlayModeRecompileSetting
    {
        const string Key = "ScriptCompilationDuringPlay";
        const int RecompileAfterFinishedPlaying = 1;

        static PlayModeRecompileSetting()
        {
            if (EditorPrefs.GetInt(Key, 0) == RecompileAfterFinishedPlaying) return;
            EditorPrefs.SetInt(Key, RecompileAfterFinishedPlaying);
            Debug.Log("[FishBreeder] Script changes made while playing now wait until you stop Play mode (Preferences > General > Script Changes While Playing).");
        }
    }
}
