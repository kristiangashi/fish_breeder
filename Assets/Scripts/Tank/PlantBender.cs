using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Makes a plant decoration bend like a real one (drawn with Shaders/PlantBend). It sways gently
    /// all the time, and a fish swimming into it pushes the leaves aside around its body and drags
    /// them a little the way it's going. When the fish leaves, the leaves spring back, overshoot and
    /// sway a few times before they settle. Up to four fish can push one plant at once; a fifth
    /// takes over the weakest push. Each push is a damped spring, so it is smooth however the fish moves.
    /// Grassy plants are <see cref="TankObstacle.passable"/>, so fish swim straight through them.
    /// </summary>
    public class PlantBender : MonoBehaviour
    {
        const int MaxContacts = 4;
        /// <summary>How wide the gap the leaves open round a fish is, in body lengths.</summary>
        const float PartRadius = 0.45f;
        /// <summary>How far the leaves are dragged along: the fish's speed times this many seconds, at most <see cref="MaxDrag"/> radii.</summary>
        const float DragSeconds = 0.45f;
        const float MaxDrag = 1.6f;
        /// <summary>Springs: quick to part in front of a fish, slower and bouncier springing back.</summary>
        const float PushHz = 3f, PushDamping = 0.8f;
        const float ReleaseHz = 1.3f, ReleaseDamping = 0.22f;

        static readonly int BaseId = Shader.PropertyToID("_BendBase");
        static readonly int[] ContactIds = { Shader.PropertyToID("_Contact0"), Shader.PropertyToID("_Contact1"), Shader.PropertyToID("_Contact2"), Shader.PropertyToID("_Contact3") };
        static readonly int[] PushIds = { Shader.PropertyToID("_Push0"), Shader.PropertyToID("_Push1"), Shader.PropertyToID("_Push2"), Shader.PropertyToID("_Push3") };
        static Material material;
        static MaterialPropertyBlock block;

        struct Contact
        {
            public Fish fish;
            public Vector3 centre;
            public float radius;
            public float part, partVelocity, partTarget;
            public Vector3 drag, dragVelocity, dragTarget;
            public bool touching;
            public bool Live => radius > 0f;
        }

        readonly Contact[] contacts = new Contact[MaxContacts];
        Tank tank;
        Renderer[] renderers;
        Vector3 baseLocal;
        float height;
        Bounds bounds;
        bool dirty = true;

        /// <summary>The plant material: the decoration atlas on the bending shader (falls back to the plain one).</summary>
        public static Material Material
        {
            get
            {
                if (material != null) return material;
                var shader = Resources.Load<Shader>("Shaders/PlantBend");
                if (shader == null || !shader.isSupported) return material = TankDecor.Material;
                var decor = TankDecor.Material;
                material = new Material(shader) { name = "Plant", enableInstancing = true };
                if (decor.mainTexture != null) material.mainTexture = decor.mainTexture;
                if (decor.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", decor.GetTexture("_BumpMap"));
                if (decor.HasProperty("_MetallicGlossMap")) material.SetTexture("_MetallicGlossMap", decor.GetTexture("_MetallicGlossMap"));
                return material;
            }
        }

        /// <summary>Sets up a freshly built plant: its renderers (near and far copies) and where its base is.</summary>
        public void Init(Tank owner, Bounds worldBounds)
        {
            tank = owner;
            renderers = GetComponentsInChildren<Renderer>(true);
            var plant = Material;
            foreach (var r in renderers) r.sharedMaterial = plant;
            baseLocal = transform.InverseTransformPoint(new Vector3(worldBounds.center.x, worldBounds.min.y, worldBounds.center.z));
            height = worldBounds.size.y;
            dirty = true;
        }

        void Update()
        {
            if (renderers == null || tank == null) return;
            // Asleep (off screen): nothing moves, and nobody is looking.
            if (TankActivity.Step(tank) == 0) return;
            if (transform.hasChanged)
            {
                // Moved or turned by the player (or the tank was rebuilt).
                transform.hasChanged = false;
                dirty = true;
            }
            if (dirty) bounds = PlantBounds();

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            bool moving = FindPushers();
            var stem = transform.TransformPoint(baseLocal);
            // A plant springs back about its stem, so a push the fish has left drifts in towards it.
            float drift = 1f - Mathf.Exp(-4f * dt);
            for (int i = 0; i < MaxContacts; i++)
            {
                ref var c = ref contacts[i];
                if (!c.Live) continue;
                float hz = c.touching ? PushHz : ReleaseHz, damping = c.touching ? PushDamping : ReleaseDamping;
                if (!c.touching) c.centre = Vector3.Lerp(c.centre, new Vector3(stem.x, c.centre.y, stem.z), drift);
                Spring(ref c.part, ref c.partVelocity, c.partTarget, hz, damping, dt);
                Spring(ref c.drag.x, ref c.dragVelocity.x, c.dragTarget.x, hz, damping, dt);
                Spring(ref c.drag.y, ref c.dragVelocity.y, c.dragTarget.y, hz, damping, dt);
                Spring(ref c.drag.z, ref c.dragVelocity.z, c.dragTarget.z, hz, damping, dt);
                // Bouncing back past straight, the leaves don't fold in on themselves.
                c.part = Mathf.Max(c.part, -0.35f);
                if (!c.touching && Settled(c))
                {
                    c = default;
                    dirty = true;
                    continue;
                }
                moving = true;
            }
            if (moving || dirty) Apply();
            dirty = false;
        }

        /// <summary>Gives every fish inside the plant a contact, and lets go of the ones that left. True if any fish is in it.</summary>
        bool FindPushers()
        {
            for (int i = 0; i < MaxContacts; i++) contacts[i].touching = false;
            bool any = false;
            var residents = tank.Residents;
            for (int f = 0; f < residents.Count; f++)
            {
                var fish = residents[f];
                if (fish == null || fish.IsDead) continue;
                float length = fish.VisualLength;
                // A big fish shoves the plant hard, but never folds it flat.
                float radius = Mathf.Min(length * PartRadius, height * 0.5f);
                var p = fish.transform.position;
                // Quick test against the plant's box, grown by the fish's size.
                float grow = radius + length * 0.5f;
                if (p.x < bounds.min.x - grow || p.x > bounds.max.x + grow || p.z < bounds.min.z - grow || p.z > bounds.max.z + grow
                    || p.y < bounds.min.y - radius || p.y > bounds.max.y + radius) continue;

                int slot = SlotFor(fish);
                if (slot < 0) continue;
                any = true;
                ref var c = ref contacts[slot];
                var swimmer = fish.Swimmer;
                var velocity = fish.transform.forward * ((swimmer != null ? swimmer.Speed : 0f) * length);
                var drag = velocity * DragSeconds;
                drag.y *= 0.3f;
                c.fish = fish;
                c.touching = true;
                // The nose leads, so the leaves part a little ahead of the fish.
                c.centre = p + fish.transform.forward * (length * 0.2f);
                c.radius = radius;
                c.partTarget = 1f;
                c.dragTarget = Vector3.ClampMagnitude(drag, Mathf.Min(radius * MaxDrag, height * 0.3f));
            }
            for (int i = 0; i < MaxContacts; i++)
            {
                ref var c = ref contacts[i];
                if (c.touching || !c.Live) continue;
                // It swam off: the leaves spring back where it left them.
                c.fish = null;
                c.partTarget = 0f;
                c.dragTarget = Vector3.zero;
            }
            return any;
        }

        int SlotFor(Fish fish)
        {
            int free = -1, weakest = -1;
            float weakestPart = float.MaxValue;
            for (int i = 0; i < MaxContacts; i++)
            {
                ref var c = ref contacts[i];
                if (c.fish == fish) return i;
                if (!c.Live) { if (free < 0) free = i; continue; }
                if (c.touching) continue;
                if (c.part < weakestPart) { weakestPart = c.part; weakest = i; }
            }
            if (free >= 0)
            {
                contacts[free] = default;
                return free;
            }
            // All four in use: take over a push that's springing back; its spring carries on, so the bend eases over.
            if (weakest >= 0) contacts[weakest].fish = null;
            return weakest;
        }

        static bool Settled(in Contact c)
        {
            const float still = 0.01f;
            return Mathf.Abs(c.part) < still && Mathf.Abs(c.partVelocity) < still * 4f
                && c.drag.magnitude < c.radius * still && c.dragVelocity.magnitude < c.radius * still * 4f;
        }

        /// <summary>A damped spring towards target (semi-implicit, steady at any frame rate up to the 50 ms cap).</summary>
        static void Spring(ref float x, ref float v, float target, float hz, float damping, float dt)
        {
            float w = 2f * Mathf.PI * hz;
            v += (-w * w * (x - target) - 2f * damping * w * v) * dt;
            x += v * dt;
        }

        void Apply()
        {
            if (block == null) block = new MaterialPropertyBlock();
            var b = transform.TransformPoint(baseLocal);
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetVector(BaseId, new Vector4(b.x, b.y, b.z, height));
                for (int i = 0; i < MaxContacts; i++)
                {
                    var c = contacts[i];
                    block.SetVector(ContactIds[i], c.Live ? new Vector4(c.centre.x, c.centre.y, c.centre.z, c.radius) : Vector4.zero);
                    block.SetVector(PushIds[i], c.Live ? new Vector4(c.drag.x, c.drag.y, c.drag.z, c.part) : Vector4.zero);
                }
                renderer.SetPropertyBlock(block);
            }
        }

        Bounds PlantBounds()
        {
            bool any = false;
            Bounds result = new Bounds(transform.position, Vector3.zero);
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;
                if (!any) { result = r.bounds; any = true; }
                else result.Encapsulate(r.bounds);
            }
            return result;
        }
    }
}
