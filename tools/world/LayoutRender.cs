using System.Globalization;
using Rth.Shared.World.Layout;

namespace Rth.WorldTool
{
    /// <summary>
    /// Draws the continent layout (docs/layout.md) so the owner can judge its scale: ground or
    /// power colours per zone, power borders, the 96-tile region grid, rivers with their flow,
    /// roads, passes, ferries, places with labels, the built regions and a scale bar.
    /// </summary>
    public static class LayoutRender
    {
        public sealed class Options
        {
            public int X0, Y0, X1, Y1;
            /// <summary>Pixels per 8-tile zone.</summary>
            public int ZonePx = 6;
            public bool Powers;
            public bool RegionKeys;
            public string Title = "";
        }

        public static Options Whole(ContinentMap map, int zonePx, bool powers, string title) => new Options
        {
            X0 = map.OriginX, Y0 = map.OriginY, X1 = map.OriginX + map.Cols * map.ZoneTiles, Y1 = map.OriginY + map.Rows * map.ZoneTiles,
            ZonePx = zonePx, Powers = powers, Title = title,
        };

        public static byte[] Draw(ContinentMap map, LayoutReport report, List<BuiltRegion> built, Options o)
        {
            int zt = map.ZoneTiles;
            float s = o.ZonePx / (float)zt; // pixels per tile
            int w = (int)Math.Round((o.X1 - o.X0) * s), h = (int)Math.Round((o.Y1 - o.Y0) * s);
            var c = new Canvas(w, h);
            float PX(float x) => (x - o.X0) * s;
            float PY(float y) => (o.Y1 - y) * s;

            // Ground or power, zone by zone.
            for (int row = 0; row < map.Rows; row++)
                for (int col = 0; col < map.Cols; col++)
                {
                    (float cx, float cy) = map.Centre(col, row);
                    if (cx < o.X0 || cx > o.X1 || cy < o.Y0 || cy > o.Y1) continue;
                    int i = map.Index(col, row);
                    Rgb colour = Hex(map.GroundAt(i).Map, new Rgb(128, 128, 128));
                    string? power = map.PowerIdAt(i);
                    if (o.Powers && power != null && !map.IsWater(i) && map.GroundAt(i).Walkable)
                        colour = Hex(map.Data.Powers[power].Colour, colour).Mix(colour, 0.25f);
                    if (power != null && !map.IsCore(i)) colour = colour.Mix(new Rgb(200, 204, 210), 0.45f);
                    int x0 = (int)PX(map.OriginX + col * zt), y0 = (int)PY(map.OriginY + (row + 1) * zt);
                    c.Fill(x0, y0, (int)PX(map.OriginX + (col + 1) * zt) - x0, (int)PY(map.OriginY + row * zt) - y0, colour);
                }

            // Power borders.
            var border = new Rgb(30, 10, 12);
            for (int row = 0; row < map.Rows; row++)
                for (int col = 0; col < map.Cols; col++)
                {
                    int i = map.Index(col, row);
                    if (map.Power[i] == ContinentMap.NoPower) continue;
                    int xl = (int)PX(map.OriginX + col * zt), yt = (int)PY(map.OriginY + (row + 1) * zt);
                    if (col + 1 < map.Cols)
                    {
                        int j = map.Index(col + 1, row);
                        if (map.Power[j] != ContinentMap.NoPower && map.Power[j] != map.Power[i])
                            c.Fill((int)PX(map.OriginX + (col + 1) * zt) - 1, yt, 2, o.ZonePx, border);
                    }
                    if (row + 1 < map.Rows)
                    {
                        int j = map.Index(col, row + 1);
                        if (map.Power[j] != ContinentMap.NoPower && map.Power[j] != map.Power[i])
                            c.Fill(xl, yt - 1, o.ZonePx, 2, border);
                    }
                }

            // The region grid.
            int rt = map.Data.RegionTiles;
            for (int x = FloorTo(o.X0, rt); x <= o.X1; x += rt) c.VLine((int)PX(x), 0, h, new Rgb(0, 0, 0), 0.3f);
            for (int y = FloorTo(o.Y0, rt); y <= o.Y1; y += rt) c.HLine(0, w, (int)PY(y), new Rgb(0, 0, 0), 0.3f);
            if (o.RegionKeys)
                for (int y = FloorTo(o.Y0, rt); y < o.Y1; y += rt)
                    for (int x = FloorTo(o.X0, rt); x < o.X1; x += rt)
                        c.Text((int)PX(x) + 3, (int)PY(y + rt) + 3, $"{x / rt},{y / rt}", 1, new Rgb(20, 20, 20), new Rgb(255, 255, 255), 0.0f);
            else
            {
                for (int x = FloorTo(o.X0, rt); x < o.X1; x += rt * 2) c.Text((int)PX(x) + 2, 16, (x / rt).ToString(CultureInfo.InvariantCulture), 1, new Rgb(255, 255, 255), new Rgb(0, 0, 0));
                for (int y = FloorTo(o.Y0, rt); y < o.Y1; y += rt * 2) c.Text(2, (int)PY(y + rt) + 2, (y / rt).ToString(CultureInfo.InvariantCulture), 1, new Rgb(255, 255, 255), new Rgb(0, 0, 0));
            }

            // Rivers, with chevrons pointing downstream.
            var riverColour = new Rgb(150, 200, 255);
            foreach (RiverDef r in map.Data.Rivers)
            {
                var pts = r.Line.Select(p => (PX(p[0]), PY(p[1]))).ToList();
                c.Polyline(pts, Math.Max(1, (int)(r.HalfWidth * 2 * s)), riverColour, 0);
                c.Chevrons(pts, 70, riverColour);
            }

            // Roads, passes and ferries.
            foreach (RoadLine road in map.Roads)
            {
                var pts = road.Points.Select(p => (PX(p.x), PY(p.y))).ToList();
                switch (road.Kind)
                {
                    case "main": c.Polyline(pts, 3, new Rgb(250, 228, 170), 0); break;
                    case "pass": c.Polyline(pts, 3, new Rgb(255, 255, 255), 6); break;
                    case "ferry": c.Polyline(pts, 2, new Rgb(230, 240, 255), 3); break;
                    default: c.Polyline(pts, 1, new Rgb(235, 215, 160), 0); break;
                }
            }

            // Built regions.
            foreach (BuiltRegion b in built)
                c.Frame((int)PX(b.X0), (int)PY(b.Y1), (int)((b.X1 - b.X0) * s), (int)((b.Y1 - b.Y0) * s), new Rgb(255, 60, 220), 2);

            // Power names where the data puts them.
            foreach (var p in map.Data.Powers)
            {
                if (p.Value.Label == null) continue;
                float lx = p.Value.Label[0], ly = p.Value.Label[1];
                if (lx < o.X0 || lx > o.X1 || ly < o.Y0 || ly > o.Y1) continue;
                c.TextCentred((int)PX(lx), (int)PY(ly), p.Value.Name.ToUpperInvariant(), o.ZonePx >= 12 ? 4 : o.ZonePx >= 6 ? 3 : 2, new Rgb(255, 245, 235), new Rgb(0, 0, 0));
            }

            // Places.
            foreach (PlaceDef p in map.Data.Places)
            {
                float x = PX(p.At[0]), y = PY(p.At[1]);
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                int size = p.Tier switch { "city" => 9, "town" => 7, "village" => 5, "hamlet" => 3, _ => 5 };
                int scale = p.Tier switch { "city" => o.ZonePx >= 6 ? 3 : 2, "town" => o.ZonePx >= 6 ? 2 : 1, "village" => o.ZonePx >= 12 ? 2 : 1, _ => 1 };
                var fill = p.Tier == "landmark" ? new Rgb(255, 225, 110) : new Rgb(250, 242, 225);
                if (p.Tier == "landmark") c.Diamond((int)x, (int)y, 5, fill);
                else
                {
                    c.Fill((int)x - size / 2 - 1, (int)y - size / 2 - 1, size + 2, size + 2, new Rgb(0, 0, 0));
                    c.Fill((int)x - size / 2, (int)y - size / 2, size, size, fill);
                }
                if (p.Tier == "hamlet" && o.ZonePx < 12) continue;
                string name = p.Name.StartsWith("a ", StringComparison.Ordinal) && o.ZonePx < 12 ? "" : p.Name;
                if (name.Length > 0) c.Text((int)x + size / 2 + 3, (int)y - 4 * scale, name.ToUpperInvariant(), scale, new Rgb(255, 255, 255), new Rgb(0, 0, 0));
            }

            // Title, scale bar and legend.
            c.Text(10, h - 40 - 14 * 3, o.Title.ToUpperInvariant(), 2, new Rgb(255, 255, 255), new Rgb(0, 0, 0));
            int bar = (int)(500 * s), bx = 10, by = h - 30;
            c.Fill(bx - 2, by - 2, bar + 4, 10, new Rgb(0, 0, 0));
            for (int k = 0; k < 5; k++) c.Fill(bx + (int)(k * 100 * s), by, (int)(100 * s), 6, k % 2 == 0 ? new Rgb(255, 255, 255) : new Rgb(120, 120, 120));
            c.Text(bx, by + 12, "500 TILES: 5 MIN WALKING, 2.5 MIN RUNNING. GRID: 96-TILE REGIONS", 1, new Rgb(255, 255, 255), new Rgb(0, 0, 0));
            Legend(c, map, o, w, h);
            return c.Png();
        }

