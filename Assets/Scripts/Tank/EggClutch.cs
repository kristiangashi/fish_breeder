using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// A clutch of eggs resting on a tank's sand. Eggs hatch into fry after a few time steps
    /// (the shop opening or closing), never in real time. Filthy water kills some eggs on the way.
    /// </summary>
    public class EggClutch : MonoBehaviour
    {
        static readonly List<EggClutch> all = new List<EggClutch>();

        readonly List<Genome> genomes = new List<Genome>();
        int generation = 1;
        string parents;

        public Tank Tank { get; private set; }
        /// <summary>The species of the first egg (a supplier box mixes species).</summary>
        public Species Species { get; private set; }
        /// <summary>What the eggs are called: a species for bred eggs, the box name for supplier eggs.</summary>
        public string Label { get; private set; }
        /// <summary>Laid by a pair in the shop (not bought), so hatching counts as breeding.</summary>
        public bool Bred => parents != null;
        public int Count => genomes.Count;
        /// <summary>Time steps until the eggs hatch.</summary>
        public int StepsToHatch { get; private set; }
        public static IReadOnlyList<EggClutch> All => all;

        /// <summary>A box of eggs from the supplier: wild genes, shipped nearly ready, so they hatch at the next time step.</summary>
        public static EggClutch Lay(Tank tank, EggTierBalance tier, List<Genome> eggs)
        {
            var clutch = LayFrom(tank, eggs, 1, null, 1, tier.color, tier.speckled);
            clutch.Label = tier.name.ToLower();
            return clutch;
        }

        /// <summary>Eggs laid by a pair in the tank, one genome per egg.</summary>
        public static EggClutch LayFrom(Tank tank, List<Genome> eggs, int generation, string parents, int stepsToHatch)
            => LayFrom(tank, eggs, generation, parents, stepsToHatch, new Color(1f, 0.93f, 0.75f), false);

        static EggClutch LayFrom(Tank tank, List<Genome> eggs, int generation, string parents, int stepsToHatch, Color color, bool speckled)
        {
            var species = eggs[0].Species;
            int count = eggs.Count;
            var bounds = tank.InnerBounds;
            var spot = tank.RandomFreePoint(0.12f);
            spot.y = bounds.min.y;

            var clutch = new GameObject($"Eggs - {species}").AddComponent<EggClutch>();
            clutch.transform.position = spot;
            clutch.transform.SetParent(tank.transform, true);
            clutch.Tank = tank;
            clutch.Species = species;
            clutch.Label = $"{species} eggs";
            clutch.genomes.AddRange(eggs);
            clutch.generation = generation;
            clutch.parents = parents;
            clutch.StepsToHatch = Mathf.Max(1, stepsToHatch);

            var material = MaterialFactory.Create(color, 0.8f);
            if (speckled) material.mainTexture = ItemPreviews.Speckles(color);
            for (int i = 0; i < count; i++)
            {
                var egg = MaterialFactory.Decor(PrimitiveType.Sphere, "Egg", clutch.transform,
                    new Vector3(GameRandom.Range(-0.025f, 0.025f), 0.008f, GameRandom.Range(-0.025f, 0.025f)), Vector3.one * 0.014f, Color.white);
                egg.GetComponent<Renderer>().sharedMaterial = material;
            }
            all.Add(clutch);
            return clutch;
        }

        /// <summary>Eggs waiting to hatch in a tank; they will need room as fry.</summary>
        public static int EggsIn(Tank tank)
        {
            int eggs = 0;
            foreach (var clutch in all)
                if (clutch.Tank == tank) eggs += clutch.Count;
            return eggs;
        }

        /// <summary>
        /// Time step for every clutch in every tank: filthy water may kill eggs, and clutches that
        /// are due hatch. Returns how many fry were born.
        /// </summary>
        public static int AdvanceAll()
        {
            int born = 0;
            foreach (var clutch in all.ToArray())
            {
                clutch.LoseEggsToDirtyWater();
                if (clutch.genomes.Count == 0)
                {
                    GameLog.Add($"A clutch of {clutch.Label} died in the dirty water.");
                    Destroy(clutch.gameObject);
                    continue;
                }
                if (--clutch.StepsToHatch <= 0) born += clutch.Hatch();
            }
            return born;
        }

        void LoseEggsToDirtyWater()
        {
            if (Tank == null) return;
            float chance = Balance.Data.timeStep.eggLossInFilthyWater * Mathf.Clamp01((0.5f - Tank.WaterQuality) * 2f) * Perks.Less(PerkEffect.HardyEggs);
            if (chance <= 0f) return;
            for (int i = genomes.Count - 1; i >= 0; i--)
            {
                if (!GameRandom.Chance(chance)) continue;
                genomes.RemoveAt(i);
                if (i < transform.childCount) Destroy(transform.GetChild(i).gameObject);
            }
        }

        int Hatch()
        {
            int born = 0;
            if (Tank != null)
            {
                var hatched = new List<Fish>();
                foreach (var genome in genomes)
                {
                    if (Tank.IsFull) break;
                    var position = Tank.Clamp(transform.position + new Vector3(GameRandom.Range(-0.05f, 0.05f), 0.05f, GameRandom.Range(-0.05f, 0.05f)), 0.05f);
                    var fry = FishFactory.Spawn(genome, generation, 0.05f, Tank, position, parents);
                    hatched.Add(fry);
                    born++;
                    var progression = Balance.Data.progression;
                    if (Bred) Progression.Award(XpTrack.Fishkeeping, Progression.BredFishXp(progression.xpFryBorn, fry.Species, fry.Phenotype.RarityLabel));
                    else Progression.Award(XpTrack.Fishkeeping, progression.xpEggHatched);
                }
                Progression.Count(GoalKind.HatchEggs, born);
                Fish.ReportCrossBred(hatched);
                if (born > 0)
                {
                    Fx.Text(transform.position + Vector3.up * 0.08f, born == 1 ? "Hatched!" : $"{born} hatched!", UiTheme.Sun, 20f, 2f);
                    Fx.Bubbles(transform.position + Vector3.up * 0.02f, 8, Tank.SurfaceY, 0.05f);
                }
                if (Bred) Progression.Count(GoalKind.BreedFish, born);
            }
            Destroy(gameObject);
            return born;
        }

        void OnDestroy() => all.Remove(this);
    }
}
