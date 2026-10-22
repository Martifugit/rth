using System;
using System.Collections.Generic;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// Tile sets from shapes, shared by the region compiler and the Unity builders so a road or a
    /// river covers the same tiles on both sides.
    /// </summary>
    public static class TileShapes
    {
        /// <summary>
        /// Tiles whose centre lies within <paramref name="halfWidth"/> of a polyline (a river or
        /// road), clipped to the rectangle at (<paramref name="originX"/>, <paramref name="originY"/>).
        /// </summary>
        public static HashSet<(int x, int y)> Along(IList<float[]> line, float halfWidth, int originX, int originY, int width, int height)
        {
            var tiles = new HashSet<(int x, int y)>();
            for (int i = 0; i + 1 < line.Count; i++)
            {
                float ax = line[i][0], ay = line[i][1], bx = line[i + 1][0], by = line[i + 1][1];
                int minX = (int)Math.Floor(Math.Min(ax, bx) - halfWidth) - 1;
                int maxX = (int)Math.Ceiling(Math.Max(ax, bx) + halfWidth) + 1;
                int minY = (int)Math.Floor(Math.Min(ay, by) - halfWidth) - 1;
                int maxY = (int)Math.Ceiling(Math.Max(ay, by) + halfWidth) + 1;
                for (int y = Math.Max(originY, minY); y <= Math.Min(originY + height - 1, maxY); y++)
                    for (int x = Math.Max(originX, minX); x <= Math.Min(originX + width - 1, maxX); x++)
                        if (DistanceToSegment(x + 0.5f, y + 0.5f, ax, ay, bx, by) <= halfWidth) tiles.Add((x, y));
            }
            return tiles;
        }

        private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = ((px - ax) * abx + (py - ay) * aby) / Math.Max(0.0001f, abx * abx + aby * aby);
            t = Math.Max(0f, Math.Min(1f, t));
            float cx = px - (ax + abx * t), cy = py - (ay + aby * t);
            return (float)Math.Sqrt(cx * cx + cy * cy);
        }
    }
}
