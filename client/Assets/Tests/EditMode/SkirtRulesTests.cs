using System;
using System.Numerics;
using NUnit.Framework;

namespace Rth.Client.Tests
{
    /// <summary>
    /// The skirt rig's rule (SkirtRules), on a stand-in body: hips at 1.0 m, hip joints 0.09 m
    /// either side at 0.9 m, knees at 0.5 m, ankles at 0.1 m, capsules 0.07 m, twelve chains hanging
    /// from a ring of radius 0.18 m at the hips. Forward is +y, up +z, the left leg at -x.
    /// </summary>
    public sealed class SkirtRulesTests
    {
        const int N = 12;
        static readonly Vector3 Down = -Vector3.UnitZ, Up = Vector3.UnitZ;

        static (Vector3[] heads, Vector3[] outs) Ring()
        {
            var heads = new Vector3[N];
            var outs = new Vector3[N];
            for (int k = 0; k < N; k++)
            {
                float a = 2 * MathF.PI * k / N;
                outs[k] = new Vector3(-MathF.Sin(a), MathF.Cos(a), 0);
                heads[k] = outs[k] * 0.18f + new Vector3(0, 0, 1.0f);
            }
            return (heads, outs);
        }

        static SkirtRules.Capsule[] Legs(Vector3 kneeL, Vector3 ankleL, Vector3 kneeR, Vector3 ankleR)
        {
            var hipL = new Vector3(-0.09f, 0, 0.9f);
            var hipR = new Vector3(0.09f, 0, 0.9f);
            return new[]
            {
                SkirtRules.Capsule.OfBone(hipL, kneeL, 0.07f, true), SkirtRules.Capsule.OfBone(hipR, kneeR, 0.07f, true),
                SkirtRules.Capsule.OfBone(kneeL, ankleL, 0.07f, false), SkirtRules.Capsule.OfBone(kneeR, ankleR, 0.07f, false),
            };
        }

        static float[] Lengths(float l) { var a = new float[N]; Array.Fill(a, l); return a; }

        [Test]
        public void StandingEveryChainHangsStraightDown()
        {
            var (heads, outs) = Ring();
            var caps = Legs(new Vector3(-0.09f, 0, 0.5f), new Vector3(-0.09f, 0, 0.1f),
                            new Vector3(0.09f, 0, 0.5f), new Vector3(0.09f, 0, 0.1f));
            foreach (var a in SkirtRules.Ring(heads, outs, Lengths(0.5f), Down, Up, caps))
                Assert.That(a, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void ALegForwardSwingsTheFrontOutAndLeavesTheBackHanging()
        {
            var (heads, outs) = Ring();
            var caps = Legs(new Vector3(-0.09f, 0.25f, 0.62f), new Vector3(-0.09f, 0.45f, 0.28f),   // left leg forward
                            new Vector3(0.09f, 0, 0.5f), new Vector3(0.09f, 0, 0.1f));
            var a = SkirtRules.Ring(heads, outs, Lengths(0.5f), Down, Up, caps);
            Assert.That(a[0], Is.GreaterThan(0.3f));          // the front
            Assert.That(a[6], Is.EqualTo(0f).Within(1e-4f));  // the back
        }

        [Test]
        public void SittingTheFrontLiesOverTheThighsAndNothingComesThrough()
        {
            var (heads, outs) = Ring();
            var caps = Legs(new Vector3(-0.09f, 0.4f, 0.9f), new Vector3(-0.09f, 0.4f, 0.5f),      // thighs level
                            new Vector3(0.09f, 0.4f, 0.9f), new Vector3(0.09f, 0.4f, 0.5f));
            var lengths = Lengths(0.5f);
            var a = SkirtRules.Ring(heads, outs, lengths, Down, Up, caps);
            Assert.That(a[0], Is.GreaterThan(1.2f));          // the front swings up over the lap
            for (int k = 0; k < N; k++)
            {
                var d = SkirtRules.Along(Down, outs[k], a[k]);
                var head = heads[k] + d * (SkirtRules.Skip * lengths[k]);
                Assert.That(SkirtRules.Gap(head, heads[k] + d * lengths[k], caps), Is.GreaterThanOrEqualTo(0f), $"chain {k}");
            }
        }

        [Test]
        public void NeighboursNeverHangMoreThanTheSagApart()
        {
            var (heads, outs) = Ring();
            var caps = Legs(new Vector3(-0.09f, 0.4f, 0.9f), new Vector3(-0.09f, 0.4f, 0.5f),
                            new Vector3(0.09f, 0.4f, 0.9f), new Vector3(0.09f, 0.4f, 0.5f));
            var a = SkirtRules.Ring(heads, outs, Lengths(0.5f), Down, Up, caps);
            float sag = SkirtRules.SagDegrees * MathF.PI / 180f + 1e-4f;
            for (int k = 0; k < N; k++)
                Assert.That(MathF.Max(a[(k + N - 1) % N], a[(k + 1) % N]) - a[k], Is.LessThanOrEqualTo(sag));
        }

        [Test]
        public void SegmentDistance()
        {
            Assert.That(SkirtRules.SegSeg(new Vector3(0, 0, 0), new Vector3(1, 0, 0),
                                          new Vector3(0.5f, 1, -1), new Vector3(0.5f, 1, 1)), Is.EqualTo(1f).Within(1e-5f));
        }
    }
}
