using FishBreeder.Core;
using FishBreeder.Feeding;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Wanders around the tank, chases food when hungry and steers around decorations.
    /// The fish always moves along its own forward axis and turns at a limited rate,
    /// which reads as real swimming. Obstacles are colliders marked with <see cref="TankObstacle"/>.
    /// </summary>
    [RequireComponent(typeof(Fish))]
    public class FishSwimmer : MonoBehaviour
    {
        [SerializeField] float turnDegreesPerSecond = 110f;
        [SerializeField] float acceleration = 0.4f;
        [SerializeField] float maxPitch = 35f;
        [SerializeField] float foodSenseRadius = 1.5f;
        [Tooltip("How far ahead to look for obstacles, in body lengths (plus a little per unit of speed).")]
        [SerializeField] float lookAheadBodyLengths = 2.5f;
        [Tooltip("How strongly Owner keep away from the glass, in body lengths.")]
        [SerializeField] float wallComfortBodyLengths = 1.2f;

        static readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        static readonly Collider[] overlapBuffer = new Collider[16];

        // Probe directions in local space: straight, then fanning out left/right/up/down.
        static readonly Vector3[] probeDirections =
        {
            new Vector3(0f, 0f, 1f),
            new Vector3(-0.5f, 0f, 0.87f), new Vector3(0.5f, 0f, 0.87f),
            new Vector3(0f, 0.4f, 0.92f), new Vector3(0f, -0.4f, 0.92f),
            new Vector3(-0.87f, 0f, 0.5f), new Vector3(0.87f, 0f, 0.5f),
            new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f),
        };

        Fish owner;

        // Looked up lazily: when Fish is added, Unity adds this required component
        // (and runs its Awake) before the Fish component itself exists.
        Fish Owner => owner != null ? owner : owner = GetComponent<Fish>();
        Vector3 target;
        float retargetIn;
        float currentSpeed;
        float blockedTime;
        float bank;
        FoodPellet targetFood;

        void Update()
        {
            if (Owner.Tank == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            var phenotype = Owner.Phenotype;
            float bodyLength = phenotype.lengthMeters * transform.localScale.x;

            float cruiseSpeed = Mathf.Lerp(0.1f, 0.3f, phenotype.swimSpeed)
                                * Owner.Health.SwimSpeedMultiplier
                                * Mathf.Lerp(0.6f, 1f, Owner.Growth);
            float desiredSpeed = cruiseSpeed;
            float pitchLimit = maxPitch;

            UpdateFoodTarget();
            if (targetFood != null)
            {
                target = targetFood.transform.position;
                desiredSpeed *= 1.6f;
                pitchLimit = 70f;

                var mouth = transform.position + transform.forward * bodyLength * 0.5f;
                float reach = bodyLength * 0.3f + 0.03f;
                if ((target - mouth).sqrMagnitude < reach * reach)
                {
                    Owner.Eat(targetFood);
                    targetFood = null;
                    PickWanderTarget(bodyLength);
                }
            }
            else
            {
                retargetIn -= dt;
                if (retargetIn <= 0f || (target - transform.position).sqrMagnitude < bodyLength * bodyLength)
                    PickWanderTarget(bodyLength);
            }

            var toTarget = target - transform.position;
            float targetDistance = toTarget.magnitude;
            var desired = targetDistance > 1e-4f ? toTarget / targetDistance : transform.forward;

            // Steer around obstacles between us and the target.
            float lookAhead = bodyLength * lookAheadBodyLengths + currentSpeed * 0.5f;
            float danger = AvoidObstacles(bodyLength, Mathf.Min(lookAhead, targetDistance), ref desired);

            // Keep a comfortable distance from the glass.
            desired = (desired + WallPush(bodyLength * wallComfortBodyLengths)).normalized;

            if (danger > 0.6f) blockedTime += dt;
            else blockedTime = Mathf.Max(0f, blockedTime - dt);
            if (blockedTime > 2.5f && targetFood == null)
            {
                PickWanderTarget(bodyLength);
                blockedTime = 0f;
            }

            Turn(desired, pitchLimit, 1f + danger * 1.5f, dt);

            // Slow down when something is close ahead, or when turning hard.
            float alignment = Mathf.Clamp01(Vector3.Dot(transform.forward, desired));
            desiredSpeed *= Mathf.Lerp(0.45f, 1f, alignment) * Mathf.Lerp(1f, 0.5f, danger);
            currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * dt);

            var next = transform.position + transform.forward * currentSpeed * dt;
            transform.position = Owner.Tank.Clamp(PushOutOfObstacles(next, bodyLength * 0.3f), bodyLength * 0.5f);

            Owner.Visual.Animate(currentSpeed / Mathf.Max(cruiseSpeed, 0.01f), bank, dt);
        }

        void Turn(Vector3 desired, float pitchLimit, float turnBoost, float dt)
        {
            var look = Quaternion.LookRotation(desired, Vector3.up);
            var euler = look.eulerAngles;
            float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.x), -pitchLimit, pitchLimit);
            look = Quaternion.Euler(pitch, euler.y, 0f);

            float yawBefore = transform.eulerAngles.y;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnDegreesPerSecond * turnBoost * dt);
            float yawRate = Mathf.DeltaAngle(yawBefore, transform.eulerAngles.y) / dt;

            // Lean into the turn a little, like a real fish.
            bank = Mathf.Lerp(bank, Mathf.Clamp(-yawRate * 0.15f, -25f, 25f), dt * 4f);
        }

        /// <summary>
        /// Casts a fan of probes; if the way towards <paramref name="desired"/> is blocked, swaps it
        /// for the clearest probe. Returns 0 (clear) to 1 (about to hit something).
        /// </summary>
        float AvoidObstacles(float bodyLength, float lookAhead, ref Vector3 desired)
        {
            float radius = bodyLength * 0.2f;
            float straightClearance = Clearance(transform.position, desired, radius, lookAhead);
            if (straightClearance >= lookAhead) return 0f;

            var bestDirection = desired;
            float bestScore = float.MinValue;
            foreach (var local in probeDirections)
            {
                var direction = transform.TransformDirection(local);
                float clearance = Clearance(transform.position, direction, radius, lookAhead);
                // Prefer open water, then directions close to where we want to go.
                float score = clearance / lookAhead + 0.35f * Vector3.Dot(direction, desired);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDirection = direction;
                }
            }

            float danger = 1f - straightClearance / lookAhead;
            desired = Vector3.Slerp(desired, bestDirection, Mathf.Clamp01(0.4f + danger)).normalized;
            return danger;
        }

        static float Clearance(Vector3 origin, Vector3 direction, float radius, float distance)
        {
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, hitBuffer, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < count; i++)
            {
                var hit = hitBuffer[i];
                if (!TankObstacle.IsObstacle(hit.collider)) continue;
                // A distance of 0 means we start inside or touching it.
                if (hit.distance < nearest) nearest = hit.distance;
            }
            return nearest;
        }

        Vector3 WallPush(float comfort)
        {
            var b = Owner.Tank.InnerBounds;
            var p = transform.position;
            var push = Vector3.zero;
            push.x += Push(p.x - b.min.x, comfort) - Push(b.max.x - p.x, comfort);
            push.y += Push(p.y - b.min.y, comfort) - Push(b.max.y - p.y, comfort);
            push.z += Push(p.z - b.min.z, comfort) - Push(b.max.z - p.z, comfort);
            return push;
        }

        static float Push(float distanceToWall, float comfort)
        {
            return distanceToWall >= comfort ? 0f : 1f - Mathf.Clamp01(distanceToWall / comfort);
        }

        /// <summary>Safety net: if the fish ends up inside a decoration, nudge it back out.</summary>
        static Vector3 PushOutOfObstacles(Vector3 position, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(position, radius, overlapBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = overlapBuffer[i];
                if (!TankObstacle.IsObstacle(other)) continue;
                var closest = other.ClosestPoint(position);
                var away = position - closest;
                if (away.sqrMagnitude < 1e-8f) away = position - other.bounds.center;
                float distance = away.magnitude;
                if (distance < radius && distance > 1e-5f)
                    position += away / distance * (radius - distance);
            }
            return position;
        }

        void UpdateFoodTarget()
        {
            if (!Owner.WantsFood)
            {
                targetFood = null;
                return;
            }
            if (targetFood == null || targetFood.IsConsumed)
                targetFood = FoodPellet.FindNearest(transform.position, foodSenseRadius);
        }

        void PickWanderTarget(float bodyLength)
        {
            target = Owner.Tank.RandomFreePoint(Mathf.Max(bodyLength, 0.05f));
            retargetIn = GameRandom.Range(3f, 8f);
        }

        void OnEnable()
        {
            if (Owner != null && Owner.Tank != null) PickWanderTarget(Owner.Phenotype.lengthMeters);
        }
    }
}
