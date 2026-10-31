using System.Collections.Generic;
using FishBreeder.Feeding;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// How much of a tank's fish life is worked out this frame, so a store full of aquariums only
    /// pays for the ones you're looking at:
    /// - the tank you're in, or one that fills a good part of the screen, runs every frame;
    /// - tanks further off on screen run every 2nd or 3rd frame (half or a third of the fish each
    ///   frame, so the work stays even);
    /// - tanks off screen are asleep: their fish hold still until the camera comes back.
    /// A tank with food in it never sleeps, so fish don't miss a meal because you walked away.
    /// Nothing here touches the game's rules: hunger, illness and growth move on at time steps.
    /// </summary>
    public static class TankActivity
    {
        /// <summary>Screen height share a tank needs to run every frame in the store view.</summary>
        const float FullShare = 0.6f;
        /// <summary>Below this share, every 3rd frame; between the two, every 2nd.</summary>
        const float ThirdShare = 0.22f;

        static readonly Dictionary<Tank, int> steps = new Dictionary<Tank, int>();
        static readonly Plane[] planes = new Plane[6];
        static int frame = -1;
        static Camera cam;

        /// <summary>
        /// 0 if the tank is asleep this frame, otherwise how many frames go by between updates of
        /// each fish (1 = every frame).
        /// </summary>
        public static int Step(Tank tank)
        {
            if (tank == null) return 1;
            if (frame != Time.frameCount)
            {
                frame = Time.frameCount;
                steps.Clear();
                cam = Camera.main;
                if (cam != null) GeometryUtility.CalculateFrustumPlanes(cam, planes);
            }
            if (!steps.TryGetValue(tank, out int step))
                steps[tank] = step = Work(tank);
            return step;
        }

        /// <summary>True if this fish (given a steady per-fish slot) gets its update on this frame.</summary>
        public static bool IsTurn(int step, int slot) => step <= 1 || (Time.frameCount + slot) % step == 0;

        static int Work(Tank tank)
        {
            if (cam == null || tank == Tank.Focused) return 1;
            var bounds = tank.InnerBounds;
            bool fed = HasFood(tank);
            if (!GeometryUtility.TestPlanesAABB(planes, bounds)) return fed ? 3 : 0;

            // How much of the screen's height the tank takes up.
            float distance = Mathf.Max(0.1f, Vector3.Distance(cam.transform.position, bounds.ClosestPoint(cam.transform.position)));
            float share = bounds.size.y / (2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            if (Tank.Focused == null && share >= FullShare) return 1;
            return share >= ThirdShare ? 2 : 3;
        }

        static bool HasFood(Tank tank)
        {
            foreach (var pellet in FoodPellet.Active)
                if (pellet != null && pellet.Tank == tank && !pellet.IsConsumed) return true;
            return false;
        }
    }
}
