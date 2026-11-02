using System;
using System.Collections.Generic;

namespace Rth.Client.Ui
{
    /// <summary>
    /// The rules for placing windows (docs/interface.md), as plain maths with no Unity types so they
    /// are tested anywhere (`dotnet test` runs them through tools/client-tests): snapping edges flush
    /// to the screen and to neighbours (with the guide lines to draw), finding the nearest place a
    /// window fits without overlapping another, resizing that stops flush at the neighbours, sizes
    /// picked from a panel's allowed list, and the screen-size buckets layouts are saved under.
    /// Positions are panel pixels, y down, origin top left.
    ///
    /// There are no gaps or margins anywhere: a window snaps so its edge touches the screen's edge
    /// or a neighbour's edge exactly, so a whole interface can be built flush. There is no grid
    /// either: a grid fought the edges (a window could sit on the grid or flush, rarely both).
    /// </summary>
    public static class WindowRules
    {
        /// <summary>
        /// How close (screen pixels, whatever the interface scale) an edge must come to a line to
        /// snap onto it. Callers turn it into panel pixels with <see cref="SnapDistanceFor"/>.
        /// </summary>
        public const float SnapScreenPixels = 8f;

        /// <summary>The snap distance in panel pixels at an interface scale, so it feels the same at every scale.</summary>
        public static float SnapDistanceFor(float scale) => SnapScreenPixels / Math.Max(0.1f, scale);

        /// <summary>A rectangle: x, y the top-left corner, w, h the size.</summary>
        public readonly struct Box : IEquatable<Box>
        {
            public readonly float X, Y, W, H;

            public Box(float x, float y, float w, float h)
            {
                X = x;
                Y = y;
                W = w;
                H = h;
            }

            public float Right => X + W;
            public float Bottom => Y + H;
            public float CentreX => X + W / 2f;
            public float CentreY => Y + H / 2f;

            public Box At(float x, float y) => new Box(x, y, W, H);

            /// <summary>Whether the two share any area (touching edges don't count).</summary>
            public bool Overlaps(Box o) => X < o.Right - 0.5f && o.X < Right - 0.5f && Y < o.Bottom - 0.5f && o.Y < Bottom - 0.5f;

            /// <summary>Whether the two touch along an edge (flush), sharing some length of it.</summary>
            public bool Touches(Box o)
            {
                bool sideBySide = (Math.Abs(Right - o.X) < 0.5f || Math.Abs(o.Right - X) < 0.5f) && Y < o.Bottom - 0.5f && o.Y < Bottom - 0.5f;
                bool stacked = (Math.Abs(Bottom - o.Y) < 0.5f || Math.Abs(o.Bottom - Y) < 0.5f) && X < o.Right - 0.5f && o.X < Right - 0.5f;
                return sideBySide || stacked;
            }

            /// <summary>Whether any of its edges lies on the screen's edge.</summary>
            public bool TouchesScreen(float screenW, float screenH) =>
                Math.Abs(X) < 0.5f || Math.Abs(Y) < 0.5f || Math.Abs(Right - screenW) < 0.5f || Math.Abs(Bottom - screenH) < 0.5f;

            public bool Equals(Box o) => X == o.X && Y == o.Y && W == o.W && H == o.H;
            public override bool Equals(object obj) => obj is Box b && Equals(b);
            public override int GetHashCode() => (X, Y, W, H).GetHashCode();
            public override string ToString() => $"({X}, {Y}, {W} x {H})";
        }

        /// <summary>A line the moving window lined up with: vertical (at x) or horizontal (at y).</summary>
        public readonly struct Guide
        {
            public readonly bool Vertical;
            public readonly float At;

            public Guide(bool vertical, float at)
            {
                Vertical = vertical;
                At = at;
            }
        }

        // ------------------------------------------------------------------ snapping

        /// <summary>
        /// Snaps a window being moved, each axis on its own: its edge onto the screen's edge, its
        /// edge flush against a neighbour's facing edge, or its edge in line with a neighbour's same
        /// edge (so a bar stacked under another lines up with it), whichever is nearest within
        /// <paramref name="snap"/> panel pixels; or its centre onto the screen's centre. Neighbours
        /// count only when they are level with it on the other axis (or within the snap distance of
        /// it), so a window across the screen never pulls. A snap of 0 (Alt held) snaps nothing.
        /// The result is on whole pixels. Returns the snapped box and the lines it lined up with.
        /// </summary>
        public static Box Snap(Box moving, IReadOnlyList<Box> others, float screenW, float screenH, float snap, List<Guide> guides = null)
        {
            if (snap <= 0f) return Round(moving);
            float x = SnapAxis(moving.X, moving.W, screenW, others, snap, true, moving, out float? gx);
            float y = SnapAxis(moving.Y, moving.H, screenH, others, snap, false, moving, out float? gy);
            if (guides != null)
            {
                if (gx.HasValue) guides.Add(new Guide(true, gx.Value));
                if (gy.HasValue) guides.Add(new Guide(false, gy.Value));
            }
            return Round(moving.At(x, y));
        }

