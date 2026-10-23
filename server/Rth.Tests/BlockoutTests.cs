using Rth.Server.Content;
using Rth.Shared.World;
using Rth.Shared.World.Data;
using Rth.Shared.World.Layout;

namespace Rth.Tests;

/// <summary>The walkable blockout (L2, docs/layout.md): regions generated from the continent layout.</summary>
public class BlockoutTests
{
    private static string Root => Path.GetDirectoryName(Repo.ServerDirectory)!;

    [Fact]
    public void TheGeneratedRegionsAreUpToDateWithTheLayout()
    {
        LayoutWorkspace lw = LayoutWorkspace.Load(Root);
        Blockout.Result result = Blockout.Generate(lw.Map, lw.Built);
        (Dictionary<string, string> write, List<string> delete) = Blockout.Changes(Path.Combine(Root, "world", "regions"), result);
        Assert.True(write.Count + delete.Count == 0,
            "Out of date with world/layout/continent.json (run `dotnet run --project tools/world -- blockout`): " +
            string.Join(", ", write.Keys.Concat(delete).Select(Path.GetFileName).Take(10)));
    }

    /// <summary>
    /// L2 is done when the owner can walk from Tolling to Cruor, over a pass of the spine, and into
    /// every power's land: walked here on the exported maps the server loads.
    /// </summary>
    [Fact]
    public void FromTollingYouCanWalkToCruorOverEveryPassAndIntoEveryPower()
    {
        LayoutWorkspace lw = LayoutWorkspace.Load(Root);
        ContinentMap layout = lw.Map;
        WorldMap world = WorldLoader.Load(Path.Combine(Repo.ServerDirectory, "data", "maps"), "tolling").Map;
        int x0 = layout.OriginX, y0 = layout.OriginY, w = layout.Cols * layout.ZoneTiles, h = layout.Rows * layout.ZoneTiles;
        bool[] reached = Walk(world, world.Spawn, x0, y0, w, h);
        bool Reached(float x, float y) => reached[((int)y - y0) * w + (int)x - x0];

        PlaceDef cruor = layout.Places["cruor"];
        Assert.True(Reached(cruor.At[0], cruor.At[1]), "Cruor can't be walked to from Tolling.");
        foreach (PassDef pass in layout.Data.Spine.Passes)
        {
            float[] crest = pass.Line[pass.Line.Count / 2];
            Assert.True(Reached(crest[0], crest[1]), $"The pass {pass.Id} can't be walked over at ({crest[0]}, {crest[1]}).");
        }

        // Every core power's land, and nearly all walkable core land, is reachable.
        var reachedBy = new Dictionary<string, int>();
        int walkable = 0, got = 0;
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                (int col, int row) = layout.ZoneOf(x + 0.5f, y + 0.5f);
                int i = layout.Index(col, row);
                if (!layout.IsCore(i) || world.IsBlocked(0, x, y)) continue;
                walkable++;
                if (!reached[(y - y0) * w + x - x0]) continue;
                got++;
                string power = layout.PowerIdAt(i)!;
                reachedBy[power] = reachedBy.TryGetValue(power, out int n) ? n + 1 : 1;
            }
        foreach (KeyValuePair<string, PowerDef> p in layout.Data.Powers.Where(p => p.Value.Core))
            Assert.True(reachedBy.ContainsKey(p.Key), $"No land of {p.Value.Name} can be walked to from Tolling.");
        Assert.True(got >= walkable * 0.97, $"Only {got} of {walkable} walkable core tiles can be walked to from Tolling.");
    }

    [Fact]
    public void GeneratedRegionsAreLeftOutOfTheLayoutsBuiltRegions()
    {
        LayoutWorkspace lw = LayoutWorkspace.Load(Root);
        Assert.Contains(lw.Built, b => b.Id == "tolling");
        // A land region still marked generated is the generator's; one taken over by hand is built.
        HashSet<string> generated = RegionFiles.RegionPaths(Path.Combine(Root, "world", "regions"))
            .Select(RegionFiles.LoadRegion).Where(r => r.Generated != null).Select(r => r.Id).ToHashSet();
        Assert.NotEmpty(generated);
        Assert.DoesNotContain(lw.Built, b => generated.Contains(b.Id));
    }

    [Fact]
    public void ATilesPieceGivesGroundBlocksAndWater()
    {
        var region = new RegionData { Id = "t", Origin = new[] { 0, 0 }, Size = new[] { 8, 8 } };
        region.Materials["G"] = "#00ff00";
        region.Materials["W"] = "#0000ff";
        region.Materials["R"] = "#808080";
        region.Pieces.Add(new Piece
        {
            Id = "ground",
            Kind = PieceKind.Tiles,
            Rect = new[] { 0, 0, 8, 8 },
            Legend = new SortedDictionary<string, TileKind>
            {
                ["g"] = new TileKind { Material = "G" },
                ["~"] = new TileKind { Material = "W", Water = true },
                ["m"] = new TileKind { Material = "R", Blocks = true },
            },
            Rows = new List<string> { "gggggggg", "g~~ggggg", "gggggmmg", "gggggggg", "gggggggg", "gggggggg", "gggggggg", "ggg     " },
        });
        RegionCompiler.Result r = RegionCompiler.Compile(region, null);
        Assert.Empty(r.Errors);
        Assert.False(r.Map.IsBlocked(0, 0, 7));
        Assert.True(r.Map.IsBlocked(0, 1, 6)); // water, the second row from the north
        Assert.True(r.Map.IsBlocked(0, 5, 5)); // rock
        Assert.True(r.Map.IsBlocked(0, 5, 0)); // a space: no ground
        Assert.False(r.Map.IsBlocked(0, 2, 0));

        // The rows read back the same after formatting, one to a line.
        string text = RegionFiles.Format(region);
        Assert.Contains("\n      \"g~~ggggg\",\n", text);
        RegionData again = RegionFiles.ParseRegion(text);
        Assert.Equal(region.Pieces[0].Rows, again.Pieces[0].Rows);
        Assert.True(again.Pieces[0].Legend!["~"].Water);
    }

    [Fact]
    public void LegendsAreFormattedInCharacterOrderOnEveryMachine()
    {
        // The JSON parser sorts by the machine's culture, which can put '-' after '+'.
        string json = "{ \"id\": \"t\", \"origin\": [0, 0], \"size\": [8, 8], \"pieces\": [ { \"id\": \"ground\", \"kind\": \"tiles\", " +
                      "\"legend\": { \"+\": { \"material\": \"A\" }, \"-\": { \"material\": \"B\" }, \"g\": { \"material\": \"C\" } } } ] }";
        RegionData region = RegionFiles.ParseRegion(json);
        Assert.Equal(new[] { "+", "-", "g" }, region.Pieces[0].Legend!.Keys);
        string text = RegionFiles.Format(region);
        Assert.True(text.IndexOf("\"+\"", StringComparison.Ordinal) < text.IndexOf("\"-\"", StringComparison.Ordinal));
    }

    [Fact]
    public void ATilesPieceWithAnUnknownCharacterIsAnError()
    {
        var region = new RegionData { Id = "t", Origin = new[] { 0, 0 }, Size = new[] { 8, 8 } };
        region.Materials["G"] = "#00ff00";
        region.Pieces.Add(new Piece
        {
            Id = "ground",
            Kind = PieceKind.Tiles,
            Rect = new[] { 0, 0, 8, 8 },
            Legend = new SortedDictionary<string, TileKind> { ["g"] = new TileKind { Material = "G" } },
            Rows = Enumerable.Repeat("gggggggg", 7).Append("gggxgggg").ToList(),
        });
        Assert.Contains(RegionCompiler.Compile(region, null).Errors, e => e.Contains("'x'"));
    }

    private static bool[] Walk(WorldMap map, TileCoord start, int x0, int y0, int w, int h)
    {
        var reached = new bool[w * h];
        var queue = new Queue<(int x, int y)>();
        reached[(start.Y - y0) * w + start.X - x0] = true;
        queue.Enqueue((start.X, start.Y));
        (int dx, int dy)[] steps = { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();
            foreach ((int dx, int dy) in steps)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < x0 || ny < y0 || nx >= x0 + w || ny >= y0 + h) continue;
                int i = (ny - y0) * w + nx - x0;
                if (reached[i] || !map.CanStep(0, x, y, dx, dy)) continue;
                reached[i] = true;
                queue.Enqueue((nx, ny));
            }
        }
        return reached;
    }
}
