using Rth.Shared.World.Data;
using Rth.Shared.World.Layout;
using Rth.WorldTool;

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
              blockout [check]   generate the walkable blockout from the layout: world/regions/land_*.json for
                                 every region with core land and the ring around it, then export their maps
                                 (check: only check that the generated regions are up to date)
              chart [x0 y0 x1 y1] draw the players' world map's ground (WorldChart + ChartPainter) to
                                 world/renders/chart.png, with rivers and roads roughly on top
            """);
        return command == "help" ? 0 : 2;
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
    Blockout.Result result = Blockout.Generate(lw.Map, lw.Built);
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
