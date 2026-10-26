using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>
    /// The finances window (F, or click your money). The money in and out today and yesterday is
    /// always there; the rest opens up as you go: spending by category (Shopkeeping level), income
    /// by category (after your first sale), a day-by-day history (a higher Shopkeeping level),
    /// earnings per tank (after earning enough from sales) and the best-selling fish (the
    /// Bookkeeper perk). Locked parts say what opens them.
    /// </summary>
    public class FinancesView : MonoBehaviour
    {
        enum Period { Today, Week, AllTime }

        static bool open;
        static readonly string[] PeriodNames = { "Today", "Last 7 days", "All time" };

        Period period = Period.Week;
        Vector2 leftScroll, rightScroll;
        Rect window;

        public static bool IsOpen => open;
        public static void Open() => open = true;
        public static void Close() => open = false;

        public static void Toggle()
        {
            if (open) Close();
            else Open();
        }

        void Awake() => open = false;

        void Update()
        {
            if (ViewMode.Watching || GUIUtility.keyboardControl != 0) return;
            if (Input.GetKeyDown(KeyCode.F)) Toggle();
        }

        void OnGUI()
        {
            if (!open || ViewMode.Watching) return;
            UiTheme.Apply();
            GUI.depth = -10;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            float w = Mathf.Min(980f, width - 40f);
            float h = Mathf.Min(700f, height - 50f);
            window = new Rect((width - w) * 0.5f, (height - h) * 0.5f, w, h);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.01f, 0.06f, 0.1f, 0.45f), 0f, 0f);

            UiCheck.Begin("Finances window", window, UiStyles.Window);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Finances", UiTheme.Title, GUILayout.ExpandWidth(false));
            GUILayout.Space(16f);
            GUILayout.Label($"<size=18><b>{Economy.Format(Economy.Money)}</b></size>  in the till", Rich(), GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close (F / Esc)", GUILayout.Width(120f))) Close();
            GUILayout.EndHorizontal();
            GUILayout.Label("<size=12>Where your money comes from and where it goes. More of the books open up as the shop grows.</size>", Rich());
            GUILayout.Space(4f);

            float column = (w - UiStyles.Window.padding.horizontal - 16f) * 0.5f;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(column));
            leftScroll = UiCheck.BeginScroll("Finances: summary and categories", leftScroll);
            DrawSummary();
            DrawPeriodTabs();
            DrawCategories(true);
            DrawCategories(false);
            UiCheck.EndScroll();
            GUILayout.EndVertical();
            GUILayout.Space(16f);
            GUILayout.BeginVertical(GUILayout.Width(column));
            rightScroll = UiCheck.BeginScroll("Finances: history and sales", rightScroll);
            DrawHistory();
            DrawPerTank();
            DrawBestsellers();
            UiCheck.EndScroll();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            UiCheck.End();

            var e = Event.current;
            if (e.type == EventType.MouseDown)
            {
                if (!window.Contains(e.mousePosition)) Close();
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && window.Contains(e.mousePosition)) e.Use();
        }

        static void DrawSummary()
        {
            Heading("TODAY AND YESTERDAY");
            var today = Finance.Book(Economy.Day);
            DayLine($"Today (day {Economy.Day})", today);
            if (Economy.Day > 1) DayLine($"Yesterday (day {Economy.Day - 1})", Finance.Book(Economy.Day - 1));
        }

        static void DayLine(string label, DayBook book)
        {
            GUILayout.BeginVertical(UiTheme.Card);
            GUILayout.Label($"<b>{label}</b>", Rich());
            GUILayout.BeginHorizontal();
            GUILayout.Label($"In <color={UiTheme.LeafHex}><b>{Economy.Format(book.Income)}</b></color>", Rich());
            GUILayout.Label($"Out <color={UiTheme.CoralHex}><b>{Economy.Format(book.Spending)}</b></color>", Rich());
            GUILayout.Label($"Profit {Signed(book.Profit)}", Rich());
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        void DrawPeriodTabs()
        {
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < PeriodNames.Length; i++)
                if (GUILayout.Button(PeriodNames[i], i == (int)period ? UiTheme.Primary : UiTheme.Skin.button)) period = (Period)i;
            GUILayout.EndHorizontal();
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
            GUILayout.Space(8f);
            Heading(income ? "MONEY IN" : "MONEY OUT");
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
                GUILayout.Label("<i>Nothing yet.</i>", Rich());
                return;
            }
            var color = income ? UiTheme.Leaf : UiTheme.Coral;
            foreach (var k in order)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Finance.Name((MoneyKind)k), Rich(), GUILayout.Width(190f));
                var bar = GUILayoutUtility.GetRect(60f, 10f, GUILayout.ExpandWidth(true));
                bar.y += 6f;
                GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0.05f, 0.1f, 0.6f), 0f, 4f);
                GUI.DrawTexture(new Rect(bar.x, bar.y, Mathf.Max(6f, bar.width * totals[k] / (float)sum), bar.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 4f);
                GUILayout.Label($"<b>{Economy.Format(totals[k])}</b>", Rich(), GUILayout.Width(80f));
                GUILayout.EndHorizontal();
            }
            GUILayout.Label($"<size=11>Total: <b>{Economy.Format(sum)}</b></size>", Rich());
        }

        /// <summary>Profit day by day, as bars up (profit) or down (loss) from a middle line.</summary>
        static void DrawHistory()
        {
            Heading("DAY BY DAY");
            if (!Finance.ShowsHistory)
            {
                Locked($"Opens at {Progression.TrackName(XpTrack.Shopkeeping)} level {Finance.Settings.historyLevel}.");
                return;
            }
            var days = Finance.Recent(Finance.HistoryDays);
            int most = 1;
            foreach (var d in days) most = Mathf.Max(most, Mathf.Abs(d.Profit));
            var area = GUILayoutUtility.GetRect(200f, 120f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(area, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0.05f, 0.1f, 0.35f), 0f, 6f);
            float mid = area.y + area.height * 0.5f;
            GUI.DrawTexture(new Rect(area.x + 4f, mid, area.width - 8f, 1f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(1f, 1f, 1f, 0.25f), 0f, 0f);
            float slot = (area.width - 8f) / days.Count;
            for (int i = 0; i < days.Count; i++)
            {
                int profit = days[i].Profit;
                float hgt = (area.height * 0.5f - 14f) * Mathf.Abs(profit) / most;
                var bar = profit >= 0 ? new Rect(area.x + 4f + slot * i + slot * 0.2f, mid - hgt, slot * 0.6f, hgt) : new Rect(area.x + 4f + slot * i + slot * 0.2f, mid + 1f, slot * 0.6f, hgt);
                if (hgt > 0.5f) GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, profit >= 0 ? UiTheme.Leaf : UiTheme.Coral, 0f, 3f);
                GUI.Label(new Rect(area.x + 4f + slot * i, area.yMax - 16f, slot, 16f), $"<size=10>{days[i].day}</size>", Centered());
                GUI.Label(new Rect(area.x + 4f + slot * i, bar.y + (profit >= 0 ? -16f : hgt), slot, 16f), new GUIContent("", $"Day {days[i].day}: in {Economy.Format(days[i].Income)}, out {Economy.Format(days[i].Spending)}"));
            }
            int week = 0;
            foreach (var d in days) week += d.Profit;
            GUILayout.Label($"<size=11>Profit over these {days.Count} days: {Signed(week)}." + (string.IsNullOrEmpty(GUI.tooltip) ? "" : $"   {GUI.tooltip}") + "</size>", Rich());
        }

        /// <summary>What each tank's fish have sold for, all time.</summary>
        static void DrawPerTank()
        {
            GUILayout.Space(8f);
            Heading("EARNINGS PER TANK");
            if (!Finance.ShowsPerTank)
            {
                Locked($"Opens once you've earned {Economy.Format(Finance.Settings.perTankAfterEarning)} from sales ({Economy.Format(Finance.SalesTotal)} so far).");
                return;
            }
            Ranked(Merge(b => b.salesByTank), null);
        }

        /// <summary>Which kinds of fish sell best, all time: money and how many.</summary>
        static void DrawBestsellers()
        {
            GUILayout.Space(8f);
            Heading("BEST-SELLING FISH");
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

        static void Ranked(Dictionary<string, int> money, Dictionary<string, int> counts)
        {
            if (money.Count == 0)
            {
                GUILayout.Label("<i>No sales yet.</i>", Rich());
                return;
            }
            var keys = new List<string>(money.Keys);
            keys.Sort((a, b) => money[b].CompareTo(money[a]));
            int rank = 0;
            foreach (var key in keys)
            {
                if (++rank > 10) break;
                string count = counts != null && counts.TryGetValue(key, out var n) ? $"  <size=11>({n} sold)</size>" : "";
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{rank}. {key}{count}", Rich());
                GUILayout.Label($"<b>{Economy.Format(money[key])}</b>", Rich(), GUILayout.Width(80f));
                GUILayout.EndHorizontal();
            }
        }

        static void Locked(string why) => GUILayout.Label($"<color=#9fb4bc><b>Locked.</b> <i>{why}</i></color>", Rich());

        static string Signed(int amount) => amount >= 0 ? $"<color={UiTheme.LeafHex}><b>+{Economy.Format(amount)}</b></color>" : $"<color={UiTheme.CoralHex}><b>-{Economy.Format(-amount)}</b></color>";

        static void Heading(string text) => GUILayout.Label($"<b><color={UiTheme.SunHex}>{text}</color></b>", Rich());

        static GUIStyle rich, centered;
        static GUIStyle Rich() => rich != null ? rich : rich = new GUIStyle(UiTheme.Skin.label) { richText = true, wordWrap = true };
        static GUIStyle Centered() => centered != null ? centered : centered = new GUIStyle(UiTheme.Skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0) };
    }
}
