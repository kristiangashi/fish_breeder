using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Marks a collider (on this object or its children) as something fish swim around:
    /// rocks, plants, driftwood, castles. Fish ignore every collider without this.
    /// Passable obstacles (grassy plants, see <see cref="PlantBender"/>) are swum through instead:
    /// fish still nose around them, but never steer away from them or get pushed out.
    /// </summary>
    public class TankObstacle : MonoBehaviour
    {
        static readonly List<TankObstacle> all = new List<TankObstacle>();

        /// <summary>Every active obstacle, for fish that want to go and nose around one.</summary>
        public static IReadOnlyList<TankObstacle> All => all;

        /// <summary>Fish swim through it (it bends out of the way) rather than round it.</summary>
        public bool passable;

        /// <summary>True for a collider fish must swim round (not a passable plant).</summary>
        public static bool IsObstacle(Collider collider)
        {
            if (collider == null) return false;
            var obstacle = collider.GetComponentInParent<TankObstacle>();
            return obstacle != null && !obstacle.passable;
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