        private static float SnapAxis(float start, float length, float screen, IReadOnlyList<Box> others, float snap, bool horizontal, Box moving, out float? guide)
        {
            float best = snap;
            float result = start;
            float? found = null;
            void Try(float edgeOffset, float line)
            {
                float d = Math.Abs(start + edgeOffset - line);
                if (d < best)
                {
                    best = d;
                    result = line - edgeOffset;
                    found = line;
                }
            }
            Try(0f, 0f);
            Try(length, screen);
            Try(length / 2f, screen / 2f);
            foreach (Box o in others)
            {
                if (!Level(moving, o, snap, horizontal)) continue;
                float oStart = horizontal ? o.X : o.Y;
                float oEnd = horizontal ? o.Right : o.Bottom;
                Try(0f, oEnd); // flush: its start against their end
                Try(length, oStart); // flush: its end against their start
                Try(0f, oStart); // in line: start with start
                Try(length, oEnd); // in line: end with end
            }
            guide = found;
            return result;
        }

        /// <summary>Whether two boxes are level on the axis other than the one being snapped (overlap, or nearly).</summary>
        private static bool Level(Box a, Box b, float snap, bool horizontal) =>
            horizontal
                ? a.Y < b.Bottom + snap && b.Y < a.Bottom + snap
                : a.X < b.Right + snap && b.X < a.Right + snap;

        /// <summary>
        /// The lines a box sits flush on, to draw while moving or resizing it: each of its edges that
        /// lies on the screen's edge or on the edge of a neighbour level with it, and the screen's
        /// centre lines when it is centred on them. Each line once.
        /// </summary>
        public static List<Guide> Contacts(Box b, IReadOnlyList<Box> others, float screenW, float screenH)
        {
            var guides = new List<Guide>();
            void Add(bool vertical, float at)
            {
                foreach (Guide g in guides)
                    if (g.Vertical == vertical && Math.Abs(g.At - at) < 0.5f) return;
                guides.Add(new Guide(vertical, at));
            }
            void Edges(bool vertical, float start, float end, float line)
            {
                if (Math.Abs(start - line) < 0.5f) Add(vertical, start);
                if (Math.Abs(end - line) < 0.5f) Add(vertical, end);
            }
            Edges(true, b.X, b.Right, 0f);
            Edges(true, b.X, b.Right, screenW);
            Edges(false, b.Y, b.Bottom, 0f);
            Edges(false, b.Y, b.Bottom, screenH);
            if (Math.Abs(b.CentreX - screenW / 2f) < 0.5f) Add(true, screenW / 2f);
            if (Math.Abs(b.CentreY - screenH / 2f) < 0.5f) Add(false, screenH / 2f);
            foreach (Box o in others)
            {
                if (Level(b, o, 0.5f, true))
                {
                    Edges(true, b.X, b.Right, o.X);
                    Edges(true, b.X, b.Right, o.Right);
                }
                if (Level(b, o, 0.5f, false))
                {
                    Edges(false, b.Y, b.Bottom, o.Y);
                    Edges(false, b.Y, b.Bottom, o.Bottom);
                }
            }
            return guides;
        }

        private static Box Round(Box b) => new Box((float)Math.Round(b.X), (float)Math.Round(b.Y), b.W, b.H);

        // ------------------------------------------------------------------ no overlap

        /// <summary>Whether a box is on screen and clear of every other box.</summary>
        public static bool Fits(Box b, IReadOnlyList<Box> others, float screenW, float screenH)
        {
            if (b.X < -0.5f || b.Y < -0.5f || b.Right > screenW + 0.5f || b.Bottom > screenH + 0.5f) return false;
            foreach (Box o in others)
                if (b.Overlaps(o)) return false;
            return true;
        }

