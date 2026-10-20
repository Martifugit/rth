using UnityEngine;

namespace Rth.MapTools
{
    /// <summary>
    /// One region of the world: the rectangle of tiles that gets exported. One Unity unit is one
    /// tile: tile (x, y) covers world X [x, x+1] and Z [y, y+1], in absolute world coordinates, so
    /// neighbouring region scenes line up when loaded together. Origin and size must be multiples
    /// of 8 (whole zones) and regions must not overlap. Everything the exporter looks at sits
    /// under this object.
    /// </summary>
    public sealed class MapArea : MonoBehaviour
    {
        [Tooltip("File name (without extension) written to server/data/maps/. Unique per region.")]
        public string exportName = "region";

        [Tooltip("Shown to players as they walk in: \"Tolling\".")]
        public string title = "";

        [Tooltip("Absolute tile coordinate of the south-west corner (a multiple of 8). Tolling's is (0, 0).")]
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
