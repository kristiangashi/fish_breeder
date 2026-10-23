using System.Collections.Generic;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>Things research teaches about a species, each unlocked at its own research point total.</summary>
    public enum ResearchFact { Temperature, Ph, Plants, School, Breeding, Lifespan }

    /// <summary>
    /// What the player has learned about each species. Every time step, each species living in the
    /// tanks earns research points (times the research speed, which grows with the Research level),
    /// and facts such as its temperature range unlock at their point totals. Two kinds of adult fish
    /// sharing a tank may show how they get on, and a fish eating one of its favourite foods may show
    /// that it loves it. Measuring pH at all needs a Research level of its own.
    /// </summary>
    public static class Research
    {
        static readonly Dictionary<string, float> points = new Dictionary<string, float>();
        static readonly List<string> order = new List<string>();
        static readonly HashSet<string> relationships = new HashSet<string>();
        static readonly HashSet<string> favourites = new HashSet<string>();

        static ResearchBalance Data => Balance.Data.research;

        /// <summary>Species studied so far, in the order they were first kept.</summary>
        public static IReadOnlyList<string> Studied => order;

        public static void Reset()
        {
            points.Clear();
            order.Clear();
            relationships.Clear();
            favourites.Clear();
        }

        /// <summary>Multiplier on everything research does: 1 at Research level 1, more at higher levels.</summary>
        public static float Speed => 1f + Data.speedPerLevel * (Progression.Level(XpTrack.Research) - 1);

        public static bool CanReadPh => Progression.IsUnlocked(XpTrack.Research, Data.phReadingLevel);

        public static float Points(Species species) => species != null && points.TryGetValue(species.id, out var p) ? p : 0f;

        public static float PointsNeeded(ResearchFact fact)
        {
            foreach (var entry in Data.facts)
                if (entry != null && entry.fact == fact) return entry.points;
            return 0f;
        }

        public static bool Knows(Species species, ResearchFact fact)
        {
            if (species == null) return false;
            if (fact == ResearchFact.Ph && !CanReadPh) return false;
            return Points(species) >= PointsNeeded(fact);
        }

        /// <summary>The next fact still to learn about this species, or null when it's all known.</summary>
        public static ResearchFact? NextFact(Species species)
        {
            ResearchFact? next = null;
            float best = float.MaxValue;
            foreach (var entry in Data.facts)
            {
                if (entry == null || Points(species) >= entry.points || entry.points >= best) continue;
                best = entry.points;
                next = entry.fact;
            }
            return next;
        }

        public static bool KnowsRelationship(Species a, Species b) => a != null && b != null && relationships.Contains(PairKey(a.id, b.id));

        public static bool KnowsFavourite(Species species, FoodType food) => species != null && favourites.Contains(species.id + "|" + (int)food);

        static string PairKey(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;

        public static string FactName(ResearchFact fact)
        {
            switch (fact)
            {
                case ResearchFact.Temperature: return "temperature range";
                case ResearchFact.Ph: return "pH range";
                case ResearchFact.Plants: return "feelings about plants";
                case ResearchFact.School: return "need for company";
                case ResearchFact.Breeding: return "breeding";
                default: return "lifespan";
            }
        }

        /// <summary>Time step: species in the tanks earn research points, and adult tank mates may show how they get on.</summary>
        public static void OnTimeStep(IEnumerable<Tank> tanks)
        {
            var kept = new Dictionary<string, Species>();
            var adultsByTank = new List<Dictionary<string, int>>();
            foreach (var tank in tanks)
            {
                var adults = new Dictionary<string, int>();
                foreach (var fish in tank.Residents)
                {
                    if (fish == null || fish.IsDead || fish.IdentityHidden) continue;
                    var species = fish.Species;
                    kept[species.id] = species;
                    if (fish.Stage == LifeStage.Adult) adults[species.id] = (adults.TryGetValue(species.id, out var n) ? n : 0) + 1;
                }
                adultsByTank.Add(adults);
            }

            float speed = Speed;
            foreach (var species in kept.Values) Study(species, Data.pointsPerStep * speed);

            foreach (var adults in adultsByTank)
            {
                var ids = new List<string>(adults.Keys);
                for (int i = 0; i < ids.Count; i++)
                for (int j = i; j < ids.Count; j++)
                {
                    if (i == j && adults[ids[i]] < 2) continue;
                    var a = Species.Get(ids[i]);
                    var b = Species.Get(ids[j]);
                    if (KnowsRelationship(a, b) || !GameRandom.Chance(Mathf.Clamp01(Data.relationshipChance * speed))) continue;
                    relationships.Add(PairKey(a.id, b.id));
                    GameLog.Add($"Research: {DescribeRelationship(a, b)}");
                    Progression.Award(XpTrack.Research, Data.xpRelationship);
                }
            }
        }

        static void Study(Species species, float amount)
        {
            if (!points.ContainsKey(species.id))
            {
                points[species.id] = 0f;
                order.Add(species.id);
            }
            float before = points[species.id];
            points[species.id] = before + amount;
            Progression.Award(XpTrack.Research, Data.xpPerSpeciesPerStep);
            foreach (var entry in Data.facts)
            {
                if (entry == null || before >= entry.points || points[species.id] < entry.points) continue;
                if (entry.fact == ResearchFact.Ph && !CanReadPh) continue;
                GameLog.Add($"Research: you've learned the {species.displayName}'s {FactName(entry.fact)}.");
                Progression.Award(XpTrack.Research, Data.xpFact);
            }
        }

        /// <summary>A fish ate: if it was one of its favourites, it may show it.</summary>
        public static void OnAte(Fish fish, FoodType food)
        {
            var species = fish.Species;
            if (fish.IdentityHidden || !species.favoriteFoods.Contains(food) || KnowsFavourite(species, food)) return;
            if (!GameRandom.Chance(Mathf.Clamp01(Data.favouriteFoodChance * Speed))) return;
            favourites.Add(species.id + "|" + (int)food);
            GameLog.Add($"Research: {species.displayName}s love {FoodPellet.Name(food)}!");
            Progression.Award(XpTrack.Research, Data.xpFavouriteFood);
        }

        /// <summary>One sentence on how two species get on, both ways round.</summary>
        public static string DescribeRelationship(Species a, Species b)
        {
            var ab = Relationships.Get(a, b);
            var ba = Relationships.Get(b, a);
            string an = Plural(a), bn = Plural(b);
            if (a.id == b.id)
            {
                switch (ab)
                {
                    case Relationship.Predator: return $"{an} eat smaller {an}.";
                    case Relationship.Competitive: return $"{an} fight their own kind.";
                    case Relationship.Preferred: return $"{an} are happiest in a group of their own kind.";
                    default: return $"{an} get along fine with each other.";
                }
            }
            if (ab == Relationship.Predator && ba == Relationship.Predator) return $"{an} and {bn} hunt each other.";
            if (ab == Relationship.Predator) return $"{an} hunt {bn}.";
            if (ba == Relationship.Predator) return $"{bn} hunt {an}.";
            if (ab == Relationship.Competitive || ba == Relationship.Competitive) return $"{an} and {bn} nip at each other.";
            if (ab == Relationship.Preferred && ba == Relationship.Preferred) return $"{an} and {bn} do well together.";
            if (ab == Relationship.Preferred) return $"{an} like having {bn} around.";
            if (ba == Relationship.Preferred) return $"{bn} like having {an} around.";
            return $"{an} and {bn} get along fine.";
        }

        public static string Plural(Species species)
        {
            string name = species.displayName;
            if (name.EndsWith("fish") || name.EndsWith("Corydoras Catfish")) return name;
            if (name.EndsWith("y") && !name.EndsWith("ey")) return name.Substring(0, name.Length - 1) + "ies";
            return name + "s";
        }
    }
}
