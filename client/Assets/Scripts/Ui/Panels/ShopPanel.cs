using System.Collections.Generic;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// A shop, while the server has it open, a fixed size: its stock as item slots with the stock
    /// count on each and the price beneath, out-of-stock items dimmed. Click to buy one,
    /// right-click for more. If the shop buys, click items in your pack to sell them.
    /// </summary>
    public sealed class ShopPanel : UiPanel
    {
        public override string Id => "shop";
        public override string Title => Session?.Shop != null ? Session.Shop.Title : "Shop";
        public override string Icon => "coins";
        public override string DefaultWindow => UiDefaultLayout.Town;
        public override bool OpenByDefault => false;
        public override Vector2? FixedSize => new Vector2(500f, 400f);
        public override bool? ServerOpen => Session.OpenInterface == InterfaceKind.Shop && Session.Shop != null;
        public override void OnClosedByPlayer() => Session.CloseInterface();

        private VisualElement _grid;
        private Label _footer;
        private ShopMessage _shown;

        protected override void Build(VisualElement root)
        {
            VisualElement well = UiKit.El(root, "well");
            well.style.flexGrow = 1;
            ScrollView scroll = UiKit.Scroll(well);
            _grid = UiKit.El(scroll, "slot-grid");
            _footer = UiKit.Text(root, "", "prose", "small");
            _footer.style.marginTop = 6f;
        }

        public override void Refresh()
        {
            ShopMessage shop = Session.Shop;
            if (shop == null || shop == _shown) return;
            _shown = shop;
            Window?.Activate(this); // the title follows the shop

            bool marks = shop.CurrencyName.ToLowerInvariant().Contains("mark");
            Texture2D currency = IconLibrary.Get(marks ? "coins" : "task");
            _grid.Clear();
            for (int i = 0; i < shop.Entries.Count; i++)
            {
                int index = i;
                ShopEntry entry = shop.Entries[i];
                ItemDef def = Session.Item(entry.ItemId);

                VisualElement cell = UiKit.El(_grid);
                cell.style.width = 70f;
                cell.style.alignItems = Align.Center;
                cell.style.marginBottom = 6f;
                VisualElement slot = UiKit.El(cell, "slot");
                slot.style.width = slot.style.height = 48f;
                VisualElement icon = UiKit.El(slot, "slot__icon");
                icon.pickingMode = PickingMode.Ignore;
                UiKit.SetItem(icon, def);
                if (entry.Stock >= 0)
                {
                    Label stock = UiKit.Text(slot, UiKit.FormatCount(entry.Stock), "slot__count");
                    stock.pickingMode = PickingMode.Ignore;
                    stock.style.color = entry.Stock == 0 ? UiKit.Bad : UiKit.Value;
                }
                if (entry.Stock == 0) slot.AddToClassList("slot--locked");

                VisualElement price = UiKit.El(cell, "row");
                UiKit.Icon(price, currency, 14f).style.unityBackgroundImageTintColor = UiKit.Value;
                UiKit.Text(price, entry.Price.ToString("N0"), "value", "small").style.marginLeft = 2f;

                string name = def?.Name ?? entry.ItemId;
                slot.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button == 0) Session.ShopBuy(index, 1);
                    else if (e.button == 1)
                        Ui.Menu(e.position, name + " (" + entry.Price + " each)", new List<(string, System.Action)>
                        {
                            ("Buy 1", () => Session.ShopBuy(index, 1)),
                            ("Buy 5", () => Session.ShopBuy(index, 5)),
                            ("Buy 10", () => Session.ShopBuy(index, 10)),
                            ("Buy X...", () => Ui.PromptNumber("Buy how many?", n => Session.ShopBuy(index, n))),
                        });
                });
                UiKit.Tooltip(slot, () => (def != null ? GameText.Describe(def, 1) : UiKit.Title(name)) +
                    "\n\nPrice: " + UiKit.Colour(entry.Price.ToString("N0") + " " + shop.CurrencyName.ToLowerInvariant(), UiKit.Value) +
                    (entry.Stock < 0 ? "" : "\nIn stock: " + UiKit.Colour(entry.Stock.ToString(), entry.Stock > 0 ? UiKit.Value : UiKit.Bad)));
            }
            _footer.text = "Prices in " + UiKit.Colour(shop.CurrencyName.ToLowerInvariant(), UiKit.Value) + "." +
                (shop.SellPercent > 0 ? UiKit.Colour("  Click items in your pack to sell them for " + shop.SellPercent + "% of their worth.", UiKit.Muted) : UiKit.Colour("  This shop doesn't buy.", UiKit.Muted));
        }
    }
}
