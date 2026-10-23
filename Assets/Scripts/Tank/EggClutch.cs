using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// A clutch of eggs resting on a tank's sand. Eggs hatch into fry at the next time step
    /// (when the shop opens or closes), not in real time.
    /// </summary>
    public class EggClutch : MonoBehaviour
    {
        static readonly List<EggClutch> all = new List<EggClutch>();

        readonly List<Genome> genomes = new List<Genome>();
        int generation = 1;
        string parents;

        public Tank Tank { get; private set; }
        public Species Species { get; private set; }
        public int Count => genomes.Count;

        /// <summary>Eggs from the supplier: wild genes.</summary>
        public static EggClutch Lay(Tank tank, Species species, int count)
        {
            var wild = new List<Genome>();
            for (int i = 0; i < count; i++) wild.Add(Genome.CreateRandom(species));
            return LayFrom(tank, wild, 1, null);
        }

        /// <summary>Eggs laid by a pair in the tank, one genome per egg.</summary>
        public static EggClutch LayFrom(Tank tank, List<Genome> eggs, int generation, string parents)
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
            clutch.genomes.AddRange(eggs);
            clutch.generation = generation;
            clutch.parents = parents;

            var material = MaterialFactory.Create(new Color(1f, 0.93f, 0.75f), 0.8f);
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

        /// <summary>Hatches every clutch in every tank. Returns how many fry were born.</summary>
        public static int HatchAll()
        {
            int born = 0;
            foreach (var clutch in all.ToArray()) born += clutch.Hatch();
            return born;
        }

        int Hatch()
        {
            int born = 0;
            if (Tank != null)
            {
                foreach (var genome in genomes)
                {
                    if (Tank.IsFull) break;
                    var position = Tank.Clamp(transform.position + new Vector3(GameRandom.Range(-0.05f, 0.05f), 0.05f, GameRandom.Range(-0.05f, 0.05f)), 0.05f);
                    FishFactory.Spawn(genome, generation, 0.05f, Tank, position, parents);
                    born++;
                }
            }
            Destroy(gameObject);
            return born;
        }

        void OnDestroy() => all.Remove(this);
    }
}
