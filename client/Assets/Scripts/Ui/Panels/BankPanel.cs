using System.Collections.Generic;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The bank, while the server has it open, a fixed size. On the left, everything stored
    /// (search, click to withdraw, right-click for more), with how many a click moves (1, 5, 10,
    /// X, All) and whether items come out as notes. On the right, tabs for what you carry: the
    /// pack and worn equipment (more, such as a beast of burden, can be added as tabs); a click
    /// there deposits. Drag from the pack or worn onto the bank to deposit everything of it.
    /// </summary>
    public sealed class BankPanel : UiPanel
    {
        private const string XPref = "rth.bank.x";

        public override string Id => "bank";
        public override string Title => Session != null && !string.IsNullOrEmpty(Session.InterfaceTitle) ? Session.InterfaceTitle : "Bank";
        public override string Icon => "coins";
        public override string DefaultWindow => UiDefaultLayout.Town;
        public override bool OpenByDefault => false;
        public override Vector2? FixedSize => new Vector2(780f, 480f);
        public override bool? ServerOpen => Session.OpenInterface == InterfaceKind.Bank;
        public override void OnClosedByPlayer() => Session.CloseInterface();

        private enum Quantity
        {
            One,
            Five,
            Ten,
            X,
            All,
        }

        private TextField _search;
        private VisualElement _grid;
        private Label _used;
        private readonly List<ItemSlotView> _slots = new List<ItemSlotView>();
        private readonly List<int> _bankIndex = new List<int>();
        private readonly List<(Quantity Q, Label Button)> _quantityButtons = new List<(Quantity, Label)>();
        private UiKit.Check _noted;
        private string _signature = "";
        private Quantity _quantity = Quantity.One;
        private bool _asNotes;

        // The side: tabs over what the player carries.
        private readonly List<(string Name, VisualElement Tab, VisualElement Page)> _sideTabs = new List<(string, VisualElement, VisualElement)>();
        private readonly List<ItemSlotView> _packSlots = new List<ItemSlotView>();
        private VisualElement _packGrid;
        private WornPanel.Figure _figure;

        private int X
        {
            get => PlayerPrefs.GetInt(XPref, 14);
            set => PlayerPrefs.SetInt(XPref, value);
        }

        protected override void Build(VisualElement root)
        {
            VisualElement columns = UiKit.El(root, "row");
            columns.style.flexGrow = 1;
            columns.style.alignItems = Align.Stretch;

            // ---------------------------------------------------------------- the bank itself
            VisualElement left = UiKit.El(columns);
            left.style.flexGrow = 1;
            left.style.marginRight = 10f;
            VisualElement top = UiKit.El(left, "row");
            top.style.marginBottom = 6f;
            UiKit.Icon(top, IconLibrary.Get("guide"), 18f).style.unityBackgroundImageTintColor = UiKit.Muted;
            _search = new TextField();
            _search.AddToClassList("input");
            _search.style.flexGrow = 1;
            _search.style.marginLeft = 6f;
            top.Add(_search);
            _used = UiKit.Text(top, "", "muted", "small");
            _used.style.marginLeft = 10f;

            VisualElement well = UiKit.El(left, "well");
            well.style.flexGrow = 1;
            ScrollView scroll = UiKit.Scroll(well);
            _grid = UiKit.El(scroll, "slot-grid");
            Ui.AddDropTarget(well, data => data is ItemDrag, data => DepositDragged((ItemDrag)data));

            VisualElement controls = UiKit.El(left, "row");
            controls.style.marginTop = 6f;
            UiKit.Text(controls, "Quantity", "muted", "small").style.marginRight = 4f;
            foreach (Quantity q in new[] { Quantity.One, Quantity.Five, Quantity.Ten, Quantity.X, Quantity.All })
            {
                Quantity quantity = q;
                Label button = UiKit.Button(controls, Label(q), () => ChooseQuantity(quantity));
                button.style.minWidth = 34f;
                UiKit.Tooltip(button, () => quantity == Quantity.X ? "A number you choose (" + X + "). Click again to change it." : "Withdraw and deposit " + Label(quantity).ToLowerInvariant() + " at a time.");
                _quantityButtons.Add((q, button));
            }
            UiKit.El(controls, "spacer");
            _noted = new UiKit.Check(controls, "Withdraw as notes", () => _asNotes = !_asNotes);
            UiKit.Tooltip(_noted.Root, () => UiKit.Title("Bank notes") + "\nNotes stack in one pack slot. Any strongbox exchanges them for the items again.");

            // ---------------------------------------------------------------- what you carry
            VisualElement side = UiKit.El(columns, "card");
            side.style.width = 214f;
            VisualElement tabs = UiKit.El(side, "row");
            tabs.style.marginBottom = 6f;
            VisualElement pages = UiKit.El(side);
            pages.style.flexGrow = 1;

            VisualElement packPage = UiKit.El(pages);
            packPage.style.flexGrow = 1;
            _packGrid = UiKit.El(packPage, "slot-grid");
            _packGrid.style.width = 4 * 44f;
            _packGrid.style.alignSelf = Align.Center;
            UiKit.Button(packPage, "Deposit pack", () => Session.BankDeposit(-1, 0)).style.marginTop = 6f;
            AddSideTab(tabs, pages, "Pack", "pack", packPage);

            VisualElement wornPage = UiKit.El(pages);
            wornPage.style.flexGrow = 1;
            _figure = new WornPanel.Figure(Ui, Session, wornPage, slot => Session.BankDeposit((int)slot, 1, ContainerKind.Equipment));
            UiKit.Button(wornPage, "Deposit worn items", () => Session.BankDeposit(-1, 0, ContainerKind.Equipment)).style.marginTop = 6f;
            AddSideTab(tabs, pages, "Worn", "worn", wornPage);
            ShowSideTab("Pack");

            ChooseQuantity(Quantity.One);
        }

        private static string Label(Quantity q) => q switch
        {
            Quantity.One => "1",
            Quantity.Five => "5",
            Quantity.Ten => "10",
            Quantity.X => "X",
            _ => "All",
        };

        private void AddSideTab(VisualElement tabs, VisualElement pages, string name, string icon, VisualElement page)
        {
            VisualElement tab = UiKit.El(tabs, "modal-tab");
            tab.style.flexGrow = 1;
            tab.style.justifyContent = Justify.Center;
            tab.style.paddingTop = tab.style.paddingBottom = 4f;
            UiKit.Icon(tab, IconLibrary.Get(icon), 18f, "modal-tab__icon");
            UiKit.Text(tab, name.ToUpperInvariant(), "modal-tab__label");
            tab.RegisterCallback<ClickEvent>(_ => ShowSideTab(name));
            _sideTabs.Add((name, tab, page));
        }

        private void ShowSideTab(string name)
        {
            foreach ((string n, VisualElement tab, VisualElement page) in _sideTabs)
            {
                tab.EnableInClassList("modal-tab--active", n == name);
                page.style.display = n == name ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void ChooseQuantity(Quantity q)
        {
            if (q == Quantity.X && _quantity == Quantity.X) Ui.PromptNumber("How many?", n => X = n);
            else if (q == Quantity.X && !PlayerPrefs.HasKey(XPref)) Ui.PromptNumber("How many?", n => X = n);
            _quantity = q;
            foreach ((Quantity quantity, Label button) in _quantityButtons) button.EnableInClassList("button--on", quantity == q);
        }

        /// <summary>The count a click moves: 0 means all.</summary>
        private int Count => _quantity switch
        {
            Quantity.One => 1,
            Quantity.Five => 5,
            Quantity.Ten => 10,
            Quantity.X => X,
            _ => 0,
        };

        private void DepositDragged(ItemDrag drag)
        {
            if (drag.From == ContainerKind.Inventory) Session.BankDeposit(drag.Slot, 0);
            else if (drag.From == ContainerKind.Equipment) Session.BankDeposit(drag.Slot, 1, ContainerKind.Equipment);
        }

        public override void Refresh()
        {
            _noted.Set(_asNotes);
            RefreshPack();
            _figure.Refresh(Session);

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
            while (_slots.Count < _bankIndex.Count) AddBankSlot(_slots.Count);
            for (int k = 0; k < _slots.Count; k++)
            {
                bool shown = k < _bankIndex.Count;
                _slots[k].Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown) continue;
                ItemSlot slot = Session.Bank[_bankIndex[k]];
                _slots[k].Show(slot, Session.Item(slot.ItemId));
            }
            _used.text = used + " / " + Session.Bank.Count;
        }

        private void RefreshPack()
        {
            List<ItemSlot> items = Session.Inventory;
            while (_packSlots.Count < items.Count) AddPackSlot(_packSlots.Count);
            for (int i = 0; i < _packSlots.Count; i++)
            {
                ItemSlot slot = i < items.Count ? items[i] : default;
                _packSlots[i].Show(slot, slot.IsEmpty ? null : Session.Item(slot.ItemId));
            }
        }

        private void AddPackSlot(int index)
        {
            var view = new ItemSlotView(_packGrid);
            _packSlots.Add(view);
            view.Root.RegisterCallback<PointerDownEvent>(e =>
            {
                if (index >= Session.Inventory.Count || Session.Inventory[index].IsEmpty) return;
                ItemDef def = Session.Item(Session.Inventory[index].ItemId);
                if (e.button == 0) Session.BankDeposit(index, Count);
                else if (e.button == 1 && def != null) Ui.Menu(e.position, def.Name, GameText.ItemOptions(Session, ContainerKind.Inventory, index, def));
            });
            UiKit.Tooltip(view.Root, () =>
            {
                if (index >= Session.Inventory.Count || Session.Inventory[index].IsEmpty) return null;
                ItemDef def = Session.Item(Session.Inventory[index].ItemId);
                return def == null ? null : GameText.Describe(def, Session.Inventory[index].Count) + "\n" + UiKit.Colour("Click to deposit.", UiKit.Muted);
            });
        }

        private void AddBankSlot(int k)
        {
            var view = new ItemSlotView(_grid);
            _slots.Add(view);
            view.Root.RegisterCallback<PointerDownEvent>(e =>
            {
                if (k >= _bankIndex.Count) return;
                int index = _bankIndex[k];
                ItemSlot slot = Session.Bank[index];
                string name = Session.Item(slot.ItemId)?.Name ?? slot.ItemId;
                if (e.button == 0) Session.BankWithdraw(index, Count, _asNotes);
                else if (e.button == 1)
                    Ui.Menu(e.position, name, new List<(string, System.Action)>
                    {
                        ("Withdraw 1", () => Session.BankWithdraw(index, 1, _asNotes)),
                        ("Withdraw 5", () => Session.BankWithdraw(index, 5, _asNotes)),
                        ("Withdraw 10", () => Session.BankWithdraw(index, 10, _asNotes)),
                        ("Withdraw " + X, () => Session.BankWithdraw(index, X, _asNotes)),
                        ("Withdraw X...", () => Ui.PromptNumber("Withdraw how many?", n =>
                        {
                            X = n;
                            Session.BankWithdraw(index, n, _asNotes);
                        })),
                        ("Withdraw all", () => Session.BankWithdraw(index, 0, _asNotes)),
                        (_asNotes ? "Withdraw as items instead" : "Withdraw as notes instead", () => _asNotes = !_asNotes),
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
