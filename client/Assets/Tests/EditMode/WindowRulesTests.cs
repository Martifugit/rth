using System.Collections.Generic;
using NUnit.Framework;
using Rth.Client.Ui;
using Box = Rth.Client.Ui.WindowRules.Box;
using Edge = Rth.Client.Ui.WindowRules.Edge;

namespace Rth.Client.Tests
{
    /// <summary>The windows' placement rules: flush snapping, no overlap, resizing against neighbours, presets, layout buckets.</summary>
    public class WindowRulesTests
    {
        private const float W = 1920f, H = 1080f;
        private const float Snap = 8f;

        // ------------------------------------------------------------------ snapping

        [Test]
        public void AWindowNearAnotherSnapsFlushBesideIt()
        {
            var other = new Box(100, 100, 200, 200);
            var guides = new List<WindowRules.Guide>();
            Box snapped = WindowRules.Snap(new Box(306, 140, 150, 100), new[] { other }, W, H, Snap, guides);
            Assert.AreEqual(300f, snapped.X); // its left edge onto the other's right edge: no gap
            Assert.IsTrue(guides.Exists(g => g.Vertical && g.At == 300f));
        }

        [Test]
        public void AWindowUnderAnotherLinesUpWithItsEdges()
        {
            var bar = new Box(600, 900, 636, 110);
            // Dropped just under the bar and a little to the right: flush below it, left edges in line.
            Box snapped = WindowRules.Snap(new Box(605, 1013, 636, 62), new[] { bar }, W, 1200, Snap);
            Assert.AreEqual(600f, snapped.X);
            Assert.AreEqual(1010f, snapped.Y);
        }

        [Test]
        public void AWindowSnapsFlushToTheScreensEdge()
        {
            Box snapped = WindowRules.Snap(new Box(1715, 3, 200, 100), new Box[0], W, H, Snap);
            Assert.AreEqual(W - 200f, snapped.X);
            Assert.AreEqual(0f, snapped.Y);
        }

        [Test]
        public void AFarWindowDoesNotPull()
        {
            // The other is level with it on neither axis: nothing to snap to.
            var far = new Box(100, 100, 200, 200);
            Box snapped = WindowRules.Snap(new Box(305, 700, 150, 100), new[] { far }, W, H, Snap);
            Assert.AreEqual(305f, snapped.X);
            Assert.AreEqual(700f, snapped.Y);
        }

        [Test]
        public void FarFromEverythingItStaysWhereItIsOnWholePixels()
        {
            Box snapped = WindowRules.Snap(new Box(613.4f, 421.6f, 150, 100), new Box[0], W, H, Snap);
            Assert.AreEqual(613f, snapped.X);
            Assert.AreEqual(422f, snapped.Y);
        }

        [Test]
        public void HoldingAltSnapsNothing()
        {
            var other = new Box(100, 100, 200, 200);
            Box snapped = WindowRules.Snap(new Box(303, 140, 150, 100), new[] { other }, W, H, 0f);
            Assert.AreEqual(303f, snapped.X);
        }

        [Test]
        public void TheSnapDistanceIsTheSameOnScreenAtEveryScale()
        {
            Assert.AreEqual(WindowRules.SnapScreenPixels, WindowRules.SnapDistanceFor(1f));
            Assert.AreEqual(WindowRules.SnapScreenPixels / 2f, WindowRules.SnapDistanceFor(2f));
            Assert.AreEqual(WindowRules.SnapScreenPixels / 0.75f, WindowRules.SnapDistanceFor(0.75f), 0.001f);
        }

        [Test]
        public void ContactsAreTheLinesAWindowSitsFlushOn()
        {
            var side = new Box(1608, 680, 312, 400);
            List<WindowRules.Guide> lines = WindowRules.Contacts(new Box(1608, 460, 312, 220), new[] { side }, W, H);
            Assert.IsTrue(lines.Exists(g => !g.Vertical && g.At == 680f)); // on top of the side window
            Assert.IsTrue(lines.Exists(g => g.Vertical && g.At == W)); // against the screen's right edge
            Assert.IsTrue(lines.Exists(g => g.Vertical && g.At == 1608f)); // left edges in line
        }

        // ------------------------------------------------------------------ no overlap

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

        // ------------------------------------------------------------------ resizing

