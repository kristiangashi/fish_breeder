using System;
using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// A filter, heater or bubbler installed in a tank. It wears with every time step it runs and
    /// may break down; a broken one does nothing until it's repaired with a repair kit. Repairs
    /// take off only part of the wear, so old equipment keeps breaking more often until it's
    /// replaced with a new (or better) model.
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

        public EquipmentTierBalance Info => Equipment.Tier(kind, tier);
        public string Name => Info != null ? Info.name : Equipment.KindName(kind);

        /// <summary>Chance of breaking down at the next time step.</summary>
        public float BreakChance
        {
            get
            {
                var info = Info;
                if (info == null) return 0f;
                return Mathf.Clamp01(Mathf.Min(info.maxBreakChance, info.breakChance + info.breakChancePerStep * wear));
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

        /// <summary>Fishkeeping level of the first model of this kind.</summary>
        public static int FirstLevel(EquipmentKind kind)
        {
            var tiers = Tiers(kind);
            return tiers != null && tiers.Count > 0 ? tiers[0].unlockLevel : 1;
        }

        public static string KindName(EquipmentKind kind) => kind.ToString().ToLower();
    }
}
