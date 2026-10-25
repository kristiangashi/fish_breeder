using FishBreeder.Shop;
using UnityEngine;

namespace FishBreeder.People
{
    /// <summary>
    /// The fish seller at his stall: where egg and fry boxes are bought (equipment, food and
    /// decorations stay on the supplier computer). Stands with his arms folded, keeps an eye on
    /// you, beckons you over while the shop is shut, and shows off his stock when you buy.
    /// </summary>
    public class FishSeller : MonoBehaviour
    {
        public static FishSeller Instance { get; private set; }

        CrewRig rig;
        float idleTimer = 3f;
        float lookTimer;
        bool watchCustomer;
        static readonly Gesture[] WhileClosed = { Gesture.Beckon, Gesture.Wave, Gesture.LookAround, Gesture.Nod, Gesture.Think, Gesture.Beckon };
        static readonly Gesture[] WhileOpen = { Gesture.LookAround, Gesture.Nod, Gesture.Think, Gesture.Shrug };

        /// <summary>True while the shop is open (customers about); set by the store.</summary>
        public static bool ShopOpen;

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            rig = CrewModels.Spawn(CrewModels.FishSeller, transform, Customer.PersonScale);
            if (rig != null) rig.ArmsCrossed = true;
        }

        void Update()
        {
            if (rig == null) return;
            lookTimer -= Time.deltaTime;
            if (lookTimer <= 0f)
            {
                lookTimer = Random.Range(2f, 5f);
                watchCustomer = ShopOpen && Random.value < 0.6f;
            }
            Customer nearest = null;
            if (watchCustomer)
            {
                float best = 5f;
                foreach (var customer in Customer.All)
                {
                    if (customer == null) continue;
                    float d = Vector3.Distance(customer.transform.position, transform.position);
                    if (d < best)
                    {
                        best = d;
                        nearest = customer;
                    }
                }
            }
            var cam = Camera.main;
            rig.LookTarget = nearest != null ? nearest.FacePoint : cam != null && !watchCustomer ? cam.transform.position : (Vector3?)null;

            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f)
            {
                idleTimer = Random.Range(5f, 11f);
                var choices = ShopOpen ? WhileOpen : WhileClosed;
                if (!rig.Busy) rig.Play(choices[Random.Range(0, choices.Length)], cam != null ? cam.transform.position : (Vector3?)null);
            }
        }

        /// <summary>The player walked up to the stall.</summary>
        public void Greet()
        {
            var cam = Camera.main;
            if (rig != null) rig.Play(Gesture.Wave, cam != null ? cam.transform.position : (Vector3?)null);
            idleTimer = Random.Range(6f, 10f);
        }

        /// <summary>A box was bought: hold out the goods, then a thumbs up.</summary>
        public void Sold()
        {
            if (rig == null) return;
            var cam = Camera.main;
            rig.Play(Gesture.Present, cam != null ? cam.transform.position : (Vector3?)null);
            rig.Queue(Gesture.ThumbsUp, cam != null ? cam.transform.position : (Vector3?)null);
            idleTimer = Random.Range(6f, 10f);
        }
    }
}
