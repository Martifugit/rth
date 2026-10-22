using System.Collections.Generic;
using NUnit.Framework;
using Rth.Client.Ui;
using Box = Rth.Client.Ui.WindowRules.Box;

namespace Rth.Client.Tests
{
    /// <summary>The docked windows' placement rules: snapping, no overlap, resizing against neighbours.</summary>
    public class WindowRulesTests
    {
        private const float W = 1920f, H = 1080f;

        [Test]
        public void AWindowNearAnotherSnapsFlushBesideIt()
        {
            var other = new Box(100, 100, 200, 200);
            var guides = new List<WindowRules.Guide>();
            Box snapped = WindowRules.Snap(new Box(306, 140, 150, 100), new[] { other }, W, H, guides);
            Assert.AreEqual(300f, snapped.X); // its left edge onto the other's right edge
            Assert.IsTrue(guides.Exists(g => g.Vertical && g.At == 300f));
        }

        [Test]
        public void AWindowLinesUpWithAnothersCentre()
        {
            var other = new Box(100, 100, 200, 200); // centre x 200
            Box snapped = WindowRules.Snap(new Box(126, 400, 150, 100), new[] { other }, W, H);
            Assert.AreEqual(125f, snapped.X); // its centre (125 + 75) onto 200
        }

        [Test]
        public void FarFromEverythingItSnapsToTheGrid()
        {
            Box snapped = WindowRules.Snap(new Box(613, 421, 150, 100), new Box[0], W, H);
            Assert.AreEqual(616f, snapped.X);
            Assert.AreEqual(424f, snapped.Y);
        }

        [Test]
        public void AWindowDroppedOnAnotherMovesToTheNearestFreePlace()
        {
            var other = new Box(500, 500, 300, 300);
            Box? free = WindowRules.NearestFree(new Box(760, 520, 100, 100), new[] { other }, W, H);
            Assert.IsTrue(free.HasValue);
            Assert.AreEqual(800f, free.Value.X); // pushed out of the right side, the shortest way
            Assert.AreEqual(520f, free.Value.Y);
            Assert.IsFalse(free.Value.Overlaps(other));
        }

        [Test]
        public void AWindowThatFitsStaysPut()
        {
            Box? free = WindowRules.NearestFree(new Box(10, 10, 100, 100), new[] { new Box(500, 500, 50, 50) }, W, H);
            Assert.AreEqual(new Box(10, 10, 100, 100), free);
        }

        [Test]
        public void WithNoRoomAnywhereThereIsNoPlace()
        {
            Box? free = WindowRules.NearestFree(new Box(0, 0, 100, 100), new[] { new Box(0, 0, 200, 200) }, 200, 200);
            Assert.IsNull(free);
        }

        [Test]
        public void ResizingStopsAtANeighbour()
        {
            var start = new Box(100, 100, 200, 200);
            var neighbour = new Box(350, 120, 100, 100);
            Box r = WindowRules.Resize(start, WindowRules.Corner.BottomRight, 200, 50, 100, 100, new[] { neighbour }, W, H);
            Assert.AreEqual(350f, r.Right); // stopped at the neighbour
            Assert.AreEqual(350f, r.Bottom); // grew downwards freely
        }

        [Test]
        public void ResizingFromTheTopLeftKeepsTheBottomRight()
        {
            var start = new Box(300, 300, 200, 200);
            Box r = WindowRules.Resize(start, WindowRules.Corner.TopLeft, -100, -50, 100, 100, new Box[0], W, H);
            Assert.AreEqual(new Box(200, 250, 300, 250), r);
        }

        [Test]
        public void ResizingNeverGoesBelowTheMinimumOrOffScreen()
        {
            var start = new Box(10, 10, 200, 200);
            Box small = WindowRules.Resize(start, WindowRules.Corner.BottomRight, -500, -500, 120, 80, new Box[0], W, H);
            Assert.AreEqual(120f, small.W);
            Assert.AreEqual(80f, small.H);
            Box big = WindowRules.Resize(start, WindowRules.Corner.TopLeft, -500, -500, 120, 80, new Box[0], W, H);
            Assert.AreEqual(0f, big.X);
            Assert.AreEqual(0f, big.Y);
        }

        [Test]
        public void SettlingALayoutLeavesNoOverlaps()
        {
            var layout = new[] { new Box(0, 0, 400, 400), new Box(300, 300, 400, 400), new Box(350, 100, 300, 300) };
            List<Box> settled = WindowRules.Settle(layout, W, H);
            Assert.AreEqual(layout[0], settled[0]); // the first keeps its place
            for (int i = 0; i < settled.Count; i++)
                for (int j = i + 1; j < settled.Count; j++)
                    Assert.IsFalse(settled[i].Overlaps(settled[j]), settled[i] + " overlaps " + settled[j]);
        }
    }
}