        private static void Legend(Canvas c, ContinentMap map, Options o, int w, int h)
        {
            var rows = new List<(Rgb colour, string text)>();
            if (o.Powers)
                foreach (var p in map.Data.Powers) rows.Add((Hex(p.Value.Colour, new Rgb(0, 0, 0)), p.Value.Name));
            else
                foreach (var g in map.Data.Grounds) rows.Add((Hex(g.Value.Map, new Rgb(0, 0, 0)), g.Value.Name + (g.Value.Walkable && !g.Value.Water ? "" : " (blocks)")));
            rows.Add((new Rgb(250, 228, 170), "main road"));
            rows.Add((new Rgb(255, 255, 255), "pass (dashed), ferry (dotted)"));
            rows.Add((new Rgb(255, 60, 220), "built region"));
            int lw = 250, lh = rows.Count * 12 + 10;
            int x0 = w - lw - 8, y0 = h - lh - 8;
            c.Fill(x0, y0, lw, lh, new Rgb(16, 18, 24));
            for (int k = 0; k < rows.Count; k++)
            {
                c.Fill(x0 + 6, y0 + 6 + k * 12, 14, 8, rows[k].colour);
                c.Text(x0 + 26, y0 + 6 + k * 12, rows[k].text.ToUpperInvariant(), 1, new Rgb(230, 230, 230), null);
            }
        }

