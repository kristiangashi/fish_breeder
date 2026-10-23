using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder.Feeding
{
    /// <summary>Drops food at the surface of the tank.</summary>
    public class FeedingSystem : MonoBehaviour
    {
        [SerializeField] Tank tank;
        [SerializeField] float pelletSize = 0.015f;

        public void Initialize(Tank targetTank) => tank = targetTank;

        /// <summary>Drops enough food for the fish in the tank (at least <paramref name="minimum"/> pellets).</summary>
        public void Feed(FoodType type, int minimum)
        {
            if (tank == null) return;
            int count = Mathf.Max(minimum, tank.Residents.Count + 2);
            // XP and the daily goal count once per tank between time steps, so dumping food doesn't farm XP.
            if (!tank.FedSinceTimeStep)
            {
                Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpFeedTank);
                Progression.Count(GoalKind.FeedTanks);
            }
            tank.FedSinceTimeStep = true;
            var bounds = tank.InnerBounds;
            var material = MaterialFactory.Create(FoodPellet.Info(type).color, type == FoodType.Bloodworms || type == FoodType.BrineShrimp ? 0.5f : 0.1f);

            for (int i = 0; i < count; i++)
            {
                // Each food has its own look: flat flakes and wafers, round pellets, little worms and shrimp.
                var shape = type == FoodType.Bloodworms || type == FoodType.BrineShrimp ? PrimitiveType.Capsule
                    : type == FoodType.Flakes || type == FoodType.AlgaeWafers ? PrimitiveType.Cylinder : PrimitiveType.Sphere;
                var pellet = GameObject.CreatePrimitive(shape);
                pellet.name = $"Food ({type})";
                Destroy(pellet.GetComponent<Collider>());
                pellet.GetComponent<Renderer>().sharedMaterial = material;
                pellet.transform.localScale = FoodScale(type);
                pellet.transform.rotation = Quaternion.Euler(GameRandom.Range(-25f, 25f), GameRandom.Range(0f, 360f), shape == PrimitiveType.Capsule ? 90f : 0f);
                pellet.transform.position = new Vector3(
                    GameRandom.Range(bounds.min.x + 0.1f, bounds.max.x - 0.1f),
                    bounds.max.y - 0.02f,
                    GameRandom.Range(bounds.min.z + 0.1f, bounds.max.z - 0.1f));
                pellet.AddComponent<FoodPellet>().Initialize(tank, type);
            }
        }

        Vector3 FoodScale(FoodType type)
        {
            switch (type)
            {
                case FoodType.Flakes: return new Vector3(pelletSize * 1.3f, pelletSize * 0.08f, pelletSize * 1.1f);
                case FoodType.AlgaeWafers: return new Vector3(pelletSize * 1.6f, pelletSize * 0.2f, pelletSize * 1.6f);
                case FoodType.Bloodworms: return new Vector3(pelletSize * 0.3f, pelletSize * 0.9f, pelletSize * 0.3f);
                case FoodType.BrineShrimp: return new Vector3(pelletSize * 0.45f, pelletSize * 0.5f, pelletSize * 0.45f);
                default: return Vector3.one * pelletSize;
            }
        }
    }
}
