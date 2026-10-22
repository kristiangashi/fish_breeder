using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder.Feeding
{
    public enum FoodType { Flakes, Premium }

    /// <summary>
    /// A piece of food sinking through the tank. Uneaten food rots on the floor and dirties the water.
    /// </summary>
    public class FoodPellet : MonoBehaviour
    {
        static readonly List<FoodPellet> active = new List<FoodPellet>();
        public static IReadOnlyList<FoodPellet> Active => active;

        Tank tank;
        float sinkSpeed;
        float timeOnFloor;

        public FoodType Type { get; private set; }
        /// <summary>How much hunger one bite removes.</summary>
        public float Nutrition { get; private set; }
        /// <summary>Multiplier on the per-meal infection chance.</summary>
        public float Contamination { get; private set; }
        public bool IsSpoiled { get; private set; }
        public bool IsConsumed { get; private set; }

        public void Initialize(Tank tank, FoodType type)
        {
            this.tank = tank;
            Type = type;
            sinkSpeed = GameRandom.Range(0.05f, 0.09f);

            switch (type)
            {
                case FoodType.Premium:
                    Nutrition = 0.5f;
                    Contamination = 0.25f;
                    break;
                default:
                    Nutrition = 0.35f;
                    // Cheap flakes are usually fine, but now and then a spoiled one gets through.
                    IsSpoiled = GameRandom.Chance(0.1f);
                    Contamination = IsSpoiled ? 4f : 1f;
                    break;
            }
        }

        void OnEnable() => active.Add(this);

        void OnDisable() => active.Remove(this);

        void Update()
        {
            if (tank == null) return;
            float floorY = tank.InnerBounds.min.y + transform.localScale.y * 0.5f;
            var pos = transform.position;
            if (pos.y > floorY)
            {
                pos.y = Mathf.Max(floorY, pos.y - sinkSpeed * Time.deltaTime);
                pos.x += Mathf.Sin(Time.time * 2f + GetInstanceID()) * 0.01f * Time.deltaTime;
                transform.position = pos;
                return;
            }

            timeOnFloor += Time.deltaTime;
            if (timeOnFloor >= GameConfig.Active.pelletRotSeconds)
            {
                tank.Pollute(GameConfig.Active.waterQualityLossPerRottenPellet);
                Destroy(gameObject);
            }
        }

        public bool TryConsume()
        {
            if (IsConsumed) return false;
            IsConsumed = true;
            Destroy(gameObject);
            return true;
        }

        public static FoodPellet FindNearest(Vector3 position, float maxDistance)
        {
            FoodPellet best = null;
            float bestSqr = maxDistance * maxDistance;
            foreach (var pellet in active)
            {
                if (pellet.IsConsumed) continue;
                float sqr = (pellet.transform.position - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = pellet;
                }
            }
            return best;
        }
    }
}
