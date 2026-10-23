using System;
using System.Collections.Generic;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.Health;
using UnityEngine;

namespace FishBreeder.Core
{
    [Serializable]
    public class FoodBalance
    {
        public FoodType type;
        public string name;
        public string blurb;
        public int price;
        public int feedings;
        [Tooltip("Fishkeeping level needed to buy it.")]
        public int unlockLevel = 1;
    }

    [Serializable]
    public class SectionBalance
    {
        public string name;
        public int price;
        [Tooltip("Shopkeeping level needed to unlock it.")]
        public int unlockLevel = 1;
    }

    /// <summary>A size a tank can be, from the one every tank starts as to the biggest upgrade.</summary>
    [Serializable]
    public class TankSizeBalance
    {
        public string name;
        [Tooltip("Inside of the glass, in metres.")]
        public float width = 1.6f, height = 0.9f, depth = 0.7f;
        [Tooltip("Most fish it holds.")]
        public int capacity = 24;
        [Tooltip("Cost to upgrade a tank to this size (the first size is what every tank starts as).")]
        public int upgradePrice;
        [Tooltip("Fishkeeping level needed.")]
        public int unlockLevel = 1;
        [Tooltip("Decorations it has room for.")]
        public int decorationSlots = 12;

        public Vector3 Size => new Vector3(width, height, depth);
    }

    /// <summary>
    /// A box of eggs from the supplier. Each egg hatches a fish from the normal pool or, with a
    /// chance that shrinks for every rare egg already in the box, from the rare pool. So one rare
    /// fish in a box is far likelier than two, and two far likelier than three.
    /// </summary>
    [Serializable]
    public class EggTierBalance
    {
        public string id;
        public string name;
        public string blurb;
        public int price = 30;
        public int eggs = 5;
        [Tooltip("Fishkeeping level needed to buy it.")]
        public int unlockLevel = 1;
        [Tooltip("Rarest and commonest species of the normal pool.")]
        public SpeciesRarity normalMin, normalMax = SpeciesRarity.Uncommon;
        [Tooltip("In the normal pool each step up in rarity is this much less likely (1 = all equal).")]
        [Range(0f, 1f)] public float normalRarityFalloff = 0.4f;
        [Tooltip("Rarity of the rare pool.")]
        public SpeciesRarity rarePool = SpeciesRarity.Rare;
        [Tooltip("Chance the first egg is rare.")]
        [Range(0f, 1f)] public float rareChance = 0.05f;
        [Tooltip("The chance is multiplied by this for every rare egg already in the box.")]
        [Range(0f, 1f)] public float rareFalloff = 0.35f;
        public Color color = new Color(1f, 0.93f, 0.75f);
        public bool speckled;
    }

    /// <summary>A decoration the supplier sells. The id is the model's name in Resources/Decor.</summary>
    [Serializable]
    public class DecorationBalance
    {
        public string id;
        public string name;
        public int price = 25;
        [Tooltip("Shopkeeping level needed to buy it.")]
        public int unlockLevel = 1;
        [Tooltip("Counts as a plant: some fish love plants.")]
        public bool plant;
        [Tooltip("Height in a standard tank (metres). Bigger tanks get bigger pieces.")]
        public float height = 0.3f;
        [Tooltip("Goes along the back of the tank (tall pieces) rather than the front.")]
        public bool back;
    }

    [Serializable]
    public class StoreBalance
    {
        public int startingMoney = 1000;
        [Tooltip("If money is below this when the shop closes, you get topped up to it.")]
        public int dailyAllowance = 100;
        public int starterFlakeFeedings = 10;
        public int breederTankPrice = 350, forSaleTankPrice = 250, displayTankPrice = 500, quarantineTankPrice = 200;
        public int maxBreederTanks = 3, maxForSaleTanks = 6, maxDisplayTanks = 6, maxQuarantineTanks = 1;
        [Tooltip("Shopkeeping level to buy another tank of this kind (for sale and display tanks sit on the shop floor).")]
        public int forSaleTankLevel = 1, displayTankLevel = 2;
        [Tooltip("Fishkeeping level to buy another tank of this kind (they live in the back room).")]
        public int breederTankLevel = 4, quarantineTankLevel = 3;
        public List<SectionBalance> sections = new List<SectionBalance>
        {
            new SectionBalance { name = "Main floor", price = 0, unlockLevel = 1 },
            new SectionBalance { name = "East wing", price = 2500, unlockLevel = 4 },
            new SectionBalance { name = "Garden room", price = 6000, unlockLevel = 10 },
            new SectionBalance { name = "Grand hall", price = 12000, unlockLevel = 18 },
        };
        public List<TankSizeBalance> tankSizes = new List<TankSizeBalance>
        {
            new TankSizeBalance { name = "Standard", width = 1.6f, height = 0.9f, depth = 0.7f, capacity = 24, upgradePrice = 0, unlockLevel = 1, decorationSlots = 12 },
            new TankSizeBalance { name = "Large", width = 2.3f, height = 1f, depth = 0.85f, capacity = 36, upgradePrice = 600, unlockLevel = 6, decorationSlots = 16 },
            new TankSizeBalance { name = "Grand", width = 3f, height = 1.1f, depth = 1f, capacity = 50, upgradePrice = 1500, unlockLevel = 14, decorationSlots = 20 },
        };
        [Tooltip("Decoration ids the three starting tanks come with.")]
        public List<string> starterDecorations = new List<string> { "Rock", "Plant", "Driftwood", "Seaweed" };
    }

