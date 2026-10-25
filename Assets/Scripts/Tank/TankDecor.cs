using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// The decorations in one tank, bought from the supplier. Pieces are the Meshy decoration set
    /// in Resources/Decor: one FBX per piece, each with a low-poly "_Collider" mesh fish swim
    /// around, sharing one texture atlas. Each tank has a number of spots (more in bigger tanks):
    /// tall pieces go along the back, small ones up front, with open water left between them.
    /// </summary>
    public class TankDecor : MonoBehaviour
    {
        const string Folder = "Decor/";

        /// <summary>Yaw that turns a piece's front (Blender's -Y) towards the camera.</summary>
        const float FaceCameraYaw = 180f;

        /// <summary>Tank height the decoration heights in the balance file are for.</summary>
        const float StandardTankHeight = 0.9f;

        struct Spot
        {
            public float x, z;   // fraction of the tank's half width / half depth (-1..1), +z is the back
            public float yaw;    // degrees, relative to facing the camera
            public int row;      // 0 back, 1 middle, 2 front
        }

        class Placed
        {
            public string id;
            public int spot;
            public GameObject piece;
        }

        readonly List<Placed> placed = new List<Placed>();
        Spot[] spots = new Spot[0];
        Transform root;
        Vector3 tankSize;
        static Material material;

        public int Count => placed.Count;
        public int Capacity => spots.Length;
        public bool IsFull => placed.Count >= spots.Length;

        public bool HasPlants
        {
            get
            {
                foreach (var p in placed)
                {
                    var item = Find(p.id);
                    if (item != null && item.plant) return true;
                }
                return false;
            }
        }

        /// <summary>Decoration ids in the order they were added.</summary>
        public List<string> Ids
        {
            get
            {
                var ids = new List<string>();
                foreach (var p in placed) ids.Add(p.id);
                return ids;
            }
        }

        public static DecorationBalance Find(string id)
        {
            foreach (var item in Balance.Data.supplier.decorations)
                if (item != null && item.id == id) return item;
            return null;
        }

        /// <summary>
        /// (Re)builds every piece under a new visuals root for a tank of this size, keeping what was
        /// placed. Called when the tank is built and again when it is upgraded to a bigger size.
        /// </summary>
        public void Rebuild(Transform visualsRoot, Vector3 size, int spotCount)
        {
            root = visualsRoot;
            tankSize = size;
            spots = MakeSpots(spotCount);
            var keep = Ids;
            foreach (var p in placed) if (p.piece != null) Destroy(p.piece);
            placed.Clear();
            foreach (var id in keep) Add(id);
        }

        /// <summary>Changes how many spots the tank has (a perk added some), keeping what's placed.</summary>
        public void SetSpotCount(int count)
        {
            if (root == null || count == spots.Length) return;
            Rebuild(root, tankSize, count);
        }

        /// <summary>Puts a decoration in the next free spot that suits it. False if the tank has no room.</summary>
        public bool Add(string id)
        {
            var item = Find(id);
            if (item == null || root == null) return false;
            int spot = FreeSpot(item.back);
            if (spot < 0) return false;
            var piece = Build(item, spots[spot]);
            placed.Add(new Placed { id = id, spot = spot, piece = piece });
            // New colliders must be in the physics scene before fish look for room around them.
            Physics.SyncTransforms();
            return true;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= placed.Count) return;
            if (placed[index].piece != null) Destroy(placed[index].piece);
            placed.RemoveAt(index);
        }

        int FreeSpot(bool back)
        {
            var preferred = back ? new[] { 0, 1, 2 } : new[] { 2, 1, 0 };
            foreach (int row in preferred)
                for (int i = 0; i < spots.Length; i++)
                    if (spots[i].row == row && !Taken(i)) return i;
            return -1;
        }

        bool Taken(int spot)
        {
            foreach (var p in placed) if (p.spot == spot) return true;
            return false;
        }

        /// <summary>Spots in three rows, spread across the tank with a little fixed jitter so rows don't look like a grid.</summary>
        static Spot[] MakeSpots(int count)
        {
            count = Mathf.Max(0, count);
            int back = Mathf.CeilToInt(count * 0.4f);
            int middle = Mathf.CeilToInt((count - back) * 0.45f);
            int front = count - back - middle;
            var list = new List<Spot>();
            AddRow(list, back, 0, 0.68f);
            AddRow(list, middle, 1, 0.15f);
            AddRow(list, front, 2, -0.48f);
            return list.ToArray();
        }

        static void AddRow(List<Spot> list, int count, int row, float z)
        {
            for (int i = 0; i < count; i++)
            {
                int n = list.Count;
                float x = count == 1 ? 0f : Mathf.Lerp(-0.86f, 0.86f, i / (float)(count - 1));
                // Neighbouring rows are offset so pieces don't line up front to back.
                x += (row == 1 ? 0.5f / Mathf.Max(1, count) : 0f) + Mathf.Sin(n * 12.9898f) * 0.05f;
                list.Add(new Spot
                {
                    x = Mathf.Clamp(x, -0.9f, 0.9f),
                    z = z + Mathf.Sin(n * 78.233f) * 0.1f,
                    yaw = Mathf.Sin(n * 4.1414f) * 45f,
                    row = row,
                });
            }
        }

        GameObject Build(DecorationBalance item, Spot spot)
        {
            float height = item.height * tankSize.y / StandardTankHeight;
            var prefab = Resources.Load<GameObject>(Folder + item.id);
            GameObject piece;
            if (prefab == null)
            {
                Debug.LogWarning($"[FishBreeder] Decoration {item.id} is missing from Resources/{Folder}; using a placeholder rock.");
                piece = MaterialFactory.Obstacle(PrimitiveType.Sphere, item.id, root, Vector3.zero, new Vector3(height * 1.4f, height, height), Quaternion.identity, new Color(0.45f, 0.45f, 0.48f));
                piece.transform.localPosition = new Vector3(spot.x * tankSize.x * 0.5f, height * 0.4f, spot.z * tankSize.z * 0.5f);
                return piece;
            }

            piece = Instantiate(prefab, root, false);
            piece.name = item.id;
            var t = piece.transform;
            t.localPosition = new Vector3(spot.x * tankSize.x * 0.5f, 0f, spot.z * tankSize.z * 0.5f);
            t.localRotation = Quaternion.Euler(0f, FaceCameraYaw + spot.yaw, 0f);
            Dress(piece, out var renderers);
            if (renderers.Count == 0) return piece;

            // Scale to the wanted height, then rest the lowest point on the sand.
            var bounds = WorldBounds(renderers);
            if (bounds.size.y > 1e-5f) t.localScale *= height * root.lossyScale.y / bounds.size.y;
            bounds = WorldBounds(renderers);
            t.position += Vector3.up * (root.position.y - bounds.min.y);

            piece.AddComponent<TankObstacle>();
            return piece;
        }

        /// <summary>Gives a decoration model its material and turns its "_Collider" meshes into colliders.</summary>
        public static void Dress(GameObject piece, out List<Renderer> renderers, bool colliders = true)
        {
            renderers = new List<Renderer>();
            foreach (var filter in piece.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var renderer = filter.GetComponent<Renderer>();
                if (filter.name.EndsWith("_Collider"))
                {
                    if (colliders)
                    {
                        var collider = filter.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = filter.sharedMesh;
                    }
                    if (renderer != null) Destroy(renderer);
                    continue;
                }
                if (renderer == null) continue;
                renderer.sharedMaterial = Material;
                renderers.Add(renderer);
            }
        }

        static Bounds WorldBounds(List<Renderer> renderers)
        {
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        public static Material Material
        {
            get
            {
                if (material == null) material = CreateMaterial();
                return material;
            }
        }

        static Material CreateMaterial()
        {
            var created = new Material(MaterialFactory.Lit) { name = "Decor" };
            var albedo = Resources.Load<Texture2D>(Folder + "Decor_Albedo");
            var normal = Resources.Load<Texture2D>(Folder + "Decor_Normal");
            var metallicSmoothness = Resources.Load<Texture2D>(Folder + "Decor_MetallicSmoothness");
            if (albedo != null) created.mainTexture = albedo;
            if (normal != null && created.HasProperty("_BumpMap"))
            {
                created.SetTexture("_BumpMap", normal);
                created.EnableKeyword("_NORMALMAP");
            }
            if (metallicSmoothness != null && created.HasProperty("_MetallicGlossMap"))
            {
                created.SetTexture("_MetallicGlossMap", metallicSmoothness);
                created.EnableKeyword("_METALLICGLOSSMAP");
                created.EnableKeyword("_METALLICSPECGLOSSMAP"); // URP Lit's name for the same map
                if (created.HasProperty("_GlossMapScale")) created.SetFloat("_GlossMapScale", 1f);
                if (created.HasProperty("_Smoothness")) created.SetFloat("_Smoothness", 1f);
            }
            return created;
        }
    }
}
