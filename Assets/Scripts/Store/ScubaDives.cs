using System;
using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>One upgrade for the fish seller's diving gear: each makes rare finds likelier.</summary>
    [Serializable]
    public class ScubaGearBalance
    {
        public string name;
        public string blurb;
        public int price;
        [Tooltip("Fishkeeping level needed.")]
        public int level = 1;
        [Tooltip("How much likelier rare finds get: a find's chance is multiplied by 1 + this x its rarity step / 5 (Common is step 0, Ancient step 5).")]
        public float rareBoost = 0.5f;

        public static List<ScubaGearBalance> CreateDefaults() => new List<ScubaGearBalance>
        {
            new ScubaGearBalance { name = "His own wetsuit and tank", blurb = "No more rented gear: he can stay down longer and look further.", price = 2500, level = 14, rareBoost = 0.6f },
            new ScubaGearBalance { name = "Dive computer and torch", blurb = "Deeper dives, and a light for the caves where shy fish hide.", price = 6000, level = 20, rareBoost = 1.2f },
            new ScubaGearBalance { name = "Underwater scooter", blurb = "He covers a whole reef in one dive.", price = 12000, level = 28, rareBoost = 2f },
            new ScubaGearBalance { name = "Rebreather", blurb = "No bubbles to scare the fish: the rarest come right up to him.", price = 22000, level = 36, rareBoost = 3f },
        };
    }

    /// <summary>A fish the seller can find diving, and how rare it is in the wild.</summary>
    [Serializable]
    public class ScubaFindBalance
    {
        public string species;
        public SpeciesRarity rarity;

        /// <summary>
        /// The fish that only live in the public aquarium, by how rare they are in the wild: the
        /// tinfoil-barb-type Silver Flamefin is common in big South East Asian rivers, while a
        /// flowerhorn-type Crimson Jewel is a cichlid hybrid that hardly ever turns up wild.
        /// </summary>
        public static List<ScubaFindBalance> CreateDefaults() => new List<ScubaFindBalance>
        {
            new ScubaFindBalance { species = "SilverFlamefin", rarity = SpeciesRarity.Common },
            new ScubaFindBalance { species = "CrimsonJewelCichlid", rarity = SpeciesRarity.Legendary },
        };
    }

    /// <summary>The fish seller's dives for fish that only live in the public aquarium, and the holding tanks they wait in.</summary>
    [Serializable]
    public class ScubaBalance
    {
        [Tooltip("Fishkeeping level that opens the dives and the holding tanks.")]
        public int unlockLevel = 10;
        [Tooltip("What each dive costs.")]
        public int diveFee = 250;
        [Tooltip("Chance a find comes back as an egg rather than a fry (livebearers always come as fry).")]
        [Range(0f, 1f)] public float eggChance = 0.5f;
        [Tooltip("How likely each rarity is to be found: Common, Uncommon, Rare, Exotic, Legendary, Ancient.")]
        public List<float> rarityWeights = new List<float> { 100f, 40f, 15f, 5f, 1.5f, 0.5f };
        public List<ScubaGearBalance> gear = ScubaGearBalance.CreateDefaults();
        public List<ScubaFindBalance> finds = ScubaFindBalance.CreateDefaults();
        [Tooltip("What a holding tank costs.")]
        public int holdingTankPrice = 900;
        [Tooltip("Finds a holding tank keeps at each size (standard, large, grand).")]
        public List<int> holdingFishPerSize = new List<int> { 1, 2, 3 };
        [Tooltip("Fishkeeping XP for each find, and for letting one go back to the wild.")]
        public int xpFind = 15, xpRelease = 5;
    }
}

namespace FishBreeder.Store
{
    /// <summary>
    /// The fish seller goes scuba diving for the fish that only live in the public aquarium. The
    /// player books a dive (a fee) and he's gone until the next time step, when he comes back with
    /// one egg or fry he saved, which goes into a holding tank in the big breeding room. He won't go
    /// while every holding tank is full. Better gear makes rare finds likelier. With Keep diving on,
    /// he books himself again after each dive while there's room and money.
    /// </summary>
    public static class ScubaDives
    {
        public static ScubaBalance Data
        {
            get
            {
                var aquarium = Balance.Data.aquarium ?? (Balance.Data.aquarium = new AquariumBalance());
                return aquarium.scuba ?? (aquarium.scuba = new ScubaBalance());
            }
        }

        /// <summary>Gear upgrades bought (0 = rented gear).</summary>
        public static int GearLevel { get; private set; }
        /// <summary>He's out diving: back with a find at the next time step.</summary>
        public static bool Out { get; private set; }
        /// <summary>He goes again after each dive while there's room in a holding tank and money for the fee.</summary>
        public static bool KeepDiving { get; set; }
        /// <summary>What he brought back last time, for the seller's window.</summary>
        public static string LastFind { get; private set; } = "";

