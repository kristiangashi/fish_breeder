using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
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
        /// <summary>Wounds from bites and nips, and lasting harm from medicine overdoses.</summary>
        public float injury, medicineDamage;
        /// <summary>Health gained from tank mates it likes.</summary>
        public float companionship;
        /// <summary>0..1 comfort level (not a penalty): tank mates, plants, decor, pests, crowding.</summary>
        public float comfortLevel;
        /// <summary>0..1 how far the water is from what the species needs.</summary>
        public float environmentMismatch;
        public float total;

        /// <summary>The factor hurting most, as a cause of death.</summary>
        public string WorstCause(DiseaseDefinition disease, string injuryCause = null)
        {
            float worst = hunger;
            string cause = "starvation";
            if (illness > worst) { worst = illness; cause = disease != null ? disease.displayName : "illness"; }
            if (environment > worst) { worst = environment; cause = "poor water"; }
            if (comfort > worst) { worst = comfort; cause = "stress"; }
            if (oldAge > worst) { worst = oldAge; cause = "old age"; }
            if (injury > worst) { worst = injury; cause = string.IsNullOrEmpty(injuryCause) ? "its wounds" : $"wounds ({injuryCause})"; }
            if (medicineDamage > worst) { worst = medicineDamage; cause = "medicine poisoning"; }
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

        /// <summary>Wounds from bites and nips (0..1). They heal a little every time step.</summary>
        public float Injury { get; private set; }
        public string InjuryCause { get; private set; }
        /// <summary>Medicine built up in the fish. Too much at once overdoses it.</summary>
        public float MedicineLoad { get; private set; }
        /// <summary>Comfort lost to medicine it didn't need (0..1), wearing off over time steps.</summary>
        public float MedicineStress { get; private set; }
        /// <summary>Overdoses so far: each takes health and value for good, and enough of them make it infertile.</summary>
        public int MedicineDamage { get; private set; }
        public bool Infertile => MedicineDamage > 0 && MedicineDamage >= Balance.Data.medicine.infertileAtDamage;

        public enum MedicineResult { Cured, NotNeeded, Wrong }

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

        public void Injure(float amount, string cause)
        {
            if (Owner.IsDead || amount <= 0f) return;
            Injury = Mathf.Clamp01(Injury + amount);
            InjuryCause = cause;
        }

        /// <summary>
        /// A dose of medicine went into this fish's tank. It cures the illness it's meant for; a fish
        /// that didn't need it is stressed by it, and too much medicine overdoses any fish.
        /// </summary>
        public MedicineResult ReceiveMedicine(Medicine medicine)
        {
            if (Owner.IsDead) return MedicineResult.NotNeeded;
            var rules = Balance.Data.medicine;
            MedicineResult result;
            if (IsSick && medicine != Medicine.None && medicine == Disease.cure)
            {
                GameLog.Add($"{Owner.FishName} was cured of {Disease.displayName}.");
                Cure();
                Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpCureFish);
                MedicineLoad += rules.loadPerNeededDose;
                result = MedicineResult.Cured;
            }
            else
            {
                MedicineStress = Mathf.Clamp01(MedicineStress + rules.stressPerDose);
                MedicineLoad += rules.loadPerDose;
                result = IsSick ? MedicineResult.Wrong : MedicineResult.NotNeeded;
            }
            if (MedicineLoad >= rules.overdoseLoad && MedicineDamage < rules.maxDamage)
            {
                MedicineDamage++;
                MedicineLoad = rules.overdoseLoad * 0.5f;
                GameLog.Add($"{Owner.FishName} has had too much medicine and is damaged for good" + (Infertile ? " (it can't breed any more)." : "."));
            }
            return result;
        }

        /// <summary>Re-applies the illness tint, e.g. after the model is rebuilt.</summary>
        public void RefreshTint() => Owner.Visual.SetTint(IsSick ? Color.Lerp(Color.white, Disease.symptomTint, Severity) : Color.white);

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

            var medicine = Balance.Data.medicine;
            Injury = Mathf.Max(0f, Injury - Balance.Data.tankMates.injuryHealPerStep);
            MedicineLoad = Mathf.Max(0f, MedicineLoad - medicine.loadClearedPerStep);
            MedicineStress = Mathf.Max(0f, MedicineStress - medicine.stressRecoveryPerStep);

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

            var mates = Balance.Data.tankMates;
            report.comfortLevel = 1f;
            if (tank != null)
            {
                report.deadTankmates = tank.Corpses.Count * weights.perDeadTankmate;
                report.comfortLevel = Comfort(fish, tank, weights, null, out int liked);
                report.companionship = liked * mates.preferredHealth;
                report.environmentMismatch = EnvironmentMismatch(species, tank);
            }
            report.comfort = (1f - report.comfortLevel) * weights.comfortWeight;
            report.environment = report.environmentMismatch * weights.environmentWeight;
            report.injury = Injury * mates.injuryWeight;
            report.medicineDamage = MedicineDamage * Balance.Data.medicine.damageHealth;

            report.total = Mathf.Clamp01(1f - report.hunger - report.illness - report.comfort - report.environment - report.oldAge - report.deadTankmates
                                         - report.injury - report.medicineDamage + report.companionship);
            return report;
        }

        /// <summary>What is making this fish more or less comfortable, for its status window.</summary>
        public List<string> ComfortNotes()
        {
            var notes = new List<string>();
            if (Owner.Tank != null) Comfort(Owner, Owner.Tank, Balance.Data.health, notes, out _);
            return notes;
        }

        float Comfort(Fish fish, Tank tank, HealthBalance weights, List<string> notes, out int likedKinds)
        {
            var species = fish.Species;
            var mates = Balance.Data.tankMates;
            float comfort = 0.75f;
            likedKinds = 0;
            if (species.likesPlants && tank.HasPlants) comfort += weights.plantsBonus;

            // Tank mates, once per kind of fish (its own kind counts too: two male bettas fight).
            int sameKind = 0;
            var seen = new HashSet<string>();
            foreach (var other in tank.Residents)
            {
                if (other == fish || other.IsDead) continue;
                var otherSpecies = other.Species;
                if (otherSpecies.id == species.id) sameKind++;
                if (!seen.Add(otherSpecies.id)) continue;
                string name = otherSpecies.id == species.id ? $"other {species.displayName}s" : otherSpecies.displayName;
                if (Relationships.Get(otherSpecies, species) == Relationship.Predator)
                {
                    comfort -= mates.predatorFear;
                    notes?.Add($"afraid of the {name}");
                }
                if (Relationships.AreCompetitive(species, otherSpecies))
                {
                    comfort -= mates.competitiveStress;
                    notes?.Add($"fights with {name}");
                }
                if (Relationships.Get(species, otherSpecies) == Relationship.Preferred && likedKinds < 2)
                {
                    likedKinds++;
                    comfort += mates.preferredComfort;
                    notes?.Add($"likes the {name}");
                }
            }
            if (species.schoolSize > 0 && sameKind + 1 < species.schoolSize)
            {
                comfort -= weights.tooFewSchoolmates;
                notes?.Add($"wants {species.schoolSize}+ of its kind");
            }

            if (tank.DecorCount > weights.idealDecorMax) { comfort -= 0.15f; notes?.Add("too cluttered"); }
            else if (tank.DecorCount < weights.idealDecorMin) { comfort -= 0.1f; notes?.Add("too bare"); }
            else comfort += 0.1f;

            if (tank.HasPests) { comfort -= weights.pestsComfortLoss; notes?.Add("pests"); }
            if (tank.Residents.Count > tank.Capacity * 0.75f) { comfort -= 0.15f; notes?.Add("crowded"); }
            if (MedicineStress > 0.01f) { comfort -= MedicineStress; notes?.Add("upset by medicine"); }
            return Mathf.Clamp01(comfort);
        }

        /// <summary>0 when the water suits the species perfectly, 1 when it's hopeless.</summary>
        public static float EnvironmentMismatch(Genetics.Species species, Tank tank)
        {
            var weights = Balance.Data.health;
            float temp = OutsideBy(tank.Temperature, species.tempMin, species.tempMax) * weights.tempPenaltyPerDegree;
            float ph = OutsideBy(tank.Ph, species.phMin, species.phMax) * weights.phPenaltyPerUnit;
            float salt = OutsideBy(tank.Salinity, species.salinityMin, species.salinityMax) * weights.saltPenaltyPerPpt;
            float mismatch = temp + ph + salt
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
