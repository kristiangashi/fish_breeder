using System.Collections;
using FishBreeder.Core;
using FishBreeder.People;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Shop
{
    /// <summary>
    /// A visitor to the public aquarium (the same people as shoppers): queues at the ticket booth,
    /// pays or walks off at the price, then strolls between the exhibits, stopping along the glass
    /// to watch the fish and react, and leaves by the aquarium's own door.
    /// </summary>
    public partial class Customer
    {
        AquariumDay aquarium;
        float sightSum;
        int looks;
        float willing;
        bool admitted;

        /// <summary>Here for the public aquarium rather than the shop.</summary>
        public bool IsVisitor => aquarium != null;

        public static Customer SpawnVisitor(AquariumDay aquarium, StoreLayout layout)
        {
            var customer = Create(null, layout, layout.AquariumDoorOutside);
            customer.aquarium = aquarium;
            customer.StartCoroutine(customer.Visit());
            return customer;
        }

        IEnumerator Visit()
        {
            aquarium.JoinQueue(this);
            while (true)
            {
                int place = aquarium.QueuePlace(this);
                yield return WalkTo(layout.BoothQueueSpot(place));
                faceTarget = Quaternion.LookRotation(place == 0 ? layout.BoothFacing : Vector3.forward);
                if (place == 0) break;
                while (aquarium.QueuePlace(this) == place) yield return null;
            }
            yield return new WaitForSeconds(PublicAquarium.Data.serveSeconds);
            admitted = aquarium.TryAdmit(layout.BoothWindowPoint, out willing);
            aquarium.LeaveQueue(this);
            faceTarget = null;
            if (!admitted)
            {
                // Too dear: hands on hips at the price, and off home.
                ShowMood(Mood.Mad);
                if (rig != null) rig.Play(Gesture.Mad, layout.BoothWindowPoint);
                yield return new WaitForSeconds(1.6f);
                yield return Leave();
                yield break;
            }
            if (rig != null) rig.Play(Gesture.Nod, layout.BoothWindowPoint);

            yield return WalkTo(layout.AquariumDoorInside);
            var d = PublicAquarium.Data;
            int planned = GameRandom.Range(d.minLooks, d.maxLooks + 1);
            int start = GameRandom.Range(0, 2);
            for (int i = 0; i < planned; i++)
            {
                var exhibits = aquarium.OpenExhibits();
                if (exhibits.Count == 0) break;
                var exhibit = exhibits[(start + i) % exhibits.Count];
                // Somewhere along the glass of the big tank, not all on one spot.
                float along = exhibit.Tank.InnerSize.x * 0.4f;
                yield return WalkTo(StoreLayout.ViewingSpot(exhibit) + StoreLayout.AlongGlass(exhibit, GameRandom.Range(-along, along)));
                faceTarget = Quaternion.LookRotation(-exhibit.Tank.Front);
                watching = exhibit.Tank;
                yield return new WaitForSeconds(GameRandom.Range(1f, 2f));
                var look = PublicAquarium.LookAt(exhibit.Tank);
                aquarium.RecordLook(look);
                sightSum += look.score;
                looks++;
                if (look.mood != Mood.None) ShowMood(look.mood);
                React(look.mood, exhibit.Tank, null);
                yield return new WaitForSeconds(GameRandom.Range(d.minLookSeconds, d.maxLookSeconds));
                faceTarget = null;
                watching = null;
            }
            FinishVisit();
            yield return Leave();
        }

        /// <summary>Their verdict on the visit: once, as they leave (or when the aquarium closes on them).</summary>
        void FinishVisit()
        {
            if (!admitted) return;
            admitted = false;
            float sight = looks > 0 ? sightSum / looks : PublicAquarium.AquariumAppeal(layout) / 100f;
            float score = aquarium.RecordVisit(sight, willing);
            if (score >= PublicAquarium.Data.happyScore)
            {
                ShowMood(Mood.Happy);
                if (rig != null) rig.Play(Gesture.Cheer, null);
            }
            else if (score < PublicAquarium.Data.unhappyScore)
            {
                ShowMood(Mood.Sad);
                if (rig != null) rig.Play(Gesture.HeadShake, null);
            }
        }
    }
}
