using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rth.Shared.World.Data;

namespace Rth.Shared.World.Layout
{
    /// <summary>
    /// The walkable blockout (L2, docs/layout.md): every 96-tile region of the layout that holds core
    /// land, and the ring of sea and frontier around them, as region data drawn from the layout. Each
    /// is one <see cref="PieceKind.Tiles"/> piece: the zone grid's ground with its edges wandered so
    /// coasts and borders don't step by whole zones, the rivers, passes and roads at their real widths
    /// in tiles, and a plain square where each place will stand. No trees, rocks or props. Frontier
    /// land is drawn but blocked; the built regions (Tolling, Palmerway) are left alone.
    /// </summary>
    public static class Blockout
    {
        /// <summary>The <see cref="RegionData.Generated"/> tag of the regions this writes.</summary>
        public const string Generator = "blockout";

        /// <summary>How far, in tiles, borders wander from the zone grid, and over what distance.</summary>
        public const float Warp = 5f, WarpPeriod = 13f;

        public const char MainRoad = '=', Road = '-', Settlement = '+', Landmark = '*', Frontier = '%';

        public sealed class Result
        {
            public List<RegionData> Regions = new List<RegionData>();
            public List<string> Warnings = new List<string>();
        }

        public static string RegionId(int rx, int ry) => $"land_{Key(rx)}_{Key(ry)}";
        private static string Key(int v) => v < 0 ? "m" + -v : v.ToString();

        public static Result Generate(ContinentMap map, IReadOnlyList<BuiltRegion> built)
        {
            var result = new Result();
            int size = map.Data.RegionTiles;
            byte[] ground = BaseGround(map);
            int cols = (map.Cols * map.ZoneTiles) / size, rows = (map.Rows * map.ZoneTiles) / size;
            int rx0 = map.OriginX / size, ry0 = map.OriginY / size;

            // Cells with core land, then the ring around them so the coast and the edge of the built
            // world are drawn rather than left to the unbuilt-land plane.
            var core = new bool[cols, rows];
            for (int i = 0; i < map.Count; i++)
                if (map.IsCore(i))
                {
                    int col = i % map.Cols, row = i / map.Cols;
                    core[col * map.ZoneTiles / size, row * map.ZoneTiles / size] = true;
                }
            for (int cy = 0; cy < rows; cy++)
                for (int cx = 0; cx < cols; cx++)
                {
                    bool wanted = false;
                    for (int dy = -1; dy <= 1 && !wanted; dy++)
                        for (int dx = -1; dx <= 1 && !wanted; dx++)
                            wanted = cx + dx >= 0 && cy + dy >= 0 && cx + dx < cols && cy + dy < rows && core[cx + dx, cy + dy];
                    if (!wanted) continue;
                    int x0 = map.OriginX + cx * size, y0 = map.OriginY + cy * size;
                    BuiltRegion? over = built.FirstOrDefault(b => b.X0 < x0 + size && x0 < b.X1 && b.Y0 < y0 + size && y0 < b.Y1);
                    if (over != null)
                    {
                        if (over.X0 != x0 || over.Y0 != y0 || over.X1 != x0 + size || over.Y1 != y0 + size)
                            result.Warnings.Add($"region {rx0 + cx},{ry0 + cy} is left out: '{over.Id}' covers part of it (built regions should fill whole 96-tile cells).");
                        continue;
                    }
                    result.Regions.Add(Cell(map, ground, rx0 + cx, ry0 + cy, x0, y0, size));
                }
            return result;
        }

        /// <summary>
        /// The zone grid's ground with the rivers, bridges and passes taken out (each takes the ground
        /// of the nearest zone without one): those are drawn from their lines, at their real width.
        /// </summary>
        private static byte[] BaseGround(ContinentMap map)
        {
            var lined = new HashSet<string> { map.Data.RiverGround, map.Data.BridgeGround, map.Data.PassGround };
            var ground = (byte[])map.Ground.Clone();
            var done = new bool[map.Count];
            var queue = new Queue<int>();
            for (int i = 0; i < map.Count; i++)
                if (!lined.Contains(map.GroundIdAt(i))) { done[i] = true; queue.Enqueue(i); }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int col = i % map.Cols, row = i / map.Cols;
                foreach ((int dc, int dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    if (!map.InGrid(col + dc, row + dr)) continue;
                    int j = map.Index(col + dc, row + dr);
                    if (done[j]) continue;
                    done[j] = true;
                    ground[j] = ground[i];
                    queue.Enqueue(j);
                }
            }
            return ground;
        }

