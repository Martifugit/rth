using Rth.Shared.World.Data;

namespace Rth.Tests;

/// <summary>An area's plan (shared/World/Data/AreaPlan.cs, docs/areas/plan-template.md) and the planning gate.</summary>
public class AreaPlanTests
{
    private static string Root => Path.GetDirectoryName(Repo.ServerDirectory)!;

    /// <summary>Two areas side by side, a with one region at (0, 0) and b east of it.</summary>
    private static AreaPlan.World World()
    {
        var areas = new WorldAreas();
        areas.Areas.Add(new WorldAreas.Area { Id = "a", Wave = 1, Regions = { "r0" } });
        areas.Areas.Add(new WorldAreas.Area { Id = "b", Wave = 1, Regions = { "r1" } });
        areas.Areas.Add(new WorldAreas.Area { Id = "far", Wave = 1, Regions = { "r9" } });
        var w = new AreaPlan.World { Areas = areas, Objects = { "copper_seam", "bank_chest" }, Npcs = { "fen_rat" }, Skills = { "mining" }, Quests = { "the-bell" } };
        w.Regions["r0"] = (0, 0, 96, 96);
        w.Regions["r1"] = (96, 0, 96, 96);
        w.Regions["r9"] = (960, 960, 96, 96);
        return w;
    }

    private static AreaPlan Complete() => new()
    {
        Area = "a", Doc = "docs/areas/a.md", Region = "the heartland",
        Places = { new AreaPlan.Place { Id = "town", Name = "Town", Kind = "town", At = new[] { 30, 40 }, Levels = new[] { 1, 10 }, Why = "the start", FoundBy = "you wake there", Hook = "the bell tower" } },
        Buildings = { new AreaPlan.Building { Id = "bank", Place = "town", Rect = new[] { 28, 38, 32, 42 }, Use = "bank", Contents = { "bank_chest" }, Why = "every town has one" } },
        Contents =
        {
            new AreaPlan.Content { Id = "copper", Kind = "node", Object = "copper_seam", Count = 5, Rect = new[] { 40, 60, 50, 70 }, Place = "town", Skill = "mining", Levels = new[] { 1, 10 }, Why = "a quarry by the river", Supports = { "quest:the-bell" } },
        },
        Heights = { new AreaPlan.Height { Id = "quarry", Job = "nodes", Rise = -2.5f, Outline = { new[] { 40f, 60f }, new[] { 50f, 70f } }, Profile = "a pit", Routes = { "the ramp" }, Why = "the rocks' face" } },
        Neighbours = { new AreaPlan.Tie { Area = "b", Ties = "the road east" } },
    };

    [Fact]
    public void ACompletePlanHasNothingMissing()
    {
        Assert.Empty(Complete().Check(World()));
    }

    [Fact]
    public void EveryPlaceSaysHowItIsFoundAndWhatCatchesTheEye()
    {
        AreaPlan plan = Complete();
        plan.Places[0].FoundBy = "";
        plan.Places[0].Hook = " ";
        List<string> errors = plan.Check(World());
        Assert.Contains(errors, e => e.Contains("foundBy"));
        Assert.Contains(errors, e => e.Contains("hook"));
    }

    [Fact]
    public void EverythingSaysWhyItIsThere()
    {
        AreaPlan plan = Complete();
        plan.Contents[0].Why = "";
        plan.Buildings[0].Why = "";
        plan.Heights[0].Why = "";
        List<string> errors = plan.Check(World());
        Assert.Contains(errors, e => e.StartsWith("content copper") && e.Contains("why"));
        Assert.Contains(errors, e => e.StartsWith("building bank") && e.Contains("why"));
        Assert.Contains(errors, e => e.StartsWith("height quarry") && e.Contains("why"));
    }

    [Fact]
    public void ContentMustExistOrBeMarkedNewAndStayInsideTheArea()
    {
        AreaPlan plan = Complete();
        plan.Contents[0].Object = "gold_seam";
        plan.Contents[0].Supports.Add("quest:no-such-quest");
        plan.Contents[0].Rect = new[] { 90, 60, 100, 70 };   // into the next area
        List<string> errors = plan.Check(World());
        Assert.Contains(errors, e => e.Contains("'gold_seam'"));
        Assert.Contains(errors, e => e.Contains("quest:no-such-quest"));
        Assert.Contains(errors, e => e.Contains("inside the area"));
        plan.Contents[0].New = true;
        Assert.DoesNotContain(plan.Check(World()), e => e.Contains("'gold_seam'"));
    }

