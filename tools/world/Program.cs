using Rth.Shared.World;
using Rth.Shared.World.Data;
using Rth.Shared.World.Layout;
using Rth.WorldTool;
using Rth.Shared.World.Ground;

// The world data from the command line (docs/workflow.md). Every command works on the whole
// checkout: world/regions/*.json in, server/data/maps/* and world/renders/* out.

string? root = WorldWorkspace.FindRoot(Directory.GetCurrentDirectory());
if (root == null)
{
    Console.Error.WriteLine("Run this inside the R'th repository (no Rth.slnx found above the current folder).");
    return 2;
}

string command = args.Length > 0 ? args[0] : "help";
string[] rest = args.Skip(1).ToArray();
WorldWorkspace ws = WorldWorkspace.Load(root);

switch (command)
{
    case "check":
        return Check(ws);

    case "export":
    {
        foreach (string e in ws.LoadErrors) Console.Error.WriteLine("error: " + e);
        int failed = 0;
        foreach (WorldWorkspace.Entry e in ws.Regions)
            foreach (string error in e.Compiled.Errors)
            {
                Console.Error.WriteLine($"error: {e.Region.Id}: {error}");
                failed++;
            }
        List<string> changed = ws.Export();
        foreach (string path in changed) Console.WriteLine("wrote " + Path.GetRelativePath(root, path).Replace('\\', '/'));
        Console.WriteLine(changed.Count == 0 ? "Maps are up to date." : $"{changed.Count} map files written.");
        return failed > 0 || ws.LoadErrors.Count > 0 ? 1 : 0;
    }

    case "render":
    {
        string outDir = Path.Combine(root, "world", "renders");
        Directory.CreateDirectory(outDir);
        MapRender.LoadPalette(root);
        var regions = rest.Length == 0 ? ws.Regions : ws.Regions.Where(e => rest.Contains(e.Region.Id, StringComparer.OrdinalIgnoreCase)).ToList();
        foreach (WorldWorkspace.Entry e in regions)
        {
            string png = Path.Combine(outDir, e.Region.Id + ".png");
            File.WriteAllBytes(png, MapRender.Region(e.Compiled, 8));
            string txt = Path.Combine(outDir, e.Region.Id + ".txt");
            File.WriteAllText(txt, MapRender.Text(e.Compiled));
            Console.WriteLine($"wrote {Path.GetRelativePath(root, png).Replace('\\', '/')} and {Path.GetFileName(txt)}");
        }
        if (rest.Length == 0 && ws.Regions.Count > 0)
        {
            File.WriteAllBytes(Path.Combine(outDir, "world.png"), MapRender.World(ws.Regions.Where(e => !e.Region.Instanced).Select(e => e.Compiled).ToList(), 2));
            Console.WriteLine("wrote world/renders/world.png");
        }
        return 0;
    }

    case "format":
    {
        int changed = 0;
        foreach (WorldWorkspace.Entry e in ws.Regions)
            if (RegionFiles.WriteIfChanged(e.Path, RegionFiles.Format(e.Region)))
            {
                Console.WriteLine("formatted " + Path.GetFileName(e.Path));
                changed++;
            }
        Console.WriteLine(changed == 0 ? "Every region file is already formatted." : $"{changed} files formatted.");
        return 0;
    }

    case "layout":
        return Layout(root, rest);

    case "chart":
        return Chart(root, rest);

    case "blockout":
        return BlockoutCommand(root, rest);

    case "areas":
    {
        // The world in areas (docs/world-areas.md): check world/areas.json, say where each area stands, draw the coverage map.
        WorldAreas areas = WorldAreas.Load(WorldAreas.PathIn(root));
        var regionRects = ws.Regions.Where(e => !e.Region.Instanced)
            .ToDictionary(e => e.Region.Id, e => (e.Region.OriginX, e.Region.OriginY, e.Region.Width, e.Region.Height));
        List<string> errors = areas.Check(regionRects.Keys);
        foreach (string e in errors) Console.Error.WriteLine("error: " + e);
        foreach (var g in areas.Ungated().Where(u => u.grandfathered).GroupBy(u => u.area.Id))
            Console.WriteLine($"warning: {g.Key}: {string.Join(", ", g.Select(u => u.package))} ticked before the planning gate; redo against its plan once approved");
        int n = 0;
        Console.WriteLine($"  {"#",3} {"wave",4} {"stage",5}  {"area",-42} {"regions",7}  next");
        foreach (WorldAreas.Area a in areas.Areas)
        {
            n++;
            WorldAreas.Package? next = areas.Next(a);
            string nextText = a.Wave == 0 ? "(held back)" : next == null ? "finished" : $"{next.Id} {next.Name}";
            Console.WriteLine($"  {n,3} {a.Wave,4} {areas.StageOf(a),5}  {a.Name,-42} {a.Regions.Count,7}  {nextText}");
        }
        int built = areas.Areas.Count(a => a.Wave > 0);
        Console.WriteLine($"{built} areas to build ({areas.Areas.Where(a => a.Wave > 0).Sum(a => a.Regions.Count)} regions), one held back; " +
                          string.Join(", ", Enumerable.Range(0, 5).Select(s => $"{areas.Areas.Count(a => a.Wave > 0 && areas.StageOf(a) == s)} at stage {s}")));
        if (!rest.Contains("check"))
        {
            LayoutWorkspace lw = LayoutWorkspace.Load(root);
            if (lw.Map.Count > 0)
            {
                byte[] png = AreaRender.Draw(areas, regionRects, WorldChart.From(lw.Map));
                Directory.CreateDirectory(Path.Combine(root, "docs", "images"));
                File.WriteAllBytes(Path.Combine(root, "docs", "images", "areas.png"), png);
                Console.WriteLine("drew docs/images/areas.png");
            }
        }
        return errors.Count == 0 ? 0 : 1;
    }

    case "plan":
        return PlanCommand(root, ws, rest);

    case "probe":
    {
        // Heights and grounds along a line of the generated landscape: probe x0 y0 x1 y1 [steps]
        LayoutWorkspace lw = LayoutWorkspace.Load(root);
        Landscape land = Landscape.Build(lw.Map, lw.Built, lw.Edits);
        float px0 = float.Parse(rest[0]), py0 = float.Parse(rest[1]), px1 = float.Parse(rest[2]), py1 = float.Parse(rest[3]);
        int steps = rest.Length > 4 ? int.Parse(rest[4]) : 24;
        for (int s = 0; s <= steps; s++)
        {
            float x = px0 + (px1 - px0) * s / steps, y = py0 + (py1 - py0) * s / steps;
            int tx = (int)Math.Floor(x), ty = (int)Math.Floor(y);
            string what = land.InBounds(tx, ty) ? land.GroundId(land.Index(tx, ty)) + (land.Beyond[land.Index(tx, ty)] ? " (beyond)" : "") : "-";
            Console.WriteLine($"({tx,5}, {ty,5})  h {land.Heights.Corner(tx, ty),6:0.00}  {what}");
        }
        return 0;
    }

    case "paint" when rest.Length >= 2 && rest[0] == "area":
    {
        // An area's regions as their data paints them (the real ground, hand work and all): paint area <id> [pixels a tile]
        WorldAreas areas = WorldAreas.Load(WorldAreas.PathIn(root));
        WorldAreas.Area? area = areas.Areas.FirstOrDefault(a => a.Id == rest[1]);
        if (area == null)
        {
            Console.Error.WriteLine($"No area '{rest[1]}' in world/areas.json.");
            return 2;
        }
        MapRender.LoadPalette(root);
        int ppt = rest.Length >= 3 ? int.Parse(rest[2]) : 4;
        var parts = ws.Regions.Where(e => area.Regions.Contains(e.Region.Id)).Select(e => e.Region).ToList();
        string outDir = Path.Combine(root, "world", "renders");
        Directory.CreateDirectory(outDir);
        string file = Path.Combine(outDir, "area-" + area.Id + ".png");
        File.WriteAllBytes(file, PaintRender.Regions(parts, ppt));
        Console.WriteLine($"wrote {Path.GetRelativePath(root, file).Replace('\\', '/')}");
        return 0;
    }

    case "paint":
    {
        // The generated landscape as the client paints it: paint [x0 y0 x1 y1] [pixels a tile] [name]
        LayoutWorkspace lw = LayoutWorkspace.Load(root);
        if (lw.Map.Count == 0)
        {
            foreach (string e in lw.Map.Errors) Console.Error.WriteLine("error: " + e);
            return 1;
        }
        MapRender.LoadPalette(root);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        Landscape land = Landscape.Build(lw.Map, lw.Built, lw.Edits);
        foreach (string w in land.Warnings) Console.WriteLine("warning: " + w);
        Console.WriteLine($"landscape built in {timer.ElapsedMilliseconds} ms");
        string outDir = Path.Combine(root, "world", "renders");
        Directory.CreateDirectory(outDir);
        // One picture, or several from one build: x0 y0 x1 y1 pixels name, repeated.
        var jobs = new List<(int x0, int y0, int x1, int y1, int ppt, string name)>();
        if (rest.Length >= 12 && rest.Length % 6 == 0)
            for (int k = 0; k < rest.Length; k += 6)
                jobs.Add((int.Parse(rest[k]), int.Parse(rest[k + 1]), int.Parse(rest[k + 2]), int.Parse(rest[k + 3]), int.Parse(rest[k + 4]), rest[k + 5]));
        else
            jobs.Add((rest.Length >= 4 ? int.Parse(rest[0]) : land.X0, rest.Length >= 4 ? int.Parse(rest[1]) : land.Y0,
                      rest.Length >= 4 ? int.Parse(rest[2]) : land.X0 + land.W, rest.Length >= 4 ? int.Parse(rest[3]) : land.Y0 + land.H,
                      rest.Length >= 5 ? int.Parse(rest[4]) : 1, rest.Length >= 6 ? rest[5] : "paint"));
        foreach (var j in jobs)
        {
            File.WriteAllBytes(Path.Combine(outDir, j.name + ".png"), PaintRender.Landscape(land, j.x0, j.y0, j.x1, j.y1, j.ppt, true));
            Console.WriteLine($"wrote world/renders/{j.name}.png ({(j.x1 - j.x0) * j.ppt} x {(j.y1 - j.y0) * j.ppt}) at {timer.ElapsedMilliseconds} ms");
        }
        return 0;
    }

    case "strokes":
    {
        // Paths and rivers redrawn from a plan of points (GroundStrokes): strokes <plan.json>
        bool sketch = rest.Contains("--sketch");
        rest = rest.Where(a => a != "--sketch").ToArray();
        if (rest.Length != 1 || !File.Exists(rest[0]))
        {
            Console.Error.WriteLine("Usage: strokes <plan.json> [--sketch] (see shared/World/Ground/GroundStrokes.cs)");
            return 2;
        }
        StrokePlan plan = StrokePlan.Parse(File.ReadAllText(rest[0]));
        var ids = new HashSet<string>(plan.Regions, StringComparer.Ordinal);
        if (plan.Area != null)
        {
            WorldAreas.Area? area = WorldAreas.Load(WorldAreas.PathIn(root)).Areas.FirstOrDefault(a => a.Id == plan.Area);
            if (area == null) { Console.Error.WriteLine($"No area '{plan.Area}' in world/areas.json."); return 2; }
            ids.UnionWith(area.Regions);
        }
        var entries = ws.Regions.Where(e => ids.Contains(e.Region.Id)).ToList();
        // Before the takeover too, so a region taken over whose tiles don't change is still written.
        var before = ws.Regions.ToDictionary(e => e.Region.Id, e => RegionFiles.Format(e.Region));
        foreach (WorldWorkspace.Entry e in entries.Where(e => e.Region.Generated != null))
        {
            if (!plan.TakeOver)
            {
                Console.WriteLine($"note: {e.Region.Id} is generated; the next `blockout` redraws it without these strokes (set \"takeOver\").");
                continue;
            }
            // As the Editor takes a region over (WorldSync): no longer generated, its scene in its area's folder.
            WorldAreas.Area? own = WorldAreas.Load(WorldAreas.PathIn(root)).AreaOf(e.Region.Id);
            e.Region.Generated = null;
            if (own != null && e.Region.Scene.StartsWith("Assets/Scenes/Land/", StringComparison.Ordinal))
                e.Region.Scene = $"Assets/Scenes/{Blockout.Pascal(own.Id)}/{Path.GetFileName(e.Region.Scene)}";
            Console.WriteLine($"{e.Region.Id}: taken over, its scene goes to {e.Region.Scene}. Run `blockout` so the generated land round it follows.");
        }
        ContinentData data = ContinentData.Parse(File.ReadAllText(Path.Combine(root, "world", "layout", "continent.json")));
        foreach (string line in GroundStrokes.Apply(entries.Select(e => e.Region).ToList(), data, plan)) Console.WriteLine(line);
        // The rings of neighbours' tiles follow, here and next door.
        var all = ws.Regions.Where(e => !e.Region.Instanced).Select(e => e.Region).ToList();
        GroundEdit.RefreshSurrounds(all);
        // A river's lowered bed or an island's rise at a border: the neighbours' edges follow.
        var edited = new HashSet<string>(ws.Regions.Where(e => before.TryGetValue(e.Region.Id, out string was) && was != RegionFiles.Format(e.Region)).Select(e => e.Region.Id));
        TerrainSeams.Result seams = TerrainSeams.Follow(ws.Regions.Select(e => e.Region).ToList(), edited);
        foreach (string m in seams.Messages) Console.WriteLine("note: " + m);
        if (seams.GeneratedNeighbours.Count > 0) Console.WriteLine($"note: run `blockout` so the generated land meets them ({string.Join(", ", seams.GeneratedNeighbours)}).");
        // The planning gate (docs/world-areas.md, stage 0): no ground changes in an area whose plan the owner hasn't approved.
        WorldAreas gate = WorldAreas.Load(WorldAreas.PathIn(root));
        var unplanned = ws.Regions.Where(e => before.TryGetValue(e.Region.Id, out string was) && was != RegionFiles.Format(e.Region))
            .Select(e => gate.AreaOf(e.Region.Id)).Where(a => a != null && !gate.Approved(a)).Select(a => a!.Id).Distinct().ToList();
        if (unplanned.Count > 0 && !sketch)
        {
            Console.Error.WriteLine($"error: nothing written: the plan for {string.Join(", ", unplanned)} isn't approved ({WorldAreas.PlanApproved} in world/areas.json). " +
                                    "Plan first (docs/areas/plan-template.md); --sketch writes anyway, for a throwaway branch.");
            return 1;
        }
        if (unplanned.Count > 0) Console.WriteLine($"sketch: writing ground in {string.Join(", ", unplanned)} without an approved plan; not for main.");
        foreach (WorldWorkspace.Entry e in ws.Regions)
            if (before.TryGetValue(e.Region.Id, out string was) && was != RegionFiles.Format(e.Region))
            {
                RegionFiles.SaveRegion(e.Region, e.Path);
                Console.WriteLine("wrote " + Path.GetRelativePath(root, e.Path).Replace(Path.DirectorySeparatorChar, '/'));
            }
        return 0;
    }

    case "terrain":
        return TerrainCommand(root, ws, rest);

    case "seams":
    {
        // Every border whose two sides disagree on the ground's height, mended: built regions meet halfway.
        int n = FollowSeams(root, ws, null);
        Console.WriteLine(n == 0 ? "Every border meets." : $"{n} regions written.");
        return 0;
    }

    case "terrain-save":
    {
        // The Editor's terrain editor saves through here, so the blockout is drawn by .NET as always.
        if (rest.Length != 1 || !File.Exists(rest[0]))
        {
            Console.Error.WriteLine("Usage: terrain-save <heights file written by WorldTerrain.WriteHeights>");
            return 2;
        }
        WorldTerrain.Result saved = WorldTerrain.Save(root, WorldTerrain.ReadHeights(rest[0]));
        foreach (string m in saved.Messages) Console.WriteLine(m);
        foreach (string e in saved.Errors) Console.Error.WriteLine("error: " + e);
        Console.WriteLine("regions: " + string.Join(",", saved.Regions));
        return saved.Errors.Count > 0 ? 1 : 0;
    }

    default:
        Console.WriteLine("""
            R'th world data (docs/workflow.md). Commands:
              check              validate world/regions against the content, and that server/data/maps is up to date
              export             write server/data/maps from world/regions (+ the hand-made overlays)
              render [region...] draw regions to world/renders/<id>.png (and .txt), plus world.png
              format             rewrite world/regions/*.json in the standard layout
              layout [check]     compile world/layout/continent.json: run its checks, write zones.json and
                                 regions.txt, and draw docs/images/layout-*.png (check: only check, and that
                                 the generated files are up to date)
              terrain <region> flat|layout [--force]
                                 give a region terrain to sculpt: flat, or the generated land's heights
                                 (continent.json "terrain", as the blockout draws them); --force replaces
                                 terrain it already has
              terrain-save <file> write heights edited over the whole world (the Editor's Edit Terrain) into
                                 the data: built regions' terrain, generated land's raise (world/layout/sculpt),
                                 then the blockout and the maps
              seams              mend every border where two built regions disagree on the ground's height
                                 (they meet halfway); every command that writes terrain already does this
              blockout [check]   generate the walkable blockout from the layout: world/regions/land_*.json for
                                 every region with core land and the ring around it, then export their maps
                                 (check: only check that the generated regions are up to date)
              plan [check|render|built] <area>
                                 an area's plan (world/plans/<area>.json, docs/areas/plan-template.md): check it is
                                 complete (every place found and hooked, every thing with a why, ties to every
                                 neighbour); render it over the area's ground to world/renders/plan-<area>.png;
                                 compare the regions as built with it. plan alone lists every area's plan
              strokes <plan> [--sketch]
                                 apply a strokes plan (world/strokes); refused in an area whose plan isn't approved
              chart [x0 y0 x1 y1] draw the players' world map's ground (WorldChart + ChartPainter) to
                                 world/renders/chart.png, with rivers and roads roughly on top
            """);
        return command == "help" ? 0 : 2;
}

static AreaPlan.World PlanWorld(string root, WorldWorkspace ws)
{
    string content = Path.Combine(root, "server", "data", "content");
    HashSet<string> Ids(string file)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        string path = Path.Combine(content, file);
        if (!File.Exists(path)) return set;
        foreach (var item in Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(path)))
            if (item["id"] is Newtonsoft.Json.Linq.JToken id) set.Add((string)id!);
        return set;
    }
    var w = new AreaPlan.World
    {
        Areas = WorldAreas.Load(WorldAreas.PathIn(root)),
        Objects = Ids("objects.json"),
        Npcs = Ids("npcs.json"),
        Quests = Ids("quests.json"),
        FileExists = f => File.Exists(Path.Combine(root, f)),
    };
    foreach (WorldWorkspace.Entry e in ws.Regions.Where(e => !e.Region.Instanced))
        w.Regions[e.Region.Id] = (e.Region.OriginX, e.Region.OriginY, e.Region.Width, e.Region.Height);
    string quests = Path.Combine(root, "docs", "quests");
    if (Directory.Exists(quests))
        foreach (string f in Directory.GetFiles(quests, "*.md")) w.Quests.Add(Path.GetFileNameWithoutExtension(f));
    var skills = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(content, "skills.json")))["skills"];
    if (skills is Newtonsoft.Json.Linq.JArray list)
        foreach (var sk in list)
            if (sk["id"] is Newtonsoft.Json.Linq.JToken id) w.Skills.Add((string)id!);
    return w;
}

