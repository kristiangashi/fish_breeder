using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// The aquarium: its bounds, the fish living in it and the water quality, which
    /// raises the chance of every meal making a fish sick as it drops.
    /// </summary>
    public class Tank : MonoBehaviour
    {
        [SerializeField] Vector3 innerSize = new Vector3(1.6f, 0.9f, 0.7f);
        [SerializeField, Range(0f, 1f)] float waterQuality = 1f;

        readonly List<Fish> residents = new List<Fish>();
        Material backWallMaterial;
        static readonly Color ClearWater = new Color(0.12f, 0.35f, 0.55f);
        static readonly Color MurkyWater = new Color(0.25f, 0.33f, 0.12f);

        public IReadOnlyList<Fish> Residents => residents;
        public float WaterQuality => waterQuality;
        public Bounds InnerBounds => new Bounds(transform.position + Vector3.up * innerSize.y * 0.5f, innerSize);
        public bool IsFull => residents.Count >= GameConfig.Active.maxFishPerTank;
        public int FreeSpace => Mathf.Max(0, GameConfig.Active.maxFishPerTank - residents.Count);

        /// <summary>1 in clean water, up to GameConfig.dirtyWaterInfectionMultiplier in filthy water.</summary>
        public float InfectionMultiplier => Mathf.Lerp(GameConfig.Active.dirtyWaterInfectionMultiplier, 1f, waterQuality);

        public void Add(Fish fish)
        {
            if (!residents.Contains(fish)) residents.Add(fish);
        }

        public void Remove(Fish fish) => residents.Remove(fish);

        public void Pollute(float amount) => waterQuality = Mathf.Clamp01(waterQuality - amount);

        public void Clean()
        {
            waterQuality = 1f;
            GameLog.Add("You cleaned the tank. The water is crystal clear.");
        }

        public Vector3 RandomPoint(float margin)
        {
            var b = InnerBounds;
            b.Expand(-margin * 2f);
            return GameRandom.InsideBounds(b);
        }

        /// <summary>Random point in open water, at least <paramref name="clearance"/> away from any decoration.</summary>
        public Vector3 RandomFreePoint(float clearance)
        {
            var point = RandomPoint(clearance);
            for (int attempt = 0; attempt < 12; attempt++)
            {
                if (!OverlapsObstacle(point, clearance)) return point;
                point = RandomPoint(clearance);
            }
            return point;
        }

        static readonly Collider[] overlapBuffer = new Collider[16];

        public static bool OverlapsObstacle(Vector3 point, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(point, radius, overlapBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (TankObstacle.IsObstacle(overlapBuffer[i])) return true;
            return false;
        }

        public Vector3 Clamp(Vector3 point, float margin) => Clamp(point, margin, margin);

        /// <summary>Keeps a point inside the water, with its own (usually smaller) margin above the sand.</summary>
        public Vector3 Clamp(Vector3 point, float margin, float floorMargin)
        {
            var b = InnerBounds;
            return new Vector3(
                Mathf.Clamp(point.x, b.min.x + margin, b.max.x - margin),
                Mathf.Clamp(point.y, b.min.y + floorMargin, b.max.y - margin),
                Mathf.Clamp(point.z, b.min.z + margin, b.max.z - margin));
        }

        void Update()
        {
            Pollute(residents.Count * GameConfig.Active.bioloadPerFishPerSecond * Time.deltaTime);
            ApplyWaterLook();
        }

        void ApplyWaterLook()
        {
            var water = Color.Lerp(MurkyWater, ClearWater, waterQuality);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = water;
            RenderSettings.fogDensity = Mathf.Lerp(0.6f, 0.08f, waterQuality);
            if (backWallMaterial != null) backWallMaterial.color = water * 1.3f;
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = water * 0.5f;
        }

        /// <summary>Simple tank made of primitives. Replace with a Meshy or hand-made tank model later.</summary>
        public void BuildPlaceholderVisuals()
        {
            var b = innerSize;
            var root = new GameObject("Visuals").transform;
            root.SetParent(transform, false);

            MaterialFactory.Decor(PrimitiveType.Cube, "Sand", root, new Vector3(0f, -0.025f, 0f), new Vector3(b.x + 0.04f, 0.05f, b.z + 0.04f), new Color(0.85f, 0.75f, 0.55f));
            var wall = MaterialFactory.Decor(PrimitiveType.Cube, "BackWall", root, new Vector3(0f, b.y * 0.5f, b.z * 0.5f + 0.01f), new Vector3(b.x + 0.04f, b.y, 0.02f), ClearWater);
            backWallMaterial = wall.GetComponent<Renderer>().sharedMaterial;

            // Glass frame: the 12 edges of the box.
            var frame = new Color(0.1f, 0.1f, 0.12f);
            const float t = 0.02f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                MaterialFactory.Decor(PrimitiveType.Cube, "FrameV", root, new Vector3(sx * b.x * 0.5f, b.y * 0.5f, sz * b.z * 0.5f), new Vector3(t, b.y, t), frame);
            for (int sy = 0; sy <= 1; sy++)
            for (int s = -1; s <= 1; s += 2)
            {
                MaterialFactory.Decor(PrimitiveType.Cube, "FrameX", root, new Vector3(0f, sy * b.y, s * b.z * 0.5f), new Vector3(b.x, t, t), frame);
                MaterialFactory.Decor(PrimitiveType.Cube, "FrameZ", root, new Vector3(s * b.x * 0.5f, sy * b.y, 0f), new Vector3(t, t, b.z), frame);
            }

            if (TankDecor.Available) TankDecor.Build(root, b);
            else BuildPlaceholderObstacles(root, b);
            BuildPebbles(root, b);
            // New colliders must be in the physics scene before fish pick spawn points.
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Stand-in decor for when the Meshy decorations are missing: rocks, a rock arch, driftwood
        /// and plants spread through the tank so fish have to swim around (and through) them.
        /// All are TankObstacles with colliders.
        /// </summary>
        static void BuildPlaceholderObstacles(Transform root, Vector3 b)
        {
            var rock = new Color(0.45f, 0.45f, 0.48f);
            var darkRock = new Color(0.35f, 0.34f, 0.36f);
            var wood = new Color(0.4f, 0.28f, 0.18f);

            // Big rocks: one front-left, one in the middle, one back-right.
            MaterialFactory.Obstacle(PrimitiveType.Sphere, "Rock", root, new Vector3(-b.x * 0.3f, 0.08f, -b.z * 0.05f), new Vector3(0.3f, 0.22f, 0.22f), Quaternion.Euler(0f, 20f, 0f), rock);
            MaterialFactory.Obstacle(PrimitiveType.Sphere, "Rock", root, new Vector3(b.x * 0.05f, 0.1f, b.z * 0.15f), new Vector3(0.24f, 0.32f, 0.2f), Quaternion.Euler(0f, -10f, 8f), darkRock);
            MaterialFactory.Obstacle(PrimitiveType.Sphere, "Rock", root, new Vector3(b.x * 0.38f, 0.06f, b.z * 0.2f), new Vector3(0.2f, 0.14f, 0.18f), Quaternion.identity, rock);

            // Rock arch the fish can swim through.
            float archX = b.x * 0.25f, archZ = -b.z * 0.12f, archGap = 0.2f, archHeight = 0.32f;
            MaterialFactory.Obstacle(PrimitiveType.Cube, "ArchPillar", root, new Vector3(archX - archGap * 0.5f - 0.04f, archHeight * 0.5f, archZ), new Vector3(0.08f, archHeight, 0.12f), Quaternion.identity, darkRock);
            MaterialFactory.Obstacle(PrimitiveType.Cube, "ArchPillar", root, new Vector3(archX + archGap * 0.5f + 0.04f, archHeight * 0.5f, archZ), new Vector3(0.08f, archHeight, 0.12f), Quaternion.identity, darkRock);
            MaterialFactory.Obstacle(PrimitiveType.Cube, "ArchTop", root, new Vector3(archX, archHeight + 0.03f, archZ), new Vector3(archGap + 0.2f, 0.06f, 0.14f), Quaternion.Euler(0f, 0f, 3f), darkRock);

            // Driftwood lying diagonally across the back.
            MaterialFactory.Obstacle(PrimitiveType.Cylinder, "Driftwood", root, new Vector3(-b.x * 0.1f, 0.12f, b.z * 0.32f), new Vector3(0.05f, 0.4f, 0.05f), Quaternion.Euler(0f, 35f, 75f), wood);

            // Plants: tall thin stalks in a few clumps.
            var clumps = new[] { new Vector2(-b.x * 0.42f, b.z * 0.3f), new Vector2(-b.x * 0.05f, -b.z * 0.25f), new Vector2(b.x * 0.42f, -b.z * 0.25f) };
            foreach (var clump in clumps)
            {
                for (int i = 0; i < 4; i++)
                {
                    float h = GameRandom.Range(0.25f, 0.6f);
                    var offset = new Vector2(GameRandom.Range(-0.06f, 0.06f), GameRandom.Range(-0.05f, 0.05f));
                    MaterialFactory.Obstacle(PrimitiveType.Capsule, "Plant", root,
                        new Vector3(clump.x + offset.x, h * 0.5f, clump.y + offset.y), new Vector3(0.025f, h * 0.5f, 0.025f),
                        Quaternion.Euler(GameRandom.Range(-6f, 6f), 0f, GameRandom.Range(-6f, 6f)),
                        new Color(0.2f, GameRandom.Range(0.5f, 0.75f), 0.25f));
                }
            }

        }

        /// <summary>Pebbles are pure decoration; fish can swim over them.</summary>
        static void BuildPebbles(Transform root, Vector3 b)
        {
            for (int i = 0; i < 14; i++)
            {
                float size = GameRandom.Range(0.02f, 0.05f);
                MaterialFactory.Decor(PrimitiveType.Sphere, "Pebble", root,
                    new Vector3(GameRandom.Range(-b.x * 0.48f, b.x * 0.48f), size * 0.2f, GameRandom.Range(-b.z * 0.45f, b.z * 0.45f)),
                    new Vector3(size * 1.3f, size * 0.6f, size), new Color(GameRandom.Range(0.5f, 0.8f), GameRandom.Range(0.45f, 0.7f), 0.45f));
            }
        }
    }
}
