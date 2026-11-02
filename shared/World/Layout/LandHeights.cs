using System;
using System.Collections.Generic;

namespace Rth.Shared.World.Layout
{
    /// <summary>
    /// The generated land's heights over the whole layout (docs/layout.md section 8,
    /// docs/world-plan.md), one per tile corner, in tiles. <see cref="Landscape"/> builds them in
    /// passes; this holds them and does the passes that only need heights: passes cut as climbs to a
    /// saddle, roads levelled across, settlement platforms, the meeting with built regions (falling
    /// to 0 at a flat one, blending into the edge of one with terrain of its own) and the hand-sculpted
    /// raise on top. One field over the whole layout, so generated regions agree at their borders.
    /// </summary>
    public sealed class LandHeights
    {
        /// <summary>The tiles covered: corners run from (X0, Y0) to (X0 + Width, Y0 + Height).</summary>
        public readonly int X0, Y0, Width, Height;
        private readonly float[] _corners;

        public LandHeights(int x0, int y0, int width, int height)
        {
            X0 = x0;
            Y0 = y0;
            Width = width;
            Height = height;
            _corners = new float[(width + 1) * (height + 1)];
        }

        /// <summary>The height of a corner, in tiles (0 outside).</summary>
        public float Corner(int x, int y)
        {
            int i = x - X0, j = y - Y0;
            return i < 0 || j < 0 || i > Width || j > Height ? 0f : _corners[j * (Width + 1) + i];
        }

        /// <summary>A corner by its position in the field (0, 0 is the south-west corner), clamped to the field.</summary>
        public float CornerAt(int i, int j) => _corners[Math.Max(0, Math.Min(Height, j)) * (Width + 1) + Math.Max(0, Math.Min(Width, i))];

        public float CornerIndex(int c) => _corners[c];
        public void SetCornerIndex(int c, float h) => _corners[c] = h;

        /// <summary>The ground at a point, on the same triangles as <see cref="Heightfield.Sample"/>.</summary>
        public float Sample(float x, float y)
        {
            float lx = Math.Max(0f, Math.Min(Width, x - X0)), ly = Math.Max(0f, Math.Min(Height, y - Y0));
            int i = Math.Min((int)Math.Floor(lx), Width - 1), j = Math.Min((int)Math.Floor(ly), Height - 1);
            float u = lx - i, v = ly - j;
            float sw = CornerAt(i, j), se = CornerAt(i + 1, j), nw = CornerAt(i, j + 1), ne = CornerAt(i + 1, j + 1);
            return u >= v ? sw + (se - sw) * u + (ne - se) * v : sw + (ne - nw) * u + (nw - sw) * v;
        }

        /// <summary>A rectangle's heights, or null where it is flat.</summary>
        public Heightfield? Field(int x0, int y0, int width, int height)
        {
            var field = new Heightfield(x0, y0, width, height);
            bool any = false;
            for (int y = 0; y <= height; y++)
                for (int x = 0; x <= width; x++)
                {
                    float h = Corner(x0 + x, y0 + y);
                    if (h != 0f) any = true;
                    field.SetCorner(x0 + x, y0 + y, h);
                }
            return any ? field : null;
        }

        /// <summary>Corners from tile heights: each the mean of the tiles round it.</summary>
        public void FromTiles(float[] tile)
        {
            for (int y = 0; y <= Height; y++)
                for (int x = 0; x <= Width; x++)
                {
                    float s = 0f;
                    for (int dy = -1; dy <= 0; dy++)
                        for (int dx = -1; dx <= 0; dx++)
                        {
                            int tx = Math.Max(0, Math.Min(Width - 1, x + dx)), ty = Math.Max(0, Math.Min(Height - 1, y + dy));
                            s += tile[ty * Width + tx];
                        }
                    _corners[y * (Width + 1) + x] = s / 4f;
                }
        }

