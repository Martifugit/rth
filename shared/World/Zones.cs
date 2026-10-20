namespace Rth.Shared.World
{
    /// <summary>
    /// The world is divided into 8x8-tile zones: the unit of collision storage, region alignment
    /// and the server's spatial index. Works for negative coordinates (arithmetic shifts floor).
    /// </summary>
    public static class Zones
    {
        public const int Shift = 3;
        public const int Size = 1 << Shift;

        /// <summary>The zone coordinate containing a tile coordinate.</summary>
        public static int ZoneOf(int tile) => tile >> Shift;

        /// <summary>A tile's position within its zone, 0..7.</summary>
        public static int Local(int tile) => tile & (Size - 1);

        public static bool IsAligned(int tile) => Local(tile) == 0;
    }
}
