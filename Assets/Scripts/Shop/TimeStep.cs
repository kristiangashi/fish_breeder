using System.Collections.Generic;
using FishBreeder.Breeding;
using FishBreeder.Core;
using FishBreeder.Health;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Shop
{
    /// <summary>
    /// Everything that happens while time passes. Runs twice a day: when the shop closes (or the day
    /// passes with it shut) and overnight when the player goes to sleep; nothing else moves time on.
    /// Order: eggs develop (and hatch when due), pregnant fish give birth when due, water changes
    /// (waste, algae, pH, pests, temperature), fish age and get hungrier, illness progresses,
    /// spreads or strikes, compatible pairs may breed, each fish in poor health rolls for death, and
    /// finally predators hunt and rivals in crowded tanks nip each other.
    /// </summary>
    public static class TimeStep
    {
        public static string Run(IReadOnlyList<StoreTank> tanks)
        {
            var balance = Balance.Data.timeStep;
            // Food still falling when time moves on gets eaten (or rots) in the time we don't see.
            Feeding.FoodPellet.SettleAll();
            int hatched = EggClutch.AdvanceAll();
            var mothers = new List<Fish>();
            foreach (var storeTank in tanks) mothers.AddRange(storeTank.Tank.Residents);
            int bornLive = BreedingSystem.AdvancePregnancies(mothers);

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

            int conceived = 0;
            foreach (var storeTank in tanks)
            {
                // The seller's finds sit alone in their holding tanks: nothing breeds there.
                if (storeTank.Role == TankRole.Holding) continue;
                float chance = (storeTank.Role == TankRole.Breeder ? balance.pregnancyChanceBreederTank : balance.pregnancyChanceOtherTank) * Perks.More(PerkEffect.BreedingChance);
                conceived += BreedingSystem.ChanceBreeding(storeTank.Tank, chance);
            }

            // Every fish's health is settled before anyone dies, so one death can't set off a chain of
            // "rot from dead tank mates" deaths within the same time step. Health only ever moves here.
            var reports = new List<HealthReport>();
            foreach (var fish in fishes)
            {
                if (!fish.IsDead) fish.Health.Settle();
                reports.Add(fish.IsDead ? default : fish.Health.Report());
            }

            int died = 0;
            for (int i = 0; i < fishes.Count; i++)
            {
                var fish = fishes[i];
                if (fish.IsDead) continue;
                var report = reports[i];
                float health = fish.Health.Current;
                if (health >= balance.deathHealthThreshold) continue;
                float danger = (balance.deathHealthThreshold - health) / Mathf.Max(0.01f, balance.deathHealthThreshold);
                if (!GameRandom.Chance(balance.deathChanceAtZeroHealth * Mathf.Pow(danger, 1.5f))) continue;
                fish.Die(report.WorstCause(fish.Health.Disease, fish.Health.InjuryCause));
                died++;
            }

            // Predators hunt and crowded rivals nip, after the death rolls so an eaten fish
            // isn't judged as well.
            int eaten = 0, wounded = 0;
            foreach (var storeTank in tanks) TankMates.Run(storeTank.Tank, ref eaten, ref wounded);

            var allTanks = new List<Tank>();
            foreach (var storeTank in tanks) allTanks.Add(storeTank.Tank);
            Research.OnTimeStep(allTanks);

            // Grown fish over the aquarium's limit for their kind go to the fish market.
            int marketed = PublicAquarium.TrimToLimits(tanks);
            // The fish seller comes back from a dive with a find for a holding tank.
            ScubaDives.OnTimeStep(tanks);

            var parts = new List<string>();
            if (hatched > 0) parts.Add($"{hatched} eggs hatched");
            if (bornLive > 0) parts.Add($"{bornLive} fry born");
            if (conceived > 0) parts.Add(conceived == 1 ? "a new pregnancy or clutch of eggs" : $"{conceived} new pregnancies or clutches of eggs");
            if (fellIll > 0) parts.Add($"{fellIll} fish fell ill");
            if (died > 0) parts.Add($"{died} died");
            if (eaten > 0) parts.Add(eaten == 1 ? "a fish was eaten by a tank mate" : $"{eaten} fish were eaten by tank mates");
            if (wounded > 0) parts.Add(wounded == 1 ? "a fish was bitten or nipped" : $"{wounded} fish were bitten or nipped");
            if (marketed > 0) parts.Add(marketed == 1 ? "a fish from the aquarium went to the fish market" : $"{marketed} fish from the aquarium went to the fish market");
            string summary = parts.Count > 0 ? string.Join(", ", parts) + "." : "A quiet time in the tanks.";
            GameLog.Add(summary);
            return summary;
        }
    }
}
