using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// The 3D filters, heaters and bubblers in Resources/Equipment: the Meshy equipment set, split
    /// by Tools/Meshy/split_equipment.py into one FBX per piece (each with a low-poly "_Collider"
    /// mesh) sharing one texture atlas. There are three designs of each kind for the four models
    /// sold, so two neighbouring models share a design. The smart heater controller is the best
    /// heater with its control box, which hangs on the outside of the glass.
    /// </summary>
    public static class EquipmentModels
    {
        const string Folder = "Equipment/";
        public const string HeaterController = "Heater2_Controller";

        /// <summary>
        /// Design for each model, by kind (Filter, Heater, Bubbler) and tier. The filters are the
        /// part that sits on the tank floor; the part on top is the same for every model.
        /// </summary>
        static readonly int[][] DesignForTier =
        {
            new[] { 0, 0, 1, 2 }, // sponge and hang-on-back share the small box; canister; sump
            new[] { 0, 1, 2, 2 }, // basic, adjustable, titanium; the smart controller is the titanium tube plus its box
            new[] { 0, 1, 1, 2 }, // air stone; the long stone for both air pumps; oxygen reactor
        };

        /// <summary>Longest side of each design in a Standard tank (metres; scaled with the tank's height).</summary>
        static readonly Dictionary<string, float> Sizes = new Dictionary<string, float>
        {
            { "Filter0", 0.22f }, { "Filter1", 0.24f }, { "Filter2", 0.34f },
            { "Heater0", 0.3f }, { "Heater1", 0.3f }, { "Heater2", 0.28f }, { HeaterController, 0.17f },
            { "Bubbler0", 0.13f }, { "Bubbler1", 0.3f }, { "Bubbler2", 0.2f },
        };

        static Material material;

        public static string Design(EquipmentKind kind, int tier)
        {
            var designs = DesignForTier[(int)kind];
            return kind.ToString() + designs[Mathf.Clamp(tier, 0, designs.Length - 1)];
        }

        /// <summary>The smart heater controller's box goes on the outside of the tank.</summary>
        public static bool HasController(EquipmentKind kind, int tier) => kind == EquipmentKind.Heater && tier >= 3;

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
                    if (renderer != null) Object.Destroy(renderer);
                    continue;
                }
                if (renderer != null) renderer.sharedMaterial = Material;
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
            var created = new Material(MaterialFactory.Lit) { name = "Equipment" };
            var albedo = Resources.Load<Texture2D>(Folder + "Equipment_Albedo");
            var normal = Resources.Load<Texture2D>(Folder + "Equipment_Normal");
            var metallicSmoothness = Resources.Load<Texture2D>(Folder + "Equipment_MetallicSmoothness");
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
