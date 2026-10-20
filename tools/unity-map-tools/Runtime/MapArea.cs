using UnityEngine;

namespace Rth.MapTools
{
    /// <summary>
    /// The rectangle of tiles that gets exported. One Unity unit is one tile: tile (x, y) covers
    /// world X [x, x+1] and Z [y, y+1]. Everything the exporter looks at sits under this object.
    /// </summary>
    public sealed class MapArea : MonoBehaviour
    {
        [Tooltip("File name (without extension) written to server/data/maps/.")]
        public string exportName = "test";

        [Tooltip("Tile coordinate of the south-west corner.")]
        public Vector2Int origin = Vector2Int.zero;

        public Vector2Int size = new Vector2Int(32, 32);

        [Tooltip("Floor level. Only plane 0 is used until there are buildings with upstairs.")]
        public int plane;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            var center = new Vector3(origin.x + size.x * 0.5f, 0.05f, origin.y + size.y * 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(size.x, 0.1f, size.y));
        }
    }
}
