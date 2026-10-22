using System.Collections;
using FishBreeder.Core;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.Health;
using UnityEngine;

namespace FishBreeder
{
    public enum LifeStage { Fry, Juvenile, Adult }

    /// <summary>
    /// A living fish: genome, hunger and growth. Age never kills a fish; once grown it lives
    /// until a chance event (see <see cref="FishHealth"/>) takes it.
    /// </summary>
    [RequireComponent(typeof(FishHealth), typeof(FishSwimmer), typeof(FishVisual))]
    public class Fish : MonoBehaviour
    {
        [SerializeField] string fishName;
        [SerializeField] int generation = 1;
        [SerializeField] Genome genome;
        [SerializeField, Range(0f, 1f)] float hunger;
        [SerializeField, Range(0f, 1f)] float growth = 1f;

        public string FishName => fishName;
        public int Generation => generation;
        public Genome Genome => genome;
        public Phenotype Phenotype { get; private set; }
        public float Hunger => hunger;
        public float Growth => growth;
        public LifeStage Stage => growth >= 1f ? LifeStage.Adult : growth >= 0.4f ? LifeStage.Juvenile : LifeStage.Fry;
        public bool IsDead { get; private set; }
        public bool IsStarving => hunger >= GameConfig.Active.starvingThreshold;
        public bool WantsFood => hunger > 0.25f;
        public float BreedCooldown { get; set; }
        public string ParentNames { get; private set; }
        public Tank Tank { get; private set; }

        public FishHealth Health { get; private set; }
        public FishSwimmer Swimmer { get; private set; }
        public FishVisual Visual { get; private set; }

        void Awake()
        {
            Health = GetComponent<FishHealth>();
            Swimmer = GetComponent<FishSwimmer>();
            Visual = GetComponent<FishVisual>();
        }

        public void Initialize(Genome dna, int gen, float startGrowth, Tank tank, string parents = null, string customName = null)
        {
            genome = dna;
            Phenotype = dna.Express();
            generation = gen;
            growth = Mathf.Clamp01(startGrowth);
            hunger = GameRandom.Range(0f, 0.3f);
            ParentNames = parents;
            fishName = string.IsNullOrEmpty(customName) ? FishNames.Random() : customName;
            gameObject.name = $"Fish - {fishName}";
            Tank = tank;

            Visual.Build(Phenotype);
            ApplyGrowthScale();
            tank.Add(this);
        }

        void Update()
        {
            if (IsDead) return;
            var config = GameConfig.Active;
            float dt = Time.deltaTime;

            hunger = Mathf.Clamp01(hunger + config.hungerPerSecond * dt);

            // Growth only pauses when starving. It never leads to death.
            if (growth < 1f && !IsStarving)
            {
                growth = Mathf.Min(1f, growth + dt / config.secondsToAdult);
                ApplyGrowthScale();
            }

            if (BreedCooldown > 0f) BreedCooldown -= dt;
        }

        void ApplyGrowthScale()
        {
            transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1f, growth);
        }

        public void Eat(FoodPellet pellet)
        {
            if (IsDead || pellet == null) return;
            bool wasStarving = IsStarving;
            if (!pellet.TryConsume()) return;

            hunger = Mathf.Max(0f, hunger - pellet.Nutrition);
            Health.OnAte(pellet, wasStarving);
        }

        public void Die(string cause)
        {
            if (IsDead) return;
            IsDead = true;
            if (Tank != null) Tank.Remove(this);
            GameLog.Add($"{fishName} died of {cause}.");
            Swimmer.enabled = false;
            StartCoroutine(FloatUpAndVanish());
        }

        IEnumerator FloatUpAndVanish()
        {
            var heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (heading.sqrMagnitude < 0.001f) heading = Vector3.forward;
            var bellyUp = Quaternion.LookRotation(heading.normalized, Vector3.down);
            float surfaceY = Tank != null ? Tank.InnerBounds.max.y - 0.03f : transform.position.y + 0.5f;

            for (float t = 0f; t < 8f; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, bellyUp, Time.deltaTime * 2f);
                var pos = transform.position;
                pos.y = Mathf.MoveTowards(pos.y, surfaceY, 0.06f * Time.deltaTime);
                transform.position = pos;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
