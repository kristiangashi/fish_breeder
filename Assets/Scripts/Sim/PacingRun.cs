using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FishBreeder.Core;
using FishBreeder.Shop;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Sim
{
    /// <summary>
    /// Headless pacing simulator. Started from the command line (see Tools/PacingSim/run.ps1):
    /// <c>-pacingSim &lt;output folder&gt; -pacingBots Cautious,Greedy -pacingSeeds 10 -pacingDays 120</c>.
    /// For every bot and seed it starts a new game in a fresh save slot, lets the bot play the
    /// given number of days through <see cref="DevFastForward"/> (the real time steps, the real
    /// shoppers), writes one JSON line per run with a row per day, deletes the slot, and quits
    /// when all runs are done.
    /// </summary>
    public class PacingRun : MonoBehaviour
    {
        [Serializable]
        public class DayRow
        {
            public int day, money, fishkeeping, shopkeeping, fishkeepingXp, shopkeepingXp;
            public float reputation;
            public int fish, tanks, sold, earned, visitors, deaths, boxes, rescues;
            public int bestFishValue, bestRarity;
            public string bestFish;
            public string events;
            /// <summary>Shoppers' complaints that day, "label: count; ...".</summary>
            public string complaints;
            public float stars;
            /// <summary>Living Uncommon / Rare-or-better fish, and how many of each have died so far.</summary>
            public int uncommon, rare, uncommonDeaths, rareDeaths;
            /// <summary>Most Uncommons in any one tank; fish of each rarity seen so far (Rare = Rare or rarer, fry included).</summary>
            public int maxUncommonInTank, seenCommon, seenUncommon, seenRare;
            /// <summary>Fish whose tank's algae suits them (Healthy green; algae eaters: grazing and no bloom), of all fish; tanks Overgrown or in bloom.</summary>
            public int fishAlgaeOk, fishAlgaeAll, tanksHarmfulAlgae, tanksWithFish;
            /// <summary>Tanks with fish in each algae stage (in bloom is <see cref="blooms"/>).</summary>
            public int tanksClear, tanksHealthy, tanksOvergrown;
            /// <summary>Running totals from the bot's care: tank sessions, those needing a wipe for water, wipes, filter-overloaded sessions, and sessions left under 75% water (overloaded / within the filter).</summary>
            public int sessions, choreSessions, wipes, overloadedSessions, overwhelmedOverloaded, overwhelmedWithinFilter, wipesOverloaded;
            /// <summary>Average algae stage (0 Clear .. 3 Bloom) and water quality (0..1) over tanks with fish.</summary>
            public float algae, water;
            /// <summary>The worst tank: highest algae stage, and lowest water health (what shoppers judge as dirty).</summary>
            public float maxAlgae, minWater;
            public int blooms;
            /// <summary>Yesterday's money in from sales and out by kind (care = medicine + water treatment).</summary>
            public int salesIn, foodOut, careOut, eggsOut, gearOut, net;
        }

        [Serializable]
        public class RunResult
        {
            public string bot;
            public int seed;
            public float seconds;
            public string error;
            /// <summary>Deaths by cause over the whole run, "cause: count; ...".</summary>
            public string deathCauses;
            /// <summary>Money in and out over the whole run by kind, "Sales: +1234; Food: -300; ...".</summary>
            public string books;
            public List<DayRow> days = new List<DayRow>();
        }

        string outFolder;
        readonly List<string> bots = new List<string>();
        int seeds = 5, days = 120, firstSeed = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (FindAnyObjectByType<PacingRun>() != null || Arg("-pacingSim") == null) return;
            var go = new GameObject("PacingRun");
            DontDestroyOnLoad(go);
            go.AddComponent<PacingRun>();
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        void Awake()
        {
            outFolder = Arg("-pacingSim");
            foreach (var b in (Arg("-pacingBots") ?? "Cautious,Greedy,Breeder,Careless,Mixer").Split(','))
                if (BotStyle.Find(b.Trim()) != null) bots.Add(b.Trim());
            if (int.TryParse(Arg("-pacingSeeds"), out var s)) seeds = Mathf.Max(1, s);
            if (int.TryParse(Arg("-pacingDays"), out var d)) days = Mathf.Max(1, d);
            if (int.TryParse(Arg("-pacingFirstSeed"), out var f)) firstSeed = f;
            Directory.CreateDirectory(outFolder);
        }

        IEnumerator Start()
        {
            string results = Path.Combine(outFolder, "runs.jsonl");
            File.WriteAllText(results, "");
            Log($"Pacing sim: {bots.Count} bots x {seeds} seeds x {days} days -> {results}");
            foreach (var botName in bots)
                for (int i = 0; i < seeds; i++)
                {
                    int seed = firstSeed + i;
                    int slot = SaveGame.FreeSlot();
                    // The same play log a person's game writes, so bot and real curves compare directly.
                    PlayLog.Player = "bot:" + botName.ToLower();
                    PlayLog.PathOverride = Path.Combine(outFolder, $"playlog-{botName}-{seed}.jsonl");
                    GameSession.Play(slot, true);
                    // Let the scene reload and the game build itself.
                    ShopDay shopDay = null;
                    for (int frame = 0; frame < 600 && shopDay == null; frame++)
                    {
                        yield return null;
                        shopDay = FindAnyObjectByType<ShopDay>();
                    }
                    yield return null;
                    yield return null;
                    var result = new RunResult { bot = botName, seed = seed };
                    float started = Time.realtimeSinceStartup;
                    // The first game of each bot keeps its whole game log, to see what went on.
                    StreamWriter gameLog = i == 0 ? new StreamWriter(Path.Combine(outFolder, $"log-{botName}-{seed}.txt")) : null;
                    Action<string> logLine = line => gameLog.WriteLine($"[day {Economy.Day} {shopDay.Phase}] {line}");
                    if (gameLog != null) GameLog.Added += logLine;
                    try { Play(shopDay, BotStyle.Find(botName), seed, result); }
                    catch (Exception e) { result.error = e.ToString(); Debug.LogException(e); }
                    finally
                    {
                        if (gameLog != null) { GameLog.Added -= logLine; gameLog.Dispose(); }
                    }
                    result.seconds = Time.realtimeSinceStartup - started;
                    File.AppendAllText(results, JsonUtility.ToJson(result) + "\n");
                    var last = result.days.Count > 0 ? result.days[result.days.Count - 1] : null;
                    Log($"Pacing sim: {botName} seed {seed}: {result.days.Count} days in {result.seconds:0.0}s" + (last != null ? $", ${last.money}, FK {last.fishkeeping}, SK {last.shopkeeping}" : "") + (result.error != null ? " ERROR" : ""));
                    // Leave the slot behind only while the next game builds (the bootstrap may still be saving).
                    yield return null;
                    SaveGame.Delete(slot);
                }
            Log("Pacing sim: done.");
            yield return null;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }

        static void Log(string line) => Console.WriteLine(line);

        void Play(ShopDay shopDay, BotStyle style, int seed, RunResult result)
        {
            if (shopDay == null) throw new Exception("The game didn't start (no ShopDay).");
            GameRandom.Seed(seed);
            var store = FindAnyObjectByType<StoreLayout>();
            var bot = new PacingBot(style, store, seed);
            var recorder = new Recorder(bot, store, shopDay, result);
            for (int d = 0; d < days; d++) DevFastForward.Run(shopDay, recorder, 1);
            var causes = new List<string>();
            foreach (var pair in bot.DeathCauses) causes.Add($"{pair.Key}: {pair.Value}");
            result.deathCauses = string.Join("; ", causes);
            var income = new int[Finance.KindCount];
            var spending = new int[Finance.KindCount];
            foreach (var book in Finance.Recent(days + 2))
                for (int k = 0; k < Finance.KindCount; k++) { income[k] += book.income[k]; spending[k] += book.spending[k]; }
            var lines = new List<string>();
            for (int k = 0; k < Finance.KindCount; k++)
            {
                if (income[k] != 0) lines.Add($"{(MoneyKind)k}: +{income[k]}");
                if (spending[k] != 0) lines.Add($"{(MoneyKind)k}: -{spending[k]}");
            }
            result.books = string.Join("; ", lines);
        }

        /// <summary>Wraps the bot: notes the shop's numbers each morning and the day's sales each evening.</summary>
        class Recorder : IDayBot
        {
            readonly PacingBot bot;
            readonly StoreLayout store;
            readonly ShopDay shopDay;
            readonly RunResult result;
            DayRow today;
            readonly HashSet<Fish> seen = new HashSet<Fish>();
            readonly int[] seenByRarity = new int[3];

            public Recorder(PacingBot bot, StoreLayout store, ShopDay shopDay, RunResult result)
            {
                this.bot = bot;
                this.store = store;
                this.shopDay = shopDay;
                this.result = result;
            }

            public string Name => bot.Name;

            public void Morning()
            {
                bot.Events.Clear();
                // Algae as the night's time steps left it, before the bot cleans (what the fish lived in).
                var algae = new DayRow();
                CountAlgae(algae);
                bot.Morning();
                today = Sample();
                today.algae = algae.algae;
                today.maxAlgae = algae.maxAlgae;
                today.tanksWithFish = algae.tanksWithFish;
                today.tanksClear = algae.tanksClear;
                today.tanksHealthy = algae.tanksHealthy;
                today.tanksOvergrown = algae.tanksOvergrown;
                today.blooms = algae.blooms;
                today.tanksHarmfulAlgae = algae.tanksHarmfulAlgae;
                today.fishAlgaeOk = algae.fishAlgaeOk;
                today.fishAlgaeAll = algae.fishAlgaeAll;
                today.events = string.Join("; ", bot.Events);
                result.days.Add(today);
            }

            public void Evening()
            {
                var report = shopDay.LastReport;
                if (today != null && report != null && !report.morning)
                {
                    today.sold = report.fishSold;
                    today.earned = report.earnings;
                    today.visitors = report.visitors;
                    today.stars = report.stars;
                    var parts = new List<string>();
                    for (int c = 0; c < report.complaints.Length; c++)
                        if (report.complaints[c] > 0) parts.Add($"{Reputation.Label((Complaint)c)}: {report.complaints[c]}");
                    today.complaints = string.Join("; ", parts);
                }
                bot.Evening();
            }

            /// <summary>Algae stages over the tanks with fish (shop tanks, not the public aquarium's).</summary>
            void CountAlgae(DayRow row)
            {
                foreach (var t in store.Tanks)
                {
                    if (t.Role == TankRole.Exhibit || t.Role == TankRole.Holding || t.Tank.Residents.Count == 0) continue;
                    var stage = t.Tank.Algae;
                    row.algae += (int)stage;
                    row.tanksWithFish++;
                    switch (stage)
                    {
                        case AlgaeStage.Clear: row.tanksClear++; break;
                        case AlgaeStage.HealthyGreen: row.tanksHealthy++; break;
                        case AlgaeStage.Overgrown: row.tanksOvergrown++; break;
                        default: row.blooms++; break;
                    }
                    if (t.Tank.TooMuchAlgae) row.tanksHarmfulAlgae++;
                    foreach (var fish in t.Tank.Residents)
                    {
                        if (fish.IsDead || fish.Species == null) continue;
                        row.fishAlgaeAll++;
                        bool ok = fish.Species.eatsAlgae ? !t.Tank.InBloom && t.Tank.AlgaeToGraze : stage == AlgaeStage.HealthyGreen;
                        if (ok) row.fishAlgaeOk++;
                    }
                    row.maxAlgae = Mathf.Max(row.maxAlgae, (int)stage);
                }
                if (row.tanksWithFish > 0) row.algae /= row.tanksWithFish;
            }

            DayRow Sample()
            {
                var row = new DayRow
                {
                    day = Economy.Day,
                    money = Economy.Money,
                    fishkeeping = Progression.Fishkeeping,
                    shopkeeping = Progression.Shopkeeping,
                    fishkeepingXp = Progression.Xp(XpTrack.Fishkeeping),
                    shopkeepingXp = Progression.Xp(XpTrack.Shopkeeping),
                    reputation = Economy.Reputation,
                    deaths = bot.Deaths,
                    boxes = bot.BoxesBought,
                    rescues = bot.GrandmaRescues,
                    tanks = store.Tanks.Count,
                };
                foreach (var t in store.Tanks)
                {
                    int uncommonHere = 0;
                    foreach (var fish in t.Tank.Residents)
                    {
                        if (fish.IsDead) continue;
                        row.fish++;
                        if (fish.Species != null && seen.Add(fish)) seenByRarity[Mathf.Clamp((int)fish.Species.rarity, 0, 2)]++;
                        if (fish.IdentityHidden) continue;
                        if (fish.Species.rarity == Genetics.SpeciesRarity.Uncommon) uncommonHere++;
                        int value = fish.AppraisedPrice;
                        if (value > row.bestFishValue)
                        {
                            row.bestFishValue = value;
                            row.bestFish = fish.Phenotype.SpeciesName;
                        }
                        row.bestRarity = Mathf.Max(row.bestRarity, (int)fish.Species.rarity);
                        if (fish.Species.rarity == Genetics.SpeciesRarity.Uncommon) row.uncommon++;
                        else if (fish.Species.rarity >= Genetics.SpeciesRarity.Rare) row.rare++;
                    }
                    row.maxUncommonInTank = Mathf.Max(row.maxUncommonInTank, uncommonHere);
                }
                row.sessions = bot.Sessions;
                row.choreSessions = bot.ChoreSessions;
                row.wipes = bot.Wipes;
                row.wipesOverloaded = bot.WipesOverloaded;
                row.overloadedSessions = bot.OverloadedSessions;
                row.overwhelmedOverloaded = bot.OverwhelmedOverloaded;
                row.overwhelmedWithinFilter = bot.OverwhelmedWithinFilter;
                row.seenCommon = seenByRarity[0];
                row.seenUncommon = seenByRarity[1];
                row.seenRare = seenByRarity[2];
                row.uncommonDeaths = bot.DeathsOfRarity(1);
                row.rareDeaths = bot.DeathsOfRarity(2);
                int withFish = 0;
                foreach (var t in store.Tanks)
                {
                    if (t.Role == TankRole.Exhibit || t.Role == TankRole.Holding || t.Tank.Residents.Count == 0) continue;
                    withFish++;
                    row.water += t.Tank.WaterQuality;
                    row.minWater = withFish == 1 ? t.Tank.WaterHealth : Mathf.Min(row.minWater, t.Tank.WaterHealth);
                }
                if (withFish > 0) row.water /= withFish;
                var books = Finance.Recent(2);
                if (books.Count > 1)
                {
                    var y = books[0];
                    row.salesIn = y.income[(int)MoneyKind.Sales];
                    row.foodOut = y.spending[(int)MoneyKind.Food];
                    row.careOut = y.spending[(int)MoneyKind.Care];
                    row.eggsOut = y.spending[(int)MoneyKind.FishAndEggs];
                    row.gearOut = y.spending[(int)MoneyKind.Equipment];
                    int inn = 0, outt = 0;
                    for (int k = 0; k < Finance.KindCount; k++) { inn += y.income[k]; outt += y.spending[k]; }
                    row.net = inn - outt;
                }
                return row;
            }
        }
    }
}
