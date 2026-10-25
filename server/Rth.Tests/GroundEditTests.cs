using Rth.Shared.World.Data;
using Rth.Shared.World.Ground;

namespace Rth.Tests;

/// <summary>The ground brush's edits (shared/World/Ground/GroundEdit.cs).</summary>
public class GroundEditTests
{
    private static RegionData Region(string id, int x0, string fill, int margin = 2)
    {
        var region = new RegionData { Id = id, Origin = new[] { x0, 0 }, Size = new[] { 8, 8 } };
        region.Materials["LandGrass"] = "Grass";
        var piece = new Piece
        {
            Id = "ground", Kind = PieceKind.Tiles, Rect = new[] { x0, 0, x0 + 8, 8 }, Paint = true,
            Legend = new SortedDictionary<string, TileKind>(StringComparer.Ordinal) { [fill] = new TileKind { Material = "LandGrass" } },
            Rows = Enumerable.Repeat(new string(fill[0], 8), 8).ToList(),
        };
        region.Pieces.Add(piece);
        if (margin > 0) GroundEdit.EnsureRing(piece, margin);
        return region;
    }

    private static readonly TileKind Road = new() { Material = "LandDirtroad", Layer = "over" };
    private static readonly Dictionary<string, string> RoadColours = new() { ["LandDirtroad"] = "Dirt" };

    [Fact]
    public void PaintingANewKindAddsItToTheLegendUnderItsOwnLetter()
    {
        RegionData r = Region("a", 0, "g");
        Piece p = GroundEdit.Tiles(r)!;
        char c = GroundEdit.CharFor(r, p, Road, RoadColours, '-');
        Assert.Equal('-', c);
        Assert.Equal("Dirt", r.Materials["LandDirtroad"]);
        Assert.True(GroundEdit.Set(p, 3, 7, c));
        Assert.False(GroundEdit.Set(p, 3, 7, c));
        Assert.Equal("ggg-gggg", p.Rows![0]); // the north row
        Assert.Equal('-', GroundEdit.At(p, 3, 7));
        Assert.Equal('-', GroundEdit.CharFor(r, p, Road, RoadColours, '-')); // the same kind again: the same character

        // Its letter taken by something else: the next spare one.
        var lava = new TileKind { Material = "LandLava", Blocks = true, Height = 1.6f };
        char l = GroundEdit.CharFor(r, p, lava, new Dictionary<string, string> { ["LandLava"] = "Lava" }, '-');
        Assert.NotEqual('-', l);

        // A material name the region already colours otherwise gets its own name.
        var other = new TileKind { Material = "LandGrass", Height = 0.5f };
        GroundEdit.CharFor(r, p, other, new Dictionary<string, string> { ["LandGrass"] = "Moss" });
        Assert.Equal("Grass", r.Materials["LandGrass"]);
        Assert.Equal("Moss", r.Materials["LandGrass2"]);

        // Unused kinds go.
        GroundEdit.Prune(p);
        Assert.Equal(new[] { "-", "g" }, p.Legend!.Keys);
    }

    [Fact]
    public void ARingFollowsWhatTheNeighbourHolds()
    {
        RegionData west = Region("west", 0, "g"), east = Region("east", 8, "h");
        east.Materials["LandHeath"] = "Heath";
        GroundEdit.Tiles(east)!.Legend = new SortedDictionary<string, TileKind>(StringComparer.Ordinal) { ["h"] = new TileKind { Material = "LandHeath" } };
        Piece w = GroundEdit.Tiles(west)!;
        // A road along the west region's east edge.
        char c = GroundEdit.CharFor(west, w, Road, RoadColours, '-');
        for (int y = 0; y < 8; y++) GroundEdit.Set(w, 7, y, c);

        List<string> changed = GroundEdit.RefreshSurrounds(new[] { west, east });
        Assert.Contains("east", changed);
        Piece e = GroundEdit.Tiles(east)!;
        // The east region's ring: 2 rows of 12 along the north, then 8 rows of 4 (west 2, east 2), then 2 of 12.
        Assert.Equal(12, e.Surround!.Count);
        Assert.Equal(4, e.Surround[2].Length);
        Assert.Equal("g-", e.Surround[2].Substring(0, 2)); // tiles 6 and 7 of the west region
        Assert.Equal("  ", e.Surround[2].Substring(2)); // nothing to the east: left as it was
        Assert.Equal("Dirt", east.Materials["LandDirtroad"]);
        Assert.Equal("LandGrass", e.Legend!["g"].Material);
        Assert.Empty(GroundEdit.RefreshSurrounds(new[] { west, east })); // settled
    }

    [Fact]
    public void ARoadStrokeIsStraightOrDiagonal()
    {
        Assert.Equal((10, 0), GroundEdit.Snap45(0, 0, 10, 3));
        Assert.Equal((7, 7), GroundEdit.Snap45(0, 0, 6, 7));
        Assert.Equal((0, -9), GroundEdit.Snap45(0, 0, 2, -9));
        HashSet<(int x, int y)> line = GroundEdit.Line(0, 0, 5, 0, 2);
        Assert.Equal(12, line.Count); // six tiles long, two wide
        Assert.Single(GroundEdit.Disc(4, 4, 0));
        Assert.Equal(4, GroundEdit.Line(3, 3, 3, 3, 2).Count); // a click with a stroke two wide: two by two
        Assert.Equal(3, GroundEdit.Line(0, 0, 2, 2, 1).Count); // a diagonal one wide: one tile a step
        Assert.Equal(5, GroundEdit.Disc(4, 4, 1).Count()); // a radius of one: the tile and its four sides
    }
}
