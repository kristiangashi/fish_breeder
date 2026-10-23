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
    }

    [Serializable]
    public class SectionBalance
    {
        public string name;
        public int price;
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
        public List<SectionBalance> sections = new List<SectionBalance>
        {
            new SectionBalance { name = "Main floor", price = 0 },
            new SectionBalance { name = "East wing", price = 2500 },
            new SectionBalance { name = "Garden room", price = 6000 },
            new SectionBalance { name = "Grand hall", price = 12000 },
        };
    }

    [Serializable]
    public class SupplierBalance
    {
        public List<FoodBalance> food = new List<FoodBalance>
        {
            new FoodBalance { type = FoodType.Flakes, name = "Flakes tub", blurb = "Cheap. Now and then a spoiled flake makes a fish sick.", price = 15, feedings = 20 },
            new FoodBalance { type = FoodType.Premium, name = "Premium pellets", blurb = "More filling and much less likely to cause illness. Some species need it.", price = 45, feedings = 20 },
        };
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
