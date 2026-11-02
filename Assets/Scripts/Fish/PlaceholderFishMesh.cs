using System.Collections.Generic;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Builds simple but fully 3D fish parts in code, driven by genes. Used until Meshy
    /// models are assigned in a FishModelLibrary. Fish face +Z, up is +Y.
    /// </summary>
    public static class PlaceholderFishMesh
    {
        /// <summary>Body radius (0..1) along the length; t = 0 at the tail, 1 at the nose.</summary>
        public static float Profile(float t)
        {
            float s = Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(t), 1.4f)));
            return Mathf.Pow(s, 0.6f);
        }

        /// <summary>Laterally flattened, teardrop-shaped body. UV.x runs tail→nose, UV.y around the body (0 = back, 0.5 = belly).</summary>
        public static Mesh Body(float length, float height, float width, int rings = 28, int sides = 24)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, t);
                float r = Profile(t);
                for (int j = 0; j <= sides; j++)
                {
                    float v = j / (float)sides;
                    float angle = v * Mathf.PI * 2f;
                    float x = Mathf.Sin(angle) * r * width * 0.5f;
                    float y = Mathf.Cos(angle) * r * height * 0.5f;
                    vertices.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(t, v));
                }
            }

            int stride = sides + 1;
            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int v0 = i * stride + j;
                    int v1 = v0 + 1;
                    int v2 = v0 + stride;
                    int v3 = v2 + 1;
                    triangles.Add(v0); triangles.Add(v2); triangles.Add(v1);
                    triangles.Add(v1); triangles.Add(v2); triangles.Add(v3);
                }
            }

            var mesh = new Mesh { name = "FishBody" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Tail fin in the YZ plane, starting at the origin and extending backwards (-Z).</summary>
        public static Mesh Tail(TailShape shape, float size)
        {
            var outline = TailOutline(shape);
            var points = new List<Vector3> { Vector3.zero };
            foreach (var p in outline) points.Add(new Vector3(0f, p.y * size, p.x * size));

            var triangles = new List<int>();
            for (int i = 1; i < points.Count - 1; i++)
            {
                triangles.Add(0); triangles.Add(i); triangles.Add(i + 1);
            }
            return DoubleSided("FishTail", points, triangles);
        }

        /// <summary>
        /// Dorsal-style fin standing up (+Y) from a base along Z. Built as a strip from the
        /// base line to the outline, so jagged shapes triangulate cleanly.
        /// </summary>
        public static Mesh Fin(FinShape shape, float length, float height)
        {
            var outline = FinOutline(shape);
            var points = new List<Vector3>();
            foreach (var p in outline)
            {
                points.Add(new Vector3(0f, 0f, p.x * length));
                points.Add(new Vector3(0f, p.y * height, p.x * length));
            }

            var triangles = new List<int>();
            for (int i = 0; i < outline.Count - 1; i++)
            {
                int b0 = i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                triangles.Add(b0); triangles.Add(t0); triangles.Add(t1);
                triangles.Add(b0); triangles.Add(t1); triangles.Add(b1);
            }
            return DoubleSided("FishFin", points, triangles);
        }

        // Outlines are (z, y) pairs in fin-length units.

        static List<Vector2> TailOutline(TailShape shape)
        {
            switch (shape)
            {
                case TailShape.Round:
                    return WithRoot(Arc(new Vector2(-0.5f, 0f), 0.45f, 10));
                case TailShape.Lyre:
                    return WithRoot(new List<Vector2>
                    {
                        new Vector2(-1.3f, 0.8f), new Vector2(-0.9f, 0.45f), new Vector2(-0.5f, 0f),
                        new Vector2(-0.9f, -0.45f), new Vector2(-1.3f, -0.8f),
                    });
                case TailShape.Sword:
                    return WithRoot(new List<Vector2>
                    {
                        new Vector2(-0.6f, 0.35f), new Vector2(-0.5f, 0f),
                        new Vector2(-1.6f, -0.3f), new Vector2(-1.5f, -0.45f),
                    });
                case TailShape.Veil:
                    return WithRoot(Arc(new Vector2(-0.9f, -0.25f), 0.75f, 14), bottomRoot: new Vector2(-0.05f, -0.12f));
                default: // Fork
                    return WithRoot(new List<Vector2>
                    {
                        new Vector2(-1f, 0.55f), new Vector2(-0.55f, 0f), new Vector2(-1f, -0.55f),
                    });
            }
        }

        static List<Vector2> FinOutline(FinShape shape)
        {
            const int samples = 11;
            var points = new List<Vector2>();
            for (int i = 0; i < samples; i++)
            {
                float s = i / (float)(samples - 1);
                float wave = Mathf.Sin(Mathf.PI * s);
                switch (shape)
                {
                    case FinShape.Fan:
                    {
                        float y = wave;
                        points.Add(new Vector2(0.5f - s - 0.3f * y, y));
                        break;
                    }
                    case FinShape.Spiky:
                        points.Add(new Vector2(0.5f - s, wave * (i % 2 == 1 ? 1f : 0.45f)));
                        break;
                    case FinShape.Veil:
                        points.Add(new Vector2(0.5f - 1.6f * s, 1.1f * Mathf.Sin(Mathf.PI * s * 0.85f)));
                        break;
                    case FinShape.Ribbon:
                        points.Add(new Vector2(0.7f - 1.6f * s, 0.45f * wave * (0.8f + 0.2f * Mathf.Sin(6f * Mathf.PI * s))));
                        break;
                    default: // Round
                        points.Add(new Vector2(0.5f - s, 0.7f * wave));
                        break;
                }
            }
            return points;
        }

        static List<Vector2> Arc(Vector2 center, float radius, int segments)
        {
            var points = new List<Vector2>();
            for (int k = 0; k <= segments; k++)
            {
                float phi = Mathf.Lerp(90f, -90f, k / (float)segments) * Mathf.Deg2Rad;
                points.Add(center + radius * new Vector2(-Mathf.Cos(phi), Mathf.Sin(phi)));
            }
            return points;
        }

        static List<Vector2> WithRoot(List<Vector2> outline, Vector2? bottomRoot = null)
        {
            outline.Insert(0, new Vector2(-0.1f, 0.1f));
            outline.Add(bottomRoot ?? new Vector2(-0.1f, -0.1f));
            return outline;
        }

        static Mesh DoubleSided(string name, List<Vector3> points, List<int> triangles)
        {
            var vertices = new List<Vector3>(points);
            vertices.AddRange(points);
            var allTriangles = new List<int>(triangles);
            int offset = points.Count;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                allTriangles.Add(triangles[i] + offset);
                allTriangles.Add(triangles[i + 2] + offset);
                allTriangles.Add(triangles[i + 1] + offset);
            }

            var uvs = new List<Vector2>();
            foreach (var v in vertices) uvs.Add(new Vector2(v.z, v.y));

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(allTriangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
