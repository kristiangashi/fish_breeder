using System.Collections.Generic;
using FishBreeder.Feeding;

namespace FishBreeder.Core
{
    /// <summary>
    /// The shop's money, food stock and in-game day. Money is whole dollars.
    /// The day only moves on when the shop opens or closes (or the dev skip button is used).
    /// </summary>
    public static class Economy
    {
        static readonly Dictionary<FoodType, int> food = new Dictionary<FoodType, int>();
        static readonly List<string> ledger = new List<string>();

        public static int Money { get; private set; }
        public static int Day { get; private set; } = 1;
        public static IReadOnlyList<string> Ledger => ledger;

        public static void Reset(int money, int starterFlakes)
        {
            Money = money;
            Day = 1;
            food.Clear();
            ledger.Clear();
            if (starterFlakes > 0) AddFood(FoodType.Flakes, starterFlakes);
        }

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

        /// <summary>One time step: everything that happens between days. Shop open/close will call this.</summary>
        public static void AdvanceDay()
        {
            Day++;
            int hatched = EggClutch.HatchAll();
            GameLog.Add(hatched > 0 ? $"Day {Day}. {hatched} eggs hatched overnight!" : $"Day {Day}.");
        }
    }
}
