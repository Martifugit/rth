using Rth.Shared.World.Data;
using Rth.Shared.World.Layout;
using Canvas = Rth.WorldTool.LayoutRender.Canvas;
using Rgb = Rth.WorldTool.LayoutRender.Rgb;

namespace Rth.WorldTool;

/// <summary>
/// The coverage map (docs/world-areas.md): the continent, each area outlined and numbered and
/// tinted by the stage it has reached, so where the world stands can be seen at a glance.
/// </summary>
public static class AreaRender
{
    /// <summary>Stage colours, from not started to finished.</summary>
    private static readonly Rgb[] StageColour =
    {
        new(150, 150, 150), // 0: generated, untouched
        new(70, 140, 230),  // 1: geography
        new(240, 200, 60),  // 2: structure
        new(80, 200, 90),   // 3: life
        new(250, 250, 250), // 4: finished
    };

    public static byte[] Draw(WorldAreas areas, Dictionary<string, (int x, int y, int w, int h)> regions, WorldChart chart)
    {
        const int tilesPerPixel = 2;
        byte[] rgba = ChartPainter.Paint(chart, null, tilesPerPixel, out int w, out int h);
        var c = new Canvas(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int o = ((h - 1 - y) * w + x) * 4;
                c.Set(x, y, new Rgb(rgba[o], rgba[o + 1], rgba[o + 2]));
            }
        int Px(int tx) => (tx - chart.OriginX) / tilesPerPixel;
        int Py(int ty) => h - 1 - (ty - chart.OriginY) / tilesPerPixel;

        // Each region: its area's stage as a tint; outlines where the area changes.
        var areaOf = new Dictionary<(int, int), WorldAreas.Area>();
        foreach (WorldAreas.Area a in areas.Areas)
            foreach (string r in a.Regions)
                if (regions.TryGetValue(r, out var rect)) areaOf[(rect.x, rect.y)] = a;
        int size = chart.RegionTiles;
        foreach (var e in areaOf)
        {
            (int rx, int ry) = e.Key;
            WorldAreas.Area a = e.Value;
            bool held = a.Wave == 0;
            Rgb tint = StageColour[Math.Min(4, areas.StageOf(a))];
            for (int ty = ry; ty < ry + size; ty += tilesPerPixel)
                for (int tx = rx; tx < rx + size; tx += tilesPerPixel)
                    c.Blend(Px(tx), Py(ty), held ? new Rgb(20, 24, 30) : tint, held ? 0.45f : 0.35f);
            // Borders against another area (or nothing).
            WorldAreas.Area? N(int dx, int dy) => areaOf.TryGetValue((rx + dx * size, ry + dy * size), out var n) ? n : null;
            Rgb line = new(15, 15, 18);
            if (N(-1, 0) != a) c.VLine(Px(rx), Py(ry + size), Py(ry) + 1, line, 0.9f);
            if (N(1, 0) != a) c.VLine(Px(rx + size) - 1, Py(ry + size), Py(ry) + 1, line, 0.9f);
            if (N(0, -1) != a) c.HLine(Px(rx), Px(rx + size), Py(ry), line, 0.9f);
            if (N(0, 1) != a) c.HLine(Px(rx), Px(rx + size), Py(ry + size) + 1, line, 0.9f);
        }

        // Each area's number (its place in the list) at the middle of its regions.
        int n = 0;
        foreach (WorldAreas.Area a in areas.Areas)
        {
            n++;
            if (a.Wave == 0) continue;
            var cells = a.Regions.Where(regions.ContainsKey).Select(r => regions[r]).ToList();
            if (cells.Count == 0) continue;
            int cx = (int)cells.Average(r => r.x + r.w / 2f), cy = (int)cells.Average(r => r.y + r.h / 2f);
            c.TextCentred(Px(cx), Py(cy) - 6, n.ToString(), 2, new Rgb(255, 255, 255), new Rgb(0, 0, 0));
            c.TextCentred(Px(cx), Py(cy) + 10, "S" + areas.StageOf(a), 1, StageColour[Math.Min(4, areas.StageOf(a))], new Rgb(0, 0, 0));
        }
        c.Text(12, h - 40, "R'TH: AREAS BY STAGE   GREY 0 LAYOUT  BLUE 1 GEOGRAPHY  YELLOW 2 STRUCTURE  GREEN 3 LIFE  WHITE 4 FINISHED", 1, new Rgb(230, 230, 230), new Rgb(0, 0, 0));
        return c.Png();
    }
}
