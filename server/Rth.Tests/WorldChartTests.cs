using Rth.Server;
using Rth.Server.Content;
using Rth.Shared.Net;
using Rth.Shared.World;
using Rth.Shared.World.Layout;

namespace Rth.Tests;

/// <summary>The world map: the continent layout loaded by the server and sent to players as a <see cref="WorldChart"/>.</summary>
public class WorldChartTests
{
    // Land from x 8 to 184, held by "a" (west) and "b" (east); a river across the west; a road
    // from a named town to a placeholder village; a named and a placeholder landmark.
    private const string Small = """
        {
          "zoneTiles": 8, "regionTiles": 96, "bounds": [0, 0, 192, 96], "seed": 3,
          "grounds": {
            "sea":    { "letter": "~", "name": "Sea", "map": "#24476b", "water": true, "walkable": false, "height": 0 },
            "lake":   { "letter": "l", "water": true, "walkable": false, "height": 0 },
            "river":  { "letter": "r", "water": true, "walkable": false, "height": 0 },
            "bridge": { "letter": "b", "height": 1 },
            "pass":   { "letter": "p", "height": 8 },
            "plain":  { "letter": "g", "name": "Plain", "map": "#7f9e4c", "height": 2 },
            "wood":   { "letter": "w", "name": "Wood", "palette": "GraveTurf", "height": 3 }
          },
          "powers": {
            "a": { "name": "The west (for now)", "colour": "#a03a3a", "ground": "wood", "label": [40, 60] },
            "b": { "name": "p-east", "ground": "plain" },
            "wild": { "name": "Wild", "ground": "plain" }
          },
          "tiers": { "town": { "built": 500, "across": 20 }, "village": { "built": 200, "across": 10 } },
          "land": [ { "id": "land", "poly": [[8, 8], [184, 8], [184, 88], [8, 88]] } ],
          "realms": [
            { "id": "west", "power": "a", "poly": [[0, 0], [96, 0], [96, 96], [0, 96]] },
            { "id": "east", "power": "b", "poly": [[96, 0], [192, 0], [192, 96], [96, 96]] }
          ],
          "spine": { "line": [[400, 0], [400, 96]], "passes": [] },
          "rivers": [ { "id": "r1", "name": "the brook", "line": [[48, 88], [48, 8]], "halfWidth": 2, "into": "sea" } ],
          "places": [
            { "id": "tolling", "name": "Tolling", "tier": "town", "at": [30, 48], "capital": true },
            { "id": "p-farm", "name": "p-farm", "tier": "village", "at": [150, 48] },
            { "id": "mill", "name": "the mill", "tier": "landmark", "at": [70, 30] },
            { "id": "p-secret", "name": "a hidden cell", "tier": "landmark", "at": [120, 70] },
            { "id": "vault", "name": "The Vault", "tier": "landmark", "at": [130, 20], "hidden": true }
          ],
          "roads": [ { "id": "road", "kind": "main", "line": ["tolling", [90, 48], "p-farm"] } ]
        }
        """;

    private static WorldChart Chart()
    {
        ContinentMap map = ContinentCompiler.Compile(ContinentData.Parse(Small));
        Assert.Empty(map.Errors);
        return WorldChart.From(map);
    }

    [Fact]
    public void RunLengthEncodingRoundTrips()
    {
        byte[] values = { 0, 0, 0, 1, 51, 51, WorldChart.NoPower, WorldChart.NoPower, 2, 0 };
        string text = WorldChart.Encode(values);
        Assert.Equal("3AB2z2~CA", text);
        Assert.Equal(values, WorldChart.Decode(text, values.Length));

        var big = new byte[50000];
        for (int i = 0; i < big.Length; i++) big[i] = (byte)(i / 997 % 7);
        Assert.Equal(big, WorldChart.Decode(WorldChart.Encode(big), big.Length));

        Assert.Throws<FormatException>(() => WorldChart.Decode("3A", 4));   // too short
        Assert.Throws<FormatException>(() => WorldChart.Decode("5A", 4));   // too long
        Assert.Throws<FormatException>(() => WorldChart.Decode("2A!", 3));  // not a symbol
    }

