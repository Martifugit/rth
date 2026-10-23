using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rth.Client
{
    /// <summary>
    /// The dev "::fly" camera: free of the player, fast, for judging a place's scale from anywhere.
    /// The server turns it on and off (<see cref="FlyState"/>, so it is a dev command like the
    /// others); while it is on, the world streams around the camera, the draw distance is at least
    /// "Very far", and this owns the keys and clicks: WASD to move, Space/E and Ctrl/Q to rise and
    /// sink, Shift for four times the speed, the wheel to set the speed, right- or middle-drag and
    /// the arrows to look. A left-click on the ground lands the player there (<c>::land x y</c>);
    /// Escape (with no window open) or <c>::fly</c> stops. Lives on the camera beside <see cref="CameraRig"/>,
    /// which takes over again, easing back to the player, when flying stops.
    /// </summary>
    [DefaultExecutionOrder(-50)] // before UiRoot, so Escape closes a window rather than also landing
    public sealed class FlyCamera : MonoBehaviour
    {
        private const string SpeedPref = "rth.flySpeed";

        public GameSession session;

        public float lookSpeed = 0.2f;
        public float keyTurnSpeed = 90f;

        /// <summary>Whether the camera is flying (the camera rig, the streamer and the input stand aside).</summary>
        public static bool Active { get; private set; }

        private float _yaw, _pitch;
        private float _speed;
        private bool _asked; // a ::fly or ::land is on its way; don't send another until the server answers
        private GUIStyle _style;

        private void OnDisable() => Active = false;

        private void Update()
        {
            bool want = session != null && session.IsInGame && session.ServerState<FlyState>(FlyState.Key).On;
            if (want != Active)
            {
                Active = want;
                _asked = false;
                if (want) Begin();
            }
            if (!Active) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;
            bool typing = Ui.UiRoot.IsTyping;

            Vector3 move = Vector3.zero;
            if (keyboard != null && !typing)
            {
                if (keyboard.wKey.isPressed) move.z += 1f;
                if (keyboard.sKey.isPressed) move.z -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed) move.x -= 1f;
                if (keyboard.spaceKey.isPressed || keyboard.eKey.isPressed) move.y += 1f;
                if (keyboard.leftCtrlKey.isPressed || keyboard.qKey.isPressed) move.y -= 1f;
                if (keyboard.leftArrowKey.isPressed) _yaw -= keyTurnSpeed * dt;
                if (keyboard.rightArrowKey.isPressed) _yaw += keyTurnSpeed * dt;
                if (keyboard.upArrowKey.isPressed) _pitch -= keyTurnSpeed * 0.5f * dt;
                if (keyboard.downArrowKey.isPressed) _pitch += keyTurnSpeed * 0.5f * dt;
                if (keyboard.escapeKey.wasPressedThisFrame && !Ui.UiRoot.IsModalOpen) Ask("::fly");
            }

            Vector2 pointer = Vector2.zero;
            bool overUi = true;
            if (mouse != null)
            {
                pointer = mouse.position.ReadValue();
                overUi = Ui.UiRoot.IsPointerOverUi(pointer);
                if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    _yaw += delta.x * lookSpeed;
                    _pitch -= delta.y * lookSpeed;
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !overUi)
                {
                    _speed = Mathf.Clamp(_speed * (scroll > 0f ? 1.25f : 0.8f), 2f, 500f);
                    PlayerPrefs.SetFloat(SpeedPref, _speed);
                }
                if (mouse.leftButton.wasPressedThisFrame && !overUi) Land(pointer);
            }
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);

            // Forward and sideways stay level, whatever the pitch, so looking down doesn't dive.
            Quaternion heading = Quaternion.Euler(0f, _yaw, 0f);
            float speed = _speed * (keyboard != null && keyboard.leftShiftKey.isPressed ? 4f : 1f);
            Vector3 p = transform.position + (heading * new Vector3(move.x, 0f, move.z) + Vector3.up * move.y) * speed * dt;
            p.y = Mathf.Max(p.y, GroundHeight.TerrainAt(p.x, p.z) + 0.5f);
            transform.SetPositionAndRotation(p, Quaternion.Euler(_pitch, _yaw, 0f));
        }

        private void Begin()
        {
            Vector3 e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
            _speed = PlayerPrefs.GetFloat(SpeedPref, 20f);
        }

        /// <summary>A click on the ground: the player stands there and flying stops.</summary>
        private void Land(Vector2 pointer)
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null) return;
            RaycastHit[] hits = Physics.RaycastAll(cam.ScreenPointToRay(pointer), 5000f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                if (h.collider.GetComponentInParent<EntityView>() != null) continue;
                Ask("::land " + Mathf.FloorToInt(h.point.x) + " " + Mathf.FloorToInt(h.point.z));
                return;
            }
        }

        private void Ask(string command)
        {
            if (_asked) return;
            _asked = true;
            session.Say(command);
        }

        private void OnGUI()
        {
            if (!Active) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
                _style.normal.textColor = new Color(1f, 0.9f, 0.6f);
            }
            Vector3 p = transform.position;
            string text = "Flying  " + Mathf.RoundToInt(_speed) + " tiles/s  at (" + Mathf.FloorToInt(p.x) + ", " + Mathf.FloorToInt(p.z) + "), "
                + Mathf.RoundToInt(p.y - GroundHeight.TerrainAt(p.x, p.z)) + " up\n"
                + "WASD move   Space/Ctrl up/down   Shift fast   wheel speed   right-drag look   click ground: land   Esc: stop";
            var rect = new Rect(0f, 8f, Screen.width, 44f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _style);
            GUI.color = Color.white;
            GUI.Label(rect, text, _style);
        }
    }
}
