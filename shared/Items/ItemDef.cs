using System.Collections.Generic;
using Rth.Shared.Combat;

namespace Rth.Shared.Items
{
    /// <summary>
    /// An item type, loaded from server/data/content/items.json and sent to clients. What an item
    /// can do comes from its optional components: equipment, consumable, and later carcass,
    /// reagent, tool and so on. Adding a capability means adding a component here, an action in
    /// <see cref="ItemActions"/>, and a handler on the server.
    /// </summary>
    public sealed class ItemDef
    {
        public string Id = "";
        public string Name = "";
        public string Examine = "";
        /// <summary>Stackable items share one slot however many there are (coins, glands, beads).</summary>
        public bool Stackable;
        public int Value;
        /// <summary>#RRGGBB tint for the placeholder icon until items have real art.</summary>
        public string Color = "#a0a0a0";
        /// <summary>Free-form labels other systems can match on ("food", "reagent", "occult"...).</summary>
        public List<string> Tags = new List<string>();

        public EquipmentDef? Equipment;
        public ConsumableDef? Consumable;
    }

    public enum EquipSlot : byte
    {
        Head = 0,
        Cape = 1,
        Neck = 2,
        Weapon = 3,
        Body = 4,
        Offhand = 5,
        Legs = 6,
        Hands = 7,
        Feet = 8,
        Ring = 9,
    }

    public sealed class EquipmentDef
    {
        public EquipSlot Slot;
        /// <summary>Stat id (see <see cref="StatIds"/>) to the amount it adds while worn.</summary>
        public Dictionary<string, int> Stats = new Dictionary<string, int>();
    }

    /// <summary>Something eaten or drunk: applies its effect operations, then is used up.</summary>
    public sealed class ConsumableDef
    {
        /// <summary>The menu word: "Eat", "Drink"...</summary>
        public string Verb = "Eat";
        /// <summary>Consumables in the same group share a cooldown (food, potions).</summary>
        public string CooldownGroup = "food";
        public int CooldownTicks = 2;
        /// <summary>How long it holds up your next attack.</summary>
        public int AttackDelayTicks = 2;
        public List<EffectOp> Effects = new List<EffectOp>();
        /// <summary>Item left behind in the same slot (an empty vial), or null.</summary>
        public string? Leaves;
    }

    /// <summary>
    /// One generic effect step, shared by consumables now and potions, rituals or skills later.
    /// The server has a handler per <see cref="Op"/>; new ops are registered, not hardcoded.
    /// </summary>
    public sealed class EffectOp
    {
        public const string HealBlood = "healBlood";
        public const string HealBloodPercent = "healBloodPercent";
        public const string RestoreBreath = "restoreBreath";
        public const string AddPhlegm = "addPhlegm";
        public const string ShiftBile = "shiftBile";
        public const string SettleBile = "settleBile";
        public const string ApplyEffect = "applyEffect";

        public string Op = "";
        public int Amount;
        /// <summary>For <see cref="ApplyEffect"/>.</summary>
        public EffectType Effect;
        public int Ticks;
    }
}
