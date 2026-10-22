using Rth.Shared.World;
using UnityEngine;

namespace Rth.MapTools
{
    /// <summary>
    /// A hand-made change to the ground that survives re-baking the region from the world layout
    /// (docs/world-building.md): flatten a town, raise a hill, dig a pond. It covers the tile
    /// corners from its position (rounded) over <see cref="size"/>, or the ellipse inside them,
    /// fading out over <see cref="falloff"/> tiles. Stamps apply in hierarchy order.
    /// </summary>
    public sealed class MapTerrainStamp : MonoBehaviour
    {
        public StampMode mode = StampMode.Level;
        public Vector2Int size = new Vector2Int(8, 8);
        [Tooltip("Set: the height. Add: how much to raise (negative lowers). Level: added to the average.")]
        public float height;
        public float falloff = 3f;
        public bool round;

        public Vector2Int Corner => new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.z));

        public TerrainStamp ToStamp() => new TerrainStamp
        {
            Mode = mode, X = Corner.x, Y = Corner.y, Width = size.x, Depth = size.y, Height = height, Falloff = falloff, Round = round,
        };

        private void OnDrawGizmos()
        {
            Gizmos.color = mode == StampMode.Add ? new Color(0.3f, 0.9f, 0.3f) : mode == StampMode.Set ? new Color(0.9f, 0.6f, 0.2f) : new Color(0.3f, 0.6f, 1f);
            Vector2Int c = Corner;
            Gizmos.DrawWireCube(new Vector3(c.x + size.x / 2f, transform.position.y, c.y + size.y / 2f), new Vector3(size.x, 0.05f, size.y));
        }
    }
}
