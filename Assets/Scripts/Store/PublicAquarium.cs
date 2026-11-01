using System;
using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using FishBreeder.Health;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>One of the ticket booth's upgrades: each makes the estimator surer and lets a few more visitors in at once.</summary>
    [Serializable]
    public class BoothUpgradeBalance
    {
        public string name;
        public string blurb;
        public int price;
        [Tooltip("Shopkeeping level needed.")]
        public int level = 1;
        [Tooltip("More visitors allowed inside at once.")]
        public int extraVisitors = 2;

        public static List<BoothUpgradeBalance> CreateDefaults() => new List<BoothUpgradeBalance>
        {
            new BoothUpgradeBalance { name = "Price board", blurb = "A chalk board of what other aquariums charge, so you can see where your price sits.", price = 1500, level = 6, extraVisitors = 2 },
            new BoothUpgradeBalance { name = "Visitor counter", blurb = "A turnstile that counts everyone coming and going, and how many walked off at the booth.", price = 4000, level = 12, extraVisitors = 2 },
            new BoothUpgradeBalance { name = "Computerised ticketing", blurb = "A ticketing system that knows exactly what visitors will pay. The estimate is spot on.", price = 9000, level = 20, extraVisitors = 3 },
        };
    }

    /// <summary>The public aquarium (the second room): its exhibits, visitors, ticket prices and rating (see Docs/PublicAquarium.md).</summary>
    [Serializable]
    public class AquariumBalance
    {
        [Tooltip("What the game calls the second room.")]
        public string roomName = "Public aquarium";
        [Tooltip("What each of the two exhibits (wall aquariums) costs.")]
        public int exhibitPrice = 12000;
        [Tooltip("Shopkeeping level needed to buy an exhibit.")]
        public int exhibitLevel = 8;
        [Tooltip("Visitors an hour at the start, before appeal and rating.")]
        public float visitorsPerHour = 30f;
        [Tooltip("Visitors are multiplied by the first number at 0 appeal and the second at 100.")]
        public float appealTrafficAtZero = 0.15f, appealTrafficAtFull = 2.5f;
        [Tooltip("The rating multiplier on visitors: the first number at 0 stars, the second at 5.")]
        public float trafficAtNoStars = 0.3f, trafficAtFiveStars = 2f;
        [Tooltip("Rating the aquarium starts with (0..1, a fifth of it per star).")]
        [Range(0f, 1f)] public float startingRating = 0.5f;
        [Tooltip("Share of the old rating kept after a full day (the rest moves to how the day's visitors felt).")]
        [Range(0f, 0.95f)] public float ratingMemory = 0.7f;
        [Tooltip("A day with fewer visitors than this moves the rating less, in proportion.")]
        public int fullDayVisitors = 20;
        [Tooltip("Fair ticket price at 0 appeal and at 100.")]
        public float minFairPrice = 3f, maxFairPrice = 40f;
        [Tooltip("The fair price is multiplied by the first number at 0 stars and the second at 5.")]
        public float fairPriceAtNoStars = 0.85f, fairPriceAtFiveStars = 1.2f;
        [Tooltip("Each visitor pays up to the fair price times a random number between these.")]
        public float minWillingness = 0.75f, maxWillingness = 1.35f;
        public int startingTicketPrice = 5;
        public int maxTicketPrice = 200;
        [Tooltip("Most visitors inside at once, before booth upgrades.")]
        public int maxVisitorsInside = 10;
        [Tooltip("How far off the estimator can be (a share of the real number) at each booth level: none, then after each upgrade.")]
        public List<float> estimatorError = new List<float> { 0.25f, 0.15f, 0.07f, 0f };
        public List<BoothUpgradeBalance> boothUpgrades = BoothUpgradeBalance.CreateDefaults();
        [Tooltip("Looks each visitor takes at the exhibits.")]
        public int minLooks = 2, maxLooks = 4;
        public float minLookSeconds = 5f, maxLookSeconds = 11f;
        [Tooltip("Seconds the booth takes to sell a ticket.")]
        public float serveSeconds = 1.2f;
        [Tooltip("A visit scoring this or more is a happy one; below the second, an unhappy one.")]
        [Range(0f, 1f)] public float happyScore = 0.65f, unhappyScore = 0.4f;
        [Tooltip("How much of a visit's score is what they saw (the rest is value for money).")]
        [Range(0f, 1f)] public float sightShare = 0.6f;
        public int xpPerTicket = 1, xpHappyVisitor = 1;
        public int reviewsKept = 12;

        [Header("Who lives in the exhibits")]
        [Tooltip("Small fish (the hybrids, each species' Size group) an exhibit holds as bought, then after each height upgrade (the last number carries on).")]
        public List<int> smallPerExhibit = new List<int> { 20, 30, 40 };
        [Tooltip("Medium fish an exhibit holds as bought, then after each height upgrade.")]
        public List<int> mediumPerExhibit = new List<int> { 5, 10, 15 };
        [Tooltip("Large fish an exhibit holds as bought, then after each height upgrade.")]
        public List<int> largePerExhibit = new List<int> { 1, 3, 5 };
        [Tooltip("Chance a pair breeds in an exhibit, as a share of the chance in an ordinary tank: small, medium and large fish.")]
        public float smallBreeding = 0.35f, mediumBreeding = 0.15f, largeBreeding = 0.05f;

        [Header("Appeal")]
        [Tooltip("Points for how full an exhibit is (half its room for full marks), how many kinds, how eye-catching, aquarium quality and clean water.")]
        public float fullnessPoints = 20f, varietyPoints = 35f, looksPoints = 20f, qualityPoints = 15f, cleanPoints = 5f;
        [Tooltip("Kinds of fish an exhibit needs for full variety points.")]
        public int fullVarietyKinds = 8;
        [Tooltip("An exhibit where one kind is more than this share of the fish looks samey and loses points, up to Samey penalty when it's all one kind.")]
        [Range(0f, 1f)] public float sameyShare = 0.4f;
        public float sameyPenalty = 20f;
        [Tooltip("Visitors see both exhibits: each kind of fish in the whole aquarium past Kinds before bonus adds Kind bonus to its appeal, up to Max kind bonus.")]
        public int kindsBeforeBonus = 5;
        public float kindBonus = 2f, maxKindBonus = 16f;

        [Header("Fish market")]
        [Tooltip("What the fish market pays, as a share of a fish's appraised value.")]
        [Range(0f, 2f)] public float marketShare = 0.75f;
        [Tooltip("Shopkeeping XP for each fish sold on the market.")]
        public int xpMarketSale = 3;

        [Header("The seller's dives")]
        public ScubaBalance scuba = new ScubaBalance();
    }
}

