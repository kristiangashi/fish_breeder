using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Health;
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
            // Shoppers only ask for fish the player could get: not wild fish from a box that's still locked.
            var all = new List<Genetics.Species>();
            foreach (var species in Genetics.Species.All) if (EggBoxes.Obtainable(species)) all.Add(species);
            return new ShopperProfile
            {
                priceTolerance = GameRandom.Range(shop.minPriceTolerance, shop.maxPriceTolerance) * Mathf.Lerp(0.9f, 1.1f, Economy.Reputation) * Perks.More(PerkEffect.PriceTolerance),
                favoriteSpecies = all.Count > 0 ? all[GameRandom.Range(0, all.Count)].id : null,
            };
        }
    }

    public struct Reaction
    {
        public Mood mood;
        /// <summary>The fish they decided to buy, or null.</summary>
        public Fish purchase;
        /// <summary>What they'll tell their friends about, if anything (see <see cref="Reputation"/>).</summary>
        public bool complained;
        public Complaint complaint;

        public static Reaction Complain(Mood mood, Complaint complaint) => new Reaction { mood = mood, complained = true, complaint = complaint };
    }

    /// <summary>
    /// What a shopper thinks of a tank. Shared by walking customers and the dev "skip day"
    /// simulation, so both follow the same rules.
    /// Dead fish make them sad, bad water and sick fish gross them out (each sick fish they can see
    /// makes it likelier, and sick fish never sell), fish fighting upsets them,
    /// overpriced fish make them mad, an almost empty sale tank leaves them wanting, attractive
    /// fish catch their eye (and sell more easily), and a nice
    /// tank (or a fish they want to buy) makes them happy. Each complaint is remembered by
    /// <see cref="Reputation"/>, which turns it into fewer visitors or fewer sales.
    /// </summary>
    public static class ShopperBrain
    {
        public static Reaction Evaluate(ShopperProfile shopper, StoreTank storeTank)
        {
            var shop = Balance.Data.shop;
            var tank = storeTank.Tank;
            var reaction = new Reaction();

            // Dead fish put some shoppers off, more of them the more there are; the rest carry on.
            if (tank.Corpses.Count > 0 && GameRandom.Chance(Mathf.Min(shop.maxSadChance, shop.sadChancePerDeadFish * tank.Corpses.Count) * Perks.Less(PerkEffect.DeadFishTolerance)))
                return Reaction.Complain(Mood.Sad, Complaint.DeadFish);

            float averageHealth = AverageHealth(tank);
            float tolerance = Perks.Value(PerkEffect.GrossTolerance);
            bool dirty = tank.WaterHealth < shop.grossWaterQuality * (1f - tolerance) || tank.Algae > shop.grossAlgae * (1f + tolerance);
            if (dirty && GameRandom.Chance(0.85f)) return Reaction.Complain(Mood.Grossed, Complaint.DirtyWater);

            // Sick fish put people off too, more of them the more sick fish they can see.
            int sickCount = SickCount(tank);
            bool poorly = tank.Residents.Count > 0 && averageHealth < 0.45f;
            float sickChance = Mathf.Min(shop.maxSickChance, shop.sickChancePerFish * sickCount);
            if (poorly) sickChance = Mathf.Max(sickChance, 0.85f);
            if (sickChance > 0f && GameRandom.Chance(sickChance * Perks.Less(PerkEffect.GrossTolerance)))
                return Reaction.Complain(Mood.Grossed, Complaint.SickFish);

            // Fish chasing and biting each other in a public tank upset people.
            bool fighting = TankMates.IsFighting(tank);
            if (fighting && GameRandom.Chance(shop.fightingUpsetChance))
                return Reaction.Complain(Mood.Sad, Complaint.Fighting);

            if (storeTank.Role == TankRole.ForSale)
            {
                var candidates = new List<Fish>();
                // Fry are too young to sell: only juveniles and adults go home with customers.
                // Nobody buys a fish that looks ill.
                foreach (var fish in tank.Residents)
                    if (!fish.IsDead && !fish.Reserved && fish.Price > 0 && fish.Stage != LifeStage.Fry && !fish.Health.IsSick) candidates.Add(fish);
                if (candidates.Count < shop.fewFishBelow && GameRandom.Chance(shop.fewFishComplaintChance))
                {
                    reaction.complained = true;
                    reaction.complaint = Complaint.FewFish;
                }
                if (candidates.Count == 0)
                {
                    if (tank.Residents.Count > 0 && GameRandom.Chance(0.3f)) reaction.mood = Mood.Happy;
                    return reaction;
                }

                // The eye goes to the most attractive fish first; a favourite kind can still win them over.
                var pick = PickByAttention(candidates, shop.attentionPower);
                foreach (var fish in candidates)
                    if (fish.Species.id == shopper.favoriteSpecies && GameRandom.Chance(0.6f)) pick = fish;

                float willingToPay = pick.TrueValueExact * shopper.priceTolerance;
                if (pick.Price > willingToPay)
                {
                    if (GameRandom.Chance(0.8f)) return Reaction.Complain(Mood.Mad, Complaint.HighPrices);
                    return reaction;
                }

                float bargain = Mathf.Clamp(willingToPay / pick.Price, 1f, 1.5f);
                float chance = shop.purchaseChance * Perks.More(PerkEffect.PurchaseChance) * Mathf.Lerp(0.5f, 1f, averageHealth) * bargain
                               * (pick.Species.id == shopper.favoriteSpecies ? 1.5f : 1f)
                               * (tank.Corpses.Count > 0 ? shop.deadFishPurchaseMultiplier : 1f)
                               * (fighting ? shop.fightingPurchaseMultiplier : 1f)
                               * (sickCount > 0 ? shop.sickFishPurchaseMultiplier : 1f)
                               * Mathf.Lerp(shop.plainFishPurchase, shop.stunningFishPurchase, pick.Attractiveness)
                               * Reputation.SalesMultiplier;
                if (GameRandom.Chance(chance))
                {
                    reaction.mood = Mood.Happy;
                    reaction.purchase = pick;
                    reaction.complained = false;
                    pick.Reserved = true;
                }
                else if (GameRandom.Chance(0.4f)) reaction.mood = Mood.Happy;
                return reaction;
            }

            bool lovely = tank.Residents.Count >= 3 && tank.WaterHealth > 0.7f && averageHealth > 0.7f && !fighting;
            float happyChance = storeTank.Role == TankRole.Display ? 0.7f : 0.45f;
            if (lovely && GameRandom.Chance(happyChance)) reaction.mood = Mood.Happy;
            return reaction;
        }

        /// <summary>Living fish in the tank with an illness.</summary>
        public static int SickCount(Tank tank)
        {
            int count = 0;
            foreach (var fish in tank.Residents)
                if (!fish.IsDead && fish.Health.IsSick) count++;
            return count;
        }

        /// <summary>A fish chosen with weight attractiveness^power: the eye-catching ones get looked at most.</summary>
        static Fish PickByAttention(List<Fish> candidates, float power)
        {
            float total = 0f;
            var weights = new float[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                weights[i] = Mathf.Pow(Mathf.Max(0.02f, candidates[i].Attractiveness), Mathf.Max(0f, power));
                total += weights[i];
            }
            float roll = GameRandom.Range(0f, total);
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0f) return candidates[i];
            }
            return candidates[candidates.Count - 1];
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
