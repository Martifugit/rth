using UnityEngine;
using UnityEngine.InputSystem;

namespace Rth.Client
{
    /// <summary>
    /// Orbit camera around the local player: arrow keys or middle-drag to rotate, scroll to zoom.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
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

        private Vector3 _focus;

        private void Start()
        {
            _focus = target != null ? target.position : idleFocus;
        }

        private void LateUpdate()
        {
            ReadInput();

            Vector3 goal = (target != null ? target.position : idleFocus) + Vector3.up;
            _focus = Vector3.Lerp(_focus, goal, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));

            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
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
}
