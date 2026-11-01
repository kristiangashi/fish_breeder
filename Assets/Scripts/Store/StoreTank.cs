using System.Collections.Generic;
using FishBreeder.Breeding;
using FishBreeder.Feeding;
using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>Saved by name, so new kinds can go anywhere. Exhibits are the public aquarium's two big tanks.</summary>
    public enum TankRole { Breeder, ForSale, Display, Quarantine, Exhibit }

    /// <summary>A tank placed in the store: what it's for, and the systems that act on it.</summary>
    public class StoreTank : MonoBehaviour
    {
        // Serialized so the references survive a script reload while playing.
        [SerializeField] TankRole role;
        [SerializeField] int number;
        [SerializeField] Tank tank;
        [SerializeField] FeedingSystem feeding;
        [SerializeField] BreedingSystem breeding;

        public TankRole Role => role;
        public int Number => number;
        public Tank Tank => tank;
        public FeedingSystem Feeding => feeding;
        public BreedingSystem Breeding => breeding;
        /// <summary>Where it stands in the store.</summary>
        public StoreLayout.Slot Slot { get; set; }
        /// <summary>The stand and click volume under the tank, rebuilt when the tank changes size.</summary>
        public GameObject Stand { get; set; }
        public bool InBackRoom => Slot != null && Slot.backRoom;

        [SerializeField] string customName;
        /// <summary>The name the player gave it, or null for the usual "Breeder tank 1".</summary>
        public string CustomName => string.IsNullOrEmpty(customName) ? null : customName;
        public string Title => CustomName ?? (Role == TankRole.Quarantine ? "Quarantine tank" : Role == TankRole.Exhibit ? $"{PublicAquarium.ClimateName(Climate)} aquarium".TrimStart() : $"{RoleName(Role)} tank {Number}");

        [SerializeField] ExhibitClimate climate;
        /// <summary>For an exhibit, which fish it takes (temperate or tropical).</summary>
        public ExhibitClimate Climate => climate;

        /// <summary>Makes it the public aquarium's temperate or tropical exhibit (None for any other tank).</summary>
        public void SetClimate(ExhibitClimate value)
        {
            climate = value;
            tank.Climate = value;
            gameObject.name = Title;
            tank.Title = Title;
        }

        /// <summary>Gives the tank the player's own name (blank goes back to the usual one).</summary>
        public void Rename(string name)
        {
            customName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            gameObject.name = Title;
            tank.Title = Title;
        }

        public static string RoleName(TankRole role) => role == TankRole.ForSale ? "For Sale" : role.ToString();

        /// <summary>Turns the tank into another type, numbered among its new kind (the caller checks it's allowed and pays).</summary>
        public void SetRole(TankRole newRole, int newNumber)
        {
            role = newRole;
            number = newNumber;
            gameObject.name = Title;
            tank.Title = Title;
            tank.PricesNewFish = role == TankRole.ForSale;
            if (tank.PricesNewFish)
                foreach (var fish in tank.Residents)
                    if (fish.Price <= 0) fish.Price = fish.AppraisedValue;
        }

        /// <summary>Builds a tank with its feeding and breeding systems at <paramref name="position"/> (the sand level), turned <paramref name="yaw"/> degrees (its front faces -Z at 0).</summary>
        public static StoreTank Create(TankRole role, int number, Vector3 position, float yaw, Transform parent, int size, IEnumerable<string> decorations)
        {
            var go = new GameObject($"{RoleName(role)} tank {number}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var storeTank = go.AddComponent<StoreTank>();
            storeTank.role = role;
            storeTank.number = number;
            storeTank.tank = go.AddComponent<Tank>();
            storeTank.tank.Title = storeTank.Title;
            storeTank.tank.Build(size, decorations);
            storeTank.tank.PricesNewFish = role == TankRole.ForSale;
            storeTank.feeding = go.AddComponent<FeedingSystem>();
            storeTank.feeding.Initialize(storeTank.tank);
            storeTank.breeding = go.AddComponent<BreedingSystem>();
            storeTank.breeding.Initialize(storeTank.tank);
            return storeTank;
        }
    }
}
