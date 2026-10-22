using System.Collections.Generic;
using Rth.Shared.Items;
using Rth.Shared.Skills;

namespace Rth.Server.Content
{
    // ------------------------------------------------------------------ uses (uses.json)

    /// <summary>
    /// What happens when a pack item is used on something ("Use rat gall -> Corvin Sallow"). One
    /// entry names the item, exactly one target (an object type, an NPC type or another item) and
    /// what follows, built from mechanisms that already exist: dialogue conditions and actions,
    /// effect ops, recipes and conversations. The first entry that matches and whose checks pass
    /// wins; a match whose checks fail says <see cref="FailMessage"/>; nothing matching at all says
    /// "Nothing interesting happens."
    /// </summary>
    public sealed class UseDef
    {
        public string Id = "";
        /// <summary>The item used: an item id, or "tag:&lt;tag&gt;" for any item with that tag.</summary>
        public string Item = "";
        /// <summary>Used on a placed object of this type.</summary>
        public string? OnObject;
        /// <summary>Used on an NPC of this type, or "tag:&lt;tag&gt;".</summary>
        public string? OnNpc;
        /// <summary>
        /// Used on another pack item: an id or "tag:&lt;tag&gt;". Either item may be the one picked up
        /// first; <see cref="Item"/> is always the one matching <see cref="Item"/>.
        /// </summary>
        public string? OnItem;

        /// <summary>Skill levels it needs (checked with boosts).</summary>
        public List<SkillRequirement> Requirements = new List<SkillRequirement>();
        /// <summary>Dialogue conditions that must all hold ("flag", "hasItem", "questStage"...).</summary>
        public List<DialogueCondition> Conditions = new List<DialogueCondition>();
        /// <summary>Said when it matches but a requirement or condition fails; null for the fallback line.</summary>
        public string? FailMessage;

        /// <summary>Uses up one of the item used.</summary>
        public bool Consume;
        /// <summary>For an item on an item: uses up one of the other item too.</summary>
        public bool ConsumeTarget;
        /// <summary>A character animation, facing the target.</summary>
        public string? Animation;
        /// <summary>Said to the player ({tokens} filled as in dialogue).</summary>
        public string? Message;
        /// <summary>Effect ops on the player, as a consumable's.</summary>
        public List<EffectOp> Effects = new List<EffectOp>();
        /// <summary>Dialogue actions run in order ("giveItem", "setFlag", "setQuestStage"...).</summary>
        public List<DialogueAction> Actions = new List<DialogueAction>();
        /// <summary>A recipe made from here, repeated while the inputs last (its level, tools and inputs apply).</summary>
        public string? Recipe;
        /// <summary>A conversation started (with the NPC, or spoken by the object's name).</summary>
        public string? Dialogue;

        /// <summary>"object:&lt;type&gt;", "npc:&lt;type&gt;" or "item:&lt;id&gt;": what it names as its target.</summary>
        public string TargetKey =>
            OnObject != null ? "object:" + OnObject : OnNpc != null ? "npc:" + OnNpc : "item:" + OnItem;
    }
}
