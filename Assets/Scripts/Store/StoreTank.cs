using FishBreeder.Breeding;
using FishBreeder.Feeding;
using UnityEngine;

namespace FishBreeder.Store
{
    public enum TankRole { Breeder, ForSale, Display, Quarantine }

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

        public string Title => Role == TankRole.Quarantine ? "Quarantine tank" : $"{RoleName(Role)} tank {Number}";

        public static string RoleName(TankRole role) => role == TankRole.ForSale ? "For Sale" : role.ToString();

        /// <summary>Builds a tank with its feeding and breeding systems at <paramref name="position"/> (the sand level).</summary>
        public static StoreTank Create(TankRole role, int number, Vector3 position, Transform parent)
        {
            var go = new GameObject($"{RoleName(role)} tank {number}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var storeTank = go.AddComponent<StoreTank>();
            storeTank.role = role;
            storeTank.number = number;
            storeTank.tank = go.AddComponent<Tank>();
            storeTank.tank.BuildPlaceholderVisuals();
            storeTank.tank.PricesNewFish = role == TankRole.ForSale;
            storeTank.feeding = go.AddComponent<FeedingSystem>();
            storeTank.feeding.Initialize(storeTank.tank);
            storeTank.breeding = go.AddComponent<BreedingSystem>();
            storeTank.breeding.Initialize(storeTank.tank);
            return storeTank;
        }
    }
}
