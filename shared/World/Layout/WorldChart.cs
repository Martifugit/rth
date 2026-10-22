using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Rth.Shared.World.Layout
{
    /// <summary>
    /// The continent as players see it on the world map (docs/interface.md, L3): a compact copy of
    /// the compiled layout (<see cref="ContinentMap"/>) with only what a map draws, and nothing a
    /// player shouldn't read. The server builds it from world/layout/ at startup and sends it in
    /// <c>ClientContent.Chart</c>; the client paints it (<see cref="ChartPainter"/>) and draws the
    /// lines, places and labels over it.
    ///
    /// The zone grids are run-length encoded (<see cref="Encode"/>): rows from the south, columns
    /// from the west, one symbol per 8 × 8 zone. Rivers, bridges and passes are lines drawn on
    /// top, so their zones carry the ground around them.
    /// </summary>
    public sealed class WorldChart
    {
        public const byte NoPower = ContinentMap.NoPower;

        public int OriginX, OriginY, ZoneTiles = 8, Cols, Rows;
        /// <summary>The region grid's size in tiles (96).</summary>
        public int RegionTiles = 96;
        public List<ChartGround> Grounds = new List<ChartGround>();
        public List<ChartPower> Powers = new List<ChartPower>();
        /// <summary>Each zone's ground (an index into <see cref="Grounds"/>), run-length encoded.</summary>
        public string Ground = "";
        /// <summary>Each zone's power (an index into <see cref="Powers"/>; <see cref="NoPower"/> at sea), run-length encoded.</summary>
        public string Power = "";
        public List<ChartLine> Rivers = new List<ChartLine>();
        /// <summary>Roads: kind "main", "road", "pass" (over the spine) or "ferry" (over water).</summary>
        public List<ChartLine> Roads = new List<ChartLine>();
        public List<ChartPlace> Places = new List<ChartPlace>();

        [JsonIgnore]
        public int WidthTiles => Cols * ZoneTiles;
        [JsonIgnore]
        public int HeightTiles => Rows * ZoneTiles;

        private byte[]? _ground, _power;

        [JsonIgnore]
        public byte[] GroundGrid => _ground ??= Decode(Ground, Cols * Rows);
        [JsonIgnore]
        public byte[] PowerGrid => _power ??= Decode(Power, Cols * Rows);

        /// <summary>The zone holding a tile, or -1 off the chart.</summary>
        public int ZoneIndex(float x, float y)
        {
            int col = (int)Math.Floor((x - OriginX) / ZoneTiles), row = (int)Math.Floor((y - OriginY) / ZoneTiles);
            return col < 0 || row < 0 || col >= Cols || row >= Rows ? -1 : row * Cols + col;
        }

        public ChartGround? GroundAt(float x, float y)
        {
            int i = ZoneIndex(x, y);
            return i < 0 || GroundGrid[i] >= Grounds.Count ? null : Grounds[GroundGrid[i]];
        }

        public ChartPower? PowerAt(float x, float y)
        {
            int i = ZoneIndex(x, y);
            return i < 0 || PowerGrid[i] >= Powers.Count ? null : Powers[PowerGrid[i]];
        }

        // ------------------------------------------------------------------ building

        /// <summary>The chart of a compiled layout. Placeholder names (p-...) never reach it.</summary>
        public static WorldChart From(ContinentMap map)
        {
            if (map.Count == 0) throw new ArgumentException("The layout didn't compile: " + string.Join("; ", map.Errors));
            ContinentData d = map.Data;
            var chart = new WorldChart
            {
                OriginX = map.OriginX,
                OriginY = map.OriginY,
                ZoneTiles = map.ZoneTiles,
                Cols = map.Cols,
                Rows = map.Rows,
                RegionTiles = d.RegionTiles,
            };
            for (int g = 0; g < map.GroundDefs.Count; g++)
            {
                GroundDef def = map.GroundDefs[g];
                chart.Grounds.Add(new ChartGround
                {
                    Id = map.GroundIds[g],
                    Name = def.Name,
                    Colour = string.IsNullOrEmpty(def.Map) ? def.Palette : def.Map,
                    Water = def.Water,
                    Walkable = def.Walkable && !def.Water,
                    Height = def.Height,
                    Rough = def.Rough,
                });
            }
            foreach (string id in map.PowerIds)
            {
                PowerDef p = d.Powers[id];
                chart.Powers.Add(new ChartPower { Id = id, Name = PlayerName(p.Name), Colour = p.Colour, Core = p.Core, Label = p.Label });
            }

            chart.Ground = Encode(SurfaceGround(map));
            chart.Power = Encode(map.Power);

            foreach (RiverDef r in d.Rivers)
                chart.Rivers.Add(new ChartLine { Id = r.Id, Kind = "river", Name = PlayerName(r.Name), Width = r.HalfWidth * 2f, Points = Flatten(r.Line.Select(p => (p[0], p[1]))) });
            foreach (RoadLine road in map.Roads)
            {
                if (road.Points.Count < 2) continue;
                float width = road.Kind == "pass" ? d.Spine.Passes.FirstOrDefault(p => p.Id == road.Id)?.HalfWidth * 2f ?? 8f : road.Kind == "main" ? 4f : 3f;
                string name = road.Kind == "pass" ? PlayerName(d.Spine.Passes.FirstOrDefault(p => p.Id == road.Id)?.Name ?? "") : "";
                chart.Roads.Add(new ChartLine { Id = road.Id, Kind = road.Kind, Name = name, Width = width, Points = Flatten(road.Points) });
            }
            foreach (PlaceDef p in d.Places)
            {
                if (p.Hidden) continue;
                bool placeholder = IsPlaceholder(p.Id) || IsPlaceholder(p.Name);
                // A landmark is only worth a mark with a name, and the unnamed ones include secrets
                // (the hidden occult cells): leave them off until they are named.
                if (!p.IsSettlement && placeholder) continue;
                chart.Places.Add(new ChartPlace
                {
                    Id = p.Id,
                    Name = IsPlaceholder(p.Name) ? "" : Capitalise(p.Name),
                    Tier = p.Tier,
                    X = p.At[0],
                    Y = p.At[1],
                    Capital = p.Capital,
                });
            }
            return chart;
        }

        /// <summary>A placeholder name from the layout ("p-lakeport"), which players never see.</summary>
        public static bool IsPlaceholder(string? name) =>
            string.IsNullOrWhiteSpace(name) || name!.TrimStart().StartsWith("p-", StringComparison.OrdinalIgnoreCase);

        /// <summary>A name as players read it: placeholders blank, a closing note in brackets dropped ("The cold north (unclaimed)").</summary>
        public static string PlayerName(string? name)
        {
            if (IsPlaceholder(name)) return "";
            return Capitalise(Regex.Replace(name!, @"\s*\([^)]*\)\s*$", ""));
        }

        private static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static float[] Flatten(IEnumerable<(float x, float y)> points)
        {
            var list = new List<float>();
            foreach ((float x, float y) in points)
            {
                list.Add((float)Math.Round(x, 1));
                list.Add((float)Math.Round(y, 1));
            }
            return list.ToArray();
        }

        /// <summary>
        /// The zone grid's grounds with river, bridge and pass zones given the nearest other land's
        /// ground: the map draws those as lines, a few tiles wide, not as 8-tile blocks.
        /// </summary>
        private static byte[] SurfaceGround(ContinentMap map)
        {
            ContinentData d = map.Data;
            var lines = new HashSet<int> { map.GroundIds.IndexOf(d.RiverGround), map.GroundIds.IndexOf(d.BridgeGround), map.GroundIds.IndexOf(d.PassGround) };
            byte[] ground = (byte[])map.Ground.Clone();
            var done = new bool[ground.Length];
            var queue = new Queue<int>();
            for (int i = 0; i < ground.Length; i++)
                if (!lines.Contains(ground[i])) { done[i] = true; if (!map.IsWater(i)) queue.Enqueue(i); }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int col = i % map.Cols, row = i / map.Cols;
                foreach ((int dc, int dr) in Neighbours)
                {
                    int c = col + dc, r = row + dr;
                    if (!map.InGrid(c, r)) continue;
                    int j = map.Index(c, r);
                    if (done[j]) continue;
                    done[j] = true;
                    ground[j] = ground[i];
                    queue.Enqueue(j);
                }
            }
            // Line zones with no land about them at all (a river through open water) keep the power's ground.
            for (int i = 0; i < ground.Length; i++)
                if (!done[i] && map.Power[i] != ContinentMap.NoPower)
                    ground[i] = (byte)map.GroundIds.IndexOf(d.Powers[map.PowerIds[map.Power[i]]].Ground);
            return ground;
        }

        private static readonly (int, int)[] Neighbours = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        // ------------------------------------------------------------------ run-length encoding

        /// <summary>The symbols, by value: 0-51 are letters; <see cref="NoPower"/> is '~'. Digits are counts.</summary>
        private const string Symbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        /// <summary>
        /// Runs of equal values as "[count]symbol": "A" is one zone of value 0, "12C" twelve of
        /// value 2. Values are 0-51, or 255 ('~').
        /// </summary>
        public static string Encode(byte[] values)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < values.Length;)
            {
                int run = 1;
                while (i + run < values.Length && values[i + run] == values[i]) run++;
                if (run > 1) sb.Append(run);
                sb.Append(Symbol(values[i]));
                i += run;
            }
            return sb.ToString();
        }

        public static byte[] Decode(string? text, int length)
        {
            var values = new byte[length];
            int at = 0, count = 0;
            foreach (char c in text ?? "")
            {
                if (c >= '0' && c <= '9')
                {
                    count = count * 10 + (c - '0');
                    if (count > length) throw new FormatException("The chart's grid is longer than its size.");
                    continue;
                }
                byte v = Value(c);
                int run = Math.Max(1, count);
                if (at + run > length) throw new FormatException("The chart's grid is longer than its size.");
                for (int k = 0; k < run; k++) values[at++] = v;
                count = 0;
            }
            if (at != length) throw new FormatException($"The chart's grid has {at} zones, not {length}.");
            return values;
        }

        private static char Symbol(byte v)
        {
            if (v == NoPower) return '~';
            if (v >= Symbols.Length) throw new ArgumentOutOfRangeException(nameof(v), "Chart values above 51 aren't supported.");
            return Symbols[v];
        }

        private static byte Value(char c)
        {
            if (c == '~') return NoPower;
            int v = Symbols.IndexOf(c);
            if (v < 0) throw new FormatException($"'{c}' isn't a chart symbol.");
            return (byte)v;
        }
    }

    public sealed class ChartGround
    {
        public string Id = "";
        public string Name = "";
        /// <summary>"#rrggbb", or a name in the art palette.</summary>
        public string Colour = "";
        public bool Water;
        public bool Walkable = true;
        /// <summary>Its macro height in tiles (the terrain's), for the map's hill shading.</summary>
        public float Height;
        /// <summary>How rough the ground is (the terrain's noise, tiles): the map's texture.</summary>
        public float Rough;
    }

    public sealed class ChartPower
    {
        public string Id = "";
        /// <summary>The name players read; empty for a placeholder.</summary>
        public string Name = "";
        public string Colour = "";
        /// <summary>False for frontiers beyond the first build.</summary>
        public bool Core = true;
        /// <summary>Where the map writes the name, [x, y] in tiles, if anywhere.</summary>
        public float[]? Label;
    }

    /// <summary>A river or road: points in tiles, flattened [x0, y0, x1, y1, ...].</summary>
    public sealed class ChartLine
    {
        public string Id = "";
        public string Kind = "";
        /// <summary>The name players read; empty for none or a placeholder.</summary>
        public string Name = "";
        /// <summary>Its width on the ground, in tiles.</summary>
        public float Width;
        public float[] Points = Array.Empty<float>();
    }

    public sealed class ChartPlace
    {
        public string Id = "";
        /// <summary>The name players read; empty for a place that has no name yet (drawn as an unnamed mark).</summary>
        public string Name = "";
        /// <summary>city, town, village, hamlet or landmark.</summary>
        public string Tier = "";
        public float X, Y;
        public bool Capital;
    }
}
