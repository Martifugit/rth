using Rth.Shared.Angling;
using Rth.Shared.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// A rod's rig (docs/fishing.md 4), open while the server says so (right-click a rod, "Rig"):
    /// the rod drawn across the window with its slots along it, from the reel at the butt to the
    /// lure at the tip, each holding its part and showing its condition. Drag a part from the pack
    /// onto a slot (or right-click it, "Rig"); click a slot to take its part off. What fits is the
    /// server's to say (the rod's kind and tier); it explains when something doesn't.
    /// </summary>
    public sealed class RigPanel : UiPanel
    {
        public override string Id => "rig";
        public override string Title => _rodName ?? "Rig";
        public override string Icon => "fishing";
        public override string DefaultWindow => UiDefaultLayout.Town;
        public override bool OpenByDefault => false;
        public override Vector2? FixedSize => new Vector2(520f, 250f);
        public override bool? ServerOpen => !string.IsNullOrEmpty(Session.ServerState<AnglingTackleState>(AnglingTackleState.StateKey).Open);
        public override void OnClosedByPlayer() => Session.Intent(AnglingTackleState.IntentKind, "close");

        private VisualElement _rod;
        private VisualElement _slots;
        private Label _header;
        private Label _stats;
        private string _rodName;
        private int _version = -1;

        private static readonly Color Wood = new Color(0.45f, 0.32f, 0.18f);

        protected override void Build(VisualElement root)
        {
            _header = UiKit.Text(root, "", "small");
            VisualElement stage = UiKit.El(root);
            stage.style.height = 120f;
            stage.style.marginTop = 6f;
            stage.style.position = Position.Relative;
            // The rod itself: a tapering line across the window, butt on the left.
            _rod = UiKit.El(stage);
            _rod.style.position = Position.Absolute;
            _rod.style.left = 10f;
            _rod.style.right = 10f;
            _rod.style.top = 56f;
            _rod.style.height = 6f;
            _rod.style.backgroundColor = Wood;
            _rod.style.borderTopRightRadius = _rod.style.borderBottomRightRadius = 3f;
            _rod.pickingMode = PickingMode.Ignore;
            _slots = UiKit.El(stage, "row");
            _slots.style.position = Position.Absolute;
            _slots.style.left = 0f;
            _slots.style.right = 0f;
            _slots.style.top = 0f;
            _slots.style.bottom = 0f;
            _slots.style.justifyContent = Justify.SpaceAround;
            _slots.style.alignItems = Align.Center;
            _stats = UiKit.Text(root, "", "small", "prose");
            _stats.style.marginTop = 4f;
            UiKit.Text(root, UiKit.Colour("Drag parts from your pack onto the rod. Click a part to take it off.", UiKit.Muted), "small").style.marginTop = 2f;
        }

        public override void Refresh()
        {
            if (_version == Session.StateVersion) return;
            _version = Session.StateVersion;
            AnglingTackleState state = Session.ServerState<AnglingTackleState>(AnglingTackleState.StateKey);
            ItemDef rod = Session.Item(state.Open);
            string name = rod?.Name;
            if (name != _rodName)
            {
                _rodName = name;
                Window?.Activate(this);   // the title follows the rod
            }
            AnglingTackleDef t = rod?.Tackle;
            _header.text = t == null ? "" : UiKit.Colour("Tier " + t.Tier + " " + t.Kind + " rod", UiKit.Value) + "   condition " + Condition(state.RodCondition);

            _slots.Clear();
            foreach (AnglingSlotState slot in state.Slots) Slot(slot);

            AnglingRigStats r = state.Stats;
            _stats.text = "Holds in the red " + UiKit.Colour(r.SnapBuffer.ToString(), UiKit.Value)
                          + "   Line " + UiKit.Colour(r.Strength.ToString(), UiKit.Value)
                          + "   Reel " + UiKit.Colour(r.ReelSpeed.ToString(), UiKit.Value)
                          + "   Drag " + UiKit.Colour(r.RunDrag.ToString(), UiKit.Value)
                          + "   Hook " + UiKit.Colour(r.HookHold.ToString(), UiKit.Value)
                          + (r.Lantern ? "   " + UiKit.Colour("lantern", UiKit.Value) : "");
        }

        private void Slot(AnglingSlotState s)
        {
            ItemDef def = Session.Item(s.Item);
            VisualElement column = UiKit.El(_slots);
            column.style.alignItems = Align.Center;
            UiKit.Text(column, s.Slot.ToString().ToUpperInvariant(), "small").style.color = UiKit.Muted;
            VisualElement box = UiKit.Slots.Make(column, out VisualElement icon);
            box.style.marginTop = 2f;
            box.style.marginBottom = 2f;
            if (def != null)
            {
                UiKit.SetItem(icon, def);
                UiKit.Tooltip(box, () => GameText.Describe(def, 1) + "\n" + UiKit.Colour("Click to take it off.", UiKit.Muted));
                box.RegisterCallback<ClickEvent>(_ => Session.Intent(AnglingTackleState.IntentKind, "unrig:" + s.Slot));
            }
            else UiKit.Tooltip(box, () => UiKit.Title(s.Slot.ToString()) + "\n" + UiKit.Colour("Empty. Drag a " + s.Slot.ToString().ToLowerInvariant() + " here from your pack.", UiKit.Muted));
            Ui.AddDropTarget(box, data => data is ItemDrag d && d.From == ContainerKind.Inventory && Session.Item(d.ItemId)?.Tackle != null,
                data => Session.Intent(AnglingTackleState.IntentKind, "put:" + ((ItemDrag)data).Slot));
            UiKit.Text(column, def != null ? Condition(s.Condition) : " ", "small");
        }

        private static string Condition(double c)
        {
            Color colour = c >= 70 ? UiKit.Good : c >= 30 ? UiKit.Value : UiKit.Bad;
            return UiKit.Colour(Mathf.RoundToInt((float)c) + "%", colour);
        }
    }
}
