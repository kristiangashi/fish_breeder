using FishBreeder.Breeding;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Builds a playable tank at runtime: camera, light, tank, systems, HUD and starter fish.
    /// If the open scene has no GameBootstrap, one is created automatically when you press Play,
    /// so a brand-new empty scene is enough to try the game.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] FishModelLibrary modelLibrary;
        [SerializeField] int starterFish = 6;
        [Tooltip("0 = random every run.")]
        [SerializeField] int randomSeed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            // Remove this once real scenes contain a GameBootstrap (or a menu scene exists).
            if (FindAnyObjectByType<GameBootstrap>() == null)
                new GameObject("Fish Breeder").AddComponent<GameBootstrap>();
        }

        void Start()
        {
            if (randomSeed != 0) GameRandom.Seed(randomSeed);
            GameConfig.Active = config != null ? config : ScriptableObject.CreateInstance<GameConfig>();
            FishVisual.DefaultLibrary = modelLibrary;
            GameLog.Clear();

            var tank = new GameObject("Tank").AddComponent<Tank>();
            tank.BuildPlaceholderVisuals();

            var feeding = gameObject.AddComponent<FeedingSystem>();
            feeding.Initialize(tank);
            var breeding = gameObject.AddComponent<BreedingSystem>();
            breeding.Initialize(tank);

            var cam = SetupCamera(tank);
            SetupLight();

            for (int i = 0; i < starterFish; i++)
                FishFactory.Spawn(Genome.CreateRandom(), 1, 1f, tank, tank.RandomPoint(0.15f));

            gameObject.AddComponent<FishTankHUD>().Initialize(tank, feeding, breeding, cam);
            GameLog.Add("Welcome! Feed your fish, keep the water clean and breed for rare traits.");
        }

        static Camera SetupCamera(Tank tank)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            var center = tank.InnerBounds.center;
            cam.transform.position = center + new Vector3(0f, 0.1f, -1.9f);
            cam.transform.LookAt(center);
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            return cam;
        }

        static void SetupLight()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) return;

            var sun = new GameObject("Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(0.9f, 0.97f, 1f);
            sun.transform.rotation = Quaternion.Euler(60f, -20f, 0f);
        }
    }
}
