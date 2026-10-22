using Rth.MapTools;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Shows the floors the local player can be on: a level marked <see cref="MapLevel.hiddenFromBelow"/>
    /// (an upper floor and what is in it) is switched off while they stand on a lower floor, so the
    /// floor above never hides the room they are in. Purely visual: the server keeps a plane of
    /// collision per floor and sends each player only the people on theirs.
    /// </summary>
    public sealed class LevelDisplay : MonoBehaviour
    {
        private WorldView _world;
        private int _plane;

        private void Update()
        {
            if (_world == null) _world = FindAnyObjectByType<WorldView>();
            EntityView local = _world != null ? _world.Local : null;
            // Between being removed and re-added (a climb) there is no local player: keep the last floor.
            if (local != null) _plane = local.Plane;

            foreach (MapLevel level in MapLevel.All)
            {
                if (level == null) continue;
                bool show = !level.hiddenFromBelow || level.level <= _plane;
                if (level.gameObject.activeSelf != show) level.gameObject.SetActive(show);
            }
        }
    }
}
