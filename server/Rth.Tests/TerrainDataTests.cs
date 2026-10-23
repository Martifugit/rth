using Rth.Shared.World;
using Rth.Shared.World.Data;

namespace Rth.Tests;

/// <summary>Terrain heights in a region's data (docs/world-guide.md section 4).</summary>
public class TerrainDataTests
{
    private static RegionData Region(int x0 = 0, int y0 = 0, int size = 8)
    {
        var region = new RegionData { Id = "r" + x0 + "_" + y0, Origin = new[] { x0, y0 }, Size = new[] { size, size } };
        region.Materials["G"] = "#00ff00";
        region.Pieces.Add(new Piece
        {
            Id = "ground",
            Kind = PieceKind.Tiles,
            Rect = new[] { x0, y0, x0 + size, y0 + size },
            Legend = new SortedDictionary<string, TileKind> { ["g"] = new TileKind { Material = "G" } },
            Rows = Enumerable.Repeat(new string('g', size), size).ToList(),
        });
        return region;
    }

    [Fact]
    public void HeightsSurviveTheTextFormWithRunsForFlatGround()
    {
        var field = new Heightfield(0, 0, 8, 8);
        field.SetCorner(3, 4, 1.25f);
        field.SetCorner(4, 4, -0.5f);
        RegionTerrain terrain = TerrainText.Write(field);
        Assert.Equal(9, terrain.Rows.Count);
        Assert.Equal("0*9", terrain.Rows[0]);                 // the north row: flat
        Assert.Equal("0*3 125 -50 0*4", terrain.Rows[8 - 4]);  // row y = 4, written from the north

        RegionData region = Region();
        region.Terrain = terrain;
        Heightfield back = TerrainText.Read(region)!;
        Assert.Equal(1.25f, back.Corner(3, 4), 3);
        Assert.Equal(-0.5f, back.Corner(4, 4), 3);
        Assert.Equal(0f, back.Corner(8, 8), 3);
    }

    [Fact]
    public void ACliffBlocksItsTilesAndGentleSlopesDont()
    {
        RegionData region = Region();
        var field = new Heightfield(0, 0, 8, 8);
        field.Fill((x, y) => x * 0.3f);       // a gentle rise east: walkable
        field.SetCorner(6, 2, 3f);            // a spike: the four tiles round it are cliffs
        region.Terrain = TerrainText.Write(field);
        RegionCompiler.Result r = RegionCompiler.Compile(region, null);
        Assert.Empty(r.Errors);
        Assert.False(r.Map.IsBlocked(0, 1, 1));
        Assert.True(r.Map.IsBlocked(0, 5, 1));
        Assert.True(r.Map.IsBlocked(0, 6, 2));
        Assert.False(r.Map.IsBlocked(0, 3, 6));
        Assert.NotNull(r.Terrain);
    }

    [Fact]
    public void MistakesInTheTerrainAreErrors()
    {
        RegionData region = Region();
        region.Terrain = new RegionTerrain { Rows = Enumerable.Repeat("0*9", 8).ToList() };
        Assert.Contains(RegionCompiler.Compile(region, null).Errors, e => e.Contains("needs 9"));

        region.Terrain = new RegionTerrain { Rows = Enumerable.Repeat("0*9", 8).Append("0*8").ToList() };
        Assert.Contains(RegionCompiler.Compile(region, null).Errors, e => e.Contains("9 corners") || e.Contains("needs 9"));

        region.Terrain = new RegionTerrain { Rows = Enumerable.Repeat("0*9", 8).Append("0*4 high 0*4").ToList() };
        Assert.Contains(RegionCompiler.Compile(region, null).Errors, e => e.Contains("'high'"));
    }

    [Fact]
    public void TheTerrainIsWrittenOneRowToALine()
    {
        RegionData region = Region();
        var field = new Heightfield(0, 0, 8, 8);
        field.SetCorner(2, 2, 0.4f);
        region.Terrain = TerrainText.Write(field);
        string text = RegionFiles.Format(region);
        Assert.Contains("\"terrain\": { \"cliffStep\": 1, \"rows\": [\n    \"0*9\",\n", text);
        RegionData again = RegionFiles.ParseRegion(text);
        Assert.Equal(region.Terrain.Rows, again.Terrain!.Rows);
        Assert.Equal(text, RegionFiles.Format(again));
    }

    [Fact]
    public void FlatPiecesOnTerrainAreFlaggedAndTilesArent()
    {
        RegionData region = Region();
        region.Terrain = TerrainText.Write(new Heightfield(0, 0, 8, 8));
        region.Pieces.Add(new Piece { Id = "field", Kind = PieceKind.Patch, Rect = new[] { 1, 1, 3, 3 }, Material = "G" });
        List<string> warnings = RegionCompiler.Compile(region, null).Warnings;
        Assert.Contains(warnings, w => w.Contains("'field'"));
        Assert.DoesNotContain(warnings, w => w.Contains("'ground'"));
    }

    [Fact]
    public void ANeighbourAtAnotherHeightAlongTheBorderIsASeam()
    {
        RegionData west = Region(0, 0), east = Region(8, 0);
        var raised = new Heightfield(0, 0, 8, 8);
        raised.Fill((x, y) => x >= 6 ? 1f : 0f); // reaches the shared border at x = 8
        west.Terrain = TerrainText.Write(raised);
        WorldWorkspace.Entry E(RegionData r) => new() { Region = r, Compiled = RegionCompiler.Compile(r, null) };

        string? seam = WorldWorkspace.Seam(E(west), E(east));
        Assert.NotNull(seam);
        Assert.Contains("9 corners differ", seam);

        var lowered = new Heightfield(0, 0, 8, 8);
        lowered.Fill((x, y) => x == 4 ? 1f : 0f); // a ridge inside, back to 0 at the border
        west.Terrain = TerrainText.Write(lowered);
        Assert.Null(WorldWorkspace.Seam(E(west), E(east)));

        RegionData far = Region(32, 0); // not a neighbour
        west.Terrain = TerrainText.Write(raised);
        Assert.Null(WorldWorkspace.Seam(E(west), E(far)));
    }
}
