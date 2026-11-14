using UnityEngine;

namespace Rth.MapTools
{
    /// <summary>
    /// A water surface (a lake's, a river's): drawn over ground painted with a water layer, which is
    /// what blocks walking. The surface is only how the water looks, so the map exporter never takes
    /// it for ground.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapWater : MonoBehaviour
    {
    }
}