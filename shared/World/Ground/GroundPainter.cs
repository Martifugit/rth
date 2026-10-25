using System;
using System.Collections.Generic;

namespace Rth.Shared.World.Ground
{
    /// <summary>How one kind of tile looks: its colour (0..1), layer and shape group.</summary>
    public struct GroundLook
    {
        public float R, G, B;
        public GroundLayer Layer;
        /// <summary>Overlays shape against others of their group (water is always one group).</summary>
        public int Group;
        /// <summary>Blocked ground standing above the rest (a rock shelf, the frontier): drawn crisp, never blended.</summary>
        public bool Shelf;

        public static GroundLook Of(float r, float g, float b, GroundLayer layer = GroundLayer.Under, int group = 1) =>
            new GroundLook { R = r, G = g, B = b, Layer = layer, Group = layer == GroundLayer.Water ? 1 << 20 : group };
    }

    /// <summary>
    /// A region's ground to paint (<see cref="GroundPainter"/>): its tiles' kinds over the region and
    /// a margin of its neighbours' tiles all round, so colours blend across region borders the same
    /// way from both sides.
    /// </summary>
    public sealed class GroundCanvas
    {
        /// <summary>The region's south-west tile and size, and the margin of neighbouring tiles around it.</summary>
        public int X0, Y0, Width, Height, Margin;
        /// <summary>Kinds over the region grown by <see cref="Margin"/>, row by row from the south; -1: none.</summary>
        public int[] Kinds = Array.Empty<int>();
        public GroundLook[] Looks = Array.Empty<GroundLook>();
        /// <summary>The region's heights (null: flat); tiles too steep to walk are drawn as cliff.</summary>
        public Heightfield? Field;
        public float CliffStep = 1f;
        /// <summary>The colour of cliff faces, per kind (null: <see cref="CliffColour"/>).</summary>
        public float[]? CliffColours;
        public float[] CliffColour = { 0.42f, 0.40f, 0.37f };

        public int Cols => Width + 2 * Margin;
        public int Rows => Height + 2 * Margin;

        /// <summary>The kind of an absolute tile (-1 outside the canvas or no ground).</summary>
        public int KindAt(int x, int y)
        {
            int i = x - X0 + Margin, j = y - Y0 + Margin;
            return i < 0 || j < 0 || i >= Cols || j >= Rows ? -1 : Kinds[j * Cols + i];
        }
    }

    /// <summary>
    /// Paints a region's ground into a texture the way RuneScape paints its ground
    /// (docs/world-plan.md section 7): underlays blended over the tiles around each corner, so no
    /// tile edge shows; overlays and water crisp, with diagonal half tiles (<see cref="GroundShapes"/>);
    /// a gentle drift of lightness over the land so large areas don't look printed; cliffs in rock.
    /// Albedo only: the client lights it, and <see cref="Shade"/> lights it for pictures. Shared, so
    /// `tools/world render` draws exactly what the client draws.
    /// </summary>
    public static class GroundPainter
    {
        /// <summary>How far, in tiles, an underlay's colour reaches into its neighbours'.</summary>
        public const int BlendRadius = 2;

