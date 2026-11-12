using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.Health;
using FishBreeder.People;
using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>
    /// The closed-shop jobs a player does, as plain calls with the same costs, XP and checks as the
    /// buttons in the tank window and the supplier. Bots (the pacing simulator and the dev skip-ahead)
    /// use these so they play by the game's own rules. Each returns false (and changes nothing) when
    /// the player couldn't have done it: not unlocked, can't afford it, nothing to do.
    /// </summary>
    public static class PlayerActions
    {
        // ---------------------------------------------------------------- supplier

        /// <summary>Why an egg (or fry) box can't be ordered into this tank right now, or null if it can.</summary>
        public static string WhyCantOrder(EggTierBalance tier, StoreTank target, bool fry)
        {
            if (tier == null || target == null) return "no tank";
            if (target.Role == TankRole.Exhibit || target.Role == TankRole.Holding) return "exhibits and holding tanks don't take boxes";
            if (tier.saltwater != target.Tank.IsSaltwater) return "wrong water";
            if (FishSeller.Away) return "the seller is away";
            if (!Progression.IsUnlocked(XpTrack.Fishkeeping, tier.unlockLevel)) return "locked";
            if (!EggBoxes.GoalMet(tier)) return "goal not met";
            int level = Progression.UnlockLevel(XpTrack.Fishkeeping);
            var tank = target.Tank;
            if (!fry && !EggBoxes.EverHatches(tier, true)) return "no egg layers";
            if (EggBoxes.Pool(tier, false, level, !fry, tank.SizeIndex).Count + EggBoxes.Pool(tier, true, level, !fry, tank.SizeIndex).Count == 0) return "nothing for this tank";
            if (FreeSpace(target) < tier.eggs) return "not enough room";
            int price = Perks.Supplier(fry ? tier.fryPrice : tier.price);
            if (!Economy.CanAfford(price)) return "can't afford it";
            return null;
        }

        /// <summary>Free spaces a box can fill: the tank's limit less fish and eggs already in it.</summary>
        public static int FreeSpace(StoreTank tank) => Mathf.Max(0, tank.Tank.FreeSpaceBeforeLimit - EggClutch.EggsOf(tank.Tank, SizeGroup.Tiny));

        /// <summary>Orders a box of eggs (they hatch at the next time step) for a tank.</summary>
        public static bool OrderEggs(EggTierBalance tier, StoreTank target)
        {
            if (WhyCantOrder(tier, target, false) != null) return false;
            if (!Economy.TrySpend(Perks.Supplier(tier.price), tier.name.ToLower(), MoneyKind.FishAndEggs)) return false;
            var tank = target.Tank;
            var eggs = EggBoxes.Roll(tier, Progression.UnlockLevel(XpTrack.Fishkeeping), true, tank.SizeIndex, g => tank.GroupFreeSpace(g) - EggClutch.EggsOf(tank, g));
            EggClutch.Lay(tank, tier, eggs);
            if (FishSeller.Instance != null) FishSeller.Instance.Sold();
            else FishSeller.Away = true;
            GameLog.Add($"{tier.eggs} {tier.name.ToLower()} delivered to {target.Title}. They hatch at the next time step.");
            return true;
        }

        /// <summary>Orders a box of mixed fry, delivered straight into the tank.</summary>
        public static bool OrderFry(EggTierBalance tier, StoreTank target)
        {
            if (WhyCantOrder(tier, target, true) != null) return false;
            if (!Economy.TrySpend(Perks.Supplier(tier.fryPrice), EggBoxes.FryName(tier).ToLower(), MoneyKind.FishAndEggs)) return false;
            var tank = target.Tank;
            foreach (var genome in EggBoxes.Roll(tier, Progression.UnlockLevel(XpTrack.Fishkeeping), false, tank.SizeIndex, g => tank.GroupFreeSpace(g) - EggClutch.EggsOf(tank, g)))
                FishFactory.Spawn(genome, 1, 0.05f, tank, tank.RandomFreePoint(0.1f));
            if (FishSeller.Instance != null) FishSeller.Instance.Sold();
            else FishSeller.Away = true;
            GameLog.Add($"{tier.eggs} {EggBoxes.FryName(tier).ToLower()} delivered to {target.Title}. You'll see what they are when they grow.");
            return true;
        }

        public static bool BuyFood(FoodBalance offer)
        {
            if (offer == null || !Progression.IsUnlocked(XpTrack.Fishkeeping, offer.unlockLevel)) return false;
            if (!Economy.TrySpend(Perks.Supplier(offer.price), offer.name.ToLower(), MoneyKind.Food)) return false;
            Economy.AddFood(offer.type, offer.feedings);
            GameLog.Add($"Bought {offer.name.ToLower()}: {offer.feedings} more feedings.");
            return true;
        }

        // ---------------------------------------------------------------- tank care

        /// <summary>Feeds the tank one portion of this food (the Feed button).</summary>
        public static bool Feed(StoreTank tank, FoodType food)
        {
            if (tank == null || tank.Feeding == null || !Economy.TryUseFood(food)) return false;
            tank.Feeding.Feed(food, 8);
            return true;
        }

        public static bool Clean(StoreTank tank) => tank != null && !tank.Tank.InBloom && tank.Tank.CleansLeft > 0 && tank.Tank.Clean();

        public static bool DeepClean(StoreTank tank) => tank != null && tank.Tank.DeepCleansLeft > 0 && Economy.CanAfford(Balance.Data.algae.deepCleanPrice) && tank.Tank.DeepClean();

        public static int WaterTreatmentPrice => Perks.Supplier(Balance.Data.supplier.waterTreatmentPrice);

        public static bool TreatWater(StoreTank tank)
        {
            if (tank == null || !Progression.IsUnlocked(XpTrack.Fishkeeping, Balance.Data.supplier.waterConditionerLevel)) return false;
            if (!Economy.TrySpend(WaterTreatmentPrice, $"water conditioner for {tank.Title}", MoneyKind.Care)) return false;
            tank.Tank.TreatWater();
            return true;
        }

        /// <summary>Nets out the dead fish. Returns how many.</summary>
        public static int RemoveDead(StoreTank tank)
        {
            if (tank == null) return 0;
            int count = tank.Tank.Corpses.Count;
            if (count == 0) return 0;
            foreach (var corpse in new List<Fish>(tank.Tank.Corpses)) corpse.RemoveFromTank();
            Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpRemoveDeadFish * count);
            GameLog.Add("You netted out the dead fish.");
            return count;
        }

        /// <summary>Medicine for the whole tank. Returns how many fish it cured (-1 if it couldn't be given).</summary>
        public static int Dose(StoreTank tank, Medicine medicine)
        {
            var supplier = Balance.Data.supplier;
            if (tank == null || medicine == Medicine.None || !Progression.IsUnlocked(XpTrack.Fishkeeping, supplier.medicineLevel)) return -1;
            int price = Perks.Supplier(supplier.medicinePrice);
            if (!Economy.TrySpend(price, $"{medicine.ToString().ToLower()} for {tank.Title}", MoneyKind.Care)) return -1;
            int cured = 0, stressed = 0;
            foreach (var fish in new List<Fish>(tank.Tank.Residents))
                if (fish.Health.ReceiveMedicine(medicine) == FishHealth.MedicineResult.Cured) cured++;
                else stressed++;
            if (cured > 0) Milestones.Add(MilestoneKind.CureFish, cured);
            GameLog.Add($"{medicine} went into {tank.Title}: " + (cured > 0 ? $"{cured} cured" : "nobody cured") + (stressed > 0 ? $", {stressed} fish stressed by medicine they didn't need." : "."));
            return cured;
        }

        /// <summary>Buys and installs a piece of equipment (replacing what's there).</summary>
        public static bool BuyEquipment(StoreTank tank, EquipmentKind kind, int tier)
        {
            var info = Equipment.Tier(kind, tier);
            if (tank == null || info == null || !Equipment.Fits(info, tank.Tank) || !Progression.IsUnlocked(XpTrack.Fishkeeping, info.unlockLevel)) return false;
            if (!Economy.TrySpend(Perks.Supplier(info.price), $"a {info.name.ToLower()} for {tank.Title}", MoneyKind.Equipment)) return false;
            tank.Tank.Install(kind, tier);
            Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpBuyEquipment);
            Milestones.Add(MilestoneKind.BuyEquipment);
            GameLog.Add($"{info.name} installed in {tank.Title}.");
            return true;
        }

        public static bool Repair(StoreTank tank, EquipmentKind kind)
        {
            var item = tank != null ? tank.Tank.GetEquipment(kind) : null;
            return item != null && item.broken && Economy.CanAfford(item.RepairPrice) && tank.Tank.Repair(kind);
        }

        // ---------------------------------------------------------------- fish

        /// <summary>Moves a fish to another tank; in a For Sale tank it gets <paramref name="price"/> (or the appraised price when 0).</summary>
        public static bool Move(Fish fish, StoreTank destination, int price = 0)
        {
            if (fish == null || destination == null || !fish.MoveTo(destination.Tank)) return false;
            if (destination.Role == TankRole.ForSale) fish.Price = price > 0 ? price : fish.Price > 0 ? fish.Price : fish.AppraisedPrice;
            return true;
        }
    }
}
