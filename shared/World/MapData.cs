using System;
using System.IO;

namespace Rth.Shared.World
{
    /// <summary>
    /// Collision data for a rectangular area of the world, on one or more planes (floors).
    /// Written by the Unity map exporter, read by the server. Tiles outside the area are blocked.
    /// </summary>
    public sealed class MapData
    {
        /// <summary>"RTHM" in little-endian byte order.</summary>
        public const uint Magic = 0x4D485452;
        public const int FormatVersion = 1;

        private readonly CollisionFlags[] _flags;

        public int OriginX { get; }
        public int OriginY { get; }
        public int Width { get; }
        public int Height { get; }
        public int Planes { get; }

        /// <summary>Where new players appear.</summary>
        public int SpawnPlane { get; set; }
        public TileCoord Spawn { get; set; }

        public MapData(int originX, int originY, int width, int height, int planes = 1)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Map must have a positive size.");
            if (planes <= 0) throw new ArgumentException("Map must have at least one plane.");
            OriginX = originX;
            OriginY = originY;
            Width = width;
            Height = height;
            Planes = planes;
            Spawn = new TileCoord(originX + width / 2, originY + height / 2);
            _flags = new CollisionFlags[width * height * planes];
        }

        public bool Contains(int plane, int x, int y) =>
            plane >= 0 && plane < Planes &&
            x >= OriginX && x < OriginX + Width &&
            y >= OriginY && y < OriginY + Height;

        private int Index(int plane, int x, int y) => (plane * Height + (y - OriginY)) * Width + (x - OriginX);

        public CollisionFlags GetFlags(int plane, int x, int y) =>
            Contains(plane, x, y) ? _flags[Index(plane, x, y)] : CollisionFlags.Blocked;

        public void SetFlags(int plane, int x, int y, CollisionFlags flags)
        {
            if (!Contains(plane, x, y)) throw new ArgumentOutOfRangeException(nameof(x), "Tile is outside the map.");
            _flags[Index(plane, x, y)] = flags;
        }

        public void AddFlags(int plane, int x, int y, CollisionFlags flags)
        {
            if (Contains(plane, x, y)) _flags[Index(plane, x, y)] |= flags;
        }

        public void Block(int plane, int x, int y) => AddFlags(plane, x, y, CollisionFlags.Blocked);

        public bool IsBlocked(int plane, int x, int y) => (GetFlags(plane, x, y) & CollisionFlags.Blocked) != 0;

        /// <summary>Adds a wall on one edge of a tile, and the matching flag on the neighbour across it.</summary>
        public void AddWall(int plane, int x, int y, WallSide side)
        {
            switch (side)
            {
                case WallSide.North:
                    AddFlags(plane, x, y, CollisionFlags.WallNorth);
                    AddFlags(plane, x, y + 1, CollisionFlags.WallSouth);
                    break;
                case WallSide.East:
                    AddFlags(plane, x, y, CollisionFlags.WallEast);
                    AddFlags(plane, x + 1, y, CollisionFlags.WallWest);
                    break;
                case WallSide.South:
                    AddFlags(plane, x, y, CollisionFlags.WallSouth);
                    AddFlags(plane, x, y - 1, CollisionFlags.WallNorth);
                    break;
                case WallSide.West:
                    AddFlags(plane, x, y, CollisionFlags.WallWest);
                    AddFlags(plane, x - 1, y, CollisionFlags.WallEast);
                    break;
            }
        }

        /// <summary>
        /// Whether a single step of (dx, dy), each in -1..1, is allowed from (x, y). Diagonal steps
        /// need both straight routes around the corner to be open, so walls can't be cut through.
        /// </summary>
        public bool CanStep(int plane, int x, int y, int dx, int dy)
        {
            if (dx == 0 && dy == 0) return true;
            if (dx < -1 || dx > 1 || dy < -1 || dy > 1) return false;
            if (dx == 0 || dy == 0) return CanStepStraight(plane, x, y, dx, dy);
            return CanStepStraight(plane, x, y, dx, 0)
                && CanStepStraight(plane, x, y, 0, dy)
                && CanStepStraight(plane, x + dx, y, 0, dy)
                && CanStepStraight(plane, x, y + dy, dx, 0);
        }

        private bool CanStepStraight(int plane, int x, int y, int dx, int dy)
        {
            CollisionFlags from = GetFlags(plane, x, y);
            CollisionFlags to = GetFlags(plane, x + dx, y + dy);
            if ((to & CollisionFlags.Blocked) != 0) return false;
            if (dy == 1) return (from & CollisionFlags.WallNorth) == 0 && (to & CollisionFlags.WallSouth) == 0;
            if (dy == -1) return (from & CollisionFlags.WallSouth) == 0 && (to & CollisionFlags.WallNorth) == 0;
            if (dx == 1) return (from & CollisionFlags.WallEast) == 0 && (to & CollisionFlags.WallWest) == 0;
            return (from & CollisionFlags.WallWest) == 0 && (to & CollisionFlags.WallEast) == 0;
        }

        public void Write(Stream stream)
        {
            using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                w.Write(Magic);
                w.Write(FormatVersion);
                w.Write(OriginX);
                w.Write(OriginY);
                w.Write(Width);
                w.Write(Height);
                w.Write(Planes);
                w.Write(SpawnPlane);
                w.Write(Spawn.X);
                w.Write(Spawn.Y);
                foreach (CollisionFlags f in _flags) w.Write((ushort)f);
            }
        }

        public static MapData Read(Stream stream)
        {
            using (var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                if (r.ReadUInt32() != Magic) throw new InvalidDataException("Not an R'th map file.");
                int version = r.ReadInt32();
                if (version != FormatVersion) throw new InvalidDataException("Unsupported map format version " + version + ".");
                var map = new MapData(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
                map.SpawnPlane = r.ReadInt32();
                map.Spawn = new TileCoord(r.ReadInt32(), r.ReadInt32());
                for (int i = 0; i < map._flags.Length; i++) map._flags[i] = (CollisionFlags)r.ReadUInt16();
                return map;
            }
        }

        public void Save(string path)
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (dir != null) Directory.CreateDirectory(dir);
            using (var f = File.Create(path)) Write(f);
        }

        public static MapData Load(string path)
        {
            using (var f = File.OpenRead(path)) return Read(f);
        }
    }
}
