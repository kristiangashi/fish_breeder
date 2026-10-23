using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Feeding;
using UnityEngine;

namespace FishBreeder.Health
{
    /// <summary>
    /// What is pulling a fish's health down. Each value is how much health that factor takes
    /// (0 = none); <see cref="total"/> is what is left, 0..1.
    /// </summary>
    public struct HealthReport
    {
        public float hunger, illness, comfort, environment, oldAge, deadTankmates;
        /// <summary>0..1 comfort level (not a penalty): tank mates, plants, decor, pests, crowding.</summary>
        public float comfortLevel;
        /// <summary>0..1 how far the water is from what the species needs.</summary>
        public float environmentMismatch;
        public float total;

        /// <summary>The factor hurting most, as a cause of death.</summary>
        public string WorstCause(DiseaseDefinition disease)
        {
            float worst = hunger;
            string cause = "starvation";
            if (illness > worst) { worst = illness; cause = disease != null ? disease.displayName : "illness"; }
            if (environment > worst) { worst = environment; cause = "poor water"; }
            if (comfort > worst) { worst = comfort; cause = "stress"; }
            if (oldAge > worst) { worst = oldAge; cause = "old age"; }
            if (deadTankmates > worst) cause = "rot from dead tank mates";
            return cause;
        }
    }

    /// <summary>
    /// A fish's health and illness. Health is worked out from hunger, illness, comfort, the water
    /// (temperature, pH, salt, quality, oxygen), old age and dead tank mates. Illness progresses,
    /// spreads and heals only at time steps; a fish dies only from the time step's death roll,
    /// which gets likelier the lower its health.
    /// </summary>
    [RequireComponent(typeof(Fish))]
    public class FishHealth : MonoBehaviour
    {
        Fish owner;

        // Looked up lazily: when Fish is added, Unity adds this required component
        // (and runs its Awake) before the Fish component itself exists.
        Fish Owner => owner != null ? owner : owner = GetComponent<Fish>();

        public DiseaseDefinition Disease { get; private set; }
        public float Severity { get; private set; }
        public bool IsSick => Disease != null;

        public float SwimSpeedMultiplier => IsSick ? Mathf.Lerp(1f, Disease.swimSpeedMultiplier, Mathf.Max(Severity, 0.3f)) : 1f;

        public float InfectionChanceForMeal(FoodPellet pellet, bool wasStarving)
        {
            var config = GameConfig.Active;
            float chance = config.baseInfectionChancePerMeal * pellet.Contamination;
            if (Owner.Tank != null) chance *= Owner.Tank.InfectionMultiplier;
            if (wasStarving) chance *= config.starvingInfectionMultiplier;
            chance *= 1f - Owner.Phenotype.hardiness * config.hardinessProtection;
            return Mathf.Clamp01(chance);
        }

        public void OnAte(FoodPellet pellet, bool wasStarving)
        {
            if (IsSick || Owner.IsDead) return;
            if (!GameRandom.Chance(InfectionChanceForMeal(pellet, wasStarving))) return;
            Infect(DiseaseDefinition.PickWeighted(Balance.Data.diseases, GameRandom.Value), pellet.IsSpoiled ? "spoiled food" : "its food");
        }

        public bool Infect(DiseaseDefinition disease, string source)
        {
            if (disease == null || IsSick || Owner.IsDead) return false;
            Disease = disease;
            Severity = 0.1f;
            Owner.Visual.SetTint(Color.Lerp(Color.white, Disease.symptomTint, Severity));
            GameLog.Add($"{Owner.FishName} caught {disease.displayName} from {source}.");
            return true;
        }

        public bool Treat(Medicine medicine)
        {
            if (!IsSick || Owner.IsDead) return false;
            if (medicine != Medicine.None && medicine == Disease.cure)
            {
                GameLog.Add($"{Owner.FishName} was cured of {Disease.displayName}.");
                Cure();
                return true;
            }
            GameLog.Add($"{medicine} did nothing for {Owner.FishName}'s {Disease.displayName}.");
            return false;
        }

        void Cure()
        {
            Disease = null;
            Severity = 0f;
            Owner.Visual.SetTint(Color.white);
        }

