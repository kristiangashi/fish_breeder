using System.Collections.Generic;
using FishBreeder.Breeding;
using FishBreeder.Core;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Shop
{
    /// <summary>
    /// Everything that happens between one shop phase and the next. Runs once when the shop opens
    /// and once when it closes; nothing else moves time on.
    /// Order: eggs hatch, water changes (waste, algae, pH, pests, temperature), fish age and get
    /// hungrier, illness progresses, spreads or strikes, pairs may breed, then each fish in poor
    /// health rolls for death.
    /// </summary>
    public static class TimeStep
    {
        public static string Run(IReadOnlyList<StoreTank> tanks)
        {
            var balance = Balance.Data.timeStep;
            int hatched = EggClutch.HatchAll();

            var fishes = new List<Fish>();
            foreach (var storeTank in tanks)
            {
                storeTank.Tank.OnTimeStep();
                fishes.AddRange(storeTank.Tank.Residents);
            }

            foreach (var fish in fishes) fish.OnTimeStep();

            int fellIll = 0;
            var newlyInfected = new List<Fish>();
            foreach (var fish in fishes)
                if (fish.Health.OnTimeStep(newlyInfected)) fellIll++;
            fellIll += newlyInfected.Count;

            int born = 0;
            foreach (var storeTank in tanks)
            {
                float chance = storeTank.Role == TankRole.Breeder ? balance.pregnancyChanceBreederTank : balance.pregnancyChanceOtherTank;
                born += BreedingSystem.ChancePregnancies(storeTank.Tank, chance);
            }

            int died = 0;
            foreach (var fish in fishes)
            {
                if (fish.IsDead) continue;
                var report = fish.Health.Report();
                if (report.total >= balance.deathHealthThreshold) continue;
                float danger = (balance.deathHealthThreshold - report.total) / Mathf.Max(0.01f, balance.deathHealthThreshold);
                if (!GameRandom.Chance(balance.deathChanceAtZeroHealth * Mathf.Pow(danger, 1.5f))) continue;
                fish.Die(report.WorstCause(fish.Health.Disease));
                died++;
            }

            var parts = new List<string>();
            if (hatched > 0) parts.Add($"{hatched} eggs hatched");
            if (born > 0) parts.Add($"{born} new fry or eggs");
            if (fellIll > 0) parts.Add($"{fellIll} fish fell ill");
            if (died > 0) parts.Add($"{died} died");
            string summary = parts.Count > 0 ? string.Join(", ", parts) + "." : "A quiet time in the tanks.";
            GameLog.Add(summary);
            return summary;
        }
    }
}
