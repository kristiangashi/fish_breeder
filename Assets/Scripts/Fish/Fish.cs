using System.Collections;
using FishBreeder.Core;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.Health;
using UnityEngine;

namespace FishBreeder
{
    public enum LifeStage { Fry, Juvenile, Adult }

    public enum Sex { Female, Male }

    /// <summary>
    /// A fish: genome, sex, age, hunger, growth and what it's worth. It changes only at time steps
    /// (shop opening and closing) and when fed. Dead fish stay in the tank, floating at the top,
    /// until the player removes them.
    /// </summary>
    [RequireComponent(typeof(FishHealth), typeof(FishSwimmer), typeof(FishVisual))]
    public class Fish : MonoBehaviour
    {
        [SerializeField] string fishName;
        [SerializeField] int generation = 1;
        [SerializeField] Genome genome;
        [SerializeField] Sex sex;
        [SerializeField, Range(0f, 1f)] float hunger;
        [SerializeField, Range(0f, 1f)] float growth = 1f;
        [SerializeField] float ageDays;
        [SerializeField] int price;
        [Tooltip("Hidden: the fish's true value relative to its appraised price.")]
        [SerializeField] float valueFactor = 1f;

        public string FishName => fishName;
        public int Generation => generation;
        public Genome Genome => genome;
        /// <summary>Always the current balance values for this fish's species.</summary>
        public Species Species => Species.Get(genome.species);
        public Sex Sex => sex;
        public Phenotype Phenotype { get; private set; }
        public float Hunger => hunger;
        public float Growth => growth;
        public float AgeDays => ageDays;
        public LifeStage Stage => growth >= 1f ? LifeStage.Adult : growth >= 0.4f ? LifeStage.Juvenile : LifeStage.Fry;
        public bool IsDead { get; private set; }
        public string CauseOfDeath { get; private set; }
        public bool IsStarving => hunger >= GameConfig.Active.starvingThreshold;
        public bool WantsFood => hunger > 0.25f;
        public int BreedCooldown { get; set; }
        public string ParentNames { get; private set; }
        public Tank Tank { get; private set; }
        /// <summary>Asking price in a For Sale tank. 0 = not for sale.</summary>
        public int Price { get => price; set => price = Mathf.Max(0, value); }
        /// <summary>A customer has picked this fish and is on the way to the register.</summary>
        public bool Reserved { get; set; }

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
            ageDays = growth * Species.daysToAdult;
            hunger = GameRandom.Range(0f, 0.2f);
            sex = GameRandom.Chance(0.5f) ? Sex.Male : Sex.Female;
            valueFactor = GameRandom.Range(0.8f, 1.25f);
            ParentNames = parents;
            fishName = string.IsNullOrEmpty(customName) ? FishNames.Random() : customName;
            gameObject.name = $"Fish - {fishName}";
            Tank = tank;

            Visual.Build(Phenotype);
            ApplyGrowthScale();
            tank.Add(this);
        }

        /// <summary>One time step (half a day): age, hunger and growth. Illness and death are handled by the shop's time step.</summary>
        public void OnTimeStep()
        {
            if (IsDead) return;
            const float daysPerStep = 0.5f;
            var species = Species;
            ageDays += daysPerStep;
            hunger = Mathf.Clamp01(hunger + daysPerStep / Mathf.Max(0.5f, species.hungerToleranceDays) * GameConfig.Active.starvingThreshold);
            if (growth < 1f && !IsStarving)
            {
                growth = Mathf.Min(1f, growth + daysPerStep / Mathf.Max(0.5f, species.daysToAdult));
                ApplyGrowthScale();
            }
            if (BreedCooldown > 0) BreedCooldown--;
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

            float nutrition = pellet.Nutrition;
            if (Species.needsPremiumFood && pellet.Type != FoodType.Premium) nutrition *= 0.5f;
            hunger = Mathf.Max(0f, hunger - nutrition);
            Health.OnAte(pellet, wasStarving);
        }

        /// <summary>Market value of a typical fish like this one: species, looks, age and health. Shown to the player.</summary>
        public int AppraisedValue
        {
            get
            {
                var species = Species;
                float looks = 1f;
                switch (Phenotype.RarityLabel)
                {
                    case "Uncommon": looks = 1.5f; break;
                    case "Rare": looks = 2.5f; break;
                    case "Exotic": looks = 4f; break;
                }
                float stage = Stage == LifeStage.Adult ? 1f : Stage == LifeStage.Juvenile ? 0.6f : 0.3f;
                float health = Mathf.Lerp(0.4f, 1f, Health.Report().total);
                return Mathf.Max(1, Mathf.RoundToInt(species.baseValue * looks * stage * health));
            }
        }

        /// <summary>What customers really think it's worth. Hidden from the player.</summary>
        public int TrueValue => Mathf.Max(1, Mathf.RoundToInt(AppraisedValue * valueFactor));

        /// <summary>Moves the fish into another tank. Fish in a For Sale tank get the appraised price if they had none.</summary>
        public bool MoveTo(Tank destination)
        {
            if (destination == null || destination == Tank || destination.IsFull || IsDead) return false;
            if (Tank != null) Tank.Remove(this);
            Tank = destination;
            destination.Add(this);
            transform.position = destination.RandomFreePoint(0.1f);
            Swimmer.enabled = false;
            Swimmer.enabled = true;
            Visual.ResetSpine();
            return true;
        }

        public void Die(string cause)
        {
            if (IsDead) return;
            IsDead = true;
            CauseOfDeath = cause;
            Reserved = false;
            if (Tank != null) Tank.AddCorpse(this);
            GameLog.Add($"{fishName} the {Species} died of {cause}.");
            Swimmer.enabled = false;
            Visual.SetTint(new Color(0.75f, 0.75f, 0.7f));
            StartCoroutine(FloatUp());
        }

        /// <summary>Takes a dead fish (or a sold one) out of its tank for good.</summary>
        public void RemoveFromTank()
        {
            if (Tank != null) Tank.Remove(this);
            Tank = null;
            Destroy(gameObject);
        }

        IEnumerator FloatUp()
        {
            var heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (heading.sqrMagnitude < 0.001f) heading = Vector3.forward;
            var bellyUp = Quaternion.LookRotation(heading.normalized, Vector3.down);
            float surfaceY = Tank != null ? Tank.InnerBounds.max.y - 0.07f : transform.position.y + 0.5f;

            while (true)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, bellyUp, Time.deltaTime * 2f);
                var pos = transform.position;
                pos.y = Mathf.MoveTowards(pos.y, surfaceY + Mathf.Sin(Time.time * 0.7f) * 0.005f, 0.06f * Time.deltaTime);
                transform.position = pos;
                yield return null;
            }
        }
    }
}
