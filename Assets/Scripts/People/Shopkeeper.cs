using System.Collections.Generic;
using FishBreeder.Shop;
using UnityEngine;

namespace FishBreeder.People
{
    /// <summary>
    /// You, behind the register: the owner the player picked. Waves at people coming in, watches
    /// whoever is nearest, hands over the bag and gives a thumbs up or a clap after a sale, and fills
    /// quiet moments wiping the counter, stretching and looking around.
    /// </summary>
    public class Shopkeeper : MonoBehaviour
    {
        public static Shopkeeper Instance { get; private set; }

        GameObject placeholder;
        CrewRig rig;
        float idleTimer = 5f;
        readonly HashSet<Customer> greeted = new HashSet<Customer>();
        static readonly Gesture[] QuietMoments = { Gesture.Wipe, Gesture.Stretch, Gesture.LookAround, Gesture.Wipe, Gesture.Think };

        /// <summary>Puts the owner model in; the primitive body stays hidden unless the model is missing.</summary>
        public void Build(GameObject placeholderBody)
        {
            placeholder = placeholderBody;
            Rebuild();
        }

        void OnEnable()
        {
            Instance = this;
            ShopOwner.Changed += Rebuild;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            ShopOwner.Changed -= Rebuild;
        }

        void Rebuild()
        {
            if (rig != null) Destroy(rig.gameObject);
            rig = CrewModels.Spawn(ShopOwner.ModelId, transform, Customer.PersonScale);
            if (placeholder != null) placeholder.SetActive(rig == null);
        }

        void Update()
        {
            if (rig == null) return;
            greeted.RemoveWhere(c => c == null);

            Customer nearest = null;
            float best = 6f;
            foreach (var customer in Customer.All)
            {
                if (customer == null) continue;
                float d = Vector3.Distance(customer.transform.position, transform.position);
                if (!greeted.Contains(customer) && !customer.IsLeaving)
                {
                    greeted.Add(customer);
                    if (!rig.Busy) rig.Play(Gesture.Wave, customer.FacePoint);
                }
                if (d < best)
                {
                    best = d;
                    nearest = customer;
                }
            }
            rig.LookTarget = nearest != null ? nearest.FacePoint : (Vector3?)null;

            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f)
            {
                idleTimer = Random.Range(7f, 14f);
                if (!rig.Busy) rig.Play(nearest != null ? Gesture.Nod : QuietMoments[Random.Range(0, QuietMoments.Length)]);
            }
        }

        /// <summary>Reaches the bag over the counter, then a little celebration.</summary>
        public void HandOver(Customer customer)
        {
            if (rig == null || customer == null) return;
            rig.Play(Gesture.HandOver, customer.FacePoint);
            rig.Queue(Random.value < 0.5f ? Gesture.ThumbsUp : Gesture.Clap, customer.FacePoint);
        }
    }
}
