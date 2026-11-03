using Rth.Shared.World.Data;
using static Rth.WorldTool.LayoutRender;

namespace Rth.WorldTool;

/// <summary>
/// An area's plan drawn over its painted ground (docs/areas/plan-template.md, stage 0.3): heights
/// and their jobs, routes, buildings, content by kind with its counts, dungeon entrances, places and
/// the space kept for later, each labelled, so the plan is looked at rather than only read.
/// </summary>
internal static class PlanRender
{
    private static readonly Rgb Ink = new(250, 250, 245), Shadow = new(0, 0, 0);
    private static readonly Rgb HeightInk = new(255, 214, 90), RouteInk = new(235, 235, 235), BuildingInk = new(255, 150, 60);
    private static readonly Rgb ReserveInk = new(170, 200, 255), DungeonInk = new(255, 70, 70), PlaceInk = new(255, 255, 255);

    private static Rgb KindInk(string kind) => kind switch
    {
        "node" => new Rgb(110, 230, 120),
        "creature" => new Rgb(255, 95, 95),
        "station" => new Rgb(255, 170, 230),
        "npc" => new Rgb(140, 210, 255),
        "view" => new Rgb(255, 240, 150),
        "shortcut" => new Rgb(200, 160, 255),
        _ => new Rgb(220, 220, 220),
    };

    public static byte[] Draw(AreaPlan plan, IReadOnlyList<RegionData> regions, int ppt)
    {
        byte[] rgba = PaintRender.RegionsRgba(regions, ppt, out int w, out int h, out int x0, out int y0);
        const int legend = 64;
        var ground = Canvas.FromRgba(rgba, w, h);   // rows from the south, as the painter writes them
        var c = new Canvas(w, h + legend);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) c.Set(x, y, ground.Get(x, h - 1 - y));
        // Dim the ground so the plan reads over it.
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) c.Blend(x, y, new Rgb(20, 24, 30), 0.35f);

        float Px(float x) => (x - x0) * ppt;
        float Py(float y) => h - (y - y0) * ppt;
        (int, int, int, int) Box(int[] r) => ((int)Px(r[0]), (int)Py(r[3]), (r[2] - r[0]) * ppt, (r[3] - r[1]) * ppt);
        List<(float, float)> Pts(List<float[]> pts) => pts.Select(p => (Px(p[0]), Py(p[1]))).ToList();

        foreach (AreaPlan.Reserve r in plan.Reserved.Where(r => r.Rect != null))
        {
            var (bx, by, bw, bh) = Box(r.Rect!);
            for (int y = by; y < by + bh; y++)
                for (int x = bx; x < bx + bw; x++)
                    if ((x + y) % 8 < 2) c.Blend(x, y, ReserveInk, 0.6f);
            c.Frame(bx, by, bw, bh, ReserveInk, 1);
            c.Text(bx + 3, by + 3, "KEPT: " + r.Id, 1, ReserveInk, Shadow);
        }
        foreach (AreaPlan.Height ht in plan.Heights.Where(ht => ht.Outline.Count >= 2))
        {
            var pts = Pts(ht.Outline);
            c.Polyline(pts, 2, HeightInk, 0);
            var (lx, ly) = pts[0];
            c.Text((int)lx + 3, (int)ly + 3, $"{ht.Id} {ht.Rise:+0.#;-0.#} {ht.Job.ToUpperInvariant()}", 1, HeightInk, Shadow);
        }
        foreach (AreaPlan.Route r in plan.Routes.Where(r => r.Points.Count >= 2))
            c.Polyline(Pts(r.Points), r.Kind == "main" ? 2 : 1, r.Kind == "river" ? new Rgb(120, 190, 255) : RouteInk, r.Kind == "path" || r.Kind == "track" ? 6 : 0);
        foreach (AreaPlan.Building b in plan.Buildings.Where(b => b.Rect != null))
        {
            var (bx, by, bw, bh) = Box(b.Rect!);
            c.Frame(bx, by, bw, bh, BuildingInk, 2);
        }
        foreach (AreaPlan.Content ct in plan.Contents.Where(ct => ct.Rect != null))
        {
            var (bx, by, bw, bh) = Box(ct.Rect!);
            Rgb ink = KindInk(ct.Kind);
            c.Frame(bx, by, Math.Max(bw, 3), Math.Max(bh, 3), ink, 1);
            string label = (ct.Object ?? ct.Npc ?? ct.Id).Replace('_', ' ') + (ct.Count > 1 ? " X" + ct.Count : "");
            c.Text(bx + 2, by + bh + 2, label, 1, ink, Shadow);
        }
        foreach (AreaPlan.Dungeon d in plan.Dungeons.Where(d => d.Entrance != null))
        {
            int dx = (int)Px(d.Entrance![0] + 0.5f), dy = (int)Py(d.Entrance[1] + 0.5f);
            c.Diamond(dx, dy, 5, DungeonInk);
            c.Text(dx + 8, dy - 3, "BELOW: " + d.Name, 1, DungeonInk, Shadow);
        }
        foreach (AreaPlan.Place p in plan.Places)
        {
            int[]? at = p.At ?? (p.Rect != null ? new[] { (p.Rect[0] + p.Rect[2]) / 2, (p.Rect[1] + p.Rect[3]) / 2 } : null);
            if (at == null) continue;
            c.TextCentred((int)Px(at[0] + 0.5f), (int)Py(at[1] + 0.5f) - 14, p.Name, 2, PlaceInk, Shadow);
        }

        c.Fill(0, h, w, legend, new Rgb(16, 18, 22));
        c.Text(8, h + 6, $"PLAN: {plan.Area}  ({plan.Status})", 2, Ink, null);
        int lx2 = 8, ly2 = h + 30;
        void Key(string name, Rgb ink) { c.Fill(lx2, ly2, 10, 7, ink); c.Text(lx2 + 14, ly2, name, 1, ink, null); lx2 += 14 + name.Length * 6 + 14; }
        Key("HEIGHT", HeightInk); Key("ROUTE", RouteInk); Key("BUILDING", BuildingInk); Key("NODE", KindInk("node"));
        Key("CREATURE", KindInk("creature")); Key("STATION", KindInk("station")); Key("NPC", KindInk("npc"));
        Key("VIEW", KindInk("view")); Key("SHORTCUT", KindInk("shortcut")); Key("BELOW", DungeonInk); Key("KEPT", ReserveInk);
        string skills = string.Join("  ", plan.Skills().Select(kv => $"{kv.Key.ToUpperInvariant()} {kv.Value.Count}"));
        c.Text(8, h + 46, "SKILLS: " + skills, 1, Ink, null);
        return c.Png();
    }
}
