using System;
using System.Collections.Generic;

namespace Rth.Shared.World
{
    /// <summary>
    /// Breadth-first search over the tile grid with 8-way movement, limited to a square around
    /// the start. If the goal can't be reached, walks to the reachable tile closest to it.
    /// </summary>
    public static class Pathfinder
    {
        public const int DefaultSearchRadius = 64;

        // West, east, south, north, then the diagonals: straight lines are preferred on ties.
        private static readonly int[] StepX = { -1, 1, 0, 0, -1, 1, -1, 1 };
        private static readonly int[] StepY = { 0, 0, -1, 1, -1, -1, 1, 1 };

        /// <summary>The tiles to walk through, in order, excluding the start. Empty if there is nowhere better to go.</summary>
        public static List<TileCoord> FindPath(MapData map, int plane, TileCoord start, TileCoord goal, int searchRadius = DefaultSearchRadius)
        {
            var path = new List<TileCoord>();
            if (start == goal) return path;

            int size = searchRadius * 2 + 1;
            int minX = start.X - searchRadius;
            int minY = start.Y - searchRadius;
            var parent = new int[size * size];
            var distance = new int[size * size];
            for (int i = 0; i < parent.Length; i++) parent[i] = -1;

            int startIndex = searchRadius * size + searchRadius;
            parent[startIndex] = startIndex;
            var queue = new Queue<int>();
            queue.Enqueue(startIndex);

            int goalIndex = -1;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int cx = minX + current % size;
                int cy = minY + current / size;
                if (cx == goal.X && cy == goal.Y)
                {
                    goalIndex = current;
                    break;
                }

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + StepX[d];
                    int ny = cy + StepY[d];
                    int lx = nx - minX;
                    int ly = ny - minY;
                    if (lx < 0 || ly < 0 || lx >= size || ly >= size) continue;
                    int next = ly * size + lx;
                    if (parent[next] != -1) continue;
                    if (!map.CanStep(plane, cx, cy, StepX[d], StepY[d])) continue;
                    parent[next] = current;
                    distance[next] = distance[current] + 1;
                    queue.Enqueue(next);
                }
            }

            if (goalIndex == -1) goalIndex = ClosestReachable(parent, distance, size, minX, minY, goal);
            if (goalIndex == startIndex) return path;

            for (int i = goalIndex; i != startIndex; i = parent[i])
                path.Add(new TileCoord(minX + i % size, minY + i / size));
            path.Reverse();
            return path;
        }

        private static int ClosestReachable(int[] parent, int[] distance, int size, int minX, int minY, TileCoord goal)
        {
            int best = -1;
            int bestDistanceSq = int.MaxValue;
            int bestPathLength = int.MaxValue;
            for (int i = 0; i < parent.Length; i++)
            {
                if (parent[i] == -1) continue;
                var tile = new TileCoord(minX + i % size, minY + i / size);
                int dSq = tile.DistanceSquared(goal);
                if (dSq < bestDistanceSq || (dSq == bestDistanceSq && distance[i] < bestPathLength))
                {
                    best = i;
                    bestDistanceSq = dSq;
                    bestPathLength = distance[i];
                }
            }
            return best;
        }
    }
}
