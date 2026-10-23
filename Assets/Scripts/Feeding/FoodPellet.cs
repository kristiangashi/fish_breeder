using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder.Feeding
{
    /// <summary>Kinds of food. Each species has favourites (see Species.favoriteFoods) but eats them all.</summary>
    public enum FoodType { Flakes, Premium, AlgaeWafers, Bloodworms, BrineShrimp }

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
            var info = Info(type);
            sinkSpeed = info.sinkSpeed * GameRandom.Range(0.75f, 1.25f);
            Nutrition = info.nutrition;
            // Food is usually fine, but now and then a spoiled piece gets through.
            IsSpoiled = GameRandom.Chance(info.spoilChance);
            Contamination = info.contamination * (IsSpoiled ? 4f : 1f);
        }

        /// <summary>The supplier's numbers for this food (a plain default if the balance file lacks it).</summary>
        public static FoodBalance Info(FoodType type)
        {
            foreach (var food in Balance.Data.supplier.food)
                if (food != null && food.type == type) return food;
            return new FoodBalance { type = type, name = type.ToString() };
        }

        /// <summary>"algae wafers": the food's name for sentences.</summary>
        public static string Name(FoodType type) => Info(type).name.ToLower();

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

        /// <summary>Nearest uneaten pellet within <paramref name="maxDistance"/> and no higher than <paramref name="maxHeight"/>.</summary>
        public static FoodPellet FindNearest(Vector3 position, float maxDistance, float maxHeight = float.PositiveInfinity)
        {
            FoodPellet best = null;
            float bestSqr = maxDistance * maxDistance;
            foreach (var pellet in active)
            {
                if (pellet.IsConsumed || pellet.transform.position.y > maxHeight) continue;
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
