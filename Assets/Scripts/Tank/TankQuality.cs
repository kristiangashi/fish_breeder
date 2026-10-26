using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// A tank's quality score: points for its equipment (more for better models) and its
    /// decorations (more for plants and for variety). Reaching the next size's quality lets the
    /// tank be upgraded to that size, and bigger tanks can keep fish that want more room
    /// (<see cref="Species.minTankSize"/>): rarer fish need better aquariums.
    /// </summary>
    public static class TankQuality
    {
        public struct Part
        {
            public string label;
            public int points;
        }

        public static int Score(Tank tank) => Mathf.RoundToInt(Points(tank, null));

        /// <summary>What the score is made of, one line per piece of equipment and per kind of decoration bonus.</summary>
        public static List<Part> Breakdown(Tank tank)
        {
            var parts = new List<Part>();
            Points(tank, parts);
            return parts;
        }

        static float Points(Tank tank, List<Part> parts)
        {
            var q = Balance.Data.quality;
            float total = 0f;
            foreach (var kind in Equipment.Kinds)
            {
                var item = tank.GetEquipment(kind);
                if (item == null) continue;
                float points = q.equipmentPoints + q.pointsPerEquipmentTier * item.tier;
                if (item.broken) points *= q.brokenEquipmentShare;
                total += points;
                if (parts != null) parts.Add(new Part { label = item.broken ? $"{item.Name} (broken)" : item.Name, points = Mathf.RoundToInt(points) });
            }
            if (tank.Decor != null)
            {
                var ids = tank.Decor.Ids;
                int plants = 0;
                var kinds = new HashSet<string>();
                foreach (var id in ids)
                {
                    kinds.Add(id);
                    var item = TankDecor.Find(id);
                    if (item != null && item.plant) plants++;
                }
                float decor = ids.Count * q.decorationPoints, plant = plants * q.plantPoints, variety = kinds.Count * q.varietyPoints;
                total += decor + plant + variety;
                if (parts != null && ids.Count > 0)
                {
                    parts.Add(new Part { label = $"{ids.Count} decoration{(ids.Count == 1 ? "" : "s")}", points = Mathf.RoundToInt(decor) });
                    if (plants > 0) parts.Add(new Part { label = $"{plants} plant{(plants == 1 ? "" : "s")}", points = Mathf.RoundToInt(plant) });
                    parts.Add(new Part { label = $"{kinds.Count} different kind{(kinds.Count == 1 ? "" : "s")}", points = Mathf.RoundToInt(variety) });
                }
            }
            return total;
        }

        public static string SizeName(int size)
        {
            var sizes = Balance.Data.store.tankSizes;
            if (sizes == null || sizes.Count == 0) return "Standard";
            return sizes[Mathf.Clamp(size, 0, sizes.Count - 1)].name;
        }

        /// <summary>The smallest tank size a fish of this genome will live in: the biggest need of its body, fins and tail species.</summary>
        public static int MinSize(Genome genome)
        {
            int size = Species.Get(genome.species).minTankSize;
            size = Mathf.Max(size, Species.Get(genome.FinSpecies).minTankSize);
            return Mathf.Max(size, Species.Get(genome.TailSpecies).minTankSize);
        }

        public static bool Fits(Species species, Tank tank) => species.minTankSize <= tank.SizeIndex;

        /// <summary>"Wants a Large tank or bigger", or null when the tank is big enough.</summary>
        public static string TooSmallFor(int minSize, Tank tank) => minSize <= tank.SizeIndex ? null : $"Wants a {SizeName(minSize)} tank or bigger";

        /// <summary>
        /// The fish that need a tank of exactly this size, for the upgrade button: discovered kinds
        /// by name, the rest counted ("Angelfish, Oscar and 2 kinds not found yet"). Null if none.
        /// </summary>
        public static string KindsNeeding(int size)
        {
            var names = new List<string>();
            int unknown = 0;
            foreach (var species in Species.All)
            {
                if (species.minTankSize != size) continue;
                if (Journal.IsDiscovered(species)) names.Add(species.displayName);
                else unknown++;
            }
            if (unknown > 0) names.Add($"{unknown} kind{(unknown == 1 ? "" : "s")} of fish you haven't found yet");
            if (names.Count == 0) return null;
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
        }
    }
}
