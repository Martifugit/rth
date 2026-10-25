using System;
using System.Collections.Generic;

namespace Rth.Shared.World.Layout
{
    /// <summary>
    /// Finds where a road goes between two stops (docs/world-plan.md section 7): the cheapest way
    /// over the land, as roads are made. Steep ground costs more the steeper it is and cliffs can't
    /// be crossed; lakes and the sea can't be crossed (that is a ferry); a river can, at a price, so
    /// roads bridge it where it is narrow; ground that already carries a road is cheap, so roads
    /// join into a network instead of running side by side. The tile path is then pulled straight
    /// where the land allows (an empire's highway runs straighter than a track) and its corners
    /// rounded, so a road curves instead of zigzagging.
    /// </summary>
    public sealed class RoadRouter
    {
        private readonly Landscape _land;
        private readonly int W, H, X0, Y0;
        private readonly float[] _slope;
        private readonly float[] _g;
        private readonly int[] _from, _stamp;
        private int _generation;

        public RoadRouter(Landscape land)
        {
            _land = land;
            W = land.W; H = land.H; X0 = land.X0; Y0 = land.Y0;
            _slope = new float[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float a = land.Heights.CornerAt(x, y), b = land.Heights.CornerAt(x + 1, y), c = land.Heights.CornerAt(x, y + 1), d = land.Heights.CornerAt(x + 1, y + 1);
                    _slope[y * W + x] = Math.Max(Math.Max(a, b), Math.Max(c, d)) - Math.Min(Math.Min(a, b), Math.Min(c, d));
                }
            _g = new float[W * H];
            _from = new int[W * H];
            _stamp = new int[W * H];
        }

        private float Cost(int i) => _land.Cost(i, _slope[i]);

        /// <summary>The road from (ax, ay) to (bx, by) as a line in tiles, or empty when there is no way.</summary>
        public List<float[]> Find(float ax, float ay, float bx, float by, string kind)
        {
            int start = Passable((int)Math.Floor(ax), (int)Math.Floor(ay)), goal = Passable((int)Math.Floor(bx), (int)Math.Floor(by));
            if (start < 0 || goal < 0) return new List<float[]>();
            List<int> tiles = Search(start, goal, ax, ay, bx, by);
            if (tiles.Count == 0) return new List<float[]>();

            var pts = new List<float[]>(tiles.Count + 2) { new[] { ax, ay } };
            foreach (int i in tiles) pts.Add(new[] { X0 + i % W + 0.5f, Y0 + i / W + 0.5f });
            pts.Add(new[] { bx, by });
            float tolerance = kind == "main" ? 2.6f : kind == "road" ? 1.6f : 1.1f;
            List<float[]> simple = Simplify(pts, tolerance);
            return Round(simple, kind == "main" ? 1 : 2);
        }

        /// <summary>The nearest tile a road may use, within a few tiles (a stop may sit on a square's edge or by water).</summary>
        private int Passable(int x, int y)
        {
            for (int r = 0; r <= 6; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int tx = x + dx - X0, ty = y + dy - Y0;
                        if (tx < 0 || ty < 0 || tx >= W || ty >= H) continue;
                        if (Cost(ty * W + tx) >= 0f) return ty * W + tx;
                    }
            return -1;
        }

        private List<int> Search(int start, int goal, float ax, float ay, float bx, float by)
        {
            _generation++;
            float dist = (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
            float pad = Math.Max(48f, dist * 0.4f);
            int minX = Math.Max(0, (int)(Math.Min(ax, bx) - pad) - X0), maxX = Math.Min(W - 1, (int)(Math.Max(ax, bx) + pad) - X0);
            int minY = Math.Max(0, (int)(Math.Min(ay, by) - pad) - Y0), maxY = Math.Min(H - 1, (int)(Math.Max(ay, by) + pad) - Y0);
            int gx = goal % W, gy = goal / W;
            var open = new Heap();
            _g[start] = 0f;
            _from[start] = -1;
            _stamp[start] = _generation;
            open.Push(start, Heuristic(start % W, start / W, gx, gy));
            var closed = new HashSet<int>();
            while (open.Count > 0)
            {
                int i = open.Pop();
                if (i == goal) break;
                if (!closed.Add(i)) continue;
                int x = i % W, y = i / W;
                float ci = Cost(i);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < minX || ny < minY || nx > maxX || ny > maxY) continue;
                        int j = ny * W + nx;
                        float cj = Cost(j);
                        if (cj < 0f) continue;
                        // No cutting a corner past ground a road can't use.
                        if (dx != 0 && dy != 0 && (Cost(y * W + nx) < 0f || Cost(ny * W + x) < 0f)) continue;
                        float step = (dx != 0 && dy != 0 ? 1.41421f : 1f) * (ci + cj) * 0.5f;
                        float g = _g[i] + step;
                        if (_stamp[j] == _generation && g >= _g[j]) continue;
                        _stamp[j] = _generation;
                        _g[j] = g;
                        _from[j] = i;
                        open.Push(j, g + Heuristic(nx, ny, gx, gy));
                    }
            }
            if (_stamp[goal] != _generation) return new List<int>();
            var path = new List<int>();
            for (int i = goal; i >= 0; i = _from[i]) path.Add(i);
            path.Reverse();
            return path;
        }

