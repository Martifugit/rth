using Rth.Shared.World.Data;
using Rth.Shared.World.Ground;
using Rth.Shared.World.Layout;

namespace Rth.Tests;

/// <summary>Plans of strokes and swaps over regions' tiles (shared/World/Ground/GroundStrokes.cs).</summary>
public class GroundStrokesTests
{
    private static string Root => Path.GetDirectoryName(Repo.ServerDirectory)!;

    [Fact]
    public void ASwapOfSmallPatchesLeavesTheBigOnesAlone()
    {
        ContinentData data = ContinentData.Parse(File.ReadAllText(Path.Combine(Root, "world", "layout", "continent.json")));
        var brushes = GroundEdit.Brushes(data).ToDictionary(b => b.Id);
        var region = new RegionData { Id = "test", Origin = new[] { 0, 0 }, Size = new[] { 16, 16 } };
        var piece = new Piece { Id = "ground", Kind = PieceKind.Tiles, Rect = new[] { 0, 0, 16, 16 }, Paint = true, Legend = new SortedDictionary<string, TileKind>(StringComparer.Ordinal), Rows = new List<string>() };
        region.Pieces.Add(piece);
        char Char(string id) => GroundEdit.CharFor(region, piece, brushes[id].Kind, brushes[id].Materials, brushes[id].Letter);
        char g = Char("grass"), f = Char("furrow");
        piece.Rows = Enumerable.Repeat(new string(g, 16), 16).ToList();
        // A field of 5 x 5 and a scrap of 3 tiles.
        for (int y = 2; y < 7; y++) for (int x = 2; x < 7; x++) GroundEdit.Set(piece, x, y, f);
        for (int x = 10; x < 13; x++) GroundEdit.Set(piece, x, 12, f);

        var plan = new StrokePlan { Swaps = { new GroundSwap { From = { "furrow" }, NearWater = 0, Smaller = 10, Otherwise = "surround" } } };
        GroundStrokes.Apply(new[] { region }, data, plan);

        Assert.Equal(f, GroundEdit.At(piece, 4, 4));
        Assert.Equal(f, GroundEdit.At(piece, 2, 2));
        for (int x = 10; x < 13; x++) Assert.Equal(g, GroundEdit.At(piece, x, 12));
    }

    [Fact]
    public void ARaisedScarpIsSteepAlongItsWholeFaceAndGentleBehind()
    {
        ContinentData data = ContinentData.Parse(File.ReadAllText(Path.Combine(Root, "world", "layout", "continent.json")));
        var brushes = GroundEdit.Brushes(data).ToDictionary(b => b.Id);
        var region = new RegionData { Id = "test", Origin = new[] { 0, 0 }, Size = new[] { 64, 64 } };
        var piece = new Piece { Id = "ground", Kind = PieceKind.Tiles, Rect = new[] { 0, 0, 64, 64 }, Paint = true, Legend = new SortedDictionary<string, TileKind>(StringComparer.Ordinal), Rows = new List<string>() };
        region.Pieces.Add(piece);
        char g = GroundEdit.CharFor(region, piece, brushes["grass"].Kind, brushes["grass"].Materials, brushes["grass"].Letter);
        piece.Rows = Enumerable.Repeat(new string(g, 64), 64).ToList();
        region.Terrain = TerrainText.Write(new Rth.Shared.World.Heightfield(0, 0, 64, 64));

        // A plateau west of x 40, its face east, its back running off west.
        var plan = new StrokePlan
        {
            Shapes = { new GroundShape { Id = "edge", Raise = 6f, Gentle = 12f, Ragged = 0f, CliffSide = new[] { 1f, 0f },
                Points = { new[] { -10f, 4f }, new[] { 15f, 4f }, new[] { 40f, 4f }, new[] { 40f, 12f }, new[] { 40f, 24f }, new[] { 40f, 36f }, new[] { 40f, 48f }, new[] { 40f, 60f }, new[] { 15f, 60f }, new[] { -10f, 60f }, new[] { -10f, 32f } } } },
        };
        GroundStrokes.Apply(new[] { region }, data, plan);
        var after = TerrainText.Read(region)!;
        foreach (int y in new[] { 16, 32, 48 })
        {
            Assert.True(after.Corner(38, y) - after.Corner(41, y) > 4f, $"the face at y {y} is not steep");
            Assert.InRange(after.Corner(20, y), 5.9f, 6.1f);
        }
        // The south and north ends ease in (they don't face east).
        Assert.True(after.Corner(20, 8) - after.Corner(20, 4) < 4f);
    }

