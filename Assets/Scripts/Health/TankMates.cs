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
    /// </summary>
    public static class TankMates
    {
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
                    if (Relationships.Get(a.Species, b.Species) == Relationship.Predator) return true;
                    if (crowded && b.Stage == LifeStage.Adult && Relationships.AreCompetitive(a.Species, b.Species)) return true;
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
                bool fits = target.BodyLength <= hunter.BodyLength * rules.preyMaxSizeRatio;
                if (fits && GameRandom.Chance(rules.eatChance))
                {
                    Fx.Text(hunter.transform.position, "CHOMP!", UiTheme.Coral, 20f);
                    target.Eaten(hunter);
                    eaten++;
                }
                else if (GameRandom.Chance(rules.biteChance))
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
                if (nippers.Count == 0 || !GameRandom.Chance(rules.nipChance)) continue;
                var nipper = nippers[GameRandom.Range(0, nippers.Count)];
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
        float nextLunge;

        void Awake()
        {
            tank = GetComponent<Tank>();
            nextLunge = Time.time + GameRandom.Range(2f, 6f);
        }

        void Update()
        {
            if (tank == null || Time.time < nextLunge) return;
            nextLunge = Time.time + GameRandom.Range(3f, 8f);

            var pairs = new List<Fish>();
            bool crowded = tank.Residents.Count > tank.Capacity * Balance.Data.tankMates.crowdedAbove;
            foreach (var hunter in tank.Residents)
            {
                if (hunter == null || hunter.IsDead || hunter.Stage != LifeStage.Adult) continue;
                foreach (var prey in tank.Residents)
                    if (prey != hunter && !prey.IsDead && (Relationships.Get(hunter.Species, prey.Species) == Relationship.Predator
                        || (crowded && prey.Stage == LifeStage.Adult && Relationships.AreCompetitive(hunter.Species, prey.Species))))
                    {
                        pairs.Add(hunter);
                        pairs.Add(prey);
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