    [Serializable]
    public class SupplierBalance
    {
        public List<FoodBalance> food = new List<FoodBalance>
        {
            new FoodBalance { type = FoodType.Flakes, name = "Flakes tub", blurb = "Cheap. Now and then a spoiled flake makes a fish sick.", price = 15, feedings = 20 },
            new FoodBalance { type = FoodType.Premium, name = "Premium pellets", blurb = "More filling and much less likely to cause illness. Some species need it.", price = 45, feedings = 20, unlockLevel = 3 },
        };
        public List<EggTierBalance> eggTiers = new List<EggTierBalance>
        {
            new EggTierBalance { id = "Simple", name = "Simple eggs", blurb = "Everyday fish, mostly common ones.", price = 30, eggs = 5, unlockLevel = 1,
                normalMin = SpeciesRarity.Common, normalMax = SpeciesRarity.Uncommon, normalRarityFalloff = 0.4f, rarePool = SpeciesRarity.Rare, rareChance = 0.05f, rareFalloff = 0.3f,
                color = new Color(1f, 0.93f, 0.75f) },
            new EggTierBalance { id = "Rare", name = "Rare eggs", blurb = "A mix of everyday and rare fish. Rare ones are much likelier than in simple eggs.", price = 120, eggs = 5, unlockLevel = 5,
                normalMin = SpeciesRarity.Common, normalMax = SpeciesRarity.Uncommon, normalRarityFalloff = 0.7f, rarePool = SpeciesRarity.Rare, rareChance = 0.6f, rareFalloff = 0.45f,
                color = new Color(0.55f, 0.8f, 1f), speckled = true },
            new EggTierBalance { id = "UltraRare", name = "Ultra rare eggs", blurb = "Rare fish, with a chance of something exotic.", price = 400, eggs = 4, unlockLevel = 15,
                normalMin = SpeciesRarity.Uncommon, normalMax = SpeciesRarity.Rare, normalRarityFalloff = 0.8f, rarePool = SpeciesRarity.Exotic, rareChance = 0.5f, rareFalloff = 0.4f,
                color = new Color(0.75f, 0.55f, 1f), speckled = true },
            new EggTierBalance { id = "Legendary", name = "Legendary eggs", blurb = "Coming later.", price = 1500, eggs = 3, unlockLevel = 30,
                normalMin = SpeciesRarity.Rare, normalMax = SpeciesRarity.Exotic, normalRarityFalloff = 0.8f, rarePool = SpeciesRarity.Legendary, rareChance = 0.45f, rareFalloff = 0.35f,
                color = new Color(1f, 0.78f, 0.25f), speckled = true },
            new EggTierBalance { id = "Ancient", name = "Ancient eggs", blurb = "Coming later.", price = 5000, eggs = 3, unlockLevel = 45,
                normalMin = SpeciesRarity.Exotic, normalMax = SpeciesRarity.Legendary, normalRarityFalloff = 0.8f, rarePool = SpeciesRarity.Ancient, rareChance = 0.4f, rareFalloff = 0.3f,
                color = new Color(0.45f, 0.75f, 0.55f), speckled = true },
        };
        public List<DecorationBalance> decorations = new List<DecorationBalance>
        {
            new DecorationBalance { id = "Rock", name = "Rock", price = 15, unlockLevel = 1, height = 0.2f },
            new DecorationBalance { id = "Plant", name = "Leafy plant", price = 20, unlockLevel = 1, plant = true, height = 0.5f, back = true },
            new DecorationBalance { id = "Seaweed", name = "Seaweed", price = 20, unlockLevel = 1, plant = true, height = 0.4f },
            new DecorationBalance { id = "Driftwood", name = "Driftwood", price = 35, unlockLevel = 2, height = 0.3f, back = true },
            new DecorationBalance { id = "TallPlant", name = "Tall plant", price = 30, unlockLevel = 3, plant = true, height = 0.55f, back = true },
            new DecorationBalance { id = "FernRock", name = "Fern on a rock", price = 40, unlockLevel = 4, plant = true, height = 0.16f },
            new DecorationBalance { id = "Clam", name = "Clam shell", price = 45, unlockLevel = 5, height = 0.06f },
            new DecorationBalance { id = "TreasureChest", name = "Treasure chest", price = 80, unlockLevel = 7, height = 0.14f },
            new DecorationBalance { id = "Diver", name = "Deep-sea diver", price = 120, unlockLevel = 9, height = 0.32f },
        };
        [Tooltip("Share of a decoration's price you get back when you take it out of a tank.")]
        [Range(0f, 1f)] public float decorationSellBack = 0.5f;
        [Tooltip("Fishkeeping level needed for each piece of equipment and care.")]
        public int filterLevel = 2, heaterLevel = 1, bubblerLevel = 1, waterConditionerLevel = 1, medicineLevel = 1, marineSaltLevel = 10;
        public int filterPrice = 150;
        [Tooltip("Waste build-up in a tank with a filter, relative to one without.")]
        [Range(0f, 1f)] public float filterWasteMultiplier = 0.4f;
        public int heaterPrice = 120;
        public int bubblerPrice = 80;
        public int waterConditionerPrice = 20;
        public int waterConditionerDoses = 5;
        [Tooltip("Cost to turn a tank to salt water (or back).")]
        public int marineSaltPrice = 40;
        public int medicinePrice = 15;
    }

