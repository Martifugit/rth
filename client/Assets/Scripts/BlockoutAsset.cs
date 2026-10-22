using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Marks a piece of a blockout as the stand-in for an asset that still has to be made: a
    /// townhouse, a statue, a market stall. Builders tag what they place (WorldKit.Asset), and
    /// saving a region writes the tally of every tagged asset, with notes and sizes, to
    /// docs/regions/&lt;region&gt;-assets.md. Does nothing in play.
    /// </summary>
    public sealed class BlockoutAsset : MonoBehaviour
    {
        [Tooltip("What asset this stands in for: \"cruor/townhouse_narrow\". Shared by every copy.")]
        public string id = "";

        [TextArea(2, 6)]
        [Tooltip("What the artist needs to know: what it is, what it's made of, what it must read as.")]
        public string note = "";
    }
}
