using System.Collections.Generic;
using UnityEngine;

namespace Rth.MapTools
{
    /// <summary>
    /// The root under a region's <see cref="MapArea"/> that Sync World builds from the region's data
    /// (world/regions/&lt;id&gt;.json). Don't edit inside it by hand except to move, turn or delete a
    /// whole piece: saving the scene writes those back to the data, and the next sync redraws the
    /// rest. Add new things to the data, or under the <see cref="HandmadeRoot"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DataRoot : MonoBehaviour
    {
        [Tooltip("The pieces Sync World built here, so a piece missing on save counts as deleted.")]
        public List<string> builtIds = new List<string>();
    }
}
