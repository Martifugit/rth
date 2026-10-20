using System;
using Rth.Server.Content;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    public sealed partial class World
    {
        /// <summary>Despawned NPCs linger this long after dying, so clients can show the death.</summary>
        private const int CorpseTicks = 2;

        private void AddNpc(NpcSpawn spawn)
        {
            if (!Content.Npcs.TryGetValue(spawn.Npc, out NpcDef? def))
            {
                _log("Spawn at (" + spawn.X + ", " + spawn.Y + ") names unknown NPC '" + spawn.Npc + "'; skipped.");
                return;
            }
            var npc = new Npc(_nextEntityId++, def, spawn) { NextWanderTick = (uint)_random.Next(1, 10) };
            _npcs.Add(npc.Id, npc);
        }

        private void ProcessNpcs()
        {
            foreach (Npc npc in _npcs.Values)
            {
                if (npc.Despawned)
                {
                    if (CurrentTick >= npc.RespawnAtTick) RespawnNpc(npc);
                    continue;
                }
                if (npc.IsDead)
                {
                    if (CurrentTick >= npc.DiedAtTick + CorpseTicks)
                    {
                        npc.Despawned = true;
                        npc.RespawnAtTick = CurrentTick + (uint)Math.Max(1, npc.Def.RespawnTicks);
                    }
                    continue;
                }
                ProcessNpc(npc);
                ExpireEffects(npc);
            }
        }

        private void ProcessNpc(Npc npc)
        {
            NpcDef def = npc.Def;

            if (npc.Returning)
            {
                ProcessMovement(npc);
                if (npc.Waypoints.Count == 0)
                {
                    npc.Returning = false;
                    ResetHealth(npc);
                }
                return;
            }

            Entity? target = npc.TargetId != 0 ? FindEntity(npc.TargetId) : null;
            if (target != null && (target.IsDead || target.Plane != npc.Plane))
            {
                npc.ClearCombat();
                target = null;
            }

            // Dragged too far from home: give up, walk back and reset.
            if (npc.Position.ChebyshevDistance(npc.Home) > def.LeashRadius)
            {
                npc.ClearCombat();
                npc.Returning = true;
                SetPath(npc, npc.Home);
                return;
            }

            if (target == null && def.Aggressive && def.Attacks) target = FindAggroTarget(npc);

            if (target != null && def.Attacks)
            {
                npc.TargetId = target.Id;
                if (Engage(npc, target, def.AttackRange) && CurrentTick >= npc.NextAttackTick)
                {
                    NpcAttack(npc, target);
                    npc.NextAttackTick = CurrentTick + (uint)def.AttackTicks;
                }
                return;
            }

            if (def.Regenerates && !npc.InCombat(CurrentTick, Rules.CombatTimeoutTicks) && npc.Blood < npc.MaxBlood)
                ResetHealth(npc);

            Wander(npc);
        }

        private Entity? FindAggroTarget(Npc npc)
        {
            Player? best = null;
            int bestDistance = int.MaxValue;
            foreach (Player player in _players.Values)
            {
                if (player.IsDead || player.Plane != npc.Plane) continue;
                int distance = player.Position.ChebyshevDistance(npc.Position);
                if (distance > npc.Def.AggroRadius || distance >= bestDistance) continue;
                if (!LineOfSight.Has(Map, npc.Plane, npc.Position, player.Position)) continue;
                best = player;
                bestDistance = distance;
            }
            return best;
        }

        private void NpcAttack(Npc npc, Entity target)
        {
            _actions.Add(new ActionEvent { SourceId = npc.Id, TargetId = target.Id, AbilityId = "npc:" + npc.Def.Id, Pole = 0 });
            if (_random.NextDouble() < npc.Def.Accuracy)
                Damage(target, _random.Next(0, npc.Def.MaxHit + 1), npc, HitKind.Normal, npc.Def.Style);
            else
                Damage(target, 0, npc, HitKind.Miss, npc.Def.Style);
        }

        private void Wander(Npc npc)
        {
            if (npc.Waypoints.Count > 0)
            {
                ProcessMovement(npc);
                return;
            }
            if (npc.WanderRadius <= 0 || CurrentTick < npc.NextWanderTick) return;
            npc.NextWanderTick = CurrentTick + (uint)_random.Next(6, 16);

            int r = npc.WanderRadius;
            var spot = new TileCoord(npc.Home.X + _random.Next(-r, r + 1), npc.Home.Y + _random.Next(-r, r + 1));
            if (Map.IsBlocked(npc.Plane, spot.X, spot.Y)) return;
            SetPath(npc, spot);
            ProcessMovement(npc);
        }

        private void ResetHealth(Npc npc)
        {
            if (npc.Blood == npc.MaxBlood) return;
            npc.Blood = npc.MaxBlood;
            npc.HealthChanged = true;
        }

        private void RespawnNpc(Npc npc)
        {
            npc.Despawned = false;
            npc.IsDead = false;
            npc.Returning = false;
            npc.Position = npc.Home;
            npc.Plane = npc.Spawn.Plane;
            npc.MaxBlood = npc.Def.Blood;
            npc.Blood = npc.MaxBlood;
            npc.Waypoints.Clear();
            npc.Effects.Clear();
            npc.LastCombatTick = 0;
            npc.Teleported = true;
        }
    }
}
