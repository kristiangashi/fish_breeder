using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder.Health
{
    /// <summary>
    /// What tank mates do to each other at a time step, from the relationship table: predators hunt
    /// (prey that fits in their mouth may be eaten, bigger prey bitten), and in an overcrowded tank
    /// fish that don't get on nip each other. Comfort effects are in <see cref="FishHealth"/>.
    ///
    /// All of it depends on distance: fish only fight or hunt when they come within range of each
    /// other (<see cref="TankMateBalance.fightRange"/>, <see cref="TankMateBalance.huntRange"/>).
    /// Each tank keeps track of how much of the time each pair of kinds spends that close
    /// (<see cref="TankMateBehaviour"/>), so in a big enough tank fish that would fight in a small
    /// one keep apart and leave each other alone.
    /// </summary>
    public static class TankMates
    {
        /// <summary>
        /// How strongly these two kinds go at each other in this tank, 0 (they keep apart) to 1 (as
        /// much as in a small tank), from how much of the time they spend within range.
        /// </summary>
        public static float Strength(Tank tank, Species a, Species b, bool hunting)
        {
            var rules = Balance.Data.tankMates;
            float share = MeetShare(tank, a, b, hunting);
            if (share <= rules.calmShare) return 0f;
            float full = Mathf.Max(rules.calmShare + 0.001f, rules.fullAggressionShare);
            return Mathf.Clamp01((share - rules.calmShare) / (full - rules.calmShare));
        }

        /// <summary>Share of the time fish of these two kinds spend within range of each other here (measured as they swim, or estimated from the tank's size until then).</summary>
        public static float MeetShare(Tank tank, Species a, Species b, bool hunting)
        {
            var behaviour = tank != null ? tank.GetComponent<TankMateBehaviour>() : null;
            if (behaviour != null && behaviour.TryShare(a, b, hunting, out var measured)) return measured;
            return EstimatedShare(tank, hunting);
        }

        /// <summary>Two fish wandering the whole tank: the share of it within range of one of them.</summary>
        public static float EstimatedShare(Tank tank, bool hunting)
        {
            if (tank == null) return 1f;
            var rules = Balance.Data.tankMates;
            float range = hunting ? rules.huntRange : rules.fightRange;
            var size = tank.InnerSize;
            float volume = Mathf.Max(0.01f, size.x * size.y * size.z);
            return Mathf.Clamp01(4f / 3f * Mathf.PI * range * range * range / volume);
        }

        /// <summary>
        /// Fish are fighting in this tank: a grown predator shares it with prey, or (when it's
        /// crowded) grown fish that don't get on nip at each other. Shoppers can see it.
        /// </summary>
        public static bool IsFighting(Tank tank)
        {
            var rules = Balance.Data.tankMates;
            bool crowded = tank.Residents.Count > tank.Capacity * rules.crowdedAbove;
            foreach (var a in tank.Residents)
            {
                if (a == null || a.IsDead || a.Stage != LifeStage.Adult) continue;
                foreach (var b in tank.Residents)
                {
                    if (b == a || b == null || b.IsDead) continue;
                    if (Relationships.Get(a.Species, b.Species) == Relationship.Predator && Strength(tank, a.Species, b.Species, true) > 0f) return true;
                    if (crowded && b.Stage == LifeStage.Adult && Relationships.AreCompetitive(a.Species, b.Species) && Strength(tank, a.Species, b.Species, false) > 0f) return true;
                }
            }
            return false;
        }

        public static void Run(Tank tank, ref int eaten, ref int wounded)
        {
            var rules = Balance.Data.tankMates;

            foreach (var hunter in new List<Fish>(tank.Residents))
            {
                if (hunter == null || hunter.IsDead || hunter.Stage != LifeStage.Adult) continue;
                var prey = new List<Fish>();
                foreach (var other in tank.Residents)
                    if (other != hunter && !other.IsDead && Relationships.Get(hunter.Species, other.Species) == Relationship.Predator) prey.Add(other);
                if (prey.Count == 0) continue;

                var target = prey[GameRandom.Range(0, prey.Count)];
                // It has to find the prey first: in a big tank the two rarely meet.
                float strength = Strength(tank, hunter.Species, target.Species, true);
                if (strength <= 0f) continue;
                bool fits = target.BodyLength <= hunter.BodyLength * rules.preyMaxSizeRatio;
                if (fits && GameRandom.Chance(rules.eatChance * strength))
                {
                    Fx.Text(hunter.transform.position, "CHOMP!", UiTheme.Coral, 20f);
                    target.Eaten(hunter);
                    eaten++;
                }
                else if (GameRandom.Chance(rules.biteChance * strength))
                {
                    target.Health.Injure(rules.biteInjury, $"bitten by {hunter.FishName} the {hunter.Species}");
                    wounded++;
                }
            }

            if (tank.Residents.Count <= tank.Capacity * rules.crowdedAbove) return;
            foreach (var victim in new List<Fish>(tank.Residents))
            {
                if (victim == null || victim.IsDead || victim.Stage != LifeStage.Adult) continue;
                var nippers = new List<Fish>();
                foreach (var other in tank.Residents)
                    if (other != victim && !other.IsDead && other.Stage == LifeStage.Adult && Relationships.AreCompetitive(victim.Species, other.Species)) nippers.Add(other);
                if (nippers.Count == 0) continue;
                var nipper = nippers[GameRandom.Range(0, nippers.Count)];
                if (!GameRandom.Chance(rules.nipChance * Strength(tank, nipper.Species, victim.Species, false))) continue;
                victim.Health.Injure(rules.nipInjury, $"nipped by {nipper.FishName} the {nipper.Species}");
                wounded++;
            }
        }
    }

    /// <summary>
    /// What you see of a predator in the tank (or of fish that don't get on, in a crowded tank):
    /// every few seconds one bolts a little way at another, which darts away. Only for show; the
    /// real hunting and nipping happen at time steps. Shoppers notice it, though.
    /// </summary>
    public class TankMateBehaviour : MonoBehaviour
    {
        Tank tank;
        float nextLunge, nextSample;

        // Share of the time (a running average) fish of two kinds spend within range of each
        // other, by "hunter>prey" or "a>b" for fighting kinds, for the hunting and fighting ranges.
        readonly Dictionary<string, float> huntShares = new Dictionary<string, float>();
        readonly Dictionary<string, float> fightShares = new Dictionary<string, float>();
        const float SampleEvery = 0.5f;
        // Each sample moves the average this far: it follows the last half a minute or so.
        const float SampleWeight = 0.03f;

        void Awake()
        {
            tank = GetComponent<Tank>();
            nextLunge = Time.time + GameRandom.Range(2f, 6f);
            // Tanks take their samples on different frames.
            nextSample = Time.time + Random.Range(0f, SampleEvery);
        }

        static string Key(Species a, Species b) => a.id + ">" + b.id;

        public bool TryShare(Species a, Species b, bool hunting, out float share)
        {
            var shares = hunting ? huntShares : fightShares;
            if (shares.TryGetValue(Key(a, b), out share)) return true;
            return !hunting && shares.TryGetValue(Key(b, a), out share);
        }

        // The tank's living fish by kind, regrouped for each pass: pairs of kinds are looked up once,
        // not once per pair of fish.
        readonly List<Species> kinds = new List<Species>();
        readonly Dictionary<Species, List<Fish>> byKind = new Dictionary<Species, List<Fish>>();

        void Group()
        {
            foreach (var list in byKind.Values) list.Clear();
            kinds.Clear();
            foreach (var fish in tank.Residents)
            {
                if (fish == null || fish.IsDead || fish.Species == null) continue;
                if (!byKind.TryGetValue(fish.Species, out var list)) byKind[fish.Species] = list = new List<Fish>();
                if (list.Count == 0) kinds.Add(fish.Species);
                list.Add(fish);
            }
        }

        /// <summary>Every half second: for each pair of kinds that hunt or fight, how many of their fish are within range right now.</summary>
        void Sample()
        {
            var rules = Balance.Data.tankMates;
            Group();
            foreach (var a in kinds)
                foreach (var b in kinds)
                {
                    if (Relationships.Get(a, b) == Relationship.Predator) Measure(huntShares, a, b, rules.huntRange, true);
                    if (Relationships.AreCompetitive(a, b)) Measure(fightShares, a, b, rules.fightRange, false);
                }
        }

        void Measure(Dictionary<string, float> shares, Species first, Species second, float range, bool hunting)
        {
            int pairs = 0, near = 0;
            float rangeSq = range * range;
            foreach (var a in byKind[first])
            {
                // Only grown fish hunt or fight; a predator hunts prey of any age.
                if (a.Stage != LifeStage.Adult) continue;
                var at = a.transform.position;
                foreach (var b in byKind[second])
                {
                    if (b == a || (!hunting && b.Stage != LifeStage.Adult)) continue;
                    pairs++;
                    if ((at - b.transform.position).sqrMagnitude <= rangeSq) near++;
                }
            }
            if (pairs == 0) return;
            string key = Key(first, second);
            float now = near / (float)pairs;
            float old = shares.TryGetValue(key, out var s) ? s : TankMates.EstimatedShare(tank, hunting);
            shares[key] = Mathf.Lerp(old, now, SampleWeight);
        }

        void Update()
        {
            // Fish in a sleeping tank hold still, so there's nothing new to see or measure.
            if (tank == null || TankActivity.Step(tank) == 0) return;
            Perf.TankMates.Begin();
            Step();
            Perf.TankMates.End();
        }

        void Step()
        {
            if (Time.time >= nextSample)
            {
                nextSample = Time.time + SampleEvery;
                Sample();
            }
            if (Time.time < nextLunge) return;
            nextLunge = Time.time + GameRandom.Range(3f, 8f);

            var pairs = new List<Fish>();
            var rules = Balance.Data.tankMates;
            bool crowded = tank.Residents.Count > tank.Capacity * rules.crowdedAbove;
            Group();
            foreach (var hunterKind in kinds)
                foreach (var preyKind in kinds)
                {
                    bool hunts = Relationships.Get(hunterKind, preyKind) == Relationship.Predator;
                    bool fights = crowded && Relationships.AreCompetitive(hunterKind, preyKind);
                    if (!hunts && !fights) continue;
                    // Only goes for fish that come within range.
                    float range = hunts ? rules.huntRange : rules.fightRange;
                    foreach (var hunter in byKind[hunterKind])
                    {
                        if (hunter.Stage != LifeStage.Adult) continue;
                        var at = hunter.transform.position;
                        foreach (var prey in byKind[preyKind])
                        {
                            if (prey == hunter || (!hunts && prey.Stage != LifeStage.Adult)) continue;
                            if ((at - prey.transform.position).sqrMagnitude > range * range) continue;
                            pairs.Add(hunter);
                            pairs.Add(prey);
                        }
                    }
                }
            if (pairs.Count == 0) return;

            // Closer prey gets picked more often: a predator goes for what's near it.
            int pick = 0;
            float best = float.MaxValue;
            for (int i = 0; i < pairs.Count; i += 2)
            {
                float score = (pairs[i].transform.position - pairs[i + 1].transform.position).sqrMagnitude * GameRandom.Range(0.3f, 1f);
                if (score < best) { best = score; pick = i; }
            }
            var predator = pairs[pick];
            var target = pairs[pick + 1];
            predator.Swimmer.Lunge(target);
            target.Swimmer.Flee(predator.transform.position, 0.12f);
            Fx.Text(target.transform.position + Vector3.up * 0.04f, "!", UiTheme.Sun, 18f, 0.9f);
        }
    }
}
