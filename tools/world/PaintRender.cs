using Rth.Shared.World;
using Rth.Shared.World.Data;
using Rth.Shared.World.Ground;

namespace Rth.WorldTool;

/// <summary>
/// Pictures of the painted ground (docs/world-plan.md section 7), drawn by the same painter the
/// client uses (<see cref="GroundPainter"/>) and lit by a low sun, so the ground can be judged
/// without Unity.
/// </summary>
public static class PaintRender
{
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
