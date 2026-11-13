using System.Collections.Generic;
using System.Linq;
using Rth.Server.Content;
using Rth.Server.Persistence;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>
    /// Occultism's spellbooks (spellbooks.json, docs/systems.md "Spellbooks"): which spells a
    /// player knows, and the active one their occult auto-attack casts. Spells are learned once
    /// (the dialogue action <c>learnSpell</c>, from a house contact or a quest) and kept forever;
    /// starter spells are known from the start. The active spell is chosen with the "spell"
    /// intent; each cast burns its reagents (<see cref="CastProblem"/>, <see cref="PayForCast"/>,
    /// called by combat). Saved as the "spells" section; clients get the "spells" state.
    /// </summary>
    public sealed class SpellSystem : GameSystem
    {
        public const string SaveKey = "spells";

        private sealed class Saved
        {
            public List<string> Known = new List<string>();
            public string? Active;
        }

        public sealed class PlayerSpells
        {
            public readonly HashSet<string> Known = new HashSet<string>();
            public string? Active;
            public bool Dirty = true;
        }

        protected override void OnAttach()
        {
            World.Find<IntentSystem>()?.Register(SpellsState.Intent, (player, id) => SetActive(player, id));
            DialogueSystem? dialogue = World.Find<DialogueSystem>();
            dialogue?.RegisterAction("learnSpell", (player, action, _) =>
            {
                if (action.Id != null) Learn(player, action.Id);
            });
            dialogue?.RegisterCondition("knowsSpell", (player, condition, _) => condition.Id != null && Knows(player, condition.Id));
        }

        public PlayerSpells Of(Player player) => player.Component<PlayerSpells>();

        public bool Knows(Player player, string spellId) => Of(player).Known.Contains(spellId);

        /// <summary>Learns a spell for good. Returns false if there is no such spell or it was known already.</summary>
        public bool Learn(Player player, string spellId)
        {
            if (!World.Content.Spells.TryGetValue(spellId, out SpellDef? spell)) return false;
            PlayerSpells spells = Of(player);
            if (!spells.Known.Add(spellId)) return false;
            spells.Dirty = true;
            string book = World.Content.Spellbooks.TryGetValue(spell.Spellbook, out SpellbookDef? b) ? b.Name : spell.Spellbook;
            World.Tell(player, TextKind.Info, "You learn " + spell.Name + ", from " + book + ".");
            if (spells.Active == null && LevelProblem(player, spell) == null) spells.Active = spellId;
            return true;
        }

        /// <summary>Makes a known spell the one the auto-attack casts. Tells the player why not if it can't be.</summary>
        public bool SetActive(Player player, string spellId)
        {
            if (!World.Content.Spells.TryGetValue(spellId, out SpellDef? spell) || !Knows(player, spellId))
            {
                World.Tell(player, TextKind.Warning, "You don't know that spell.");
                return false;
            }
            string? problem = LevelProblem(player, spell);
            if (problem != null)
            {
                World.Tell(player, TextKind.Warning, problem);
                return false;
            }
            PlayerSpells spells = Of(player);
            if (spells.Active == spellId) return true;
            spells.Active = spellId;
            spells.Dirty = true;
            World.Tell(player, TextKind.Info, "Your occult attacks now cast " + spell.Name + ".");
            return true;
        }

        /// <summary>The spell the player's occult auto-attack casts, or null for none (it falls back to the plain auto-attack).</summary>
        public SpellDef? Active(Player player)
        {
            string? id = Of(player).Active;
            return id != null && World.Content.Spells.TryGetValue(id, out SpellDef? spell) && Knows(player, id) && LevelProblem(player, spell) == null ? spell : null;
        }

        /// <summary>The auto-attack the active spell casts, or null.</summary>
        public AbilityDef? ActiveAbility(Player player)
        {
            SpellDef? spell = Active(player);
            return spell != null && World.Content.Abilities.TryGetValue(spell.Ability, out AbilityDef? ability) ? ability : null;
        }

        private string? LevelProblem(Player player, SpellDef spell)
        {
            SkillSystem? skills = World.Find<SkillSystem>();
            if (skills == null || skills.Level(player, World.Content.SpellSkill) >= spell.Level) return null;
            return spell.Name + " needs " + skills.Name(World.Content.SpellSkill) + " " + spell.Level + ".";
        }

        /// <summary>Why the active spell can't be cast now (out of a reagent), or null.</summary>
        public string? CastProblem(Player player)
        {
            SpellDef? spell = Active(player);
            ItemSystem? items = World.Find<ItemSystem>();
            if (spell == null || items == null) return null;
            foreach (ItemCost reagent in spell.Reagents)
                if (items.CountOf(player, reagent.Item) < reagent.Count)
                    return "You're out of " + (World.Content.Item(reagent.Item)?.Name.ToLowerInvariant() ?? reagent.Item) + " for " + spell.Name + ".";
            return null;
        }

        /// <summary>Burns the active spell's reagents for one cast.</summary>
        public void PayForCast(Player player)
        {
            SpellDef? spell = Active(player);
            ItemSystem? items = World.Find<ItemSystem>();
            if (spell == null || items == null) return;
            foreach (ItemCost reagent in spell.Reagents) items.Take(player, reagent.Item, reagent.Count);
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            PlayerSpells spells = Of(player);
            Saved? saved = save?.Section<Saved>(SaveKey);
            if (saved != null)
                foreach (string id in saved.Known)
                    if (World.Content.Spells.ContainsKey(id)) spells.Known.Add(id);
            foreach (SpellDef spell in World.Content.SpellList)
                if (spell.Starter) spells.Known.Add(spell.Id);
            spells.Active = saved?.Active != null && spells.Known.Contains(saved.Active)
                ? saved.Active
                : World.Content.SpellList.FirstOrDefault(s => s.Starter)?.Id;
            spells.Dirty = true;
        }

        public override void OnSave(Player player, PlayerSave save)
        {
            PlayerSpells spells = Of(player);
            save.SetSection(SaveKey, new Saved { Known = spells.Known.OrderBy(k => k).ToList(), Active = spells.Active });
        }

        public override void OnContentChanged()
        {
            foreach (Player player in World.Players) Of(player).Dirty = true;
        }

        public override void SendState(Player player)
        {
            PlayerSpells spells = Of(player);
            if (!spells.Dirty) return;
            spells.Dirty = false;
            player.Connection.Send(JsonStateMessage.Of(SpellsState.Key, new SpellsState
            {
                Known = World.Content.SpellList.Where(s => spells.Known.Contains(s.Id)).Select(s => s.Id).ToList(),
                Active = spells.Active ?? "",
            }));
        }
    }
}
