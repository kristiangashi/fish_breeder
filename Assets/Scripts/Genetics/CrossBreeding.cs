using System;
using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder.Genetics
{
    /// <summary>Two species that can cross-breed (either way round).</summary>
    [Serializable]
    public class CrossPair
    {
        public string a;
        public string b;

        public bool Matches(string x, string y) => (a == x && b == y) || (a == y && b == x);
    }

    /// <summary>
    /// One hybrid look: the body of one species with the fins of another. Set per combination in
    /// the balance console: how likely a cross comes out this way round, and what it's worth.
    /// </summary>
    [Serializable]
    public class HybridCombo
    {
        [Tooltip("Species id of the body.")]
        public string body;
        [Tooltip("Species id of the fins (dorsal, anal, pelvic and pectoral).")]
        public string fins;
        [Tooltip("Shown in game instead of \"<body> with <fins> fins\". Leave empty for that.")]
        public string name = "";
        [Tooltip("Relative chance a fry of this cross comes out this way round (the two ways are compared).")]
        public float chance = 1f;
        [Tooltip("What a healthy, common-looking adult is worth; replaces the body species' base value.")]
        public int value = 60;
    }

    [Serializable]
    public class CrossBreedingBalance
    {
        [Tooltip("Chance breeding at time steps is this much likelier for same-species pairs than for a cross (0.5 = a cross is half as likely).")]
        public float crossChanceMultiplier = 0.5f;
        [Tooltip("Chance a hybrid's tail comes from the same parent as its fins; otherwise it comes from the body's parent.")]
        public float tailWithFins = 0.75f;
        public List<CrossPair> pairs = CrossBreeding.DefaultPairs();
        public List<HybridCombo> combos = new List<HybridCombo>();
    }

    /// <summary>
    /// Which species can cross-breed and what their young look like. A cross between two species
    /// gives fry with the body of one parent and the fins of the other (the tail usually follows
    /// the fins); which way round is rolled from the two combos' chances. Hybrids breeding on pass
    /// each part down from either parent, but only ever into combinations that a compatible pair
    /// could make. The body decides everything else: needs, behaviour, how it's born.
    /// </summary>
    public static class CrossBreeding
    {
        static CrossBreedingBalance Data
        {
            get
            {
                var data = Balance.Data;
                if (data.crossBreeding == null) data.crossBreeding = new CrossBreedingBalance();
                return data.crossBreeding;
            }
        }

        /// <summary>
        /// Real hybrids fishkeepers know: comet and fantail goldfish (one species), guppies and
        /// mollies (both Poecilia), and green terrors with electric blue acaras (both Andinoacara).
        /// More can be added in the balance console.
        /// </summary>
        public static List<CrossPair> DefaultPairs() => new List<CrossPair>
        {
            new CrossPair { a = "CometGoldfish", b = "FancyGoldfish" },
            new CrossPair { a = "Guppy", b = "BlackMolly" },
            new CrossPair { a = "GreenTerror", b = "BlueAcara" },
        };

        public static bool Compatible(Species a, Species b) => a != null && b != null && Compatible(a.id, b.id);

        public static bool Compatible(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (a == b) return true;
            foreach (var pair in Data.pairs)
                if (pair != null && pair.Matches(a, b)) return true;
            return false;
        }

        /// <summary>The combo for this body and fins (made with default values if the file has none yet).</summary>
        public static HybridCombo Combo(string body, string fins)
        {
            if (body == fins) return null;
            foreach (var combo in Data.combos)
                if (combo != null && combo.body == body && combo.fins == fins) return combo;
            var made = MakeDefault(body, fins, Species.All);
            Data.combos.Add(made);
            return made;
        }

        /// <summary>A hybrid is worth more than either parent: half as much again as the two together.</summary>
        public static HybridCombo MakeDefault(string body, string fins, IReadOnlyList<Species> species)
        {
            Species b = null, f = null;
            foreach (var s in species)
            {
                if (s.id == body) b = s;
                if (s.id == fins) f = s;
            }
            int value = Mathf.RoundToInt(((b != null ? b.baseValue : 20) + (f != null ? f.baseValue : 20)) * 1.5f);
            return new HybridCombo { body = body, fins = fins, chance = 1f, value = value };
        }

        /// <summary>Adds the two combos of every compatible pair that the file doesn't have yet.</summary>
        public static void FillCombos(CrossBreedingBalance data, IReadOnlyList<Species> species)
        {
            if (data.pairs == null) data.pairs = new List<CrossPair>();
            if (data.combos == null) data.combos = new List<HybridCombo>();
            foreach (var pair in data.pairs)
            {
                if (pair == null || string.IsNullOrEmpty(pair.a) || string.IsNullOrEmpty(pair.b) || pair.a == pair.b) continue;
                AddIfMissing(data, pair.a, pair.b, species);
                AddIfMissing(data, pair.b, pair.a, species);
            }
        }

        static void AddIfMissing(CrossBreedingBalance data, string body, string fins, IReadOnlyList<Species> species)
        {
            foreach (var combo in data.combos)
                if (combo != null && combo.body == body && combo.fins == fins) return;
            data.combos.Add(MakeDefault(body, fins, species));
        }

        public static float CrossChanceMultiplier => Mathf.Clamp01(Data.crossChanceMultiplier);

        /// <summary>
        /// Sets which species each part of a newborn comes from. Pure same-species parents give pure
        /// young; a first cross rolls which parent gives the body (from the combos' chances), the
        /// other gives the fins, and the tail usually follows the fins. With hybrid parents every part
        /// comes from either parent, as long as the result is a combination a compatible pair could make.
        /// </summary>
        public static void InheritParts(Genome child, Genome mother, Genome father)
        {
            string mb = mother.species, mf = mother.FinSpecies, mt = mother.TailSpecies;
            string fb = father.species, ff = father.FinSpecies, ft = father.TailSpecies;
            bool motherPure = mb == mf && mb == mt;
            bool fatherPure = fb == ff && fb == ft;

            if (motherPure && fatherPure && mb == fb)
            {
                child.species = mb;
                child.finSpecies = child.tailSpecies = "";
                return;
            }

            string body, fins, tail;
            if (motherPure && fatherPure)
            {
                var mBody = Combo(mb, fb);
                var fBody = Combo(fb, mb);
                float m = Mathf.Max(0f, mBody.chance), f = Mathf.Max(0f, fBody.chance);
                bool motherBody = m + f <= 0f ? GameRandom.Chance(0.5f) : GameRandom.Range(0f, m + f) < m;
                body = motherBody ? mb : fb;
                fins = motherBody ? fb : mb;
                tail = GameRandom.Chance(Data.tailWithFins) ? fins : body;
            }
            else
            {
                body = GameRandom.Chance(0.5f) ? mb : fb;
                fins = GameRandom.Chance(0.5f) ? mf : ff;
                tail = GameRandom.Chance(0.5f) ? mt : ft;
                if (!Compatible(body, fins)) fins = body;
                if (!Compatible(body, tail)) tail = body;
            }
            child.species = body;
            child.finSpecies = fins == body ? "" : fins;
            child.tailSpecies = tail == body ? "" : tail;
        }

        /// <summary>
        /// What a healthy, common-looking adult of this look is worth: the species' base value for a
        /// pure fish, the combo's value for a cross. A fish with only a foreign tail is halfway between.
        /// </summary>
        public static int BaseValue(Species body, Species fins, Species tail)
        {
            if (body == null) return 1;
            if (fins != null && fins != body) return Combo(body.id, fins.id).value;
            if (tail != null && tail != body) return Mathf.RoundToInt((body.baseValue + Combo(body.id, tail.id).value) * 0.5f);
            return body.baseValue;
        }

        /// <summary>The look's name: the combo's own name, or "Guppy with Black Molly fins".</summary>
        public static string Name(Species body, Species fins, Species tail)
        {
            if (body == null) return "";
            bool finsDiffer = fins != null && fins != body;
            bool tailDiffers = tail != null && tail != body;
            if (finsDiffer)
            {
                var combo = Combo(body.id, fins.id);
                if (combo != null && !string.IsNullOrEmpty(combo.name)) return combo.name;
            }
            if (finsDiffer && tailDiffers && tail == fins) return $"{body.displayName} with {fins.displayName} fins";
            if (finsDiffer && tailDiffers) return $"{body.displayName} with {fins.displayName} fins and {tail.displayName} tail";
            if (finsDiffer) return $"{body.displayName} with {fins.displayName} fins";
            if (tailDiffers) return $"{body.displayName} with {tail.displayName} tail";
            return body.displayName;
        }
    }
}
