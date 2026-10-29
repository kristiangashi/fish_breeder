using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
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
    /// Each species keeps to its own depth band and pace (see <see cref="Species"/>). Schooling
    /// species swim as a loose school once there are enough of them in the tank (<see cref="FishSchool"/>).
    /// Fish that like certain decorations (<see cref="Species.likedDecorations"/>) keep near them:
    /// their trips start and end by a favourite plant or rock and they seldom lap the tank. Roamers
    /// (<see cref="Species.roams"/>) do the opposite, taking long trips all over the tank.
    ///
    /// Bottom dwellers (corydoras) follow what they do in real tanks instead: they move over the
    /// sand in short hops (a quick burst of tail beats, then a glide), stop to root nose-down in
    /// the gravel with their barbels, rest motionless propped on their fins, eat food that has
    /// sunk, and every minute or so dash straight up to gulp air at the surface (they breathe air
    /// through their gut, more often after a meal) and glide back down nose first.
    /// </summary>
    [RequireComponent(typeof(Fish))]
    public class FishSwimmer : MonoBehaviour
    {
        enum Activity
        {
            Hover, Potter, Inspect, Cruise,
            // Bottom dwellers
            Hop, Sift, Rest, AirDash, Descend,
        }

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
        float activityStarted;
        float activitySpeed;
        float airDashIn = -1f;
        Vector3 target;
        float currentSpeed; // body lengths per second
        float blockedTime;
        float bank;
        float yaw, pitch, yawVelocity, pitchVelocity;
        bool headingInitialised;
        FoodPellet targetFood;
        /// <summary>The piece of food it's swimming after, if any.</summary>
        public FoodPellet TargetFood => targetFood;
        FishSchool school;

        // A short burst: a predator bolting at prey, or prey darting away.
        float burstLeft, burstDelay;
        Fish burstPrey;
        Vector3 burstAway;

        /// <summary>Bolts a little way at <paramref name="prey"/>: a nip that's all show (the real hunting happens at time steps).</summary>
        public void Lunge(Fish prey)
        {
            if (prey == null) return;
            burstPrey = prey;
            burstDelay = 0f;
            burstLeft = 0.45f;
        }

        /// <summary>Darts away from <paramref name="danger"/> after a split-second <paramref name="reaction"/>.</summary>
        public void Flee(Vector3 danger, float reaction)
        {
            var away = transform.position - danger;
            away.y *= 0.3f;
            burstAway = away.sqrMagnitude > 1e-6f ? away.normalized : -transform.forward;
            burstPrey = null;
            burstDelay = reaction;
            burstLeft = 0.6f;
        }

        void Update()
        {
            if (Owner.Tank == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Perf.FishBrain.Begin();

            var phenotype = Owner.Phenotype;
            var species = phenotype.species;
            bool bottomDweller = species.style == SwimStyle.Bottom;
            float bodyLength = Owner.VisualLength;
            float speedScale = species.speed
                               * Mathf.Lerp(0.8f, 1.2f, phenotype.swimSpeed)
                               * Owner.Health.SwimSpeedMultiplier
                               * Mathf.Lerp(0.8f, 1f, Owner.Growth);
            float pitchLimit = maxPitch;
            float extraPitch = 0f;
            float fidget = 0f;
            float desiredSpeed;

            UpdateFoodTarget(bottomDweller, bodyLength);
            bool bursting = Burst(bodyLength, dt);
            if (bursting)
            {
                desiredSpeed = burstPrey != null ? 3.2f : 4f;
                pitchLimit = 45f;
            }
            else if (targetFood != null)
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
                    if (bottomDweller)
                    {
                        // Corydoras gulp air more often during and after a meal.
                        airDashIn = Mathf.Min(airDashIn, GameRandom.Range(6f, 15f));
                        StartActivity(Activity.Sift, bodyLength);
                    }
                    else StartActivity(Activity.Hover, bodyLength);
                }
            }
            else if (bottomDweller)
            {
                desiredSpeed = BottomActivity(bodyLength, dt, ref pitchLimit, ref extraPitch, ref fidget);
            }
            else if ((school = FishSchool.For(Owner)) != null)
            {
                desiredSpeed = Schooling(school, bodyLength);
            }
            else
            {
                activityTimeLeft -= dt;
                float distance = (target - transform.position).magnitude;
                bool arrived = activity != Activity.Hover && distance < bodyLength * 0.6f;
                if (arrived || activityTimeLeft <= 0f || IsBottomActivity(activity)) NextActivity(bodyLength);
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
            Perf.FishProbes.Begin();
            float danger = AvoidObstacles(bodyLength, Mathf.Min(lookAhead, Mathf.Max(targetDistance, bodyLength)), ref desired);

            // Keep a comfortable distance from the glass (bottom dwellers hug the sand, and
            // reach the surface when they go up for air).
            desired = (desired + WallPush(bodyLength * wallComfortBodyLengths, !bottomDweller,
                activity != Activity.AirDash || targetFood != null)).normalized;
            Perf.FishProbes.End();

            if (danger > 0.6f) blockedTime += dt;
            else blockedTime = Mathf.Max(0f, blockedTime - dt);
            if (blockedTime > 2.5f && targetFood == null)
            {
                StartActivity(bottomDweller ? Activity.Hop : Activity.Potter, bodyLength);
                blockedTime = 0f;
            }

            float turnRate = activity == Activity.Hover && targetFood == null ? turnDegreesPerSecond * 0.5f : turnDegreesPerSecond;
            // A burst turns as fast as dodging does.
            Turn(desired, pitchLimit, extraPitch, bursting ? turnDegreesPerSecond * 2f : turnRate, bursting ? 1f : danger, dt);

            // Slow down when something is close ahead, or when turning hard.
            float alignment = Mathf.Clamp01(Vector3.Dot(transform.forward, desired));
            desiredSpeed *= Mathf.Lerp(0.3f, 1f, alignment) * Mathf.Lerp(1f, 0.5f, danger);
            float thrust = Mathf.Max(fidget, Mathf.Clamp01((desiredSpeed - currentSpeed) / 0.3f));
            // Bottom dwellers start and stop sharply: that is what makes their hops read as hops.
            float accel = bursting ? acceleration * 8f : bottomDweller ? acceleration * 3f : acceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, accel * dt);

            var next = transform.position + transform.forward * currentSpeed * bodyLength * dt;
            float floorMargin = bottomDweller ? FloorClearance(bodyLength) : bodyLength * 0.5f;
            transform.position = Owner.Tank.Clamp(PushOutOfObstacles(next, bodyLength * 0.3f), bodyLength * 0.5f, floorMargin);
            Perf.FishBrain.End();

            // Resting on the sand, the tail goes still; hovering midwater fish keep sculling.
            Owner.Visual.IdleStroke = activity == Activity.Rest && targetFood == null ? 0.02f : 0.12f;
            Owner.Visual.Animate(currentSpeed, thrust, bank, yawVelocity, dt);
        }

        /// <summary>
        /// Eases the heading towards <paramref name="desired"/> with a critically damped spring,
        /// so turns build up and settle smoothly instead of snapping. Danger (an obstacle ahead)
        /// makes the fish react faster.
        /// </summary>
        void Turn(Vector3 desired, float pitchLimit, float extraPitch, float turnRate, float danger, float dt)
        {
            if (!headingInitialised)
            {
                var e = transform.eulerAngles;
                yaw = e.y;
                pitch = Mathf.DeltaAngle(0f, e.x);
                headingInitialised = true;
            }

            float targetYaw = Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg;
            // Positive pitch is nose down.
            float targetPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(desired.y, -1f, 1f)) * Mathf.Rad2Deg + extraPitch, -pitchLimit, pitchLimit);

            float smooth = turnSmoothTime * Mathf.Lerp(1f, 0.5f, danger);
            float maxRate = turnRate * (1f + danger);
            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, smooth, maxRate, dt);
            pitch = Mathf.SmoothDampAngle(pitch, targetPitch, ref pitchVelocity, smooth * 1.6f, maxRate * 0.5f, dt);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            // Lean into the turn a little, more so at speed.
            float lean = Mathf.Clamp(-yawVelocity * 0.12f * Mathf.Clamp01(currentSpeed), -20f, 20f);
            bank = Mathf.Lerp(bank, lean, dt * 3f);
        }

        /// <summary>Runs a lunge or a dart while one is going; true while it lasts (it sets the target).</summary>
        bool Burst(float bodyLength, float dt)
        {
            if (burstLeft <= 0f) return false;
            if (burstDelay > 0f)
            {
                burstDelay -= dt;
                return false;
            }
            burstLeft -= dt;
            if (burstPrey != null)
            {
                if (burstPrey.IsDead || burstPrey.Tank != Owner.Tank) burstLeft = 0f;
                else
                {
                    target = burstPrey.transform.position;
                    // Stop just short: a nip, not a collision.
                    if ((target - transform.position).magnitude < (bodyLength + burstPrey.BodyLength) * 0.5f) burstLeft = 0f;
                }
            }
            else target = Owner.Tank.Clamp(transform.position + burstAway * bodyLength * 4f, bodyLength * 0.5f);

            if (burstLeft > 0f) return true;
            // Afterwards, a moment to collect itself.
            StartActivity(Owner.Phenotype.species.style == SwimStyle.Bottom ? Activity.Rest : Activity.Hover, bodyLength);
            return false;
        }

        /// <summary>
        /// Swims with the school: towards where it's going, closing up after straying, heading
        /// roughly the way the others are and keeping a little room from its neighbours. Sets the
        /// target and returns the speed it wants (body lengths per second).
        /// </summary>
        float Schooling(FishSchool school, float bodyLength)
        {
            if (activity != Activity.Potter)
            {
                activity = Activity.Potter;
                activityStarted = Time.time;
            }
            var position = transform.position;
            // Loose: about a body length and a half between fish, a wider group for a bigger school.
            float radius = bodyLength * (1.2f + 0.45f * Mathf.Sqrt(school.Members.Count));
            var toCenter = school.Center - position;
            float strayed = Mathf.Clamp01((toCenter.magnitude - radius) / radius);

            var toGoal = school.Destination - position;
            var steer = (toGoal.sqrMagnitude > 1e-6f ? toGoal.normalized : transform.forward) * (school.Milling ? 0.25f : 0.9f);
            steer += school.Heading * (school.Milling ? 0.25f : 0.6f);
            if (toCenter.sqrMagnitude > 1e-6f) steer += toCenter.normalized * (0.15f + 1.6f * strayed);

            float personal = bodyLength * 1.1f;
            foreach (var mate in school.Members)
            {
                if (mate == Owner) continue;
                var away = position - mate.transform.position;
                float d = away.magnitude;
                if (d < personal && d > 1e-4f) steer += away / d * (1f - d / personal) * 1.8f;
            }
            // Each fish wanders a touch on its own, so the school never looks drilled.
            float t = Time.time * 0.4f + (GetInstanceID() % 1000) * 0.37f;
            steer += new Vector3(Mathf.Sin(t), Mathf.Sin(t * 1.3f) * 0.3f, Mathf.Cos(t * 0.8f)) * 0.25f;
            steer.y *= 0.5f;
            if (steer.sqrMagnitude < 1e-6f) steer = transform.forward;
            target = position + steer.normalized * bodyLength * 3f;
            float pace = school.Milling ? 0.3f : potterSpeed * 1.15f;
            return pace * (1f + 0.8f * strayed);
        }

        static bool IsBottomActivity(Activity a) => a >= Activity.Hop;

        /// <summary>Clearance between the sand and a bottom dweller's centre.</summary>
        static float FloorClearance(float bodyLength) => bodyLength * 0.14f;

        /// <summary>
        /// Runs the bottom dweller's routine and returns the speed it wants (body lengths per second).
        /// </summary>
        float BottomActivity(float bodyLength, float dt, ref float pitchLimit, ref float extraPitch, ref float fidget)
        {
            if (airDashIn < 0f) airDashIn = GameRandom.Range(20f, 60f);
            airDashIn -= dt;
            activityTimeLeft -= dt;
            if (!IsBottomActivity(activity))
            {
                StartActivity(Activity.Descend, bodyLength);
                return 0.6f;
            }

            float age = Time.time - activityStarted;
            float distance = (target - transform.position).magnitude;
            switch (activity)
            {
                case Activity.Hop:
                    pitchLimit = 30f;
                    if (distance < bodyLength * 0.4f || activityTimeLeft <= 0f) NextBottomActivity(bodyLength);
                    // A burst of tail beats, then a glide, repeated until it arrives.
                    return Mathf.Repeat(age, 0.9f) < 0.3f ? 1.8f : 0.35f;

                case Activity.Sift:
                    // Nose down in the gravel, barbels working: small quick bobs and a busy tail.
                    target = transform.position + Flat(transform.forward) * bodyLength;
                    extraPitch = 25f + Mathf.Sin(age * 17f) * 4f;
                    pitchLimit = 35f;
                    fidget = 0.35f;
                    if (activityTimeLeft <= 0f) NextBottomActivity(bodyLength);
                    return 0f;

                case Activity.Rest:
                    target = transform.position + Flat(transform.forward) * bodyLength;
                    if (activityTimeLeft <= 0f) NextBottomActivity(bodyLength);
                    return 0f;

                case Activity.AirDash:
                    pitchLimit = 80f;
                    var top = Owner.Tank.InnerBounds.max.y - bodyLength * 0.6f;
                    if (transform.position.y >= top || activityTimeLeft <= 0f) StartActivity(Activity.Descend, bodyLength);
                    return 2.4f;

                default: // Descend
                    pitchLimit = 60f;
                    if (distance < bodyLength * 0.5f || activityTimeLeft <= 0f) StartActivity(Activity.Sift, bodyLength);
                    return 0.6f;
            }
        }

        void NextBottomActivity(float bodyLength)
        {
            if (airDashIn <= 0f)
            {
                airDashIn = GameRandom.Range(40f, 100f);
                StartActivity(Activity.AirDash, bodyLength);
                return;
            }
            float roll = GameRandom.Value;
            switch (activity)
            {
                case Activity.Hop:
                    StartActivity(roll < 0.55f ? Activity.Sift : roll < 0.8f ? Activity.Hop : Activity.Rest, bodyLength);
                    break;
                case Activity.Sift:
                    StartActivity(roll < 0.65f ? Activity.Hop : Activity.Rest, bodyLength);
                    break;
                default:
                    StartActivity(Activity.Hop, bodyLength);
                    break;
            }
        }

        void NextActivity(float bodyLength)
        {
            // After a trip, usually pause a moment; after a pause, usually potter on.
            float roll = GameRandom.Value;
            var species = Owner.Phenotype.species;
            if (species.roams)
            {
                // Roamers rarely stop and often take a long lap.
                if (activity != Activity.Hover && roll < 0.3f) StartActivity(Activity.Hover, bodyLength);
                else if (roll < 0.55f) StartActivity(Activity.Potter, bodyLength);
                else if (roll < 0.65f) StartActivity(Activity.Inspect, bodyLength);
                else StartActivity(Activity.Cruise, bodyLength);
                return;
            }
            bool home = LikedPieces().Count > 0;
            if (activity != Activity.Hover && roll < 0.55f) StartActivity(Activity.Hover, bodyLength);
            else if (roll < 0.2f) StartActivity(Activity.Hover, bodyLength);
            else if (roll < (home ? 0.55f : 0.65f)) StartActivity(Activity.Potter, bodyLength);
            else if (roll < (home ? 0.97f : 0.9f)) StartActivity(Activity.Inspect, bodyLength);
            else StartActivity(Activity.Cruise, bodyLength);
        }

        /// <summary>Indexes of the decorations in this fish's tank that its kind likes to stay near.</summary>
        List<int> LikedPieces()
        {
            liked.Clear();
            var species = Owner.Phenotype.species;
            var decor = Owner.Tank != null ? Owner.Tank.Decor : null;
            if (decor == null || species.likedDecorations == null || species.likedDecorations.Count == 0) return liked;
            var ids = decor.Ids;
            for (int i = 0; i < ids.Count; i++)
                if (species.LikesDecoration(ids[i])) liked.Add(i);
            return liked;
        }

        readonly List<int> liked = new List<int>();

        /// <summary>A free spot close beside one of the decorations it likes, at its own depth, or null.</summary>
        Vector3? NearLikedPiece(float bodyLength, float margin, float minLengths, float maxLengths)
        {
            var pieces = LikedPieces();
            if (pieces.Count == 0) return null;
            var tank = Owner.Tank;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var b = tank.Decor.BoundsAt(pieces[GameRandom.Range(0, pieces.Count)]);
                if (b.size == Vector3.zero) continue;
                var around = Quaternion.Euler(0f, GameRandom.Range(0f, 360f), 0f) * Vector3.forward;
                var point = b.center + around * (new Vector2(b.extents.x, b.extents.z).magnitude + bodyLength * GameRandom.Range(minLengths, maxLengths));
                point.y = GameRandom.Range(b.min.y + bodyLength * 0.5f, Mathf.Max(b.min.y + bodyLength * 0.5f, b.max.y + bodyLength));
                point = InDepthBand(tank.Clamp(point, margin), bodyLength);
                if (!Tank.OverlapsObstacle(point, margin * 0.5f)) return point;
            }
            return null;
        }

        void StartActivity(Activity next, float bodyLength)
        {
            activity = next;
            activityStarted = Time.time;
            var tank = Owner.Tank;
            var position = transform.position;
            float margin = Mathf.Max(bodyLength, 0.05f);
            float floorY = tank.InnerBounds.min.y + FloorClearance(bodyLength);
            switch (next)
            {
                case Activity.Hop:
                    activityTimeLeft = 6f;
                    // Now and then head for the foot of a decoration, otherwise a short way along the sand.
                    target = GameRandom.Chance(0.2f) ? DecorFoot(bodyLength, floorY) : NearbyPoint(position, bodyLength, 1f, 3f, margin);
                    target.y = floorY;
                    break;

                case Activity.Sift:
                    activityTimeLeft = GameRandom.Range(1.5f, 4f);
                    break;

                case Activity.Rest:
                    activityTimeLeft = GameRandom.Range(3f, 8f);
                    break;

                case Activity.AirDash:
                    activityTimeLeft = 6f;
                    target = tank.Clamp(position + Flat(transform.forward) * bodyLength + Vector3.up * tank.InnerBounds.size.y, margin * 0.5f);
                    break;

                case Activity.Descend:
                    activityTimeLeft = 8f;
                    target = NearbyPoint(position, bodyLength, 1f, 2.5f, margin);
                    target.y = floorY;
                    break;

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
                    var species = Owner.Phenotype.species;
                    // Homebodies potter about their favourite spot; roamers potter further afield.
                    var home = GameRandom.Chance(0.75f) ? NearLikedPiece(bodyLength, margin, 0.5f, 2.5f) : null;
                    target = home ?? NearbyPoint(position, bodyLength, species.roams ? 3f : 1.5f, species.roams ? 8f : 4.5f, margin);
                    break;

                case Activity.Inspect:
                    activitySpeed = potterSpeed * 0.8f;
                    activityTimeLeft = 10f;
                    target = (GameRandom.Chance(0.85f) ? NearLikedPiece(bodyLength, margin, 0.3f, 1.2f) : null) ?? InspectPoint(position, bodyLength, margin);
                    break;

                default:
                    activitySpeed = cruiseSpeed;
                    activityTimeLeft = 12f;
                    target = InDepthBand(tank.RandomFreePoint(margin), bodyLength);
                    break;
            }
        }

        /// <summary>Moves a point's height into this species' preferred depth band.</summary>
        Vector3 InDepthBand(Vector3 point, float bodyLength)
        {
            var species = Owner.Phenotype.species;
            var b = Owner.Tank.InnerBounds;
            float low = b.min.y + bodyLength * 0.5f, high = b.max.y - bodyLength * 0.5f;
            float min = Mathf.Lerp(low, high, species.depthMin), max = Mathf.Lerp(low, high, species.depthMax);
            if (point.y < min || point.y > max) point.y = GameRandom.Range(min, max);
            return point;
        }

        /// <summary>A spot on the sand right next to a decoration.</summary>
        Vector3 DecorFoot(float bodyLength, float floorY)
        {
            var obstacles = TankObstacle.All;
            for (int attempt = 0; attempt < 6 && obstacles.Count > 0; attempt++)
            {
                var obstacle = obstacles[GameRandom.Range(0, obstacles.Count)];
                if (obstacle == null) continue;
                var b = obstacle.Bounds;
                var around = Quaternion.Euler(0f, GameRandom.Range(0f, 360f), 0f) * Vector3.forward;
                var point = b.center + around * (new Vector2(b.extents.x, b.extents.z).magnitude + bodyLength * 0.5f);
                point.y = floorY;
                point = Owner.Tank.Clamp(point, bodyLength, FloorClearance(bodyLength));
                if (!Tank.OverlapsObstacle(point, bodyLength * 0.3f)) return point;
            }
            return NearbyPoint(transform.position, bodyLength, 1f, 3f, bodyLength);
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
                if (Owner.Phenotype.species.style != SwimStyle.Bottom) point = InDepthBand(point, bodyLength);
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
                    point = InDepthBand(tank.Clamp(point, margin), bodyLength);
                    if (!Tank.OverlapsObstacle(point, margin * 0.5f)) return point;
                }
            }

            // Goldfish love picking at the gravel; fish that live up high just potter instead.
            if (Owner.Phenotype.species.depthMin > 0.3f) return NearbyPoint(from, bodyLength, 1.5f, 5f, margin);
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

        Vector3 WallPush(float comfort, bool fromFloor = true, bool fromSurface = true)
        {
            var b = Owner.Tank.InnerBounds;
            var p = transform.position;
            var push = Vector3.zero;
            push.x += Push(p.x - b.min.x, comfort) - Push(b.max.x - p.x, comfort);
            if (fromFloor) push.y += Push(p.y - b.min.y, comfort * 0.5f);
            if (fromSurface) push.y -= Push(b.max.y - p.y, comfort);
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

        /// <summary>Bottom dwellers only go for food that has sunk to near the sand.</summary>
        void UpdateFoodTarget(bool bottomDweller, float bodyLength)
        {
            if (!Owner.WantsFood || activity == Activity.AirDash)
            {
                targetFood = null;
                return;
            }
            float maxHeight = bottomDweller ? Owner.Tank.InnerBounds.min.y + bodyLength * 2f : float.PositiveInfinity;
            if (targetFood != null && !targetFood.IsConsumed && targetFood.transform.position.y <= maxHeight) return;
            targetFood = FoodPellet.FindNearest(transform.position, foodSenseRadius, maxHeight);
        }

        void OnEnable()
        {
            if (Owner != null && Owner.Tank != null)
                StartActivity(Owner.Phenotype.species.style == SwimStyle.Bottom ? Activity.Descend : Activity.Potter, Owner.Phenotype.lengthMeters);
        }
    }
}
