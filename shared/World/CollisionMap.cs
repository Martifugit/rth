using System;

namespace Rth.Shared.World
{
    /// <summary>
    /// Per-tile collision for some part of the world, on one or more planes, and the movement
    /// rules over it. <see cref="MapData"/> is one exported region (a rectangle); <see cref="WorldMap"/>
    /// stitches regions into the whole world. Tiles a map doesn't contain are blocked.
    /// </summary>
    public abstract class CollisionMap
    {
        /// <summary>Where new players appear.</summary>
        public int SpawnPlane { get; set; }
        public TileCoord Spawn { get; set; }

        public abstract bool Contains(int plane, int x, int y);

        public abstract CollisionFlags GetFlags(int plane, int x, int y);

        /// <summary>Adds flags to a tile. Does nothing outside the map.</summary>
        public abstract void AddFlags(int plane, int x, int y, CollisionFlags flags);

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

        /// <summary>Whether there's no wall on the edge between a tile and its straight neighbour (ignores blocked tiles).</summary>
        public bool EdgeOpen(int plane, int x, int y, int dx, int dy)
        {
            if (Math.Abs(dx) + Math.Abs(dy) != 1 || !Contains(plane, x + dx, y + dy)) return false;
            return NoWallBetween(GetFlags(plane, x, y), GetFlags(plane, x + dx, y + dy), dx, dy);
        }

        private bool CanStepStraight(int plane, int x, int y, int dx, int dy)
        {
            CollisionFlags to = GetFlags(plane, x + dx, y + dy);
            if ((to & CollisionFlags.Blocked) != 0) return false;
            return NoWallBetween(GetFlags(plane, x, y), to, dx, dy);
        }

        /// <summary>Whether the walls of two straight neighbours leave the edge between them open.</summary>
        public static bool NoWallBetween(CollisionFlags from, CollisionFlags to, int dx, int dy)
        {
            if (dy == 1) return (from & CollisionFlags.WallNorth) == 0 && (to & CollisionFlags.WallSouth) == 0;
            if (dy == -1) return (from & CollisionFlags.WallSouth) == 0 && (to & CollisionFlags.WallNorth) == 0;
            if (dx == 1) return (from & CollisionFlags.WallEast) == 0 && (to & CollisionFlags.WallWest) == 0;
            return (from & CollisionFlags.WallWest) == 0 && (to & CollisionFlags.WallEast) == 0;
        }
    }
}
