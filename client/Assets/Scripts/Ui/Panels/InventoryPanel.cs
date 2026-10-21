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
    /// while a bank or shop is open), right-click lists every option. Drag an item to another
    /// slot to move it, onto an action bar to put it there, or onto the bank to deposit it.
    /// </summary>
    public sealed class InventoryPanel : UiPanel
    {
        public override string Id => "inventory";
        public override string Title => "Backpack";
        public override string Icon => "pack";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 0;
        public override Key Hotkey => Key.B;
        public override Vector2 MinSize => new Vector2(200f, 260f);

        private VisualElement _grid;
        private readonly List<ItemSlotView> _slots = new List<ItemSlotView>();

        protected override void Build(VisualElement root)
        {
            _grid = UiKit.El(root, "slot-grid");
            _grid.style.width = 4 * 44f;
            _grid.style.alignSelf = Align.Center;
        }

        public override void Refresh()
        {
            List<ItemSlot> items = Session.Inventory;
            while (_slots.Count < items.Count) _slots.Add(CreateSlot(Ui, Session, _grid, _slots.Count));
            for (int i = 0; i < _slots.Count; i++)
            {
                ItemSlot slot = i < items.Count ? items[i] : default;
                _slots[i].Show(slot, slot.IsEmpty ? null : Session.Item(slot.ItemId));
                _slots[i].Root.style.display = i < items.Count ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// A pack slot with all its behaviour: click, right-click, drag out, and taking drops from
        /// other pack slots. Shared by every view of the pack (this panel, the bank's side).
        /// </summary>
        public static ItemSlotView CreateSlot(UiRoot ui, GameSession session, VisualElement parent, int index)
        {
            var view = new ItemSlotView(parent);

            ItemDef Def()
            {
                if (index >= session.Inventory.Count) return null;
                ItemSlot slot = session.Inventory[index];
                return slot.IsEmpty ? null : session.Item(slot.ItemId);
            }

            view.Root.RegisterCallback<PointerDownEvent>(e =>
            {
                ItemDef def = Def();
                if (def != null && e.button == 1) ui.Menu(e.position, def.Name, GameText.ItemOptions(session, ContainerKind.Inventory, index, def));
            });
            ui.DragSource(view.Root, () =>
            {
                ItemDef def = Def();
                if (def == null) return null;
                return new UiRoot.Drag
                {
                    Data = new ItemDrag { From = ContainerKind.Inventory, Slot = index, ItemId = def.Id },
                    Icon = IconLibrary.ForItem(def),
                    Tint = IconLibrary.ItemTint(def),
                };
            }, () =>
            {
                ItemDef def = Def();
                if (def != null) GameText.DefaultAction(session, ContainerKind.Inventory, index, def);
            });
            ui.AddDropTarget(view.Root,
                data => data is ItemDrag d && d.From == ContainerKind.Inventory && d.Slot != index,
                data => session.MoveItem(((ItemDrag)data).Slot, index));
            UiKit.Tooltip(view.Root, () =>
            {
                ItemDef def = Def();
                return def == null ? null : GameText.Describe(def, session.Inventory[index].Count);
            });
            return view;
        }
    }
}