static int PlanCommand(string root, WorldWorkspace ws, string[] args)
{
    AreaPlan.World w = PlanWorld(root, ws);
    if (args.Length == 0)
    {
        foreach (WorldAreas.Area a in w.Areas.Areas.Where(a => a.Wave > 0))
        {
            string path = AreaPlan.PathFor(root, a.Id);
            string state = !File.Exists(path) ? "no plan" : AreaPlan.Parse(File.ReadAllText(path)).Status;
            Console.WriteLine($"  {a.Wave,2}  {a.Id,-34} {state,-8} {(w.Areas.Approved(a) ? "approved " + a.Done[WorldAreas.PlanApproved] : "")}");
        }
        return 0;
    }
    string verb = args.Length >= 2 ? args[0] : "check";
    string areaId = args[^1];
    string file = AreaPlan.PathFor(root, areaId);
    if (!File.Exists(file)) { Console.Error.WriteLine($"No plan: world/plans/{areaId}.json (docs/areas/plan-template.md)."); return 2; }
    AreaPlan plan;
    try { plan = AreaPlan.Parse(File.ReadAllText(file)); }
    catch (FormatException e) { Console.Error.WriteLine($"error: {areaId}.json: {e.Message}"); return 1; }
    WorldAreas.Area? area = w.Areas.Areas.FirstOrDefault(a => a.Id == areaId);
    var regions = area == null ? new List<RegionData>() : ws.Regions.Where(e => area.Regions.Contains(e.Region.Id)).Select(e => e.Region).ToList();
    switch (verb)
    {
        case "check":
        {
            List<string> errors = plan.Check(w);
            foreach (string e in errors) Console.Error.WriteLine("error: " + e);
            Console.WriteLine($"{plan.Places.Count} places, {plan.Buildings.Count} buildings, {plan.Contents.Count} content groups " +
                              $"({plan.Contents.Sum(c => c.Count)} things), {plan.Dungeons.Count} below ground, {plan.Heights.Count} heights, {plan.Reserved.Count} kept for later.");
            foreach (var kv in plan.Skills()) Console.WriteLine($"  {kv.Key}: {string.Join(", ", kv.Value)}");
            Console.WriteLine(errors.Count == 0 ? "The plan is complete." : $"{errors.Count} things missing.");
            return errors.Count == 0 ? 0 : 1;
        }
        case "render":
        {
            if (regions.Count == 0) { Console.Error.WriteLine($"No regions for area '{areaId}'."); return 2; }
            MapRender.LoadPalette(root);
            string outDir = Path.Combine(root, "world", "renders");
            Directory.CreateDirectory(outDir);
            string png = Path.Combine(outDir, "plan-" + areaId + ".png");
            int ppt = args.Length >= 3 && int.TryParse(args[1], out int p) ? p : 4;
            File.WriteAllBytes(png, PlanRender.Draw(plan, regions, ppt));
            Console.WriteLine("wrote " + Path.GetRelativePath(root, png).Replace(Path.DirectorySeparatorChar, '/'));
            return 0;
        }
        case "built":
        {
            List<string> notes = plan.Built(regions);
            foreach (string n in notes) Console.WriteLine(n);
            Console.WriteLine(notes.Count == 0 ? "Built as planned." : $"{notes.Count} differences from the plan.");
            return 0;
        }
        default:
            Console.Error.WriteLine("Usage: plan [check|render [pixels]|built] <area>");
            return 2;
    }
}