namespace FishBreeder.Store
{
    /// <summary>Which fish an exhibit in the public aquarium takes.</summary>
    public enum ExhibitClimate { None, Temperate, Tropical }

    /// <summary>
    /// How big a kind of fish is in the public aquarium, from its <see cref="SizeGroup"/>: each size
    /// has its own limit in an exhibit. None is a Tiny tank fish, which can't go in.
    /// </summary>
    public enum AquariumSize { None, Small, Medium, Large }

    /// <summary>What can spoil a visit to the aquarium, for its reviews.</summary>
    public enum AquariumIssue { DeadFish, DirtyWater, SickFish, Fighting, FewFish, TooPricey }

    /// <summary>One day at the public aquarium, for the evening report (and the save).</summary>
    [Serializable]
    public class AquariumReport
    {
        public int visitors, turnedAway, tickets, earnings, happy, unhappy;
        public float scoreSum;
        public float ratingChange;
        public int[] issues = new int[PublicAquarium.IssueCount];
        public List<string> reviews = new List<string>();

        public int Judged => tickets + turnedAway;
        /// <summary>The day's average visit, 0..1 (turned-away visitors count at half weight).</summary>
        public float AverageScore
        {
            get
            {
                float weight = tickets + turnedAway * 0.5f;
                return weight > 0f ? scoreSum / weight : 0.5f;
            }
        }
    }

    /// <summary>
    /// The public aquarium: the second room, with its own entrance and ticket booth, where people
    /// pay to look at two big exhibits (temperate on the north wall, tropical on the east) and
    /// leave. How good the exhibits are (their appeal) sets what a ticket is fairly worth and
    /// draws visitors; how each visit went moves the aquarium's own rating, which multiplies how
    /// many come. The booth's estimator guesses the fair price and the day ahead, a little wrong
    /// until the booth has had all three upgrades. Visitors themselves are run by
    /// <see cref="Shop.AquariumDay"/>; this holds the numbers and the rules.
    /// </summary>
    public static partial class PublicAquarium
    {
        public const int IssueCount = 6;

        public class Review
        {
            public int day;
            public float stars;
            public string text;
        }

        public struct Appeal
        {
            public float total;
            public float fullness, variety, looks, quality, clean;
            public float dead, dirty, sick, fighting, samey;
            public int fish, kinds;
        }

        static readonly List<Review> reviews = new List<Review>();

        public static AquariumBalance Data => Balance.Data.aquarium ?? (Balance.Data.aquarium = new AquariumBalance());
        /// <summary>0..1, a fifth of it per star.</summary>
        public static float Rating { get; private set; } = 0.5f;
        public static float Stars => Rating * 5f;
        public static int TicketPrice { get; private set; } = 5;
        /// <summary>Upgrades bought for the ticket booth (0 to 3).</summary>
        public static int BoothLevel { get; private set; }
        /// <summary>Which way and how far off the estimator is today, -1..1 (rolled each morning).</summary>
        public static float EstimateRoll { get; private set; }
        public static IReadOnlyList<Review> Reviews => reviews;
        /// <summary>The day so far while the shop is open, or null.</summary>
        public static AquariumReport Today { get; set; }

        public static string RoomName => string.IsNullOrEmpty(Data.roomName) ? "Public aquarium" : Data.roomName;

        public static void Reset()
        {
            Rating = Mathf.Clamp01(Data.startingRating);
            TicketPrice = Mathf.Max(1, Data.startingTicketPrice);
            BoothLevel = 0;
            reviews.Clear();
            appealCache.Clear();
            speciesLimits.Clear();
            Today = null;
            RollEstimate();
            ScubaDives.Reset();
        }

        /// <summary>A new morning: the estimator is off by a different amount today.</summary>
        public static void NewDay()
        {
            RollEstimate();
            ForgetAppeal();
        }

        /// <summary>Time has moved on (fish grew, died, the water changed): appeal is worked out afresh.</summary>
        public static void ForgetAppeal() => appealCache.Clear();

        static void RollEstimate() => EstimateRoll = GameRandom.Range(-1f, 1f);

        public static void SetTicketPrice(int price) => TicketPrice = Mathf.Clamp(price, 1, Mathf.Max(1, Data.maxTicketPrice));

#if UNITY_EDITOR
        /// <summary>Developer shortcut: nudges the rating (0.1 is half a star).</summary>
        public static void DevChangeRating(float delta) => Rating = Mathf.Clamp01(Rating + delta);
#endif

        // ------------------------------------------------------------ climate

