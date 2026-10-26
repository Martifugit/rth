using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Shared.World.Data;
using Rth.Shared.World.Layout;

namespace Rth.Shared.World.Ground
{
    /// <summary>
    /// Changing a region's ground tile by tile, as the Editor's ground brush does
    /// (docs/world-guide.md section 4): the grounds there are to paint with, finding or adding the
    /// legend character a kind is written as, setting tiles, and refreshing the ring of the
    /// neighbours' tiles a painted region keeps (<see cref="Piece.Surround"/>) so its colours blend
    /// across the border with what the neighbours really hold.
    /// </summary>
    public static class GroundEdit
    {
        /// <summary>A ground to paint with: the tile kind it is written as, and its materials' colours.</summary>
        public sealed class Brush
        {
            public string Id = "";
            public string Name = "";
            /// <summary>"Ground", "Surfaces" (roads, paving, fields), "Water" or "Blocked", for the brush's list.</summary>
            public string Group = "";
            public TileKind Kind = new TileKind();
            public Dictionary<string, string> Materials = new Dictionary<string, string>(StringComparer.Ordinal);
            /// <summary>The character it is written as where a region doesn't use that one already (the layout's letter).</summary>
            public char? Letter;
            /// <summary>The colour the brush's swatch shows (a palette name or #hex).</summary>
            public string Colour => Materials.TryGetValue(Kind.Material, out string c) ? c : "";
        }

        /// <summary>Characters a new kind may be written as, in the order they are taken.</summary>
        private const string Spare = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#$%&*+-=?@^~<>/|:;.,_()[]{}'`";

        /// <summary>Every ground of the layout (continent.json), as the blockout draws it.</summary>
        public static List<Brush> Brushes(ContinentData data)
        {
            var brushes = new List<Brush>();
            foreach (KeyValuePair<string, GroundDef> g in data.Grounds)
            {
                (TileKind kind, Dictionary<string, string> materials) = Blockout.KindOf(data, g.Key);
                brushes.Add(new Brush
                {
                    Id = g.Key, Name = g.Value.Name.Length > 0 ? g.Value.Name : g.Key, Group = GroupOf(kind),
                    Kind = kind, Materials = materials, Letter = g.Value.Letter.Length == 1 ? g.Value.Letter[0] : (char?)null,
                });
            }
            return brushes;
        }

        /// <summary>A brush for a kind found in a region's legend (ground the layout doesn't have).</summary>
        public static Brush BrushOf(RegionData region, TileKind kind)
        {
            var b = new Brush { Id = kind.Material, Name = Readable(kind.Material), Group = GroupOf(kind), Kind = Copy(kind) };
            foreach (string? m in new[] { kind.Material, kind.Cliff })
                if (m != null && region.Materials.TryGetValue(m, out string c)) b.Materials[m] = c;
            return b;
        }

        public static string GroupOf(TileKind kind) =>
            kind.Water == true ? "Water" : kind.IsOverlay ? "Surfaces" : kind.Blocks == true ? "Blocked" : "Ground";

        /// <summary>"LandDirtroad" reads as "Dirtroad".</summary>
        private static string Readable(string material) =>
            material.StartsWith("Land", StringComparison.Ordinal) && material.Length > 4 ? material.Substring(4) : material;

        /// <summary>Whether two kinds draw and behave the same.</summary>
        public static bool Same(TileKind a, TileKind b) =>
            a.Material == b.Material && (a.Blocks ?? false) == (b.Blocks ?? false) && (a.Water ?? false) == (b.Water ?? false) &&
            Math.Abs((a.Height ?? 0f) - (b.Height ?? 0f)) < 1e-4f && a.Cliff == b.Cliff &&
            LayerOf(a) == LayerOf(b) && (a.Beyond ?? false) == (b.Beyond ?? false);

        /// <summary>A kind's layer as it paints: "over", "soft" or null (natural ground).</summary>
        private static string? LayerOf(TileKind k) => k.IsOverlay || k.IsSoft ? k.Layer : null;

        public static TileKind Copy(TileKind k) => new TileKind
        {
            Material = k.Material, Blocks = k.Blocks, Water = k.Water, Height = k.Height, Cliff = k.Cliff, Layer = k.Layer, Beyond = k.Beyond,
        };

        /// <summary>The region's ground: its first tiles piece, or null.</summary>
        public static Piece? Tiles(RegionData region) => region.Pieces.FirstOrDefault(p => p.Kind == PieceKind.Tiles && p.Rows != null && p.Legend != null);

