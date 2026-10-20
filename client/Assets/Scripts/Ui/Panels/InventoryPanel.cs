using System.Collections.Generic;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The 28-slot backpack, four across. Click uses an item's first action (or deposits/sells
    /// while a bank or shop is open), right-click lists every option, drag moves it to another
    /// slot (its icon follows the pointer).
    /// </summary>
    public sealed class InventoryPanel : UiPanel
    {
        public override string Id => "inventory";
        public override string Title => "Backpack";
        public override string Icon => "pack";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 0;
        public override Key Hotkey => Key.B;
        public override Vector2 MinSize => new Vector2(186f, 200f);

        private VisualElement _grid;
        private Label _free;
        private readonly List<SlotView> _slots = new List<SlotView>();
        private int _dragFrom = -1;
        private bool _dragMoved;
        private Vector2 _dragStart;

        protected override void Build(VisualElement root)
        {
            VisualElement well = UiKit.El(root, "well");
            well.style.alignSelf = Align.Center;
            _grid = UiKit.El(well, "slot-grid");
            _grid.style.width = 4 * 42f;
            _free = UiKit.Text(root, "", "muted", "small");
            _free.style.unityTextAlign = TextAnchor.MiddleCenter;
            _free.style.marginTop = 3f;

            root.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (_dragFrom < 0) return;
                if (!_dragMoved && ((Vector2)e.position - _dragStart).magnitude > 5f) _dragMoved = true;
                if (_dragMoved) Ui.ShowDragGhost(null, IconLibrary.ForItem(Def(_dragFrom)), (Vector2)e.position - new Vector2(24f, 24f));
            });
            root.RegisterCallback<PointerUpEvent>(_ => EndDrag());
            root.RegisterCallback<PointerLeaveEvent>(_ => EndDrag());
        }

        public override void Refresh()
        {
            List<ItemSlot> items = Session.Inventory;
            while (_slots.Count < items.Count) AddSlot(_slots.Count);
            int free = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                ItemSlot slot = i < items.Count ? items[i] : default;
                if (slot.IsEmpty) free++;
                _slots[i].Show(slot, slot.IsEmpty ? null : Session.Item(slot.ItemId));
                _slots[i].Root.EnableInClassList("slot--dragging", i == _dragFrom && _dragMoved);
                _slots[i].Root.style.display = i < items.Count ? DisplayStyle.Flex : DisplayStyle.None;
            }
            _free.text = free + " free";
        }

        private void AddSlot(int index)
        {
            var view = new SlotView(_grid);
            _slots.Add(view);
            view.Root.RegisterCallback<PointerDownEvent>(e =>
            {
                ItemDef def = Def(index);
                if (def == null) return;
                if (e.button == 0)
                {
                    _dragFrom = index;
                    _dragMoved = false;
                    _dragStart = e.position;
                }
                else if (e.button == 1)
                {
                    Ui.Menu(e.position, def.Name, GameText.ItemOptions(Session, ContainerKind.Inventory, index, def));
                }
            });
            view.Root.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button != 0 || _dragFrom < 0) return;
                if (_dragFrom == index && !_dragMoved) GameText.DefaultAction(Session, ContainerKind.Inventory, index, Def(index));
                else if (_dragFrom != index) Session.MoveItem(_dragFrom, index);
                EndDrag();
                e.StopPropagation();
            });
            UiKit.Tooltip(view.Root, () =>
            {
                ItemDef def = Def(index);
                return def == null || _dragMoved ? null : GameText.Describe(def, Session.Inventory[index].Count);
            });
        }

        private ItemDef Def(int index)
        {
            if (index < 0 || index >= Session.Inventory.Count) return null;
            ItemSlot slot = Session.Inventory[index];
            return slot.IsEmpty ? null : Session.Item(slot.ItemId);
        }

        private void EndDrag()
        {
            _dragFrom = -1;
            _dragMoved = false;
            Ui.HideDragGhost();
        }

        /// <summary>One item slot: the item's icon and, for stacks, the count in the classic colours.</summary>
        public sealed class SlotView
        {
            public readonly VisualElement Root;
            private readonly VisualElement _icon;
            private readonly Label _count;
            private ItemDef _shownDef;

            public SlotView(VisualElement parent)
            {
                Root = UiKit.El(parent, "slot");
                _icon = UiKit.El(Root, "slot__icon");
                _count = UiKit.Text(Root, "", "slot__count");
                _icon.pickingMode = _count.pickingMode = PickingMode.Ignore;
            }

            public void Show(ItemSlot slot, ItemDef def)
            {
                if (def != _shownDef)
                {
                    _shownDef = def;
                    UiKit.SetIcon(_icon, def != null ? IconLibrary.ForItem(def) : null);
                }
                bool stack = !slot.IsEmpty && (slot.Count > 1 || (def != null && def.Stackable));
                _count.text = stack ? UiKit.FormatCount(slot.Count) : "";
                if (stack) _count.style.color = UiKit.CountColour(slot.Count);
            }
        }
    }
}