        /// <summary>Temperate for cold-water fish, tropical for warm (bottom feeders and shellfish go by their temperature).</summary>
        public static ExhibitClimate ClimateOf(Species species)
        {
            if (species == null) return ExhibitClimate.None;
            switch (species.category)
            {
                case FishCategory.FreshwaterCold:
                case FishCategory.SaltwaterCold: return ExhibitClimate.Temperate;
                case FishCategory.FreshwaterTropical:
                case FishCategory.SaltwaterTropical: return ExhibitClimate.Tropical;
                default: return (species.tempMin + species.tempMax) * 0.5f < 22f ? ExhibitClimate.Temperate : ExhibitClimate.Tropical;
            }
        }

        /// <summary>Whether a fish can go in a tank of this climate (None takes anything).</summary>
        public static bool Suits(ExhibitClimate climate, Species species) => climate == ExhibitClimate.None || species == null || ClimateOf(species) == climate;

        public static string ClimateName(ExhibitClimate climate) => climate == ExhibitClimate.Temperate ? "Temperate" : climate == ExhibitClimate.Tropical ? "Tropical" : "";

        // ------------------------------------------------------------ who lives here

        /// <summary>
        /// A kind of fish's size in the aquarium: its size group (set per species in the balance
        /// console). Tiny tank fish are None: they can't go in at all, unless set to live only here,
        /// when they count as small.
        /// </summary>
        public static AquariumSize SizeClass(Species species)
        {
            if (species == null || !species.CanLiveInEastWing) return AquariumSize.None;
            switch (species.sizeGroup)
            {
                case SizeGroup.Large: return AquariumSize.Large;
                case SizeGroup.Medium: return AquariumSize.Medium;
                default: return AquariumSize.Small;
            }
        }

        public static string SizeName(AquariumSize size) => size == AquariumSize.None ? "Tiny" : size.ToString();

        /// <summary>How many times an exhibit has been raised (0 as bought).</summary>
        public static int ExhibitStep(Tank tank) => tank != null && tank.IsWallAquarium ? tank.SizeIndex - Tank.WallSize : 0;

        static List<int> LimitList(AquariumSize size)
        {
            var d = Data;
            return size == AquariumSize.Small ? d.smallPerExhibit : size == AquariumSize.Medium ? d.mediumPerExhibit : size == AquariumSize.Large ? d.largePerExhibit : null;
        }

        /// <summary>How many fish of this size an exhibit holds at its height.</summary>
        public static int ExhibitLimit(Tank tank, AquariumSize size) => ExhibitLimitAt(ExhibitStep(tank), size);

        /// <summary>How many fish of this size an exhibit holds after this many height upgrades.</summary>
        public static int ExhibitLimitAt(int step, AquariumSize size)
        {
            var list = LimitList(size);
            if (list == null || list.Count == 0) return 0;
            return Mathf.Max(0, list[Mathf.Clamp(step, 0, list.Count - 1)]);
        }

        /// <summary>All the fish an exhibit holds, every size together.</summary>
        public static int ExhibitCapacity(Tank tank) => Mathf.Max(1, ExhibitLimit(tank, AquariumSize.Small) + ExhibitLimit(tank, AquariumSize.Medium) + ExhibitLimit(tank, AquariumSize.Large));

        /// <summary>Living fish of this size in a tank (eggs not counted).</summary>
        public static int CountOfSize(Tank tank, AquariumSize size)
        {
            int n = 0;
            foreach (var fish in tank.Residents)
                if (fish != null && !fish.IsDead && SizeClass(fish.Species) == size) n++;
            return n;
        }

        /// <summary>How many more of this kind an exhibit has room for (0 for little fish, or the wrong climate).</summary>
        public static int ExhibitRoomFor(Tank tank, Species species)
        {
            var size = SizeClass(species);
            if (size == AquariumSize.None || !tank.Suits(species)) return 0;
            return Mathf.Max(0, ExhibitLimit(tank, size) - CountOfSize(tank, size));
        }

        /// <summary>Breeding is rarer in the exhibits, and rarer still the bigger the fish.</summary>
        public static float BreedingFactor(Species species)
        {
            switch (SizeClass(species))
            {
                case AquariumSize.Small: return Mathf.Max(0f, Data.smallBreeding);
                case AquariumSize.Medium: return Mathf.Max(0f, Data.mediumBreeding);
                case AquariumSize.Large: return Mathf.Max(0f, Data.largeBreeding);
                default: return 0f;
            }
        }

        /// <summary>A holding tank keeps one, two or three of the seller's finds, by its size.</summary>
        public static int HoldingCapacity(Tank tank)
        {
            var list = ScubaDives.Data.holdingFishPerSize;
            if (list == null || list.Count == 0) return 1;
            return Mathf.Max(1, list[Mathf.Clamp(tank != null ? tank.SizeIndex : 0, 0, list.Count - 1)]);
        }

        /// <summary>"20/30/40": an exhibit's limit for this size as bought and after each upgrade.</summary>
        public static string LimitsText(AquariumSize size)
        {
            var list = LimitList(size);
            return list == null || list.Count == 0 ? "0" : string.Join("/", list);
        }

        /// <summary>For the fish panel: where this kind may live, in a sentence.</summary>
        public static string WhereItLives(Fish fish)
        {
            var species = fish.Species;
            var size = SizeClass(species);
            string room = RoomName.ToLower();
            if (size == AquariumSize.None) return $"A tiny tank fish: it lives in ordinary tanks, not the {room}.";
            string limits = $"{SizeName(size).ToLower()} fish in the {room} ({LimitsText(size)} per exhibit)";
            if (species.eastWingOnly) return $"Lives only in the {room}, or a holding tank until it goes there. A {limits}.";
            if (!species.GrowsWithRoom) return $"Lives in ordinary tanks or the {room}. A {limits}.";
            if (fish.IsFullGrown) return $"Full grown: too big for any tank but the {room} now. A {limits}.";
            return $"Grows big: it can live in ordinary tanks or the {room} until it's full grown, then only the {room}. A {limits}.";
        }

