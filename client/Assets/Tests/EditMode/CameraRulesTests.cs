using System.Collections.Generic;
using NUnit.Framework;

namespace Rth.Client.Tests
{
    /// <summary>
    /// The camera's rules (CameraRules). They use no Unity types, so these run as Unity EditMode
    /// tests and, through tools/client-tests, with <c>dotnet test</c>.
    /// </summary>
    public sealed class CameraRulesTests
    {
        [TestCase(0f, 22f)]
        [TestCase(-3f, 22f)]       // falling ground leaves the minimum alone
        [TestCase(1f, 22f)]        // 16.9° is under the minimum
        [TestCase(2f, 33.75f)]     // two tiles up: about 34°
        [TestCase(3f, 50.625f)]
        [TestCase(4f, 67f)]        // four tiles or more: the maximum
        [TestCase(9f, 67f)]
        public void GroundAboveThePlayerRaisesThePitchFloor(float groundAbove, float floor)
        {
            Assert.AreEqual(floor, CameraRules.LiftFloor(groundAbove, 22f, 67f), 0.001f);
        }

        [Test]
        public void TheFloorRisesQuicklyAndSettlesSlowly()
        {
            // One 20 ms cycle moves 1/24 of the gap up, or 1/80 of it down.
            Assert.AreEqual(20f + 24f / 24f, CameraRules.EaseFloor(20f, 44f, 0.02f), 0.001f);
            Assert.AreEqual(44f - 24f / 80f, CameraRules.EaseFloor(44f, 20f, 0.02f), 0.001f);

            // The same in one long step as in many short ones.
            float a = 20f, b = 20f;
            for (int i = 0; i < 50; i++) a = CameraRules.EaseFloor(a, 60f, 0.01f);
            b = CameraRules.EaseFloor(b, 60f, 0.5f);
            Assert.AreEqual(a, b, 0.01f);

            // Half a second in: most of the way up, but only a quarter of the way down.
            Assert.Greater(CameraRules.EaseFloor(20f, 60f, 0.5f), 45f);
            Assert.Greater(CameraRules.EaseFloor(60f, 20f, 0.5f), 45f);
            Assert.AreEqual(60f, CameraRules.EaseFloor(60f, 60f, 0.5f));
        }

        [Test]
        public void ThePlayersPitchIsKeptAboveTheFloor()
        {
            Assert.AreEqual(50f, CameraRules.EffectivePitch(50f, 30f, 67f));
            Assert.AreEqual(40f, CameraRules.EffectivePitch(25f, 40f, 67f));
            Assert.AreEqual(67f, CameraRules.EffectivePitch(25f, 80f, 67f));
        }

        [Test]
        public void HighestNearLooksNineByNine()
        {
            float At(int x, int z) => x == 14 && z == 6 ? 5f : x == 15 && z == 6 ? 9f : 0f;
            Assert.AreEqual(5f, CameraRules.HighestNear(At, 10, 10), 0.0001f);   // (15, 6) is five tiles out
            Assert.AreEqual(9f, CameraRules.HighestNear(At, 11, 10), 0.0001f);
        }

        [Test]
        public void TheLineWalksEveryTileItCrosses()
        {
            var tiles = new List<(int x, int z)>();
            CameraRules.LineTiles(0.5f, 0.2f, 3.5f, 1.4f, tiles);
            CollectionAssert.AreEqual(new[] { (0, 0), (1, 0), (2, 0), (2, 1), (3, 1) }, tiles);

            CameraRules.LineTiles(2.5f, 2.5f, 2.5f, -0.5f, tiles); // straight south
            CollectionAssert.AreEqual(new[] { (2, 2), (2, 1), (2, 0), (2, -1) }, tiles);

            CameraRules.LineTiles(0.5f, 0.5f, 0.7f, 0.2f, tiles); // within one tile
            CollectionAssert.AreEqual(new[] { (0, 0) }, tiles);
        }

        [Test]
        public void ACornerCountsForBothSides()
        {
            var tiles = new List<(int x, int z)>();
            CameraRules.LineTiles(0.5f, 0.5f, 2.5f, 2.5f, tiles);
            CollectionAssert.AreEqual(new[] { (0, 0), (1, 0), (0, 1), (1, 1), (2, 1), (1, 2), (2, 2) }, tiles);
        }

        [Test]
        public void ARoofOnTheLineOfSightHides()
        {
            // Camera 10 tiles south and 6 up; the player stands at (5, 12).
            var line = new List<(int x, int z)>();
            CameraRules.LineTiles(5.5f, 2.5f, 5.5f, 12.5f, line);

            // A house just south of the player, roof 3.4 high: the sight line there is about 0.9 up.
            Assert.IsTrue(CameraRules.RoofOnSight(3, 9, 5, 2, 3.4f, 5.5f, 6.2f, 2.5f, 5.5f, 0.2f, 12.5f, line));
            // The same house beside the line (east of it) does not.
            Assert.IsFalse(CameraRules.RoofOnSight(7, 9, 5, 2, 3.4f, 5.5f, 6.2f, 2.5f, 5.5f, 0.2f, 12.5f, line));
            // Near the camera the line is high above a low roof: the camera sees over it.
            Assert.IsFalse(CameraRules.RoofOnSight(3, 3, 5, 2, 3.4f, 5.5f, 6.2f, 2.5f, 5.5f, 0.2f, 12.5f, line));
        }
    }
}
