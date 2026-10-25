using System;
using System.Collections.Generic;
using System.Linq;

namespace Rth.Shared.World.Layout
{
    /// <summary>A routed road or a winding river, as a line in tiles.</summary>
    public sealed class LandLine
    {
        public string Id = "";
        /// <summary>main, road, track (roads) or river.</summary>
        public string Kind = "";
        public List<float[]> Points = new List<float[]>();
        /// <summary>Rivers: half the width at each point.</summary>
        public List<float> HalfWidths = new List<float>();
    }

    /// <summary>
    /// The whole continent at tile scale (docs/world-plan.md): what each tile is and how high each
    /// corner stands, built from the layout in passes, the way a place is made by hand:
    /// <list type="number">
    /// <item>ground from the zone grid, its borders wandered so nothing steps by whole zones;</item>
    /// <item>rivers wound along their lines, widening downstream; passes cut through the crest;</item>
    /// <item>heights: each ground's height smoothed into the next, the land's rolling, the landforms
    /// (<see cref="Relief"/>), the crest's peaks, the fall to the sea; lakes held flat at their level;
    /// rivers falling steadily to their mouths in valleys of their own; passes climbing to saddles;</item>
    /// <item>roads routed over that land between their stops (<see cref="RoadRouter"/>), then
    /// levelled; settlements flattened into platforms; the land meeting the built regions;</item>
    /// <item>the painted detail: patches of other grounds, scree on steep slopes, snow, beaches and
    /// banks, fields round the villages, worn ground round the settlements, roads of each power's
    /// own making, squares, and the marks of landmarks and sites.</item>
    /// </list>
    /// Scaffolding: a region taken over by hand keeps what it was given and is built on from there.
    /// </summary>
    public sealed class Landscape
    {
        /// <summary>Characters for land beyond the first build, one per ground found there (its twin: drawn hazed, blocked).</summary>
        public const string BeyondChars = "0123456789&$@?<>()[]{}^|`'%";

        public readonly ContinentMap Map;
        public readonly int X0, Y0, W, H;
        /// <summary>Each tile's ground (an index into the map's grounds), row by row from the south.</summary>
        public readonly byte[] Ground;
        /// <summary>Each tile's power (<see cref="ContinentMap.NoPower"/> on the sea).</summary>
        public readonly byte[] Power;
        /// <summary>Land beyond the first build: drawn, but blocked.</summary>
        public readonly bool[] Beyond;
        public LandHeights Heights = null!;
        public readonly List<LandLine> Roads = new List<LandLine>();
        public readonly List<LandLine> Rivers = new List<LandLine>();
        public readonly List<string> Warnings = new List<string>();

        private ContinentData Data => Map.Data;
        private readonly Dictionary<string, byte> _g = new Dictionary<string, byte>();
        private readonly HashSet<byte> _water = new HashSet<byte>();
        private byte _sea, _lake, _river, _bridge, _pass;
        private float[] _levels = Array.Empty<float>(); // water level per corner (NaN: not water)

        private Landscape(ContinentMap map)
        {
            Map = map;
            X0 = map.OriginX;
            Y0 = map.OriginY;
            W = map.Cols * map.ZoneTiles;
            H = map.Rows * map.ZoneTiles;
            Ground = new byte[W * H];
            Power = new byte[W * H];
            Beyond = new bool[W * H];
            for (int k = 0; k < map.GroundIds.Count; k++)
            {
                _g[map.GroundIds[k]] = (byte)k;
                if (map.GroundDefs[k].Water) _water.Add((byte)k);
            }
        }

        public int Index(int x, int y) => (y - Y0) * W + (x - X0);
        public bool InBounds(int x, int y) => x >= X0 && y >= Y0 && x < X0 + W && y < Y0 + H;
        public bool IsWater(int i) => _water.Contains(Ground[i]);
        public string GroundId(int i) => Map.GroundIds[Ground[i]];

        /// <summary>The character a tile is drawn with: its ground's letter, or for land beyond the first build its ground's twin.</summary>
        public char Char(int i) => Beyond[i] && !IsWater(i) && _twins.TryGetValue(Ground[i], out char c) ? c : Map.GroundDefs[Ground[i]].Letter[0];

        /// <summary>What a character stands for: a ground's id, and whether it is that ground's twin beyond the first build.</summary>
        public (string ground, bool beyond) Decode(char c)
        {
            foreach (KeyValuePair<byte, char> t in _twins) if (t.Value == c) return (Map.GroundIds[t.Key], true);
            KeyValuePair<string, GroundDef> g = Data.Grounds.First(e => e.Value.Letter[0] == c);
            return (g.Key, false);
        }

        private readonly Dictionary<byte, char> _twins = new Dictionary<byte, char>();

        /// <summary>Gives each ground found beyond the first build its twin character, in ground order, so every region agrees.</summary>
        private void Twins()
        {
            var found = new SortedSet<byte>();
            for (int i = 0; i < Ground.Length; i++) if (Beyond[i] && !IsWater(i)) found.Add(Ground[i]);
            int k = 0;
            foreach (byte g in found)
            {
                if (k >= BeyondChars.Length) { Warnings.Add($"too many grounds beyond the first build; {Map.GroundIds[g]} is drawn as itself"); continue; }
                _twins[g] = BeyondChars[k++];
            }
        }

        private byte G(string? id, byte fallback) => id != null && _g.TryGetValue(id, out byte b) ? b : fallback;

        public static Landscape Build(ContinentMap map, IReadOnlyList<BuiltRegion> built, HeightEdits? edits = null)
        {
            var land = new Landscape(map);
            land.Lay();
            land.Wind();
            land.Raise(built);
            land.Route();
            land.Settle(built);
            land.Heights.Finish(edits);
            land.Paint(built);
            land.Twins();
            return land;
        }

        // ------------------------------------------------------------------ 1. ground