        // ------------------------------------------------------------ the fish market

        static readonly Dictionary<string, int> speciesLimits = new Dictionary<string, int>();

        /// <summary>Most of this kind the player wants in the aquarium (both exhibits together); 0 = no limit.</summary>
        public static int SpeciesLimit(string id) => id != null && speciesLimits.TryGetValue(id, out var n) ? n : 0;

        public static void SetSpeciesLimit(string id, int limit)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (limit <= 0) speciesLimits.Remove(id);
            else speciesLimits[id] = limit;
        }

        /// <summary>Living fish of this kind in the exhibits.</summary>
        public static int CountInAquarium(StoreLayout store, string id)
        {
            int n = 0;
            foreach (var exhibit in Exhibits(store))
                foreach (var fish in exhibit.Tank.Residents)
                    if (fish != null && !fish.IsDead && fish.Species.id == id) n++;
            return n;
        }

        /// <summary>What the fish market pays for this fish today.</summary>
        public static int MarketPrice(Fish fish) => fish == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(fish.AppraisedValue * Mathf.Max(0f, Data.marketShare)));

        /// <summary>Sells a fish on the fish market: paid at once, and it's gone.</summary>
        public static int SellOnMarket(Fish fish, bool automatic = false)
        {
            if (fish == null || fish.IsDead) return 0;
            int price = MarketPrice(fish);
            Economy.Earn(price, $"{fish.FishName} the {fish.SpeciesLabel} on the fish market", MoneyKind.Market);
            Progression.Award(XpTrack.Shopkeeping, Data.xpMarketSale);
            GameLog.Add(automatic
                ? $"{fish.FishName} the {fish.SpeciesLabel} went to the fish market for {Economy.Format(price)}: the aquarium is at its limit of {SpeciesLimit(fish.Species.id)}."
                : $"Sold {fish.FishName} the {fish.SpeciesLabel} on the fish market for {Economy.Format(price)}.");
            fish.RemoveFromTank();
            ForgetAppeal();
            return price;
        }

        /// <summary>Lets a fish go back to the wild: no money, a little Fishkeeping XP for doing right by it.</summary>
        public static void Release(Fish fish)
        {
            if (fish == null || fish.IsDead) return;
            Progression.Award(XpTrack.Fishkeeping, ScubaDives.Data.xpRelease);
            GameLog.Add($"You let {fish.FishName} the {fish.SpeciesLabel} go back to the wild. Swim free!");
            fish.RemoveFromTank();
            ForgetAppeal();
        }

        /// <summary>
        /// After a time step: a kind with a limit set that has more fish than that in the aquarium
        /// (babies grown up, usually) sends grown ones, picked at random, to the fish market until
        /// it's back to the limit. Returns how many went.
        /// </summary>
        public static int TrimToLimits(IReadOnlyList<StoreTank> tanks)
        {
            if (speciesLimits.Count == 0 || tanks == null) return 0;
            var living = new Dictionary<string, int>();
            var adults = new Dictionary<string, List<Fish>>();
            foreach (var storeTank in tanks)
            {
                if (storeTank.Role != TankRole.Exhibit) continue;
                foreach (var fish in storeTank.Tank.Residents)
                {
                    if (fish == null || fish.IsDead) continue;
                    string id = fish.Species.id;
                    living[id] = living.TryGetValue(id, out var n) ? n + 1 : 1;
                    if (fish.Stage != LifeStage.Adult) continue;
                    if (!adults.TryGetValue(id, out var list)) adults[id] = list = new List<Fish>();
                    list.Add(fish);
                }
            }
            int sold = 0;
            foreach (var pair in living)
            {
                int limit = SpeciesLimit(pair.Key), extra = pair.Value - limit;
                if (limit <= 0 || extra <= 0 || !adults.TryGetValue(pair.Key, out var grown)) continue;
                for (; extra > 0 && grown.Count > 0; extra--)
                {
                    int pick = GameRandom.Range(0, grown.Count);
                    SellOnMarket(grown[pick], true);
                    grown.RemoveAt(pick);
                    sold++;
                }
            }
            return sold;
        }

        // ------------------------------------------------------------ appeal

        /// <summary>The exhibits that are in (one or both), north first.</summary>
        public static List<StoreTank> Exhibits(StoreLayout store)
        {
            var list = new List<StoreTank>();
            if (store == null) return list;
            foreach (var tank in store.Tanks) if (tank.Role == TankRole.Exhibit) list.Add(tank);
            list.Sort((a, b) => store.SlotOrder(a.Slot).CompareTo(store.SlotOrder(b.Slot)));
            return list;
        }

        /// <summary>
        /// How good one exhibit is to look at, 0..100: how full it is, how many kinds, how
        /// eye-catching the fish are, the aquarium's quality and clear water, minus dead, sick or
        /// fighting fish and dirty water.
        /// </summary>
        public static Appeal ExhibitAppeal(Tank tank)
        {
            if (tank == null) return new Appeal();
            // Worked out once a frame per tank: the aquarium window asks many times over.
            if (appealCache.TryGetValue(tank, out var cached) && cached.frame == Time.frameCount) return cached.appeal;
            var a = WorkOutAppeal(tank);
            appealCache[tank] = (Time.frameCount, a);
            return a;
        }

        static readonly Dictionary<Tank, (int frame, Appeal appeal)> appealCache = new Dictionary<Tank, (int, Appeal)>();

        static Appeal WorkOutAppeal(Tank tank)
        {
            var a = new Appeal();
            var kinds = new HashSet<string>();
            var kindCounts = new Dictionary<string, int>();
            float looks = 0f;
            int sick = 0, living = 0;
            foreach (var fish in tank.Residents)
            {
                if (fish == null || fish.IsDead) continue;
                living++;
                if (fish.Health.IsSick) sick++;
                if (fish.Stage == LifeStage.Fry) continue;
                a.fish++;
                looks += fish.Attractiveness;
                kinds.Add(fish.Species.id);
                kindCounts[fish.Species.id] = kindCounts.TryGetValue(fish.Species.id, out var count) ? count + 1 : 1;
            }
            a.kinds = kinds.Count;
            if (living == 0) return a;
            var d = Data;
            a.fullness = d.fullnessPoints * Mathf.Clamp01(a.fish / Mathf.Max(1f, tank.Capacity * 0.5f));
            // Variety counts for a lot: people don't come to see one or two kinds of fish. The first
            // few kinds count most, so going from one to three is worth more than seven to nine.
            a.variety = d.varietyPoints * Mathf.Sqrt(Mathf.Clamp01(a.kinds / (float)Mathf.Max(1, d.fullVarietyKinds)));
            int most = 0;
            foreach (var n in kindCounts.Values) most = Mathf.Max(most, n);
            float share = a.fish > 0 ? most / (float)a.fish : 0f;
            if (a.fish >= 3 && share > d.sameyShare) a.samey = -d.sameyPenalty * Mathf.Clamp01((share - d.sameyShare) / Mathf.Max(0.01f, 1f - d.sameyShare));
            a.looks = a.fish > 0 ? d.looksPoints * Mathf.Clamp01(looks / a.fish) : 0f;
            a.quality = d.qualityPoints * Mathf.Clamp01(tank.Quality / 80f);
            var shop = Balance.Data.shop;
            bool dirty = tank.WaterHealth < shop.grossWaterQuality || tank.Algae > shop.grossAlgae;
            a.clean = !dirty && tank.WaterHealth > 0.7f && tank.Algae < 0.3f ? d.cleanPoints : 0f;
            a.dead = -Mathf.Min(40f, 8f * tank.Corpses.Count);
            a.dirty = dirty ? -25f : 0f;
            a.sick = -30f * sick / Mathf.Max(1f, living);
            a.fighting = TankMates.IsFighting(tank) ? -15f : 0f;
            a.total = Mathf.Clamp(a.fullness + a.variety + a.looks + a.quality + a.clean + a.samey + a.dead + a.dirty + a.sick + a.fighting, 0f, 100f);
            return a;
        }

        /// <summary>Different kinds of grown and growing fish across both exhibits (fry don't count).</summary>
        public static int AquariumKinds(StoreLayout store)
        {
            var kinds = new HashSet<string>();
            foreach (var exhibit in Exhibits(store))
                foreach (var fish in exhibit.Tank.Residents)
                    if (fish != null && !fish.IsDead && fish.Stage != LifeStage.Fry) kinds.Add(fish.Species.id);
            return kinds.Count;
        }

        /// <summary>Extra appeal for a varied aquarium as a whole: each kind past the first few, up to a limit.</summary>
        public static float KindBonus(StoreLayout store) => Mathf.Min(Data.maxKindBonus, Mathf.Max(0, AquariumKinds(store) - Data.kindsBeforeBonus) * Data.kindBonus);

        /// <summary>The whole aquarium's appeal, 0..100: the better exhibit counts 65%, the other 35%, plus a bonus for many kinds in all.</summary>
        public static float AquariumAppeal(StoreLayout store)
        {
            float best = 0f, other = 0f;
            foreach (var exhibit in Exhibits(store))
            {
                float score = ExhibitAppeal(exhibit.Tank).total;
                if (score > best)
                {
                    other = best;
                    best = score;
                }
                else if (score > other) other = score;
            }
            if (best <= 0f && other <= 0f) return 0f;
            return Mathf.Min(100f, best * 0.65f + other * 0.35f + KindBonus(store));
        }

        /// <summary>The room is bought and at least one exhibit has a living fish to see.</summary>
        public static bool CanOpen(StoreLayout store)
        {
            if (store == null || store.AquariumSection == null || !store.AquariumSection.unlocked) return false;
            foreach (var exhibit in Exhibits(store))
                foreach (var fish in exhibit.Tank.Residents)
                    if (fish != null && !fish.IsDead) return true;
            return false;
        }

        public static string WhyClosed(StoreLayout store)
        {
            if (store == null || store.AquariumSection == null || !store.AquariumSection.unlocked) return $"The {RoomName.ToLower()} isn't built yet.";
            if (Exhibits(store).Count == 0) return "No exhibits yet: buy the temperate or tropical aquarium.";
            if (!CanOpen(store)) return "The exhibits have no fish in them yet.";
            return null;
        }

        // ------------------------------------------------------------ prices and visitors

        /// <summary>What a ticket is really worth to the average visitor today: more with appeal, a little more with stars.</summary>
        public static float FairPrice(float appeal)
        {
            var d = Data;
            float price = Mathf.Lerp(d.minFairPrice, d.maxFairPrice, Mathf.Pow(Mathf.Clamp01(appeal / 100f), 1.2f));
            return price * Mathf.Lerp(d.fairPriceAtNoStars, d.fairPriceAtFiveStars, Rating);
        }

        public static float FairPrice(StoreLayout store) => FairPrice(AquariumAppeal(store));

        /// <summary>The rating's multiplier on how many people come.</summary>
        public static float RatingMultiplier => Mathf.Lerp(Data.trafficAtNoStars, Data.trafficAtFiveStars, Rating);

        public static float AppealMultiplier(float appeal) => Mathf.Lerp(Data.appealTrafficAtZero, Data.appealTrafficAtFull, Mathf.Clamp01(appeal / 100f));

        /// <summary>People heading for the aquarium each hour (whether or not they buy a ticket).</summary>
        public static float VisitorsPerHour(StoreLayout store)
        {
            if (!CanOpen(store)) return 0f;
            return Data.visitorsPerHour * AppealMultiplier(AquariumAppeal(store)) * RatingMultiplier;
        }

        /// <summary>Most visitors inside at once: more with each booth upgrade.</summary>
        public static int MaxInside
        {
            get
            {
                int most = Mathf.Max(1, Data.maxVisitorsInside);
                var upgrades = Data.boothUpgrades;
                for (int i = 0; i < BoothLevel && upgrades != null && i < upgrades.Count; i++) most += upgrades[i].extraVisitors;
                return most;
            }
        }

        /// <summary>The most this visitor would pay for a ticket today.</summary>
        public static float RollWillingness(float fair) => fair * GameRandom.Range(Data.minWillingness, Data.maxWillingness);

        /// <summary>Share of visitors who'd pay this price, when a ticket is fairly worth <paramref name="fair"/>.</summary>
        public static float BuyShare(float price, float fair)
        {
            var d = Data;
            if (fair <= 0.01f) return 0f;
            float lo = Mathf.Min(d.minWillingness, d.maxWillingness), hi = Mathf.Max(d.minWillingness, d.maxWillingness);
            if (hi - lo < 1e-4f) return price <= fair * lo ? 1f : 0f;
            return Mathf.Clamp01((hi - price / fair) / (hi - lo));
        }

        /// <summary>
        /// A visit's score, 0..1: what they saw (0..1) and value for money (what they'd have paid
        /// against what they did).
        /// </summary>
        public static float VisitScore(float sight, float willing, float paid)
        {
            float value = willing > 0.01f ? Mathf.Clamp01(0.5f + (willing - paid) / willing) : 0f;
            float share = Mathf.Clamp01(Data.sightShare);
            return Mathf.Clamp01(sight * share + value * (1f - share));
        }

        // ------------------------------------------------------------ the estimator

        /// <summary>How far off the estimator can be at the booth's level (0 once it's had every upgrade).</summary>
        public static float EstimatorRange
        {
            get
            {
                var errors = Data.estimatorError;
                if (errors == null || errors.Count == 0) return 0f;
                return Mathf.Max(0f, errors[Mathf.Clamp(BoothLevel, 0, errors.Count - 1)]);
            }
        }

        /// <summary>Today's error on the estimate: the same all day, smaller with each upgrade.</summary>
        public static float EstimateError => EstimatorRange * EstimateRoll;

        public struct Estimate
        {
            public float fairPrice, visitors, tickets, earnings;
        }

        /// <summary>
        /// What the booth guesses for a day open at this price: the fair price, people coming, tickets
        /// sold and ticket money. Every figure is off by today's error (none after three upgrades).
        /// </summary>
        public static Estimate Guess(StoreLayout store, int price)
        {
            float appeal = AquariumAppeal(store);
            if (!CanOpen(store)) return new Estimate { fairPrice = FairPrice(appeal) * (1f + EstimateError) };
            float hours = Balance.Data.shop.dayLengthSeconds / 3600f;
            float error = 1f + EstimateError;
            var e = new Estimate { fairPrice = FairPrice(appeal) * error };
            e.visitors = VisitorsPerHour(store) * hours * error;
            e.tickets = e.visitors * BuyShare(price, e.fairPrice);
            e.earnings = e.tickets * price;
            return e;
        }

        /// <summary>The price the estimator thinks brings in the most ticket money today.</summary>
        public static int BestPriceGuess(StoreLayout store)
        {
            int best = 1;
            float most = -1f;
            int top = Mathf.Max(1, Data.maxTicketPrice);
            var guess = Guess(store, 1);
            for (int p = 1; p <= top; p++)
            {
                float money = guess.visitors * BuyShare(p, guess.fairPrice) * p;
                if (money > most + 0.01f)
                {
                    most = money;
                    best = p;
                }
            }
            return best;
        }

        // ------------------------------------------------------------ the booth

        public static int BoothUpgradeCount => Data.boothUpgrades != null ? Data.boothUpgrades.Count : 0;

        /// <summary>The next upgrade for the booth, or null when it has them all.</summary>
        public static BoothUpgradeBalance NextBoothUpgrade => BoothLevel < BoothUpgradeCount ? Data.boothUpgrades[BoothLevel] : null;

        public static int BoothUpgradePrice(BoothUpgradeBalance upgrade) => upgrade == null ? 0 : Perks.Build(upgrade.price);

        /// <summary>Why the next booth upgrade can't be bought (<see cref="StoreLayout.NoMoney"/> last), or null if it can.</summary>
        public static string WhyCantUpgradeBooth()
        {
            var next = NextBoothUpgrade;
            if (next == null) return "The booth has every upgrade.";
            if (!Progression.IsUnlocked(XpTrack.Shopkeeping, next.level)) return $"Needs {Progression.LockText(XpTrack.Shopkeeping, next.level)}.";
            if (!Economy.CanAfford(BoothUpgradePrice(next))) return StoreLayout.NoMoney;
            return null;
        }

        public static bool TryUpgradeBooth(StoreLayout store)
        {
            var next = NextBoothUpgrade;
            var why = WhyCantUpgradeBooth();
            if (why != null)
            {
                GameLog.Add(why);
                return false;
            }
            if (!Economy.TrySpend(BoothUpgradePrice(next), $"the ticket booth's {next.name.ToLower()}", MoneyKind.Tanks)) return false;
            BoothLevel++;
            Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpTankUpgrade);
            if (store != null) store.RebuildAquarium();
            GameLog.Add(BoothLevel >= BoothUpgradeCount
                ? $"The ticket booth has its {next.name.ToLower()}: the estimate is spot on now."
                : $"The ticket booth has its {next.name.ToLower()}: the estimate is surer (off by up to {EstimatorRange * 100f:0}%).");
            return true;
        }

        // ------------------------------------------------------------ a visit

        public struct Look
        {
            /// <summary>0..1: how much they liked what they saw.</summary>
            public float score;
            public Shop.Mood mood;
            public bool spoiled;
            public AquariumIssue issue;
        }

        /// <summary>
        /// A visitor looks into an exhibit: the appeal is what they make of it, unless something
        /// spoils it on the spot (dead fish, murky water, sick or fighting fish, a near-empty tank).
        /// </summary>
        public static Look LookAt(Tank tank)
        {
            var appeal = ExhibitAppeal(tank);
            var look = new Look { score = appeal.total / 100f };
            var shop = Balance.Data.shop;
            if (tank.Corpses.Count > 0 && GameRandom.Chance(Mathf.Min(shop.maxSadChance, shop.sadChancePerDeadFish * tank.Corpses.Count)))
                return Spoil(look, Shop.Mood.Sad, AquariumIssue.DeadFish);
            if (appeal.dirty < 0f && GameRandom.Chance(0.85f)) return Spoil(look, Shop.Mood.Grossed, AquariumIssue.DirtyWater);
            if (appeal.sick < 0f && GameRandom.Chance(Mathf.Min(shop.maxSickChance, -appeal.sick / 30f * 1.5f))) return Spoil(look, Shop.Mood.Grossed, AquariumIssue.SickFish);
            if (appeal.fighting < 0f && GameRandom.Chance(shop.fightingUpsetChance)) return Spoil(look, Shop.Mood.Sad, AquariumIssue.Fighting);
            // An empty-looking or samey exhibit: people came to see lots of different fish.
            if (appeal.fish < 4 || appeal.kinds < 3 || appeal.samey < -0.25f * Data.sameyPenalty)
            {
                look.issue = AquariumIssue.FewFish;
                look.spoiled = GameRandom.Chance(appeal.kinds <= 1 ? 0.75f : 0.5f);
            }
            if (look.score >= Data.happyScore && GameRandom.Chance(0.75f)) look.mood = Shop.Mood.Happy;
            return look;
        }

        static Look Spoil(Look look, Shop.Mood mood, AquariumIssue issue)
        {
            look.mood = mood;
            look.spoiled = true;
            look.issue = issue;
            look.score *= 0.4f;
            return look;
        }

        // ------------------------------------------------------------ the day

        /// <summary>
        /// The day is over: the day's average visit moves the rating towards it (less on a quiet
        /// day), and its reviews are written and kept.
        /// </summary>
        public static void EndOfDay(AquariumReport report, int day)
        {
            if (report == null) return;
            ForgetAppeal();
            if (report.Judged > 0)
            {
                float weight = Data.fullDayVisitors > 0 ? Mathf.Clamp01(report.Judged / (float)Data.fullDayVisitors) : 1f;
                float keep = Mathf.Clamp(Data.ratingMemory, 0f, 0.95f);
                float before = Rating;
                Rating = Mathf.Clamp01(Rating + (report.AverageScore - Rating) * (1f - keep) * weight);
                report.ratingChange = Rating - before;
            }
            report.reviews = WriteReviews(report);
            float stars = Mathf.Round(Mathf.Lerp(1f, 5f, report.AverageScore) * 2f) / 2f;
            foreach (var text in report.reviews) reviews.Insert(0, new Review { day = day, stars = stars, text = text });
            int max = Mathf.Max(1, Data.reviewsKept);
            if (reviews.Count > max) reviews.RemoveRange(max, reviews.Count - max);
            Progression.Award(XpTrack.Shopkeeping, Mathf.RoundToInt(Balance.Data.progression.xpPerReviewStar * stars * 0.5f));
        }

        /// <summary>The day's aquarium reviews: praise or a shrug, then the most common gripes (up to three in all).</summary>
        static List<string> WriteReviews(AquariumReport report)
        {
            var result = new List<string>();
            if (report.Judged == 0) return result;
            float average = report.AverageScore;
            if (average >= Data.happyScore) result.Add(Pick(Praise, result));
            else if (average >= Data.unhappyScore) result.Add(Pick(Meh, result));
            var order = new List<int>();
            for (int i = 0; i < IssueCount; i++) if (report.issues[i] > 0) order.Add(i);
            order.Sort((a, b) => report.issues[b].CompareTo(report.issues[a]));
            foreach (var i in order)
            {
                if (result.Count >= 3) break;
                // A gripe only a few people had doesn't make the reviews on a good day.
                if (average >= Data.happyScore && report.issues[i] * 4 < report.Judged) continue;
                result.Add(Pick(IssueReviews((AquariumIssue)i), result));
            }
            if (result.Count < 3 && report.tickets > 0 && report.turnedAway * 6 < report.Judged && TicketPrice < FairPriceToday * 0.8f) result.Add(Pick(Bargain, result));
            if (result.Count == 0) result.Add(Pick(Meh, result));
            return result;
        }

        /// <summary>The fair price as it stood when the day was judged (set by <see cref="Shop.AquariumDay"/>).</summary>
        public static float FairPriceToday { get; set; }

        static string Pick(string[] options, List<string> already)
        {
            for (int tries = 0; tries < 6; tries++)
            {
                string pick = options[GameRandom.Range(0, options.Length)];
                if (!already.Contains(pick)) return pick;
            }
            return options[GameRandom.Range(0, options.Length)];
        }

        static string[] IssueReviews(AquariumIssue issue)
        {
            switch (issue)
            {
                case AquariumIssue.DeadFish: return DeadFish;
                case AquariumIssue.DirtyWater: return DirtyWater;
                case AquariumIssue.SickFish: return SickFish;
                case AquariumIssue.Fighting: return Fighting;
                case AquariumIssue.FewFish: return FewFish;
                default: return TooPricey;
            }
        }

        static readonly string[] Praise =
        {
            "Breathtaking. We sat in front of the big tank for half an hour.",
            "The kids pressed their noses to the glass the whole time. Worth every penny.",
            "Gorgeous aquariums, crystal clear water. A lovely afternoon out.",
            "I didn't expect a little seaside aquarium to be this good!",
            "So many colourful fish. Coming back with the grandparents.",
            "Peaceful, beautiful, and the fish all looked so healthy.",
            "Best thing to do in town on a rainy day.",
            "Proper wow moment when you walk in and see that wall of water.",
            "Every time I looked I spotted a fish I hadn't seen before.",
        };

        static readonly string[] Meh =
        {
            "Nice enough. A quick look and we were done.",
            "Pleasant, but there isn't much to it yet.",
            "Fine for twenty minutes. Not sure I'd come again.",
            "Some pretty fish. I hope it grows.",
            "Okay. My son liked the big orange one.",
        };

        static readonly string[] Bargain =
        {
            "Can't believe how cheap the tickets are for this!",
            "A bargain. We'll be back next weekend.",
            "Great value. They could charge more, honestly.",
        };

        static readonly string[] TooPricey =
        {
            "Way too expensive for two fish tanks. Turned around at the booth.",
            "They want HOW much to look at some fish?",
            "Nice aquarium, but the ticket price is a joke.",
            "Paid a fortune and was out again in ten minutes.",
            "Not worth the entry fee.",
        };

        static readonly string[] DeadFish =
        {
            "There was a dead fish floating right at the front of the big tank.",
            "My daughter asked why that fish was upside down. Not the day out I planned.",
            "Dead fish on display. Somebody please net them out.",
        };

        static readonly string[] DirtyWater =
        {
            "Couldn't see a thing through the green water.",
            "The glass was filthy. You pay to look, at least let us see.",
            "Murky tanks. Felt sorry for the fish.",
        };

        static readonly string[] SickFish =
        {
            "Lots of the fish had spots and ragged fins. Sad to see.",
            "The fish looked poorly. I hope someone's looking after them.",
            "Sickly fish in a public aquarium? Not great.",
        };

        static readonly string[] Fighting =
        {
            "One big bully chasing everything else round the tank.",
            "The fish kept fighting. The kids found it upsetting.",
            "More of a wrestling ring than an aquarium.",
        };

        static readonly string[] FewFish =
        {
            "Huge tank, hardly any fish in it.",
            "A lot of water and not much else.",
            "Pretty decorations, but where are the fish?",
            "Only one kind of fish in there. Bit samey.",
            "Hundreds of the same fish. Seen one, seen them all.",
            "I wanted to see lots of different fish, not two kinds over and over.",
            "Needs more variety. The aquarium across town has dozens of species.",
        };

        // ------------------------------------------------------------ saving

        internal static void Save(SaveData d)
        {
            d.aquarium = new AquariumSave
            {
                rating = Rating, ticketPrice = TicketPrice, boothLevel = BoothLevel, estimateRoll = EstimateRoll,
                today = Today, reviews = new List<ReviewSave>(), speciesLimits = new List<KeyInt>(),
            };
            foreach (var r in reviews) d.aquarium.reviews.Add(new ReviewSave { day = r.day, stars = r.stars, text = r.text });
            foreach (var pair in speciesLimits) d.aquarium.speciesLimits.Add(new KeyInt { key = pair.Key, value = pair.Value });
            d.aquarium.scuba = ScubaDives.Save();
        }

        internal static void Load(SaveData d)
        {
            var s = d.aquarium;
            reviews.Clear();
            // A save from before the aquarium: everything as a new game starts.
            if (s == null || s.ticketPrice <= 0)
            {
                Reset();
                return;
            }
            Rating = Mathf.Clamp01(s.rating);
            SetTicketPrice(s.ticketPrice);
            BoothLevel = Mathf.Clamp(s.boothLevel, 0, BoothUpgradeCount);
            EstimateRoll = Mathf.Clamp(s.estimateRoll, -1f, 1f);
            Today = s.today != null && s.today.issues != null && s.today.issues.Length == IssueCount ? s.today : null;
            if (s.reviews != null) foreach (var r in s.reviews) reviews.Add(new Review { day = r.day, stars = r.stars, text = r.text });
            speciesLimits.Clear();
            if (s.speciesLimits != null) foreach (var pair in s.speciesLimits) SetSpeciesLimit(pair.key, pair.value);
            ScubaDives.Load(s.scuba);
        }
    }

    [Serializable]
    public class AquariumSave
    {
        public float rating;
        public int ticketPrice, boothLevel;
        public float estimateRoll;
        public AquariumReport today;
        public List<ReviewSave> reviews = new List<ReviewSave>();
        /// <summary>The most of each kind the player wants in the aquarium.</summary>
        public List<KeyInt> speciesLimits = new List<KeyInt>();
        public ScubaSave scuba;
    }
}
