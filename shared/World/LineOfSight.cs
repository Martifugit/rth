using System;

namespace Rth.Shared.World
{
    /// <summary>
    /// Whether a projectile can fly between two tiles. Walls (tile edges) block it; blocked tiles
    /// such as rocks and water don't, since things can be shot over them.
    /// </summary>
    public static class LineOfSight
    {
        public static bool Has(MapData map, int plane, TileCoord from, TileCoord to)
        {
            int x = from.X;
            int y = from.Y;
            int dx = Math.Abs(to.X - x);
            int dy = Math.Abs(to.Y - y);
            int sx = Math.Sign(to.X - x);
            int sy = Math.Sign(to.Y - y);
            int err = dx - dy;

            // Bresenham, one tile step at a time; each step must not cross a wall.
            while (x != to.X || y != to.Y)
            {
                int e2 = 2 * err;
                int stepX = 0;
                int stepY = 0;
                if (e2 > -dy)
                {
                    err -= dy;
                    stepX = sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    stepY = sy;
                }
                if (!EdgeOpen(map, plane, x, y, stepX, stepY)) return false;
                x += stepX;
                y += stepY;
            }
            return true;
        }

        private static bool EdgeOpen(MapData map, int plane, int x, int y, int dx, int dy)
        {
            if (dx != 0 && dy != 0)
            {
                // Diagonal: passes if either way around the corner is open.
                return (Straight(map, plane, x, y, dx, 0) && Straight(map, plane, x + dx, y, 0, dy))
                    || (Straight(map, plane, x, y, 0, dy) && Straight(map, plane, x, y + dy, dx, 0));
            }
            return Straight(map, plane, x, y, dx, dy);
        }

        private static bool Straight(MapData map, int plane, int x, int y, int dx, int dy)
        {
            CollisionFlags from = map.GetFlags(plane, x, y);
            CollisionFlags to = map.GetFlags(plane, x + dx, y + dy);
            if (!map.Contains(plane, x + dx, y + dy)) return false;
            if (dy == 1) return (from & CollisionFlags.WallNorth) == 0 && (to & CollisionFlags.WallSouth) == 0;
            if (dy == -1) return (from & CollisionFlags.WallSouth) == 0 && (to & CollisionFlags.WallNorth) == 0;
            if (dx == 1) return (from & CollisionFlags.WallEast) == 0 && (to & CollisionFlags.WallWest) == 0;
            return (from & CollisionFlags.WallWest) == 0 && (to & CollisionFlags.WallEast) == 0;
        }
    }
}
