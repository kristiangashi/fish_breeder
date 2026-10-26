using System.Collections;
using System.Collections.Generic;
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
    /// <summary>How big a fish is for its species. Only for looks.</summary>
    public enum FishSize { Small, Medium, Large }

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
        [SerializeField] FishSize size = FishSize.Medium;

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

        /// <summary>
        /// Bought fry (and fry from bought eggs) are tiny grey fish: nobody can tell what they'll be
        /// until they grow into juveniles. Bred fry are known from their parents.
        /// </summary>
        public bool IdentityHidden => Stage == LifeStage.Fry && generation <= 1;
        /// <summary>The species name, or "mystery fry" while that can't be told yet.</summary>
        public string SpeciesLabel => IdentityHidden ? "mystery fry" : Phenotype.SpeciesName;

        /// <summary>Length in the water right now (fry are about 3 cm whatever they grow into), for who can eat whom. Its size (small, medium, large) is only for looks, so it doesn't count here.</summary>
        public float BodyLength => (Stage == LifeStage.Fry ? FishVisual.FryLength : Phenotype.lengthMeters) * GrowthScale;

        /// <summary>How long it looks, size included: for swimming room.</summary>
        public float VisualLength => BodyLength * SizeScale;

        /// <summary>Small, medium or large for its species, rolled at birth. Only for looks.</summary>
        public FishSize Size => size;
        public float SizeScale => size == FishSize.Small ? 0.85f : size == FishSize.Large ? 1.18f : 1f;
        float GrowthScale => Mathf.Lerp(0.5f, 1f, growth);
        public bool IsDead { get; private set; }
        public string CauseOfDeath { get; private set; }
        public bool IsStarving => hunger >= GameConfig.Active.starvingThreshold;
        public bool WantsFood => hunger > 0.25f;
        public int BreedCooldown { get; set; }
        /// <summary>Time steps since it last ate one of its favourite foods (large = not lately).</summary>
        int stepsSinceFavourite = 99;
        /// <summary>Ate a favourite food since the last time step or the one before: it's content.</summary>
        public bool AteFavouriteRecently => stepsSinceFavourite <= 1;

        // Pregnancy (livebearer females): the fry's genes are fixed when she gets pregnant.
        readonly List<Genome> unborn = new List<Genome>();
        int unbornGeneration;
        public bool IsPregnant => unborn.Count > 0;
        /// <summary>Time steps until she gives birth.</summary>
        public int StepsToBirth { get; private set; }
        public int UnbornCount => unborn.Count;
        public string UnbornFather { get; private set; }
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
            float sizeRoll = GameRandom.Value;
            size = sizeRoll < 0.25f ? FishSize.Small : sizeRoll < 0.75f ? FishSize.Medium : FishSize.Large;
            ParentNames = parents;
            fishName = string.IsNullOrEmpty(customName) ? FishNames.Random() : customName;
            gameObject.name = $"Fish - {fishName}";
            Tank = tank;

            Visual.Build(Phenotype, Stage == LifeStage.Fry);
            ApplyGrowthScale();
            tank.Add(this);

            Milestones.Add(MilestoneKind.GetFish);
            if (!IdentityHidden) Journal.Discover(Species);
            Fx.Bubbles(transform.position, 3, tank.SurfaceY, 0.02f);
        }

        /// <summary>One time step (half a day): age, hunger and growth. Illness and death are handled by the shop's time step.</summary>
        public void OnTimeStep()
        {
            if (IsDead) return;
            const float daysPerStep = 0.5f;
            var species = Species;
            ageDays += daysPerStep;
            hunger = Mathf.Clamp01(hunger + daysPerStep / Mathf.Max(0.5f, species.hungerToleranceDays) * GameConfig.Active.starvingThreshold * Perks.Less(PerkEffect.SlowerHunger));
            if (growth < 1f && !IsStarving)
            {
                bool wasFry = Stage == LifeStage.Fry;
                bool wasHidden = IdentityHidden;
                growth = Mathf.Min(1f, growth + daysPerStep / Mathf.Max(0.5f, species.daysToAdult) * Perks.More(PerkEffect.FasterGrowth));
                ApplyGrowthScale();
                if (wasFry && Stage != LifeStage.Fry) GrowOutOfFry(wasHidden);
                if (growth >= 1f) GrewUp();
            }
            if (BreedCooldown > 0) BreedCooldown--;
            if (stepsSinceFavourite < 99) stepsSinceFavourite++;
        }

        /// <summary>A grey fry has grown enough to show what it really is: swap in its own model and colours.</summary>
        void GrowOutOfFry(bool wasHidden)
        {
            Visual.Build(Phenotype, false);
            Visual.ResetSpine();
            Health.RefreshTint();
            if (!wasHidden) return;
            GameLog.Add($"{fishName} has grown enough to tell: it's a {SpeciesLabel}!");
            Fx.Text(transform.position + Vector3.up * 0.05f, $"It's a {SpeciesLabel}!", UiTheme.Aqua, 18f, 2.2f);
            Journal.Discover(Species);
        }

        /// <summary>Became an adult: a milestone and a little cheer.</summary>
        void GrewUp()
        {
            Milestones.Add(MilestoneKind.GrowUp);
            Fx.Text(transform.position + Vector3.up * 0.06f, "All grown up!", UiTheme.Sun, 18f, 2f);
            if (Tank != null) Fx.Bubbles(transform.position, 5, Tank.SurfaceY, 0.03f);
        }

        /// <summary>Developer: picks the fish's sex (for making breeding pairs).</summary>
        public void DevSetSex(Sex value) => sex = value;

        /// <summary>Developer shortcut: makes the fish an adult right away (for testing looks and breeding).</summary>
        public void DevGrowUp()
        {
            if (IsDead || growth >= 1f) return;
            bool wasFry = Stage == LifeStage.Fry;
            bool wasHidden = IdentityHidden;
            growth = 1f;
            ageDays = Mathf.Max(ageDays, Species.daysToAdult);
            hunger = 0f;
            ApplyGrowthScale();
            if (wasFry) GrowOutOfFry(wasHidden);
            GrewUp();
        }

        void ApplyGrowthScale()
        {
            transform.localScale = Vector3.one * (GrowthScale * SizeScale);
        }

        public void Eat(FoodPellet pellet)
        {
            if (IsDead || pellet == null) return;
            bool wasStarving = IsStarving;
            if (!pellet.TryConsume()) return;

            // Fish eat anything, but a favourite food fills them up more and cheers them up.
            float nutrition = pellet.Nutrition * Perks.More(PerkEffect.BetterFood);
            if (Species.favoriteFoods.Contains(pellet.Type))
            {
                if (stepsSinceFavourite > 0 && !IdentityHidden) Fx.Text(transform.position + Vector3.up * 0.04f, "♥", UiTheme.Hibiscus, 18f, 1.2f);
                nutrition *= Balance.Data.health.favouriteFoodNutrition;
                stepsSinceFavourite = 0;
            }
            hunger = Mathf.Max(0f, hunger - nutrition);
            Health.OnAte(pellet, wasStarving);
            Research.OnAte(this, pellet.Type);
        }

        /// <summary>Market value of a typical fish like this one: species, looks, age and health. Shown to the player.</summary>
        public int AppraisedValue
        {
            get
            {
                var p = Phenotype;
                if (IdentityHidden) return Mathf.Max(1, Balance.Data.supplier.unknownFryValue);
                float looks = 1f;
                switch (Phenotype.RarityLabel)
                {
                    case "Uncommon": looks = 1.5f; break;
                    case "Rare": looks = 2.5f; break;
                    case "Exotic": looks = 4f; break;
                }
                float stage = Stage == LifeStage.Adult ? 1f : Stage == LifeStage.Juvenile ? 0.6f : 0.3f;
                float health = Mathf.Lerp(0.4f, 1f, Health.Current);
                float damage = Mathf.Clamp01(1f - Health.MedicineDamage * Balance.Data.medicine.damageValue);
                int baseValue = CrossBreeding.BaseValue(p.species, p.finSpecies, p.tailSpecies);
                float pedigree = generation > 1 ? Perks.More(PerkEffect.PedigreeValue) : 1f;
                return Mathf.Max(1, Mathf.RoundToInt(baseValue * looks * stage * health * damage * pedigree));
            }
        }

        /// <summary>What customers really think it's worth. Hidden from the player.</summary>
        public int TrueValue => Mathf.Max(1, Mathf.RoundToInt(TrueValueExact));
        /// <summary>Unrounded true value, so cheap fish aren't over- or under-valued by rounding.</summary>
        public float TrueValueExact => AppraisedValue * valueFactor;

        /// <summary>The smallest tank size this fish will live in (its body, fins or tail species may want more room).</summary>
        public int MinTankSize => TankQuality.MinSize(genome);

        /// <summary>Moves the fish into another tank. Fish in a For Sale tank get the appraised price if they had none.</summary>
        public bool MoveTo(Tank destination)
        {
            if (destination == null || destination == Tank || destination.IsFull || IsDead) return false;
            if (MinTankSize > destination.SizeIndex)
            {
                GameLog.Add($"{FishName} won't go in {destination.Title}: it wants a {TankQuality.SizeName(MinTankSize).ToLower()} tank or bigger.");
                return false;
            }
            if (Tank != null) Tank.Remove(this);
            Tank = destination;
            destination.Add(this);
            transform.position = destination.RandomFreePoint(0.1f);
            Swimmer.enabled = false;
            Swimmer.enabled = true;
            Visual.ResetSpine();
            return true;
        }

        public void BecomePregnant(List<Genome> fry, int fryGeneration, string father, int steps)
        {
            unborn.Clear();
            unborn.AddRange(fry);
            unbornGeneration = fryGeneration;
            UnbornFather = father;
            StepsToBirth = Mathf.Max(1, steps);
        }

        /// <summary>Logs the cross-bred looks among newborn fry, e.g. "2 Guppy with Black Molly fins".</summary>
        public static void ReportCrossBred(List<Fish> newborn)
        {
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var fry in newborn)
            {
                if (fry == null || !fry.Phenotype.IsHybrid) continue;
                string name = fry.Phenotype.SpeciesName;
                if (!counts.ContainsKey(name))
                {
                    counts[name] = 0;
                    order.Add(name);
                }
                counts[name]++;
            }
            if (order.Count == 0) return;
            var parts = new List<string>();
            foreach (var name in order) parts.Add($"{counts[name]} {name}");
            GameLog.Add($"Cross-bred fry: {string.Join(", ", parts)}!");
            foreach (var fry in newborn)
                if (fry != null && fry.Phenotype.IsHybrid)
                {
                    Fx.Text(fry.transform.position + Vector3.up * 0.05f, "Cross-bred!", UiTheme.Sun, 16f, 2.2f);
                    break;
                }
        }

        /// <summary>Time step for a pregnant fish. Returns how many fry she gave birth to (0 while still pregnant).</summary>
        public int AdvancePregnancy()
        {
            if (!IsPregnant || IsDead || Tank == null) return 0;
            if (--StepsToBirth > 0) return 0;

            int born = 0;
            string parents = $"{fishName} x {UnbornFather}";
            var children = new List<Fish>();
            foreach (var genome in unborn)
            {
                if (Tank.IsFull) break;
                var position = Tank.Clamp(transform.position + Random.insideUnitSphere * 0.05f, 0.05f);
                var child = FishFactory.Spawn(genome, unbornGeneration, 0.05f, Tank, position, parents);
                children.Add(child);
                born++;
                var rarity = child.Phenotype.RarityLabel;
                Progression.Award(XpTrack.Fishkeeping, Progression.BredFishXp(Balance.Data.progression.xpFryBorn, child.Species, rarity));
                if (rarity == "Rare" || rarity == "Exotic")
                    GameLog.Add($"{rarity} fry! {child.FishName}: {child.Phenotype.Describe()}.");
            }
            Progression.Count(GoalKind.BreedFish, born);
            ReportCrossBred(children);
            if (born > 0)
            {
                Fx.Text(transform.position + Vector3.up * 0.06f, born == 1 ? "+1 fry!" : $"+{born} fry!", UiTheme.Hibiscus, 20f, 2f);
                Fx.Bubbles(transform.position, 6, Tank.SurfaceY, 0.04f);
            }
            int lost = unborn.Count - born;
            unborn.Clear();
            BreedCooldown = Mathf.Max(0, GameConfig.Active.breedingCooldownSteps - Perks.Count(PerkEffect.ShorterRest));
            GameLog.Add($"{fishName} the {Species} gave birth to {born} fry (generation {unbornGeneration})." + (lost > 0 ? $" {lost} didn't make it: the tank is full." : ""));
            return born;
        }

        public void Die(string cause)
        {
            if (IsDead) return;
            if (IsPregnant) unborn.Clear();
            IsDead = true;
            CauseOfDeath = cause;
            Reserved = false;
            if (Tank != null) Tank.AddCorpse(this);
            GameLog.Add($"{fishName} the {SpeciesLabel} died of {cause}.");
            Fx.Text(transform.position + Vector3.up * 0.05f, "RIP", new Color(0.8f, 0.85f, 0.9f), 16f, 2f);
            Swimmer.enabled = false;
            Visual.ShowIllness(null, 0f);
            Visual.SetTint(new Color(0.75f, 0.75f, 0.7f));
            StartCoroutine(FloatUp());
        }

        /// <summary>Caught and eaten by a predator in its tank: gone without a trace.</summary>
        public void Eaten(Fish predator)
        {
            if (IsDead) return;
            if (IsPregnant) unborn.Clear();
            IsDead = true;
            CauseOfDeath = $"eaten by {predator.FishName}";
            Reserved = false;
            GameLog.Add($"{predator.FishName} the {predator.Species} ate {fishName} the {SpeciesLabel}!");
            RemoveFromTank();
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
