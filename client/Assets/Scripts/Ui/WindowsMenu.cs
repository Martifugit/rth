using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>
    /// The interface menu (the button under the minimap): every window the player can show or hide,
    /// grouped into what is always on screen and the panels, each with its icon, hotkey and whether
    /// it is showing; a click shows or hides it. Below: the interface lock and reset layout. Closes
    /// when the pointer leaves it.
    /// </summary>
    public static class WindowsMenu
    {
        /// <summary>How far the pointer may stray outside the menu before it closes.</summary>
        private const float LeaveMargin = 14f;

        private static VisualElement _open;

        public static bool IsOpen => _open != null && _open.parent != null;

        public static void Close()
        {
            _open?.RemoveFromHierarchy();
            _open = null;
        }

        /// <summary>Opens the menu with its top right corner at <paramref name="at"/> (panel pixels).</summary>
        public static void Open(UiRoot ui, Vector2 at)
        {
            Close();
            // A margin around the menu that still counts as inside: leaving it closes the menu.
            var catcher = new VisualElement();
            catcher.style.paddingLeft = catcher.style.paddingRight = catcher.style.paddingTop = catcher.style.paddingBottom = LeaveMargin;
            catcher.RegisterCallback<PointerLeaveEvent>(_ => Close());
            VisualElement menu = UiKit.El(catcher, "windows-menu");
            UiKit.Text(menu, "INTERFACE", "windows-menu__title");

            UiPanel[] panels = ui.Panels.Where(p => p.ServerOpen == null).ToArray();
            Section(ui, menu, "On screen", panels.Where(p => p.Bare || p.FixedSize.HasValue));
            Section(ui, menu, "Panels", panels.Where(p => !p.Bare && !p.FixedSize.HasValue));

            VisualElement footer = UiKit.El(menu, "windows-menu__footer");
            Label lockButton = UiKit.Button(footer, "", () => ui.Locked = !ui.Locked);
            lockButton.schedule.Execute(() => lockButton.text = ui.Locked ? "Unlock interface" : "Lock interface").Every(100);
            UiKit.El(footer, "spacer");
            UiKit.Button(footer, "Reset layout", () =>
            {
                ui.ResetLayout();
                Close();
            });

            _open = catcher;
            ui.ShowMenu(catcher, at);
        }

        private static void Section(UiRoot ui, VisualElement menu, string title, System.Collections.Generic.IEnumerable<UiPanel> panels)
        {
            UiKit.Text(menu, title.ToUpperInvariant(), "windows-menu__section");
            foreach (UiPanel panel in panels.OrderBy(p => p.Title))
            {
                UiPanel p = panel;
                VisualElement row = UiKit.El(menu, "windows-menu__row");
                UiKit.Icon(row, IconLibrary.Get(p.Icon), 18f, "windows-menu__icon").pickingMode = PickingMode.Ignore;
                UiKit.Text(row, p.Title, "windows-menu__name").pickingMode = PickingMode.Ignore;
                if (p.Hotkey != Key.None) UiKit.Text(row, p.Hotkey.ToString(), "windows-menu__key").pickingMode = PickingMode.Ignore;
                Label state = UiKit.Text(row, "", "windows-menu__state");
                state.pickingMode = PickingMode.Ignore;
                void Show()
                {
                    bool open = p.Window != null;
                    state.text = open ? "Shown" : "Hidden";
                    row.EnableInClassList("windows-menu__row--open", open);
                }
                Show();
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (p.Window != null) ui.Close(p);
                    else ui.Open(p);
                    Show();
                });
            }
        }
    }
}
