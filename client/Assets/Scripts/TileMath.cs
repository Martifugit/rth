using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>One Unity unit per tile: tile (x, y) covers world X [x, x+1] and Z [y, y+1].</summary>
    public static class TileMath
    {
        /// <summary>
        /// The middle of a tile, on the terrain (<see cref="GroundHeight.TerrainAtTile"/>; 0 where the
        /// ground is flat). For something standing on a floor, use <see cref="GroundHeight.Centre"/>.
        /// </summary>
        public static Vector3 Center(TileCoord tile) => new Vector3(tile.X + 0.5f, GroundHeight.TerrainAtTile(tile.X, tile.Y), tile.Y + 0.5f);

        public static TileCoord FromWorld(Vector3 position) =>
            new TileCoord(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.z));
    }
}
