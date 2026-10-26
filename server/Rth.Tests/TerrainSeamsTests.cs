using Rth.Shared.World;
using Rth.Shared.World.Data;

namespace Rth.Tests;

/// <summary>The ground stays whole across region borders whatever writes a region's heights (shared/World/Data/TerrainSeams.cs).</summary>
public class TerrainSeamsTests
{
    private static RegionData Region(string id, int x0, float height, bool generated = false)
    {
        var region = new RegionData { Id = id, Origin = new[] { x0, 0 }, Size = new[] { 8, 8 }, Generated = generated ? "blockout" : null };
        var field = new Heightfield(x0, 0, 8, 8);
        for (int y = 0; y <= 8; y++)
            for (int x = x0; x <= x0 + 8; x++) field.SetCorner(x, y, height);
        region.Terrain = TerrainText.Write(field);
        return region;
    }

    private static float Corner(RegionData r, int x, int y) => TerrainText.Read(r)!.Corner(x, y);

    [Fact]
    public void TheRegionJustWrittenKeepsItsEdgeAndTheNeighbourFollows()
    {
        RegionData west = Region("west", 0, 1f), east = Region("east", 8, 0.5f);
        TerrainSeams.Result r = TerrainSeams.Follow(new[] { west, east }, new[] { "west" });
        Assert.Equal(new[] { "east" }, r.Changed);
        for (int y = 0; y <= 8; y++) Assert.Equal(1f, Corner(east, 8, y), 3);
        Assert.Equal(0.5f, Corner(east, 9, 4), 3); // only the edge moves
        Assert.Equal(1f, Corner(west, 8, 4), 3);
        Assert.Empty(r.Messages); // half a tile: no note
        Assert.Empty(TerrainSeams.Follow(new[] { west, east }, new[] { "west" }).Changed); // settled
    }

    [Fact]
    public void MendingTheWholeWorldMeetsHalfway()
    {
        RegionData west = Region("west", 0, 0.49f), east = Region("east", 8, 0.47f);
        TerrainSeams.Result r = TerrainSeams.Follow(new[] { west, east }, null);
        Assert.Equal(new[] { "east", "west" }, r.Changed);
        Assert.Equal(0.48f, Corner(west, 8, 3), 3);
        Assert.Equal(0.48f, Corner(east, 8, 3), 3);
    }

    [Fact]
    public void GeneratedLandIsLeftForTheBlockout()
    {
        RegionData built = Region("built", 0, 1f), land = Region("land", 8, 0f, generated: true);
        string before = RegionFiles.Format(land);
        TerrainSeams.Result r = TerrainSeams.Follow(new[] { built, land }, null);
        Assert.Empty(r.Changed);
        Assert.Equal(new[] { "land" }, r.GeneratedNeighbours);
        Assert.Equal(before, RegionFiles.Format(land));
    }

    [Fact]
    public void RegionsThatOnlyTouchAtACornerOrSitOnOtherPlanesAreNotNeighbours()
    {
        RegionData a = Region("a", 0, 1f), b = Region("b", 8, 0f);
        b.Origin = new[] { 8, 8 };
        Assert.Empty(TerrainSeams.SharedCorners(a, b));
        RegionData c = Region("c", 8, 0f);
        c.Plane = 1;
        Assert.Empty(TerrainSeams.SharedCorners(a, c));
    }
}
