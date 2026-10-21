using System.Collections.Generic;
using Rth.Shared.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// Focuses: stances held with breath, the prayer analogue. Breath at the top, then the focuses
    /// as a book of icons; a held one glows. Click one to hold or release it, or drag it onto an
    /// action bar and use it from there like an ability. Deflection comes first, one per combat
    /// style; the breath skill will add more.
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
        private readonly List<(Focuses.Focus Focus, VisualElement Slot)> _slots = new List<(Focuses.Focus, VisualElement)>();

        protected override void Build(VisualElement root)
        {
            VisualElement card = UiKit.El(root, "card");
            card.style.flexShrink = 0;
            _breath = new UiKit.Gauge(card, "breath", UiKit.BreathColour);
            _breath.Root.style.marginRight = 0f;
            _breath.Root.style.flexBasis = StyleKeyword.Auto;
            _held = UiKit.Text(card, "", "muted", "small");
            _held.style.marginTop = 4f;

            UiKit.Heading(root, "Deflection").style.marginTop = 10f;
            VisualElement grid = UiKit.El(root, "slot-grid");
            grid.style.flexShrink = 0;
            foreach (Focuses.Focus focus in Focuses.All)
            {
                Focuses.Focus f = focus;
                VisualElement column = UiKit.El(grid);
                column.style.alignItems = Align.Center;
                column.style.marginRight = 8f;
                VisualElement slot = UiKit.El(column, "slot");
                slot.style.width = slot.style.height = 48f;
                VisualElement i = UiKit.El(slot, "slot__icon");
                i.style.width = i.style.height = 38f;
                i.pickingMode = PickingMode.Ignore;
                UiKit.SetIcon(i, IconLibrary.Get(f.Icon), Focuses.Tint);
                UiKit.Text(column, f.Deflects.ToString(), "slot-caption");
                Ui.DragSource(slot,
                    () => new UiRoot.Drag { Data = new FocusDrag { FocusId = f.Id }, Icon = IconLibrary.Get(f.Icon), Tint = Focuses.Tint },
                    () => f.Toggle(Session));
                UiKit.Tooltip(slot, () => Focuses.Describe(f));
                _slots.Add((f, slot));
            }

            UiKit.El(root, "spacer");
            Label hint = UiKit.Text(root, "Drag a focus onto an action bar to use it from there.", "muted", "small");
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.flexShrink = 0;
        }

        public override void Refresh()
        {
            var state = Session.PlayerState;
            if (state == null) return;
            _breath.Set(state.Breath / (float)Mathf.Max(1, state.MaxBreath), state.Breath + " / " + state.MaxBreath);
            _held.text = state.Deflect == CombatStyle.None ? "No focus held." : "Holding: deflect " + state.Deflect.ToString().ToLowerInvariant() + ".";
            foreach ((Focuses.Focus focus, VisualElement slot) in _slots) slot.EnableInClassList("slot--on", focus.IsHeld(state));
        }
    }
}