    [Serializable]
    public class ShopBalance
    {
        [Tooltip("Real seconds the shop stays open (3600 = one hour).")]
        public float dayLengthSeconds = 3600f;
        public float customersPerHour = 40f;
        [Tooltip("Extra traffic per display tank (0.15 = +15%).")]
        public float trafficPerDisplayTank = 0.15f;
        public float trafficPerOtherTank = 0.05f;
        [Tooltip("Extra traffic per decoration in a tank customers can see (breeder and quarantine tanks are in the back room).")]
        public float trafficPerDecoration = 0.01f;
        public int maxCustomersInStore = 8;
        public int minTankVisits = 2, maxTankVisits = 5;
        public float minLookSeconds = 4f, maxLookSeconds = 9f;
        public float walkSpeed = 1.2f;
        [Tooltip("Chance a happy shopper at a For Sale tank buys a fish they like.")]
        [Range(0f, 1f)] public float purchaseChance = 0.35f;
        [Tooltip("Shoppers pay up to fish value times a random number between these.")]
        public float minPriceTolerance = 0.95f, maxPriceTolerance = 1.35f;
        [Tooltip("Water quality below this grosses shoppers out.")]
        [Range(0f, 1f)] public float grossWaterQuality = 0.45f;
        [Range(0f, 1f)] public float grossAlgae = 0.6f;
        [Tooltip("Reputation (0..1) the shop starts with. Reviews move it up or down each day.")]
        [Range(0f, 1f)] public float startingReputation = 0.5f;
        public float reputationChangePerDay = 0.15f;
    }

    /// <summary>Everything that happens at each time step (shop opening and closing).</summary>
    [Serializable]
    public class TimeStepBalance
    {
        [Tooltip("Chance per compatible pair per time step of a pregnancy or a clutch of eggs, in a breeder tank.")]
        [Range(0f, 1f)] public float pregnancyChanceBreederTank = 0.6f;
        [Range(0f, 1f)] public float pregnancyChanceOtherTank = 0.1f;
        [Tooltip("Fish this stressed by their water (wrong temperature, pH, salt, dirt or low oxygen; 0..1) won't breed.")]
        [Range(0f, 1f)] public float maxStressToBreed = 0.35f;
        [Tooltip("Chance per egg per time step of dying in filthy water; falls to nothing at half water quality.")]
        [Range(0f, 1f)] public float eggLossInFilthyWater = 0.5f;
        [Tooltip("Algae each algae eater grazes off per time step.")]
        public float algaeEatenPerGrazer = 0.03f;
        [Tooltip("Chance per fish of catching something, before water, stress and hardiness.")]
        [Range(0f, 1f)] public float diseaseChance = 0.03f;
        public float algaePerStep = 0.06f;
        [Tooltip("Extra algae growth per decoration piece (too many decorations speed algae).")]
        public float algaePerDecoration = 0.01f;
        public float waterQualityLossPerStep = 0.04f;
        public float wasteQualityLossPerFish = 0.01f;
        public float corpseQualityLoss = 0.05f;
        [Tooltip("pH drift towards acidic per step in dirty water.")]
        public float phDriftPerStep = 0.15f;
        [Range(0f, 1f)] public float pestChance = 0.08f;
        [Tooltip("No death roll while health is at or above this.")]
        [Range(0f, 1f)] public float deathHealthThreshold = 0.5f;
        [Range(0f, 1f)] public float deathChanceAtZeroHealth = 0.7f;
        [Tooltip("Tank wipes allowed per closed-shop session.")]
        public int wipesPerSession = 3;
        public float wipeAlgaeRemoved = 0.35f;
        public float wipeQualityRestored = 0.25f;
    }