        /// <summary>
        /// The nearest place to <paramref name="wanted"/> where the window fits on screen without
        /// overlapping another: where it is if it fits, else flush against a neighbour's or the
        /// screen's edge. Null when there is no room anywhere at this size.
        /// </summary>
        public static Box? NearestFree(Box wanted, IReadOnlyList<Box> others, float screenW, float screenH)
        {
            Box clamped = Clamp(wanted, screenW, screenH);
            if (Fits(clamped, others, screenW, screenH)) return clamped;

            // Candidate positions on each axis: where it is, and flush against every edge there is.
            var xs = new List<float> { clamped.X, 0f, screenW - wanted.W };
            var ys = new List<float> { clamped.Y, 0f, screenH - wanted.H };
            foreach (Box o in others)
            {
                xs.Add(o.Right);
                xs.Add(o.X - wanted.W);
                xs.Add(o.X);
                xs.Add(o.Right - wanted.W);
                ys.Add(o.Bottom);
                ys.Add(o.Y - wanted.H);
                ys.Add(o.Y);
                ys.Add(o.Bottom - wanted.H);
            }
            Box? best = null;
            float bestDistance = float.MaxValue;
            foreach (float x in xs)
                foreach (float y in ys)
                {
                    Box c = wanted.At(x, y);
                    if (!Fits(c, others, screenW, screenH)) continue;
                    float d = (x - wanted.X) * (x - wanted.X) + (y - wanted.Y) * (y - wanted.Y);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = c;
                    }
                }
            return best;
        }

        // ------------------------------------------------------------------ neighbours

        /// <summary>How close (pixels) two edges must be to count as shared, and to line up when dropped.</summary>
        public const float Flush = 0.5f;

        /// <summary>
        /// The windows sharing an edge that is being dragged: flush against it (within
        /// <see cref="Flush"/>) and overlapping it along its length. Returns, for each, its index in
        /// <paramref name="others"/> and which of its own edges faces the moving one. Dragging that
        /// edge moves theirs with it, like a splitter between panes.
        /// </summary>
        public static List<(int Index, Edge Facing)> Sharing(Box b, Edge edges, IReadOnlyList<Box> others)
        {
            var found = new List<(int, Edge)>();
            for (int i = 0; i < others.Count; i++)
            {
                Box o = others[i];
                bool alongY = o.Y < b.Bottom - Flush && b.Y < o.Bottom - Flush;
                bool alongX = o.X < b.Right - Flush && b.X < o.Right - Flush;
                if ((edges & Edge.Right) != 0 && alongY && Math.Abs(o.X - b.Right) <= Flush) found.Add((i, Edge.Left));
                else if ((edges & Edge.Left) != 0 && alongY && Math.Abs(o.Right - b.X) <= Flush) found.Add((i, Edge.Right));
                else if ((edges & Edge.Bottom) != 0 && alongX && Math.Abs(o.Y - b.Bottom) <= Flush) found.Add((i, Edge.Top));
                else if ((edges & Edge.Top) != 0 && alongX && Math.Abs(o.Bottom - b.Y) <= Flush) found.Add((i, Edge.Bottom));
            }
            return found;
        }

        /// <summary>
        /// Keeps a resized box from squeezing the windows that share its moving edges below their
        /// minimums, then gives each of them its new box: its facing edge follows the moved edge.
        /// </summary>
        public static (Box Box, List<Box> Linked) ResizeShared(Box resized, IReadOnlyList<(Box Start, Edge Facing, float MinW, float MinH)> linked)
        {
            float x0 = resized.X, x1 = resized.Right, y0 = resized.Y, y1 = resized.Bottom;
            foreach (var (o, facing, minW, minH) in linked)
            {
                if (facing == Edge.Left) x1 = Math.Min(x1, o.Right - minW);
                else if (facing == Edge.Right) x0 = Math.Max(x0, o.X + minW);
                else if (facing == Edge.Top) y1 = Math.Min(y1, o.Bottom - minH);
                else if (facing == Edge.Bottom) y0 = Math.Max(y0, o.Y + minH);
            }
            var box = new Box(x0, y0, Math.Max(1f, x1 - x0), Math.Max(1f, y1 - y0));
            var moved = new List<Box>();
            foreach (var (o, facing, _, _) in linked)
            {
                if (facing == Edge.Left) moved.Add(new Box(box.Right, o.Y, o.Right - box.Right, o.H));
                else if (facing == Edge.Right) moved.Add(new Box(o.X, o.Y, box.X - o.X, o.H));
                else if (facing == Edge.Top) moved.Add(new Box(o.X, box.Bottom, o.W, o.Bottom - box.Bottom));
                else moved.Add(new Box(o.X, o.Y, o.W, box.Y - o.Y));
            }
            return (box, moved);
        }

