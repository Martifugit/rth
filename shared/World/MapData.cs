using System;
using System.IO;

namespace Rth.Shared.World
{
    /// <summary>
    /// Collision for one exported region: a rectangle of the world, on one or more planes
    /// (floors). Written by the Unity map exporter, read by the server, which stitches regions
    /// into a <see cref="WorldMap"/>. Tiles outside the rectangle are blocked.
    /// </summary>
    public sealed class MapData : CollisionMap
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

        public override bool Contains(int plane, int x, int y) =>
            plane >= 0 && plane < Planes &&
            x >= OriginX && x < OriginX + Width &&
            y >= OriginY && y < OriginY + Height;

        private int Index(int plane, int x, int y) => (plane * Height + (y - OriginY)) * Width + (x - OriginX);

        public override CollisionFlags GetFlags(int plane, int x, int y) =>
            Contains(plane, x, y) ? _flags[Index(plane, x, y)] : CollisionFlags.Blocked;

        public void SetFlags(int plane, int x, int y, CollisionFlags flags)
        {
            if (!Contains(plane, x, y)) throw new ArgumentOutOfRangeException(nameof(x), "Tile is outside the map.");
            _flags[Index(plane, x, y)] = flags;
        }

        public override void AddFlags(int plane, int x, int y, CollisionFlags flags)
        {
            if (Contains(plane, x, y)) _flags[Index(plane, x, y)] |= flags;
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
