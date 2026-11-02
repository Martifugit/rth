using Rth.Shared.Angling;
using Rth.Shared.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The angler's rig (docs/fishing.md 4), from the "tackle" state: a part in each slot with its
    /// condition, and the numbers they make. Parts go in from the pack ("Rig"); clicking a slot
    /// takes its part back out (the "tackle" intent). Worn parts give less; mend them at a gear
    /// bench or with a tackle-maker.
    /// </summary>
    public sealed class TacklePanel : UiPanel
    {
        public override string Id => "tackle";
        public override string Title => "Rig";
        public override string Icon => "fishing";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override bool OpenByDefault => false;
        public override Vector2 DefaultSize => new Vector2(260f, 330f);

        private VisualElement _slots;
        private Label _stats;
        private int _version = -1;

        protected override void Build(VisualElement root)
        {
            UiKit.Heading(root, "Rig");
            _slots = UiKit.El(root);
            UiKit.Heading(root, "In a fight").style.marginTop = 8f;
            _stats = UiKit.Text(root, "", "small", "prose");
            UiKit.Text(root, UiKit.Colour("Click a part to take it off. Rig parts from your pack.", UiKit.Muted), "small").style.marginTop = 6f;
        }

        public override void Refresh()
        {
            if (_version == Session.StateVersion) return;
            _version = Session.StateVersion;
            AnglingTackleState state = Session.ServerState<AnglingTackleState>(AnglingTackleState.StateKey);
            _slots.Clear();
            foreach (AnglingSlotState slot in state.Slots)
            {
                AnglingSlotState s = slot;
                ItemDef def = Session.Item(s.Item);
                VisualElement row = UiKit.El(_slots, "master-row");
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                VisualElement icon = UiKit.El(row, "slot__icon");
                icon.style.width = icon.style.height = 24f;
                icon.style.marginRight = 6f;
                icon.pickingMode = PickingMode.Ignore;
                if (def != null) UiKit.SetItem(icon, def);
                Label name = UiKit.Text(row, s.Slot + ": " + (def != null ? def.Name : UiKit.Colour("nothing", UiKit.Muted)));
                name.style.flexGrow = 1;
                if (def != null)
                {
                    Color c = s.Condition >= 70 ? UiKit.Good : s.Condition >= 30 ? UiKit.Value : UiKit.Bad;
                    UiKit.Text(row, UiKit.Colour(Mathf.RoundToInt((float)s.Condition) + "%", c), "small");
                    UiKit.Tooltip(row, () => GameText.Describe(def, 1));
                    row.RegisterCallback<ClickEvent>(_ => Session.Intent("tackle", "unrig:" + s.Slot));
                }
            }
            AnglingRigStats r = state.Stats;
            _stats.text = "Red buffer " + UiKit.Colour(r.SnapBuffer.ToString(), UiKit.Value) + " steps   Line strength " + UiKit.Colour(r.Strength.ToString(), UiKit.Value)
                          + "\nReel speed " + UiKit.Colour(r.ReelSpeed.ToString(), UiKit.Value) + "   Run drag " + UiKit.Colour(r.RunDrag.ToString(), UiKit.Value)
                          + "\nHook hold " + UiKit.Colour(r.HookHold.ToString(), UiKit.Value)
                          + (r.Lantern ? "   " + UiKit.Colour("lantern lit", UiKit.Value) : "")
                          + (r.Listening ? "   " + UiKit.Colour("listening", UiKit.Value) : "");
        }
    }
}
