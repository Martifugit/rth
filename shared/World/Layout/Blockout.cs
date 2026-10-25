using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rth.Shared.World.Data;

namespace Rth.Shared.World.Layout
{
    /// <summary>
    /// The walkable blockout (L2, docs/layout.md section 8; docs/world-plan.md): every 96-tile region
    /// of the layout that holds core land, and the ring of sea and frontier around them, as region
    /// data drawn from the <see cref="Landscape"/>. Each is one painted <see cref="PieceKind.Tiles"/>
    /// piece (<see cref="Piece.Paint"/>: underlays blended, roads, paving and water crisp with
    /// diagonal shapes) with a ring of its neighbours' tiles so the colours blend across borders, and
    /// its terrain. No trees, rocks or props. Frontier land is drawn but blocked; the built regions
    /// (Tolling, Palmerway, Selenfall) are left alone, and the land blends into their edges.
    /// </summary>
    public static class Blockout
    {
        /// <summary>The <see cref="RegionData.Generated"/> tag of the regions this writes.</summary>
        public const string Generator = "blockout";

        /// <summary>How far, in tiles, borders wander from the zone grid, and over what distance.</summary>
        public const float Warp = 5f, WarpPeriod = 13f;

        /// <summary>Tiles of the neighbours' ground each region keeps round it for blending.</summary>
        public const int Margin = 4;

        public sealed class Result
        {
            public List<RegionData> Regions = new List<RegionData>();
            public List<string> Warnings = new List<string>();
            /// <summary>The routed roads and wound rivers, for the world map.</summary>
            public Landscape? Land;
        }

        public static string RegionId(int rx, int ry) => $"land_{Key(rx)}_{Key(ry)}";
        private static string Key(int v) => v < 0 ? "m" + -v : v.ToString();

        public static Result Generate(ContinentMap map, IReadOnlyList<BuiltRegion> built, HeightEdits? edits = null)
        {
            var result = new Result();
            Landscape land = Landscape.Build(map, built, edits);
            result.Land = land;
            result.Warnings.AddRange(land.Warnings);
            int size = map.Data.RegionTiles;
            foreach ((int cx, int cy) in Cells(map, built, result.Warnings))
            {
                int x0 = map.OriginX + cx * size, y0 = map.OriginY + cy * size;
                RegionData region = Region(map, land, map.OriginX / size + cx, map.OriginY / size + cy, x0, y0, size);
                Heightfield? field = land.Heights.Field(x0, y0, size, size);
                if (field != null) region.Terrain = TerrainText.Write(field);
                result.Regions.Add(region);
            }
            return result;
        }

        /// <summary>
        /// The generated land's terrain over the whole layout: what a generated region carries, and
        /// where a region taken over starts (`tools/world terrain ... layout`).
        /// </summary>
        public static LandHeights Heights(ContinentMap map, IReadOnlyList<BuiltRegion> built, HeightEdits? edits = null) =>
            Landscape.Build(map, built, edits).Heights;

