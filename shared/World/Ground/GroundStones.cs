using System;
using System.Collections.Generic;

namespace Rth.Shared.World.Ground
{
    /// <summary>One small flat stone lying on the ground: its outline (absolute tile coordinates, corner by corner) and colour.</summary>
    public sealed class Stone
    {
        public float[] Outline = Array.Empty<float>();
        public float R, G, B;
        /// <summary>Which of the three tones it has (0 pale, 1 mid, 2 dark); stones of one colour and tone share a material.</summary>
        public int Tone;
    }

    /// <summary>
    /// The stones lying over the ground (a tiles piece's <see cref="Data.Piece.Stones"/>), as
    /// RuneScape scatters small angular chips of rock over its paths and round its rocks: flat
    /// polygons of three to five corners, mostly small, in a pale, a mid and a dark tone of their
    /// colour. They are painted on their own, tile by tile and however thick, so they lie exactly
    /// where they were put (never spilling over a path's edge by themselves) and on any ground.
    /// They lie on the ground as geometry, so they stay crisp at any distance. Shared: the client
    /// builds them into a mesh, the tools draw them into pictures, and both put every stone in the
    /// same place.
    /// </summary>
    public static class GroundStones
    {
        /// <summary>Stones a tile of a share of 1.</summary>
        public const float PerTile = 10f;
        private const int Tries = 8;

        public static readonly float[] Tones = { 1.0f, 0.86f, 0.7f };

        /// <summary>The thicknesses the brush and the blockout paint: a few stones a tile, up to a close scatter.</summary>
        public static readonly float[] Levels = { 0.12f, 0.25f, 0.45f, 0.7f };

        /// <summary>The level nearest a share (-1: nearer none than the thinnest).</summary>
        public static int Level(float share)
        {
            if (share < Levels[0] * 0.5f) return -1;
            int best = 0;
            for (int i = 1; i < Levels.Length; i++)
                if (Math.Abs(Levels[i] - share) < Math.Abs(Levels[best] - share)) best = i;
            return best;
        }

        public static List<Stone> Scatter(GroundCanvas c)
        {
            var stones = new List<Stone>();
            if (c.Stones == null || c.Stones.Length < c.Width * c.Height) return stones;
            for (int ty = 0; ty < c.Height; ty++)
                for (int tx = 0; tx < c.Width; tx++)
                {
                    int si = c.Stones[ty * c.Width + tx];
                    if (si < 0 || si >= c.StoneLooks.Length) continue;
                    StoneLook look = c.StoneLooks[si];
                    if (look.Share <= 0f || look.R + look.G + look.B <= 0f) continue;
                    int ax = c.X0 + tx, ay = c.Y0 + ty;
                    int own = c.KindAt(ax, ay);
                    // Not on water, rock shelves or cliffs: stones lie on ground you can walk.
                    if (own < 0 || c.Looks[own].Layer == GroundLayer.Water || c.Looks[own].Shelf) continue;
                    if (c.Field != null && c.Field.IsSteep(ax, ay, c.CliffStep)) continue;
                    float chance = Math.Min(1f, look.Share) * PerTile / Tries;
                    for (int t = 0; t < Tries; t++)
                    {
                        if (Hash(ax, ay, t * 7 + 3) >= chance) continue;
                        // Mostly small chips, a few larger; three to five corners, unevenly placed,
                        // and wholly inside their own tile.
                        float size = Hash(ax, ay, t * 7 + 5);
                        float radius = 0.08f + 0.15f * size * size;
                        float u = radius + (1f - 2f * radius) * Hash(ax, ay, t * 7 + 1);
                        float v = radius + (1f - 2f * radius) * Hash(ax, ay, t * 7 + 2);
                        float tone = Hash(ax, ay, t * 7 + 4);
                        var stone = new Stone { Tone = tone < 0.45f ? 0 : tone < 0.85f ? 1 : 2, R = look.R, G = look.G, B = look.B };
                        int corners = 3 + (int)(Hash(ax, ay, t * 7 + 6) * 3f);
                        float turn = Hash(ax, ay, t * 7 + 7) * 6.2832f;
                        stone.Outline = new float[corners * 2];
                        for (int k = 0; k < corners; k++)
                        {
                            float a = turn + (k + 0.35f * (Hash(ax + k, ay, t * 13 + 8) - 0.5f)) / corners * 6.2832f;
                            float rr = radius * (0.6f + 0.4f * Hash(ax, ay + k, t * 13 + 9));
                            stone.Outline[k * 2] = ax + u + (float)Math.Cos(a) * rr;
                            stone.Outline[k * 2 + 1] = ay + v + (float)Math.Sin(a) * rr;
                        }
                        stones.Add(stone);
                    }
                }
            return stones;
        }

