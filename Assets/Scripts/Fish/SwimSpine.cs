using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// The procedural backbone of a swimming fish, sampled at <see cref="SampleCount"/> points
    /// from nose to tail tip and handed to the FishSwim shader each frame.
    ///
    /// Three layers, from the ground up:
    /// 1. Path following: the body is laid along the path the nose has just travelled, so a turn
    ///    bends the whole fish into a curve instead of pivoting it like a plank.
    /// 2. Carangiform body wave: a travelling sine from head to tail with the classic mackerel
    ///    amplitude envelope (Videler and Hess 1984): about 2% of body length at the nose,
    ///    nearly still at a third of the way back, 10% at the tail tip. One wavelength per body.
    /// 3. Roll: banking into turns, plus a small twist wave on the tail (as in ABZÛ's fish).
    /// </summary>
    public class SwimSpine
    {
        public const int SampleCount = 16;

        /// <summary>Lateral amplitude as a fraction of body length at s (0 = nose, 1 = tail tip).</summary>
        public static float Envelope(float s) => 0.02f - 0.0825f * s + 0.1625f * s * s;

        // Recorded nose positions in world space, newest first.
        readonly List<Vector3> trail = new List<Vector3>();
        readonly List<Vector3> trailUp = new List<Vector3>();
        readonly Vector3[] spinePoints = new Vector3[SampleCount];
        readonly Vector3[] spineUps = new Vector3[SampleCount];
        readonly Vector3[] midline = new Vector3[SampleCount];

        public readonly Vector4[] Positions = new Vector4[SampleCount];
        public readonly Vector4[] Forwards = new Vector4[SampleCount];
        public readonly Vector4[] Sides = new Vector4[SampleCount];
        public readonly Vector4[] Ups = new Vector4[SampleCount];

        /// <summary>Body waves per body length (1 / wavelength).</summary>
        public float wavesPerBody = 1f;
        /// <summary>Peak tail twist in degrees.</summary>
        public float tailTwistDegrees = 8f;
        /// <summary>0..1: how much of the front of the body stays straight (deep-bodied fish are stiff).</summary>
        public float stiffness;
        /// <summary>Where the body starts to wiggle, as a fraction of its length from the nose (the species' bendStart).</summary>
        public float bendStart = 0.2f;

        /// <summary>
        /// How much of the swimming wave reaches s (0 = nose, 1 = tail tip): none in front of
        /// <paramref name="bendStart"/>, growing to full over the next <see cref="Genetics.Species.BendRamp"/> of the body.
        /// </summary>
        public static float BendMask(float s, float bendStart)
        {
            float end = Mathf.Min(1f, bendStart + Genetics.Species.BendRamp);
            return end <= bendStart ? (s >= bendStart ? 1f : 0f) : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bendStart, end, s));
        }

        public void Reset(Vector3 nose, Vector3 forward, Vector3 up, float length)
        {
            trail.Clear();
            trailUp.Clear();
            trail.Add(nose - forward * (length * 1.5f));
            trailUp.Add(up);
        }

        /// <summary>Call once per frame before <see cref="Evaluate"/>.</summary>
        public void Record(Vector3 nose, Vector3 forward, Vector3 up, float length)
        {
            if (trail.Count == 0)
            {
                Reset(nose, forward, up, length);
                return;
            }

            float spacing = length / 24f;
            if ((nose - trail[0]).sqrMagnitude >= spacing * spacing)
            {
                trail.Insert(0, nose);
                trailUp.Insert(0, up);
            }

            // Keep a bit more than one body length of history.
            float total = (nose - trail[0]).magnitude;
            for (int i = 1; i < trail.Count; i++)
            {
                total += (trail[i] - trail[i - 1]).magnitude;
                if (total > length * 1.5f && i + 1 < trail.Count)
                {
                    trail.RemoveRange(i + 1, trail.Count - i - 1);
                    trailUp.RemoveRange(i + 1, trailUp.Count - i - 1);
                    break;
                }
            }
        }

        /// <summary>
        /// Eases the recorded path towards a straight line behind the nose. While a fish hangs in
        /// place and turns, its nose draws a tight arc; without this, the body would snake through
        /// that old arc the moment it sets off.
        /// </summary>
        public void Relax(Vector3 nose, Vector3 forward, float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (amount <= 0f) return;
            float distance = 0f;
            var previous = nose;
            for (int i = 0; i < trail.Count; i++)
            {
                distance += (trail[i] - previous).magnitude;
                previous = trail[i];
                trail[i] = Vector3.Lerp(trail[i], nose - forward * distance, amount);
            }
        }

        /// <summary>
        /// Builds the spine for this frame and writes it in <paramref name="meshSpace"/>'s local space.
        /// </summary>
        /// <param name="phase">Tail-beat phase in radians (ωt).</param>
        /// <param name="amplitude">Multiplier on the body-wave envelope; 1 = full cruising stroke.</param>
        /// <param name="bankDegrees">Roll into the current turn.</param>
        /// <param name="follow">How closely the body follows the head's path this frame (0 = straight body).</param>
        /// <param name="curl">Extra C-bend for turning on the spot: sideways tail offset as a fraction of
        /// body length, positive towards the fish's right.</param>
        /// <param name="stiffen">0..1: how much of the body's <see cref="stiffness"/> applies right now (turning curl only).</param>
        public void Evaluate(Vector3 nose, Vector3 forward, Vector3 up, float length,
            float phase, float amplitude, float bankDegrees, float follow, float curl, float stiffen, Transform meshSpace)
        {
            float stiff = Mathf.Clamp01(stiffness * stiffen);
            // 1. Lay the spine along the recent path, blended with a straight body. The samples
            // go tail-wards, so one walk down the recorded path finds them all.
            var walk = new TrailWalk { previous = nose, previousUp = up };
            for (int i = 0; i < SampleCount; i++)
            {
                float distance = i / (SampleCount - 1f) * length;
                var straight = nose - forward * distance;
                if (follow > 0f)
                {
                    SampleTrail(ref walk, forward, distance, out var pathPoint, out var pathUp);
                    spinePoints[i] = Vector3.Lerp(straight, pathPoint, follow);
                    spineUps[i] = Vector3.Lerp(up, pathUp, follow).normalized;
                }
                else
                {
                    spinePoints[i] = straight;
                    spineUps[i] = up;
                }
            }

            // 2. Add the travelling body wave sideways to the spine.
            float k = wavesPerBody * Mathf.PI * 2f;
            for (int i = 0; i < SampleCount; i++)
            {
                float s = i / (SampleCount - 1f);
                var tangent = Tangent(spinePoints, i, forward);
                var side = Vector3.Cross(spineUps[i], tangent).normalized;
                // The body only wiggles behind its bend start; a turn on the spot curls the same part.
                float bend = BendMask(s, bendStart);
                float behind = Mathf.InverseLerp(bendStart, 1f, s);
                float curlShape = Mathf.Pow(behind, Mathf.Lerp(2f, 3.5f, stiff));
                float sway = Envelope(s) * length * amplitude * bend * Mathf.Sin(k * s - phase) + curl * length * curlShape;
                midline[i] = spinePoints[i] + side * sway;
            }

            // 3. Frames along the final midline, rolled for banking and the tail twist.
            var toMesh = meshSpace.worldToLocalMatrix;
            for (int i = 0; i < SampleCount; i++)
            {
                float s = i / (SampleCount - 1f);
                var f = Tangent(midline, i, forward);
                var side = Vector3.Cross(spineUps[i], f).normalized;
                var u = Vector3.Cross(f, side);

                float tailMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, s));
                float roll = bankDegrees * (1f - 0.3f * s) + tailTwistDegrees * amplitude * tailMask * Mathf.Cos(k * s - phase);
                var spin = Quaternion.AngleAxis(roll, f);
                side = spin * side;
                u = spin * u;

                // Directions pick up the mesh space's (uniform) scale here; the shader normalises them.
                Positions[i] = toMesh.MultiplyPoint3x4(midline[i]);
                Forwards[i] = toMesh.MultiplyVector(f);
                Sides[i] = toMesh.MultiplyVector(side);
                Ups[i] = toMesh.MultiplyVector(u);
            }
        }

        /// <summary>Direction towards the nose at sample i.</summary>
        static Vector3 Tangent(Vector3[] points, int i, Vector3 fallback)
        {
            int ahead = Mathf.Max(0, i - 1);
            int behind = Mathf.Min(points.Length - 1, i + 1);
            var d = points[ahead] - points[behind];
            return d.sqrMagnitude > 1e-12f ? d.normalized : fallback;
        }

        /// <summary>Where a walk down the recorded path has got to.</summary>
        struct TrailWalk
        {
            public int index;
            public float walked;
            public Vector3 previous, previousUp;
        }

        /// <summary>
        /// Point <paramref name="distance"/> back along the path from the live nose, carrying on
        /// from where the last call left off (distances must not go down between calls).
        /// </summary>
        void SampleTrail(ref TrailWalk walk, Vector3 forward, float distance, out Vector3 point, out Vector3 pointUp)
        {
            while (walk.index < trail.Count)
            {
                float segment = (trail[walk.index] - walk.previous).magnitude;
                float remaining = distance - walk.walked;
                if (segment >= remaining && segment > 1e-6f)
                {
                    float t = remaining / segment;
                    point = Vector3.Lerp(walk.previous, trail[walk.index], t);
                    pointUp = Vector3.Lerp(walk.previousUp, trailUp[walk.index], t);
                    return;
                }
                walk.walked += segment;
                walk.previous = trail[walk.index];
                walk.previousUp = trailUp[walk.index];
                walk.index++;
            }

            // Past the recorded history: continue straight back.
            var back = trail.Count >= 2 ? (trail[trail.Count - 1] - trail[trail.Count - 2]) : -forward;
            back = back.sqrMagnitude > 1e-12f ? back.normalized : -forward;
            point = walk.previous + back * (distance - walk.walked);
            pointUp = walk.previousUp;
        }
    }
}
