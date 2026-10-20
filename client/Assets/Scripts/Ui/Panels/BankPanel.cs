using System.Collections.Generic;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The bank, while the server has it open: everything you've stored in a scrolling grid
    /// (click to withdraw one, right-click for more), a search box, how full it is, and "deposit
    /// pack". Click items in your pack to deposit them while it's open.
    /// </summary>
    public sealed class BankPanel : UiPanel
    {
        public override string Id => "bank";
        public override string Title => Session != null && !string.IsNullOrEmpty(Session.InterfaceTitle) ? Session.InterfaceTitle : "Bank";
        public override string Icon => "coins";
        public override string DefaultWindow => UiDefaultLayout.Town;
        public override bool OpenByDefault => false;
        public override Vector2 MinSize => new Vector2(300f, 240f);
        public override Vector2 DefaultSize => new Vector2(430f, 420f);
        public override bool? ServerOpen => Session.OpenInterface == InterfaceKind.Bank;
        public override void OnClosedByPlayer() => Session.CloseInterface();

        private TextField _search;
        private VisualElement _grid;
        private Label _used;
        private readonly List<InventoryPanel.SlotView> _slots = new List<InventoryPanel.SlotView>();
        private readonly List<int> _bankIndex = new List<int>();
        private string _signature = "";

        protected override void Build(VisualElement root)
        {
            VisualElement top = UiKit.El(root, "row");
            top.style.marginBottom = 4f;
            UiKit.Text(top, "Search", "muted", "small").style.marginRight = 4f;
            _search = new TextField();
            _search.AddToClassList("chat-input");
            _search.style.flexGrow = 1;
            _search.Q(className: "unity-text-field__input").style.backgroundColor = new Color(0.12f, 0.1f, 0.08f);
            _search.Q(className: "unity-text-field__input").style.color = new Color(0.9f, 0.86f, 0.74f);
            top.Add(_search);

            VisualElement well = UiKit.El(root, "well");
            well.style.flexGrow = 1;
            var scroll = new ScrollView();
            scroll.AddToClassList("scroll");
            well.Add(scroll);
            _grid = UiKit.El(scroll, "slot-grid");

            VisualElement footer = UiKit.El(root, "row");
            footer.style.marginTop = 4f;
            _used = UiKit.Text(footer, "", "small");
            UiKit.El(footer, "spacer");
            UiKit.Button(footer, "Deposit pack", () => Session.BankDeposit(-1, 0));
        }

        public override void Refresh()
        {
            string filter = (_search.value ?? "").Trim().ToLowerInvariant();
            var sb = new System.Text.StringBuilder(filter);
            foreach (ItemSlot s in Session.Bank) sb.Append(s.ItemId).Append(s.Count).Append('|');
            string signature = sb.ToString();
            if (signature == _signature) return;
            _signature = signature;

            _bankIndex.Clear();
            int used = 0;
            for (int i = 0; i < Session.Bank.Count; i++)
            {
                ItemSlot slot = Session.Bank[i];
                if (slot.IsEmpty) continue;
                used++;
                ItemDef def = Session.Item(slot.ItemId);
                string name = (def?.Name ?? slot.ItemId).ToLowerInvariant();
                if (filter.Length == 0 || name.Contains(filter)) _bankIndex.Add(i);
            }
            while (_slots.Count < _bankIndex.Count) AddSlot(_slots.Count);
            for (int k = 0; k < _slots.Count; k++)
            {
                bool shown = k < _bankIndex.Count;
                _slots[k].Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown) continue;
                ItemSlot slot = Session.Bank[_bankIndex[k]];
                _slots[k].Show(slot, Session.Item(slot.ItemId));
            }
            _used.text = "Used " + UiKit.Colour(used + "/" + Session.Bank.Count, UiKit.Value) + UiKit.Colour("   Click your pack to deposit.", UiKit.Muted);
        }

        private void AddSlot(int k)
        {
            var view = new InventoryPanel.SlotView(_grid);
            _slots.Add(view);
            view.Root.RegisterCallback<PointerDownEvent>(e =>
            {
                if (k >= _bankIndex.Count) return;
                int index = _bankIndex[k];
                ItemSlot slot = Session.Bank[index];
                string name = Session.Item(slot.ItemId)?.Name ?? slot.ItemId;
                if (e.button == 0) Session.BankWithdraw(index, 1);
                else if (e.button == 1)
                    Ui.Menu(e.position, name, new List<(string, System.Action)>
                    {
                        ("Withdraw 1", () => Session.BankWithdraw(index, 1)),
                        ("Withdraw 5", () => Session.BankWithdraw(index, 5)),
                        ("Withdraw 10", () => Session.BankWithdraw(index, 10)),
                        ("Withdraw all", () => Session.BankWithdraw(index, 0)),
                    });
            });
            UiKit.Tooltip(view.Root, () =>
            {
                if (k >= _bankIndex.Count) return null;
                ItemSlot slot = Session.Bank[_bankIndex[k]];
                ItemDef def = Session.Item(slot.ItemId);
                return def == null ? slot.ItemId : GameText.Describe(def, slot.Count);
            });
        }
    }
}
