using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Furnishes the tank with the Meshy decoration set in Resources/Decor: one FBX per piece,
    /// each with a low-poly "_Collider" mesh fish swim around, sharing one texture atlas.
    /// </summary>
    public static class TankDecor
    {
        const string Folder = "Decor/";

        /// <summary>Yaw that turns a piece's front (Blender's -Y) towards the camera.</summary>
        const float FaceCameraYaw = 180f;

        struct Placement
        {
            public string model;
            public float x, z;    // fraction of the tank's half width / half depth (-1..1), +z is the back
            public float height;  // metres
            public float yaw;     // degrees, relative to facing the camera

            public Placement(string model, float x, float z, float height, float yaw)
            {
                this.model = model;
                this.x = x;
                this.z = z;
                this.height = height;
                this.yaw = yaw;
            }
        }

        // Tall plants along the back, rocks and wood in the middle, small treasures up front,
        // with open water left between them for fish to swim through.
        static readonly Placement[] Layout =
        {
            new Placement("TallPlant", -0.88f, 0.65f, 0.6f, 0f),
            new Placement("Rock", -0.62f, 0.35f, 0.2f, 25f),
            new Placement("Plant", -0.4f, 0.75f, 0.5f, 40f),
            new Placement("Seaweed", -0.9f, -0.45f, 0.35f, 0f),
            new Placement("TreasureChest", -0.3f, -0.4f, 0.14f, -20f),
            new Placement("Driftwood", 0.02f, 0.5f, 0.3f, 10f),
            new Placement("Clam", 0.12f, -0.55f, 0.06f, 15f),
            new Placement("Seaweed", 0.28f, 0.8f, 0.45f, 60f),
            new Placement("Diver", 0.5f, 0.15f, 0.32f, -25f),
            new Placement("FernRock", 0.8f, 0.6f, 0.16f, -30f),
            new Placement("TallPlant", 0.9f, -0.5f, 0.45f, 90f),
        };

        /// <summary>True if the decoration models are in the project.</summary>
        public static bool Available => Resources.Load<GameObject>(Folder + "Rock") != null;

        /// <summary>Places the decorations under <paramref name="root"/>, sitting on the sand.</summary>
        public static void Build(Transform root, Vector3 tankSize)
        {
            var material = CreateMaterial();
            foreach (var placement in Layout)
            {
                var prefab = Resources.Load<GameObject>(Folder + placement.model);
                if (prefab == null)
                {
                    Debug.LogWarning($"[FishBreeder] Decoration {placement.model} is missing from Resources/{Folder}.");
                    continue;
                }
                Place(prefab, placement, root, tankSize, material);
            }
        }

        static void Place(GameObject prefab, Placement placement, Transform root, Vector3 tankSize, Material material)
        {
            var piece = Object.Instantiate(prefab, root, false);
            piece.name = placement.model;
            var t = piece.transform;
            t.localPosition = new Vector3(placement.x * tankSize.x * 0.5f, 0f, placement.z * tankSize.z * 0.5f);
            t.localRotation = Quaternion.Euler(0f, FaceCameraYaw + placement.yaw, 0f);

            var renderers = new System.Collections.Generic.List<Renderer>();
            foreach (var filter in piece.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var renderer = filter.GetComponent<Renderer>();
                if (filter.name.EndsWith("_Collider"))
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    if (renderer != null) Object.Destroy(renderer);
                    continue;
                }
                if (renderer == null) continue;
                renderer.sharedMaterial = material;
                renderers.Add(renderer);
            }
            if (renderers.Count == 0) return;

            // Scale to the wanted height, then rest the lowest point on the sand.
            var bounds = WorldBounds(renderers);
            if (bounds.size.y > 1e-5f) t.localScale *= placement.height * root.lossyScale.y / bounds.size.y;
            bounds = WorldBounds(renderers);
            t.position += Vector3.up * (root.position.y - bounds.min.y);

            piece.AddComponent<TankObstacle>();
        }

        static Bounds WorldBounds(System.Collections.Generic.List<Renderer> renderers)
        {
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static Material CreateMaterial()
        {
            var material = new Material(MaterialFactory.Lit) { name = "Decor" };
            var albedo = Resources.Load<Texture2D>(Folder + "Decor_Albedo");
            var normal = Resources.Load<Texture2D>(Folder + "Decor_Normal");
            var metallicSmoothness = Resources.Load<Texture2D>(Folder + "Decor_MetallicSmoothness");
            if (albedo != null) material.mainTexture = albedo;
            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (metallicSmoothness != null && material.HasProperty("_MetallicGlossMap"))
            {
                material.SetTexture("_MetallicGlossMap", metallicSmoothness);
                material.EnableKeyword("_METALLICGLOSSMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP"); // URP Lit's name for the same map
                if (material.HasProperty("_GlossMapScale")) material.SetFloat("_GlossMapScale", 1f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f);
            }
            return material;
        }
    }
}