        /// <summary>The ground as RGBA bytes, <paramref name="pixelsPerTile"/> to a tile, rows from the south (a texture's order).</summary>
        public static byte[] Paint(GroundCanvas c, int pixelsPerTile, out int width, out int height)
        {
            int ppt = Math.Max(1, pixelsPerTile);
            width = c.Width * ppt;
            height = c.Height * ppt;
            var rgba = new byte[width * height * 4];
            float[] corners = BlendCorners(c);
            GroundShapes.Compute(c.Kinds, c.Cols, c.Rows, k => c.Looks[k].Shelf ? GroundLayer.Over : c.Looks[k].Layer,
                                 k => c.Looks[k].Shelf ? 1 << 21 : c.Looks[k].Group, out byte[] shape, out int[] other);
            int cw = c.Width + 1;

            for (int ty = 0; ty < c.Height; ty++)
                for (int tx = 0; tx < c.Width; tx++)
                {
                    int gi = (ty + c.Margin) * c.Cols + tx + c.Margin;
                    int own = c.Kinds[gi];
                    int ax = c.X0 + tx, ay = c.Y0 + ty;
                    bool cliff = own >= 0 && c.Field != null && c.Looks[own].Layer == GroundLayer.Under && c.Field.IsSteep(ax, ay, c.CliffStep);
                    float jitter = 1f + 0.035f * Hash01(ax, ay);
                    for (int py = 0; py < ppt; py++)
                        for (int px = 0; px < ppt; px++)
                        {
                            float u = (px + 0.5f) / ppt, v = (py + 0.5f) / ppt;
                            int kind = own;
                            if (shape[gi] != GroundShapes.None && GroundShapes.InHalf(shape[gi], u, v)) kind = other[gi];
                            float r, g, b;
                            if (kind >= 0 && c.Looks[kind].Layer != GroundLayer.Under || kind >= 0 && c.Looks[kind].Shelf)
                            {
                                GroundLook l = c.Looks[kind];
                                // Made surfaces are near flat, as RuneScape's overlays are: only a breath of variation.
                                float j = l.Layer == GroundLayer.Water ? 1f : 1f + (jitter - 1f) * 0.35f;
                                r = l.R * j; g = l.G * j; b = l.B * j;
                            }
                            else if (cliff)
                            {
                                float[] rock = own >= 0 && c.CliffColours != null && own * 3 + 2 < c.CliffColours.Length && c.CliffColours[own * 3] >= 0
                                    ? new[] { c.CliffColours[own * 3], c.CliffColours[own * 3 + 1], c.CliffColours[own * 3 + 2] } : c.CliffColour;
                                float d = 0.92f + 0.08f * v; // the face darkens towards its foot
                                r = rock[0] * d * jitter; g = rock[1] * d * jitter; b = rock[2] * d * jitter;
                            }
                            else
                            {
                                // Bilinear between the tile's blended corners.
                                int s = (ty * cw + tx) * 3, n = ((ty + 1) * cw + tx) * 3;
                                r = Lerp(Lerp(corners[s], corners[s + 3], u), Lerp(corners[n], corners[n + 3], u), v);
                                g = Lerp(Lerp(corners[s + 1], corners[s + 4], u), Lerp(corners[n + 1], corners[n + 4], u), v);
                                b = Lerp(Lerp(corners[s + 2], corners[s + 5], u), Lerp(corners[n + 2], corners[n + 5], u), v);
                            }
                            int o = ((ty * ppt + py) * width + tx * ppt + px) * 4;
                            rgba[o] = Byte(r);
                            rgba[o + 1] = Byte(g);
                            rgba[o + 2] = Byte(b);
                            rgba[o + 3] = 255;
                        }
                }
            return rgba;
        }

        /// <summary>How far apart two colours (RGB, 0..1) can be and still blend fully; further apart, they keep an edge.</summary>
        public const float BlendSimilarity = 0.11f;

        /// <summary>
        /// Each corner's underlay colour: a tent-weighted mean of the underlay tiles within
        /// <see cref="BlendRadius"/> (overlays and water don't count; they lie on top), weighted
        /// again by how alike each tile's colour is to the corner's own four tiles: grounds of close
        /// colours (grass and meadow) shade into each other over a few tiles, while very different
        /// ones (pasture and scree) meet at an edge half a tile wide instead of smearing into a
        /// stain. A faint drift of lightness over the land (the same at a corner whichever region
        /// paints it) keeps large areas from looking printed.
        /// </summary>
        private static float[] BlendCorners(GroundCanvas c)
        {
            int cw = c.Width + 1, ch = c.Height + 1, R = BlendRadius;
            var result = new float[cw * ch * 3];
            float inv = 1f / (BlendSimilarity * BlendSimilarity);
            for (int cy = 0; cy < ch; cy++)
                for (int cx = 0; cx < cw; cx++)
                {
                    float r = 0, g = 0, b = 0, w = 0;
                    int ax = c.X0 + cx, ay = c.Y0 + cy;
                    // The corner's own colour: the mean of the underlay tiles touching it.
                    float rr = 0, rg = 0, rb = 0, rn = 0;
                    for (int ty = ay - 1; ty <= ay; ty++)
                        for (int tx = ax - 1; tx <= ax; tx++)
                        {
                            int k = c.KindAt(tx, ty);
                            if (k < 0 || c.Looks[k].Layer != GroundLayer.Under || c.Looks[k].Shelf) continue;
                            rr += c.Looks[k].R; rg += c.Looks[k].G; rb += c.Looks[k].B; rn++;
                        }
                    if (rn > 0) { rr /= rn; rg /= rn; rb /= rn; }
                    // Tiles whose centre lies within R of the corner on both axes.
                    for (int ty = ay - R; ty < ay + R; ty++)
                        for (int tx = ax - R; tx < ax + R; tx++)
                        {
                            int k = c.KindAt(tx, ty);
                            if (k < 0) continue;
                            GroundLook l = c.Looks[k];
                            if (l.Layer != GroundLayer.Under || l.Shelf) continue;
                            float wx = R - Math.Abs(tx + 0.5f - ax), wy = R - Math.Abs(ty + 0.5f - ay);
                            float wt = wx * wy;
                            if (rn > 0)
                            {
                                float d2 = (l.R - rr) * (l.R - rr) + (l.G - rg) * (l.G - rg) + (l.B - rb) * (l.B - rb);
                                wt *= (float)Math.Exp(-d2 * inv);
                            }
                            r += l.R * wt; g += l.G * wt; b += l.B * wt; w += wt;
                        }
                    if (w <= 0f)
                    {
                        // Nothing natural near (the middle of a lake or a square): the nearest tile's own colour.
                        int k = c.KindAt(ax, ay);
                        if (k < 0) k = c.KindAt(ax - 1, ay - 1);
                        GroundLook l = k >= 0 ? c.Looks[k] : default;
                        r = l.R; g = l.G; b = l.B; w = 1f;
                    }
                    float drift = 1f + 0.03f * Noise(ax / 17f, ay / 17f) + 0.015f * Noise(ax / 5.3f + 40f, ay / 5.3f);
                    int o = (cy * cw + cx) * 3;
                    result[o] = r / w * drift;
                    result[o + 1] = g / w * drift;
                    result[o + 2] = b / w * drift;
                }
            return result;
        }

