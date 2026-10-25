using System;

namespace Rth.Shared.World.Ground
{
    /// <summary>How a tile's ground is drawn: blended with its neighbours, a crisp made surface, or water.</summary>
    public enum GroundLayer : byte
    {
        /// <summary>Natural ground (grass, heath, sand, rock): its colour blends over the tiles around it.</summary>
        Under,
        /// <summary>A made surface (road, paving, planks): one crisp colour, its corners cut on the diagonal.</summary>
        Over,
        /// <summary>Water: crisp like an overlay, drawn as its own surface by the client.</summary>
        Water,
    }

    /// <summary>
    /// RuneScape's half-tile shapes, worked out from the neighbours instead of placed by hand
    /// (docs/world-plan.md section 7): a made surface or water would otherwise step tile by tile
    /// along every diagonal. A tile may have one corner's half (the triangle on that corner's side
    /// of the diagonal between its two neighbouring corners) drawn as something else:
    /// an overlay or water tile at the outside corner of a step (both neighbours beside the corner
    /// outside it, both across the tile inside it) gives that half to what lies beyond. So a
    /// staircase of tiles reads as a 45-degree edge. Collision stays per tile.
    /// </summary>
    public static class GroundShapes
    {
        public const byte None = 0, SouthWest = 1, SouthEast = 2, NorthEast = 3, NorthWest = 4;

        /// <summary>No kind: the half shows the blended ground beneath.</summary>
        public const int Beneath = -1;

        /// <summary>
        /// For each tile of a grid (row by row from the south; -1 is no ground), the corner whose half
        /// is drawn as another kind (<see cref="None"/> for none) and that kind (<see cref="Beneath"/>:
        /// the blended underlay).
        /// </summary>
        public static void Compute(int[] kinds, int cols, int rows, Func<int, GroundLayer> layerOf, Func<int, int> groupOf,
                                   out byte[] corner, out int[] other)
        {
            corner = new byte[kinds.Length];
            other = new int[kinds.Length];
            for (int i = 0; i < other.Length; i++) other[i] = Beneath;

            int Kind(int x, int y) => x < 0 || y < 0 || x >= cols || y >= rows ? -1 : kinds[y * cols + x];
            // The group a tile's overlay belongs to (0: none); shapes form within one group.
            int Group(int x, int y)
            {
                int k = Kind(x, y);
                return k < 0 || layerOf(k) == GroundLayer.Under ? 0 : groupOf(k);
            }
            // The tiles round corner c of tile (x, y) other than (x, y): (side a, side b, diagonal).
            (int ax, int ay, int bx, int by, int dx, int dy) Around(int x, int y, byte c) => c switch
            {
                SouthWest => (x - 1, y, x, y - 1, x - 1, y - 1),
                SouthEast => (x + 1, y, x, y - 1, x + 1, y - 1),
                NorthEast => (x + 1, y, x, y + 1, x + 1, y + 1),
                _ => (x - 1, y, x, y + 1, x - 1, y + 1),
            };
            byte Opposite(byte c) => c switch { SouthWest => NorthEast, SouthEast => NorthWest, NorthEast => SouthWest, _ => SouthEast };

            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    int k = kinds[i];
                    if (k < 0) continue;
                    int g = Group(x, y);
                    if (g == 0) continue;
                    // The outside corner of a diagonal edge: the two neighbours beside the corner lie
                    // outside the group and the two across the tile inside it. At most one corner of a
                    // tile can qualify, and only outer corners are ever cut, so a staircase becomes one
                    // straight 45-degree edge (cutting inner corners as well would leave teeth).
                    for (byte c = SouthWest; c <= NorthWest; c++)
                    {
                        (int ax, int ay, int bx, int by, _, _) = Around(x, y, c);
                        (int oax, int oay, int obx, int oby, _, _) = Around(x, y, Opposite(c));
                        if (Group(ax, ay) == g || Group(bx, by) == g || Group(oax, oay) != g || Group(obx, oby) != g) continue;
                        corner[i] = c;
                        // The half goes to what lies beyond: another overlay or water if both sides agree.
                        int ka = Kind(ax, ay), kb = Kind(bx, by);
                        other[i] = ka >= 0 && ka == kb && layerOf(ka) != GroundLayer.Under ? ka : Beneath;
                        break;
                    }
                }
        }

        /// <summary>Whether a point of a tile (u, v in 0..1 from its south-west corner) is on the given corner's half.</summary>
        public static bool InHalf(byte c, float u, float v) => c switch
        {
            SouthWest => u + v < 1f,
            NorthEast => u + v > 1f,
            SouthEast => u > v,
            NorthWest => v > u,
            _ => false,
        };
    }
}
