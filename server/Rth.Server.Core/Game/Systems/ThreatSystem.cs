using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Combat;

namespace Rth.Server.Game
{
    /// <summary>
    /// Who a creature is angriest at, and who earned its loot. Every blow a player lands adds to
    /// their threat on the creature (their damage, raised by the <see cref="StatIds.Threat"/> stat)
    /// and to their share of the damage. A creature fighting several players turns on whoever
    /// holds clearly the most threat (<see cref="SwitchMargin"/>), so a group
    /// can keep it on whoever is best placed to take it; a taunt (<see cref="AbilityDef.TauntTicks"/>)
    /// takes it outright for a while. Loot goes to whoever did the most damage, not whoever landed the last
    /// blow. A creature that resets (leashes home, dies, respawns) forgets everyone.
    /// </summary>
    public sealed class ThreatSystem : GameSystem
    {
        /// <summary>One creature's grudges.</summary>
        public sealed class NpcThreat
        {
            public readonly Dictionary<int, double> Threat = new Dictionary<int, double>();
            public readonly Dictionary<int, int> Damage = new Dictionary<int, int>();
            /// <summary>The name of each player who hurt it, by entity id, so a share can still be named after they left.</summary>
            public readonly Dictionary<int, string> Names = new Dictionary<int, string>();
            /// <summary>Who taunted it last, and until when it fights only them.</summary>
            public int TauntedBy;
            public uint TauntedUntil;

            public void Clear()
            {
                Threat.Clear();
                Damage.Clear();
                Names.Clear();
                TauntedBy = 0;
                TauntedUntil = 0;
            }
        }

        /// <summary>A creature turns only to someone with this much more threat than its target has (1.1: 10% more).</summary>
        public const double SwitchMargin = 1.1;

        protected override void OnAttach()
        {
            World.Events.Subscribe<DamageDealt>(OnDamage);
        }

        public NpcThreat Of(Npc npc) => npc.Component<NpcThreat>();

        private void OnDamage(DamageDealt e)
        {
            if (!(e.Target is Npc npc) || !(e.Source is Player player)) return;
            NpcThreat threat = Of(npc);
            threat.Damage[player.Id] = (threat.Damage.TryGetValue(player.Id, out int dealt) ? dealt : 0) + e.Amount;
            threat.Names[player.Id] = player.Name;
            Add(npc, player, e.Amount * (1 + player.Stats.Get(StatIds.Threat) / 100.0));
        }

        /// <summary>Adds threat (from damage, healing a friend, a taunt) without counting as damage.</summary>
        public void Add(Npc npc, Player player, double amount)
        {
            if (amount <= 0) return;
            NpcThreat threat = Of(npc);
            threat.Threat[player.Id] = (threat.Threat.TryGetValue(player.Id, out double held) ? held : 0) + amount;
        }

        /// <summary>A taunt: the creature turns on the player at once and fights only them for <paramref name="ticks"/>; their threat becomes the highest.</summary>
        public void Taunt(Npc npc, Player player, int ticks)
        {
            NpcThreat threat = Of(npc);
            threat.TauntedBy = player.Id;
            threat.TauntedUntil = World.CurrentTick + (uint)Math.Max(1, ticks);
            double top = threat.Threat.Count == 0 ? 0 : threat.Threat.Values.Max();
            threat.Threat[player.Id] = Math.Max(top * SwitchMargin + 1, threat.Threat.TryGetValue(player.Id, out double held) ? held : 0);
            if (npc.Def.Attacks && !npc.Returning)
            {
                npc.TargetId = player.Id;
                npc.ChaseGoal = null;
            }
        }

        public void Forget(Npc npc) => Of(npc).Clear();

        /// <summary>
        /// Who a creature should fight now: its target, unless someone it can still reach holds
        /// clearly more threat. Players who died, left or went to another floor are forgotten.
        /// </summary>
        public Entity? Choose(Npc npc, Entity? current)
        {
            NpcThreat threat = Of(npc);
            if (threat.TauntedBy != 0 && World.CurrentTick < threat.TauntedUntil &&
                World.FindEntity(threat.TauntedBy) is Player taunter && !taunter.IsDead && taunter.Plane == npc.Plane)
                return taunter;
            if (threat.Threat.Count == 0) return current;
            Entity? best = null;
            double bestThreat = 0;
            List<int>? gone = null;
            foreach (KeyValuePair<int, double> entry in threat.Threat)
            {
                Entity? candidate = World.FindEntity(entry.Key);
                if (candidate == null || candidate.IsDead || candidate.Plane != npc.Plane ||
                    candidate.Position.ChebyshevDistance(npc.Position) > npc.Def.LeashRadius * 2)
                {
                    (gone ??= new List<int>()).Add(entry.Key);
                    continue;
                }
                if (entry.Value > bestThreat)
                {
                    best = candidate;
                    bestThreat = entry.Value;
                }
            }
            if (gone != null)
                foreach (int id in gone) threat.Threat.Remove(id);
            if (best == null) return current;
            if (current == null || current.IsDead) return best;
            double currentThreat = threat.Threat.TryGetValue(current.Id, out double held) ? held : 0;
            return bestThreat > currentThreat * SwitchMargin ? best : current;
        }

        /// <summary>The player who did the most damage to a creature (still in the world), or null if nobody did.</summary>
        public Player? TopDamage(Npc npc)
        {
            foreach (KeyValuePair<int, int> entry in Of(npc).Damage.OrderByDescending(d => d.Value))
                if (World.FindEntity(entry.Key) is Player player) return player;
            return null;
        }

        /// <summary>Everyone who hurt a creature (still in the world), most damage first.</summary>
        public IEnumerable<Player> Contributors(Npc npc) =>
            Of(npc).Damage.OrderByDescending(d => d.Value).Select(d => World.FindEntity(d.Key)).OfType<Player>();

        /// <summary>A player who leaves the world is struck off every grudge.</summary>
        public override void OnLogout(Player player)
        {
            foreach (Npc npc in World.Npcs)
            {
                NpcThreat threat = Of(npc);
                threat.Threat.Remove(player.Id);
            }
        }
    }
}
