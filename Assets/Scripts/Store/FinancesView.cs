using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishBreeder.Store
{
    /// <summary>
    /// The finances window (F, or click your money). The money in and out today and yesterday is
    /// always there; the rest opens up as you go: spending by category (Shopkeeping level), income
    /// by category (after your first sale), a day-by-day history (a higher Shopkeeping level),
    /// earnings per tank (after earning enough from sales) and the best-selling fish (the
    /// Bookkeeper perk). Locked parts say what opens them. Drawn in the #finances-window
    /// (Window.uxml, styled in FishBreeder.uss).
    /// </summary>
    public class FinancesView : MonoBehaviour
    {
        enum Period { Today, Week, AllTime }

        static bool open;
        static readonly string[] PeriodNames = { "Today", "Last 7 days", "All time" };

        Period period = Period.Week;
        UiWindow window;
        UiFrame ui;

        public static bool IsOpen => open;
        public static void Open() => open = true;
        public static void Close() => open = false;

        public static void Toggle()
        {
            if (open) Close();
            else Open();
        }

        void Awake()
        {
            open = false;
            window = new UiWindow(transform, "finances-window", "Finances", 10, true, Close);
            window.CloseButton.text = "Close (F / Esc)";
            ui = window.Frame;
        }

        void OnDestroy() => window?.Destroy();

        void OnDisable() => window?.Show(false);

        void Update()
        {
            if (!ViewMode.Watching && !UiPanels.Typing && Input.GetKeyDown(KeyCode.F)) Toggle();
            bool show = open && !ViewMode.Watching;
            window.Show(show);
            if (show) window.Fill(DrawHeader, DrawBody);
        }

        void DrawHeader()
        {
            ui.Label($"<size=18><b>{Economy.Format(Economy.Money)}</b></size>  in the till", "fb-text fb-header-text");
        }

        void DrawBody()
        {
            ui.Label("Where your money comes from and where it goes. More of the books open up as the shop grows.", "fb-text fb-text--small");
            ui.BeginRow("fb-halves");
            ui.BeginScroll("fb-scroll fb-half--left");
            DrawSummary();
            DrawPeriodTabs();
            DrawCategories(true);
            DrawCategories(false);
            ui.Pop();
            ui.BeginScroll();
            DrawHistory();
            DrawPerTank();
            DrawBestsellers();
            ui.Pop();
            ui.Pop();
        }

        void DrawSummary()
        {
            ui.Section("TODAY AND YESTERDAY");
            DayLine($"Today (day {Economy.Day})", Finance.Book(Economy.Day));
            if (Economy.Day > 1) DayLine($"Yesterday (day {Economy.Day - 1})", Finance.Book(Economy.Day - 1));
        }

        void DayLine(string label, DayBook book)
        {
            ui.BeginCard();
            ui.Label($"<b>{label}</b>");
            ui.BeginRow("fb-row fb-spread");
            ui.Label($"In <color={UiTheme.LeafHex}><b>{Economy.Format(book.Income)}</b></color>");
            ui.Label($"Out <color={UiTheme.CoralHex}><b>{Economy.Format(book.Spending)}</b></color>");
            ui.Label($"Profit {Signed(book.Profit)}");
            ui.Pop();
            ui.Pop();
        }

        void DrawPeriodTabs()
        {
            ui.Space(6f);
            period = (Period)ui.Tabs((int)period, PeriodNames);
        }

        List<DayBook> Books()
        {
            switch (period)
            {
                case Period.Today: return new List<DayBook> { Finance.Today };
                case Period.Week: return Finance.Recent(7);
                default: return new List<DayBook>(Finance.All);
            }
        }

        /// <summary>Money in (or out) by category over the chosen period, biggest first, with bars.</summary>
        void DrawCategories(bool income)
        {
            ui.Section(income ? "MONEY IN" : "MONEY OUT");
            bool shown = income ? Finance.ShowsIncome : Finance.ShowsSpending;
            if (!shown)
            {
                var settings = Finance.Settings;
                Locked(income ? $"Opens after your first {(settings.incomeAfterSales == 1 ? "sale" : $"{settings.incomeAfterSales} sales")}."
                    : $"Opens at {Progression.TrackName(XpTrack.Shopkeeping)} level {settings.spendingLevel}.");
                return;
            }
            var totals = new int[Finance.KindCount];
            int sum = 0;
            foreach (var book in Books())
                for (int k = 0; k < totals.Length; k++)
                {
                    int v = income ? book.income[k] : book.spending[k];
                    totals[k] += v;
                    sum += v;
                }
            var order = new List<int>();
            for (int k = 0; k < totals.Length; k++) if (totals[k] > 0) order.Add(k);
            order.Sort((a, b) => totals[b].CompareTo(totals[a]));
            if (order.Count == 0)
            {
                ui.Label("<i>Nothing yet.</i>");
                return;
            }
            var color = income ? UiTheme.Leaf : UiTheme.Coral;
            foreach (var k in order)
            {
                ui.BeginRow("fb-row fb-category");
                ui.Label(Finance.Name((MoneyKind)k), "fb-text fb-category__name");
                ui.Bar(totals[k] / (float)sum, color, "fb-bar fb-grow");
                ui.Label($"<b>{Economy.Format(totals[k])}</b>", "fb-text fb-value");
                ui.Pop();
            }
            ui.Label($"Total: <b>{Economy.Format(sum)}</b>", "fb-small");
        }

        /// <summary>Profit day by day, as bars up (profit) or down (loss) from a middle line.</summary>
        void DrawHistory()
        {
            ui.Section("DAY BY DAY");
            if (!Finance.ShowsHistory)
            {
                Locked($"Opens at {Progression.TrackName(XpTrack.Shopkeeping)} level {Finance.Settings.historyLevel}.");
                return;
            }
            var days = Finance.Recent(Finance.HistoryDays);
            int most = 1;
            foreach (var d in days) most = Mathf.Max(most, Mathf.Abs(d.Profit));
            ui.Begin("chart", "fb-chart");
            foreach (var day in days)
            {
                int profit = day.Profit;
                var slot = ui.Begin("slot", "fb-chart__slot");
                slot.tooltip = $"Day {day.day}: in {Economy.Format(day.Income)}, out {Economy.Format(day.Spending)}";
                float share = Mathf.Abs(profit) / (float)most * 100f;
                ui.Begin("up", "fb-chart__half fb-chart__up");
                if (profit > 0) ChartBar(share, UiTheme.Leaf);
                ui.Pop();
                ui.Begin("down", "fb-chart__half");
                if (profit < 0) ChartBar(share, UiTheme.Coral);
                ui.Pop();
                ui.Label(day.day.ToString(), "fb-chart__day");
                ui.Pop();
            }
            ui.Pop();
            int week = 0;
            foreach (var d in days) week += d.Profit;
            string tip = ui.Tooltip.StartsWith("Day ") ? $"   {ui.Tooltip}" : "";
            ui.Label($"Profit over these {days.Count} days: {Signed(week)}.{tip}", "fb-small");
        }

        void ChartBar(float percent, Color color)
        {
            var bar = ui.Begin("bar", "fb-chart__bar");
            bar.style.height = Length.Percent(percent);
            bar.style.backgroundColor = color;
            bar.pickingMode = PickingMode.Ignore;
            ui.Pop();
        }

        /// <summary>What each tank's fish have sold for, all time.</summary>
        void DrawPerTank()
        {
            ui.Section("EARNINGS PER TANK");
            if (!Finance.ShowsPerTank)
            {
                Locked($"Opens once you've earned {Economy.Format(Finance.Settings.perTankAfterEarning)} from sales ({Economy.Format(Finance.SalesTotal)} so far).");
                return;
            }
            Ranked(Merge(b => b.salesByTank), null);
        }

        /// <summary>Which kinds of fish sell best, all time: money and how many.</summary>
        void DrawBestsellers()
        {
            ui.Section("BEST-SELLING FISH");
            if (!Finance.ShowsBestsellers)
            {
                Locked("Opens with the Bookkeeper perk (Perks, Finances tree).");
                return;
            }
            Ranked(Merge(b => b.salesBySpecies), Merge(b => b.soldBySpecies));
        }

        delegate Dictionary<string, int> Pick(DayBook book);

        static Dictionary<string, int> Merge(Pick pick)
        {
            var total = new Dictionary<string, int>();
            foreach (var book in Finance.All)
                foreach (var pair in pick(book)) total[pair.Key] = (total.TryGetValue(pair.Key, out var n) ? n : 0) + pair.Value;
            return total;
        }

        void Ranked(Dictionary<string, int> money, Dictionary<string, int> counts)
        {
            if (money.Count == 0)
            {
                ui.Label("<i>No sales yet.</i>");
                return;
            }
            var keys = new List<string>(money.Keys);
            keys.Sort((a, b) => money[b].CompareTo(money[a]));
            int rank = 0;
            foreach (var key in keys)
            {
                if (++rank > 10) break;
                string count = counts != null && counts.TryGetValue(key, out var n) ? $"  <size=11>({n} sold)</size>" : "";
                ui.Line($"{rank}. {key}{count}", $"<b>{Economy.Format(money[key])}</b>");
            }
        }

        void Locked(string why) => ui.Label($"<color=#9fb4bc><b>Locked.</b> <i>{why}</i></color>");

        static string Signed(int amount) => amount >= 0 ? $"<color={UiTheme.LeafHex}><b>+{Economy.Format(amount)}</b></color>" : $"<color={UiTheme.CoralHex}><b>-{Economy.Format(-amount)}</b></color>";
    }
}
