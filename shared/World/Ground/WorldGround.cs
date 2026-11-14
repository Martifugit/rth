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

    /// <summary>What a tile of the world holds: a ground, and maybe stones lying over it, this thickly.</summary>
    public struct GroundTile
    {
        public string Ground;
        public string? Stone;
        public float Share;

        public GroundTile(string ground, string? stone = null, float share = 0f)
        {
            Ground = ground;
            Stone = stone;
            Share = share;
        }
    }

    /// <summary>
    /// A square of the world's ground as a Unity terrain holds it (docs/world-guide.md section 4):
    /// <see cref="Size"/> tiles a side on a fixed grid, a height on every tile corner and, for every
    /// tile, a weight per layer (grounds and stones, named as in the <see cref="GroundTable"/>).
    /// A tile is the ground layer that weighs most there, so a soft brush still paints crisp tiles;
    /// its stones are the heaviest stone layer, the weight being how thickly they lie.
    /// </summary>
    public sealed class GroundChunk
    {
        public const int Size = 128;

        public readonly int I, J;
        public int X0 => I * Size;
        public int Y0 => J * Size;

        /// <summary>Corner heights in tiles, [y, x] from the chunk's south-west corner, (Size + 1) a side.</summary>
        public readonly float[,] Heights = new float[Size + 1, Size + 1];
        /// <summary>Tiles that have ground ([y, x]); the rest are holes (the sea, unbuilt land).</summary>
        public readonly bool[,] Solid = new bool[Size, Size];
        /// <summary>The layers, by name: grounds' ids, and stones' as <see cref="GroundTable.StonePrefix"/> + id.</summary>
        public readonly List<string> Layers = new List<string>();
        /// <summary>Per tile and layer ([y, x, layer], layers as in <see cref="Layers"/>), 0..1.</summary>
        public float[,,] Weights = new float[Size, Size, 0];

        public GroundChunk(int i, int j)
        {
            I = i;
            J = j;
        }

        public static int IndexOf(int tile) => (int)Math.Floor(tile / (double)Size);

        public int Layer(string name)
        {
            int i = Layers.IndexOf(name);
            if (i >= 0) return i;
            Layers.Add(name);
            var grown = new float[Size, Size, Layers.Count];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    for (int l = 0; l < Layers.Count - 1; l++)
                        grown[y, x, l] = Weights[y, x, l];
            Weights = grown;
            return Layers.Count - 1;
        }

        /// <summary>Sets a tile (absolute) to one ground and its stones.</summary>
        public void Set(int x, int y, GroundTile tile)
        {
            int lx = x - X0, ly = y - Y0;
            Solid[ly, lx] = true;
            int g = Layer(tile.Ground);
            int s = tile.Stone != null ? Layer(GroundTable.StonePrefix + tile.Stone) : -1;
            for (int l = 0; l < Layers.Count; l++) Weights[ly, lx, l] = 0f;
            float share = s >= 0 ? Math.Max(0f, Math.Min(1f, tile.Share)) : 0f;
            Weights[ly, lx, g] = 1f - share;
            if (s >= 0) Weights[ly, lx, s] = share;
        }

        /// <summary>What a tile (absolute) holds, or null for a hole.</summary>
        public GroundTile? At(int x, int y)
        {
            int lx = x - X0, ly = y - Y0;
            if (lx < 0 || ly < 0 || lx >= Size || ly >= Size || !Solid[ly, lx]) return null;
            int ground = -1, stone = -1;
            for (int l = 0; l < Layers.Count; l++)
            {
                bool isStone = Layers[l].StartsWith(GroundTable.StonePrefix, StringComparison.Ordinal);
                float w = Weights[ly, lx, l];
                if (isStone) { if (stone < 0 || w > Weights[ly, lx, stone]) stone = l; }
                else if (ground < 0 || w > Weights[ly, lx, ground]) ground = l;
            }
            if (ground < 0) return null;
            float share = stone >= 0 ? (float)Math.Round(Weights[ly, lx, stone], 2) : 0f;
            return share >= 0.01f
                ? new GroundTile(Layers[ground], Layers[stone].Substring(GroundTable.StonePrefix.Length), share)
                : new GroundTile(Layers[ground]);
        }

        public float Corner(int x, int y) => Heights[y - Y0, x - X0];
        public void SetCorner(int x, int y, float h) => Heights[y - Y0, x - X0] = h;
    }

    /// <summary>
    /// The world's ground between the Unity terrains, which are its truth, and the region data the
    /// compiler and the game's ground drawing read (docs/world-guide.md section 4): the chunks made
    /// once from the regions, and each region's tiles, stones, ring and heights written from them.
    /// </summary>
    public static class WorldGround
    {
        /// <summary>The world's ground in chunks, from the regions' tiles and heights (where the terrains first come from).</summary>
        public static Dictionary<(int i, int j), GroundChunk> ChunksFromRegions(IEnumerable<RegionData> regions, GroundTable table) =>
            ChunksFromRegions(regions, table, out _);

        /// <summary>
        /// As above; <paramref name="disagreements"/> are the corners on region borders where two
        /// regions held different heights (a terrain holds one: the first region's, in order).
        /// </summary>
        public static Dictionary<(int i, int j), GroundChunk> ChunksFromRegions(IEnumerable<RegionData> regions, GroundTable table,
            out HashSet<(int x, int y)> disagreements)
        {
            var chunks = new Dictionary<(int, int), GroundChunk>();
            List<RegionData> grounds = regions.Where(r => GroundEdit.Tiles(r) != null).ToList();
            foreach (RegionData region in grounds)
            {
                Piece piece = GroundEdit.Tiles(region)!;
                int[] r = piece.Rect!;
                for (int y = r[1]; y < r[3]; y++)
                    for (int x = r[0]; x < r[2]; x++)
                    {
                        if (!(GroundEdit.At(piece, x, y) is char c) || c == ' ' || !piece.Legend!.TryGetValue(c.ToString(), out TileKind kind)) continue;
                        string ground = table.GroundOf(region, kind) ?? throw new InvalidOperationException($"{region.Id}: ({x}, {y}) is a ground the table doesn't have.");
                        StoneKind? stones = GroundEdit.StoneAt(piece, x, y);
                        string? stone = stones != null ? table.StoneOf(region, stones.Material) : null;
                        var key = (GroundChunk.IndexOf(x), GroundChunk.IndexOf(y));
                        if (!chunks.TryGetValue(key, out GroundChunk chunk)) chunks[key] = chunk = new GroundChunk(key.Item1, key.Item2);
                        chunk.Set(x, y, new GroundTile(ground, stone, stone != null ? stones!.Share : 0f));
                    }
            }

            // Corners, once every chunk exists: in each chunk that holds them (a corner on a chunk's edge is in two or four).
            var heightOf = new Dictionary<(int, int), float>();
            disagreements = new HashSet<(int, int)>();
            foreach (RegionData region in grounds)
            {
                Heightfield heights = TerrainText.ReadOrFlat(region);
                int[] r = GroundEdit.Tiles(region)!.Rect!;
                for (int y = r[1]; y <= r[3]; y++)
                    for (int x = r[0]; x <= r[2]; x++)
                    {
                        float h = heights.Corner(x, y);
                        if (heightOf.TryGetValue((x, y), out float had))
                        {
                            if (Math.Abs(had - h) > 1e-4f) disagreements.Add((x, y));
                            continue;
                        }
                        heightOf[(x, y)] = h;
                        foreach ((int i, int j) in ChunksOfCorner(x, y))
                            if (chunks.TryGetValue((i, j), out GroundChunk ch)) ch.SetCorner(x, y, h);
                    }
            }
            return chunks;
        }
        /// <summary>The chunks a corner is in: one, or two or four on chunks' edges.</summary>
        public static IEnumerable<(int i, int j)> ChunksOfCorner(int x, int y)
        {
            int i = GroundChunk.IndexOf(x), j = GroundChunk.IndexOf(y);
            bool onX = x == i * GroundChunk.Size, onY = y == j * GroundChunk.Size;
            yield return (i, j);
            if (onX) yield return (i - 1, j);
            if (onY) yield return (i, j - 1);
            if (onX && onY) yield return (i - 1, j - 1);
        }

        /// <summary>A world read from chunks: each tile's ground and each corner's height.</summary>
        public sealed class ChunkWorld
        {
            private readonly IReadOnlyDictionary<(int i, int j), GroundChunk> _chunks;

            public ChunkWorld(IReadOnlyDictionary<(int i, int j), GroundChunk> chunks) => _chunks = chunks;

            public GroundTile? Tile(int x, int y) =>
                _chunks.TryGetValue((GroundChunk.IndexOf(x), GroundChunk.IndexOf(y)), out GroundChunk c) ? c.At(x, y) : null;

            public float? Corner(int x, int y)
            {
                foreach ((int i, int j) in ChunksOfCorner(x, y))
                    if (_chunks.TryGetValue((i, j), out GroundChunk c)) return c.Corner(x, y);
                return null;
            }
        }

        /// <summary>
        /// Writes a region's ground from the world: its tiles piece's rows and legend, its stones,
        /// its ring of the neighbours' tiles and its terrain. Characters the region already writes a
        /// kind as are kept, so files only change where the ground did. True if anything changed.
        /// </summary>
        public static bool Apply(RegionData region, GroundTable table, Func<int, int, GroundTile?> tile, Func<int, int, float?> corner)
        {
            if (!(GroundEdit.Tiles(region) is Piece piece)) return false;
            string before = RegionFiles.Format(region);
            int[] r = piece.Rect!;

            var kinds = new Dictionary<string, TileKind>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, TileKind> g in table.Grounds) kinds[g.Key] = g.Value;
            IReadOnlyDictionary<string, string> MaterialsOf(TileKind k)
            {
                var m = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string? name in new[] { k.Material, k.Cliff })
                    if (name != null && table.Materials.TryGetValue(name, out string c)) m[name] = c;
                return m;
            }

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
            TileKind KindFor(string id) => regionKind.TryGetValue(id, out TileKind k) ? k : kinds[id];
            char CharOf(string id, char? was)
            {
                if (was is char w && groundOfChar.TryGetValue(w, out string had) && had == id) return w;
                TileKind kind = KindFor(id);
                char c = GroundEdit.CharFor(region, piece, kind, regionKind.ContainsKey(id) ? new Dictionary<string, string>() : MaterialsOf(kind));
                groundOfChar[c] = id;
                if (!regionKind.ContainsKey(id)) regionKind[id] = piece.Legend[c.ToString()];
                return c;
            }

            for (int y = r[1]; y < r[3]; y++)
                for (int x = r[0]; x < r[2]; x++)
                {
                    GroundTile? t = tile(x, y);
                    if (t == null || !kinds.ContainsKey(t.Value.Ground))
                    {
                        GroundEdit.Set(piece, x, y, ' ');
                        GroundEdit.SetStone(piece, x, y, ' ');
                        continue;
                    }
                    GroundEdit.Set(piece, x, y, CharOf(t.Value.Ground, GroundEdit.At(piece, x, y)));
                    if (t.Value.Stone is string stone && table.Stones.TryGetValue(stone, out string material) && t.Value.Share >= 0.01f)
                    {
                        float share = (float)Math.Round(t.Value.Share, 2);
                        StoneKind? had = GroundEdit.StoneAt(piece, x, y);
                        if (had != null && table.StoneOf(region, had.Material) == stone && Math.Abs(had.Share - share) < 0.005f) continue;
                        GroundEdit.SetStone(piece, x, y, GroundEdit.StoneCharFor(piece, StoneMaterial(region, table, stone, material), share));
                    }
                    else GroundEdit.SetStone(piece, x, y, ' ');
                }

            // The ring of the neighbours' tiles a painted region keeps (a region without one stays without).
            if (piece.Paint == true)
            {
                GroundEdit.RefreshSurround(region, (x, y) =>
                {
                    GroundTile? t = tile(x, y);
                    if (t == null || !kinds.ContainsKey(t.Value.Ground)) return null;
                    TileKind kind = KindFor(t.Value.Ground);
                    return (kind, regionKind.ContainsKey(t.Value.Ground) ? new Dictionary<string, string>() : MaterialsOf(kind), ' ');
                });
            }
            GroundEdit.Prune(piece);
            GroundEdit.PruneStones(piece);

            // Heights: every corner of the region the world has; written only when one changed (so
            // the rows keep their layout), and flat ground stays without a terrain.
            Heightfield field = TerrainText.ReadOrFlat(region);
            bool moved = false, any = region.Terrain != null;
            for (int y = region.OriginY; y <= region.OriginY + region.Height; y++)
                for (int x = region.OriginX; x <= region.OriginX + region.Width; x++)
                {
                    if (!(corner(x, y) is float h)) continue;
                    float rounded = TerrainText.Hundredths(TerrainText.Clamp(h)) * TerrainText.Step;
                    if (TerrainText.Hundredths(field.Corner(x, y)) == TerrainText.Hundredths(rounded)) continue;
                    field.SetCorner(x, y, rounded);
                    moved = true;
                    if (rounded != 0f) any = true;
                }
            if (moved && any) region.Terrain = TerrainText.Write(field, region.Terrain?.CliffStep ?? 1f);
            return RegionFiles.Format(region) != before;
        }

        /// <summary>
        /// The region's name for a stones material: one its stones of that colour already use, else
        /// the table's (or another if the region gives that name another colour).
        /// </summary>
        private static string StoneMaterial(RegionData region, GroundTable table, string stone, string material)
        {
            foreach (StoneKind k in GroundEdit.Tiles(region)?.StoneLegend?.Values ?? Enumerable.Empty<StoneKind>())
                if (table.StoneOf(region, k.Material) == stone) return k.Material;
            string colour = table.Materials.TryGetValue(material, out string c) ? c : "";
            string name = material;
            for (int n = 2; region.Materials.TryGetValue(name, out string have) && have != colour; n++) name = material + n;
            region.Materials[name] = colour;
            return name;
        }    }
}