        /// <summary>
        /// Lights painted ground for a picture (the client lights it itself): a low sun from the
        /// south-west, so slopes facing it brighten and the far sides darken, as the game's sun does.
        /// </summary>
        public static void Shade(byte[] rgba, int width, int height, int pixelsPerTile, GroundCanvas c)
        {
            if (c.Field == null) return;
            Heightfield f = c.Field;
            float lx = -0.55f, ly = 0.75f, lz = -0.38f; // towards the sun: west, up, south
            float len = (float)Math.Sqrt(lx * lx + ly * ly + lz * lz);
            lx /= len; ly /= len; lz /= len;
            for (int py = 0; py < height; py++)
                for (int px = 0; px < width; px++)
                {
                    float x = c.X0 + (px + 0.5f) / pixelsPerTile, z = c.Y0 + (py + 0.5f) / pixelsPerTile;
                    float e = 0.5f;
                    float dx = (f.Sample(x + e, z) - f.Sample(x - e, z)) / (2 * e);
                    float dz = (f.Sample(x, z + e) - f.Sample(x, z - e)) / (2 * e);
                    float nx = -dx, ny = 1f, nz = -dz;
                    float nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    float lambert = Math.Max(0f, (nx * lx + ny * ly + nz * lz) / nl);
                    float light = 0.5f + 0.62f * lambert;
                    int o = (py * width + px) * 4;
                    for (int k = 0; k < 3; k++) rgba[o + k] = Byte(rgba[o + k] / 255f * light);
                }
        }

        /// <summary>Kinds over a region grown by a margin, from a tiles piece (see <see cref="Data.Piece.Surround"/>).</summary>
        public static int[] Grid(IList<string> rows, IList<string>? surround, int width, int height, int margin, Func<char, int> kindOf)
        {
            int cols = width + 2 * margin, rowsN = height + 2 * margin;
            var grid = new int[cols * rowsN];
            for (int i = 0; i < grid.Length; i++) grid[i] = -1;
            void Set(int col, int rowFromNorth, char ch)
            {
                int y = rowsN - 1 - rowFromNorth;
                if (col < 0 || col >= cols || y < 0 || y >= rowsN || ch == ' ') return;
                grid[y * cols + col] = kindOf(ch);
            }
            for (int r = 0; r < rows.Count && r < height; r++)
                for (int x = 0; x < rows[r].Length && x < width; x++) Set(x + margin, r + margin, rows[r][x]);
            if (surround != null && margin > 0)
            {
                // The margin: `margin` full rows along the north, then for each of the region's rows the
                // west and east parts (2 × margin characters), then `margin` full rows along the south.
                for (int r = 0; r < surround.Count; r++)
                {
                    string s = surround[r];
                    if (r < margin || r >= margin + height)
                        for (int x = 0; x < s.Length && x < cols; x++) Set(x, r, s[x]);
                    else
                        for (int x = 0; x < s.Length && x < 2 * margin; x++) Set(x < margin ? x : width + x, r, s[x]);
                }
            }
            // Where a neighbour isn't known, the region's own edge carries on outwards.
            for (int y = 0; y < rowsN; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    if (grid[i] >= 0) continue;
                    int ix = Math.Max(margin, Math.Min(margin + width - 1, x)), iy = Math.Max(margin, Math.Min(margin + height - 1, y));
                    if (ix != x || iy != y) grid[i] = grid[iy * cols + ix];
                }
            return grid;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static byte Byte(float v) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v * 255f)));

        /// <summary>A tile's own small lightness offset in [-1, 1].</summary>
        private static float Hash01(int x, int y)
        {
            uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u;
            h ^= h >> 13; h *= 0x2C1B3C6Du; h ^= h >> 15;
            return (h & 0xFFFF) / 32767.5f - 1f;
        }

        /// <summary>Smooth value noise in [-1, 1].</summary>
        private static float Noise(float x, float y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            float sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            float a = Hash01(ix, iy), b = Hash01(ix + 1, iy), c = Hash01(ix, iy + 1), d = Hash01(ix + 1, iy + 1);
            return Lerp(Lerp(a, b, sx), Lerp(c, d, sx), sy);
        }
    }
}
