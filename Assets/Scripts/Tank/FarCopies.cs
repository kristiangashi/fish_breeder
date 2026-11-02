using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Level of detail for things in the tanks: a <see cref="LODGroup"/> that draws the full model
    /// up close, a low-poly copy (see <see cref="MeshDecimator"/>, made once per mesh) further off,
    /// and nothing when it's a speck. The copies never cast shadows.
    /// </summary>
    public static class FarCopies
    {
        static readonly Dictionary<Mesh, Mesh> copies = new Dictionary<Mesh, Mesh>();

        public static Mesh For(Mesh mesh, int cells)
        {
            if (mesh == null) return null;
            if (copies.TryGetValue(mesh, out var known) && known != null) return known;
            var copy = mesh.isReadable ? MeshDecimator.Simplify(mesh, cells) : mesh;
            copies[mesh] = copy;
            return copy;
        }

        /// <param name="nearShare">Screen height share above which the full models are drawn.</param>
        /// <param name="cullShare">Screen height share below which nothing is drawn.</param>
        public static LODGroup Add(GameObject root, List<Renderer> renderers, int cells, float nearShare, float cullShare)
        {
            var far = new List<Renderer>();
            foreach (var renderer in renderers)
            {
                var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                var mesh = filter != null ? For(filter.sharedMesh, cells) : null;
                if (mesh == null) continue;
                var go = new GameObject(renderer.name + " (far)");
                go.layer = renderer.gameObject.layer;
                go.transform.SetParent(renderer.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var copy = go.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                far.Add(copy);
            }
            var group = root.GetComponent<LODGroup>();
            if (group == null) group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(nearShare, renderers.ToArray()), new LOD(cullShare, far.ToArray()) });
            group.RecalculateBounds();
            return group;
        }
    }
}
