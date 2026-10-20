using UnityEngine;

namespace Rth.MapTools
{
    /// <summary>
    /// A creature lives here: the tile under this object's position. Exported to
    /// server/data/maps/&lt;map&gt;.spawns.json. The id must match one in server/data/content/npcs.json.
    /// </summary>
    public sealed class MapNpcSpawn : MonoBehaviour
    {
        public string npcId = "fen_rat";

        [Tooltip("Tiles it may wander from here. -1 uses the creature's default.")]
        public int wanderRadius = -1;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f);
            Vector3 p = transform.position;
            var center = new Vector3(Mathf.Floor(p.x) + 0.5f, 0.4f, Mathf.Floor(p.z) + 0.5f);
            Gizmos.DrawWireSphere(center, 0.4f);
            if (wanderRadius > 0)
                Gizmos.DrawWireCube(new Vector3(center.x, 0.05f, center.z), new Vector3(wanderRadius * 2 + 1, 0.1f, wanderRadius * 2 + 1));
        }
    }
}