    /// <summary>How much each factor can take off a fish's health (1 = all of it).</summary>
    [Serializable]
    public class HealthBalance
    {
        public float hungerWeight = 0.6f;
        public float illnessWeight = 0.7f;
        public float comfortWeight = 0.35f;
        public float environmentWeight = 0.6f;
        public float oldAgeWeight = 0.5f;
        public float perDeadTankmate = 0.08f;
        public float perIncompatibleTankmateSpecies = 0.25f;
        public float tooFewSchoolmates = 0.2f;
        public float plantsBonus = 0.15f;
        public int idealDecorMin = 3, idealDecorMax = 10;
        public float pestsComfortLoss = 0.25f;
    }

    /// <summary>
    /// Experience and levels. Two tracks, Fishkeeping (fish care) and Shopkeeping (running the
    /// shop), each up to <see cref="maxLevel"/>. Harder things give more XP.
    /// </summary>
    [Serializable]
    public class ProgressionBalance
    {
        public int maxLevel = 60;
        [Tooltip("XP to go from level 1 to 2.")]
        public float xpForLevel2 = 100f;
        [Tooltip("Each level needs this many times the XP of the one before.")]
        public float xpGrowthPerLevel = 1.08f;

        [Header("Fishkeeping XP")]
        public int xpFeedTank = 2;
        public int xpWipeTank = 1;
        public int xpTreatWater = 2;
        public int xpRemoveDeadFish = 1;
        public int xpCureFish = 10;
        public int xpEggHatched = 3;
        [Tooltip("Pregnancy or clutch of eggs from a pair in your tanks.")]
        public int xpBreedingStarted = 10;
        [Tooltip("Each fry born or hatched from your own breeding.")]
        public int xpFryBorn = 4;
        [Tooltip("Bred fish XP is multiplied by 1 + this for each step of species rarity above Common.")]
        public float xpRarityMultiplier = 0.75f;
        [Tooltip("Extra XP for a bred fry with uncommon, rare or exotic looks.")]
        public int xpUncommonLooks = 3, xpRareLooks = 8, xpExoticLooks = 20;

        [Header("Shopkeeping XP")]
        public int xpSaleBase = 6;
        public float xpPerSaleDollar = 0.2f;
        public int xpHappyCustomer = 1;
        [Tooltip("At closing, per review star.")]
        public int xpPerReviewStar = 4;
        public int xpBuyTank = 20;
        public int xpTankUpgrade = 30;
        public int xpUnlockSection = 60;
        public int xpBuyDecoration = 4;
        public int xpBuyEquipment = 6;

        [Header("Daily goals")]
        [Tooltip("XP for finishing a daily goal, plus the per-level amount below.")]
        public int dailyGoalXp = 100;
        public float dailyGoalXpPerLevel = 12f;
        [Tooltip("Goal targets grow by this share per level above 1.")]
        public float goalTargetGrowthPerLevel = 0.08f;
        public List<DailyGoalBalance> dailyGoals = new List<DailyGoalBalance>
        {
            new DailyGoalBalance { kind = GoalKind.BreedFish, track = XpTrack.Fishkeeping, text = "Breed {0} fish", baseTarget = 5 },
            new DailyGoalBalance { kind = GoalKind.HatchEggs, track = XpTrack.Fishkeeping, text = "Hatch {0} eggs", baseTarget = 5 },
            new DailyGoalBalance { kind = GoalKind.FeedTanks, track = XpTrack.Fishkeeping, text = "Feed {0} tanks", baseTarget = 4 },
            new DailyGoalBalance { kind = GoalKind.CleanTanks, track = XpTrack.Fishkeeping, text = "Wipe tanks {0} times", baseTarget = 4 },
            new DailyGoalBalance { kind = GoalKind.SellFish, track = XpTrack.Shopkeeping, text = "Sell {0} fish", baseTarget = 5 },
            new DailyGoalBalance { kind = GoalKind.HappyCustomers, track = XpTrack.Shopkeeping, text = "Make {0} customers happy", baseTarget = 12 },
            new DailyGoalBalance { kind = GoalKind.EarnMoney, track = XpTrack.Shopkeeping, text = "Earn ${0} from sales", baseTarget = 150 },
            new DailyGoalBalance { kind = GoalKind.BuyDecorations, track = XpTrack.Shopkeeping, text = "Buy {0} decorations", baseTarget = 2 },
        };

