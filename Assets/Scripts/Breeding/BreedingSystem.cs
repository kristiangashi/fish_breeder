using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using FishBreeder.Health;
using UnityEngine;

namespace FishBreeder.Breeding
{
    /// <summary>
    /// Game rules around breeding. A compatible pair is a healthy, fed, unstressed adult male and
    /// female of the same species (or of two species that can cross-breed, see CrossBreeding), both rested, with the female not already pregnant.
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
            else if (!CrossBreeding.Compatible(a.Species, b.Species)) reason = $"A {a.Species} and a {b.Species} can't breed together.";
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
            if (fish.Health.Infertile) return $"{fish.FishName} can't breed: too much medicine damaged it.";
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

        /// <summary>A litter or clutch size for this species, bigger with the Big families perk (fractions round up by chance).</summary>
        static int LitterSize(Species species)
        {
            float size = GameRandom.Range(species.litterMin, species.litterMax + 1) * Perks.More(PerkEffect.BiggerLitters);
            int whole = Mathf.FloorToInt(size);
            return whole + (GameRandom.Chance(size - whole) ? 1 : 0);
        }

        /// <summary>
        /// A compatible pair breeds: the young's genes are fixed now, then the mother carries them
        /// (livebearers) or they wait as eggs on the sand (egg-layers). Returns how many young are on the way.
        /// </summary>
        static int Conceive(Tank tank, Fish a, Fish b)
        {
            var config = GameConfig.Active;
            var mother = a.Sex == Sex.Female ? a : b;
            var father = mother == a ? b : a;
            // The mother's species decides how the young come: live fry or eggs, and how many.
            var species = mother.Species;
            bool cross = mother.Species != father.Species;
            int count = Mathf.Min(LitterSize(species), tank.FreeSpace - EggClutch.EggsIn(tank));
            if (count <= 0) return 0;

            int generation = Mathf.Max(a.Generation, b.Generation) + 1;
            var genomes = new List<Genome>();
            for (int i = 0; i < count; i++)
                genomes.Add(BreedingService.Cross(mother.Genome, father.Genome, Mathf.Clamp01(config.mutationChance * Perks.More(PerkEffect.MoreMutations)), config.mutationStrength));

            int rest = Mathf.Max(0, config.breedingCooldownSteps - Perks.Count(PerkEffect.ShorterRest));
            int gestation = Mathf.Max(1, species.gestationSteps - Perks.Count(PerkEffect.QuickerBirths));
            int hatch = Mathf.Max(1, species.eggHatchSteps - Perks.Count(PerkEffect.QuickerBirths));
            father.BreedCooldown = rest;
            Fx.Text(a.transform.position + Vector3.up * 0.05f, "♥", UiTheme.Hibiscus, 24f, 1.8f);
            Fx.Text(b.transform.position + Vector3.up * 0.05f, "♥", UiTheme.Hibiscus, 24f, 1.8f);
            Progression.Award(XpTrack.Fishkeeping, Progression.BredFishXp(Balance.Data.progression.xpBreedingStarted, species, null));
            string crossNote = cross ? $" It's a cross with a {father.Species}, so expect some unusual fry!" : "";
            if (species.reproduction == Reproduction.Livebearer)
            {
                mother.BecomePregnant(genomes, generation, father.FishName, gestation);
                GameLog.Add($"{mother.FishName} the {species} is pregnant by {father.FishName}! About {count} fry due in {Steps(gestation)}.{crossNote}");
            }
            else
            {
                mother.BreedCooldown = rest;
                EggClutch.LayFrom(tank, genomes, generation, $"{mother.FishName} x {father.FishName}", hatch);
                string eggs = cross ? $"{species} x {father.Species}" : species.ToString();
                GameLog.Add($"{mother.FishName} and {father.FishName} laid {count} {eggs} eggs. They hatch in {Steps(hatch)}.");
            }
            return count;
        }

        public static string Steps(int steps) => steps == 1 ? "1 time step" : $"{steps} time steps";

        /// <summary>
        /// Time step: every compatible pair in the tank may breed. Each fish breeds at most once per step.
        /// Fish pair up with their own species first; fish left over may then cross with a species
        /// they can cross-breed with, at a lower chance. Returns how many pregnancies and clutches started.
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
            var singleFemales = new List<Fish>();
            var singleMales = new List<Fish>();
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
                for (int i = pairs; i < group.females.Count; i++) singleFemales.Add(group.females[i]);
                for (int i = pairs; i < group.males.Count; i++) singleMales.Add(group.males[i]);
            }

            // Fish without a partner of their own kind may cross with a compatible species.
            float crossChance = Mathf.Clamp01(chance * CrossBreeding.CrossChanceMultiplier * Perks.More(PerkEffect.CrossChance));
            Shuffle(singleFemales);
            Shuffle(singleMales);
            foreach (var female in singleFemales)
            {
                for (int i = 0; i < singleMales.Count; i++)
                {
                    var male = singleMales[i];
                    if (!CrossBreeding.Compatible(female.Species, male.Species)) continue;
                    singleMales.RemoveAt(i);
                    if (GameRandom.Chance(crossChance) && Conceive(tank, female, male) > 0) started++;
                    break;
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
            int mystery = 0;
            foreach (var fish in tank.Residents)
            {
                if (fish.IsDead) continue;
                if (fish.IdentityHidden)
                {
                    mystery++;
                    continue;
                }
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
            for (int i = 0; i < order.Count; i++)
                for (int j = i + 1; j < order.Count; j++)
                {
                    if (!CrossBreeding.Compatible(order[i], order[j])) continue;
                    var x = bySpecies[order[i]];
                    var y = bySpecies[order[j]];
                    bool ready = (x.females.Count > 0 && y.males.Count > 0) || (y.females.Count > 0 && x.males.Count > 0);
                    string pair = $"{Species.Get(order[i]).displayName} x {Species.Get(order[j]).displayName}";
                    lines.Add(ready ? $"{pair}: can cross-breed" : $"{pair}: could cross-breed when ready");
                }
            if (mystery > 0) lines.Add(mystery == 1 ? "1 mystery fry, too small to tell what it is" : $"{mystery} mystery fry, too small to tell what they are");
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
