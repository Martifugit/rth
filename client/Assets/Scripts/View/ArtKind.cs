using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Marks a model for the art validator (R'th > Art > Validate Art) and names its kind
    /// ("tree", "wall", "prop"...; client/Assets/Art/art-budgets.json). Optional: without it the
    /// validator guesses the kind from the name. No effect in game.
    /// </summary>
    public sealed class ArtKind : MonoBehaviour
    {
        public string kind = "prop";
    }
}
