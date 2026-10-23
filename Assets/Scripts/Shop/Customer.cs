using System.Collections;
using FishBreeder.Core;
using FishBreeder.Store;
using UnityEngine;
using UnityEngine.AI;

namespace FishBreeder.Shop
{
    /// <summary>
    /// A shopper: walks in, looks at a few random tanks (showing a thought bubble now and then),
    /// may pick a fish from a For Sale tank, pays at the register and walks home.
    /// Placeholder body built from primitives, like the rest of the store.
    /// </summary>
    public class Customer : MonoBehaviour
    {
        ShopDay day;
        StoreLayout layout;
        NavMeshAgent agent;
        Transform body;
        Transform handPoint;
        ShopperProfile profile;
        Fish purchase;
        float walkPhase;
        Quaternion? faceTarget;

        public Mood Mood { get; private set; }
        public float MoodTimeLeft { get; private set; }
        public Vector3 HeadPoint => transform.position + Vector3.up * 2.05f;
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
                agent.radius = 0.2f;
                agent.height = 1.7f;
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

        void BuildBody()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
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
            int visits = GameRandom.Range(shop.minTankVisits, shop.maxTankVisits + 1);
            StoreTank last = null;
            for (int i = 0; i < visits && purchase == null; i++)
            {
                var tanks = layout.Tanks;
                if (tanks.Count == 0) break;
                int index = GameRandom.Range(0, tanks.Count);
                if (tanks[index] == last && tanks.Count > 1) index = (index + 1) % tanks.Count;
                var tank = tanks[index];
                last = tank;

                yield return WalkTo(StoreLayout.ViewingSpot(tank) + Vector3.right * GameRandom.Range(-0.55f, 0.55f));
                faceTarget = Quaternion.LookRotation(Vector3.forward);
                yield return new WaitForSeconds(GameRandom.Range(0.8f, 1.6f));

                var reaction = ShopperBrain.Evaluate(profile, tank);
                day.RecordMood(reaction.mood);
                if (reaction.mood != Mood.None) ShowMood(reaction.mood);
                purchase = reaction.purchase;
                yield return new WaitForSeconds(GameRandom.Range(shop.minLookSeconds, shop.maxLookSeconds));
                faceTarget = null;
            }

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
                faceTarget = Quaternion.LookRotation(Vector3.back);
                if (place == 0) break;
                while (day.QueuePlace(this) == place) yield return null;
            }

            yield return day.HandOverBag(this, handPoint);
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
                agent.isStopped = true;
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

            // A little bob and sway while walking.
            walkPhase += speed * Time.deltaTime * 7f;
            float moving = Mathf.Clamp01(speed / 0.5f);
            body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(walkPhase)) * 0.04f * moving, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(walkPhase) * 3f * moving);
        }
    }
}