        /// <summary>
        /// The legend character the piece writes <paramref name="kind"/> as, added (with its materials
        /// in the region's) if it has none. A material whose name the region already gives another
        /// colour is added under a new name, so neither changes colour.
        /// </summary>
        public static char CharFor(RegionData region, Piece piece, TileKind kind, IReadOnlyDictionary<string, string> materials, char? prefer = null)
        {
            TileKind wanted = Copy(kind);
            wanted.Material = MaterialFor(region, kind.Material, materials);
            if (kind.Cliff != null) wanted.Cliff = MaterialFor(region, kind.Cliff, materials);
            foreach (KeyValuePair<string, TileKind> e in piece.Legend!)
                if (e.Key.Length == 1 && Same(e.Value, wanted)) return e.Key[0];
            char c = prefer is char p && p != ' ' && !piece.Legend.ContainsKey(p.ToString()) ? p : Spare.FirstOrDefault(s => !piece.Legend.ContainsKey(s.ToString()));
            if (c == default) throw new InvalidOperationException($"{region.Id}: its ground has no characters left for another kind ({piece.Legend.Count} in use).");
            piece.Legend[c.ToString()] = wanted;
            return c;
        }

        private static string MaterialFor(RegionData region, string name, IReadOnlyDictionary<string, string> materials)
        {
            if (!materials.TryGetValue(name, out string colour)) return name; // the region's own, or drawn magenta (Sync World warns)
            string candidate = name;
            for (int n = 2; region.Materials.TryGetValue(candidate, out string have) && have != colour; n++) candidate = name + n;
            region.Materials[candidate] = colour;
            return candidate;
        }

        /// <summary>The character at an absolute tile of the piece's rectangle, or null outside it.</summary>
        public static char? At(Piece piece, int x, int y)
        {
            int[] r = piece.Rect!;
            if (x < r[0] || y < r[1] || x >= r[2] || y >= r[3]) return null;
            string row = piece.Rows![r[3] - 1 - y];
            int i = x - r[0];
            return i < row.Length ? row[i] : (char?)null;
        }

        /// <summary>Sets an absolute tile of the piece's rectangle; false outside it or when it already is <paramref name="c"/>.</summary>
        public static bool Set(Piece piece, int x, int y, char c)
        {
            int[] r = piece.Rect!;
            if (x < r[0] || y < r[1] || x >= r[2] || y >= r[3]) return false;
            int ri = r[3] - 1 - y, i = x - r[0];
            string row = piece.Rows![ri].PadRight(r[2] - r[0]);
            if (row[i] == c) return false;
            piece.Rows[ri] = row.Substring(0, i) + c + row.Substring(i + 1);
            return true;
        }

        /// <summary>
        /// What the world holds at a tile, from a set of regions: the kind there, its materials'
        /// colours and the character that region writes it as; null where none of them has ground.
        /// </summary>
        public sealed class WorldTiles
        {
            private readonly List<(RegionData region, Piece piece)> _grounds = new List<(RegionData, Piece)>();

            public WorldTiles(IEnumerable<RegionData> regions)
            {
                foreach (RegionData r in regions)
                    if (!r.Instanced && Tiles(r) is Piece p) _grounds.Add((r, p));
            }

            public (TileKind kind, IReadOnlyDictionary<string, string> materials, char letter)? At(int x, int y)
            {
                foreach ((RegionData region, Piece piece) in _grounds)
                {
                    if (!(GroundEdit.At(piece, x, y) is char c) || !piece.Legend!.TryGetValue(c.ToString(), out TileKind kind)) continue;
                    var materials = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (string? m in new[] { kind.Material, kind.Cliff })
                        if (m != null && region.Materials.TryGetValue(m, out string colour)) materials[m] = colour;
                    return (kind, materials, c);
                }
                return null;
            }
        }

        /// <summary>
        /// Rewrites the region's ring of neighbours' tiles from what the world holds there (tiles no
        /// region has keep what they had: the sea, unbuilt land), then drops legend entries nothing
        /// uses. True if anything changed.
        /// </summary>
        public static bool RefreshSurround(RegionData region, Func<int, int, (TileKind kind, IReadOnlyDictionary<string, string> materials, char letter)?> world)
        {
            if (!(Tiles(region) is Piece piece) || piece.Margin is not int m || m <= 0 || piece.Surround == null) return false;
            int[] rect = piece.Rect!;
            int x0 = rect[0], y0 = rect[1], x1 = rect[2], y1 = rect[3];
            var surround = new List<string>(piece.Surround.Count);
            int line = 0;
            for (int y = y1 + m - 1; y >= y0 - m; y--, line++)
            {
                bool full = y >= y1 || y < y0;
                string old = line < piece.Surround.Count ? piece.Surround[line] : "";
                var row = new System.Text.StringBuilder(old.Length);
                int i = 0;
                for (int x = x0 - m; x < x1 + m; x++)
                {
                    if (!full && x >= x0 && x < x1) continue;
                    char was = i < old.Length ? old[i] : ' ';
                    var there = world(x, y);
                    row.Append(there.HasValue ? CharFor(region, piece, there.Value.kind, there.Value.materials, there.Value.letter) : was);
                    i++;
                }
                surround.Add(row.ToString());
            }
            bool changed = !surround.SequenceEqual(piece.Surround);
            piece.Surround = surround;
            return Prune(piece) | changed;
        }

