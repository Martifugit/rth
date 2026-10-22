using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Rth.Shared.World
{
    /// <summary>
    /// The world's large-scale shape (docs/world-building.md): for every 8 × 8 tile zone a biome
    /// and a macro height, as rows of characters (client/Assets/World/layout.json, north first).
    /// A region's ground is baked from it: the macro height (smooth between zone centres), plus the
    /// biome's base noise, plus the region's own <see cref="TerrainStamp"/>s (the hand-made
    /// detail: a flattened town, a raised hill, a dug pond). Because every region samples the same
    /// functions of absolute position, neighbouring regions meet exactly at their borders, and
    /// re-baking after the layout changes keeps every stamp. Presentation and tooling only.
    /// </summary>
    public sealed class WorldLayout
    {
        public const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

        public int ZoneTiles = 8;
        /// <summary>Absolute tile of the south-west corner of the zone grid.</summary>
        public int OriginX;
        public int OriginY;
        public float SeaLevel;
        /// <summary>The base noise's seed: one for the whole world, so regions meet.</summary>
        public int Seed = 1;
        public Dictionary<string, BiomeDef> Biomes = new Dictionary<string, BiomeDef>();
        /// <summary>One biome letter per zone, rows from north to south.</summary>
        public List<string> Zones = new List<string>();
        /// <summary>One base-36 digit per zone (half tiles: 0-z is 0-17.5), rows from north to south.</summary>
        public List<string> Heights = new List<string>();

        public int Columns => Zones.Count == 0 ? 0 : Zones[0].Length;
        public int Rows => Zones.Count;

        public static WorldLayout Parse(string json)
        {
            WorldLayout layout = JsonConvert.DeserializeObject<WorldLayout>(json) ?? new WorldLayout();
            layout.Validate();
            return layout;
        }

        private void Validate()
        {
            if (ZoneTiles <= 0) throw new FormatException("zoneTiles must be positive.");
            if (Heights.Count != Zones.Count) throw new FormatException($"{Zones.Count} zone rows but {Heights.Count} height rows.");
            for (int r = 0; r < Zones.Count; r++)
            {
                if (Zones[r].Length != Columns || Heights[r].Length != Columns)
                    throw new FormatException($"Row {r} (from the north) is not {Columns} zones long.");
                foreach (char c in Zones[r])
                    if (!Biomes.ContainsKey(c.ToString())) throw new FormatException($"Row {r} uses biome '{c}', which isn't defined.");
                foreach (char c in Heights[r])
                    if (Digits.IndexOf(c) < 0) throw new FormatException($"Row {r} has height '{c}'; use 0-9 and a-z.");
            }
        }

        /// <summary>The zone (column, row from the south) holding a tile, clamped to the grid.</summary>
        private (int col, int row) ZoneOf(int x, int y) =>
            (Math.Clamp(FloorDiv(x - OriginX, ZoneTiles), 0, Columns - 1), Math.Clamp(FloorDiv(y - OriginY, ZoneTiles), 0, Rows - 1));

        private static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);

        private char ZoneLetter(int col, int row) => Zones[Rows - 1 - row][col];

        private float ZoneHeight(int col, int row)
        {
            col = Math.Clamp(col, 0, Columns - 1);
            row = Math.Clamp(row, 0, Rows - 1);
            return Digits.IndexOf(Heights[Rows - 1 - row][col]) * 0.5f;
        }

        /// <summary>The biome under a tile; tiles off the grid are the first water biome's (the open sea).</summary>
        public BiomeDef BiomeAt(int x, int y)
        {
            (int col, int row) = ZoneOf(x, y);
            return Biomes[ZoneLetter(col, row).ToString()];
        }

        /// <summary>The macro height at a point: bilinear between zone centres.</summary>
        public float MacroHeight(float x, float y)
        {
            float gx = (x - OriginX) / ZoneTiles - 0.5f;
            float gy = (y - OriginY) / ZoneTiles - 0.5f;
            int c0 = (int)Math.Floor(gx), r0 = (int)Math.Floor(gy);
            float u = gx - c0, v = gy - r0;
            float a = ZoneHeight(c0, r0), b = ZoneHeight(c0 + 1, r0), c = ZoneHeight(c0, r0 + 1), d = ZoneHeight(c0 + 1, r0 + 1);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        /// <summary>
        /// The ground for a rectangle of tiles: macro height, the biome's base noise (none on
        /// water, which sits at sea level), then the stamps in order.
        /// </summary>
        public Heightfield Bake(int originX, int originY, int width, int depth, int seed, IEnumerable<TerrainStamp>? stamps = null)
        {
            var field = new Heightfield(originX, originY, width, depth);
            field.Fill((x, y) => BaseHeight(x, y, seed));
            if (stamps != null)
                foreach (TerrainStamp s in stamps) s.Apply(field);
            return field;
        }

        /// <summary>The height of a tile corner before any stamps.</summary>
        public float BaseHeight(int x, int y, int seed)
        {
            // A corner touches four tiles; it is water if any of them is, so shores meet the sea.
            float rough = float.MaxValue;
            for (int dy = -1; dy <= 0; dy++)
                for (int dx = -1; dx <= 0; dx++)
                {
                    BiomeDef b = BiomeAt(x + dx, y + dy);
                    if (b.Water) return SeaLevel;
                    rough = Math.Min(rough, b.Rough);
                }
            float noise = (Heightfield.Noise(x, y, seed) - 0.6f) / (3.75f - 0.6f); // 0..1
            return SeaLevel + MacroHeight(x, y) + rough * noise;
        }

        /// <summary>Each tile's ground colour (a palette name) from its biome, row by row from the south.</summary>
        public string[] GroundColours(int originX, int originY, int width, int depth)
        {
            var colours = new string[width * depth];
            for (int j = 0; j < depth; j++)
                for (int i = 0; i < width; i++)
                    colours[j * width + i] = BiomeAt(originX + i, originY + j).Ground;
            return colours;
        }
    }

    public sealed class BiomeDef
    {
        public string Name = "";
        /// <summary>Ground colour: a palette name (client/Assets/Resources/Art/palette.json).</summary>
        public string Ground = "Grass";
        public bool Water;
        /// <summary>False for land that is drawn but not walked (the mountain wall, the frozen north).</summary>
        public bool Walkable = true;
        /// <summary>Base noise amplitude in tiles.</summary>
        public float Rough = 1f;
    }

    public enum StampMode
    {
        /// <summary>Set the ground to <see cref="TerrainStamp.Height"/>.</summary>
        Set,
        /// <summary>Raise (or with a negative height, lower) the ground by <see cref="TerrainStamp.Height"/>.</summary>
        Add,
        /// <summary>Flatten to the average of the ground under the stamp, plus <see cref="TerrainStamp.Height"/>.</summary>
        Level,
    }

    /// <summary>
    /// A hand-made change to baked ground: a rectangle of tile corners, or with <see cref="Round"/>
    /// the ellipse inside it, whose effect fades to nothing over <see cref="Falloff"/> tiles outside.
    /// Stamps are what a region keeps when the layout under it changes.
    /// </summary>
    public sealed class TerrainStamp
    {
        public StampMode Mode = StampMode.Level;
        public int X, Y, Width, Depth;
        public float Height;
        public float Falloff = 3f;
        public bool Round;

        /// <summary>1 inside the shape, falling smoothly to 0 at <see cref="Falloff"/> tiles outside it.</summary>
        public float Weight(float x, float y)
        {
            float outside;
            if (Round)
            {
                float rx = Width / 2f, ry = Depth / 2f;
                float dx = (x - (X + rx)) / Math.Max(rx, 0.01f), dy = (y - (Y + ry)) / Math.Max(ry, 0.01f);
                float r = (float)Math.Sqrt(dx * dx + dy * dy);
                outside = Math.Max(0f, r - 1f) * Math.Min(rx, ry);
            }
            else
            {
                float ox = Math.Max(Math.Max(X - x, x - (X + Width)), 0f);
                float oy = Math.Max(Math.Max(Y - y, y - (Y + Depth)), 0f);
                outside = (float)Math.Sqrt(ox * ox + oy * oy);
            }
            if (outside <= 0f) return 1f;
            if (Falloff <= 0f || outside >= Falloff) return 0f;
            float t = 1f - outside / Falloff;
            return t * t * (3f - 2f * t);
        }

        public void Apply(Heightfield field)
        {
            float level = 0f;
            if (Mode == StampMode.Level)
            {
                int n = 0;
                for (int y = Y; y <= Y + Depth; y++)
                    for (int x = X; x <= X + Width; x++)
                    {
                        level += field.Corner(x, y);
                        n++;
                    }
                level = level / Math.Max(1, n) + Height;
            }
            int reach = (int)Math.Ceiling(Falloff);
            for (int y = Y - reach; y <= Y + Depth + reach; y++)
                for (int x = X - reach; x <= X + Width + reach; x++)
                {
                    if (x < field.OriginX || x > field.OriginX + field.Width || y < field.OriginY || y > field.OriginY + field.Depth) continue;
                    float w = Weight(x, y);
                    if (w <= 0f) continue;
                    float h = field.Corner(x, y);
                    float target = Mode == StampMode.Add ? h + Height : Mode == StampMode.Set ? Height : level;
                    field.SetCorner(x, y, h + (target - h) * w);
                }
        }
    }
}
