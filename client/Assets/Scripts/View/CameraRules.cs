using System;
using System.Collections.Generic;

namespace Rth.Client
{
    /// <summary>
    /// The camera's rules from the 2004 game (docs/world-scale.md, section 5), as plain maths
    /// with no Unity types so they can be tested anywhere: the pitch floor that tilts the camera
    /// up over high ground, its easing, and the walk along the line of sight that finds roofs in
    /// the way. Angles are degrees; distances and heights are tiles.
    /// </summary>
    public static class CameraRules
    {
        /// <summary>Each tile of ground above the player raises the lowest pitch this much.</summary>
        public const float LiftDegreesPerTile = 16.875f;

        /// <summary>How far round the focus the lift looks for high ground (a 9 × 9 square).</summary>
        public const int LiftRadius = 4;

        /// <summary>Above this pitch the camera sees over roofs, so the line-of-sight rule is off.</summary>
        public const float RoofSightMaxPitch = 54.5f;

        /// <summary>The reference eases the floor once per 20 ms client cycle.</summary>
        public const float EaseCycleSeconds = 0.02f;
        public const float EaseUpPerCycle = 1f / 24f;
        public const float EaseDownPerCycle = 1f / 80f;

        /// <summary>
        /// The lowest pitch the ground allows: <paramref name="groundAbove"/> is the highest ground
        /// near the focus minus the ground under the player. Never below the camera's own minimum
        /// or above its maximum; flat or falling ground leaves the minimum as it is.
        /// </summary>
        public static float LiftFloor(float groundAbove, float minPitch, float maxPitch)
        {
            if (groundAbove <= 0f) return minPitch;
            return Clamp(groundAbove * LiftDegreesPerTile, minPitch, maxPitch);
        }

        /// <summary>
        /// Moves the floor towards its target: quickly when rising (1/24 of the gap per 20 ms),
        /// slowly when falling (1/80), the same at any frame rate.
        /// </summary>
        public static float EaseFloor(float current, float target, float deltaSeconds)
        {
            if (deltaSeconds <= 0f || current == target) return current;
            float perCycle = target > current ? EaseUpPerCycle : EaseDownPerCycle;
            float kept = (float)Math.Pow(1f - perCycle, deltaSeconds / EaseCycleSeconds);
            float next = target + (current - target) * kept;
            return Math.Abs(next - target) < 0.001f ? target : next;
        }

        /// <summary>The pitch the camera actually uses: the player's own, raised to the floor, never above the maximum.</summary>
        public static float EffectivePitch(float chosenPitch, float floor, float maxPitch) =>
            Math.Min(Math.Max(chosenPitch, floor), maxPitch);

        /// <summary>The highest of <paramref name="heightAt"/> over tile centres within <see cref="LiftRadius"/> of a tile.</summary>
        public static float HighestNear(Func<int, int, float> heightAt, int tileX, int tileZ, int radius = LiftRadius)
        {
            float best = float.MinValue;
            for (int z = tileZ - radius; z <= tileZ + radius; z++)
                for (int x = tileX - radius; x <= tileX + radius; x++)
                    best = Math.Max(best, heightAt(x, z));
            return best;
        }

        /// <summary>
        /// Every tile the straight line from (ax, az) to (bx, bz) passes through, in order from the
        /// first point, including both ends. Corners the line only touches count for both tiles.
        /// </summary>
        public static void LineTiles(float ax, float az, float bx, float bz, List<(int x, int z)> into)
        {
            into.Clear();
            int x = (int)Math.Floor(ax), z = (int)Math.Floor(az);
            int endX = (int)Math.Floor(bx), endZ = (int)Math.Floor(bz);
            float dx = bx - ax, dz = bz - az;
            int stepX = Math.Sign(dx), stepZ = Math.Sign(dz);
            // Distance along the line (as a fraction of it) to the next grid line on each axis.
            float nextX = stepX == 0 ? float.MaxValue : ((stepX > 0 ? x + 1 : x) - ax) / dx;
            float nextZ = stepZ == 0 ? float.MaxValue : ((stepZ > 0 ? z + 1 : z) - az) / dz;
            float deltaX = stepX == 0 ? float.MaxValue : Math.Abs(1f / dx);
            float deltaZ = stepZ == 0 ? float.MaxValue : Math.Abs(1f / dz);
            into.Add((x, z));
            int guard = Math.Abs(endX - x) + Math.Abs(endZ - z) + 2;
            while ((x != endX || z != endZ) && guard-- > 0)
            {
                if (Math.Abs(nextX - nextZ) < 1e-6f)
                {
                    // Through a corner: both side tiles touch the line.
                    into.Add((x + stepX, z));
                    into.Add((x, z + stepZ));
                    x += stepX;
                    z += stepZ;
                    nextX += deltaX;
                    nextZ += deltaZ;
                }
                else if (nextX < nextZ)
                {
                    x += stepX;
                    nextX += deltaX;
                }
                else
                {
                    z += stepZ;
                    nextZ += deltaZ;
                }
                into.Add((x, z));
            }
        }

        /// <summary>
        /// Whether a roof over tiles [x0, x0+width) × [z0, z0+depth), reaching up to
        /// <paramref name="roofTop"/>, stands in the line of sight from the camera to a point on the
        /// player. <paramref name="line"/> holds the tiles from <see cref="LineTiles"/> between the
        /// two. A roofed tile counts only where the sight line passes below the roof's top, so a
        /// roof the camera sees right over stays.
        /// </summary>
        public static bool RoofOnSight(int x0, int z0, int width, int depth, float roofTop,
            float camX, float camY, float camZ, float toX, float toY, float toZ, IList<(int x, int z)> line)
        {
            float dx = toX - camX, dz = toZ - camZ;
            float lengthSq = dx * dx + dz * dz;
            for (int i = 0; i < line.Count; i++)
            {
                (int x, int z) = line[i];
                if (x < x0 || x >= x0 + width || z < z0 || z >= z0 + depth) continue;
                // Where along the line this tile is: the projection of its centre, clamped.
                float t = lengthSq < 1e-6f ? 1f : Clamp(((x + 0.5f - camX) * dx + (z + 0.5f - camZ) * dz) / lengthSq, 0f, 1f);
                float sight = camY + (toY - camY) * t;
                if (roofTop > sight) return true;
            }
            return false;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
