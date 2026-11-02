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

        [Tooltip("Tiles it walks between (absolute tiles), in order; empty: it wanders instead.")]
        public Vector2Int[] patrol = new Vector2Int[0];

        [Tooltip("\"loop\" (back to the first tile) or \"bounce\" (back the way it came).")]
        public string patrolMode = "loop";

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f);
            Vector3 p = transform.position;
            var center = new Vector3(Mathf.Floor(p.x) + 0.5f, 0.4f, Mathf.Floor(p.z) + 0.5f);
            Gizmos.DrawWireSphere(center, 0.4f);
            if (patrol != null && patrol.Length > 1)
                for (int i = 0; i < patrol.Length; i++)
                {
                    if (i == patrol.Length - 1 && patrolMode == "bounce") break;
                    Vector2Int a = patrol[i], b = patrol[(i + 1) % patrol.Length];
                    Gizmos.DrawLine(new Vector3(a.x + 0.5f, 0.3f, a.y + 0.5f), new Vector3(b.x + 0.5f, 0.3f, b.y + 0.5f));
                }
            if (wanderRadius > 0)
                Gizmos.DrawWireCube(new Vector3(center.x, 0.05f, center.z), new Vector3(wanderRadius * 2 + 1, 0.1f, wanderRadius * 2 + 1));
        }
    }
}