        /// <summary>
        /// How thickly stones lie along a ground that carries them (the blockout's paths): about
        /// <paramref name="share"/> on the whole, but unevenly, in patches several tiles long, bare
        /// stretches between them, thicker where the ground is wide (a bend, a meeting of paths)
        /// and thinner at its edges. <paramref name="carries"/> says which tiles may have stones
        /// (null: any, and no thickening or thinning by width, as the brush paints).
        /// Returns a level of <see cref="Levels"/> (-1: none).
        /// </summary>
        public static int Spread(int x, int y, float share, Func<int, int, bool>? carries)
        {
            if (carries != null && !carries(x, y)) return -1;
            int near = carries == null ? 6 : 0;
            if (carries != null)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if ((dx != 0 || dy != 0) && carries(x + dx, y + dy)) near++;
            float patch = 0.8f * GroundPainter.Noise(x / 7.5f + 311f, y / 7.5f - 97f) + 0.2f * GroundPainter.Noise(x / 2.7f - 23f, y / 2.7f + 61f);
            float wide = near >= 8 ? 1.35f : near >= 6 ? 1f : 0.6f;
            return Level(share * wide * Math.Max(0f, 0.85f + 1.25f * patch));
        }

        /// <summary>Draws stones into a painted picture (rows from the south, <paramref name="ppt"/> pixels a tile from (x0, y0)).</summary>
        public static void Draw(byte[] rgba, int width, int height, int ppt, int x0, int y0, IEnumerable<Stone> stones)
        {
            foreach (Stone s in stones)
            {
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                for (int k = 0; k < s.Outline.Length; k += 2)
                {
                    minX = Math.Min(minX, s.Outline[k]); maxX = Math.Max(maxX, s.Outline[k]);
                    minY = Math.Min(minY, s.Outline[k + 1]); maxY = Math.Max(maxY, s.Outline[k + 1]);
                }
                float t = Tones[s.Tone];
                byte r = Byte(s.R * t), g = Byte(s.G * t), b = Byte(s.B * t);
                for (int py = Math.Max(0, (int)((minY - y0) * ppt)); py <= Math.Min(height - 1, (int)((maxY - y0) * ppt)); py++)
                    for (int px = Math.Max(0, (int)((minX - x0) * ppt)); px <= Math.Min(width - 1, (int)((maxX - x0) * ppt)); px++)
                    {
                        if (!Inside(s.Outline, x0 + (px + 0.5f) / ppt, y0 + (py + 0.5f) / ppt)) continue;
                        int o = (py * width + px) * 4;
                        rgba[o] = r; rgba[o + 1] = g; rgba[o + 2] = b;
                    }
            }
        }

        private static bool Inside(float[] poly, float x, float y)
        {
            bool inside = false;
            int n = poly.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = poly[i * 2], yi = poly[i * 2 + 1], xj = poly[j * 2], yj = poly[j * 2 + 1];
                if (yi > y != yj > y && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        private static float Hash(int x, int y, int salt)
        {
            uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)salt * 0xCB1AB31Fu;
            h ^= h >> 13; h *= 0x2C1B3C6Du; h ^= h >> 15; h *= 0x297A2D39u; h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static byte Byte(float v) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v * 255f)));
    }
}
