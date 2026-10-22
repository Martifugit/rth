using Rth.Shared.World;
using Rth.Shared.World.Layout;

namespace Rth.Tests;

public class ContinentLayoutTests
{
    // Two regions side by side: land from x 8 to 184, a north-south spine at x 96 (its crest
    // blocks), the west held by "a" and the east by "b". Passes, rivers and roads are added per test.
    private const string Small = """
        {
          "zoneTiles": 8, "regionTiles": 96, "bounds": [0, 0, 192, 96], "seed": 3,
          "grounds": {
            "sea":    { "letter": "~", "water": true, "walkable": false, "height": 0 },
            "lake":   { "letter": "l", "water": true, "walkable": false, "height": 0 },
            "river":  { "letter": "r", "water": true, "walkable": false, "height": 0 },
            "bridge": { "letter": "b", "height": 1 },
            "pass":   { "letter": "p", "height": 8 },
            "plain":  { "letter": "g", "height": 2 },
            "crest":  { "letter": "m", "walkable": false, "height": 14 }
          },
          "powers": {
            "a": { "name": "A", "ground": "plain", "share": [0.4, 0.6] },
            "b": { "name": "B", "ground": "plain", "share": [0.4, 0.6] },
            "wild": { "name": "Wild", "ground": "plain" }
          },
          "tiers": { "city": { "built": 1000, "across": 40 }, "town": { "built": 500, "across": 20 } },
          "land": [ { "id": "land", "poly": [[8, 8], [184, 8], [184, 88], [8, 88]] } ],
          "realms": [
            { "id": "west", "power": "a", "poly": [[0, 0], [96, 0], [96, 96], [0, 96]] },
            { "id": "east", "power": "b", "poly": [[96, 0], [192, 0], [192, 96], [96, 96]] }
          ],
          "areas": [ { "id": "crest", "ground": "crest", "along": "spine", "halfWidth": 6 } ],
          "spine": { "line": [[96, -20], [96, 120]], "passes": [] },
          "places": [
            { "id": "tolling", "tier": "town", "at": [40, 48], "capital": true },
            { "id": "far", "tier": "city", "at": [150, 48], "capital": true }
          ],
          "targets": { "coreTiles": [1000, 100000], "nearestCapital": [10, 500] }
        }
        """;

    private static ContinentData Data(Action<ContinentData>? change = null)
    {
        ContinentData d = ContinentData.Parse(Small);
        change?.Invoke(d);
        return d;
    }

    private static (ContinentMap map, LayoutReport report) Run(Action<ContinentData>? change = null)
    {
        ContinentMap map = ContinentCompiler.Compile(Data(change));
        Assert.Empty(map.Errors);
        return (map, LayoutReport.Run(map));
    }

    private static CheckResult Result(LayoutReport r, string id) => r.Results.First(c => c.Id == id);

    [Fact]
    public void TheCrestCutsTheLandUntilAPassCrossesIt()
    {
        var (_, closed) = Run();
        Assert.Equal(CheckStatus.Error, Result(closed, "I-REACH").Status);
        Assert.Contains("far", Result(closed, "I-REACH").Text);

        var (map, open) = Run(d => d.Spine.Passes.Add(new PassDef { Id = "gap", Line = { new float[] { 80, 48 }, new float[] { 112, 48 } } }));
        Assert.Equal(CheckStatus.Ok, Result(open, "I-REACH").Status);
        Assert.Equal("pass", map.GroundIdAt(LayoutReport.ZoneIndex(map, 96, 48)));
        Assert.Equal("crest", map.GroundIdAt(LayoutReport.ZoneIndex(map, 96, 80)));
    }

