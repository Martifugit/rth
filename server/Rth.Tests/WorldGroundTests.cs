using Rth.Shared.World;
using Rth.Shared.World.Data;
using Rth.Shared.World.Ground;

namespace Rth.Tests;

public class WorldGroundTests
{
    private static List<RegionData> ShippedRegions()
    {
        string dir = Path.Combine(Path.GetDirectoryName(Repo.ServerDirectory)!, "world", "regions");
        return RegionFiles.RegionPaths(dir).Select(RegionFiles.LoadRegion).ToList();
    }

    [Fact]
    public void TheShippedWorldGoesThroughTheTerrainsUnchanged()
    {
        // Every region's ground into the terrains' chunks and back: the tiles, stones and heights
        // come back as they were. Only two things may move: corners where two regions disagreed at
        // their border (a terrain holds one height there), and rings of the neighbours' tiles that
        // had gone stale (they are refreshed from what the neighbours really hold).
        List<RegionData> regions = ShippedRegions();
        GroundTable table = GroundTable.FromRegions(regions);
        var world = new WorldGround.ChunkWorld(WorldGround.ChunksFromRegions(regions, table, out HashSet<(int x, int y)> disagreements));

        var problems = new List<string>();
        int rings = 0;
        foreach (RegionData region in regions)
        {
            if (GroundEdit.Tiles(region) is not Piece) continue;
            RegionData before = RegionFiles.ParseRegion(RegionFiles.Format(region));
            WorldGround.Apply(region, table, world.Tile, world.Corner);
            Piece a = GroundEdit.Tiles(before)!, b = GroundEdit.Tiles(region)!;
            int[] r = a.Rect!;
            for (int y = r[1]; y < r[3]; y++)
                for (int x = r[0]; x < r[2]; x++)
                {
                    string ka = KindAt(before, a, x, y), kb = KindAt(region, b, x, y);
                    if (ka != kb) problems.Add($"{region.Id} ({x}, {y}): {ka} became {kb}");
                    string sa = StonesAt(a, x, y), sb = StonesAt(b, x, y);
                    if (sa != sb) problems.Add($"{region.Id} ({x}, {y}) stones: {sa} became {sb}");
                }
            Heightfield ha = TerrainText.ReadOrFlat(before), hb = TerrainText.ReadOrFlat(region);
            for (int y = region.OriginY; y <= region.OriginY + region.Height; y++)
                for (int x = region.OriginX; x <= region.OriginX + region.Width; x++)
                    if (TerrainText.Hundredths(ha.Corner(x, y)) != TerrainText.Hundredths(hb.Corner(x, y)) && !disagreements.Contains((x, y)))
                        problems.Add($"{region.Id} corner ({x}, {y}): {ha.Corner(x, y)} became {hb.Corner(x, y)}");
            if (!(a.Surround ?? new List<string>()).SequenceEqual(b.Surround ?? new List<string>())) rings++;
        }
        Assert.True(problems.Count == 0, $"{problems.Count} differences:\n" + string.Join("\n", problems.Take(15)));
        Console.WriteLine($"{disagreements.Count} border corners disagreed; {rings} rings refreshed.");
    }

    private static string KindAt(RegionData region, Piece piece, int x, int y)
    {
        if (!(GroundEdit.At(piece, x, y) is char c) || c == ' ') return "none";
        TileKind k = piece.Legend![c.ToString()];
        string Colour(string? m) => m != null && region.Materials.TryGetValue(m, out string v) ? v : "-";
        return $"{Colour(k.Material)}/{Colour(k.Cliff)}/{k.Blocks}/{k.Water}/{k.Height}/{k.Layer}/{k.Beyond}";
    }

    private static string StonesAt(Piece piece, int x, int y) =>
        GroundEdit.StoneAt(piece, x, y) is StoneKind s ? $"{s.Material}@{s.Share:0.00}" : "none";
    [Fact]
    public void TheShippedTableIsUpToDate()
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
    public void TheHeaviestGroundLayerIsTheTileAndStonesKeepTheirShare()
    {
        var chunk = new GroundChunk(0, 0);
        chunk.Set(3, 4, new GroundTile("grass", "gravel", 0.25f));
        GroundTile t = chunk.At(3, 4)!.Value;
        Assert.Equal("grass", t.Ground);
        Assert.Equal("gravel", t.Stone);
        Assert.Equal(0.25f, t.Share, 3);

        // A soft brush leaves a mix: the heavier ground wins, the stones stay.
        int path = chunk.Layer("path");
        chunk.Weights[4, 3, path] = 0.6f;
        chunk.Weights[4, 3, chunk.Layers.IndexOf("grass")] = 0.15f;
        Assert.Equal("path", chunk.At(3, 4)!.Value.Ground);
        Assert.Null(chunk.At(5, 5)); // never painted: a hole
    }

    private static string FirstDifference(string a, string b)
    {
        string[] la = a.Split('\n'), lb = b.Split('\n');
        for (int i = 0; i < Math.Min(la.Length, lb.Length); i++)
            if (la[i] != lb[i]) return $"line {i + 1}: '{Cut(la[i])}' became '{Cut(lb[i])}'";
        return $"length {la.Length} became {lb.Length}";
    }

    private static string Cut(string s) => s.Length > 160 ? s.Substring(0, 160) + "..." : s;
}
