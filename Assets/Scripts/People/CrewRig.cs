using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.People
{
    /// <summary>Little acted moments a character can play.</summary>
    public enum Gesture
    {
        PointAndWow, Wow, LeanIn, Think, Nod, HeadShake, Grossed, Mad, Wave, Cheer, Clap,
        ThumbsUp, Shrug, HandOver, Stretch, Wipe, Beckon, Present, LookAround,
    }

    /// <summary>
    /// Moves a crew model's skeleton by code, no animation clips: a relaxed stance (the models are
    /// modelled in a T-pose), breathing and glancing around, a walk cycle driven by Speed, the head
    /// following LookTarget, and gestures like pointing at a tank, looking back and going "wow".
    /// Every pose is described in the character's own space (x right, y up, z forward): limbs as the
    /// direction they point, the spine and head as angles. Limbs are aimed bone by bone each frame,
    /// so it works whatever way the bones were oriented in the model file.
    /// </summary>
    public class CrewRig : MonoBehaviour
    {
        /// <summary>One pose. Arms are in the chest's space, legs in the hips'; angles in degrees.</summary>
        public struct Pose
        {
            public Vector3 armL, foreL, armR, foreR, legL, shinL, legR, shinR;
            public float bend, twist, lean, headYaw, headPitch, headRoll, hop;
            /// <summary>How much the right arm points at the target, the head looks at it, or at the camera (0 to 1).</summary>
            public float point, look, lookCamera;

            public static Pose Lerp(Pose a, Pose b, float t)
            {
                return new Pose
                {
                    armL = Slerp(a.armL, b.armL, t), foreL = Slerp(a.foreL, b.foreL, t),
                    armR = Slerp(a.armR, b.armR, t), foreR = Slerp(a.foreR, b.foreR, t),
                    legL = Slerp(a.legL, b.legL, t), shinL = Slerp(a.shinL, b.shinL, t),
                    legR = Slerp(a.legR, b.legR, t), shinR = Slerp(a.shinR, b.shinR, t),
                    bend = Mathf.Lerp(a.bend, b.bend, t), twist = Mathf.Lerp(a.twist, b.twist, t), lean = Mathf.Lerp(a.lean, b.lean, t),
                    headYaw = Mathf.Lerp(a.headYaw, b.headYaw, t), headPitch = Mathf.Lerp(a.headPitch, b.headPitch, t), headRoll = Mathf.Lerp(a.headRoll, b.headRoll, t),
                    hop = Mathf.Lerp(a.hop, b.hop, t),
                    point = Mathf.Lerp(a.point, b.point, t), look = Mathf.Lerp(a.look, b.look, t), lookCamera = Mathf.Lerp(a.lookCamera, b.lookCamera, t),
                };
            }

            static Vector3 Slerp(Vector3 a, Vector3 b, float t) => Vector3.Slerp(a, b, t).normalized;

            /// <summary>Sets the right arm, and the left arm as its mirror image.</summary>
            public void BothArms(Vector3 arm, Vector3 fore)
            {
                armR = arm.normalized;
                foreR = fore.normalized;
                armL = Mirror(arm);
                foreL = Mirror(fore);
            }
        }

        class Limb
        {
            public Transform upper, lower, end;
            public bool Valid => upper != null && lower != null && end != null;
        }

        class Key
        {
            public float time;
            public Pose pose;
        }

        class Act
        {
            public Gesture gesture;
            public Vector3? target;
        }

        /// <summary>Walking speed in metres a second; drives the walk cycle.</summary>
        public float Speed;
        /// <summary>Where the head turns when nothing else is going on (null: glance around).</summary>
        public Vector3? LookTarget;
        /// <summary>Stand with arms folded instead of hanging (the fish seller's way).</summary>
        public bool ArmsCrossed;

        /// <summary>The right hand, for handing things over.</summary>
        public Transform RightHand => armR != null && armR.Valid ? armR.end : transform;
        /// <summary>Height of the top of the head above the feet, in world units.</summary>
        public float Height { get; private set; } = 1.6f;
        public bool Busy => keys != null;

        Transform hips, spine, chest, neck, head;
        Limb armL, armR, legL, legR;
        readonly List<Transform> bones = new List<Transform>();
        readonly List<Quaternion> restRotations = new List<Quaternion>();
        Vector3 hipsRest;
        bool ready;

        List<Key> keys;
        float gestureTime;
        Vector3? gestureTarget;
        readonly Queue<Act> queue = new Queue<Act>();

        float walkPhase, walkWeight;
        float breathPhase;
        float glanceTimer = 1f;
        Vector2 glance, glanceNow;
        Pose current;
        bool hasCurrent;

        void Awake() => Bind();

        /// <summary>Finds the skeleton. The bone names come from the rig built for the crew models.</summary>
        void Bind()
        {
            var all = GetComponentsInChildren<Transform>(true);
            hips = Find(all, "hips");
            spine = Find(all, "spine");
            chest = Find(all, "chest");
            neck = Find(all, "neck");
            head = Find(all, "head");
            var a = new Limb { upper = Find(all, "upperarml"), lower = Find(all, "lowerarml"), end = Find(all, "handl") };
            var b = new Limb { upper = Find(all, "upperarmr"), lower = Find(all, "lowerarmr"), end = Find(all, "handr") };
            var c = new Limb { upper = Find(all, "upperlegl"), lower = Find(all, "lowerlegl"), end = Find(all, "footl") };
            var d = new Limb { upper = Find(all, "upperlegr"), lower = Find(all, "lowerlegr"), end = Find(all, "footr") };
            ready = hips != null && spine != null && chest != null && head != null && a.Valid && b.Valid && c.Valid && d.Valid;
            if (!ready)
            {
                Debug.LogWarning($"[FishBreeder] {name} has no usable skeleton; it will stand still.");
                return;
            }
            // Which side is which, from where the bones are (the file's "left" can come in mirrored).
            bool aIsLeft = transform.InverseTransformPoint(a.upper.position).x < 0f;
            armL = aIsLeft ? a : b;
            armR = aIsLeft ? b : a;
            bool cIsLeft = transform.InverseTransformPoint(c.upper.position).x < 0f;
            legL = cIsLeft ? c : d;
            legR = cIsLeft ? d : c;

            foreach (var t in new[] { hips, spine, chest, neck, head, armL.upper, armL.lower, armL.end, armR.upper, armR.lower, armR.end, legL.upper, legL.lower, legL.end, legR.upper, legR.lower, legR.end })
            {
                if (t == null) continue;
                bones.Add(t);
                restRotations.Add(t.localRotation);
            }
            hipsRest = hips.localPosition;

            float top = 0f;
            foreach (var r in GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y - transform.position.y);
            if (top > 0.1f) Height = top;
            breathPhase = Random.value * 10f;
        }

        static Transform Find(Transform[] all, string wanted)
        {
            foreach (var t in all)
            {
                var clean = new System.Text.StringBuilder();
                foreach (char ch in t.name) if (char.IsLetterOrDigit(ch)) clean.Append(char.ToLowerInvariant(ch));
                if (clean.ToString() == wanted) return t;
            }
            return null;
        }

        /// <summary>Plays a gesture now, dropping whatever was playing or queued.</summary>
        public void Play(Gesture gesture, Vector3? target = null)
        {
            queue.Clear();
            Begin(gesture, target);
        }

        /// <summary>Plays a gesture after the current one (and any already queued).</summary>
        public void Queue(Gesture gesture, Vector3? target = null)
        {
            if (keys == null) Begin(gesture, target);
            else queue.Enqueue(new Act { gesture = gesture, target = target });
        }

        void Begin(Gesture gesture, Vector3? target)
        {
            if (!ready) return;
            var from = hasCurrent ? current : BasePose(0f);
            keys = Build(gesture, from, BasePose(0f));
            gestureTime = 0f;
            gestureTarget = target;
        }

        /// <summary>Puts the model in its standing pose straight away (for photos, before any frame runs).</summary>
        public void StandNow()
        {
            if (!ready) return;
            Apply(BasePose(0f));
        }

        void LateUpdate()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            walkWeight = Mathf.MoveTowards(walkWeight, Mathf.Clamp01(Speed / 0.35f), dt * 4f);
            walkPhase += Speed * dt / Mathf.Max(0.2f, Height * 0.42f) * Mathf.PI;
            breathPhase += dt;

            UpdateGlance(dt);
            var pose = BasePose(walkWeight);
            if (keys != null)
            {
                gestureTime += dt;
                if (gestureTime >= keys[keys.Count - 1].time)
                {
                    keys = null;
                    if (queue.Count > 0)
                    {
                        var next = queue.Dequeue();
                        Begin(next.gesture, next.target);
                    }
                }
                if (keys != null)
                {
                    var acted = Sample(keys, gestureTime);
                    // Legs keep walking if they have to.
                    if (walkWeight > 0.01f)
                    {
                        acted.legL = pose.legL; acted.legR = pose.legR; acted.shinL = pose.shinL; acted.shinR = pose.shinR;
                        acted.hop = pose.hop;
                    }
                    pose = acted;
                }
            }
            current = pose;
            hasCurrent = true;
            Apply(pose);
        }

        void UpdateGlance(float dt)
        {
            glanceTimer -= dt;
            if (glanceTimer <= 0f)
            {
                glanceTimer = Random.Range(1.5f, 4.5f);
                glance = Random.value < 0.35f ? Vector2.zero : new Vector2(Random.Range(-40f, 40f), Random.Range(-12f, 10f));
            }
            glanceNow = Vector2.Lerp(glanceNow, glance, 1f - Mathf.Exp(-4f * dt));
        }

        /// <summary>Standing (breathing, shifting, glancing) blended with walking.</summary>
        Pose BasePose(float walk)
        {
            var p = new Pose();
            float breath = Mathf.Sin(breathPhase * 1.6f);
            if (ArmsCrossed)
            {
                p.armR = new Vector3(0.32f, -0.85f, 0.4f).normalized;
                p.foreR = new Vector3(-0.95f, 0.12f, 0.32f).normalized;
                p.armL = new Vector3(-0.3f, -0.85f, 0.42f).normalized;
                p.foreL = new Vector3(0.95f, 0.24f, 0.3f).normalized;
            }
            else p.BothArms(new Vector3(0.17f, -1f, 0.04f + breath * 0.01f), new Vector3(0.07f, -1f, 0.22f));
            p.legR = new Vector3(0.05f, -1f, 0f).normalized;
            p.shinR = new Vector3(0f, -1f, 0.02f).normalized;
            p.legL = Mirror(p.legR);
            p.shinL = Mirror(p.shinR);
            p.bend = breath * 1.2f;
            p.lean = Mathf.Sin(breathPhase * 0.31f) * 1.8f;
            p.headYaw = glanceNow.x;
            p.headPitch = glanceNow.y;
            p.look = LookTarget.HasValue ? 1f : 0f;

            if (walk > 0.01f)
            {
                float s = Mathf.Sin(walkPhase);
                float swing = 26f * Mathf.Deg2Rad;
                var w = new Pose();
                w = p;
                w.legR = LegDirection(1f, s * swing);
                w.legL = LegDirection(-1f, -s * swing);
                w.shinR = ShinDirection(1f, s * swing, Mathf.Max(0f, Mathf.Sin(walkPhase + 2.2f)) * 50f * Mathf.Deg2Rad);
                w.shinL = ShinDirection(-1f, -s * swing, Mathf.Max(0f, Mathf.Sin(walkPhase + 2.2f + Mathf.PI)) * 50f * Mathf.Deg2Rad);
                if (!ArmsCrossed)
                {
                    float arm = 20f * Mathf.Deg2Rad;
                    w.armR = new Vector3(0.14f, -Mathf.Cos(-s * arm), Mathf.Sin(-s * arm)).normalized;
                    w.armL = new Vector3(-0.14f, -Mathf.Cos(s * arm), Mathf.Sin(s * arm)).normalized;
                    w.foreR = new Vector3(0.05f, -0.9f, 0.35f + Mathf.Max(0f, -s) * 0.3f).normalized;
                    w.foreL = new Vector3(-0.05f, -0.9f, 0.35f + Mathf.Max(0f, s) * 0.3f).normalized;
                }
                w.hop = Mathf.Abs(Mathf.Cos(walkPhase)) * 0.025f;
                w.twist = s * 6f;
                w.lean = 0f;
                w.bend = 4f;
                p = Pose.Lerp(p, w, walk);
            }
            return p;
        }

        static Vector3 LegDirection(float side, float angle) => new Vector3(side * 0.05f, -Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        static Vector3 ShinDirection(float side, float angle, float knee) => new Vector3(side * 0.03f, -Mathf.Cos(angle - knee), Mathf.Sin(angle - knee)).normalized;
        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z).normalized;

        static Pose Sample(List<Key> keys, float time)
        {
            if (time <= keys[0].time) return keys[0].pose;
            for (int i = 1; i < keys.Count; i++)
            {
                if (time > keys[i].time) continue;
                float span = keys[i].time - keys[i - 1].time;
                float t = span > 1e-4f ? (time - keys[i - 1].time) / span : 1f;
                return Pose.Lerp(keys[i - 1].pose, keys[i].pose, Mathf.SmoothStep(0f, 1f, t));
            }
            return keys[keys.Count - 1].pose;
        }

        // ------------------------------------------------------------------ applying a pose

        void Apply(Pose p)
        {
            for (int i = 0; i < bones.Count; i++) bones[i].localRotation = restRotations[i];
            hips.localPosition = hipsRest;

            var root = transform.rotation;
            float scale = transform.lossyScale.y;
            hips.position += transform.up * p.hop * scale;

            Vector3? target = gestureTarget ?? LookTarget;
            // Looking at something: work out how far round it is, and let the spine take some of a big turn.
            float lookYaw = 0f, lookPitch = 0f, lookWeight = 0f;
            if (target.HasValue && p.look > 0.001f)
            {
                Aim(target.Value, out lookYaw, out lookPitch);
                lookWeight = p.look;
            }
            var cam = Camera.main;
            if (cam != null && p.lookCamera > 0.001f)
            {
                Aim(cam.transform.position, out float camYaw, out float camPitch);
                float w = p.lookCamera;
                lookYaw = Mathf.Lerp(lookYaw * lookWeight, camYaw, w);
                lookPitch = Mathf.Lerp(lookPitch * lookWeight, camPitch, w);
                lookWeight = 1f;
            }
            else
            {
                lookYaw *= lookWeight;
                lookPitch *= lookWeight;
            }
            float spineTurn = Mathf.Clamp(lookYaw * 0.4f, -40f, 40f);
            float headYaw = Mathf.Clamp(Mathf.Lerp(p.headYaw, 0f, lookWeight) + lookYaw - spineTurn, -80f, 80f);
            float headPitch = Mathf.Clamp(Mathf.Lerp(p.headPitch, 0f, lookWeight) + lookPitch + p.headPitch * lookWeight * 0.5f, -40f, 40f);

            // Spine, half in each of the two bones.
            var hipsFrame = root;
            var half = Quaternion.AngleAxis((p.twist + spineTurn) * 0.5f, Vector3.up) * Quaternion.AngleAxis(p.bend * 0.5f, Vector3.right) * Quaternion.AngleAxis(-p.lean * 0.5f, Vector3.forward);
            Turn(spine, hipsFrame, half);
            var spineFrame = hipsFrame * half;
            Turn(chest, spineFrame, half);
            var chestFrame = spineFrame * half;

            var headTurn = Quaternion.AngleAxis(headYaw, Vector3.up) * Quaternion.AngleAxis(headPitch, Vector3.right) * Quaternion.AngleAxis(-p.headRoll, Vector3.forward);
            if (neck != null)
            {
                var neckTurn = Quaternion.Slerp(Quaternion.identity, headTurn, 0.4f);
                Turn(neck, chestFrame, neckTurn);
                Turn(head, chestFrame * neckTurn, Quaternion.Slerp(Quaternion.identity, headTurn, 0.6f));
            }
            else Turn(head, chestFrame, headTurn);

            // Arms; pointing swaps the right arm's direction for the way to the target.
            var armR = p.armR;
            var foreR = p.foreR;
            if (target.HasValue && p.point > 0.001f)
            {
                var toTarget = Quaternion.Inverse(chestFrame) * (target.Value - this.armR.upper.position).normalized;
                armR = Vector3.Slerp(armR, (toTarget + Vector3.up * 0.1f).normalized, p.point).normalized;
                foreR = Vector3.Slerp(foreR, (toTarget + Vector3.up * 0.15f).normalized, p.point).normalized;
            }
            AimLimb(armL, chestFrame, p.armL, p.foreL);
            AimLimb(this.armR, chestFrame, armR, foreR);
            AimLimb(legL, hipsFrame, p.legL, p.shinL);
            AimLimb(legR, hipsFrame, p.legR, p.shinR);
        }

        /// <summary>Yaw and pitch (degrees, in the character's own frame) from the head to a point.</summary>
        void Aim(Vector3 point, out float yaw, out float pitch)
        {
            var local = Quaternion.Inverse(transform.rotation) * (point - head.position);
            yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            pitch = Mathf.Atan2(-local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            // Anything behind is reached by turning as far as possible.
            yaw = Mathf.Clamp(yaw, -120f, 120f);
        }

        /// <summary>Rotates a bone about its joint by a rotation given in the axes of the frame.</summary>
        static void Turn(Transform bone, Quaternion frame, Quaternion turn)
        {
            if (bone == null) return;
            bone.rotation = frame * turn * Quaternion.Inverse(frame) * bone.rotation;
        }

        static void AimLimb(Limb limb, Quaternion frame, Vector3 upper, Vector3 lower)
        {
            AimBone(limb.upper, limb.lower, frame * upper);
            AimBone(limb.lower, limb.end, frame * lower);
        }

        static void AimBone(Transform bone, Transform child, Vector3 direction)
        {
            var current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-10f || direction.sqrMagnitude < 1e-10f) return;
            bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
        }

        // ------------------------------------------------------------------ gestures

        /// <summary>Keyframes for a gesture: from the pose it starts in, through the act, back to standing.</summary>
        static List<Key> Build(Gesture gesture, Pose from, Pose rest)
        {
            var k = new List<Key> { new Key { time = 0f, pose = from } };
            var p = rest;
            switch (gesture)
            {
                case Gesture.PointAndWow:
                    p.point = 1f; p.look = 1f; p.bend = 6f;
                    Add(k, 0.45f, p);
                    Add(k, 1.6f, p);
                    p.look = 0f; p.lookCamera = 1f; p.point = 0.85f;
                    Add(k, 2.05f, p);
                    p = WowPose(rest);
                    Add(k, 2.5f, p);
                    p.hop = 0.06f; Add(k, 2.68f, p);
                    p.hop = 0f; Add(k, 2.86f, p);
                    p.hop = 0.05f; Add(k, 3.04f, p);
                    p.hop = 0f; Add(k, 3.22f, p);
                    Add(k, 3.5f, p);
                    Add(k, 4.1f, rest);
                    break;

                case Gesture.Wow:
                    p = WowPose(rest);
                    p.look = 1f;
                    Add(k, 0.35f, p);
                    p.hop = 0.06f; Add(k, 0.55f, p);
                    p.hop = 0f; Add(k, 0.75f, p);
                    p.hop = 0.05f; Add(k, 0.95f, p);
                    p.hop = 0f; Add(k, 1.15f, p);
                    Add(k, 1.5f, p);
                    Add(k, 2.0f, rest);
                    break;

                case Gesture.LeanIn:
                    p.bend = 24f; p.look = 1f;
                    p.BothArms(new Vector3(0.15f, -0.9f, -0.45f), new Vector3(-0.7f, -0.25f, -0.6f));
                    Add(k, 0.55f, p);
                    p.headRoll = 8f; Add(k, 1.4f, p);
                    p.headRoll = -4f; Add(k, 2.3f, p);
                    Add(k, 2.9f, rest);
                    break;

                case Gesture.Think:
                    p.armR = new Vector3(0.25f, -0.55f, 0.6f).normalized;
                    p.foreR = new Vector3(-0.3f, 0.9f, 0.3f).normalized;
                    p.armL = new Vector3(-0.2f, -0.9f, 0.35f).normalized;
                    p.foreL = new Vector3(0.9f, 0.15f, 0.45f).normalized;
                    p.headRoll = 10f; p.look = 1f;
                    Add(k, 0.4f, p);
                    p.headRoll = -6f; Add(k, 1.5f, p);
                    p.headRoll = 8f; Add(k, 2.4f, p);
                    Add(k, 3.0f, rest);
                    break;

                case Gesture.Nod:
                    p.headPitch = 16f; Add(k, 0.22f, p);
                    p.headPitch = -2f; Add(k, 0.45f, p);
                    p.headPitch = 16f; Add(k, 0.68f, p);
                    p.headPitch = 0f; Add(k, 0.9f, p);
                    Add(k, 1.2f, rest);
                    break;

                case Gesture.HeadShake:
                    p.look = 0f; p.bend = 10f; p.headPitch = 18f;
                    p.BothArms(new Vector3(0.07f, -1f, 0.1f), new Vector3(0.02f, -1f, 0.15f));
                    p.headYaw = 22f; Add(k, 0.3f, p);
                    p.headYaw = -22f; Add(k, 0.6f, p);
                    p.headYaw = 22f; Add(k, 0.9f, p);
                    p.headYaw = -22f; Add(k, 1.2f, p);
                    p.headYaw = 0f; Add(k, 1.5f, p);
                    Add(k, 2.2f, p);
                    Add(k, 2.7f, rest);
                    break;

                case Gesture.Grossed:
                    p.look = 0f; p.bend = -8f; p.headYaw = -32f; p.headPitch = -6f;
                    p.armR = new Vector3(0.25f, -0.45f, 0.8f).normalized;
                    p.foreR = new Vector3(-0.55f, 0.75f, 0.35f).normalized;
                    p.armL = new Vector3(-0.12f, -0.85f, 0.45f).normalized;
                    p.foreL = new Vector3(0.85f, 0f, 0.55f).normalized;
                    Add(k, 0.3f, p);
                    for (int i = 0; i < 3; i++)
                    {
                        p.foreR = new Vector3(-0.1f, 0.9f, 0.4f).normalized; Add(k, 0.55f + i * 0.5f, p);
                        p.foreR = new Vector3(-0.6f, 0.7f, 0.35f).normalized; Add(k, 0.8f + i * 0.5f, p);
                    }
                    Add(k, 2.3f, rest);
                    break;

                case Gesture.Mad:
                    p.look = 1f;
                    p.BothArms(new Vector3(0.75f, -0.62f, -0.05f), new Vector3(-0.6f, -0.72f, 0.15f));
                    Add(k, 0.3f, p);
                    p.hop = 0.045f; Add(k, 0.42f, p);
                    p.hop = 0f; Add(k, 0.54f, p);
                    p.headYaw = 12f; Add(k, 1.0f, p);
                    p.headYaw = -12f; Add(k, 1.25f, p);
                    p.headYaw = 12f; Add(k, 1.5f, p);
                    p.headYaw = 0f; Add(k, 1.75f, p);
                    Add(k, 2.3f, rest);
                    break;

                case Gesture.Wave:
                    p.look = 1f; p.lookCamera = 0f;
                    p.armR = new Vector3(0.75f, 0.55f, 0.25f).normalized;
                    p.foreR = new Vector3(0.15f, 1f, 0.15f).normalized;
                    Add(k, 0.3f, p);
                    for (int i = 0; i < 2; i++)
                    {
                        p.foreR = new Vector3(0.7f, 0.75f, 0.1f).normalized; Add(k, 0.55f + i * 0.5f, p);
                        p.foreR = new Vector3(0.05f, 1f, 0.15f).normalized; Add(k, 0.8f + i * 0.5f, p);
                    }
                    Add(k, 1.75f, rest);
                    break;

                case Gesture.Cheer:
                    p.lookCamera = 0.6f;
                    p.BothArms(new Vector3(0.45f, 0.9f, 0.1f), new Vector3(0.25f, 1f, 0.05f));
                    Add(k, 0.25f, p);
                    p.hop = 0.1f; Add(k, 0.42f, p);
                    p.hop = 0f; Add(k, 0.6f, p);
                    p.hop = 0.09f; Add(k, 0.78f, p);
                    p.hop = 0f; Add(k, 0.96f, p);
                    Add(k, 1.45f, rest);
                    break;

                case Gesture.Clap:
                    p.lookCamera = 0.5f;
                    p.armR = new Vector3(0.3f, -0.55f, 0.75f).normalized;
                    p.armL = Mirror(p.armR);
                    for (int i = 0; i < 4; i++)
                    {
                        p.foreR = new Vector3(-0.78f, 0.35f, 0.5f).normalized; p.foreL = Mirror(p.foreR); Add(k, 0.25f + i * 0.28f, p);
                        p.foreR = new Vector3(-0.3f, 0.35f, 0.88f).normalized; p.foreL = Mirror(p.foreR); Add(k, 0.39f + i * 0.28f, p);
                    }
                    Add(k, 1.75f, rest);
                    break;

                case Gesture.ThumbsUp:
                    p.look = 1f;
                    p.armR = new Vector3(0.35f, -0.5f, 0.7f).normalized;
                    p.foreR = new Vector3(0.05f, 0.95f, 0.3f).normalized;
                    Add(k, 0.3f, p);
                    p.headPitch = 12f; Add(k, 0.6f, p);
                    p.headPitch = 0f; Add(k, 0.85f, p);
                    Add(k, 1.6f, p);
                    Add(k, 2.0f, rest);
                    break;

                case Gesture.Shrug:
                    p.BothArms(new Vector3(0.35f, -0.9f, 0.15f), new Vector3(0.55f, 0.15f, 0.85f));
                    p.headRoll = 12f; p.hop = 0.02f;
                    Add(k, 0.3f, p);
                    p.hop = 0f; Add(k, 1.2f, p);
                    Add(k, 1.6f, rest);
                    break;

                case Gesture.HandOver:
                    p.look = 1f; p.bend = 10f;
                    p.armR = new Vector3(0.12f, -0.25f, 1f).normalized;
                    p.foreR = new Vector3(0f, 0.05f, 1f).normalized;
                    Add(k, 0.35f, p);
                    Add(k, 1.0f, p);
                    Add(k, 1.4f, rest);
                    break;

                case Gesture.Stretch:
                    p.look = 0f; p.bend = -10f; p.headPitch = -15f;
                    p.BothArms(new Vector3(0.2f, 1f, 0f), new Vector3(0.1f, 1f, 0f));
                    Add(k, 0.7f, p);
                    p.lean = 8f; Add(k, 1.3f, p);
                    p.lean = -8f; Add(k, 1.9f, p);
                    Add(k, 2.6f, rest);
                    break;

                case Gesture.Wipe:
                    p.look = 0f; p.bend = 18f; p.headPitch = 20f;
                    p.armR = new Vector3(0.2f, -0.5f, 0.85f).normalized;
                    for (int i = 0; i < 4; i++)
                    {
                        p.foreR = new Vector3(0.55f, -0.6f, 0.6f).normalized; Add(k, 0.35f + i * 0.6f, p);
                        p.foreR = new Vector3(-0.3f, -0.6f, 0.75f).normalized; Add(k, 0.65f + i * 0.6f, p);
                    }
                    Add(k, 3.2f, rest);
                    break;

                case Gesture.Beckon:
                    p.lookCamera = 1f;
                    p.armR = new Vector3(0.3f, -0.3f, 0.9f).normalized;
                    for (int i = 0; i < 3; i++)
                    {
                        p.foreR = new Vector3(0.1f, 0.9f, 0.4f).normalized; Add(k, 0.3f + i * 0.45f, p);
                        p.foreR = new Vector3(0.1f, 0.25f, 0.95f).normalized; Add(k, 0.52f + i * 0.45f, p);
                    }
                    Add(k, 2.0f, rest);
                    break;

                case Gesture.Present:
                    p.lookCamera = 0.7f; p.bend = 6f;
                    p.BothArms(new Vector3(0.25f, -0.55f, 0.8f), new Vector3(0.05f, 0f, 1f));
                    Add(k, 0.35f, p);
                    p.hop = 0.03f; Add(k, 0.6f, p);
                    p.hop = 0f; Add(k, 0.8f, p);
                    Add(k, 1.5f, p);
                    Add(k, 1.9f, rest);
                    break;

                case Gesture.LookAround:
                    p.look = 0f; p.headPitch = -6f;
                    p.headYaw = 50f; Add(k, 0.6f, p);
                    Add(k, 1.0f, p);
                    p.headYaw = -50f; Add(k, 1.7f, p);
                    Add(k, 2.1f, p);
                    Add(k, 2.6f, rest);
                    break;
            }
            return k;
        }

        /// <summary>Hands up to the cheeks, looking up a little: "wow!"</summary>
        static Pose WowPose(Pose rest)
        {
            var p = rest;
            p.point = 0f;
            p.look = 0f;
            p.lookCamera = 1f;
            p.headPitch = -10f;
            p.bend = -6f;
            p.BothArms(new Vector3(0.42f, -0.5f, 0.5f), new Vector3(-0.35f, 0.85f, 0.35f));
            return p;
        }

        static void Add(List<Key> keys, float time, Pose pose) => keys.Add(new Key { time = time, pose = pose });
    }
}