        private static RegionData Cell(ContinentMap map, byte[] ground, int rx, int ry, int x0, int y0, int size)
        {
            var tiles = new char[size, size]; // [x, y] from the south-west
            for (int ty = 0; ty < size; ty++)
                for (int tx = 0; tx < size; tx++)
                {
                    float x = x0 + tx + 0.5f, y = y0 + ty + 0.5f;
                    float wx = x + Warp * Noise(map.Data.Seed, x / WarpPeriod, y / WarpPeriod);
                    float wy = y + Warp * Noise(map.Data.Seed + 101, x / WarpPeriod, y / WarpPeriod);
                    (int col, int row) = map.ZoneOf(wx, wy);
                    col = Math.Max(0, Math.Min(map.Cols - 1, col));
                    row = Math.Max(0, Math.Min(map.Rows - 1, row));
                    int i = map.Index(col, row);
                    tiles[tx, ty] = map.Power[i] == ContinentMap.NoPower ? map.GroundDefs[ground[i]].Letter[0]
                        : !map.IsCore(i) ? Frontier
                        : map.GroundDefs[ground[i]].Letter[0];
                }

            char Letter(string groundId) => map.Data.Grounds[groundId].Letter[0];
            char river = Letter(map.Data.RiverGround), bridge = Letter(map.Data.BridgeGround), pass = Letter(map.Data.PassGround);
            var water = new HashSet<char>(map.Data.Grounds.Values.Where(g => g.Water).Select(g => g.Letter[0]));
            void Paint(IEnumerable<(int x, int y)> at, Func<char, char?> to)
            {
                foreach ((int x, int y) in at)
                {
                    char? c = to(tiles[x - x0, y - y0]);
                    if (c.HasValue) tiles[x - x0, y - y0] = c.Value;
                }
            }
            IEnumerable<(int, int)> Along(IList<float[]> line, float halfWidth) => TileShapes.Along(line, halfWidth, x0, y0, size, size);

            // Rivers over the land (the sea and the frontier stay as they are), then passes and roads,
            // which cross water as a bridge or ford, and the squares where places will be built.
            foreach (RiverDef r in map.Data.Rivers)
                Paint(Along(r.Line, r.HalfWidth), c => water.Contains(c) || c == Frontier ? (char?)null : river);
            foreach (PassDef p in map.Data.Spine.Passes)
                Paint(Along(p.Line, p.HalfWidth), c => c == Frontier ? (char?)null : water.Contains(c) ? bridge : pass);
            foreach (RoadLine road in map.Roads)
            {
                if (road.Kind != "main" && road.Kind != "road") continue;
                char mark = road.Kind == "main" ? MainRoad : Road;
                List<float[]> line = road.Points.Select(p => new[] { p.x, p.y }).ToList();
                Paint(Along(line, road.Kind == "main" ? 2f : 1.5f), c => c == Frontier ? (char?)null : water.Contains(c) ? bridge : mark);
            }
            foreach (PlaceDef place in map.Data.Places)
            {
                int side = Square(place.Tier);
                int px = (int)Math.Floor(place.At[0]) - side / 2, py = (int)Math.Floor(place.At[1]) - side / 2;
                var square = new List<(int, int)>();
                for (int y = Math.Max(py, y0); y < Math.Min(py + side, y0 + size); y++)
                    for (int x = Math.Max(px, x0); x < Math.Min(px + side, x0 + size); x++) square.Add((x, y));
                char mark = place.IsSettlement ? Settlement : Landmark;
                Paint(square, c => c == Frontier ? (char?)null : mark);
            }

            var region = new RegionData
            {
                Id = RegionId(rx, ry),
                Title = Title(map, x0, y0, size),
                Scene = $"Assets/Scenes/Land/Land_{Key(rx)}_{Key(ry)}.unity",
                Origin = new[] { x0, y0 },
                Size = new[] { size, size },
                Seed = (int)(Geometry.Hash(map.Data.Seed, RegionId(rx, ry)) & 0x7FFFFFFF),
                Generated = Generator,
            };
            var legend = new SortedDictionary<string, TileKind>(StringComparer.Ordinal);
            var rowsText = new List<string>(size);
            for (int ty = size - 1; ty >= 0; ty--)
            {
                var row = new char[size];
                for (int tx = 0; tx < size; tx++)
                {
                    char c = tiles[tx, ty];
                    row[tx] = c;
                    string key = c.ToString();
                    if (legend.ContainsKey(key)) continue;
                    (string material, string palette, TileKind kind) = Kind(map, c);
                    legend[key] = kind;
                    region.Materials[material] = palette;
                }
                rowsText.Add(new string(row));
            }
            region.Pieces.Add(new Piece
            {
                Id = "ground",
                Kind = PieceKind.Tiles,
                Rect = new[] { x0, y0, x0 + size, y0 + size },
                Legend = legend,
                Rows = rowsText,
            });
            return region;
        }

        /// <summary>The side of the plain square standing in for a place, by its tier.</summary>
        public static int Square(string tier) => tier switch
        {
            "city" => 16,
            "town" => 12,
            "village" => 8,
            "hamlet" => 6,
            _ => 4,
        };

