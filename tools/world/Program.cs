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

    case "grounds":
    {
        // The world's kinds of ground (world/grounds.json), gathered from the regions: one terrain layer each.
        GroundTable table = GroundTable.FromRegions(ws.Regions.Select(e => e.Region));
        table.Save(GroundTable.PathIn(root));
        Console.WriteLine($"world/grounds.json: {table.Grounds.Count} grounds, {table.Stones.Count} kinds of stones, {table.Materials.Count} materials.");
        return 0;
    }

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

    default:
        Console.WriteLine("""
            R'th world data (docs/workflow.md). Commands:
              check              validate world/regions against the content, and that server/data/maps is up to date
              export             write server/data/maps from world/regions (+ the hand-made overlays)
              render [region...] draw regions to world/renders/<id>.png (and .txt), plus world.png
              format             rewrite world/regions/*.json in the standard layout
              grounds            gather the regions' kinds of ground into world/grounds.json (a terrain layer each)
              paint area <id> [pixels a tile]
                                 an area's regions painted as the client paints their ground, to world/renders
              layout [check]     compile world/layout/continent.json: run its checks, write zones.json and
                                 regions.txt, and draw docs/images/layout-*.png (check: only check, and that
                                 the generated files are up to date)
              plan [check|render|built] <area>
                                 an area's plan (world/plans/<area>.json, docs/areas/plan-template.md): check it is
                                 complete (every place found and hooked, every thing with a why, ties to every
                                 neighbour); render it over the area's ground to world/renders/plan-<area>.png;
                                 compare the regions as built with it. plan alone lists every area's plan
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