        public static bool Unlocked => Progression.IsUnlocked(XpTrack.Fishkeeping, Data.unlockLevel);
        public static int Fee => Perks.Supplier(Data.diveFee);

        public static void Reset()
        {
            GearLevel = 0;
            Out = false;
            KeepDiving = false;
            LastFind = "";
        }

        // ------------------------------------------------------------ holding room

        public static List<StoreTank> HoldingTanks(IReadOnlyList<StoreTank> tanks)
        {
            var list = new List<StoreTank>();
            if (tanks != null) foreach (var tank in tanks) if (tank.Role == TankRole.Holding) list.Add(tank);
            return list;
        }

        /// <summary>Room left in a holding tank for a find (eggs waiting count).</summary>
        public static int RoomIn(Tank tank) => Mathf.Max(0, tank.Capacity - tank.Residents.Count - EggClutch.EggsIn(tank));

        public static int RoomLeft(IReadOnlyList<StoreTank> tanks)
        {
            int room = 0;
            foreach (var tank in HoldingTanks(tanks)) room += RoomIn(tank.Tank);
            return room;
        }

        // ------------------------------------------------------------ booking

        /// <summary>Why he can't go diving now (<see cref="StoreLayout.NoMoney"/> last), or null if he can.</summary>
        public static string WhyCantDive(IReadOnlyList<StoreTank> tanks)
        {
            if (!Unlocked) return $"Needs {Progression.LockText(XpTrack.Fishkeeping, Data.unlockLevel)}.";
            if (Out) return "He's out diving already: he's back at the next time step.";
            if (HoldingTanks(tanks).Count == 0) return "Buy a holding tank in the big breeding room first: he needs somewhere to put what he finds.";
            if (RoomLeft(tanks) <= 0) return "Every holding tank is full. Move, sell or release a find first.";
            if (FindTable(GearLevel).Count == 0) return "There's nothing out there for him to find yet.";
            if (!Economy.CanAfford(Fee)) return StoreLayout.NoMoney;
            return null;
        }

        public static bool TryDive(IReadOnlyList<StoreTank> tanks, bool automatic = false)
        {
            var why = WhyCantDive(tanks);
            if (why != null)
            {
                if (!automatic) GameLog.Add(why);
                return false;
            }
            if (!Economy.TrySpend(Fee, "the fish seller's dive", MoneyKind.FishAndEggs)) return false;
            Out = true;
            GameLog.Add(automatic
                ? $"The fish seller has gone diving again ({Economy.Format(Fee)}). He's back at the next time step."
                : $"The fish seller has packed his scuba gear and gone diving ({Economy.Format(Fee)}). He's back at the next time step with whatever he finds.");
            return true;
        }

        /// <summary>Called off before he's gone (the fee comes back).</summary>
        public static void CancelDive()
        {
            if (!Out) return;
            Out = false;
            Economy.Earn(Fee, "a dive called off", MoneyKind.Refunds);
            GameLog.Add("You called off the dive. The fish seller is back at his van.");
        }

        // ------------------------------------------------------------ finds

        /// <summary>Each fish he might find with this gear and its weight (rare ones weigh more with better gear).</summary>
        public static List<(Species species, float weight)> FindTable(int gearLevel)
        {
            var list = new List<(Species, float)>();
            var d = Data;
            float boost = 0f;
            if (d.gear != null) for (int i = 0; i < gearLevel && i < d.gear.Count; i++) boost = Mathf.Max(boost, d.gear[i].rareBoost);
            if (d.finds == null) return list;
            foreach (var find in d.finds)
            {
                var species = find != null ? Species.Find(find.species) : null;
                if (species == null) continue;
                int step = (int)find.rarity;
                float weight = d.rarityWeights != null && d.rarityWeights.Count > 0 ? d.rarityWeights[Mathf.Clamp(step, 0, d.rarityWeights.Count - 1)] : 1f;
                weight *= 1f + boost * step / 5f;
                if (weight > 0f) list.Add((species, weight));
            }
            return list;
        }

        /// <summary>The chance of each fish on one dive with this gear (sums to 1).</summary>
        public static List<(Species species, float chance)> Odds(int gearLevel)
        {
            var table = FindTable(gearLevel);
            float total = 0f;
            foreach (var row in table) total += row.weight;
            var odds = new List<(Species, float)>();
            foreach (var row in table) odds.Add((row.species, total > 0f ? row.weight / total : 0f));
            return odds;
        }

        public static SpeciesRarity WildRarity(Species species)
        {
            if (Data.finds != null) foreach (var find in Data.finds) if (find != null && species != null && find.species == species.id) return find.rarity;
            return species != null ? species.rarity : SpeciesRarity.Common;
        }

