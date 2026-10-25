using System;
using System.Collections.Generic;

namespace Rth.Shared.World.Ground
{
    /// <summary>
    /// Where a painted ground meets its water (docs/world-plan.md section 7): which tile corners touch
    /// water, how far each is from dry land, and how much water lies round it (a blur of the water's
    /// tiles). The ground is drawn dipping under the water by that blur, and the water as one flat
    /// surface above it, so the waterline is wherever the bank crosses the surface: the blur's
    /// outline rather than the tiles', so it runs straight along a diagonal and rounds the corners,
    /// at whatever height the bank has, never a step or a tile's point. Walking stays tile by tile; this is only how it is drawn. Corners are on
    /// the canvas's grid (the region and its margin): (0, 0) is the canvas's south-west corner.
    /// </summary>
    public sealed class Shore
    {
        /// <summary>How far the ground is drawn below its height under open water (tiles).</summary>
        public const float FullDip = 0.7f;
        /// <summary>The blur's reach: tiles whose middles lie this near a corner count towards it.</summary>
        public const float Reach = 2f;

        private readonly int[] _kinds;
        private readonly bool[] _water;
        private readonly int _cols, _rows;
        private readonly bool[] _wet;
        private readonly int[] _distance;
        private readonly float[] _wetness;

        /// <summary>Corners a side: one more than the canvas's tiles.</summary>
        public int CornerCols => _cols + 1;
        public int CornerRows => _rows + 1;

        private Shore(int[] kinds, int cols, int rows, bool[] water)
        {
            _kinds = kinds;
            _cols = cols;
            _rows = rows;
            _water = water;
            int cc = cols + 1, cr = rows + 1;
            _wet = new bool[cc * cr];
            _distance = new int[cc * cr];
            _wetness = new float[cc * cr];
            var queue = new Queue<int>();
            for (int y = 0; y < cr; y++)
                for (int x = 0; x < cc; x++)
                {
                    bool wet = false, dry = false;
                    for (int oy = -1; oy <= 0; oy++)
                        for (int ox = -1; ox <= 0; ox++)
                        {
                            int k = Kind(x + ox, y + oy);
                            if (k < 0) continue;
                            if (k < water.Length && water[k]) wet = true;
                            else dry = true;
                        }
                    int i = y * cc + x;
                    _wet[i] = wet;
                    // How much of the ground round the corner is water, nearer tiles counting more.
                    float soaked = 0f, all = 0f;
                    for (int ty = y - 2; ty < y + 2; ty++)
                        for (int tx = x - 2; tx < x + 2; tx++)
                        {
                            int k = Kind(tx, ty);
                            if (k < 0) continue;
                            float dx = tx + 0.5f - x, dy = ty + 0.5f - y;
                            float weight = Reach - (float)Math.Sqrt(dx * dx + dy * dy);
                            if (weight <= 0f) continue;
                            all += weight;
                            if (k < water.Length && water[k]) soaked += weight;
                        }
                    _wetness[i] = all > 0f ? soaked / all : 0f;
                    _distance[i] = wet && dry ? 0 : int.MaxValue;
                    if (wet && dry) queue.Enqueue(i);
                }
            // Out from the waterline, through corners under water.
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % cc, y = i / cc;
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = x + ox, ny = y + oy;
                        if (nx < 0 || ny < 0 || nx >= cc || ny >= cr) continue;
                        int n = ny * cc + nx;
                        if (!_wet[n] || _distance[n] <= _distance[i] + 1) continue;
                        _distance[n] = _distance[i] + 1;
                        queue.Enqueue(n);
                    }
            }
        }

        public static Shore Of(GroundCanvas canvas, bool[] water) => new Shore(canvas.Kinds, canvas.Cols, canvas.Rows, water);

        /// <summary>For tests and tools: a grid of kinds (row by row from the south) and which kinds are water.</summary>
        public static Shore Of(int[] kinds, int cols, int rows, bool[] water) => new Shore(kinds, cols, rows, water);

        private int Kind(int tx, int ty) => tx < 0 || ty < 0 || tx >= _cols || ty >= _rows ? -1 : _kinds[ty * _cols + tx];

        private int Index(int x, int y) => Math.Clamp(y, 0, _rows) * (_cols + 1) + Math.Clamp(x, 0, _cols);

        /// <summary>Whether the corner touches water.</summary>
        public bool Wet(int x, int y) => _wet[Index(x, y)];

        /// <summary>Corners from dry land: 0 on the waterline; large where no land is near.</summary>
        public int Distance(int x, int y) => _distance[Index(x, y)];

        /// <summary>How much of the ground round the corner is water: 0 inland, 1 out in open water, about a half on a straight shore.</summary>
        public float Wetness(int x, int y) => _wetness[Index(x, y)];

        /// <summary>
        /// How far the ground is drawn below its height here: by the wetness, steeply, so it is
        /// barely lowered on land and reaches a little over the water's own depth (0.15) at a half:
        /// on a level shore the waterline runs where the blur is a half.
        /// </summary>
        public float Dip(int x, int y) => FullDip * (float)Math.Pow(_wetness[Index(x, y)], 2.2);

        /// <summary>How shallow the water is over the corner: 1 at and beyond the waterline, 0 a few tiles out.</summary>
        public float Shallow(int x, int y)
        {
            int i = Index(x, y);
            if (!_wet[i]) return 1f;
            int d = _distance[i];
            return d == 0 ? 1f : d == 1 ? 0.55f : d == 2 ? 0.25f : 0f;
        }

        /// <summary>A water kind on a tile touching the corner (the lowest-numbered), or -1.</summary>
        public int WaterAround(int x, int y)
        {
            int found = -1;
            for (int oy = -1; oy <= 0; oy++)
                for (int ox = -1; ox <= 0; ox++)
                {
                    int k = Kind(x + ox, y + oy);
                    if (k >= 0 && k < _water.Length && _water[k] && (found < 0 || k < found)) found = k;
                }
            return found;
        }
    }
}