static int Layout(string root, string[] args)
{
    bool checkOnly = args.Contains("check");
    LayoutWorkspace lw = LayoutWorkspace.Load(root);
    Console.Write(lw.Report.Text(lw.Map));
    if (checkOnly)
    {
        List<string> stale = lw.Stale();
        foreach (string s in stale) Console.Error.WriteLine($"error: {Path.GetRelativePath(root, s).Replace('\\', '/')} is out of date: run `layout`");
        return lw.Report.HasErrors || stale.Count > 0 ? 1 : 0;
    }
    if (lw.Map.Count > 0)
    {
        foreach (string path in lw.Write()) Console.WriteLine("wrote " + Path.GetRelativePath(root, path).Replace('\\', '/'));
        string images = Path.Combine(root, "docs", "images");
        Directory.CreateDirectory(images);
        void Png(string name, LayoutRender.Options o)
        {
            File.WriteAllBytes(Path.Combine(images, name), LayoutRender.Draw(lw.Map, lw.Report, lw.Built, o));
            Console.WriteLine("drew docs/images/" + name);
        }
        Png("layout-continent.png", LayoutRender.Whole(lw.Map, 6, false, "R'th: the continent layout (grounds)"));
        Png("layout-powers.png", LayoutRender.Whole(lw.Map, 4, true, "R'th: the powers"));
        Png("layout-heartland.png", new LayoutRender.Options
        {
            X0 = -288, Y0 = -192, X1 = 576, Y1 = 480, ZonePx = 16, RegionKeys = true,
            Title = "Tolling, Palmerway and Cruor (region keys: rx,ry)",
        });
    }
    return lw.Report.HasErrors ? 1 : 0;
}

