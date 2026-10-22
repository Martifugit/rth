using System;
using System.Collections.Generic;
using System.Globalization;

namespace Rth.Shared.World.Layout
{
    /// <summary>
    /// Paints a <see cref="WorldChart"/>'s ground as a picture, one pixel a tile: each ground in its
    /// colour with edges that wander (so 8-tile zones don't show as blocks), hill shading from the
    /// grounds' heights, shallows along the coast, an inked coastline and faint borders between the
    /// powers. Lines, places and labels are drawn over it by whoever shows it (the client draws them
    /// as vectors, so they stay crisp when zoomed). Plain maths with no Unity, so it can run on a
    /// worker thread and in tools.
    /// </summary>
    public static class ChartPainter
    {
        /// <summary>
        /// RGBA, four bytes a pixel, rows from the <b>south</b> (Unity's texture order), one pixel
        /// for every <paramref name="tilesPerPixel"/> tiles (1 for the full picture; more for a
        /// quick draft): <paramref name="width"/> = chart.WidthTiles / tilesPerPixel.
        /// <paramref name="resolve"/> turns a colour that isn't "#rrggbb" (an art palette name) into one.
        /// </summary>
        public static byte[] Paint(WorldChart chart, Func<string, Rgb?>? resolve, int tilesPerPixel, out int width, out int height)
        {
            int Z = chart.ZoneTiles, cols = chart.Cols, rows = chart.Rows;
            float tpp = Math.Max(1, tilesPerPixel);
            int W = width = cols * Z / (int)tpp, H = height = rows * Z / (int)tpp;
            byte[] ground = chart.GroundGrid, power = chart.PowerGrid;
            int zones = cols * rows;

            var colours = new Rgb[chart.Grounds.Count];
            var water = new bool[chart.Grounds.Count];
            var texture = new float[chart.Grounds.Count];
            for (int g = 0; g < colours.Length; g++)
            {
                ChartGround def = chart.Grounds[g];
                colours[g] = Parse(def.Colour) ?? resolve?.Invoke(def.Colour) ?? new Rgb(128, 128, 128);
                water[g] = def.Water;
                texture[g] = Math.Min(0.12f, 0.02f + def.Rough * 0.025f);
            }
            bool IsWaterZone(int z) => ground[z] < water.Length && water[ground[z]];

            // Per zone: distance to water (land) or to land (water), heights, and the hill shade.
            int[] toWater = Distances(cols, rows, z => IsWaterZone(z));
            int[] toLand = Distances(cols, rows, z => !IsWaterZone(z));
            var heights = new float[zones];
            for (int z = 0; z < zones; z++)
                heights[z] = IsWaterZone(z) || ground[z] >= chart.Grounds.Count ? 0f : chart.Grounds[ground[z]].Height * Math.Min(1f, toWater[z] / 3f);
            var shade = new float[zones];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    float H0(int cc, int rr) => heights[Math.Max(0, Math.Min(rows - 1, rr)) * cols + Math.Max(0, Math.Min(cols - 1, cc))];
                    float dx = (H0(c + 1, r) - H0(c - 1, r)) / (2f * Z);
                    float dy = (H0(c, r + 1) - H0(c, r - 1)) / (2f * Z);
                    // Light from the north-west: slopes rising to the south-east face it.
                    shade[r * cols + c] = Clamp(1f + 0.9f * (dx - dy), 0.62f, 1.3f);
                }

