using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>
    /// Something in the store that reacts to a click in the store view. Click volumes live on the
    /// Ignore Raycast layer so they never get in the way of fish, food or the tank view's own clicks.
    /// </summary>
    public class StoreClickable : MonoBehaviour
    {
        public enum Kind { Tank, EmptySlot, LockedSection, Register, Computer }

        public const int Layer = 2; // Ignore Raycast
        public const int Mask = 1 << Layer;

        public Kind kind;
        public StoreLayout.Slot slot;
        public StoreLayout.Section section;
        public StoreTank tank;

        public static StoreClickable Add(Transform parent, string name, Vector3 worldCenter, Vector3 size, Kind kind)
        {
            var go = new GameObject($"Click - {name}") { layer = Layer };
            go.transform.SetParent(parent, true);
            go.transform.position = worldCenter;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;
            var clickable = go.AddComponent<StoreClickable>();
            clickable.kind = kind;
            return clickable;
        }
    }
}
