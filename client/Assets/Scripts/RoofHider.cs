using System.Collections.Generic;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// A building's roof, with anything above the ground floor (upper storeys belong under the
    /// same object). It fades away while the local player is inside the building (whether roofs
    /// are switched on or not), stays away everywhere when roofs are switched off, and fades away
    /// when it stands on the line of sight from the camera to the player below 54.5° pitch (the
    /// 2004 rule, <see cref="CameraRules.RoofOnSight"/>; Settings can turn it off). Otherwise it
    /// fades like any other <see cref="Occluder"/> when it blocks the view.
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

        // The tiles from the camera to the player, walked once per frame for every roof.
        private static readonly List<(int x, int z)> Line = new List<(int x, int z)>();
        private static int _lineFrame = -1;
        private static bool _lineActive;
        private static Vector3 _eye;
        private static Vector3 _feet;

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
            _occluder.ForceHidden = inside || !ShowRoofs || OnSightLine(local);
        }

        private bool OnSightLine(EntityView local)
        {
            if (local == null || !PrepareLine(local)) return false;
            return CameraRules.RoofOnSight(interior.x, interior.y, interior.width, interior.height, _occluder.Bounds.max.y,
                _eye.x, _eye.y, _eye.z, _feet.x, _feet.y, _feet.z, Line);
        }

        private static bool PrepareLine(EntityView local)
        {
            if (_lineFrame == Time.frameCount) return _lineActive;
            _lineFrame = Time.frameCount;
            CameraRig rig = CameraRig.Current;
            _lineActive = rig != null && CameraRig.RoofSight && rig.EffectivePitch < CameraRules.RoofSightMaxPitch;
            if (!_lineActive) return false;
            _eye = rig.transform.position;
            _feet = local.transform.position + Vector3.up * 0.2f;
            CameraRules.LineTiles(_eye.x, _eye.z, _feet.x, _feet.z, Line);
            return true;
        }
    }
}