        [Test]
        public void ResizingStopsFlushAtANeighbour()
        {
            var start = new Box(100, 100, 200, 200);
            var neighbour = new Box(350, 120, 100, 100);
            Box r = WindowRules.Resize(start, Edge.BottomRight, 200, 50, 100, 100, new[] { neighbour }, W, H);
            Assert.AreEqual(350f, r.Right); // stopped flush at the neighbour
            Assert.AreEqual(350f, r.Bottom); // grew downwards freely
        }

        [Test]
        public void ResizingIntoANeighbourStopsInsteadOfJumpingBack()
        {
            // Dragged far into the neighbour: the edge stays flush against it, never back at the start.
            var start = new Box(100, 100, 200, 200);
            var neighbour = new Box(320, 0, 300, 600);
            Box r = WindowRules.Resize(start, Edge.Right, 500, 0, 100, 100, new[] { neighbour }, W, H, Snap);
            Assert.AreEqual(new Box(100, 100, 220, 200), r);
        }

        [Test]
        public void ADiagonalResizeSlidesAlongANeighbour()
        {
            var start = new Box(100, 100, 200, 200);
            var below = new Box(0, 400, 600, 100); // under it, wider than it
            Box r = WindowRules.Resize(start, Edge.BottomRight, 120, 300, 100, 100, new[] { below }, W, H);
            Assert.AreEqual(420f, r.Right); // still grew sideways
            Assert.AreEqual(400f, r.Bottom); // stopped on the window below
        }

        [Test]
        public void AnEdgeHandleMovesOnlyThatEdge()
        {
            var start = new Box(300, 300, 200, 200);
            Box r = WindowRules.Resize(start, Edge.Left, -50, 80, 100, 100, new Box[0], W, H);
            Assert.AreEqual(new Box(250, 300, 250, 200), r);
        }

        [Test]
        public void ResizingFromTheTopLeftKeepsTheBottomRight()
        {
            var start = new Box(300, 300, 200, 200);
            Box r = WindowRules.Resize(start, Edge.TopLeft, -100, -50, 100, 100, new Box[0], W, H);
            Assert.AreEqual(new Box(200, 250, 300, 250), r);
        }

        [Test]
        public void ResizingNeverGoesBelowTheMinimumOrOffScreen()
        {
            var start = new Box(10, 10, 200, 200);
            Box small = WindowRules.Resize(start, Edge.BottomRight, -500, -500, 120, 80, new Box[0], W, H);
            Assert.AreEqual(120f, small.W);
            Assert.AreEqual(80f, small.H);
            Box big = WindowRules.Resize(start, Edge.TopLeft, -500, -500, 120, 80, new Box[0], W, H);
            Assert.AreEqual(0f, big.X);
            Assert.AreEqual(0f, big.Y);
        }

        [Test]
        public void AResizedEdgeSnapsOntoANeighboursEdge()
        {
            var start = new Box(100, 100, 200, 200);
            var neighbour = new Box(300, 100, 200, 296); // flush beside it
            var guides = new List<WindowRules.Guide>();
            // The bottom edge, dragged to 391, snaps to the neighbour's bottom at 396.
            Box r = WindowRules.Resize(start, Edge.BottomRight, 0, 91, 100, 100, new[] { neighbour }, W, H, Snap, guides);
            Assert.AreEqual(396f, r.Bottom);
            Assert.IsTrue(guides.Exists(g => !g.Vertical && g.At == 396f));
        }

        [Test]
        public void AResizedEdgeSnapsFlushToTheScreen()
        {
            Box r = WindowRules.Resize(new Box(1500, 100, 200, 200), Edge.Right, 215, 0, 100, 100, new Box[0], W, H, Snap);
            Assert.AreEqual(W, r.Right);
        }

        [Test]
        public void AllowedSizesPickTheNearestThatFits()
        {
            var sizes = new List<(float W, float H)> { (636, 62), (318, 124), (212, 186) };
            var start = new Box(600, 500, 636, 62);
            // Dragged narrower and taller from the bottom right: the half-width, two-row bar.
            Box dragged = WindowRules.Resize(start, Edge.BottomRight, -300, 70, 212, 62, new Box[0], W, H);
            Box r = WindowRules.NearestAllowed(start, dragged, Edge.BottomRight, sizes, new Box[0], W, H);
            Assert.AreEqual(new Box(600, 500, 318, 124), r);
            // From the top left the bottom right stays put.
            Box fromTop = WindowRules.NearestAllowed(start, new Box(1030, 380, 206, 182), Edge.TopLeft, sizes, new Box[0], W, H);
            Assert.AreEqual(new Box(1024, 376, 212, 186), fromTop);
        }

