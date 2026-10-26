using Rth.Shared.World;
using Rth.Shared.World.Data;
using Rth.Shared.World.Layout;

namespace Rth.Tests;

/// <summary>
/// The terrain editor's way back into the data (<see cref="WorldTerrain"/>): heights edited over
/// the whole world land in built regions' terrain and generated regions' raise, run on a copy of
/// the world's data so the real files are never touched.
/// </summary>
public sealed class WorldTerrainTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rth-terrain-" + Guid.NewGuid().ToString("N"));

    public WorldTerrainTests()
    {
        string repo = Path.GetDirectoryName(Repo.ServerDirectory)!;
        Copy(Path.Combine(repo, "world", "regions"), Path.Combine(_root, "world", "regions"));
        Copy(Path.Combine(repo, "world", "layout"), Path.Combine(_root, "world", "layout"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "areas"));
        File.Copy(Path.Combine(repo, "docs", "areas", "targets.json"), Path.Combine(_root, "docs", "areas", "targets.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(from, to));
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(from, to));
    }

    private RegionData Region(string id) => RegionFiles.LoadRegion(Path.Combine(_root, "world", "regions", id + ".json"));

    private static Heightfield Edited(WorldTerrain world, params (int x, int y, float h)[] corners)
    {
        Heightfield f = world.Field;
        var edited = new Heightfield(f.OriginX, f.OriginY, f.Width, f.Depth, f.Corners);
        foreach ((int x, int y, float h) in corners) edited.SetCorner(x, y, h);
        return edited;
    }

    [Fact]
    public void SavingTheHeightsAsTheyAreChangesNothing()
    {
        WorldTerrain world = WorldTerrain.Load(_root);
        WorldTerrain.Result result = WorldTerrain.Save(_root, world.Field);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Regions);
        Assert.False(Directory.Exists(HeightEdits.DirectoryIn(_root)));
    }

    [Fact]
    public void SculptingGeneratedLandKeepsItGeneratedAndTheBlockoutDrawsIt()
    {
        WorldTerrain world = WorldTerrain.Load(_root);
        Assert.NotNull(Region("land_0_1").Generated);
        WorldTerrain.Result result = WorldTerrain.Save(_root, Edited(world, (40, 140, 2.5f), (41, 140, 2.37f)));
        Assert.Empty(result.Errors);
        Assert.Contains("land_0_1", result.Regions);

        RegionData region = Region("land_0_1");
        Assert.NotNull(region.Generated);
        Heightfield heights = TerrainText.Read(region)!;
        Assert.Equal(2.5f, heights.Corner(40, 140), 3);
        Assert.Equal(2.37f, heights.Corner(41, 140), 3);
        Assert.True(File.Exists(Path.Combine(HeightEdits.DirectoryIn(_root), "land_0_1.json")));

        // Regenerating the blockout gives the same files: the raise is part of the layout's land now.
        LayoutWorkspace lw = LayoutWorkspace.Load(_root);
        (Dictionary<string, string> write, List<string> delete) =
            Blockout.Changes(Path.Combine(_root, "world", "regions"), Blockout.Generate(lw.Map, lw.Built, lw.Edits));
        Assert.Empty(write);
        Assert.Empty(delete);

        // And loading again reads back exactly what was saved.
        Assert.Empty(WorldTerrain.Save(_root, WorldTerrain.Load(_root).Field).Regions);
    }

    [Fact]
    public void ACornerOnABorderIsTheSameInBothRegions()
    {
        WorldTerrain world = WorldTerrain.Load(_root);
        WorldTerrain.Result result = WorldTerrain.Save(_root, Edited(world, (96, 150, 3f)));
        Assert.Empty(result.Errors);
        Assert.Equal(3f, TerrainText.Read(Region("land_0_1"))!.Corner(96, 150), 3);
        Assert.Equal(3f, TerrainText.Read(Region("land_1_1"))!.Corner(96, 150), 3);
    }

    [Fact]
    public void SculptingABuiltRegionWritesItsTerrain()
    {
        RegionData before = Region("land_m1_2");
        Assert.Null(before.Generated);
        WorldTerrain world = WorldTerrain.Load(_root);
        float h = world.Field.Corner(-50, 240) + 1.25f;
        WorldTerrain.Result result = WorldTerrain.Save(_root, Edited(world, (-50, 240, h)));
        Assert.Empty(result.Errors);
        Assert.Equal(new[] { "land_m1_2" }, result.Regions);
        Assert.Equal(h, TerrainText.Read(Region("land_m1_2"))!.Corner(-50, 240), 3);
        Assert.False(Directory.Exists(HeightEdits.DirectoryIn(_root)));
    }

    [Fact]
    public void GroundWithoutTilesIsLeftAsItIs()
    {
        // A region built the old way, flat and without tile ground (as Tolling was).
        RegionData old = Region("land_0_0");
        old.Pieces.RemoveAll(p => p.Kind == PieceKind.Tiles);
        old.Generated = null;
        old.Terrain = null;
        RegionFiles.SaveRegion(old, Path.Combine(_root, "world", "regions", "land_0_0.json"));

        WorldTerrain world = WorldTerrain.Load(_root);
        Assert.True(world.Pinned(50, 50));
        Assert.True(world.Pinned(96, 50)); // its border with the land east of it
        Assert.Null(world.Tile(50, 50));
        Assert.NotNull(world.Tile(40, 140));
        WorldTerrain.Result result = WorldTerrain.Save(_root, Edited(world, (50, 50, 4f), (96, 50, 4f)));
        Assert.Empty(result.Regions);
        Assert.Contains(result.Messages, m => m.Contains("left as they are"));
        Assert.Null(Region("land_0_0").Terrain);
    }
}
