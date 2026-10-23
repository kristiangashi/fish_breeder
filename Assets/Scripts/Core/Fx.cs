using System.Collections.Generic;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Little bits of life when things happen: words that pop up over fish and tanks ("All grown
    /// up!", hearts, "CHOMP!"), bursts of bubbles and confetti, celebration cards for milestones
    /// and new species, the OPEN! stamp, the day-change wash, and in the tanks themselves rising
    /// bubbles and ripples on the water. Anything can call these; <see cref="FxOverlay"/> draws the
    /// screen parts and the 3D parts look after themselves.
    /// </summary>
    public static class Fx
    {
        internal class Floater
        {
            public Vector3 world;
            public string text;
            public Color color;
            public float size, life, age;
        }

        internal class Particle
        {
            public Vector2 pos, vel;
            public Color color;
            public float size, life, age;
            public bool bubble;
        }

        internal class PendingBurst
        {
            public Vector3 world;
            public Color color;
            public int count;
            public bool bubbles;
        }

        internal class Celebration
        {
            public string kicker, title, subtitle, reward;
            public Texture2D icon;
            public Species species;
            public Color accent;
            public float age;
            public bool burst;
        }

        internal static readonly List<Floater> Floaters = new List<Floater>();
        internal static readonly List<Particle> Particles = new List<Particle>();
        internal static readonly List<PendingBurst> Bursts = new List<PendingBurst>();
        internal static readonly Queue<Celebration> Queue = new Queue<Celebration>();
        internal static Celebration Current;
        internal static string StampText;
        internal static Color StampColor;
        internal static float StampAge = -1f;
        internal static bool StampFresh;
        internal static string DayTitle, DaySubtitle;
        internal static float DayAge = -1f;

        /// <summary>While true (the day report is up), celebration cards wait their turn.</summary>
        public static bool Hold { get; set; }

        public static readonly Color[] Confetti =
        {
            UiTheme.Coral, UiTheme.Sun, UiTheme.Aqua, UiTheme.Leaf, UiTheme.Hibiscus, new Color(0.55f, 0.75f, 1f),
        };

        public static void Reset()
        {
            Floaters.Clear();
            Particles.Clear();
            Bursts.Clear();
            Queue.Clear();
            Current = null;
            StampAge = DayAge = -1f;
            Hold = false;
        }

        /// <summary>A word that pops up at a point in the world and floats away.</summary>
        public static void Text(Vector3 world, string text, Color color, float size = 16f, float life = 1.6f)
        {
            if (Floaters.Count > 60) Floaters.RemoveAt(0);
            Floaters.Add(new Floater { world = world, text = text, color = color, size = size, life = life });
        }

        /// <summary>A burst of confetti (or bubbles) on screen where a point in the world is.</summary>
        public static void Burst(Vector3 world, Color color, int count = 14, bool bubbles = false)
            => Bursts.Add(new PendingBurst { world = world, color = color, count = count, bubbles = bubbles });

        /// <summary>A burst at a point on the GUI (unscaled GUI units).</summary>
        public static void ScreenBurst(Vector2 gui, Color? color, int count, bool bubbles, float speed = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = GameRandom.Range(0f, Mathf.PI * 2f);
                float force = GameRandom.Range(60f, 220f) * speed;
                Particles.Add(new Particle
                {
                    pos = gui,
                    vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) - (bubbles ? 0.8f : 0.6f)) * force,
                    color = color ?? Confetti[GameRandom.Range(0, Confetti.Length)],
                    size = bubbles ? GameRandom.Range(8f, 20f) : GameRandom.Range(5f, 10f),
                    life = GameRandom.Range(0.9f, 1.6f),
                    bubble = bubbles,
                });
            }
            if (Particles.Count > 400) Particles.RemoveRange(0, Particles.Count - 400);
        }

        public static void Milestone(MilestoneBalance milestone)
        {
            string xp = milestone.xp > 0 ? $"   +{milestone.xp} {Progression.TrackName(milestone.track)} XP" : "";
            Queue.Enqueue(new Celebration
            {
                kicker = "MILESTONE REACHED",
                title = milestone.title,
                subtitle = milestone.Describe(),
                reward = $"+{Economy.Format(milestone.reward)}{xp}",
                accent = UiTheme.Sun,
            });
        }

        /// <summary>A species seen for the first time: a card, then the journal opens on it.</summary>
        public static void Discovery(Species species)
        {
            Queue.Enqueue(new Celebration
            {
                kicker = "NEW FISH DISCOVERED!",
                title = species.displayName,
                subtitle = string.IsNullOrEmpty(species.scientificName) ? $"{species.rarity} species" : $"{species.scientificName}  ·  {species.rarity} species",
                reward = "Added to your fish journal",
                species = species,
                accent = UiTheme.Aqua,
            });
        }

        /// <summary>A big stamp in the middle of the screen, like OPEN!</summary>
        public static void Stamp(string text, Color color)
        {
            StampText = text;
            StampColor = color;
            StampAge = 0f;
            StampFresh = true;
        }

        /// <summary>The screen washes ocean blue for a moment with the new day's name.</summary>
        public static void Day(string title, string subtitle)
        {
            DayTitle = title;
            DaySubtitle = subtitle;
            DayAge = 0f;
        }

        // ---------------------------------------------------------------- in the tanks

        static Material bubbleMaterial, rippleMaterial;
        static Mesh ringMesh;

        /// <summary>A handful of bubbles that wobble up to the surface and pop.</summary>
        public static void Bubbles(Vector3 point, int count, float surfaceY, float spread = 0.04f)
        {
            if (bubbleMaterial == null) bubbleMaterial = MaterialFactory.Transparent(new Color(0.85f, 0.95f, 1f, 0.45f));
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Bubble";
                Object.Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = bubbleMaterial;
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.position = point + new Vector3(GameRandom.Range(-spread, spread), GameRandom.Range(-spread, spread) * 0.5f, GameRandom.Range(-spread, spread));
                go.transform.localScale = Vector3.one * GameRandom.Range(0.006f, 0.016f);
                go.AddComponent<FxBubble>().Begin(surfaceY, GameRandom.Range(0f, 0.35f));
            }
        }

        /// <summary>A ripple spreading on the water surface.</summary>
        public static void Ripple(Vector3 surfacePoint, float radius = 0.12f, float duration = 0.9f)
        {
            if (rippleMaterial == null) rippleMaterial = MaterialFactory.Transparent(new Color(0.9f, 0.98f, 1f, 0.6f));
            if (ringMesh == null) ringMesh = BuildRing(36, 0.82f);
            var go = new GameObject("Ripple");
            go.transform.position = surfacePoint;
            go.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = rippleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<FxRipple>().Begin(radius, duration);
        }

        /// <summary>Sparkles across the front glass of a tank, for a freshly wiped tank.</summary>
        public static void Sparkle(Bounds inner, int count = 7)
        {
            for (int i = 0; i < count; i++)
            {
                var point = new Vector3(GameRandom.Range(inner.min.x, inner.max.x), GameRandom.Range(inner.min.y, inner.max.y), inner.min.z);
                Text(point, i % 2 == 0 ? "✦" : "✧", Color.Lerp(Color.white, UiTheme.Aqua, GameRandom.Range(0f, 0.6f)), GameRandom.Range(14f, 26f), GameRandom.Range(0.8f, 1.4f));
            }
        }

        static Mesh BuildRing(int segments, float inner)
        {
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 12];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices[i * 2] = dir * inner;
                vertices[i * 2 + 1] = dir;
                int n = (i + 1) % segments;
                int t = i * 12;
                // Both sides, so it shows from above and below the surface.
                triangles[t] = i * 2; triangles[t + 1] = n * 2; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = n * 2; triangles[t + 4] = n * 2 + 1; triangles[t + 5] = i * 2 + 1;
                triangles[t + 6] = i * 2; triangles[t + 7] = i * 2 + 1; triangles[t + 8] = n * 2;
                triangles[t + 9] = n * 2; triangles[t + 10] = i * 2 + 1; triangles[t + 11] = n * 2 + 1;
            }
            var mesh = new Mesh { name = "Ripple ring", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>A bubble rising to the surface with a little wobble, then gone.</summary>
    public class FxBubble : MonoBehaviour
    {
        float surface, delay, speed, phase;
        Vector3 start;

        public void Begin(float surfaceY, float startDelay)
        {
            surface = surfaceY;
            delay = startDelay;
            speed = GameRandom.Range(0.1f, 0.18f);
            phase = GameRandom.Range(0f, 6f);
            start = transform.position;
            if (delay > 0f) transform.localScale *= 0.01f;
        }

        void Update()
        {
            if (delay > 0f)
            {
                delay -= Time.deltaTime;
                if (delay <= 0f) transform.localScale *= 100f;
                return;
            }
            var pos = transform.position;
            pos.y += speed * Time.deltaTime;
            float wobble = Mathf.Sin(Time.time * 9f + phase) * 0.006f;
            pos.x = start.x + wobble;
            transform.position = pos;
            if (pos.y >= surface) Destroy(gameObject);
        }
    }

    /// <summary>A ring on the water that grows and fades.</summary>
    public class FxRipple : MonoBehaviour
    {
        float radius, duration, age;
        MaterialPropertyBlock block;
        Renderer rend;
        Color baseColor;

        public void Begin(float maxRadius, float seconds)
        {
            radius = maxRadius;
            duration = seconds;
            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            baseColor = rend.sharedMaterial.color;
            transform.localScale = Vector3.one * 0.01f;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            float r = Mathf.Lerp(0.01f, radius, 1f - (1f - t) * (1f - t));
            transform.localScale = new Vector3(r, 1f, r);
            var color = baseColor;
            color.a *= 1f - t;
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            rend.SetPropertyBlock(block);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
