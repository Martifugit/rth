using System;
using System.Collections.Generic;
using Rth.Client.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client
{
    /// <summary>
    /// The right-click menu: a title and a list of options at the pointer, in the classic style.
    /// Anything can open one (the world, item slots, bank and shop slots, skill tiles). Picking an
    /// option or clicking anywhere else closes it. Drawn in the interface document (<see cref="UiRoot"/>).
    /// </summary>
    public sealed class PopupMenu : MonoBehaviour
    {
        private static PopupMenu _instance;

        private VisualElement _menu;
        private int _openedFrame;

        public static bool IsOpen => _instance != null && _instance._menu != null && _instance._menu.parent != null;

        private void Awake() => _instance = this;
        private void OnEnable() => _instance = this;

        /// <summary>Opens a menu at a GUI position (top-left origin, screen pixels).</summary>
        public static void Open(Vector2 guiPosition, string title, List<(string Label, Action Action)> options)
        {
            UiRoot ui = UiRoot.Instance;
            if (_instance == null || ui == null || options.Count == 0) return;
            _instance.Show(ui, guiPosition, title, options);
        }

        public static void Close()
        {
            if (_instance != null && _instance._menu != null) _instance._menu.RemoveFromHierarchy();
        }

        private void Show(UiRoot ui, Vector2 guiPosition, string title, List<(string Label, Action Action)> options)
        {
            Close();
            _openedFrame = Time.frameCount;
            _menu = new VisualElement();
            _menu.AddToClassList("menu");
            Label header = UiKit.Text(_menu, title, "menu__title");
            header.pickingMode = PickingMode.Ignore;
            foreach ((string label, Action action) in options)
            {
                Action act = action;
                Label row = UiKit.Text(_menu, label, "menu__option");
                row.RegisterCallback<ClickEvent>(e =>
                {
                    Close();
                    act();
                    e.StopPropagation();
                });
            }
            Vector2 at = ui.GuiToPanel(guiPosition);
            ui.ShowMenu(_menu, at);
        }

        private void Update()
        {
            if (!IsOpen || Time.frameCount == _openedFrame) return;
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }
            if (mouse == null || !(mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return;
            // A press anywhere but on the menu closes it (a press on an option is handled by its click).
            Vector2 screen = mouse.position.ReadValue();
            Vector2 gui = new Vector2(screen.x, Screen.height - screen.y);
            UiRoot ui = UiRoot.Instance;
            if (ui == null || !_menu.worldBound.Contains(ui.GuiToPanel(gui))) Close();
        }
    }
}
