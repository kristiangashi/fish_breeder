using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder.Breeding
{
    /// <summary>
    /// Game rules around breeding: a healthy adult male and female of the same species.
    /// Livebearers give birth to fry straight away; egg-layers lay eggs that hatch at the next time step.
    /// The player breeds pairs by hand in a breeder tank; any tank can also have chance pregnancies at time steps.
    /// </summary>
    public class BreedingSystem : MonoBehaviour
    {
        [SerializeField] Tank tank;

        public void Initialize(Tank targetTank) => tank = targetTank;

        public bool CanBreed(Fish a, Fish b, out string reason) => CanPair(tank, a, b, out reason);

        public static bool CanPair(Tank tank, Fish a, Fish b, out string reason)
        {
            reason = null;
            if (a == null || b == null) reason = "Select two fish to breed.";
            else if (a == b) reason = "A fish can't breed with itself.";
            else if (a.IsDead || b.IsDead) reason = "Both fish need to be alive.";
            else if (a.Species != b.Species) reason = $"A {a.Species} can only breed with another {a.Species}.";
            else if (a.Sex == b.Sex) reason = $"Both are {a.Sex.ToString().ToLower()}s. You need a male and a female.";
            else if (a.Stage != LifeStage.Adult) reason = $"{a.FishName} isn't an adult yet.";
            else if (b.Stage != LifeStage.Adult) reason = $"{b.FishName} isn't an adult yet.";
            else if (a.Health.IsSick) reason = $"{a.FishName} is sick.";
            else if (b.Health.IsSick) reason = $"{b.FishName} is sick.";
            else if (a.BreedCooldown > 0) reason = $"{a.FishName} needs to rest ({a.BreedCooldown} more time steps).";
            else if (b.BreedCooldown > 0) reason = $"{b.FishName} needs to rest ({b.BreedCooldown} more time steps).";
            else if (tank.FreeSpace - EggClutch.EggsIn(tank) <= 0) reason = "The tank is full.";
            return reason == null;
        }

        public int Breed(Fish a, Fish b)
        {
            if (!CanBreed(a, b, out var reason))
            {
                GameLog.Add(reason);
                return 0;
            }
            return Produce(tank, a, b);
        }

        /// <summary>Makes the offspring of a valid pair. Returns how many fry or eggs there are.</summary>
        static int Produce(Tank tank, Fish a, Fish b)
        {
            var config = GameConfig.Active;
            var species = a.Species;
            var mother = a.Sex == Sex.Female ? a : b;
            var father = mother == a ? b : a;
            int count = Mathf.Min(GameRandom.Range(species.litterMin, species.litterMax + 1), tank.FreeSpace - EggClutch.EggsIn(tank));
            if (count <= 0) return 0;

            int generation = Mathf.Max(a.Generation, b.Generation) + 1;
            string parents = $"{mother.FishName} x {father.FishName}";
            var genomes = new List<Genome>();
            for (int i = 0; i < count; i++)
                genomes.Add(BreedingService.Cross(mother.Genome, father.Genome, config.mutationChance, config.mutationStrength));

            a.BreedCooldown = config.breedingCooldownSteps;
            b.BreedCooldown = config.breedingCooldownSteps;

            if (species.reproduction == Reproduction.Livebearer)
            {
                foreach (var genome in genomes)
                {
                    var position = tank.Clamp(mother.transform.position + Random.insideUnitSphere * 0.05f, 0.05f);
                    var child = FishFactory.Spawn(genome, generation, 0.05f, tank, position, parents);
                    var rarity = child.Phenotype.RarityLabel;
                    if (rarity == "Rare" || rarity == "Exotic")
                        GameLog.Add($"{rarity} fry! {child.FishName}: {child.Phenotype.Describe()}.");
                }
                GameLog.Add($"{mother.FishName} gave birth to {count} {species} fry (generation {generation}).");
            }
            else
            {
                EggClutch.LayFrom(tank, genomes, generation, parents);
                GameLog.Add($"{mother.FishName} and {father.FishName} laid {count} {species} eggs. They hatch at the next time step.");
            }
            return count;
        }

        /// <summary>Time step: each species with a free healthy pair may breed on its own.</summary>
        public static int ChancePregnancies(Tank tank, float chance)
        {
            int total = 0;
            var bySpecies = new Dictionary<string, List<Fish>>();
            foreach (var fish in tank.Residents)
            {
                if (!bySpecies.TryGetValue(fish.Species.id, out var list)) bySpecies[fish.Species.id] = list = new List<Fish>();
                list.Add(fish);
            }
            foreach (var group in bySpecies.Values)
            {
                Fish female = null, male = null;
                foreach (var fish in group)
                {
                    if (fish.IsDead || fish.Stage != LifeStage.Adult || fish.Health.IsSick || fish.BreedCooldown > 0) continue;
                    if (fish.Sex == Sex.Female && female == null) female = fish;
                    if (fish.Sex == Sex.Male && male == null) male = fish;
                }
                if (female == null || male == null || !GameRandom.Chance(chance)) continue;
                total += Produce(tank, female, male);
            }
            return total;
        }
    }
}
