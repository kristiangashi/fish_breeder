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
        [Tooltip("Hunger one bite takes away (hunger runs 0..1).")]
        public float nutrition = 0.35f;
        [Tooltip("Multiplier on the chance a meal makes a fish sick.")]
        public float contamination = 1f;
        [Tooltip("Chance a piece is spoiled (4x as likely to make a fish sick).")]
        [Range(0f, 1f)] public float spoilChance;
        [Tooltip("How fast it sinks, metres per second. Bottom feeders only eat food near the sand.")]
        public float sinkSpeed = 0.07f;
        public Color color = new Color(0.55f, 0.4f, 0.25f);
    }

    /// <summary>One thing research teaches about a species, and the research points it takes.</summary>
    [Serializable]
    public class ResearchFactBalance
    {
        public ResearchFact fact;
        public float points = 2f;
    }

    /// <summary>
    /// Research: the more time you spend keeping a species, the more you learn about it. Every time
    /// step each species in your tanks earns research points (times your research speed, which
    /// grows with the Research level), and facts unlock at their point totals. Adult tank mates may
    /// reveal how they get on, and feeding may reveal favourite foods.
    /// </summary>
    [Serializable]
    public class ResearchBalance
    {
        [Tooltip("Research points each species you keep earns per time step, at research speed 1.")]
        public float pointsPerStep = 1f;
        [Tooltip("Research speed = 1 + this x (Research level - 1).")]
        public float speedPerLevel = 0.2f;
        public List<ResearchFactBalance> facts = new List<ResearchFactBalance>
        {
            new ResearchFactBalance { fact = ResearchFact.Temperature, points = 2f },
            new ResearchFactBalance { fact = ResearchFact.Plants, points = 3f },
            new ResearchFactBalance { fact = ResearchFact.School, points = 4f },
            new ResearchFactBalance { fact = ResearchFact.Ph, points = 6f },
            new ResearchFactBalance { fact = ResearchFact.Breeding, points = 8f },
            new ResearchFactBalance { fact = ResearchFact.Lifespan, points = 12f },
        };
        [Tooltip("Chance per time step, at research speed 1, that two kinds of adult fish sharing a tank show how they get on.")]
        [Range(0f, 1f)] public float relationshipChance = 0.08f;
        [Tooltip("Chance, at research speed 1, that a fish eating one of its favourite foods shows it's a favourite.")]
        [Range(0f, 1f)] public float favouriteFoodChance = 0.15f;
        [Tooltip("Research level needed to measure pH at all.")]
        public int phReadingLevel = 2;
        [Header("Research XP")]
        public int xpPerSpeciesPerStep = 2;
        public int xpFact = 8;
        public int xpRelationship = 15;
        public int xpFavouriteFood = 10;
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
        [Tooltip("Price of the same tier as a box of fry (as many fry as eggs). Fry boxes can hold any fish, egg boxes only egg-layers.")]
        public int fryPrice = 45;
    }

    public enum EquipmentKind { Filter, Heater, Bubbler }

    /// <summary>
    /// One model of filter, heater or bubbler. Equipment wears with age: the chance of breaking at
    /// a time step starts at <see cref="breakChance"/> and grows by <see cref="breakChancePerStep"/>
    /// for every time step it has run, up to <see cref="maxBreakChance"/>. Better models do their
    /// job better and break less.
    /// </summary>
    [Serializable]
    public class EquipmentTierBalance
    {
        public string name;
        public string blurb;
        public int price = 100;
        [Tooltip("Fishkeeping level needed to buy it.")]
        public int unlockLevel = 1;
        [Tooltip("Filter: share of the waste left in the water (lower is better). Heater: how far the water can drift from the setting each time step (C). Bubbler: fish it keeps in full oxygen.")]
        public float effect = 1f;
        [Tooltip("Chance of breaking down at a time step when new.")]
        [Range(0f, 1f)] public float breakChance = 0.02f;
        [Tooltip("Added to the chance for every time step it has been running.")]
        public float breakChancePerStep = 0.004f;
        [Range(0f, 1f)] public float maxBreakChance = 0.3f;
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
        public List<string> starterDecorations = new List<string>();
        [Tooltip("A tank's fish count is a soft limit: you can go over it (with serious problems for the fish) up to this many times the limit.")]
        public float tankHardLimitMultiplier = 1.5f;
    }

    [Serializable]
    public class SupplierBalance
    {
        public List<FoodBalance> food = new List<FoodBalance>
        {
            new FoodBalance { type = FoodType.Flakes, name = "Flakes", blurb = "Cheap everyday food. Now and then a spoiled flake makes a fish sick.", price = 15, feedings = 20,
                nutrition = 0.35f, contamination = 1f, spoilChance = 0.1f, sinkSpeed = 0.06f, color = new Color(0.62f, 0.45f, 0.25f) },
            new FoodBalance { type = FoodType.AlgaeWafers, name = "Algae wafers", blurb = "Sinking wafers of algae and greens, for grazers and bottom feeders.", price = 30, feedings = 20, unlockLevel = 2,
                nutrition = 0.45f, contamination = 0.5f, spoilChance = 0.03f, sinkSpeed = 0.22f, color = new Color(0.3f, 0.48f, 0.2f) },
            new FoodBalance { type = FoodType.Premium, name = "Premium pellets", blurb = "Filling and much less likely to cause illness.", price = 45, feedings = 20, unlockLevel = 3,
                nutrition = 0.5f, contamination = 0.25f, spoilChance = 0f, sinkSpeed = 0.09f, color = new Color(0.9f, 0.45f, 0.2f) },
            new FoodBalance { type = FoodType.BrineShrimp, name = "Frozen brine shrimp", blurb = "Tiny shrimp that drift slowly down. Small fish go mad for them.", price = 35, feedings = 15, unlockLevel = 3,
                nutrition = 0.45f, contamination = 0.6f, spoilChance = 0.03f, sinkSpeed = 0.045f, color = new Color(0.95f, 0.55f, 0.5f) },
            new FoodBalance { type = FoodType.Bloodworms, name = "Frozen bloodworms", blurb = "Rich midge larvae, a treat for most fish. Frozen food carries a little more risk.", price = 40, feedings = 15, unlockLevel = 4,
                nutrition = 0.6f, contamination = 1.4f, spoilChance = 0.05f, sinkSpeed = 0.1f, color = new Color(0.6f, 0.08f, 0.1f) },
        };
        public List<EggTierBalance> eggTiers = new List<EggTierBalance>
        {
            new EggTierBalance { id = "Simple", name = "Simple eggs", blurb = "Everyday fish, mostly common ones.", price = 30, fryPrice = 45, eggs = 5, unlockLevel = 1,
                normalMin = SpeciesRarity.Common, normalMax = SpeciesRarity.Uncommon, normalRarityFalloff = 0.4f, rarePool = SpeciesRarity.Rare, rareChance = 0.05f, rareFalloff = 0.3f,
                color = new Color(1f, 0.93f, 0.75f) },
            new EggTierBalance { id = "Rare", name = "Rare eggs", blurb = "A mix of everyday and rare fish. Rare ones are much likelier than in simple eggs.", price = 120, fryPrice = 170, eggs = 5, unlockLevel = 5,
                normalMin = SpeciesRarity.Common, normalMax = SpeciesRarity.Uncommon, normalRarityFalloff = 0.7f, rarePool = SpeciesRarity.Rare, rareChance = 0.6f, rareFalloff = 0.45f,
                color = new Color(0.55f, 0.8f, 1f), speckled = true },
            new EggTierBalance { id = "UltraRare", name = "Ultra rare eggs", blurb = "Rare fish, with a chance of something exotic.", price = 400, fryPrice = 560, eggs = 4, unlockLevel = 15,
                normalMin = SpeciesRarity.Uncommon, normalMax = SpeciesRarity.Rare, normalRarityFalloff = 0.8f, rarePool = SpeciesRarity.Exotic, rareChance = 0.5f, rareFalloff = 0.4f,
                color = new Color(0.75f, 0.55f, 1f), speckled = true },
            new EggTierBalance { id = "Legendary", name = "Legendary eggs", blurb = "Coming later.", price = 1500, fryPrice = 2100, eggs = 3, unlockLevel = 30,
                normalMin = SpeciesRarity.Rare, normalMax = SpeciesRarity.Exotic, normalRarityFalloff = 0.8f, rarePool = SpeciesRarity.Legendary, rareChance = 0.45f, rareFalloff = 0.35f,
                color = new Color(1f, 0.78f, 0.25f), speckled = true },
            new EggTierBalance { id = "Ancient", name = "Ancient eggs", blurb = "Coming later.", price = 5000, fryPrice = 7000, eggs = 3, unlockLevel = 45,
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
        [Tooltip("Fishkeeping level needed for care products.")]
        public int waterConditionerLevel = 1, medicineLevel = 1, marineSaltLevel = 10, repairKitLevel = 1;
        public List<EquipmentTierBalance> filters = new List<EquipmentTierBalance>
        {
            new EquipmentTierBalance { name = "Sponge filter", blurb = "Cheap and basic.", price = 150, unlockLevel = 2, effect = 0.45f, breakChance = 0.02f, breakChancePerStep = 0.004f, maxBreakChance = 0.3f },
            new EquipmentTierBalance { name = "Hang-on-back filter", blurb = "Cleaner water, and it lasts longer.", price = 350, unlockLevel = 7, effect = 0.32f, breakChance = 0.012f, breakChancePerStep = 0.003f, maxBreakChance = 0.22f },
            new EquipmentTierBalance { name = "Canister filter", blurb = "Much cleaner water, rarely breaks.", price = 800, unlockLevel = 16, effect = 0.2f, breakChance = 0.007f, breakChancePerStep = 0.002f, maxBreakChance = 0.14f },
            new EquipmentTierBalance { name = "Sump system", blurb = "The best there is.", price = 1800, unlockLevel = 28, effect = 0.1f, breakChance = 0.003f, breakChancePerStep = 0.001f, maxBreakChance = 0.07f },
        };
        public List<EquipmentTierBalance> heaters = new List<EquipmentTierBalance>
        {
            new EquipmentTierBalance { name = "Basic heater", blurb = "The water drifts a little either side of the setting.", price = 120, unlockLevel = 1, effect = 1.5f, breakChance = 0.025f, breakChancePerStep = 0.004f, maxBreakChance = 0.3f },
            new EquipmentTierBalance { name = "Adjustable heater", blurb = "Steadier, and it lasts longer.", price = 280, unlockLevel = 6, effect = 1f, breakChance = 0.015f, breakChancePerStep = 0.003f, maxBreakChance = 0.22f },
            new EquipmentTierBalance { name = "Titanium heater", blurb = "Very steady, rarely breaks.", price = 650, unlockLevel = 14, effect = 0.5f, breakChance = 0.008f, breakChancePerStep = 0.002f, maxBreakChance = 0.14f },
            new EquipmentTierBalance { name = "Smart heater controller", blurb = "Holds the exact temperature.", price = 1500, unlockLevel = 26, effect = 0f, breakChance = 0.004f, breakChancePerStep = 0.001f, maxBreakChance = 0.07f },
        };
        public List<EquipmentTierBalance> bubblers = new List<EquipmentTierBalance>
        {
            new EquipmentTierBalance { name = "Air stone", blurb = "Enough air for a small group.", price = 80, unlockLevel = 1, effect = 14f, breakChance = 0.02f, breakChancePerStep = 0.004f, maxBreakChance = 0.3f },
            new EquipmentTierBalance { name = "Twin-outlet air pump", blurb = "Air for a busy tank.", price = 220, unlockLevel = 5, effect = 22f, breakChance = 0.012f, breakChancePerStep = 0.003f, maxBreakChance = 0.22f },
            new EquipmentTierBalance { name = "Silent air pump", blurb = "Air for a crowded tank, rarely breaks.", price = 520, unlockLevel = 12, effect = 34f, breakChance = 0.007f, breakChancePerStep = 0.002f, maxBreakChance = 0.14f },
            new EquipmentTierBalance { name = "Oxygen reactor", blurb = "Air for any tank.", price = 1300, unlockLevel = 24, effect = 60f, breakChance = 0.003f, breakChancePerStep = 0.001f, maxBreakChance = 0.07f },
        };
        [Tooltip("A pack of repair kits; one kit fixes one broken piece of equipment.")]
        public int repairKitPrice = 30;
        public int repairKitsPerPack = 3;
        [Tooltip("Share of the equipment's wear (age) a repair takes off. Old equipment keeps breaking more often until replaced.")]
        [Range(0f, 1f)] public float repairWearRemoved = 0.25f;
        [Tooltip("What a bought fry is worth while it's still too small to tell what it is.")]
        public int unknownFryValue = 4;
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
        [Tooltip("Chance a shopper is put off by a tank, for each dead fish in it (up to the max).")]
        [Range(0f, 1f)] public float sadChancePerDeadFish = 0.3f;
        [Range(0f, 1f)] public float maxSadChance = 0.9f;
        [Tooltip("Purchase chance is multiplied by this in a For Sale tank with dead fish in it.")]
        [Range(0f, 1f)] public float deadFishPurchaseMultiplier = 0.6f;
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
        [Tooltip("Wipes each tank gets per closed-shop session at Fishkeeping level 1.")]
        public int wipesPerSession = 1;
        [Tooltip("Each tank gets one more wipe per session every this many Fishkeeping levels (0 = never).")]
        public int extraWipeEveryFishkeepingLevels = 8;
        [Tooltip("Extra water quality lost per time step for each fish over the tank's limit.")]
        public float overcrowdedWastePerFish = 0.02f;
        [Tooltip("Illness chances go up by this share for each fish over the tank's limit.")]
        public float overcrowdedIllnessPerFish = 0.1f;
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
        [Tooltip("Water stress per degree outside a fish's temperature range (1 = as bad as it gets).")]
        public float tempPenaltyPerDegree = 0.2f;
        [Tooltip("Water stress per pH unit outside the fish's range.")]
        public float phPenaltyPerUnit = 0.5f;
        [Tooltip("Water stress per ppt of salt outside the fish's range.")]
        public float saltPenaltyPerPpt = 0.08f;
        public float oldAgeWeight = 0.5f;
        public float perDeadTankmate = 0.08f;
        public float tooFewSchoolmates = 0.2f;
        public float plantsBonus = 0.15f;
        public int idealDecorMin = 3, idealDecorMax = 10;
        public float pestsComfortLoss = 0.25f;
        [Tooltip("Health every fish in a tank loses for each fish over the tank's limit.")]
        public float overcrowdedHealthPerFish = 0.05f;
        [Range(0f, 1f)] public float overcrowdedMaxPenalty = 0.6f;
        [Tooltip("Health only changes at time steps, moving towards what the fish's conditions call for by at most this much per step.")]
        [Range(0f, 1f)] public float healthChangePerStep = 0.35f;
        [Tooltip("A favourite food fills a fish this many times as much.")]
        public float favouriteFoodNutrition = 1.4f;
        [Tooltip("Comfort a fish gets for a time step after a meal of one of its favourite foods.")]
        public float favouriteFoodComfort = 0.1f;
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
        public int xpRepairEquipment = 2;
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

    /// <summary>What the relationships between tank mates do (the table itself is <see cref="BalanceData.relationships"/>).</summary>
    [Serializable]
    public class TankMateBalance
    {
        [Tooltip("A predator hunts at every time step; chance it catches and eats prey that fits in its mouth.")]
        [Range(0f, 1f)] public float eatChance = 0.35f;
        [Tooltip("Prey up to this share of the predator's length fits in its mouth.")]
        [Range(0f, 1f)] public float preyMaxSizeRatio = 0.6f;
        [Tooltip("Chance a hunt that doesn't end in a meal still wounds the prey.")]
        [Range(0f, 1f)] public float biteChance = 0.5f;
        public float biteInjury = 0.3f;
        [Tooltip("Comfort lost by prey living with a predator of theirs.")]
        public float predatorFear = 0.2f;
        [Tooltip("Comfort lost for each kind of fish that nips it or that it fights.")]
        public float competitiveStress = 0.12f;
        [Tooltip("Nipping only draws blood when the tank holds more than this share of its capacity.")]
        [Range(0f, 1f)] public float crowdedAbove = 0.7f;
        [Tooltip("Chance per time step that a fish with a competitive tank mate gets nipped in a crowded tank.")]
        [Range(0f, 1f)] public float nipChance = 0.35f;
        public float nipInjury = 0.15f;
        [Tooltip("Comfort gained for each kind of preferred tank mate (counted up to 2 kinds).")]
        public float preferredComfort = 0.1f;
        [Tooltip("Health gained for each kind of preferred tank mate (counted up to 2 kinds).")]
        public float preferredHealth = 0.04f;
        [Tooltip("Wounds heal by this much per time step (wounds run 0..1).")]
        public float injuryHealPerStep = 0.1f;
        [Tooltip("Health a fish loses at full wounds.")]
        public float injuryWeight = 0.8f;
    }

    /// <summary>
    /// Medicine goes into the whole tank. It cures fish with the illness it treats; every other fish
    /// in the water is stressed by it, and too many doses close together damage a fish for life.
    /// </summary>
    [Serializable]
    public class MedicineBalance
    {
        [Tooltip("Comfort lost by a fish that got medicine it didn't need (0..1).")]
        public float stressPerDose = 0.3f;
        public float stressRecoveryPerStep = 0.15f;
        [Tooltip("Medicine build-up from one dose in a fish that didn't need it.")]
        public float loadPerDose = 1f;
        [Tooltip("Build-up in a fish the dose was meant for.")]
        public float loadPerNeededDose = 0.5f;
        public float loadClearedPerStep = 0.35f;
        [Tooltip("At this much build-up the fish is overdosed and damaged for life.")]
        public float overdoseLoad = 3f;
        [Tooltip("Health lost for good per overdose.")]
        public float damageHealth = 0.08f;
        [Tooltip("Value lost per overdose (0.2 = 20%).")]
        [Range(0f, 1f)] public float damageValue = 0.2f;
        public int maxDamage = 3;
        [Tooltip("After this many overdoses the fish can't breed any more.")]
        public int infertileAtDamage = 2;
    }

    [Serializable]
    public class BalanceData
    {
        /// <summary>Which round of default changes this file has been through (see <see cref="Balance.Upgrade"/>).</summary>
        public int dataVersion = Balance.CurrentDataVersion;
        public List<Species> species = Species.CreateDefaults();
        public List<RelationshipBalance> relationships = Relationships.CreateDefaults();
        public List<DiseaseDefinition> diseases = DiseaseDefinition.CreateDefaults();
        public GameConfig rules = new GameConfig();
        public TimeStepBalance timeStep = new TimeStepBalance();
        public HealthBalance health = new HealthBalance();
        public ShopBalance shop = new ShopBalance();
        public StoreBalance store = new StoreBalance();
        public SupplierBalance supplier = new SupplierBalance();
        public ProgressionBalance progression = new ProgressionBalance();
        public NotificationBalance notifications = new NotificationBalance();
        public TankMateBalance tankMates = new TankMateBalance();
        public MedicineBalance medicine = new MedicineBalance();
        public ResearchBalance research = new ResearchBalance();
    }

    /// <summary>
    /// All balancing numbers, loaded from Resources/Balance/balance.json (written by the balance
    /// console, see Docs/BalanceConsole.md). Without that file the defaults in code are used.
    /// </summary>
    public static class Balance
    {
        public const string ResourcePath = "Balance/balance";
        /// <summary>
        /// 2: real-world species data (names, families, temperature and pH ranges, schools).
        /// 3: favourite foods and new foods, no starting decorations, one wipe per tank.
        /// </summary>
        public const int CurrentDataVersion = 3;
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
                Upgrade(parsed, ReadDataVersion(json));
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

        static int ReadDataVersion(string json)
        {
            var match = System.Text.RegularExpressions.Regex.Match(json, "\"dataVersion\"\\s*:\\s*(\\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 1;
        }

        /// <summary>
        /// Brings a file saved before a change to the defaults up to date, once: version 2 replaces
        /// the made-up species data with real-world names, families, categories, temperature, pH
        /// and salt ranges and school sizes, and sets the new fry box prices.
        /// </summary>
        static void Upgrade(BalanceData data, int fromVersion)
        {
            if (fromVersion < 2)
            {
                var defaults = new Dictionary<string, Species>();
                foreach (var s in Species.CreateDefaults()) defaults[s.id] = s;
                foreach (var s in data.species)
                {
                    if (s == null || s.id == null || !defaults.TryGetValue(s.id, out var d)) continue;
                    if (s.id == "FancyGoldfish" && s.displayName == "Fancy Goldfish") s.displayName = d.displayName;
                    s.scientificName = d.scientificName;
                    s.family = d.family;
                    s.category = d.category;
                    s.tempMin = d.tempMin; s.tempMax = d.tempMax;
                    s.phMin = d.phMin; s.phMax = d.phMax;
                    s.salinityMin = d.salinityMin; s.salinityMax = d.salinityMax;
                    s.schoolSize = d.schoolSize;
                    s.likesPlants = d.likesPlants;
                }
                var tiers = new SupplierBalance().eggTiers;
                if (data.supplier != null && data.supplier.eggTiers != null)
                    foreach (var tier in data.supplier.eggTiers)
                        foreach (var d in tiers)
                            if (tier != null && tier.id == d.id) tier.fryPrice = d.fryPrice;
            }
            if (fromVersion < 3)
            {
                var defaults = new Dictionary<string, Species>();
                foreach (var s in Species.CreateDefaults()) defaults[s.id] = s;
                foreach (var s in data.species)
                    if (s != null && s.id != null && defaults.TryGetValue(s.id, out var d)) s.favoriteFoods = new List<FoodType>(d.favoriteFoods);
                var fresh = new SupplierBalance();
                if (data.supplier != null)
                {
                    // The food list gains new foods and per-food numbers; take the new list, keeping prices you set.
                    var food = new List<FoodBalance>(fresh.food);
                    if (data.supplier.food != null)
                        foreach (var old in data.supplier.food)
                            foreach (var f in food)
                                if (old != null && old.type == f.type) { f.price = old.price; f.feedings = old.feedings; f.unlockLevel = old.unlockLevel; }
                    data.supplier.food = food;
                }
                if (data.store != null) data.store.starterDecorations = new List<string>();
                if (data.timeStep != null) data.timeStep.wipesPerSession = 1;
            }
            data.dataVersion = CurrentDataVersion;
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
