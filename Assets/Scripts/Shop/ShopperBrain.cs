using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Shop
{
    /// <summary>One shopper's tastes, rolled when they walk in.</summary>
    public class ShopperProfile
    {
        /// <summary>Pays up to a fish's true value times this.</summary>
        public float priceTolerance;
        public string favoriteSpecies;

        public static ShopperProfile Random()
        {
            var shop = Balance.Data.shop;
            var all = Genetics.Species.All;
            return new ShopperProfile
            {
                priceTolerance = GameRandom.Range(shop.minPriceTolerance, shop.maxPriceTolerance) * Mathf.Lerp(0.9f, 1.1f, Economy.Reputation),
                favoriteSpecies = all.Count > 0 ? all[GameRandom.Range(0, all.Count)].id : null,
            };
        }
    }

    public struct Reaction
    {
        public Mood mood;
        /// <summary>The fish they decided to buy, or null.</summary>
        public Fish purchase;
    }

    /// <summary>
    /// What a shopper thinks of a tank. Shared by walking customers and the dev "skip day"
    /// simulation, so both follow the same rules.
    /// Dead fish make them sad, bad water grosses them out, overpriced fish make them mad,
    /// and a nice tank (or a fish they want to buy) makes them happy.
    /// </summary>
    public static class ShopperBrain
    {
        public static Reaction Evaluate(ShopperProfile shopper, StoreTank storeTank)
        {
            var shop = Balance.Data.shop;
            var tank = storeTank.Tank;
            var reaction = new Reaction();

            // Dead fish put some shoppers off, more of them the more there are; the rest carry on.
            if (tank.Corpses.Count > 0 && GameRandom.Chance(Mathf.Min(shop.maxSadChance, shop.sadChancePerDeadFish * tank.Corpses.Count)))
            {
                reaction.mood = Mood.Sad;
                return reaction;
            }

            float averageHealth = AverageHealth(tank);
            bool gross = tank.WaterHealth < shop.grossWaterQuality || tank.Algae > shop.grossAlgae || (tank.Residents.Count > 0 && averageHealth < 0.45f);
            if (gross && GameRandom.Chance(0.85f))
            {
                reaction.mood = Mood.Grossed;
                return reaction;
            }

            if (storeTank.Role == TankRole.ForSale)
            {
                var candidates = new List<Fish>();
                // Fry are too young to sell: only juveniles and adults go home with customers.
                foreach (var fish in tank.Residents)
                    if (!fish.IsDead && !fish.Reserved && fish.Price > 0 && fish.Stage != LifeStage.Fry) candidates.Add(fish);
                if (candidates.Count == 0)
                {
                    if (tank.Residents.Count > 0 && GameRandom.Chance(0.3f)) reaction.mood = Mood.Happy;
                    return reaction;
                }

                var pick = candidates[GameRandom.Range(0, candidates.Count)];
                foreach (var fish in candidates)
                    if (fish.Species.id == shopper.favoriteSpecies && GameRandom.Chance(0.6f)) pick = fish;

                float willingToPay = pick.TrueValueExact * shopper.priceTolerance;
                if (pick.Price > willingToPay)
                {
                    if (GameRandom.Chance(0.8f)) reaction.mood = Mood.Mad;
                    return reaction;
                }

                float bargain = Mathf.Clamp(willingToPay / pick.Price, 1f, 1.5f);
                float chance = shop.purchaseChance * Mathf.Lerp(0.5f, 1f, averageHealth) * bargain
                               * (pick.Species.id == shopper.favoriteSpecies ? 1.5f : 1f)
                               * (tank.Corpses.Count > 0 ? shop.deadFishPurchaseMultiplier : 1f);
                if (GameRandom.Chance(chance))
                {
                    reaction.mood = Mood.Happy;
                    reaction.purchase = pick;
                    pick.Reserved = true;
                }
                else if (GameRandom.Chance(0.4f)) reaction.mood = Mood.Happy;
                return reaction;
            }

            bool lovely = tank.Residents.Count >= 3 && tank.WaterHealth > 0.7f && averageHealth > 0.7f;
            float happyChance = storeTank.Role == TankRole.Display ? 0.7f : 0.45f;
            if (lovely && GameRandom.Chance(happyChance)) reaction.mood = Mood.Happy;
            return reaction;
        }

        public static float AverageHealth(Tank tank)
        {
            if (tank.Residents.Count == 0) return 1f;
            float sum = 0f;
            foreach (var fish in tank.Residents) sum += fish.Health.Current;
            return sum / tank.Residents.Count;
        }
    }
}