    [Fact]
    public void AHeightNeedsAJobAndEveryNeighbourItsTies()
    {
        AreaPlan plan = Complete();
        plan.Heights[0].Job = "scenery";
        plan.Neighbours.Clear();
        List<string> errors = plan.Check(World());
        Assert.Contains(errors, e => e.StartsWith("height quarry") && e.Contains("job"));
        Assert.Contains(errors, e => e.Contains("'b'") && e.Contains("ties"));
        Assert.DoesNotContain(errors, e => e.Contains("'far'"));   // not a neighbour
    }

    [Fact]
    public void TheBuiltRegionsAreComparedWithThePlan()
    {
        AreaPlan plan = Complete();
        var region = new RegionData { Id = "r0", Origin = new[] { 0, 0 }, Size = new[] { 96, 96 } };
        region.Pieces.Add(new Piece { Id = "c1", Kind = PieceKind.Rock, At = new[] { 41, 61 }, Object = "copper_seam" });
        region.Pieces.Add(new Piece { Id = "c2", Kind = PieceKind.Rock, At = new[] { 20, 20 }, Object = "copper_seam" });   // outside the rect
        List<string> notes = plan.Built(new[] { region });
        Assert.Contains(notes, n => n == "node copper: 1 of 5 built");
        Assert.Contains(notes, n => n == "building bank: not built");
    }

    [Fact]
    public void GroundWorkTickedWithoutAnApprovedPlanIsAnErrorFromTheGateOn()
    {
        var areas = new WorldAreas
        {
            Stages =
            {
                new WorldAreas.Stage { Number = 0, Packages = { new WorldAreas.Package { Id = "0.5" } } },
                new WorldAreas.Stage { Number = 1, Packages = { new WorldAreas.Package { Id = "1.1" } } },
            },
        };
        var a = new WorldAreas.Area { Id = "a", Regions = { "r" } };
        areas.Areas.Add(a);
        a.Done["1.1"] = "2026-11-01 before the gate";
        Assert.Empty(areas.Check(new[] { "r" }));
        Assert.True(areas.Ungated().Single().grandfathered);
        Assert.Equal(0, areas.StageOf(a));   // stage 1 doesn't count without the plan

        a.Done["1.1"] = WorldAreas.GateSince;
        Assert.Contains(areas.Check(new[] { "r" }), e => e.Contains("plan isn't approved"));
        a.Done["0.5"] = WorldAreas.GateSince;
        Assert.Empty(areas.Check(new[] { "r" }));
        Assert.Equal(1, areas.StageOf(a));
    }

    [Fact]
    public void EveryShippedPlanIsComplete()
    {
        string folder = Path.Combine(Root, "world", AreaPlan.Folder);
        if (!Directory.Exists(folder)) return;
        var w = new AreaPlan.World { Areas = WorldAreas.Load(WorldAreas.PathIn(Root)), FileExists = f => File.Exists(Path.Combine(Root, f)) };
        string content = Path.Combine(Root, "server", "data", "content");
        foreach (var o in Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(Path.Combine(content, "objects.json")))) w.Objects.Add((string)o["id"]!);
        foreach (var o in Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(Path.Combine(content, "npcs.json")))) w.Npcs.Add((string)o["id"]!);
        foreach (var o in Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(Path.Combine(content, "quests.json")))) w.Quests.Add((string)o["id"]!);
        foreach (string f in Directory.GetFiles(Path.Combine(Root, "docs", "quests"), "*.md")) w.Quests.Add(Path.GetFileNameWithoutExtension(f));
        foreach (var s in (Newtonsoft.Json.Linq.JArray)Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(content, "skills.json")))["skills"]!) w.Skills.Add((string)s["id"]!);
        foreach (RegionData r in RegionFiles.RegionPaths(Path.Combine(Root, "world", "regions")).Select(RegionFiles.LoadRegion).Where(r => !r.Instanced))
            w.Regions[r.Id] = (r.OriginX, r.OriginY, r.Width, r.Height);
        foreach (string file in Directory.GetFiles(folder, "*.json"))
        {
            List<string> errors = AreaPlan.Parse(File.ReadAllText(file)).Check(w);
            Assert.True(errors.Count == 0, Path.GetFileName(file) + ":\n" + string.Join("\n", errors));
        }
    }
}