        [Tooltip("Each Fishkeeping level makes a tank wipe this much more effective (0.02 = +2%).")]
        public float wipeBonusPerFishkeepingLevel = 0.02f;
    }

    [Serializable]
    public class DailyGoalBalance
    {
        public GoalKind kind;
        public XpTrack track;
        [Tooltip("{0} is replaced by the target.")]
        public string text;
        public int baseTarget = 5;
    }

    /// <summary>The to-do list you go through after closing the shop.</summary>
    [Serializable]
    public class NotificationBalance
    {
        [Tooltip("Go straight to the first problem after reading the day report.")]
        public bool startAutomatically = true;
        [Tooltip("Seconds to wait after a problem is dealt with before going to the next one (negative = only with the Next button).")]
        public float autoAdvanceSeconds = 1.2f;
        [Tooltip("A tank is 'dirty' below this water health (0..1).")]
        [Range(0f, 1f)] public float dirtyWaterBelow = 0.6f;
        [Range(0f, 1f)] public float algaeAbove = 0.45f;
        [Tooltip("Fish are 'hungry' at this hunger (0..1).")]
        [Range(0f, 1f)] public float hungryAbove = 0.35f;
    }

    [Serializable]
    public class BalanceData
    {
        public List<Species> species = Species.CreateDefaults();
        public List<DiseaseDefinition> diseases = DiseaseDefinition.CreateDefaults();
        public GameConfig rules = new GameConfig();
        public TimeStepBalance timeStep = new TimeStepBalance();
        public HealthBalance health = new HealthBalance();
        public ShopBalance shop = new ShopBalance();
        public StoreBalance store = new StoreBalance();
        public SupplierBalance supplier = new SupplierBalance();
        public ProgressionBalance progression = new ProgressionBalance();
        public NotificationBalance notifications = new NotificationBalance();
    }

    /// <summary>
    /// All balancing numbers, loaded from Resources/Balance/balance.json (written by the balance
    /// console, see Docs/BalanceConsole.md). Without that file the defaults in code are used.
    /// </summary>
    public static class Balance
    {
        public const string ResourcePath = "Balance/balance";
        public const string AssetPath = "Assets/Resources/Balance/balance.json";

        static BalanceData data;

        /// <summary>Raised after new values are applied while the game is running.</summary>
        public static event Action Changed;

        public static BalanceData Data
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        public static void Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            data = asset != null ? Parse(asset.text) : null;
            if (data == null) data = new BalanceData();
        }

        /// <summary>Parses balance JSON; missing fields keep their defaults. Null if the JSON is broken.</summary>
        public static BalanceData Parse(string json)
        {
            try
            {
                var parsed = JsonUtility.FromJson<BalanceData>(json);
                if (parsed == null) return null;
                if (parsed.species == null) parsed.species = new List<Species>();
                AddMissingSpecies(parsed.species);
                return parsed;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FishBreeder] Balance file could not be read, using defaults: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Species added to the game after the balance file was saved join it with their default
        /// values, so new fish show up without losing anything edited in the console.
        /// </summary>
        static void AddMissingSpecies(List<Species> species)
        {
            var known = new HashSet<string>();
            foreach (var s in species) if (s != null && !string.IsNullOrEmpty(s.id)) known.Add(s.id);
            foreach (var s in Species.CreateDefaults())
                if (!known.Contains(s.id)) species.Add(s);
        }

        /// <summary>Applies new balance JSON right away (the console calls this while playing).</summary>
        public static bool Apply(string json)
        {
            var parsed = Parse(json);
            if (parsed == null) return false;
            data = parsed;
            Changed?.Invoke();
            return true;
        }

        public static string DefaultJson() => JsonUtility.ToJson(new BalanceData(), true);
    }
}
