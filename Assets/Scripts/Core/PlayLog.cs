using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Automatic play log for balancing: one JSON line per in-game day plus a line for each key
    /// event (level ups, deaths, sales, Grandma, discoveries, shop open/close), appended to
    /// "playlog{slot}.jsonl" next to the save. The progression simulator's bots write the same
    /// format (set <see cref="Player"/>), so a real playthrough can be plotted next to them.
    ///
    /// Every line has "t" (session, day or event), "day" (in-game day), "min" (real minutes
    /// played in this save) and "at" (UTC wall clock). Reloading an older save can repeat days:
    /// readers should keep the last "day" line for each day.
    ///
    /// session: player, format, slot, loaded, version, money.
    /// day: money, income, spending (that day's books), fk/sk/rs + _xp (levels and XP into the
    /// level), rep (0..1), fish (alive), best_value/best_appraised/best_fish (most valuable fish
    /// owned, true hidden value), species (journal count), sold, revenue, deaths, born, grandma
    /// (rescues), discovered, unlocks (list).
    /// event "ev": open, close/stayed_closed (visitors, sold, earned, stars), sale (fish, price,
    /// value, appraised), death (fish, cause, value), grandma_gift/grandma_rescue (amount),
    /// level (track, level, unlocks), discover (fish).
    /// </summary>
    public static class PlayLog
    {
        /// <summary>Format version, written on every session line.</summary>
        public const int FormatVersion = 1;

        /// <summary>Turn off to stop writing (tests that shouldn't leave files).</summary>
        public static bool Enabled = true;
        /// <summary>Who is playing: "human", or a bot name such as "bot:cautious".</summary>
        public static string Player = "human";
        /// <summary>Overrides where the log goes (the simulator points it at its run folder).</summary>
        public static string PathOverride;

        /// <summary>Real seconds played in this save (focused and not paused).</summary>
        public static double PlaySeconds { get; private set; }

        // Today's counters, written on the day line and then reset.
        static int sold, revenue, deaths, grandma, born, discovered;
        static readonly List<string> unlocks = new List<string>();

        public static string FilePath => !string.IsNullOrEmpty(PathOverride) ? PathOverride : PathFor(SaveGame.Slot);
        public static string PathFor(int slot) => Path.Combine(SaveGame.Folder, $"playlog{slot}.jsonl");

        public static void Reset()
        {
            PlaySeconds = 0;
            sold = revenue = deaths = grandma = born = discovered = 0;
            unlocks.Clear();
        }

        /// <summary>Counts real play time; called every frame while a game is running.</summary>
        public static void Tick(float unscaledDelta)
        {
            if (Time.timeScale <= 0f || !Application.isFocused && !Application.isBatchMode) return;
            PlaySeconds += Mathf.Min(unscaledDelta, 1f);
        }

        // ------------------------------------------------------------------ events

        public static void StartSession(bool loaded) =>
            Write(Line("session").Str("player", Player).Num("format", FormatVersion).Num("slot", SaveGame.Slot)
                .Bool("loaded", loaded).Str("version", Application.version).Num("money", Economy.Money));

        public static void ShopOpened() => Write(Line("event").Str("ev", "open"));

        public static void ShopClosed(int visitors, int fishSold, int earnings, float stars, bool stayedClosed) =>
            Write(Line("event").Str("ev", stayedClosed ? "stayed_closed" : "close").Num("visitors", visitors)
                .Num("sold", fishSold).Num("earned", earnings).Num("stars", stars));

        public static void Sale(Fish fish, int price)
        {
            sold++;
            revenue += price;
            Write(Line("event").Str("ev", "sale").Str("fish", fish.SpeciesLabel).Num("price", price)
                .Num("value", fish.TrueValue).Num("appraised", fish.AppraisedPrice));
        }

        public static void Death(Fish fish, string cause)
        {
            deaths++;
            Write(Line("event").Str("ev", "death").Str("fish", fish.SpeciesLabel).Str("cause", cause).Num("value", fish.TrueValue));
        }

        public static void Born(int count)
        {
            if (count > 0) born += count;
        }

        public static void Grandma(int amount, bool rescue)
        {
            if (rescue) grandma++;
            // Grandma comes overnight, after the day count has moved on: it belongs to the day that ended.
            Write(Line("event", Economy.Day - 1).Str("ev", rescue ? "grandma_rescue" : "grandma_gift").Num("amount", amount));
        }

        public static void LevelUp(XpTrack track, int level, List<string> unlocked)
        {
            var clean = new List<string>();
            foreach (var u in unlocked)
            {
                string text = Regex.Replace(u, "<.*?>", "");
                if (text.Contains("perk point")) continue;
                clean.Add(text);
                unlocks.Add(text);
            }
            Write(Line("event").Str("ev", "level").Str("track", track.ToString()).Num("level", level).List("unlocks", clean));
        }

        public static void Discovered(string species)
        {
            discovered++;
            Write(Line("event").Str("ev", "discover").Str("fish", species));
        }

        /// <summary>The day just ended (after the night step and Grandma): one summary line.</summary>
        public static void EndOfDay(int endedDay, IReadOnlyList<Store.StoreTank> tanks)
        {
            int fish = 0, best = 0, bestAppraised = 0;
            string bestName = null;
            if (tanks != null)
                foreach (var storeTank in tanks)
                {
                    var tank = storeTank != null ? storeTank.Tank : null;
                    if (tank == null) continue;
                    foreach (var f in tank.Residents)
                    {
                        if (f == null || f.IsDead) continue;
                        fish++;
                        int value = f.TrueValue;
                        if (value <= best) continue;
                        best = value;
                        bestAppraised = f.AppraisedPrice;
                        bestName = f.SpeciesLabel;
                    }
                }
            var book = Finance.Book(endedDay);
            Write(Line("day", endedDay)
                .Num("money", Economy.Money).Num("income", book.Income).Num("spending", book.Spending)
                .Num("fk", Progression.Level(XpTrack.Fishkeeping)).Num("fk_xp", Progression.Xp(XpTrack.Fishkeeping))
                .Num("sk", Progression.Level(XpTrack.Shopkeeping)).Num("sk_xp", Progression.Xp(XpTrack.Shopkeeping))
                .Num("rs", Progression.Level(XpTrack.Research)).Num("rs_xp", Progression.Xp(XpTrack.Research))
                .Num("rep", Economy.Reputation).Num("fish", fish).Num("best_value", best).Num("best_appraised", bestAppraised)
                .Str("best_fish", bestName).Num("species", Journal.Count)
                .Num("sold", sold).Num("revenue", revenue).Num("deaths", deaths).Num("born", born)
                .Num("grandma", grandma).Num("discovered", discovered).List("unlocks", unlocks));
            sold = revenue = deaths = grandma = born = discovered = 0;
            unlocks.Clear();
        }

        // ------------------------------------------------------------------ saving

        internal static void Save(SaveData d)
        {
            d.playSeconds = PlaySeconds;
            d.playLogToday = new[] { sold, revenue, deaths, grandma, born, discovered };
            d.playLogUnlocks = new List<string>(unlocks);
        }

        internal static void Load(SaveData d)
        {
            Reset();
            PlaySeconds = d.playSeconds;
            var t = d.playLogToday;
            if (t != null && t.Length >= 6) { sold = t[0]; revenue = t[1]; deaths = t[2]; grandma = t[3]; born = t[4]; discovered = t[5]; }
            if (d.playLogUnlocks != null) unlocks.AddRange(d.playLogUnlocks);
        }

        // ------------------------------------------------------------------ writing

        static Json Line(string type, int day = -1) =>
            new Json().Str("t", type).Num("day", day >= 0 ? day : Economy.Day).Num("min", Math.Round(PlaySeconds / 60.0, 1))
                .Str("at", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

        static void Write(Json line)
        {
            if (!Enabled) return;
            try
            {
                string path = FilePath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(path, line.Close() + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FishBreeder] Play log couldn't be written: {e.Message}");
            }
        }

        /// <summary>A tiny one-line JSON object writer (JsonUtility can't do lists of mixed fields).</summary>
        class Json
        {
            readonly StringBuilder sb = new StringBuilder("{");

            StringBuilder Key(string key)
            {
                if (sb.Length > 1) sb.Append(',');
                return sb.Append('"').Append(key).Append("\":");
            }

            public Json Num(string key, double value)
            {
                Key(key).Append(value.ToString("0.####", CultureInfo.InvariantCulture));
                return this;
            }

            public Json Bool(string key, bool value)
            {
                Key(key).Append(value ? "true" : "false");
                return this;
            }

            public Json Str(string key, string value)
            {
                Key(key);
                if (value == null) sb.Append("null");
                else Quote(value);
                return this;
            }

            public Json List(string key, List<string> values)
            {
                Key(key).Append('[');
                for (int i = 0; i < values.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Quote(values[i]);
                }
                sb.Append(']');
                return this;
            }

            void Quote(string s)
            {
                sb.Append('"');
                foreach (char c in s)
                {
                    if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                    else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                }
                sb.Append('"');
            }

            public string Close() => sb.Append('}').ToString();
        }
    }
}
