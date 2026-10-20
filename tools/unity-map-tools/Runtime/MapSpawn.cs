using UnityEngine;

namespace Rth.MapTools
{
    /// <summary>Where new players appear: the tile under this object's position.</summary>
    public sealed class MapSpawn : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Vector3 p = transform.position;
            Gizmos.DrawWireCube(new Vector3(Mathf.Floor(p.x) + 0.5f, 0.5f, Mathf.Floor(p.z) + 0.5f), new Vector3(1f, 1f, 1f));
        }
    }
}
