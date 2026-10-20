using UnityEngine;

namespace Rth.MapTools
{
    /// <summary>
    /// Nobody can stand on the tiles this object covers: rocks, trees, water, furniture.
    /// The footprint comes from the colliders on this object and its children.
    /// </summary>
    public sealed class MapBlocker : MonoBehaviour
    {
    }
}
