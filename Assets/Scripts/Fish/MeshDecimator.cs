using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Makes a rough low-poly copy of a mesh for seeing from far away (level of detail), by
    /// vertex clustering: the mesh is cut into a grid of cells, every vertex in a cell becomes one,
    /// and triangles that shrink to nothing are dropped. Crude up close, fine at a few pixels tall.
    /// Submeshes stay apart, so each keeps its own material.
    /// </summary>
    public static class MeshDecimator
    {
        class Cluster
        {
            public Vector3 position, normal;
            public Vector2 uv;
            public Vector4 tangent;
            public Vector3 firstNormal;
            public int count;
        }

        class Grid
        {
            public Vector3[] vertices, normals;
            public Vector2[] uvs;
            public Vector4[] tangents;
            public Vector3 origin;
            public float cell;
            public readonly List<Cluster> clusters = new List<Cluster>();
            readonly Dictionary<long, int> lookup = new Dictionary<long, int>();
            /// <summary>Source vertex to cluster, for the submesh being worked on.</summary>
            public readonly Dictionary<int, int> remap = new Dictionary<int, int>();

            public int Remap(int index, int sub)
            {
                if (remap.TryGetValue(index, out int known)) return known;
                var p = vertices[index];
                int cx = Mathf.FloorToInt((p.x - origin.x) / cell);
                int cy = Mathf.FloorToInt((p.y - origin.y) / cell);
                int cz = Mathf.FloorToInt((p.z - origin.z) / cell);
                long key = ((long)sub << 48) | ((long)(cx & 0xFFFF) << 32) | ((long)(cy & 0xFFFF) << 16) | (long)(cz & 0xFFFF);
                if (!lookup.TryGetValue(key, out int id))
                {
                    id = clusters.Count;
                    lookup[key] = id;
                    clusters.Add(new Cluster
                    {
                        uv = uvs != null ? uvs[index] : Vector2.zero,
                        tangent = tangents != null ? tangents[index] : new Vector4(1f, 0f, 0f, 1f),
                        firstNormal = normals != null ? normals[index] : Vector3.up,
                    });
                }
                var cluster = clusters[id];
                cluster.position += p;
                if (normals != null) cluster.normal += normals[index];
                cluster.count++;
                remap[index] = id;
                return id;
            }
        }

        /// <param name="cellsAlongLongest">Grid cells along the mesh's longest side: fewer means fewer triangles.</param>
        public static Mesh Simplify(Mesh source, int cellsAlongLongest, string name = null)
        {
            var size = source.bounds.size;
            float cell = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) / Mathf.Max(2, cellsAlongLongest);
            if (cell <= 0f) return source;
            var origin = source.bounds.min;

            var vertices = source.vertices;
            var normals = source.normals;
            var uvs = source.uv;
            var tangents = source.tangents;
            bool hasNormals = normals.Length == vertices.Length;
            bool hasUvs = uvs.Length == vertices.Length;
            bool hasTangents = tangents.Length == vertices.Length;

            var grid = new Grid
            {
                vertices = vertices, normals = hasNormals ? normals : null, uvs = hasUvs ? uvs : null,
                tangents = hasTangents ? tangents : null, origin = origin, cell = cell,
            };
            var clusters = grid.clusters;
            var submeshTriangles = new List<List<int>>();
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var triangles = source.GetTriangles(s);
                var kept = new List<int>(triangles.Length / 4);
                var seen = new HashSet<long>();
                grid.remap.Clear();
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = grid.Remap(triangles[t], s), b = grid.Remap(triangles[t + 1], s), c = grid.Remap(triangles[t + 2], s);
                    if (a == b || b == c || a == c) continue;
                    // The same three corners in the same turn: keep one.
                    int lo = Mathf.Min(a, Mathf.Min(b, c));
                    int first = lo == a ? a : lo == b ? b : c;
                    int second = lo == a ? b : lo == b ? c : a;
                    int third = lo == a ? c : lo == b ? a : b;
                    if (!seen.Add(((long)first << 42) | ((long)second << 21) | (long)third)) continue;
                    kept.Add(a);
                    kept.Add(b);
                    kept.Add(c);
                }
                submeshTriangles.Add(kept);

            }

            var outVertices = new Vector3[clusters.Count];
            var outNormals = new Vector3[clusters.Count];
            var outUvs = new Vector2[clusters.Count];
            var outTangents = new Vector4[clusters.Count];
            for (int i = 0; i < clusters.Count; i++)
            {
                var c = clusters[i];
                outVertices[i] = c.position / Mathf.Max(1, c.count);
                // Both sides of a thin fin cancel out: keep one side's normal.
                outNormals[i] = c.normal.sqrMagnitude > 1e-6f ? c.normal.normalized : c.firstNormal;
                outUvs[i] = c.uv;
                outTangents[i] = c.tangent;
            }

            var mesh = new Mesh { name = name ?? source.name + " (far)" };
            if (outVertices.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = outVertices;
            mesh.normals = outNormals;
            mesh.uv = outUvs;
            mesh.tangents = outTangents;
            mesh.subMeshCount = submeshTriangles.Count;
            for (int s = 0; s < submeshTriangles.Count; s++) mesh.SetTriangles(submeshTriangles[s], s, false);
            mesh.bounds = source.bounds;
            return mesh;
        }
    }
}
