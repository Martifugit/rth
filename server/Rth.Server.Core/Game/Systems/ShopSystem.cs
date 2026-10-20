using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Content;
using Rth.Server.Items;
using Rth.Shared.Items;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>
    /// Something prices can be paid in. Shops name a currency by string: an item id ("marks"),
    /// or a prefix a system registered ("points:yard_tokens"). Add new kinds (faction favour,
    /// event tickets) by registering them.
    /// </summary>
    public interface ICurrency
    {
        string Name(string currency);
        long Balance(Player player, string currency);
        bool TrySpend(Player player, string currency, int amount);
        /// <summary>Pays the player. Returns how much couldn't be given (e.g. no inventory space).</summary>
        int Give(Player player, string currency, int amount);
    }

    /// <summary>
    /// Shops from shops.json: limited or unlimited stock that restocks over time, priced in any
    /// currency, optionally buying items back. Shared stock: if one player buys the last vial,
    /// everyone sees it gone. Opened by the NPC action "trade" or the dialogue action "openShop".
    /// </summary>
    public sealed class ShopSystem : GameSystem
    {
        public const string Trade = "trade";

        private readonly Dictionary<string, ICurrency> _currencies = new Dictionary<string, ICurrency>();
        private readonly Dictionary<string, int[]> _stock = new Dictionary<string, int[]>();
        private readonly Dictionary<string, int[]> _restockTimers = new Dictionary<string, int[]>();
        private readonly HashSet<string> _changed = new HashSet<string>();

        private InterfaceSystem Ui => World.Get<InterfaceSystem>();

        /// <summary>Currencies whose name starts with <paramref name="prefix"/> are handled by <paramref name="currency"/>.</summary>
        public void RegisterCurrency(string prefix, ICurrency currency) => _currencies[prefix] = currency;

        public ICurrency CurrencyFor(string currency) =>
            _currencies.FirstOrDefault(c => c.Key.Length > 0 && currency.StartsWith(c.Key)).Value ?? _currencies[""];

        protected override void OnAttach()
        {
            RegisterCurrency("", new ItemCurrency(World));
            World.Get<NpcSystem>().RegisterAction(Trade, (player, npc) =>
            {
                if (npc.Def.Shop != null) Open(player, npc.Def.Shop, npc);
            });
            World.Get<DialogueSystem>().RegisterAction("openShop", (player, a, npc) =>
            {
                if (a.Id != null) Open(player, a.Id, npc);
            });
            World.OnMessage<ShopBuyMessage>(Buy);
            World.OnMessage<ShopSellMessage>(Sell);
        }

        private int[] Stock(ShopDef shop)
        {
            if (!_stock.TryGetValue(shop.Id, out int[]? stock) || stock.Length != shop.Stock.Count)
            {
                _stock[shop.Id] = stock = shop.Stock.Select(s => s.Quantity).ToArray();
                _restockTimers[shop.Id] = new int[shop.Stock.Count];
            }
            return stock;
        }

        public void Open(Player player, string shopId, Npc? npc)
        {
            if (!World.Content.Shops.TryGetValue(shopId, out ShopDef? shop)) return;
            Ui.Open(player, InterfaceKind.Shop, shop.Name, npc?.Plane ?? player.Plane, npc?.Position ?? player.Position, shop.Id);
            player.Connection.Send(Describe(shop));
        }

        private ShopMessage Describe(ShopDef shop)
        {
            int[] stock = Stock(shop);
            return new ShopMessage
            {
                Title = shop.Name,
                CurrencyName = CurrencyFor(shop.Currency).Name(shop.Currency),
                SellPercent = shop.SellPercent,
                Entries = shop.Stock.Select((s, i) => new ShopEntry { ItemId = s.Item, Price = s.Price, Stock = stock[i] }).ToList(),
            };
        }

        private ShopDef? OpenShop(Player player)
        {
            OpenInterface open = Ui.State(player);
            return open.Kind == InterfaceKind.Shop && World.Content.Shops.TryGetValue(open.Id, out ShopDef? shop) ? shop : null;
        }

        private void Buy(Player player, ShopBuyMessage m)
        {
            ShopDef? shop = OpenShop(player);
            if (shop == null || m.Index < 0 || m.Index >= shop.Stock.Count || m.Count < 1) return;
            ShopStockDef entry = shop.Stock[m.Index];
            ItemDef? def = World.Content.Item(entry.Item);
            if (def == null) return;
            int[] stock = Stock(shop);
            ICurrency currency = CurrencyFor(shop.Currency);
            ItemContainer inv = World.Get<ItemSystem>().Of(player).Inventory;

            int count = m.Count;
            if (stock[m.Index] >= 0) count = Math.Min(count, stock[m.Index]);
            count = (int)Math.Min(count, currency.Balance(player, shop.Currency) / entry.Price);
            if (!def.Stackable) count = Math.Min(count, inv.FreeSlots);
            else if (!inv.CanAdd(def, count)) count = 0;

            if (count <= 0)
            {
                World.Tell(player, TextKind.Warning,
                    stock[m.Index] == 0 ? "That's sold out." :
                    currency.Balance(player, shop.Currency) < entry.Price ? "You can't afford that." :
                    "You don't have enough inventory space.");
                return;
            }
            if (!currency.TrySpend(player, shop.Currency, count * entry.Price)) return;
            inv.Add(def, count);
            if (stock[m.Index] >= 0)
            {
                stock[m.Index] -= count;
                _changed.Add(shop.Id);
            }
        }

        private void Sell(Player player, ShopSellMessage m)
        {
            ShopDef? shop = OpenShop(player);
            if (shop == null) return;
            if (shop.SellPercent <= 0)
            {
                World.Tell(player, TextKind.Warning, "This shop doesn't buy anything.");
                return;
            }
            ItemContainer inv = World.Get<ItemSystem>().Of(player).Inventory;
            if (!inv.InRange(m.Slot) || inv.IsEmpty(m.Slot)) return;
            string id = inv[m.Slot].ItemId;
            ItemDef? def = World.Content.Item(id);
            if (def == null || id == shop.Currency) return;
            int each = def.Value * shop.SellPercent / 100;
            if (each <= 0)
            {
                World.Tell(player, TextKind.Warning, "They don't want that.");
                return;
            }
            int count = Math.Min(Math.Max(1, m.Count), inv.CountOf(id));
            int removed = inv.RemoveAt(m.Slot, count);
            if (removed < count) removed += inv.Remove(id, count - removed);
            int unpaid = CurrencyFor(shop.Currency).Give(player, shop.Currency, removed * each);
            if (unpaid > 0) World.Get<GroundItemSystem>().Spawn(shop.Currency, unpaid, player.Plane, player.Position, player);

            // A shop that stocks this item takes it back into stock.
            int index = shop.Stock.FindIndex(s => s.Item == id && s.Quantity > 0);
            if (index >= 0)
            {
                Stock(shop)[index] += removed;
                _changed.Add(shop.Id);
            }
        }

        public override void OnTimers()
        {
            foreach (ShopDef shop in World.Content.Shops.Values)
            {
                int[] stock = Stock(shop);
                int[] timers = _restockTimers[shop.Id];
                for (int i = 0; i < shop.Stock.Count; i++)
                {
                    ShopStockDef def = shop.Stock[i];
                    if (def.Quantity < 0) continue;
                    if (stock[i] == def.Quantity)
                    {
                        timers[i] = 0;
                        continue;
                    }
                    if (++timers[i] < Math.Max(1, def.RestockTicks)) continue;
                    timers[i] = 0;
                    stock[i] += stock[i] < def.Quantity ? 1 : -1; // overstock (from sales) drifts back down too
                    _changed.Add(shop.Id);
                }
            }
        }

        public override void SendState(Player player)
        {
            ShopDef? shop = OpenShop(player);
            if (shop != null && _changed.Contains(shop.Id)) player.Connection.Send(Describe(shop));
        }

        public override void OnCleanup() => _changed.Clear();

        public override void OnContentChanged()
        {
            _stock.Clear();
            _restockTimers.Clear();
        }

        /// <summary>Paying in an item, such as marks.</summary>
        private sealed class ItemCurrency : ICurrency
        {
            private readonly World _world;

            public ItemCurrency(World world)
            {
                _world = world;
            }

            public string Name(string currency) => _world.Content.Item(currency)?.Name ?? currency;
            public long Balance(Player player, string currency) => _world.Get<ItemSystem>().CountOf(player, currency);
            public bool TrySpend(Player player, string currency, int amount) => _world.Get<ItemSystem>().Take(player, currency, amount);
            public int Give(Player player, string currency, int amount) => _world.Get<ItemSystem>().Give(player, currency, amount);
        }
    }
}
