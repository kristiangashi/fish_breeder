using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;

namespace FishBreeder.Store
{
    /// <summary>What the supplier sells and the store's tank prices, read from the balance file.</summary>
    public static class SupplierCatalog
    {
        public static IReadOnlyList<Species> Fish => Species.All;

        public static List<FoodBalance> Food => Balance.Data.supplier.food;

        public static SupplierBalance Equipment => Balance.Data.supplier;

        /// <summary>A new tank's price, after building discounts.</summary>
        public static int TankPrice(TankRole role) => Perks.Build(BaseTankPrice(role));

        static int BaseTankPrice(TankRole role)
        {
            var store = Balance.Data.store;
            switch (role)
            {
                case TankRole.Breeder: return store.breederTankPrice;
                case TankRole.ForSale: return store.forSaleTankPrice;
                case TankRole.Display: return store.displayTankPrice;
                default: return store.quarantineTankPrice;
            }
        }

        /// <summary>The level that unlocks buying another tank of this kind.</summary>
        public static void TankLevel(TankRole role, out XpTrack track, out int level)
        {
            var store = Balance.Data.store;
            switch (role)
            {
                case TankRole.Breeder: track = XpTrack.Fishkeeping; level = store.breederTankLevel; break;
                case TankRole.Quarantine: track = XpTrack.Fishkeeping; level = store.quarantineTankLevel; break;
                case TankRole.Display: track = XpTrack.Shopkeeping; level = store.displayTankLevel; break;
                default: track = XpTrack.Shopkeeping; level = store.forSaleTankLevel; break;
            }
        }

        public static List<DecorationBalance> Decorations => Balance.Data.supplier.decorations;

        /// <summary>Most tanks of each kind the store can hold.</summary>
        public static int TankLimit(TankRole role)
        {
            var store = Balance.Data.store;
            switch (role)
            {
                case TankRole.Breeder: return store.maxBreederTanks;
                case TankRole.ForSale: return store.maxForSaleTanks + Perks.Count(PerkEffect.MoreShopTanks);
                case TankRole.Display: return store.maxDisplayTanks + Perks.Count(PerkEffect.MoreShopTanks);
                default: return store.maxQuarantineTanks;
            }
        }
    }
}
