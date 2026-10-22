using UnityEngine;

namespace Rth.MapTools
{
    /// <summary>
    /// The root under a region's <see cref="MapArea"/> for hand-made content: anything not worth
    /// turning into data (unique set dressing, a one-off mesh, light tweaks). Sync World never
    /// changes it; it reads its collision, objects and creatures into
    /// world/regions/&lt;id&gt;.handmade.json so the region still exports without Unity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandmadeRoot : MonoBehaviour
    {
    }
}
