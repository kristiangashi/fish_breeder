using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Makes a plant decoration bend like a real one (drawn with Shaders/PlantBend). The shader
    /// bends the whole plant as one stem, keeping its length and the shape of every leaf; it also
    /// waves gently all the time. This keeps the push from fish: one tilt of the stem's tip,
    /// a damped spring. A fish swimming into the plant tips it the way the fish is going and away
    /// from its body, more the higher up it pushes; when it leaves, the plant springs back, swings
    /// past upright and settles. Grassy plants are <see cref="TankObstacle.passable"/>, so fish
    /// swim straight through them.
    /// </summary>
    public class PlantBender : MonoBehaviour
    {
        /// <summary>How far a fish shoves the stem: its speed times this many seconds, plus <see cref="Shove"/> body lengths away from its body.</summary>
        const float DragSeconds = 0.45f;
        const float Shove = 0.25f;
        /// <summary>Most the fish can tip the plant (radians at the tip; the shader's wave comes on top).</summary>
        const float MaxTilt = 0.6f;
        /// <summary>Springs: quick to give way to a fish, slower and bouncier springing back.</summary>
        const float PushHz = 2.5f, PushDamping = 0.8f;
        const float ReleaseHz = 1.2f, ReleaseDamping = 0.2f;

        static readonly int BaseId = Shader.PropertyToID("_BendBase");
        static readonly int TiltId = Shader.PropertyToID("_BendTilt");
        static Material material;
        static MaterialPropertyBlock block;

        Tank tank;
        Renderer[] renderers;
        Vector3 baseLocal;
        float heightLocal;
        Bounds bounds;
        Vector2 tilt, tiltVelocity, tiltTarget;
        bool pushed, dirty = true;

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
            // Kept in the piece's own units, so it follows when the player resizes the piece.
            heightLocal = worldBounds.size.y / Mathf.Max(1e-5f, transform.lossyScale.y);
            dirty = true;
        }

        float Height => heightLocal * transform.lossyScale.y;

        void Update()
        {
            if (renderers == null || tank == null) return;
            // Asleep (off screen): nothing moves, and nobody is looking.
            if (TankActivity.Step(tank) == 0) return;
            if (transform.hasChanged)
            {
                // Moved, turned or resized by the player (or the tank was rebuilt).
                transform.hasChanged = false;
                dirty = true;
            }
            if (dirty) bounds = PlantBounds();

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            bool touching = FindPush();
            float hz = touching ? PushHz : ReleaseHz, damping = touching ? PushDamping : ReleaseDamping;
            Spring(ref tilt.x, ref tiltVelocity.x, tiltTarget.x, hz, damping, dt);
            Spring(ref tilt.y, ref tiltVelocity.y, tiltTarget.y, hz, damping, dt);

            bool still = !touching && tilt.sqrMagnitude < 1e-6f && tiltVelocity.sqrMagnitude < 1e-5f;
            if (still && pushed)
            {
                tilt = tiltVelocity = Vector2.zero;
                pushed = false;
                dirty = true;
            }
            if (!still) pushed = true;
            if (pushed || dirty) Apply();
            dirty = false;
        }

        /// <summary>Sums how hard every fish inside the plant tips it. True if any fish is in it.</summary>
        bool FindPush()
        {
            var b = transform.TransformPoint(baseLocal);
            float height = Height;
            float halfWidth = Mathf.Max(bounds.extents.x, bounds.extents.z);
            var push = Vector2.zero;
            bool any = false;
            var residents = tank.Residents;
            for (int f = 0; f < residents.Count; f++)
            {
                var fish = residents[f];
                if (fish == null || fish.IsDead) continue;
                float length = fish.VisualLength;
                var p = fish.transform.position;
                // Quick test against the plant's box, grown by half the fish.
                float grow = length * 0.5f;
                if (p.x < bounds.min.x - grow || p.x > bounds.max.x + grow || p.z < bounds.min.z - grow || p.z > bounds.max.z + grow
                    || p.y < bounds.min.y || p.y > bounds.max.y + grow * 0.5f) continue;
                any = true;

                var fromStem = new Vector2(p.x - b.x, p.z - b.z);
                float reach = halfWidth + grow;
                // Right through the middle shoves hardest; brushing the edge, only a little.
                float near = 1f - Mathf.Clamp01(fromStem.magnitude / Mathf.Max(1e-4f, reach));
                // A push high up the stem tips it further than one near the base.
                float lever = Mathf.Clamp01((p.y - b.y) / Mathf.Max(1e-4f, height));
                var swimmer = fish.Swimmer;
                var forward = fish.transform.forward;
                var drag = new Vector2(forward.x, forward.z) * ((swimmer != null ? swimmer.Speed : 0f) * length * DragSeconds);
                var away = fromStem.sqrMagnitude > 1e-8f ? -fromStem.normalized * (length * Shove) : Vector2.zero;
                // Only sideways: along its path the fish drags the stem on, it never pushes it back.
                var heading = new Vector2(forward.x, forward.z);
                if (heading.sqrMagnitude > 1e-6f)
                {
                    heading.Normalize();
                    away -= heading * Vector2.Dot(away, heading);
                }
                push += (drag + away) * (near * (0.4f + 0.6f * lever));
            }
            // Sideways shove at the tip, as an angle.
            tiltTarget = Vector2.ClampMagnitude(push / Mathf.Max(1e-4f, height) * 2.5f, MaxTilt);
            return any;
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
            var baseValue = new Vector4(b.x, b.y, b.z, Height);
            var tiltValue = new Vector4(tilt.x, 0f, tilt.y, 0f);
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetVector(BaseId, baseValue);
                block.SetVector(TiltId, tiltValue);
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
