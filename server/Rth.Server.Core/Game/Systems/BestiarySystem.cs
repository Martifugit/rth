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
        [JsonIgnore] public bool GuideSent;
    }

    /// <summary>
    /// The bestiary: how many of each creature a player has killed, and everything each creature
    /// has given them (a drop log). A creature gives twice: its drops, and the yield of butchering
    /// its carcass, which is credited back to it. Bosses are here too: completions, attempts,
    /// best and last times, and what their rewards gave. Sent as "bestiary" state, with the
    /// <see cref="BestiaryGuide"/> (worked out from content and spawns) as "bestiaryGuide".
    /// </summary>
    public sealed class BestiarySystem : GameSystem
    {
        public const string StateKey = "bestiary";
        /// <summary>The same for everyone: sent at login and after content changes.</summary>
        public const string GuideKey = "bestiaryGuide";
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
            World.Events.Subscribe<BossRunEnded>(e =>
            {
                BossRecord record = Boss(e.Player, e.Boss.Id);
                record.Attempts++;
                if (e.Won)
                {
                    record.Completions++;
                    record.LastTicks = e.Ticks;
                    if (record.BestTicks == 0 || e.Ticks < record.BestTicks) record.BestTicks = e.Ticks;
                }
                Of(e.Player).Dirty = true;
            });
            World.Events.Subscribe<ItemsObtained>(e =>
            {
                if (e.Source.StartsWith("boss:"))
                {
                    BossRecord record = Boss(e.Player, e.Source.Substring(5));
                    foreach (Drop d in e.Items)
                        record.Obtained[d.ItemId] = (record.Obtained.TryGetValue(d.ItemId, out long had) ? had : 0) + d.Count;
                    Of(e.Player).Dirty = true;
                    return;
                }
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

        /// <summary>The player's record with a boss (made on first use).</summary>
        public BossRecord Boss(Player player, string bossId)
        {
            Dictionary<string, BossRecord> bosses = Of(player).State.Bosses;
            if (!bosses.TryGetValue(bossId, out BossRecord? record)) bosses[bossId] = record = new BossRecord();
            return record;
        }

        private BestiaryEntry Entry(Player player, string npcId)
        {
            Dictionary<string, BestiaryEntry> creatures = Of(player).State.Creatures;
            if (!creatures.TryGetValue(npcId, out BestiaryEntry? entry)) creatures[npcId] = entry = new BestiaryEntry();
            return entry;
        }

        private void BuildRecipeMap()
        {
            _recipeCreature = BestiaryGuideBuilder.CarcassRecipes(World.Content);
            _guide = null;
        }

        /// <summary>
        /// The guide (where creatures live, their carcasses and notable drops), from content and
        /// the map's spawns. Built when first asked for after a content change.
        /// </summary>
        public BestiaryGuide Guide => _guide ??= BestiaryGuideBuilder.Build(
            World.Content, World.Find<NpcSystem>()?.Spawns ?? (IReadOnlyList<NpcSpawn>)new List<NpcSpawn>(), World.Regions);

        private BestiaryGuide? _guide;

        public override void OnContentChanged()
        {
            BuildRecipeMap();
            foreach (Player player in World.Players)
            {
                Of(player).Dirty = true;
                Of(player).GuideSent = false;
            }
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
            if (!b.GuideSent)
            {
                b.GuideSent = true;
                player.Connection.Send(JsonStateMessage.Of(GuideKey, Guide));
            }
            if (!b.Dirty) return;
            b.Dirty = false;
            player.Connection.Send(JsonStateMessage.Of(StateKey, b.State));
        }
    }
}
