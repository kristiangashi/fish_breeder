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
    /// The score sets the aquarium's level: 1 to start, 2 at the Large tank's quality, 3 at the
    /// Grand tank's. Fish that need a bigger tank also need its level, so if decorations or
    /// equipment come out and the level drops below what they need, they're unhappy, and more so
    /// the further the quality falls (see <see cref="FishBreeder.Health.FishHealth"/>).
    /// </summary>
    public static class TankQuality
    {
        public struct Part
        {
            public string label;
            public int points;
        }

        /// <summary>A change to try before making it, to see what the quality would be afterwards.</summary>
        public struct WhatIf
        {
            /// <summary>A placed decoration taken out (its index), or -1.</summary>
            public int removeDecoration;
            /// <summary>A decoration put in, or null.</summary>
            public string addDecoration;
            /// <summary>The background taken down.</summary>
            public bool removeBackground;
            /// <summary>Equipment swapped: this kind gets this model (-1 takes it out).</summary>
            public bool changeEquipment;
            public EquipmentKind kind;
            public int tier;

            public static WhatIf None => new WhatIf { removeDecoration = -1 };
            public static WhatIf RemoveDecoration(int index) => new WhatIf { removeDecoration = index };
            public static WhatIf RemoveBackground => new WhatIf { removeDecoration = -1, removeBackground = true };
            public static WhatIf Equip(EquipmentKind kind, int tier) => new WhatIf { removeDecoration = -1, changeEquipment = true, kind = kind, tier = tier };
        }

        public static int Score(Tank tank) => Mathf.RoundToInt(Points(tank, null, WhatIf.None));

        /// <summary>What the score would be after a change.</summary>
        public static int ScoreAfter(Tank tank, WhatIf change) => Mathf.RoundToInt(Points(tank, null, change));

        // ------------------------------------------------------------ levels

        /// <summary>The aquarium level a score reaches: 1, then one more for each tank size's quality reached.</summary>
        public static int LevelFor(int score)
        {
            var sizes = Balance.Data.store.tankSizes;
            int level = 1;
            if (sizes != null)
                for (int i = 1; i < sizes.Count; i++)
                    if (sizes[i].qualityNeeded > 0 && score >= sizes[i].qualityNeeded) level = Mathf.Max(level, i + 1);
            return level;
        }

        public static int Level(Tank tank) => LevelFor(Score(tank));

        public static int MaxLevel => Mathf.Max(1, Balance.Data.store.tankSizes != null ? Balance.Data.store.tankSizes.Count : 1);

        /// <summary>Quality a level starts at (0 for level 1).</summary>
        public static int QualityForLevel(int level)
        {
            var sizes = Balance.Data.store.tankSizes;
            if (level <= 1 || sizes == null || sizes.Count == 0) return 0;
            return sizes[Mathf.Clamp(level - 1, 0, sizes.Count - 1)].qualityNeeded;
        }

        /// <summary>The level a fish needs: the level of the smallest tank it lives in (1 for fish happy anywhere).</summary>
        public static int LevelNeeded(Fish fish) => fish.MinTankSize + 1;

        /// <summary>
        /// A warning before a change that drops the level below what fish in the tank need: who
        /// they are and what it'll do. Null when nobody minds.
        /// </summary>
        public static string WarnIfDrops(Tank tank, WhatIf change, string what)
        {
            int now = Score(tank), after = ScoreAfter(tank, change);
            if (after >= now) return null;
            int level = LevelFor(after);
            var kinds = new SortedDictionary<string, int>();
            foreach (var fish in tank.Residents)
            {
                if (fish == null || fish.IsDead || LevelNeeded(fish) <= level) continue;
                string name = fish.IdentityHidden ? "mystery fry" : fish.Species.displayName;
                kinds[name] = (kinds.TryGetValue(name, out var n) ? n : 0) + 1;
            }
            if (kinds.Count == 0) return null;
            var names = new List<string>();
            foreach (var pair in kinds) names.Add(pair.Value == 1 ? $"a {pair.Key}" : $"{pair.Value} {pair.Key}");
            string who = names.Count == 1 ? names[0] : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
            int total = 0;
            foreach (var n in kinds.Values) total += n;
            return $"{what} drops this aquarium to quality {after} (level {level}). {Capitalise(who)} here need{(total == 1 ? "s" : "")} a higher level: {(total == 1 ? "it'll" : "they'll")} be unhappy, and more so the lower it goes.";
        }

        static string Capitalise(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);

        /// <summary>One line per level for the aquarium's level help: the quality it starts at and what it lets the tank keep.</summary>
        public static List<string> LevelLines()
        {
            var lines = new List<string>();
            var sizes = Balance.Data.store.tankSizes;
            for (int level = 1; level <= MaxLevel; level++)
            {
                string size = sizes != null && level - 1 < sizes.Count ? sizes[level - 1].name : "";
                lines.Add(level == 1 ? $"Level 1: any aquarium. Fish that are happy in a {size.ToLower()} tank." : $"Level {level}: quality {QualityForLevel(level)}+. Lets a tank grow to {size.ToLower()}, and keeps the fish that need one happy.");
            }
            return lines;
        }

        /// <summary>What the score is made of, one line per piece of equipment and per kind of decoration bonus.</summary>
        public static List<Part> Breakdown(Tank tank)
        {
            var parts = new List<Part>();
            Points(tank, parts, WhatIf.None);
            return parts;
        }

        static float Points(Tank tank, List<Part> parts, WhatIf change)
        {
            var q = Balance.Data.quality;
            float total = 0f;
            foreach (var kind in Equipment.Kinds)
            {
                var item = tank.GetEquipment(kind);
                int tier = item != null ? item.tier : -1;
                bool broken = item != null && item.broken;
                if (change.changeEquipment && change.kind == kind)
                {
                    tier = change.tier;
                    broken = false;
                }
                if (tier < 0) continue;
                float points = q.equipmentPoints + q.pointsPerEquipmentTier * tier;
                if (broken) points *= q.brokenEquipmentShare;
                total += points;
                if (parts != null && item != null) parts.Add(new Part { label = item.broken ? $"{item.Name} (broken)" : item.Name, points = Mathf.RoundToInt(points) });
            }
            if (tank.Decor != null)
            {
                var ids = new List<string>(tank.Decor.Ids);
                if (change.removeDecoration >= 0 && change.removeDecoration < ids.Count) ids.RemoveAt(change.removeDecoration);
                if (!string.IsNullOrEmpty(change.addDecoration)) ids.Add(change.addDecoration);
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
            var background = change.removeBackground ? null : TankBackgrounds.Find(tank.BackgroundId);
            if (background != null)
            {
                total += q.backgroundPoints;
                if (parts != null) parts.Add(new Part { label = $"{background.name} background", points = Mathf.RoundToInt(q.backgroundPoints) });
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
