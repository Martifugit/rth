using System;
using System.Collections.Generic;

namespace Rth.Shared.World.Layout
{
    /// <summary>
    /// A first terrain for the generated land (docs/layout.md section 8), from <see cref="TerrainDef"/>:
    /// each tile's ground gives a height, smoothed so hills spread into foothills; the crest gets a
    /// sharper ridge on top; each pass is cut as an even climb to its saddle; and the land falls to 0
    /// at the edge of every flat built region. One field over the whole layout, so generated regions
    /// agree at their borders. Scaffolding like the rest of the blockout: a region taken over keeps
    /// its heights, and its edges must meet the generated land around it (`tools/world check`).
    /// </summary>
    public sealed class LandHeights
    {
        /// <summary>The tiles covered: corners run from (X0, Y0) to (X0 + Width, Y0 + Height).</summary>
        public readonly int X0, Y0, Width, Height;
        private readonly float[] _corners;

        private LandHeights(int x0, int y0, int width, int height)
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

        /// <param name="tileAt">The blockout's character for a tile (its ground's letter, or a road, a place or the frontier).</param>
        public static LandHeights Compute(ContinentData data, int x0, int y0, int width, int height,
                                          Func<int, int, char> tileAt, IReadOnlyList<BuiltRegion> built)
        {
            var land = new LandHeights(x0, y0, width, height);
            TerrainDef? t = data.Terrain;
            if (t == null) return land;

            // What each tile asks for: its ground's height. Roads, places, rivers, bridges and passes
            // ask nothing and follow the land around them; open water and the frontier lie at 0.
            var rise = new Dictionary<char, float>();
            var ridge = new HashSet<char>();
            foreach (KeyValuePair<string, GroundDef> g in data.Grounds)
            {
                char c = g.Value.Letter[0];
                if (g.Key == data.RiverGround || g.Key == data.BridgeGround || g.Key == data.PassGround) continue;
                rise[c] = g.Value.Water ? 0f : t.Heights.TryGetValue(g.Key, out float h) ? h : 0f;
                if (t.RidgeGrounds.Contains(g.Key)) ridge.Add(c);
            }
            rise[Blockout.Frontier] = 0f;
            var sum = new float[width * height];
            var weight = new float[width * height];
            var crest = new float[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    char c = tileAt(x0 + x, y0 + y);
                    if (rise.TryGetValue(c, out float h))
                    {
                        sum[y * width + x] = h;
                        weight[y * width + x] = 1f;
                    }
                    if (ridge.Contains(c)) crest[y * width + x] = 1f;
                }
            for (int pass = 0; pass < 3; pass++)
            {
                Blur(sum, width, height, t.Smooth);
                Blur(weight, width, height, t.Smooth);
            }
            for (int pass = 0; pass < 2; pass++) Blur(crest, width, height, 2);
            float top = 0f;
            foreach (float h in t.Heights.Values) top = Math.Max(top, h);
            var tile = new float[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    float baseHeight = weight[i] > 1e-4f ? sum[i] / weight[i] : 0f;
                    float wx = x0 + x, wy = y0 + y;
                    // Raised land is uneven, more so the higher it is; the crest breaks into peaks and cols.
                    float rough = top > 0 ? t.Rough * Math.Min(1f, baseHeight / top) * Noise(data.Seed + 7, wx / 23f, wy / 23f) : 0f;
                    float peaks = t.Peaks * crest[i] * Ridged(data.Seed + 11, wx / t.PeakScale, wy / t.PeakScale);
                    tile[i] = baseHeight + rough + t.Ridge * crest[i] + peaks;
                }

            // Corners: the mean of the tiles around them.
            for (int y = 0; y <= height; y++)
                for (int x = 0; x <= width; x++)
                {
                    float s = 0f;
                    for (int dy = -1; dy <= 0; dy++)
                        for (int dx = -1; dx <= 0; dx++)
                        {
                            int tx = Math.Max(0, Math.Min(width - 1, x + dx)), ty = Math.Max(0, Math.Min(height - 1, y + dy));
                            s += tile[ty * width + tx];
                        }
                    land._corners[y * (width + 1) + x] = s / 4f;
                }

            foreach (PassDef pass in data.Spine.Passes) land.Cut(pass, t);
            land.Settle(built, t.BuiltBlend);
            for (int i = 0; i < land._corners.Length; i++)
                land._corners[i] = (float)Math.Round(Math.Max(-4f, Math.Min(16f, land._corners[i])) * 100f) / 100f;
            return land;
        }

