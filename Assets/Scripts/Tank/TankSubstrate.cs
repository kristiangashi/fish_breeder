using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// The bed of a tank, built at runtime: a gently uneven surface (higher towards the back, as
    /// aquascapers slope it) with a tiling texture and normal map drawn in code, and a skirt down to
    /// the base so the layer shows through the front glass. Fresh water tanks get river gravel
    /// (a pebble texture plus a few hundred real 3D pebbles on top for depth); salt water tanks get
    /// fine rippled sand with a sprinkle of crushed coral. Textures are made once and shared.
    /// </summary>
    public static class TankSubstrate
    {
        public enum Kind { Gravel, Sand }

        const int TextureSize = 256;
        /// <summary>Metres of tank floor one copy of the texture covers.</summary>
        const float GravelTile = 0.3f, SandTile = 0.4f;

        static Texture2D gravelAlbedo, gravelNormal, sandAlbedo, sandNormal, palette;
        static Mesh pebbleShape;

        static readonly Color[] GravelColors =
        {
            new Color(0.55f, 0.5f, 0.45f), new Color(0.42f, 0.38f, 0.33f), new Color(0.68f, 0.62f, 0.52f), new Color(0.3f, 0.28f, 0.26f),
            new Color(0.6f, 0.46f, 0.36f), new Color(0.76f, 0.73f, 0.68f), new Color(0.48f, 0.44f, 0.4f), new Color(0.37f, 0.31f, 0.25f),
        };
        static readonly Color[] CoralColors =
        {
            new Color(0.97f, 0.95f, 0.9f), new Color(0.95f, 0.85f, 0.8f), new Color(0.9f, 0.88f, 0.82f), new Color(0.98f, 0.92f, 0.85f),
            new Color(0.85f, 0.8f, 0.72f), new Color(0.96f, 0.8f, 0.76f), new Color(0.92f, 0.9f, 0.86f), new Color(0.88f, 0.84f, 0.78f),
        };

        /// <summary>Builds the bed for a tank of this inner size under <paramref name="parent"/>, reaching <paramref name="depth"/> below the floor.</summary>
        public static GameObject Build(Transform parent, Vector3 size, Kind kind, float depth)
        {
            var root = new GameObject(kind == Kind.Sand ? "Sand" : "Gravel");
            root.transform.SetParent(parent, false);
            bool sand = kind == Kind.Sand;
            int seed = GameRandom.Range(0, 100000);

            var bed = new GameObject("Bed");
            bed.transform.SetParent(root.transform, false);
            bed.AddComponent<MeshFilter>().sharedMesh = BedMesh(size, depth, sand, seed);
            var material = MaterialFactory.Create(Color.white, sand ? 0.08f : 0.18f);
            material.mainTexture = sand ? SandAlbedo : GravelAlbedo;
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", sand ? SandNormal : GravelNormal);
                material.EnableKeyword("_NORMALMAP");
            }
            bed.AddComponent<MeshRenderer>().sharedMaterial = material;

            // Loose pieces on top: pebbles on gravel, a sprinkle of coral bits on sand.
            float area = size.x * size.z / (1.6f * 0.7f);
            int count = Mathf.RoundToInt((sand ? 45f : 240f) * area);
            var stones = new GameObject("Stones");
            stones.transform.SetParent(root.transform, false);
            stones.AddComponent<MeshFilter>().sharedMesh = StonesMesh(size, sand, seed, count);
            var stoneMaterial = MaterialFactory.Create(Color.white, sand ? 0.3f : 0.25f);
            stoneMaterial.mainTexture = Palette;
            stones.AddComponent<MeshRenderer>().sharedMaterial = stoneMaterial;
            return root;
        }

        /// <summary>Height of the bed above the tank floor at (x, z) in tank space.</summary>
        static float Height(float x, float z, Vector3 size, bool sand, int seed)
        {
            float back = Mathf.Clamp01(z / size.z + 0.5f);
            if (sand)
            {
                // Soft dunes and the little ripples water leaves in sand.
                float dunes = (ValueNoise(x * 4f, z * 4f, seed) - 0.5f) * 0.008f;
                float bend = ValueNoise(x * 1.5f, z * 1.5f, seed + 7) * 1.4f;
                float ripples = Mathf.Sin((x * 0.8f + z * 0.6f) * 70f + bend * 6f) * 0.0018f;
                return -0.003f + back * 0.006f + dunes + ripples;
            }
            float mounds = (ValueNoise(x * 6f, z * 6f, seed) - 0.5f) * 0.008f;
            float grain = (ValueNoise(x * 40f, z * 40f, seed + 3) - 0.5f) * 0.002f;
            return -0.004f + back * 0.009f + mounds + grain;
        }

        static Mesh BedMesh(Vector3 size, float depth, bool sand, int seed)
        {
            float tile = sand ? SandTile : GravelTile;
            int nx = Mathf.Max(8, Mathf.RoundToInt(size.x / 0.02f));
            int nz = Mathf.Max(4, Mathf.RoundToInt(size.z / 0.02f));
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
            {
                float x = (i / (float)nx - 0.5f) * size.x;
                float z = (j / (float)nz - 0.5f) * size.z;
                vertices.Add(new Vector3(x, Height(x, z, size, sand, seed), z));
                uvs.Add(new Vector2(x / tile, z / tile));
            }
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }

            // Skirts on all four sides, from the surface down to the base, facing out through the glass.
            Skirt(vertices, uvs, triangles, nx, i => i, nx + 1, Vector3.back, -depth, tile, true);                      // front (j = 0)
            Skirt(vertices, uvs, triangles, nx, i => nz * (nx + 1) + i, nx + 1, Vector3.forward, -depth, tile, false);  // back
            Skirt(vertices, uvs, triangles, nz, j => j * (nx + 1), nx + 1, Vector3.left, -depth, tile, false);          // left
            Skirt(vertices, uvs, triangles, nz, j => j * (nx + 1) + nx, nx + 1, Vector3.right, -depth, tile, true);     // right

            var mesh = new Mesh { name = sand ? "SandBed" : "GravelBed" };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A strip from the surface edge (vertex indices from <paramref name="edge"/>) straight down to <paramref name="bottom"/>.</summary>
        static void Skirt(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, int segments, System.Func<int, int> edge, int stride,
            Vector3 outward, float bottom, float tile, bool flip)
        {
            int start = vertices.Count;
            for (int k = 0; k <= segments; k++)
            {
                var top = vertices[edge(k)];
                float along = Mathf.Abs(outward.x) > 0.5f ? top.z : top.x;
                vertices.Add(top);
                uvs.Add(new Vector2(along / tile, top.y / tile));
                vertices.Add(new Vector3(top.x, bottom, top.z));
                uvs.Add(new Vector2(along / tile, bottom / tile));
            }
            for (int k = 0; k < segments; k++)
            {
                int t0 = start + k * 2, b0 = t0 + 1, t1 = t0 + 2, b1 = t0 + 3;
                if (flip)
                {
                    triangles.Add(t0); triangles.Add(t1); triangles.Add(b0);
                    triangles.Add(t1); triangles.Add(b1); triangles.Add(b0);
                }
                else
                {
                    triangles.Add(t0); triangles.Add(b0); triangles.Add(t1);
                    triangles.Add(t1); triangles.Add(b0); triangles.Add(b1);
                }
            }
        }

        /// <summary>All the loose pebbles (or coral bits) in one mesh; each takes its colour from a texel of the palette.</summary>
        static Mesh StonesMesh(Vector3 size, bool sand, int seed, int count)
        {
            var shape = PebbleShape;
            var baseVertices = shape.vertices;
            var baseTriangles = shape.triangles;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var random = new System.Random(seed);
            System.Func<float> R = () => (float)random.NextDouble();

            for (int n = 0; n < count; n++)
            {
                float radius = sand ? Mathf.Lerp(0.003f, 0.007f, R()) : Mathf.Lerp(0.006f, 0.017f, R() * R());
                float x = (R() - 0.5f) * (size.x - radius * 4f);
                float z = (R() - 0.5f) * (size.z - radius * 4f);
                var scale = new Vector3(radius * Mathf.Lerp(1f, 1.5f, R()), radius * Mathf.Lerp(0.45f, 0.75f, R()), radius);
                var rotation = Quaternion.Euler(Mathf.Lerp(-12f, 12f, R()), R() * 360f, Mathf.Lerp(-12f, 12f, R()));
                var position = new Vector3(x, Height(x, z, size, sand, seed) + scale.y * 0.35f, z);
                var uv = new Vector2((random.Next(8) + 0.5f) / 8f, sand ? 0.75f : 0.25f);
                int first = vertices.Count;
                int bumpSeed = random.Next(1000);
                for (int v = 0; v < baseVertices.Length; v++)
                {
                    var p = baseVertices[v];
                    // Knock the round shape about a little so no two pebbles match.
                    float bump = 0.85f + 0.25f * Hash(v * 13 + bumpSeed * 7);
                    vertices.Add(position + rotation * Vector3.Scale(p * bump, scale));
                    uvs.Add(uv);
                }
                foreach (var t in baseTriangles) triangles.Add(first + t);
            }

            var mesh = new Mesh { name = sand ? "CoralBits" : "Pebbles" };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ shapes and textures

        /// <summary>A once-subdivided icosahedron: 42 vertices, enough for a pebble this small.</summary>
        static Mesh PebbleShape
        {
            get
            {
                if (pebbleShape != null) return pebbleShape;
                float t = (1f + Mathf.Sqrt(5f)) / 2f;
                var vertices = new List<Vector3>
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
                };
                for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;
                int[] faces =
                {
                    0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                    3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
                };
                var midpoints = new Dictionary<long, int>();
                var triangles = new List<int>();
                for (int f = 0; f < faces.Length; f += 3)
                {
                    int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                    int ab = Midpoint(vertices, midpoints, a, b), bc = Midpoint(vertices, midpoints, b, c), ca = Midpoint(vertices, midpoints, c, a);
                    triangles.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                pebbleShape = new Mesh { name = "Pebble" };
                pebbleShape.SetVertices(vertices);
                pebbleShape.SetTriangles(triangles, 0);
                return pebbleShape;
            }
        }

        static int Midpoint(List<Vector3> vertices, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out var index)) return index;
            vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
            cache[key] = vertices.Count - 1;
            return vertices.Count - 1;
        }

        /// <summary>Two rows of eight colours: gravel stones on the bottom row, coral bits on the top.</summary>
        static Texture2D Palette
        {
            get
            {
                if (palette != null) return palette;
                palette = new Texture2D(8, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "SubstratePalette" };
                for (int i = 0; i < 8; i++)
                {
                    palette.SetPixel(i, 0, GravelColors[i]);
                    palette.SetPixel(i, 1, CoralColors[i]);
                }
                palette.Apply();
                return palette;
            }
        }

        static Texture2D GravelAlbedo
        {
            get
            {
                if (gravelAlbedo == null) MakeGravel();
                return gravelAlbedo;
            }
        }

        static Texture2D GravelNormal
        {
            get
            {
                if (gravelNormal == null) MakeGravel();
                return gravelNormal;
            }
        }

        static Texture2D SandAlbedo
        {
            get
            {
                if (sandAlbedo == null) MakeSand();
                return sandAlbedo;
            }
        }

        static Texture2D SandNormal
        {
            get
            {
                if (sandNormal == null) MakeSand();
                return sandNormal;
            }
        }

        /// <summary>
        /// Tiling gravel: a jittered Voronoi pattern where every cell is a stone with its own colour,
        /// domed in the middle and dark in the cracks between stones.
        /// </summary>
        static void MakeGravel()
        {
            const int cells = 11;
            var points = new Vector2[cells * cells];
            var colors = new Color[cells * cells];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vector2(0.15f + 0.7f * Hash(i * 3 + 1), 0.15f + 0.7f * Hash(i * 3 + 2));
                var c = GravelColors[(int)(Hash(i * 3 + 3) * 7.999f)];
                colors[i] = c * Mathf.Lerp(0.85f, 1.12f, Hash(i * 5 + 11));
            }

            var albedo = new Color[TextureSize * TextureSize];
            var height = new float[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float u = x / (float)TextureSize * cells, v = y / (float)TextureSize * cells;
                int cx = Mathf.FloorToInt(u), cy = Mathf.FloorToInt(v);
                float f1 = 9f, f2 = 9f;
                int nearest = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cx + dx, gy = cy + dy;
                    int wx = (gx % cells + cells) % cells, wy = (gy % cells + cells) % cells;
                    int id = wy * cells + wx;
                    float d = Vector2.Distance(new Vector2(u, v), new Vector2(gx + points[id].x, gy + points[id].y));
                    if (d < f1) { f2 = f1; f1 = d; nearest = id; }
                    else if (d < f2) f2 = d;
                }
                float edge = Mathf.Clamp01((f2 - f1) / 0.35f);
                float dome = Mathf.Sqrt(edge);
                float speckle = 0.94f + 0.12f * Hash(x * 7919 + y * 104729);
                var color = colors[nearest] * Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, edge * 2.2f)) * speckle;
                color.a = 1f;
                albedo[y * TextureSize + x] = color;
                height[y * TextureSize + x] = dome;
            }
            gravelAlbedo = MakeTexture("GravelAlbedo", albedo, false);
            gravelNormal = MakeTexture("GravelNormal", NormalsFromHeight(height, 3f), true);
        }

        /// <summary>Tiling sand: fine grains, soft mottling and the odd dark or white speck.</summary>
        static void MakeSand()
        {
            var baseColor = new Color(0.9f, 0.85f, 0.74f);
            var albedo = new Color[TextureSize * TextureSize];
            var noise = new float[TextureSize * TextureSize];
            for (int i = 0; i < noise.Length; i++) noise[i] = Hash(i * 31 + 17);
            var height = new float[noise.Length];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                // A little blur turns single-pixel noise into grains.
                float sum = 0f;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    sum += noise[Wrap(y + dy) * TextureSize + Wrap(x + dx)];
                height[y * TextureSize + x] = sum / 9f;

                float mottle = TileNoise(x / (float)TextureSize, y / (float)TextureSize, 6);
                float grain = noise[y * TextureSize + x];
                var color = baseColor * (0.9f + 0.12f * grain + 0.1f * (mottle - 0.5f));
                float speck = Hash(x * 92821 + y * 68917);
                if (speck > 0.985f) color *= 0.55f;
                else if (speck < 0.02f) color = Color.Lerp(color, Color.white, 0.6f);
                color.a = 1f;
                albedo[y * TextureSize + x] = color;
            }
            sandAlbedo = MakeTexture("SandAlbedo", albedo, false);
            sandNormal = MakeTexture("SandNormal", NormalsFromHeight(height, 1.2f), true);
        }

        static Color[] NormalsFromHeight(float[] height, float strength)
        {
            var normals = new Color[height.Length];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float dx = height[y * TextureSize + Wrap(x + 1)] - height[y * TextureSize + Wrap(x - 1)];
                float dy = height[Wrap(y + 1) * TextureSize + x] - height[Wrap(y - 1) * TextureSize + x];
                var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                normals[y * TextureSize + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            return normals;
        }

        static Texture2D MakeTexture(string name, Color[] pixels, bool linear)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true, linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        static int Wrap(int i) => (i % TextureSize + TextureSize) % TextureSize;

        /// <summary>Repeatable pseudo-random number in 0..1 for an integer.</summary>
        static float Hash(int n)
        {
            unchecked
            {
                uint h = (uint)n * 747796405u + 2891336453u;
                h = ((h >> (int)((h >> 28) + 4u)) ^ h) * 277803737u;
                h = (h >> 22) ^ h;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>Smooth value noise in 0..1.</summary>
        static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix * 374761 + iy * 668265 + seed * 9973);
            float b = Hash((ix + 1) * 374761 + iy * 668265 + seed * 9973);
            float c = Hash(ix * 374761 + (iy + 1) * 668265 + seed * 9973);
            float d = Hash((ix + 1) * 374761 + (iy + 1) * 668265 + seed * 9973);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Value noise that tiles across 0..1 with <paramref name="cells"/> cells per side.</summary>
        static float TileNoise(float u, float v, int cells)
        {
            float x = u * cells, y = v * cells;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(Corner(ix, iy, cells), Corner(ix + 1, iy, cells), fx), Mathf.Lerp(Corner(ix, iy + 1, cells), Corner(ix + 1, iy + 1, cells), fx), fy);
        }

        static float Corner(int cx, int cy, int cells) => Hash(((cx % cells + cells) % cells) * 7919 + ((cy % cells + cells) % cells) * 104729);
    }
}
