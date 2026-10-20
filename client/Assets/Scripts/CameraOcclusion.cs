using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Keeps the player in sight: every frame, any <see cref="Occluder"/> whose bounds cross the
    /// lines from the camera to the player's head and feet is asked to fade. Lives on the camera.
    /// </summary>
    public sealed class CameraOcclusion : MonoBehaviour
    {
        public WorldView world;

        [Tooltip("Ignore anything this close to the player along the line (so standing beside a tree doesn't fade it).")]
        public float playerClearance = 0.6f;

        private void LateUpdate()
        {
            if (world == null) world = FindAnyObjectByType<WorldView>();
            EntityView local = world != null ? world.Local : null;
            Vector3 eye = transform.position;

            foreach (Occluder o in Occluder.All)
            {
                bool blocking = false;
                if (local != null)
                {
                    Vector3 feet = local.transform.position + Vector3.up * 0.2f;
                    Vector3 head = local.transform.position + Vector3.up * (local.Height + 0.1f);
                    blocking = Crosses(o.Bounds, eye, head) || Crosses(o.Bounds, eye, feet);
                }
                o.SetBlocking(blocking);
            }
        }

        private bool Crosses(Bounds bounds, Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            if (length < 0.01f) return false;
            return bounds.IntersectRay(new Ray(from, dir / length), out float hit) && hit < length - playerClearance;
        }
    }
}
