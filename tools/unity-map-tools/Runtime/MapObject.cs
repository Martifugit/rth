using UnityEngine;

namespace Rth.MapTools
{
    /// <summary>
    /// An interactive object (a workstation, later rocks, doors, altars) on the tile under this
    /// object's position. <see cref="objectType"/> must match an id in server/data/content/objects.json.
    /// Exported to &lt;map&gt;.objects.json; the client also uses this marker to know what was clicked.
    /// Usually paired with a <see cref="MapBlocker"/> so nobody stands inside it.
    /// </summary>
    public sealed class MapObject : MonoBehaviour
    {
        public string objectType = "skinning_rack";

        public Vector2Int Tile
        {
            get
            {
                Vector3 p = transform.position;
                return new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f);
            Vector2Int t = Tile;
            Gizmos.DrawWireCube(new Vector3(t.x + 0.5f, 0.6f, t.y + 0.5f), new Vector3(0.9f, 1.2f, 0.9f));
        }
    }
}
