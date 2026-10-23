using Rth.MapTools;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// How high the ground is under a point, for standing characters, items and markers on hills,
    /// stairs and upper floors. The server is flat; this is only how it looks. The ground is
    /// whatever collider the map exporter counts as ground (anything that is not a wall, blocker or
    /// object) on the same floor (<see cref="MapLevel"/>) as the thing standing on it.
    /// </summary>
    public static class GroundHeight
    {
        /// <summary>How far above the current surface to start looking down: more than a step or a kerb, less than a storey.</summary>
        public const float StepUp = 1.5f;

        private const float StartHeight = 600f;
        private static readonly RaycastHit[] Hits = new RaycastHit[48];

        /// <summary>
        /// Looks for the ground at (x, z) on <paramref name="plane"/>. With <paramref name="near"/>
        /// (a height the thing is standing at now) it takes the surface just under that, so a roof or
        /// a floor above is never mistaken for the ground; without it, the highest ground there is.
        /// False where there is none (the region is still streaming in, or it is a void).
        /// </summary>
        public static bool TryAt(float x, float z, int plane, out float y, float near = float.NaN)
        {
            float start = float.IsNaN(near) ? StartHeight : near + StepUp;
            int count = Physics.RaycastNonAlloc(new Vector3(x, start, z), Vector3.down, Hits, StartHeight * 2f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.point.y > best && IsGround(hit.collider, plane)) best = hit.point.y;
            }
            y = float.IsNegativeInfinity(best) ? (float.IsNaN(near) ? 0f : near) : best;
            return !float.IsNegativeInfinity(best);
        }

        /// <summary>The height of the ground at (x, z), or sea level / <paramref name="near"/> where there is none.</summary>
        public static float At(float x, float z, int plane, float near = float.NaN)
        {
            TryAt(x, z, plane, out float y, near);
            return y;
        }

        /// <summary>
        /// The height of the loaded regions' terrain under a point (a <see cref="MapTerrain"/>, or a
        /// region's data terrain drawn by <see cref="TileGround"/>), ignoring buildings and floors
        /// (0 where there is none). A lookup, not a raycast: cheap enough for the camera every frame.
        /// </summary>
        public static float TerrainAt(float x, float z) =>
            MapTerrain.TryHeightAt(x, z, out float h) ? h : TileGround.TryHeightAt(x, z, out float g) ? g : 0f;

        /// <summary>The terrain under the middle of a tile.</summary>
        public static float TerrainAtTile(int x, int y) => TerrainAt(x + 0.5f, y + 0.5f);

        /// <summary>The centre of a tile at ground height; false if there is no ground to find yet.</summary>
        public static bool TryCentre(Rth.Shared.World.TileCoord tile, int plane, out Vector3 centre, float near = float.NaN)
        {
            centre = TileMath.Center(tile);
            bool found = TryAt(centre.x, centre.z, plane, out float y, near);
            centre.y = y;
            return found;
        }

        public static Vector3 Centre(Rth.Shared.World.TileCoord tile, int plane, float near = float.NaN)
        {
            TryCentre(tile, plane, out Vector3 centre, near);
            return centre;
        }

        /// <summary>Whether a collider is walkable ground on this floor (the same test the exporter makes).</summary>
        public static bool IsGround(Collider collider, int plane)
        {
            if (collider.GetComponentInParent<EntityView>() != null) return false;
            if (collider.GetComponentInParent<MapWall>() != null || collider.GetComponentInParent<MapBlocker>() != null) return false;
            if (collider.GetComponentInParent<MapObject>() != null) return false;
            if (collider.GetComponent<GroundItemView>() != null) return false;
            return MapLevel.Of(collider) == plane;
        }
    }
}