    [Fact]
    public void AGradedRoadClimbsACliffItsWholeLengthWalkable()
    {
        ContinentData data = ContinentData.Parse(File.ReadAllText(Path.Combine(Root, "world", "layout", "continent.json")));
        var brushes = GroundEdit.Brushes(data).ToDictionary(b => b.Id);
        var region = new RegionData { Id = "test", Origin = new[] { 0, 0 }, Size = new[] { 48, 48 } };
        var piece = new Piece { Id = "ground", Kind = PieceKind.Tiles, Rect = new[] { 0, 0, 48, 48 }, Paint = true, Legend = new SortedDictionary<string, TileKind>(StringComparer.Ordinal), Rows = new List<string>() };
        region.Pieces.Add(piece);
        char g = GroundEdit.CharFor(region, piece, brushes["grass"].Kind, brushes["grass"].Materials, brushes["grass"].Letter);
        piece.Rows = Enumerable.Repeat(new string(g, 48), 48).ToList();
        // Low land at 0.5 to the south, a plateau at 6.5 from y 24 north: a cliff along y 24.
        var field = new Rth.Shared.World.Heightfield(0, 0, 48, 48);
        for (int y = 0; y <= 48; y++) for (int x = 0; x <= 48; x++) field.SetCorner(x, y, y >= 24 ? 6.5f : 0.5f);
        region.Terrain = TerrainText.Write(field);

        // Three legs switching back up the cliff's face.
        var road = new Stroke
        {
            Id = "climb", Ground = "path", Width = new[] { 2f, 2f }, Wind = 0f, Ragged = 0f, Grade = 0.3f,
            Points = { new[] { 4f, 14f }, new[] { 40f, 20f }, new[] { 8f, 26f }, new[] { 40f, 32f } },
        };
        List<string> log = GroundStrokes.Apply(new[] { region }, data, new StrokePlan { Strokes = { road } });
        Assert.DoesNotContain("too steep", log[0]);
        var after = TerrainText.Read(region)!;
        char path = GroundEdit.CharFor(region, piece, brushes["path"].Kind, brushes["path"].Materials, brushes["path"].Letter);
        int tiles = 0;
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
            {
                if (GroundEdit.At(piece, x, y) != path || x < 6 || x > 38) continue;   // the road, away from its ends' turns
                float lo = new[] { after.Corner(x, y), after.Corner(x + 1, y), after.Corner(x, y + 1), after.Corner(x + 1, y + 1) }.Min();
                float hi = new[] { after.Corner(x, y), after.Corner(x + 1, y), after.Corner(x, y + 1), after.Corner(x + 1, y + 1) }.Max();
                Assert.True(hi - lo < 1f, $"road tile ({x}, {y}) rises {hi - lo:0.00}: a cliff");
                tiles++;
            }
        Assert.True(tiles > 60);
        Assert.Equal(0.5f, after.Corner(2, 2), 2);   // the land away from the road untouched
        Assert.Equal(6.5f, after.Corner(30, 44), 2);
    }

    [Fact]
    public void AWaterShapeRedrawsAMereAndDriesTheOldOne()
    {
        ContinentData data = ContinentData.Parse(File.ReadAllText(Path.Combine(Root, "world", "layout", "continent.json")));
        var brushes = GroundEdit.Brushes(data).ToDictionary(b => b.Id);
        var region = new RegionData { Id = "test", Origin = new[] { 0, 0 }, Size = new[] { 40, 40 } };
        var piece = new Piece { Id = "ground", Kind = PieceKind.Tiles, Rect = new[] { 0, 0, 40, 40 }, Paint = true, Legend = new SortedDictionary<string, TileKind>(StringComparer.Ordinal), Rows = new List<string>() };
        region.Pieces.Add(piece);
        char Char(string id) => GroundEdit.CharFor(region, piece, brushes[id].Kind, brushes[id].Materials, brushes[id].Letter);
        char g = Char("grass"), l = Char("lake"), s = Char("sand");
        piece.Rows = Enumerable.Repeat(new string(g, 40), 40).ToList();
        // The old mere: a square of water at 0.1 in a sand rim, land at 1 round it.
        var field = new Rth.Shared.World.Heightfield(0, 0, 40, 40);
        for (int y = 0; y <= 40; y++) for (int x = 0; x <= 40; x++) field.SetCorner(x, y, 1f);
        for (int y = 4; y < 20; y++) for (int x = 4; x < 20; x++) GroundEdit.Set(piece, x, y, x == 4 || y == 4 || x == 19 || y == 19 ? s : l);
        for (int y = 5; y <= 19; y++) for (int x = 5; x <= 19; x++) field.SetCorner(x, y, 0.1f);
        region.Terrain = TerrainText.Write(field);

        // The new one: further north-east, overlapping the old.
        var plan = new StrokePlan
        {
            Shapes =
            {
                new GroundShape
                {
                    Id = "mere", Water = true, Ground = "lake", Level = 0.1f, Ragged = 0f, Drain = { "lake" }, DrainGrounds = { "sand" },
                    Points = { new[] { 12f, 12f }, new[] { 32f, 12f }, new[] { 32f, 32f }, new[] { 12f, 32f } },
                },
            },
        };
        GroundStrokes.Apply(new[] { region }, data, plan);
        var after = TerrainText.Read(region)!;

        Assert.Equal(l, GroundEdit.At(piece, 25, 25));               // new water
        Assert.Equal(0.1f, after.Corner(25, 25), 2);
        Assert.Equal(g, GroundEdit.At(piece, 7, 7));                 // old water dried
        Assert.Equal(g, GroundEdit.At(piece, 4, 10));                // the old rim, away from water now
        Assert.InRange(after.Corner(7, 7), 0.2f, 1f);                // dried ground eases up from the shore
        Assert.True(after.Corner(6, 6) > after.Corner(9, 9));        // higher away from the new water
        for (int x = 0; x <= 40; x++) Assert.Equal(1f, after.Corner(x, 0), 2); // the land beyond untouched
    }
}
