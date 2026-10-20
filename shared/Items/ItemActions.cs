using System;
using System.Collections.Generic;

namespace Rth.Shared.Items
{
    /// <summary>Which item container something is in.</summary>
    public enum ContainerKind : byte
    {
        Inventory = 0,
        Equipment = 1,
        Bank = 2,
    }

    /// <summary>A menu option on an item. <see cref="Id"/> is what the server dispatches on; <see cref="Label"/> is shown.</summary>
    public readonly struct ItemAction
    {
        public ItemAction(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public string Id { get; }
        public string Label { get; }
    }

    /// <summary>
    /// The options an item offers, in menu order (the first is the left-click default). Client and
    /// server both use this, so the menu always matches what the server accepts. Each provider looks
    /// at an item's components; new components add a provider.
    /// </summary>
    public static class ItemActions
    {
        public const string Consume = "consume";
        public const string Equip = "equip";
        public const string Unequip = "unequip";
        public const string Drop = "drop";
        public const string Examine = "examine";

        private static readonly List<Func<ItemDef, ContainerKind, ItemAction?>> Providers = new List<Func<ItemDef, ContainerKind, ItemAction?>>
        {
            (def, where) => where == ContainerKind.Inventory && def.Consumable != null ? new ItemAction(Consume, def.Consumable.Verb) : (ItemAction?)null,
            (def, where) => where == ContainerKind.Inventory && def.Equipment != null
                ? new ItemAction(Equip, def.Equipment.Slot == EquipSlot.Weapon || def.Equipment.Slot == EquipSlot.Offhand ? "Wield" : "Wear")
                : (ItemAction?)null,
            (def, where) => where == ContainerKind.Equipment ? new ItemAction(Unequip, "Remove") : (ItemAction?)null,
            (def, where) => where == ContainerKind.Inventory ? new ItemAction(Drop, "Drop") : (ItemAction?)null,
            (def, where) => new ItemAction(Examine, "Examine"),
        };

        public static List<ItemAction> For(ItemDef def, ContainerKind where)
        {
            var actions = new List<ItemAction>();
            foreach (var provider in Providers)
            {
                ItemAction? action = provider(def, where);
                if (action.HasValue) actions.Add(action.Value);
            }
            return actions;
        }

        public static bool Offers(ItemDef def, ContainerKind where, string actionId)
        {
            foreach (ItemAction a in For(def, where))
                if (a.Id == actionId) return true;
            return false;
        }
    }
}
