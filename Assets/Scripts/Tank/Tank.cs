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

        public Vector3 Clamp(Vector3 point, float margin)
        {
            var b = InnerBounds;
            return new Vector3(
                Mathf.Clamp(point.x, b.min.x + margin, b.max.x - margin),
                Mathf.Clamp(point.y, b.min.y + margin, b.max.y - margin),
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

            for (int i = 0; i < 5; i++)
            {
                float size = GameRandom.Range(0.06f, 0.16f);
                MaterialFactory.Decor(PrimitiveType.Sphere, "Rock", root,
                    new Vector3(GameRandom.Range(-b.x * 0.45f, b.x * 0.45f), size * 0.25f, GameRandom.Range(0f, b.z * 0.4f)),
                    new Vector3(size * 1.4f, size * 0.7f, size), new Color(0.45f, 0.45f, 0.48f));
            }
            for (int i = 0; i < 7; i++)
            {
                float h = GameRandom.Range(0.2f, 0.55f);
                MaterialFactory.Decor(PrimitiveType.Capsule, "Plant", root,
                    new Vector3(GameRandom.Range(-b.x * 0.48f, b.x * 0.48f), h * 0.5f, GameRandom.Range(b.z * 0.1f, b.z * 0.45f)),
                    new Vector3(0.03f, h * 0.5f, 0.03f), new Color(0.2f, GameRandom.Range(0.5f, 0.75f), 0.25f));
            }
        }
    }
}
