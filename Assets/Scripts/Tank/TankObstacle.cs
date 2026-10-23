using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Marks a collider (on this object or its children) as something fish swim around:
    /// rocks, plants, driftwood, castles. Fish ignore every collider without this.
    /// </summary>
    public class TankObstacle : MonoBehaviour
    {
        static readonly List<TankObstacle> all = new List<TankObstacle>();

        /// <summary>Every active obstacle, for fish that want to go and nose around one.</summary>
        public static IReadOnlyList<TankObstacle> All => all;

        public static bool IsObstacle(Collider collider)
        {
            return collider != null && collider.GetComponentInParent<TankObstacle>() != null;
        }

        /// <summary>World bounds of all this obstacle's colliders.</summary>
        public Bounds Bounds
        {
            get
            {
                var colliders = GetComponentsInChildren<Collider>();
                if (colliders.Length == 0) return new Bounds(transform.position, Vector3.zero);
                var bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
                return bounds;
            }
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);
    }
}
