using System;
using System.Collections.Generic;

namespace Rth.Shared.World
{
    /// <summary>
    /// Breadth-first search over the tile grid with 8-way movement, limited to a square around
    /// the start. If the goal can't be reached, walks to the reachable tile closest to it.
    /// </summary>
    /// <remarks>
    /// Searches run thousands of times a tick on a busy server (every creature's wander and chase,
    /// every click), so the search's arrays are kept per thread and reused: a tile counts as
    /// visited only if it carries this search's stamp, so nothing is cleared between searches.
    /// </remarks>
    public static class Pathfinder
    {
        public const int DefaultSearchRadius = 64;

        // West, east, south, north, then the diagonals: straight lines are preferred on ties.
        private static readonly int[] StepX = { -1, 1, 0, 0, -1, 1, -1, 1 };
        private static readonly int[] StepY = { 0, 0, -1, 1, -1, -1, 1, 1 };

        /// <summary>One thread's search arrays, sized for the largest radius it has searched.</summary>
        private sealed class Buffers
        {
            public int Size;
            public int[] Stamp = Array.Empty<int>();
            public int[] Parent = Array.Empty<int>();
            public int[] Distance = Array.Empty<int>();
            /// <summary>The search's queue, which is also every visited tile in the order found.</summary>
            public int[] Queue = Array.Empty<int>();
            public int Generation;

            public void Fit(int size)
            {
                if (size <= Size) return;
                Size = size;
                int cells = size * size;
                Stamp = new int[cells];
                Parent = new int[cells];
                Distance = new int[cells];
                Queue = new int[cells];
                Generation = 0;
            }
        }

        [ThreadStatic] private static Buffers? _buffers;

        /// <summary>The tiles to walk through, in order, excluding the start. Empty if there is nowhere better to go.</summary>
        public static List<TileCoord> FindPath(CollisionMap map, int plane, TileCoord start, TileCoord goal, int searchRadius = DefaultSearchRadius)
        {
            var path = new List<TileCoord>();
            if (start == goal) return path;

            int size = searchRadius * 2 + 1;
            int minX = start.X - searchRadius;
            int minY = start.Y - searchRadius;
            Buffers b = _buffers ??= new Buffers();
            b.Fit(size);
            if (++b.Generation == int.MaxValue)
            {
                Array.Clear(b.Stamp, 0, b.Stamp.Length);
                b.Generation = 1;
            }
            int gen = b.Generation;
            // The arrays may be wider than this search; index them with their own width.
            int width = b.Size;
            int[] stamp = b.Stamp, parent = b.Parent, distance = b.Distance, queue = b.Queue;

            int startIndex = searchRadius * width + searchRadius;
            stamp[startIndex] = gen;
            parent[startIndex] = startIndex;
            distance[startIndex] = 0;
            int head = 0, tail = 0;
            queue[tail++] = startIndex;

            int goalIndex = -1;
            while (head < tail)
            {
                int current = queue[head++];
                int cx = minX + current % width;
                int cy = minY + current / width;
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
                    int next = ly * width + lx;
                    if (stamp[next] == gen) continue;
                    if (!map.CanStep(plane, cx, cy, StepX[d], StepY[d])) continue;
                    stamp[next] = gen;
                    parent[next] = current;
                    distance[next] = distance[current] + 1;
                    queue[tail++] = next;
                }
            }

            if (goalIndex == -1) goalIndex = ClosestReachable(queue, tail, distance, width, minX, minY, goal);
            if (goalIndex == startIndex) return path;

            for (int i = goalIndex; i != startIndex; i = parent[i])
                path.Add(new TileCoord(minX + i % width, minY + i / width));
            path.Reverse();
            return path;
        }

        /// <summary>
        /// The visited tile nearest the goal (then the shortest walk, then the lowest row and
        /// column, as a scan of the square would find it first).
        /// </summary>
        private static int ClosestReachable(int[] visited, int count, int[] distance, int width, int minX, int minY, TileCoord goal)
        {
            int best = -1;
            int bestDistanceSq = int.MaxValue;
            int bestPathLength = int.MaxValue;
            for (int n = 0; n < count; n++)
            {
                int i = visited[n];
                var tile = new TileCoord(minX + i % width, minY + i / width);
                int dSq = tile.DistanceSquared(goal);
                if (dSq < bestDistanceSq ||
                    (dSq == bestDistanceSq && (distance[i] < bestPathLength || (distance[i] == bestPathLength && i < best))))
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