        /// <summary>Cells with core land, and the ring around them, so the coast and the edge of the built world are drawn; built regions left out.</summary>
        private static List<(int, int)> Cells(ContinentMap map, IReadOnlyList<BuiltRegion> built, List<string> warnings)
        {
            int size = map.Data.RegionTiles;
            int cols = (map.Cols * map.ZoneTiles) / size, rows = (map.Rows * map.ZoneTiles) / size;
            int rx0 = map.OriginX / size, ry0 = map.OriginY / size;
            // Every cell with land (the frontier too: seen from the passes and coasts, it must be there
            // rather than the unbuilt land under the world), and the ring of sea round them.
            var core = new bool[cols, rows];
            for (int i = 0; i < map.Count; i++)
                if (map.IsLand(i))
                {
                    int col = i % map.Cols, row = i / map.Cols;
                    core[col * map.ZoneTiles / size, row * map.ZoneTiles / size] = true;
                }
            var cells = new List<(int, int)>();
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
                            warnings.Add($"region {rx0 + cx},{ry0 + cy} is left out: '{over.Id}' covers part of it (built regions should fill whole 96-tile cells).");
                        continue;
                    }
                    cells.Add((cx, cy));
                }
            return cells;
        }

        private static RegionData Region(ContinentMap map, Landscape land, int rx, int ry, int x0, int y0, int size)
        {
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
            char sea = map.Data.Grounds[map.Data.SeaGround].Letter[0];
            char At(int x, int y) => land.InBounds(x, y) ? land.Char(land.Index(x, y)) : sea;

            var used = new HashSet<char>();
            var rows = new List<string>(size);
            for (int y = y0 + size - 1; y >= y0; y--)
            {
                var row = new char[size];
                for (int x = 0; x < size; x++) used.Add(row[x] = At(x0 + x, y));
                rows.Add(new string(row));
            }
            // The neighbours' tiles all round: full rows along the north and south, the west and east parts between.
            var surround = new List<string>(size + 2 * Margin);
            for (int y = y0 + size + Margin - 1; y >= y0 - Margin; y--)
            {
                bool full = y >= y0 + size || y < y0;
                var row = new System.Text.StringBuilder(full ? size + 2 * Margin : 2 * Margin);
                for (int x = x0 - Margin; x < x0 + size + Margin; x++)
                {
                    if (!full && x >= x0 && x < x0 + size) continue;
                    char c = At(x, y);
                    used.Add(c);
                    row.Append(c);
                }
                surround.Add(row.ToString());
            }

            var legend = new SortedDictionary<string, TileKind>(StringComparer.Ordinal);
            foreach (char c in used.OrderBy(c => c))
            {
                (TileKind kind, Dictionary<string, string> materials) = Kind(map, land, c);
                legend[c.ToString()] = kind;
                foreach (KeyValuePair<string, string> m in materials) region.Materials[m.Key] = m.Value;
            }
            region.Pieces.Add(new Piece
            {
                Id = "ground",
                Kind = PieceKind.Tiles,
                Rect = new[] { x0, y0, x0 + size, y0 + size },
                Legend = legend,
                Rows = rows,
                Paint = true,
                Margin = Margin,
                Surround = surround,
            });
            return region;
        }

        /// <summary>The side of the plain square standing in for a place, by its tier (the paved middle, in tiles).</summary>
        public static int Square(string tier) => (int)Math.Round(Landscape.SquareRadius(new PlaceDef { Tier = tier }) * 2f);

        /// <summary>What a character is, and the materials (with their palette colours) it uses.</summary>
        private static (TileKind kind, Dictionary<string, string> materials) Kind(ContinentMap map, Landscape land, char c)
        {
            (string id, bool beyond) = land.Decode(c);
            return KindOf(map.Data, id, beyond);
        }

        /// <summary>
        /// The tile kind a ground of the layout is drawn as, and the materials (with their palette
        /// colours) it uses: what the blockout writes, and what the Editor's ground brush paints.
        /// </summary>
        public static (TileKind kind, Dictionary<string, string> materials) KindOf(ContinentData data, string id, bool beyond = false)
        {
            var materials = new Dictionary<string, string>();
            var g = new KeyValuePair<string, GroundDef>(id, data.Grounds[id]);
            if (beyond)
            {
                // Land beyond the first build: its own ground, hazed by the painter, and blocked.
                string far = "LandFar" + Pascal(g.Key);
                materials[far] = g.Value.Palette;
                return (new TileKind { Material = far, Blocks = true, Beyond = true }, materials);
            }
            string name = "Land" + Pascal(g.Key);
            materials[name] = g.Value.Palette;
            var kind = new TileKind { Material = name };
            if (g.Value.Water)
            {
                kind.Water = true;
                kind.Height = -0.15f;
            }
            else if (!g.Value.Walkable && data.Terrain?.RidgeGrounds.Contains(g.Key) != true)
            {
                // Ground the layout can't cross and the terrain doesn't raise (lava, a rock shelf):
                // blocked, and standing a storey above the land (docs/world-scale.md 3). The crest and
                // glaciers are ordinary ground: only their steepness blocks them, as cliffs.
                kind.Blocks = true;
                kind.Height = 1.6f;
            }
            else if (g.Key == data.BridgeGround) kind.Height = 0.05f;
            if (g.Value.Layer == "over") kind.Layer = "over";
            if (g.Value.Cliff != null)
            {
                string cliff = "LandCliff" + Pascal(g.Value.Cliff);
                materials[cliff] = g.Value.Cliff;
                kind.Cliff = cliff;
            }
            return (kind, materials);
        }

        public static string Pascal(string id) => string.Concat(id.Split('-', '_').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w.Substring(1)));

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

        // ------------------------------------------------------------------ files

        /// <summary>
        /// What writing the blockout would change in a regions folder: files to write (path, text) and
        /// to delete. Built regions are left alone but for their ring of the neighbours' tiles
        /// (<see cref="Piece.Surround"/>), which follows the land round them.
        /// </summary>
        public static (Dictionary<string, string> write, List<string> delete) Changes(string regionsDirectory, Result result)
        {
            var write = new Dictionary<string, string>(StringComparer.Ordinal);
            var delete = new List<string>();
            var wanted = new HashSet<string>(StringComparer.Ordinal);

            // The rings of the neighbours' tiles, from what the world really holds: where the land
            // meets a built region, its own painted ground, both ways (Ground.GroundEdit).
            var built = new List<(RegionData region, string path)>();
            if (Directory.Exists(regionsDirectory))
                foreach (string path in RegionFiles.RegionPaths(regionsDirectory))
                {
                    RegionData r;
                    try { r = RegionFiles.LoadRegion(path); }
                    catch (FormatException) { continue; }
                    if (r.Generated == null && !r.Instanced && Ground.GroundEdit.Tiles(r) != null) built.Add((r, path));
                }
            var world = result.Regions.Concat(built.Select(b => b.region)).ToList();
            Ground.GroundEdit.RefreshSurrounds(world, new HashSet<string>(result.Regions.Select(r => r.Id), StringComparer.Ordinal));
            var ringed = new HashSet<string>(Ground.GroundEdit.RefreshSurrounds(world, new HashSet<string>(built.Select(b => b.region.Id), StringComparer.Ordinal)), StringComparer.Ordinal);
            foreach ((RegionData region, string path) in built)
                if (ringed.Contains(region.Id))
                {
                    string text = RegionFiles.Format(region);
                    if (File.ReadAllText(path).Replace("\r\n", "\n") != text) write[path] = text;
                }

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
            // Where the roads and rivers really run, for the world map, beside the layout.
            if (result.Land != null)
            {
                string routes = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(regionsDirectory))!, "layout", LandRoutes.FileName);
                string text = LandRoutes.From(result.Land).ToJson();
                if (!File.Exists(routes) || File.ReadAllText(routes).Replace("\r\n", "\n") != text) write[routes] = text;
            }
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