        private void Lay()
        {
            _sea = G(Data.SeaGround, 0);
            _lake = G("lake", _sea);
            _river = G(Data.RiverGround, _sea);
            _bridge = G(Data.BridgeGround, 0);
            _pass = G(Data.PassGround, 0);

            // The layout's shapes drawn tile by tile (the zone grid is only for the checks and the
            // world map): land and sea, who holds it, then each power's ground and the areas over it.
            int n = W * H;
            var land = new bool[n];
            var ship = new bool[n];
            foreach (ShapeEntry e in Data.Land)
                Rasterize(e, i => { land[i] = true; ship[i] = e.By == "ship"; });
            foreach (ShapeEntry e in Data.Seas) Rasterize(e, i => land[i] = false);
            const byte unclaimed = 254;
            var power = new byte[n];
            for (int i = 0; i < n; i++) power[i] = land[i] ? unclaimed : ContinentMap.NoPower;
            foreach (ShapeEntry e in Data.Realms)
            {
                byte p = (byte)Map.PowerIds.IndexOf(e.Power!);
                Rasterize(e, i => { if (land[i]) power[i] = p; });
            }
            ClaimGaps(power, unclaimed, (byte)Map.PowerIds.IndexOf(Data.DefaultPower));
            var ground = new byte[n];
            for (int i = 0; i < n; i++)
                ground[i] = power[i] == ContinentMap.NoPower ? _sea : G(Data.Powers[Map.PowerIds[power[i]]].Ground, _sea);
            foreach (ShapeEntry e in Data.Areas)
            {
                byte g = G(e.Ground, _sea);
                Rasterize(e, i => { if (land[i]) ground[i] = g; });
            }

            // Every border wanders a little, tile by tile, so nothing is ruled.
            for (int ty = 0; ty < H; ty++)
                for (int tx = 0; tx < W; tx++)
                {
                    float x = X0 + tx + 0.5f, y = Y0 + ty + 0.5f;
                    int sx = (int)Math.Floor(x + 2.5f * LandNoise.Value(Data.Seed, x / 7f, y / 7f)) - X0;
                    int sy = (int)Math.Floor(y + 2.5f * LandNoise.Value(Data.Seed + 101, x / 7f, y / 7f)) - Y0;
                    sx = Math.Max(0, Math.Min(W - 1, sx));
                    sy = Math.Max(0, Math.Min(H - 1, sy));
                    int i = ty * W + tx, j = sy * W + sx;
                    Ground[i] = ground[j];
                    Power[i] = power[j];
                    Beyond[i] = power[j] != ContinentMap.NoPower && (!Data.Powers[Map.PowerIds[power[j]]].Core || ship[j]);
                }
            for (int pass = 0; pass < 2; pass++) Shores();
        }

