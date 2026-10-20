using System.Collections.Generic;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The 28-slot pack. Click uses an item's first action (or deposits/sells while a bank or
    /// shop is open), right-click lists every option, drag moves it to another slot.
    /// </summary>
    public sealed class InventoryPanel : UiPanel
    {
        public override string Id => "inventory";
        public override string Title => "Backpack";
        public override string Tab => "Pack";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 0;
        public override Key Hotkey => Key.B;

        private VisualElement _grid;
        private readonly List<SlotView> _slots = new List<SlotView>();
        private int _dragFrom = -1;

        protected override void Build(VisualElement root)
        {
            _grid = UiKit.El(root, "slot-grid");
            root.RegisterCallback<PointerUpEvent>(_ => EndDrag());
            root.RegisterCallback<PointerLeaveEvent>(_ => EndDrag());
        }

        public override void Refresh()
        {
            List<ItemSlot> items = Session.Inventory;
            while (_slots.Count < items.Count) AddSlot(_slots.Count);
            for (int i = 0; i < _slots.Count; i++)
            {
                ItemSlot slot = i < items.Count ? items[i] : default;
                _slots[i].Show(slot, slot.IsEmpty ? null : Session.Item(slot.ItemId));
                _slots[i].Root.EnableInClassList("slot--dragging", i == _dragFrom);
                _slots[i].Root.style.display = i < items.Count ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void AddSlot(int index)
        {
            var view = new SlotView(_grid);
            _slots.Add(view);
            view.Root.RegisterCallback<PointerDownEvent>(e =>
            {
                ItemDef def = Def(index);
                if (def == null) return;
                if (e.button == 0) _dragFrom = index;
                else if (e.button == 1) Ui.Menu(e.position, def.Name, GameText.ItemOptions(Session, ContainerKind.Inventory, index, def));
            });
            view.Root.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button != 0 || _dragFrom < 0) return;
                if (_dragFrom == index) GameText.DefaultAction(Session, ContainerKind.Inventory, index, Def(index));
                else Session.MoveItem(_dragFrom, index);
                _dragFrom = -1;
                e.StopPropagation();
            });
            UiKit.Tooltip(view.Root, () =>
            {
                ItemDef def = Def(index);
                return def == null ? null : GameText.Describe(def, Session.Inventory[index].Count);
            });
        }

        private ItemDef Def(int index)
        {
            if (index >= Session.Inventory.Count) return null;
            ItemSlot slot = Session.Inventory[index];
            return slot.IsEmpty ? null : Session.Item(slot.ItemId);
        }

        private void EndDrag() => _dragFrom = -1;

        /// <summary>One item slot: a tinted square (until there is item art), the name and the count.</summary>
        public sealed class SlotView
        {
            public readonly VisualElement Root;
            private readonly VisualElement _icon;
            private readonly Label _name;
            private readonly Label _count;

            public SlotView(VisualElement parent)
            {
                Root = UiKit.El(parent, "slot");
                _icon = UiKit.El(Root, "slot__icon");
                _name = UiKit.Text(Root, "", "slot__name");
                _count = UiKit.Text(Root, "", "slot__count");
                _icon.pickingMode = _name.pickingMode = _count.pickingMode = PickingMode.Ignore;
            }

            public void Show(ItemSlot slot, ItemDef def)
            {
                bool empty = slot.IsEmpty;
                _icon.style.display = _name.style.display = empty ? DisplayStyle.None : DisplayStyle.Flex;
                if (empty)
                {
                    _count.text = "";
                    return;
                }
                _icon.style.backgroundColor = (def != null ? WorldView.ParseColor(def.Color) : Color.magenta) * 0.85f;
                _name.text = def?.Name ?? slot.ItemId;
                _count.text = slot.Count > 1 || (def != null && def.Stackable) ? UiKit.FormatCount(slot.Count) : "";
            }
        }
    }
}
