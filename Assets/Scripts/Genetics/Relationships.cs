using System;
using System.Collections.Generic;
using FishBreeder.Core;

namespace FishBreeder.Genetics
{
    /// <summary>How one species behaves towards another when they share a tank.</summary>
    public enum Relationship
    {
        /// <summary>They get along fine.</summary>
        Neutral,
        /// <summary>It attacks the other and tries to eat it if it fits in its mouth.</summary>
        Predator,
        /// <summary>They nip at each other: stressful, and injuries in an overcrowded tank.</summary>
        Competitive,
        /// <summary>It is happier and healthier with the other around.</summary>
        Preferred,
    }

    /// <summary>One cell of the relationship table: how species <see cref="a"/> treats species <see cref="b"/>.</summary>
    [Serializable]
    public class RelationshipBalance
    {
        public string a;
        public string b;
        public Relationship relation;
    }

    /// <summary>
    /// The relationship table, edited in the balance console. Each entry is one way round: "a is a
    /// Predator of b" means a hunts b. Pairs that aren't listed are Neutral. Competitive and
    /// Preferred are normally set both ways; a species can also have a relationship with its own
    /// kind (two male bettas fight, schooling fish like company).
    /// </summary>
    public static class Relationships
    {
        static readonly Dictionary<string, Relationship> lookup = new Dictionary<string, Relationship>();
        static List<RelationshipBalance> builtFrom;
        static int builtCount = -1;

        /// <summary>How <paramref name="a"/> treats <paramref name="b"/>.</summary>
        public static Relationship Get(Species a, Species b)
        {
            if (a == null || b == null) return Relationship.Neutral;
            Refresh();
            return lookup.TryGetValue(Key(a.id, b.id), out var relation) ? relation : Relationship.Neutral;
        }

        /// <summary>True if either nips the other.</summary>
        public static bool AreCompetitive(Species a, Species b) => Get(a, b) == Relationship.Competitive || Get(b, a) == Relationship.Competitive;

        static string Key(string a, string b) => a + "|" + b;

        static void Refresh()
        {
            var list = Balance.Data.relationships;
            if (ReferenceEquals(list, builtFrom) && list != null && list.Count == builtCount) return;
            lookup.Clear();
            builtFrom = list;
            builtCount = list != null ? list.Count : 0;
            if (list == null) return;
            foreach (var entry in list)
                if (entry != null && !string.IsNullOrEmpty(entry.a) && !string.IsNullOrEmpty(entry.b))
                    lookup[Key(entry.a, entry.b)] = entry.relation;
        }