        /// <summary>
        /// Time step: illness gets worse, may heal or spread; a healthy fish may catch something.
        /// Returns true if the fish fell ill this step.
        /// </summary>
        public bool OnTimeStep(List<Fish> newlyInfected)
        {
            if (Owner.IsDead) return false;
            var config = GameConfig.Active;
            float hardiness = Owner.Phenotype.hardiness;

            if (IsSick)
            {
                Severity = Mathf.Clamp01(Severity + Disease.progressionPerStep * (1f - 0.5f * hardiness));
                Owner.Visual.SetTint(Color.Lerp(Color.white, Disease.symptomTint, Severity));
                if (GameRandom.Chance(Disease.recoveryChancePerStep * (0.5f + hardiness)))
                {
                    GameLog.Add($"{Owner.FishName} recovered from {Disease.displayName}.");
                    Cure();
                    return false;
                }
                if (Disease.contagionChancePerStep > 0f && Owner.Tank != null)
                {
                    foreach (var other in Owner.Tank.Residents)
                    {
                        if (other == Owner || other.IsDead || other.Health.IsSick || newlyInfected.Contains(other)) continue;
                        float resistance = 1f - other.Phenotype.hardiness * config.hardinessProtection;
                        if (GameRandom.Chance(Disease.contagionChancePerStep * resistance) && other.Health.Infect(Disease, Owner.FishName))
                            newlyInfected.Add(other);
                    }
                }
                return false;
            }

            if (newlyInfected.Contains(Owner)) return false;
            float stress = 1f + (1f - Report().total);
            float chance = Balance.Data.timeStep.diseaseChance * stress * (1f - hardiness * config.hardinessProtection);
            if (Owner.Tank != null) chance *= Owner.Tank.InfectionMultiplier;
            if (!GameRandom.Chance(chance)) return false;
            return Infect(DiseaseDefinition.PickWeighted(Balance.Data.diseases, GameRandom.Value), Owner.Tank != null && Owner.Tank.WaterQuality < 0.5f ? "the dirty water" : "a passing bug");
        }

        public HealthReport Report()
        {
            var report = new HealthReport();
            var fish = Owner;
            var species = fish.Species;
            var tank = fish.Tank;
            var weights = Balance.Data.health;

            if (fish.Hunger > 0.5f) report.hunger = (fish.Hunger - 0.5f) / 0.5f * weights.hungerWeight;
            if (IsSick) report.illness = (0.25f + 0.75f * Severity) * Disease.lethality * weights.illnessWeight;
            if (fish.AgeDays > species.lifespanDays)
                report.oldAge = Mathf.Clamp01((fish.AgeDays - species.lifespanDays) / Mathf.Max(1f, species.lifespanDays * 0.5f)) * weights.oldAgeWeight;

            report.comfortLevel = 1f;
            if (tank != null)
            {
                report.deadTankmates = tank.Corpses.Count * weights.perDeadTankmate;
                report.comfortLevel = Comfort(fish, tank, weights);
                report.environmentMismatch = EnvironmentMismatch(species, tank);
            }
            report.comfort = (1f - report.comfortLevel) * weights.comfortWeight;
            report.environment = report.environmentMismatch * weights.environmentWeight;

            report.total = Mathf.Clamp01(1f - report.hunger - report.illness - report.comfort - report.environment - report.oldAge - report.deadTankmates);
            return report;
        }

        static float Comfort(Fish fish, Tank tank, HealthBalance weights)
        {
            var species = fish.Species;
            float comfort = 0.75f;
            if (species.likesPlants && tank.HasPlants) comfort += weights.plantsBonus;

            int sameKind = 0;
            var disliked = new HashSet<string>();
            foreach (var other in tank.Residents)
            {
                if (other.IsDead) continue;
                var otherSpecies = other.Species;
                if (otherSpecies.id == species.id) sameKind++;
                else if (species.Dislikes(otherSpecies) || otherSpecies.Dislikes(species)) disliked.Add(otherSpecies.id);
            }
            comfort -= disliked.Count * weights.perIncompatibleTankmateSpecies;
            if (species.schoolSize > 0 && sameKind < species.schoolSize) comfort -= weights.tooFewSchoolmates;

            if (tank.DecorCount > weights.idealDecorMax) comfort -= 0.15f;
            else if (tank.DecorCount < weights.idealDecorMin) comfort -= 0.1f;
            else comfort += 0.1f;

            if (tank.HasPests) comfort -= weights.pestsComfortLoss;
            if (tank.Residents.Count > GameConfig.Active.maxFishPerTank * 0.75f) comfort -= 0.15f;
            return Mathf.Clamp01(comfort);
        }

        /// <summary>0 when the water suits the species perfectly, 1 when it's hopeless.</summary>
        public static float EnvironmentMismatch(Genetics.Species species, Tank tank)
        {
            float temp = OutsideBy(tank.Temperature, species.tempMin, species.tempMax) / 4f;
            float ph = OutsideBy(tank.Ph, species.phMin, species.phMax) / 1f;
            float salt = OutsideBy(tank.Salinity, species.salinityMin, species.salinityMax) / 10f;
            float mismatch = temp * 0.5f + ph * 0.35f + salt * 0.8f
                             + (1f - tank.WaterQuality) * 0.5f
                             + (1f - tank.Oxygen) * 0.4f;
            return Mathf.Clamp01(mismatch);
        }

        static float OutsideBy(float value, float min, float max)
        {
            if (value < min) return min - value;
            if (value > max) return value - max;
            return 0f;
        }
    }
}
