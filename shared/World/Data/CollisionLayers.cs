using System;
using System.Collections.Generic;
using System.Text;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// A region's collision before it is flattened into a <see cref="MapData"/>: per plane, which
    /// tiles have ground, which are blocked and which edges have walls. Kept apart because a tile
    /// is walkable only if something gives it ground and nothing blocks it, so the layers from the
    /// region's data and from its hand-made scene content can be merged before that rule is applied.
    /// </summary>
    public sealed class CollisionLayers : CollisionMap
    {
        private bool[] _ground;
        private bool[] _blocked;
        private CollisionFlags[] _walls;

        public int OriginX { get; }
        public int OriginY { get; }
        public int Width { get; }
        public int Height { get; }
        public int Planes { get; private set; }

        public CollisionLayers(int originX, int originY, int width, int height, int planes = 1)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Layers must have a positive size.");
            OriginX = originX;
            OriginY = originY;
            Width = width;
            Height = height;
            Planes = Math.Max(1, planes);
            _ground = new bool[Width * Height * Planes];
            _blocked = new bool[_ground.Length];
            _walls = new CollisionFlags[_ground.Length];
        }

        /// <summary>Adds planes (floors) up to <paramref name="planes"/>, keeping what is there.</summary>
        public void EnsurePlanes(int planes)
        {
            if (planes <= Planes) return;
            int size = Width * Height * planes;
            Array.Resize(ref _ground, size);
            Array.Resize(ref _blocked, size);
            Array.Resize(ref _walls, size);
            Planes = planes;
        }

        public override bool Contains(int plane, int x, int y) =>
            plane >= 0 && plane < Planes && x >= OriginX && x < OriginX + Width && y >= OriginY && y < OriginY + Height;

        private int Index(int plane, int x, int y) => (plane * Height + (y - OriginY)) * Width + (x - OriginX);

        public bool HasGround(int plane, int x, int y) => Contains(plane, x, y) && _ground[Index(plane, x, y)];
        public bool IsBlockedLayer(int plane, int x, int y) => Contains(plane, x, y) && _blocked[Index(plane, x, y)];
        public CollisionFlags WallsAt(int plane, int x, int y) => Contains(plane, x, y) ? _walls[Index(plane, x, y)] : CollisionFlags.None;

        public void SetGround(int plane, int x, int y)
        {
            if (Contains(plane, x, y)) _ground[Index(plane, x, y)] = true;
        }

        /// <summary>The flags the server will see: walls, and Blocked where there is no ground or something blocks.</summary>
        public override CollisionFlags GetFlags(int plane, int x, int y)
        {
            if (!Contains(plane, x, y)) return CollisionFlags.Blocked;
            int i = Index(plane, x, y);
            return _walls[i] | (!_ground[i] || _blocked[i] ? CollisionFlags.Blocked : CollisionFlags.None);
        }

        /// <summary>Blocked goes to the blocking layer and wall flags to the walls; ground is set with <see cref="SetGround"/>.</summary>
        public override void AddFlags(int plane, int x, int y, CollisionFlags flags)
        {
            if (!Contains(plane, x, y)) return;
            int i = Index(plane, x, y);
            if ((flags & CollisionFlags.Blocked) != 0) _blocked[i] = true;
            _walls[i] |= flags & ~CollisionFlags.Blocked;
        }

        /// <summary>Adds another region's layers (the same rectangle) on top of these.</summary>
        public void Merge(CollisionLayers other)
        {
            if (other.OriginX != OriginX || other.OriginY != OriginY || other.Width != Width || other.Height != Height)
                throw new ArgumentException("Layers cover a different rectangle.");
            EnsurePlanes(other.Planes);
            for (int i = 0; i < other._ground.Length; i++)
            {
                _ground[i] |= other._ground[i];
                _blocked[i] |= other._blocked[i];
                _walls[i] |= other._walls[i];
            }
        }

        public MapData ToMapData(int spawnPlane, TileCoord? spawn)
        {
            var map = new MapData(OriginX, OriginY, Width, Height, Planes);
            for (int p = 0; p < Planes; p++)
                for (int y = OriginY; y < OriginY + Height; y++)
                    for (int x = OriginX; x < OriginX + Width; x++)
                        map.SetFlags(p, x, y, GetFlags(p, x, y));
            if (spawn.HasValue)
            {
                map.SpawnPlane = spawnPlane;
                map.Spawn = spawn.Value;
            }
            return map;
        }

        // ------------------------------------------------------------------ text form

        /// <summary>A tile in <see cref="LayerText.Rows"/>: no ground.</summary>
        public const char VoidChar = ' ';
        public const char GroundChar = '.';
        public const char BlockedChar = '#';

        /// <summary>
        /// One plane as text, for people and agents to read: rows of characters from the north
        /// (" " no ground, "." ground, "#" blocked), and the walls as "x,y,N" or "x,y,E" edges
        /// (a south or west edge is the north or east edge of the tile beyond it).
        /// </summary>
        public LayerText ToText(int plane)
        {
            var text = new LayerText { Plane = plane };
            var row = new StringBuilder(Width);
            for (int y = OriginY + Height - 1; y >= OriginY; y--)
            {
                row.Clear();
                for (int x = OriginX; x < OriginX + Width; x++)
                {
                    int i = Index(plane, x, y);
                    row.Append(_blocked[i] ? BlockedChar : _ground[i] ? GroundChar : VoidChar);
                }
                text.Rows.Add(row.ToString().TrimEnd());
            }
            for (int y = OriginY; y < OriginY + Height; y++)
                for (int x = OriginX; x < OriginX + Width; x++)
                {
                    CollisionFlags w = _walls[Index(plane, x, y)];
                    if ((w & CollisionFlags.WallNorth) != 0) text.Walls.Add(Edge(x, y, 'N'));
                    if ((w & CollisionFlags.WallEast) != 0) text.Walls.Add(Edge(x, y, 'E'));
                    // Edges on the rectangle's border: the tile beyond isn't stored, so keep this side.
                    if ((w & CollisionFlags.WallSouth) != 0 && y == OriginY) text.Walls.Add(Edge(x, y - 1, 'N'));
                    if ((w & CollisionFlags.WallWest) != 0 && x == OriginX) text.Walls.Add(Edge(x - 1, y, 'E'));
                }
            text.Walls.Sort(StringComparer.Ordinal);
            return text;
        }

        private static string Edge(int x, int y, char side) => x + "," + y + "," + side;

        /// <summary>Reads one plane written by <see cref="ToText"/> back into these layers.</summary>
        public void AddText(LayerText text)
        {
            EnsurePlanes(text.Plane + 1);
            for (int r = 0; r < text.Rows.Count && r < Height; r++)
            {
                int y = OriginY + Height - 1 - r;
                string row = text.Rows[r];
                for (int c = 0; c < row.Length && c < Width; c++)
                {
                    int x = OriginX + c;
                    switch (row[c])
                    {
                        case GroundChar: SetGround(text.Plane, x, y); break;
                        case BlockedChar: SetGround(text.Plane, x, y); Block(text.Plane, x, y); break;
                        case VoidChar: break;
                        default: throw new FormatException($"Plane {text.Plane}, row {r} from the north: unknown tile '{row[c]}'.");
                    }
                }
            }
            foreach (string wall in text.Walls)
            {
                string[] p = wall.Split(',');
                if (p.Length != 3 || !int.TryParse(p[0], out int x) || !int.TryParse(p[1], out int y) || (p[2] != "N" && p[2] != "E"))
                    throw new FormatException($"Plane {text.Plane}: bad wall '{wall}' (use x,y,N or x,y,E).");
                AddWall(text.Plane, x, y, p[2] == "N" ? WallSide.North : WallSide.East);
            }
        }
    }

    /// <summary>One plane of <see cref="CollisionLayers"/> as text (see <see cref="CollisionLayers.ToText"/>).</summary>
    public sealed class LayerText
    {
        public int Plane;
        public List<string> Rows = new List<string>();
        public List<string> Walls = new List<string>();
    }
}
