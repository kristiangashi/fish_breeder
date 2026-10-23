using FishBreeder.Core;
using FishBreeder.Feeding;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Moves a fish around the tank the way aquarium fish actually behave: mostly short, unhurried
    /// trips of a few body lengths, pauses to hover in place, nosing around decorations and the
    /// sand, and only now and then a longer lap of the tank. Hungry fish chase food.
    /// The fish always moves along its own forward axis and turns smoothly at a limited rate
    /// (turning on the spot when hovering). Obstacles are colliders marked with <see cref="TankObstacle"/>.
    /// Speeds are in body lengths per second, so small and big fish both look right.
    /// </summary>
    [RequireComponent(typeof(Fish))]
    public class FishSwimmer : MonoBehaviour
    {
        enum Activity { Hover, Potter, Inspect, Cruise }

        [Tooltip("Top turning speed. Turns ease in and out up to this rate.")]
        [SerializeField] float turnDegreesPerSecond = 90f;
        [Tooltip("Seconds to settle onto a new heading when relaxed; halved when dodging.")]
        [SerializeField] float turnSmoothTime = 0.55f;
        [Tooltip("Body lengths per second gained or lost each second.")]
        [SerializeField] float acceleration = 1.2f;
        [SerializeField] float maxPitch = 20f;
        [SerializeField] float foodSenseRadius = 1.5f;
        [Tooltip("Pottering speed in body lengths per second.")]
        [SerializeField] float potterSpeed = 0.7f;
        [Tooltip("Speed of the occasional lap of the tank, in body lengths per second.")]
        [SerializeField] float cruiseSpeed = 1.4f;
        [Tooltip("How far ahead to look for obstacles, in body lengths (plus a little per unit of speed).")]
        [SerializeField] float lookAheadBodyLengths = 2.5f;
        [Tooltip("How strongly fish keep away from the glass, in body lengths.")]
        [SerializeField] float wallComfortBodyLengths = 1.2f;

        static readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        static readonly Collider[] overlapBuffer = new Collider[16];

        // Probe directions in local space: straight, then fanning out left/right/up/down.
        static readonly Vector3[] probeDirections =
        {
            new Vector3(0f, 0f, 1f),
            new Vector3(-0.5f, 0f, 0.87f).normalized, new Vector3(0.5f, 0f, 0.87f).normalized,
            new Vector3(0f, 0.4f, 0.92f).normalized, new Vector3(0f, -0.4f, 0.92f).normalized,
            new Vector3(-0.87f, 0f, 0.5f).normalized, new Vector3(0.87f, 0f, 0.5f).normalized,
            new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f),
        };

        Fish owner;

        // Looked up lazily: when Fish is added, Unity adds this required component
        // (and runs its Awake) before the Fish component itself exists.
        Fish Owner => owner != null ? owner : owner = GetComponent<Fish>();
        Activity activity = Activity.Hover;
        float activityTimeLeft;
        float activitySpeed;
        Vector3 target;
        float currentSpeed; // body lengths per second
        float blockedTime;
        float bank;
        float yaw, pitch, yawVelocity, pitchVelocity;
        bool headingInitialised;
        FoodPellet targetFood;

        void Update()
        {
            if (Owner.Tank == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            var phenotype = Owner.Phenotype;
            float bodyLength = phenotype.lengthMeters * transform.localScale.x;
            float speedScale = Mathf.Lerp(0.8f, 1.2f, phenotype.swimSpeed)
                               * Owner.Health.SwimSpeedMultiplier
                               * Mathf.Lerp(0.8f, 1f, Owner.Growth);
            float pitchLimit = maxPitch;
            float desiredSpeed;

            UpdateFoodTarget();
            if (targetFood != null)
            {
                target = targetFood.transform.position;
                desiredSpeed = cruiseSpeed * 1.2f;
                pitchLimit = 70f;

                var mouth = transform.position + transform.forward * bodyLength * 0.5f;
                float reach = bodyLength * 0.3f + 0.03f;
                if ((target - mouth).sqrMagnitude < reach * reach)
                {
                    Owner.Eat(targetFood);
                    targetFood = null;
                    StartActivity(Activity.Hover, bodyLength);
                }
            }
            else
            {
                activityTimeLeft -= dt;
                float distance = (target - transform.position).magnitude;
                bool arrived = activity != Activity.Hover && distance < bodyLength * 0.6f;
                if (arrived || activityTimeLeft <= 0f) NextActivity(bodyLength);
                desiredSpeed = activitySpeed;

                // Ease in to a stop rather than overshooting the spot.
                if (activity != Activity.Hover)
                    desiredSpeed *= Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(distance / (bodyLength * 2f)));
            }
            desiredSpeed *= speedScale;

            var toTarget = target - transform.position;
            float targetDistance = toTarget.magnitude;
            var desired = targetDistance > 1e-4f ? toTarget / targetDistance : transform.forward;

            // Steer around obstacles between us and the target.
            float lookAhead = bodyLength * (lookAheadBodyLengths + currentSpeed * 0.5f);
            float danger = AvoidObstacles(bodyLength, Mathf.Min(lookAhead, Mathf.Max(targetDistance, bodyLength)), ref desired);

            // Keep a comfortable distance from the glass.
            desired = (desired + WallPush(bodyLength * wallComfortBodyLengths)).normalized;

            if (danger > 0.6f) blockedTime += dt;
            else blockedTime = Mathf.Max(0f, blockedTime - dt);
            if (blockedTime > 2.5f && targetFood == null)
            {
                StartActivity(Activity.Potter, bodyLength);
                blockedTime = 0f;
            }

            float turnRate = activity == Activity.Hover && targetFood == null ? turnDegreesPerSecond * 0.5f : turnDegreesPerSecond;
            Turn(desired, pitchLimit, turnRate, danger, dt);

            // Slow down when something is close ahead, or when turning hard.
            float alignment = Mathf.Clamp01(Vector3.Dot(transform.forward, desired));
            desiredSpeed *= Mathf.Lerp(0.3f, 1f, alignment) * Mathf.Lerp(1f, 0.5f, danger);
            float thrust = Mathf.Clamp01((desiredSpeed - currentSpeed) / 0.3f);
            currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * dt);

            var next = transform.position + transform.forward * currentSpeed * bodyLength * dt;
            transform.position = Owner.Tank.Clamp(PushOutOfObstacles(next, bodyLength * 0.3f), bodyLength * 0.5f);

            Owner.Visual.Animate(currentSpeed, thrust, bank, yawVelocity, dt);
        }

        /// <summary>
        /// Eases the heading towards <paramref name="desired"/> with a critically damped spring,
        /// so turns build up and settle smoothly instead of snapping. Danger (an obstacle ahead)
        /// makes the fish react faster.
        /// </summary>
        void Turn(Vector3 desired, float pitchLimit, float turnRate, float danger, float dt)
        {
            if (!headingInitialised)
            {
                var e = transform.eulerAngles;
                yaw = e.y;
                pitch = Mathf.DeltaAngle(0f, e.x);
                headingInitialised = true;
            }

            float targetYaw = Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg;
            float targetPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(desired.y, -1f, 1f)) * Mathf.Rad2Deg, -pitchLimit, pitchLimit);

            float smooth = turnSmoothTime * Mathf.Lerp(1f, 0.5f, danger);
            float maxRate = turnRate * (1f + danger);
            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, smooth, maxRate, dt);
            pitch = Mathf.SmoothDampAngle(pitch, targetPitch, ref pitchVelocity, smooth * 1.6f, maxRate * 0.5f, dt);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            // Lean into the turn a little, more so at speed.
            float lean = Mathf.Clamp(-yawVelocity * 0.12f * Mathf.Clamp01(currentSpeed), -20f, 20f);
            bank = Mathf.Lerp(bank, lean, dt * 3f);
        }

        void NextActivity(float bodyLength)
        {
            // After a trip, usually pause a moment; after a pause, usually potter on.
            float roll = GameRandom.Value;
            if (activity != Activity.Hover && roll < 0.55f) StartActivity(Activity.Hover, bodyLength);
            else if (roll < 0.2f) StartActivity(Activity.Hover, bodyLength);
            else if (roll < 0.65f) StartActivity(Activity.Potter, bodyLength);
            else if (roll < 0.9f) StartActivity(Activity.Inspect, bodyLength);
            else StartActivity(Activity.Cruise, bodyLength);
        }

        void StartActivity(Activity next, float bodyLength)
        {
            activity = next;
            var tank = Owner.Tank;
            var position = transform.position;
            float margin = Mathf.Max(bodyLength, 0.05f);
            switch (next)
            {
                case Activity.Hover:
                    // Hang in place, drifting forward a touch and slowly turning to look around.
                    activitySpeed = 0.06f;
                    activityTimeLeft = GameRandom.Range(1.5f, 5f);
                    var look = Quaternion.Euler(0f, GameRandom.Range(-70f, 70f), 0f) * Flat(transform.forward);
                    target = tank.Clamp(position + look * bodyLength * 3f + Vector3.up * GameRandom.Range(-0.3f, 0.3f) * bodyLength, margin);
                    break;

                case Activity.Potter:
                    activitySpeed = potterSpeed * GameRandom.Range(0.8f, 1.2f);
                    activityTimeLeft = 8f;
                    target = NearbyPoint(position, bodyLength, 1.5f, 4.5f, margin);
                    break;

                case Activity.Inspect:
                    activitySpeed = potterSpeed * 0.8f;
                    activityTimeLeft = 10f;
                    target = InspectPoint(position, bodyLength, margin);
                    break;

                default:
                    activitySpeed = cruiseSpeed;
                    activityTimeLeft = 12f;
                    target = tank.RandomFreePoint(margin);
                    break;
            }
        }

        /// <summary>A free spot a few body lengths away, mostly ahead and roughly level.</summary>
        Vector3 NearbyPoint(Vector3 from, float bodyLength, float minLengths, float maxLengths, float margin)
        {
            var tank = Owner.Tank;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float spread = attempt < 6 ? 110f : 180f;
                var direction = Quaternion.Euler(0f, GameRandom.Range(-spread, spread), 0f) * Flat(transform.forward);
                direction.y = GameRandom.Range(-0.35f, 0.35f);
                var point = tank.Clamp(from + direction.normalized * GameRandom.Range(minLengths, maxLengths) * bodyLength, margin);
                if ((point - from).magnitude < minLengths * bodyLength * 0.7f) continue;
                if (Tank.OverlapsObstacle(point, margin * 0.6f)) continue;
                return point;
            }
            return tank.RandomFreePoint(margin);
        }

        /// <summary>A spot right next to a decoration, or just above the sand, to nose around.</summary>
        Vector3 InspectPoint(Vector3 from, float bodyLength, float margin)
        {
            var tank = Owner.Tank;
            var obstacles = TankObstacle.All;
            if (obstacles.Count > 0 && GameRandom.Chance(0.7f))
            {
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    var obstacle = obstacles[GameRandom.Range(0, obstacles.Count)];
                    if (obstacle == null) continue;
                    var b = obstacle.Bounds;
                    var around = Quaternion.Euler(0f, GameRandom.Range(0f, 360f), 0f) * Vector3.forward;
                    var point = b.center + around * (new Vector2(b.extents.x, b.extents.z).magnitude + bodyLength * 0.8f);
                    point.y = GameRandom.Range(b.min.y + bodyLength * 0.5f, Mathf.Max(b.min.y + bodyLength * 0.5f, b.max.y));
                    point = tank.Clamp(point, margin);
                    if (!Tank.OverlapsObstacle(point, margin * 0.5f)) return point;
                }
            }

            // Goldfish love picking at the gravel.
            var sand = NearbyPoint(from, bodyLength, 1.5f, 5f, margin);
            sand.y = tank.InnerBounds.min.y + bodyLength * 0.6f;
            return tank.Clamp(sand, bodyLength * 0.5f);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
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
            int count = Physics.SphereCastNonAlloc(origin, radius, direction.normalized, hitBuffer, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
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
            push.y += Push(p.y - b.min.y, comfort * 0.5f) - Push(b.max.y - p.y, comfort);
            push.z += Push(p.z - b.min.z, comfort) - Push(b.max.z - p.z, comfort);
            return push;
        }

        static float Push(float distanceToWall, float comfort)
        {
            return distanceToWall >= comfort ? 0f : 1f - Mathf.Clamp01(distanceToWall / comfort);
        }

        static SphereCollider pushProbe;

        /// <summary>
        /// Safety net: if the fish ends up inside a decoration, nudge it back out.
        /// Uses ComputePenetration because decorations have non-convex mesh colliders.
        /// </summary>
        static Vector3 PushOutOfObstacles(Vector3 position, float radius)
        {
            if (pushProbe == null)
            {
                // A trigger far below the tank: only used as a shape for ComputePenetration.
                var go = new GameObject("FishPushProbe") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.position = Vector3.down * 1000f;
                pushProbe = go.AddComponent<SphereCollider>();
                pushProbe.isTrigger = true;
            }
            pushProbe.radius = radius;

            int count = Physics.OverlapSphereNonAlloc(position, radius, overlapBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = overlapBuffer[i];
                if (!TankObstacle.IsObstacle(other)) continue;
                var otherTransform = other.transform;
                if (Physics.ComputePenetration(pushProbe, position, Quaternion.identity,
                        other, otherTransform.position, otherTransform.rotation, out var direction, out var distance))
                    position += direction * distance;
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

        void OnEnable()
        {
            if (Owner != null && Owner.Tank != null) StartActivity(Activity.Potter, Owner.Phenotype.lengthMeters);
        }
    }
}
