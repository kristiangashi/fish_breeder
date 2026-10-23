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

            var disease = DiseaseDefinition.PickWeighted(GameConfig.Active.diseases, GameRandom.Value);
            Infect(disease, pellet.IsSpoiled ? "spoiled food" : "its food");
        }

        public bool Infect(DiseaseDefinition disease, string source)
        {
            if (disease == null || IsSick || Owner.IsDead) return false;
            Disease = disease;
            Severity = 0.05f;
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

        void Update()
        {
            if (!IsSick || Owner.IsDead) return;

            float dt = Time.deltaTime;
            float hardiness = Owner.Phenotype.hardiness;

            Severity = Mathf.Clamp01(Severity + Disease.progressionPerSecond * (1f - 0.5f * hardiness) * dt);
            Owner.Visual.SetTint(Color.Lerp(Color.white, Disease.symptomTint, Severity));

            if (GameRandom.ChancePerSecond(Disease.recoveryPerSecond * (0.5f + hardiness), dt))
            {
                GameLog.Add($"{Owner.FishName} recovered from {Disease.displayName}.");
                Cure();
                return;
            }

            if (GameRandom.ChancePerSecond(Disease.lethalityPerSecond * Severity * Severity, dt))
            {
                Owner.Die(Disease.displayName);
                return;
            }

            if (Disease.contagionPerSecond > 0f && Owner.Tank != null)
                SpreadToTankMates(dt);
        }

        void SpreadToTankMates(float dt)
        {
            var config = GameConfig.Active;
            float radiusSqr = Disease.contagionRadius * Disease.contagionRadius;
            foreach (var other in Owner.Tank.Residents)
            {
                if (other == Owner || other.IsDead || other.Health.IsSick) continue;
                if ((other.transform.position - transform.position).sqrMagnitude > radiusSqr) continue;

                float resistance = 1f - other.Phenotype.hardiness * config.hardinessProtection;
                if (GameRandom.ChancePerSecond(Disease.contagionPerSecond * resistance, dt))
                    other.Health.Infect(Disease, Owner.FishName);
            }
        }
    }
}
