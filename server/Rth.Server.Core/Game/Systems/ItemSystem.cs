using System;
using System.Collections.Generic;
using Rth.Server.Content;
using Rth.Server.Items;
using Rth.Server.Persistence;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>A player's item containers, attached to them as a component.</summary>
    public sealed class PlayerItems
    {
        public ItemContainer Inventory = new ItemContainer(28);
        public ItemContainer Equipment = new ItemContainer(Enum.GetValues(typeof(EquipSlot)).Length);

        public ItemContainer? Get(ContainerKind kind) => kind switch
        {
            ContainerKind.Inventory => Inventory,
            ContainerKind.Equipment => Equipment,
            _ => null,
        };
    }

    /// <summary>The item an action was used on, and where it is.</summary>
    public sealed class ItemUse
    {
        public ItemUse(ContainerKind kind, ItemContainer container, int slot, ItemDef def)
        {
            Kind = kind;
            Container = container;
            Slot = slot;
            Def = def;
        }

        public ContainerKind Kind { get; }
        public ItemContainer Container { get; }
        public int Slot { get; }
        public ItemDef Def { get; }
        public int Count => Container[Slot].Count;
    }

    /// <summary>
    /// Inventories and equipment: item actions, equipment stats, consumables, starter kits and
    /// saving. Two registries make it extensible: <see cref="RegisterAction"/> (what an item menu
    /// option does; e.g. the ground-item system adds "drop") and <see cref="RegisterEffectOp"/>
    /// (what a consumable effect does; Medicine potions or rituals can add their own).
    /// </summary>
    public sealed class ItemSystem : GameSystem, IStatContributor
    {
        private const string SaveSection = "items";

        private readonly Dictionary<string, Action<Player, ItemUse>> _actions = new Dictionary<string, Action<Player, ItemUse>>();
        private readonly Dictionary<string, Action<Player, EffectOp>> _effectOps = new Dictionary<string, Action<Player, EffectOp>>();

        private sealed class ItemsSave
        {
            public List<SavedSlot> Inventory = new List<SavedSlot>();
            public List<SavedSlot> Equipment = new List<SavedSlot>();
        }

        private sealed class SavedSlot
        {
            public int Slot;
            public string Item = "";
            public int Count = 1;
        }

        public void RegisterAction(string actionId, Action<Player, ItemUse> handler) => _actions[actionId] = handler;

        public void RegisterEffectOp(string op, Action<Player, EffectOp> handler) => _effectOps[op] = handler;

        public PlayerItems Of(Player player) => player.Component<PlayerItems>();

        protected override void OnAttach()
        {
            World.OnMessage<ItemActionMessage>(HandleItemAction);
            World.OnMessage<MoveItemMessage>(HandleMove);

            RegisterAction(ItemActions.Consume, Consume);
            RegisterAction(ItemActions.Equip, Equip);
            RegisterAction(ItemActions.Unequip, Unequip);
            RegisterAction(ItemActions.Examine, (player, use) =>
                World.Tell(player, TextKind.Info, string.IsNullOrEmpty(use.Def.Examine) ? "It's " + use.Def.Name.ToLowerInvariant() + "." : use.Def.Examine));

            CombatSystem Combat() => World.Get<CombatSystem>();
            RegisterEffectOp(EffectOp.HealBlood, (p, op) => Combat().Heal(p, op.Amount));
            RegisterEffectOp(EffectOp.HealBloodPercent, (p, op) => Combat().Heal(p, p.MaxBlood * op.Amount / 100));
            RegisterEffectOp(EffectOp.AddPhlegm, (p, op) => Combat().AddPhlegm(p, op.Amount));
            RegisterEffectOp(EffectOp.ShiftBile, (p, op) => Combat().ShiftBile(p, op.Amount));
            RegisterEffectOp(EffectOp.SettleBile, (p, op) => Combat().SettleBile(p, op.Amount));
            RegisterEffectOp(EffectOp.ApplyEffect, (p, op) => Combat().ApplyEffect(p, op.Effect, op.Amount, op.Ticks));
        }

        // ------------------------------------------------------------------ public API for other systems

        /// <summary>Puts items in the player's inventory. Returns how many didn't fit.</summary>
        public int Give(Player player, string itemId, int count)
        {
            ItemDef? def = World.Content.Item(itemId);
            if (def == null) return count;
            return Of(player).Inventory.Add(def, count);
        }

        public bool CanGive(Player player, string itemId, int count)
        {
            ItemDef? def = World.Content.Item(itemId);
            return def != null && Of(player).Inventory.CanAdd(def, count);
        }

        public int CountOf(Player player, string itemId) => Of(player).Inventory.CountOf(itemId);

        /// <summary>Removes items from the inventory if there are enough. Returns whether it did.</summary>
        public bool Take(Player player, string itemId, int count)
        {
            ItemContainer inv = Of(player).Inventory;
            if (inv.CountOf(itemId) < count) return false;
            inv.Remove(itemId, count);
            return true;
        }

        /// <summary>Runs a list of effect operations on a player (consumables, and anything else that wants to).</summary>
        public void ApplyEffects(Player player, IEnumerable<EffectOp> ops)
        {
            foreach (EffectOp op in ops)
            {
                if (_effectOps.TryGetValue(op.Op, out Action<Player, EffectOp>? handler)) handler(player, op);
                else World.Log("No handler for effect op '" + op.Op + "'.");
            }
        }

        // ------------------------------------------------------------------ messages

        private void HandleItemAction(Player player, ItemActionMessage message)
        {
            if (player.IsDead) return;
            ItemContainer? container = Of(player).Get(message.Container);
            if (container == null || !container.InRange(message.Slot) || container.IsEmpty(message.Slot)) return;
            ItemDef? def = World.Content.Item(container[message.Slot].ItemId);
            if (def == null)
            {
                World.Tell(player, TextKind.Warning, "You can't make sense of that item any more.");
                return;
            }
            if (!ItemActions.Offers(def, message.Container, message.ActionId)) return;
            if (_actions.TryGetValue(message.ActionId, out Action<Player, ItemUse>? handler))
                handler(player, new ItemUse(message.Container, container, message.Slot, def));
        }

        private void HandleMove(Player player, MoveItemMessage message)
        {
            ItemContainer inv = Of(player).Inventory;
            if (inv.InRange(message.From) && inv.InRange(message.To) && message.From != message.To) inv.Swap(message.From, message.To);
        }

        // ------------------------------------------------------------------ built-in actions

        private void Consume(Player player, ItemUse use)
        {
            ConsumableDef c = use.Def.Consumable!;
            string key = "item:" + c.CooldownGroup;
            uint now = World.CurrentTick;
            if (player.Cooldowns.TryGetValue(key, out uint ready) && now < ready) return;

            use.Container.RemoveAt(use.Slot, 1);
            if (c.Leaves != null && use.Container.IsEmpty(use.Slot)) use.Container.Set(use.Slot, c.Leaves, 1);
            else if (c.Leaves != null) Give(player, c.Leaves, 1);

            World.Tell(player, TextKind.Info, "You " + c.Verb.ToLowerInvariant() + " the " + use.Def.Name.ToLowerInvariant() + ".");
            ApplyEffects(player, c.Effects);
            player.Cooldowns[key] = now + (uint)Math.Max(1, c.CooldownTicks);
            player.ActionDelayUntilTick = Math.Max(player.ActionDelayUntilTick, now + (uint)c.AttackDelayTicks);
        }

        private void Equip(Player player, ItemUse use)
        {
            string? problem = World.Find<SkillSystem>()?.Check(player, use.Def.Equipment!.Requirements, "wear the " + use.Def.Name.ToLowerInvariant());
            if (problem != null)
            {
                World.Tell(player, TextKind.Warning, problem);
                return;
            }
            int slot = (int)use.Def.Equipment!.Slot;
            ItemContainer equipment = Of(player).Equipment;
            ItemSlot worn = equipment[slot];
            equipment.Set(slot, use.Def.Id, 1);
            if (worn.IsEmpty) use.Container.Clear(use.Slot);
            else use.Container.Set(use.Slot, worn.ItemId, worn.Count);
            World.RecalculateStats(player);
        }

        private void Unequip(Player player, ItemUse use)
        {
            ItemContainer inv = Of(player).Inventory;
            int free = inv.FirstFreeSlot();
            if (free < 0)
            {
                World.Tell(player, TextKind.Warning, "You don't have enough inventory space.");
                return;
            }
            inv.Set(free, use.Def.Id, use.Count);
            use.Container.Clear(use.Slot);
            World.RecalculateStats(player);
        }

        // ------------------------------------------------------------------ stats

        public void Contribute(Player player, StatBlock stats)
        {
            ItemContainer equipment = Of(player).Equipment;
            for (int i = 0; i < equipment.Size; i++)
            {
                ItemDef? def = equipment.IsEmpty(i) ? null : World.Content.Item(equipment[i].ItemId);
                if (def?.Equipment == null) continue;
                foreach (KeyValuePair<string, int> stat in def.Equipment.Stats) stats.Add(stat.Key, stat.Value);
            }
        }

        public override void OnContentChanged()
        {
            foreach (Player player in World.Players) World.RecalculateStats(player);
        }

        // ------------------------------------------------------------------ login, saving, sync

        public override void OnLogin(Player player, PlayerSave? save)
        {
            PlayerItems items = Of(player);
            items.Inventory = new ItemContainer(World.Content.World.InventorySize);
            ItemsSave? saved = save?.Section<ItemsSave>(SaveSection);

            if (saved == null)
            {
                // A new character (or one from before items existed) gets the starter kit.
                foreach (ItemAmount start in World.Content.World.StarterInventory) Give(player, start.Item, start.Count);
                foreach (string id in World.Content.World.StarterEquipment)
                {
                    ItemDef? def = World.Content.Item(id);
                    if (def?.Equipment != null) items.Equipment.Set((int)def.Equipment.Slot, id, 1);
                }
            }
            else
            {
                Restore(items.Inventory, saved.Inventory);
                Restore(items.Equipment, saved.Equipment);
            }
            World.RecalculateStats(player);
        }

        private void Restore(ItemContainer container, List<SavedSlot> slots)
        {
            foreach (SavedSlot s in slots)
            {
                if (container.InRange(s.Slot) && container.IsEmpty(s.Slot)) container.Set(s.Slot, s.Item, s.Count);
                else World.Log("Dropped saved item " + s.Item + " x" + s.Count + ": slot " + s.Slot + " unavailable.");
            }
        }

        public override void OnSave(Player player, PlayerSave save)
        {
            PlayerItems items = Of(player);
            save.SetSection(SaveSection, new ItemsSave { Inventory = ToSaved(items.Inventory), Equipment = ToSaved(items.Equipment) });
        }

        private static List<SavedSlot> ToSaved(ItemContainer container)
        {
            var list = new List<SavedSlot>();
            for (int i = 0; i < container.Size; i++)
                if (!container.IsEmpty(i)) list.Add(new SavedSlot { Slot = i, Item = container[i].ItemId, Count = container[i].Count });
            return list;
        }

        public override void SendState(Player player)
        {
            PlayerItems items = Of(player);
            Send(player, ContainerKind.Inventory, items.Inventory);
            Send(player, ContainerKind.Equipment, items.Equipment);
        }

        private static void Send(Player player, ContainerKind kind, ItemContainer container)
        {
            if (!container.Dirty) return;
            container.Dirty = false;
            player.Connection.Send(new ContainerMessage { Kind = kind, Slots = container.Snapshot() });
        }
    }
}
