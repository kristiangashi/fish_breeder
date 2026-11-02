using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// The 3D filters, heaters and bubblers in Resources/Equipment: two Meshy sheets, split by
    /// Tools/Meshy/split_equipment.py into one FBX per piece (each with a low-poly "_Collider"
    /// mesh). The filters that hang on the back glass have their own textures ("Filters_");
    /// everything else, including the filter bases kept for under the tank (FilterBase0-2, not
    /// used yet), shares the "Equipment_" textures. There are three designs of each kind for the
    /// four models sold, so two neighbouring models share a design. The smart heater controller
    /// is the best heater with its control box, which hangs on the outside of the glass.
    /// </summary>
    public static class EquipmentModels
    {
        const string Folder = "Equipment/";
        public const string HeaterController = "Heater2_Controller";

        /// <summary>
        /// Design for each model, by kind (Filter, Heater, Bubbler) and tier.
        /// </summary>
        static readonly int[][] DesignForTier =
        {
            // The last two of each are the wall aquarium's own models: they use the best design until they get their own.
            new[] { 0, 1, 1, 2, 2, 2 }, // sponge; hang-on-back and canister share the two-tray filter; the smart filter for the sump
            new[] { 0, 1, 2, 2, 2, 2 }, // basic, adjustable, titanium; the smart controller is the titanium tube plus its box
            new[] { 0, 1, 1, 2, 2, 2 }, // air stone; the long stone for both air pumps; oxygen reactor
        };

        /// <summary>Longest side of each design in a Standard tank (metres; scaled with the tank's height).</summary>
        static readonly Dictionary<string, float> Sizes = new Dictionary<string, float>
        {
            { "Filter0", 0.66f }, { "Filter1", 0.67f }, { "Filter2", 0.67f },
            { "FilterBase0", 0.22f }, { "FilterBase1", 0.24f }, { "FilterBase2", 0.34f },
            { "Heater0", 0.3f }, { "Heater1", 0.3f }, { "Heater2", 0.28f }, { HeaterController, 0.17f },
            { "Bubbler0", 0.13f }, { "Bubbler1", 0.3f }, { "Bubbler2", 0.2f },
        };

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        public static string Design(EquipmentKind kind, int tier)
        {
            var designs = DesignForTier[(int)kind];
            return kind.ToString() + designs[Mathf.Clamp(tier, 0, designs.Length - 1)];
        }

        /// <summary>The smart heater controller's box goes on the outside of the tank.</summary>
        public static bool HasController(EquipmentKind kind, int tier) => kind == EquipmentKind.Heater && (tier == 3 || tier == 5);

        public static float Size(string design) => Sizes.TryGetValue(design, out var size) ? size : 0.2f;

        /// <summary>A copy of the design with its material, or null if the model is missing.</summary>
        public static GameObject Spawn(string design, Transform parent, bool colliders)
        {
            var prefab = Resources.Load<GameObject>(Folder + design);
            if (prefab == null)
            {
                Debug.LogWarning($"[FishBreeder] Equipment model {design} is missing from Resources/{Folder}; using a placeholder.");
                return null;
            }
            var piece = Object.Instantiate(prefab, parent, false);
            piece.name = design;
            foreach (var filter in piece.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var renderer = filter.GetComponent<Renderer>();
                if (filter.name.EndsWith("_Collider"))
                {
                    if (colliders) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    if (renderer != null)
                    {
                        // Hidden now: Destroy waits for the end of the frame, after a shop photo is taken.
                        renderer.enabled = false;
                        Object.Destroy(renderer);
                    }
                    continue;
                }
                if (renderer != null) renderer.sharedMaterial = MaterialFor(design);
            }
            return piece;
        }

        /// <summary>The piece's bounds in the space of another transform (its renderers' mesh bounds, carried over corner by corner).</summary>
        public static Bounds LocalBounds(GameObject piece, Transform space)
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (var renderer in piece.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = space.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!any) bounds = new Bounds(p, Vector3.zero);
                    else bounds.Encapsulate(p);
                    any = true;
                }
            }
            return bounds;
        }

        /// <summary>The vertices of a piece's visible meshes (not its colliders), in <paramref name="space"/>.</summary>
        static List<Vector3> Vertices(GameObject piece, Transform space)
        {
            var points = new List<Vector3>();
            foreach (var renderer in piece.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable || filter.name.EndsWith("_Collider")) continue;
                foreach (var vertex in filter.sharedMesh.vertices)
                    points.Add(space.InverseTransformPoint(renderer.transform.TransformPoint(vertex)));
            }
            return points;
        }

        /// <summary>
        /// The middle of the lowest slice of a piece's mesh, in <paramref name="space"/>: the mouth
        /// of a filter's intake tube, which is the only part reaching that far down.
        /// </summary>
        public static Vector3 LowestPart(GameObject piece, Transform space, float slice = 0.12f)
        {
            var bounds = LocalBounds(piece, space);
            float top = bounds.min.y + Mathf.Max(0.001f, bounds.size.y * slice);
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var p in Vertices(piece, space))
            {
                if (p.y > top) continue;
                sum += p;
                count++;
            }
            if (count == 0) return bounds.center - Vector3.up * bounds.extents.y;
            var middle = sum / count;
            return new Vector3(middle.x, bounds.min.y, middle.z);
        }

        /// <summary>
        /// Where a bubbler's bubbles come out, in <paramref name="space"/>: the top of the air stone
        /// (Bubbler0), <paramref name="along"/> outlets spread over the top of the long stone
        /// (Bubbler1), or the tip of each upright point of the oxygen reactor (Bubbler2).
        /// Empty if the mesh can't be read.
        /// </summary>
        public static List<Vector3> BubbleOutlets(GameObject piece, Transform space, string design, int along)
        {
            var outlets = new List<Vector3>();
            var points = Vertices(piece, space);
            if (points.Count == 0) return outlets;
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);
            float height = bounds.size.y;

            if (design == "Bubbler2")
            {
                // A tip is the point furthest from the middle of the base within its own reach.
                float reach = 0.12f * Mathf.Max(bounds.size.x, bounds.size.z);
                var bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var score = new float[points.Count];
                for (int i = 0; i < points.Count; i++) score[i] = (points[i] - bottom).sqrMagnitude;
                for (int i = 0; i < points.Count; i++)
                {
                    var p = points[i];
                    if (p.y < bounds.min.y + 0.8f * height) continue;
                    bool tip = true;
                    for (int j = 0; j < points.Count && tip; j++)
                        if (score[j] > score[i] && (points[j] - p).sqrMagnitude < reach * reach) tip = false;
                    foreach (var outlet in outlets)
                        if ((outlet - p).sqrMagnitude < reach * reach * 0.25f) tip = false;
                    if (tip) outlets.Add(p);
                }
            }
            else if (design == "Bubbler1")
            {
                // Evenly along the stone's length, each on top of the stone in its slice.
                bool alongX = bounds.size.x >= bounds.size.z;
                float start = alongX ? bounds.min.x : bounds.min.z;
                float length = alongX ? bounds.size.x : bounds.size.z;
                for (int k = 0; k < along; k++)
                {
                    float from = start + length * (0.05f + 0.9f * k / along);
                    float to = start + length * (0.05f + 0.9f * (k + 1) / along);
                    float top = float.MinValue, side = 0f;
                    int count = 0;
                    foreach (var p in points)
                    {
                        float at = alongX ? p.x : p.z;
                        if (at < from || at > to) continue;
                        top = Mathf.Max(top, p.y);
                        side += alongX ? p.z : p.x;
                        count++;
                    }
                    if (count == 0) continue;
                    side /= count;
                    float middle = (from + to) * 0.5f;
                    outlets.Add(alongX ? new Vector3(middle, top, side) : new Vector3(side, top, middle));
                }
            }
            if (outlets.Count == 0)
            {
                // The middle of the very top.
                Vector3 sum = Vector3.zero;
                int count = 0;
                foreach (var p in points)
                {
                    if (p.y < bounds.max.y - 0.08f * height) continue;
                    sum += p;
                    count++;
                }
                outlets.Add(sum / count);
            }
            return outlets;
        }

        /// <summary>
        /// Turns, scales and moves a piece (a child of <paramref name="space"/>) so its longest side
        /// is <paramref name="size"/> and its bounds line up with <paramref name="anchor"/>: per axis,
        /// <paramref name="align"/> -1 puts the bounds' low side on the anchor, 0 the middle, 1 the high side.
        /// </summary>
        public static Bounds Place(GameObject piece, Transform space, Quaternion rotation, float size, Vector3 anchor, Vector3 align)
        {
            var t = piece.transform;
            t.localRotation = rotation;
            var bounds = LocalBounds(piece, space);
            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (longest > 1e-5f) t.localScale *= size / longest;
            bounds = LocalBounds(piece, space);
            var target = new Vector3(
                anchor.x - bounds.center.x - align.x * bounds.extents.x,
                anchor.y - bounds.center.y - align.y * bounds.extents.y,
                anchor.z - bounds.center.z - align.z * bounds.extents.z);
            t.localPosition += target;
            bounds.center += target;
            return bounds;
        }

        /// <summary>Which texture set a design uses: the hanging filters have their own.</summary>
        static string Textures(string design) => design.StartsWith("Filter") && !design.StartsWith("FilterBase") ? "Filters" : "Equipment";

        static Material MaterialFor(string design)
        {
            string textures = Textures(design);
            if (!materials.TryGetValue(textures, out var material) || material == null)
                materials[textures] = material = CreateMaterial(textures);
            return material;
        }

        static Material CreateMaterial(string textures)
        {
            var created = new Material(MaterialFactory.Lit) { name = textures };
            var albedo = Resources.Load<Texture2D>(Folder + textures + "_Albedo");
            var normal = Resources.Load<Texture2D>(Folder + textures + "_Normal");
            var metallicSmoothness = Resources.Load<Texture2D>(Folder + textures + "_MetallicSmoothness");
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
                created.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (created.HasProperty("_GlossMapScale")) created.SetFloat("_GlossMapScale", 1f);
                if (created.HasProperty("_Smoothness")) created.SetFloat("_Smoothness", 1f);
            }
            return created;
        }
    }
}
