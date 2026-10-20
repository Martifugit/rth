using UnityEngine;

namespace Rth.MapTools
{
    /// <summary>
    /// A thin wall or fence that blocks movement across tile edges without filling the tiles.
    /// Its collider bounds should be long along one axis and sit on a whole-number grid line
    /// along the other; the exporter snaps it to the nearest line.
    /// </summary>
    public sealed class MapWall : MonoBehaviour
    {
    }
}
