using System.Globalization;
using System.IO.Compression;
using System.Text;
using Rth.Shared.World;
using Rth.Shared.World.Data;

namespace Rth.WorldTool
{
    /// <summary>
    /// Draws compiled regions from above, so anyone can see a region's layout without Unity: a
    /// PNG (ground in its material colours, blocked tiles darker, water blue, walls as lines,
    /// trees, rocks, objects, creatures and the spawn as marks, the 8-tile zone grid) and a text
    /// map with coordinates.
    /// </summary>
    public static class MapRender
    {
        private static readonly Rgb Void = new(27, 31, 36);
        private static readonly Rgb DefaultGround = new(110, 150, 80);
        private static readonly Rgb Blocked = new(70, 70, 74);
        private static readonly Rgb WallColor = new(25, 20, 18);
        private static readonly Rgb ZoneLine = new(0, 0, 0);
        private static readonly Rgb TreeColor = new(28, 70, 30);
        private static readonly Rgb RockColor = new(150, 150, 155);
        private static readonly Rgb ObjectColor = new(60, 220, 240);
        private static readonly Rgb NpcColor = new(250, 140, 40);
        private static readonly Rgb SpawnColor = new(240, 60, 220);

        private readonly record struct Rgb(byte R, byte G, byte B)
        {
            public Rgb Mix(Rgb o, float t) => new((byte)(R + (o.R - R) * t), (byte)(G + (o.G - G) * t), (byte)(B + (o.B - B) * t));
        }

        // ------------------------------------------------------------------ PNG

        /// <summary>The art palette's colours by name (client/Assets/Resources/Art/palette.json), for materials that name one.</summary>
        public static Dictionary<string, string> Palette { get; } = new(StringComparer.Ordinal);

