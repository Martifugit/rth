using Rth.Shared.World.Data;
using Rth.Shared.World.Ground;

namespace Rth.WorldTool;

/// <summary>
/// Regions' ground as flat colours, a tile at a time, for drawing plans over (the real look is the
/// Unity terrains'): each tile in its ground's colour, holes black.
/// </summary>
public static class GroundRender
{
    /// <summary>The regions' ground as RGBA rows from the south; (x0, y0) is the south-west tile.</summary>
    public static byte[] RegionsRgba(IReadOnlyList<RegionData> regions, int ppt, out int w, out int h, out int x0, out int y0)
    {
        x0 = regions.Min(r => r.OriginX); y0 = regions.Min(r => r.OriginY);
        int x1 = regions.Max(r => r.OriginX + r.Width), y1 = regions.Max(r => r.OriginY + r.Height);
        w = (x1 - x0) * ppt; h = (y1 - y0) * ppt;
        var all = new byte[w * h * 4];
        for (int i = 3; i < all.Length; i += 4) all[i] = 255;
        foreach (RegionData region in regions)
        {
            if (GroundEdit.Tiles(region) is not Piece piece) continue;
            int[] r = piece.Rect!;
            for (int y = r[1]; y < r[3]; y++)
                for (int x = r[0]; x < r[2]; x++)
                {
                    if (GroundEdit.At(piece, x, y) is not char c || c == ' ' || !piece.Legend!.TryGetValue(c.ToString(), out TileKind kind)) continue;
                    (byte cr, byte cg, byte cb) = Colour(region, kind.Material);
                    for (int py = 0; py < ppt; py++)
                        for (int px = 0; px < ppt; px++)
                        {
                            int o = (((y - y0) * ppt + py) * w + (x - x0) * ppt + px) * 4;
                            all[o] = cr; all[o + 1] = cg; all[o + 2] = cb;
                        }
                }
        }
        return all;
    }

    private static (byte, byte, byte) Colour(RegionData region, string material)
    {
        string value = region.Materials.TryGetValue(material, out string? v) ? v : "#808080";
        if (MapRender.Palette.TryGetValue(value, out string? hex)) value = hex;
        string s = value.TrimStart('#');
        if (s.Length != 6 || !int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out int rgb)) return (128, 128, 128);
        return ((byte)(rgb >> 16), (byte)(rgb >> 8 & 255), (byte)(rgb & 255));
    }
}