// Terrain for a region (docs/world-guide.md section 4): heights in its data, to sculpt from.
static int TerrainCommand(string root, WorldWorkspace ws, string[] args)
{
    if (args.Length < 2 || (args[1] != "flat" && args[1] != "layout"))
    {
        Console.Error.WriteLine("Usage: terrain <region> flat|layout [--force]");
        return 2;
    }
    WorldWorkspace.Entry? e = ws.Find(args[0]);
    if (e == null)
    {
        Console.Error.WriteLine($"No region '{args[0]}' in world/regions.");
        return 1;
    }
    RegionData region = e.Region;
    if (region.Terrain != null && !args.Contains("--force"))
    {
        Console.Error.WriteLine($"{region.Id} already has terrain; --force replaces it (sculpted work would be lost).");
        return 1;
    }
    if (region.Generated != null)
        Console.WriteLine($"note: {region.Id} is generated; `blockout` would overwrite it. Delete its \"generated\" line to keep the terrain.");
    Heightfield field;
    if (args[1] == "flat") field = new Heightfield(region.OriginX, region.OriginY, region.Width, region.Height);
    else
    {
        // The generated land's heights (continent.json "terrain"), as the blockout gives them.
        LayoutWorkspace lw = LayoutWorkspace.Load(root);
        field = Blockout.Heights(lw.Map, lw.Built, lw.Edits).Field(region.OriginX, region.OriginY, region.Width, region.Height)
                ?? new Heightfield(region.OriginX, region.OriginY, region.Width, region.Height);
    }
    region.Terrain = TerrainText.Write(field, region.Terrain?.CliffStep ?? 1f);
    RegionFiles.SaveRegion(region, e.Path);
    Console.WriteLine($"{region.Id}: {args[1]} terrain written.");
    FollowSeams(root, ws, new[] { region.Id });
    Console.WriteLine("Run `export`.");
    return 0;
}

