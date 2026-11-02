using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Meshes that show an illness on a fish, a little exaggerated and cartoony so it reads at a
    /// glance without being gross. They sit in the same space as the fish's parts and use the swim
    /// shader, so they bend with the body.
    ///
    /// Based on what the real illnesses look like: white spot (ich) covers the body and fins in
    /// raised white grains like salt; fin rot eats the fin edges away, leaving them ragged and
    /// whitish with red, sore patches at the base.
    /// </summary>
    public static class FishSymptoms
    {
        /// <summary>White grains all over: mostly the body, some on the fins.</summary>
        public static Mesh Spots(List<FishParts.Piece> pieces, int count, float radius, int seed)
        {
            var random = new System.Random(seed);
            var body = new List<FishParts.Piece>();
            var fins = new List<FishParts.Piece>();
            foreach (var piece in pieces) (piece.role == FishParts.Body ? body : fins).Add(piece);
            var blobs = new MeshBuilder();
            for (int i = 0; i < count; i++)
            {
                var from = fins.Count > 0 && random.NextDouble() < 0.25 ? fins : body;
                if (from.Count == 0) from = pieces;
                var piece = from[random.Next(from.Count)];
                var vertices = piece.mesh.vertices;
                var normals = piece.mesh.normals;
                if (vertices.Length == 0) continue;
                int v = random.Next(vertices.Length);
                var normal = v < normals.Length ? normals[v] : Vector3.up;
                float r = radius * (0.7f + 0.6f * (float)random.NextDouble());
                blobs.Blob(vertices[v] + normal * r * 0.3f, r, normal);
            }
            return blobs.ToMesh("White spot");
        }

        /// <summary>
        /// Ragged, whitish fin edges (<paramref name="edges"/>) and red sore patches at the fin bases
        /// (<paramref name="sores"/>). Each fin's edge is the end furthest from the body.
        /// </summary>
        public static void FinRot(List<FishParts.Piece> pieces, int count, float radius, int seed, out Mesh edges, out Mesh sores)
        {
            var random = new System.Random(seed);
            var edge = new MeshBuilder();
            var sore = new MeshBuilder();
            foreach (var piece in pieces)
            {
                if (piece.role == FishParts.Body) continue;
                var vertices = piece.mesh.vertices;
                var normals = piece.mesh.normals;
                if (vertices.Length < 4) continue;
                float meanX = 0f;
                foreach (var p in vertices) meanX += p.x;
                meanX /= vertices.Length;
                var reach = new float[vertices.Length];
                for (int i = 0; i < vertices.Length; i++) reach[i] = Reach(piece.role, vertices[i], meanX);
                var sorted = (float[])reach.Clone();
                System.Array.Sort(sorted);
                float outer = sorted[(int)(sorted.Length * 0.72f)];
                float inner = sorted[(int)(sorted.Length * 0.22f)];
                int perFin = Mathf.Max(3, count / 4);
                for (int n = 0, tries = 0; n < perFin && tries < perFin * 20; tries++)
                {
                    int v = random.Next(vertices.Length);
                    if (reach[v] < outer) continue;
                    var normal = v < normals.Length ? normals[v] : Vector3.up;
                    edge.Blob(vertices[v], radius * (0.8f + 0.7f * (float)random.NextDouble()), normal);
                    n++;
                }
                for (int n = 0, tries = 0; n < perFin / 2 && tries < perFin * 20; tries++)
                {
                    int v = random.Next(vertices.Length);
                    if (reach[v] > inner) continue;
                    var normal = v < normals.Length ? normals[v] : Vector3.up;
                    sore.Blob(vertices[v], radius * (0.6f + 0.4f * (float)random.NextDouble()), normal);
                    n++;
                }
            }
            edges = edge.ToMesh("Fin rot edges");
            sores = sore.ToMesh("Fin rot sores");
        }

        /// <summary>How far out along its fin a point is (higher = nearer the edge).</summary>
        static float Reach(string role, Vector3 p, float meanX)
        {
            switch (role)
            {
                case FishParts.Tail: return -p.z;
                case FishParts.Dorsal: return p.y;
                case FishParts.Ventral: return -p.y;
                default: return Mathf.Abs(p.x - meanX);
            }
        }

        /// <summary>Little rounded lumps (squashed octahedra, smooth-shaded).</summary>
        class MeshBuilder
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<int> triangles = new List<int>();

            static readonly Vector3[] Corners =
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
            };
            static readonly int[] Faces =
            {
                2, 4, 0,  2, 0, 5,  2, 5, 1,  2, 1, 4,
                3, 0, 4,  3, 5, 0,  3, 1, 5,  3, 4, 1,
            };

            public void Blob(Vector3 centre, float radius, Vector3 normal)
            {
                // Flatter against the skin, so it reads as a raised spot rather than a ball.
                var rotation = Quaternion.FromToRotation(Vector3.up, normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up);
                int start = vertices.Count;
                foreach (var corner in Corners)
                {
                    var local = new Vector3(corner.x, corner.y * 0.55f, corner.z);
                    vertices.Add(centre + rotation * local * radius);
                    normals.Add(rotation * corner);
                }
                foreach (int index in Faces) triangles.Add(start + index);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
