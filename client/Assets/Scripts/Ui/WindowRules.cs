using System;
using System.Collections.Generic;

namespace Rth.Client.Ui
{
    /// <summary>
    /// The rules for placing docked windows (docs/interface.md), as plain maths with no Unity types
    /// so they are tested anywhere (`dotnet test` runs them through tools/client-tests): snapping
    /// to the screen, a grid and other windows' edges and centres (with the guide lines to draw),
    /// finding the nearest place a window fits without overlapping another, and resizing that
    /// stops at the neighbours. Positions are panel pixels, y down, origin top left.
    /// </summary>
    public static class WindowRules
    {
        /// <summary>The grid windows snap to when nothing nearer catches them.</summary>
        public const float Grid = 8f;

        /// <summary>How close (pixels) an edge must come to a line to snap onto it.</summary>
        public const float SnapDistance = 10f;

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
        /// Snaps a window being moved: each axis pulls its nearest edge or centre onto the nearest
        /// line within <see cref="SnapDistance"/> (the screen's edges and centre, other windows'
        /// edges and centres, so it can sit flush beside one or line up with it); failing that, onto
        /// the grid. Returns the snapped box and the lines it lined up with.
        /// </summary>
        public static Box Snap(Box moving, IReadOnlyList<Box> others, float screenW, float screenH, List<Guide> guides = null)
        {
            var xs = new List<float> { 0f, screenW, screenW / 2f };
            var ys = new List<float> { 0f, screenH, screenH / 2f };
            foreach (Box o in others)
            {
                xs.Add(o.X);
                xs.Add(o.Right);
                xs.Add(o.CentreX);
                ys.Add(o.Y);
                ys.Add(o.Bottom);
                ys.Add(o.CentreY);
            }
            float x = SnapAxis(moving.X, moving.W, xs, out float? gx);
            float y = SnapAxis(moving.Y, moving.H, ys, out float? gy);
            if (guides != null)
            {
                if (gx.HasValue) guides.Add(new Guide(true, gx.Value));
                if (gy.HasValue) guides.Add(new Guide(false, gy.Value));
            }
            return moving.At(x, y);
        }

        private static float SnapAxis(float start, float length, List<float> lines, out float? guide)
        {
            float best = SnapDistance;
            float result = start;
            guide = null;
            foreach (float line in lines)
            {
                // The window's start, centre or end onto the line.
                foreach (float offset in new[] { 0f, length / 2f, length })
                {
                    float d = Math.Abs(start + offset - line);
                    if (d < best)
                    {
                        best = d;
                        result = line - offset;
                        guide = line;
                    }
                }
            }
            if (guide == null) result = (float)Math.Round(start / Grid) * Grid;
            return result;
        }

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

        public static Box Clamp(Box b, float screenW, float screenH) =>
            b.At(Math.Max(0f, Math.Min(b.X, screenW - b.W)), Math.Max(0f, Math.Min(b.Y, screenH - b.H)));

        // ------------------------------------------------------------------ resizing

        /// <summary>Which corner is being dragged: the other corner stays put.</summary>
        public enum Corner
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
        }

        /// <summary>
        /// Resizes a window by dragging a corner by (<paramref name="dx"/>, <paramref name="dy"/>):
        /// never below <paramref name="minW"/> x <paramref name="minH"/>, never off screen, and
        /// stopping where it would run into a neighbour.
        /// </summary>
        public static Box Resize(Box start, Corner corner, float dx, float dy, float minW, float minH,
            IReadOnlyList<Box> others, float screenW, float screenH)
        {
            bool left = corner == Corner.TopLeft || corner == Corner.BottomLeft;
            bool top = corner == Corner.TopLeft || corner == Corner.TopRight;

            // The moving edges, kept off the screen's edges and above the minimum size.
            float x0 = start.X, x1 = start.Right, y0 = start.Y, y1 = start.Bottom;
            if (left) x0 = Math.Max(0f, Math.Min(start.X + dx, x1 - minW));
            else x1 = Math.Min(screenW, Math.Max(start.Right + dx, x0 + minW));
            if (top) y0 = Math.Max(0f, Math.Min(start.Y + dy, y1 - minH));
            else y1 = Math.Min(screenH, Math.Max(start.Bottom + dy, y0 + minH));

            // Stop the moving edges at neighbours: first horizontally within the old height, then
            // vertically within the new width, so a diagonal drag slides along a neighbour.
            foreach (Box o in others)
            {
                if (o.Bottom <= start.Y + 0.5f || o.Y >= start.Bottom - 0.5f) continue; // not beside it
                if (left && o.Right <= start.X + 0.5f) x0 = Math.Max(x0, o.Right);
                if (!left && o.X >= start.Right - 0.5f) x1 = Math.Min(x1, o.X);
            }
            foreach (Box o in others)
            {
                if (o.Right <= x0 + 0.5f || o.X >= x1 - 0.5f) continue; // not above or below it
                if (top && o.Bottom <= start.Y + 0.5f) y0 = Math.Max(y0, o.Bottom);
                if (!top && o.Y >= start.Bottom - 0.5f) y1 = Math.Min(y1, o.Y);
            }
            var result = new Box(x0, y0, Math.Max(minW, x1 - x0), Math.Max(minH, y1 - y0));
            // If the minimum size itself can't fit, keep the window as it was.
            return Fits(result, others, screenW, screenH) ? result : start;
        }

        // ------------------------------------------------------------------ settling a layout

        /// <summary>
        /// Moves windows (in order: earlier ones keep their place) so none overlaps another: each
        /// goes to the nearest free place, or stays where it is if there is no room at all.
        /// </summary>
        public static List<Box> Settle(IReadOnlyList<Box> windows, float screenW, float screenH)
        {
            var placed = new List<Box>();
            foreach (Box w in windows)
            {
                Box? free = NearestFree(w, placed, screenW, screenH);
                placed.Add(free ?? Clamp(w, screenW, screenH));
            }
            return placed;
        }
    }
}
