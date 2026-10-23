using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// Turns a region's data (and what its scene adds by hand) into what the server loads: the
    /// collision map, the creature spawns, the objects and the region's info. Plain C#, so anyone
    /// can export and check a region without Unity (tools/world). Sync World draws the same pieces
    /// in Unity with the same footprints and walls, and checks that the scene exports the same map.
    /// </summary>
    public static class RegionCompiler
    {
        public sealed class Result
        {
            public RegionData Region = new RegionData();
            public CollisionLayers Layers = new CollisionLayers(0, 0, 1, 1);
            public MapData Map = new MapData(0, 0, 1, 1);
            public List<ObjectEntry> Objects = new List<ObjectEntry>();
            public List<NpcEntry> Npcs = new List<NpcEntry>();
            public bool HasSpawn;
            public List<string> Errors = new List<string>();
            public List<string> Warnings = new List<string>();
            /// <summary>The region's heights, or null when it is flat.</summary>
            public Heightfield? Terrain;
        }

        /// <summary>Kinds drawn as flat boxes at height 0, which don't follow a terrain.</summary>
        private static readonly HashSet<string> FlatKinds = new HashSet<string> { PieceKind.Patch, PieceKind.Road, PieceKind.Water, PieceKind.House, PieceKind.Wall, PieceKind.Fence };

        public static Result Compile(RegionData region, HandmadeData? handmade)
        {
            var result = new Result { Region = region };
            int w = region.Width, h = region.Height;
            if (w <= 0 || h <= 0)
            {
                result.Errors.Add($"Region '{region.Id}' has no size.");
                w = Math.Max(w, 8);
                h = Math.Max(h, 8);
            }
            if (region.OriginX % 8 != 0 || region.OriginY % 8 != 0 || w % 8 != 0 || h % 8 != 0)
                result.Errors.Add($"Region '{region.Id}' must start and end on whole zones: origin and size multiples of 8.");

            var layers = new CollisionLayers(region.OriginX, region.OriginY, w, h, region.Plane + 1);
            result.Layers = layers;
            if (region.Ground != null)
            {
                CheckMaterial(region, region.Ground, "ground", result);
                for (int y = region.OriginY; y < region.OriginY + h; y++)
                    for (int x = region.OriginX; x < region.OriginX + w; x++)
                        layers.SetGround(region.Plane, x, y);
            }

            int[]? spawn = null;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Piece piece in region.Pieces)
            {
                string where = $"Piece '{piece.Id}' ({piece.Kind})";
                if (string.IsNullOrWhiteSpace(piece.Id)) result.Errors.Add($"A {piece.Kind} piece has no id.");
                else if (!ids.Add(piece.Id)) result.Errors.Add($"{where}: the id is used twice.");
                int plane = piece.Plane ?? region.Plane;
                if (plane < 0 || plane > 3) result.Errors.Add($"{where}: plane {plane} is not 0-3.");
                layers.EnsurePlanes(plane + 1);
                try
                {
                    AddPiece(region, piece, plane, layers, result, ref spawn);
                }
                catch (PieceException e)
                {
                    result.Errors.Add($"{where}: {e.Message}");
                }
            }

            // Terrain: a tile too steep to walk is a cliff, blocked as the Unity exporter blocks it.
            try
            {
                result.Terrain = TerrainText.Read(region);
            }
            catch (FormatException e)
            {
                result.Errors.Add(e.Message);
            }
            if (result.Terrain != null)
            {
                for (int y = region.OriginY; y < region.OriginY + h; y++)
                    for (int x = region.OriginX; x < region.OriginX + w; x++)
                        if (result.Terrain.IsSteep(x, y, region.Terrain!.CliffStep)) layers.Block(region.Plane, x, y);
                foreach (Piece piece in region.Pieces)
                    if ((piece.Plane ?? region.Plane) == region.Plane && FlatKinds.Contains(piece.Kind))
                        result.Warnings.Add($"Piece '{piece.Id}' ({piece.Kind}) is drawn flat at height 0 over the region's terrain: on terrain, draw ground with a tiles piece.");
            }

            if (handmade != null)
            {
                var extra = new CollisionLayers(region.OriginX, region.OriginY, w, h, 1);
                foreach (LayerText text in handmade.Planes) extra.AddText(text);
                layers.Merge(extra);
                result.Objects.AddRange(handmade.Objects);
                result.Npcs.AddRange(handmade.Npcs);
                if (handmade.Spawn != null)
                {
                    if (spawn != null) result.Errors.Add("Both the data and the hand-made content place the player spawn.");
                    else spawn = handmade.Spawn;
                }
            }

            result.HasSpawn = spawn != null;
            result.Map = layers.ToMapData(spawn != null ? spawn[0] : 0, spawn != null ? new TileCoord(spawn[1], spawn[2]) : (TileCoord?)null);
            if (spawn != null && result.Map.IsBlocked(spawn[0], spawn[1], spawn[2]))
                result.Errors.Add($"The spawn tile ({spawn[1]}, {spawn[2]}) is blocked.");
            foreach (NpcEntry n in result.Npcs)
                if (result.Map.IsBlocked(n.Plane, n.X, n.Y))
                    result.Warnings.Add($"NPC spawn '{n.Npc}' stands on a blocked tile ({n.X}, {n.Y}) on plane {n.Plane}.");
            return result;
        }

        public sealed class PieceException : Exception
        {
            public PieceException(string message) : base(message) { }
        }

        private static void AddPiece(RegionData region, Piece piece, int plane, CollisionLayers layers, Result result, ref int[]? spawn)
        {
            switch (piece.Kind)
            {
                case PieceKind.Patch:
                    CheckMaterial(region, Need(piece.Material, "material"), piece.Id, result);
                    foreach ((int x, int y) in RectTiles(NeedRect(piece))) layers.SetGround(plane, x, y);
                    break;
                case PieceKind.Road:
                    CheckMaterial(region, Need(piece.Material, "material"), piece.Id, result);
                    foreach ((int x, int y) in LineTiles(region, piece)) layers.SetGround(plane, x, y);
                    break;
                case PieceKind.Water:
                    CheckMaterial(region, Need(piece.Material, "material"), piece.Id, result);
                    foreach ((int x, int y) in piece.Rect != null ? RectTiles(NeedRect(piece)) : LineTiles(region, piece)) layers.Block(plane, x, y);
                    break;
                case PieceKind.Wall:
                case PieceKind.Fence:
                    CheckMaterial(region, Need(piece.Material, "material"), piece.Id, result);
                    int[] from = NeedPair(piece.From, "from"), to = NeedPair(piece.To, "to");
                    if (from[0] != to[0] && from[1] != to[1]) throw new PieceException("must run along one grid line (from and to share x or y).");
                    AddWallLine(layers, plane, from[0], from[1], to[0], to[1], piece.Gaps);
                    break;
                case PieceKind.House:
                    int[] r = NeedRect(piece);
                    foreach (string m in new[] { Need(piece.Walls, "walls"), Need(piece.Floor, "floor"), Need(piece.Roof, "roof") })
                        CheckMaterial(region, m, piece.Id, result);
                    string door = Need(piece.Door, "door");
                    if (door != "N" && door != "E" && door != "S" && door != "W") throw new PieceException("door must be N, E, S or W.");
                    if (!piece.DoorAt.HasValue) throw new PieceException("needs doorAt (the tile along the door's side).");
                    foreach ((int x, int y) in RectTiles(r)) layers.SetGround(plane, x, y);
                    int d = piece.DoorAt.Value;
                    AddWallLine(layers, plane, r[0], r[1], r[2], r[1], door == "S" ? new List<int> { d } : null);
                    AddWallLine(layers, plane, r[0], r[3], r[2], r[3], door == "N" ? new List<int> { d } : null);
                    AddWallLine(layers, plane, r[0], r[1], r[0], r[3], door == "W" ? new List<int> { d } : null);
                    AddWallLine(layers, plane, r[2], r[1], r[2], r[3], door == "E" ? new List<int> { d } : null);
                    break;
                case PieceKind.Blocker:
                    foreach ((int x, int y) in RectTiles(NeedRect(piece))) layers.Block(plane, x, y);
                    break;
                case PieceKind.Tree:
                case PieceKind.Rock:
                case PieceKind.Prop:
                    NeedPair(piece.At, "at");
                    if (piece.Parts != null)
                        foreach (Part part in piece.Parts) CheckMaterial(region, part.Material, piece.Id, result);
                    foreach ((int x, int y) in FootprintTiles(piece)) layers.Block(plane, x, y);
                    break;
                case PieceKind.Tiles:
                    foreach ((int x, int y, TileKind kind) in TilesOf(piece))
                    {
                        layers.SetGround(plane, x, y);
                        if (!kind.Walkable) layers.Block(plane, x, y);
                    }
                    foreach (TileKind kind in piece.Legend!.Values)
                    {
                        CheckMaterial(region, kind.Material, piece.Id, result);
                        if (kind.Cliff != null) CheckMaterial(region, kind.Cliff, piece.Id, result);
                    }
                    break;
                case PieceKind.Npc:
                    NeedPair(piece.At, "at");
                    result.Npcs.Add(new NpcEntry { Npc = Need(piece.Npc, "npc"), Plane = plane, X = piece.X, Y = piece.Y, Wander = piece.Wander ?? -1 });
                    break;
                case PieceKind.Spawn:
                    NeedPair(piece.At, "at");
                    if (spawn != null) throw new PieceException("a region has one player spawn.");
                    spawn = new[] { plane, piece.X, piece.Y };
                    break;
                default:
                    throw new PieceException($"unknown kind; use one of {string.Join(", ", PieceKind.All)}.");
            }

            if (piece.Kind == PieceKind.Tiles && piece.Rect != null &&
                (piece.Rect[0] < region.OriginX || piece.Rect[1] < region.OriginY || piece.Rect[2] > region.OriginX + region.Width || piece.Rect[3] > region.OriginY + region.Height))
                throw new PieceException("its rect reaches outside the region.");
            if (piece.At != null && !InRegion(region, piece.X, piece.Y)) throw new PieceException($"({piece.X}, {piece.Y}) is outside the region.");
            if (piece.Object != null)
            {
                if (piece.At == null) throw new PieceException("an object needs a tile (at).");
                var entry = new ObjectEntry { Type = piece.Object, Plane = plane, X = piece.X, Y = piece.Y };
                if (piece.ClimbTo != null)
                {
                    if (piece.ClimbTo.Length != 3) throw new PieceException("climbTo is [plane, x, y].");
                    entry.ToPlane = piece.ClimbTo[0];
                    entry.ToX = piece.ClimbTo[1];
                    entry.ToY = piece.ClimbTo[2];
                }
                result.Objects.Add(entry);
            }
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>
        /// The tiles a placed piece blocks: its <see cref="Piece.Footprint"/> turned with it, or by
        /// default its own tile (trees, rocks, and props that are objects).
        /// </summary>
        public static IEnumerable<(int x, int y)> FootprintTiles(Piece piece)
        {
            int[]? f = piece.Footprint;
            if (f == null)
            {
                bool blocksOwnTile = piece.Kind == PieceKind.Tree || piece.Kind == PieceKind.Rock || (piece.Kind == PieceKind.Prop && piece.Object != null);
                if (blocksOwnTile) yield return (piece.X, piece.Y);
                yield break;
            }
            if (f.Length != 4) throw new PieceException("footprint is [dx0, dy0, dx1, dy1).");
            int turns = QuarterTurns(piece.Rot);
            for (int dy = f[1]; dy < f[3]; dy++)
                for (int dx = f[0]; dx < f[2]; dx++)
                {
                    (int rx, int ry) = Turn(dx, dy, turns);
                    yield return (piece.X + rx, piece.Y + ry);
                }
        }

        /// <summary>
        /// Every tile a tiles piece gives, with what it is (spaces, no ground, are left out). Throws
        /// a <see cref="PieceException"/> when the rows don't fit the rectangle or use an unknown character.
        /// </summary>
        public static IEnumerable<(int x, int y, TileKind kind)> TilesOf(Piece piece)
        {
            int[] r = NeedRect(piece);
            if (piece.Legend == null || piece.Legend.Count == 0) throw new PieceException("needs a legend (what each character is).");
            var legend = new Dictionary<char, TileKind>();
            foreach (KeyValuePair<string, TileKind> e in piece.Legend)
            {
                if (e.Key.Length != 1 || e.Key[0] == ' ') throw new PieceException($"legend key '{e.Key}' must be one character other than a space.");
                if (string.IsNullOrWhiteSpace(e.Value.Material)) throw new PieceException($"legend '{e.Key}' needs a material.");
                legend[e.Key[0]] = e.Value;
            }
            int w = r[2] - r[0], h = r[3] - r[1];
            List<string> rows = piece.Rows ?? throw new PieceException("needs rows.");
            if (rows.Count != h) throw new PieceException($"has {rows.Count} rows; its rect is {h} tall.");
            var tiles = new List<(int, int, TileKind)>();
            for (int i = 0; i < h; i++)
            {
                string row = rows[i];
                if (row.Length > w) throw new PieceException($"row {i} from the north is {row.Length} long; its rect is {w} wide.");
                int y = r[3] - 1 - i;
                for (int c = 0; c < row.Length; c++)
                {
                    if (row[c] == ' ') continue;
                    if (!legend.TryGetValue(row[c], out TileKind? kind)) throw new PieceException($"row {i} from the north uses '{row[c]}', which the legend doesn't list.");
                    tiles.Add((r[0] + c, y, kind));
                }
            }
            return tiles;
        }

        /// <summary>A footprint can only turn in quarter turns; anything else is rounded.</summary>
        public static int QuarterTurns(float? degrees) => ((int)Math.Round((degrees ?? 0f) / 90f) % 4 + 4) % 4;

        /// <summary>Turns a tile offset clockwise (seen from above) by quarter turns about the anchor tile.</summary>
        private static (int, int) Turn(int dx, int dy, int turns)
        {
            for (int i = 0; i < turns; i++) (dx, dy) = (dy, -dx);
            return (dx, dy);
        }

        public static IEnumerable<(int x, int y)> RectTiles(int[] r)
        {
            for (int y = r[1]; y < r[3]; y++)
                for (int x = r[0]; x < r[2]; x++)
                    yield return (x, y);
        }

        private static IEnumerable<(int x, int y)> LineTiles(RegionData region, Piece piece)
        {
            if (piece.Line == null || piece.Line.Count < 2) throw new PieceException("needs a line of at least two points.");
            if (!piece.Width.HasValue) throw new PieceException("needs width (half the width, in tiles).");
            return TileShapes.Along(piece.Line, piece.Width.Value, region.OriginX, region.OriginY, region.Width, region.Height);
        }

        /// <summary>
        /// Walls along a grid line from (x0, y0) to (x1, y1), except the gap tiles: the edges the
        /// Unity exporter finds for WorldKit.Wall's boxes (one per run between gaps).
        /// </summary>
        public static void AddWallLine(CollisionMap map, int plane, int x0, int y0, int x1, int y1, IList<int>? gaps)
        {
            bool alongX = y0 == y1;
            int from = alongX ? Math.Min(x0, x1) : Math.Min(y0, y1);
            int to = alongX ? Math.Max(x0, x1) : Math.Max(y0, y1);
            var gapSet = gaps != null ? new HashSet<int>(gaps) : new HashSet<int>();
            int runStart = from;
            for (int t = from; t <= to; t++)
            {
                bool end = t == to || gapSet.Contains(t);
                if (!end) continue;
                for (int i = runStart; i < t; i++)
                {
                    if (alongX) map.AddWall(plane, i, y0 - 1, WallSide.North);
                    else map.AddWall(plane, x0 - 1, i, WallSide.East);
                }
                runStart = t + 1;
            }
        }

        // ------------------------------------------------------------------ checks

        private static bool InRegion(RegionData r, int x, int y) =>
            x >= r.OriginX && x < r.OriginX + r.Width && y >= r.OriginY && y < r.OriginY + r.Height;

        private static string Need(string? value, string field) =>
            string.IsNullOrWhiteSpace(value) ? throw new PieceException($"needs {field}.") : value!;

        private static int[] NeedPair(int[]? value, string field) =>
            value == null || value.Length != 2 ? throw new PieceException($"needs {field} as [x, y].") : value;

        private static int[] NeedRect(Piece piece)
        {
            int[]? r = piece.Rect;
            if (r == null || r.Length != 4) throw new PieceException("needs rect as [x0, y0, x1, y1).");
            if (r[2] <= r[0] || r[3] <= r[1]) throw new PieceException("rect is empty: x1 and y1 are excluded, so they must be larger than x0 and y0.");
            return r;
        }

        private static void CheckMaterial(RegionData region, string name, string where, Result result)
        {
            if (!region.Materials.ContainsKey(name))
                result.Errors.Add($"'{where}' uses material '{name}', which the region's materials don't list.");
        }

        // ------------------------------------------------------------------ files

        /// <summary>The files the server loads, by file name, with the exact text the Unity exporter writes.</summary>
        public static Dictionary<string, byte[]> Files(Result result)
        {
            string id = result.Region.Id;
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            using (var stream = new MemoryStream())
            {
                result.Map.Write(stream);
                files[id + ".rthmap"] = stream.ToArray();
            }
            files[id + ".spawns.json"] = Utf8(SpawnsJson(result.Npcs));
            files[id + ".objects.json"] = Utf8(ObjectsJson(result.Objects));
            files[id + ".region.json"] = Utf8(RegionJson(result.Region, result.HasSpawn));
            return files;
        }

        /// <summary>Writes the region's files into the maps folder (server/data/maps).</summary>
        public static void WriteFiles(Result result, string mapsDirectory)
        {
            Directory.CreateDirectory(mapsDirectory);
            foreach (KeyValuePair<string, byte[]> file in Files(result))
                File.WriteAllBytes(Path.Combine(mapsDirectory, file.Key), file.Value);
        }

        private static byte[] Utf8(string s) => new UTF8Encoding(false).GetBytes(s);

        public static string ObjectsJson(IEnumerable<ObjectEntry> objects)
        {
            var sb = new StringBuilder("[\n");
            int count = 0;
            foreach (ObjectEntry o in objects)
            {
                if (count++ > 0) sb.Append(",\n");
                sb.Append("  { \"type\": \"").Append(o.Type.Trim().Replace("\"", ""))
                  .Append("\", \"plane\": ").Append(o.Plane)
                  .Append(", \"x\": ").Append(o.X)
                  .Append(", \"y\": ").Append(o.Y);
                if (o.ToPlane.HasValue)
                    sb.Append(", \"toPlane\": ").Append(o.ToPlane.Value)
                      .Append(", \"toX\": ").Append(o.ToX ?? 0)
                      .Append(", \"toY\": ").Append(o.ToY ?? 0);
                sb.Append(" }");
            }
            return sb.Append("\n]\n").ToString();
        }

        public static string SpawnsJson(IEnumerable<NpcEntry> npcs)
        {
            var sb = new StringBuilder("[\n");
            int count = 0;
            foreach (NpcEntry n in npcs)
            {
                if (count++ > 0) sb.Append(",\n");
                sb.Append("  { \"npc\": \"").Append(n.Npc.Trim().Replace("\"", ""))
                  .Append("\", \"plane\": ").Append(n.Plane)
                  .Append(", \"x\": ").Append(n.X)
                  .Append(", \"y\": ").Append(n.Y)
                  .Append(", \"wander\": ").Append(n.Wander)
                  .Append(" }");
            }
            return sb.Append("\n]\n").ToString();
        }

        public static string RegionJson(RegionData region, bool hasSpawn)
        {
            static string Quote(string? s) => "\"" + (s ?? "").Replace("\\", "").Replace("\"", "'") + "\"";
            string title = string.IsNullOrWhiteSpace(region.Title) ? region.Id : region.Title.Trim();
            string scene = Path.GetFileNameWithoutExtension(region.Scene ?? "");
            return "{\n" +
                   "  \"scene\": " + Quote(scene) + ",\n" +
                   "  \"title\": " + Quote(title) + ",\n" +
                   "  \"hasSpawn\": " + (hasSpawn ? "true" : "false") + ",\n" +
                   "  \"instanced\": " + (region.Instanced ? "true" : "false") + ",\n" +
                   "  \"music\": " + Quote(region.Music) + (region.Atmosphere != null ? ",\n" + AtmosphereJson(region.Atmosphere) : "") + "\n" +
                   "}\n";
        }

        private static string AtmosphereJson(RegionAtmosphere a)
        {
            static string Hex(string c) => "\"#" + c.TrimStart('#').ToUpperInvariant() + "\"";
            static string F(float f) => f.ToString(CultureInfo.InvariantCulture);
            return "  \"atmosphere\": { \"sunIntensity\": " + F(a.SunIntensity) + ", \"sun\": " + Hex(a.Sun) +
                   ", \"ambientSky\": " + Hex(a.AmbientSky) + ", \"ambientEquator\": " + Hex(a.AmbientEquator) +
                   ", \"ambientGround\": " + Hex(a.AmbientGround) + ", \"fog\": " + Hex(a.Fog) +
                   ", \"fogStart\": " + F(a.FogStart) + ", \"fogEnd\": " + F(a.FogEnd) + " }";
        }
    }
}
