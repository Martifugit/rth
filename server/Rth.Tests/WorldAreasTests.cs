using Rth.Shared.World.Data;

namespace Rth.Tests;

/// <summary>The world in areas (world/areas.json, docs/world-areas.md).</summary>
public class WorldAreasTests
{
    private static string Root => Path.GetDirectoryName(Repo.ServerDirectory)!;

    [Fact]
    public void EveryRegionIsInExactlyOneArea()
    {
        WorldAreas areas = WorldAreas.Load(WorldAreas.PathIn(Root));
        IEnumerable<string> regions = RegionFiles.RegionPaths(Path.Combine(Root, "world", "regions"))
            .Select(RegionFiles.LoadRegion).Where(r => !r.Instanced).Select(r => r.Id);
        Assert.Empty(areas.Check(regions));
    }

    [Fact]
    public void AnAreasStageIsTheLastStageWithEveryPackageDone()
    {
        var areas = new WorldAreas
        {
            Stages =
            {
                new WorldAreas.Stage { Number = 1, Packages = { new WorldAreas.Package { Id = "1.1" }, new WorldAreas.Package { Id = "1.2" } } },
                new WorldAreas.Stage { Number = 2, Packages = { new WorldAreas.Package { Id = "2.1" } } },
            },
        };
        var a = new WorldAreas.Area { Id = "a", Regions = { "r" } };
        areas.Areas.Add(a);
        Assert.Equal(0, areas.StageOf(a));
        Assert.Equal("1.1", areas.Next(a)!.Id);
        a.Done["1.1"] = "today";
        a.Done["2.1"] = "today"; // out of order: stage 2 doesn't count until stage 1 is done
        Assert.Equal(0, areas.StageOf(a));
        a.Done["1.2"] = "today";
        Assert.Equal(2, areas.StageOf(a));
        Assert.Null(areas.Next(a));

        Assert.Contains(areas.Check(new[] { "r", "s" }), e => e.Contains("region s is in no area"));
        a.Done["9.9"] = "today";
        Assert.Contains(areas.Check(new[] { "r" }), e => e.Contains("'9.9'"));
    }
}
