using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Keeps the player in sight: every frame, any <see cref="Occluder"/> that crosses the
    /// lines from the camera to the player's head and feet is asked to fade (tested against
    /// what it draws, so the air under a canopy beside you doesn't count). One with a guard
    /// radius also fades when it hides the ground around the player (in an arena, what happens
    /// near you matters as much as you do). Lives on the camera.
    /// </summary>
    public sealed class CameraOcclusion : MonoBehaviour
    {
        public WorldView world;

        [Tooltip("Ignore anything this close to the player along the line (so standing beside a tree doesn't fade it).")]
        public float playerClearance = 0.6f;

        private void LateUpdate()
        {
            if (world == null) world = FindAnyObjectByType<WorldView>();
            // A flying camera is nowhere near the player: nothing is in the way.
            EntityView local = world != null && !FlyCamera.Active ? world.Local : null;
            Vector3 eye = transform.position;

            foreach (Occluder o in Occluder.All)
            {
                bool blocking = false;
                if (local != null)
                {
                    Vector3 feet = local.transform.position + Vector3.up * 0.2f;
                    Vector3 head = local.transform.position + Vector3.up * (local.Height + 0.1f);
                    blocking = Crosses(o, eye, head) || Crosses(o, eye, feet);
                    for (int i = 0; !blocking && o.guardRadius > 0f && i < GuardPoints; i++)
                    {
                        float a = i * Mathf.PI * 2f / GuardPoints;
                        Vector3 around = feet + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * o.guardRadius + Vector3.up * 0.8f;
                        blocking = Crosses(o, eye, around);
                    }
                }
                o.SetBlocking(blocking);
            }
        }

        private const int GuardPoints = 12;

        private bool Crosses(Occluder o, Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            if (length < 0.01f) return false;
            return o.Hits(new Ray(from, dir / length), length - playerClearance);
        }
    }
}
