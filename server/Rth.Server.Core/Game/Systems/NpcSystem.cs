using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Content;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>NPC spawning and behaviour: wandering, aggression, chasing, attacking, leashing, respawning.</summary>
    public sealed class NpcSystem : GameSystem
    {
        /// <summary>Dead NPCs linger this long before vanishing, so clients can show the death.</summary>
        private const int CorpseTicks = 2;

        private readonly List<NpcSpawn> _spawns;

        public NpcSystem(IEnumerable<NpcSpawn> spawns)
        {
            _spawns = spawns.ToList();
        }

        private CombatSystem Combat => World.Get<CombatSystem>();

        protected override void OnAttach()
        {
            foreach (NpcSpawn spawn in _spawns) Spawn(spawn);
        }

        public Npc? Spawn(NpcSpawn spawn)
        {
            if (!World.Content.Npcs.TryGetValue(spawn.Npc, out NpcDef? def))
            {
                World.Log("Spawn at (" + spawn.X + ", " + spawn.Y + ") names unknown NPC '" + spawn.Npc + "'; skipped.");
                return null;
            }
            var npc = new Npc(World.NextEntityId(), def, spawn) { NextWanderTick = (uint)World.Random.Next(1, 10) };
            World.AddNpc(npc);
            return npc;
        }

        public override void OnContentChanged()
        {
            foreach (Npc npc in World.Npcs)
            {
                if (!World.Content.Npcs.TryGetValue(npc.Def.Id, out NpcDef? def)) continue; // removed type: keep the old one
                npc.Def = def;
                npc.MaxBlood = def.Blood;
                npc.Blood = Math.Min(npc.Blood, def.Blood);
            }
        }

        public override void OnNpcs()
        {
            uint now = World.CurrentTick;
            foreach (Npc npc in World.Npcs)
            {
                if (npc.Despawned)
                {
                    if (now >= npc.RespawnAtTick) Respawn(npc);
                    continue;
                }
                if (npc.IsDead)
                {
                    if (now >= npc.DiedAtTick + CorpseTicks)
                    {
                        npc.Despawned = true;
                        npc.RespawnAtTick = now + (uint)Math.Max(1, npc.Def.RespawnTicks);
                    }
                    continue;
                }
                Think(npc);
            }
        }

        private void Think(Npc npc)
        {
            NpcDef def = npc.Def;

            if (npc.Returning)
            {
                World.ProcessMovement(npc);
                if (npc.Waypoints.Count == 0)
                {
                    npc.Returning = false;
                    ResetHealth(npc);
                }
                return;
            }

            Entity? target = npc.TargetId != 0 ? World.FindEntity(npc.TargetId) : null;
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
                World.SetPath(npc, npc.Home);
                return;
            }

            if (target == null && def.Aggressive && def.Attacks) target = FindAggroTarget(npc);

            if (target != null && def.Attacks)
            {
                npc.TargetId = target.Id;
                if (Combat.Engage(npc, target, def.AttackRange) && World.CurrentTick >= npc.NextAttackTick)
                {
                    Attack(npc, target);
                    npc.NextAttackTick = World.CurrentTick + (uint)def.AttackTicks;
                }
                return;
            }

            if (def.Regenerates && !npc.InCombat(World.CurrentTick, World.Rules.CombatTimeoutTicks) && npc.Blood < npc.MaxBlood)
                ResetHealth(npc);

            Wander(npc);
        }

        private Entity? FindAggroTarget(Npc npc)
        {
            Player? best = null;
            int bestDistance = int.MaxValue;
            foreach (Player player in World.Players)
            {
                if (player.IsDead || player.Plane != npc.Plane) continue;
                int distance = player.Position.ChebyshevDistance(npc.Position);
                if (distance > npc.Def.AggroRadius || distance >= bestDistance) continue;
                if (!LineOfSight.Has(World.Map, npc.Plane, npc.Position, player.Position)) continue;
                best = player;
                bestDistance = distance;
            }
            return best;
        }

        private void Attack(Npc npc, Entity target)
        {
            World.AddAction(new ActionEvent { SourceId = npc.Id, TargetId = target.Id, AbilityId = "npc:" + npc.Def.Id, Pole = 0 });
            if (World.Random.NextDouble() < npc.Def.Accuracy)
                Combat.Damage(target, World.Random.Next(0, npc.Def.MaxHit + 1), npc, HitKind.Normal, npc.Def.Style);
            else
                Combat.Damage(target, 0, npc, HitKind.Miss, npc.Def.Style);
        }

        private void Wander(Npc npc)
        {
            if (npc.Waypoints.Count > 0)
            {
                World.ProcessMovement(npc);
                return;
            }
            if (npc.WanderRadius <= 0 || World.CurrentTick < npc.NextWanderTick) return;
            npc.NextWanderTick = World.CurrentTick + (uint)World.Random.Next(6, 16);

            int r = npc.WanderRadius;
            var spot = new TileCoord(npc.Home.X + World.Random.Next(-r, r + 1), npc.Home.Y + World.Random.Next(-r, r + 1));
            if (World.Map.IsBlocked(npc.Plane, spot.X, spot.Y)) return;
            World.SetPath(npc, spot);
            World.ProcessMovement(npc);
        }

        private static void ResetHealth(Npc npc)
        {
            if (npc.Blood == npc.MaxBlood) return;
            npc.Blood = npc.MaxBlood;
            npc.HealthChanged = true;
        }

        private static void Respawn(Npc npc)
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
