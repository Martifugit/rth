using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rth.Shared.World.Data;

namespace Rth.Shared.World.Ground
{
    /// <summary>
    /// The world's kinds of ground (world/grounds.json), one per Unity terrain layer
    /// (docs/world-guide.md section 4): what each draws as and does (a <see cref="TileKind"/>),
    /// the materials' colours, and the kinds of stones that lie over the ground.
    /// </summary>
    public sealed class GroundTable
    {
        /// <summary>Material name to colour (a palette name or #hex), shared by every region.</summary>
        public SortedDictionary<string, string> Materials = new SortedDictionary<string, string>(StringComparer.Ordinal);
        /// <summary>Ground id (the terrain layer's name) to what it is.</summary>
        public SortedDictionary<string, TileKind> Grounds = new SortedDictionary<string, TileKind>(StringComparer.Ordinal);
        /// <summary>Stone id (the terrain layer's name, after <see cref="StonePrefix"/>) to the material they are drawn in.</summary>
        public SortedDictionary<string, string> Stones = new SortedDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>A terrain layer holding stones rather than ground is named this, then the stone's id.</summary>
        public const string StonePrefix = "stones-";

        public static string PathIn(string repoRoot) => System.IO.Path.Combine(repoRoot, "world", "grounds.json");

        public static GroundTable Load(string path)
        {
            GroundTable? table = JsonConvert.DeserializeObject<GroundTable>(File.ReadAllText(path));
            return table ?? throw new FormatException(path + " is empty.");
        }

        public string Format() => JsonConvert.SerializeObject(this, new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver
            {
                NamingStrategy = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy { ProcessDictionaryKeys = false },
            },
        }).Replace("\r\n", "\n") + "\n";

        public void Save(string path) => RegionFiles.WriteIfChanged(path, Format());

        /// <summary>The id of the ground that draws and behaves as <paramref name="kind"/> does in <paramref name="region"/>, or null.</summary>
        public string? GroundOf(RegionData region, TileKind kind)
        {
            foreach (KeyValuePair<string, TileKind> g in Grounds)
                if (SameAs(g.Value, region, kind)) return g.Key;
            return null;
        }

        /// <summary>The id of the stones drawn in the colour <paramref name="material"/> has in <paramref name="region"/>, or null.</summary>
        public string? StoneOf(RegionData region, string material)
        {
            string colour = ColourIn(region, material);
            foreach (KeyValuePair<string, string> s in Stones)
                if (Materials.TryGetValue(s.Value, out string c) && c == colour) return s.Key;
            return null;
        }

        private bool SameAs(TileKind ground, RegionData region, TileKind kind)
        {
            TileKind a = GroundEdit.Copy(ground), b = GroundEdit.Copy(kind);
            // Materials match by colour (a region may call a colour by another name); then the rest must agree.
            if (!Materials.TryGetValue(a.Material, out string ca) || ca != ColourIn(region, b.Material)) return false;
            if ((a.Cliff == null) != (b.Cliff == null)) return false;
            if (a.Cliff != null && (!Materials.TryGetValue(a.Cliff, out string cc) || cc != ColourIn(region, b.Cliff!))) return false;
            b.Material = a.Material;
            b.Cliff = a.Cliff;
            return GroundEdit.Same(a, b);
        }

        private static string ColourIn(RegionData region, string material) =>
            region.Materials.TryGetValue(material, out string c) ? c : "?" + material;

        /// <summary>
        /// Gathers every kind of ground and stones the regions use into a table: a ground per kind
        /// that draws differently, named after its material ("LandHeath" is "heath"; another kind of
        /// the same material gets a number).
        /// </summary>
        public static GroundTable FromRegions(IEnumerable<RegionData> regions)
        {
            var table = new GroundTable();
            foreach (RegionData region in regions)
            {
                if (!(GroundEdit.Tiles(region) is Piece piece)) continue;
                foreach (TileKind kind in piece.Legend!.Values)
                {
                    if (table.GroundOf(region, kind) != null) continue;
                    TileKind ground = GroundEdit.Copy(kind);
                    ground.Material = table.MaterialFrom(region, kind.Material);
                    if (kind.Cliff != null) ground.Cliff = table.MaterialFrom(region, kind.Cliff);
                    table.Grounds[table.FreeId(table.Grounds.Keys, Readable(ground.Material))] = ground;
                }
                foreach (StoneKind stone in piece.StoneLegend?.Values ?? Enumerable.Empty<StoneKind>())
                {
                    if (table.StoneOf(region, stone.Material) != null) continue;
                    string material = table.MaterialFrom(region, stone.Material);
                    string name = Readable(material);
                    if (name.StartsWith("stones-", StringComparison.Ordinal) && name.Length > 7) name = name.Substring(7);
                    table.Stones[table.FreeId(table.Stones.Keys, name)] = material;
                }
            }
            return table;
        }

        /// <summary>The table's name for a region's material, added (under a new name if the table gives that name another colour).</summary>
        private string MaterialFrom(RegionData region, string material)
        {
            string colour = ColourIn(region, material);
            string name = material;
            for (int n = 2; Materials.TryGetValue(name, out string have) && have != colour; n++) name = material + n;
            Materials[name] = colour;
            return name;
        }

