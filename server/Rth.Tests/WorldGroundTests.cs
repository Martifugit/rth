using Rth.Shared.World;
using Rth.Shared.World.Data;
using Rth.Shared.World.Ground;

namespace Rth.Tests;

/// <summary>A region's ground written from its terrain into its data (shared/World/Ground/WorldGround.cs).</summary>
public class WorldGroundTests
{
    private static List<RegionData> ShippedRegions()
    {
        string dir = Path.Combine(Path.GetDirectoryName(Repo.ServerDirectory)!, "world", "regions");
        return RegionFiles.RegionPaths(dir).Select(RegionFiles.LoadRegion).ToList();
    }

    [Fact]
    public void WritingTheShippedGroundBackGivesTheSameMaps()
    {
        // Every region's own grounds and heights written back through Apply: the server's maps are the same.
        List<RegionData> regions = ShippedRegions();
        GroundTable table = GroundTable.FromRegions(regions);
        var problems = new List<string>();
        foreach (RegionData region in regions)
        {
            if (GroundEdit.Tiles(region) is not Piece) continue;
            RegionData before = RegionFiles.ParseRegion(RegionFiles.Format(region));
            Piece piece = GroundEdit.Tiles(before)!;
            Heightfield heights = TerrainText.ReadOrFlat(before);
            WorldGround.Apply(region, table, (x, y) => WorldGround.GroundAt(before, piece, table, x, y), (x, y) => heights.Corner(x, y));

            MapData a = RegionCompiler.Compile(before, null).Map, b = RegionCompiler.Compile(region, null).Map;
            for (int p = 0; p < a.Planes; p++)
                for (int y = a.OriginY; y < a.OriginY + a.Height; y++)
                    for (int x = a.OriginX; x < a.OriginX + a.Width; x++)
                        if (a.GetFlags(p, x, y) != b.GetFlags(p, x, y))
                            problems.Add($"{region.Id} ({x}, {y}): {a.GetFlags(p, x, y)} became {b.GetFlags(p, x, y)}");
        }
        Assert.True(problems.Count == 0, $"{problems.Count} tiles changed:\n" + string.Join("\n", problems.Take(15)));
    }

    [Fact]
    public void TheShippedTableHasEveryGround()
    {
        string path = GroundTable.PathIn(Path.GetDirectoryName(Repo.ServerDirectory)!);
        Assert.True(File.Exists(path), "world/grounds.json is missing");
        GroundTable shipped = GroundTable.Load(path);
        foreach (RegionData region in ShippedRegions())
        {
            if (GroundEdit.Tiles(region) is not Piece piece) continue;
            foreach (TileKind kind in piece.Legend!.Values)
                Assert.True(shipped.GroundOf(region, kind) != null, $"{region.Id}: a ground ({kind.Material}) world/grounds.json doesn't have");
        }
    }

    [Fact]
    public void ANewGroundIsAddedToTheLegendAndLooksAreDropped()
    {
        var region = new RegionData { Id = "r", Origin = new[] { 0, 0 }, Size = new[] { 4, 4 } };
        region.Materials["LandGrass"] = "Grass";
        var piece = new Piece
        {
            Id = "ground", Kind = PieceKind.Tiles, Rect = new[] { 0, 0, 4, 4 }, Paint = true, Margin = 2,
            Surround = new List<string>(), Legend = new SortedDictionary<string, TileKind>(StringComparer.Ordinal) { ["g"] = new TileKind { Material = "LandGrass" } },
            Rows = Enumerable.Repeat("gggg", 4).ToList(),
        };
        region.Pieces.Add(piece);
        var table = new GroundTable();
        table.Materials["LandGrass"] = "Grass";
        table.Materials["LandLake"] = "Mere";
        table.Grounds["grass"] = new TileKind { Material = "LandGrass" };
        table.Grounds["lake"] = new TileKind { Material = "LandLake", Water = true, Height = -0.15f };

        Assert.True(WorldGround.Apply(region, table, (x, y) => x == 1 && y == 1 ? "lake" : "grass", (x, y) => x == 4 ? 2f : 0f));
        Assert.Equal('g', GroundEdit.At(piece, 0, 0));
        char lake = GroundEdit.At(piece, 1, 1)!.Value;
        Assert.True(piece.Legend[lake.ToString()].Water);
        Assert.Equal("Mere", region.Materials["LandLake"]);
        Assert.Null(piece.Paint);
        Assert.Null(piece.Surround);
        Assert.Equal(2f, TerrainText.Read(region)!.Corner(4, 2));
        Assert.False(WorldGround.Apply(region, table, (x, y) => x == 1 && y == 1 ? "lake" : "grass", (x, y) => x == 4 ? 2f : 0f)); // settled
    }
}
