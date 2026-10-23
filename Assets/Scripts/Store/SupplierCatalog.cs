using FishBreeder.Feeding;
using FishBreeder.Genetics;

namespace FishBreeder.Store
{
    /// <summary>What the supplier sells and for how much. All prices are placeholders to balance later.</summary>
    public static class SupplierCatalog
    {
        public readonly struct FishOffer
        {
            public readonly Species species;
            public readonly int clutchPrice, clutchSize, fryPrice;

            public FishOffer(Species species, int clutchPrice, int clutchSize, int fryPrice)
            {
                this.species = species;
                this.clutchPrice = clutchPrice;
                this.clutchSize = clutchSize;
                this.fryPrice = fryPrice;
            }
        }

        public readonly struct FoodOffer
        {
            public readonly FoodType type;
            public readonly string name, blurb;
            public readonly int price, portions;

            public FoodOffer(FoodType type, string name, string blurb, int price, int portions)
            {
                this.type = type;
                this.name = name;
                this.blurb = blurb;
                this.price = price;
                this.portions = portions;
            }
        }

        /// <summary>Per species: price of a clutch, eggs in it, price of one fry.</summary>
        public static readonly FishOffer[] Fish =
        {
            new FishOffer(Species.Get("CometGoldfish"), 20, 4, 12),
            new FishOffer(Species.Get("FancyGoldfish"), 40, 3, 25),
            new FishOffer(Species.Get("Angelfish"), 60, 3, 35),
            new FishOffer(Species.Get("Betta"), 50, 3, 30),
            new FishOffer(Species.Get("Clownfish"), 90, 3, 55),
            new FishOffer(Species.Get("Guppy"), 15, 5, 8),
            new FishOffer(Species.Get("TigerBarb"), 25, 4, 14),
            new FishOffer(Species.Get("NeonTetra"), 20, 5, 10),
            new FishOffer(Species.Get("ZebraDanio"), 15, 5, 8),
            new FishOffer(Species.Get("Corydoras"), 35, 4, 20),
        };

        public static readonly FoodOffer[] Food =
        {
            new FoodOffer(FoodType.Flakes, "Flakes tub", "Cheap. Now and then a spoiled flake makes a fish sick.", 15, 20),
            new FoodOffer(FoodType.Premium, "Premium pellets", "More filling and much less likely to cause illness.", 45, 20),
        };

        public const int FilterPrice = 150;

        public static int TankPrice(TankRole role)
        {
            switch (role)
            {
                case TankRole.Breeder: return 350;
                case TankRole.ForSale: return 250;
                case TankRole.Display: return 500;
                default: return 200;
            }
        }

        /// <summary>Most tanks of each kind the store can hold.</summary>
        public static int TankLimit(TankRole role)
        {
            switch (role)
            {
                case TankRole.Breeder: return 3;
                case TankRole.Quarantine: return 1;
                default: return 6;
            }
        }
    }
}
