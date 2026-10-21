using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Persistence;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Religion;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>A player's hymns being sung, in the order they were started.</summary>
    public sealed class PlayerReligion
    {
        public List<string> Singing = new List<string>();
    }

    /// <summary>
    /// Religion: Tithe's hymns (prayers for every style) and the devotion they drain. A hymn's
    /// effect is its stats, so combat needs no hymn code; this system decides which hymns are sung,
    /// drains devotion each tick and restores it (altars, draughts, respawning). Cruor's verses
    /// belong here too, when melee exists.
    /// </summary>
    /// <remarks>
    /// Any number of hymns can be sung at once, except that only one overhead is up at a time, and
    /// a hymn replaces the others in its line (Fervour replaces Zeal). Running out of devotion
    /// silences them all. Singing in combat trains Religion a little.
    /// </remarks>
    public sealed class ReligionSystem : GameSystem, IStatContributor
    {
        private const string SaveSection = "religion";
        /// <summary>The object action that restores devotion in full (altars, shrines, waystones).</summary>
        public const string PrayAction = "pray";

        private sealed class SavedReligion
        {
            public double Devotion;
        }

        protected override void OnAttach()
        {
            World.OnMessage<SetHymnMessage>((p, m) => SetHymn(p, m.HymnId, m.On));
            World.Events.Subscribe<EntityDied>(OnDied);

            ItemSystem? items = World.Find<ItemSystem>();
            items?.RegisterEffectOp(EffectOp.RestoreDevotion, (p, op) => Restore(p, op.Amount));
            items?.RegisterEffectOp(EffectOp.RestoreDevotionPercent, (p, op) => Restore(p, p.MaxDevotion * op.Amount / 100.0));

            World.Find<ObjectSystem>()?.RegisterAction(PrayAction, (player, placed, _) =>
                World.StartInteraction(player, new PrayAtObject(placed)));
        }

        public PlayerReligion Of(Player player) => player.Component<PlayerReligion>();

        /// <summary>The ids of the hymns the player is singing.</summary>
        public IReadOnlyList<string> Singing(Player player) => Of(player).Singing;

        public bool IsSinging(Player player, string hymnId) => Of(player).Singing.Contains(hymnId);

        // ------------------------------------------------------------------ singing

        /// <summary>Starts or stops a hymn, telling the player why if it can't be sung.</summary>
        public void SetHymn(Player player, string hymnId, bool on)
        {
            if (player.IsDead) return;
            PlayerReligion religion = Of(player);
            if (!on)
            {
                if (religion.Singing.Remove(hymnId)) World.RecalculateStats(player);
                return;
            }
            if (!World.Content.Hymns.TryGetValue(hymnId, out HymnDef? hymn) || religion.Singing.Contains(hymnId)) return;

            string? problem = World.Find<SkillSystem>()?.Check(player, hymn.Requirements, "sing " + hymn.Name);
            if (problem != null)
            {
                World.Tell(player, TextKind.Warning, problem);
                return;
            }
            if (player.Devotion <= 0)
            {
                World.Tell(player, TextKind.Warning, "You have no devotion left. Pray at an altar to restore it.");
                return;
            }
            religion.Singing.RemoveAll(id => World.Content.Hymns.TryGetValue(id, out HymnDef? other) && other.ExcludedBy(hymn));
            religion.Singing.Add(hymnId);
            World.RecalculateStats(player);
        }

        /// <summary>Stops every hymn the player is singing.</summary>
        public void Silence(Player player)
        {
            PlayerReligion religion = Of(player);
            if (religion.Singing.Count == 0) return;
            religion.Singing.Clear();
            World.RecalculateStats(player);
        }

        public void Contribute(Player player, StatBlock stats)
        {
            // Stats are recalculated whenever the hymns sung change, so the overhead follows here too.
            player.SetOverhead(Of(player).Singing.FirstOrDefault(id => World.Content.Hymns.TryGetValue(id, out HymnDef? h) && h.Overhead));
            foreach (string id in Of(player).Singing)
                if (World.Content.Hymns.TryGetValue(id, out HymnDef? hymn))
                    foreach (KeyValuePair<string, int> stat in hymn.Stats) stats.Add(stat.Key, stat.Value);
        }

        // ------------------------------------------------------------------ devotion

        /// <summary>Devotion drained per tick by what the player sings now, after their devotion bonus.</summary>
        public double DrainPerTick(Player player)
        {
            double drain = 0;
            foreach (string id in Of(player).Singing)
                if (World.Content.Hymns.TryGetValue(id, out HymnDef? hymn)) drain += hymn.Drain;
            double bonus = player.Stats.Get(Rth.Shared.Combat.StatIds.DevotionBonus);
            return drain * Math.Max(0, 1 - bonus / 100.0);
        }

        public void Restore(Player player, double amount)
        {
            if (amount <= 0) return;
            player.Devotion = Math.Min(player.MaxDevotion, player.Devotion + amount);
        }

        public override void AfterPlayer(Player player)
        {
            if (player.IsDead || Of(player).Singing.Count == 0) return;
            double drain = Math.Min(player.Devotion, DrainPerTick(player));
            player.Devotion -= drain;

            HymnsFile settings = World.Content.HymnSettings;
            if (drain > 0 && World.Find<CombatSystem>()?.InCombat(player) == true)
                World.Find<SkillSystem>()?.AddXp(player, settings.Skill, drain * settings.XpPerDevotion);

            if (player.Devotion > 0) return;
            player.Devotion = 0;
            Silence(player);
            World.Tell(player, TextKind.Warning, "Your devotion is spent. Your hymns fall silent.");
        }

        private void OnDied(EntityDied e)
        {
            if (!(e.Victim is Player player)) return;
            Silence(player);
            player.Devotion = player.MaxDevotion; // restored when the Assayer puts you back together
        }

        public override void OnContentChanged()
        {
            foreach (Player player in World.Players)
                if (Of(player).Singing.RemoveAll(id => !World.Content.Hymns.ContainsKey(id)) > 0) World.RecalculateStats(player);
        }

        // ------------------------------------------------------------------ saving

        public override void OnLogin(Player player, PlayerSave? save)
        {
            // Stats (and so max devotion) were recalculated by the systems before this one.
            SavedReligion? saved = save?.Section<SavedReligion>(SaveSection);
            player.Devotion = Math.Max(0, Math.Min(player.MaxDevotion, saved?.Devotion ?? player.MaxDevotion));
        }

        public override void OnSave(Player player, PlayerSave save) =>
            save.SetSection(SaveSection, new SavedReligion { Devotion = player.Devotion });
    }

    /// <summary>Walks to an altar (or anything with a "pray" action) and restores devotion in full.</summary>
    public sealed class PrayAtObject : ObjectInteraction
    {
        public PrayAtObject(PlacedObject altar) : base(altar) { }

        protected override bool Work(World world, Player player)
        {
            if (player.Devotion >= player.MaxDevotion)
            {
                world.Tell(player, TextKind.Info, "Your devotion is already full.");
                return false;
            }
            world.Get<ReligionSystem>().Restore(player, player.MaxDevotion);
            world.Tell(player, TextKind.Info, "You pray. Your devotion is restored.");
            return false;
        }
    }
}
