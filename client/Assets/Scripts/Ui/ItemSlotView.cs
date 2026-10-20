using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>One item slot: the item's icon (a note shows as paper) and, for stacks, the count in the classic colours.</summary>
    public sealed class ItemSlotView
    {
        public readonly VisualElement Root;
        private readonly VisualElement _icon;
        private readonly Label _count;
        private ItemDef _shownDef;

        public ItemSlotView(VisualElement parent, params string[] extraClasses)
        {
            Root = UiKit.El(parent, "slot");
            foreach (string c in extraClasses) Root.AddToClassList(c);
            _icon = UiKit.El(Root, "slot__icon");
            _count = UiKit.Text(Root, "", "slot__count");
            _icon.pickingMode = _count.pickingMode = PickingMode.Ignore;
        }

        public void Show(ItemSlot slot, ItemDef def)
        {
            if (def != _shownDef)
            {
                _shownDef = def;
                UiKit.SetItem(_icon, def);
            }
            bool stack = !slot.IsEmpty && (slot.Count > 1 || (def != null && def.Stackable));
            _count.text = stack ? UiKit.FormatCount(slot.Count) : "";
            if (stack) _count.style.color = UiKit.CountColour(slot.Count);
        }
    }
}
