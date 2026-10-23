using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// The aquarium: its bounds, the fish living (and lying dead) in it, its water and equipment.
    /// Water changes only at time steps (shop opening and closing) and through the player's actions,
    /// so a tank left alone while the shop is closed stays exactly as it is.
    /// </summary>
    public class Tank : MonoBehaviour
    {
        [SerializeField] Vector3 innerSize = new Vector3(1.6f, 0.9f, 0.7f);
        [SerializeField, Range(0f, 1f)] float waterQuality = 1f;
        [SerializeField, Range(0f, 1f)] float algae;
        [SerializeField] float temperature = 22f;
        [SerializeField] float ph = 7.2f;
        [SerializeField] float salinity;
        [SerializeField] bool pests;
        [SerializeField] float heaterSetting = 25f;

        readonly List<Fish> residents = new List<Fish>();
        readonly List<Fish> corpses = new List<Fish>();
        Material backWallMaterial;
        TankWater water;
        Transform visuals;
        static readonly Color ClearWater = new Color(0.12f, 0.35f, 0.55f);
        static readonly Color MurkyWater = new Color(0.25f, 0.33f, 0.12f);
        static readonly Color AlgaeWater = new Color(0.18f, 0.42f, 0.16f);

        /// <summary>The tank the camera is looking into; null in the store view.</summary>
        public static Tank Focused;

        /// <summary>Living fish.</summary>
        public IReadOnlyList<Fish> Residents => residents;
        /// <summary>Dead fish floating at the top until the player removes them.</summary>
        public IReadOnlyList<Fish> Corpses => corpses;
        public float WaterQuality => waterQuality;
        public float Algae => algae;
        public float Temperature => temperature;
        public float Ph => ph;
        public float Salinity => salinity;
        public bool IsSaltwater => salinity > 15f;
        public bool HasPests => pests;
        public bool HasFilter { get; private set; }
        public bool HasHeater { get; private set; }
        public bool HasBubbler { get; private set; }
        public float HeaterSetting => heaterSetting;
        public int WipesLeft { get; private set; }
        public int DecorCount { get; private set; }
        public bool HasPlants { get; private set; }

        /// <summary>1 when there's plenty, dropping as more fish share the water without a bubbler.</summary>
        public float Oxygen
        {
            get
            {
                if (HasBubbler) return 1f;
                int limit = Mathf.Max(1, GameConfig.Active.fishPerTankWithoutBubbler);
                return Mathf.Clamp01(1f - Mathf.Max(0, residents.Count - limit) / (float)limit);
            }
        }

        /// <summary>One number for how the water looks to a customer: 1 sparkling, 0 a swamp.</summary>
        public float WaterHealth => Mathf.Clamp01(Mathf.Min(waterQuality, 1f - algae * 0.8f) - (pests ? 0.15f : 0f));

        public Bounds InnerBounds => new Bounds(transform.position + Vector3.up * innerSize.y * 0.5f, innerSize);
        public bool IsFull => residents.Count >= GameConfig.Active.maxFishPerTank;
        public int FreeSpace => Mathf.Max(0, GameConfig.Active.maxFishPerTank - residents.Count);

        /// <summary>1 in clean water, up to GameConfig.dirtyWaterInfectionMultiplier in filthy water.</summary>
        public float InfectionMultiplier => Mathf.Lerp(GameConfig.Active.dirtyWaterInfectionMultiplier, 1f, waterQuality) * (pests ? 1.3f : 1f);

        void Awake()
        {
            temperature = GameConfig.Active.roomTemperature;
            ph = GameConfig.Active.startingPh;
            WipesLeft = Balance.Data.timeStep.wipesPerSession;
        }

        /// <summary>For Sale tanks: fish arriving without a price get their appraised value as a starting price.</summary>
        public bool PricesNewFish { get; set; }

        public void Add(Fish fish)
        {
            corpses.Remove(fish);
            if (!residents.Contains(fish)) residents.Add(fish);
            if (PricesNewFish && fish.Price <= 0) fish.Price = fish.AppraisedValue;
        }

        public void Remove(Fish fish)
        {
            residents.Remove(fish);
            corpses.Remove(fish);
        }

        /// <summary>A fish died here: it stays (and slowly harms the others) until removed.</summary>
        public void AddCorpse(Fish fish)
        {
            residents.Remove(fish);
            if (!corpses.Contains(fish)) corpses.Add(fish);
        }

        public void Pollute(float amount) => waterQuality = Mathf.Clamp01(waterQuality - amount);

        /// <summary>One wipe: scrubs algae and freshens the water. Limited per closed-shop session.</summary>
        public bool Clean()
        {
            if (WipesLeft <= 0)
            {
                GameLog.Add("No more wipes until the next time the shop opens and closes.");
                return false;
            }
            var balance = Balance.Data.timeStep;
            WipesLeft--;
            algae = Mathf.Clamp01(algae - balance.wipeAlgaeRemoved);
            waterQuality = Mathf.Clamp01(waterQuality + balance.wipeQualityRestored);
            GameLog.Add($"You wiped the tank. Water {waterQuality * 100f:0}%, algae {algae * 100f:0}%. {WipesLeft} wipes left.");
            return true;
        }

        /// <summary>Water conditioner: resets pH for the water type, kills pests, freshens a little.</summary>
        public void TreatWater()
        {
            ph = IsSaltwater ? 8.1f : 7.0f;
            pests = false;
            waterQuality = Mathf.Clamp01(waterQuality + 0.1f);
            GameLog.Add($"Water treated: pH {ph:0.0}, pests gone.");
        }

        /// <summary>Developer shortcut: makes the water filthy to check how dirty tanks look and how shoppers react.</summary>
        public void DevMakeDirty()
        {
            waterQuality = 0.15f;
            algae = 0.55f;
            GameLog.Add("(dev) Water made dirty.");
        }

        public void SetSaltwater(bool salt)
        {
            salinity = salt ? 35f : 0f;
            ph = salt ? 8.1f : GameConfig.Active.startingPh;
            GameLog.Add(salt ? "The tank is now salt water." : "The tank is now fresh water.");
        }

        public void SetHeater(float celsius) => heaterSetting = Mathf.Clamp(celsius, 18f, 32f);

        /// <summary>Called by the shop at every time step (opening and closing).</summary>
        public void OnTimeStep()
        {
            var balance = Balance.Data.timeStep;
            var supplier = Balance.Data.supplier;
            float filter = HasFilter ? supplier.filterWasteMultiplier : 1f;

            waterQuality = Mathf.Clamp01(waterQuality
                - balance.waterQualityLossPerStep * filter
                - residents.Count * balance.wasteQualityLossPerFish * filter
                - corpses.Count * balance.corpseQualityLoss);
            float algaeGrowth = balance.algaePerStep + DecorCount * balance.algaePerDecoration;
            int grazers = 0;
            foreach (var fish in residents) if (!fish.IsDead && fish.Species.eatsAlgae) grazers++;
            algae = Mathf.Clamp01(algae + algaeGrowth * (1.5f - waterQuality) - grazers * balance.algaeEatenPerGrazer);
            float neutral = IsSaltwater ? 8.1f : 7f;
            ph = Mathf.MoveTowards(ph, neutral - 1.2f, balance.phDriftPerStep * (1f - waterQuality) * 2f);
            if (!pests && waterQuality < 0.5f && GameRandom.Chance(balance.pestChance * (1.5f - waterQuality)))
            {
                pests = true;
                GameLog.Add($"Snails and worms have got into a tank. Treat the water to get rid of them.");
            }
            temperature = HasHeater ? heaterSetting : GameConfig.Active.roomTemperature;
            WipesLeft = balance.wipesPerSession;
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

        void Update() => ApplyWaterLook();

        public Color WaterColor
        {
            get
            {
                var color = Color.Lerp(MurkyWater, ClearWater, waterQuality);
                return Color.Lerp(color, AlgaeWater, algae * 0.7f);
            }
        }

        void ApplyWaterLook()
        {
            var color = WaterColor;
            if (backWallMaterial != null) backWallMaterial.color = color * 1.3f;
            // Dirty water shows only inside the glass, through the water volume's murk.
            if (water != null) water.SetLook(color, waterQuality, algae);
        }

        public void InstallFilter()
        {
            if (HasFilter) return;
            HasFilter = true;
            var b = innerSize;
            var dark = new Color(0.15f, 0.16f, 0.18f);
            MaterialFactory.Decor(PrimitiveType.Cube, "Filter", visuals, new Vector3(b.x * 0.36f, b.y - 0.02f, b.z * 0.5f + 0.07f), new Vector3(0.22f, 0.2f, 0.1f), dark);
            MaterialFactory.Decor(PrimitiveType.Cylinder, "FilterIntake", visuals, new Vector3(b.x * 0.36f, b.y * 0.55f, b.z * 0.5f - 0.03f), new Vector3(0.025f, b.y * 0.35f, 0.025f), dark);
        }

        public void InstallHeater()
        {
            if (HasHeater) return;
            HasHeater = true;
            var b = innerSize;
            MaterialFactory.Decor(PrimitiveType.Cylinder, "Heater", visuals, new Vector3(-b.x * 0.45f, b.y * 0.5f, b.z * 0.42f), new Vector3(0.03f, b.y * 0.3f, 0.03f), new Color(0.75f, 0.3f, 0.2f));
        }

        public void InstallBubbler()
        {
            if (HasBubbler) return;
            HasBubbler = true;
            var b = innerSize;
            MaterialFactory.Decor(PrimitiveType.Cylinder, "Bubbler", visuals, new Vector3(-b.x * 0.2f, 0.01f, b.z * 0.38f), new Vector3(0.1f, 0.01f, 0.1f), new Color(0.3f, 0.32f, 0.35f));
            var bubbles = visuals.gameObject.AddComponent<TankBubbles>();
            bubbles.Initialize(this, new Vector3(-b.x * 0.2f, 0.02f, b.z * 0.38f), b.y);
        }

        /// <summary>Simple tank made of primitives. Replace with a Meshy or hand-made tank model later.</summary>
        public void BuildPlaceholderVisuals()
        {
            var b = innerSize;
            var root = new GameObject("Visuals").transform;
            root.SetParent(transform, false);
            visuals = root;

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

            if (TankDecor.Available) DecorCount = TankDecor.Build(root, b);
            else DecorCount = BuildPlaceholderObstacles(root, b);
            HasPlants = DecorCount > 0;
            BuildPebbles(root, b);
            water = TankWater.Build(root, b);
            // New colliders must be in the physics scene before fish pick spawn points.
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Stand-in decor for when the Meshy decorations are missing: rocks, a rock arch, driftwood
        /// and plants spread through the tank so fish have to swim around (and through) them.
        /// All are TankObstacles with colliders.
        /// </summary>
        static int BuildPlaceholderObstacles(Transform root, Vector3 b)
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
            return 9;
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
