using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>The six skill trees: three for Fishkeeping, three for Shopkeeping.</summary>
    public enum PerkTree { Aquariums, Biology, Breeding, Finances, Construction, PublicRelations }

    /// <summary>What a perk changes in the game. Several perks can share an effect; their values add up.</summary>
    public enum PerkEffect
    {
        // Aquariums
        CleanerWater, StrongerWipes, SlowerAlgae, DecorationSpots, SturdierEquipment, BetterRepairs, RoomierTanks, ExtraWipes,
        // Biology
        FewerIllnesses, SlowerHunger, WiderTolerance, GentlerMedicine, FasterGrowth, BetterFood, LongerLife, FasterResearch,
        // Breeding
        BreedingChance, ShorterRest, BiggerLitters, HardyEggs, QuickerBirths, MoreMutations, CrossChance, PedigreeValue,
        // Finances
        SupplierDiscount, PriceTolerance, SaleTips, BiggerAllowance, PurchaseChance, Interest,
        // Construction
        BuildDiscount, DecorDiscount, MoreShopTanks, TankTraffic, DecorTraffic, UpgradeDiscount, BiggerCrowds,
        // Public relations
        MoreCustomers, ReputationGain, ReputationShield, GrossTolerance, DeadFishTolerance, ExtraTankVisits, DailyReputation,
        // Finances (added later, so saved files keep their numbers)
        Bookkeeping,
    }

    /// <summary>One perk in a skill tree. Each rank bought adds <see cref="perRank"/> to its effect.</summary>
    [Serializable]
    public class PerkBalance
    {
        public string id;
        public PerkTree tree;
        [Tooltip("1 is open from the start; each tier after needs more points spent in the tiers above it.")]
        public int tier = 1;
        public string name;
        [Tooltip("{0} is replaced by the perk's value.")]
        public string text;
        public int maxRank = 3;
        public float perRank = 0.05f;
        [Tooltip("Shows the value as a percentage (0.05 shows as 5%).")]
        public bool percent = true;
        public PerkEffect effect;

        public string FormatValue(float value) => percent ? $"{value * 100f:0.#}%" : $"{value:0.##}";

        public string Describe(int rank) => string.Format(text ?? "", FormatValue(perRank * Mathf.Max(1, rank)));

        public static List<PerkBalance> CreateDefaults() => new List<PerkBalance>
        {
            // Aquariums: keeping tanks clean, roomy and running.
            Make("clean_freak", PerkTree.Aquariums, 1, "Clean freak", "Water gets dirty {0} slower", 5, 0.06f, PerkEffect.CleanerWater),
            Make("elbow_grease", PerkTree.Aquariums, 1, "Elbow grease", "Wipes clean {0} more", 3, 0.15f, PerkEffect.StrongerWipes),
            Make("algae_control", PerkTree.Aquariums, 2, "Algae control", "Algae grows {0} slower", 5, 0.08f, PerkEffect.SlowerAlgae),
            Make("aquascaper", PerkTree.Aquariums, 2, "Aquascaper", "{0} more decoration spots in every tank", 3, 2f, PerkEffect.DecorationSpots, false),
            Make("tinkerer", PerkTree.Aquariums, 3, "Tinkerer", "Equipment breaks down {0} less often", 5, 0.08f, PerkEffect.SturdierEquipment),
            Make("handyman", PerkTree.Aquariums, 3, "Handyman", "Repair kits take off {0} more wear", 3, 0.15f, PerkEffect.BetterRepairs),
            Make("roomy_tanks", PerkTree.Aquariums, 4, "Roomy tanks", "Tanks hold {0} more fish", 3, 0.1f, PerkEffect.RoomierTanks),
            Make("tireless", PerkTree.Aquariums, 4, "Tireless", "{0} extra wipe for every tank each session", 1, 1f, PerkEffect.ExtraWipes, false),

            // Biology: healthy, well-fed, long-lived fish.
            Make("immunity", PerkTree.Biology, 1, "Strong immunity", "Fish catch illnesses {0} less often", 5, 0.08f, PerkEffect.FewerIllnesses),
            Make("slow_metabolism", PerkTree.Biology, 1, "Slow metabolism", "Fish get hungry {0} slower", 3, 0.08f, PerkEffect.SlowerHunger),
            Make("comfort_zone", PerkTree.Biology, 2, "Comfort zone", "Wrong temperature, pH and salt hurt fish {0} less", 5, 0.1f, PerkEffect.WiderTolerance),
            Make("gentle_dosing", PerkTree.Biology, 2, "Gentle dosing", "Medicine stresses fish {0} less and takes {0} more to overdose", 3, 0.15f, PerkEffect.GentlerMedicine),
            Make("growth_formula", PerkTree.Biology, 3, "Growth formula", "Fish grow up {0} faster", 5, 0.08f, PerkEffect.FasterGrowth),
            Make("nutritionist", PerkTree.Biology, 3, "Nutritionist", "Every feeding fills fish {0} more", 3, 0.1f, PerkEffect.BetterFood),
            Make("longevity", PerkTree.Biology, 4, "Longevity", "Fish live {0} longer before old age sets in", 3, 0.1f, PerkEffect.LongerLife),
            Make("naturalist", PerkTree.Biology, 4, "Naturalist", "Research goes {0} faster", 1, 0.25f, PerkEffect.FasterResearch),

            // Breeding: more, faster and stranger fry.
            Make("matchmaker", PerkTree.Breeding, 1, "Matchmaker", "Fish are {0} more likely to breed", 5, 0.06f, PerkEffect.BreedingChance),
            Make("quick_recovery", PerkTree.Breeding, 1, "Quick recovery", "Parents rest {0} fewer time step between broods", 1, 1f, PerkEffect.ShorterRest, false),
            Make("big_families", PerkTree.Breeding, 2, "Big families", "Litters and clutches are {0} bigger", 5, 0.08f, PerkEffect.BiggerLitters),
            Make("hardy_eggs", PerkTree.Breeding, 2, "Hardy eggs", "Eggs are {0} less likely to die in dirty water", 3, 0.25f, PerkEffect.HardyEggs),
            Make("due_early", PerkTree.Breeding, 3, "Due early", "Pregnancies and eggs take {0} fewer time steps", 2, 1f, PerkEffect.QuickerBirths, false),
            Make("wild_genes", PerkTree.Breeding, 3, "Wild genes", "Mutations are {0} more likely, for surprise looks", 5, 0.15f, PerkEffect.MoreMutations),
            Make("hybrid_expert", PerkTree.Breeding, 4, "Hybrid expert", "Different species cross-breed {0} more often", 3, 0.25f, PerkEffect.CrossChance),
            Make("pedigree", PerkTree.Breeding, 4, "Pedigree", "Fish bred in your shop are worth {0} more", 1, 0.15f, PerkEffect.PedigreeValue),

            // Finances: spend less, earn more.
            Make("bargain_hunter", PerkTree.Finances, 1, "Bargain hunter", "Supplier prices are {0} lower", 5, 0.03f, PerkEffect.SupplierDiscount),
            Make("sweet_talker", PerkTree.Finances, 1, "Sweet talker", "Customers pay up to {0} more before a price puts them off", 3, 0.04f, PerkEffect.PriceTolerance),
            Make("tip_jar", PerkTree.Finances, 2, "Tip jar", "Every sale brings a {0} tip on top", 5, 0.03f, PerkEffect.SaleTips),
            Make("safety_net", PerkTree.Finances, 2, "Safety net", "Grandma's gifts are ${0} bigger", 3, 50f, PerkEffect.BiggerAllowance, false),
            Make("bookkeeper", PerkTree.Finances, 2, "Bookkeeper", "The finances screen (F) shows your best-selling fish and what each kind earns", 1, 1f, PerkEffect.Bookkeeping, false),
            Make("closer", PerkTree.Finances, 3, "Closer", "Customers are {0} more likely to buy", 5, 0.05f, PerkEffect.PurchaseChance),
            Make("savings", PerkTree.Finances, 3, "Savings account", "Your money earns {0} interest every night", 3, 0.01f, PerkEffect.Interest),
            Make("premium_brand", PerkTree.Finances, 4, "Premium brand", "Customers pay up to {0} more before a price puts them off", 3, 0.05f, PerkEffect.PriceTolerance),
            Make("wholesale", PerkTree.Finances, 4, "Wholesale account", "Supplier prices are {0} lower", 1, 0.1f, PerkEffect.SupplierDiscount),

            // Construction: a bigger, busier, cheaper-to-build shop.
            Make("builders_discount", PerkTree.Construction, 1, "Builder's discount", "Tanks, tank upgrades and new rooms cost {0} less", 5, 0.04f, PerkEffect.BuildDiscount),
            Make("interior_design", PerkTree.Construction, 1, "Interior design", "Decorations cost {0} less", 3, 0.1f, PerkEffect.DecorDiscount),
            Make("floor_space", PerkTree.Construction, 2, "More floor space", "Room for {0} more for sale and {0} more display tank", 2, 1f, PerkEffect.MoreShopTanks, false),
            Make("showroom", PerkTree.Construction, 2, "Showroom", "Tanks bring in {0} more customers", 3, 0.15f, PerkEffect.TankTraffic),
            Make("feature_walls", PerkTree.Construction, 3, "Feature walls", "Decorations bring in {0} more customers", 5, 0.2f, PerkEffect.DecorTraffic),
            Make("glass_supplier", PerkTree.Construction, 3, "Glass supplier", "Tank upgrades cost {0} less", 3, 0.1f, PerkEffect.UpgradeDiscount),
            Make("wide_aisles", PerkTree.Construction, 4, "Wide aisles", "{0} more customers fit in the store at once", 3, 2f, PerkEffect.BiggerCrowds, false),
            Make("master_builder", PerkTree.Construction, 4, "Master builder", "Tanks, tank upgrades and new rooms cost {0} less", 1, 0.15f, PerkEffect.BuildDiscount),

            // Public relations: more customers, better reputation.
            Make("word_of_mouth", PerkTree.PublicRelations, 1, "Word of mouth", "{0} more customers come in", 5, 0.05f, PerkEffect.MoreCustomers),
            Make("friendly_service", PerkTree.PublicRelations, 1, "Friendly service", "Good days raise your reputation {0} more", 3, 0.15f, PerkEffect.ReputationGain),
            Make("thick_skin", PerkTree.PublicRelations, 2, "Thick skin", "Bad days lower your reputation {0} less", 3, 0.2f, PerkEffect.ReputationShield),
            Make("spotless_image", PerkTree.PublicRelations, 2, "Spotless image", "Customers put up with {0} dirtier tanks", 5, 0.06f, PerkEffect.GrossTolerance),
            Make("damage_control", PerkTree.PublicRelations, 3, "Damage control", "Dead fish upset customers {0} less", 3, 0.25f, PerkEffect.DeadFishTolerance),
            Make("window_shoppers", PerkTree.PublicRelations, 3, "Window shoppers", "Customers look at {0} more tank each", 2, 1f, PerkEffect.ExtraTankVisits, false),
            Make("loyal_regulars", PerkTree.PublicRelations, 4, "Loyal regulars", "{0} more customers come in", 3, 0.08f, PerkEffect.MoreCustomers),
            Make("local_celebrity", PerkTree.PublicRelations, 4, "Local celebrity", "Your reputation climbs {0} every night on its own", 1, 0.02f, PerkEffect.DailyReputation),
        };

        static PerkBalance Make(string id, PerkTree tree, int tier, string name, string text, int maxRank, float perRank, PerkEffect effect, bool percent = true)
            => new PerkBalance { id = id, tree = tree, tier = tier, name = name, text = text, maxRank = maxRank, perRank = perRank, effect = effect, percent = percent };
    }

    /// <summary>How perk points are earned and how the tiers open up.</summary>
    [Serializable]
    public class PerkSettingsBalance
    {
        [Tooltip("Perk points for each Fishkeeping or Shopkeeping level gained.")]
        public int pointsPerLevel = 1;
        [Tooltip("Points spent in a tree's earlier tiers to open each next tier.")]
        public int pointsPerTier = 5;
        public int tiers = 4;
        [Tooltip("Most the savings perk pays in one night.")]
        public int maxInterestPerNight = 150;
    }

    /// <summary>
    /// Perks: every Fishkeeping level gives a point to spend in the Aquariums, Biology or Breeding
    /// tree, and every Shopkeeping level one for Finances, Construction or Public Relations. A tree's
    /// first tier is open from the start; each tier after it opens once enough points (5 by
    /// default) have been spent in the tiers above. The game asks <see cref="Value"/> (and the
    /// helpers) for each effect's total.
    /// </summary>
    public static class Perks
    {
        static readonly Dictionary<string, int> ranks = new Dictionary<string, int>();

        static PerkSettingsBalance Settings => Balance.Data.perkSettings;
        public static List<PerkBalance> All => Balance.Data.perks;

        /// <summary>Raised after a perk is bought, so things built from perks (decoration spots) can refresh.</summary>
        public static event Action Changed;

        public static void Reset()
        {
            ranks.Clear();
            Changed?.Invoke();
        }

        public static XpTrack TrackOf(PerkTree tree) => tree <= PerkTree.Breeding ? XpTrack.Fishkeeping : XpTrack.Shopkeeping;

        public static string TreeName(PerkTree tree) => tree == PerkTree.PublicRelations ? "Public Relations" : tree.ToString();

        public static string TreeBlurb(PerkTree tree)
        {
            switch (tree)
            {
                case PerkTree.Aquariums: return "Cleaner, roomier tanks and equipment that lasts.";
                case PerkTree.Biology: return "Healthier, hardier, longer-lived fish.";
                case PerkTree.Breeding: return "More fry, sooner, with rarer looks.";
                case PerkTree.Finances: return "Pay less, charge more, earn on the side.";
                case PerkTree.Construction: return "Cheaper building and a bigger, busier store.";
                default: return "More customers and a reputation that sticks.";
            }
        }

        public static int Rank(PerkBalance perk) => perk != null && perk.id != null && ranks.TryGetValue(perk.id, out var r) ? r : 0;

        public static int Earned(XpTrack track) => track == XpTrack.Research ? 0 : (Progression.Level(track) - 1) * Mathf.Max(0, Settings.pointsPerLevel);

        public static int Spent(PerkTree tree)
        {
            int total = 0;
            foreach (var perk in All) if (perk != null && perk.tree == tree) total += Rank(perk);
            return total;
        }

        public static int Spent(XpTrack track)
        {
            int total = 0;
            foreach (var perk in All) if (perk != null && TrackOf(perk.tree) == track) total += Rank(perk);
            return total;
        }

        /// <summary>Perk points waiting to be spent for this track.</summary>
        public static int Available(XpTrack track) => Mathf.Max(0, Earned(track) - Spent(track));

        public static int TotalAvailable => Available(XpTrack.Fishkeeping) + Available(XpTrack.Shopkeeping);

        /// <summary>Points spent in this tree's tiers before <paramref name="tier"/>.</summary>
        public static int SpentAbove(PerkTree tree, int tier)
        {
            int total = 0;
            foreach (var perk in All) if (perk != null && perk.tree == tree && perk.tier < tier) total += Rank(perk);
            return total;
        }

        public static int NeededFor(int tier) => Mathf.Max(0, tier - 1) * Mathf.Max(0, Settings.pointsPerTier);

        public static bool TierOpen(PerkTree tree, int tier) => SpentAbove(tree, tier) >= NeededFor(tier);

        /// <summary>Why this perk can't take another rank now, or null when it can.</summary>
        public static string WhyNot(PerkBalance perk)
        {
            if (perk == null) return "Unknown perk.";
            if (Rank(perk) >= perk.maxRank) return "Maxed out.";
            if (!TierOpen(perk.tree, perk.tier))
            {
                int more = NeededFor(perk.tier) - SpentAbove(perk.tree, perk.tier);
                return $"Spend {more} more point{(more == 1 ? "" : "s")} in the tiers above.";
            }
            if (Available(TrackOf(perk.tree)) <= 0) return $"Needs a {Progression.TrackName(TrackOf(perk.tree))} perk point: level up to earn one.";
            return null;
        }

        public static bool TrySpend(PerkBalance perk)
        {
            var reason = WhyNot(perk);
            if (reason != null)
            {
                GameLog.Add(reason);
                return false;
            }
            ranks[perk.id] = Rank(perk) + 1;
            GameLog.Add($"New perk: {perk.name} (rank {Rank(perk)}/{perk.maxRank}). {perk.Describe(Rank(perk))}.");
            Changed?.Invoke();
            return true;
        }

        /// <summary>The total of every perk with this effect (0 when none are taken).</summary>
        public static float Value(PerkEffect effect)
        {
            float total = 0f;
            foreach (var perk in All)
                if (perk != null && perk.effect == effect) total += Rank(perk) * perk.perRank;
            return total;
        }

        /// <summary>Whole-number effects: extra wipes, spots, tanks and the like.</summary>
        public static int Count(PerkEffect effect) => Mathf.RoundToInt(Value(effect));

        /// <summary>1 + the effect: for things perks increase.</summary>
        public static float More(PerkEffect effect) => 1f + Value(effect);

        /// <summary>1 - the effect, never below 10%: for things perks reduce.</summary>
        public static float Less(PerkEffect effect) => Mathf.Max(0.1f, 1f - Value(effect));

        /// <summary>A price after a discount effect (at most 90% off).</summary>
        public static int Price(int price, PerkEffect discount) => price <= 0 ? price : Mathf.Max(1, Mathf.RoundToInt(price * Less(discount)));

        /// <summary>A supplier price: fish, eggs, food, equipment and care items.</summary>
        public static int Supplier(int price) => Price(price, PerkEffect.SupplierDiscount);

        /// <summary>A building price: tanks and new rooms.</summary>
        public static int Build(int price) => Price(price, PerkEffect.BuildDiscount);

        /// <summary>A tank upgrade: both building discounts.</summary>
        public static int Upgrade(int price) => Price(Build(price), PerkEffect.UpgradeDiscount);

        public static int Decoration(int price) => Price(price, PerkEffect.DecorDiscount);

        public static int MaxInterestPerNight => Settings.maxInterestPerNight;

#if UNITY_EDITOR
        /// <summary>Developer shortcut: takes back every perk so the points can be spent again.</summary>
        public static void DevRefund()
        {
            ranks.Clear();
            Changed?.Invoke();
        }
#endif
    }
}
