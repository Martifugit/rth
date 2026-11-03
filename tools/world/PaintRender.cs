using Rth.Shared.World;
using Rth.Shared.World.Data;
using Rth.Shared.World.Ground;
using Rth.Shared.World.Layout;

namespace Rth.WorldTool;

/// <summary>
/// Pictures of the painted ground (docs/world-plan.md section 7), drawn by the same painter the
/// client uses (<see cref="GroundPainter"/>) and lit by a low sun, so a change to the layout or the
/// painting can be judged without Unity.
/// </summary>
public static class PaintRender
{
    /// <summary>A rectangle of the generated landscape, <paramref name="ppt"/> pixels a tile, as a PNG.</summary>
    public static byte[] Landscape(Landscape land, int x0, int y0, int x1, int y1, int ppt, bool labels)
    {
        ContinentData data = land.Map.Data;
        const int margin = 4;
        int w = x1 - x0, h = y1 - y0;
        var letters = new Dictionary<char, int>();
        var looks = new List<GroundLook>();
        var cliffs = new List<float>();
        int Look(char c)
        {
            if (letters.TryGetValue(c, out int k)) return k;
            k = looks.Count;
            letters[c] = k;
            (string id, bool beyond) = land.Decode(c);
            var g = new KeyValuePair<string, GroundDef>(id, data.Grounds[id]);
            float[] rgb = Colour(g.Value.Palette) ?? new[] { 0.4f, 0.55f, 0.3f };
            if (beyond) rgb = PaintedGround.Haze(rgb);
            GroundLayer layer = g.Value.Water ? GroundLayer.Water : g.Value.Layer == "over" ? GroundLayer.Over : GroundLayer.Under;
            GroundLook look = GroundLook.Of(rgb[0], rgb[1], rgb[2], layer, layer == GroundLayer.Over ? 1 + k : 1);
            look.Shelf = !beyond && !g.Value.Water && !g.Value.Walkable && data.Terrain?.RidgeGrounds.Contains(g.Key) != true;
            look = look.Path(g.Value.Layer == "soft");
            looks.Add(look);
            float[]? cliff = Colour(g.Value.Cliff);
            cliffs.AddRange(cliff ?? new[] { -1f, -1f, -1f });
            return k;
        }
        char sea = data.Grounds[data.SeaGround].Letter[0];
        var grid = new int[(w + 2 * margin) * (h + 2 * margin)];
        for (int y = 0; y < h + 2 * margin; y++)
            for (int x = 0; x < w + 2 * margin; x++)
            {
                int ax = x0 - margin + x, ay = y0 - margin + y;
                grid[y * (w + 2 * margin) + x] = Look(land.InBounds(ax, ay) ? land.Char(land.Index(ax, ay)) : sea);
            }
        var canvas = new GroundCanvas
        {
            X0 = x0, Y0 = y0, Width = w, Height = h, Margin = margin, Kinds = grid, Looks = looks.ToArray(),
            Field = land.Heights.Field(x0, y0, w, h) ?? new Heightfield(x0, y0, w, h), CliffColours = cliffs.ToArray(),
        };
        // The stones the blockout would scatter over grounds that carry them.
        ScatterDef? Scatter(int x, int y)
        {
            if (!land.InBounds(x, y)) return null;
            (string id, bool beyond) = land.Decode(land.Char(land.Index(x, y)));
            return beyond ? null : data.Grounds[id].Stones;
        }
        var stoneIds = data.Stones.Keys.ToList();
        canvas.StoneLooks = stoneIds.Select(id => Colour(data.Stones[id].Palette) ?? new[] { 0.53f, 0.5f, 0.4f })
            .SelectMany(c => GroundStones.Levels.Select(l => new StoneLook { R = c[0], G = c[1], B = c[2], Share = l })).ToArray();
        canvas.Stones = new int[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                ScatterDef? s = Scatter(x0 + x, y0 + y);
                int level = s == null ? -1 : GroundStones.Spread(x0 + x, y0 + y, s.Share, (px, py) => Scatter(px, py)?.Kind == s.Kind);
                canvas.Stones[y * w + x] = level < 0 ? -1 : stoneIds.IndexOf(s!.Kind) * GroundStones.Levels.Length + level;
            }
        byte[] rgba = GroundPainter.Paint(canvas, ppt, out int pw, out int ph);
        GroundStones.Draw(rgba, pw, ph, ppt, x0, y0, GroundStones.Scatter(canvas));
        GroundPainter.Shade(rgba, pw, ph, ppt, canvas);
        if (labels) Marks(land, rgba, pw, ph, x0, y0, ppt);
        return ToPng(rgba, pw, ph);
    }

