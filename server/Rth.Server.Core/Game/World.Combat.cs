using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Content;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    public sealed partial class World
    {
        // ------------------------------------------------------------------ player intents

        private void HandleAttack(Player player, int targetId)
        {
            if (player.IsDead) return;
            Entity? target = FindEntity(targetId);
            if (target == null || target == player || target.IsDead || !CanSee(player, target)) return;
            if (target is Player)
            {
                Tell(player, TextKind.Warning, "You can't attack other players.");
                return;
            }
            if (player.TargetId != targetId) player.QueuedAbility = null;
            player.TargetId = targetId;
            player.ChaseGoal = null;
            player.Waypoints.Clear();
        }

        private void HandleUseAbility(Player player, string abilityId)
        {
            if (player.IsDead) return;
            if (!Content.Abilities.TryGetValue(abilityId, out AbilityDef? ability) || !Content.ActionBar.Contains(abilityId)) return;
            if (player.TargetId == 0)
            {
                Tell(player, TextKind.Warning, "You need a target first.");
                return;
            }
            string? problem = ability.CheckRequirements(player.Phlegm, player.Bile);
            if (problem != null)
            {
                Tell(player, TextKind.Warning, problem);
                return;
            }
            player.QueuedAbility = abilityId;
        }

        private void HandleDeflect(Player player, CombatStyle style)
        {
            if (style != CombatStyle.None && player.Breath <= 0)
            {
                Tell(player, TextKind.Warning, "You have no breath left to deflect with.");
                return;
            }
            player.Deflect = style;
        }

        // ------------------------------------------------------------------ engaging

        /// <summary>Whether <paramref name="attacker"/> can hit <paramref name="target"/> from where it stands.</summary>
        internal bool InRange(Entity attacker, Entity target, int range)
        {
            if (attacker.Plane != target.Plane) return false;
            TileCoord a = attacker.Position;
            TileCoord b = target.Position;
            int dx = b.X - a.X;
            int dy = b.Y - a.Y;
            if (range <= 1)
            {
                // Melee: orthogonally adjacent, with no wall in between.
                return Math.Abs(dx) + Math.Abs(dy) == 1 && Map.CanStep(attacker.Plane, a.X, a.Y, dx, dy);
            }
            return a.ChebyshevDistance(b) <= range && LineOfSight.Has(Map, attacker.Plane, a, b);
        }

        /// <summary>Moves the attacker towards its target until in range. Returns whether it ended in range.</summary>
        private bool Engage(Entity attacker, Entity target, int range)
        {
            if (!InRange(attacker, target, range))
            {
                if (attacker.Waypoints.Count == 0 || attacker.ChaseGoal != target.Position)
                {
                    PlanChase(attacker, target, range);
                    attacker.ChaseGoal = target.Position;
                }
                ProcessMovement(attacker, () => InRange(attacker, target, range));
            }
            if (InRange(attacker, target, range))
            {
                attacker.Waypoints.Clear();
                return true;
            }
            return false;
        }

        private void PlanChase(Entity attacker, Entity target, int range)
        {
            if (range > 1)
            {
                SetPath(attacker, target.Position);
                return;
            }

            // Melee: head for the closest tile beside the target that can actually reach it.
            List<TileCoord>? best = null;
            foreach ((int dx, int dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                TileCoord beside = target.Position.Offset(dx, dy);
                if (Map.IsBlocked(attacker.Plane, beside.X, beside.Y) || !Map.CanStep(attacker.Plane, beside.X, beside.Y, -dx, -dy)) continue;
                if (beside == attacker.Position) return;
                List<TileCoord> path = Pathfinder.FindPath(Map, attacker.Plane, attacker.Position, beside, _config.PathSearchRadius);
                if (path.Count == 0 || path[path.Count - 1] != beside) continue;
                if (best == null || path.Count < best.Count) best = path;
            }
            attacker.Waypoints.Clear();
            if (best != null)
                foreach (TileCoord tile in best) attacker.Waypoints.Enqueue(tile);
        }

        // ------------------------------------------------------------------ player combat

        private void ProcessPlayerCombat(Player player)
        {
            Entity? target = player.TargetId != 0 ? FindEntity(player.TargetId) : null;
            if (target != null && (target.IsDead || !CanSee(player, target)))
            {
                player.ClearCombat();
                player.QueuedAbility = null;
                target = null;
            }

            if (target == null)
            {
                ProcessMovement(player);
                return;
            }

            AbilityDef? queued = player.QueuedAbility != null && Content.Abilities.TryGetValue(player.QueuedAbility, out AbilityDef? q) ? q : null;
            int range = queued?.Range ?? Content.AutoAttack.Range;
            if (!Engage(player, target, range)) return;
            if (CurrentTick < player.GlobalCooldownReadyTick) return;

            if (queued != null)
            {
                if (player.Cooldowns.TryGetValue(queued.Id, out uint ready) && CurrentTick < ready) return; // waits in the queue
                player.QueuedAbility = null;
                string? problem = queued.CheckRequirements(player.Phlegm, player.Bile);
                if (problem != null)
                {
                    Tell(player, TextKind.Warning, problem);
                    return;
                }
                UseAbility(player, target, queued);
                player.GlobalCooldownReadyTick = CurrentTick + (uint)Rules.GlobalCooldownTicks;
                player.Cooldowns[queued.Id] = CurrentTick + (uint)Math.Max(queued.CooldownTicks, Rules.GlobalCooldownTicks);
                player.NextAttackTick = Math.Max(player.NextAttackTick, player.GlobalCooldownReadyTick);
                return;
            }

            if (CurrentTick >= player.NextAttackTick)
            {
                UseAbility(player, target, Content.AutoAttack);
                player.NextAttackTick = CurrentTick + (uint)Math.Max(1, Content.AutoAttack.CooldownTicks);
            }
        }

        /// <summary>Fires an ability (or the auto-attack) from a player at a target: damage, resources, effects.</summary>
        private void UseAbility(Player player, Entity target, AbilityDef ability)
        {
            sbyte pole = (sbyte)Math.Sign(ability.BileShift);
            _actions.Add(new ActionEvent { SourceId = player.Id, TargetId = target.Id, AbilityId = ability.Id, Pole = pole });

            int dealt = 0;
            if (ability.DamageMax > 0)
            {
                double evasion = target is Npc npc ? npc.Def.Evasion : 0;
                double hitChance = Math.Max(0.05, Math.Min(1.0, Rules.PlayerAccuracy - evasion));
                if (_random.NextDouble() < hitChance)
                {
                    int percent = _random.Next(ability.DamageMin, ability.DamageMax + 1);
                    double damage = Rules.PlayerAbilityDamage * percent / 100.0;
                    damage *= 1 + (Rules.CholericBonusPercent(player.Bile) + player.EffectTotal(EffectType.Fervour)) / 100.0;
                    dealt = Damage(target, (int)Math.Round(damage), player, HitKind.Normal);
                }
                else
                {
                    Damage(target, 0, player, HitKind.Miss);
                }
            }

            // Resources move whether or not it landed: the humours were spent either way.
            switch (ability.Type)
            {
                case AbilityType.Basic:
                    player.Phlegm = Math.Min(Rules.MaxPhlegm, player.Phlegm + Rules.PhlegmGainFor(ability.PhlegmGain, player.Bile));
                    break;
                case AbilityType.Threshold:
                case AbilityType.Ultimate:
                    player.Phlegm = Math.Max(0, player.Phlegm - ability.PhlegmCost);
                    break;
            }

            int bile = player.Bile + ability.BileShift;
            if (ability.BileTowardZero > 0) bile = bile > 0 ? Math.Max(0, bile - ability.BileTowardZero) : Math.Min(0, bile + ability.BileTowardZero);
            player.Bile = Rules.ClampBile(bile);

            int heal = dealt * ability.HealPercentOfDamage / 100 + player.MaxBlood * ability.HealPercentOfMaxBlood / 100;
            if (heal > 0) Heal(player, heal);

            if (ability.Effect != EffectType.None && ability.EffectTicks > 0)
            {
                player.Effects.RemoveAll(e => e.Type == ability.Effect);
                player.Effects.Add(new ActiveEffect { Type = ability.Effect, Magnitude = ability.EffectMagnitude, EndTick = CurrentTick + (uint)ability.EffectTicks });
            }
        }

        // ------------------------------------------------------------------ damage, healing, death

        /// <summary>Applies damage after the target's wards; returns the amount actually taken.</summary>
        internal int Damage(Entity target, int amount, Entity? source, HitKind kind, CombatStyle style = CombatStyle.None)
        {
            if (target.IsDead) return 0;

            if (target is Player defender && kind != HitKind.Fever && amount > 0)
            {
                double ward = Rules.MelancholicWardPercent(defender.Bile) + defender.EffectTotal(EffectType.Ward);
                double scaled = amount * Math.Max(0, 1 - ward / 100.0);
                if (style != CombatStyle.None && defender.Deflect == style && defender.Breath > 0)
                {
                    scaled *= 1 - Rules.DeflectReductionPercent / 100.0;
                    kind = HitKind.Deflected;
                }
                amount = (int)Math.Round(scaled);
            }

            amount = Math.Min(amount, target.Blood);
            target.Blood -= amount;
            target.HealthChanged |= amount > 0;
            _hits.Add(new HitEvent { TargetId = target.Id, SourceId = source?.Id ?? 0, Amount = amount, Kind = kind });

            if (source != null && kind != HitKind.Fever)
            {
                target.LastCombatTick = CurrentTick;
                source.LastCombatTick = CurrentTick;
                Retaliate(target, source);
            }

            if (target.Blood <= 0) Kill(target, source);
            return amount;
        }

        /// <summary>Being hit makes NPCs fight back, and idle players auto-retaliate.</summary>
        private static void Retaliate(Entity target, Entity source)
        {
            if (target.TargetId != 0) return;
            if (target is Npc npc && (!npc.Def.Attacks || npc.Returning)) return;
            if (target is Player player && player.Waypoints.Count > 0) return;
            target.TargetId = source.Id;
            target.ChaseGoal = null;
        }

        private void Heal(Entity target, int amount)
        {
            int healed = Math.Min(amount, target.MaxBlood - target.Blood);
            if (healed <= 0) return;
            target.Blood += healed;
            target.HealthChanged = true;
            _hits.Add(new HitEvent { TargetId = target.Id, SourceId = target.Id, Amount = healed, Kind = HitKind.Heal });
        }

        private void Kill(Entity victim, Entity? killer)
        {
            victim.IsDead = true;
            victim.DiedAtTick = CurrentTick;
            victim.ClearCombat();
            victim.Waypoints.Clear();
            _deaths.Add(victim.Id);
            foreach (Entity other in AllEntities())
            {
                if (other.TargetId != victim.Id) continue;
                other.ClearCombat();
                if (other is Player p) p.QueuedAbility = null;
            }

            if (victim is Player player)
            {
                player.Deaths++;
                player.QueuedAbility = null;
                player.Deflect = CombatStyle.None;
                player.RespawnAtTick = CurrentTick + (uint)Rules.RespawnDelayTicks;
                Tell(player, TextKind.Info, "Your humours come apart.");
                Tell(player, TextKind.Assayer, AssayerRemark(player));
            }
            else if (killer is Player winner)
            {
                Tell(winner, TextKind.Info, "You have defeated the " + victim.Name.ToLowerInvariant() + ".");
            }
        }

        /// <summary>What the Assayer says over the scales, based on how this player died.</summary>
        private string AssayerRemark(Player player)
        {
            AssayerLines lines = Content.Assayer;
            List<string> pool =
                player.Deaths == 1 ? lines.First :
                player.Deaths >= 10 && lines.Frequent.Count > 0 && _random.Next(3) == 0 ? lines.Frequent :
                Rules.IsCholeric(player.Bile) ? lines.Choleric :
                Rules.IsMelancholic(player.Bile) ? lines.Melancholic :
                lines.Balanced;
            if (pool.Count == 0) pool = lines.Balanced;
            string line = pool.Count > 0 ? pool[_random.Next(pool.Count)] : "The Assayer weighs you, and says nothing.";
            return line.Replace("{name}", player.Name).Replace("{deaths}", player.Deaths.ToString());
        }

        private void RespawnPlayer(Player player)
        {
            player.IsDead = false;
            player.Plane = Map.SpawnPlane;
            player.Position = Map.Spawn;
            player.Waypoints.Clear();
            player.Teleported = true;
            player.Blood = player.MaxBlood;
            player.HealthChanged = true;
            player.Phlegm = 0;
            player.Bile = 0; // the Assayer puts back a balanced measure
            player.Effects.Clear();
            player.Cooldowns.Clear();
            player.LastCombatTick = 0;
        }

        // ------------------------------------------------------------------ per-tick resources

        private void TickResources(Player player)
        {
            CombatRules rules = Rules;
            bool inCombat = player.InCombat(CurrentTick, rules.CombatTimeoutTicks);

            if (rules.IsFevered(player.Bile) && player.Blood > 1)
            {
                int burn = Math.Max(1, (int)Math.Ceiling(player.MaxBlood * rules.FeverBloodPercentPerTick / 100.0));
                burn = Math.Min(burn, player.Blood - 1); // fever weakens but never kills
                player.Blood -= burn;
                player.HealthChanged = true;
                _hits.Add(new HitEvent { TargetId = player.Id, SourceId = player.Id, Amount = burn, Kind = HitKind.Fever });
            }

            if (player.Deflect != CombatStyle.None)
            {
                player.Breath = Math.Max(0, player.Breath - rules.BreathDrainPerTick);
                if (player.Breath == 0)
                {
                    player.Deflect = CombatStyle.None;
                    Tell(player, TextKind.Warning, "You are out of breath. Your deflection falls.");
                }
            }
            else if (!inCombat)
            {
                player.Breath = Math.Min(player.MaxBreath, player.Breath + rules.BreathRegenPerTick);
            }

            if (inCombat) return;

            // Out of combat the body settles: bile drifts back, phlegm fades, blood returns.
            if (player.Bile > 0) player.Bile = Math.Max(0, player.Bile - rules.BileDecayPerTick);
            else if (player.Bile < 0) player.Bile = Math.Min(0, player.Bile + rules.BileDecayPerTick);
            player.Phlegm = Math.Max(0, player.Phlegm - rules.PhlegmDecayPerTick);
            if (player.Blood < player.MaxBlood && !rules.IsFevered(player.Bile))
            {
                int regen = Math.Max(1, (int)Math.Round(player.MaxBlood * rules.BloodRegenPercentPerTick / 100.0));
                player.Blood = Math.Min(player.MaxBlood, player.Blood + regen);
                player.HealthChanged = true;
            }
        }

        private void ExpireEffects(Entity entity) => entity.Effects.RemoveAll(e => CurrentTick >= e.EndTick);
    }
}