        /// <summary>Rounds to the hundredth of a tile within the world's -4 to 16 (world-scale.md 3), then adds what was sculpted by hand.</summary>
        public void Finish(HeightEdits? edits)
        {
            Round();
            if (edits != null) Raise(edits);
        }

        private void Round()
        {
            for (int i = 0; i < _corners.Length; i++)
                _corners[i] = (float)Math.Round(Rth.Shared.World.Data.TerrainText.Clamp(_corners[i]) * 100f) / 100f;
        }

        /// <summary>
        /// Adds what was sculpted by hand, after rounding, so a height sculpted is the height drawn;
        /// a corner two regions share is raised once.
        /// </summary>
        private void Raise(HeightEdits edits)
        {
            var before = (float[])_corners.Clone();
            foreach (Heightfield raise in edits.Regions.Values)
                for (int y = raise.OriginY; y <= raise.OriginY + raise.Depth; y++)
                    for (int x = raise.OriginX; x <= raise.OriginX + raise.Width; x++)
                    {
                        int i = x - X0, j = y - Y0;
                        if (i < 0 || j < 0 || i > Width || j > Height) continue;
                        int c = j * (Width + 1) + i;
                        _corners[c] = before[c] + raise.Corner(x, y);
                    }
            Round();
        }

        /// <summary>A pass climbs evenly from each end to its saddle (the middle point of its line).</summary>
        public void Cut(PassDef pass, TerrainDef t)
        {
            IList<float[]> line = pass.Line;
            if (line.Count < 2) return;
            var along = new float[line.Count];
            for (int k = 1; k < line.Count; k++)
                along[k] = along[k - 1] + (float)Math.Sqrt(Sq(line[k][0] - line[k - 1][0]) + Sq(line[k][1] - line[k - 1][1]));
            float saddleAt = along[line.Count / 2], length = along[line.Count - 1];
            float start = Corner((int)Math.Round(line[0][0]), (int)Math.Round(line[0][1]));
            float end = Corner((int)Math.Round(line[line.Count - 1][0]), (int)Math.Round(line[line.Count - 1][1]));
            float Profile(float s) => s <= saddleAt
                ? start + (t.PassSaddle - start) * (saddleAt > 0 ? s / saddleAt : 1f)
                : t.PassSaddle + (end - t.PassSaddle) * (length > saddleAt ? (s - saddleAt) / (length - saddleAt) : 1f);

            float reach = pass.HalfWidth + t.PassBlend;
            Along(line, reach, (c, best, bestS) =>
            {
                float h = Profile(bestS);
                float keep = best <= pass.HalfWidth ? 0f : LandNoise.Smooth((best - pass.HalfWidth) / t.PassBlend);
                _corners[c] = h + (_corners[c] - h) * keep;
            });
        }

        /// <summary>
        /// A road levelled across: within <paramref name="reach"/> of its line the land takes the
        /// height of the line's own smoothed profile (fully on the road, fading beyond), so a road
        /// on a hillside is a shelf, not a tilt. Corners touching water are left alone.
        /// </summary>
        public void Level(LandLine road, float reach, Func<int, bool> waterTile)
        {
            if (road.Points.Count < 2) return;
            var pts = road.Points;
            var along = new float[pts.Count];
            for (int k = 1; k < pts.Count; k++) along[k] = along[k - 1] + (float)Math.Sqrt(Sq(pts[k][0] - pts[k - 1][0]) + Sq(pts[k][1] - pts[k - 1][1]));
            float length = along[pts.Count - 1];
            int n = Math.Max(2, (int)Math.Ceiling(length));
            var profile = new float[n + 1];
            for (int s = 0; s <= n; s++)
            {
                (float x, float y) = PointAt(pts, along, s);
                profile[s] = Sample(x, y);
            }
            // Smoothed along the road over about 8 tiles, so it climbs evenly.
            var smooth = new float[n + 1];
            for (int s = 0; s <= n; s++)
            {
                float sum = 0f;
                int count = 0;
                for (int k = Math.Max(0, s - 4); k <= Math.Min(n, s + 4); k++) { sum += profile[k]; count++; }
                smooth[s] = sum / count;
            }
            float inner = reach - 1.5f;
            Along(pts, reach, (c, d, s) =>
            {
                int cx = c % (Width + 1), cy = c / (Width + 1);
                for (int dy = -1; dy <= 0; dy++)
                    for (int dx = -1; dx <= 0; dx++)
                    {
                        int tx = cx + dx, ty = cy + dy;
                        if (tx >= 0 && ty >= 0 && tx < Width && ty < Height && waterTile(ty * Width + tx)) return;
                    }
                float h = smooth[Math.Max(0, Math.Min(n, (int)Math.Round(s)))];
                float keep = d <= inner ? 0f : LandNoise.Smooth((d - inner) / 1.5f);
                _corners[c] = h + (_corners[c] - h) * keep;
            });
        }

