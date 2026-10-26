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
        /// <summary>Each fixes one broken filter, heater or bubbler.</summary>
        public static IReadOnlyList<string> Ledger => ledger;
        /// <summary>What Grandma sent last night, for the morning banner (null if nothing).</summary>
        public static string GrandmaNote { get; private set; }

        public static void Reset()
        {
            var store = Balance.Data.store;
            Money = store.startingMoney;
            Day = 1;
            GrandmaNote = null;
            Reputation = Balance.Data.shop.startingReputation;
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

        /// <summary>Always true in <see cref="DevMode"/>.</summary>
        public static bool CanAfford(int amount) => DevMode.On || Money >= amount;

        /// <summary>Pays for something; in <see cref="DevMode"/> it's free. The kind sorts it on the finances screen.</summary>
        public static bool TrySpend(int amount, string what, MoneyKind kind = MoneyKind.Other)
        {
            if (DevMode.On)
            {
                ledger.Add($"Day {Day}: {what} (dev mode, free)");
                return true;
            }
            if (!CanAfford(amount))
            {
                GameLog.Add($"Not enough money for {what} ({Format(amount)}).");
                return false;
            }
            Money -= amount;
            ledger.Add($"Day {Day}: -{Format(amount)} {what}");
            Finance.Record(amount, kind, false);
            return true;
        }

        public static void Earn(int amount, string what, MoneyKind kind = MoneyKind.Other)
        {
            Money += amount;
            ledger.Add($"Day {Day}: +{Format(amount)} {what}");
            Finance.Record(amount, kind, true);
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

        /// <summary>
        /// Called when you go to sleep: the next day starts. Before the grand opening Grandma sends
        /// a gift every night; after it you have to make a profit, and she only steps in when you're broke.
        /// </summary>
        public static void NextDay()
        {
            bool settingUp = DaysUntilOpening > 0;
            Day++;
            GrandmaNote = null;
            int interest = Mathf.Min(Perks.MaxInterestPerNight, Mathf.FloorToInt(Mathf.Max(0, Money) * Perks.Value(PerkEffect.Interest)));
            if (interest > 0)
            {
                Earn(interest, "interest on savings", MoneyKind.Interest);
                GameLog.Add($"Your savings earned {Format(interest)} interest overnight.");
            }
            float buzz = Perks.Value(PerkEffect.DailyReputation);
            if (buzz > 0f) ChangeReputation(buzz);
            var store = Balance.Data.store;
            int gift = store.grandmaGift + Perks.Count(PerkEffect.BiggerAllowance);
            if (gift <= 0) return;
            if (settingUp)
            {
                Earn(gift, "gift from Grandma", MoneyKind.Grandma);
                GameLog.Add($"Grandma sent {Format(gift)} with a note: \"I believe in you!\" You now have {Format(Money)}.");
                GrandmaNote = $"Grandma sent {Format(gift)}";
            }
            else if (Money < store.brokeBelow)
            {
                Earn(gift, "Grandma's rescue", MoneyKind.Grandma);
                GameLog.Add($"You were broke, so Grandma saved the day with {Format(gift)} to get you back on your feet. You now have {Format(Money)}.");
                GrandmaNote = $"Grandma saved the day with {Format(gift)}";
            }
        }

        public static void ChangeReputation(float delta) => Reputation = Mathf.Clamp01(Reputation + delta);
    }
}
