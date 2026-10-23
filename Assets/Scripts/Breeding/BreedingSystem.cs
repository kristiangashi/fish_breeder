using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder.Breeding
{
    /// <summary>Game rules around breeding two fish in the tank.</summary>
    public class BreedingSystem : MonoBehaviour
    {
        [SerializeField] Tank tank;

        public void Initialize(Tank targetTank) => tank = targetTank;

        public bool CanBreed(Fish a, Fish b, out string reason)
        {
            reason = null;
            if (a == null || b == null) reason = "Select two fish to breed.";
            else if (a == b) reason = "A fish can't breed with itself.";
            else if (a.IsDead || b.IsDead) reason = "Both fish need to be alive.";
            else if (a.Phenotype.species != b.Phenotype.species) reason = $"A {a.Phenotype.species} can only breed with another {a.Phenotype.species}.";
            else if (a.Stage != LifeStage.Adult) reason = $"{a.FishName} isn't an adult yet.";
            else if (b.Stage != LifeStage.Adult) reason = $"{b.FishName} isn't an adult yet.";
            else if (a.Health.IsSick) reason = $"{a.FishName} is sick.";
            else if (b.Health.IsSick) reason = $"{b.FishName} is sick.";
            else if (a.BreedCooldown > 0f) reason = $"{a.FishName} needs to rest ({a.BreedCooldown:0}s).";
            else if (b.BreedCooldown > 0f) reason = $"{b.FishName} needs to rest ({b.BreedCooldown:0}s).";
            else if (tank.IsFull) reason = "The tank is full.";
            return reason == null;
        }

        public List<Fish> Breed(Fish a, Fish b)
        {
            var fry = new List<Fish>();
            if (!CanBreed(a, b, out var reason))
            {
                GameLog.Add(reason);
                return fry;
            }

            var config = GameConfig.Active;
            int count = Mathf.Min(GameRandom.Range(config.minFryPerBreeding, config.maxFryPerBreeding + 1), tank.FreeSpace);
            var nest = (a.transform.position + b.transform.position) * 0.5f;
            int generation = Mathf.Max(a.Generation, b.Generation) + 1;

            for (int i = 0; i < count; i++)
            {
                var genome = BreedingService.Cross(a.Genome, b.Genome, config.mutationChance, config.mutationStrength);
                var position = tank.Clamp(nest + Random.insideUnitSphere * 0.05f, 0.05f);
                var child = FishFactory.Spawn(genome, generation, 0.05f, tank, position, $"{a.FishName} x {b.FishName}");
                fry.Add(child);

                var rarity = child.Phenotype.RarityLabel;
                if (rarity == "Rare" || rarity == "Exotic")
                    GameLog.Add($"{rarity} fry! {child.FishName}: {child.Phenotype.Describe()}.");
            }

            a.BreedCooldown = config.breedingCooldownSeconds;
            b.BreedCooldown = config.breedingCooldownSeconds;
            GameLog.Add($"{a.FishName} and {b.FishName} had {count} fry (generation {generation}).");
            return fry;
        }
    }
}