        private string FreeId(IEnumerable<string> taken, string id)
        {
            var used = new HashSet<string>(taken, StringComparer.Ordinal);
            string candidate = id;
            for (int n = 2; used.Contains(candidate) || used.Contains(StonePrefix + candidate); n++) candidate = id + "-" + n;
            return candidate;
        }

        /// <summary>"LandDirtroad" reads as "dirtroad".</summary>
        private static string Readable(string material)
        {
            string s = material.StartsWith("Land", StringComparison.Ordinal) && material.Length > 4 ? material.Substring(4) : material;
            var b = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsUpper(c) && i > 0 && !char.IsUpper(s[i - 1])) b.Append('-');
                b.Append(char.ToLowerInvariant(c));
            }
            return b.ToString();
        }
    }

    /// <summary>
    /// A region's ground between its Unity terrain, which is the truth, and its data
    /// (docs/world-guide.md section 4). The server walks tiles, so on saving each tile becomes the
    /// ground painted heaviest on it and each tile corner the terrain's height there: the region's
    /// `tiles` piece (rows and legend) and `terrain`, which the compiler reads for what blocks
    /// (water, rock, ground too steep to walk). Nothing about how the ground looks is kept there.
    /// </summary>
    public static class WorldGround
    {
        /// <summary>The ground id of a tile in the region's data, or null where it has none.</summary>
        public static string? GroundAt(RegionData region, Piece piece, GroundTable table, int x, int y) =>
            GroundEdit.At(piece, x, y) is char c && c != ' ' && piece.Legend!.TryGetValue(c.ToString(), out TileKind kind) ? table.GroundOf(region, kind) : null;

        /// <summary>
        /// Writes a region's ground: each tile of its tiles piece from <paramref name="ground"/> (a
        /// ground id, or null for none), each corner's height from <paramref name="corner"/>. The
        /// characters it already writes a ground as are kept, so files only change where the ground
        /// did. True if anything changed.
        /// </summary>
        public static bool Apply(RegionData region, GroundTable table, Func<int, int, string?> ground, Func<int, int, float?> corner)
        {
            if (!(GroundEdit.Tiles(region) is Piece piece)) return false;
            string before = RegionFiles.Format(region);
            int[] r = piece.Rect!;

            // What each of the region's characters is, and the region's own way of writing each ground
            // (its material names), so a ground it already has is written as it was.
            var groundOfChar = new Dictionary<char, string>();
            var regionKind = new Dictionary<string, TileKind>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, TileKind> e in piece.Legend!)
                if (e.Key.Length == 1 && table.GroundOf(region, e.Value) is string id)
                {
                    groundOfChar[e.Key[0]] = id;
                    if (!regionKind.ContainsKey(id)) regionKind[id] = e.Value;
                }
            char CharOf(string id, char? was)
            {
                if (was is char w && groundOfChar.TryGetValue(w, out string had) && had == id) return w;
                TileKind kind = regionKind.TryGetValue(id, out TileKind k) ? k : table.Grounds[id];
                var materials = new Dictionary<string, string>(StringComparer.Ordinal);
                if (!regionKind.ContainsKey(id))
                    foreach (string? name in new[] { kind.Material, kind.Cliff })
                        if (name != null && table.Materials.TryGetValue(name, out string colour)) materials[name] = colour;
                char c = GroundEdit.CharFor(region, piece, kind, materials);
                groundOfChar[c] = id;
                if (!regionKind.ContainsKey(id)) regionKind[id] = piece.Legend[c.ToString()];
                return c;
            }

            for (int y = r[1]; y < r[3]; y++)
                for (int x = r[0]; x < r[2]; x++)
                {
                    string? g = ground(x, y);
                    GroundEdit.Set(piece, x, y, g != null && table.Grounds.ContainsKey(g) ? CharOf(g, GroundEdit.At(piece, x, y)) : ' ');
                }
            // Only what the server needs: how the ground looks is the terrain's.
            piece.Paint = null;
            piece.Margin = null;
            piece.Surround = null;
            piece.Stones = null;
            piece.StoneLegend = null;
            GroundEdit.Prune(piece);

            // Heights on every corner; written only when one changed (so the rows keep their layout).
            Heightfield field = TerrainText.ReadOrFlat(region);
            bool moved = false, any = region.Terrain != null;
            for (int y = region.OriginY; y <= region.OriginY + region.Height; y++)
                for (int x = region.OriginX; x <= region.OriginX + region.Width; x++)
                {
                    if (!(corner(x, y) is float h)) continue;
                    int hundredths = TerrainText.Hundredths(TerrainText.Clamp(h));
                    if (TerrainText.Hundredths(field.Corner(x, y)) == hundredths) continue;
                    field.SetCorner(x, y, hundredths * TerrainText.Step);
                    moved = true;
                    if (hundredths != 0) any = true;
                }
            if (moved && any) region.Terrain = TerrainText.Write(field, region.Terrain?.CliffStep ?? RegionTerrain.DefaultCliffStep);

            return RegionFiles.Format(region) != before;
        }
    }
}