    [Fact]
    public void TravelIsCountedInTilesTheWayTheGameWalks()
    {
        var (_, report) = Run(d => d.Spine.Passes.Add(new PassDef { Id = "gap", Line = { new float[] { 80, 48 }, new float[] { 112, 48 } } }));
        Route route = Assert.Single(report.Routes);
        Assert.Equal(110, route.Straight);
        Assert.InRange(route.Overland, 104, 120); // 8-tile zone steps along the pass
        Assert.Equal("1.1", Route.Minutes(110, 1));  // 110 ticks of 0.6 s
        Assert.Equal("0.6", Route.Minutes(110, 2));
    }

    [Fact]
    public void ARiverBlocksUntilARoadBridgesIt()
    {
        void River(ContinentData d)
        {
            d.Spine.Passes.Add(new PassDef { Id = "gap", Line = { new float[] { 80, 48 }, new float[] { 112, 48 } } });
            d.Rivers.Add(new RiverDef { Id = "r", Line = { new float[] { 140, 84 }, new float[] { 140, 0 } }, Into = "sea" });
        }
        var (_, blocked) = Run(River);
        Assert.Contains("far", Result(blocked, "I-REACH").Text);
        Assert.Equal(CheckStatus.Ok, Result(blocked, "rivers").Status);

        var (map, bridged) = Run(d =>
        {
            River(d);
            d.Roads.Add(new RoadDef { Id = "road", Line = { "tolling", "far" } });
        });
        Assert.Equal(CheckStatus.Ok, Result(bridged, "I-REACH").Status);
        Assert.Equal("bridge", map.GroundIdAt(LayoutReport.ZoneIndex(map, 140, 48)));
        Assert.True(bridged.Routes[0].ByRoad > 0);
    }

    [Fact]
    public void ARiverMustEndWhereItSays()
    {
        var (_, report) = Run(d => d.Rivers.Add(new RiverDef { Id = "r", Line = { new float[] { 140, 80 }, new float[] { 140, 40 } }, Into = "sea" }));
        Assert.Equal(CheckStatus.Error, Result(report, "rivers").Status);
    }

    [Fact]
    public void ARoadMayNotCrossTheCrestOrOpenWater()
    {
        ContinentData d = Data();
        d.Roads.Add(new RoadDef { Id = "over", Line = { "tolling", "far" } });
        ContinentMap map = ContinentCompiler.Compile(d);
        Assert.Contains(map.Errors, e => e.Contains("road over crosses crest"));
    }

    [Fact]
    public void SharesSizeAndRegionsAreMeasured()
    {
        var (_, report) = Run();
        Assert.Equal(22 * 10 * 64, report.CoreLand); // 176 x 80 tiles of land in 8-tile zones
        Assert.Equal(CheckStatus.Ok, Result(report, "shares").Status);
        Assert.Equal(2, report.Regions.Count);
        Assert.Equal("a", report.Regions.Single(r => r.Rx == 0).Realm);
        Assert.Equal("b", report.Regions.Single(r => r.Rx == 1).Realm);
        Assert.Contains("town tolling", report.Regions.Single(r => r.Rx == 0).Role);
    }

    [Fact]
    public void ScaleStretchesTheLayoutAboutItsAnchor()
    {
        ContinentData d = Data(x =>
        {
            x.Bounds = new[] { -96, -96, 288, 192 };
            x.Scale = 1.5f;
            x.Anchor = new float[] { 40, 48 };
        });
        ContinentMap map = ContinentCompiler.Compile(d);
        Assert.Empty(map.Errors);
        Assert.Equal(new float[] { 40, 48 }, map.Places["tolling"].At);
        Assert.Equal(new float[] { 205, 48 }, map.Places["far"].At); // 110 tiles away becomes 165
    }