        static Species Roll()
        {
            var table = FindTable(GearLevel);
            float total = 0f;
            foreach (var row in table) total += row.weight;
            if (total <= 0f) return null;
            float roll = GameRandom.Range(0f, total);
            foreach (var row in table)
            {
                roll -= row.weight;
                if (roll <= 0f) return row.species;
            }
            return table[table.Count - 1].species;
        }

        /// <summary>
        /// Time step: back from the dive with one egg or fry he saved, put in the holding tank with
        /// the most room. If the holding tanks filled up meanwhile he doesn't go, and the fee comes back.
        /// Then, with Keep diving on, he books the next dive himself if he can.
        /// </summary>
        public static void OnTimeStep(IReadOnlyList<StoreTank> tanks)
        {
            if (Out)
            {
                Out = false;
                StoreTank best = null;
                foreach (var tank in HoldingTanks(tanks))
                    if (RoomIn(tank.Tank) > 0 && (best == null || RoomIn(tank.Tank) > RoomIn(best.Tank))) best = tank;
                var species = best != null ? Roll() : null;
                if (species == null)
                {
                    Economy.Earn(Fee, "a dive with nowhere to put the fish", MoneyKind.Refunds);
                    GameLog.Add("The fish seller didn't dive after all: your holding tanks are full. He gave the fee back.");
                }
                else Bring(best, species);
            }
            if (KeepDiving && !Out && WhyCantDive(tanks) == null) TryDive(tanks, true);
        }

        static void Bring(StoreTank holding, Species species)
        {
            var tank = holding.Tank;
            var genome = Genome.CreateRandom(species);
            bool egg = species.reproduction == Reproduction.EggLayer && GameRandom.Chance(Data.eggChance);
            // He knows his fish, so a find is never a mystery fry (generation 2 shows what it is).
            if (egg) EggClutch.LayFrom(tank, new List<Genome> { genome }, 2, "found by the fish seller", Mathf.Max(1, species.eggHatchSteps));
            else FishFactory.Spawn(genome, 2, 0.05f, tank, tank.RandomFreePoint(0.1f), "found by the fish seller");
            Progression.Award(XpTrack.Fishkeeping, Data.xpFind);
            Journal.Discover(species);
            var rarity = WildRarity(species);
            LastFind = $"{(egg ? "An egg" : "A fry")} of a {species.displayName} ({rarity.ToString().ToLower()} in the wild), in {holding.Title}.";
            GameLog.Add($"The fish seller is back from his dive! He saved {(egg ? "an egg" : "a fry")} of a {species.displayName} ({rarity.ToString().ToLower()} in the wild) and put it in {holding.Title}. Raise it there, then put it in the {PublicAquarium.RoomName.ToLower()}, sell it on the fish market or let it go.");
            Fx.Card("BACK FROM THE DIVE", species.displayName, $"{(egg ? "An egg" : "A fry")} he saved on his dive, {rarity.ToString().ToLower()} in the wild. It's waiting in {holding.Title}.", "Raise it for the aquarium", UiTheme.Aqua);
        }

        // ------------------------------------------------------------ gear

        public static int GearCount => Data.gear != null ? Data.gear.Count : 0;
        public static ScubaGearBalance NextGear => GearLevel < GearCount ? Data.gear[GearLevel] : null;
        public static int GearPrice(ScubaGearBalance gear) => gear == null ? 0 : Perks.Build(gear.price);

        public static string WhyCantUpgradeGear()
        {
            var next = NextGear;
            if (next == null) return "He has the best gear there is.";
            if (!Progression.IsUnlocked(XpTrack.Fishkeeping, next.level)) return $"Needs {Progression.LockText(XpTrack.Fishkeeping, next.level)}.";
            if (!Economy.CanAfford(GearPrice(next))) return StoreLayout.NoMoney;
            return null;
        }

        public static bool TryUpgradeGear()
        {
            var next = NextGear;
            var why = WhyCantUpgradeGear();
            if (why != null)
            {
                GameLog.Add(why);
                return false;
            }
            if (!Economy.TrySpend(GearPrice(next), $"the fish seller's {next.name.ToLower()}", MoneyKind.Equipment)) return false;
            GearLevel++;
            Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpBuyEquipment);
            GameLog.Add($"The fish seller has {next.name.ToLower()} now: rare fish are likelier on his dives.");
            return true;
        }

        // ------------------------------------------------------------ saving

        internal static ScubaSave Save() => new ScubaSave { gear = GearLevel, diving = Out, keepDiving = KeepDiving, lastFind = LastFind };

        internal static void Load(ScubaSave s)
        {
            Reset();
            if (s == null) return;
            GearLevel = Mathf.Clamp(s.gear, 0, GearCount);
            Out = s.diving;
            KeepDiving = s.keepDiving;
            LastFind = s.lastFind ?? "";
        }
    }

    [Serializable]
    public class ScubaSave
    {
        public int gear;
        public bool diving, keepDiving;
        public string lastFind;
    }
}