    [Fact]
    public void TheChartKeepsTheLayoutsGridAndGroundsInTheirColours()
    {
        WorldChart chart = Chart();
        Assert.Equal((0, 0, 24, 12), (chart.OriginX, chart.OriginY, chart.Cols, chart.Rows));
        Assert.Equal(192, chart.WidthTiles);
        Assert.Equal("Sea", chart.GroundAt(2, 2)!.Name);
        Assert.Null(chart.PowerAt(2, 2));
        Assert.Equal("Wood", chart.GroundAt(30, 30)!.Name);
        Assert.Equal("GraveTurf", chart.GroundAt(30, 30)!.Colour); // no map colour: the palette name
        Assert.Equal("#7f9e4c", chart.GroundAt(150, 30)!.Colour);
        Assert.Equal("a", chart.PowerAt(30, 30)!.Id);
        Assert.Equal("b", chart.PowerAt(150, 30)!.Id);
        Assert.Null(chart.GroundAt(-1, 5));
    }

    [Fact]
    public void RiversAndRoadsAreLinesOverTheGroundAroundThem()
    {
        WorldChart chart = Chart();
        // The river's zones carry the wood about them; the river itself is a line.
        Assert.Equal("Wood", chart.GroundAt(48, 50)!.Name);
        ChartLine river = Assert.Single(chart.Rivers);
        Assert.Equal("The brook", river.Name);
        Assert.Equal(4f, river.Width);
        Assert.Equal(new float[] { 48, 88, 48, 8 }, river.Points);

        ChartLine road = Assert.Single(chart.Roads);
        Assert.Equal("main", road.Kind);
        Assert.Equal(new float[] { 30, 48, 90, 48, 150, 48 }, road.Points);
    }

    [Fact]
    public void PlayersNeverReadAPlaceholderOrASecret()
    {
        WorldChart chart = Chart();
        Assert.Equal(new[] { "tolling", "p-farm", "mill" }, chart.Places.Select(p => p.Id));
        Assert.Equal("Tolling", chart.Places[0].Name);
        Assert.True(chart.Places[0].Capital);
        Assert.Equal("", chart.Places[1].Name);      // an unnamed mark
        Assert.Equal("village", chart.Places[1].Tier);
        Assert.Equal("The mill", chart.Places[2].Name);
        Assert.Equal("The west", chart.Powers.Single(p => p.Id == "a").Name);
        Assert.Equal("", chart.Powers.Single(p => p.Id == "b").Name);

        Assert.True(WorldChart.IsPlaceholder("p-lakeport"));
        Assert.True(WorldChart.IsPlaceholder("P-Head (the falls)"));
        Assert.False(WorldChart.IsPlaceholder("Palmerway"));
    }

    [Fact]
    public void TheShippedLayoutMakesAChartWithNoPlaceholderNames()
    {
        var log = new List<string>();
        WorldChart? chart = ChartLoader.Load(LayoutDirectory, log.Add);
        Assert.NotNull(chart);
        Assert.Contains(log, l => l.StartsWith("World map:"));
        Assert.Contains(chart!.Places, p => p.Name == "Tolling" && p.Tier == "town");
        Assert.Contains(chart.Places, p => p.Name == "Cruor" && p.Tier == "city");
        Assert.Contains(chart.Roads, r => r.Kind == "pass");
        IEnumerable<string> names = chart.Places.Select(p => p.Name)
            .Concat(chart.Powers.Select(p => p.Name))
            .Concat(chart.Rivers.Concat(chart.Roads).Select(l => l.Name));
        Assert.DoesNotContain(names, n => n.Contains("p-", StringComparison.OrdinalIgnoreCase));
        Assert.All(chart.Grounds, g => Assert.NotNull(ChartPainter.Parse(g.Colour)));

        // Small enough to send with the content at every login.
        string json = ContentInfoMessage.From(new ClientContent { Chart = chart }).Json;
        Assert.InRange(json.Length, 1000, 64 * 1024);
    }

