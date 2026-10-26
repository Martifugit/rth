using Rth.Shared.World;
using Rth.Shared.World.Data;
using Rth.Shared.World.Ground;

namespace Rth.Tests;

/// <summary>Stones painted on their own over the ground, and the painter's broad patches (shared/World/Ground).</summary>
public class GroundStonesTests
{
    private static (RegionData region, Piece piece) Region()
    {
        var region = new RegionData { Id = "r", Origin = new[] { 0, 0 }, Size = new[] { 16, 16 } };
        region.Materials["LandGrass"] = "Grass";
        var piece = new Piece
        {
            Id = "ground", Kind = PieceKind.Tiles, Rect = new[] { 0, 0, 16, 16 }, Paint = true,
            Legend = new SortedDictionary<string, TileKind>(StringComparer.Ordinal) { ["g"] = new TileKind { Material = "LandGrass" } },
            Rows = Enumerable.Repeat(new string('g', 16), 16).ToList(),
        };
        region.Pieces.Add(piece);
        return (region, piece);
    }

    private static readonly GroundEdit.StoneBrush Gravel = new() { Id = "gravel", Name = "Gravel", Material = "LandStonesGravel", Colour = "#877f67" };

    private static GroundCanvas Canvas(RegionData region, Piece piece) =>
        PaintedGround.Canvas(region, piece, m => region.Materials.TryGetValue(m, out string? v) ? PaintedGround.Hex(v) ?? new[] { 0.35f, 0.45f, 0.2f } : null, null);

    [Fact]
    public void StonesLieOnlyOnTheTilesTheyArePaintedOn()
    {
        (RegionData region, Piece piece) = Region();
        // A path of stones three tiles wide, thickly.
        for (int x = 0; x < 16; x++)
            for (int y = 6; y < 9; y++)
                GroundEdit.SetStone(piece, x, y, GroundEdit.StoneCharFor(region, piece, Gravel, GroundStones.Levels.Length - 1));
        List<Stone> stones = GroundStones.Scatter(Canvas(region, piece));
        Assert.True(stones.Count > 16 * 3 * 3, $"only {stones.Count} stones on 48 thick tiles");
        foreach (Stone s in stones)
            for (int k = 1; k < s.Outline.Length; k += 2)
                Assert.InRange(s.Outline[k], 6f, 9f); // never over the path's edge
    }

    [Fact]
    public void StonesAreWrittenClearedAndPrunedAsTheirOwnLayer()
    {
        (RegionData region, Piece piece) = Region();
        char c = GroundEdit.StoneCharFor(region, piece, Gravel, 1);
        Assert.Equal('1', c);
        Assert.Equal("#877f67", region.Materials["LandStonesGravel"]);
        Assert.True(GroundEdit.SetStone(piece, 2, 15, c));
        Assert.False(GroundEdit.SetStone(piece, 2, 15, c));
        Assert.Equal(GroundStones.Levels[1], GroundEdit.StoneAt(piece, 2, 15)!.Share);
        Assert.Equal('g', GroundEdit.At(piece, 2, 15)); // the ground under them is untouched

        // Saved and read back, the layer is the same.
        RegionData again = RegionFiles.ParseRegion(RegionFiles.Format(region));
        Piece p2 = GroundEdit.Tiles(again)!;
        GroundEdit.PruneStones(p2);
        Assert.Equal("  1", p2.Stones![0]);
        Assert.Equal("LandStonesGravel", p2.StoneLegend!["1"].Material);
        Assert.Empty(RegionCompiler.Compile(again, null).Errors);

        // Cleared, the layer goes altogether.
        GroundEdit.SetStone(piece, 2, 15, ' ');
        GroundEdit.PruneStones(piece);
        Assert.Null(piece.Stones);
        Assert.Null(piece.StoneLegend);
    }

    [Fact]
    public void AStoneCharacterTheLegendDoesNotListIsAnError()
    {
        (RegionData region, Piece piece) = Region();
        piece.Stones = Enumerable.Repeat(new string(' ', 16), 16).ToList();
        piece.Stones[3] = "  x";
        Assert.NotEmpty(RegionCompiler.Compile(region, null).Errors);
    }

    [Fact]
    public void ThePathsStonesAreUnevenButNeverAbsentForLong()
    {
        // A long straight path three tiles wide: patches of thicker and thinner stones, bare stretches between.
        bool Path(int x, int y) => y >= 0 && y < 3;
        var levels = Enumerable.Range(0, 400).Select(x => GroundStones.Spread(x, 1, 0.3f, Path)).ToList();
        Assert.True(levels.Distinct().Count() >= 3, "the stones should lie unevenly: " + string.Join(",", levels.Distinct()));
        Assert.Contains(-1, levels);
        int run = 0, longest = 0;
        foreach (int l in levels) { run = l < 0 ? run + 1 : 0; longest = Math.Max(longest, run); }
        Assert.True(longest < 40, $"a bare stretch {longest} tiles long");
        Assert.Equal(-1, GroundStones.Spread(5, 7, 0.3f, Path)); // off the path: none
    }

    [Fact]
    public void GrassIsPaintedInBroadPatchesNotTileByTile()
    {
        (RegionData region, Piece piece) = Region();
        GroundCanvas canvas = Canvas(region, piece);
        byte[] rgba = GroundPainter.Paint(canvas, 4, out int w, out int h);
        // Neighbouring tiles of one ground differ by a whisker; across the region the patches show.
        int maxStep = 0, min = 255, max = 0;
        for (int ty = 0; ty < 16; ty++)
            for (int tx = 0; tx < 16; tx++)
            {
                int g = rgba[((ty * 4 + 2) * w + tx * 4 + 2) * 4 + 1];
                min = Math.Min(min, g); max = Math.Max(max, g);
                if (tx > 0) maxStep = Math.Max(maxStep, Math.Abs(g - rgba[((ty * 4 + 2) * w + tx * 4 - 2) * 4 + 1]));
            }
        Assert.True(maxStep <= 3, $"a tile-to-tile step of {maxStep}");
        Assert.True(max - min >= 1, "the land should drift a little even over 16 tiles");
    }
}
