using Rth.MapTools;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// How high the ground is under a point, for standing things on it and for the camera. It
    /// comes from the loaded regions' <see cref="MapTerrain"/>s; where there is none, the ground
    /// is at 0, as every region was before terrain. Purely visual: the server has no heights.
    /// </summary>
    public static class GroundHeight
    {
        public static float At(float x, float z) => MapTerrain.TryHeightAt(x, z, out float h) ? h : 0f;

        /// <summary>The ground under the middle of a tile.</summary>
        public static float AtTile(int x, int y) => At(x + 0.5f, y + 0.5f);

        /// <summary>The same point, moved up or down onto the ground.</summary>
        public static Vector3 OnGround(Vector3 p)
        {
            p.y = At(p.x, p.z);
            return p;
        }
    }
}