        /// <summary>A settlement's platform: the land within radius r eased towards its own mean height, flat across the middle.</summary>
        public void Platform(float cx, float cy, float r, Func<int, bool> waterCorner)
        {
            float sum = 0f;
            int count = 0;
            ForCorners(cx, cy, r * 0.5f, (c, d) =>
            {
                if (waterCorner(c)) return;
                sum += _corners[c];
                count++;
            });
            if (count == 0) return;
            float target = sum / count;
            ForCorners(cx, cy, r, (c, d) =>
            {
                if (waterCorner(c)) return;
                float keep = LandNoise.Smooth((d - r * 0.55f) / (r * 0.45f));
                _corners[c] = target + (_corners[c] - target) * keep;
            });
        }

        /// <summary>
        /// The land meets each built region at its edge: a flat one (no terrain of its own) at 0,
        /// falling over <paramref name="blend"/> tiles; one with terrain at its own edge heights, so
        /// a region taken over never shows a step against the generated land round it.
        /// </summary>
        public void Settle(IReadOnlyList<BuiltRegion> built, float blend)
        {
            foreach (BuiltRegion b in built)
            {
                for (int y = (int)Math.Floor(b.Y0 - blend); y <= (int)Math.Ceiling(b.Y1 + blend); y++)
                    for (int x = (int)Math.Floor(b.X0 - blend); x <= (int)Math.Ceiling(b.X1 + blend); x++)
                    {
                        int i = x - X0, j = y - Y0;
                        if (i < 0 || j < 0 || i > Width || j > Height) continue;
                        float dx = Math.Max(0, Math.Max(b.X0 - x, x - b.X1)), dy = Math.Max(0, Math.Max(b.Y0 - y, y - b.Y1));
                        float d = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (d >= blend) continue;
                        int c = j * (Width + 1) + i;
                        // The built region's own height at the nearest point of its edge (0 when flat).
                        float edge = b.Terrain == null ? 0f : b.Terrain.Corner(Math.Max(b.X0, Math.Min(b.X1, x)), Math.Max(b.Y0, Math.Min(b.Y1, y)));
                        _corners[c] = edge + (_corners[c] - edge) * LandNoise.Smooth(d / blend);
                    }
            }
            // Then every corner on or inside a built region is exactly its own, so neighbouring
            // built regions' blends can't move each other's borders.
            foreach (BuiltRegion b in built)
                for (int y = b.Y0; y <= b.Y1; y++)
                    for (int x = b.X0; x <= b.X1; x++)
                    {
                        int i = x - X0, j = y - Y0;
                        if (i < 0 || j < 0 || i > Width || j > Height) continue;
                        _corners[j * (Width + 1) + i] = b.Terrain == null ? 0f : b.Terrain.Corner(x, y);
                    }
        }

        private void ForCorners(float cx, float cy, float r, Action<int, float> at)
        {
            for (int y = (int)Math.Floor(cy - r); y <= (int)Math.Ceiling(cy + r); y++)
                for (int x = (int)Math.Floor(cx - r); x <= (int)Math.Ceiling(cx + r); x++)
                {
                    int i = x - X0, j = y - Y0;
                    if (i < 0 || j < 0 || i > Width || j > Height) continue;
                    float d = (float)Math.Sqrt(Sq(x - cx) + Sq(y - cy));
                    if (d <= r) at(j * (Width + 1) + i, d);
                }
        }

