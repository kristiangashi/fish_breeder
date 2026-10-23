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
        public static void Run(Tank tank, ref int eaten, ref int wounded)
        {
            var rules = Balance.Data.tankMates;

            foreach (var hunter in new List<Fish>(tank.Residents))
            {
                if (hunter == null || hunter.IsDead || hunter.Stage == LifeStage.Fry) continue;
                var prey = new List<Fish>();
                foreach (var other in tank.Residents)
                    if (other != hunter && !other.IsDead && Relationships.Get(hunter.Species, other.Species) == Relationship.Predator) prey.Add(other);
                if (prey.Count == 0) continue;

                var target = prey[GameRandom.Range(0, prey.Count)];
                bool fits = target.BodyLength <= hunter.BodyLength * rules.preyMaxSizeRatio;
                if (fits && GameRandom.Chance(rules.eatChance))
                {
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
                if (victim == null || victim.IsDead) continue;
                var nippers = new List<Fish>();
                foreach (var other in tank.Residents)
                    if (other != victim && !other.IsDead && other.Stage != LifeStage.Fry && Relationships.AreCompetitive(victim.Species, other.Species)) nippers.Add(other);
                if (nippers.Count == 0 || !GameRandom.Chance(rules.nipChance)) continue;
                var nipper = nippers[GameRandom.Range(0, nippers.Count)];
                victim.Health.Injure(rules.nipInjury, $"nipped by {nipper.FishName} the {nipper.Species}");
                wounded++;
            }
        }
    }

    /// <summary>
    /// What you see of a predator in the tank: every few seconds it bolts a little way at one of its
    /// prey, which darts away. Only for show; the real hunting happens at time steps.
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
            foreach (var hunter in tank.Residents)
            {
                if (hunter == null || hunter.IsDead || hunter.Stage == LifeStage.Fry) continue;
                foreach (var prey in tank.Residents)
                    if (prey != hunter && !prey.IsDead && Relationships.Get(hunter.Species, prey.Species) == Relationship.Predator)
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
        }
    }
}
