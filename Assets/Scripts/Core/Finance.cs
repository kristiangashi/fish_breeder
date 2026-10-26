using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>What money came in for or went out on, for the finances screen.</summary>
    public enum MoneyKind
    {
        // Money in
        Sales, Tips, Grandma, Milestones, Interest, Refunds,
        // Money out
        FishAndEggs, Food, Care, Equipment, Decorations, Tanks, Rooms, Research,
        Other,
    }

    /// <summary>
    /// When each part of the finances screen opens up. The daily summary is always there; the rest
    /// comes with Shopkeeping levels, your first sales, money earned and the Bookkeeper perk.
    /// </summary>
    [Serializable]
    public class FinanceBalance
    {
        [Tooltip("Shopkeeping level that shows spending by category.")]
        public int spendingLevel = 2;
        [Tooltip("Fish sold (in total) before income by category shows.")]
        public int incomeAfterSales = 1;
        [Tooltip("Shopkeeping level that shows the day-by-day history.")]
        public int historyLevel = 3;
        [Tooltip("Days the history shows.")]
        public int historyDays = 7;
        [Tooltip("Money earned from sales (in total) before earnings per tank show.")]
        public int perTankAfterEarning = 1000;
    }

    /// <summary>One day's money in and out, by kind, and what sold.</summary>
    public class DayBook
    {
        public int day;
        public readonly int[] income = new int[Finance.KindCount];
        public readonly int[] spending = new int[Finance.KindCount];
        public readonly Dictionary<string, int> salesBySpecies = new Dictionary<string, int>();
        public readonly Dictionary<string, int> soldBySpecies = new Dictionary<string, int>();
        public readonly Dictionary<string, int> salesByTank = new Dictionary<string, int>();

        public int Income
        {
            get { int n = 0; foreach (var v in income) n += v; return n; }
        }

        public int Spending
        {
            get { int n = 0; foreach (var v in spending) n += v; return n; }
        }

        public int Profit => Income - Spending;
    }

    /// <summary>
    /// The shop's books: every dollar that comes in or goes out (through <see cref="Economy"/>)
    /// lands in the day's book by kind, and every sale by species and tank. The finances screen
    /// reads them.
    /// </summary>
    public static class Finance
    {
        public static readonly int KindCount = Enum.GetValues(typeof(MoneyKind)).Length;

        static readonly List<DayBook> days = new List<DayBook>();

        public static int FishSold { get; private set; }
        public static int SalesTotal { get; private set; }

        static FinanceBalance Data => Balance.Data.finances;

        public static void Reset()
        {
            days.Clear();
            FishSold = 0;
            SalesTotal = 0;
        }

        public static bool IsIncome(MoneyKind kind) => kind <= MoneyKind.Refunds;

        public static string Name(MoneyKind kind)
        {
            switch (kind)
            {
                case MoneyKind.Sales: return "Fish sales";
                case MoneyKind.Tips: return "Tips";
                case MoneyKind.Grandma: return "From Grandma";
                case MoneyKind.Milestones: return "Milestone rewards";
                case MoneyKind.Interest: return "Interest";
                case MoneyKind.Refunds: return "Refunds";
                case MoneyKind.FishAndEggs: return "Fish, eggs and fry";
                case MoneyKind.Food: return "Food";
                case MoneyKind.Care: return "Medicine, salt and repairs";
                case MoneyKind.Equipment: return "Equipment";
                case MoneyKind.Decorations: return "Decorations";
                case MoneyKind.Tanks: return "Tanks and upgrades";
                case MoneyKind.Rooms: return "New rooms";
                case MoneyKind.Research: return "Research budget";
                default: return "Other";
            }
        }

        /// <summary>The book for this day, started if needed.</summary>
        public static DayBook Book(int day)
        {
            for (int i = days.Count - 1; i >= 0; i--) if (days[i].day == day) return days[i];
            var book = new DayBook { day = day };
            days.Add(book);
            days.Sort((a, b) => a.day.CompareTo(b.day));
            return book;
        }

        public static DayBook Today => Book(Economy.Day);

        /// <summary>The last <paramref name="count"/> days' books, oldest first (days with nothing are filled in empty).</summary>
        public static List<DayBook> Recent(int count)
        {
            var list = new List<DayBook>();
            for (int day = Mathf.Max(1, Economy.Day - count + 1); day <= Economy.Day; day++) list.Add(Book(day));
            return list;
        }

        /// <summary>Every book so far.</summary>
        public static IReadOnlyList<DayBook> All => days;

        public static void Record(int amount, MoneyKind kind, bool income)
        {
            if (amount <= 0) return;
            var book = Today;
            if (income) book.income[(int)kind] += amount;
            else book.spending[(int)kind] += amount;
        }

        public static void RecordSale(string species, string tank, int amount)
        {
            var book = Today;
            Add(book.salesBySpecies, species, amount);
            Add(book.soldBySpecies, species, 1);
            Add(book.salesByTank, tank, amount);
            FishSold++;
            SalesTotal += amount;
        }

        static void Add(Dictionary<string, int> map, string key, int amount)
        {
            if (string.IsNullOrEmpty(key)) key = "?";
            map[key] = (map.TryGetValue(key, out var n) ? n : 0) + amount;
        }

        // ---------------------------------------------------------------- what the screen shows

        public static bool ShowsSpending => Progression.IsUnlocked(XpTrack.Shopkeeping, Data.spendingLevel);
        public static bool ShowsIncome => DevMode.On || FishSold >= Data.incomeAfterSales;
        public static bool ShowsHistory => Progression.IsUnlocked(XpTrack.Shopkeeping, Data.historyLevel);
        public static bool ShowsBestsellers => DevMode.On || Perks.Count(PerkEffect.Bookkeeping) > 0;
        public static bool ShowsPerTank => DevMode.On || SalesTotal >= Data.perTankAfterEarning;
        public static int HistoryDays => Mathf.Clamp(Data.historyDays, 2, 30);
        public static FinanceBalance Settings => Data;
    }
}
