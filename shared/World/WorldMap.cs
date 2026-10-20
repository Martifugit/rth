using System;
using System.Collections.Generic;

namespace Rth.Shared.World
{
    /// <summary>
    /// The whole world's collision, stitched together from exported regions. Stored sparsely in
    /// 8x8-tile zones, so the space between regions (sea, wilds nobody has built yet) costs
    /// nothing: a tile in a zone that doesn't exist is blocked.
    /// Coordinates are absolute and may be negative. Tolling's south-west corner is (0, 0).
    /// </summary>
    public sealed class WorldMap : CollisionMap
    {
        public const int MaxPlanes = 4;

        private readonly Dictionary<long, CollisionFlags[]> _zones = new Dictionary<long, CollisionFlags[]>();
        private readonly List<WorldRegion> _regions = new List<WorldRegion>();

        public IReadOnlyList<WorldRegion> Regions => _regions;

        private static long ZoneKey(int plane, int x, int y) =>
            ((long)plane << 48) | ((long)(Zones.ZoneOf(x) & 0xFFFFFF) << 24) | (long)(Zones.ZoneOf(y) & 0xFFFFFF);

        private static int TileIndex(int x, int y) => (Zones.Local(y) << Zones.Shift) | Zones.Local(x);

        public override bool Contains(int plane, int x, int y) =>
            plane >= 0 && plane < MaxPlanes && _zones.ContainsKey(ZoneKey(plane, x, y));

        public override CollisionFlags GetFlags(int plane, int x, int y)
        {
            if (plane < 0 || plane >= MaxPlanes) return CollisionFlags.Blocked;
            return _zones.TryGetValue(ZoneKey(plane, x, y), out CollisionFlags[]? zone) ? zone[TileIndex(x, y)] : CollisionFlags.Blocked;
        }

        public override void AddFlags(int plane, int x, int y, CollisionFlags flags)
        {
            if (plane < 0 || plane >= MaxPlanes) return;
            if (_zones.TryGetValue(ZoneKey(plane, x, y), out CollisionFlags[]? zone)) zone[TileIndex(x, y)] |= flags;
        }

        /// <summary>The region covering a tile, if any.</summary>
        public WorldRegion? RegionAt(int x, int y)
        {
            foreach (WorldRegion region in _regions)
                if (region.Contains(x, y)) return region;
            return null;
        }

        public WorldRegion? FindRegion(string name)
        {
            foreach (WorldRegion region in _regions)
                if (string.Equals(region.Name, name, StringComparison.OrdinalIgnoreCase)) return region;
            return null;
        }

        /// <summary>
        /// Copies a region's collision into the world. Regions must be zone-aligned and must not
        /// overlap, so every tile has exactly one source.
        /// </summary>
        public void AddRegion(WorldRegion info, MapData map)
        {
            if (map.OriginX != info.X || map.OriginY != info.Y || map.Width != info.Width || map.Height != info.Height)
                throw new ArgumentException("Region '" + info.Name + "' doesn't match its map's rectangle.");
            if (!Zones.IsAligned(map.OriginX) || !Zones.IsAligned(map.OriginY) || !Zones.IsAligned(map.Width) || !Zones.IsAligned(map.Height))
                throw new ArgumentException("Region '" + info.Name + "' must start and end on whole zones (multiples of " + Zones.Size + " tiles).");
            if (map.Planes > MaxPlanes)
                throw new ArgumentException("Region '" + info.Name + "' has more than " + MaxPlanes + " planes.");
            foreach (WorldRegion other in _regions)
            {
                if (string.Equals(other.Name, info.Name, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Two regions are named '" + info.Name + "'.");
                if (other.Overlaps(info))
                    throw new ArgumentException("Regions '" + other.Name + "' and '" + info.Name + "' overlap.");
            }

            for (int plane = 0; plane < map.Planes; plane++)
                for (int y = map.OriginY; y < map.OriginY + map.Height; y++)
                    for (int x = map.OriginX; x < map.OriginX + map.Width; x++)
                    {
                        long key = ZoneKey(plane, x, y);
                        if (!_zones.TryGetValue(key, out CollisionFlags[]? zone))
                        {
                            zone = new CollisionFlags[Zones.Size * Zones.Size];
                            _zones.Add(key, zone);
                        }
                        zone[TileIndex(x, y)] = map.GetFlags(plane, x, y);
                    }
            _regions.Add(info);
        }
    }
}
