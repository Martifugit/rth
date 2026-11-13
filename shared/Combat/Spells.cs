using System.Collections.Generic;
using Rth.Shared.Items;

namespace Rth.Shared.Combat
{
    /// <summary>
    /// spellbooks.json: Occultism's second layer (docs/systems.md "Spellbooks"). Each occult house
    /// has a spellbook; a player learns spells from them (a house contact, a quest), keeps them
    /// forever, and picks one as their active spell, which their occult auto-attack casts. Each
    /// cast burns the spell's reagents; abilities never do.
    /// </summary>
    public sealed class SpellbooksFile
    {
        public List<SpellbookDef> Spellbooks = new List<SpellbookDef>();
        public List<SpellDef> Spells = new List<SpellDef>();
        /// <summary>The skill spells need levels in.</summary>
        public string Skill = "occultism";
    }

    public sealed class SpellbookDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>The house it belongs to (lore; shown in the spellbook).</summary>
        public string House = "";
        public string Description = "";
    }

    public sealed class SpellDef
    {
        public string Id = "";
        public string Name = "";
        public string Spellbook = "";
        public string Description = "";
        public string Icon = "";
        /// <summary>The auto-attack it casts (abilities.json, type Auto, occult): its damage, range, speed, bile and animation.</summary>
        public string Ability = "";
        /// <summary>The level in the spellbooks' skill needed to cast it.</summary>
        public int Level = 1;
        /// <summary>Burnt by every cast.</summary>
        public List<ItemCost> Reagents = new List<ItemCost>();
        /// <summary>Known by every character from the start.</summary>
        public bool Starter;
    }

    /// <summary>The "spells" server state: what the player has learned and which one their auto-attack casts.</summary>
    public sealed class SpellsState
    {
        public const string Key = "spells";
        /// <summary>The intent that sets the active spell (its argument is the spell's id).</summary>
        public const string Intent = "spell";

        public List<string> Known = new List<string>();
        public string Active = "";
    }
}
