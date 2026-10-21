using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Rth.Client.Dev
{
    /// <summary>
    /// A simulated mouse for scripted interface checks from the Editor CLI. Call
    /// <see cref="Pointer"/> to say where it is and whether the left button is down; the driver
    /// sends that state from the game's own frame. Call <see cref="Stop"/> when done: it removes
    /// the device and restores the input settings, so the real mouse works again (leaving Play
    /// does the same). A development tool: nothing in the game uses it, and it never disables
    /// real devices.
    /// </summary>
    public sealed class InputDriver : MonoBehaviour
    {
        private static InputDriver _instance;
        private Mouse _mouse;
        private Vector2 _position;
        private bool _down;
        private InputSettings.BackgroundBehavior _background;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInput;

        /// <summary>Where the game last saw the simulated pointer, and whether it was pressed.</summary>
        public static string Seen { get; private set; } = "";

        private static InputDriver Instance
        {
            get
            {
                if (_instance == null) _instance = new GameObject("Input Driver").AddComponent<InputDriver>();
                return _instance;
            }
        }

        private void Awake()
        {
            // Driven from the CLI, the Game view rarely has focus; without these the Input System
            // drops the simulated device's events as background input. Restored in OnDestroy.
            _background = InputSystem.settings.backgroundBehavior;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        /// <summary>Puts the simulated pointer at a screen point (bottom-left origin), left button up or down.</summary>
        public static void Pointer(Vector2 at, bool down)
        {
            InputDriver driver = Instance;
            driver._position = at;
            driver._down = down;
        }

        /// <summary>Ends simulation: removes the simulated mouse and gives the real one back.</summary>
        public static void Stop()
        {
            if (_instance != null) Destroy(_instance.gameObject);
            _instance = null;
        }

        private void Update()
        {
            if (_mouse == null || !_mouse.added) _mouse = InputSystem.AddDevice<Mouse>("SimulatedMouse");
            _mouse.MakeCurrent();
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = _position, buttons = (ushort)(_down ? 1 : 0) });
            Seen = _mouse.position.ReadValue() + (_mouse.leftButton.isPressed ? " down" : " up");
        }

        private void OnDestroy()
        {
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            _mouse = null;
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is Mouse real && real.enabled)
                {
                    real.MakeCurrent();
                    break;
                }
            }
        }
    }
}