            // Each pixel's ground and power, read at a point pushed about by noise.
            var gbuf = new byte[W * H];
            var pbuf = new byte[W * H];
            uint seed = 7;
            for (int py = 0; py < H; py++)
                for (int px = 0; px < W; px++)
                {
                    float x = (px + 0.5f) * tpp, y = (py + 0.5f) * tpp;
                    float jx = x + 11f * (Noise(x / 26f, y / 26f, seed) - 0.5f) + 5f * (Noise(x / 9f, y / 9f, seed + 2) - 0.5f) + 1.6f * (Noise(x / 3f, y / 3f, seed + 4) - 0.5f);
                    float jy = y + 11f * (Noise(x / 26f, y / 26f, seed + 1) - 0.5f) + 5f * (Noise(x / 9f, y / 9f, seed + 3) - 0.5f) + 1.6f * (Noise(x / 3f, y / 3f, seed + 5) - 0.5f);
                    int c = Math.Max(0, Math.Min(cols - 1, (int)(jx / Z))), r = Math.Max(0, Math.Min(rows - 1, (int)(jy / Z)));
                    int p = py * W + px;
                    gbuf[p] = ground[r * cols + c];
                    pbuf[p] = power[r * cols + c];
                }

            var rgba = new byte[W * H * 4];
            var ink = new Rgb(34, 26, 18);
            for (int py = 0; py < H; py++)
                for (int px = 0; px < W; px++)
                {
                    int p = py * W + px;
                    int g = gbuf[p];
                    bool wet = g < water.Length && water[g];
                    float x = (px + 0.5f) * tpp, y = (py + 0.5f) * tpp;
                    Rgb col = g < colours.Length ? colours[g] : new Rgb(128, 128, 128);
                    float grain = Noise(x / 2.2f, y / 2.2f, seed + 9) - 0.5f;
                    if (wet)
                    {
                        float d = Bilinear(toLand, cols, rows, x / Z, y / Z);
                        float shallow = Clamp(1.6f - d * 0.55f, 0f, 1f);
                        col = col.Mix(new Rgb(Mul(col.R, 1.5f), Mul(col.G, 1.55f), Mul(col.B, 1.35f)), shallow * 0.55f);
                        col = col.Scale(1f + grain * 0.05f + (Noise(x / 9f, y / 3f, seed + 11) - 0.5f) * 0.06f);
                    }
                    else
                    {
                        float s = Bilinear(shade, cols, rows, x / Z - 0.5f, y / Z - 0.5f, true);
                        float blot = Noise(x / 6f, y / 6f, seed + 13) - 0.5f;
                        col = col.Scale(s * (1f + grain * texture[g] * 2f + blot * texture[g] * 1.6f));
                    }

                    // Coastline: land beside water gets a line of ink, water beside land a pale rim.
                    bool edge = false, border = false, seam = false;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = px + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = py + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        int q = ny * W + nx;
                        int ng = gbuf[q];
                        bool nwet = ng < water.Length && water[ng];
                        if (nwet != wet) edge = true;
                        else if (!wet && ng != g) seam = true;
                        if (!wet && pbuf[q] != pbuf[p] && pbuf[q] != WorldChart.NoPower && pbuf[p] != WorldChart.NoPower) border = true;
                    }
                    if (edge) col = wet ? col.Mix(new Rgb(214, 226, 222), 0.35f) : col.Mix(ink, 0.7f);
                    else if (border && ((px >> 2) + (py >> 2) & 1) == 0) col = col.Mix(ink, 0.38f);
                    else if (seam) col = col.Mix(ink, 0.14f);

                    // A slight grade towards the interface: a little less saturated, a little warmer.
                    int lum = (col.R * 30 + col.G * 59 + col.B * 11) / 100;
                    col = col.Mix(new Rgb((byte)lum, (byte)lum, (byte)lum), 0.2f);
                    col = new Rgb(col.R, Mul(col.G, 0.98f), Mul(col.B, 0.92f));