        public static void LoadPalette(string root)
        {
            string path = Path.Combine(root, "client", "Assets", "Resources", "Art", "palette.json");
            if (!File.Exists(path)) return;
            foreach (var c in Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path))["colours"] ?? new Newtonsoft.Json.Linq.JArray())
                if ((string?)c["name"] is string n && (string?)c["hex"] is string h) Palette[n] = h;
        }

        public static byte[] Region(RegionCompiler.Result r, int px)
        {
            RegionData region = r.Region;
            int w = region.Width * px, h = region.Height * px;
            var image = new Rgb[w * h];
            Rgb[,] paint = Paint(r);
            int plane = region.Plane;
            for (int ty = 0; ty < region.Height; ty++)
                for (int tx = 0; tx < region.Width; tx++)
                {
                    int x = region.OriginX + tx, y = region.OriginY + ty;
                    Rgb c = r.Layers.HasGround(plane, x, y) || r.Layers.IsBlockedLayer(plane, x, y) ? paint[tx, ty] : Void;
                    if (r.Layers.IsBlockedLayer(plane, x, y) && !IsWater(r, tx, ty)) c = c.Mix(Blocked, 0.65f);
                    Fill(image, w, tx * px, (region.Height - 1 - ty) * px, px, px, c);
                }
            // The zone grid, faintly.
            for (int ty = 0; ty < region.Height; ty += 8)
                for (int x = 0; x < w; x++) Blend(image, w, x, (region.Height - ty) * px - 1, ZoneLine, 0.25f, h);
            for (int tx = 0; tx < region.Width; tx += 8)
                for (int y = 0; y < h; y++) Blend(image, w, tx * px, y, ZoneLine, 0.25f, h);
            // Walls: north and east edges of every tile.
            for (int ty = 0; ty < region.Height; ty++)
                for (int tx = 0; tx < region.Width; tx++)
                {
                    CollisionFlags f = r.Layers.WallsAt(plane, region.OriginX + tx, region.OriginY + ty);
                    int left = tx * px, top = (region.Height - 1 - ty) * px;
                    if ((f & CollisionFlags.WallNorth) != 0) Fill(image, w, left, top, px, 2, WallColor);
                    if ((f & CollisionFlags.WallSouth) != 0) Fill(image, w, left, top + px - 2, px, 2, WallColor);
                    if ((f & CollisionFlags.WallEast) != 0) Fill(image, w, left + px - 2, top, 2, px, WallColor);
                    if ((f & CollisionFlags.WallWest) != 0) Fill(image, w, left, top, 2, px, WallColor);
                }
            // Marks for placed things.
            foreach (Piece p in region.Pieces)
            {
                if (p.At == null || (p.Plane ?? region.Plane) != plane) continue;
                int left = (p.X - region.OriginX) * px, top = (region.Height - 1 - (p.Y - region.OriginY)) * px;
                switch (p.Kind)
                {
                    case PieceKind.Tree: Fill(image, w, left + 1, top + 1, px - 2, px - 2, TreeColor); break;
                    case PieceKind.Rock: Fill(image, w, left + 2, top + 2, px - 4, px - 4, RockColor); break;
                }
            }
            foreach (ObjectEntry o in r.Objects.Where(o => o.Plane == plane))
                Frame(image, w, (o.X - region.OriginX) * px, (region.Height - 1 - (o.Y - region.OriginY)) * px, px, ObjectColor);
            foreach (NpcEntry n in r.Npcs.Where(n => n.Plane == plane))
                Fill(image, w, (n.X - region.OriginX) * px + px / 4, (region.Height - 1 - (n.Y - region.OriginY)) * px + px / 4, px / 2, px / 2, NpcColor);
            if (r.HasSpawn && r.Map.SpawnPlane == plane)
                Frame(image, w, (r.Map.Spawn.X - region.OriginX) * px, (region.Height - 1 - (r.Map.Spawn.Y - region.OriginY)) * px, px, SpawnColor);
            return Png(w, h, image);
        }

        /// <summary>Every region at its place in the world, at <paramref name="px"/> pixels a tile.</summary>
        public static byte[] World(List<RegionCompiler.Result> regions, int px)
        {
            int minX = regions.Min(r => r.Region.OriginX), minY = regions.Min(r => r.Region.OriginY);
            int maxX = regions.Max(r => r.Region.OriginX + r.Region.Width), maxY = regions.Max(r => r.Region.OriginY + r.Region.Height);
            int w = (maxX - minX) * px, h = (maxY - minY) * px;
            var image = new Rgb[w * h];
            Array.Fill(image, new Rgb(12, 14, 18));
            foreach (RegionCompiler.Result r in regions)
            {
                Rgb[,] paint = Paint(r);
                RegionData region = r.Region;
                for (int ty = 0; ty < region.Height; ty++)
                    for (int tx = 0; tx < region.Width; tx++)
                    {
                        int x = region.OriginX + tx, y = region.OriginY + ty;
                        bool ground = r.Layers.HasGround(region.Plane, x, y) || r.Layers.IsBlockedLayer(region.Plane, x, y);
                        Rgb c = ground ? paint[tx, ty] : Void;
                        if (r.Layers.IsBlockedLayer(region.Plane, x, y) && !IsWater(r, tx, ty)) c = c.Mix(Blocked, 0.65f);
                        Fill(image, w, (x - minX) * px, (maxY - 1 - y) * px, px, px, c);
                    }
            }
            return Png(w, h, image);
        }

        /// <summary>Each tile's ground colour: the region's ground, then patches, roads, house floors and water in order.</summary>
        private static Rgb[,] Paint(RegionCompiler.Result r)
        {
            RegionData region = r.Region;
            var paint = new Rgb[region.Width, region.Height];
            Rgb ground = region.Ground != null ? MaterialColor(region, region.Ground) : DefaultGround;
            for (int y = 0; y < region.Height; y++)
                for (int x = 0; x < region.Width; x++) paint[x, y] = ground;
            void Set(IEnumerable<(int x, int y)> tiles, Rgb c)
            {
                foreach ((int x, int y) in tiles)
                {
                    int tx = x - region.OriginX, ty = y - region.OriginY;
                    if (tx >= 0 && ty >= 0 && tx < region.Width && ty < region.Height) paint[tx, ty] = c;
                }
            }
            foreach (Piece p in region.Pieces)
            {
                if ((p.Plane ?? region.Plane) != region.Plane) continue;
                try
                {
                    switch (p.Kind)
                    {
                        case PieceKind.Patch when p.Rect != null:
                            Set(RegionCompiler.RectTiles(p.Rect), MaterialColor(region, p.Material));
                            break;
                        case PieceKind.House when p.Rect != null:
                            Set(RegionCompiler.RectTiles(p.Rect), MaterialColor(region, p.Floor));
                            break;
                        case PieceKind.Road when p.Line != null && p.Width.HasValue:
                        case PieceKind.Water when p.Rect == null && p.Line != null && p.Width.HasValue:
                            Set(TileShapes.Along(p.Line, p.Width!.Value, region.OriginX, region.OriginY, region.Width, region.Height), MaterialColor(region, p.Material));
                            break;
                        case PieceKind.Water when p.Rect != null:
                            Set(RegionCompiler.RectTiles(p.Rect), MaterialColor(region, p.Material));
                            break;
                        case PieceKind.Tiles:
                            foreach (var group in RegionCompiler.TilesOf(p).GroupBy(t => t.kind))
                                Set(group.Select(t => (t.x, t.y)), MaterialColor(region, group.Key.Material));
                            break;
                    }
                }
                catch (Exception)
                {
                    // A broken piece is reported by check; draw the rest.
                }
            }
            return paint;
        }

        private static bool IsWater(RegionCompiler.Result r, int tx, int ty)
        {
            int x = r.Region.OriginX + tx, y = r.Region.OriginY + ty;
            foreach (Piece p in r.Region.Pieces)
            {
                if (p.Kind == PieceKind.Tiles && p.Rect != null && p.Rows != null && p.Legend != null)
                {
                    int row = p.Rect[3] - 1 - y, col = x - p.Rect[0];
                    if (row >= 0 && row < p.Rows.Count && col >= 0 && col < p.Rows[row].Length &&
                        p.Legend.TryGetValue(p.Rows[row][col].ToString(), out TileKind? kind) && kind.Water == true) return true;
                }
                if (p.Kind != PieceKind.Water) continue;
                if (p.Rect != null && x >= p.Rect[0] && x < p.Rect[2] && y >= p.Rect[1] && y < p.Rect[3]) return true;
            }
            return false;
        }

        private static Rgb MaterialColor(RegionData region, string? name)
        {
            if (name == null || !region.Materials.TryGetValue(name, out string? value)) return DefaultGround;
            if (Palette.TryGetValue(value, out string? named)) value = named;
            string hex = value.TrimStart('#');
            if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                return new Rgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return DefaultGround;
        }

        private static void Fill(Rgb[] image, int w, int x0, int y0, int fw, int fh, Rgb c)
        {
            int h = image.Length / w;
            for (int y = Math.Max(0, y0); y < Math.Min(h, y0 + fh); y++)
                for (int x = Math.Max(0, x0); x < Math.Min(w, x0 + fw); x++) image[y * w + x] = c;
        }

        private static void Frame(Rgb[] image, int w, int x0, int y0, int size, Rgb c)
        {
            Fill(image, w, x0, y0, size, 1, c);
            Fill(image, w, x0, y0 + size - 1, size, 1, c);
            Fill(image, w, x0, y0, 1, size, c);
            Fill(image, w, x0 + size - 1, y0, 1, size, c);
        }

        private static void Blend(Rgb[] image, int w, int x, int y, Rgb c, float t, int h)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            image[y * w + x] = image[y * w + x].Mix(c, t);
        }

        private static byte[] Png(int w, int h, Rgb[] image)
        {
            var rgb = new byte[w * h * 3];
            for (int i = 0; i < image.Length; i++)
            {
                rgb[i * 3] = image[i].R;
                rgb[i * 3 + 1] = image[i].G;
                rgb[i * 3 + 2] = image[i].B;
            }
            return Png(w, h, rgb);
        }

        /// <summary>An RGB PNG from rows of pixels, top first, three bytes each.</summary>
        public static byte[] Png(int w, int h, byte[] rgb)
        {
            var raw = new byte[h * (w * 3 + 1)];
            for (int y = 0; y < h; y++)
                Buffer.BlockCopy(rgb, y * w * 3, raw, y * (w * 3 + 1) + 1, w * 3);
            using var png = new MemoryStream();
            png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var header = new byte[13];
            WriteBE(header, 0, (uint)w);
            WriteBE(header, 4, (uint)h);
            header[8] = 8; // bit depth
            header[9] = 2; // RGB
            Chunk(png, "IHDR", header);
            using (var z = new MemoryStream())
            {
                using (var zlib = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(raw);
                Chunk(png, "IDAT", z.ToArray());
            }
            Chunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            WriteBE(len, 0, (uint)data.Length);
            s.Write(len);
            byte[] typeBytes = Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes);
            s.Write(data);
            var crc = new byte[4];
            WriteBE(crc, 0, Crc32(typeBytes, data));
            s.Write(crc);
        }

        private static void WriteBE(byte[] b, int at, uint v)
        {
            b[at] = (byte)(v >> 24);
            b[at + 1] = (byte)(v >> 16);
            b[at + 2] = (byte)(v >> 8);
            b[at + 3] = (byte)v;
        }

        private static uint[]? _crcTable;

        private static uint Crc32(byte[] a, byte[] b)
        {
            if (_crcTable == null)
            {
                _crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    _crcTable[n] = c;
                }
            }
            uint crc = 0xFFFFFFFFu;
            foreach (byte x in a) crc = _crcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
            foreach (byte x in b) crc = _crcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }

        // ------------------------------------------------------------------ text

        /// <summary>
        /// The region as characters, north at the top, with x coordinates above every 8th column
        /// and y at the start of every row: " " no ground, "." ground, "#" blocked, "~" water,
        /// "T" tree, "r" rock, "o" object, "n" creature, "@" spawn. Walls aren't drawn; see the PNG.
        /// </summary>
        public static string Text(RegionCompiler.Result r)
        {
            RegionData region = r.Region;
            int plane = region.Plane;
            var cells = new char[region.Width, region.Height];
            for (int ty = 0; ty < region.Height; ty++)
                for (int tx = 0; tx < region.Width; tx++)
                {
                    int x = region.OriginX + tx, y = region.OriginY + ty;
                    cells[tx, ty] = r.Layers.IsBlockedLayer(plane, x, y) ? (IsWater(r, tx, ty) ? '~' : '#') : r.Layers.HasGround(plane, x, y) ? '.' : ' ';
                }
            void Mark(int x, int y, char c)
            {
                int tx = x - region.OriginX, ty = y - region.OriginY;
                if (tx >= 0 && ty >= 0 && tx < region.Width && ty < region.Height) cells[tx, ty] = c;
            }
            foreach (Piece p in region.Pieces.Where(p => p.At != null && (p.Plane ?? region.Plane) == plane))
                if (p.Kind == PieceKind.Tree) Mark(p.X, p.Y, 'T');
                else if (p.Kind == PieceKind.Rock) Mark(p.X, p.Y, 'r');
            foreach (ObjectEntry o in r.Objects.Where(o => o.Plane == plane)) Mark(o.X, o.Y, 'o');
            foreach (NpcEntry n in r.Npcs.Where(n => n.Plane == plane)) Mark(n.X, n.Y, 'n');
            if (r.HasSpawn) Mark(r.Map.Spawn.X, r.Map.Spawn.Y, '@');

            var sb = new StringBuilder();
            sb.AppendLine($"{region.Id} ({region.Title}): x {region.OriginX}..{region.OriginX + region.Width - 1}, y {region.OriginY}..{region.OriginY + region.Height - 1}, plane {plane}. North is up.");
            sb.AppendLine("' ' no ground  '.' ground  '#' blocked  '~' water  'T' tree  'r' rock  'o' object  'n' creature  '@' spawn");
            var ruler = new StringBuilder(new string(' ', 7));
            for (int tx = 0; tx < region.Width; tx += 8) ruler.Append((region.OriginX + tx).ToString(CultureInfo.InvariantCulture).PadRight(8));
            sb.AppendLine(ruler.ToString().TrimEnd());
            for (int ty = region.Height - 1; ty >= 0; ty--)
            {
                sb.Append((region.OriginY + ty).ToString(CultureInfo.InvariantCulture).PadLeft(6)).Append(' ');
                for (int tx = 0; tx < region.Width; tx++) sb.Append(cells[tx, ty]);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
