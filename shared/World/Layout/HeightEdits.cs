using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rth.Shared.World.Data;

namespace Rth.Shared.World.Layout
{
    /// <summary>
    /// Heights sculpted by hand onto the generated land (docs/layout.md section 8), kept apart from
    /// the land regions so the blockout can still regenerate them: one file per generated region,
    /// world/layout/sculpt/&lt;id&gt;.json, holding how far each of its corners stands above (or
    /// below) what <see cref="LandHeights"/> gives, in the terrain's text rows (hundredths of a
    /// tile, from the north). The blockout adds them on top, so sculpted hills ride on the layout's
    /// own and survive changes to it. Written by the Editor's terrain editor (<c>WorldTerrain</c>).
    /// </summary>
    public sealed class HeightEdits
    {
        public sealed class SculptFile
        {
            public int[] Origin = { 0, 0 };
            public int[] Size = { 0, 0 };
            public List<string> Rows = new List<string>();
        }

        /// <summary>Each region's raise, by region id.</summary>
        public readonly SortedDictionary<string, Heightfield> Regions = new SortedDictionary<string, Heightfield>(StringComparer.Ordinal);

        public static string DirectoryIn(string root) => Path.Combine(root, "world", "layout", "sculpt");

        public static HeightEdits Load(string root)
        {
            var edits = new HeightEdits();
            string dir = DirectoryIn(root);
            if (!System.IO.Directory.Exists(dir)) return edits;
            foreach (string path in System.IO.Directory.GetFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                try
                {
                    SculptFile f = JsonConvert.DeserializeObject<SculptFile>(System.IO.File.ReadAllText(path)) ?? throw new FormatException("empty file");
                    edits.Regions[Path.GetFileNameWithoutExtension(path)] = TerrainText.ReadRows(f.Rows, f.Origin[0], f.Origin[1], f.Size[0], f.Size[1]);
                }
                catch (Exception e) when (e is JsonException || e is FormatException || e is IndexOutOfRangeException)
                {
                    throw new FormatException($"{Path.GetFileName(path)}: {e.Message}", e);
                }
            }
            return edits;
        }

        /// <summary>
        /// Writes a region's raise (or deletes its file when it is all 0). Returns true if the file changed.
        /// </summary>
        public static bool Save(string root, string id, Heightfield raise)
        {
            string path = Path.Combine(DirectoryIn(root), id + ".json");
            if (raise.Corners.All(h => TerrainText.Hundredths(h) == 0))
            {
                if (!System.IO.File.Exists(path)) return false;
                System.IO.File.Delete(path);
                return true;
            }
            System.IO.Directory.CreateDirectory(DirectoryIn(root));
            var lines = new List<string>
            {
                "{",
                $"  \"origin\": [{raise.OriginX}, {raise.OriginY}],",
                $"  \"size\": [{raise.Width}, {raise.Depth}],",
                "  \"rows\": [",
            };
            List<string> rows = TerrainText.WriteRows(raise);
            for (int i = 0; i < rows.Count; i++) lines.Add("    " + JsonConvert.ToString(rows[i]) + (i < rows.Count - 1 ? "," : ""));
            lines.Add("  ]");
            lines.Add("}");
            return RegionFiles.WriteIfChanged(path, string.Join("\n", lines) + "\n");
        }

        /// <summary>The raise at a corner (0 where nothing was sculpted).</summary>
        public float At(int x, int y)
        {
            foreach (Heightfield f in Regions.Values)
                if (x >= f.OriginX && y >= f.OriginY && x <= f.OriginX + f.Width && y <= f.OriginY + f.Depth)
                    return f.Corner(x, y);
            return 0f;
        }
    }
}