        /// <summary>Calls back once for each corner within reach of a line: its distance and how far along the line its nearest point is.</summary>
        private void Along(IList<float[]> line, float reach, Action<int, float, float> at)
        {
            var along = new float[line.Count];
            for (int k = 1; k < line.Count; k++)
                along[k] = along[k - 1] + (float)Math.Sqrt(Sq(line[k][0] - line[k - 1][0]) + Sq(line[k][1] - line[k - 1][1]));
            var seen = new Dictionary<int, (float d, float s)>();
            for (int k = 1; k < line.Count; k++)
            {
                float ax = line[k - 1][0], ay = line[k - 1][1], bx = line[k][0], by = line[k][1];
                float dx = bx - ax, dy = by - ay, len2 = dx * dx + dy * dy;
                for (int y = (int)Math.Floor(Math.Min(ay, by) - reach); y <= (int)Math.Ceiling(Math.Max(ay, by) + reach); y++)
                    for (int x = (int)Math.Floor(Math.Min(ax, bx) - reach); x <= (int)Math.Ceiling(Math.Max(ax, bx) + reach); x++)
                    {
                        int i = x - X0, j = y - Y0;
                        if (i < 0 || j < 0 || i > Width || j > Height) continue;
                        float u = len2 > 0 ? Math.Max(0f, Math.Min(1f, ((x - ax) * dx + (y - ay) * dy) / len2)) : 0f;
                        float d = (float)Math.Sqrt(Sq(ax + u * dx - x) + Sq(ay + u * dy - y));
                        if (d >= reach) continue;
                        int c = j * (Width + 1) + i;
                        float s = along[k - 1] + u * (along[k] - along[k - 1]);
                        if (!seen.TryGetValue(c, out var old) || d < old.d) seen[c] = (d, s);
                    }
            }
            foreach (KeyValuePair<int, (float d, float s)> e in seen) at(e.Key, e.Value.d, e.Value.s);
        }

        private static (float x, float y) PointAt(List<float[]> pts, float[] along, float s)
        {
            for (int k = 1; k < pts.Count; k++)
                if (along[k] >= s || k == pts.Count - 1)
                {
                    float seg = along[k] - along[k - 1];
                    float t = seg > 0 ? Math.Max(0f, Math.Min(1f, (s - along[k - 1]) / seg)) : 0f;
                    return (pts[k - 1][0] + (pts[k][0] - pts[k - 1][0]) * t, pts[k - 1][1] + (pts[k][1] - pts[k - 1][1]) * t);
                }
            return (pts[0][0], pts[0][1]);
        }

        /// <summary>A box blur of the given radius, rows then columns, in place.</summary>
        public static void Blur(float[] v, int width, int height, int radius)
        {
            if (radius <= 0) return;
            var tmp = new float[v.Length];
            var line = new double[Math.Max(width, height) + 1];
            for (int y = 0; y < height; y++)
            {
                line[0] = 0;
                for (int x = 0; x < width; x++) line[x + 1] = line[x] + v[y * width + x];
                for (int x = 0; x < width; x++)
                {
                    int a = Math.Max(0, x - radius), b = Math.Min(width, x + radius + 1);
                    tmp[y * width + x] = (float)((line[b] - line[a]) / (b - a));
                }
            }
            for (int x = 0; x < width; x++)
            {
                line[0] = 0;
                for (int y = 0; y < height; y++) line[y + 1] = line[y] + tmp[y * width + x];
                for (int y = 0; y < height; y++)
                {
                    int a = Math.Max(0, y - radius), b = Math.Min(height, y + radius + 1);
                    v[y * width + x] = (float)((line[b] - line[a]) / (b - a));
                }
            }
        }

        private static float Sq(float v) => v * v;
    }
}
