using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FishBreeder.EditorTools
{
    /// <summary>
    /// Makes the project buildable. The game builds itself at runtime (<see cref="Core.GameBootstrap"/>),
    /// so all it needs is one saved scene in the build list: Assets/Scenes/Main.unity, an empty scene
    /// with its lighting generated (so a build lights the shop the way the editor does). It is opened
    /// instead of an untouched "Untitled" scene, so Build doesn't ask to save one.
    ///
    /// Runtime materials use the Standard shader, which a build only carries in the variants that
    /// some material asset uses: Resources/BuildShaders holds one material per combination the game
    /// switches on (plain, normal map, normal + metallic map, see-through).
    ///
    /// Fish Breeder > Build and Run builds a Windows player to Builds/Windows and starts it.
    /// </summary>
    [InitializeOnLoad]
    static class GameBuild
    {
        public const string MainScene = "Assets/Scenes/Main.unity";
        const string ShaderFolder = "Assets/Resources/BuildShaders";
        const string Output = "Builds/Windows/Fish Breeder.exe";

        static GameBuild()
        {
            EditorApplication.delayCall += Setup;
        }

        static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            KeepShaderVariants();
            if (!File.Exists(MainScene)) CreateMainScene();
            if (!EditorBuildSettings.scenes.Any(s => s.path == MainScene && s.enabled))
            {
                var scenes = EditorBuildSettings.scenes.Where(s => s.path != MainScene).ToList();
                scenes.Insert(0, new EditorBuildSettingsScene(MainScene, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"[FishBreeder] Added {MainScene} to the build.");
            }
            if (IsUntouchedUntitled(SceneManager.GetActiveScene())) EditorSceneManager.OpenScene(MainScene);
        }

        /// <summary>A never-saved scene with nothing in it but Unity's default camera and light.</summary>
        static bool IsUntouchedUntitled(Scene scene)
        {
            if (SceneManager.sceneCount != 1 || !string.IsNullOrEmpty(scene.path)) return false;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name != "Main Camera" && root.name != "Directional Light") return false;
            return true;
        }

        static void CreateMainScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MainScene));
            var active = SceneManager.GetActiveScene();
            bool replace = IsUntouchedUntitled(active);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, replace ? NewSceneMode.Single : NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, MainScene);
            // Generate the lighting once (sky ambient and reflections): there is nothing to bake, so it's quick.
            if (replace)
            {
                Lightmapping.Bake();
                EditorSceneManager.SaveScene(scene);
            }
            else EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
            Debug.Log($"[FishBreeder] Created {MainScene}, the scene the game builds from.");
        }

        static void KeepShaderVariants()
        {
            var standard = Shader.Find("Standard");
            if (standard == null) return;
            if (!AssetDatabase.IsValidFolder(ShaderFolder))
            {
                Directory.CreateDirectory(ShaderFolder);
                AssetDatabase.Refresh();
            }
            bool made = false;
            made |= Keep(standard, "Standard", false);
            made |= Keep(standard, "StandardNormal", false, "_NORMALMAP");
            made |= Keep(standard, "StandardNormalMetallic", false, "_NORMALMAP", "_METALLICGLOSSMAP");
            made |= Keep(standard, "StandardMetallic", false, "_METALLICGLOSSMAP");
            made |= Keep(standard, "StandardFade", true);
            if (made) AssetDatabase.SaveAssets();
        }

        static bool Keep(Shader shader, string name, bool fade, params string[] keywords)
        {
            string path = $"{ShaderFolder}/{name}.mat";
            if (File.Exists(path)) return false;
            var material = new Material(shader) { name = name };
            foreach (var keyword in keywords) material.EnableKeyword(keyword);
            if (fade)
            {
                // The same set-up as MaterialFactory.Transparent.
                material.SetFloat("_Mode", 2f);
                material.EnableKeyword("_ALPHABLEND_ON");
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(material, path);
            return true;
        }

        [MenuItem("Fish Breeder/Build and Run")]
        static void BuildAndRun()
        {
            Setup();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = Output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.AutoRunPlayer,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"[FishBreeder] Built {Path.GetFullPath(Output)} ({summary.totalSize / (1024 * 1024)} MB) in {summary.totalTime.TotalSeconds:0}s and started it.");
            else
                Debug.LogError($"[FishBreeder] Build {summary.result}: {summary.totalErrors} error(s). See the Console for what went wrong.");
        }
    }
}
