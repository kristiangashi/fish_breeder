using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using FishBreeder.People;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Sim
{
    /// <summary>How a pacing bot plays. Each style is a few knobs on the same routine.</summary>
    public class BotStyle
    {
        public string name;
        /// <summary>Chance each chore is done when it's due (1 = never forgets).</summary>
        public float care = 1f;
        /// <summary>Asking price as a share of the appraised price.</summary>
        public float markup = 1f;
        /// <summary>Money kept back from anything that isn't care or food.</summary>
        public int reserve = 200;
        /// <summary>Buys a new tank once it has this many times its price (plus the reserve).</summary>
        public float tankCushion = 2f;
        /// <summary>Breeding stock kept back per species in breeder tanks (0 = sells everything grown).</summary>
        public int keepPerSpecies = 2;
        /// <summary>Buys the dearest egg box it can afford rather than the cheapest that fits.</summary>
        public bool bestEggs;
        /// <summary>Upgrades filters and heaters as soon as better ones unlock.</summary>
        public bool upgradesGear;
        /// <summary>Sets up cross-breeding pairs (the most valuable mix it can make) in its breeder tanks, and stocks the For Sale tanks with its most valuable fish first.</summary>
        public bool mixes;

        public static readonly BotStyle[] All =
        {
            new BotStyle { name = "Mixer", care = 1f, markup = 1.05f, reserve = 200, tankCushion = 2f, keepPerSpecies = 2, bestEggs = true, mixes = true },
            new BotStyle { name = "Cautious", care = 1f, markup = 1f, reserve = 300, tankCushion = 3f, keepPerSpecies = 2 },
            new BotStyle { name = "Greedy", care = 0.9f, markup = 1.25f, reserve = 50, tankCushion = 1.2f, keepPerSpecies = 0, bestEggs = true },
            new BotStyle { name = "Breeder", care = 1f, markup = 1.05f, reserve = 200, tankCushion = 2f, keepPerSpecies = 4, bestEggs = true, upgradesGear = true },
            new BotStyle { name = "Careless", care = 0.65f, markup = 1f, reserve = 0, tankCushion = 1.5f, keepPerSpecies = 2 },
        };

        public static BotStyle Find(string name)
        {
            foreach (var s in All) if (string.Equals(s.name, name, System.StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }
    }

    /// <summary>
    /// A scripted player for <see cref="DevFastForward"/>: each morning and evening it does the
    /// closed-shop jobs through <see cref="PlayerActions"/> (so with the game's own costs, checks and
    /// XP), the way its <see cref="BotStyle"/> says. It also notes a few numbers per day for the
    /// pacing report.
    /// </summary>
    public class PacingBot : IDayBot
    {
        readonly BotStyle style;
        readonly StoreLayout store;
        readonly System.Random luck;
        /// <summary>Days each fish has been on sale at its current price, to mark down what doesn't sell.</summary>
        readonly Dictionary<Fish, int> onSale = new Dictionary<Fish, int>();
        readonly HashSet<Fish> deadSeen = new HashSet<Fish>();
        /// <summary>The mixer's cross-breeding parents: never sent to the For Sale tanks.</summary>
        readonly HashSet<Fish> crossParents = new HashSet<Fish>();

        public string Name => style.name;
        public int Deaths { get; private set; }
        public int BoxesBought { get; private set; }
        public int GrandmaRescues { get; private set; }
        public List<string> Events { get; } = new List<string>();
        public Dictionary<string, int> DeathCauses { get; } = new Dictionary<string, int>();
        readonly int[] deathsByRarity = new int[8];
        /// <summary>The to-do list's water line: below it a tank needs wiping.</summary>
        const float ChoreWater = 0.75f;
        /// <summary>A tidy player tops the water up with a clean below this (cleans are free), not only once the to-do list asks.</summary>
        const float TopUpWater = 0.85f;
        /// <summary>Running counts over tank care sessions (one per tank with fish per closed session).</summary>
        public int WipesOverloaded;
        public int Sessions, ChoreSessions, Wipes, OverloadedSessions, OverwhelmedOverloaded, OverwhelmedWithinFilter;
        /// <summary>Deaths so far of this rarity (Rare counts Rare and everything rarer).</summary>
        public int DeathsOfRarity(int rarity)
        {
            if (rarity < 2) return deathsByRarity[rarity];
            int n = 0;
            for (int r = rarity; r < deathsByRarity.Length; r++) n += deathsByRarity[r];
            return n;
        }

        public PacingBot(BotStyle style, StoreLayout store, int seed)
        {
            this.style = style;
            this.store = store;
            luck = new System.Random(seed ^ 0x5eed);
        }

        /// <summary>For the dev fast-forward's bot list: plays the current game.</summary>
        protected PacingBot(string styleName) : this(BotStyle.Find(styleName), Object.FindAnyObjectByType<StoreLayout>(), System.Environment.TickCount) { }

        bool Does() => luck.NextDouble() < style.care;

        public void Morning()
        {
            if (Economy.GrandmaNote != null && Economy.GrandmaNote.Contains("saved")) GrandmaRescues++;
            CountDead();
            Care();
            KeepFoodStocked();
            Equip();
            if (style.mixes) PairForMixes();
            SortFish();
            Expand();
            Equip();
            SortByWater();
            ThinOut();
            OrderBox();
        }

        public void Evening()
        {
            CountDead();
            Care();
            ThinOut();
        }

        /// <summary>
        /// A crowded tank makes every fish in it sick, so fish over a tank's limit (new fry, mostly)
        /// move to whichever tank has room: grown fish to the For Sale tanks first, fry anywhere else.
        /// </summary>
        void ThinOut()
        {
            if (!Does()) return;
            foreach (var t in OwnTanks())
            {
                var tank = t.Tank;
                var extras = new List<Fish>();
                foreach (var fish in tank.Residents) if (!fish.IsDead && !fish.IsPregnant) extras.Add(fish);
                // Fry move first (they're the newcomers), then the least valuable.
                extras.Sort((a, b) => a.Stage != b.Stage ? a.Stage.CompareTo(b.Stage) : a.AppraisedPrice.CompareTo(b.AppraisedPrice));
                foreach (var fish in extras)
                {
                    if (Crowding(tank) <= 0) break;
                    StoreTank best = null;
                    foreach (var other in OwnTanks())
                    {
                        if (other == t || other.Tank.FreeSpaceBeforeLimit <= 0 || other.Tank.IsSaltwater != tank.IsSaltwater) continue;
                        bool sale = other.Role == TankRole.ForSale;
                        if (sale && fish.Stage == LifeStage.Fry && best != null) continue;
                        if (best == null || (fish.Stage != LifeStage.Fry && sale && best.Role != TankRole.ForSale)
                            || other.Tank.FreeSpaceBeforeLimit > best.Tank.FreeSpaceBeforeLimit) best = other;
                    }
                    if (best == null) break;
                    PlayerActions.Move(fish, best, Mathf.Max(1, Mathf.RoundToInt(fish.AppraisedPrice * style.markup)));
                }
            }
        }

        /// <summary>
        /// Fish whose water is wrong for them (cold-water goldfish in a heated tropical tank, say)
        /// move to a tank that suits them better, the way a player sorts a mixed egg box. Mystery
        /// fry stay put: the player can't tell what they are yet.
        /// </summary>
        void SortByWater()
        {
            if (!Does()) return;
            foreach (var t in OwnTanks())
                foreach (var fish in new List<Fish>(t.Tank.Residents))
                {
                    if (fish.IsDead || fish.IdentityHidden || fish.IsPregnant) continue;
                    float here = Health.FishHealth.EnvironmentMismatch(fish.Species, t.Tank);
                    if (here < 0.15f) continue;
                    StoreTank best = null;
                    float bestScore = here - 0.1f;
                    foreach (var other in OwnTanks())
                    {
                        if (other == t || other.Tank.FreeSpaceBeforeLimit <= 0) continue;
                        float score = Health.FishHealth.EnvironmentMismatch(fish.Species, other.Tank);
                        if (score < bestScore) { bestScore = score; best = other; }
                    }
                    if (best != null) PlayerActions.Move(fish, best, Mathf.Max(1, Mathf.RoundToInt(fish.AppraisedPrice * style.markup)));
                }
        }

        /// <summary>Fish over the tank's limit (0 or less when there's room).</summary>
        static int Crowding(Tank tank) => tank.TinyCount - tank.Capacity;

        bool AnyCrowded()
        {
            foreach (var t in OwnTanks()) if (Crowding(t.Tank) > 0) return true;
            return false;
        }

        IEnumerable<StoreTank> OwnTanks()
        {
            foreach (var t in store.Tanks)
                if (t.Role != TankRole.Exhibit && t.Role != TankRole.Holding) yield return t;
        }

        void CountDead()
        {
            foreach (var t in store.Tanks)
                foreach (var corpse in t.Tank.Corpses)
                    if (deadSeen.Add(corpse))
                    {
                        Deaths++;
                        if (corpse.Species != null) deathsByRarity[Mathf.Clamp((int)corpse.Species.rarity, 0, deathsByRarity.Length - 1)]++;
                        string cause = string.IsNullOrEmpty(corpse.CauseOfDeath) ? "unknown" : corpse.CauseOfDeath;
                        DeathCauses.TryGetValue(cause, out int n);
                        DeathCauses[cause] = n + 1;
                    }
        }

        // ---------------------------------------------------------------- care

        void Care()
        {
            foreach (var t in OwnTanks())
            {
                var tank = t.Tank;
                if (tank.Corpses.Count > 0 && Does()) PlayerActions.RemoveDead(t);
                // An empty tank still goes green and murky, and shoppers look in it too.
                if (tank.Residents.Count == 0)
                {
                    if (tank.TooMuchAlgae && tank.CleansLeft > 0 && Does()) PlayerActions.Clean(t);
                    if (tank.WaterQuality < 0.55f && Does()) PlayerActions.TreatWater(t);
                    continue;
                }
                if (!tank.FedSinceTimeStep && Does()) PlayerActions.Feed(t, PickFood(t));
                if (tank.InBloom) { if (Does()) PlayerActions.DeepClean(t); }
                else
                {
                    // Wipe while the algae is overgrown or the water is under 85% (the to-do list asks at 75%),
                    // and note whether the water needed it and whether the wipes kept up (water chores, fish cap).
                    bool overloaded = tank.FishLoad > tank.FilterFishCapacity;
                    bool needed = tank.WaterQuality < ChoreWater;
                    Sessions++;
                    if (overloaded) OverloadedSessions++;
                    if (needed) ChoreSessions++;
                    while ((tank.TooMuchAlgae || tank.WaterQuality < TopUpWater) && tank.CleansLeft > 0 && Does())
                    {
                        if (!PlayerActions.Clean(t)) break;
                        Wipes++;
                        if (overloaded) WipesOverloaded++;
                    }
                    if (tank.WaterQuality < ChoreWater)
                    {
                        if (overloaded) OverwhelmedOverloaded++;
                        else OverwhelmedWithinFilter++;
                    }
                }
                if ((tank.HasPests || tank.WaterQuality < 0.55f) && Does()) PlayerActions.TreatWater(t);
                foreach (var kind in Equipment.Kinds)
                {
                    var item = tank.GetEquipment(kind);
                    if (item != null && item.broken && Does()) PlayerActions.Repair(t, kind);
                }
                Medicate(t);
            }
        }

        void Medicate(StoreTank t)
        {
            // With a quarantine tank, the sick go there to be dosed, so the healthy aren't stressed and damaged by medicine they don't need.
            if (t.Role != TankRole.Quarantine)
            {
                StoreTank quarantine = null;
                foreach (var other in OwnTanks()) if (other.Role == TankRole.Quarantine && other.Tank.IsSaltwater == t.Tank.IsSaltwater) quarantine = other;
                if (quarantine != null)
                    foreach (var fish in new List<Fish>(t.Tank.Residents))
                        if (!fish.IsDead && fish.Health.IsSick && fish.Health.Disease.cure != Health.Medicine.None && quarantine.Tank.FreeSpaceBeforeLimit > 0 && Does())
                            PlayerActions.Move(fish, quarantine, Mathf.Max(1, fish.Price));
            }
            var cures = new HashSet<Health.Medicine>();
            foreach (var fish in t.Tank.Residents)
                if (!fish.IsDead && fish.Health.IsSick && fish.Health.Disease.cure != Health.Medicine.None) cures.Add(fish.Health.Disease.cure);
            foreach (var medicine in cures) if (Does()) PlayerActions.Dose(t, medicine);
        }

        FoodType PickFood(StoreTank t)
        {
            // A favourite food in stock if any fish has one, else whatever there's most of.
            foreach (var fish in t.Tank.Residents)
                if (fish.Species.favoriteFoods != null)
                    foreach (var food in fish.Species.favoriteFoods)
                        if (Economy.FoodPortions(food) > 0) return food;
            FoodType best = FoodType.Flakes;
            foreach (FoodType food in System.Enum.GetValues(typeof(FoodType)))
                if (food != FoodType.AlgaeWafers && Economy.FoodPortions(food) > Economy.FoodPortions(best)) best = food;
            return best;
        }

        void KeepFoodStocked()
        {
            int tanksWithFish = 0;
            foreach (var t in OwnTanks()) if (t.Tank.Residents.Count > 0 || EggClutch.EggsOf(t.Tank, SizeGroup.Tiny) > 0) tanksWithFish++;
            int need = tanksWithFish * 4;
            int have = 0;
            foreach (FoodType food in System.Enum.GetValues(typeof(FoodType))) if (food != FoodType.AlgaeWafers) have += Economy.FoodPortions(food);
            FoodBalance flakes = null;
            foreach (var offer in SupplierCatalog.Food) if (offer.type == FoodType.Flakes) flakes = offer;
            for (int i = 0; i < 5 && have < need && flakes != null; i++)
            {
                if (!PlayerActions.BuyFood(flakes)) break;
                have += flakes.feedings;
            }
        }

        // ---------------------------------------------------------------- gear

        void Equip()
        {
            foreach (var t in OwnTanks())
            {
                var tank = t.Tank;
                if (tank.Residents.Count == 0 && EggClutch.EggsOf(tank, SizeGroup.Tiny) == 0) continue;
                BuyGear(t, EquipmentKind.Filter, !tank.HasFilter);
                bool cold = false;
                foreach (var fish in tank.Residents)
                    if (tank.Temperature < fish.Species.tempMin || tank.Temperature > fish.Species.tempMax) cold = true;
                BuyGear(t, EquipmentKind.Heater, !tank.HasHeater && cold);
                if (tank.HasHeater) tank.SetHeater(tank.SuggestedTemperature);
                BuyGear(t, EquipmentKind.Bubbler, tank.GetEquipment(EquipmentKind.Bubbler) == null && tank.Residents.Count > tank.OxygenFor);
            }
        }

        void BuyGear(StoreTank t, EquipmentKind kind, bool missing)
        {
            var installed = t.Tank.GetEquipment(kind);
            if (!missing && !(style.upgradesGear && installed != null)) return;
            int best = -1;
            foreach (int tier in Equipment.TiersFor(kind, t.Tank))
            {
                var info = Equipment.Tier(kind, tier);
                if (info == null || !Progression.IsUnlocked(XpTrack.Fishkeeping, info.unlockLevel)) continue;
                int price = Perks.Supplier(info.price);
                // A missing piece is care: only the cheapest, and the reserve may go on it.
                if (missing) { if (Economy.Money >= price && (best < 0 || price < Perks.Supplier(Equipment.Tier(kind, best).price))) best = tier; }
                else if (tier > installed.tier && Economy.Money - price >= style.reserve) best = Mathf.Max(best, tier);
            }
            if (best >= 0) PlayerActions.BuyEquipment(t, kind, best);
        }

        // ---------------------------------------------------------------- fish

        /// <summary>
        /// Each breeder tank without a cross pair gets the most valuable one the bot can make: an
        /// adult female and male of two different species that can cross-breed, from any of its tanks.
        /// </summary>
        void PairForMixes()
        {
            var adults = new List<(Fish fish, StoreTank tank)>();
            foreach (var t in OwnTanks())
                foreach (var fish in t.Tank.Residents)
                    if (!fish.IsDead && !fish.IdentityHidden && fish.Stage == LifeStage.Adult && !fish.Health.IsSick && !fish.IsPregnant) adults.Add((fish, t));
            var used = new HashSet<Fish>();
            // The first breeder tank keeps breeding its own kinds (the stock); the others get cross pairs.
            bool first = true;
            foreach (var breeder in OwnTanks())
            {
                if (breeder.Role != TankRole.Breeder) continue;
                if (first) { first = false; continue; }
                bool hasCross = false;
                foreach (var a in breeder.Tank.Residents)
                    foreach (var b in breeder.Tank.Residents)
                        if (a.Sex == Sex.Female && b.Sex == Sex.Male && !a.IsDead && !b.IsDead && a.Species != b.Species && CrossBreeding.Compatible(a.Species, b.Species))
                        {
                            hasCross = true;
                            crossParents.Add(a);
                            crossParents.Add(b);
                        }
                if (hasCross) continue;
                (Fish, StoreTank) bestF = default, bestM = default;
                int bestValue = 0;
                foreach (var f in adults)
                {
                    if (f.fish.Sex != Sex.Female || used.Contains(f.fish) || f.tank.Tank.IsSaltwater != breeder.Tank.IsSaltwater) continue;
                    foreach (var m in adults)
                    {
                        if (m.fish.Sex != Sex.Male || used.Contains(m.fish) || m.fish.Species == f.fish.Species || !CrossBreeding.Compatible(f.fish.Species, m.fish.Species)) continue;
                        int value = Fish.Appraise(f.fish.Species, m.fish.Species, m.fish.Species, LifeStage.Adult);
                        if (value > bestValue) { bestValue = value; bestF = f; bestM = m; }
                    }
                }
                if (bestValue == 0) continue;
                used.Add(bestF.Item1);
                used.Add(bestM.Item1);
                bool moved = (bestF.Item2 == breeder || PlayerActions.Move(bestF.Item1, breeder, Mathf.Max(1, bestF.Item1.Price)))
                             && (bestM.Item2 == breeder || PlayerActions.Move(bestM.Item1, breeder, Mathf.Max(1, bestM.Item1.Price)));
                if (moved)
                {
                    crossParents.Add(bestF.Item1);
                    crossParents.Add(bestM.Item1);
                    Events.Add($"paired {bestF.Item1.Species} x {bestM.Item1.Species} (${bestValue})");
                }
            }
        }

        /// <summary>Grown fish go to the For Sale tanks, except breeding stock kept in the breeder tanks. Unsold fish are marked down.</summary>
        void SortFish()
        {
            var forSale = new List<StoreTank>();
            foreach (var t in OwnTanks()) if (t.Role == TankRole.ForSale) forSale.Add(t);

            // Mark down what hasn't sold in two days, never below half the appraisal.
            foreach (var t in forSale)
                foreach (var fish in t.Tank.Residents)
                {
                    if (fish.IsDead) continue;
                    onSale.TryGetValue(fish, out int days);
                    onSale[fish] = ++days;
                    if (days >= 2 && fish.Price > fish.AppraisedPrice * 0.5f)
                    {
                        fish.Price = Mathf.Max(1, Mathf.RoundToInt(fish.Price * 0.9f));
                        onSale[fish] = 0;
                    }
                }

            var kept = new Dictionary<string, int>();
            var waiting = new List<(Fish fish, StoreTank tank)>();
            foreach (var t in OwnTanks())
                if (t.Role != TankRole.ForSale)
                    foreach (var fish in t.Tank.Residents) waiting.Add((fish, t));
            // A mixer stocks its dearest fish first, so cheap ones don't take the room.
            if (style.mixes) waiting.Sort((a, b) => (b.tank.Role == TankRole.Breeder).CompareTo(a.tank.Role == TankRole.Breeder) != 0
                ? (b.tank.Role == TankRole.Breeder).CompareTo(a.tank.Role == TankRole.Breeder) : b.fish.AppraisedPrice.CompareTo(a.fish.AppraisedPrice));
            foreach (var (fish, t) in waiting)
            {
                if (fish.IsDead || fish.Stage == LifeStage.Fry || fish.IsPregnant) continue;
                string kind = fish.Phenotype.SpeciesName;
                kept.TryGetValue(kind, out int have);
                bool breederTank = t.Role == TankRole.Breeder;
                if (breederTank && crossParents.Contains(fish)) continue;
                if (breederTank && have < style.keepPerSpecies)
                {
                    kept[kind] = have + 1;
                    continue;
                }
                foreach (var dest in forSale)
                    if (PlayerActions.Move(fish, dest, Mathf.Max(1, Mathf.RoundToInt(fish.AppraisedPrice * style.markup)))) { onSale[fish] = 0; break; }
            }
        }

        // ---------------------------------------------------------------- buying

        void OrderBox()
        {
            if (FishSeller.Away || AnyCrowded()) return;
            // Into the tank with most room, breeder tanks first (that's where fry grow up best).
            StoreTank target = null;
            foreach (var t in OwnTanks())
            {
                if (t.Role == TankRole.ForSale || t.Tank.IsSaltwater) continue;
                if (target == null || (t.Role == TankRole.Breeder && target.Role != TankRole.Breeder)
                    || (t.Role == target.Role && PlayerActions.FreeSpace(t) > PlayerActions.FreeSpace(target))) target = t;
            }
            if (target == null) return;

            EggTierBalance pick = null;
            foreach (var tier in EggBoxes.Tiers)
            {
                if (tier.saltwater || PlayerActions.WhyCantOrder(tier, target, false) != null) continue;
                int price = Perks.Supplier(tier.price);
                // The opening stock is an investment the reserve can go on; after that boxes come out of profit.
                int keep = Economy.CanOpenShop ? style.reserve : 0;
                if (Economy.Money - price < keep) continue;
                if (pick == null || (style.bestEggs ? price > Perks.Supplier(pick.price) : price < Perks.Supplier(pick.price))) pick = tier;
            }
            // A cautious buyer moves up a tier once it can afford it three times over.
            if (!style.bestEggs && pick != null)
                foreach (var tier in EggBoxes.Tiers)
                {
                    int price = Perks.Supplier(tier.price);
                    if (!tier.saltwater && price > Perks.Supplier(pick.price) && Economy.Money >= price * 3 + style.reserve && PlayerActions.WhyCantOrder(tier, target, false) == null) pick = tier;
                }
            if (pick != null && PlayerActions.OrderEggs(pick, target))
            {
                BoxesBought++;
                Events.Add($"{pick.id} eggs");
            }
        }

        static readonly TankRole[] ExpandOrder = { TankRole.ForSale, TankRole.Breeder, TankRole.Quarantine, TankRole.ForSale, TankRole.Breeder, TankRole.Display };

        void Expand()
        {
            foreach (var role in ExpandOrder)
                foreach (var slot in store.Slots)
                {
                    if (slot.tank != null || !slot.IsOpen || slot.wall || slot.Exhibit || slot.holding || !slot.Allows(role)) continue;
                    if (!store.CanBuyTank(slot, role, out _)) continue;
                    int price = StoreLayout.TankPrice(slot, role);
                    if (Economy.Money < style.reserve + price * style.tankCushion) return;
                    var bought = store.TryBuyTank(slot, role);
                    if (bought != null) Events.Add($"bought {StoreTank.RoleName(role)} tank");
                    return;
                }
        }
    }

    // One class per style, so each shows up in the dev fast-forward's bot list.
    public class CautiousBot : PacingBot { public CautiousBot() : base("Cautious") { } }
    public class GreedyBot : PacingBot { public GreedyBot() : base("Greedy") { } }
    public class BreederBot : PacingBot { public BreederBot() : base("Breeder") { } }
    public class CarelessBot : PacingBot { public CarelessBot() : base("Careless") { } }
    public class MixerBot : PacingBot { public MixerBot() : base("Mixer") { } }
}
