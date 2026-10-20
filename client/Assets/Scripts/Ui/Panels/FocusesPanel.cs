using System.Collections.Generic;
using Rth.Shared.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// Focuses: stances that spend breath (the prayer analogue). Deflection is the first kind,
    /// one per combat style (keys Z / X / C). More focuses come with the breath skill.
    /// </summary>
    public sealed class FocusesPanel : UiPanel
    {
        public override string Id => "focuses";
        public override string Title => "Focuses";
        public override string Tab => "Focus";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 3;
        public override Key Hotkey => Key.F;

        private UiKit.Bar _breath;
        private readonly List<(CombatStyle Style, Label Button)> _deflects = new List<(CombatStyle, Label)>();

        protected override void Build(VisualElement root)
        {
            _breath = new UiKit.Bar(root, new Color(0.5f, 0.72f, 0.95f));
            UiKit.Text(root, "Deflection", "heading").style.marginTop = 4f;
            Label help = UiKit.Text(root, "Halves the damage of one style while held. Drains breath each tick.", "muted");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.fontSize = 10;
            help.style.marginBottom = 4f;
            foreach ((CombatStyle style, string label) in new[] { (CombatStyle.Melee, "Deflect melee  (Z)"), (CombatStyle.Ranged, "Deflect ranged  (X)"), (CombatStyle.Occult, "Deflect occult  (C)") })
            {
                CombatStyle s = style;
                Label button = UiKit.Button(root, label, () => Session.ToggleDeflect(s));
                _deflects.Add((style, button));
            }
        }

        public override void Refresh()
        {
            var state = Session.PlayerState;
            if (state == null) return;
            _breath.Set(state.Breath / (float)Mathf.Max(1, state.MaxBreath), "Breath " + state.Breath + " / " + state.MaxBreath);
            foreach ((CombatStyle style, Label button) in _deflects) button.EnableInClassList("button--on", state.Deflect == style);
        }
    }
}
