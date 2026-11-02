using System;
using System.Numerics;

namespace Rth.Client
{
    /// <summary>
    /// The skirt rig's rule (tools/character-models/posing.py, which the Blender review runs; keep
    /// the two the same), as plain maths with no Unity types so it can be tested anywhere.
    ///
    /// Skirts, robes and the skirts of tunics hang from a ring of short bone chains on the hips,
    /// never animated. Every frame, after the animation, each segment hangs straight down (as near
    /// as its plane allows) and swings outward in the vertical plane through its chain only as far
    /// as it must to clear capsules round the hidden thighs and shins; no chain may hang more than
    /// <see cref="SagDegrees"/> below a neighbour (the cloth between them is taut), and the cloth
    /// between two neighbours must clear the capsules too. So the cloth lies outside where the legs
    /// are in any animation, with no legs under it. Angles are radians; distances metres.
    /// </summary>
    public static class SkirtRules
    {
        public const float Clear = 0.012f;          // how far a chain keeps beyond a capsule
        public const float ThighFrom = 0.3f;        // a thigh's capsule starts this far down it
        public const float Skip = 0.15f;            // the share of a segment the test skips at its head
        public const float SagDegrees = 35f;
        public const int SweepDegrees = 160;
        public const int StepDegrees = 2;

        public readonly struct Capsule
        {
            public readonly Vector3 A, B;
            public readonly float Radius;
            public Capsule(Vector3 a, Vector3 b, float radius) { A = a; B = b; Radius = radius; }

            /// <summary>A leg bone's capsule from its head and tail; a thigh's starts ThighFrom down it.</summary>
            public static Capsule OfBone(Vector3 head, Vector3 tail, float radius, bool thigh) =>
                new Capsule(thigh ? Vector3.Lerp(head, tail, ThighFrom) : head, tail, radius);
        }

        /// <summary>
        /// The swing of every segment of one ring (upper or lower), given each chain's head, its
        /// outward direction (horizontal at rest, turned with the hips), the hips' down and the
        /// segments' lengths. <paramref name="up"/> is the world's up.
        /// </summary>
        public static float[] Ring(Vector3[] heads, Vector3[] outs, float[] lengths, Vector3 down, Vector3 up,
                                   Capsule[] capsules)
        {
            int n = heads.Length;
            Func<int, float, float> solve = (k, least) => Chain(heads[k], lengths[k], down, outs[k], up, capsules, least);
            var angles = new float[n];
            for (int k = 0; k < n; k++) angles[k] = solve(k, float.NegativeInfinity);
            Taut(angles, solve);
            Panels(angles, heads, outs, lengths, down, capsules);
            Taut(angles, solve);
            return angles;
        }

        /// <summary>A segment's direction for a swing.</summary>
        public static Vector3 Along(Vector3 down, Vector3 outward, float angle) =>
            down * MathF.Cos(angle) + outward * MathF.Sin(angle);

        /// <summary>
        /// How far one segment swings out: from as near straight down as its plane allows (or
        /// <paramref name="least"/>) outward until it clears every capsule; if none does, the
        /// swing that comes closest.
        /// </summary>
        public static float Chain(Vector3 head, float length, Vector3 down, Vector3 outward, Vector3 up,
                                  Capsule[] capsules, float least)
        {
            var worldDown = -up;
            float start = MathF.Atan2(Vector3.Dot(worldDown, outward), Vector3.Dot(worldDown, down));
            start = Math.Clamp(start, -0.5f, 1.5f);
            start = MathF.Max(start, least);
            float best = start, bestGap = float.NegativeInfinity;
            for (int i = 0; i <= SweepDegrees / StepDegrees; i++)
            {
                float th = start + Rad(i * StepDegrees);
                var d = Along(down, outward, th);
                float gap = Gap(head + d * (Skip * length), head + d * length, capsules);
                if (gap >= 0) return th;
                if (gap > bestGap) { best = th; bestGap = gap; }
            }
            return best;
        }

        /// <summary>No chain more than SagDegrees below a neighbour: the lowest are raised and solved again.</summary>
        public static void Taut(float[] angles, Func<int, float, float> solve)
        {
            int n = angles.Length;
            float sag = Rad(SagDegrees);
            for (int round = 0; round < n; round++)
            {
                bool moved = false;
                for (int k = 0; k < n; k++)
                {
                    float need = MathF.Max(angles[(k + n - 1) % n], angles[(k + 1) % n]) - sag;
                    if (angles[k] < need - 1e-6f) { angles[k] = solve(k, need); moved = true; }
                }
                if (!moved) break;
            }
        }

        /// <summary>Where the cloth between two neighbours comes through a capsule, both swing further out.</summary>
        public static void Panels(float[] angles, Vector3[] heads, Vector3[] outs, float[] lengths, Vector3 down,
                                  Capsule[] capsules)
        {
            int n = angles.Length;
            float step = Rad(StepDegrees), most = Rad(SweepDegrees);
            for (int round = 0; round < SweepDegrees / StepDegrees; round++)
            {
                bool moved = false;
                for (int k = 0; k < n; k++)
                {
                    int j = (k + 1) % n;
                    var e0 = heads[k] + Along(down, outs[k], angles[k]) * lengths[k];
                    var e1 = heads[j] + Along(down, outs[j], angles[j]) * lengths[j];
                    foreach (float t in new[] { 0.25f, 0.5f, 0.75f })
                    {
                        var h = Vector3.Lerp(heads[k], heads[j], t);
                        var e = Vector3.Lerp(e0, e1, t);
                        if (Gap(Vector3.Lerp(h, e, Skip), e, capsules) < 0)
                        {
                            angles[k] = MathF.Min(angles[k] + step, most);
                            angles[j] = MathF.Min(angles[j] + step, most);
                            moved = true;
                            break;
                        }
                    }
                }
                if (!moved) break;
            }
        }

        /// <summary>How far a segment keeps clear of the capsules beyond Clear (negative: through one).</summary>
        public static float Gap(Vector3 a, Vector3 b, Capsule[] capsules)
        {
            float gap = 1f;
            foreach (var c in capsules)
                gap = MathF.Min(gap, SegSeg(a, b, c.A, c.B) - c.Radius - Clear);
            return gap;
        }

        /// <summary>The least distance between two segments.</summary>
        public static float SegSeg(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float c = Vector3.Dot(d1, r), b = Vector3.Dot(d1, d2);
            float den = a * e - b * b;
            float s = den > 1e-12f ? Math.Clamp((b * f - c * e) / den, 0f, 1f) : 0f;
            float t = e > 1e-12f ? (b * s + f) / e : 0f;
            if (t < 0) { t = 0; s = a > 1e-12f ? Math.Clamp(-c / a, 0f, 1f) : 0f; }
            else if (t > 1) { t = 1; s = a > 1e-12f ? Math.Clamp((b - c) / a, 0f, 1f) : 0f; }
            return ((p1 + d1 * s) - (p2 + d2 * t)).Length();
        }

        static float Rad(float degrees) => degrees * MathF.PI / 180f;
    }
}
