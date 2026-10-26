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
}
