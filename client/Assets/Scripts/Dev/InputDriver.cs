using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Rth.Client.Dev
{
    /// <summary>
    /// Plays simulated mouse input through the Input System so interface behaviour that needs a
    /// real pointer (dragging, clicking) can be tested from scripts and the Editor CLI. Call
    /// <see cref="Pointer"/> to say where the pointer is and whether the left button is down; the
    /// driver sends that state from the game's own frame (events sent from Editor callbacks land
    /// in the Editor's input state, which the game never reads). A development tool: nothing in
    /// the game uses it.
    /// </summary>
    public sealed class InputDriver : MonoBehaviour
    {
        private static InputDriver _instance;
        private Mouse _mouse;
        private Vector2 _position;
        private bool _down;

        /// <summary>Where the game last saw the simulated pointer, and whether it was pressed.</summary>
        public static string Seen { get; private set; } = "";

        private static InputDriver Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Driven from the CLI, the Game view rarely has focus; without these the Input
                    // System drops the simulated device's events as background input.
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    _instance = new GameObject("Input Driver").AddComponent<InputDriver>();
                }
                return _instance;
            }
        }

        /// <summary>Puts the simulated pointer at a screen point (bottom-left origin), left button up or down.</summary>
        public static void Pointer(Vector2 at, bool down)
        {
            InputDriver driver = Instance;
            driver._position = at;
            driver._down = down;
        }

        private void Update()
        {
            if (_mouse == null || !_mouse.added) _mouse = InputSystem.AddDevice<Mouse>("SimulatedMouse");
            // While driving, the real mouse must not become Mouse.current (the game reads that).
            foreach (InputDevice device in InputSystem.devices.ToArray())
                if (device is Mouse other && other != _mouse && other.enabled) InputSystem.DisableDevice(other);
            _mouse.MakeCurrent();
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = _position, buttons = (ushort)(_down ? 1 : 0) });
            Seen = _mouse.position.ReadValue() + (_mouse.leftButton.isPressed ? " down" : " up");
        }

        private void OnDestroy()
        {
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            foreach (InputDevice device in InputSystem.devices.ToArray())
                if (device is Mouse other && !other.enabled) InputSystem.EnableDevice(other);
        }
    }
}
