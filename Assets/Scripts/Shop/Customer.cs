using System.Collections;
using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.People;
using FishBreeder.Store;
using UnityEngine;
using UnityEngine.AI;

namespace FishBreeder.Shop
{
    /// <summary>
    /// A shopper: walks in, looks at a few random tanks (showing a thought bubble now and then),
    /// may pick a fish from a For Sale tank, pays at the register and walks home.
    /// One of the crew lineup's customer models, acting out what they think of each tank (pointing
    /// at a fish and going "wow", leaning in, shaking their head at a dead fish, holding their nose
    /// at dirty water, hands on hips at the prices). Falls back to a primitive body without models.
    /// </summary>
    public class Customer : MonoBehaviour
    {
        /// <summary>People are drawn a bit smaller than life so the tanks and the store read bigger.</summary>
        public const float PersonScale = 0.9f;

        static readonly List<Customer> all = new List<Customer>();
        /// <summary>Everyone in or around the shop right now.</summary>
        public static IReadOnlyList<Customer> All => all;

        ShopDay day;
        StoreLayout layout;
        NavMeshAgent agent;
        Transform body;
        CrewRig rig;
        Transform handPoint;
        Tank watching;
        Fish glanceFish;
        float fishGlance;
        ShopperProfile profile;
        Fish purchase;
        float walkPhase;
        Quaternion? faceTarget;

        public Mood Mood { get; private set; }
        public float MoodTimeLeft { get; private set; }
        /// <summary>Above the head, where thought bubbles go.</summary>
        public Vector3 HeadPoint => transform.position + Vector3.up * (rig != null ? rig.Height + 0.35f : 2.05f * PersonScale);
        /// <summary>The face, for other people to look at.</summary>
        public Vector3 FacePoint => transform.position + Vector3.up * (rig != null ? rig.Height * 0.9f : 1.62f * PersonScale);
        public bool IsLeaving { get; private set; }

        public static Customer Spawn(ShopDay day, StoreLayout layout)
        {
            // Built inactive so the agent gets the right agent type before it looks for the walkable area.
            var go = new GameObject("Customer");
            go.SetActive(false);
            go.transform.position = layout.DoorOutside;
            go.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var customer = go.AddComponent<Customer>();
            customer.day = day;
            customer.layout = layout;
            customer.profile = ShopperProfile.Random();
            customer.BuildBody();

            NavMeshAgent agent = null;
            if (StoreNavMesh.Ready)
            {
                agent = go.AddComponent<NavMeshAgent>();
                agent.agentTypeID = StoreNavMesh.AgentTypeId;
                agent.radius = 0.2f * PersonScale;
                agent.height = 1.7f * PersonScale;
                agent.speed = Balance.Data.shop.walkSpeed * GameRandom.Range(0.85f, 1.15f);
                agent.angularSpeed = 400f;
                agent.acceleration = 6f;
                agent.stoppingDistance = 0.05f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            }
            go.SetActive(true);
            if (agent != null)
            {
                if (agent.Warp(layout.DoorOutside)) customer.agent = agent;
                else Destroy(agent);
            }
            customer.StartCoroutine(customer.Run());
            return customer;
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void BuildBody()
        {
            rig = CrewModels.Spawn(CrewModels.Customers[GameRandom.Range(0, CrewModels.Customers.Length)], transform, PersonScale);
            if (rig != null)
            {
                body = rig.transform;
                handPoint = rig.RightHand;
                return;
            }
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            body.localScale = Vector3.one * PersonScale;
            var shirt = Color.HSVToRGB(GameRandom.Value, GameRandom.Range(0.35f, 0.75f), GameRandom.Range(0.55f, 0.9f));
            var trousers = Color.HSVToRGB(GameRandom.Range(0.55f, 0.7f), GameRandom.Range(0.1f, 0.5f), GameRandom.Range(0.2f, 0.45f));
            var skin = Color.Lerp(new Color(0.98f, 0.82f, 0.68f), new Color(0.45f, 0.3f, 0.2f), GameRandom.Value);
            MaterialFactory.Decor(PrimitiveType.Cube, "Legs", body, new Vector3(0f, 0.42f, 0f), new Vector3(0.32f, 0.84f, 0.2f), trousers);
            MaterialFactory.Decor(PrimitiveType.Capsule, "Torso", body, new Vector3(0f, 1.17f, 0f), new Vector3(0.44f, 0.36f, 0.28f), shirt);
            MaterialFactory.Decor(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 1.62f, 0f), new Vector3(0.24f, 0.27f, 0.24f), skin);
            MaterialFactory.Decor(PrimitiveType.Capsule, "ArmL", body, new Vector3(-0.27f, 1.1f, 0f), new Vector3(0.1f, 0.3f, 0.1f), shirt);
            MaterialFactory.Decor(PrimitiveType.Capsule, "ArmR", body, new Vector3(0.27f, 1.1f, 0f), new Vector3(0.1f, 0.3f, 0.1f), shirt);
            handPoint = new GameObject("Hand").transform;
            handPoint.SetParent(body, false);
            handPoint.localPosition = new Vector3(0.3f, 0.78f, 0.08f);
        }

