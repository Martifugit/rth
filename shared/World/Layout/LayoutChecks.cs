using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Rth.Shared.World.Layout
{
    public enum CheckStatus { Ok, Info, Warn, Error }

    public sealed class CheckResult
    {
        public string Id = "";
        public CheckStatus Status;
        public string Text = "";
        public override string ToString() => $"{Status.ToString().ToLowerInvariant(),-5} {Id}: {Text}";
    }

    /// <summary>A region already built (world/regions), to check that the layout holds it.</summary>
    public sealed class BuiltRegion
    {
        public string Id = "";
        public int X0, Y0, X1, Y1;
        /// <summary>It has heights of its own; one without is flat, so the land around falls to 0 at its edge.</summary>
        public bool HasTerrain;
    }

    /// <summary>One 96-tile region of the grid laid over the layout.</summary>
    public sealed class RegionCell
    {
        public int Rx, Ry;
        public int LandZones, CoreZones, Zones;
        public string Realm = "sea";
        public string Role = "";
        public string Key => Rx.ToString(CultureInfo.InvariantCulture) + "," + Ry.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>A route between two places: tiles walked, as the game counts them (one tile a step, diagonals too).</summary>
    public sealed class Route
    {
        public string From = "", To = "";
        public int Straight;
        public int Overland = -1;
        public int ByRoad = -1;
        public static string Minutes(int tiles, int tilesPerTick) => tiles < 0 ? "-" : (tiles * 0.6 / tilesPerTick / 60.0).ToString("0.0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The layout's checks (docs/layout.md): reach from Tolling, travel between capitals, size and
    /// region count, land per power, the settlement ladder, rivers' mouths and the built regions.
    /// </summary>
    public sealed class LayoutReport
    {
        public List<CheckResult> Results = new List<CheckResult>();
        public List<RegionCell> Regions = new List<RegionCell>();
        public List<Route> Routes = new List<Route>();
        public bool[] Reached = Array.Empty<bool>();
        public long CoreLand, FrontierLand, IslandLand, Walkable;
        public Dictionary<string, long> LandByPower = new Dictionary<string, long>();

        public bool HasErrors => Results.Any(r => r.Status == CheckStatus.Error);

        private void Add(string id, CheckStatus s, string text) => Results.Add(new CheckResult { Id = id, Status = s, Text = text });

        public static LayoutReport Run(ContinentMap map, IEnumerable<BuiltRegion>? built = null)
        {
            var report = new LayoutReport();
            foreach (string e in map.Errors) report.Add("compile", CheckStatus.Error, e);
            foreach (string w in map.Warnings) report.Add("compile", CheckStatus.Warn, w);
            if (map.Count == 0) return report;
            report.Measure(map);
            report.CheckPlaces(map);
            report.CheckReach(map);
            report.CheckRivers(map);
            report.CheckTravel(map);
            report.CheckSize(map);
            report.CheckShares(map);
            report.CheckLadder(map);
            report.BuildRegions(map, built?.ToList() ?? new List<BuiltRegion>());
            if (built != null) report.CheckBuilt(map, built);
            return report;
        }

        private void Measure(ContinentMap map)
        {
            int t = map.TilesPerZone;
            for (int i = 0; i < map.Count; i++)
            {
                if (!map.IsLand(i)) continue;
                if (map.IsCore(i))
                {
                    CoreLand += t;
                    string p = map.PowerIdAt(i)!;
                    LandByPower[p] = (LandByPower.TryGetValue(p, out long v) ? v : 0) + t;
                }
                else if (map.ByShip[i]) IslandLand += t;
                else FrontierLand += t;
                if (map.IsWalkable(i)) Walkable += t;
            }
        }

        private void CheckPlaces(ContinentMap map)
        {
            int bad = 0;
            foreach (PlaceDef p in map.Data.Places)
            {
                (int c, int r) = map.ZoneOf(p.At[0], p.At[1]);
                if (!map.InGrid(c, r)) { Add("places", CheckStatus.Error, $"{p.Id} is outside the layout's bounds"); bad++; continue; }
                int i = map.Index(c, r);
                if (!map.IsWalkable(i))
                {
                    Add("places", CheckStatus.Error, $"{p.Id} at ({p.At[0]}, {p.At[1]}) stands on {map.GroundIdAt(i)}, which can't be walked");
                    bad++;
                }
            }
            if (bad == 0) Add("places", CheckStatus.Ok, $"all {map.Data.Places.Count} places stand on walkable ground");
        }

        // ------------------------------------------------------------------ reach and routes

        /// <summary>Zones reachable on foot from a zone: 8 ways, no cutting corners past blocked zones, plus ferries.</summary>
        public static int[] Distances(ContinentMap map, int start, bool roadsOnly)
        {
            var dist = new int[map.Count];
            for (int k = 0; k < dist.Length; k++) dist[k] = int.MaxValue;
            if (start < 0) return dist;
            var ferries = Ferries(map);
            bool Ok(int i) => map.IsWalkable(i) && (!roadsOnly || map.Road[i]);
            // Uniform step costs (one zone) except ferries, so a bucketed Dijkstra.
            var heap = new SortedSet<(int d, int i)>();
            dist[start] = 0;
            heap.Add((0, start));
            while (heap.Count > 0)
            {
                var (d, i) = heap.Min;
                heap.Remove(heap.Min);
                if (d > dist[i]) continue;
                int col = i % map.Cols, row = i / map.Cols;
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dc == 0 && dr == 0) continue;
                        int c = col + dc, r = row + dr;
                        if (!map.InGrid(c, r)) continue;
                        int j = map.Index(c, r);
                        if (!Ok(j)) continue;
                        // No cutting a corner past blocked ground (a road may cut past open ground).
                        if (dc != 0 && dr != 0 && (!map.IsWalkable(map.Index(col + dc, row)) || !map.IsWalkable(map.Index(col, row + dr)))) continue;
                        Relax(j, d + map.ZoneTiles);
                    }
                if (ferries.TryGetValue(i, out var links))
                    foreach (var (j, cost) in links)
                        if (Ok(j) || map.IsWalkable(j)) Relax(j, d + cost);
            }
            return dist;

            void Relax(int j, int nd)
            {
                if (nd >= dist[j]) return;
                if (dist[j] != int.MaxValue) heap.Remove((dist[j], j));
                dist[j] = nd;
                heap.Add((nd, j));
            }
        }

        private static Dictionary<int, List<(int to, int cost)>> Ferries(ContinentMap map)
        {
            var links = new Dictionary<int, List<(int, int)>>();
            foreach (RoadLine road in map.Roads.Where(r => r.Kind == "ferry" && r.Points.Count >= 2))
            {
                var a = road.Points[0];
                var b = road.Points[road.Points.Count - 1];
                int cost = 0;
                for (int k = 0; k + 1 < road.Points.Count; k++)
                    cost += (int)Math.Max(Math.Abs(road.Points[k + 1].x - road.Points[k].x), Math.Abs(road.Points[k + 1].y - road.Points[k].y));
                int ia = ZoneIndex(map, a.x, a.y), ib = ZoneIndex(map, b.x, b.y);
                if (ia < 0 || ib < 0) continue;
                void Link(int f, int t)
                {
                    if (!links.TryGetValue(f, out var l)) links[f] = l = new List<(int, int)>();
                    l.Add((t, cost));
                }
                Link(ia, ib);
                Link(ib, ia);
            }
            return links;
        }

        public static int ZoneIndex(ContinentMap map, float x, float y)
        {
            (int c, int r) = map.ZoneOf(x, y);
            return map.InGrid(c, r) ? map.Index(c, r) : -1;
        }

        private void CheckReach(ContinentMap map)
        {
            if (!map.Places.TryGetValue("tolling", out PlaceDef? tolling))
            {
                Add("I-REACH", CheckStatus.Error, "no place with id 'tolling' to start from");
                return;
            }
            int[] dist = Distances(map, ZoneIndex(map, tolling.At[0], tolling.At[1]), false);
            Reached = dist.Select(d => d != int.MaxValue).ToArray();
            var unreachedPlaces = map.Data.Places.Where(p => { int i = ZoneIndex(map, p.At[0], p.At[1]); return i < 0 || !Reached[i]; }).Select(p => p.Id).ToList();
            long lost = 0, walk = 0;
            var lostRegions = new SortedSet<string>();
            for (int i = 0; i < map.Count; i++)
            {
                if (!map.IsWalkable(i) || map.ByShip[i]) continue;
                walk += map.TilesPerZone;
                if (Reached[i]) continue;
                lost += map.TilesPerZone;
                (float x, float y) = map.Centre(i % map.Cols, i / map.Cols);
                lostRegions.Add($"{FloorDiv((int)x, map.Data.RegionTiles)},{FloorDiv((int)y, map.Data.RegionTiles)}");
            }
            if (unreachedPlaces.Count > 0)
                Add("I-REACH", CheckStatus.Error, "not reachable from Tolling on foot or by ferry: " + string.Join(", ", unreachedPlaces));
            else
                Add("I-REACH", CheckStatus.Ok, $"every place is reachable from Tolling over land, bridges, passes or ferries");
            double share = walk == 0 ? 0 : lost / (double)walk;
            CheckStatus s = share > map.Data.Targets.UnreachableShare ? CheckStatus.Warn : lost > 0 ? CheckStatus.Info : CheckStatus.Ok;
            Add("reach-land", s, lost == 0
                ? "all walkable land (islands aside) is reachable from Tolling"
                : $"{lost:N0} walkable tiles ({share:P1}) can't be reached from Tolling (cut off by rivers without a bridge, or by the crest), in regions " + string.Join(" ", lostRegions.Take(30)) + (lostRegions.Count > 30 ? " ..." : ""));
        }

        private void CheckRivers(ContinentMap map)
        {
            int bad = 0;
            foreach (RiverDef r in map.Data.Rivers)
            {
                float[] src = r.Line[0], mouth = r.Line[r.Line.Count - 1];
                int si = ZoneIndex(map, src[0], src[1]);
                if (si < 0 || !map.IsLand(si) || map.GroundIdAt(si) != map.Data.RiverGround && map.IsWater(si))
                {
                    Add("rivers", CheckStatus.Error, $"{r.Id} rises at ({src[0]}, {src[1]}), which isn't land");
                    bad++;
                }
                bool ok;
                string into = r.Into;
                int mi = ZoneIndex(map, mouth[0], mouth[1]);
                if (into == "sea") ok = Near(map, mi, i => map.GroundIdAt(i) == map.Data.SeaGround);
                else if (into.StartsWith("lake:", StringComparison.Ordinal))
                {
                    string id = into.Substring(5);
                    ShapeEntry? lake = map.Data.Areas.FirstOrDefault(a => a.Id == id);
                    ok = lake != null && Near(map, mi, i => map.GroundIdAt(i) == lake.Ground);
                }
                else if (into.StartsWith("river:", StringComparison.Ordinal))
                {
                    RiverDef? other = map.Data.Rivers.FirstOrDefault(o => o.Id == into.Substring(6));
                    ok = other != null && other != r && Geometry.DistanceToLine(other.Line, mouth[0], mouth[1]) <= map.ZoneTiles * 1.5f;
                }
                else ok = false;
                if (!ok)
                {
                    Add("rivers", CheckStatus.Error, $"{r.Id} should end in {into}, but its last point ({mouth[0]}, {mouth[1]}) doesn't reach it");
                    bad++;
                }
            }
            if (bad == 0) Add("rivers", CheckStatus.Ok, $"{map.Data.Rivers.Count} rivers rise on land and flow into the sea, a lake or another river");
        }

        private static bool Near(ContinentMap map, int i, Func<int, bool> test)
        {
            if (i < 0) return false;
            int col = i % map.Cols, row = i / map.Cols;
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                    if (map.InGrid(col + dc, row + dr) && test(map.Index(col + dc, row + dr))) return true;
            return false;
        }

        private void CheckTravel(ContinentMap map)
        {
            var capitals = map.Data.Places.Where(p => p.Capital).ToList();
            if (map.Places.TryGetValue("tolling", out PlaceDef? t) && !capitals.Contains(t)) capitals.Insert(0, t);
            var overland = new Dictionary<string, int[]>();
            var road = new Dictionary<string, int[]>();
            foreach (PlaceDef p in capitals)
            {
                int i = ZoneIndex(map, p.At[0], p.At[1]);
                overland[p.Id] = Distances(map, i, false);
                road[p.Id] = Distances(map, i, true);
            }
            for (int a = 0; a < capitals.Count; a++)
                for (int b = a + 1; b < capitals.Count; b++)
                {
                    PlaceDef pa = capitals[a], pb = capitals[b];
                    int ib = ZoneIndex(map, pb.At[0], pb.At[1]);
                    int o = overland[pa.Id][ib], rd = road[pa.Id][ib];
                    Routes.Add(new Route
                    {
                        From = pa.Id, To = pb.Id,
                        Straight = (int)Math.Max(Math.Abs(pa.At[0] - pb.At[0]), Math.Abs(pa.At[1] - pb.At[1])),
                        Overland = o == int.MaxValue ? -1 : o,
                        ByRoad = rd == int.MaxValue ? -1 : rd,
                    });
                }
            float[] range = map.Data.Targets.NearestCapital;
            var outside = new List<string>();
            foreach (PlaceDef p in capitals.Where(c => c.Capital))
            {
                var mine = Routes.Where(r => (r.From == p.Id || r.To == p.Id) && r.Overland >= 0
                    && map.Places[r.From == p.Id ? r.To : r.From].Capital).ToList();
                if (mine.Count == 0) { outside.Add(p.Id + " (no route)"); continue; }
                int nearest = mine.Min(r => r.Overland);
                if (nearest < range[0] || nearest > range[1]) outside.Add($"{p.Id} {nearest}");
            }
            Add("travel", outside.Count == 0 ? CheckStatus.Ok : CheckStatus.Warn, outside.Count == 0
                ? $"every capital's nearest other capital is {range[0]}-{range[1]} tiles away on foot (RuneScape's capitals: about 230-600)"
                : $"nearest capital outside {range[0]}-{range[1]} tiles: " + string.Join(", ", outside));
            var missing = Routes.Where(r => r.ByRoad < 0).Select(r => r.From + "-" + r.To).ToList();
            if (missing.Count > 0) Add("roads", CheckStatus.Info, "capitals with no road between them (not every pair needs one): " + string.Join(", ", missing));
        }

        private void CheckSize(ContinentMap map)
        {
            float[] r = map.Data.Targets.CoreTiles;
            Add("size", CoreLand >= r[0] && CoreLand <= r[1] ? CheckStatus.Ok : CheckStatus.Warn,
                $"core land {CoreLand / 1e6:0.00}M tiles (target {r[0] / 1e6:0.00}-{r[1] / 1e6:0.00}M; the 2004 game's continent is 0.73M); frontiers {FrontierLand / 1e6:0.00}M, islands {IslandLand / 1e6:0.00}M");
        }

        private void CheckShares(ContinentMap map)
        {
            var parts = new List<string>();
            bool off = false;
            foreach (var p in map.Data.Powers.Where(p => p.Value.Core))
            {
                long land = LandByPower.TryGetValue(p.Key, out long v) ? v : 0;
                double share = CoreLand == 0 ? 0 : land / (double)CoreLand;
                string range = "";
                if (p.Value.Share != null && p.Value.Share.Length == 2)
                {
                    bool inside = share >= p.Value.Share[0] - 1e-6 && share <= p.Value.Share[1] + 1e-6;
                    off |= !inside;
                    range = $" (guide {p.Value.Share[0]:P0}-{p.Value.Share[1]:P0}{(inside ? "" : ", OUTSIDE")})";
                }
                parts.Add($"{p.Key} {share:P1}{range}");
            }
            Add("shares", off ? CheckStatus.Warn : CheckStatus.Ok, "core land by power: " + string.Join("; ", parts));
        }

        private void CheckLadder(ContinentMap map)
        {
            var count = map.Data.Places.Where(p => p.IsSettlement).GroupBy(p => p.Tier).ToDictionary(g => g.Key, g => g.Count());
            int N(string tier) => count.TryGetValue(tier, out int v) ? v : 0;
            double per = CoreLand / 100000.0;
            LayoutTargets t = map.Data.Targets;
            double towns = N("town") / per, villages = N("village") / per;
            bool ok = towns >= t.Towns[0] && towns <= t.Towns[1] && villages >= t.Villages[0] && villages <= t.Villages[1];
            Add("T-LADDER", ok ? CheckStatus.Ok : CheckStatus.Warn,
                $"{N("city")} cities, {N("town")} towns, {N("village")} villages, {N("hamlet")} hamlets: {towns:0.00} towns and {villages:0.00} villages per 100k core tiles (targets {t.Towns[0]}-{t.Towns[1]} and {t.Villages[0]}-{t.Villages[1]})");

            long builtTiles = 0;
            foreach (PlaceDef p in map.Data.Places.Where(p => p.IsSettlement))
                builtTiles += p.Across.HasValue ? (long)p.Across.Value * p.Across.Value / 2 : map.Data.Tiers[p.Tier].Built;
            double built = CoreLand == 0 ? 0 : builtTiles / (double)CoreLand;
            Add("T-LAND-BUILT", built >= t.Built[0] && built <= t.Built[1] ? CheckStatus.Ok : CheckStatus.Info,
                $"estimated from tiers: {built:P0} of core land inside settlements (target {t.Built[0]:P0}-{t.Built[1]:P0}; pass 1 of the area plan refines it)");

            var cities = map.Data.Places.Where(p => p.Tier == "city").Select(p => (p, across: p.Across ?? map.Data.Tiers["city"].Across)).OrderByDescending(c => c.across).ToList();
            if (cities.Count >= 2 && cities[0].p.Id == "cruor")
            {
                double ratio = cities[0].across * (double)cities[0].across / (cities[1].across * (double)cities[1].across);
                Add("T-CITY-DOMINANCE", ratio >= t.CityDominance[0] && ratio <= t.CityDominance[1] ? CheckStatus.Ok : CheckStatus.Warn,
                    $"by area only: Cruor {cities[0].across} tiles across against {cities[1].p.Id} {cities[1].across}: {ratio:0.0}x (target {t.CityDominance[0]}-{t.CityDominance[1]}x)");
            }
            else Add("T-CITY-DOMINANCE", CheckStatus.Warn, "Cruor should be the largest city");
        }

        // ------------------------------------------------------------------ the region grid

        private static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);

        private void BuildRegions(ContinentMap map, List<BuiltRegion> built)
        {
            int rt = map.Data.RegionTiles, per = rt / map.ZoneTiles;
            int rx0 = FloorDiv(map.OriginX, rt), ry0 = FloorDiv(map.OriginY, rt);
            int rCols = map.Cols / per, rRows = map.Rows / per;
            for (int ry = 0; ry < rRows; ry++)
                for (int rx = 0; rx < rCols; rx++)
                {
                    var cell = new RegionCell { Rx = rx0 + rx, Ry = ry0 + ry, Zones = per * per };
                    var powers = new Dictionary<string, int>();
                    var grounds = new Dictionary<string, int>();
                    bool river = false, road = false, lake = false, coast = false;
                    for (int r = ry * per; r < (ry + 1) * per; r++)
                        for (int c = rx * per; c < (rx + 1) * per; c++)
                        {
                            int i = map.Index(c, r);
                            string g = map.GroundIdAt(i);
                            if (g == map.Data.SeaGround) { coast = true; continue; }
                            if (map.IsWater(i) && g != map.Data.RiverGround) lake = true;
                            if (g == map.Data.RiverGround || g == map.Data.BridgeGround) river = true;
                            road |= map.Road[i];
                            if (!map.IsLand(i)) continue;
                            cell.LandZones++;
                            if (map.IsCore(i)) cell.CoreZones++;
                            string p = map.PowerIdAt(i)!;
                            powers[p] = (powers.TryGetValue(p, out int pv) ? pv : 0) + 1;
                            grounds[g] = (grounds.TryGetValue(g, out int gv) ? gv : 0) + 1;
                        }
                    if (cell.LandZones * 10 < cell.Zones) { cell.Realm = "sea"; cell.Role = cell.LandZones > 0 ? "sea, with a scrap of coast" : "sea"; }
                    else
                    {
                        cell.Realm = powers.OrderByDescending(p => p.Value).First().Key;
                        cell.Role = Role(map, cell, grounds, river, road, lake, coast, built);
                    }
                    if (map.Data.RegionNotes.TryGetValue(cell.Key, out string? note)) cell.Role = note + (cell.Role.Length > 0 ? "; " + cell.Role : "");
                    if (cell.LandZones > 0) Regions.Add(cell);
                }
            int core = Regions.Count(r => r.CoreZones * 4 >= r.Zones);
            int any = Regions.Count(r => r.CoreZones > 0);
            Add("regions", CheckStatus.Info, $"{Regions.Count} regions of {rt} x {rt} hold land; {core} are mostly core land (a quarter or more), {any} touch it; the grid is {rCols} x {rRows}");
        }

        private static string Role(ContinentMap map, RegionCell cell, Dictionary<string, int> grounds, bool river, bool road, bool lake, bool coast, List<BuiltRegion> built)
        {
            int rt = map.Data.RegionTiles;
            int x0 = cell.Rx * rt, y0 = cell.Ry * rt;
            var bits = new List<string>();
            foreach (BuiltRegion b in built)
                if (b.X0 < x0 + rt && b.X1 > x0 && b.Y0 < y0 + rt && b.Y1 > y0) bits.Add("built: " + b.Id);
            var here = map.Data.Places.Where(p => p.At[0] >= x0 && p.At[0] < x0 + rt && p.At[1] >= y0 && p.At[1] < y0 + rt).ToList();
            string[] order = { "city", "town", "village", "hamlet", "landmark" };
            foreach (PlaceDef p in here.OrderBy(p => Array.IndexOf(order, p.Tier)))
                bits.Add($"{p.Tier} {(p.Name.Length > 0 ? p.Name : p.Id)}");
            foreach (PassDef p in map.Data.Spine.Passes)
                if (p.Line.Any(v => v[0] >= x0 && v[0] < x0 + rt && v[1] >= y0 && v[1] < y0 + rt) ||
                    Geometry.DistanceToLine(p.Line, x0 + rt / 2f, y0 + rt / 2f) < rt / 2f) bits.Add("pass " + p.Name);
            if (bits.Count == 0 || bits.All(b => b.StartsWith("pass", StringComparison.Ordinal)))
            {
                string main = grounds.OrderByDescending(g => g.Value).First().Key;
                bits.Add("open " + map.Data.Grounds[main].Name.ToLowerInvariant());
            }
            var features = new List<string>();
            if (road) features.Add("road");
            if (river) features.Add("river");
            if (lake) features.Add("lake");
            if (coast) features.Add("coast");
            return string.Join("; ", bits) + (features.Count > 0 ? " (" + string.Join(", ", features) + ")" : "");
        }

        private void CheckBuilt(ContinentMap map, IEnumerable<BuiltRegion> built)
        {
            foreach (BuiltRegion b in built)
            {
                int land = 0, all = 0;
                var powers = new Dictionary<string, int>();
                for (int y = b.Y0; y < b.Y1; y += map.ZoneTiles)
                    for (int x = b.X0; x < b.X1; x += map.ZoneTiles)
                    {
                        int i = ZoneIndex(map, x + 0.5f, y + 0.5f);
                        all++;
                        if (i < 0 || !map.IsLand(i)) continue;
                        land++;
                        string p = map.PowerIdAt(i)!;
                        powers[p] = (powers.TryGetValue(p, out int v) ? v : 0) + 1;
                    }
                string realm = powers.Count == 0 ? "sea" : powers.OrderByDescending(p => p.Value).First().Key;
                CheckStatus s = land * 2 >= all ? CheckStatus.Ok : CheckStatus.Error;
                Add("built", s, $"{b.Id} ({b.X0}-{b.X1 - 1} x {b.Y0}-{b.Y1 - 1}): {land * 100 / Math.Max(1, all)}% land in the layout, realm {realm}");
            }
        }

        // ------------------------------------------------------------------ text

        public string Text(ContinentMap map)
        {
            var sb = new StringBuilder();
            foreach (CheckResult r in Results) sb.AppendLine(r.ToString());
            sb.AppendLine();
            sb.AppendLine("Travel between capitals (tiles; minutes at 0.6 s a tick, walking 1 tile a tick, running 2):");
            sb.AppendLine($"  {"from",-16} {"to",-16} {"straight",8} {"on foot",8} {"by road",8} {"walk",6} {"run",6}");
            foreach (Route r in Routes)
            {
                int best = r.ByRoad >= 0 ? r.ByRoad : r.Overland;
                sb.AppendLine($"  {r.From,-16} {r.To,-16} {r.Straight,8} {(r.Overland < 0 ? "-" : r.Overland.ToString(CultureInfo.InvariantCulture)),8} {(r.ByRoad < 0 ? "-" : r.ByRoad.ToString(CultureInfo.InvariantCulture)),8} {Route.Minutes(best, 1),6} {Route.Minutes(best, 2),6}");
            }
            return sb.ToString();
        }

        /// <summary>The region grid as a table: one line per region with land, north first.</summary>
        public string RegionTable(ContinentMap map)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# The 96-tile region grid over world/layout/continent.json. Generated by `dotnet run --project tools/world -- layout`; do not edit.");
            sb.AppendLine("# region (rx,ry): tiles x0..x1, y0..y1; land% core%; realm; role. Tolling is 0,0. Notes come from regionNotes in continent.json.");
            int rt = map.Data.RegionTiles;
            foreach (RegionCell c in Regions.OrderByDescending(r => r.Ry).ThenBy(r => r.Rx))
                sb.AppendLine($"{c.Key,-7} x {c.Rx * rt,5}..{c.Rx * rt + rt - 1,5}  y {c.Ry * rt,5}..{c.Ry * rt + rt - 1,5}  land {c.LandZones * 100 / c.Zones,3}%  core {c.CoreZones * 100 / c.Zones,3}%  {c.Realm,-9} {c.Role}");
            return sb.ToString();
        }
    }
}