    /// <summary>A painted region from its data (as Sync World and the client draw it).</summary>
    public static byte[]? Region(RegionData region, int ppt)
    {
        Piece? piece = region.Pieces.FirstOrDefault(p => p.Kind == PieceKind.Tiles && p.Paint == true);
        if (piece == null) return null;
        Heightfield field = TerrainText.Read(region) ?? new Heightfield(region.OriginX, region.OriginY, region.Width, region.Height);
        GroundCanvas canvas = PaintedGround.Canvas(region, piece, m => region.Materials.TryGetValue(m, out string? v) ? Colour(v) : null, field);
        byte[] rgba = GroundPainter.Paint(canvas, ppt, out int w, out int h);
        GroundStones.Draw(rgba, w, h, ppt, region.OriginX, region.OriginY, GroundStones.Scatter(canvas));
        GroundPainter.Shade(rgba, w, h, ppt, canvas);
        return ToPng(rgba, w, h);
    }

    /// <summary>Several painted regions from their data, side by side in one picture (north up).</summary>
    public static byte[] Regions(IReadOnlyList<RegionData> regions, int ppt)
    {
        byte[] all = RegionsRgba(regions, ppt, out int w, out int h, out _, out _);
        return ToPng(all, w, h);
    }

    /// <summary>The regions painted, as RGBA rows from the north; (x0, y0) is the south-west tile.</summary>
    public static byte[] RegionsRgba(IReadOnlyList<RegionData> regions, int ppt, out int w, out int h, out int x0, out int y0)
    {
        x0 = regions.Min(r => r.OriginX); y0 = regions.Min(r => r.OriginY);
        int x1 = regions.Max(r => r.OriginX + r.Width), y1 = regions.Max(r => r.OriginY + r.Height);
        w = (x1 - x0) * ppt; h = (y1 - y0) * ppt;
        var all = new byte[w * h * 4];
        foreach (RegionData region in regions)
        {
            Piece? piece = region.Pieces.FirstOrDefault(p => p.Kind == PieceKind.Tiles && p.Paint == true);
            if (piece == null) continue;
            Heightfield field = TerrainText.Read(region) ?? new Heightfield(region.OriginX, region.OriginY, region.Width, region.Height);
            GroundCanvas canvas = PaintedGround.Canvas(region, piece, m => region.Materials.TryGetValue(m, out string? v) ? Colour(v) : null, field);
            byte[] rgba = GroundPainter.Paint(canvas, ppt, out int rw, out int rh);
            GroundStones.Draw(rgba, rw, rh, ppt, region.OriginX, region.OriginY, GroundStones.Scatter(canvas));
            GroundPainter.Shade(rgba, rw, rh, ppt, canvas);
            int ox = (region.OriginX - x0) * ppt, oy = (region.OriginY - y0) * ppt;
            for (int y = 0; y < rh; y++)
                Buffer.BlockCopy(rgba, y * rw * 4, all, ((oy + y) * w + ox) * 4, rw * 4);
        }
        return all;
    }

    /// <summary>Small marks for places (white) and sites (red), so a picture can be read against the layout.</summary>
    private static void Marks(Landscape land, byte[] rgba, int w, int h, int x0, int y0, int ppt)
    {
        void Dot(float x, float y, byte r, byte g, byte b, int size)
        {
            int cx = (int)((x - x0) * ppt), cy = (int)((y - y0) * ppt);
            for (int dy = -size; dy <= size; dy++)
                for (int dx = -size; dx <= size; dx++)
                {
                    int px = cx + dx, py = cy + dy;
                    if (px < 0 || py < 0 || px >= w || py >= h) continue;
                    bool ring = Math.Max(Math.Abs(dx), Math.Abs(dy)) == size;
                    int o = (py * w + px) * 4;
                    rgba[o] = ring ? (byte)0 : r; rgba[o + 1] = ring ? (byte)0 : g; rgba[o + 2] = ring ? (byte)0 : b;
                }
        }
        int s = Math.Max(1, ppt);
        foreach (PlaceDef p in land.Map.Data.Places) Dot(p.At[0], p.At[1], 255, 255, 255, p.Tier == "city" ? s + 2 : p.Tier == "town" ? s + 1 : s);
        foreach (SiteDef site in land.Map.Data.Sites) Dot(site.At[0], site.At[1], 230, 40, 40, Math.Max(1, s - 1));
    }

    private static float[]? Colour(string? name)
    {
        if (name == null) return null;
        if (MapRender.Palette.TryGetValue(name, out string? hex)) return PaintedGround.Hex(hex);
        return PaintedGround.Hex(name);
    }

    /// <summary>RGBA rows from the south (a texture's order) to a PNG, north up.</summary>
    private static byte[] ToPng(byte[] rgba, int w, int h)
    {
        var rgb = new byte[w * h * 3];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int src = ((h - 1 - y) * w + x) * 4, dst = (y * w + x) * 3;
                rgb[dst] = rgba[src]; rgb[dst + 1] = rgba[src + 1]; rgb[dst + 2] = rgba[src + 2];
            }
        return MapRender.Png(w, h, rgb);
    }
}