    [Fact]
    public void BordersSharedByTwoRoughShapesMeetExactly()
    {
        var a = new List<float[]> { new float[] { 0, 0 }, new float[] { 200, 0 }, new float[] { 200, 200 } };
        var b = new List<float[]> { new float[] { 200, 200 }, new float[] { 200, 0 }, new float[] { 400, 100 } };
        List<float[]> ra = Geometry.Roughen(a, 30, 5), rb = Geometry.Roughen(b, 30, 5);
        // The edge (200, 0)-(200, 200): a walks it upwards, b downwards.
        List<float[]> Edge(List<float[]> pts, float[] from, float[] to)
        {
            int i = pts.FindIndex(p => p[0] == from[0] && p[1] == from[1]);
            int j = pts.FindIndex(p => p[0] == to[0] && p[1] == to[1]);
            return pts.Skip(i).Take(j - i + 1).ToList();
        }
        var up = Edge(ra, new float[] { 200, 0 }, new float[] { 200, 200 });
        var down = Edge(rb, new float[] { 200, 200 }, new float[] { 200, 0 });
        down.Reverse();
        Assert.True(up.Count > 4, "the edge was roughened");
        Assert.Equal(up.Count, down.Count);
        for (int k = 0; k < up.Count; k++) Assert.Equal(up[k], down[k]);
        Assert.Contains(up, p => Math.Abs(p[0] - 200) > 1); // and it does wander
    }

    [Fact]
    public void TheZoneGridIsTheTerrainBakesFormat()
    {
        var (map, _) = Run();
        WorldLayout layout = WorldLayout.Parse(map.ToLayoutJson());
        Assert.Equal(map.Cols, layout.Columns);
        Assert.Equal(map.Rows, layout.Rows);
        Assert.True(layout.BiomeAt(-50, -50).Water, "off the grid is sea");
        Assert.True(layout.BiomeAt(4, 4).Water, "outside the land is sea");
        Assert.False(layout.BiomeAt(40, 48).Water);
        Assert.False(layout.BiomeAt(96, 48).Walkable, "the crest");
        Assert.True(layout.MacroHeight(96, 48) > layout.MacroHeight(40, 48), "the crest stands higher");
    }

    // ------------------------------------------------------------------ the shipped layout

    private static LayoutWorkspace Shipped() => LayoutWorkspace.Load(Path.GetDirectoryName(Repo.ServerDirectory)!);

    [Fact]
    public void TheShippedLayoutPassesItsChecks()
    {
        LayoutWorkspace ws = Shipped();
        Assert.Empty(ws.Report.Results.Where(r => r.Status == CheckStatus.Error).Select(r => r.ToString()));
        Assert.Empty(ws.Stale().Select(p => Path.GetFileName(p) + " is out of date: run `dotnet run --project tools/world -- layout`"));
        Assert.InRange(ws.Report.CoreLand, ws.Data.Targets.CoreTiles[0], ws.Data.Targets.CoreTiles[1]);
    }

    [Fact]
    public void TheShippedLayoutHoldsTheBuiltRegionsInTheEmpire()
    {
        LayoutWorkspace ws = Shipped();
        Assert.Contains(ws.Built, b => b.Id == "tolling" && b.X0 == 0 && b.Y0 == 0);
        foreach (string key in new[] { "0,0", "1,0" })
        {
            RegionCell cell = ws.Report.Regions.Single(r => r.Key == key);
            Assert.Equal("empire", cell.Realm);
            Assert.True(cell.LandZones * 4 >= cell.Zones * 3, key + " is mostly land");
        }
        // Palmerway runs east towards Cruor; Tolling is on the empire's western edge.
        PlaceDef cruor = ws.Map.Places["cruor"];
        Assert.True(cruor.At[0] > 192);
        int west = LayoutReport.ZoneIndex(ws.Map, -60, 48);
        Assert.NotEqual("empire", ws.Map.PowerIdAt(west));
    }

    [Fact]
    public void TheShippedZoneGridParses()
    {
        LayoutWorkspace ws = Shipped();
        WorldLayout layout = WorldLayout.Parse(File.ReadAllText(ws.ZonesPath));
        Assert.Equal(0, layout.OriginX % layout.ZoneTiles);
        Assert.False(layout.BiomeAt(30, 48).Water, "Tolling stands on land");
    }
}
