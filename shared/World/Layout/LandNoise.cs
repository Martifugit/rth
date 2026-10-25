using System;

namespace Rth.Shared.World.Layout
{
    /// <summary>The generator's noise: smooth value noise, the same for a seed on every machine and run.</summary>
    public static class LandNoise
    {
        /// <summary>Smooth value noise in [-1, 1].</summary>
        public static float Value(int seed, float x, float y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            float sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            float n00 = Lattice(seed, ix, iy), n10 = Lattice(seed, ix + 1, iy);
            float n01 = Lattice(seed, ix, iy + 1), n11 = Lattice(seed, ix + 1, iy + 1);
            float a = n00 + (n10 - n00) * sx, b = n01 + (n11 - n01) * sx;
            return a + (b - a) * sy;
        }

        private static float Lattice(int seed, int a, int b) => (Geometry.Hash(Geometry.Hash((uint)seed, (uint)a), (uint)b) & 0xFFFF) / 32767.5f - 1f;

        /// <summary>Three octaves of <see cref="Value"/>, about [-1, 1].</summary>
        public static float Fbm(int seed, float x, float y) =>
            (0.6f * Value(seed, x, y) + 0.3f * Value(seed + 1, x * 2.13f, y * 2.13f) + 0.15f * Value(seed + 2, x * 4.37f, y * 4.37f)) / 1.05f;

        /// <summary>Ridged noise in [0, 1]: two octaves of 1 - |noise|, squared, so crests are sharp and cols deep.</summary>
        public static float Ridged(int seed, float x, float y)
        {
            float a = 1f - Math.Abs(Value(seed, x, y));
            float b = 1f - Math.Abs(Value(seed + 1, x * 2.1f, y * 2.1f));
            return Math.Max(0f, Math.Min(1f, 0.7f * a * a + 0.3f * b * b));
        }

        /// <summary>A hash in [0, 1) of a key and a seed.</summary>
        public static float Hash01(int seed, int a, int b) => (Geometry.Hash(Geometry.Hash((uint)seed, (uint)a), (uint)b) & 0xFFFFFF) / 16777216f;

        public static float Smooth(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3 - 2 * t);
        }
    }
}
