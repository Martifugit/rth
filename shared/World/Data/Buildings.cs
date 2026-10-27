using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// What a <see cref="PieceKind.Building"/> piece is: a gabled block on its <see cref="Piece.Rect"/>
    /// put together from a building kit's tile pieces (walls, doorways, windows, roof slabs, gables),
    /// as tools/art/blender/buildings/venaria.py's volume() puts one together in Blender. Its walls run
    /// along the rectangle's edges, so the tiles inside are its floor; doorways leave the edge open.
    /// </summary>
    public sealed class BuildingSpec
    {
        /// <summary>The kit (world/kits/&lt;kit&gt;.json).</summary>
        public string Kit = "venaria";
        /// <summary>Storeys under the roof (1 to 3). Only the ground floor is walked in, for now.</summary>
        public int? Storeys;
        /// <summary>The ridge's direction: "x" (east-west) or "y" (north-south); default along the longer side.</summary>
        public string? Ridge;
        /// <summary>
        /// The pieces along each face, one a tile: "S" and "N" from the west, "W" and "E" from the
        /// south; "S1", "E2"... for the storeys above. An entry is a piece name with attachments
        /// ("WallDoorway+Lantern", "Wall+Woodpile"); a missing or empty entry is the kit's plain wall.
        /// </summary>
        public SortedDictionary<string, List<string>>? Faces;
        /// <summary>Material swaps, one set a storey from the ground ({ "Stone": "Granite" }): palette names.</summary>
        public List<SortedDictionary<string, string>?>? Colors;
        /// <summary>The roof's material swaps ({ "Thatch": "Slate" }).</summary>
        public SortedDictionary<string, string>? RoofColors;
        /// <summary>Upper storeys jut out over the gable ends.</summary>
        public bool? Jetty;
        /// <summary>The gable end ("N", "E", "S", "W") whose top has the lancet slit.</summary>
        public string? Slit;
        /// <summary>The gable end carrying a bell on its apex.</summary>
        public string? Bellcote;
        /// <summary>A chimney in a gable end where the hearth is: the end and the tile along it ("W1": the west end's second tile from the south).</summary>
        public string? Chimney;
        /// <summary>The floor's palette name (default "Planks").</summary>
        public string? Floor;
    }

    /// <summary>
    /// A building kit's rules (world/kits/&lt;id&gt;.json, written by the kit's Blender script on export):
    /// where its models are, its sizes, and which pieces are doorways, what may hang on what, and
    /// what stands out from a wall far enough to block the tile outside it.
    /// </summary>
    public sealed class BuildingKit
    {
        public string Id = "";
        /// <summary>The folder of its models in the Unity project ("Assets/Art/Buildings/Venaria").</summary>
        public string Models = "";
        public float Storey = 2.6f;
        /// <summary>The roof's rise per tile run, its base over a storey, how far the verge and an upper storey's jetty reach, a roof slab's thickness.</summary>
        public float Rise = 0.9f;
        public float RoofBase = 2.7f;
        public float Verge = 0.4f;
        public float JettyOut = 0.25f;
        public float Slab = 0.09f;
        /// <summary>The plain wall, for every tile a face doesn't name.</summary>
        public string Wall = "Wall";
        /// <summary>The widest gable span (tiles across the ridge) its gable pieces cover.</summary>
        public int MaxSpan = 8;
        public List<string> Pieces = new List<string>();
        /// <summary>Wall pieces that are ways in, each with the leaf hung in it ("" for none).</summary>
        public Dictionary<string, string> Openings = new Dictionary<string, string>();
        /// <summary>Attachments and the wall pieces they may hang on.</summary>
        public Dictionary<string, List<string>> Attachments = new Dictionary<string, List<string>>();
        /// <summary>Attachments that block the tile outside their wall.</summary>
        public List<string> Blocking = new List<string>();
        /// <summary>Material swaps for every storey above the ground (an upper storey sits on a timber sill).</summary>
        public Dictionary<string, string> UpperColors = new Dictionary<string, string>();
    }

    /// <summary>The building kits in world/kits, found by walking up from the working directory.</summary>
    public static class BuildingKits
    {
        public const string Folder = "world/kits";
        private static Dictionary<string, BuildingKit>? _kits;

        /// <summary>The folder to read; null: the first world/kits above the working directory.</summary>
        public static string? Directory;

        public static BuildingKit? Get(string id)
        {
            _kits ??= Load();
            return _kits.TryGetValue(id, out BuildingKit kit) ? kit : null;
        }

        /// <summary>Adds or replaces a kit (tests, tools).</summary>
        public static void Register(BuildingKit kit)
        {
            _kits ??= Load();
            _kits[kit.Id] = kit;
        }

        /// <summary>Forgets the loaded kits, so the next lookup reads the files again.</summary>
        public static void Reload() => _kits = null;

        private static Dictionary<string, BuildingKit> Load()
        {
            var kits = new Dictionary<string, BuildingKit>(StringComparer.Ordinal);
            string? dir = Directory ?? Find();
            if (dir == null || !System.IO.Directory.Exists(dir)) return kits;
            foreach (string path in System.IO.Directory.GetFiles(dir, "*.json"))
            {
                BuildingKit? kit = JsonConvert.DeserializeObject<BuildingKit>(File.ReadAllText(path));
                if (kit != null && kit.Id.Length > 0) kits[kit.Id] = kit;
            }
            return kits;
        }

        private static string? Find()
        {
            for (string? d = System.IO.Directory.GetCurrentDirectory(); d != null; d = Path.GetDirectoryName(d))
            {
                string candidate = Path.Combine(d, "world", "kits");
                if (System.IO.Directory.Exists(candidate)) return candidate;
            }
            return null;
        }
    }

    /// <summary>Reading a building's description: its faces, ends, doors and the tiles it blocks.</summary>
    public static class Buildings
    {
        public static readonly string[] Sides = { "S", "N", "W", "E" };

        /// <summary>The ridge runs along X (east-west).</summary>
        public static bool RidgeAlongX(BuildingSpec spec, int[] rect)
        {
            if (spec.Ridge == "x") return true;
            if (spec.Ridge == "y") return false;
            return rect[2] - rect[0] >= rect[3] - rect[1];
        }

        /// <summary>Tiles along a side: S and N run east-west, W and E north-south.</summary>
        public static int Length(string side, int[] rect) => side == "S" || side == "N" ? rect[2] - rect[0] : rect[3] - rect[1];

        /// <summary>The gable ends: the sides across the ridge.</summary>
        public static bool IsGableEnd(BuildingSpec spec, int[] rect, string side) =>
            RidgeAlongX(spec, rect) ? side == "W" || side == "E" : side == "S" || side == "N";

        /// <summary>A face's entries for a storey, one a tile; a missing or empty entry is the kit's wall.</summary>
        public static List<string> Face(BuildingSpec spec, BuildingKit kit, int[] rect, string side, int storey)
        {
            int n = Length(side, rect);
            List<string>? given = null;
            spec.Faces?.TryGetValue(storey == 0 ? side : side + storey, out given);
            var list = new List<string>(n);
            for (int i = 0; i < n; i++)
                list.Add(given != null && i < given.Count && !string.IsNullOrWhiteSpace(given[i]) ? given[i].Trim() : kit.Wall);
            return list;
        }

        /// <summary>An entry's wall piece and its attachments ("WallDoorway+Lantern").</summary>
        public static (string piece, string[] attachments) Split(string entry)
        {
            string[] parts = entry.Split('+');
            return (parts[0].Trim(), parts.Skip(1).Select(p => p.Trim()).ToArray());
        }

        /// <summary>The tile just outside tile <paramref name="i"/> of a side.</summary>
        public static (int x, int y) Outside(int[] rect, string side, int i)
        {
            switch (side)
            {
                case "S": return (rect[0] + i, rect[1] - 1);
                case "N": return (rect[0] + i, rect[3]);
                case "W": return (rect[0] - 1, rect[1] + i);
                default: return (rect[2], rect[1] + i);
            }
        }

        /// <summary>A chimney's end and tile ("W1").</summary>
        public static (string side, int at)? Chimney(BuildingSpec spec)
        {
            if (string.IsNullOrWhiteSpace(spec.Chimney)) return null;
            string side = spec.Chimney!.Substring(0, 1);
            if (!Sides.Contains(side) || !int.TryParse(spec.Chimney.Substring(1), out int at))
                throw new RegionCompiler.PieceException("chimney is a gable end and a tile along it, like \"W1\".");
            return (side, at);
        }

        /// <summary>Checks a building against its kit; throws on what can't be built.</summary>
        public static BuildingKit Check(Piece piece)
        {
            BuildingSpec spec = piece.Building ?? throw new RegionCompiler.PieceException("needs a building (its kit, faces and roof).");
            BuildingKit kit = BuildingKits.Get(spec.Kit) ?? throw new RegionCompiler.PieceException($"names kit '{spec.Kit}', which isn't in {BuildingKits.Folder}.");
            int[] r = piece.Rect ?? throw new RegionCompiler.PieceException("needs a rect (the tiles inside its walls).");
            if (r.Length != 4 || r[2] <= r[0] || r[3] <= r[1]) throw new RegionCompiler.PieceException("rect is [x0, y0, x1, y1) with x1 > x0 and y1 > y0.");
            int storeys = spec.Storeys ?? 1;
            if (storeys < 1 || storeys > 3) throw new RegionCompiler.PieceException("storeys is 1 to 3.");
            if (spec.Ridge != null && spec.Ridge != "x" && spec.Ridge != "y") throw new RegionCompiler.PieceException("ridge is \"x\" or \"y\".");
            int span = RidgeAlongX(spec, r) ? r[3] - r[1] : r[2] - r[0];
            if (span % 2 != 0 || span < 2 || span > kit.MaxSpan)
                throw new RegionCompiler.PieceException($"its gable span (tiles across the ridge) is {span}; the {kit.Id} kit builds even spans from 2 to {kit.MaxSpan}.");
            if (spec.Faces != null)
                foreach (KeyValuePair<string, List<string>> face in spec.Faces)
                {
                    string side = face.Key.Substring(0, 1);
                    int storey = face.Key.Length > 1 && int.TryParse(face.Key.Substring(1), out int s) ? s : face.Key.Length > 1 ? -1 : 0;
                    if (!Sides.Contains(side) || storey < 0 || storey >= storeys)
                        throw new RegionCompiler.PieceException($"face '{face.Key}' isn't a side (S, N, W, E) and a storey below {storeys}.");
                    if (face.Value.Count > Length(side, r))
                        throw new RegionCompiler.PieceException($"face '{face.Key}' lists {face.Value.Count} tiles; that side is {Length(side, r)} long.");
                    foreach (string entry in face.Value)
                    {
                        if (string.IsNullOrWhiteSpace(entry)) continue;
                        (string wall, string[] extras) = Split(entry);
                        if (!kit.Pieces.Contains(wall)) throw new RegionCompiler.PieceException($"face '{face.Key}' uses '{wall}', which the {kit.Id} kit doesn't have.");
                        if (storey > 0 && kit.Openings.ContainsKey(wall)) throw new RegionCompiler.PieceException($"face '{face.Key}' has a doorway above the ground floor.");
                        foreach (string a in extras)
                            if (!kit.Attachments.TryGetValue(a, out List<string> on) || !on.Contains(wall))
                                throw new RegionCompiler.PieceException($"'{a}' can't hang on '{wall}' (face '{face.Key}').");
                    }
                }
            foreach ((string what, string? side) in new[] { ("slit", spec.Slit), ("bellcote", spec.Bellcote) })
                if (side != null && (!Sides.Contains(side) || !IsGableEnd(spec, r, side)))
                    throw new RegionCompiler.PieceException($"{what} is on a gable end, {(RidgeAlongX(spec, r) ? "W or E" : "S or N")}.");
            (string side, int at)? chimney = Chimney(spec);
            if (chimney.HasValue)
            {
                (string cSide, int cAt) = chimney.Value;
                if (!IsGableEnd(spec, r, cSide)) throw new RegionCompiler.PieceException($"the chimney is in a gable end, {(RidgeAlongX(spec, r) ? "W or E" : "S or N")}.");
                if (cAt < 0 || cAt >= Length(cSide, r)) throw new RegionCompiler.PieceException($"the chimney's tile {cAt} isn't along the {cSide} end.");
            }
            return kit;
        }

        /// <summary>The ground floor's doorways: side and tile along it.</summary>
        public static IEnumerable<(string side, int at)> Doors(BuildingSpec spec, BuildingKit kit, int[] rect)
        {
            foreach (string side in Sides)
            {
                List<string> face = Face(spec, kit, rect, side, 0);
                for (int i = 0; i < face.Count; i++)
                    if (kit.Openings.ContainsKey(Split(face[i]).piece)) yield return (side, i);
            }
        }

        /// <summary>Tiles outside the walls that something on them blocks: a chimney breast, a woodpile.</summary>
        public static IEnumerable<(int x, int y)> BlockedOutside(BuildingSpec spec, BuildingKit kit, int[] rect)
        {
            foreach (string side in Sides)
            {
                List<string> face = Face(spec, kit, rect, side, 0);
                for (int i = 0; i < face.Count; i++)
                    if (Split(face[i]).attachments.Any(a => kit.Blocking.Contains(a))) yield return Outside(rect, side, i);
            }
            (string side, int at)? chimney = Chimney(spec);
            if (chimney.HasValue) yield return Outside(rect, chimney.Value.side, chimney.Value.at);
        }
    }
}