        /// <summary>
        /// Defaults from fishkeeping compatibility guides: big cichlids and goldfish eat anything
        /// that fits in their mouths, angelfish eat neon tetras, tiger barbs nip long fins, male
        /// bettas fight each other and go for guppies' fins, red-tailed sharks defend their patch
        /// against other bottom fish, and schooling fish and classic community pairings do better
        /// together.
        /// </summary>
        public static List<RelationshipBalance> CreateDefaults()
        {
            var list = new List<RelationshipBalance>();

            // Predators: the left fish hunts the right one.
            Eats(list, "Oscar", "NeonTetra", "Guppy", "ZebraDanio", "Platy", "BlackMolly", "Betta", "TigerBarb", "Corydoras");
            Eats(list, "GreenTerror", "NeonTetra", "Guppy", "ZebraDanio", "Platy", "BlackMolly", "Betta");
            Eats(list, "BlueAcara", "NeonTetra", "Guppy");
            Eats(list, "Angelfish", "NeonTetra", "Guppy");
            Eats(list, "CometGoldfish", "NeonTetra", "Guppy");
            Eats(list, "FancyGoldfish", "NeonTetra");

            // Nippers and bullies.
            Both(list, Relationship.Competitive, "TigerBarb", "Angelfish", "Betta", "FancyGoldfish", "Guppy");
            Both(list, Relationship.Competitive, "Betta", "Betta", "Guppy");
            Both(list, Relationship.Competitive, "RedTailShark", "RedTailShark", "SiameseAlgaeEater", "Corydoras");
            Both(list, Relationship.Competitive, "GoldenMbuna", "GoldenMbuna", "Angelfish", "Betta", "Guppy", "BlueAcara", "GreenTerror", "Oscar");
            Both(list, Relationship.Competitive, "GreenTerror", "GreenTerror", "BlueAcara", "Oscar");
            Both(list, Relationship.Competitive, "CometGoldfish", "FancyGoldfish");

            // Wild tetras: the bucktooth eats small fish and bites scales, serpae and black skirt
            // tetras nip long fins, and the big cichlids and angelfish eat the tiny ones.
            Eats(list, "BucktoothTetra", "NeonTetra", "Guppy", "ZebraDanio", "Platy", "RubyTetra", "GlowlightTetra", "FlameTetra", "SilvertipTetra", "RummyNoseTetra");
            Eats(list, "Oscar", "RubyTetra", "GlowlightTetra", "FlameTetra", "SilvertipTetra", "RummyNoseTetra", "EmperorTetra", "LemonTetra", "XRayTetra", "BloodfinTetra", "SerpaeTetra", "PenguinTetra");
            Eats(list, "GreenTerror", "RubyTetra", "GlowlightTetra", "FlameTetra", "SilvertipTetra");
            Eats(list, "Angelfish", "RubyTetra", "GlowlightTetra");
            Both(list, Relationship.Competitive, "SerpaeTetra", "Angelfish", "Betta", "FancyGoldfish", "Guppy", "CongoTetra");
            Both(list, Relationship.Competitive, "BlackSkirtTetra", "Angelfish", "Betta", "Guppy");
            Both(list, Relationship.Competitive, "BucktoothTetra", "Angelfish", "Betta", "CongoTetra", "BleedingHeartTetra");
            foreach (var tetra in new[] { "BloodfinTetra", "SerpaeTetra", "XRayTetra", "BlackSkirtTetra", "RedeyeTetra", "LemonTetra", "BleedingHeartTetra", "BuenosAiresTetra",
                "EmperorTetra", "FlameTetra", "CongoTetra", "PenguinTetra", "GlowlightTetra", "RubyTetra", "SilvertipTetra", "RummyNoseTetra" })
                Both(list, Relationship.Preferred, tetra, tetra, "Corydoras");
            Both(list, Relationship.Preferred, "BucktoothTetra", "BucktoothTetra");

            // Good company.
            Both(list, Relationship.Preferred, "NeonTetra", "NeonTetra", "Corydoras");
            Both(list, Relationship.Preferred, "ZebraDanio", "ZebraDanio", "FancyGoldfish");
            Both(list, Relationship.Preferred, "Corydoras", "Corydoras", "Angelfish");
            Both(list, Relationship.Preferred, "SilverDollar", "SilverDollar", "BlueAcara");
            Both(list, Relationship.Preferred, "TigerBarb", "TigerBarb");
            Both(list, Relationship.Preferred, "Clownfish", "Clownfish");
            Both(list, Relationship.Preferred, "CometGoldfish", "CometGoldfish");
            Both(list, Relationship.Preferred, "FancyGoldfish", "FancyGoldfish");
            return list;
        }

        static void Eats(List<RelationshipBalance> list, string predator, params string[] prey)
        {
            foreach (var p in prey) list.Add(new RelationshipBalance { a = predator, b = p, relation = Relationship.Predator });
        }

        static void Both(List<RelationshipBalance> list, Relationship relation, string a, params string[] others)
        {
            foreach (var b in others)
            {
                list.Add(new RelationshipBalance { a = a, b = b, relation = relation });
                if (b != a) list.Add(new RelationshipBalance { a = b, b = a, relation = relation });
            }
        }
    }
}