        IEnumerator Run()
        {
            yield return WalkTo(layout.DoorInside);

            var shop = Balance.Data.shop;
            int visits = GameRandom.Range(shop.minTankVisits, shop.maxTankVisits + 1) + Perks.Count(PerkEffect.ExtraTankVisits);
            StoreTank last = null;
            for (int i = 0; i < visits && purchase == null; i++)
            {
                var tanks = layout.PublicTanks;
                if (tanks.Count == 0) break;
                int index = GameRandom.Range(0, tanks.Count);
                if (tanks[index] == last && tanks.Count > 1) index = (index + 1) % tanks.Count;
                var tank = tanks[index];
                last = tank;

                yield return WalkTo(StoreLayout.ViewingSpot(tank) + StoreLayout.AlongGlass(tank, GameRandom.Range(-0.55f, 0.55f)));
                faceTarget = Quaternion.LookRotation(-tank.Tank.Front);
                watching = tank.Tank;
                yield return new WaitForSeconds(GameRandom.Range(0.8f, 1.6f));

                var reaction = ShopperBrain.Evaluate(profile, tank);
                day.Record(reaction);
                if (reaction.mood != Mood.None) ShowMood(reaction.mood);
                purchase = reaction.purchase;
                React(reaction.mood, tank.Tank, reaction.purchase);
                yield return new WaitForSeconds(GameRandom.Range(shop.minLookSeconds, shop.maxLookSeconds));
                faceTarget = null;
                watching = null;
            }

            day.RecordWish(profile, purchase != null);
            if (purchase != null) yield return BuyAtRegister();
            yield return Leave();
        }

        IEnumerator BuyAtRegister()
        {
            day.JoinQueue(this);
            while (true)
            {
                int place = day.QueuePlace(this);
                yield return WalkTo(layout.RegisterQueueSpot(place));
                faceTarget = Quaternion.LookRotation(place == 0 ? layout.RegisterFacing : Vector3.back);
                if (place == 0) break;
                while (day.QueuePlace(this) == place) yield return null;
            }

            yield return day.HandOverBag(this, handPoint);
            if (rig != null) rig.Play(GameRandom.Value < 0.5f ? Gesture.Cheer : Gesture.Wow, Shopkeeper.Instance != null ? Shopkeeper.Instance.transform.position + Vector3.up * 1.3f : (Vector3?)null);
            CompletePurchase();
            day.LeaveQueue(this);
            faceTarget = null;
        }

        void CompletePurchase()
        {
            if (purchase == null) return;
            if (!purchase.IsDead) day.Sell(purchase, layout.RegisterPoint);
            purchase = null;
        }

        IEnumerator Leave()
        {
            IsLeaving = true;
            yield return WalkTo(layout.DoorInside);
            // A wave goodbye to the shopkeeper now and then.
            if (rig != null && Shopkeeper.Instance != null && GameRandom.Value < 0.4f)
            {
                faceTarget = Quaternion.LookRotation(Flat(Shopkeeper.Instance.transform.position - transform.position));
                yield return new WaitForSeconds(0.3f);
                rig.Play(Gesture.Wave, Shopkeeper.Instance.transform.position + Vector3.up * 1.3f);
                yield return new WaitForSeconds(1.6f);
                faceTarget = null;
            }
            yield return WalkTo(layout.DoorOutside);
            day.Remove(this);
            Destroy(gameObject);
        }

        /// <summary>The shop is closing: any fish they picked is paid for right away, then they're gone.</summary>
        public void FinishNow()
        {
            CompletePurchase();
            Destroy(gameObject);
        }

        /// <summary>Acts out what they think of a tank, aimed at a fish in it when there is one.</summary>
        void React(Mood mood, Tank tank, Fish picked)
        {
            if (rig == null) return;
            var target = picked != null ? picked.transform.position : SomeFish(tank);
            switch (mood)
            {
                case Mood.Sad: rig.Play(Gesture.HeadShake, target); break;
                case Mood.Grossed: rig.Play(Gesture.Grossed, target); break;
                case Mood.Mad: rig.Play(Gesture.Mad, target); break;
                case Mood.Happy: rig.Play(GameRandom.Value < 0.7f ? Gesture.PointAndWow : Gesture.Wow, target); break;
                default:
                    float roll = GameRandom.Value;
                    rig.Play(roll < 0.3f ? Gesture.LeanIn : roll < 0.55f ? Gesture.PointAndWow : roll < 0.8f ? Gesture.Think : Gesture.Nod, target);
                    break;
            }
        }

