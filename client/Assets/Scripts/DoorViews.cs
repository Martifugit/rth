using System.Collections.Generic;
using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Swings the doors the server says stand open (the "doors" server state, DoorSystem) and shuts
    /// the rest. The leaves are <see cref="DoorLeaf"/>s in the streamed region scenes.
    /// </summary>
    public sealed class DoorViews : MonoBehaviour
    {
        public GameSession session;

        private int _version = -1;
        private float _nextScan;
        private readonly List<DoorLeaf> _leaves = new List<DoorLeaf>();

        private void Update()
        {
            if (session == null || !session.IsInGame) return;
            // Regions stream in and out, so look for their doors again now and then.
            bool rescan = Time.unscaledTime >= _nextScan;
            if (rescan)
            {
                _nextScan = Time.unscaledTime + 2f;
                _leaves.Clear();
                _leaves.AddRange(FindObjectsByType<DoorLeaf>(FindObjectsInactive.Exclude));
            }
            if (!rescan && session.StateVersion == _version) return;
            _version = session.StateVersion;

            var open = new HashSet<(int, int, int, string)>();
            foreach (DoorState d in session.ServerState<List<DoorState>>(DoorDef.StateKey)) open.Add((d.Plane, d.X, d.Y, d.Edge));
            foreach (DoorLeaf leaf in _leaves)
                if (leaf != null) leaf.open = open.Contains((leaf.plane, leaf.x, leaf.y, leaf.edge));
        }

        /// <summary>Whether the door on this tile stands open, as the server last said.</summary>
        public static bool IsOpen(GameSession session, int plane, int x, int y)
        {
            foreach (DoorState d in session.ServerState<List<DoorState>>(DoorDef.StateKey))
                if (d.Plane == plane && d.X == x && d.Y == y) return true;
            return false;
        }
    }
}
