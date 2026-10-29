using System;
using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// A filter, heater or bubbler installed in a tank. It wears with every time step it runs and
    /// may break down; a broken one does nothing until it's repaired. Each breakdown is a fault
    /// ("The circuit got wet") with its own price: the model's repair cost times the fault's, give
    /// or take a little. Better models break less often but cost more to fix. Repairs take off
    /// only part of the wear, so old equipment keeps breaking more often until it's replaced.
    /// </summary>
    [Serializable]
    public class InstalledEquipment
    {
        public EquipmentKind kind;
        public int tier;
        /// <summary>Time steps it has been running since it was installed.</summary>
        public int ageSteps;
        /// <summary>Time steps of wear, which drives the breakdown chance. Repairs take some off.</summary>
        public float wear;
        public bool broken;
        /// <summary>What went wrong, while it's broken.</summary>
        public string fault;
        /// <summary>What fixing it costs, set when it breaks.</summary>
        public int repairCost;

        /// <summary>The repair price after perks.</summary>
        public int RepairPrice => Mathf.Max(1, Mathf.RoundToInt(repairCost * Perks.Less(PerkEffect.BetterRepairs)));

        /// <summary>Breaks down: picks what went wrong and what it'll cost to fix.</summary>
        public void Break()
        {
            broken = true;
            var info = Info;
            var faultInfo = Equipment.PickFault(kind);
            fault = faultInfo != null ? faultInfo.text : "It stopped working";
            float spread = Mathf.Clamp01(Balance.Data.supplier.repairPriceSpread);
            float baseCost = info != null ? info.repairCost : 15f;
            repairCost = Mathf.Max(1, Mathf.RoundToInt(baseCost * (faultInfo != null ? faultInfo.cost : 1f) * GameRandom.Range(1f - spread, 1f + spread)));
        }

        public EquipmentTierBalance Info => Equipment.Tier(kind, tier);
        public string Name => Info != null ? Info.name : Equipment.KindName(kind);

        /// <summary>Chance of breaking down at the next time step.</summary>
        public float BreakChance
        {
            get
            {
                var info = Info;
                if (info == null) return 0f;
                return Mathf.Clamp01(Mathf.Min(info.maxBreakChance, info.breakChance + info.breakChancePerStep * wear) * Perks.Less(PerkEffect.SturdierEquipment));
            }
        }
    }

    public static class Equipment
    {
        public static readonly EquipmentKind[] Kinds = { EquipmentKind.Filter, EquipmentKind.Heater, EquipmentKind.Bubbler };

        public static List<EquipmentTierBalance> Tiers(EquipmentKind kind)
        {
            var supplier = Balance.Data.supplier;
            switch (kind)
            {
                case EquipmentKind.Filter: return supplier.filters;
                case EquipmentKind.Heater: return supplier.heaters;
                default: return supplier.bubblers;
            }
        }

        public static EquipmentTierBalance Tier(EquipmentKind kind, int tier)
        {
            var tiers = Tiers(kind);
            if (tiers == null || tiers.Count == 0) return null;
            return tiers[Mathf.Clamp(tier, 0, tiers.Count - 1)];
        }

        /// <summary>Whether this model can go in this tank: the wall aquarium takes only the models made for it.</summary>
        public static bool Fits(EquipmentTierBalance info, Tank tank) => info != null && tank != null && info.wallAquariumOnly == tank.IsWallAquarium;

        /// <summary>The models of this kind that fit this tank, as indexes into <see cref="Tiers"/>, cheapest first.</summary>
        public static List<int> TiersFor(EquipmentKind kind, Tank tank)
        {
            var result = new List<int>();
            var tiers = Tiers(kind);
            if (tiers == null) return result;
            for (int i = 0; i < tiers.Count; i++)
                if (Fits(tiers[i], tank)) result.Add(i);
            return result;
        }

        /// <summary>Fishkeeping level of the first model of this kind.</summary>
        public static int FirstLevel(EquipmentKind kind)
        {
            var tiers = Tiers(kind);
            return tiers != null && tiers.Count > 0 ? tiers[0].unlockLevel : 1;
        }

        public static string KindName(EquipmentKind kind) => kind.ToString().ToLower();

        /// <summary>A fault for this kind of equipment, picked by weight (null if none are set up).</summary>
        public static EquipmentFaultBalance PickFault(EquipmentKind kind)
        {
            var faults = Balance.Data.supplier.faults;
            if (faults == null) return null;
            float total = 0f;
            foreach (var f in faults) if (f != null && f.equipment == kind) total += Mathf.Max(0f, f.weight);
            if (total <= 0f) return null;
            float roll = GameRandom.Range(0f, total);
            EquipmentFaultBalance last = null;
            foreach (var f in faults)
            {
                if (f == null || f.equipment != kind) continue;
                last = f;
                roll -= Mathf.Max(0f, f.weight);
                if (roll <= 0f) return f;
            }
            return last;
        }
    }
}