        [Test]
        public void AnAllowedSizeThatWouldOverlapIsSkipped()
        {
            var sizes = new List<(float W, float H)> { (636, 62), (318, 124) };
            var start = new Box(600, 500, 636, 62);
            var below = new Box(600, 562, 636, 110);
            Box r = WindowRules.NearestAllowed(start, new Box(600, 500, 318, 124), Edge.BottomRight, sizes, new[] { below }, W, H);
            Assert.AreEqual(start, r);
        }

        [Test]
        public void ASavedSizeOffTheListComesBackToTheNearest()
        {
            var sizes = new List<(float W, float H)> { (636, 62), (318, 124) };
            Assert.AreEqual((318f, 124f), WindowRules.NearestSize(300, 130, sizes));
            // One that doesn't fit the screen loses to one that does.
            Assert.AreEqual((318f, 124f), WindowRules.NearestSize(636, 62, sizes, 400, 800));
        }

        // ------------------------------------------------------------------ settling

        [Test]
        public void SettlingALayoutLeavesNoOverlaps()
        {
            var layout = new[] { new Box(0, 0, 400, 400), new Box(300, 300, 400, 400), new Box(350, 100, 300, 300) };
            List<Box> settled = WindowRules.Settle(layout, W, H);
            for (int i = 0; i < settled.Count; i++)
                for (int j = i + 1; j < settled.Count; j++)
                    Assert.IsFalse(settled[i].Overlaps(settled[j]), settled[i] + " overlaps " + settled[j]);
        }

        [Test]
        public void SettlingKeepsEveryWindowThatAlreadyFits()
        {
            // The first and second overlap; the third is clear and must not move, even though it comes last.
            var layout = new[] { new Box(0, 0, 400, 400), new Box(300, 300, 400, 400), new Box(1500, 0, 300, 300) };
            List<Box> settled = WindowRules.Settle(layout, W, H);
            Assert.AreEqual(layout[2], settled[2]);
            Assert.AreEqual(layout[0].W, settled[0].W);
            Assert.AreEqual(layout[1].W, settled[1].W); // sizes never change
        }

        // ------------------------------------------------------------------ attaching and presets

        [Test]
        public void AttachingPutsAWindowFlushAgainstAnother()
        {
            var bar = new Box(642, 970, 636, 110);
            Assert.AreEqual(new Box(642, 930, 316, 40), WindowRules.Attach(bar, WindowRules.Side.Above, WindowRules.Align.Start, 316, 40));
            Assert.AreEqual(new Box(962, 930, 316, 40), WindowRules.Attach(bar, WindowRules.Side.Above, WindowRules.Align.End, 316, 40));
            Assert.AreEqual(new Box(1278, 970, 100, 110), WindowRules.Attach(bar, WindowRules.Side.RightOf, WindowRules.Align.Start, 100, 110));
        }

        /// <summary>The panels' own sizes, as the shipped panels have them (fixed and bare ones).</summary>
        private static (float W, float H)? FixedSizes(string group)
        {
            switch (group)
            {
                case UiDefaultLayout.Minimap: return (240, 240);
                case UiDefaultLayout.ActionBar: return (636, 110);
                case UiDefaultLayout.ActionBar2:
                case UiDefaultLayout.ActionBar3: return (636, 62);
                case UiDefaultLayout.Buffs:
                case UiDefaultLayout.Debuffs: return (316, 40);
                case UiDefaultLayout.Target: return (260, 64);
                case UiDefaultLayout.Boss: return (460, 58);
                case UiDefaultLayout.XpDrops: return (180, 200);
                case UiDefaultLayout.Dialogue: return (600, 230);
                case UiDefaultLayout.Town: return (780, 480);
                default: return null;
            }
        }

