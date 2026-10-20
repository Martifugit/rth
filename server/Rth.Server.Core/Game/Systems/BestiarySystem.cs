using System.Collections.Generic;
using Newtonsoft.Json;
using Rth.Server.Content;
using Rth.Server.Persistence;
using Rth.Shared.Net;
using Rth.Shared.Progress;

namespace Rth.Server.Game
{
    public sealed class PlayerBestiary
    {
        public BestiaryState State = new BestiaryState();
        [JsonIgnore] public bool Dirty = true;
    }

    /// <summary>
    /// The bestiary: how many of each creature a player has killed, and everything each creature
    /// has given them (a drop log). A creature gives twice: its drops, and the yield of butchering
    /// its carcass, which is credited back to it. Sent as "bestiary" state.
    /// </summary>
    public sealed class BestiarySystem : GameSystem
    {
        public const string StateKey = "bestiary";
        private const string SaveSection = "bestiary";

        /// <summary>Recipe id to the creature whose carcass it processes.</summary>
        private Dictionary<string, string> _recipeCreature = new Dictionary<string, string>();

        public PlayerBestiary Of(Player player) => player.Component<PlayerBestiary>();

        protected override void OnAttach()
        {
            BuildRecipeMap();
            World.Events.Subscribe<EntityDied>(e =>
            {
                if (e.Killer is Player p && e.Victim is Npc npc)
                {
                    Entry(p, npc.Def.Id).Kills++;
                    Of(p).Dirty = true;
                }
            });
            World.Events.Subscribe<ItemsObtained>(e =>
            {
                string? creature = e.Source.StartsWith("npc:") ? e.Source.Substring(4)
                    : e.Source.StartsWith("recipe:") && _recipeCreature.TryGetValue(e.Source.Substring(7), out string? c) ? c
                    : null;
                if (creature == null) return;
                BestiaryEntry entry = Entry(e.Player, creature);
                foreach (Drop d in e.Items)
                    entry.Obtained[d.ItemId] = (entry.Obtained.TryGetValue(d.ItemId, out long n) ? n : 0) + d.Count;
                Of(e.Player).Dirty = true;
            });
        }

        private BestiaryEntry Entry(Player player, string npcId)
        {
            Dictionary<string, BestiaryEntry> creatures = Of(player).State.Creatures;
            if (!creatures.TryGetValue(npcId, out BestiaryEntry? entry)) creatures[npcId] = entry = new BestiaryEntry();
            return entry;
        }

        /// <summary>A recipe belongs to a creature if one of its inputs is something that creature drops (its carcass).</summary>
        private void BuildRecipeMap()
        {
            var map = new Dictionary<string, string>();
            var drops = new Dictionary<string, HashSet<string>>();
            foreach (NpcDef npc in World.Content.Npcs.Values)
                if (npc.DropTable != null) drops[npc.Id] = ItemsIn(npc.DropTable, new HashSet<string>(), 0);
            foreach (RecipeDef recipe in World.Content.Recipes.Values)
                foreach (ItemAmount input in recipe.Inputs)
                    foreach (KeyValuePair<string, HashSet<string>> npc in drops)
                        if (npc.Value.Contains(input.Item) && !map.ContainsKey(recipe.Id)) map[recipe.Id] = npc.Key;
            _recipeCreature = map;
        }

        private HashSet<string> ItemsIn(string tableId, HashSet<string> items, int depth)
        {
            if (depth > 8 || !World.Content.DropTables.TryGetValue(tableId, out DropTableDef? table)) return items;
            foreach (DropEntry e in table.Always) Add(e);
            foreach (DropEntry e in table.Entries) Add(e);
            return items;

            void Add(DropEntry e)
            {
                if (e.Item != null) items.Add(e.Item);
                if (e.Table != null) ItemsIn(e.Table, items, depth + 1);
            }
        }

        public override void OnContentChanged()
        {
            BuildRecipeMap();
            foreach (Player player in World.Players) Of(player).Dirty = true;
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            BestiaryState? saved = save?.Section<BestiaryState>(SaveSection);
            if (saved != null) Of(player).State = saved;
            Of(player).Dirty = true;
        }

        public override void OnSave(Player player, PlayerSave save) => save.SetSection(SaveSection, Of(player).State);

        public override void SendState(Player player)
        {
            PlayerBestiary b = Of(player);
            if (!b.Dirty) return;
            b.Dirty = false;
            player.Connection.Send(JsonStateMessage.Of(StateKey, b.State));
        }
    }
}
