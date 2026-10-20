using System.Collections.Generic;
using Rth.Shared.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// Focuses: stances held with breath, the prayer analogue. Breath at the top, then the focuses
    /// as a book of icons; a held one glows. Deflection comes first, one per combat style (keys
    /// Z / X / C); the breath skill will add more.
    /// </summary>
    public sealed class FocusesPanel : UiPanel
    {
        public override string Id => "focuses";
        public override string Title => "Focuses";
        public override string Icon => "focus";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 4;
        public override Key Hotkey => Key.F;

        private UiKit.Gauge _breath;
        private Label _held;
        private readonly List<(CombatStyle Style, VisualElement Slot)> _deflects = new List<(CombatStyle, VisualElement)>();

        protected override void Build(VisualElement root)
        {
            VisualElement card = UiKit.El(root, "card");
            _breath = new UiKit.Gauge(card, "breath", UiKit.BreathColour);
            _breath.Root.style.marginRight = 0f;
            _held = UiKit.Text(card, "", "muted", "small");
            _held.style.marginTop = 4f;

            UiKit.Heading(root, "Deflection").style.marginTop = 10f;
            VisualElement grid = UiKit.El(root, "slot-grid");
            foreach ((CombatStyle style, string icon, string key) in new[] { (CombatStyle.Melee, "deflect_melee", "Z"), (CombatStyle.Ranged, "deflect_ranged", "X"), (CombatStyle.Occult, "deflect_occult", "C") })
            {
                CombatStyle s = style;
                VisualElement column = UiKit.El(grid);
                column.style.alignItems = Align.Center;
                column.style.marginRight = 6f;
                VisualElement slot = UiKit.El(column, "slot");
                slot.style.width = slot.style.height = 48f;
                VisualElement i = UiKit.El(slot, "slot__icon");
                i.style.width = i.style.height = 38f;
                i.pickingMode = PickingMode.Ignore;
                UiKit.SetIcon(i, IconLibrary.Get(icon), new Color(0.65f, 0.8f, 1f));
                UiKit.Text(slot, key, "ability__key").pickingMode = PickingMode.Ignore;
                UiKit.Text(column, style.ToString(), "slot-caption");
                slot.RegisterCallback<ClickEvent>(_ => Session.ToggleDeflect(s));
                UiKit.Tooltip(slot, () => UiKit.Title("Deflect " + s.ToString().ToLowerInvariant()) +
                    "\nHalves the damage you take from " + s.ToString().ToLowerInvariant() + " attacks while held." +
                    "\n" + UiKit.Colour("Drains 1 breath a tick. Key: " + key, UiKit.Muted));
                _deflects.Add((style, slot));
            }
        }

        public override void Refresh()
        {
            var state = Session.PlayerState;
            if (state == null) return;
            _breath.Set(state.Breath / (float)Mathf.Max(1, state.MaxBreath), state.Breath + " / " + state.MaxBreath);
            _held.text = state.Deflect == CombatStyle.None ? "No focus held." : "Holding: deflect " + state.Deflect.ToString().ToLowerInvariant() + ".";
            foreach ((CombatStyle style, VisualElement slot) in _deflects) slot.EnableInClassList("slot--on", state.Deflect == style);
        }
    }
}
