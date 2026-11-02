using System;
using System.Collections.Generic;
using System.IO;
using FishBreeder.Core;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.Health;
using FishBreeder.Shop;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The game's saves. Each game lives in its own numbered slot file (money, levels, research,
    /// rooms, every tank with its water, equipment, decorations, eggs and fish) in a save folder of
    /// its own: play mode in the Unity editor and real builds never share saves. <see cref="AutoSave"/>
    /// decides when the slot being played is written; the main menu and pause menu pick slots.
    /// </summary>
    public static class SaveGame
    {
        const int Version = 1;
        const string OldFileName = "savegame.json";

        /// <summary>Editor play mode and builds keep their saves in different folders.</summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, Application.isEditor ? "Saves (Editor)" : "Saves");
        /// <summary>The slot being played (and written by autosave).</summary>
        public static int Slot { get; private set; } = 1;
        public static string SlotPath(int slot) => Path.Combine(Folder, $"save{slot}.json");
        public static string FilePath => SlotPath(Slot);
        public static bool Exists => File.Exists(FilePath);
        /// <summary>Real time (unscaled seconds) of the last successful save, or -1.</summary>
        public static float LastSaved { get; private set; } = -1f;

        /// <summary>Picks the slot the game plays from and saves to.</summary>
        public static void UseSlot(int slot) => Slot = Mathf.Max(1, slot);

        /// <summary>The lowest slot number with no save in it, for a new game.</summary>
        public static int FreeSlot()
        {
            Migrate();
            int slot = 1;
            while (File.Exists(SlotPath(slot))) slot++;
            return slot;
        }

        /// <summary>Every save in the folder, the most recently saved first.</summary>
        public static List<SaveSlot> Slots()
        {
            Migrate();
            var list = new List<SaveSlot>();
            if (!Directory.Exists(Folder)) return list;
            foreach (var path in Directory.GetFiles(Folder, "save*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (!int.TryParse(name.Substring(4), out int slot) || slot < 1) continue;
                var info = new SaveSlot { slot = slot, written = File.GetLastWriteTime(path) };
                try
                {
                    var header = JsonUtility.FromJson<SaveHeader>(File.ReadAllText(path));
                    if (header != null && header.version > 0)
                    {
                        info.day = header.day;
                        info.money = header.money;
                        info.levels = header.levels ?? new int[0];
                        info.readable = true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[FishBreeder] Save {slot} couldn't be read: {e.Message}");
                }
                list.Add(info);
            }
            list.Sort((a, b) => b.written.CompareTo(a.written));
            return list;
        }

        /// <summary>The save played most recently (for Continue), or null.</summary>
        public static SaveSlot Latest()
        {
            foreach (var slot in Slots()) if (slot.readable) return slot;
            return null;
        }

        /// <summary>
        /// Before slots, the editor wrote one savegame.json next to the folders. It becomes editor
        /// save 1, so nothing played so far is lost.
        /// </summary>
        static void Migrate()
        {
            if (!Application.isEditor) return;
            string old = Path.Combine(Application.persistentDataPath, OldFileName);
            if (!File.Exists(old)) return;
            try
            {
                Directory.CreateDirectory(Folder);
                int slot = 1;
                while (File.Exists(SlotPath(slot))) slot++;
                File.Move(old, SlotPath(slot));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FishBreeder] The old save couldn't be moved into the save folder: {e.Message}");
            }
        }

        static SaveData pending;

        /// <summary>Writes the whole game to the save file. Written to a temporary file first, so a crash mid-save never leaves a broken save.</summary>
        public static bool Save(StoreLayout store, ShopDay shopDay)
        {
            if (store == null) return false;
            try
            {
                var data = new SaveData { version = Version, savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
                Economy.Save(data);
                Inventory.Save(data);
                Progression.Save(data);
                Perks.Save(data);
                Finance.Save(data);
                Research.Save(data);
                Reputation.Save(data);
                PublicAquarium.Save(data);
                Journal.Save(data);
                EggBoxes.Save(data);
                Milestones.Save(data);
                GameLog.Save(data);
                store.Save(data);
                if (shopDay != null) shopDay.Save(data);

                Directory.CreateDirectory(Folder);
                string path = FilePath, temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                LastSaved = Time.unscaledTime;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FishBreeder] Saving failed: {e}");
                return false;
            }
        }

        /// <summary>
        /// Reads the save file and puts back everything that isn't in the store (money, levels,
        /// research...). Call it after the resets and before the store is built; <see cref="LoadStore"/>
        /// then puts the rooms and tanks back. False if there's no save (or it can't be read).
        /// </summary>
        public static bool LoadState()
        {
            pending = null;
            Migrate();
            if (!Exists) return false;
            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[FishBreeder] The save file couldn't be read, starting a new game: {e}");
                return false;
            }
            if (data == null || data.version <= 0) return false;
            Economy.Load(data);
            Inventory.Load(data);
            Progression.Load(data);
            Perks.Load(data);
            Finance.Load(data);
            Research.Load(data);
            Reputation.Load(data);
            PublicAquarium.Load(data);
            Journal.Load(data);
            EggBoxes.Load(data);
            Milestones.Load(data);
            GameLog.Load(data);
            pending = data;
            return true;
        }

        /// <summary>Puts the rooms, tanks and the time of day back from the save read by <see cref="LoadState"/>.</summary>
        public static void LoadStore(StoreLayout store, ShopDay shopDay)
        {
            if (pending == null) return;
            store.Load(pending);
            if (shopDay != null) shopDay.Load(pending);
            pending = null;
        }

        /// <summary>Deletes one save for good.</summary>
        public static void Delete(int slot)
        {
            string path = SlotPath(slot);
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>Deletes every save in this folder (developer use: editor saves from the editor).</summary>
        public static void DeleteAll()
        {
            Migrate();
            foreach (var slot in Slots()) Delete(slot.slot);
        }

        // ---------------------------------------------------------------- helpers for the parts

        internal static List<KeyInt> Pairs<T>(Dictionary<T, int> map, Func<T, string> key)
        {
            var list = new List<KeyInt>();
            foreach (var pair in map) list.Add(new KeyInt { key = key(pair.Key), value = pair.Value });
            return list;
        }

        internal static void Fill(Dictionary<string, int> map, List<KeyInt> pairs)
        {
            map.Clear();
            if (pairs != null) foreach (var pair in pairs) if (pair.key != null) map[pair.key] = pair.value;
        }

        internal static void Fill(HashSet<string> set, List<string> items)
        {
            set.Clear();
            if (items != null) foreach (var item in items) if (item != null) set.Add(item);
        }

        internal static bool TryEnum<T>(string text, out T value) where T : struct => Enum.TryParse(text, out value) && Enum.IsDefined(typeof(T), value);
    }

    /// <summary>
    /// Saves the game every few real minutes (15 unless changed in Settings), whenever the part of
    /// the day changes (opening, closing, staying closed, going to sleep), and when the game is
    /// quit or put in the background. With autosave turned off in Settings only the pause menu
    /// saves. Shows "Game saved" in the corner for a moment.
    /// </summary>
    public class AutoSave : MonoBehaviour
    {
        StoreLayout store;
        ShopDay shopDay;
        float timer;
        DayPhase lastPhase;
        int lastDay;
        bool ready;

        /// <summary>The game being played's autosave, or null on the main menu.</summary>
        public static AutoSave Current { get; private set; }

        public void Initialize(StoreLayout layout, ShopDay day)
        {
            store = layout;
            shopDay = day;
            lastPhase = day != null ? day.Phase : DayPhase.Morning;
            lastDay = Economy.Day;
            ready = true;
            Current = this;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Update()
        {
            if (!ready) return;
            if (!GameSettings.Autosave)
            {
                timer = 0f;
                return;
            }
            timer += Time.unscaledDeltaTime;
            bool phaseChanged = shopDay != null && shopDay.Phase != lastPhase;
            if (timer >= GameSettings.AutosaveMinutes * 60f || phaseChanged || Economy.Day != lastDay) SaveNow();
        }

        /// <summary>Saves right away (the pause menu's Save uses it too). False if saving failed.</summary>
        public bool SaveNow()
        {
            if (!ready) return false;
            timer = 0f;
            lastPhase = shopDay != null ? shopDay.Phase : lastPhase;
            lastDay = Economy.Day;
            return SaveGame.Save(store, shopDay);
        }

        /// <summary>Stops all saving from here on: leaving without saving.</summary>
        public void Stop() => ready = false;

        void OnApplicationPause(bool paused)
        {
            if (paused && GameSettings.Autosave) SaveNow();
        }

        void OnApplicationQuit()
        {
            if (GameSettings.Autosave) SaveNow();
        }
    }

    // ==================================================================== the save file

    /// <summary>One save as the load menu lists it.</summary>
    public class SaveSlot
    {
        public int slot;
        public DateTime written;
        public bool readable;
        public int day, money;
        public int[] levels = new int[0];
    }

    /// <summary>Just the start of a save file, for listing saves without restoring them.</summary>
    [Serializable]
    class SaveHeader
    {
        public int version;
        public int money;
        public int day;
        public int[] levels;
    }

    [Serializable] public class KeyInt { public string key; public int value; }
    [Serializable] public class KeyFloat { public string key; public float value; }

    [Serializable]
    public class SaveData
    {
        public int version;
        public string savedAt;

        // Economy
        public int money;
        public int day = 1;
        public float shopReputation = 0.5f;
        public string grandmaNote;
        public List<KeyInt> food = new List<KeyInt>();
        public List<string> ledger = new List<string>();

        // Inventory
        public List<KeyInt> decorations = new List<KeyInt>();
        public List<KeyInt> backgrounds = new List<KeyInt>();

        // Progression and perks
        public int[] xp = new int[0];
        public int[] levels = new int[0];
        public List<GoalSave> goals = new List<GoalSave>();
        public string lastFishGoal, lastShopGoal;
        public List<KeyInt> perks = new List<KeyInt>();

        // Finance
        public int fishSold, salesTotal;
        public List<DayBookSave> books = new List<DayBookSave>();

        // Research
        public List<KeyInt> researchCounts = new List<KeyInt>();
        public List<KeyInt> researchTotals = new List<KeyInt>();
        public List<string> researchPosted = new List<string>();
        public List<string> researchReady = new List<string>();
        public List<string> researchDone = new List<string>();
        public List<string> researchUnlocked = new List<string>();
        public List<string> favouritesSeen = new List<string>();
        public List<KeyFloat> researchProgress = new List<KeyFloat>();
        public string researchCurrent;
        public List<string> researchQueue = new List<string>();
        public int researchBudget;

        // Reputation
        public float[] complaintHeat = new float[0];
        public List<ReviewSave> reviews = new List<ReviewSave>();
        public List<KeyInt> latestWishes = new List<KeyInt>();

        // The public aquarium
        public AquariumSave aquarium;

        // Journal, egg boxes, milestones, log
        public List<string> discovered = new List<string>();
        public List<string> unseen = new List<string>();
        public List<KeyInt> raised = new List<KeyInt>();
        public List<KeyInt> milestoneProgress = new List<KeyInt>();
        public List<string> milestonesDone = new List<string>();
        public List<string> log = new List<string>();

        // Store (layout 1: the public aquarium's two exhibit spots replaced the East wing's four wall aquariums)
        public int layoutVersion;
        public List<int> sectionsUnlocked = new List<int>();
        public List<TankSave> tanks = new List<TankSave>();

        // The day
        public string phase;
        public float timeLeft;
        public DayReportSave today;
        public DayReportSave lastReport;
    }

    [Serializable]
    public class GoalSave
    {
        public string kind, track, text;
        public int target, progress, xp;
    }

    [Serializable]
    public class DayBookSave
    {
        public int day;
        public int[] income, spending;
        public List<KeyInt> salesBySpecies, soldBySpecies, salesByTank;
    }

    [Serializable]
    public class ReviewSave
    {
        public int day;
        public float stars;
        public string text;
    }

    [Serializable]
    public class DayReportSave
    {
        public int day, visitors, fishSold, earnings, happy, grossed, sad, mad;
        public float stars, reputationChange;
        public List<string> reviews;
        public int[] complaints;
        public List<KeyInt> wishes;
        public string tanks;
        public bool stayedClosed, morning;
        public bool hasAquarium;
        public AquariumReport aquarium;

        public static DayReportSave From(DayReport r)
        {
            if (r == null) return null;
            return new DayReportSave
            {
                day = r.day, visitors = r.visitors, fishSold = r.fishSold, earnings = r.earnings,
                happy = r.happy, grossed = r.grossed, sad = r.sad, mad = r.mad,
                stars = r.stars, reputationChange = r.reputationChange,
                reviews = new List<string>(r.reviews), complaints = (int[])r.complaints.Clone(),
                wishes = SaveGame.Pairs(r.wishes, k => k), tanks = r.tanks,
                stayedClosed = r.stayedClosed, morning = r.morning,
                hasAquarium = r.aquarium != null, aquarium = r.aquarium,
            };
        }

        public DayReport ToReport()
        {
            var r = new DayReport
            {
                day = day, visitors = visitors, fishSold = fishSold, earnings = earnings,
                happy = happy, grossed = grossed, sad = sad, mad = mad,
                stars = stars, reputationChange = reputationChange,
                tanks = tanks, stayedClosed = stayedClosed, morning = morning,
            };
            if (reviews != null) r.reviews.AddRange(reviews);
            if (complaints != null) Array.Copy(complaints, r.complaints, Mathf.Min(complaints.Length, r.complaints.Length));
            SaveGame.Fill(r.wishes, wishes);
            if (hasAquarium && aquarium != null && aquarium.issues != null && aquarium.issues.Length == PublicAquarium.IssueCount) r.aquarium = aquarium;
            return r;
        }
    }

    [Serializable]
    public class TankSave
    {
        public int slot;
        public string role;
        public int number;
        public string name;
        public int size;
        public float waterQuality, algae, temperature, ph, salinity, heaterSetting;
        public bool pests, fed;
        /// <summary>Cleans left this session (the old name for them was wipes).</summary>
        public int wipesLeft;
        public int deepCleansLeft = -1;
        public bool bloom;
        /// <summary>Algae added by feeding since the last time step (what's over the maximum goes at the next one).</summary>
        public float algaeFromFeeding;
        public bool hasFilter, hasHeater, hasBubbler;
        public InstalledEquipment filter, heater, bubbler;
        public string background, sideBackground;
        public float backgroundZoom = 1f;
        public Vector2 backgroundCenter = new Vector2(0.5f, 0.5f);
        public List<DecorSave> decor = new List<DecorSave>();
        public List<ClutchSave> eggs = new List<ClutchSave>();
        public List<FishSave> fish = new List<FishSave>();
    }

    [Serializable]
    public class DecorSave
    {
        public string id;
        public bool moved;
        public float x, z, yaw, size = 1f;
    }

    [Serializable]
    public class ClutchSave
    {
        public List<Genome> genomes = new List<Genome>();
        public int generation;
        public string parents;
        public bool bred;
        public int stepsToHatch;
        public string label;
        public Color color;
        public bool speckled;
    }

    [Serializable]
    public class FishSave
    {
        public string name;
        public int generation;
        public Genome genome;
        public string sex, size;
        public float hunger, growth, bigGrowth, ageDays, valueFactor, charm;
        public int price, breedCooldown, stepsSinceFavourite;
        public string parents;
        public bool dead;
        public string causeOfDeath;
        // Pregnancy
        public List<Genome> unborn = new List<Genome>();
        public int unbornGeneration, stepsToBirth;
        public string unbornFather;
        // Health
        public float health = 1f, severity, injury, medicineLoad, medicineStress;
        public float comfortBefore = -1f;
        public string disease, injuryCause;
        public int medicineDamage;
    }

    // ==================================================================== each part saves itself

    public static partial class Economy
    {
        internal static void Save(SaveData d)
        {
            d.money = Money;
            d.day = Day;
            d.shopReputation = Reputation;
            d.grandmaNote = GrandmaNote;
            d.food = SaveGame.Pairs(food, k => k.ToString());
            d.ledger = new List<string>(ledger);
        }

        internal static void Load(SaveData d)
        {
            Money = d.money;
            Day = Mathf.Max(1, d.day);
            Reputation = Mathf.Clamp01(d.shopReputation);
            GrandmaNote = d.grandmaNote;
            food.Clear();
            if (d.food != null)
                foreach (var pair in d.food)
                    if (SaveGame.TryEnum(pair.key, out FoodType type)) food[type] = pair.value;
            ledger.Clear();
            if (d.ledger != null) ledger.AddRange(d.ledger);
        }
    }

    public static partial class Inventory
    {
        internal static void Save(SaveData d)
        {
            d.decorations = SaveGame.Pairs(decorations, k => k);
            d.backgrounds = SaveGame.Pairs(backgrounds, k => k);
        }

        internal static void Load(SaveData d)
        {
            SaveGame.Fill(decorations, d.decorations);
            SaveGame.Fill(backgrounds, d.backgrounds);
        }
    }

    public static partial class Progression
    {
        internal static void Save(SaveData d)
        {
            d.xp = (int[])xp.Clone();
            d.levels = (int[])level.Clone();
            d.goals = new List<GoalSave>();
            foreach (var g in goals)
                d.goals.Add(new GoalSave { kind = g.kind.ToString(), track = g.track.ToString(), text = g.text, target = g.target, progress = g.progress, xp = g.xp });
            d.lastFishGoal = lastFishGoal?.ToString();
            d.lastShopGoal = lastShopGoal?.ToString();
        }

        internal static void Load(SaveData d)
        {
            for (int i = 0; i < xp.Length; i++)
            {
                xp[i] = d.xp != null && i < d.xp.Length ? Mathf.Max(0, d.xp[i]) : 0;
                level[i] = d.levels != null && i < d.levels.Length ? Mathf.Clamp(d.levels[i], 1, Mathf.Max(1, Data.maxLevel)) : 1;
            }
            levelUps.Clear();
            goals.Clear();
            if (d.goals != null)
                foreach (var g in d.goals)
                {
                    if (!SaveGame.TryEnum(g.kind, out GoalKind kind) || !SaveGame.TryEnum(g.track, out XpTrack track)) continue;
                    goals.Add(new DailyGoal { kind = kind, track = track, text = g.text, target = Mathf.Max(1, g.target), progress = g.progress, xp = g.xp });
                }
            lastFishGoal = SaveGame.TryEnum(d.lastFishGoal, out GoalKind fish) ? fish : (GoalKind?)null;
            lastShopGoal = SaveGame.TryEnum(d.lastShopGoal, out GoalKind shop) ? shop : (GoalKind?)null;
        }
    }

    public static partial class Perks
    {
        internal static void Save(SaveData d) => d.perks = SaveGame.Pairs(ranks, k => k);

        internal static void Load(SaveData d)
        {
            SaveGame.Fill(ranks, d.perks);
            Changed?.Invoke();
        }
    }

    public static partial class Finance
    {
        internal static void Save(SaveData d)
        {
            d.fishSold = FishSold;
            d.salesTotal = SalesTotal;
            d.books = new List<DayBookSave>();
            foreach (var book in days)
                d.books.Add(new DayBookSave
                {
                    day = book.day,
                    income = (int[])book.income.Clone(),
                    spending = (int[])book.spending.Clone(),
                    salesBySpecies = SaveGame.Pairs(book.salesBySpecies, k => k),
                    soldBySpecies = SaveGame.Pairs(book.soldBySpecies, k => k),
                    salesByTank = SaveGame.Pairs(book.salesByTank, k => k),
                });
        }

        internal static void Load(SaveData d)
        {
            FishSold = d.fishSold;
            SalesTotal = d.salesTotal;
            days.Clear();
            if (d.books == null) return;
            foreach (var saved in d.books)
            {
                var book = new DayBook { day = saved.day };
                // Money kinds are saved by position; kinds added later just start at zero.
                if (saved.income != null) Array.Copy(saved.income, book.income, Mathf.Min(saved.income.Length, book.income.Length));
                if (saved.spending != null) Array.Copy(saved.spending, book.spending, Mathf.Min(saved.spending.Length, book.spending.Length));
                SaveGame.Fill(book.salesBySpecies, saved.salesBySpecies);
                SaveGame.Fill(book.soldBySpecies, saved.soldBySpecies);
                SaveGame.Fill(book.salesByTank, saved.salesByTank);
                days.Add(book);
            }
            days.Sort((a, b) => a.day.CompareTo(b.day));
        }
    }

    public static partial class Research
    {
        internal static void Save(SaveData d)
        {
            d.researchCounts = SaveGame.Pairs(counts, k => k);
            d.researchTotals = SaveGame.Pairs(totals, k => k.ToString());
            d.researchPosted = new List<string>(posted);
            d.researchReady = new List<string>(ready);
            d.researchDone = new List<string>(done);
            d.researchUnlocked = new List<string>();
            foreach (var unlock in unlocked) d.researchUnlocked.Add(unlock.ToString());
            d.favouritesSeen = new List<string>(favouritesSeen);
            d.researchProgress = new List<KeyFloat>();
            foreach (var pair in progress) d.researchProgress.Add(new KeyFloat { key = pair.Key, value = pair.Value });
            d.researchCurrent = current;
            d.researchQueue = new List<string>(queue);
            d.researchBudget = DailyBudget;
        }

        internal static void Load(SaveData d)
        {
            SaveGame.Fill(counts, d.researchCounts);
            totals.Clear();
            if (d.researchTotals != null)
                foreach (var pair in d.researchTotals)
                    if (SaveGame.TryEnum(pair.key, out MilestoneKind kind)) totals[kind] = pair.value;
            SaveGame.Fill(posted, d.researchPosted);
            SaveGame.Fill(ready, d.researchReady);
            SaveGame.Fill(done, d.researchDone);
            unlocked.Clear();
            if (d.researchUnlocked != null)
                foreach (var text in d.researchUnlocked)
                    if (SaveGame.TryEnum(text, out ResearchUnlock unlock)) unlocked.Add(unlock);
            SaveGame.Fill(favouritesSeen, d.favouritesSeen);
            progress.Clear();
            if (d.researchProgress != null) foreach (var pair in d.researchProgress) if (pair.key != null) progress[pair.key] = pair.value;
            current = string.IsNullOrEmpty(d.researchCurrent) ? null : d.researchCurrent;
            queue.Clear();
            if (d.researchQueue != null) queue.AddRange(d.researchQueue);
            SetBudget(d.researchBudget);
        }
    }

    public static partial class Reputation
    {
        internal static void Save(SaveData d)
        {
            d.complaintHeat = (float[])heat.Clone();
            d.reviews = new List<ReviewSave>();
            foreach (var r in reviews) d.reviews.Add(new ReviewSave { day = r.day, stars = r.stars, text = r.text });
            d.latestWishes = new List<KeyInt>();
            foreach (var pair in LatestWishes) d.latestWishes.Add(new KeyInt { key = pair.Key, value = pair.Value });
        }

        internal static void Load(SaveData d)
        {
            Array.Clear(heat, 0, heat.Length);
            if (d.complaintHeat != null) Array.Copy(d.complaintHeat, heat, Mathf.Min(d.complaintHeat.Length, heat.Length));
            reviews.Clear();
            if (d.reviews != null) foreach (var r in d.reviews) reviews.Add(new Review { day = r.day, stars = r.stars, text = r.text });
            LatestWishes.Clear();
            if (d.latestWishes != null) foreach (var pair in d.latestWishes) LatestWishes.Add(new KeyValuePair<string, int>(pair.key, pair.value));
        }
    }

    public static partial class Journal
    {
        internal static void Save(SaveData d)
        {
            d.discovered = new List<string>(discovered);
            d.unseen = new List<string>(unseen);
        }

        internal static void Load(SaveData d)
        {
            SaveGame.Fill(discovered, d.discovered);
            SaveGame.Fill(unseen, d.unseen);
        }
    }

    public static partial class Milestones
    {
        internal static void Save(SaveData d)
        {
            d.milestoneProgress = SaveGame.Pairs(progress, k => k.ToString());
            d.milestonesDone = new List<string>(done);
        }

        internal static void Load(SaveData d)
        {
            progress.Clear();
            if (d.milestoneProgress != null)
                foreach (var pair in d.milestoneProgress)
                    if (SaveGame.TryEnum(pair.key, out MilestoneKind kind)) progress[kind] = pair.value;
            SaveGame.Fill(done, d.milestonesDone);
        }
    }

    public static partial class GameLog
    {
        internal static void Save(SaveData d) => d.log = new List<string>(entries);

        internal static void Load(SaveData d)
        {
            entries.Clear();
            if (d.log != null) entries.AddRange(d.log);
            while (entries.Count > MaxEntries) entries.RemoveAt(0);
        }
    }
}

namespace FishBreeder.Store
{
    public static partial class EggBoxes
    {
        internal static void Save(SaveData d) => d.raised = SaveGame.Pairs(raised, k => k);
        internal static void Load(SaveData d) => SaveGame.Fill(raised, d.raised);
    }

    public partial class StoreLayout
    {
        /// <summary>1: the public aquarium's two exhibit spots replaced the East wing's four wall aquariums.</summary>
        const int LayoutVersion = 1;
        /// <summary>Where the East wing's wall aquarium spots were in a layout 0 save: west, back, then the east wall's north and south.</summary>
        const int OldWestWall = 11, OldBackWall = 12, OldEastNorth = 13, OldEastSouth = 14;

        internal void Save(SaveData d)
        {
            d.layoutVersion = LayoutVersion;
            d.sectionsUnlocked = new List<int>();
            foreach (var section in sections) if (section.unlocked) d.sectionsUnlocked.Add(section.index);
            d.tanks = new List<TankSave>();
            foreach (var tank in tanks)
            {
                var saved = tank.Tank.Save();
                saved.slot = slots.IndexOf(tank.Slot);
                saved.role = tank.Role.ToString();
                saved.number = tank.Number;
                saved.name = tank.CustomName;
                d.tanks.Add(saved);
            }
        }

        /// <summary>Swaps the new game's rooms and starter tanks for the saved ones.</summary>
        internal void Load(SaveData d)
        {
            foreach (var section in sections) section.unlocked = section.index == 0 || (d.sectionsUnlocked != null && d.sectionsUnlocked.Contains(section.index));

            // Take out the starter tanks a new game begins with.
            foreach (var tank in tanks)
            {
                if (tank.Slot != null) tank.Slot.tank = null;
                // Out of the physics scene now, so its decorations don't block where the saved fish go.
                tank.gameObject.SetActive(false);
                Destroy(tank.gameObject);
            }
            tanks.Clear();
            publicTanks.Clear();

            var moved = d.layoutVersion < 1 ? OldWallAquariums(d.tanks) : null;
            if (d.tanks != null)
                foreach (var saved in d.tanks)
                {
                    if (moved != null && saved.slot >= OldWestWall && saved.slot <= OldEastSouth)
                    {
                        if (!moved.TryGetValue(saved.slot, out int to))
                        {
                            // Only two exhibits fit: the rest go back, paid for, fish and all.
                            int refund = Balance.Data.store.wallAquariumPrice;
                            Economy.Earn(refund, "a wall aquarium the public aquarium has no room for", MoneyKind.Refunds);
                            GameLog.Add($"The East wing is the {PublicAquarium.RoomName.ToLower()} now, with room for two exhibits. One of your wall aquariums didn't fit: it was refunded ({Economy.Format(refund)}) and its fish went back to the fish seller.");
                            continue;
                        }
                        saved.slot = to;
                        saved.role = TankRole.Exhibit.ToString();
                    }
                    if (saved.slot >= 0 && saved.slot < slots.Count && slots[saved.slot].retired && !RetireWallBreeder(saved, d.tanks)) continue;
                    if (saved.slot < 0 || saved.slot >= slots.Count || slots[saved.slot].tank != null) continue;
                    if (!SaveGame.TryEnum(saved.role, out TankRole role)) continue;
                    var slot = slots[saved.slot];
                    if (!slot.Allows(role)) continue;
                    var tank = PlaceTank(slot, role, null);
                    tank.SetRole(role, Mathf.Max(1, saved.number));
                    if (!string.IsNullOrEmpty(saved.name)) tank.Rename(saved.name);
                    if (saved.size != tank.Tank.SizeIndex && SizeInfo(tank.Tank, saved.size) != null)
                    {
                        if (tank.Tank.IsWallAquarium)
                        {
                            var at = tank.transform.position;
                            tank.transform.position = new Vector3(at.x, WallAquariumBase(SizeInfo(tank.Tank, saved.size).height), at.z);
                        }
                        tank.Tank.Resize(saved.size);
                        BuildStand(tank);
                    }
                    tank.Tank.Load(saved);
                }
            BuildShell();
            RefreshEmptySlots();
        }

        /// <summary>
        /// The big breeding room's wall breeding tank is gone: one in a save is refunded, and its fish
        /// and eggs move into a grand breeder in a free spot in the breeding room (its decorations
        /// don't fit, so they go). With no free spot its fish go back to the fish seller. False if
        /// there's nowhere for it.
        /// </summary>
        bool RetireWallBreeder(TankSave saved, List<TankSave> all)
        {
            int refund = Balance.Data.store.wallBreederPrice;
            Economy.Earn(refund, "the wall breeding tank, taken out", MoneyKind.Refunds);
            // A spot no other saved tank is coming back to.
            int to = -1;
            for (int i = 0; i < slots.Count && to < 0; i++)
                if (slots[i].tank == null && slots[i].IsOpen && !slots[i].wall && slots[i].Allows(TankRole.Breeder) && !all.Exists(t => t.slot == i)) to = i;
            if (to < 0)
            {
                GameLog.Add($"The wall breeding tank is gone from the big breeding room, which only has holding tanks now. It was refunded ({Economy.Format(refund)}), and with no free spot in the breeding room its fish went back to the fish seller.");
                return false;
            }
            saved.slot = to;
            saved.role = TankRole.Breeder.ToString();
            saved.size = Balance.Data.store.tankSizes.Count - 1;
            saved.decor = new List<DecorSave>();
            GameLog.Add($"The wall breeding tank is gone from the big breeding room, which only has holding tanks now. It was refunded ({Economy.Format(refund)}), and its fish moved into a free grand breeder tank in the breeding room.");
            return true;
        }

        /// <summary>
        /// A save from before the public aquarium: its East wing wall aquariums move onto the two
        /// exhibit spots (the back wall's to the north, the east wall's first one to the east, the
        /// west wall's to whichever is free). Old spot to new; any left out don't fit.
        /// </summary>
        Dictionary<int, int> OldWallAquariums(List<TankSave> saved)
        {
            var result = new Dictionary<int, int>();
            if (saved == null) return result;
            int north = slots.FindIndex(s => s.exhibit == ExhibitClimate.Temperate), east = slots.FindIndex(s => s.exhibit == ExhibitClimate.Tropical);
            var taken = new HashSet<int>();
            void Move(int from, params int[] options)
            {
                if (!saved.Exists(t => t.slot == from)) return;
                foreach (var to in options)
                    if (to >= 0 && taken.Add(to))
                    {
                        result[from] = to;
                        return;
                    }
            }
            Move(OldBackWall, north, east);
            Move(OldEastNorth, east, north);
            Move(OldEastSouth, east, north);
            Move(OldWestWall, north, east);
            return result;
        }
    }
}

namespace FishBreeder.Shop
{
    public partial class ShopDay
    {
        internal void Save(SaveData d)
        {
            d.phase = Phase.ToString();
            d.timeLeft = TimeLeft;
            d.today = IsOpen ? DayReportSave.From(today) : null;
            d.lastReport = DayReportSave.From(LastReport);
        }

        /// <summary>Back to the part of the day the game was saved in. A shop saved while open opens again with the time it had left.</summary>
        internal void Load(SaveData d)
        {
            LastReport = d.lastReport != null && d.lastReport.day > 0 ? d.lastReport.ToReport() : null;
            if (!SaveGame.TryEnum(d.phase, out DayPhase phase)) phase = DayPhase.Morning;
            if (phase == DayPhase.Open)
            {
                Phase = DayPhase.Open;
                TimeLeft = Mathf.Max(1f, d.timeLeft);
                spawnTimer = 2f;
                today = d.today != null ? d.today.ToReport() : new DayReport { day = Economy.Day };
                layout.BuildNavMesh();
                if (Aquarium != null) Aquarium.Resume();
            }
            else
            {
                Phase = phase;
                PublicAquarium.Today = null;
            }
        }
    }
}

namespace FishBreeder
{
    public partial class Tank
    {
        internal TankSave Save()
        {
            var s = new TankSave
            {
                size = sizeIndex,
                waterQuality = waterQuality, algae = algae, temperature = temperature, ph = ph, salinity = salinity,
                heaterSetting = heaterSetting, pests = pests, fed = FedSinceTimeStep, wipesLeft = CleansLeft, deepCleansLeft = DeepCleansLeft, bloom = bloom, algaeFromFeeding = algaeFromFeeding,
                hasFilter = filter != null, hasHeater = heater != null, hasBubbler = bubbler != null,
                filter = filter, heater = heater, bubbler = bubbler,
                background = backgroundId, sideBackground = sideBackgroundId,
                backgroundZoom = backgroundZoom, backgroundCenter = backgroundCenter,
                decor = decor != null ? decor.Save() : new List<DecorSave>(),
            };
            foreach (var clutch in EggClutch.All) if (clutch != null && clutch.Tank == this) s.eggs.Add(clutch.Save());
            foreach (var fish in residents) if (fish != null) s.fish.Add(fish.Save());
            foreach (var fish in corpses) if (fish != null) s.fish.Add(fish.Save());
            return s;
        }

        /// <summary>Puts the saved water, equipment, pictures, decorations, eggs and fish into this (empty, already sized) tank.</summary>
        internal void Load(TankSave s)
        {
            waterQuality = Mathf.Clamp01(s.waterQuality);
            algae = Mathf.Clamp01(s.algae);
            temperature = s.temperature;
            ph = s.ph;
            heaterSetting = s.heaterSetting;
            pests = s.pests;
            FedSinceTimeStep = s.fed;
            CleansLeft = Mathf.Max(0, s.wipesLeft);
            if (s.deepCleansLeft >= 0) DeepCleansLeft = s.deepCleansLeft;
            bloom = s.bloom;
            algaeFromFeeding = Mathf.Max(0f, s.algaeFromFeeding);
            // JsonUtility writes empty objects for missing equipment, hence the flags.
            filter = s.hasFilter ? Kind(s.filter, EquipmentKind.Filter) : null;
            heater = s.hasHeater ? Kind(s.heater, EquipmentKind.Heater) : null;
            bubbler = s.hasBubbler ? Kind(s.bubbler, EquipmentKind.Bubbler) : null;
            foreach (var kind in Equipment.Kinds) BuildEquipment(kind);
            UpdateEquipmentLook();
            if (!Mathf.Approximately(salinity, s.salinity))
            {
                salinity = s.salinity;
                BuildSubstrate();
            }
            backgroundId = s.background ?? "";
            backgroundZoom = Mathf.Clamp(s.backgroundZoom, 1f, MaxBackgroundZoom);
            backgroundCenter = s.backgroundCenter;
            BuildBackground();
            SetSideBackground(s.sideBackground);
            decor.Load(s.decor);
            if (s.eggs != null) foreach (var clutch in s.eggs) EggClutch.Restore(this, clutch);
            if (s.fish != null) foreach (var fish in s.fish) Fish.Restore(this, fish);
        }

        static InstalledEquipment Kind(InstalledEquipment item, EquipmentKind kind)
        {
            item = item ?? new InstalledEquipment();
            item.kind = kind;
            return item;
        }
    }

    public partial class TankDecor
    {
        internal List<DecorSave> Save()
        {
            var list = new List<DecorSave>();
            foreach (var p in placed) list.Add(new DecorSave { id = p.id, moved = p.moved, x = p.x, z = p.z, yaw = p.yaw, size = p.size });
            return list;
        }

        /// <summary>Puts saved pieces back in the order they were added (so each gets the same spot), where the player moved them and at the size they set.</summary>
        internal void Load(List<DecorSave> saved)
        {
            foreach (var p in placed) if (p.piece != null) Destroy(p.piece);
            placed.Clear();
            if (saved != null)
                foreach (var s in saved)
                {
                    if (!Add(s.id, WallAquarium ? Mathf.Max(0.01f, s.size) : 1f) || !s.moved) continue;
                    var p = placed[placed.Count - 1];
                    p.moved = true;
                    p.x = s.x;
                    p.z = s.z;
                    p.yaw = s.yaw;
                    Pose(p);
                }
            Physics.SyncTransforms();
        }
    }

    public partial class EggClutch
    {
        internal ClutchSave Save()
        {
            var s = new ClutchSave { generation = generation, parents = parents, bred = parents != null, stepsToHatch = StepsToHatch, label = Label, color = new Color(1f, 0.93f, 0.75f) };
            s.genomes.AddRange(genomes);
            var egg = transform.childCount > 0 ? transform.GetChild(0).GetComponent<Renderer>() : null;
            if (egg != null && egg.sharedMaterial != null)
            {
                s.color = egg.sharedMaterial.color;
                s.speckled = egg.sharedMaterial.mainTexture != null;
            }
            return s;
        }

        internal static void Restore(Tank tank, ClutchSave s)
        {
            if (s.genomes == null) return;
            s.genomes.RemoveAll(g => g == null || Species.Find(g.species) == null);
            if (s.genomes.Count == 0) return;
            var clutch = LayFrom(tank, s.genomes, Mathf.Max(1, s.generation), s.bred ? s.parents ?? "" : null, Mathf.Max(1, s.stepsToHatch), s.color, s.speckled);
            if (!string.IsNullOrEmpty(s.label)) clutch.Label = s.label;
        }
    }

    public partial class Fish
    {
        internal FishSave Save()
        {
            var s = new FishSave
            {
                name = fishName, generation = generation, genome = genome, sex = sex.ToString(), size = size.ToString(),
                hunger = hunger, growth = growth, bigGrowth = bigGrowth, ageDays = ageDays, valueFactor = valueFactor, charm = charm,
                price = price, breedCooldown = BreedCooldown, stepsSinceFavourite = stepsSinceFavourite,
                parents = ParentNames, dead = IsDead, causeOfDeath = CauseOfDeath,
                unbornGeneration = unbornGeneration, stepsToBirth = StepsToBirth, unbornFather = UnbornFather,
                health = Health.Current, disease = Health.Disease != null ? Health.Disease.id : null, severity = Health.Severity,
                injury = Health.Injury, injuryCause = Health.InjuryCause, medicineLoad = Health.MedicineLoad,
                medicineStress = Health.MedicineStress, medicineDamage = Health.MedicineDamage, comfortBefore = Health.ComfortBefore,
            };
            s.unborn.AddRange(unborn);
            return s;
        }

        /// <summary>A saved fish back in its tank: the same fish, without counting as a new one for milestones or the journal.</summary>
        internal static Fish Restore(Tank tank, FishSave s)
        {
            if (s.genome == null || Species.Find(s.genome.species) == null) return null;
            var go = new GameObject("Fish");
            go.transform.SetPositionAndRotation(tank.RandomFreePoint(0.1f), Quaternion.Euler(0f, GameRandom.Range(0f, 360f), 0f));
            var fish = go.AddComponent<Fish>();
            fish.Load(tank, s);
            return fish;
        }

        void Load(Tank tank, FishSave s)
        {
            genome = s.genome;
            Phenotype = genome.Express();
            generation = Mathf.Max(1, s.generation);
            growth = Mathf.Clamp01(s.growth);
            bigGrowth = Mathf.Clamp01(s.bigGrowth);
            ageDays = s.ageDays;
            hunger = Mathf.Clamp01(s.hunger);
            sex = SaveGame.TryEnum(s.sex, out Sex savedSex) ? savedSex : Sex.Female;
            size = SaveGame.TryEnum(s.size, out FishSize savedSize) ? savedSize : FishSize.Medium;
            valueFactor = s.valueFactor > 0f ? s.valueFactor : 1f;
            charm = s.charm;
            price = Mathf.Max(0, s.price);
            BreedCooldown = s.breedCooldown;
            stepsSinceFavourite = s.stepsSinceFavourite;
            ParentNames = s.parents;
            fishName = string.IsNullOrEmpty(s.name) ? FishNames.Random() : s.name;
            gameObject.name = $"Fish - {fishName}";
            Tank = tank;
            unborn.Clear();
            if (s.unborn != null) foreach (var g in s.unborn) if (g != null && Genetics.Species.Find(g.species) != null) unborn.Add(g);
            unbornGeneration = s.unbornGeneration;
            StepsToBirth = s.stepsToBirth;
            UnbornFather = s.unbornFather;
            Health.Load(s);

            Visual.Build(Phenotype, Stage == LifeStage.Fry);
            ApplyGrowthScale();
            if (!s.dead)
            {
                tank.Add(this);
                Health.RefreshTint();
                return;
            }
            IsDead = true;
            CauseOfDeath = s.causeOfDeath;
            tank.AddCorpse(this);
            Swimmer.enabled = false;
            Visual.ShowIllness(null, 0f);
            Visual.SetTint(new Color(0.75f, 0.75f, 0.7f));
            var at = transform.position;
            transform.position = new Vector3(at.x, tank.InnerBounds.max.y - 0.07f, at.z);
            StartCoroutine(FloatUp());
        }
    }
}

namespace FishBreeder.Health
{
    public partial class FishHealth
    {
        internal void Load(FishSave s)
        {
            current = Mathf.Clamp01(s.health);
            Disease = string.IsNullOrEmpty(s.disease) ? null : Balance.Data.diseases.Find(x => x != null && x.id == s.disease);
            Severity = Disease != null ? Mathf.Clamp01(s.severity) : 0f;
            Injury = Mathf.Clamp01(s.injury);
            InjuryCause = s.injuryCause;
            MedicineLoad = Mathf.Max(0f, s.medicineLoad);
            MedicineStress = Mathf.Clamp01(s.medicineStress);
            MedicineDamage = Mathf.Max(0, s.medicineDamage);
            comfortBefore = s.comfortBefore;
        }
    }
}
