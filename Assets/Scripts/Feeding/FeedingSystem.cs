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

        public void Feed(FoodType type, int count)
        {
            if (tank == null) return;
            var bounds = tank.InnerBounds;
            var color = type == FoodType.Premium ? new Color(0.9f, 0.45f, 0.2f) : new Color(0.55f, 0.4f, 0.25f);
            var material = MaterialFactory.Create(color, 0.1f);

            for (int i = 0; i < count; i++)
            {
                var pellet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pellet.name = $"Food ({type})";
                Destroy(pellet.GetComponent<Collider>());
                pellet.GetComponent<Renderer>().sharedMaterial = material;
                pellet.transform.localScale = Vector3.one * pelletSize;
                pellet.transform.position = new Vector3(
                    GameRandom.Range(bounds.min.x + 0.1f, bounds.max.x - 0.1f),
                    bounds.max.y - 0.02f,
                    GameRandom.Range(bounds.min.z + 0.1f, bounds.max.z - 0.1f));
                pellet.AddComponent<FoodPellet>().Initialize(tank, type);
            }
        }
    }
}
