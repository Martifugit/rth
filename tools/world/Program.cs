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
            File.WriteAllBytes(Path.Combine(outDir, "world.png"), MapRender.World(ws.Regions.Select(e => e.Compiled).ToList(), 2));
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

static int Check(WorldWorkspace ws)
{
    var warnings = new List<string>();
    List<string> errors = ws.Check(warnings);
    foreach (string w in warnings) Console.WriteLine("warning: " + w);
    foreach (string e in errors) Console.Error.WriteLine("error: " + e);
    Console.WriteLine($"{ws.Regions.Count} regions, {errors.Count} errors, {warnings.Count} warnings.");
    return errors.Count == 0 ? 0 : 1;
}