// The built neighbours of regions whose terrain was written take up their edges (null: mend every border).
static int FollowSeams(string root, WorldWorkspace ws, ICollection<string>? written)
{
    TerrainSeams.Result seams = TerrainSeams.Follow(ws.Regions.Select(e => e.Region).ToList(), written);
    foreach (string m in seams.Messages) Console.WriteLine("note: " + m);
    foreach (string id in seams.Changed)
    {
        WorldWorkspace.Entry e = ws.Find(id)!;
        RegionFiles.SaveRegion(e.Region, e.Path);
        Console.WriteLine($"wrote {Path.GetRelativePath(root, e.Path).Replace(Path.DirectorySeparatorChar, '/')} (its border meets its neighbours')");
    }
    if (seams.GeneratedNeighbours.Count > 0)
        Console.WriteLine($"note: run `blockout` so the generated land meets the built edges ({string.Join(", ", seams.GeneratedNeighbours)}).");
    return seams.Changed.Count;
}

// The walkable blockout (L2, docs/layout.md): regions generated from the layout.
static int BlockoutCommand(string root, string[] args)
{
    LayoutWorkspace lw = LayoutWorkspace.Load(root);
    if (lw.Map.Count == 0)
    {
        foreach (string e in lw.Map.Errors) Console.Error.WriteLine("error: " + e);
        return 1;
    }
    var timer = System.Diagnostics.Stopwatch.StartNew();
    Blockout.Result result = Blockout.Generate(lw.Map, lw.Built, lw.Edits);
    string regions = Path.Combine(root, "world", "regions");
    (Dictionary<string, string> write, List<string> delete) = Blockout.Changes(regions, result);
    foreach (string w in result.Warnings) Console.WriteLine("warning: " + w);
    string Rel(string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
    if (args.Contains("check"))
    {
        foreach (string path in write.Keys) Console.Error.WriteLine($"error: {Rel(path)} is out of date with the layout: run `blockout`");
        foreach (string path in delete) Console.Error.WriteLine($"error: {Rel(path)} is no longer in the layout: run `blockout`");
        Console.WriteLine($"{result.Regions.Count} blockout regions, {write.Count + delete.Count} out of date.");
        return write.Count + delete.Count > 0 ? 1 : 0;
    }
    foreach (KeyValuePair<string, string> f in write) File.WriteAllText(f.Key, f.Value);
    string maps = Path.Combine(root, "server", "data", "maps");
    foreach (string path in delete)
    {
        File.Delete(path);
        string id = Path.GetFileNameWithoutExtension(path);
        foreach (string map in Directory.Exists(maps) ? Directory.GetFiles(maps, id + ".*") : Array.Empty<string>())
            if (Path.GetFileName(map).Split('.')[0] == id) File.Delete(map);
        Console.WriteLine("deleted " + Rel(path));
    }
    Console.WriteLine($"{result.Regions.Count} blockout regions: {write.Count} written, {delete.Count} deleted ({timer.ElapsedMilliseconds} ms).");

    WorldWorkspace ws = WorldWorkspace.Load(root);
    int failed = 0;
    foreach (WorldWorkspace.Entry e in ws.Regions)
        foreach (string error in e.Compiled.Errors)
        {
            Console.Error.WriteLine($"error: {e.Region.Id}: {error}");
            failed++;
        }
    List<string> changed = ws.Export();
    Console.WriteLine(changed.Count == 0 ? "Maps are up to date." : $"{changed.Count} map files written.");
    return failed > 0 ? 1 : 0;
}

// The ground the in-game world map paints (docs/interface.md), to look at without Unity.
static int Chart(string root, string[] args)
{
    LayoutWorkspace lw = LayoutWorkspace.Load(root);
    if (lw.Map.Count == 0)
    {
        foreach (string e in lw.Map.Errors) Console.Error.WriteLine("error: " + e);
        return 1;
    }
    var timer = System.Diagnostics.Stopwatch.StartNew();
    WorldChart chart = WorldChart.From(lw.Map);
    string json = Newtonsoft.Json.JsonConvert.SerializeObject(chart);
    long built = timer.ElapsedMilliseconds;
    byte[] rgba = ChartPainter.Paint(chart, null, 1, out int w, out int h);
    Console.WriteLine($"chart: {json.Length / 1024} KB as JSON (ground {chart.Ground.Length / 1024} KB, power {chart.Power.Length / 1024} KB), built in {built} ms; painted {w} x {h} in {timer.ElapsedMilliseconds - built} ms");
    LayoutRender.StrokeChartLines(chart, rgba, w, h);
    int x0 = 0, y0 = 0, x1 = w, y1 = h;
    if (args.Length == 4)
    {
        x0 = int.Parse(args[0]) - chart.OriginX; y0 = int.Parse(args[1]) - chart.OriginY;
        x1 = int.Parse(args[2]) - chart.OriginX; y1 = int.Parse(args[3]) - chart.OriginY;
    }
    int cw = x1 - x0, ch = y1 - y0;
    var rgb = new byte[cw * ch * 3];
    for (int y = 0; y < ch; y++)
        for (int x = 0; x < cw; x++)
        {
            int src = ((y1 - 1 - y) * w + x0 + x) * 4, dst = (y * cw + x) * 3;
            rgb[dst] = rgba[src]; rgb[dst + 1] = rgba[src + 1]; rgb[dst + 2] = rgba[src + 2];
        }
    string outDir = Path.Combine(root, "world", "renders");
    Directory.CreateDirectory(outDir);
    File.WriteAllBytes(Path.Combine(outDir, "chart.png"), MapRender.Png(cw, ch, rgb));
    Console.WriteLine("wrote world/renders/chart.png");
    return 0;
}

static int Check(WorldWorkspace ws)
{
    var warnings = new List<string>();
    List<string> errors = ws.Check(warnings);
    foreach (string w in warnings) Console.WriteLine("warning: " + w);
    foreach (string e in errors) Console.Error.WriteLine("error: " + e);
    Console.WriteLine($"{ws.Regions.Count} regions, {errors.Count} errors, {warnings.Count} warnings.");
    return errors.Count == 0 ? 0 : 1;
}