        /// <summary>A pass climbs evenly from each end to its saddle (the middle point of its line).</summary>
        private void Cut(PassDef pass, TerrainDef t)
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
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (float[] p in line)
            {
                minX = Math.Min(minX, p[0]); maxX = Math.Max(maxX, p[0]);
                minY = Math.Min(minY, p[1]); maxY = Math.Max(maxY, p[1]);
            }
            for (int y = (int)Math.Floor(minY - reach); y <= (int)Math.Ceiling(maxY + reach); y++)
                for (int x = (int)Math.Floor(minX - reach); x <= (int)Math.Ceiling(maxX + reach); x++)
                {
                    int i = x - X0, j = y - Y0;
                    if (i < 0 || j < 0 || i > Width || j > Height) continue;
                    // The nearest point of the line: its distance, and how far along the line it is.
                    float best = float.MaxValue, bestS = 0f;
                    for (int k = 1; k < line.Count; k++)
                    {
                        float ax = line[k - 1][0], ay = line[k - 1][1], bx = line[k][0], by = line[k][1];
                        float dx = bx - ax, dy = by - ay, len2 = dx * dx + dy * dy;
                        float u = len2 > 0 ? Math.Max(0f, Math.Min(1f, ((x - ax) * dx + (y - ay) * dy) / len2)) : 0f;
                        float d = (float)Math.Sqrt(Sq(ax + u * dx - x) + Sq(ay + u * dy - y));
                        if (d < best) { best = d; bestS = along[k - 1] + u * (along[k] - along[k - 1]); }
                    }
                    if (best >= reach) continue;
                    float h = Profile(bestS);
                    int c = j * (Width + 1) + i;
                    float keep = best <= pass.HalfWidth ? 0f : Smooth((best - pass.HalfWidth) / t.PassBlend);
                    _corners[c] = h + (_corners[c] - h) * keep;
                }
        }

        /// <summary>The land falls to 0 at the edge of each flat built region (one without terrain of its own).</summary>
        private void Settle(IReadOnlyList<BuiltRegion> built, float blend)
        {
            foreach (BuiltRegion b in built)
            {
                if (b.HasTerrain) continue;
                for (int y = (int)Math.Floor(b.Y0 - blend); y <= (int)Math.Ceiling(b.Y1 + blend); y++)
                    for (int x = (int)Math.Floor(b.X0 - blend); x <= (int)Math.Ceiling(b.X1 + blend); x++)
                    {
                        int i = x - X0, j = y - Y0;
                        if (i < 0 || j < 0 || i > Width || j > Height) continue;
                        float dx = Math.Max(0, Math.Max(b.X0 - x, x - b.X1)), dy = Math.Max(0, Math.Max(b.Y0 - y, y - b.Y1));
                        float d = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (d < blend) _corners[j * (Width + 1) + i] *= Smooth(d / blend);
                    }
            }
        }

        /// <summary>A box blur of the given radius, rows then columns, in place.</summary>
        private static void Blur(float[] v, int width, int height, int radius)
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

        /// <summary>Ridged noise in [0, 1]: two octaves of 1 - |noise|, squared, so crests are sharp and cols deep.</summary>
        private static float Ridged(int seed, float x, float y)
        {
            float a = 1f - Math.Abs(Noise(seed, x, y));
            float b = 1f - Math.Abs(Noise(seed + 1, x * 2.1f, y * 2.1f));
            float r = 0.7f * a * a + 0.3f * b * b;
            return Math.Max(0f, Math.Min(1f, r));
        }

        /// <summary>Smooth value noise in [-1, 1], the same for a seed on every run.</summary>
        private static float Noise(int seed, float x, float y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            float sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            float Lattice(int a, int b) => (Geometry.Hash(Geometry.Hash((uint)seed, (uint)a), (uint)b) & 0xFFFF) / 32767.5f - 1f;
            float n0 = Lattice(ix, iy) + (Lattice(ix + 1, iy) - Lattice(ix, iy)) * sx;
            float n1 = Lattice(ix, iy + 1) + (Lattice(ix + 1, iy + 1) - Lattice(ix, iy + 1)) * sx;
            return n0 + (n1 - n0) * sy;
        }

        private static float Smooth(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3 - 2 * t);
        }

        private static float Sq(float v) => v * v;
    }
}
