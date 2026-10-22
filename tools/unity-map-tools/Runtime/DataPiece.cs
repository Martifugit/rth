using UnityEngine;

namespace Rth.MapTools
{
    /// <summary>
    /// One piece of a region's data, drawn by Sync World. Remembers where it was drawn, so a move
    /// or turn made in the Editor can be written back to the data when the scene is saved.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DataPiece : MonoBehaviour
    {
        public string pieceId = "";
        [Tooltip("Where Sync World put it: a move by whole tiles from here is written back on save.")]
        public Vector3 placedPosition;
        public float placedYaw;
        [Tooltip("Placed kinds (trees, rocks, props, creatures, the spawn) can be moved and turned; area kinds only moved.")]
        public bool canTurn;
    }
}
