using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using FishBreeder.Health;
using UnityEngine;

namespace FishBreeder.Breeding
{
    /// <summary>
    /// Game rules around breeding. A compatible pair is a healthy, fed, unstressed adult male and
    /// female of the same species, both rested, with the female not already pregnant.
    /// Livebearer females get pregnant and give birth some time steps later; egg-layers lay a
    /// clutch that hatches some time steps later. At each time step every compatible pair in a
    /// tank has a chance to breed (much higher in a breeder tank), and the player can also pair
    /// two fish by hand in a breeder tank.
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
            else if (a.Species != b.Species) reason = $"A {a.Species} can only breed with another {a.Species}.";
            else if (a.Sex == b.Sex) reason = $"Both are {a.Sex.ToString().ToLower()}s. You need a male and a female.";
            else reason = WhyNotReady(a, tank) ?? WhyNotReady(b, tank);
            if (reason == null && tank.FreeSpace - EggClutch.EggsIn(tank) <= 0) reason = "The tank is full.";
            return reason == null;
        }

        /// <summary>Why this fish can't breed right now, or null if it can.</summary>
        public static string WhyNotReady(Fish fish, Tank tank)
        {
            if (fish.IsDead) return $"{fish.FishName} is dead.";
            if (fish.Stage != LifeStage.Adult) return $"{fish.FishName} isn't an adult yet.";
            if (fish.IsPregnant) return $"{fish.FishName} is already pregnant.";
            if (fish.Health.IsSick) return $"{fish.FishName} is sick.";
            if (fish.IsStarving) return $"{fish.FishName} is too hungry.";
            if (fish.BreedCooldown > 0) return $"{fish.FishName} needs to rest ({fish.BreedCooldown} more time steps).";
            if (FishHealth.EnvironmentMismatch(fish.Species, tank) > Balance.Data.timeStep.maxStressToBreed)
                return $"{fish.FishName} is too stressed by the water to breed.";
            return null;
        }

        public int Breed(Fish a, Fish b)
        {
            if (!CanBreed(a, b, out var reason))
            {
                GameLog.Add(reason);
                return 0;
            }
            return Conceive(tank, a, b);
        }

        /// <summary>
        /// A compatible pair breeds: the young's genes are fixed now, then the mother carries them
        /// (livebearers) or they wait as eggs on the sand (egg-layers). Returns how many young are on the way.
        /// </summary>
        static int Conceive(Tank tank, Fish a, Fish b)
        {
            var config = GameConfig.Active;
            var species = a.Species;
            var mother = a.Sex == Sex.Female ? a : b;
            var father = mother == a ? b : a;
            int count = Mathf.Min(GameRandom.Range(species.litterMin, species.litterMax + 1), tank.FreeSpace - EggClutch.EggsIn(tank));
            if (count <= 0) return 0;

            int generation = Mathf.Max(a.Generation, b.Generation) + 1;
            var genomes = new List<Genome>();
            for (int i = 0; i < count; i++)
                genomes.Add(BreedingService.Cross(mother.Genome, father.Genome, config.mutationChance, config.mutationStrength));

            father.BreedCooldown = config.breedingCooldownSteps;
            if (species.reproduction == Reproduction.Livebearer)
            {
                mother.BecomePregnant(genomes, generation, father.FishName, species.gestationSteps);
                GameLog.Add($"{mother.FishName} the {species} is pregnant by {father.FishName}! About {count} fry due in {Steps(species.gestationSteps)}.");
            }
            else
            {
                mother.BreedCooldown = config.breedingCooldownSteps;
                EggClutch.LayFrom(tank, genomes, generation, $"{mother.FishName} x {father.FishName}", species.eggHatchSteps);
                GameLog.Add($"{mother.FishName} and {father.FishName} laid {count} {species} eggs. They hatch in {Steps(species.eggHatchSteps)}.");
            }
            return count;
        }

        public static string Steps(int steps) => steps == 1 ? "1 time step" : $"{steps} time steps";

        /// <summary>
        /// Time step: every compatible pair in the tank may breed. Each fish breeds at most once per step.
        /// Returns how many pregnancies and clutches started.
        /// </summary>
        public static int ChanceBreeding(Tank tank, float chance)
        {
            int started = 0;
            var bySpecies = new Dictionary<string, Couples>();
            foreach (var fish in tank.Residents)
            {
                if (WhyNotReady(fish, tank) != null) continue;
                if (!bySpecies.TryGetValue(fish.Species.id, out var group)) bySpecies[fish.Species.id] = group = new Couples();
                (fish.Sex == Sex.Female ? group.females : group.males).Add(fish);
            }
            foreach (var group in bySpecies.Values)
            {
                Shuffle(group.females);
                Shuffle(group.males);
                int pairs = Mathf.Min(group.females.Count, group.males.Count);
                for (int i = 0; i < pairs; i++)
                {
                    if (!GameRandom.Chance(chance)) continue;
                    if (Conceive(tank, group.females[i], group.males[i]) > 0) started++;
                }
            }
            return started;
        }

        /// <summary>
        /// One short line per species in the tank on how close it is to breeding, for the tank panel.
        /// </summary>
        public static List<string> Readiness(Tank tank)
        {
            var lines = new List<string>();
            var order = new List<string>();
            var bySpecies = new Dictionary<string, Couples>();
            var reasons = new Dictionary<string, string>();
            foreach (var fish in tank.Residents)
            {
                if (fish.IsDead) continue;
                string id = fish.Species.id;
                if (!bySpecies.TryGetValue(id, out var group))
                {
                    bySpecies[id] = group = new Couples();
                    order.Add(id);
                }
                string reason = WhyNotReady(fish, tank);
                if (reason == null) (fish.Sex == Sex.Female ? group.females : group.males).Add(fish);
                else if (!reasons.ContainsKey(id)) reasons[id] = reason.Replace(fish.FishName + " is ", "").Replace(fish.FishName + " ", "");
            }
            foreach (var id in order)
            {
                var group = bySpecies[id];
                string name = Species.Get(id).displayName;
                int pairs = Mathf.Min(group.females.Count, group.males.Count);
                if (pairs > 0) lines.Add($"{name}: {(pairs == 1 ? "1 pair" : pairs + " pairs")} ready to breed");
                else if (group.females.Count > 0) lines.Add($"{name}: ready, but no ready male");
                else if (group.males.Count > 0) lines.Add($"{name}: ready, but no ready female");
                else lines.Add($"{name}: not ready ({reasons[id].TrimEnd('.')})");
            }
            return lines;
        }

        /// <summary>Time step for every pregnant fish: count down, and give birth when due. Returns fry born.</summary>
        public static int AdvancePregnancies(IEnumerable<Fish> fishes)
        {
            int born = 0;
            foreach (var fish in fishes)
                if (fish != null && fish.IsPregnant) born += fish.AdvancePregnancy();
            return born;
        }

        class Couples
        {
            public readonly List<Fish> females = new List<Fish>();
            public readonly List<Fish> males = new List<Fish>();
        }

        static void Shuffle(List<Fish> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = GameRandom.Range(0, i + 1);
                var swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }
    }
}
