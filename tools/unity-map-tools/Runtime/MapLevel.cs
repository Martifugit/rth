using System.Collections.Generic;
using UnityEngine;

namespace Rth.MapTools
{
    /// <summary>
    /// Puts everything under this object on a floor (a plane): 0 is the ground, 1 the floor above.
    /// The exporter writes one plane of collision per level: a tile is walkable on a level only where
    /// that level has a floor collider, and the walls, blockers, objects and creature spawns under
    /// this object belong to that level. Anything outside a MapLevel is on the region's own plane.
    ///
    /// Terraces and hillsides are not levels (they are plane 0 at different heights); a level is
    /// for something standing over the ground at the same tile: an upper floor, a gallery, a
    /// footbridge over a street, a tower room.
    ///
    /// At runtime the client switches off a level that is <see cref="hiddenFromBelow"/> while the
    /// local player is on a lower floor, so the floor above never hides the room they are in.
    /// </summary>
    public sealed class MapLevel : MonoBehaviour
    {
        /// <summary>The highest floor a region may have (the server supports four planes).</summary>
        public const int MaxLevel = 3;

        [Range(0, MaxLevel)]
        [Tooltip("The floor: 0 is the ground, 1 the next one up.")]
        public int level = 1;

        [Tooltip("Draw it only while the player is on this floor or above (upper floors, their furniture). Turn off for a building's outer walls, which must always show.")]
        public bool hiddenFromBelow = true;

        /// <summary>Every level in the loaded scenes, for the client to switch (added on Awake, so switched-off ones stay listed).</summary>
        public static readonly List<MapLevel> All = new List<MapLevel>();

        private void Awake() => All.Add(this);

        private void OnDestroy() => All.Remove(this);

        /// <summary>The level something belongs to: its nearest MapLevel above it, or <paramref name="fallback"/>.</summary>
        public static int Of(Component thing, int fallback = 0)
        {
            MapLevel owner = thing != null ? thing.GetComponentInParent<MapLevel>(true) : null;
            return owner != null ? owner.level : fallback;
        }
    }
}