    [Fact]
    public void AMissingOrBrokenLayoutOnlyCostsTheMap()
    {
        var log = new List<string>();
        Assert.Null(ChartLoader.Load("", log.Add));
        Assert.Null(ChartLoader.Load(Path.Combine(Path.GetTempPath(), "rth-no-layout-" + Guid.NewGuid()), log.Add));
        string dir = Directory.CreateTempSubdirectory("rth-layout").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, ChartLoader.LayoutFile), "{ not json");
            Assert.Null(ChartLoader.Load(dir, log.Add));
            File.WriteAllText(Path.Combine(dir, ChartLoader.LayoutFile), """{ "bounds": [0, 0, 5, 5] }""");
            Assert.Null(ChartLoader.Load(dir, log.Add));
            File.WriteAllText(Path.Combine(dir, ChartLoader.LayoutFile), Small);
            Assert.NotNull(ChartLoader.Load(dir, log.Add));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
        Assert.Equal(4, log.Count); // no folder given: nothing to say
    }

    [Fact]
    public void PlayersGetTheChartWithTheContentAtLoginAndAfterAReload()
    {
        var fx = new WorldFixture(Maps.Open(20, 20, new TileCoord(5, 5)), new ServerConfig());
        fx.World.Chart = Chart();
        FakeConnection client = fx.Login("Alice");
        fx.Ticks(1);
        fx.World.QueueContent(TestContent.Create());
        fx.Ticks(1);

        List<ContentInfoMessage> sent = client.Sent.OfType<ContentInfoMessage>().ToList();
        Assert.Equal(2, sent.Count);
        foreach (ContentInfoMessage m in sent)
        {
            WorldChart? chart = m.Parse().Chart;
            Assert.NotNull(chart);
            Assert.Equal("Wood", chart!.GroundAt(30, 30)!.Name);
            Assert.Equal("b", chart.PowerAt(150, 30)!.Id);
            Assert.Equal(3, chart.Places.Count);
        }
    }

    [Fact]
    public void WithoutALayoutThereIsNoChart()
    {
        var fx = new WorldFixture(Maps.Open(20, 20, new TileCoord(5, 5)), new ServerConfig());
        FakeConnection client = fx.Login("Alice");
        fx.Ticks(1);
        Assert.Null(client.Sent.OfType<ContentInfoMessage>().First().Parse().Chart);
    }

    [Fact]
    public void ThePainterGivesOnePixelATileWithWaterAndLandApart()
    {
        WorldChart chart = Chart();
        byte[] rgba = ChartPainter.Paint(chart, name => name == "GraveTurf" ? new ChartPainter.Rgb(62, 107, 54) : null, 1, out int w, out int h);
        Assert.Equal((192, 96), (w, h));
        Assert.Equal(w * h * 4, rgba.Length);
        (byte r, byte g, byte b) At(int x, int y) => (rgba[(y * w + x) * 4], rgba[(y * w + x) * 4 + 1], rgba[(y * w + x) * 4 + 2]);
        var sea = At(1, 1);
        var wood = At(30, 30);
        Assert.True(sea.b > sea.g && sea.b > sea.r, "the sea is blue");
        Assert.True(wood.g > wood.b && wood.g > wood.r, "the wood is green");
        Assert.All(Enumerable.Range(0, w * h), i => Assert.Equal(255, rgba[i * 4 + 3]));

        byte[] draft = ChartPainter.Paint(chart, null, 4, out int dw, out int dh);
        Assert.Equal((48, 24), (dw, dh));
        Assert.Equal(dw * dh * 4, draft.Length);
    }

    private static string LayoutDirectory => Path.Combine(Path.GetDirectoryName(Repo.ServerDirectory)!, "world", "layout");
}
