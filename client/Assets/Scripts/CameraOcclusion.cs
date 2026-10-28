using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Keeps the player in sight: every frame, any <see cref="Occluder"/> that crosses the
    /// lines from the camera to the player's head, chest and feet (tested against what it draws, so
    /// the air under a canopy beside you doesn't count) opens a soft hole round them. One with a
    /// guard radius also opens when it hides the ground around the player (in an arena, what
    /// happens near you matters as much as you do). It also tells the hole's shader where the
    /// player is and how big to make it: a circle a little taller than they stand on screen, so it
    /// grows and shrinks with the zoom. Lives on the camera.
    /// </summary>
    public sealed class CameraOcclusion : MonoBehaviour
    {
        public WorldView world;

        [Tooltip("Ignore anything this close to the player along the line (so standing beside a tree doesn't fade it).")]
        public float playerClearance = 0.6f;

        [Tooltip("The hole's radius against the player's height on screen.")]
        public float holeSize = 0.8f;

        private static readonly int CutPoint = Shader.PropertyToID("_RthCutPoint");
        private static readonly int CutSize = Shader.PropertyToID("_RthCutSize");
        private Camera _camera;

        private void LateUpdate()
        {
            if (world == null) world = FindAnyObjectByType<WorldView>();
            // A flying camera is nowhere near the player: nothing is in the way.
            EntityView local = world != null && !FlyCamera.Active ? world.Local : null;
            Vector3 eye = transform.position;
            if (local != null) TellShader(local);

            foreach (Occluder o in Occluder.All)
            {
                bool blocking = false;
                if (local != null)
                {
                    Vector3 feet = local.transform.position + Vector3.up * 0.2f;
                    Vector3 head = local.transform.position + Vector3.up * (local.Height + 0.1f);
                    Vector3 chest = local.transform.position + Vector3.up * (local.Height * 0.55f);
                    blocking = Crosses(o, eye, head) || Crosses(o, eye, chest) || Crosses(o, eye, feet);
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

        /// <summary>The hole as last drawn: round this point (the player's chest), this radius (a fraction of the screen's height).</summary>
        private static Vector3 _holePoint;
        private static float _holeRadius;
        private static Camera _holeCamera;

        /// <summary>
        /// Whether a click went through the hole an occluder has opened, so it should reach what is
        /// seen there instead (the ground, a door beyond the wall): the hit is on an open occluder, in
        /// front of the player, inside the hole's solid middle. A hidden roof lets every click through.
        /// </summary>
        public static bool SeesThrough(RaycastHit hit, Vector2 pointer)
        {
            Occluder o = hit.collider != null ? hit.collider.GetComponentInParent<Occluder>() : null;
            if (o == null || o.Openness < 0.5f) return false;
            if (o.ForceHidden) return true;
            if (_holeCamera == null || _holeRadius <= 0f) return false;
            Vector3 player = _holeCamera.WorldToViewportPoint(_holePoint);
            Vector3 at = _holeCamera.ScreenToViewportPoint(pointer);
            if (player.z <= 0f || _holeCamera.WorldToViewportPoint(hit.point).z > player.z - 0.4f) return false;   // behind the player: solid
            Vector2 d = new Vector2((at.x - player.x) * _holeCamera.aspect, at.y - player.y);
            return d.magnitude < _holeRadius * 0.6f;                                                           // the open middle, not the dithered rim
        }

        private void TellShader(EntityView local)
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            Vector3 feet = local.transform.position;
            Vector3 head = feet + Vector3.up * local.Height;
            Shader.SetGlobalVector(CutPoint, feet + Vector3.up * (local.Height * 0.55f));
            float tall = 0.15f;
            if (_camera != null)
                tall = Mathf.Abs(_camera.WorldToViewportPoint(head).y - _camera.WorldToViewportPoint(feet).y);
            float radius = Mathf.Clamp(tall * holeSize, 0.05f, 0.35f);
            Shader.SetGlobalVector(CutSize, new Vector4(radius, 0f, 0f, 0f));
            _holePoint = feet + Vector3.up * (local.Height * 0.55f);
            _holeRadius = radius;
            _holeCamera = _camera;
        }

        private bool Crosses(Occluder o, Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            if (length < 0.01f) return false;
            return o.Hits(new Ray(from, dir / length), length - playerClearance);
        }
    }
}
