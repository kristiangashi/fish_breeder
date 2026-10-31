using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Puts fish together from parts, so any fins and tail fit on any body. Every fish model is
    /// split into objects named Body, Dorsal, Ventral, Pectoral and Tail (see Tools/Meshy/split_fins.py),
    /// plus Fill_&lt;Part&gt; patches that close the body where that part was cut off.
    ///
    /// Parts from the body's own species are used as they are. Parts from another species are
    /// fitted to the body by where they sat along their own body (0 = tail cut, 1 = nose):
    /// dorsal fins follow the new back, ventral fins the new belly, pectorals keep their height on
    /// the flank, and the tail is fixed to the new caudal peduncle. Foreign parts are scaled by how
    /// long the new body is compared to their old one, and the body gets the fill patches for the
    /// parts it lost. Built meshes are shared by every fish with the same look.
    /// </summary>
    public static class FishParts
    {
        public const string Body = "Body";
        public const string Dorsal = "Dorsal";
        public const string Ventral = "Ventral";
        public const string Pectoral = "Pectoral";
        public const string Tail = "Tail";
        public const string FillPrefix = "Fill_";
        static readonly string[] Fins = { Dorsal, Ventral, Pectoral };
        const int Bins = 20;

        /// <summary>One mesh of a built fish and the model it came from (for its texture).</summary>
        public class Piece
        {
            public Mesh mesh;
            public FishModelLibrary.Entry entry;
            /// <summary>Body (fill patches count as body), Dorsal, Ventral, Pectoral or Tail.</summary>
            public string role;
        }

        class Part
        {
            public Mesh mesh;
            /// <summary>From the mesh into the set's canonical space: head along +Z, whole fish 1 long, centred.</summary>
            public Matrix4x4 toCanonical;
        }

        /// <summary>A model's parts in canonical space, and the shape of its body along its length.</summary>
        class PartSet
        {
            public FishModelLibrary.Entry entry;
            public Part body;
            public readonly Dictionary<string, Part> parts = new Dictionary<string, Part>();
            public readonly Dictionary<string, Part> fills = new Dictionary<string, Part>();
            /// <summary>Tail cut (caudal peduncle) and nose along Z.</summary>
            public float zCut, zNose;
            public readonly float[] top = new float[Bins], bottom = new float[Bins], left = new float[Bins], right = new float[Bins];

            public float BodyLength => Mathf.Max(0.0001f, zNose - zCut);
            public float U(float z) => (z - zCut) / BodyLength;
            public float Z(float u) => zCut + u * BodyLength;
            public float Top(float u) => Sample(top, u);
            public float Bottom(float u) => Sample(bottom, u);
            public float CenterX(float u) => (Sample(left, u) + Sample(right, u)) * 0.5f;
            public float HalfWidth(float u) => Mathf.Max(0.0001f, (Sample(right, u) - Sample(left, u)) * 0.5f);
            public float CenterY(float u) => (Top(u) + Bottom(u)) * 0.5f;
            public float Height(float u) => Mathf.Max(0.0001f, Top(u) - Bottom(u));

            static float Sample(float[] bins, float u)
            {
                float f = Mathf.Clamp(u * Bins - 0.5f, 0f, Bins - 1f);
                int i = Mathf.Min((int)f, Bins - 2);
                return Mathf.Lerp(bins[i], bins[i + 1], f - i);
            }
        }

        static readonly Dictionary<GameObject, PartSet> sets = new Dictionary<GameObject, PartSet>();
        static readonly Dictionary<string, List<Piece>> looks = new Dictionary<string, List<Piece>>();

        /// <summary>True if this model is split into parts (has a Body object).</summary>
        public static bool HasParts(FishModelLibrary.Entry entry) => Set(entry) != null;

        /// <summary>Hides the fill patches of an instantiated whole model (they sit inside its own fins).</summary>
        public static void HideFills(GameObject model)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                if (renderer.name.StartsWith(FillPrefix)) renderer.enabled = false;
        }

        /// <summary>
        /// The meshes of a fish with this body, fins and tail, in the body's canonical space (head
        /// along +Z; a pure fish is 1 long and centred). Null if the body model has no parts.
        /// A fins or tail model without parts falls back to the body's own.
        /// </summary>
        public static List<Piece> Assemble(FishModelLibrary.Entry body, FishModelLibrary.Entry fins, FishModelLibrary.Entry tail)
        {
            var b = Set(body);
            if (b == null) return null;
            var f = Set(fins) ?? b;
            var t = Set(tail) ?? b;
            string key = b.entry.model.GetInstanceID() + "|" + f.entry.model.GetInstanceID() + "|" + t.entry.model.GetInstanceID();
            if (looks.TryGetValue(key, out var cached) && cached.TrueForAll(p => p.mesh != null)) return cached;

            var pieces = new List<Piece>();
            pieces.Add(Bake(b.body, b, b, Body, b));
            var filled = new HashSet<string>();
            foreach (var fin in Fins)
            {
                if (f.parts.TryGetValue(fin, out var part)) pieces.Add(Bake(part, f, b, fin, f));
                if (f != b) AddFill(pieces, filled, b, fin);
            }
            if (t.parts.TryGetValue(Tail, out var tailPart)) pieces.Add(Bake(tailPart, t, b, Tail, t));
            if (t != b)
            {
                // Some bodies close their tail cut with the dorsal and ventral patches.
                if (b.fills.ContainsKey(Tail)) AddFill(pieces, filled, b, Tail);
                else
                {
                    AddFill(pieces, filled, b, Dorsal);
                    AddFill(pieces, filled, b, Ventral);
                }
            }
            looks[key] = pieces;
            return pieces;
        }

        static void AddFill(List<Piece> pieces, HashSet<string> filled, PartSet body, string part)
        {
            if (!filled.Add(part) || !body.fills.TryGetValue(part, out var fill)) return;
            pieces.Add(Bake(fill, body, body, Body, body));
        }

        static PartSet Set(FishModelLibrary.Entry entry)
        {
            if (entry == null || entry.model == null) return null;
            if (sets.TryGetValue(entry.model, out var known)) return known;
            var set = Load(entry);
            sets[entry.model] = set;
            return set;
        }

        static PartSet Load(FishModelLibrary.Entry entry)
        {
            var set = new PartSet { entry = entry };
            var root = entry.model.transform;
            var all = new List<KeyValuePair<string, Part>>();
            foreach (var filter in entry.model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                if (!mesh.isReadable)
                {
                    Debug.LogWarning($"[FishBreeder] {mesh.name} is not readable, so {entry.displayName} can't be cross-bred. Enable Read/Write in its import settings.");
                    return null;
                }
                var part = new Part { mesh = mesh, toCanonical = root.worldToLocalMatrix * filter.transform.localToWorldMatrix };
                all.Add(new KeyValuePair<string, Part>(filter.name, part));
                if (filter.name == Body) set.body = part;
                else if (filter.name.StartsWith(FillPrefix)) set.fills[filter.name.Substring(FillPrefix.Length)] = part;
                else if (filter.name == Dorsal || filter.name == Ventral || filter.name == Pectoral || filter.name == Tail) set.parts[filter.name] = part;
            }
            if (set.body == null) return null;

            // Canonical space: head along +Z, the whole pure fish 1 long and centred on the origin.
            var points = new List<Vector3>();
            foreach (var pair in all)
            {
                if (pair.Key.StartsWith(FillPrefix)) continue;
                foreach (var v in pair.Value.mesh.vertices) points.Add(pair.Value.toCanonical.MultiplyPoint3x4(v));
            }
            var turn = Matrix4x4.Rotate(Quaternion.Euler(0f, HeadYaw(points), 0f));
            var bounds = new Bounds(turn.MultiplyPoint3x4(points[0]), Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(turn.MultiplyPoint3x4(p));
            float scale = 1f / Mathf.Max(bounds.size.z, 0.0001f);
            var normalize = Matrix4x4.Scale(Vector3.one * scale) * Matrix4x4.Translate(-bounds.center) * turn;
            foreach (var pair in all) pair.Value.toCanonical = normalize * pair.Value.toCanonical;

            Profile(set);
            return set;
        }

        /// <summary>Measures the closed body (body plus its fill patches) in bins from the tail cut to the nose.</summary>
        static void Profile(PartSet set)
        {
            var points = new List<Vector3>();
            foreach (var v in set.body.mesh.vertices) points.Add(set.body.toCanonical.MultiplyPoint3x4(v));
            foreach (var fill in set.fills.Values)
                foreach (var v in fill.mesh.vertices) points.Add(fill.toCanonical.MultiplyPoint3x4(v));

            set.zCut = float.MaxValue;
            set.zNose = float.MinValue;
            foreach (var p in points)
            {
                set.zCut = Mathf.Min(set.zCut, p.z);
                set.zNose = Mathf.Max(set.zNose, p.z);
            }
            var seen = new bool[Bins];
            for (int i = 0; i < Bins; i++)
            {
                set.top[i] = set.right[i] = float.MinValue;
                set.bottom[i] = set.left[i] = float.MaxValue;
            }
            foreach (var p in points)
            {
                int i = Mathf.Clamp((int)(set.U(p.z) * Bins), 0, Bins - 1);
                seen[i] = true;
                set.top[i] = Mathf.Max(set.top[i], p.y);
                set.bottom[i] = Mathf.Min(set.bottom[i], p.y);
                set.left[i] = Mathf.Min(set.left[i], p.x);
                set.right[i] = Mathf.Max(set.right[i], p.x);
            }
            // Empty bins borrow from the nearest measured one.
            for (int i = 0; i < Bins; i++)
            {
                if (seen[i]) continue;
                for (int d = 1; d < Bins; d++)
                {
                    int j = i - d >= 0 && seen[i - d] ? i - d : i + d < Bins && seen[i + d] ? i + d : -1;
                    if (j < 0) continue;
                    set.top[i] = set.top[j];
                    set.bottom[i] = set.bottom[j];
                    set.left[i] = set.left[j];
                    set.right[i] = set.right[j];
                    break;
                }
            }
        }

        /// <summary>
        /// Copies a part into a new mesh in the body's canonical space, fitting it to the body when
        /// it comes from another species. <paramref name="material"/> is whose texture it uses.
        /// </summary>
        static Piece Bake(Part part, PartSet source, PartSet body, string role, PartSet material)
        {
            var src = part.mesh;
            var vertices = src.vertices;
            var normals = src.normals;
            var tangents = src.tangents;
            var m = part.toCanonical;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = m.MultiplyPoint3x4(vertices[i]);
            for (int i = 0; i < normals.Length; i++) normals[i] = m.MultiplyVector(normals[i]).normalized;
            for (int i = 0; i < tangents.Length; i++)
            {
                var t = m.MultiplyVector(tangents[i]).normalized;
                tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
            }
            if (source != body) Fit(vertices, source, body, role);

            var mesh = new Mesh { name = $"{source.entry.displayName} {role} on {body.entry.displayName}" };
            if (vertices.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            if (normals.Length == vertices.Length) mesh.normals = normals;
            if (tangents.Length == vertices.Length) mesh.tangents = tangents;
            mesh.uv = src.uv;
            var triangles = new List<int>();
            for (int s = 0; s < src.subMeshCount; s++) triangles.AddRange(src.GetTriangles(s));
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            if (normals.Length != vertices.Length) mesh.RecalculateNormals();
            return new Piece { mesh = mesh, entry = material.entry, role = role };
        }

        /// <summary>Moves a part from its own body onto another one (see the class summary).</summary>
        static void Fit(Vector3[] v, PartSet s, PartSet t, string role)
        {
            float k = t.BodyLength / s.BodyLength;
            float sink = 0.012f * t.BodyLength;
            switch (role)
            {
                case Dorsal:
                case Ventral:
                {
                    // The base follows the new back (or belly) exactly. Further out the fin moves as
                    // a whole (a straight-line fit of the base's shift), so it keeps its shape
                    // instead of copying every bump of the old body's outline.
                    bool top = role == Dorsal;
                    int n = v.Length;
                    var us = new float[n];
                    var shift = new float[n];
                    var reachOut = new float[n];
                    float reach = 0.0001f;
                    for (int i = 0; i < n; i++)
                    {
                        us[i] = s.U(v[i].z);
                        float uc = Mathf.Clamp01(us[i]);
                        float edgeS = top ? s.Top(uc) : s.Bottom(uc);
                        float edgeT = top ? t.Top(uc) : t.Bottom(uc);
                        shift[i] = edgeT - k * edgeS;
                        reachOut[i] = Mathf.Abs(v[i].y - edgeS);
                        reach = Mathf.Max(reach, reachOut[i]);
                    }
                    double c = 0, su = 0, suu = 0, sd = 0, sud = 0;
                    for (int i = 0; i < n; i++)
                    {
                        if (reachOut[i] >= reach * 0.2f) continue;
                        c++; su += us[i]; suu += us[i] * us[i]; sd += shift[i]; sud += us[i] * shift[i];
                    }
                    double det = c * suu - su * su;
                    bool line = c >= 3 && System.Math.Abs(det) > 1e-9;
                    float slope = line ? (float)((c * sud - su * sd) / det) : 0f;
                    float intercept = line ? (float)((sd - slope * su) / c) : 0f;
                    for (int i = 0; i < n; i++)
                    {
                        var p = v[i];
                        float uc = Mathf.Clamp01(us[i]);
                        float d = shift[i];
                        if (line) d = Mathf.Lerp(d, intercept + slope * us[i], Mathf.SmoothStep(0f, 1f, reachOut[i] / (reach * 0.35f)));
                        float y = k * p.y + d + (top ? -sink : sink);
                        float x = t.CenterX(uc) + (p.x - s.CenterX(uc)) * k;
                        v[i] = new Vector3(x, y, t.Z(us[i]));
                    }
                    break;
                }

                case Pectoral:
                    // Each side moves rigidly, keeping its height on the flank and how far in it sits.
                    var sides = new int[v.Length];
                    for (int i = 0; i < v.Length; i++) sides[i] = v[i].x < s.CenterX(Mathf.Clamp01(s.U(v[i].z))) ? -1 : 1;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 sum = Vector3.zero;
                        int count = 0;
                        float inner = float.MaxValue;
                        for (int i = 0; i < v.Length; i++)
                        {
                            if (sides[i] != side) continue;
                            var p = v[i];
                            float lateral = Mathf.Max(0f, (p.x - s.CenterX(Mathf.Clamp01(s.U(p.z)))) * side);
                            sum += p;
                            count++;
                            inner = Mathf.Min(inner, lateral);
                        }
                        if (count == 0) continue;
                        var c = sum / count;
                        float u = Mathf.Clamp01(s.U(c.z));
                        float height = (c.y - s.Bottom(u)) / s.Height(u);
                        float depth = Mathf.Clamp01(inner / s.HalfWidth(u));
                        var anchorS = new Vector3(s.CenterX(u) + side * inner, c.y, c.z);
                        var anchorT = new Vector3(t.CenterX(u) + side * (depth * t.HalfWidth(u) - sink), t.Bottom(u) + height * t.Height(u), t.Z(u));
                        for (int i = 0; i < v.Length; i++)
                            if (sides[i] == side) v[i] = anchorT + (v[i] - anchorS) * k;
                    }
                    break;

                case Tail:
                {
                    // Fixed to the peduncle, sized between the body-length and peduncle-height ratios.
                    float kp = t.Height(0f) / s.Height(0f);
                    float kt = Mathf.Clamp(Mathf.Sqrt(k * kp), k * 0.6f, k * 1.5f);
                    var anchorS = new Vector3(s.CenterX(0f), s.CenterY(0f), s.zCut);
                    var anchorT = new Vector3(t.CenterX(0f), t.CenterY(0f), t.zCut + 1.5f * sink);
                    for (int i = 0; i < v.Length; i++) v[i] = anchorT + (v[i] - anchorS) * kt;
                    break;
                }
            }
        }

        /// <summary>
        /// The yaw that points the head along +Z. The body runs along the longer horizontal axis,
        /// and the head is the end that is widest side to side (tail fins are flat).
        /// </summary>
        public static float HeadYaw(List<Vector3> points)
        {
            if (points.Count == 0) return 0f;
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(point);
            bool alongX = bounds.size.x > bounds.size.z;

            float min = alongX ? bounds.min.x : bounds.min.z;
            float length = Mathf.Max(0.0001f, alongX ? bounds.size.x : bounds.size.z);
            float lowMin = float.MaxValue, lowMax = float.MinValue, highMin = float.MaxValue, highMax = float.MinValue;
            foreach (var point in points)
            {
                float t = ((alongX ? point.x : point.z) - min) / length;
                float lateral = alongX ? point.z : point.x;
                if (t < 0.2f) { lowMin = Mathf.Min(lowMin, lateral); lowMax = Mathf.Max(lowMax, lateral); }
                else if (t > 0.8f) { highMin = Mathf.Min(highMin, lateral); highMax = Mathf.Max(highMax, lateral); }
            }
            bool headAtHighEnd = (highMax - highMin) > (lowMax - lowMin);

            if (alongX) return headAtHighEnd ? -90f : 90f;
            return headAtHighEnd ? 0f : 180f;
        }
    }
}