                    int o = p * 4;
                    rgba[o] = col.R;
                    rgba[o + 1] = col.G;
                    rgba[o + 2] = col.B;
                    rgba[o + 3] = 255;
                }
            return rgba;
        }

        /// <summary>"#rrggbb", or null for anything else.</summary>
        public static Rgb? Parse(string? colour)
        {
            if (colour == null || colour.Length != 7 || colour[0] != '#') return null;
            if (!int.TryParse(colour.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return null;
            return new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        }

        /// <summary>Zones from each zone to the nearest zone where <paramref name="target"/> holds (0 there).</summary>
        private static int[] Distances(int cols, int rows, Func<int, bool> target)
        {
            var dist = new int[cols * rows];
            var queue = new Queue<int>();
            for (int z = 0; z < dist.Length; z++)
            {
                if (target(z)) { dist[z] = 0; queue.Enqueue(z); }
                else dist[z] = int.MaxValue;
            }
            while (queue.Count > 0)
            {
                int z = queue.Dequeue();
                int c = z % cols, r = z / cols;
                for (int k = 0; k < 4; k++)
                {
                    int nc = c + (k == 0 ? 1 : k == 1 ? -1 : 0), nr = r + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nc < 0 || nr < 0 || nc >= cols || nr >= rows) continue;
                    int n = nr * cols + nc;
                    if (dist[n] != int.MaxValue) continue;
                    dist[n] = dist[z] + 1;
                    queue.Enqueue(n);
                }
            }
            for (int z = 0; z < dist.Length; z++) if (dist[z] == int.MaxValue) dist[z] = 99;
            return dist;
        }

        private static float Bilinear(int[] grid, int cols, int rows, float c, float r)
        {
            // Distances are whole zones; read between zone centres.
            c -= 0.5f;
            r -= 0.5f;
            int c0 = (int)Math.Floor(c), r0 = (int)Math.Floor(r);
            float tc = c - c0, tr = r - r0;
            float G(int cc, int rr) => grid[Math.Max(0, Math.Min(rows - 1, rr)) * cols + Math.Max(0, Math.Min(cols - 1, cc))];
            return Lerp(Lerp(G(c0, r0), G(c0 + 1, r0), tc), Lerp(G(c0, r0 + 1), G(c0 + 1, r0 + 1), tc), tr);
        }

        private static float Bilinear(float[] grid, int cols, int rows, float c, float r, bool _)
        {
            int c0 = (int)Math.Floor(c), r0 = (int)Math.Floor(r);
            float tc = c - c0, tr = r - r0;
            float G(int cc, int rr) => grid[Math.Max(0, Math.Min(rows - 1, rr)) * cols + Math.Max(0, Math.Min(cols - 1, cc))];
            return Lerp(Lerp(G(c0, r0), G(c0 + 1, r0), tc), Lerp(G(c0, r0 + 1), G(c0 + 1, r0 + 1), tc), tr);
        }

        /// <summary>Smooth value noise in [0, 1].</summary>
        public static float Noise(float x, float y, uint seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float tx = x - x0, ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Lattice(x0, y0, seed), b = Lattice(x0 + 1, y0, seed), c = Lattice(x0, y0 + 1, seed), d = Lattice(x0 + 1, y0 + 1, seed);
            return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), ty);
        }

        // A 256 × 256 table of random values, tiled; the seed shifts where a noise reads it.
        private static readonly float[] Table = BuildTable();

        private static float[] BuildTable()
        {
            var t = new float[256 * 256];
            for (int i = 0; i < t.Length; i++) t[i] = (Geometry.Hash(0x5EEDu, (uint)i) & 0xFFFF) / 65535f;
            return t;
        }

        private static float Lattice(int x, int y, uint seed) =>
            Table[((x + (int)(seed * 97u)) & 255) | (((y + (int)(seed * 61u)) & 255) << 8)];

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        private static byte Mul(byte v, float f) => (byte)Math.Max(0, Math.Min(255, (int)(v * f + 0.5f)));

        public readonly struct Rgb
        {
            public readonly byte R, G, B;

            public Rgb(byte r, byte g, byte b)
            {
                R = r;
                G = g;
                B = b;
            }

            public Rgb Mix(Rgb o, float t) =>
                new Rgb((byte)(R + (o.R - R) * t), (byte)(G + (o.G - G) * t), (byte)(B + (o.B - B) * t));

            public Rgb Scale(float f) => new Rgb(Mul(R, f), Mul(G, f), Mul(B, f));
        }
    }
}
