using System.Collections.Generic;
using Rth.Shared.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// Focuses: stances held with breath, the prayer analogue. A book of icons with breath at the
    /// top; an active focus glows. Deflection comes first, one per combat style (keys Z / X / C);
    /// the breath skill will add more.
    /// </summary>
    public sealed class FocusesPanel : UiPanel
    {
        public override string Id => "focuses";
        public override string Title => "Focuses";
        public override string Icon => "focus";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 3;
        public override Key Hotkey => Key.F;

        private UiKit.Bar _breath;
        private Label _drain;
        private readonly List<(CombatStyle Style, VisualElement Socket)> _deflects = new List<(CombatStyle, VisualElement)>();

        protected override void Build(VisualElement root)
        {
            _breath = new UiKit.Bar(root, new Color(0.42f, 0.66f, 0.92f), IconLibrary.Get("focus"), 16f);
            _drain = UiKit.Text(root, "", "muted", "small");
            _drain.style.unityTextAlign = TextAnchor.MiddleCenter;

            UiKit.Text(root, "Deflection", "heading", "small");
            VisualElement grid = UiKit.El(root, "slot-grid");
            foreach ((CombatStyle style, string icon, string key) in new[] { (CombatStyle.Melee, "deflect_melee", "Z"), (CombatStyle.Ranged, "deflect_ranged", "X"), (CombatStyle.Occult, "deflect_occult", "C") })
            {
                CombatStyle s = style;
                VisualElement column = UiKit.El(grid);
                column.style.alignItems = Align.Center;
                VisualElement socket = UiKit.El(column, "socket");
                UiKit.Icon(socket, IconLibrary.Get(icon), 32f, "slot__icon").pickingMode = PickingMode.Ignore;
                UiKit.Text(socket, key, "ability__key").pickingMode = PickingMode.Ignore;
                UiKit.Text(column, style.ToString(), "socket-caption");
                socket.RegisterCallback<ClickEvent>(_ => Session.ToggleDeflect(s));
                UiKit.Tooltip(socket, () => UiKit.Title("Deflect " + s.ToString().ToLowerInvariant()) +
                    "\nHalves the damage you take from " + s.ToString().ToLowerInvariant() + " attacks while held." +
                    "\n" + UiKit.Colour("Drains 1 breath a tick.", UiKit.Muted) + "\nKey: " + UiKit.Colour(KeyFor(s), UiKit.Value));
                _deflects.Add((style, socket));
            }
        }

        private static string KeyFor(CombatStyle s) => s == CombatStyle.Melee ? "Z" : s == CombatStyle.Ranged ? "X" : "C";

        public override void Refresh()
        {
            var state = Session.PlayerState;
            if (state == null) return;
            _breath.Set(state.Breath / (float)Mathf.Max(1, state.MaxBreath), state.Breath + " / " + state.MaxBreath);
            _drain.text = state.Deflect == CombatStyle.None ? "No focus held" : "Holding: deflect " + state.Deflect.ToString().ToLowerInvariant();
            foreach ((CombatStyle style, VisualElement socket) in _deflects) socket.EnableInClassList("socket--on", state.Deflect == style);
        }
    }
}
