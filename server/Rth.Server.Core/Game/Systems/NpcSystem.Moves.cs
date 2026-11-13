using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Content;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Creatures' special attacks (<see cref="NpcMoveDef"/>): picked when the creature could
    /// attack, wound up in place while the telegraph shows, then landed on whoever still stands
    /// in it. Everything about a move is data; deflection hymns, wards and evasion apply as to any
    /// blow, so the counters players already have work against them.
    /// </summary>
    public sealed partial class NpcSystem
    {
        /// <summary>A creature's moves in progress and on cooldown.</summary>
        public sealed class NpcMoves
        {
            public NpcMoveDef? Pending;
            public uint LandTick;
            public uint StartTick;
            public int Plane;
            public TileCoord Centre;
            public int TargetId;
            public readonly Dictionary<string, uint> ReadyAt = new Dictionary<string, uint>();
            public readonly HashSet<string> UsedThisFight = new HashSet<string>();

            public void Reset()
            {
                Pending = null;
                ReadyAt.Clear();
                UsedThisFight.Clear();
            }
        }

        /// <summary>Creatures winding up a move now, for the "telegraphs" state.</summary>
        private readonly List<Npc> _winding = new List<Npc>();

        public NpcMoves MovesOf(Npc npc) => npc.Component<NpcMoves>();

        /// <summary>Forgets a creature's moves (it reset: leashed home, died or respawned).</summary>
        private void ResetMoves(Npc npc)
        {
            MovesOf(npc).Reset();
            _winding.Remove(npc);
        }

        /// <summary>
        /// While a move winds up the creature holds still; on its tick it lands. Returns true if
        /// the creature is busy with a move this tick (it does nothing else).
        /// </summary>
        private bool TickPendingMove(Npc npc)
        {
            NpcMoves moves = MovesOf(npc);
            if (moves.Pending == null) return false;
            npc.Waypoints.Clear();
            if (World.CurrentTick < moves.LandTick) return true;
            NpcMoveDef move = moves.Pending;
            moves.Pending = null;
            _winding.Remove(npc);
            Land(npc, move, moves);
            return true;
        }

        /// <summary>
        /// The first of the creature's moves that is ready, fits its condition, reaches the target
        /// and passes its chance; null to make the ordinary blow.
        /// </summary>
        private NpcMoveDef? ChooseMove(Npc npc, Entity target)
        {
            if (npc.Def.Moves.Count == 0) return null;
            NpcMoves moves = MovesOf(npc);
            uint now = World.CurrentTick;
            foreach (NpcMoveDef move in npc.Def.Moves)
            {
                if (moves.ReadyAt.TryGetValue(move.Id, out uint ready) && now < ready) continue;
                if (move.OncePerFight && moves.UsedThisFight.Contains(move.Id)) continue;
                if (move.BelowBloodPercent > 0 && npc.Blood * 100 >= move.BelowBloodPercent * npc.MaxBlood) continue;
                if (!Reaches(npc, move, target)) continue;
                if (move.Chance < 1 && World.Random.NextDouble() >= move.Chance) continue;
                return move;
            }
            return null;
        }

        /// <summary>Whether a move can be started on (or still lands on) a target: a sweep reaches all round, diagonals too; anything else needs the creature's usual reach and line of sight.</summary>
        private bool Reaches(Npc npc, NpcMoveDef move, Entity target) =>
            move.Area == NpcMoveDef.Areas.Self
                ? target.Plane == npc.Plane && npc.Position.ChebyshevDistance(target.Position) <= ReachOf(npc, move)
                : Combat.InRange(npc, target, ReachOf(npc, move));

        private static int ReachOf(Npc npc, NpcMoveDef move) =>
            move.Range > 0 ? move.Range : move.Area == NpcMoveDef.Areas.Self ? Math.Max(1, move.Radius) : npc.Def.AttackRange;

        /// <summary>Starts a move: it lands now, or after its wind-up with the telegraph showing.</summary>
        private void BeginMove(Npc npc, NpcMoveDef move, Entity target)
        {
            NpcMoves moves = MovesOf(npc);
            uint now = World.CurrentTick;
            moves.ReadyAt[move.Id] = now + (uint)Math.Max(1, move.CooldownTicks);
            moves.UsedThisFight.Add(move.Id);
            moves.StartTick = now;
            moves.LandTick = now + (uint)move.WindupTicks;
            moves.Plane = npc.Plane;
            moves.TargetId = target.Id;
            moves.Centre = move.Area == NpcMoveDef.Areas.Self ? npc.Position : target.Position;
            npc.Waypoints.Clear();
            npc.NextAttackTick = moves.LandTick + (uint)npc.Def.AttackTicks;

            if (!string.IsNullOrEmpty(move.Animation)) World.Animate(npc, move.Animation!, target.Position);
            else World.Face(npc, target.Position);
            if (!string.IsNullOrEmpty(move.Message))
                foreach (Entity near in World.EntitiesNear(npc.Plane, npc.Position, World.Config.ViewRadius))
                    if (near is Player p) World.Tell(p, TextKind.Warning, move.Message!);

            if (move.WindupTicks <= 0)
            {
                Land(npc, move, moves);
                return;
            }
            moves.Pending = move;
            if (!_winding.Contains(npc)) _winding.Add(npc);
        }

        /// <summary>The move lands: on its target if still in reach, or on everyone in its square.</summary>
        private void Land(Npc npc, NpcMoveDef move, NpcMoves moves)
        {
            if (npc.IsDead) return;
            CombatStyle style = move.Style ?? npc.Def.Style;
            Entity? target = World.FindEntity(moves.TargetId);
            World.AddAction(new ActionEvent { SourceId = npc.Id, TargetId = target?.Id ?? npc.Id, AbilityId = "npc:" + npc.Def.Id + ":" + move.Id, Pole = 0 });

            if (move.Damages)
            {
                var struck = new List<Entity>();
                if (move.Area == NpcMoveDef.Areas.Target)
                {
                    // A blow at one target lands only if it is still in reach: stepping away dodges it.
                    if (target != null && !target.IsDead && Reaches(npc, move, target)) struck.Add(target);
                    else if (target != null) Combat.Damage(target, 0, npc, HitKind.Miss, style);
                }
                else
                {
                    foreach (Entity near in World.EntitiesNear(moves.Plane, moves.Centre, move.Radius))
                        if (near is Player p && !p.IsDead) struck.Add(p);
                }
                foreach (Entity victim in struck) Strike(npc, move, victim, style);
            }

            if (move.HealPercent > 0 && !npc.IsDead) Combat.Heal(npc, npc.MaxBlood * move.HealPercent / 100);
        }

        private void Strike(Npc npc, NpcMoveDef move, Entity victim, CombatStyle style)
        {
            if (move.RollsAccuracy && World.Random.NextDouble() >= HitChance(npc, victim))
            {
                Combat.Damage(victim, 0, npc, HitKind.Miss, style);
                return;
            }
            int amount = move.HitPercent > 0
                ? Math.Max(1, (int)Math.Round(victim.MaxBlood * move.HitPercent / 100.0))
                : World.Random.Next(move.MinHit, move.MaxHit + 1);
            int dealt = Combat.Damage(victim, amount, npc, HitKind.Normal, style);
            if (victim.IsDead) return;
            if (move.BleedPercent > 0 && dealt > 0) Combat.AddBleed(victim, dealt * move.BleedPercent / 100, move.BleedTicks, npc, style);
            if (move.PinTicks > 0 && dealt > 0)
            {
                victim.RootedUntilTick = Math.Max(victim.RootedUntilTick, World.CurrentTick + (uint)move.PinTicks);
                victim.Waypoints.Clear();
            }
        }

        /// <summary>Every move winding up within sight of the player; sent while there are any, and once more when they're gone.</summary>
        public override void SendState(Player viewer)
        {
            var state = new TelegraphsState();
            foreach (Npc npc in _winding)
            {
                NpcMoves moves = MovesOf(npc);
                if (moves.Pending == null || npc.IsDead) continue;
                bool onTarget = moves.Pending.Area == NpcMoveDef.Areas.Target;
                if (!World.CanSee(viewer, moves.Plane, onTarget ? npc.Position : moves.Centre)) continue;
                state.Telegraphs.Add(new Telegraph
                {
                    NpcId = npc.Id,
                    Move = moves.Pending.Id,
                    Name = moves.Pending.Name,
                    Plane = moves.Plane,
                    X = moves.Centre.X,
                    Y = moves.Centre.Y,
                    Radius = moves.Pending.Radius,
                    TargetId = onTarget ? moves.TargetId : 0,
                    StartTick = moves.StartTick,
                    LandTick = moves.LandTick,
                    Style = moves.Pending.Style ?? npc.Def.Style,
                });
            }
            SawTelegraphs seen = viewer.Component<SawTelegraphs>();
            if (state.Telegraphs.Count == 0 && !seen.Any) return;
            seen.Any = state.Telegraphs.Count > 0;
            viewer.Connection.Send(JsonStateMessage.Of(TelegraphsState.Key, state));
        }

        private sealed class SawTelegraphs
        {
            public bool Any;
        }
    }
}
