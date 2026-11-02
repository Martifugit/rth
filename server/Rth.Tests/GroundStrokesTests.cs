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
