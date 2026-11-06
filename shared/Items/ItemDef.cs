using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Skills;

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
        /// <summary>Icon id (the client's icon library); empty means a guess from what the item is.</summary>
        public string Icon = "";
        /// <summary>Its model, a path under the client's Resources ("Art/Assets/Held/Common/Sword": held or worn
        /// art, made in tools/art/blender/assets/held); empty draws a stand-in.</summary>
        public string Model = "";
        /// <summary>Colours swapped on the model: a material's palette name to another palette name
        /// or #RRGGBB ("Copper": "Iron"), so one model serves every metal.</summary>
        public Dictionary<string, string> Recolor = new Dictionary<string, string>();
        /// <summary>For a bank note: the item it stands for. Notes are made for every unstackable item.</summary>
        public string? NoteOf;
        /// <summary>The bank note for this item, if it has one.</summary>
        public string? Note;
        /// <summary>Free-form labels other systems can match on ("food", "reagent", "occult"...).</summary>
        public List<string> Tags = new List<string>();

        public EquipmentDef? Equipment;
        public ConsumableDef? Consumable;
        /// <summary>A gathering tool (pickaxe, hatchet), used from the pack or worn.</summary>
        public ToolDef? Tool;
        /// <summary>Ammunition for a ranged weapon: worn as a stack in the Ammo slot, one used per shot.</summary>
        public AmmoDef? Ammo;
        /// <summary>Water carried (a full waterskin, a bucket): what fire-setting douses with.</summary>
        public WaterDef? Water;
        /// <summary>Worked on by itself, with no station (a grimy herb cleaned): see <see cref="ProcessDef"/>.</summary>
        public ProcessDef? Process;
        /// <summary>Set down as an object for a while (logs lit as a campfire): see <see cref="PlaceDef"/>.</summary>
        public PlaceDef? Place;
        /// <summary>A part for an angler's rig (rod, reel, line, hook, lure): docs/fishing.md 4.</summary>
        public Rth.Shared.Angling.AnglingTackleDef? Tackle;
        /// <summary>Opened from the pack for what's inside (a fish box): see <see cref="OpenDef"/>.</summary>
        public OpenDef? Open;
    }

    /// <summary>
    /// An item opened for what's inside (a fish box): it is used up and rolls <see cref="Rolls"/>
    /// times on its entries, each picked by weight.
    /// </summary>
    public sealed class OpenDef
    {
        public string Verb = "Open";
        public int Rolls = 1;
        public List<OpenEntry> Entries = new List<OpenEntry>();
    }

    public sealed class OpenEntry
    {
        public string Item = "";
        public int Min = 1;
        public int Max = 1;
        public int Weight = 1;
    }

    /// <summary>A number of one item: a cost, an ingredient, a reward.</summary>
    /// <summary>Water carried for dousing (fire-setting): using it leaves <see cref="Leaves"/> (the empty carrier), or nothing.</summary>
    public sealed class WaterDef
    {
        public string? Leaves;
    }

    public sealed class ItemCost
    {
        public string Item = "";
        public int Count = 1;
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
        /// <summary>The quiver: a stack of ammunition for a ranged weapon (the one slot that holds a stack).</summary>
        Ammo = 10,
    }

    /// <summary>
    /// Ammunition (docs/skills.md 3.4): worn in the <see cref="EquipSlot.Ammo"/> slot as a stack and
    /// used one per shot by a ranged weapon that fires its <see cref="Kind"/>. Its stats
    /// (<see cref="EquipmentDef.Stats"/>) count while worn, so better arrows hit harder.
    /// </summary>
    public sealed class AmmoDef
    {
        /// <summary>What it is ("arrow", "stone"); a weapon's <see cref="EquipmentDef.Ammo"/> names the kind it fires.</summary>
        public string Kind = "";
        /// <summary>Its rung on the tier ladder (docs/skills.md 2.2).</summary>
        public int Tier = 1;
    }

    public sealed class EquipmentDef
    {
        public EquipSlot Slot;
        /// <summary>Stat id (see <see cref="StatIds"/>) to the amount it adds while worn.</summary>
        public Dictionary<string, int> Stats = new Dictionary<string, int>();
        /// <summary>Skill levels needed to wear it.</summary>
        public List<SkillRequirement> Requirements = new List<SkillRequirement>();
        /// <summary>
        /// For a weapon: the style it fights with, which decides its auto-attack and which abilities
        /// it can use. None counts as empty hands.
        /// </summary>
        public CombatStyle Style;
        /// <summary>For a weapon: its own auto-attack (an Auto ability), instead of its style's.</summary>
        public string? AutoAttack;
        /// <summary>For a ranged weapon: the kind of ammunition it fires ("arrow", "stone"), worn in the Ammo slot.</summary>
        public string? Ammo;
        /// <summary>
        /// A gear family in the server's gear.json ("plate", "melee-1h"): with <see cref="Tier"/>, it
        /// fills in this item's stats and its requirement by tier (docs/skills.md 2.2). Null: the
        /// stats listed are all it has.
        /// </summary>
        public string? Family;
        /// <summary>Its rung on the tier ladder, for <see cref="Family"/>.</summary>
        public int Tier;
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
        /// <summary>The character animation others see (a clip or role in the client's animations.json).</summary>
        public string Animation = "consume";
    }

    /// <summary>
    /// One generic effect step, shared by consumables now and potions, rituals or skills later.
    /// The server has a handler per <see cref="Op"/>; new ops are registered, not hardcoded.
    /// </summary>
    public sealed class EffectOp
    {
        public const string HealBlood = "healBlood";
        public const string HealBloodPercent = "healBloodPercent";
        /// <summary>Heals <see cref="Amount"/> blood in all, a share each tick over <see cref="Ticks"/> (Medicine's salves), in combat too.</summary>
        public const string HealOverTime = "healOverTime";
        public const string RestoreDevotion = "restoreDevotion";
        /// <summary>Restores a percentage of max devotion.</summary>
        public const string RestoreDevotionPercent = "restoreDevotionPercent";
        public const string AddPhlegm = "addPhlegm";
        public const string ShiftBile = "shiftBile";
        public const string SettleBile = "settleBile";
        public const string ApplyEffect = "applyEffect";
        /// <summary>Boosts <see cref="Skill"/> by <see cref="Amount"/> levels plus <see cref="Percent"/>% for <see cref="Ticks"/> (a draught).</summary>
        public const string BoostSkill = "boostSkill";

        public string Op = "";
        public int Amount;
        /// <summary>For <see cref="ApplyEffect"/>.</summary>
        public EffectType Effect;
        public int Ticks;
        /// <summary>For <see cref="BoostSkill"/>: the skill, and a share of its level added on top of <see cref="Amount"/>.</summary>
        public string? Skill;
        public int Percent;
    }

    /// <summary>
    /// A gathering tool (docs/skills.md 2.3). A node names the kind it needs (and optionally a
    /// lowest tier); the best usable tool of that kind in the pack or worn is used automatically.
    /// Every effect is optional, so two tools of one tier can feel different.
    /// </summary>
    public sealed class ToolDef
    {
        /// <summary>What it is for nodes ("pickaxe", "hatchet"); also the word in messages.</summary>
        public string Kind = "";
        /// <summary>Its rung on the tier ladder (2.2); nodes may ask for a lowest tier, and the best tier is picked.</summary>
        public int Tier = 1;
        /// <summary>Skill levels needed to use it (a tier-2 pickaxe needs mining 10).</summary>
        public List<SkillRequirement> Requirements = new List<SkillRequirement>();
        /// <summary>Added to each attempt's chance of success.</summary>
        public double Success;
        /// <summary>The chance that a success doesn't use the node up.</summary>
        public double Conservation;
        /// <summary>A drop table rolled on each success, besides the node's extras (or instead, with <see cref="ReplacesExtras"/>).</summary>
        public string? Extras;
        public bool ReplacesExtras;
        /// <summary>Chances to change what's gathered as it comes (a hatchet that chars the log).</summary>
        public List<ToolTransform> Transforms = new List<ToolTransform>();
    }

    /// <summary>
    /// A tool's transformation: each <see cref="From"/> a node gives becomes <see cref="To"/> with
    /// <see cref="Chance"/>, if the player meets <see cref="Skill"/> at <see cref="Level"/>, and
    /// gives <see cref="Xp"/> in that skill.
    /// </summary>
    public sealed class ToolTransform
    {
        public string From = "";
        public string To = "";
        public double Chance;
        public string? Skill;
        public int Level = 1;
        public double Xp;
    }
}
