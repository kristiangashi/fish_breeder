using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Marks a collider (on this object or its children) as something fish swim around:
    /// rocks, plants, driftwood, castles. Fish ignore every collider without this.
    /// </summary>
    public class TankObstacle : MonoBehaviour
    {
        public static bool IsObstacle(Collider collider)
        {
            return collider != null && collider.GetComponentInParent<TankObstacle>() != null;
        }
    }
}
