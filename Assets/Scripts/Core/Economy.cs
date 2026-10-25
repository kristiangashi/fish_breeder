using System.Collections.Generic;
using FishBreeder.Feeding;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The shop's money, stock, reputation and in-game day. Money is whole dollars.
    /// The day only moves on when the shop closes; nothing changes while it stays closed.
    /// </summary>
    public static class Economy
    {
        static readonly Dictionary<FoodType, int> food = new Dictionary<FoodType, int>();
        static readonly List<string> ledger = new List<string>();

        public static int Money { get; private set; }
        public static int Day { get; private set; } = 1;
        /// <summary>0..1, from customer reviews. Brings in more (or fewer) customers.</summary>
        public static float Reputation { get; set; } = 0.5f;
        public static int WaterConditionerDoses { get; set; }
        /// <summary>Each fixes one broken filter, heater or bubbler.</summary>
        public static int RepairKits { get; set; }
        public static IReadOnlyList<string> Ledger => ledger;

        public static void Reset()
        {
            var store = Balance.Data.store;
            Money = store.startingMoney;
            Day = 1;
            Reputation = Balance.Data.shop.startingReputation;
            WaterConditionerDoses = 0;
            RepairKits = 0;
            food.Clear();
            ledger.Clear();
            if (store.starterFlakeFeedings > 0) AddFood(FoodType.Flakes, store.starterFlakeFeedings);
        }

        /// <summary>Days still to go before the shop's grand opening (0 once it can open).</summary>
        public static int DaysUntilOpening => Mathf.Max(0, Balance.Data.shop.setupDays + 1 - Day);

        public static bool CanOpenShop => DaysUntilOpening == 0;

#if UNITY_EDITOR
        /// <summary>Developer shortcut: jumps the calendar to the grand opening.</summary>
        public static void DevSkipSetup() => Day = Mathf.Max(Day, Balance.Data.shop.setupDays + 1);
#endif

        public static string Format(int dollars) => $"${dollars:N0}";

        public static bool CanAfford(int amount) => Money >= amount;

        public static bool TrySpend(int amount, string what)
        {
            if (!CanAfford(amount))
            {
                GameLog.Add($"Not enough money for {what} ({Format(amount)}).");
                return false;
            }
            Money -= amount;
            ledger.Add($"Day {Day}: -{Format(amount)} {what}");
            return true;
        }

        public static void Earn(int amount, string what)
        {
            Money += amount;
            ledger.Add($"Day {Day}: +{Format(amount)} {what}");
        }

        public static int FoodPortions(FoodType type) => food.TryGetValue(type, out var count) ? count : 0;

        public static void AddFood(FoodType type, int portions) => food[type] = FoodPortions(type) + portions;

        public static bool TryUseFood(FoodType type)
        {
            int count = FoodPortions(type);
            if (count <= 0)
            {
                GameLog.Add($"You're out of {type.ToString().ToLower()}. Order more at the supplier computer.");
                return false;
            }
            food[type] = count - 1;
            return true;
        }

        /// <summary>Called when the shop closes: the next day starts, and a broke player gets bailed out.</summary>
        public static void NextDay()
        {
            Day++;
            int interest = Mathf.Min(Perks.MaxInterestPerNight, Mathf.FloorToInt(Mathf.Max(0, Money) * Perks.Value(PerkEffect.Interest)));
            if (interest > 0)
            {
                Earn(interest, "interest on savings");
                GameLog.Add($"Your savings earned {Format(interest)} interest overnight.");
            }
            float buzz = Perks.Value(PerkEffect.DailyReputation);
            if (buzz > 0f) ChangeReputation(buzz);
            int allowance = Balance.Data.store.dailyAllowance + Perks.Count(PerkEffect.BiggerAllowance);
            if (Money < allowance)
            {
                Earn(allowance - Money, "daily allowance");
                GameLog.Add($"Money was tight, so you got the daily allowance: you now have {Format(Money)}.");
            }
        }

        public static void ChangeReputation(float delta) => Reputation = Mathf.Clamp01(Reputation + delta);
    }
}
