using System;

namespace Rth.Shared.World
{
    /// <summary>A tile on the grid. +X is east, +Y is north (Unity +Z).</summary>
    public readonly struct TileCoord : IEquatable<TileCoord>
    {
        public readonly int X;
        public readonly int Y;

        public TileCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public TileCoord Offset(int dx, int dy) => new TileCoord(X + dx, Y + dy);

        /// <summary>Distance in moves when diagonals cost the same as straight steps.</summary>
        public int ChebyshevDistance(TileCoord other) => Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

        public int DistanceSquared(TileCoord other)
        {
            int dx = X - other.X;
            int dy = Y - other.Y;
            return dx * dx + dy * dy;
        }

        public bool Equals(TileCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is TileCoord other && Equals(other);
        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663);
        public static bool operator ==(TileCoord a, TileCoord b) => a.Equals(b);
        public static bool operator !=(TileCoord a, TileCoord b) => !a.Equals(b);
        public override string ToString() => "(" + X + ", " + Y + ")";
    }
}
