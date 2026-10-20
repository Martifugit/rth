using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Persistence;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Fighting: attack and ability intents, ranges and chasing, damage and healing, death, and the
    /// per-tick humours (phlegm, bile, breath, regeneration). Deaths are published as
    /// <see cref="EntityDied"/>; what happens next (respawns, loot, the Assayer) belongs to whoever
    /// listens.
    /// </summary>
    public sealed class CombatSystem : GameSystem, IStatContributor
    {
        private CombatRules Rules => World.Rules;

        protected override void OnAttach()
        {
            World.OnMessage<AttackMessage>((p, m) => HandleAttack(p, m.TargetId));
            World.OnMessage<UseAbilityMessage>((p, m) => HandleUseAbility(p, m.AbilityId));
            World.OnMessage<SetDeflectMessage>((p, m) => HandleDeflect(p, m.Style));
        }

        // ------------------------------------------------------------------ lifecycle and saving

        public override void OnLogin(Player player, PlayerSave? save)
        {
            World.RecalculateStats(player);
            player.Blood = Clamp(save?.Blood ?? player.MaxBlood, 1, player.MaxBlood);
            player.Breath = Clamp(save?.Breath ?? player.MaxBreath, 0, player.MaxBreath);
            player.Bile = Rules.ClampBile(save?.Bile ?? 0);
            player.Deaths = save?.Deaths ?? 0;
        }

        public override void OnSave(Player player, PlayerSave save)
        {
            save.Blood = player.IsDead ? player.MaxBlood : player.Blood;
            save.Breath = player.Breath;
            save.Bile = player.IsDead ? 0 : player.Bile;
            save.Deaths = player.Deaths;
        }

        /// <summary>Base stats from the rules, plus timed effects from abilities and consumables.</summary>
        public void Contribute(Player player, StatBlock stats)
        {
            stats.Add(StatIds.AbilityDamage, Rules.PlayerAbilityDamage);
            stats.Add(StatIds.MaxBlood, Rules.PlayerMaxBlood);
            stats.Add(StatIds.MaxBreath, Rules.PlayerMaxBreath);
            foreach (ActiveEffect e in player.Effects)
            {
                if (e.Type == EffectType.Ward) stats.Add(StatIds.Ward, e.Magnitude);
                else if (e.Type == EffectType.Fervour) stats.Add(StatIds.DamageBonus, e.Magnitude);
            }
        }

        public override void OnContentChanged()
        {
            foreach (Player player in World.Players) World.RecalculateStats(player);
        }

        public override void OnLogout(Player player)
        {
            foreach (Entity other in World.AllEntities())
                if (other.TargetId == player.Id) other.ClearCombat();
        }

        // ------------------------------------------------------------------ intents

        private void HandleAttack(Player player, int targetId)
        {
            if (player.IsDead) return;
            Entity? target = World.FindEntity(targetId);
            if (target == null || target == player || target.IsDead || !World.CanSee(player, target)) return;
            if (target is Player)
            {
                World.Tell(player, TextKind.Warning, "You can't attack other players.");
                return;
            }
            if (player.Interaction is AttackInteraction current && current.TargetId == targetId) return;
            StartAttack(player, target);
        }

        public void StartAttack(Player player, Entity target)
        {
            player.QueuedAbility = null;
            player.Waypoints.Clear();
            World.StartInteraction(player, new AttackInteraction(target.Id));
            player.TargetId = target.Id;
            player.ChaseGoal = null;
        }

        private void HandleUseAbility(Player player, string abilityId)
        {
            if (player.IsDead) return;
            if (!World.Content.Abilities.TryGetValue(abilityId, out AbilityDef? ability) || !World.Content.ActionBar.Contains(abilityId)) return;
            if (player.TargetId == 0)
            {
                World.Tell(player, TextKind.Warning, "You need a target first.");
                return;
            }
            string? problem = ability.CheckRequirements(player.Phlegm, player.Bile);
            if (problem != null)
            {
                World.Tell(player, TextKind.Warning, problem);
                return;
            }
            player.QueuedAbility = abilityId;
        }

        private void HandleDeflect(Player player, CombatStyle style)
        {
            if (style != CombatStyle.None && player.Breath <= 0)
            {
                World.Tell(player, TextKind.Warning, "You have no breath left to deflect with.");
                return;
            }
            player.Deflect = style;
        }

        // ------------------------------------------------------------------ engaging

        /// <summary>Whether <paramref name="attacker"/> can hit <paramref name="target"/> from where it stands.</summary>
        public bool InRange(Entity attacker, Entity target, int range)
        {
            if (attacker.Plane != target.Plane) return false;
            TileCoord a = attacker.Position;
            TileCoord b = target.Position;
            int dx = b.X - a.X;
            int dy = b.Y - a.Y;
            if (range <= 1)
            {
                // Melee: orthogonally adjacent, with no wall in between.
                return Math.Abs(dx) + Math.Abs(dy) == 1 && World.Map.CanStep(attacker.Plane, a.X, a.Y, dx, dy);
            }
            return a.ChebyshevDistance(b) <= range && LineOfSight.Has(World.Map, attacker.Plane, a, b);
        }

        /// <summary>Moves the attacker towards its target until in range. Returns whether it ended in range.</summary>
        public bool Engage(Entity attacker, Entity target, int range)
        {
            if (!InRange(attacker, target, range))
            {
                if (attacker.Waypoints.Count == 0 || attacker.ChaseGoal != target.Position)
                {
                    PlanChase(attacker, target, range);
                    attacker.ChaseGoal = target.Position;
                }
                World.ProcessMovement(attacker, () => InRange(attacker, target, range));
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
                World.SetPath(attacker, target.Position);
                return;
            }

            // Melee: head for the closest tile beside the target that can actually reach it.
            List<TileCoord>? best = null;
            foreach ((int dx, int dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                TileCoord beside = target.Position.Offset(dx, dy);
                if (World.Map.IsBlocked(attacker.Plane, beside.X, beside.Y) || !World.Map.CanStep(attacker.Plane, beside.X, beside.Y, -dx, -dy)) continue;
                if (beside == attacker.Position) return;
                List<TileCoord> path = Pathfinder.FindPath(World.Map, attacker.Plane, attacker.Position, beside, World.Config.PathSearchRadius);
                if (path.Count == 0 || path[path.Count - 1] != beside) continue;
                if (best == null || path.Count < best.Count) best = path;
            }
            attacker.Waypoints.Clear();
            if (best != null)
                foreach (TileCoord tile in best) attacker.Waypoints.Enqueue(tile);
        }

        // ------------------------------------------------------------------ player attacks

        /// <summary>One tick of a player's attack interaction. Returns false when the fight is over.</summary>
        internal bool TickAttack(Player player, int targetId)
        {
            Entity? target = World.FindEntity(targetId);
            if (target == null || target.IsDead || !World.CanSee(player, target)) return false;

            AbilityDef? queued = player.QueuedAbility != null && World.Content.Abilities.TryGetValue(player.QueuedAbility, out AbilityDef? q) ? q : null;
            int range = queued?.Range ?? World.Content.AutoAttack.Range;
            if (!Engage(player, target, range)) return true;
            uint now = World.CurrentTick;
            if (now < player.GlobalCooldownReadyTick || now < player.ActionDelayUntilTick) return true;

            if (queued != null)
            {
                if (player.Cooldowns.TryGetValue(queued.Id, out uint ready) && now < ready) return true; // waits in the queue
                player.QueuedAbility = null;
                string? problem = queued.CheckRequirements(player.Phlegm, player.Bile);
                if (problem != null)
                {
                    World.Tell(player, TextKind.Warning, problem);
                    return true;
                }
                UseAbility(player, target, queued);
                player.GlobalCooldownReadyTick = now + (uint)Rules.GlobalCooldownTicks;
                player.Cooldowns[queued.Id] = now + (uint)Math.Max(queued.CooldownTicks, Rules.GlobalCooldownTicks);
                player.NextAttackTick = Math.Max(player.NextAttackTick, player.GlobalCooldownReadyTick);
                return true;
            }

            if (now >= player.NextAttackTick)
            {
                UseAbility(player, target, World.Content.AutoAttack);
                player.NextAttackTick = now + (uint)Math.Max(1, World.Content.AutoAttack.CooldownTicks);
            }
            return true;
        }

        /// <summary>Fires an ability (or the auto-attack) from a player at a target: damage, resources, effects.</summary>
        private void UseAbility(Player player, Entity target, AbilityDef ability)
        {
            sbyte pole = (sbyte)Math.Sign(ability.BileShift);
            World.AddAction(new ActionEvent { SourceId = player.Id, TargetId = target.Id, AbilityId = ability.Id, Pole = pole });

            int dealt = 0;
            if (ability.DamageMax > 0)
            {
                double evasion = target is Npc npc ? npc.Def.Evasion : 0;
                double accuracy = Rules.PlayerAccuracy + player.Stats.Get(StatIds.Accuracy) / 100.0;
                double hitChance = Math.Max(0.05, Math.Min(1.0, accuracy - evasion));
                if (World.Random.NextDouble() < hitChance)
                {
                    int percent = World.Random.Next(ability.DamageMin, ability.DamageMax + 1);
                    double damage = player.Stats.Get(StatIds.AbilityDamage) * percent / 100.0;
                    damage *= 1 + (Rules.CholericBonusPercent(player.Bile) + player.Stats.Get(StatIds.DamageBonus)) / 100.0;
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
                    AddPhlegm(player, Rules.PhlegmGainFor(ability.PhlegmGain, player.Bile));
                    break;
                case AbilityType.Threshold:
                case AbilityType.Ultimate:
                    AddPhlegm(player, -ability.PhlegmCost);
                    break;
            }

            ShiftBile(player, ability.BileShift);
            if (ability.BileTowardZero > 0) SettleBile(player, ability.BileTowardZero);

            int heal = dealt * ability.HealPercentOfDamage / 100 + player.MaxBlood * ability.HealPercentOfMaxBlood / 100;
            if (heal > 0) Heal(player, heal);

            if (ability.Effect != EffectType.None && ability.EffectTicks > 0)
                ApplyEffect(player, ability.Effect, ability.EffectMagnitude, ability.EffectTicks);
        }

        // ------------------------------------------------------------------ the humours (also used by items and other systems)

        public void AddPhlegm(Player player, int amount) =>
            player.Phlegm = Clamp(player.Phlegm + amount, 0, Rules.MaxPhlegm);

        public void ShiftBile(Player player, int amount) => player.Bile = Rules.ClampBile(player.Bile + amount);

        /// <summary>Moves bile towards zero by up to <paramref name="amount"/>, never past it.</summary>
        public void SettleBile(Player player, int amount)
        {
            if (player.Bile > 0) player.Bile = Math.Max(0, player.Bile - amount);
            else if (player.Bile < 0) player.Bile = Math.Min(0, player.Bile + amount);
        }

        public void RestoreBreath(Player player, int amount) =>
            player.Breath = Clamp(player.Breath + amount, 0, player.MaxBreath);

        public void ApplyEffect(Entity entity, EffectType type, int magnitude, int ticks)
        {
            entity.Effects.RemoveAll(e => e.Type == type);
            entity.Effects.Add(new ActiveEffect { Type = type, Magnitude = magnitude, EndTick = World.CurrentTick + (uint)ticks });
            if (entity is Player p) World.RecalculateStats(p);
        }

        // ------------------------------------------------------------------ damage, healing, death

        /// <summary>Applies damage after the target's wards; returns the amount actually taken.</summary>
        public int Damage(Entity target, int amount, Entity? source, HitKind kind, CombatStyle style = CombatStyle.None)
        {
            if (target.IsDead) return 0;

            if (target is Player defender && kind != HitKind.Fever && amount > 0)
            {
                double ward = Rules.MelancholicWardPercent(defender.Bile) + defender.Stats.Get(StatIds.Ward);
                double scaled = amount * Math.Max(0, 1 - Math.Min(90, ward) / 100.0);
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
            World.AddHit(new HitEvent { TargetId = target.Id, SourceId = source?.Id ?? 0, Amount = amount, Kind = kind });
            if (amount > 0) World.Events.Publish(new DamageDealt(target, source, amount));

            if (source != null && kind != HitKind.Fever)
            {
                target.LastCombatTick = World.CurrentTick;
                source.LastCombatTick = World.CurrentTick;
                Retaliate(target, source);
            }

            if (target.Blood <= 0) Kill(target, source);
            return amount;
        }

        /// <summary>Being hit makes NPCs fight back, and idle players auto-retaliate.</summary>
        private void Retaliate(Entity target, Entity source)
        {
            if (target.TargetId != 0) return;
            if (target is Npc npc)
            {
                if (!npc.Def.Attacks || npc.Returning) return;
                npc.TargetId = source.Id;
                npc.ChaseGoal = null;
            }
            else if (target is Player player && player.Interaction == null && player.Waypoints.Count == 0)
            {
                StartAttack(player, source);
            }
        }

        public void Heal(Entity target, int amount)
        {
            int healed = Math.Min(amount, target.MaxBlood - target.Blood);
            if (healed <= 0) return;
            target.Blood += healed;
            target.HealthChanged = true;
            World.AddHit(new HitEvent { TargetId = target.Id, SourceId = target.Id, Amount = healed, Kind = HitKind.Heal });
        }

        private void Kill(Entity victim, Entity? killer)
        {
            victim.IsDead = true;
            victim.DiedAtTick = World.CurrentTick;
            victim.ClearCombat();
            victim.Waypoints.Clear();
            World.AddDeath(victim.Id);
            foreach (Entity other in World.AllEntities())
            {
                if (other.TargetId != victim.Id) continue;
                other.ClearCombat();
                if (other is Player p && p.Interaction is AttackInteraction) World.StopInteraction(p);
            }
            if (victim is Player player) World.StopInteraction(player);
            World.Events.Publish(new EntityDied(victim, killer));
        }

        // ------------------------------------------------------------------ per tick

        public override void AfterPlayer(Player player)
        {
            if (player.IsDead) return;
            TickResources(player);
            if (player.Effects.RemoveAll(e => World.CurrentTick >= e.EndTick) > 0) World.RecalculateStats(player);
        }

        public override void OnNpcs()
        {
            foreach (Npc npc in World.Npcs) npc.Effects.RemoveAll(e => World.CurrentTick >= e.EndTick);
        }

        private void TickResources(Player player)
        {
            CombatRules rules = Rules;
            bool inCombat = player.InCombat(World.CurrentTick, rules.CombatTimeoutTicks);

            if (rules.IsFevered(player.Bile) && player.Blood > 1)
            {
                int burn = Math.Max(1, (int)Math.Ceiling(player.MaxBlood * rules.FeverBloodPercentPerTick / 100.0));
                burn = Math.Min(burn, player.Blood - 1); // fever weakens but never kills
                player.Blood -= burn;
                player.HealthChanged = true;
                World.AddHit(new HitEvent { TargetId = player.Id, SourceId = player.Id, Amount = burn, Kind = HitKind.Fever });
            }

            if (player.Deflect != CombatStyle.None)
            {
                player.Breath = Math.Max(0, player.Breath - rules.BreathDrainPerTick);
                if (player.Breath == 0)
                {
                    player.Deflect = CombatStyle.None;
                    World.Tell(player, TextKind.Warning, "You are out of breath. Your deflection falls.");
                }
            }
            else if (!inCombat)
            {
                RestoreBreath(player, rules.BreathRegenPerTick);
            }

            if (inCombat) return;

            // Out of combat the body settles: bile drifts back, phlegm fades, blood returns.
            SettleBile(player, rules.BileDecayPerTick);
            AddPhlegm(player, -rules.PhlegmDecayPerTick);
            if (player.Blood < player.MaxBlood && !rules.IsFevered(player.Bile))
            {
                int regen = Math.Max(1, (int)Math.Round(player.MaxBlood * rules.BloodRegenPercentPerTick / 100.0));
                player.Blood = Math.Min(player.MaxBlood, player.Blood + regen);
                player.HealthChanged = true;
            }
        }

        public override void SendState(Player p)
        {
            uint now = World.CurrentTick;
            var state = new PlayerStateMessage
            {
                Tick = now,
                Blood = p.Blood,
                MaxBlood = p.MaxBlood,
                Phlegm = p.Phlegm,
                Bile = p.Bile,
                Breath = p.Breath,
                MaxBreath = p.MaxBreath,
                Deflect = p.Deflect,
                InCombat = p.InCombat(now, Rules.CombatTimeoutTicks),
                TargetId = p.TargetId,
                GlobalCooldownReadyTick = Math.Max(p.GlobalCooldownReadyTick, p.ActionDelayUntilTick),
                QueuedAbility = p.QueuedAbility ?? "",
            };
            foreach (KeyValuePair<string, uint> cd in p.Cooldowns)
                if (cd.Value > now) state.Cooldowns.Add(new CooldownState { AbilityId = cd.Key, ReadyTick = cd.Value });
            foreach (ActiveEffect e in p.Effects)
                state.Effects.Add(new EffectState { Effect = e.Type, Magnitude = e.Magnitude, EndTick = e.EndTick });
            p.Connection.Send(state);
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }

    /// <summary>A player fighting one target until it dies, escapes, or they walk away.</summary>
    public sealed class AttackInteraction : Interaction
    {
        public AttackInteraction(int targetId)
        {
            TargetId = targetId;
        }

        public int TargetId { get; }

        public override bool Tick(World world, Player player) => world.Get<CombatSystem>().TickAttack(player, TargetId);

        public override void End(World world, Player player)
        {
            if (player.TargetId == TargetId) player.ClearCombat();
            player.QueuedAbility = null;
        }
    }
}