        /// <summary>Straight-line distance, a little under the cheapest cost (a road already there), so the search stays near the best route.</summary>
        private static float Heuristic(int x, int y, int gx, int gy) => 0.8f * (float)Math.Sqrt((x - gx) * (x - gx) + (y - gy) * (y - gy));

        /// <summary>
        /// Douglas-Peucker, keeping a stretch straight only where the straight line stays on ground a
        /// road can use and costs little more than the winding one.
        /// </summary>
        private List<float[]> Simplify(List<float[]> pts, float tolerance)
        {
            var keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
            var stack = new Stack<(int a, int b)>();
            stack.Push((0, pts.Count - 1));
            while (stack.Count > 0)
            {
                (int a, int b) = stack.Pop();
                if (b - a < 2) continue;
                float worst = -1f;
                int at = -1;
                for (int k = a + 1; k < b; k++)
                {
                    float d = Geometry.DistanceToSegment(pts[a][0], pts[a][1], pts[b][0], pts[b][1], pts[k][0], pts[k][1]);
                    if (d > worst) { worst = d; at = k; }
                }
                if (worst > tolerance || !Fits(pts, a, b))
                {
                    keep[at] = true;
                    stack.Push((a, at));
                    stack.Push((at, b));
                }
            }
            var result = new List<float[]>();
            for (int k = 0; k < pts.Count; k++) if (keep[k]) result.Add(pts[k]);
            return result;
        }

        /// <summary>Whether the straight line from point a to point b stays on usable ground and costs no more than 15% over the path it replaces.</summary>
        private bool Fits(List<float[]> pts, int a, int b)
        {
            float pathCost = 0f;
            for (int k = a + 1; k <= b; k++)
            {
                int i = TileAt(pts[k][0], pts[k][1]);
                float c = i < 0 ? 1f : Math.Max(0f, Cost(i));
                pathCost += c * (float)Math.Sqrt((pts[k][0] - pts[k - 1][0]) * (pts[k][0] - pts[k - 1][0]) + (pts[k][1] - pts[k - 1][1]) * (pts[k][1] - pts[k - 1][1]));
            }
            float len = (float)Math.Sqrt((pts[b][0] - pts[a][0]) * (pts[b][0] - pts[a][0]) + (pts[b][1] - pts[a][1]) * (pts[b][1] - pts[a][1]));
            int n = Math.Max(1, (int)Math.Ceiling(len * 2f));
            float lineCost = 0f;
            for (int s = 0; s <= n; s++)
            {
                float t = s / (float)n;
                // Check a tile either side too, so the road's width stays on usable ground.
                float x = pts[a][0] + (pts[b][0] - pts[a][0]) * t, y = pts[a][1] + (pts[b][1] - pts[a][1]) * t;
                int i = TileAt(x, y);
                if (i < 0) return false;
                float c = Cost(i);
                if (c < 0f) return false;
                lineCost += c * len / (n + 1);
            }
            return lineCost <= pathCost * 1.15f + 2f;
        }

        private int TileAt(float x, float y)
        {
            int tx = (int)Math.Floor(x) - X0, ty = (int)Math.Floor(y) - Y0;
            return tx < 0 || ty < 0 || tx >= W || ty >= H ? -1 : ty * W + tx;
        }

        /// <summary>Chaikin's corner cutting, keeping the ends, so bends become curves.</summary>
        private static List<float[]> Round(List<float[]> pts, int passes)
        {
            for (int p = 0; p < passes && pts.Count > 2; p++)
            {
                var next = new List<float[]> { pts[0] };
                for (int k = 0; k + 1 < pts.Count; k++)
                {
                    float[] a = pts[k], b = pts[k + 1];
                    if (k > 0) next.Add(new[] { a[0] * 0.75f + b[0] * 0.25f, a[1] * 0.75f + b[1] * 0.25f });
                    if (k + 2 < pts.Count) next.Add(new[] { a[0] * 0.25f + b[0] * 0.75f, a[1] * 0.25f + b[1] * 0.75f });
                }
                next.Add(pts[pts.Count - 1]);
                pts = next;
            }
            return pts;
        }

        /// <summary>A binary min-heap of tiles by priority.</summary>
        private sealed class Heap
        {
            private readonly List<(float p, int i)> _items = new List<(float, int)>();
            public int Count => _items.Count;

            public void Push(int i, float p)
            {
                _items.Add((p, i));
                int k = _items.Count - 1;
                while (k > 0)
                {
                    int parent = (k - 1) / 2;
                    if (_items[parent].p <= _items[k].p) break;
                    (_items[parent], _items[k]) = (_items[k], _items[parent]);
                    k = parent;
                }
            }

            public int Pop()
            {
                int top = _items[0].i;
                int last = _items.Count - 1;
                _items[0] = _items[last];
                _items.RemoveAt(last);
                int k = 0;
                while (true)
                {
                    int l = 2 * k + 1, r = l + 1, m = k;
                    if (l < _items.Count && _items[l].p < _items[m].p) m = l;
                    if (r < _items.Count && _items[r].p < _items[m].p) m = r;
                    if (m == k) break;
                    (_items[m], _items[k]) = (_items[k], _items[m]);
                    k = m;
                }
                return top;
            }
        }
    }
}