        /// <summary>
        /// A window dropped flush beside another takes on its extent along the shared edge when they
        /// are already nearly alike (within <paramref name="tolerance"/> at each end): side by side they
        /// come out the same height, stacked the same width, so neat rows and columns form without
        /// pixel pushing. Only if it still fits (screen, others, its minimum).
        /// </summary>
        public static Box MatchBeside(Box b, IReadOnlyList<Box> others, float minW, float minH, float screenW, float screenH, float tolerance)
        {
            foreach (Box o in others)
            {
                bool sideBySide = Math.Abs(o.X - b.Right) <= Flush || Math.Abs(o.Right - b.X) <= Flush;
                bool stacked = Math.Abs(o.Y - b.Bottom) <= Flush || Math.Abs(o.Bottom - b.Y) <= Flush;
                Box match = b;
                if (sideBySide && Math.Abs(o.Y - b.Y) <= tolerance && Math.Abs(o.Bottom - b.Bottom) <= tolerance && o.H >= minH)
                    match = new Box(b.X, o.Y, b.W, o.H);
                else if (stacked && Math.Abs(o.X - b.X) <= tolerance && Math.Abs(o.Right - b.Right) <= tolerance && o.W >= minW)
                    match = new Box(o.X, b.Y, o.W, b.H);
                else continue;
                if (match.Equals(b)) continue;
                var rest = new List<Box>();
                foreach (Box x in others) if (!x.Equals(o)) rest.Add(x);
                rest.Add(o);
                if (Fits(match, rest, screenW, screenH)) return match;
            }
            return b;
        }

        public static Box Clamp(Box b, float screenW, float screenH) =>
            b.At(Math.Max(0f, Math.Min(b.X, screenW - b.W)), Math.Max(0f, Math.Min(b.Y, screenH - b.H)));

        // ------------------------------------------------------------------ resizing

        /// <summary>The edges being dragged: one for an edge handle, two for a corner. The others stay put.</summary>
        [Flags]
        public enum Edge
        {
            None = 0,
            Left = 1,
            Top = 2,
            Right = 4,
            Bottom = 8,
            TopLeft = Top | Left,
            TopRight = Top | Right,
            BottomLeft = Bottom | Left,
            BottomRight = Bottom | Right,
        }

        /// <summary>
        /// Resizes a window by dragging <paramref name="edges"/> by (<paramref name="dx"/>,
        /// <paramref name="dy"/>): never below <paramref name="minW"/> x <paramref name="minH"/>,
        /// never off screen, the moving edges snapping onto the screen's and neighbours' edges within
        /// <paramref name="snap"/>, and stopping flush where they would run into a neighbour (always
        /// an answer: the edge stops, it never jumps back). Guides list the edges it snapped to.
        /// </summary>
        public static Box Resize(Box start, Edge edges, float dx, float dy, float minW, float minH,
            IReadOnlyList<Box> others, float screenW, float screenH, float snap = 0f, List<Guide> guides = null)
        {
            bool left = (edges & Edge.Left) != 0, right = (edges & Edge.Right) != 0;
            bool top = (edges & Edge.Top) != 0, bottom = (edges & Edge.Bottom) != 0;
            // A window already under its minimum (the minimum grew) may stay as it is, never shrink further.
            minW = Math.Min(minW, start.W);
            minH = Math.Min(minH, start.H);

            float x0 = start.X, x1 = start.Right, y0 = start.Y, y1 = start.Bottom;
            if (left) x0 = start.X + dx;
            if (right) x1 = start.Right + dx;
            if (top) y0 = start.Y + dy;
            if (bottom) y1 = start.Bottom + dy;

            // The moving edges snap onto the screen's edges and the edges of neighbours level with them.
            if (snap > 0f)
            {
                var probe = new Box(Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0));
                var xs = new List<float> { 0f, screenW };
                var ys = new List<float> { 0f, screenH };
                foreach (Box o in others)
                {
                    if (Level(probe, o, snap, true))
                    {
                        xs.Add(o.X);
                        xs.Add(o.Right);
                    }
                    if (Level(probe, o, snap, false))
                    {
                        ys.Add(o.Y);
                        ys.Add(o.Bottom);
                    }
                }
                if (left) x0 = SnapEdge(x0, xs, snap, true, guides);
                if (right) x1 = SnapEdge(x1, xs, snap, true, guides);
                if (top) y0 = SnapEdge(y0, ys, snap, false, guides);
                if (bottom) y1 = SnapEdge(y1, ys, snap, false, guides);
            }