        /// <summary>What a character is: its material (and that material's palette colour) and its kind.</summary>
        private static (string material, string palette, TileKind kind) Kind(ContinentMap map, char c)
        {
            switch (c)
            {
                case MainRoad: return ("LandMainRoad", "Cobble", new TileKind { Material = "LandMainRoad", Height = 0.01f });
                case Road: return ("LandRoad", "Furrow", new TileKind { Material = "LandRoad", Height = 0.01f });
                case Settlement: return ("LandSettlement", "Stone", new TileKind { Material = "LandSettlement", Height = 0.02f });
                case Landmark: return ("LandLandmark", "BlockoutSigil", new TileKind { Material = "LandLandmark", Height = 0.02f });
                case Frontier: return ("LandFrontier", "DarkRock", new TileKind { Material = "LandFrontier", Blocks = true, Height = 0.6f });
            }
            KeyValuePair<string, GroundDef> g = map.Data.Grounds.First(e => e.Value.Letter[0] == c);
            string name = "Land" + string.Concat(g.Key.Split('-', '_').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w.Substring(1)));
            var kind = new TileKind { Material = name };
            if (g.Value.Water)
            {
                kind.Water = true;
                kind.Height = -0.15f;
            }
            else if (!g.Value.Walkable)
            {
                // The crest and the glaciers: a shelf of rock about a storey high (docs/world-scale.md 3).
                kind.Blocks = true;
                kind.Height = 1.6f;
            }
            else if (g.Key == map.Data.BridgeGround) kind.Height = 0.05f;
            return (name, g.Value.Palette, kind);
        }

        /// <summary>
        /// Shown on walking in: the biggest place in the region with a real name, or else the power
        /// that holds most of its core land. Placeholder ("p-") and hidden names never reach players.
        /// </summary>
        private static string Title(ContinentMap map, int x0, int y0, int size)
        {
            string[] tiers = { "city", "town", "village", "hamlet" };
            PlaceDef? place = map.Data.Places
                .Where(p => p.IsSettlement && !p.Hidden && !p.Name.StartsWith("p-", StringComparison.Ordinal) && p.Name.Length > 0)
                .Where(p => p.At[0] >= x0 && p.At[0] < x0 + size && p.At[1] >= y0 && p.At[1] < y0 + size)
                .OrderBy(p => Array.IndexOf(tiers, p.Tier)).FirstOrDefault();
            if (place != null) return place.Name;

            var counts = new Dictionary<string, int>();
            bool anyLand = false;
            for (int y = y0; y < y0 + size; y += map.ZoneTiles)
                for (int x = x0; x < x0 + size; x += map.ZoneTiles)
                {
                    (int col, int row) = map.ZoneOf(x, y);
                    if (!map.InGrid(col, row)) continue;
                    int i = map.Index(col, row);
                    if (map.Power[i] == ContinentMap.NoPower) continue;
                    anyLand = true;
                    if (!map.IsCore(i)) continue;
                    string power = map.PowerIdAt(i)!;
                    counts[power] = counts.TryGetValue(power, out int n) ? n + 1 : 1;
                }
            if (counts.Count == 0) return anyLand ? "The frontier" : "The sea";
            string name = map.Data.Powers[counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal).First().Key].Name;
            int bracket = name.IndexOf(" (", StringComparison.Ordinal);
            return bracket > 0 ? name.Substring(0, bracket) : name;
        }

        /// <summary>Smooth value noise in [-1, 1], the same for a seed on every run.</summary>
        private static float Noise(int seed, float x, float y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            float sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            float Lattice(int a, int b) => (Geometry.Hash(Geometry.Hash((uint)seed, (uint)a), (uint)b) & 0xFFFF) / 32767.5f - 1f;
            float top = Lattice(ix, iy) + (Lattice(ix + 1, iy) - Lattice(ix, iy)) * sx;
            float bottom = Lattice(ix, iy + 1) + (Lattice(ix + 1, iy + 1) - Lattice(ix, iy + 1)) * sx;
            return top + (bottom - top) * sy;
        }

        // ------------------------------------------------------------------ files

        /// <summary>What writing the blockout would change in a regions folder: files to write (path, text) and to delete.</summary>
        public static (Dictionary<string, string> write, List<string> delete) Changes(string regionsDirectory, Result result)
        {
            var write = new Dictionary<string, string>(StringComparer.Ordinal);
            var delete = new List<string>();
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (RegionData region in result.Regions)
            {
                string path = Path.Combine(regionsDirectory, region.Id + ".json");
                wanted.Add(Path.GetFullPath(path));
                if (File.Exists(path) && !IsGenerated(path))
                {
                    result.Warnings.Add($"{region.Id}.json was taken over by hand (no \"generated\"); left as it is.");
                    continue;
                }
                string text = RegionFiles.Format(region);
                if (!File.Exists(path) || File.ReadAllText(path).Replace("\r\n", "\n") != text) write[path] = text;
            }
            if (Directory.Exists(regionsDirectory))
                foreach (string path in RegionFiles.RegionPaths(regionsDirectory))
                    if (!wanted.Contains(Path.GetFullPath(path)) && IsGenerated(path)) delete.Add(path);
            return (write, delete);
        }

        private static bool IsGenerated(string path)
        {
            try
            {
                return RegionFiles.LoadRegion(path).Generated == Generator;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
