using FishBreeder.Breeding;
using FishBreeder.Feeding;
using UnityEngine;

namespace FishBreeder.Store
{
    public enum TankRole { Breeder, ForSale, Display, Quarantine }

    /// <summary>A tank placed in the store: what it's for, and the systems that act on it.</summary>
    public class StoreTank : MonoBehaviour
    {
        public TankRole Role { get; private set; }
        public int Number { get; private set; }
        public Tank Tank { get; private set; }
        public FeedingSystem Feeding { get; private set; }
        public BreedingSystem Breeding { get; private set; }

        public string Title => Role == TankRole.Quarantine ? "Quarantine tank" : $"{RoleName(Role)} tank {Number}";

        public static string RoleName(TankRole role) => role == TankRole.ForSale ? "For Sale" : role.ToString();

        /// <summary>Builds a tank with its feeding and breeding systems at <paramref name="position"/> (the sand level).</summary>
        public static StoreTank Create(TankRole role, int number, Vector3 position, Transform parent)
        {
            var go = new GameObject($"{RoleName(role)} tank {number}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var storeTank = go.AddComponent<StoreTank>();
            storeTank.Role = role;
            storeTank.Number = number;
            storeTank.Tank = go.AddComponent<Tank>();
            storeTank.Tank.BuildPlaceholderVisuals();
            storeTank.Feeding = go.AddComponent<FeedingSystem>();
            storeTank.Feeding.Initialize(storeTank.Tank);
            storeTank.Breeding = go.AddComponent<BreedingSystem>();
            storeTank.Breeding.Initialize(storeTank.Tank);
            return storeTank;
        }
    }
}
