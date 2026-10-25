using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Shared.World.Data;

namespace Rth.Shared.World.Ground
{
    /// <summary>
    /// A painted tiles piece (<see cref="Piece.Paint"/>) made ready for <see cref="GroundPainter"/>:
    /// the client and `tools/world` both build their canvas here, so they paint the same ground.
    /// </summary>
    public static class PaintedGround
    {
        /// <summary>
        /// The canvas for a tiles piece. <paramref name="colourOf"/> gives a material's colour (0..1
        /// RGB) by its name in the region's materials; the client reads it from its materials, the
        /// tools from the art palette.
        /// </summary>
        public static GroundCanvas Canvas(RegionData region, Piece piece, Func<string, float[]?> colourOf, Heightfield? field)
        {
            int[] rect = piece.Rect ?? new[] { region.OriginX, region.OriginY, region.OriginX + region.Width, region.OriginY + region.Height };
            int width = rect[2] - rect[0], height = rect[3] - rect[1];
            int margin = piece.Paint == true ? piece.Margin ?? 0 : 0;
            var keys = (piece.Legend ?? new SortedDictionary<string, TileKind>()).Keys.ToList();
            var looks = new GroundLook[keys.Count];
            var cliffs = new float[keys.Count * 3];
            for (int k = 0; k < keys.Count; k++)
            {
                TileKind kind = piece.Legend![keys[k]];
                float[] c = colourOf(kind.Material) ?? new[] { 0.4f, 0.55f, 0.3f };
                if (kind.Beyond == true) c = Haze(c);
                GroundLayer layer = kind.Water == true ? GroundLayer.Water : kind.IsOverlay ? GroundLayer.Over : GroundLayer.Under;
                looks[k] = GroundLook.Of(c[0], c[1], c[2], layer, layer == GroundLayer.Over ? 1 + k : 1);
                looks[k].Shelf = kind.Blocks == true && (kind.Height ?? 0f) > 0.3f;
                float[]? cliff = kind.Cliff != null ? colourOf(kind.Cliff) : null;
                cliffs[k * 3] = cliff?[0] ?? -1f;
                cliffs[k * 3 + 1] = cliff?[1] ?? -1f;
                cliffs[k * 3 + 2] = cliff?[2] ?? -1f;
            }
            // Overlays of one kind shape together; a different made surface meets it square (a bridge on a road).
            var index = new Dictionary<char, int>();
            for (int k = 0; k < keys.Count; k++) if (keys[k].Length > 0) index[keys[k][0]] = k;
            int[] grid = GroundPainter.Grid(piece.Rows ?? new List<string>(), piece.Surround, width, height, margin, ch => index.TryGetValue(ch, out int k) ? k : -1);
            return new GroundCanvas
            {
                X0 = rect[0], Y0 = rect[1], Width = width, Height = height, Margin = margin,
                Kinds = grid, Looks = looks, Field = field, CliffStep = region.Terrain?.CliffStep ?? 1f, CliffColours = cliffs,
            };
        }

        /// <summary>Land beyond the first build, as if seen across a great distance: paler, greyer, a little blue.</summary>
        public static float[] Haze(float[] c) => new[] { c[0] * 0.55f + 0.40f * 0.45f, c[1] * 0.55f + 0.44f * 0.45f, c[2] * 0.55f + 0.47f * 0.45f };

        /// <summary>A colour as "#rrggbb" in 0..1 RGB, or null.</summary>
        public static float[]? Hex(string? hex)
        {
            if (hex == null) return null;
            string h = hex.TrimStart('#');
            if (h.Length != 6 || !int.TryParse(h, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int rgb)) return null;
            return new[] { (rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f };
        }
    }
}
