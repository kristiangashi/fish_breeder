using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;

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
}
