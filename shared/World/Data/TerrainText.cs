using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// Reads and writes a region's <see cref="RegionTerrain"/>: the corner heights of a
    /// <see cref="Heightfield"/> as rows of text, from the north, in hundredths of a tile, with
    /// runs of one height written <c>h*n</c> so flat ground stays short and diffs stay readable.
    /// </summary>
    public static class TerrainText
    {
        /// <summary>Heights are kept to this step (a hundredth of a tile).</summary>
        public const float Step = 0.01f;

        /// <summary>The region's heights, or null for flat ground. Throws <see cref="FormatException"/> on a bad terrain.</summary>
        public static Heightfield? Read(RegionData region)
        {
            if (region.Terrain == null) return null;
            int w = region.Width, h = region.Height;
            if (w <= 0 || h <= 0) throw new FormatException("terrain needs a region with a size.");
            var corners = new float[(w + 1) * (h + 1)];
            List<string> rows = region.Terrain.Rows ?? new List<string>();
            if (rows.Count != h + 1) throw new FormatException($"terrain has {rows.Count} rows; a region {h} tiles tall needs {h + 1} (one per row of corners).");
            for (int r = 0; r < rows.Count; r++)
            {
                int j = h - r; // rows run from the north
                int i = 0;
                foreach (string token in rows[r].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int star = token.IndexOf('*');
                    string value = star < 0 ? token : token.Substring(0, star);
                    int count = 1;
                    if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int hundredths) ||
                        star >= 0 && (!int.TryParse(token.Substring(star + 1), NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 1))
                        throw new FormatException($"terrain row {r} from the north: '{token}' is not a height in hundredths of a tile (or h*n).");
                    for (int k = 0; k < count; k++, i++)
                        if (i <= w) corners[j * (w + 1) + i] = hundredths * Step;
                }
                if (i != w + 1) throw new FormatException($"terrain row {r} from the north has {i} corners; a region {w} tiles wide needs {w + 1}.");
            }
            return new Heightfield(region.OriginX, region.OriginY, w, h, corners);
        }

        /// <summary>The heights the region stands on: its terrain, or flat ground at 0.</summary>
        public static Heightfield ReadOrFlat(RegionData region) =>
            Read(region) ?? new Heightfield(region.OriginX, region.OriginY, Math.Max(1, region.Width), Math.Max(1, region.Height));

        /// <summary>A terrain section for a heightfield covering the whole region.</summary>
        public static RegionTerrain Write(Heightfield field, float cliffStep = 1f)
        {
            var terrain = new RegionTerrain { CliffStep = cliffStep };
            for (int j = field.Depth; j >= 0; j--)
            {
                var row = new StringBuilder();
                int run = 0, last = 0;
                void Flush()
                {
                    if (run == 0) return;
                    if (row.Length > 0) row.Append(' ');
                    row.Append(last.ToString(CultureInfo.InvariantCulture));
                    if (run > 1) row.Append('*').Append(run.ToString(CultureInfo.InvariantCulture));
                }
                for (int i = 0; i <= field.Width; i++)
                {
                    int v = Hundredths(field.Corner(field.OriginX + i, field.OriginY + j));
                    if (run > 0 && v == last)
                    {
                        run++;
                        continue;
                    }
                    Flush();
                    last = v;
                    run = 1;
                }
                Flush();
                terrain.Rows.Add(row.ToString());
            }
            return terrain;
        }

        public static int Hundredths(float height) => (int)Math.Round(height / Step, MidpointRounding.AwayFromZero);
    }
}