            // Kept on screen and above the minimum size; the fixed edges never move.
            if (left) x0 = Math.Max(0f, Math.Min(x0, x1 - minW));
            if (right) x1 = Math.Min(screenW, Math.Max(x1, x0 + minW));
            if (top) y0 = Math.Max(0f, Math.Min(y0, y1 - minH));
            if (bottom) y1 = Math.Min(screenH, Math.Max(y1, y0 + minH));

            // Stop flush at neighbours: each neighbour the box now runs into pushes back whichever
            // moving edge has the least to give up, so a diagonal drag slides along a neighbour.
            for (int pass = 0; pass <= others.Count; pass++)
            {
                bool moved = false;
                foreach (Box o in others)
                {
                    var b = new Box(x0, y0, x1 - x0, y1 - y0);
                    if (!b.Overlaps(o)) continue;
                    float bestGive = float.MaxValue;
                    int choice = -1;
                    if (right && o.X >= start.Right - 0.5f && x1 - o.X < bestGive) { bestGive = x1 - o.X; choice = 0; }
                    if (left && o.Right <= start.X + 0.5f && o.Right - x0 < bestGive) { bestGive = o.Right - x0; choice = 1; }
                    if (bottom && o.Y >= start.Bottom - 0.5f && y1 - o.Y < bestGive) { bestGive = y1 - o.Y; choice = 2; }
                    if (top && o.Bottom <= start.Y + 0.5f && o.Bottom - y0 < bestGive) { bestGive = o.Bottom - y0; choice = 3; }
                    if (choice == 0) x1 = o.X;
                    else if (choice == 1) x0 = o.Right;
                    else if (choice == 2) y1 = o.Y;
                    else if (choice == 3) y0 = o.Bottom;
                    moved |= choice >= 0;
                }
                if (!moved) break;
            }
            return new Box(x0, y0, Math.Max(0f, x1 - x0), Math.Max(0f, y1 - y0));
        }

        private static float SnapEdge(float edge, List<float> lines, float snap, bool vertical, List<Guide> guides)
        {
            float best = snap;
            float result = edge;
            float? at = null;
            foreach (float line in lines)
            {
                float d = Math.Abs(edge - line);
                if (d < best)
                {
                    best = d;
                    result = line;
                    at = line;
                }
            }
            if (at.HasValue) guides?.Add(new Guide(vertical, at.Value));
            return (float)Math.Round(result);
        }

        /// <summary>
        /// For a panel that may only take certain sizes (extra action bars): the allowed size nearest
        /// to the one dragged to, kept where the edges not being dragged are, that fits on screen and
        /// clear of the neighbours. <paramref name="start"/> when none fits.
        /// </summary>
        public static Box NearestAllowed(Box start, Box dragged, Edge edges, IReadOnlyList<(float W, float H)> sizes,
            IReadOnlyList<Box> others, float screenW, float screenH)
        {
            Box best = start;
            float bestDistance = float.MaxValue;
            foreach ((float w, float h) in sizes)
            {
                Box c = AnchoredResize(start, edges, w, h);
                if (!Fits(c, others, screenW, screenH)) continue;
                float d = (w - dragged.W) * (w - dragged.W) + (h - dragged.H) * (h - dragged.H);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = c;
                }
            }
            return best;
        }

        /// <summary>The allowed size nearest a wanted one (for a window with a saved size not on the list).</summary>
        public static (float W, float H) NearestSize(float w, float h, IReadOnlyList<(float W, float H)> sizes, float maxW = float.MaxValue, float maxH = float.MaxValue)
        {
            (float W, float H) best = sizes.Count > 0 ? sizes[0] : (w, h);
            float bestDistance = float.MaxValue;
            foreach ((float sw, float sh) in sizes)
            {
                // A size bigger than the screen loses to any that fits.
                float d = (sw - w) * (sw - w) + (sh - h) * (sh - h) + (sw > maxW || sh > maxH ? 1e12f : 0f);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = (sw, sh);
                }
            }
            return best;
        }

        /// <summary>A box resized to w x h, the edges not being dragged staying where they are.</summary>
        private static Box AnchoredResize(Box start, Edge edges, float w, float h)
        {
            float x = (edges & Edge.Left) != 0 ? start.Right - w : start.X;
            float y = (edges & Edge.Top) != 0 ? start.Bottom - h : start.Y;
            return new Box(x, y, w, h);
        }

        // ------------------------------------------------------------------ attaching (default layouts)

        /// <summary>Which side of another window a window sits on.</summary>
        public enum Side
        {
            Above,
            Below,
            LeftOf,
            RightOf,
        }

        /// <summary>How a window lines up along the side it shares: start (left/top), centre or end (right/bottom).</summary>
        public enum Align
        {
            Start,
            Centre,
            End,
        }

        /// <summary>A box of w x h flush against <paramref name="target"/>'s side, lined up as asked, on whole pixels.</summary>
        public static Box Attach(Box target, Side side, Align align, float w, float h)
        {
            float x, y;
            if (side == Side.Above || side == Side.Below)
            {
                y = side == Side.Above ? target.Y - h : target.Bottom;
                x = align == Align.Start ? target.X : align == Align.End ? target.Right - w : target.CentreX - w / 2f;
            }
            else
            {
                x = side == Side.LeftOf ? target.X - w : target.Right;
                y = align == Align.Start ? target.Y : align == Align.End ? target.Bottom - h : target.CentreY - h / 2f;
            }
            return Round(new Box(x, y, w, h));
        }

        /// <summary>A box of w x h placed on the screen by an anchor (0 left/top to 1 right/bottom) and an offset, on whole pixels.</summary>
        public static Box Anchored(float anchorX, float anchorY, float offsetX, float offsetY, float w, float h, float screenW, float screenH) =>
            Round(new Box(anchorX * (screenW - w) + offsetX, anchorY * (screenH - h) + offsetY, w, h));

        // ------------------------------------------------------------------ settling a layout

        /// <summary>
        /// Moves windows so none overlaps another, moving as few as it can: every window that is on
        /// screen and overlaps nothing keeps its place; the rest (in order) go to the nearest free
        /// place, or stay where they are if there is no room at all. Sizes never change.
        /// </summary>
        public static List<Box> Settle(IReadOnlyList<Box> windows, float screenW, float screenH)
        {
            var result = new Box?[windows.Count];
            var placed = new List<Box>();
            for (int i = 0; i < windows.Count; i++)
            {
                Box w = windows[i];
                bool clear = w.X >= -0.5f && w.Y >= -0.5f && w.Right <= screenW + 0.5f && w.Bottom <= screenH + 0.5f;
                for (int j = 0; j < windows.Count && clear; j++)
                    if (j != i && w.Overlaps(windows[j])) clear = false;
                if (!clear) continue;
                result[i] = w;
                placed.Add(w);
            }
            for (int i = 0; i < windows.Count; i++)
            {
                if (result[i].HasValue) continue;
                Box w = windows[i];
                Box b = NearestFree(w, placed, screenW, screenH) ?? Clamp(w, screenW, screenH);
                result[i] = b;
                placed.Add(b);
            }
            var list = new List<Box>(windows.Count);
            foreach (Box? b in result) list.Add(b.Value);
            return list;
        }

        // ------------------------------------------------------------------ layouts per screen size

        /// <summary>The step (panel pixels) screen sizes are rounded to when picking which saved layout to use.</summary>
        public const int BucketStep = 64;

        /// <summary>
        /// The bucket a screen size (in panel pixels, so the interface scale counts) falls in: its
        /// size rounded to <see cref="BucketStep"/>. A layout is saved per bucket, so going from a
        /// laptop to a monitor and back keeps each its own arrangement.
        /// </summary>
        public static string BucketOf(float screenW, float screenH)
        {
            int w = (int)Math.Round(screenW / BucketStep) * BucketStep;
            int h = (int)Math.Round(screenH / BucketStep) * BucketStep;
            return w + "x" + h;
        }

        /// <summary>Of the buckets that have a layout, the one nearest a screen size (null if none): a new screen starts from it.</summary>
        public static string NearestBucket(IEnumerable<string> buckets, float screenW, float screenH)
        {
            string best = null;
            float bestDistance = float.MaxValue;
            foreach (string b in buckets)
            {
                string[] parts = b.Split('x');
                if (parts.Length != 2 || !float.TryParse(parts[0], out float w) || !float.TryParse(parts[1], out float h)) continue;
                float d = (w - screenW) * (w - screenW) + (h - screenH) * (h - screenH);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = b;
                }
            }
            return best;
        }
    }
}