        /// <summary>Land no realm claims (slivers between rough borders) takes its nearest claimed neighbour's power.</summary>
        private void ClaimGaps(byte[] power, byte unclaimed, byte fallback)
        {
            var queue = new Queue<int>();
            for (int i = 0; i < power.Length; i++)
                if (power[i] != unclaimed && power[i] != ContinentMap.NoPower) queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int tx = i % W, ty = i / W;
                foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int x = tx + dx, y = ty + dy;
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    int j = y * W + x;
                    if (power[j] != unclaimed) continue;
                    power[j] = power[i];
                    queue.Enqueue(j);
                }
            }
            for (int i = 0; i < power.Length; i++) if (power[i] == unclaimed) power[i] = fallback;
        }

        /// <summary>
        /// Calls back for every tile whose centre a shape covers: polygons by scanline (their edges
        /// roughened exactly as the zone grid's are), bands and ellipses by distance.
        /// </summary>
        private void Rasterize(Shape e, Action<int> at)
        {
            float minY = e.MinY ?? float.MinValue, maxY = e.MaxY ?? float.MaxValue;
            if (e.Poly != null)
            {
                List<float[]> poly = e.Rough > 0 ? Geometry.Roughen(e.Poly, e.Rough, Data.Seed) : e.Poly;
                float top = poly.Max(p => p[1]), bottom = poly.Min(p => p[1]);
                var xs = new List<float>();
                for (int ty = Math.Max(0, (int)Math.Floor(bottom) - Y0); ty <= Math.Min(H - 1, (int)Math.Ceiling(top) - Y0); ty++)
                {
                    float y = Y0 + ty + 0.5f;
                    if (y < minY || y > maxY) continue;
                    xs.Clear();
                    for (int k = 0, j = poly.Count - 1; k < poly.Count; j = k++)
                    {
                        float yi = poly[k][1], yj = poly[j][1];
                        if ((yi > y) != (yj > y)) xs.Add(poly[k][0] + (y - yi) * (poly[j][0] - poly[k][0]) / (yj - yi));
                    }
                    xs.Sort();
                    for (int k = 0; k + 1 < xs.Count; k += 2)
                    {
                        int a = Math.Max(0, (int)Math.Ceiling(xs[k] - 0.5f) - X0), b = Math.Min(W - 1, (int)Math.Floor(xs[k + 1] - 0.5f) - X0);
                        for (int tx = a; tx <= b; tx++) at(ty * W + tx);
                    }
                }
                return;
            }
            if (e.Line != null || e.Along != null)
            {
                List<float[]> line = e.Line ?? Data.Spine.Line;
                float hw = e.HalfWidth;
                int x0 = Math.Max(0, (int)Math.Floor(line.Min(p => p[0]) - hw) - X0), x1 = Math.Min(W - 1, (int)Math.Ceiling(line.Max(p => p[0]) + hw) - X0);
                int y0 = Math.Max(0, (int)Math.Floor(line.Min(p => p[1]) - hw) - Y0), y1 = Math.Min(H - 1, (int)Math.Ceiling(line.Max(p => p[1]) + hw) - Y0);
                for (int ty = y0; ty <= y1; ty++)
                {
                    float y = Y0 + ty + 0.5f;
                    if (y < minY || y > maxY) continue;
                    for (int tx = x0; tx <= x1; tx++)
                        if (Geometry.DistanceToLine(line, X0 + tx + 0.5f, y) <= hw) at(ty * W + tx);
                }
                return;
            }
            if (e.Ellipse != null)
            {
                float[] el = e.Ellipse;
                int x0 = Math.Max(0, (int)Math.Floor(el[0] - el[2]) - X0), x1 = Math.Min(W - 1, (int)Math.Ceiling(el[0] + el[2]) - X0);
                int y0 = Math.Max(0, (int)Math.Floor(el[1] - el[3]) - Y0), y1 = Math.Min(H - 1, (int)Math.Ceiling(el[1] + el[3]) - Y0);
                for (int ty = y0; ty <= y1; ty++)
                {
                    float y = Y0 + ty + 0.5f;
                    if (y < minY || y > maxY) continue;
                    for (int tx = x0; tx <= x1; tx++)
                    {
                        float dx = (X0 + tx + 0.5f - el[0]) / el[2], dy = (y - el[1]) / el[3];
                        if (dx * dx + dy * dy <= 1f) at(ty * W + tx);
                    }
                }
            }
        }

        /// <summary>
        /// Smooths the shores of the sea and the lakes: a tile of water mostly ringed by land becomes
        /// land, and a tile of land mostly ringed by water becomes water, so shores run in curves and
        /// diagonals (which the painted shapes then draw cleanly) rather than in one-tile jags.
        /// </summary>
        private void Shores()
        {
            var next = (byte[])Ground.Clone();
            var nextPower = (byte[])Power.Clone();
            var nextBeyond = (bool[])Beyond.Clone();
            for (int ty = 1; ty < H - 1; ty++)
                for (int tx = 1; tx < W - 1; tx++)
                {
                    int i = ty * W + tx;
                    byte g = Ground[i];
                    bool water = g == _sea || g == _lake;
                    int same = 0, landAt = -1;
                    byte wet = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int j = i + dy * W + dx;
                            byte n = Ground[j];
                            bool nw = n == _sea || n == _lake;
                            if (water ? n == g : nw) { same++; wet = n; }
                            if (!nw && landAt < 0) landAt = j;
                        }
                    if (water && same <= 2 && landAt >= 0)
                    {
                        next[i] = Ground[landAt];
                        nextPower[i] = Power[landAt];
                        nextBeyond[i] = Beyond[landAt];
                    }
                    else if (!water && !IsWater(i) && same >= 6)
                    {
                        next[i] = wet;
                        if (wet == _sea) { nextPower[i] = ContinentMap.NoPower; nextBeyond[i] = false; }
                    }
                }
            Array.Copy(next, Ground, Ground.Length);
            Array.Copy(nextPower, Power, Power.Length);
            Array.Copy(nextBeyond, Beyond, Beyond.Length);
        }

        // ------------------------------------------------------------------ 2. rivers and passes

        /// <summary>River tiles: which river, and how far along it (for its level).</summary>
        private int[] _riverOf = Array.Empty<int>();
        private float[] _riverAt = Array.Empty<float>();

        private void Wind()
        {
            _riverOf = new int[W * H];
            _riverAt = new float[W * H];
            for (int i = 0; i < _riverOf.Length; i++) _riverOf[i] = -1;
            var nearest = new float[W * H];
            for (int i = 0; i < nearest.Length; i++) nearest[i] = float.MaxValue;
            for (int r = 0; r < Data.Rivers.Count; r++)
            {
                RiverDef def = Data.Rivers[r];
                LandLine line = Meander(def, r);
                Rivers.Add(line);
                Stamp(line, (x, y, d, s, hw) =>
                {
                    if (d > hw) return;
                    int i = Index(x, y);
                    if (Ground[i] == _sea || Ground[i] == _lake || Beyond[i]) return;
                    if (d >= nearest[i]) return;
                    nearest[i] = d;
                    Ground[i] = _river;
                    _riverOf[i] = r;
                    _riverAt[i] = s;
                });
            }
            foreach (PassDef p in Passes)
                foreach ((int x, int y) in Data_Along(p.Line, p.HalfWidth))
                {
                    int i = Index(x, y);
                    if (Beyond[i]) continue;
                    Ground[i] = IsWater(i) ? _bridge : _pass;
                }
        }

        private List<PassDef>? _passes;

        /// <summary>
        /// The passes as they run on the ground: wound either side of the layout's line, but through
        /// its ends and its saddle (the middle point), so a pass winds up between the peaks.
        /// </summary>
        public List<PassDef> Passes => _passes ??= Data.Spine.Passes.Select((p, n) =>
        {
            var line = new LandLine { Id = p.Id };
            foreach (float[] q in p.Line) line.Points.Add(q);
            float total = 0f;
            for (int k = 1; k < p.Line.Count; k++) total += Dist(p.Line[k - 1], p.Line[k]);
            var wound = new List<float[]>();
            int steps = Math.Max(2, (int)(total / 3f));
            for (int j = 0; j <= steps; j++)
            {
                float s = total * j / steps;
                (float x, float y) = PointAt(line, s);
                (float ax, float ay) = PointAt(line, Math.Max(0f, s - 1f));
                (float bx, float by) = PointAt(line, Math.Min(total, s + 1f));
                float dx = bx - ax, dy = by - ay, len = (float)Math.Sqrt(dx * dx + dy * dy);
                // Two bends either side of the saddle, each pass its own way.
                float sign = (Geometry.Hash(Data.Seed, p.Id) & 1) == 0 ? 1f : -1f;
                float wind = sign * 11f * (float)Math.Sin(2 * Math.PI * s / total) * (0.75f + 0.25f * LandNoise.Value(Data.Seed + 500 + n, s / 30f, 0.5f));
                if (len > 0) { x += -dy / len * wind; y += dx / len * wind; }
                wound.Add(new[] { x, y });
            }
            // Keep the layout's middle point exactly (the saddle), whatever the subdivision.
            wound[wound.Count / 2] = p.Line[p.Line.Count / 2];
            return new PassDef { Id = p.Id, Name = p.Name, HalfWidth = p.HalfWidth + 1f, Line = wound };
        }).ToList();

        private IEnumerable<(int x, int y)> Data_Along(IList<float[]> line, float halfWidth)
        {
            float minX = line.Min(p => p[0]) - halfWidth - 1, maxX = line.Max(p => p[0]) + halfWidth + 1;
            float minY = line.Min(p => p[1]) - halfWidth - 1, maxY = line.Max(p => p[1]) + halfWidth + 1;
            var pts = line.ToList();
            for (int y = (int)Math.Floor(minY); y <= (int)Math.Ceiling(maxY); y++)
                for (int x = (int)Math.Floor(minX); x <= (int)Math.Ceiling(maxX); x++)
                    if (InBounds(x, y) && Geometry.DistanceToLine(pts, x + 0.5f, y + 0.5f) <= halfWidth) yield return (x, y);
        }

        /// <summary>A river's line wound either side of the layout's, its width growing from source to mouth.</summary>
        private LandLine Meander(RiverDef def, int index)
        {
            var line = new LandLine { Id = def.Id, Kind = "river" };
            var pts = def.Line;
            float total = 0f;
            var lengths = new List<float> { 0f };
            for (int k = 1; k < pts.Count; k++) lengths.Add(total += Dist(pts[k - 1], pts[k]));
            const float step = 3f;
            int n = Math.Max(2, (int)Math.Ceiling(total / step));
            float src = def.SourceHalfWidth ?? def.HalfWidth;
            for (int j = 0; j <= n; j++)
            {
                float s = total * j / n;
                int k = 1;
                while (k < pts.Count - 1 && lengths[k] < s) k++;
                float seg = lengths[k] - lengths[k - 1], t = seg > 0 ? (s - lengths[k - 1]) / seg : 0f;
                float x = pts[k - 1][0] + (pts[k][0] - pts[k - 1][0]) * t, y = pts[k - 1][1] + (pts[k][1] - pts[k - 1][1]) * t;
                float dx = pts[k][0] - pts[k - 1][0], dy = pts[k][1] - pts[k - 1][1];
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                // Winding grows away from the ends, which stay where the layout puts them.
                float ends = Math.Min(1f, Math.Min(s, total - s) / 40f);
                float wind = def.Meander * ends * (LandNoise.Value(Data.Seed + 300 + index, s / 55f, 0.5f) * 0.8f + LandNoise.Value(Data.Seed + 400 + index, s / 17f, 0.5f) * 0.35f);
                if (len > 0) { x += -dy / len * wind; y += dx / len * wind; }
                line.Points.Add(new[] { x, y });
                line.HalfWidths.Add(src + (def.HalfWidth - src) * (s / Math.Max(1f, total)));
            }
            return line;
        }

        /// <summary>Calls back for every tile near a line: its distance, how far along the line, and the half width there.</summary>
        private void Stamp(LandLine line, Action<int, int, float, float, float> at)
        {
            float along = 0f;
            for (int k = 0; k + 1 < line.Points.Count; k++)
            {
                float[] a = line.Points[k], b = line.Points[k + 1];
                float hw = Math.Max(line.HalfWidths.Count > k ? line.HalfWidths[k] : 1f, line.HalfWidths.Count > k + 1 ? line.HalfWidths[k + 1] : 1f);
                float seg = Dist(a, b);
                int x0 = (int)Math.Floor(Math.Min(a[0], b[0]) - hw - 1), x1 = (int)Math.Ceiling(Math.Max(a[0], b[0]) + hw + 1);
                int y0 = (int)Math.Floor(Math.Min(a[1], b[1]) - hw - 1), y1 = (int)Math.Ceiling(Math.Max(a[1], b[1]) + hw + 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        if (!InBounds(x, y)) continue;
                        float px = x + 0.5f, py = y + 0.5f, dx = b[0] - a[0], dy = b[1] - a[1];
                        float len2 = dx * dx + dy * dy;
                        float t = len2 > 0 ? Math.Max(0f, Math.Min(1f, ((px - a[0]) * dx + (py - a[1]) * dy) / len2)) : 0f;
                        float d = (float)Math.Sqrt(Sq(a[0] + t * dx - px) + Sq(a[1] + t * dy - py));
                        float w = line.HalfWidths.Count > k + 1 ? line.HalfWidths[k] + (line.HalfWidths[k + 1] - line.HalfWidths[k]) * t : hw;
                        at(x, y, d, along + t * seg, w);
                    }
                along += seg;
            }
        }

        // ------------------------------------------------------------------ 3. heights

        private void Raise(IReadOnlyList<BuiltRegion> built)
        {
            TerrainDef t = Data.Terrain ?? new TerrainDef();
            var relief = new Relief(Data);
            int n = W * H;

            // Each ground's height, smoothed into its neighbours'; rivers and lakes ask nothing, so the
            // land around them decides (their valleys are cut later); the sea lies at 0.
            var sum = new float[n];
            var weight = new float[n];
            var roll = new float[n];
            var crest = new float[n];
            var groundHeight = new float[Map.GroundIds.Count];
            var groundRoll = new float[Map.GroundIds.Count];
            var ridge = new bool[Map.GroundIds.Count];
            for (int k = 0; k < Map.GroundIds.Count; k++)
            {
                string id = Map.GroundIds[k];
                groundHeight[k] = t.Heights.TryGetValue(id, out float h) ? h : 0f;
                groundRoll[k] = t.Roll.TryGetValue(id, out float r) ? r : t.RollDefault;
                ridge[k] = t.RidgeGrounds.Contains(id);
            }
            for (int i = 0; i < n; i++)
            {
                byte g = Ground[i];
                if (g == _sea) { weight[i] = 1f; continue; }
                if (g == _lake || g == _river) continue;
                sum[i] = groundHeight[g];
                weight[i] = 1f;
                roll[i] = g == _pass || g == _bridge ? 0.3f : groundRoll[g];
                if (ridge[g]) crest[i] = 1f;
            }
            for (int pass = 0; pass < 3; pass++)
            {
                LandHeights.Blur(sum, W, H, t.Smooth);
                LandHeights.Blur(weight, W, H, t.Smooth);
            }
            LandHeights.Blur(roll, W, H, 8);
            for (int pass = 0; pass < 2; pass++) LandHeights.Blur(crest, W, H, 2);

            int[] seaDist = Distances(i => Ground[i] == _sea, (int)Math.Ceiling(t.CoastBlend) + 2);
            float top = 0f;
            foreach (float h in t.Heights.Values) top = Math.Max(top, h);
            var tile = new float[n];
            for (int ty = 0; ty < H; ty++)
                for (int tx = 0; tx < W; tx++)
                {
                    int i = ty * W + tx;
                    if (Ground[i] == _sea) continue;
                    float x = X0 + tx + 0.5f, y = Y0 + ty + 0.5f;
                    float h = weight[i] > 1e-4f ? sum[i] / weight[i] : 0f;
                    h += roll[i] * LandNoise.Fbm(Data.Seed + 7, x / t.RollScale, y / t.RollScale);
                    if (top > 0 && t.Rough > 0) h += t.Rough * Math.Min(1f, Math.Max(0f, h) / top) * LandNoise.Value(Data.Seed + 8, x / 23f, y / 23f);
                    h += relief.At(x, y);
                    if (crest[i] > 0f)
                        h += t.Ridge * crest[i] + t.Peaks * crest[i] * LandNoise.Ridged(Data.Seed + 11, x / t.PeakScale, y / t.PeakScale);
                    h = relief.Terrace(x, y, h);
                    // The land comes down to the sea, unless it ends in cliffs.
                    if (seaDist[i] < int.MaxValue && !relief.OnCliffCoast(x, y))
                    {
                        float k = LandNoise.Smooth((seaDist[i] - 0.5f) / t.CoastBlend);
                        h = h * (0.08f + 0.92f * k);
                    }
                    tile[i] = Math.Max(Beyond[i] ? 0.4f : 0.05f, h);
                }

            Heights = new LandHeights(X0, Y0, W, H);
            Heights.FromTiles(tile);
            foreach (PassDef p in Passes) Heights.Cut(p, t);
            Water(t);
        }

        /// <summary>Lakes flat at their level, rivers falling steadily to their mouths, and every bank and valley round them.</summary>
        private void Water(TerrainDef t)
        {
            int cw = W + 1, n = cw * (H + 1);
            _levels = new float[n];
            for (int c = 0; c < n; c++) _levels[c] = float.NaN;
            bool Tile(int tx, int ty, out int i)
            {
                i = ty * W + tx;
                return tx >= 0 && ty >= 0 && tx < W && ty < H;
            }

            // The sea: its corners at 0.
            for (int ty = 0; ty < H; ty++)
                for (int tx = 0; tx < W; tx++)
                    if (Ground[ty * W + tx] == _sea)
                        foreach (int c in Corners(tx, ty)) _levels[c] = 0f;

            // Lakes: each one flat at the lowest point of its shore.
            var lakeOf = new int[W * H];
            var lakeLevel = new List<float>();
            for (int i = 0; i < lakeOf.Length; i++) lakeOf[i] = -1;
            for (int start = 0; start < W * H; start++)
            {
                if (Ground[start] != _lake || lakeOf[start] >= 0) continue;
                int id = lakeLevel.Count;
                var members = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(start);
                lakeOf[start] = id;
                float level = float.MaxValue;
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    members.Add(i);
                    int tx = i % W, ty = i / W;
                    foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        if (!Tile(tx + dx, ty + dy, out int j)) continue;
                        if (Ground[j] == _lake) { if (lakeOf[j] < 0) { lakeOf[j] = id; queue.Enqueue(j); } }
                        else if (!IsWater(j)) level = Math.Min(level, Heights.CornerAt(Math.Max(tx, tx + dx), Math.Max(ty, ty + dy)));
                    }
                }
                if (level == float.MaxValue) level = 0f;
                level = Math.Max(0.1f, level - t.Bank);
                lakeLevel.Add(level);
                foreach (int i in members)
                    foreach (int c in Corners(i % W, i / W)) _levels[c] = level;
            }

            // Rivers: the main rivers first (into the sea or a lake), then those flowing into them.
            var order = Enumerable.Range(0, Data.Rivers.Count).OrderBy(r => Data.Rivers[r].Into.StartsWith("river:", StringComparison.Ordinal) ? 1 : 0).ToList();
            var profiles = new float[Data.Rivers.Count][];
            foreach (int r in order)
            {
                RiverDef def = Data.Rivers[r];
                LandLine line = Rivers[r];
                float total = 0f;
                for (int k = 1; k < line.Points.Count; k++) total += Dist(line.Points[k - 1], line.Points[k]);
                int steps = Math.Max(2, (int)Math.Ceiling(total));
                var profile = new float[steps + 1];
                float end = 0f;
                float[] mouth = line.Points[line.Points.Count - 1];
                if (def.Into.StartsWith("lake:", StringComparison.Ordinal))
                {
                    int best = -1;
                    float bestD = float.MaxValue;
                    for (int dy = -12; dy <= 12; dy++)
                        for (int dx = -12; dx <= 12; dx++)
                        {
                            int x = (int)mouth[0] + dx, y = (int)mouth[1] + dy;
                            if (!InBounds(x, y)) continue;
                            int i = Index(x, y);
                            if (lakeOf[i] >= 0 && dx * dx + dy * dy < bestD) { bestD = dx * dx + dy * dy; best = lakeOf[i]; }
                        }
                    end = best >= 0 ? lakeLevel[best] : 0f;
                }
                else if (def.Into.StartsWith("river:", StringComparison.Ordinal))
                {
                    int other = Data.Rivers.FindIndex(o => o.Id == def.Into.Substring(6));
                    if (other >= 0 && profiles[other] != null)
                    {
                        float at = Nearest(Rivers[other], mouth[0], mouth[1]);
                        end = profiles[other][Math.Min(profiles[other].Length - 1, (int)at)];
                    }
                }
                // The water's level: never rising downstream, never below its mouth, and kept
                // below the land it runs through.
                float levelNow = float.MaxValue;
                for (int s = 0; s <= steps; s++)
                {
                    (float x, float y) = PointAt(line, s);
                    float ground = Heights.Sample(x, y);
                    levelNow = Math.Min(levelNow, ground - t.Bank);
                    profile[s] = Math.Max(end, levelNow);
                }
                profiles[r] = profile;
            }
            for (int i = 0; i < W * H; i++)
            {
                int r = _riverOf[i];
                if (r < 0 || Ground[i] != _river) continue;
                float[] p = profiles[r];
                float level = p[Math.Max(0, Math.Min(p.Length - 1, (int)_riverAt[i]))];
                foreach (int c in Corners(i % W, i / W))
                    _levels[c] = float.IsNaN(_levels[c]) ? level : Math.Min(_levels[c], level);
            }

            // Banks and valleys: land near water comes down towards it, never below it.
            (float[] dist, float[] near) = WaterDistances(t.Valley + 2f);
            for (int c = 0; c < n; c++)
            {
                int cx = c % cw, cy = c / cw;
                if (!float.IsNaN(_levels[c]))
                {
                    Heights.SetCornerIndex(c, _levels[c]);
                    continue;
                }
                if (dist[c] > t.Valley + 1f) continue;
                float level = near[c];
                float h = Heights.CornerIndex(c);
                float bank = level + t.Bank * Math.Min(1f, dist[c]);
                float valley = bank + (h - bank) * LandNoise.Smooth(dist[c] / t.Valley);
                Heights.SetCornerIndex(c, Math.Max(bank, Math.Min(h, valley)));
            }
        }

        /// <summary>For every corner, the distance to the nearest water corner and that water's level (a two-pass chamfer).</summary>
        private (float[] dist, float[] level) WaterDistances(float max)
        {
            int cw = W + 1, ch = H + 1, n = cw * ch;
            var dist = new float[n];
            var level = new float[n];
            for (int c = 0; c < n; c++)
            {
                bool water = !float.IsNaN(_levels[c]);
                dist[c] = water ? 0f : float.MaxValue;
                level[c] = water ? _levels[c] : 0f;
            }
            const float D = 1.41421f;
            void Relax(int c, int from, float step)
            {
                if (dist[from] == float.MaxValue) return;
                float d = dist[from] + step;
                if (d < dist[c] && d <= max) { dist[c] = d; level[c] = level[from]; }
            }
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    int c = y * cw + x;
                    if (x > 0) Relax(c, c - 1, 1f);
                    if (y > 0) Relax(c, c - cw, 1f);
                    if (x > 0 && y > 0) Relax(c, c - cw - 1, D);
                    if (x < cw - 1 && y > 0) Relax(c, c - cw + 1, D);
                }
            for (int y = ch - 1; y >= 0; y--)
                for (int x = cw - 1; x >= 0; x--)
                {
                    int c = y * cw + x;
                    if (x < cw - 1) Relax(c, c + 1, 1f);
                    if (y < ch - 1) Relax(c, c + cw, 1f);
                    if (x < cw - 1 && y < ch - 1) Relax(c, c + cw + 1, D);
                    if (x > 0 && y < ch - 1) Relax(c, c + cw - 1, D);
                }
            return (dist, level);
        }

        private IEnumerable<int> Corners(int tx, int ty)
        {
            int cw = W + 1;
            yield return ty * cw + tx;
            yield return ty * cw + tx + 1;
            yield return (ty + 1) * cw + tx;
            yield return (ty + 1) * cw + tx + 1;
        }

        /// <summary>Tile distances (Chebyshev steps) from tiles passing a test, up to a limit (beyond: int.MaxValue).</summary>
        private int[] Distances(Func<int, bool> from, int limit)
        {
            var d = new int[W * H];
            var queue = new Queue<int>();
            for (int i = 0; i < d.Length; i++)
            {
                if (from(i)) { d[i] = 0; queue.Enqueue(i); }
                else d[i] = int.MaxValue;
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                if (d[i] >= limit) continue;
                int tx = i % W, ty = i / W;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = tx + dx, y = ty + dy;
                        if (x < 0 || y < 0 || x >= W || y >= H) continue;
                        int j = y * W + x;
                        if (d[j] != int.MaxValue) continue;
                        d[j] = d[i] + 1;
                        queue.Enqueue(j);
                    }
            }
            return d;
        }

        private static (float x, float y) PointAt(LandLine line, float s)
        {
            float along = 0f;
            for (int k = 0; k + 1 < line.Points.Count; k++)
            {
                float seg = Dist(line.Points[k], line.Points[k + 1]);
                if (along + seg >= s || k + 2 == line.Points.Count)
                {
                    float t = seg > 0 ? Math.Max(0f, Math.Min(1f, (s - along) / seg)) : 0f;
                    return (line.Points[k][0] + (line.Points[k + 1][0] - line.Points[k][0]) * t, line.Points[k][1] + (line.Points[k + 1][1] - line.Points[k][1]) * t);
                }
                along += seg;
            }
            return (line.Points[0][0], line.Points[0][1]);
        }

        /// <summary>How far along a line its nearest point to (x, y) is.</summary>
        private static float Nearest(LandLine line, float x, float y)
        {
            float best = float.MaxValue, bestS = 0f, along = 0f;
            for (int k = 0; k + 1 < line.Points.Count; k++)
            {
                float[] a = line.Points[k], b = line.Points[k + 1];
                float dx = b[0] - a[0], dy = b[1] - a[1], len2 = dx * dx + dy * dy;
                float t = len2 > 0 ? Math.Max(0f, Math.Min(1f, ((x - a[0]) * dx + (y - a[1]) * dy) / len2)) : 0f;
                float d = Sq(a[0] + t * dx - x) + Sq(a[1] + t * dy - y);
                float seg = (float)Math.Sqrt(len2);
                if (d < best) { best = d; bestS = along + t * seg; }
                along += seg;
            }
            return bestS;
        }

        // ------------------------------------------------------------------ 4. roads

        /// <summary>Which tiles carry a road, and of what kind (0 none, 1 track, 2 road, 3 main).</summary>
        private byte[] _road = Array.Empty<byte>();
        private bool[] _bridged = Array.Empty<bool>();

        private void Route()
        {
            _road = new byte[W * H];
            _bridged = new bool[W * H];
            var router = new RoadRouter(this);
            string[] order = { "main", "road", "track" };
            foreach (RoadDef road in Data.Roads.Where(r => r.Kind != "ferry").OrderBy(r => Array.IndexOf(order, r.Kind)))
            {
                RoadLine resolved = Map.Roads.FirstOrDefault(l => l.Id == road.Id) ?? new RoadLine();
                if (resolved.Points.Count < 2) continue;
                var line = new LandLine { Id = road.Id, Kind = road.Kind };
                for (int k = 0; k + 1 < resolved.Points.Count; k++)
                {
                    var a = resolved.Points[k];
                    var b = resolved.Points[k + 1];
                    List<float[]> part = road.Route ? router.Find(a.x, a.y, b.x, b.y, road.Kind) : new List<float[]> { new[] { a.x, a.y }, new[] { b.x, b.y } };
                    if (part.Count == 0)
                    {
                        Warnings.Add($"road {road.Id}: no way over the land from ({a.x}, {a.y}) to ({b.x}, {b.y}); drawn straight");
                        part = new List<float[]> { new[] { a.x, a.y }, new[] { b.x, b.y } };
                    }
                    if (line.Points.Count > 0) part.RemoveAt(0);
                    line.Points.AddRange(part);
                }
                Roads.Add(line);
                byte kind = (byte)(road.Kind == "main" ? 3 : road.Kind == "road" ? 2 : 1);
                float hw = HalfWidth(road.Kind);
                foreach (float[] p in line.Points) line.HalfWidths.Add(hw);
                Stamp(line, (x, y, d, s, w) =>
                {
                    if (d > w) return;
                    int i = Index(x, y);
                    if (Beyond[i]) return;
                    if (_road[i] < kind) _road[i] = kind;
                    if (IsWater(i) && Ground[i] != _sea) _bridged[i] = true;
                });
            }
            // The roads are levelled across, so a road on a slope is a shelf rather than a tilt.
            foreach (LandLine line in Roads)
                Heights.Level(line, HalfWidth(line.Kind) + 1.5f, i => IsWater(i));
        }

        public static float HalfWidth(string kind) => kind == "main" ? 1.6f : kind == "road" ? 1.05f : 0.7f;

        /// <summary>Whether a tile may carry a road, and what crossing it costs (for <see cref="RoadRouter"/>).</summary>
        internal float Cost(int i, float slope)
        {
            if (Beyond[i]) return -1f;
            byte g = Ground[i];
            if (g == _sea || g == _lake) return -1f;
            if (_bridged[i]) return 1.2f;
            GroundDef def = Map.GroundDefs[g];
            bool ridge = Data.Terrain?.RidgeGrounds.Contains(Map.GroundIds[g]) == true;
            if (!def.Walkable && !def.Water && !ridge) return -1f;
            if (slope > 0.85f) return -1f;
            float cost = 1f + 9f * slope * slope;
            if (g == _river) cost += 24f;
            if (g == _pass) cost *= 0.6f;
            if (_road[i] >= 2) cost *= 0.55f;
            else if (_road[i] == 1) cost *= 0.8f;
            return cost;
        }

        // ------------------------------------------------------------------ 5. settlements and the built regions

        private void Settle(IReadOnlyList<BuiltRegion> built)
        {
            foreach (PlaceDef p in Data.Places)
            {
                float r = PlatformRadius(p);
                if (r <= 0f) continue;
                Heights.Platform(p.At[0], p.At[1], r, c => !float.IsNaN(_levels[c]));
            }
            TerrainDef t = Data.Terrain ?? new TerrainDef();
            Heights.Settle(built, t.BuiltBlend);
        }

        public static float PlatformRadius(PlaceDef p) => p.Tier switch
        {
            "city" => 44f,
            "town" => 26f,
            "village" => 15f,
            "hamlet" => 9f,
            _ => 0f, // a landmark keeps the land it stands on (a face in a cliff, a crater's rim)
        };

        // ------------------------------------------------------------------ 6. painting

        private void Paint(IReadOnlyList<BuiltRegion> built)
        {
            TerrainDef t = Data.Terrain ?? new TerrainDef();
            int n = W * H;
            var original = (byte[])Ground.Clone();

            // Patches of other grounds: where slow noise passes the threshold that leaves about the
            // share asked for.
            var mixes = new List<(byte from, byte to, float scale, float threshold, int seed)>();
            int m = 0;
            foreach (KeyValuePair<string, GroundDef> g in Data.Grounds)
                foreach (MixDef mix in g.Value.Mix)
                {
                    if (!_g.TryGetValue(mix.Ground, out byte to)) continue;
                    int seed = Data.Seed + 700 + 13 * m++;
                    mixes.Add((_g[g.Key], to, mix.Scale, Threshold(seed, mix.Share), seed));
                }
            for (int ty = 0; ty < H; ty++)
                for (int tx = 0; tx < W; tx++)
                {
                    int i = ty * W + tx;
                    foreach (var mix in mixes)
                        if (original[i] == mix.from && LandNoise.Fbm(mix.seed, (X0 + tx) / mix.scale, (Y0 + ty) / mix.scale) > mix.threshold)
                        {
                            Ground[i] = mix.to;
                            break;
                        }
                }

            // Steep ground breaks into scree; high ground lies under snow.
            for (int ty = 0; ty < H; ty++)
                for (int tx = 0; tx < W; tx++)
                {
                    int i = ty * W + tx;
                    if (IsWater(i)) continue;
                    GroundDef def = Map.GroundDefs[original[i]];
                    int x = X0 + tx, y = Y0 + ty;
                    float a = Heights.Corner(x, y), b = Heights.Corner(x + 1, y), c = Heights.Corner(x, y + 1), d = Heights.Corner(x + 1, y + 1);
                    float slope = Math.Max(Math.Max(a, b), Math.Max(c, d)) - Math.Min(Math.Min(a, b), Math.Min(c, d));
                    float low = Math.Min(Math.Min(a, b), Math.Min(c, d));
                    bool ridge = t.RidgeGrounds.Contains(Map.GroundIds[original[i]]);
                    // The snow comes lower the further north: bare rock in the warm south, white in the cold.
                    float fall = y * t.SnowFall / 100f;
                    if (ridge && t.SnowAbove.HasValue && low > t.SnowAbove.Value - fall || t.SnowLine.HasValue && low > t.SnowLine.Value - fall)
                        Ground[i] = G("snowcap", G("snow", Ground[i]));
                    else if (def.Scree != null && slope > t.ScreeSlope && slope <= 1f)
                        Ground[i] = G(def.Scree, Ground[i]);
                }

            // Beaches along the sea, banks along lakes and rivers.
            int[] seaDist = Distances(i => Ground[i] == _sea, 3);
            int[] freshDist = Distances(i => Ground[i] == _lake || Ground[i] == _river, 2);
            for (int i = 0; i < n; i++)
            {
                if (IsWater(i) || Beyond[i]) continue;
                GroundDef def = Map.GroundDefs[original[i]];
                if (seaDist[i] <= 2 && def.Shore != null) Ground[i] = G(def.Shore, Ground[i]);
                else if (freshDist[i] <= 1 && (def.Bank ?? def.Shore) != null) Ground[i] = G(def.Bank ?? def.Shore, Ground[i]);
            }

            // Fields round the villages and towns of powers that farm.
            foreach (PlaceDef p in Data.Places.Where(p => p.IsSettlement))
            {
                int zi = LayoutReport.ZoneIndex(Map, p.At[0], p.At[1]);
                if (zi < 0 || Map.Power[zi] == ContinentMap.NoPower) continue;
                PowerDef power = Data.Powers[Map.PowerIds[Map.Power[zi]]];
                Fields(p, power, original);
            }

            // The roads, each power's own; bridges where they cross water; worn verges along highways.
            for (int i = 0; i < n; i++)
            {
                if (_road[i] == 0) continue;
                if (_bridged[i]) { Ground[i] = _bridge; continue; }
                if (IsWater(i)) continue;
                string kind = _road[i] == 3 ? "main" : _road[i] == 2 ? "road" : "track";
                PowerDef? power = Power[i] == ContinentMap.NoPower ? null : Data.Powers[Map.PowerIds[Power[i]]];
                string? id = power != null && power.Roads.TryGetValue(kind, out string? own) ? own : null;
                id ??= kind == "track" ? power?.Worn : null;
                if (id != null) Ground[i] = G(id, Ground[i]);
            }
            foreach (LandLine line in Roads.Where(l => l.Kind == "main"))
                Stamp(line, (x, y, d, s, w) =>
                {
                    if (d <= w || d > w + 1.3f) return;
                    int i = Index(x, y);
                    if (_road[i] != 0 || IsWater(i) || Beyond[i] || Power[i] == ContinentMap.NoPower) return;
                    byte worn = G(Data.Powers[Map.PowerIds[Power[i]]].Worn, 255);
                    if (worn != 255) Ground[i] = worn;
                });

            // Squares where the roads meet the places; marks for landmarks and sites.
            foreach (PlaceDef p in Data.Places)
            {
                int zi = LayoutReport.ZoneIndex(Map, p.At[0], p.At[1]);
                PowerDef? power = zi >= 0 && Map.Power[zi] != ContinentMap.NoPower ? Data.Powers[Map.PowerIds[Map.Power[zi]]] : null;
                byte paving = p.IsSettlement ? G(power?.Square, G("paving", 0)) : G("sigil", 0);
                float r = SquareRadius(p);
                Disc(p.At[0], p.At[1], r, (i, d) =>
                {
                    if (IsWater(i) || Beyond[i]) return;
                    // An octagon, so the overlay's diagonal shapes make it read as round.
                    int tx = i % W, ty = i / W;
                    float dx = Math.Abs(X0 + tx + 0.5f - p.At[0]), dy = Math.Abs(Y0 + ty + 0.5f - p.At[1]);
                    if (dx + dy <= r * 1.35f && Math.Max(dx, dy) <= r) Ground[i] = paving;
                });
            }
            byte site = G("site", 0);
            foreach (SiteDef s in Data.Sites)
                for (int dy = -1; dy <= 0; dy++)
                    for (int dx = -1; dx <= 0; dx++)
                    {
                        int x = (int)Math.Floor(s.At[0]) + dx + 1, y = (int)Math.Floor(s.At[1]) + dy + 1;
                        if (!InBounds(x, y)) continue;
                        int i = Index(x, y);
                        if (!IsWater(i) && !Beyond[i]) Ground[i] = site;
                    }
        }

        public static float SquareRadius(PlaceDef p) => p.Tier switch
        {
            "city" => 11f,
            "town" => 7f,
            "village" => 4.5f,
            "hamlet" => 2.5f,
            _ => 2f,
        };

        /// <summary>A patchwork of fields round a settlement: plots on a turned grid, with hedge lines between, thinning out with distance.</summary>
        private void Fields(PlaceDef p, PowerDef power, byte[] original)
        {
            if (power.Fields.Count == 0) return;
            var fields = power.Fields.Select(f => G(f, 255)).Where(b => b != 255).ToList();
            if (fields.Count == 0) return;
            float reach = p.Tier switch { "city" => 90f, "town" => 60f, "village" => 44f, _ => 24f };
            float inner = PlatformRadius(p) * 0.8f;
            uint key = Geometry.Hash(Data.Seed, p.Id);
            // Square to the world or at 45 degrees, so the painted shapes draw the plots' edges cleanly.
            double angle = (key & 3) * Math.PI / 4.0;
            float ca = (float)Math.Cos(angle), sa = (float)Math.Sin(angle);
            float pw = 11f + (key >> 16 & 7), ph = 7f + (key >> 20 & 3);
            Disc(p.At[0], p.At[1], reach, (i, d) =>
            {
                if (d < inner || IsWater(i) || Beyond[i] || _road[i] != 0) return;
                // Only open farmland gets farmed: not forest, moor or rock.
                if (!Map.GroundDefs[original[i]].Farm || !Map.GroundDefs[Ground[i]].Farm) return;
                int tx = i % W, ty = i / W;
                float x = X0 + tx + 0.5f - p.At[0], y = Y0 + ty + 0.5f - p.At[1];
                float u = x * ca + y * sa + 1000f, v = -x * sa + y * ca + 1000f;
                int pu = (int)Math.Floor(u / pw), pv = (int)Math.Floor(v / ph);
                float fu = u - pu * pw, fv = v - pv * ph;
                if (fu < 1f || fv < 1f) return; // the hedge line between plots
                // About half the plots near the place are fields, thinning to none at the reach; the rest stays pasture.
                float keep = LandNoise.Hash01((int)key, pu, pv);
                if (keep > 0.6f * (1f - LandNoise.Smooth((d - inner) / (reach - inner)))) return;
                float a = Heights.Corner(X0 + tx, Y0 + ty), b = Heights.Corner(X0 + tx + 1, Y0 + ty + 1);
                if (Math.Abs(a - b) > 0.5f) return; // too steep to plough
                Ground[i] = fields[(int)(LandNoise.Hash01((int)key + 1, pu, pv) * fields.Count) % fields.Count];
            });
        }

        private void Disc(float cx, float cy, float r, Action<int, float> at)
        {
            for (int y = (int)Math.Floor(cy - r); y <= (int)Math.Ceiling(cy + r); y++)
                for (int x = (int)Math.Floor(cx - r); x <= (int)Math.Ceiling(cx + r); x++)
                {
                    if (!InBounds(x, y)) continue;
                    float d = (float)Math.Sqrt(Sq(x + 0.5f - cx) + Sq(y + 0.5f - cy));
                    if (d <= r) at(Index(x, y), d);
                }
        }

        /// <summary>The threshold of a mix's noise that about <paramref name="share"/> of the land passes.</summary>
        private static float Threshold(int seed, float share)
        {
            var samples = new float[4096];
            for (int k = 0; k < samples.Length; k++)
                samples[k] = LandNoise.Fbm(seed, LandNoise.Hash01(seed, k, 1) * 300f, LandNoise.Hash01(seed, k, 2) * 300f);
            Array.Sort(samples);
            int at = (int)Math.Round((1f - Math.Max(0f, Math.Min(1f, share))) * (samples.Length - 1));
            return samples[at];
        }

        private static float Dist(float[] a, float[] b) => (float)Math.Sqrt(Sq(a[0] - b[0]) + Sq(a[1] - b[1]));
        private static float Sq(float v) => v * v;
    }
}
