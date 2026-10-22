using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Rth.Shared.World.Layout
{
    /// <summary>A compiled road: its kind and its points in tiles, with the places it stops at.</summary>
    public sealed class RoadLine
    {
        public string Id = "";
        public string Kind = "road";
        public List<(float x, float y)> Points = new List<(float x, float y)>();
        public List<string> Stops = new List<string>();
    }

    /// <summary>
    /// The continent as a grid of 8 × 8 tile zones (docs/layout.md): each zone's ground, power and
    /// whether a road runs through it, plus the resolved rivers, roads and places. Columns run
    /// east from <see cref="OriginX"/>, rows north from <see cref="OriginY"/>.
    /// </summary>
    public sealed class ContinentMap
    {
        public const byte NoPower = 255;

        public ContinentData Data = new ContinentData();
        public int ZoneTiles, OriginX, OriginY, Cols, Rows;
        public List<string> GroundIds = new List<string>();
        public List<GroundDef> GroundDefs = new List<GroundDef>();
        public List<string> PowerIds = new List<string>();
        public byte[] Ground = Array.Empty<byte>();
        public byte[] Power = Array.Empty<byte>();
        public bool[] Road = Array.Empty<bool>();
        /// <summary>Land reached only by boat (islands marked "by": "ship").</summary>
        public bool[] ByShip = Array.Empty<bool>();
        /// <summary>Distance from open sea in zones (0 on water), for heights.</summary>
        public int[] CoastDistance = Array.Empty<int>();
        public List<RoadLine> Roads = new List<RoadLine>();
        public Dictionary<string, PlaceDef> Places = new Dictionary<string, PlaceDef>();
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();

        public int Count => Cols * Rows;
        public int Index(int col, int row) => row * Cols + col;
        public bool InGrid(int col, int row) => col >= 0 && row >= 0 && col < Cols && row < Rows;

        public (int col, int row) ZoneOf(float x, float y) =>
            ((int)Math.Floor((x - OriginX) / ZoneTiles), (int)Math.Floor((y - OriginY) / ZoneTiles));

        public (float x, float y) Centre(int col, int row) =>
            (OriginX + (col + 0.5f) * ZoneTiles, OriginY + (row + 0.5f) * ZoneTiles);

        public GroundDef GroundAt(int i) => GroundDefs[Ground[i]];
        public string GroundIdAt(int i) => GroundIds[Ground[i]];
        public bool IsWater(int i) => GroundAt(i).Water;
        /// <summary>Land, counting rivers (narrower than a zone) but not lakes or the sea.</summary>
        public bool IsLand(int i) => Power[i] != NoPower && (!GroundAt(i).Water || GroundIds[Ground[i]] == Data.RiverGround);
        public bool IsWalkable(int i) => !GroundAt(i).Water && GroundAt(i).Walkable;
        public string? PowerIdAt(int i) => Power[i] == NoPower ? null : PowerIds[Power[i]];
        public bool IsCore(int i) => Power[i] != NoPower && Data.Powers[PowerIds[Power[i]]].Core && !ByShip[i];
        public int TilesPerZone => ZoneTiles * ZoneTiles;

        /// <summary>The zone grid in the terrain bake's format (<see cref="WorldLayout"/>), rows from the north.</summary>
        public WorldLayout ToWorldLayout()
        {
            var layout = new WorldLayout { ZoneTiles = ZoneTiles, OriginX = OriginX, OriginY = OriginY, SeaLevel = Data.SeaLevel, Seed = Data.Seed };
            for (int g = 0; g < GroundDefs.Count; g++)
            {
                GroundDef d = GroundDefs[g];
                layout.Biomes[d.Letter] = new BiomeDef { Name = d.Name, Ground = d.Palette, Water = d.Water, Walkable = d.Walkable && !d.Water, Rough = d.Rough };
            }
            for (int row = Rows - 1; row >= 0; row--)
            {
                var zones = new StringBuilder(Cols);
                var heights = new StringBuilder(Cols);
                for (int col = 0; col < Cols; col++)
                {
                    int i = Index(col, row);
                    zones.Append(GroundAt(i).Letter);
                    heights.Append(WorldLayout.Digits[HeightDigit(i)]);
                }
                layout.Zones.Add(zones.ToString());
                layout.Heights.Add(heights.ToString());
            }
            return layout;
        }

        /// <summary>The zone's macro height in half tiles: its ground's height, lowered towards the sea.</summary>
        public int HeightDigit(int i)
        {
            GroundDef g = GroundAt(i);
            if (g.Water) return 0;
            float ramp = Math.Min(1f, CoastDistance[i] / 3f);
            int half = (int)Math.Round(g.Height * 2f * ramp);
            return Math.Max(1, Math.Min(WorldLayout.Digits.Length - 1, half));
        }

        /// <summary>The zone grid as JSON in the <see cref="WorldLayout"/> format, one row per line so it diffs well.</summary>
        public string ToLayoutJson()
        {
            WorldLayout l = ToWorldLayout();
            var sb = new StringBuilder();
            string F(float v) => v.ToString(CultureInfo.InvariantCulture);
            sb.Append("{\n");
            sb.Append("  \"_comment\": \"Generated from world/layout/continent.json by `dotnet run --project tools/world -- layout`. Do not edit: change continent.json and re-run (docs/layout.md).\",\n");
            sb.Append($"  \"zoneTiles\": {l.ZoneTiles},\n  \"originX\": {l.OriginX},\n  \"originY\": {l.OriginY},\n  \"seaLevel\": {F(l.SeaLevel)},\n  \"seed\": {l.Seed},\n");
            sb.Append("  \"biomes\": {\n");
            var letters = l.Biomes.Keys.ToList();
            for (int k = 0; k < letters.Count; k++)
            {
                BiomeDef b = l.Biomes[letters[k]];
                sb.Append($"    {JsonString(letters[k])}: {{ \"name\": {JsonString(b.Name)}, \"ground\": {JsonString(b.Ground)}, \"water\": {(b.Water ? "true" : "false")}, \"walkable\": {(b.Walkable ? "true" : "false")}, \"rough\": {F(b.Rough)} }}");
                sb.Append(k < letters.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  },\n");
            void Rows(string name, List<string> rows, bool last)
            {
                sb.Append($"  \"{name}\": [\n");
                for (int r = 0; r < rows.Count; r++) sb.Append("    \"").Append(rows[r]).Append(r < rows.Count - 1 ? "\",\n" : "\"\n");
                sb.Append(last ? "  ]\n" : "  ],\n");
            }
            Rows("zones", l.Zones, false);
            Rows("heights", l.Heights, true);
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string JsonString(string s) => JToken.FromObject(s).ToString(Newtonsoft.Json.Formatting.None);
    }

    /// <summary>Turns <see cref="ContinentData"/> into a <see cref="ContinentMap"/> (docs/layout.md).</summary>
    public static class ContinentCompiler
    {
        public static ContinentMap Compile(ContinentData data)
        {
            var map = new ContinentMap { Data = data, ZoneTiles = data.ZoneTiles };
            Validate(data, map.Errors);
            if (map.Errors.Count > 0) return map;

            Transform(data);
            map.OriginX = data.Bounds[0];
            map.OriginY = data.Bounds[1];
            map.Cols = (data.Bounds[2] - data.Bounds[0]) / data.ZoneTiles;
            map.Rows = (data.Bounds[3] - data.Bounds[1]) / data.ZoneTiles;
            map.GroundIds = data.Grounds.Keys.ToList();
            map.GroundDefs = data.Grounds.Values.ToList();
            map.PowerIds = data.Powers.Keys.ToList();
            foreach (PlaceDef p in data.Places) map.Places[p.Id] = p;

            int n = map.Count;
            map.Ground = new byte[n];
            map.Power = new byte[n];
            map.Road = new bool[n];
            map.ByShip = new bool[n];
            byte G(string id) => (byte)map.GroundIds.IndexOf(id);
            byte P(string id) => (byte)map.PowerIds.IndexOf(id);

            var spineLine = data.Spine.Line;
            var land = data.Land.Select(e => new Region(e, spineLine, data.Seed, "land:" + e.Id)).ToList();
            var seas = data.Seas.Select(e => new Region(e, spineLine, data.Seed, "sea:" + e.Id)).ToList();
            var realms = data.Realms.Select(e => new Region(e, spineLine, data.Seed, "realm:" + e.Id)).ToList();
            var areas = data.Areas.Select(e => new Region(e, spineLine, data.Seed, "area:" + e.Id)).ToList();

            byte sea = G(data.SeaGround), river = G(data.RiverGround), bridge = G(data.BridgeGround), pass = G(data.PassGround);
            for (int row = 0; row < map.Rows; row++)
                for (int col = 0; col < map.Cols; col++)
                {
                    int i = map.Index(col, row);
                    (float x, float y) = map.Centre(col, row);
                    Region? island = null;
                    bool isLand = false;
                    foreach (Region r in land)
                        if (r.Contains(x, y)) { isLand = true; island = r.Entry.By == "ship" ? r : island; }
                    foreach (Region r in seas)
                        if (r.Contains(x, y)) isLand = false;
                    if (!isLand)
                    {
                        map.Ground[i] = sea;
                        map.Power[i] = ContinentMap.NoPower;
                        continue;
                    }
                    map.ByShip[i] = island != null;
                    string? power = null;
                    foreach (Region r in realms)
                        if (r.Contains(x, y)) power = r.Entry.Power!;
                    map.Power[i] = power == null ? Unclaimed : P(power);
                }
            ClaimGaps(map, P(data.DefaultPower));

            for (int row = 0; row < map.Rows; row++)
                for (int col = 0; col < map.Cols; col++)
                {
                    int i = map.Index(col, row);
                    if (map.Power[i] == ContinentMap.NoPower) continue;
                    (float x, float y) = map.Centre(col, row);
                    map.Ground[i] = G(data.Powers[map.PowerIds[map.Power[i]]].Ground);
                    foreach (Region r in areas)
                        if (r.Contains(x, y)) map.Ground[i] = G(r.Entry.Ground!);
                }

            // Rivers, then passes, then roads: a road over a river is a bridge.
            foreach (RiverDef r in data.Rivers)
                foreach (int i in Band(map, r.Line, r.HalfWidth))
                    if (map.Ground[i] != sea && !map.IsWater(i)) map.Ground[i] = river;
            foreach (PassDef p in data.Spine.Passes)
            {
                foreach (int i in Band(map, p.Line, p.HalfWidth))
                {
                    if (map.Ground[i] == river) map.Ground[i] = bridge;
                    else if (map.IsWater(i)) map.Errors.Add($"pass {p.Id} crosses open water near {Describe(map, i)}");
                    else map.Ground[i] = pass;
                    map.Road[i] = true;
                }
                map.Roads.Add(new RoadLine { Id = p.Id, Kind = "pass", Points = p.Line.Select(v => (v[0], v[1])).ToList() });
            }
            foreach (RoadDef road in data.Roads)
            {
                RoadLine line = Resolve(map, road);
                map.Roads.Add(line);
                if (road.Kind == "ferry" || line.Points.Count < 2) continue;
                var bad = new HashSet<string>();
                foreach (int i in Band(map, line.Points.Select(p => new[] { p.x, p.y }).ToList(), 2f))
                {
                    if (map.Ground[i] == river) map.Ground[i] = bridge;
                    else if (map.IsWater(i)) bad.Add("water");
                    else if (!map.IsWalkable(i)) bad.Add(map.GroundIdAt(i));
                    map.Road[i] = true;
                }
                foreach (string b in bad)
                    map.Errors.Add($"road {road.Id} crosses {b}: route it round, over a pass, or make it a ferry");
            }

            map.CoastDistance = CoastDistances(map, sea);
            return map;
        }

        private const byte Unclaimed = 254;

        /// <summary>
        /// Land no realm shape covers (the slivers where two rough borders don't quite meet) takes
        /// the power of the nearest claimed land; land with none near at all takes the default.
        /// </summary>
        private static void ClaimGaps(ContinentMap map, byte fallback)
        {
            var queue = new Queue<int>();
            for (int i = 0; i < map.Count; i++)
                if (map.Power[i] != Unclaimed && map.Power[i] != ContinentMap.NoPower) queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int col = i % map.Cols, row = i / map.Cols;
                foreach ((int dc, int dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    if (!map.InGrid(col + dc, row + dr)) continue;
                    int j = map.Index(col + dc, row + dr);
                    if (map.Power[j] != Unclaimed) continue;
                    map.Power[j] = map.Power[i];
                    queue.Enqueue(j);
                }
            }
            for (int i = 0; i < map.Count; i++)
                if (map.Power[i] == Unclaimed) map.Power[i] = fallback;
        }

        /// <summary>Applies <see cref="ContinentData.Scale"/> about the anchor, once, in place.</summary>
        private static void Transform(ContinentData d)
        {
            float s = d.Scale;
            if (Math.Abs(s - 1f) < 1e-6f) return;
            float ax = d.Anchor[0], ay = d.Anchor[1];
            float[] T(float[] p) => new[] { ax + (p[0] - ax) * s, ay + (p[1] - ay) * s };
            void Pts(List<float[]>? pts) { if (pts != null) for (int k = 0; k < pts.Count; k++) pts[k] = T(pts[k]); }
            void Sh(Shape e)
            {
                Pts(e.Poly);
                Pts(e.Line);
                e.HalfWidth *= s;
                e.Rough *= s;
                if (e.Ellipse != null) e.Ellipse = new[] { ax + (e.Ellipse[0] - ax) * s, ay + (e.Ellipse[1] - ay) * s, e.Ellipse[2] * s, e.Ellipse[3] * s };
                if (e.MinY.HasValue) e.MinY = ay + (e.MinY.Value - ay) * s;
                if (e.MaxY.HasValue) e.MaxY = ay + (e.MaxY.Value - ay) * s;
            }
            foreach (ShapeEntry e in d.Land.Concat(d.Seas).Concat(d.Realms).Concat(d.Areas)) Sh(e);
            Pts(d.Spine.Line);
            foreach (PassDef p in d.Spine.Passes) Pts(p.Line);
            foreach (RiverDef r in d.Rivers) Pts(r.Line);
            foreach (PlaceDef p in d.Places) p.At = T(p.At);
            foreach (PowerDef p in d.Powers.Values) if (p.Label != null) p.Label = T(p.Label);
            foreach (RoadDef r in d.Roads)
                for (int k = 0; k < r.Line.Count; k++)
                    if (r.Line[k] is JArray a) r.Line[k] = new JArray(T(new[] { (float)a[0], (float)a[1] }));
            d.Scale = 1f;
        }

        private static void Validate(ContinentData d, List<string> errors)
        {
            if (d.ZoneTiles <= 0 || d.RegionTiles % d.ZoneTiles != 0) errors.Add("regionTiles must be a multiple of zoneTiles");
            if (d.Bounds == null || d.Bounds.Length != 4 || d.Bounds[2] <= d.Bounds[0] || d.Bounds[3] <= d.Bounds[1])
                errors.Add("bounds must be [x0, y0, x1, y1] with x1 > x0 and y1 > y0");
            else if (d.Bounds.Any(b => b % d.RegionTiles != 0))
                errors.Add($"bounds must be multiples of {d.RegionTiles} so the region grid lines up");
            var letters = new HashSet<string>();
            foreach (var g in d.Grounds)
            {
                if (g.Value.Letter.Length != 1) errors.Add($"ground {g.Key}: letter must be one character");
                else if (!letters.Add(g.Value.Letter)) errors.Add($"ground {g.Key}: letter '{g.Value.Letter}' is used twice");
            }
            if (d.Grounds.Count > 250) errors.Add("too many grounds");
            void Ground(string? id, string where)
            {
                if (id == null || !d.Grounds.ContainsKey(id)) errors.Add($"{where}: unknown ground '{id}'");
            }
            void Power(string? id, string where)
            {
                if (id == null || !d.Powers.ContainsKey(id)) errors.Add($"{where}: unknown power '{id}'");
            }
            Ground(d.SeaGround, "seaGround");
            Ground(d.RiverGround, "riverGround");
            Ground(d.BridgeGround, "bridgeGround");
            Ground(d.PassGround, "passGround");
            Power(d.DefaultPower, "defaultPower");
            foreach (var p in d.Powers) Ground(p.Value.Ground, "power " + p.Key);
            foreach (ShapeEntry e in d.Realms) Power(e.Power, "realm " + e.Id);
            foreach (ShapeEntry e in d.Areas) Ground(e.Ground, "area " + e.Id);
            foreach (ShapeEntry e in d.Land.Concat(d.Seas).Concat(d.Realms).Concat(d.Areas))
            {
                int kinds = (e.Poly != null ? 1 : 0) + (e.Line != null || e.Along != null ? 1 : 0) + (e.Ellipse != null ? 1 : 0);
                if (kinds != 1) errors.Add($"shape {e.Id}: give exactly one of poly, line/along or ellipse");
                if (e.Poly != null && e.Poly.Count < 3) errors.Add($"shape {e.Id}: a polygon needs 3 points");
                if (e.Along != null && e.Along != "spine") errors.Add($"shape {e.Id}: along must be \"spine\"");
                if ((e.Line != null || e.Along != null) && e.HalfWidth <= 0) errors.Add($"shape {e.Id}: a band needs halfWidth");
                if (e.Ellipse != null && e.Ellipse.Length != 4) errors.Add($"shape {e.Id}: ellipse is [cx, cy, rx, ry]");
            }
            if (d.Spine.Line.Count < 2) errors.Add("spine: the line needs two points");
            var ids = new HashSet<string>();
            foreach (PlaceDef p in d.Places)
            {
                if (!ids.Add(p.Id)) errors.Add($"place {p.Id}: id used twice");
                if (p.Tier != "landmark" && !d.Tiers.ContainsKey(p.Tier)) errors.Add($"place {p.Id}: unknown tier '{p.Tier}'");
                if (p.At == null || p.At.Length != 2) errors.Add($"place {p.Id}: at is [x, y]");
            }
            foreach (RiverDef r in d.Rivers)
                if (r.Line.Count < 2) errors.Add($"river {r.Id}: the line needs two points");
            foreach (RoadDef r in d.Roads)
            {
                if (r.Line.Count < 2) errors.Add($"road {r.Id}: needs two stops");
                foreach (JToken t in r.Line)
                    if (t.Type == JTokenType.String && !ids.Contains((string)t!)) errors.Add($"road {r.Id}: unknown place '{t}'");
                    else if (t.Type != JTokenType.String && !(t is JArray a && a.Count == 2)) errors.Add($"road {r.Id}: a stop is a place id or [x, y]");
            }
        }

        private static RoadLine Resolve(ContinentMap map, RoadDef road)
        {
            var line = new RoadLine { Id = road.Id, Kind = road.Kind };
            foreach (JToken t in road.Line)
            {
                if (t.Type == JTokenType.String)
                {
                    PlaceDef p = map.Places[(string)t!];
                    line.Points.Add((p.At[0], p.At[1]));
                    line.Stops.Add(p.Id);
                }
                else line.Points.Add(((float)t[0]!, (float)t[1]!));
            }
            return line;
        }

        /// <summary>
        /// Zones within <paramref name="halfWidth"/> of a line, widened so even a thin line is a
        /// continuous band with no diagonal-only joins (it blocks, or carries, 8-way movement).
        /// </summary>
        public static IEnumerable<int> Band(ContinentMap map, List<float[]> line, float halfWidth)
        {
            float reach = Math.Max(halfWidth, map.ZoneTiles * 0.72f);
            float minX = line.Min(p => p[0]) - reach, maxX = line.Max(p => p[0]) + reach;
            float minY = line.Min(p => p[1]) - reach, maxY = line.Max(p => p[1]) + reach;
            (int c0, int r0) = map.ZoneOf(minX, minY);
            (int c1, int r1) = map.ZoneOf(maxX, maxY);
            for (int row = Math.Max(0, r0); row <= Math.Min(map.Rows - 1, r1); row++)
                for (int col = Math.Max(0, c0); col <= Math.Min(map.Cols - 1, c1); col++)
                {
                    (float x, float y) = map.Centre(col, row);
                    if (Geometry.DistanceToLine(line, x, y) <= reach) yield return map.Index(col, row);
                }
        }

        private static int[] CoastDistances(ContinentMap map, byte sea)
        {
            var dist = new int[map.Count];
            var queue = new Queue<int>();
            for (int i = 0; i < map.Count; i++)
            {
                if (map.Ground[i] == sea) { dist[i] = 0; queue.Enqueue(i); }
                else dist[i] = int.MaxValue;
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int col = i % map.Cols, row = i / map.Cols;
                foreach ((int dc, int dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int c = col + dc, r = row + dr;
                    if (!map.InGrid(c, r)) continue;
                    int j = map.Index(c, r);
                    if (dist[j] != int.MaxValue) continue;
                    dist[j] = dist[i] + 1;
                    queue.Enqueue(j);
                }
            }
            for (int i = 0; i < dist.Length; i++) if (dist[i] == int.MaxValue) dist[i] = 99;
            return dist;
        }

        public static string Describe(ContinentMap map, int i)
        {
            (float x, float y) = map.Centre(i % map.Cols, i / map.Cols);
            return $"({(int)x}, {(int)y})";
        }

        /// <summary>A shape made ready for point tests: rough polygons jittered once, bands resolved.</summary>
        private sealed class Region
        {
            public readonly ShapeEntry Entry;
            private readonly List<float[]>? _poly;
            private readonly List<float[]>? _line;
            private readonly float _minX, _maxX, _minY, _maxY;

            public Region(ShapeEntry e, List<float[]> spine, int seed, string key)
            {
                Entry = e;
                if (e.Poly != null) _poly = e.Rough > 0 ? Geometry.Roughen(e.Poly, e.Rough, seed) : e.Poly;
                if (e.Line != null || e.Along != null) _line = e.Line ?? spine;
                var pts = _poly ?? _line;
                float pad = _line != null ? e.HalfWidth : 0;
                if (pts != null)
                {
                    _minX = pts.Min(p => p[0]) - pad; _maxX = pts.Max(p => p[0]) + pad;
                    _minY = pts.Min(p => p[1]) - pad; _maxY = pts.Max(p => p[1]) + pad;
                }
                else if (e.Ellipse != null)
                {
                    _minX = e.Ellipse[0] - e.Ellipse[2]; _maxX = e.Ellipse[0] + e.Ellipse[2];
                    _minY = e.Ellipse[1] - e.Ellipse[3]; _maxY = e.Ellipse[1] + e.Ellipse[3];
                }
            }

            public bool Contains(float x, float y)
            {
                if (x < _minX || x > _maxX || y < _minY || y > _maxY) return false;
                if (Entry.MinY.HasValue && y < Entry.MinY.Value) return false;
                if (Entry.MaxY.HasValue && y > Entry.MaxY.Value) return false;
                if (_poly != null) return Geometry.InPolygon(_poly, x, y);
                if (_line != null) return Geometry.DistanceToLine(_line, x, y) <= Entry.HalfWidth;
                float[] el = Entry.Ellipse!;
                float dx = (x - el[0]) / el[2], dy = (y - el[1]) / el[3];
                return dx * dx + dy * dy <= 1f;
            }
        }
    }

    public static class Geometry
    {
        public static float DistanceToLine(List<float[]> line, float x, float y)
        {
            float best = float.MaxValue;
            for (int k = 0; k + 1 < line.Count; k++)
                best = Math.Min(best, DistanceToSegment(line[k][0], line[k][1], line[k + 1][0], line[k + 1][1], x, y));
            if (line.Count == 1) best = (float)Math.Sqrt((x - line[0][0]) * (x - line[0][0]) + (y - line[0][1]) * (y - line[0][1]));
            return best;
        }

        public static float DistanceToSegment(float x1, float y1, float x2, float y2, float x, float y)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float len = dx * dx + dy * dy;
            float t = len <= 0 ? 0 : Math.Max(0, Math.Min(1, ((x - x1) * dx + (y - y1) * dy) / len));
            float px = x1 + t * dx - x, py = y1 + t * dy - y;
            return (float)Math.Sqrt(px * px + py * py);
        }

        public static bool InPolygon(List<float[]> poly, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                float xi = poly[i][0], yi = poly[i][1], xj = poly[j][0], yj = poly[j][1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// A polygon with wandering edges: each edge is split at its middle, the middle pushed
        /// sideways by up to <paramref name="rough"/> tiles, and the halves split again with half the
        /// push, until pieces are about 12 tiles long. Deterministic for a seed.
        /// </summary>
        public static List<float[]> Roughen(List<float[]> poly, float rough, int seed)
        {
            var result = new List<float[]>();
            for (int k = 0; k < poly.Count; k++)
            {
                float[] a = poly[k], b = poly[(k + 1) % poly.Count];
                result.Add(a);
                // The wander depends only on the edge (and the seed), walked in a fixed direction,
                // so two shapes sharing an edge (a border) get exactly the same line.
                bool flip = a[0] > b[0] || a[0] == b[0] && a[1] > b[1];
                float[] p = flip ? b : a, q = flip ? a : b;
                uint counter = Hash(Hash(Hash((uint)seed, Bits(p[0])), Bits(p[1])), Hash(Bits(q[0]), Bits(q[1])));
                var mids = new List<float[]>();
                Split(p, q, rough, mids, ref counter);
                if (flip) mids.Reverse();
                result.AddRange(mids);
            }
            return result;
        }

        private static uint Bits(float f) => (uint)BitConverter.SingleToInt32Bits(f);

        private static void Split(float[] a, float[] b, float push, List<float[]> into, ref uint counter)
        {
            float dx = b[0] - a[0], dy = b[1] - a[1];
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 10f) return;
            counter = Hash(counter, 0x9E3779B9u);
            float t = (counter & 0xFFFF) / 65535f * 2f - 1f;
            float side = Math.Min(push, len * 0.35f) * t;
            var mid = new[] { (a[0] + b[0]) / 2 - dy / len * side, (a[1] + b[1]) / 2 + dx / len * side };
            Split(a, mid, push * 0.6f, into, ref counter);
            into.Add(mid);
            Split(mid, b, push * 0.6f, into, ref counter);
        }

        public static uint Hash(int seed, string key)
        {
            uint h = 2166136261u ^ (uint)seed;
            foreach (char c in key) h = (h ^ c) * 16777619u;
            return Hash(h, 0x85EBCA6Bu);
        }

        public static uint Hash(uint a, uint b)
        {
            uint h = a ^ (b * 0x27D4EB2Fu);
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
            return h;
        }
    }
}
