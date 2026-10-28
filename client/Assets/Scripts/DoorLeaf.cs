using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// A door's leaf, on its hinge: swings <see cref="openYaw"/> degrees about it while the door
    /// stands open. Its door is the object on tile (<see cref="x"/>, <see cref="y"/>) closing that
    /// tile's <see cref="edge"/>; <see cref="DoorViews"/> opens and shuts it as the server says.
    /// </summary>
    public sealed class DoorLeaf : MonoBehaviour
    {
        public int plane;
        public int x;
        public int y;
        public string edge = "";
        /// <summary>The swing that takes the leaf into the room (+90 or -90 about the hinge).</summary>
        public float openYaw = 90f;

        [System.NonSerialized] public bool open;
        private Quaternion _shut;
        private bool _started;

        private void Start()
        {
            _shut = transform.localRotation;
            _started = true;
        }

        private void Update()
        {
            if (!_started) return;
            Quaternion want = open ? _shut * Quaternion.Euler(0f, openYaw, 0f) : _shut;
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, want, 360f * Time.deltaTime);
        }
    }
}
