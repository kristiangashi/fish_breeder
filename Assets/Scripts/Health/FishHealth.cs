using FishBreeder.Core;
using FishBreeder.Feeding;
using UnityEngine;

namespace FishBreeder.Health
{
    /// <summary>
    /// Illness for one fish. Every meal is a dice roll for infection; once sick, each second
    /// rolls for recovery, death (more likely as severity rises) and spreading to tank mates.
    /// </summary>
    [RequireComponent(typeof(Fish))]
    public class FishHealth : MonoBehaviour
    {
        Fish fish;

        public DiseaseDefinition Disease { get; private set; }
        public float Severity { get; private set; }
        public bool IsSick => Disease != null;

        public float SwimSpeedMultiplier => IsSick ? Mathf.Lerp(1f, Disease.swimSpeedMultiplier, Mathf.Max(Severity, 0.3f)) : 1f;

        void Awake() => fish = GetComponent<Fish>();

        public float InfectionChanceForMeal(FoodPellet pellet, bool wasStarving)
        {
            var config = GameConfig.Active;
            float chance = config.baseInfectionChancePerMeal * pellet.Contamination;
            if (fish.Tank != null) chance *= fish.Tank.InfectionMultiplier;
            if (wasStarving) chance *= config.starvingInfectionMultiplier;
            chance *= 1f - fish.Phenotype.hardiness * config.hardinessProtection;
            return Mathf.Clamp01(chance);
        }

        public void OnAte(FoodPellet pellet, bool wasStarving)
        {
            if (IsSick || fish.IsDead) return;
            if (!GameRandom.Chance(InfectionChanceForMeal(pellet, wasStarving))) return;

            var disease = DiseaseDefinition.PickWeighted(GameConfig.Active.diseases, GameRandom.Value);
            Infect(disease, pellet.IsSpoiled ? "spoiled food" : "its food");
        }

        public bool Infect(DiseaseDefinition disease, string source)
        {
            if (disease == null || IsSick || fish.IsDead) return false;
            Disease = disease;
            Severity = 0.05f;
            GameLog.Add($"{fish.FishName} caught {disease.displayName} from {source}.");
            return true;
        }

        public bool Treat(Medicine medicine)
        {
            if (!IsSick || fish.IsDead) return false;
            if (medicine != Medicine.None && medicine == Disease.cure)
            {
                GameLog.Add($"{fish.FishName} was cured of {Disease.displayName}.");
                Cure();
                return true;
            }
            GameLog.Add($"{medicine} did nothing for {fish.FishName}'s {Disease.displayName}.");
            return false;
        }

        void Cure()
        {
            Disease = null;
            Severity = 0f;
            fish.Visual.SetTint(Color.white);
        }

        void Update()
        {
            if (!IsSick || fish.IsDead) return;

            float dt = Time.deltaTime;
            float hardiness = fish.Phenotype.hardiness;

            Severity = Mathf.Clamp01(Severity + Disease.progressionPerSecond * (1f - 0.5f * hardiness) * dt);
            fish.Visual.SetTint(Color.Lerp(Color.white, Disease.symptomTint, Severity));

            if (GameRandom.ChancePerSecond(Disease.recoveryPerSecond * (0.5f + hardiness), dt))
            {
                GameLog.Add($"{fish.FishName} recovered from {Disease.displayName}.");
                Cure();
                return;
            }

            if (GameRandom.ChancePerSecond(Disease.lethalityPerSecond * Severity * Severity, dt))
            {
                fish.Die(Disease.displayName);
                return;
            }

            if (Disease.contagionPerSecond > 0f && fish.Tank != null)
                SpreadToTankMates(dt);
        }

        void SpreadToTankMates(float dt)
        {
            var config = GameConfig.Active;
            float radiusSqr = Disease.contagionRadius * Disease.contagionRadius;
            foreach (var other in fish.Tank.Residents)
            {
                if (other == fish || other.IsDead || other.Health.IsSick) continue;
                if ((other.transform.position - transform.position).sqrMagnitude > radiusSqr) continue;

                float resistance = 1f - other.Phenotype.hardiness * config.hardinessProtection;
                if (GameRandom.ChancePerSecond(Disease.contagionPerSecond * resistance, dt))
                    other.Health.Infect(Disease, fish.FishName);
            }
        }
    }
}