        /// <summary>
        /// Rivers and roads stamped roughly onto a painted chart (RGBA, rows from the south), for
        /// the chart preview: the game draws them as vectors.
        /// </summary>
        public static void StrokeChartLines(WorldChart chart, byte[] rgba, int w, int h)
        {
            void Stamp(float x, float y, float radius, Rgb c, float alpha)
            {
                int r = (int)Math.Ceiling(radius);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx * dx + dy * dy > radius * radius) continue;
                        int px = (int)(x - chart.OriginX) + dx, py = (int)(y - chart.OriginY) + dy;
                        if (px < 0 || py < 0 || px >= w || py >= h) continue;
                        int o = (py * w + px) * 4;
                        rgba[o] = (byte)(rgba[o] + (c.R - rgba[o]) * alpha);
                        rgba[o + 1] = (byte)(rgba[o + 1] + (c.G - rgba[o + 1]) * alpha);
                        rgba[o + 2] = (byte)(rgba[o + 2] + (c.B - rgba[o + 2]) * alpha);
                    }
            }
            void Line(ChartLine l, float radius, Rgb c, float alpha, int dash)
            {
                float walked = 0;
                for (int k = 0; k + 3 < l.Points.Length; k += 2)
                {
                    float x0 = l.Points[k], y0 = l.Points[k + 1], x1 = l.Points[k + 2], y1 = l.Points[k + 3];
                    float len = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                    for (float t = 0; t <= len; t += 1f, walked += 1f)
                        if (dash == 0 || (int)(walked / dash) % 2 == 0)
                            Stamp(x0 + (x1 - x0) * t / Math.Max(1f, len), y0 + (y1 - y0) * t / Math.Max(1f, len), radius, c, alpha);
                }
            }
            foreach (ChartLine r in chart.Rivers) Line(r, Math.Max(1.2f, r.Width / 2f), new Rgb(66, 118, 170), 1f, 0);
            foreach (ChartLine r in chart.Roads)
            {
                if (r.Kind == "ferry") Line(r, 1f, new Rgb(230, 220, 190), 0.8f, 4);
                else if (r.Kind == "pass") Line(r, 1.5f, new Rgb(70, 50, 30), 0.9f, 6);
                else Line(r, r.Kind == "main" ? 1.6f : 1.1f, new Rgb(92, 66, 40), 0.85f, 0);
            }
            foreach (ChartPlace p in chart.Places)
                Stamp(p.X, p.Y, p.Tier == "city" ? 6 : p.Tier == "town" ? 4 : 2.5f, string.IsNullOrEmpty(p.Name) ? new Rgb(120, 100, 70) : new Rgb(240, 213, 140), 1f);
        }

        private static int FloorTo(int v, int step) => (int)Math.Floor(v / (double)step) * step;

        private static Rgb Hex(string hex, Rgb fallback)
        {
            hex = (hex ?? "").TrimStart('#');
            return hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)
                ? new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v) : fallback;
        }

        public readonly record struct Rgb(byte R, byte G, byte B)
        {
            public Rgb Mix(Rgb o, float t) => new((byte)(R + (o.R - R) * t), (byte)(G + (o.G - G) * t), (byte)(B + (o.B - B) * t));
        }

        private sealed class Canvas
        {
            private readonly int _w, _h;
            private readonly Rgb[] _px;

            public Canvas(int w, int h)
            {
                _w = w; _h = h;
                _px = new Rgb[w * h];
                Array.Fill(_px, new Rgb(20, 24, 30));
            }

            public void Set(int x, int y, Rgb c) { if (x >= 0 && y >= 0 && x < _w && y < _h) _px[y * _w + x] = c; }
            private void Blend(int x, int y, Rgb c, float t) { if (x >= 0 && y >= 0 && x < _w && y < _h) _px[y * _w + x] = _px[y * _w + x].Mix(c, t); }

            public void Fill(int x0, int y0, int fw, int fh, Rgb c)
            {
                for (int y = Math.Max(0, y0); y < Math.Min(_h, y0 + fh); y++)
                    for (int x = Math.Max(0, x0); x < Math.Min(_w, x0 + fw); x++) _px[y * _w + x] = c;
            }

            public void HLine(int x0, int x1, int y, Rgb c, float t) { for (int x = x0; x < x1; x++) Blend(x, y, c, t); }
            public void VLine(int x, int y0, int y1, Rgb c, float t) { for (int y = y0; y < y1; y++) Blend(x, y, c, t); }

            public void Frame(int x0, int y0, int fw, int fh, Rgb c, int t)
            {
                Fill(x0, y0, fw, t, c); Fill(x0, y0 + fh - t, fw, t, c);
                Fill(x0, y0, t, fh, c); Fill(x0 + fw - t, y0, t, fh, c);
            }

            public void Diamond(int x, int y, int r, Rgb c)
            {
                for (int dy = -r - 1; dy <= r + 1; dy++)
                    for (int dx = -r - 1; dx <= r + 1; dx++)
                    {
                        int d = Math.Abs(dx) + Math.Abs(dy);
                        if (d <= r) Set(x + dx, y + dy, c);
                        else if (d == r + 1) Set(x + dx, y + dy, new Rgb(0, 0, 0));
                    }
            }

            /// <summary>A line of thickness <paramref name="t"/>; with <paramref name="dash"/> > 0, drawn in dashes that long.</summary>
            public void Polyline(List<(float x, float y)> pts, int t, Rgb c, int dash)
            {
                float run = 0;
                for (int k = 0; k + 1 < pts.Count; k++)
                {
                    var (x1, y1) = pts[k];
                    var (x2, y2) = pts[k + 1];
                    float len = MathF.Max(MathF.Abs(x2 - x1), MathF.Abs(y2 - y1));
                    int steps = Math.Max(1, (int)MathF.Ceiling(len));
                    for (int i = 0; i <= steps; i++)
                    {
                        float f = i / (float)steps;
                        if (dash > 0 && ((int)((run + f * len) / dash) % 2) == 1) continue;
                        int x = (int)(x1 + (x2 - x1) * f), y = (int)(y1 + (y2 - y1) * f);
                        Fill(x - t / 2, y - t / 2, t, t, c);
                    }
                    run += len;
                }
            }

            /// <summary>Small arrowheads every <paramref name="every"/> pixels along a line, pointing along it.</summary>
            public void Chevrons(List<(float x, float y)> pts, float every, Rgb c)
            {
                float next = every / 2, run = 0;
                for (int k = 0; k + 1 < pts.Count; k++)
                {
                    var (x1, y1) = pts[k];
                    var (x2, y2) = pts[k + 1];
                    float len = MathF.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                    if (len <= 0) continue;
                    float ux = (x2 - x1) / len, uy = (y2 - y1) / len;
                    while (next <= run + len)
                    {
                        float f = next - run;
                        float x = x1 + ux * f, y = y1 + uy * f;
                        for (int side = -1; side <= 1; side += 2)
                            for (int d = 0; d <= 5; d++)
                                Set((int)(x - ux * d + uy * d * side * 0.8f), (int)(y - uy * d - ux * d * side * 0.8f), c);
                        next += every;
                    }
                    run += len;
                }
            }

            public void Text(int x, int y, string text, int scale, Rgb c, Rgb? outline, float shadow = 1f)
            {
                if (outline.HasValue)
                    foreach (var (ox, oy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, 1), (-1, 1), (1, -1) })
                        Glyphs(x + ox, y + oy, text, scale, outline.Value);
                Glyphs(x, y, text, scale, c);
            }

            public void TextCentred(int x, int y, string text, int scale, Rgb c, Rgb? outline) =>
                Text(x - text.Length * 6 * scale / 2, y - 7 * scale / 2, text, scale, c, outline);

            private void Glyphs(int x, int y, string text, int scale, Rgb c)
            {
                foreach (char ch in text)
                {
                    string[] g = Font.Glyph(ch);
                    for (int r = 0; r < 7; r++)
                        for (int k = 0; k < 5; k++)
                            if (g[r][k] == '#') Fill(x + k * scale, y + r * scale, scale, scale, c);
                    x += 6 * scale;
                }
            }

            public byte[] Png()
            {
                var rgb = new byte[_w * _h * 3];
                for (int i = 0; i < _px.Length; i++) { rgb[i * 3] = _px[i].R; rgb[i * 3 + 1] = _px[i].G; rgb[i * 3 + 2] = _px[i].B; }
                return MapRender.Png(_w, _h, rgb);
            }
        }

        /// <summary>A 5 × 7 pixel font: capitals, digits and a little punctuation.</summary>
        private static class Font
        {
            private static readonly Dictionary<char, string[]> Glyphs = Build();

            public static string[] Glyph(char c) =>
                Glyphs.TryGetValue(char.ToUpperInvariant(c), out string[]? g) ? g : Glyphs['?'];

            private static Dictionary<char, string[]> Build()
            {
                const string src = @"
A .###. #...# #...# ##### #...# #...# #...#
B ####. #...# #...# ####. #...# #...# ####.
C .###. #...# #.... #.... #.... #...# .###.
D ####. #...# #...# #...# #...# #...# ####.
E ##### #.... #.... ####. #.... #.... #####
F ##### #.... #.... ####. #.... #.... #....
G .###. #...# #.... #.### #...# #...# .####
H #...# #...# #...# ##### #...# #...# #...#
I .###. ..#.. ..#.. ..#.. ..#.. ..#.. .###.
J ..### ...#. ...#. ...#. #..#. #..#. .##..
K #...# #..#. #.#.. ##... #.#.. #..#. #...#
L #.... #.... #.... #.... #.... #.... #####
M #...# ##.## #.#.# #.#.# #...# #...# #...#
N #...# ##..# #.#.# #..## #...# #...# #...#
O .###. #...# #...# #...# #...# #...# .###.
P ####. #...# #...# ####. #.... #.... #....
Q .###. #...# #...# #...# #.#.# #..#. .##.#
R ####. #...# #...# ####. #.#.. #..#. #...#
S .#### #.... #.... .###. ....# ....# ####.
T ##### ..#.. ..#.. ..#.. ..#.. ..#.. ..#..
U #...# #...# #...# #...# #...# #...# .###.
V #...# #...# #...# #...# #...# .#.#. ..#..
W #...# #...# #...# #.#.# #.#.# #.#.# .#.#.
X #...# #...# .#.#. ..#.. .#.#. #...# #...#
Y #...# #...# .#.#. ..#.. ..#.. ..#.. ..#..
Z ##### ....# ...#. ..#.. .#... #.... #####
0 .###. #...# #..## #.#.# ##..# #...# .###.
1 ..#.. .##.. ..#.. ..#.. ..#.. ..#.. .###.
2 .###. #...# ....# ...#. ..#.. .#... #####
3 ####. ....# ....# .###. ....# ....# ####.
4 ...#. ..##. .#.#. #..#. ##### ...#. ...#.
5 ##### #.... ####. ....# ....# #...# .###.
6 ..##. .#... #.... ####. #...# #...# .###.
7 ##### ....# ...#. ..#.. .#... .#... .#...
8 .###. #...# #...# .###. #...# #...# .###.
9 .###. #...# #...# .#### ....# ...#. .##..
- ..... ..... ..... .###. ..... ..... .....
' ..#.. ..#.. .#... ..... ..... ..... .....
. ..... ..... ..... ..... ..... .##.. .##..
, ..... ..... ..... ..... .##.. ..#.. .#...
( ...#. ..#.. .#... .#... .#... ..#.. ...#.
) .#... ..#.. ...#. ...#. ...#. ..#.. .#...
: ..... .##.. .##.. ..... .##.. .##.. .....
/ ....# ...#. ...#. ..#.. .#... .#... #....
& .##.. #..#. #.#.. .#... #.#.# #..#. .##.#
+ ..... ..#.. ..#.. ##### ..#.. ..#.. .....
= ..... ..... ##### ..... ##### ..... .....
% ##..# ##..# ...#. ..#.. .#... #..## #..##
? .###. #...# ....# ...#. ..#.. ..... ..#..
";
                var map = new Dictionary<char, string[]>();
                foreach (string line in src.Split('\n'))
                {
                    string l = line.TrimEnd('\r');
                    if (l.Length < 3) continue;
                    map[l[0]] = l.Substring(2).Split(' ');
                }
                map[' '] = Enumerable.Repeat(".....", 7).ToArray();
                return map;
            }
        }
    }
}
