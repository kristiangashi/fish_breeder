using FishBreeder.Shop;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Builds the game at runtime: the store with its starting tanks, camera, light and the
    /// closed-shop view. If the open scene has no GameBootstrap, one is created automatically when
    /// you press Play, so a brand-new empty scene is enough to try the game.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] FishModelLibrary modelLibrary;
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
            Balance.Load();
            FishVisual.DefaultLibrary = modelLibrary != null ? modelLibrary : FishModelLibrary.LoadBuiltIn();
            GameLog.Clear();
            Economy.Reset();
            Progression.Reset();
            Research.Reset();
            Reputation.Reset();
            Finance.Reset();
            Journal.Reset();
            Milestones.Reset();
            Research.Refresh();
            Fx.Reset();
            ViewMode.Set(false);
            Tank.Focused = null;

            // The tanks start empty: the first fish come from the supplier computer.
            var store = new GameObject("Store").AddComponent<StoreLayout>();
            store.Build();

            var cam = SetupCamera();
            SetupLight();

            var shopDay = gameObject.AddComponent<ShopDay>();
            shopDay.Initialize(store);
            var hud = gameObject.AddComponent<FishTankHUD>();
            gameObject.AddComponent<StoreView>().Initialize(store, shopDay, hud, cam);
            gameObject.AddComponent<ReputationView>().Initialize(store, shopDay);
            gameObject.AddComponent<FinancesView>();
            gameObject.AddComponent<JournalView>();
            gameObject.AddComponent<PerksView>();
            gameObject.AddComponent<ResearchView>();
            gameObject.AddComponent<FxOverlay>();
            int setup = Economy.DaysUntilOpening;
            if (setup > 0)
            {
                GameLog.Add($"Welcome to your shop! You have {Economy.Format(Economy.Money)} and {setup} days before the grand opening to raise some fish to sell.");
                if (Balance.Data.store.grandmaGift > 0)
                    GameLog.Add($"Grandma believes in you: she'll send {Economy.Format(Balance.Data.store.grandmaGift)} every night until the grand opening. After that the shop has to pay its way (she'll only help out if you go broke).");
                GameLog.Add("Buy your first box of eggs or fry from the fish seller's van out front. Time passes twice a day: when you let the day pass (or close the shop), and when you go to sleep.");
                Fx.Day("Welcome to your fish shop!", $"{setup} days to raise some fish before the grand opening");
            }
            else GameLog.Add($"Welcome to your shop! You have {Economy.Format(Economy.Money)}. Buy your first box of eggs from the fish seller's van out front.");
        }

        static Camera SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
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
