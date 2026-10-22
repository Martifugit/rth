using UnityEngine;
using UnityEngine.InputSystem;

namespace Rth.Client
{
    /// <summary>
    /// Orbit camera around the local player: arrow keys or middle-drag to rotate, scroll to zoom.
    /// Over high ground it tilts up (the terrain lift, docs/world-scale.md section 5) without
    /// touching the pitch the player chose. Its limits come from a <see cref="CameraProfile"/>,
    /// picked in Settings.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        private const string ProfilePref = "rth.cameraProfile";
        private const string LiftPref = "rth.cameraLift";
        private const string RoofSightPref = "rth.roofSight";

        public Transform target;
        public Vector3 idleFocus = new Vector3(16f, 0f, 16f);

        public float yaw = 30f;
        public float pitch = 45f;
        public float distance = 14f;
        public float minPitch = 20f;
        public float maxPitch = 80f;
        public float minDistance = 5f;
        public float maxDistance = 30f;
        public float keyRotateSpeed = 90f;
        public float dragRotateSpeed = 0.25f;
        public float followSharpness = 12f;

        private static readonly System.Func<int, int, float> TileHeight = GroundHeight.AtTile;

        private Vector3 _focus;
        private float _floor;
        private CameraProfile _applied;

        private static int _profile = -1;
        private static int _lift = -1;
        private static int _roofSight = -1;

        /// <summary>The camera drawing the world, for rules that depend on where it is (roofs).</summary>
        public static CameraRig Current { get; private set; }

        /// <summary>The pitch in use this frame: the player's, raised by the terrain lift.</summary>
        public float EffectivePitch { get; private set; }

        /// <summary>The point the camera looks at (about the player's chest).</summary>
        public Vector3 Focus => _focus;

        /// <summary>Which limits the camera uses (saved between sessions). "Today" until the owner decides.</summary>
        public static CameraProfile Profile
        {
            get
            {
                if (_profile < 0) _profile = PlayerPrefs.GetInt(ProfilePref, (int)CameraProfile.Today);
                return (CameraProfile)_profile;
            }
            set
            {
                _profile = (int)value;
                PlayerPrefs.SetInt(ProfilePref, _profile);
            }
        }

        /// <summary>Tilt up over high ground near the player.</summary>
        public static bool TerrainLift
        {
            get => Pref(ref _lift, LiftPref);
            set => SetPref(ref _lift, LiftPref, value);
        }

        /// <summary>Hide roofs on the line of sight from the camera to the player (see <see cref="RoofHider"/>).</summary>
        public static bool RoofSight
        {
            get => Pref(ref _roofSight, RoofSightPref);
            set => SetPref(ref _roofSight, RoofSightPref, value);
        }

        private static bool Pref(ref int cache, string key)
        {
            if (cache < 0) cache = PlayerPrefs.GetInt(key, 1);
            return cache == 1;
        }

        private static void SetPref(ref int cache, string key, bool value)
        {
            cache = value ? 1 : 0;
            PlayerPrefs.SetInt(key, cache);
        }

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        private void Start()
        {
            _focus = target != null ? target.position : idleFocus;
            // "Today" is what the scene was saved with, so only another profile overwrites it.
            _applied = CameraProfile.Today;
            if (Profile != CameraProfile.Today) Apply(Profile);
            _floor = minPitch;
            EffectivePitch = pitch;
        }

        private void Apply(CameraProfile profile)
        {
            CameraLimits limits = CameraLimits.For(profile);
            distance = limits.distance;
            minDistance = limits.minDistance;
            maxDistance = limits.maxDistance;
            pitch = limits.pitch;
            minPitch = limits.minPitch;
            maxPitch = limits.maxPitch;
            _floor = minPitch;
            _applied = profile;
        }

        private void LateUpdate()
        {
            if (Profile != _applied) Apply(Profile);
            ReadInput();

            Vector3 ground = target != null ? target.position : idleFocus;
            Vector3 goal = ground + Vector3.up;
            _focus = Vector3.Lerp(_focus, goal, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));

            float floorTarget = minPitch;
            if (TerrainLift)
            {
                float here = GroundHeight.At(ground.x, ground.z);
                float highest = CameraRules.HighestNear(TileHeight, Mathf.FloorToInt(ground.x), Mathf.FloorToInt(ground.z));
                floorTarget = CameraRules.LiftFloor(highest - here, minPitch, maxPitch);
            }
            _floor = CameraRules.EaseFloor(Mathf.Max(_floor, minPitch), floorTarget, Time.deltaTime);
            EffectivePitch = CameraRules.EffectivePitch(pitch, _floor, maxPitch);

            Quaternion orbit = Quaternion.Euler(EffectivePitch, yaw, 0f);
            transform.position = _focus + orbit * new Vector3(0f, 0f, -distance);
            transform.rotation = orbit;
        }

        private void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            float dt = Time.deltaTime;

            if (keyboard != null && !Ui.UiRoot.IsTyping)
            {
                if (keyboard.leftArrowKey.isPressed) yaw += keyRotateSpeed * dt;
                if (keyboard.rightArrowKey.isPressed) yaw -= keyRotateSpeed * dt;
                if (keyboard.upArrowKey.isPressed) pitch += keyRotateSpeed * 0.5f * dt;
                if (keyboard.downArrowKey.isPressed) pitch -= keyRotateSpeed * 0.5f * dt;
            }

            if (mouse != null)
            {
                if (mouse.middleButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * dragRotateSpeed;
                    pitch -= delta.y * dragRotateSpeed;
                }
                // The wheel zooms only over the world; over a window it scrolls the window.
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !Ui.UiRoot.IsPointerOverUi(mouse.position.ReadValue()) && !Ui.UiRoot.IsModalOpen)
                    distance *= scroll > 0f ? 0.9f : 1.1f;
            }

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }

    /// <summary>The sets of camera limits to compare (docs/world-scale.md, decision D4).</summary>
    public enum CameraProfile
    {
        /// <summary>The limits the Game scene was built with.</summary>
        Today,
        /// <summary>The proposal: start at 12 and 35°, zoom 6 to 30, pitch 22° to 67°.</summary>
        Proposed,
    }

    public struct CameraLimits
    {
        public float distance, minDistance, maxDistance, pitch, minPitch, maxPitch;

        public static CameraLimits For(CameraProfile profile) => profile == CameraProfile.Proposed
            ? new CameraLimits { distance = 12f, minDistance = 6f, maxDistance = 30f, pitch = 35f, minPitch = 22f, maxPitch = 67f }
            : new CameraLimits { distance = 14f, minDistance = 5f, maxDistance = 30f, pitch = 45f, minPitch = 20f, maxPitch = 80f };
    }
}
