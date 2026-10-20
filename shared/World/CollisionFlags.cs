using System;

namespace Rth.Shared.World
{
    /// <summary>
    /// Per-tile collision. A wall flag means the tile has a wall on that edge; walls are stored
    /// on both tiles that share the edge so either side can be checked alone.
    /// </summary>
    [Flags]
    public enum CollisionFlags : ushort
    {
        None = 0,
        /// <summary>Nothing can stand here: an object footprint, water, or no ground.</summary>
        Blocked = 1 << 0,
        WallNorth = 1 << 1,
        WallEast = 1 << 2,
        WallSouth = 1 << 3,
        WallWest = 1 << 4,
    }

    public enum WallSide
    {
        North,
        East,
        South,
        West,
    }
}
