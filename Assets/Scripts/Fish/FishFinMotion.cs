using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Mouth and fin movement for the big (medium) fish, baked into a look's mesh so the FishSwim
    /// shader can play it on top of the body wave. Each vertex gets, in uv channel 3, how far it moves
    /// at full swing (xyz, in the look's canonical space) and which motion drives it (w = channel + 0.9 * lag,
    /// lag being how far behind in the cycle it follows, 0..1):
    /// 1 the mouth (lower jaw drops around a hinge behind the mouth corners, the upper lip lifts a little),
    /// 2 the pectoral fins (they flare out from their roots and fold back, tips trailing),
    /// 3 the dorsal and ventral fins (a sideways ripple running from head to tail),
    /// 4 the tail fin (its rays flex behind the body's beat).
    /// Small fish don't get the channel, so the shader leaves them as they were.
    /// </summary>
    public static class FishFinMotion
    {
        public const int UvChannel = 3;
        const int Mouth = 1, Pectoral = 2, Median = 3, Tail = 4;
        const int Bins = 20;
        const float JawDegrees = 24f, UpperLipDegrees = 7f, PectoralDegrees = 38f;
        /// <summary>Sideways swing at the tips of the dorsal/ventral fins and the tail, as a share of the fish's length.</summary>
        const float MedianSwing = 0.035f, TailSwing = 0.045f;

        /// <summary>
        /// Writes the movement channel into <paramref name="mesh"/>, the pieces combined kit by kit in
        /// <paramref name="order"/> (as FishVisual merges them). False if the body doesn't move its mouth and fins.
        /// </summary>
        public static bool Write(Mesh mesh, List<FishParts.Piece> pieces, List<FishModelLibrary.Entry> order)
        {
            var body = pieces[0].entry;
            if (body == null || !body.animateMouthAndFins) return false;

            // The closed body's outline along its length, to tell how far each fin point stands out.
            float zCut = float.MaxValue, zNose = float.MinValue;
            foreach (var piece in pieces)
            {
                if (piece.role != FishParts.Body) continue;
                foreach (var v in piece.mesh.vertices) { zCut = Mathf.Min(zCut, v.z); zNose = Mathf.Max(zNose, v.z); }
            }
            float bodyLength = Mathf.Max(0.0001f, zNose - zCut);
            var top = new float[Bins];
            var bottom = new float[Bins];
            for (int i = 0; i < Bins; i++) { top[i] = float.MinValue; bottom[i] = float.MaxValue; }
            foreach (var piece in pieces)
            {
                if (piece.role != FishParts.Body) continue;
                foreach (var v in piece.mesh.vertices)
                {
                    int i = Bin(v.z, zCut, bodyLength);
                    top[i] = Mathf.Max(top[i], v.y);
                    bottom[i] = Mathf.Min(bottom[i], v.y);
                }
            }
            for (int i = 0; i < Bins; i++)
            {
                if (top[i] > float.MinValue) continue;
                int j = i > 0 ? i - 1 : i + 1;
                top[i] = top[j] > float.MinValue ? top[j] : 0f;
                bottom[i] = bottom[j] < float.MaxValue ? bottom[j] : 0f;
            }

            var data = new List<Vector4>(mesh.vertexCount);
            foreach (var entry in order)
                foreach (var piece in pieces)
                {
                    if (piece.entry != entry) continue;
                    var vertices = piece.mesh.vertices;
                    switch (piece.role)
                    {
                        case FishParts.Body: AddMouth(data, vertices, body); break;
                        case FishParts.Pectoral: AddPectorals(data, vertices); break;
                        case FishParts.Dorsal: AddMedian(data, vertices, top, zCut, bodyLength, true); break;
                        case FishParts.Ventral: AddMedian(data, vertices, bottom, zCut, bodyLength, false); break;
                        case FishParts.Tail: AddTail(data, vertices, zCut); break;
                        default: foreach (var _ in vertices) data.Add(Vector4.zero); break;
                    }
                }
            if (data.Count != mesh.vertexCount)
            {
                Debug.LogWarning($"[FishBreeder] {mesh.name}: mouth and fin movement skipped, vertex count {data.Count} != {mesh.vertexCount}.");
                return false;
            }
            mesh.SetUVs(UvChannel, data);
            return true;
        }

        static int Bin(float z, float zCut, float length) => Mathf.Clamp((int)((z - zCut) / length * Bins), 0, Bins - 1);

        static Vector4 Encode(Vector3 move, int channel, float lag) =>
            move.sqrMagnitude < 1e-12f ? Vector4.zero : new Vector4(move.x, move.y, move.z, channel + 0.9f * Mathf.Repeat(lag, 1f));

        /// <summary>
        /// The lower jaw turns down around a hinge behind the mouth corners (fully from the corners
        /// forward, fading out towards the hinge so the cheeks stretch smoothly); the upper lip lifts a little.
        /// </summary>
        static void AddMouth(List<Vector4> data, Vector3[] vertices, FishModelLibrary.Entry entry)
        {
            float slitY = entry.mouthHeight;
            float corner = entry.mouthCorner;
            float hingeZ = corner - Mathf.Max(0.02f, entry.mouthHinge);
            var hinge = new Vector3(0f, slitY, hingeZ);
            var jaw = Quaternion.AngleAxis(JawDegrees, Vector3.right);       // +X: front end goes down
            var lip = Quaternion.AngleAxis(-UpperLipDegrees, Vector3.right);
            foreach (var p in vertices)
            {
                if (p.z <= hingeZ) { data.Add(Vector4.zero); continue; }
                float along = Mathf.SmoothStep(0f, 1f, (p.z - hingeZ) / (corner - hingeZ));
                // Just below the slit counts as jaw; the lips part over a sliver of height.
                float lower = Mathf.SmoothStep(0f, 1f, (slitY + 0.004f - p.y) / 0.012f);
                float upper = (1f - lower) * along;
                var d = p - hinge;
                var move = (jaw * d - d) * (lower * along) + (lip * d - d) * (upper * along);
                data.Add(Encode(move, Mouth, 0f));
            }
        }

        /// <summary>Each pectoral fin swings out sideways about its root (its front end), the tip trailing a little behind.</summary>
        static void AddPectorals(List<Vector4> data, Vector3[] vertices)
        {
            float cx = 0f;
            foreach (var p in vertices) cx += p.x;
            cx /= Mathf.Max(1, vertices.Length);
            for (int side = -1; side <= 1; side += 2)
            {
                float front = float.MinValue, back = float.MaxValue;
                foreach (var p in vertices)
                {
                    if ((p.x < cx ? -1 : 1) != side) continue;
                    front = Mathf.Max(front, p.z);
                    back = Mathf.Min(back, p.z);
                }
                float span = Mathf.Max(0.0001f, front - back);
                // The root: the innermost points of the front fifth of the fin.
                var root = Vector3.zero;
                int count = 0;
                foreach (var p in vertices)
                    if ((p.x < cx ? -1 : 1) == side && p.z > front - span * 0.2f) { root += p; count++; }
                root /= Mathf.Max(1, count);
                root.x = cx + (root.x - cx) * 0.7f;
                pectoralRoots[side < 0 ? 0 : 1] = root;
                pectoralSpans[side < 0 ? 0 : 1] = span;
            }
            foreach (var p in vertices)
            {
                int s = p.x < cx ? 0 : 1;
                var root = pectoralRoots[s];
                // Turning about a slightly tipped upright axis flares the fin out and a little down.
                var axis = new Vector3(0f, 1f, s == 0 ? 0.35f : -0.35f).normalized;
                var flare = Quaternion.AngleAxis(s == 0 ? PectoralDegrees : -PectoralDegrees, axis);
                var d = p - root;
                float reach = Mathf.Clamp01(d.magnitude / pectoralSpans[s]);
                data.Add(Encode(flare * d - d, Pectoral, 0.18f * reach + (s == 0 ? 0f : 0.06f)));
            }
        }

        static readonly Vector3[] pectoralRoots = new Vector3[2];
        static readonly float[] pectoralSpans = new float[2];

        /// <summary>Dorsal and ventral fins ripple sideways, more the further out from the body, in a wave running towards the tail.</summary>
        static void AddMedian(List<Vector4> data, Vector3[] vertices, float[] edge, float zCut, float bodyLength, bool dorsal)
        {
            var reach = new float[vertices.Length];
            float most = 0.0001f;
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i];
                float e = edge[Bin(p.z, zCut, bodyLength)];
                reach[i] = Mathf.Max(0f, dorsal ? p.y - e : e - p.y);
                most = Mathf.Max(most, reach[i]);
            }
            for (int i = 0; i < vertices.Length; i++)
            {
                float r = reach[i] / most;
                float swing = MedianSwing * r * Mathf.Sqrt(r);
                float lag = (zCut + bodyLength - vertices[i].z) * 1.3f + r * 0.12f;
                data.Add(Encode(new Vector3(swing, 0f, 0f), Median, lag));
            }
        }

        /// <summary>The tail fin's rays flex sideways behind the body's beat, most at the tips.</summary>
        static void AddTail(List<Vector4> data, Vector3[] vertices, float zCut)
        {
            float end = zCut;
            foreach (var p in vertices) end = Mathf.Min(end, p.z);
            float length = Mathf.Max(0.0001f, zCut - end);
            foreach (var p in vertices)
            {
                float d = Mathf.Clamp01((zCut - p.z) / length);
                data.Add(Encode(new Vector3(TailSwing * d * d, 0f, 0f), Tail, 0.28f * d));
            }
        }
    }
}
