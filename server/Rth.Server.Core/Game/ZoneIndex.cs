using System.Collections.Generic;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Which entities stand in which 8x8 zone, so "who is near here" looks at a few zones instead
    /// of the whole world. Rebuilt once a tick in the zone phase, after movement, logins and
    /// logouts, so it is exact for sending updates. Earlier phases of the next tick see the same
    /// positions, as nothing moves in between.
    /// </summary>
    public sealed class ZoneIndex
    {
        private readonly Dictionary<long, List<Entity>> _zones = new Dictionary<long, List<Entity>>();
        private readonly Stack<List<Entity>> _spare = new Stack<List<Entity>>();

        private static long Key(int plane, int zoneX, int zoneY) =>
            ((long)plane << 48) | ((long)(zoneX & 0xFFFFFF) << 24) | (long)(zoneY & 0xFFFFFF);

        public void Rebuild(IEnumerable<Entity> entities)
        {
            foreach (List<Entity> list in _zones.Values)
            {
                list.Clear();
                _spare.Push(list);
            }
            _zones.Clear();
            foreach (Entity entity in entities)
            {
                long key = Key(entity.Plane, Zones.ZoneOf(entity.Position.X), Zones.ZoneOf(entity.Position.Y));
                if (!_zones.TryGetValue(key, out List<Entity>? list))
                {
                    list = _spare.Count > 0 ? _spare.Pop() : new List<Entity>();
                    _zones.Add(key, list);
                }
                list.Add(entity);
            }
        }

        private readonly HashSet<long> _active = new HashSet<long>();

        /// <summary>
        /// Marks the zones within <paramref name="radius"/> tiles of each player (on their plane)
        /// as active: where creatures think. A radius of 0 or less marks nothing (see <see cref="IsActive"/>).
        /// </summary>
        public void MarkActive(IEnumerable<Entity> players, int radius)
        {
            _active.Clear();
            ActiveRadius = radius;
            if (radius <= 0) return;
            foreach (Entity player in players)
            {
                int minX = Zones.ZoneOf(player.Position.X - radius), maxX = Zones.ZoneOf(player.Position.X + radius);
                int minY = Zones.ZoneOf(player.Position.Y - radius), maxY = Zones.ZoneOf(player.Position.Y + radius);
                for (int zy = minY; zy <= maxY; zy++)
                    for (int zx = minX; zx <= maxX; zx++)
                        _active.Add(Key(player.Plane, zx, zy));
            }
        }

        /// <summary>The radius the active zones were last marked with (0: everywhere is active).</summary>
        public int ActiveRadius { get; private set; }

        /// <summary>How many zones are active now.</summary>
        public int ActiveZoneCount => _active.Count;

        /// <summary>Whether a tile is near enough a player for creatures there to think. Always true when activity is off.</summary>
        public bool IsActive(int plane, TileCoord tile) =>
            ActiveRadius <= 0 || _active.Contains(Key(plane, Zones.ZoneOf(tile.X), Zones.ZoneOf(tile.Y)));

        /// <summary>
        /// Entities whose zone overlaps the square of the given radius around a tile. A superset:
        /// callers still check the exact distance (and that the entity is still in the world).
        /// </summary>
        public IEnumerable<Entity> Near(int plane, TileCoord tile, int radius)
        {
            int minX = Zones.ZoneOf(tile.X - radius), maxX = Zones.ZoneOf(tile.X + radius);
            int minY = Zones.ZoneOf(tile.Y - radius), maxY = Zones.ZoneOf(tile.Y + radius);
            for (int zy = minY; zy <= maxY; zy++)
                for (int zx = minX; zx <= maxX; zx++)
                    if (_zones.TryGetValue(Key(plane, zx, zy), out List<Entity>? list))
                        foreach (Entity entity in list)
                            yield return entity;
        }
    }
}