        /// <summary>The windows on screen at once by default (the floating server windows and the boss timer come and go).</summary>
        private static readonly string[] Docked =
        {
            UiDefaultLayout.Minimap, UiDefaultLayout.XpDrops, UiDefaultLayout.Side, UiDefaultLayout.Side2, UiDefaultLayout.Chat,
            UiDefaultLayout.ActionBar, UiDefaultLayout.Buffs, UiDefaultLayout.Debuffs, UiDefaultLayout.ActionBar2,
            UiDefaultLayout.ActionBar3, UiDefaultLayout.Target,
        };

        // 720p, 1080p and 1440p in panel pixels at their fitted scales (0.75, 1, 1.25).
        private static readonly (float W, float H)[] Screens = { (1707, 960), (1920, 1080), (2048, 1152) };

        [Test]
        public void EveryPresetIsFlushAndClearAt720pTo1440p()
        {
            foreach (UiDefaultLayout.Preset preset in UiDefaultLayout.Presets)
                foreach ((float sw, float sh) in Screens)
                {
                    Dictionary<string, Box> boxes = UiDefaultLayout.Resolve(preset, sw, sh, FixedSizes);
                    var shown = new List<Box>();
                    foreach (string id in Docked)
                        if (!preset.Closed.Contains(id)) shown.Add(boxes[id]);
                    string where = preset.Id + " at " + sw + "x" + sh + ": ";
                    for (int i = 0; i < shown.Count; i++)
                    {
                        Box b = shown[i];
                        Assert.IsTrue(b.X >= 0 && b.Y >= 0 && b.Right <= sw && b.Bottom <= sh, where + b + " is off screen");
                        Assert.AreEqual((float)System.Math.Round(b.X), b.X, where + b + " is not on whole pixels");
                        bool flush = b.TouchesScreen(sw, sh);
                        for (int j = 0; j < shown.Count; j++)
                        {
                            if (j == i) continue;
                            Assert.IsFalse(b.Overlaps(shown[j]), where + b + " overlaps " + shown[j]);
                            flush |= b.Touches(shown[j]);
                        }
                        Assert.IsTrue(flush, where + b + " touches neither the screen's edge nor another window");
                    }
                }
        }

        [Test]
        public void ExtraBarsStackFlushOverTheMainBar()
        {
            Dictionary<string, Box> boxes = UiDefaultLayout.Resolve(UiDefaultLayout.Classic, W, H, FixedSizes);
            Box bar = boxes[UiDefaultLayout.ActionBar], buffs = boxes[UiDefaultLayout.Buffs], bar2 = boxes[UiDefaultLayout.ActionBar2];
            Assert.AreEqual(H, bar.Bottom);
            Assert.AreEqual(bar.Y, buffs.Bottom);
            Assert.AreEqual(buffs.Y, bar2.Bottom);
            Assert.AreEqual(bar.X, bar2.X);
        }

        [Test]
        public void MirroredSwapsTheCornersButNotTheBars()
        {
            Dictionary<string, Box> classic = UiDefaultLayout.Resolve(UiDefaultLayout.Classic, W, H, FixedSizes);
            Dictionary<string, Box> mirrored = UiDefaultLayout.Resolve(UiDefaultLayout.Mirrored, W, H, FixedSizes);
            Assert.AreEqual(0f, mirrored[UiDefaultLayout.Minimap].X);
            Assert.AreEqual(W, mirrored[UiDefaultLayout.Chat].Right);
            Assert.AreEqual(classic[UiDefaultLayout.Buffs], mirrored[UiDefaultLayout.Buffs]);
        }

        // ------------------------------------------------------------------ layouts per screen size

        [Test]
        public void ScreenSizesFallInBuckets()
        {
            Assert.AreEqual("1920x1088", WindowRules.BucketOf(1920, 1080));
            Assert.AreEqual(WindowRules.BucketOf(1920, 1080), WindowRules.BucketOf(1910, 1075)); // a few pixels make no difference
            Assert.AreNotEqual(WindowRules.BucketOf(1920, 1080), WindowRules.BucketOf(1707, 960));
        }

        [Test]
        public void ANewScreenStartsFromTheNearestSavedLayout()
        {
            var saved = new[] { "1920x1088", "1728x960", "2048x1152" };
            Assert.AreEqual("1728x960", WindowRules.NearestBucket(saved, 1600, 900));
            Assert.AreEqual("2048x1152", WindowRules.NearestBucket(saved, 2100, 1200));
            Assert.IsNull(WindowRules.NearestBucket(new string[0], 1600, 900));
        }
    }
}