        static Vector3 SomeFish(Tank tank)
        {
            var living = new List<Fish>();
            foreach (var fish in tank.Residents) if (fish != null && !fish.IsDead) living.Add(fish);
            return living.Count > 0 ? living[GameRandom.Range(0, living.Count)].transform.position : tank.InnerBounds.center;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v : Vector3.forward;
        }

        void ShowMood(Mood mood)
        {
            Mood = mood;
            MoodTimeLeft = 3.5f;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        IEnumerator WalkTo(Vector3 target)
        {
            float timeout = 40f;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                if (!agent.SetDestination(target)) yield break;
                while (timeout > 0f && agent.pathPending)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }
                // remainingDistance can still hold the previous walk's value on the first frames of a
                // new path, so measure to the path's end (the target, or the nearest reachable point).
                // Someone standing on the spot can block it; close enough and stuck counts as arrived.
                float stuck = 0f;
                while (timeout > 0f && FlatDistance(transform.position, agent.pathEndPosition) > 0.15f)
                {
                    bool blocked = agent.velocity.sqrMagnitude < 0.01f && FlatDistance(transform.position, agent.pathEndPosition) < 0.8f;
                    stuck = blocked ? stuck + Time.deltaTime : 0f;
                    if (stuck > 1.5f) break;
                    timeout -= Time.deltaTime;
                    yield return null;
                }
                // The shop can close (or the day be skipped) mid-walk and take the agent off the walkable area.
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
                yield break;
            }

            // No walkable area: straight line.
            target.y = 0f;
            while (timeout > 0f && (transform.position - target).sqrMagnitude > 0.01f)
            {
                var step = Vector3.MoveTowards(transform.position, target, Balance.Data.shop.walkSpeed * Time.deltaTime);
                var direction = step - transform.position;
                if (direction.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(direction);
                transform.position = step;
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        void Update()
        {
            if (MoodTimeLeft > 0f)
            {
                // Real time, so bubbles stay readable when the day runs at 5x or 20x.
                MoodTimeLeft -= Time.unscaledDeltaTime;
                if (MoodTimeLeft <= 0f) Mood = Mood.None;
            }

            float speed = agent != null ? agent.velocity.magnitude : 0f;
            if (faceTarget.HasValue && speed < 0.1f)
                transform.rotation = Quaternion.Slerp(transform.rotation, faceTarget.Value, Time.deltaTime * 6f);

            if (rig != null)
            {
                rig.Speed = speed;
                // At a tank, the eyes follow one fish after another.
                fishGlance -= Time.deltaTime;
                if (watching != null && fishGlance <= 0f)
                {
                    fishGlance = GameRandom.Range(1.2f, 2.5f);
                    glanceFish = null;
                    var living = new List<Fish>();
                    foreach (var fish in watching.Residents) if (fish != null && !fish.IsDead) living.Add(fish);
                    if (living.Count > 0) glanceFish = living[GameRandom.Range(0, living.Count)];
                }
                rig.LookTarget = watching == null ? (Vector3?)null : glanceFish != null ? glanceFish.transform.position : watching.InnerBounds.center;
            }
            else
            {
                // A little bob and sway while walking.
                walkPhase += speed * Time.deltaTime * 7f;
                float moving = Mathf.Clamp01(speed / 0.5f);
                body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(walkPhase)) * 0.04f * PersonScale * moving, 0f);
                body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(walkPhase) * 3f * moving);
            }

            // In a tank close-up, someone standing between the camera and the glass would fill the screen.
            bool hidden = BlocksCloseUp(Tank.Focused);
            if (body.gameObject.activeSelf == hidden) body.gameObject.SetActive(!hidden);
        }

        bool BlocksCloseUp(Tank tank)
        {
            var cam = Camera.main;
            if (tank == null || cam == null) return false;
            var bounds = tank.InnerBounds;
            Vector2 from = new Vector2(cam.transform.position.x, cam.transform.position.z);
            Vector2 to = new Vector2(bounds.center.x, bounds.min.z);
            Vector2 me = new Vector2(transform.position.x, transform.position.z);
            Vector2 view = to - from;
            float along = Vector2.Dot(me - from, view) / Mathf.Max(1e-4f, view.sqrMagnitude);
            if (along < -0.1f || along > 1f) return false;
            float side = Mathf.Abs(me.x - Mathf.Lerp(from.x, to.x, along));
            return side < bounds.extents.x + 0.6f;
        }
    }
}