        /// <summary>
        /// Gives a painted piece without a ring of the neighbours' tiles an empty one
        /// <paramref name="margin"/> tiles wide, for <see cref="RefreshSurround"/> to fill. True if it added one.
        /// </summary>
        public static bool EnsureRing(Piece piece, int margin = 2)
        {
            if (piece.Paint != true || (piece.Margin ?? 0) > 0 && piece.Surround != null) return false;
            int[] rect = piece.Rect!;
            int w = rect[2] - rect[0], h = rect[3] - rect[1];
            piece.Margin = margin;
            piece.Surround = new List<string>();
            for (int i = 0; i < h + 2 * margin; i++)
                piece.Surround.Add(new string(' ', i < margin || i >= h + margin ? w + 2 * margin : 2 * margin));
            return true;
        }

        /// <summary>Drops legend entries neither the rows nor the ring use. True if any went.</summary>
        public static bool Prune(Piece piece)
        {
            var used = new HashSet<char>();
            foreach (string row in piece.Rows!) used.UnionWith(row);
            if (piece.Surround != null) foreach (string row in piece.Surround) used.UnionWith(row);
            var unused = piece.Legend!.Keys.Where(k => k.Length != 1 || !used.Contains(k[0])).ToList();
            foreach (string k in unused) piece.Legend.Remove(k);
            return unused.Count > 0;
        }

        /// <summary>
        /// Refreshes the rings of every painted region in <paramref name="regions"/> (or only those in
        /// <paramref name="only"/>) from all of them. Returns the ids of those that changed.
        /// </summary>
        public static List<string> RefreshSurrounds(IReadOnlyList<RegionData> regions, ICollection<string>? only = null)
        {
            var world = new WorldTiles(regions);
            var changed = new List<string>();
            foreach (RegionData r in regions)
                if ((only == null || only.Contains(r.Id)) && RefreshSurround(r, world.At)) changed.Add(r.Id);
            return changed;
        }

        // ------------------------------------------------------------------ stones

        /// <summary>A kind of stones to paint with: its material's name and colour (a palette name or #hex).</summary>
        public sealed class StoneBrush
        {
            public string Id = "";
            public string Name = "";
            public string Material = "";
            public string Colour = "";
        }

        /// <summary>Every kind of stones of the layout (continent.json `stones`), as the blockout scatters them.</summary>
        public static List<StoneBrush> StoneBrushes(ContinentData data) =>
            data.Stones.Select(s => new StoneBrush
            {
                Id = s.Key, Name = s.Value.Name.Length > 0 ? s.Value.Name : s.Key, Material = "LandStones" + Blockout.Pascal(s.Key), Colour = s.Value.Palette,
            }).ToList();

        /// <summary>Characters stones may be written as, in the order they are taken.</summary>
        private const string StoneSpare = "1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!#$%&*+-=?@^~<>/|:;.,_()[]{}'`";

        /// <summary>The character the piece writes stones of <paramref name="material"/> at <paramref name="share"/> as, added to its stone legend if it has none.</summary>
        public static char StoneCharFor(Piece piece, string material, float share)
        {
            piece.StoneLegend ??= new SortedDictionary<string, StoneKind>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, StoneKind> e in piece.StoneLegend)
                if (e.Key.Length == 1 && e.Value.Material == material && Math.Abs(e.Value.Share - share) < 1e-4f) return e.Key[0];
            char c = StoneSpare.FirstOrDefault(s => !piece.StoneLegend.ContainsKey(s.ToString()));
            if (c == default) throw new InvalidOperationException($"{piece.Id}: its stones have no characters left for another kind.");
            piece.StoneLegend[c.ToString()] = new StoneKind { Material = material, Share = share };
            return c;
        }

        /// <summary>The character for a brush's stones at a level of <see cref="GroundStones.Levels"/>, its material added to the region's under a name of its colour.</summary>
        public static char StoneCharFor(RegionData region, Piece piece, StoneBrush brush, int level)
        {
            string material = MaterialFor(region, brush.Material, new Dictionary<string, string> { [brush.Material] = brush.Colour });
            return StoneCharFor(piece, material, GroundStones.Levels[Math.Max(0, Math.Min(GroundStones.Levels.Length - 1, level))]);
        }

