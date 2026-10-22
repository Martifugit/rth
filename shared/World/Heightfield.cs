using System;

namespace Rth.Shared.World
{
    /// <summary>
    /// Ground heights over a rectangle of tiles, one height per tile corner, in tiles (1 = one
    /// tile up). Tile (x, y) covers [x, x+1] × [y, y+1] and is drawn as two triangles split along
    /// its south-west to north-east diagonal; <see cref="Sample"/> follows the same split, so
    /// whatever stands on the ground meets the drawn surface exactly. Coordinates are absolute
    /// tiles. Presentation only: the server works on the tile grid and never reads heights, but
    /// the map exporter blocks tiles too steep to walk (<see cref="IsSteep"/>).
    /// </summary>
    public sealed class Heightfield
    {
        private readonly float[] _corners;

        public int OriginX { get; }
        public int OriginY { get; }
        public int Width { get; }
        public int Depth { get; }

        public Heightfield(int originX, int originY, int width, int depth, float[]? corners = null)
        {
            if (width <= 0 || depth <= 0) throw new ArgumentException("A heightfield must have a positive size.");
            OriginX = originX;
            OriginY = originY;
            Width = width;
            Depth = depth;
            _corners = new float[(width + 1) * (depth + 1)];
            if (corners != null)
            {
                if (corners.Length != _corners.Length)
                    throw new ArgumentException($"Expected {_corners.Length} corner heights for {width} x {depth} tiles, got {corners.Length}.");
                Array.Copy(corners, _corners, corners.Length);
            }
        }

        /// <summary>The corner heights, row by row from the south (for saving).</summary>
        public float[] Corners => (float[])_corners.Clone();

        public bool ContainsTile(int x, int y) => x >= OriginX && x < OriginX + Width && y >= OriginY && y < OriginY + Depth;

        public bool Contains(float x, float z) => x >= OriginX && x <= OriginX + Width && z >= OriginY && z <= OriginY + Depth;

        /// <summary>Height at an absolute corner; corners outside the field take the nearest edge's.</summary>
        public float Corner(int cx, int cy)
        {
            int i = Math.Clamp(cx - OriginX, 0, Width);
            int j = Math.Clamp(cy - OriginY, 0, Depth);
            return _corners[j * (Width + 1) + i];
        }

        public void SetCorner(int cx, int cy, float height)
        {
            int i = cx - OriginX;
            int j = cy - OriginY;
            if (i < 0 || i > Width || j < 0 || j > Depth) return;
            _corners[j * (Width + 1) + i] = height;
        }

        /// <summary>Sets every corner from a function of its absolute position.</summary>
        public void Fill(Func<int, int, float> height)
        {
            for (int j = 0; j <= Depth; j++)
                for (int i = 0; i <= Width; i++)
                    _corners[j * (Width + 1) + i] = height(OriginX + i, OriginY + j);
        }

        /// <summary>The ground's height at a point, on the same two triangles per tile the client draws.</summary>
        public float Sample(float x, float z)
        {
            float lx = Math.Clamp(x - OriginX, 0f, Width);
            float lz = Math.Clamp(z - OriginY, 0f, Depth);
            int i = Math.Min((int)Math.Floor(lx), Width - 1);
            int j = Math.Min((int)Math.Floor(lz), Depth - 1);
            float u = lx - i;
            float v = lz - j;
            int cx = OriginX + i;
            int cy = OriginY + j;
            float sw = Corner(cx, cy), se = Corner(cx + 1, cy), nw = Corner(cx, cy + 1), ne = Corner(cx + 1, cy + 1);
            // Below the diagonal (u >= v): the south-east triangle sw, se, ne; above it: sw, ne, nw.
            return u >= v
                ? sw + (se - sw) * u + (ne - se) * v
                : sw + (ne - nw) * u + (nw - sw) * v;
        }

        /// <summary>Height at the centre of a tile.</summary>
        public float TileCentre(int x, int y) => Sample(x + 0.5f, y + 0.5f);

        /// <summary>Highest corner minus lowest corner of a tile.</summary>
        public float Rise(int x, int y)
        {
            float a = Corner(x, y), b = Corner(x + 1, y), c = Corner(x, y + 1), d = Corner(x + 1, y + 1);
            return Math.Max(Math.Max(a, b), Math.Max(c, d)) - Math.Min(Math.Min(a, b), Math.Min(c, d));
        }

        /// <summary>A cliff: the tile rises more than <paramref name="cliffStep"/> tiles across, too steep to stand on.</summary>
        public bool IsSteep(int x, int y, float cliffStep) => Rise(x, y) > cliffStep;

        /// <summary>
        /// Soft rolling ground: three octaves of smoothed value noise mapped to
        /// [<paramref name="min"/>, <paramref name="max"/>] tiles (the 2004 game's default terrain is 0.6 to 3.75).
        /// The same position and seed always give the same height.
        /// </summary>
        public static float Noise(int x, int y, int seed, float min = 0.6f, float max = 3.75f)
        {
            float n = Octave(x, y, seed, 32f) * 0.6f + Octave(x, y, seed + 1, 12f) * 0.3f + Octave(x, y, seed + 2, 5f) * 0.1f;
            return min + (max - min) * n;
        }

        private static float Octave(int x, int y, int seed, float scale)
        {
            float fx = x / scale;
            float fy = y / scale;
            int x0 = (int)Math.Floor(fx);
            int y0 = (int)Math.Floor(fy);
            float u = Smooth(fx - x0);
            float v = Smooth(fy - y0);
            float a = Lattice(x0, y0, seed), b = Lattice(x0 + 1, y0, seed), c = Lattice(x0, y0 + 1, seed), d = Lattice(x0 + 1, y0 + 1, seed);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static float Lattice(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
