using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// A building's roof. It fades away while the local player is inside the building (whether
    /// roofs are switched on or not), stays away everywhere when roofs are switched off, and
    /// otherwise fades like any other <see cref="Occluder"/> when it blocks the view.
    /// Purely visual: the server doesn't know roofs exist.
    /// </summary>
    [RequireComponent(typeof(Occluder))]
    public sealed class RoofHider : MonoBehaviour
    {
        private const string Pref = "rth.roofs";

        /// <summary>Interior tiles: x, y (south-west corner), width, height.</summary>
        public RectInt interior;

        private static WorldView _world;
        private static int _showRoofs = -1;
        private Occluder _occluder;

        /// <summary>The player's roof setting (saved between sessions).</summary>
        public static bool ShowRoofs
        {
            get
            {
                if (_showRoofs < 0) _showRoofs = PlayerPrefs.GetInt(Pref, 1);
                return _showRoofs == 1;
            }
            set
            {
                _showRoofs = value ? 1 : 0;
                PlayerPrefs.SetInt(Pref, _showRoofs);
            }
        }

        private void Awake() => _occluder = GetComponent<Occluder>();

        private void Update()
        {
            if (_world == null) _world = FindAnyObjectByType<WorldView>();
            EntityView local = _world != null ? _world.Local : null;
            bool inside = local != null && interior.Contains(new Vector2Int(local.Tile.X, local.Tile.Y));
            _occluder.ForceHidden = inside || !ShowRoofs;
        }
    }
}