        /// <summary>The stones at an absolute tile: their kind, or null for none (or outside the piece).</summary>
        public static StoneKind? StoneAt(Piece piece, int x, int y)
        {
            int[] r = piece.Rect!;
            if (piece.Stones == null || piece.StoneLegend == null || x < r[0] || y < r[1] || x >= r[2] || y >= r[3]) return null;
            string row = piece.Stones[r[3] - 1 - y];
            int i = x - r[0];
            return i < row.Length && row[i] != ' ' && piece.StoneLegend.TryGetValue(row[i].ToString(), out StoneKind k) ? k : null;
        }

        /// <summary>Sets the stones at an absolute tile (' ': none), giving the piece rows of stones if it has none; false when nothing changed.</summary>
        public static bool SetStone(Piece piece, int x, int y, char c)
        {
            int[] r = piece.Rect!;
            if (x < r[0] || y < r[1] || x >= r[2] || y >= r[3]) return false;
            int w = r[2] - r[0], h = r[3] - r[1];
            if (piece.Stones == null)
            {
                if (c == ' ') return false;
                piece.Stones = Enumerable.Repeat(new string(' ', w), h).ToList();
            }
            int ri = r[3] - 1 - y, i = x - r[0];
            string row = piece.Stones[ri].PadRight(w);
            if (row[i] == c) return false;
            piece.Stones[ri] = row.Substring(0, i) + c + row.Substring(i + 1);
            return true;
        }

        /// <summary>Drops stone kinds nothing uses, and the stones altogether when none are left.</summary>
        public static void PruneStones(Piece piece)
        {
            if (piece.Stones == null || piece.StoneLegend == null) { piece.Stones = null; piece.StoneLegend = null; return; }
            var used = new HashSet<char>();
            foreach (string row in piece.Stones) used.UnionWith(row);
            foreach (string k in piece.StoneLegend.Keys.Where(k => k.Length != 1 || !used.Contains(k[0])).ToList()) piece.StoneLegend.Remove(k);
            if (piece.StoneLegend.Count == 0) { piece.Stones = null; piece.StoneLegend = null; }
            else piece.Stones = piece.Stones.Select(s => s.TrimEnd()).ToList();
        }

        // ------------------------------------------------------------------ brush shapes

        /// <summary>The tiles of a round brush: radius 0 is the one tile.</summary>
        public static IEnumerable<(int x, int y)> Disc(int cx, int cy, float radius)
        {
            int r = (int)Math.Ceiling(radius);
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius + 0.25f) yield return (x, y);
        }

        /// <summary>
        /// The tiles of a straight stroke <paramref name="width"/> tiles wide from one tile to another,
        /// as a road is drawn: centred on the tiles' middles, square-ended.
        /// </summary>
        public static HashSet<(int x, int y)> Line(int ax, int ay, int bx, int by, int width)
        {
            var tiles = new HashSet<(int, int)>();
            float half = Math.Max(1, width) / 2f;
            float dx = bx - ax, dy = by - ay, len = (float)Math.Sqrt(dx * dx + dy * dy);
            int pad = (int)Math.Ceiling(half) + 1;
            for (int y = Math.Min(ay, by) - pad; y <= Math.Max(ay, by) + pad; y++)
                for (int x = Math.Min(ax, bx) - pad; x <= Math.Max(ax, bx) + pad; x++)
                {
                    float px = x - ax, py = y - ay;
                    // Along and across the stroke (a click: a square). Across, the band is (-half, half],
                    // so an even width leans to one side, as it must on tiles; the ends are square.
                    bool point = len < 1e-4f;
                    float along = point ? px : (px * dx + py * dy) / len;
                    float across = point ? py : (px * dy - py * dx) / len;
                    bool inAlong = point
                        ? along > -half + 1e-4f && along <= half + 1e-4f
                        : along >= -(half - 0.5f) - 1e-4f && along <= len + half - 0.5f + 1e-4f;
                    if (inAlong && across > -half + 1e-4f && across <= half + 1e-4f) tiles.Add((x, y));
                }
            return tiles;
        }

        /// <summary>
        /// The end of a stroke from (<paramref name="ax"/>, <paramref name="ay"/>) toward a tile,
        /// turned to the nearest of the eight directions (straight or 45 degrees), as RuneScape's
        /// roads run.
        /// </summary>
        public static (int x, int y) Snap45(int ax, int ay, int bx, int by)
        {
            int dx = bx - ax, dy = by - ay;
            int adx = Math.Abs(dx), ady = Math.Abs(dy);
            if (adx > 2 * ady) return (bx, ay);
            if (ady > 2 * adx) return (ax, by);
            int d = Math.Max(adx, ady);
            return (ax + Math.Sign(dx) * d, ay + Math.Sign(dy) * d);
        }
    }
}
