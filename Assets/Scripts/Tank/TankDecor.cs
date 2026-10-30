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
    /// That's where every piece goes unless the player moves it (Setup tab, top view): a moved
    /// piece keeps its own place and turn, as a share of the tank's size, so it stays put when
    /// the tank is upgraded. Pieces are always kept wholly inside the glass.
    /// Plants bend and sway (<see cref="PlantBender"/>); grassy ones let fish swim through.
    /// </summary>
    public class TankDecor : MonoBehaviour
    {
        const string Folder = "Decor/";

        /// <summary>Yaw that turns a piece's front (Blender's -Y) towards the camera.</summary>
        const float FaceCameraYaw = 180f;

        /// <summary>The tallest a piece can be: it stays under the water line.</summary>
        float MaxHeight => tankSize.y * 0.95f;

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
            /// <summary>Moved by the player: <see cref="x"/>, <see cref="z"/> and <see cref="yaw"/> replace the spot's.</summary>
            public bool moved;
            public float x, z, yaw;
            /// <summary>Its size as set by the player (wall aquarium only), as a share of its normal size.</summary>
            public float size = 1f;
        }

        readonly List<Placed> placed = new List<Placed>();
        Spot[] spots = new Spot[0];
        Transform root;
        Vector3 tankSize;
        static Material material;

        /// <summary>
        /// The tank is a wall aquarium: its pieces come out at the balance's wall decoration size,
        /// and the player can make each one bigger or smaller (<see cref="Resize"/>).
        /// </summary>
        public bool WallAquarium { get; set; }

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
            var keep = new List<Placed>(placed);
            foreach (var p in placed) if (p.piece != null) Destroy(p.piece);
            placed.Clear();
            foreach (var old in keep)
            {
                if (!Add(old.id, WallAquarium ? old.size : 1f) || !old.moved) continue;
                var p = placed[placed.Count - 1];
                p.moved = true;
                p.x = old.x;
                p.z = old.z;
                p.yaw = old.yaw;
                Pose(p);
            }
            Physics.SyncTransforms();
        }

        /// <summary>Builds every piece again at the same spots and sizes (the balance changed).</summary>
        public void Refresh()
        {
            if (root != null) Rebuild(root, tankSize, spots.Length);
        }

        /// <summary>Changes how many spots the tank has (a perk added some), keeping what's placed.</summary>
        public void SetSpotCount(int count)
        {
            if (root == null || count == spots.Length) return;
            Rebuild(root, tankSize, count);
        }

        /// <summary>Puts a decoration in the next free spot that suits it. False if the tank has no room.</summary>
        public bool Add(string id) => Add(id, 1f);

        bool Add(string id, float size)
        {
            var item = Find(id);
            if (item == null || root == null) return false;
            int spot = FreeSpot(item.back);
            if (spot < 0) return false;
            var piece = Build(item, spots[spot], size);
            placed.Add(new Placed { id = id, spot = spot, piece = piece, size = size });
            // New colliders must be in the physics scene before fish look for room around them.
            Physics.SyncTransforms();
            return true;
        }

        // ------------------------------------------------------------ moving pieces by hand

        /// <summary>How many pieces the player has moved from their automatic spots.</summary>
        public int MovedCount
        {
            get
            {
                int n = 0;
                foreach (var p in placed) if (p.moved) n++;
                return n;
            }
        }

        /// <summary>The placed piece a collider belongs to, or -1.</summary>
        public int IndexOf(Collider collider)
        {
            if (collider == null) return -1;
            for (int i = 0; i < placed.Count; i++)
                if (placed[i].piece != null && collider.transform.IsChildOf(placed[i].piece.transform)) return i;
            return -1;
        }

        /// <summary>The piece whose middle is nearest this point (seen from above), within reach metres, or -1.</summary>
        public int Nearest(Vector3 world, float reach)
        {
            int best = -1;
            float bestDistance = reach;
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].piece == null) continue;
                var c = Footprint(placed[i].piece).center;
                float d = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(world.x, world.z));
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        public string IdAt(int index) => index >= 0 && index < placed.Count ? placed[index].id : null;
        public Bounds BoundsAt(int index) => index >= 0 && index < placed.Count && placed[index].piece != null ? Footprint(placed[index].piece) : default;

        /// <summary>
        /// Moves a piece so its middle (seen from above) sits over this point, then keeps it inside
        /// the glass. From now on it stays where the player put it.
        /// </summary>
        public void MoveTo(int index, Vector3 world)
        {
            if (index < 0 || index >= placed.Count || placed[index].piece == null) return;
            var p = placed[index];
            var t = p.piece.transform;
            var centre = Footprint(p.piece).center;
            var pivot = t.position + new Vector3(world.x - centre.x, 0f, world.z - centre.z);
            var local = root.InverseTransformPoint(pivot);
            Keep(p, local.x, local.z, t.localEulerAngles.y);
        }

        /// <summary>Turns a piece by this many degrees about its middle (it stays inside the glass).</summary>
        public void Turn(int index, float degrees)
        {
            if (index < 0 || index >= placed.Count || placed[index].piece == null) return;
            var p = placed[index];
            var t = p.piece.transform;
            var centre = Footprint(p.piece).center;
            t.RotateAround(centre, Vector3.up, degrees);
            var local = root.InverseTransformPoint(t.position);
            Keep(p, local.x, local.z, t.localEulerAngles.y);
        }

        /// <summary>A piece's size as the player set it (1 = normal).</summary>
        public float SizeAt(int index) => index >= 0 && index < placed.Count ? placed[index].size : 1f;

        /// <summary>
        /// Wall aquarium only: makes a piece this many times its current size, within the balance's
        /// limits, still standing on the sand and inside the glass. It stays where it is from now on.
        /// </summary>
        public void Resize(int index, float factor)
        {
            if (!WallAquarium || index < 0 || index >= placed.Count || placed[index].piece == null) return;
            var p = placed[index];
            var store = Balance.Data.store;
            float size = Mathf.Clamp(p.size * factor, Mathf.Min(store.wallDecorationMinSize, 1f), Mathf.Max(store.wallDecorationMaxSize, 1f));
            if (Mathf.Abs(size - 1f) < 0.02f) size = 1f;
            var t = p.piece.transform;
            var before = Footprint(p.piece);
            var centre = before.center;
            // Never taller than the water.
            float tallest = MaxHeight * root.lossyScale.y;
            if (size > p.size && before.size.y * size / p.size > tallest) size = Mathf.Max(p.size, p.size * tallest / Mathf.Max(1e-5f, before.size.y));
            if (Mathf.Approximately(size, p.size)) return;
            t.localScale *= size / p.size;
            p.size = size;
            // Grow about its middle, then rest it on the sand again.
            var moved = Footprint(p.piece);
            t.position += new Vector3(centre.x - moved.center.x, root.position.y - moved.min.y, centre.z - moved.center.z);
            var local = root.InverseTransformPoint(t.position);
            Keep(p, local.x, local.z, t.localEulerAngles.y);
        }

        /// <summary>Every piece back in its automatic spot.</summary>
        public void ResetPlacement()
        {
            foreach (var p in placed) p.moved = false;
            Rebuild(root, tankSize, spots.Length);
        }

        void Keep(Placed p, float localX, float localZ, float yaw)
        {
            p.moved = true;
            p.x = localX / Mathf.Max(0.01f, tankSize.x * 0.5f);
            p.z = localZ / Mathf.Max(0.01f, tankSize.z * 0.5f);
            p.yaw = yaw;
            Pose(p);
            Physics.SyncTransforms();
        }

        /// <summary>Puts a moved piece where the player left it, nudged back inside the glass if it pokes out.</summary>
        void Pose(Placed p)
        {
            if (p.piece == null) return;
            var t = p.piece.transform;
            t.localRotation = Quaternion.Euler(0f, p.yaw, 0f);
            t.localPosition = new Vector3(p.x * tankSize.x * 0.5f, t.localPosition.y, p.z * tankSize.z * 0.5f);
            // Keep the whole piece inside, 1.5 cm clear of the glass (in the tank's own axes).
            var b = LocalFootprint(p.piece);
            float marginX = tankSize.x * 0.5f - 0.015f, marginZ = tankSize.z * 0.5f - 0.015f;
            float dx = Shift(b.min.x, b.max.x, -marginX, marginX);
            float dz = Shift(b.min.z, b.max.z, -marginZ, marginZ);
            t.localPosition += new Vector3(dx, 0f, dz);
            p.x = t.localPosition.x / Mathf.Max(0.01f, tankSize.x * 0.5f);
            p.z = t.localPosition.z / Mathf.Max(0.01f, tankSize.z * 0.5f);
        }

        /// <summary>How far to slide a span to fit between the limits (centred if it's wider than the gap).</summary>
        static float Shift(float min, float max, float low, float high)
        {
            if (max - min > high - low) return (low + high) * 0.5f - (min + max) * 0.5f;
            if (min < low) return low - min;
            if (max > high) return high - max;
            return 0f;
        }

        /// <summary>The piece's visible bounds in world space.</summary>
        static Bounds Footprint(GameObject piece)
        {
            var renderers = piece.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(piece.transform.position, Vector3.zero);
            Bounds bounds = default;
            bool any = false;
            foreach (var r in renderers)
            {
                if (!r.enabled) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any ? bounds : new Bounds(piece.transform.position, Vector3.zero);
        }

        /// <summary>The piece's visible bounds in the tank's own space (the tank may be turned to face a side wall).</summary>
        Bounds LocalFootprint(GameObject piece)
        {
            var bounds = new Bounds(root.InverseTransformPoint(piece.transform.position), Vector3.zero);
            foreach (var filter in piece.GetComponentsInChildren<MeshFilter>())
            {
                var r = filter.GetComponent<Renderer>();
                if (r == null || !r.enabled || filter.sharedMesh == null) continue;
                var mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    bounds.Encapsulate(root.InverseTransformPoint(filter.transform.TransformPoint(corner)));
                }
            }
            return bounds;
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

        /// <summary>Level of detail for decorations (see <see cref="FarCopies"/>).</summary>
        const int FarCells = 22;
        const float NearShare = 0.12f, CullShare = 0.004f;

        GameObject Build(DecorationBalance item, Spot spot, float size)
        {
            // Its own size, whatever the tank's: upgrading a tank never grows what's in it.
            float height = Mathf.Min(item.height * (WallAquarium ? Mathf.Max(0.1f, Balance.Data.store.wallDecorationSize) * size : 1f), MaxHeight);
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

            piece.AddComponent<TankObstacle>().passable = item.passable;
            // A low-poly copy for the store view; the full model inside the tank you're looking at.
            FarCopies.Add(piece, renderers, FarCells, NearShare, CullShare);
            // Plants sway, and bend out of the way of fish swimming into them.
            if (item.plant) piece.AddComponent<PlantBender>().Init(GetComponent<Tank>(), WorldBounds(renderers));
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
                    if (renderer != null)
                    {
                        // Hidden now: Destroy waits for the end of the frame, after a shop photo is taken.
                        renderer.enabled = false;
                        Destroy(renderer);
                    }
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
            // Instanced: the same piece in every tank is drawn in one go.
            var created = new Material(MaterialFactory.Lit) { name = "Decor", enableInstancing = true };
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
