using System;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FishBreeder.EditorTools
{
    /// <summary>
    /// Starts the headless pacing simulator (<see cref="Sim.PacingRun"/>) when the editor is
    /// launched with <c>-pacingSim &lt;folder&gt;</c>: opens the main scene and enters play mode.
    /// The run itself quits the editor when it's done. Does nothing on a normal launch.
    /// </summary>
    [InitializeOnLoad]
    static class PacingSimLauncher
    {
        const string StartedKey = "FishBreeder.PacingSim.Started";

        static PacingSimLauncher()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-pacingSim") < 0) return;
            if (SessionState.GetBool(StartedKey, false)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(StartedKey, true);
                EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
                EditorApplication.EnterPlaymode();
            };
        }
    }
}
