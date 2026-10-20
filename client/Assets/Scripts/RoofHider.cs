using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Hides a building's roof while the local player is inside it, so you can see in (the classic
    /// trick). Purely visual: the server doesn't know roofs exist.
    /// </summary>
    public sealed class RoofHider : MonoBehaviour
    {
        /// <summary>Interior tiles: x, y (south-west corner), width, height.</summary>
        public RectInt interior;

        private static WorldView _world;
        private Renderer[] _renderers;
        private bool _hidden;

        private void Start() => _renderers = GetComponentsInChildren<Renderer>();

        private void Update()
        {
            if (_world == null) _world = FindAnyObjectByType<WorldView>();
            EntityView local = _world != null ? _world.Local : null;
            bool inside = local != null && interior.Contains(new Vector2Int(local.Tile.X, local.Tile.Y));
            if (inside == _hidden) return;
            _hidden = inside;
            foreach (Renderer r in _renderers) r.enabled = !inside;
        }
    }
}
