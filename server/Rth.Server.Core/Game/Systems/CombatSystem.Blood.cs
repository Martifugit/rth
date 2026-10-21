using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Combat;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>A player's scar stacks (Martyrdom), kept while they stay in combat.</summary>
    public sealed class PlayerScars
    {
        public int Scars;
    }

    /// <summary>
    /// Blood as a resource: what abilities and verses cost in blood, buffs and their conditional
    /// stats, bleeds, and the verse stats that change how a hit is taken (blocking, deferring it
    /// into a bleed, scarring). All of it is driven by stats and ability data, so a new verse is
    /// content: it only needs code if it needs a new stat.
    /// </summary>
    public sealed partial class CombatSystem
    {
        private PlayerScars Of(Player player) => player.Component<PlayerScars>();

        /// <summary>Whether enough scars have built up to make the next ultimate free.</summary>
        public bool HasFreeUltimate(Player player) => Rules.ScarsForFreeUltimate > 0 && Of(player).Scars >= Rules.ScarsForFreeUltimate;

        public int Scars(Player player) => Of(player).Scars;

        /// <summary>
        /// A stat as it stands for this player hitting this target right now: their stat block plus
        /// any conditional buff stats that hold (against the target's tags, at their blood).
        /// </summary>
        public int Stat(Player player, Entity target, string stat)
        {
            int total = player.Stats.Get(stat);
            foreach (ActiveEffect e in player.Effects)
            {
                BuffDef? buff = e.Buff;
                if (buff == null || !buff.Conditional) continue;
                if (buff.WhileBloodBelow > 0 && player.Blood * 100 >= player.MaxBlood * buff.WhileBloodBelow) continue;
                Dictionary<string, int> stats = buff.TargetTag == null || HasTag(target, buff.TargetTag) ? buff.Stats : buff.Otherwise;
                if (stats.TryGetValue(stat, out int value)) total += value;
            }
            return total;
        }

        private static bool HasTag(Entity entity, string tag) => entity is Npc npc && npc.Def.Tags.Contains(tag);

        /// <summary>Puts a buff on the player, replacing an earlier one with the same status.</summary>
        public void ApplyBuff(Player player, BuffDef buff)
        {
            player.Effects.RemoveAll(e => e.Status == buff.Status);
            player.Effects.Add(new ActiveEffect { Buff = buff, EndTick = World.CurrentTick + (uint)buff.Ticks });
            World.RecalculateStats(player);
        }

        /// <summary>The number a buff's status shows: its first stat.</summary>
        private static int BuffMagnitude(BuffDef buff) => buff.Stats.Count == 0 ? 0 : Math.Abs(buff.Stats.Values.First());

        /// <summary>Takes blood as a price (a verse, Hemospike), never the last point. It isn't a hit, so nothing reacts to it.</summary>
        public void PayBlood(Player player, int amount)
        {
            amount = Math.Min(amount, player.Blood - 1);
            if (amount <= 0) return;
            player.Blood -= amount;
            player.HealthChanged = true;
            World.AddHit(new HitEvent { TargetId = player.Id, SourceId = player.Id, Amount = amount, Kind = HitKind.Bleed });
        }

        /// <summary>Starts a bleed: <paramref name="amount"/> blood lost over <paramref name="ticks"/>, credited to <paramref name="source"/>.</summary>
        public void AddBleed(Entity target, int amount, int ticks, Entity? source, CombatStyle style)
        {
            if (amount <= 0 || target.IsDead) return;
            target.Bleeds.Add(new Bleed { Remaining = amount, TicksLeft = Math.Max(1, ticks), SourceId = source?.Id ?? 0, Style = style });
        }

        private void TickBleeds(Entity entity)
        {
            for (int i = 0; i < entity.Bleeds.Count && !entity.IsDead; i++)
            {
                Bleed bleed = entity.Bleeds[i];
                int amount = (bleed.Remaining + bleed.TicksLeft - 1) / bleed.TicksLeft;
                bleed.Remaining -= amount;
                bleed.TicksLeft--;
                Entity? source = bleed.SourceId == 0 ? null : World.FindEntity(bleed.SourceId);
                Damage(entity, amount, source, HitKind.Bleed, bleed.Style);
            }
            entity.Bleeds.RemoveAll(b => b.TicksLeft <= 0 || b.Remaining <= 0);
        }

        /// <summary>
        /// What the player's verses do to a blow they take (after wards and deflection): a block
        /// stops part of it and turns it into phlegm, then deferral holds part of the rest back as a
        /// bleed. Buffs that end on the next hit end here. Returns the damage still taken now.
        /// </summary>
        private int TakeBlow(Player defender, int amount, ref HitKind kind)
        {
            if (amount <= 0) return amount;

            int block = Math.Min(100, defender.Stats.Get(StatIds.BlockHit));
            if (block > 0)
            {
                int blocked = amount * block / 100;
                amount -= blocked;
                int phlegm = defender.Stats.Get(StatIds.BlockedPhlegm);
                if (phlegm > 0) AddPhlegm(defender, Math.Min(phlegm, phlegm * blocked / Math.Max(1, defender.MaxBlood)));
                kind = HitKind.Deflected;
            }

            int defer = Math.Min(100, defender.Stats.Get(StatIds.DeferDamage));
            if (defer > 0 && amount > 0)
            {
                int held = amount * defer / 100;
                amount -= held;
                AddBleed(defender, held, Rules.DeferredBleedTicks, null, CombatStyle.None);
            }

            if (defender.Effects.RemoveAll(e => e.Buff != null && e.Buff.EndsOnHitTaken) > 0) World.RecalculateStats(defender);
            return amount;
        }

        /// <summary>Taking damage (a hit or a bleed) while scarring (Martyrdom) adds scar stacks, up to what a free ultimate needs.</summary>
        private void Scar(Player player)
        {
            int perHit = player.Stats.Get(StatIds.ScarOnHit);
            if (perHit <= 0) return;
            PlayerScars scars = Of(player);
            int cap = Math.Max(1, Rules.ScarsForFreeUltimate);
            bool wasFull = scars.Scars >= cap;
            scars.Scars = Math.Min(cap, scars.Scars + perHit);
            if (!wasFull && scars.Scars >= cap) World.Tell(player, TextKind.Info, "Your scars are many. Your next ultimate costs no phlegm.");
        }
    }
}
