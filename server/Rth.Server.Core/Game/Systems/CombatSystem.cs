using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Rth.Server.Persistence;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Fighting: attack and ability intents, ranges and chasing, damage and healing, death, and the
    /// per-tick humours (phlegm, bile, regeneration). Deaths are published as
    /// <see cref="EntityDied"/>; what happens next (respawns, loot, the ferryman) belongs to whoever
    /// listens.
    /// </summary>
    /// <remarks>
    /// A player keeps their target while they stay in combat, even after walking away: using an
    /// ability then takes the fight back up (chasing, or stalling if out of range). The target is
    /// lost only by attacking something else, by it dying or leaving, or by dropping out of combat.
    /// </remarks>
    public sealed partial class CombatSystem : GameSystem, IStatContributor, IStatusSource
    {
        private CombatRules Rules => World.Rules;

        protected override void OnAttach()
        {
            World.OnMessage<AttackMessage>((p, m) => HandleAttack(p, m.TargetId));
            World.OnMessage<UseAbilityMessage>((p, m) => HandleUseAbility(p, m.AbilityId));
        }

        // ------------------------------------------------------------------ lifecycle and saving

        public override void OnLogin(Player player, PlayerSave? save)
        {
            World.RecalculateStats(player);
            player.Blood = Clamp(save?.Blood ?? player.MaxBlood, 1, player.MaxBlood);
            player.Bile = Rules.ClampBile(save?.Bile ?? 0);
            player.Deaths = save?.Deaths ?? 0;
        }

        public override void OnSave(Player player, PlayerSave save)
        {
            save.Blood = player.IsDead ? player.MaxBlood : player.Blood;
            save.Bile = player.IsDead ? 0 : player.Bile;
            save.Deaths = player.Deaths;
        }

        /// <summary>Base stats from the rules, plus timed effects from abilities and consumables.</summary>
        public void Contribute(Player player, StatBlock stats)
        {
            stats.Add(StatIds.AbilityDamage, Rules.PlayerAbilityDamage);
            stats.Add(StatIds.MaxBlood, Rules.PlayerMaxBlood);
            stats.Add(StatIds.MaxDevotion, Rules.PlayerMaxDevotion);
            foreach (ActiveEffect e in player.Effects)
            {
                if (e.Buff != null)
                {
                    if (!e.Buff.Conditional)
                        foreach (KeyValuePair<string, int> stat in e.Buff.Stats) stats.Add(stat.Key, stat.Value);
                }
                else if (e.Type == EffectType.Ward) stats.Add(StatIds.Ward, e.Magnitude);
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
            if (target is Npc npc && !npc.Def.Attackable) return;
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

        /// <summary>
        /// A pressed ability. One that's ready now is always taken (it fires this tick, or stalls if
        /// the target is out of range); one pressed during the global cooldown or its own cooldown
        /// waits in the queue only if the player has queueing on, and is ignored otherwise.
        /// </summary>
        private void HandleUseAbility(Player player, string abilityId)
        {
            if (player.IsDead) return;
            if (!World.Content.Abilities.TryGetValue(abilityId, out AbilityDef? ability) || ability.Type == AbilityType.Auto) return;
            if (ability.OnSelf)
            {
                if (!InCombat(player))
                {
                    World.Tell(player, TextKind.Warning, ability.Name + " can only be used in combat.");
                    return;
                }
            }
            else if (player.TargetId == 0)
            {
                World.Tell(player, TextKind.Warning, "You need a target first.");
                return;
            }
            string? problem = Requirements(player, ability);
            if (problem != null)
            {
                World.Tell(player, TextKind.Warning, problem);
                return;
            }
            if (!IsReady(player, ability, World.CurrentTick) && !Options(player).Queueing) return;
            if (!ability.OnSelf && !ResumeAttack(player)) return;
            player.QueuedAbility = abilityId;
        }

        /// <summary>
        /// Takes the fight back up with the kept target if the player walked away from it (or did
        /// something else). Returns false, dropping the target, if it can no longer be fought.
        /// </summary>
        private bool ResumeAttack(Player player)
        {
            if (player.Interaction is AttackInteraction current && current.TargetId == player.TargetId) return true;
            Entity? target = World.FindEntity(player.TargetId);
            if (target == null || target.IsDead || !World.CanSee(player, target))
            {
                player.ClearCombat();
                World.Tell(player, TextKind.Warning, "You need a target first.");
                return false;
            }
            StartAttack(player, target);
            return true;
        }

        /// <summary>Whether an ability could fire this tick: global cooldown, its own cooldown, and no other action (eating).</summary>
        private bool IsReady(Player player, AbilityDef ability, uint now) =>
            now >= player.GlobalCooldownReadyTick && now >= player.ActionDelayUntilTick &&
            !(player.Cooldowns.TryGetValue(ability.Id, out uint ready) && now < ready);

        /// <summary>
        /// Whether the player is in combat: engaged with a target (even while still running to it),
        /// or hit or hitting within the last <see cref="CombatRules.CombatTimeoutTicks"/>.
        /// </summary>
        public bool InCombat(Player player) =>
            player.TargetId != 0 || player.InCombat(World.CurrentTick, Rules.CombatTimeoutTicks);

        /// <summary>The player's combat options (queueing, Revolution), kept in their client state.</summary>
        public CombatOptions Options(Player player)
        {
            ClientStateSystem? state = World.Find<ClientStateSystem>();
            return new CombatOptions
            {
                Queueing = CombatOptions.QueueingFrom(state?.Get(player, CombatOptions.QueueingKey)),
                Revolution = CombatOptions.RevolutionFrom(state?.Get(player, CombatOptions.RevolutionKey)),
                TrainDefence = CombatOptions.TrainDefenceFrom(state?.Get(player, CombatOptions.TrainDefenceKey)),
            };
        }

        /// <summary>
        /// The ability ids on the main action bar of the style the player holds, left to right (what
        /// Revolution fires from). Each style keeps its own main bar; one never arranged is the
        /// content's default for that style (the occult one may still be bar 0 of the older layout).
        /// </summary>
        public IReadOnlyList<string> MainBarAbilities(Player player)
        {
            ClientStateSystem? state = World.Find<ClientStateSystem>();
            CombatStyle style = WeaponStyle(player);
            string? json = state?.Get(player, CombatOptions.MainBarKey(style));
            bool legacy = false;
            if (json == null && style == World.Content.UnarmedStyle)
            {
                json = state?.Get(player, CombatOptions.BarsKey);
                legacy = json != null;
            }
            MainBarCache cache = player.Component<MainBarCache>();
            if (cache.Loaded && cache.Style == style && ReferenceEquals(json, cache.Json)) return cache.Abilities;
            cache.Loaded = true;
            cache.Style = style;
            cache.Json = json;
            cache.Abilities = new List<string>();
            if (json == null)
            {
                cache.Abilities.AddRange(World.Content.DefaultBarFor(style)); // never arranged: the default
                return cache.Abilities;
            }
            try
            {
                string?[]? slots = legacy ? JsonConvert.DeserializeObject<string?[]?[]>(json)?.FirstOrDefault() : JsonConvert.DeserializeObject<string?[]>(json);
                foreach (string? slot in slots ?? new string?[0])
                    if (slot != null && slot.StartsWith(CombatOptions.AbilityPrefix, StringComparison.Ordinal))
                        cache.Abilities.Add(slot.Substring(CombatOptions.AbilityPrefix.Length));
            }
            catch (JsonException)
            {
                // An unreadable layout fires nothing.
            }
            return cache.Abilities;
        }

        private sealed class MainBarCache
        {
            public bool Loaded;
            public CombatStyle Style;
            public string? Json;
            public List<string> Abilities = new List<string>();
        }

        /// <summary>Why the player can't use an ability right now (weapon, levels, items, blood, phlegm, bile), or null.</summary>
        private string? Requirements(Player player, AbilityDef ability)
        {
            SkillSystem? skills = World.Find<SkillSystem>();
            ItemSystem? items = World.Find<ItemSystem>();
            return ability.CheckRequirements(new AbilityUser
            {
                Phlegm = player.Phlegm,
                Bile = player.Bile,
                Blood = player.Blood,
                MaxBlood = player.MaxBlood,
                WeaponStyle = WeaponStyle(player),
                FreeUltimate = HasFreeUltimate(player),
                LevelOf = skills == null ? (Func<string, int>?)null : s => skills.Level(player, s),
                SkillName = skills == null ? null : (Func<string, string>)skills.Name,
                CountOf = items == null ? (Func<string, int>?)null : i => items.CountOf(player, i),
                ItemName = i => World.Content.Item(i)?.Name ?? i,
            });
        }

        /// <summary>The weapon the player holds, or null for empty hands.</summary>
        public ItemDef? Weapon(Player player) => World.Find<ItemSystem>()?.Worn(player, EquipSlot.Weapon);

        /// <summary>The style the player fights with: their weapon's, or the unarmed style.</summary>
        public CombatStyle WeaponStyle(Player player) => CombatStyles.OfWeapon(Weapon(player), World.Content.UnarmedStyle);

        /// <summary>The auto-attack the player makes with what they hold.</summary>
        public AbilityDef AutoAttack(Player player) => World.Content.AutoAttackFor(Weapon(player));

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
            World.PathBeside(attacker, target.Plane, target.Position);
        }

        // ------------------------------------------------------------------ player attacks

        /// <summary>One tick of a player's attack interaction. Returns false when the fight is over.</summary>
        internal bool TickAttack(Player player, int targetId)
        {
            Entity? target = World.FindEntity(targetId);
            if (target == null || target.IsDead || !World.CanSee(player, target))
            {
                if (player.TargetId == targetId) player.ClearCombat();
                return false;
            }

            uint now = World.CurrentTick;
            FireSelfAbility(player, now);
            AbilityDef? queued = AbilityOrNull(player.QueuedAbility);
            if (queued != null && queued.OnSelf) queued = null; // used where you stand, once ready: see FireSelfAbility
            AbilityDef? stalled = AbilityOrNull(player.StalledAbility);
            if (player.StalledAbility != null && stalled == null) player.StalledAbility = null; // removed by a content reload
            AbilityDef auto = AutoAttack(player);
            int range = auto.Range;
            if (stalled != null && queued != null) range = Math.Min(stalled.Range, queued.Range);
            else if (stalled != null) range = stalled.Range;
            else if (queued != null) range = queued.Range;

            if (!Engage(player, target, range))
            {
                // Stalling: an ability used out of range goes on cooldown now and fires on arrival.
                if (stalled == null && queued != null && IsReady(player, queued, now))
                {
                    player.QueuedAbility = null;
                    if (CanStillUse(player, queued))
                    {
                        player.StalledAbility = queued.Id;
                        StartCooldowns(player, queued, now);
                    }
                }
                return true;
            }

            bool released = false;
            if (stalled != null)
            {
                // Its cooldowns ran while the player closed in, so the next ability may follow at once.
                player.StalledAbility = null;
                if (InRange(player, target, stalled.Range) && CanStillUse(player, stalled))
                {
                    UseAbility(player, target, stalled);
                    player.NextAttackTick = Math.Max(player.NextAttackTick, now + (uint)Rules.GlobalCooldownTicks);
                    released = true;
                }
            }

            if (queued != null)
            {
                if (!IsReady(player, queued, now)) return true; // waits in the queue
                if (!InRange(player, target, queued.Range)) return true; // the stall's range was longer: close in next tick
                player.QueuedAbility = null;
                if (!CanStillUse(player, queued)) return true;
                UseAbility(player, target, queued);
                StartCooldowns(player, queued, now);
                return true;
            }
            if (released) return true;
            if (now < player.GlobalCooldownReadyTick || now < player.ActionDelayUntilTick) return true;

            AbilityDef? revolution = NextRevolutionAbility(player, target, now);
            if (revolution != null)
            {
                UseAbility(player, target, revolution);
                StartCooldowns(player, revolution, now);
                return true;
            }

            if (now >= player.NextAttackTick)
            {
                UseAbility(player, target, auto);
                player.NextAttackTick = now + (uint)Math.Max(1, auto.CooldownTicks);
            }
            return true;
        }

        /// <summary>
        /// Fires a queued ability used on yourself (<see cref="AbilityDef.OnSelf"/>: keepsStall
        /// abilities and verses) once it's ready, wherever the player stands, leaving any stall in
        /// place and keeping them in combat.
        /// </summary>
        private void FireSelfAbility(Player player, uint now)
        {
            AbilityDef? ability = AbilityOrNull(player.QueuedAbility);
            if (ability == null || !ability.OnSelf || !IsReady(player, ability, now)) return;
            player.QueuedAbility = null;
            if (!CanStillUse(player, ability)) return;
            UseAbility(player, player, ability);
            StartCooldowns(player, ability, now);
            player.LastCombatTick = now;
        }

        private AbilityDef? AbilityOrNull(string? id) =>
            id != null && World.Content.Abilities.TryGetValue(id, out AbilityDef? ability) ? ability : null;

        /// <summary>Checks requirements again at the moment of use (phlegm or bile may have moved); tells the player if not.</summary>
        private bool CanStillUse(Player player, AbilityDef ability)
        {
            string? problem = Requirements(player, ability);
            if (problem == null) return true;
            World.Tell(player, TextKind.Warning, problem);
            return false;
        }

        private void StartCooldowns(Player player, AbilityDef ability, uint now)
        {
            player.GlobalCooldownReadyTick = now + (uint)Rules.GlobalCooldownTicks;
            player.Cooldowns[ability.Id] = now + (uint)Math.Max(ability.CooldownTicks, Rules.GlobalCooldownTicks);
            player.NextAttackTick = Math.Max(player.NextAttackTick, player.GlobalCooldownReadyTick);
        }

        /// <summary>
        /// Revolution: the leftmost ability on the main bar that the player's mode fires by itself and
        /// that is ready, usable and in range; null if none (or the mode is manual).
        /// </summary>
        private AbilityDef? NextRevolutionAbility(Player player, Entity target, uint now)
        {
            CombatOptions options = Options(player);
            if (options.Revolution == RevolutionMode.Manual) return null;
            foreach (string id in MainBarAbilities(player))
            {
                AbilityDef? ability = AbilityOrNull(id);
                if (ability == null || !options.AutoCasts(ability.Type)) continue;
                if (IsReady(player, ability, now) && Requirements(player, ability) == null && InRange(player, target, ability.Range)) return ability;
            }
            return null;
        }

        /// <summary>Fires an ability (or the auto-attack) from a player at a target: damage, resources, effects.</summary>
        private void UseAbility(Player player, Entity target, AbilityDef ability)
        {
            sbyte pole = (sbyte)Math.Sign(ability.BileShift);
            World.AddAction(new ActionEvent { SourceId = player.Id, TargetId = target.Id, AbilityId = ability.Id, Pole = pole });
            bool freeUltimate = ability.Type == AbilityType.Ultimate && HasFreeUltimate(player);

            // Blood is paid up front: the verse's price, and Hemospike's toll on every ability.
            int bloodCost = ability.BloodCost(player.Blood, player.MaxBlood);
            if (ability.Type != AbilityType.Auto && ability.Type != AbilityType.Verse)
                bloodCost += player.MaxBlood * player.Stats.Get(StatIds.BloodPerAbility) / 100;
            PayBlood(player, bloodCost);

            int dealt = 0;
            if (ability.DamageMax > 0)
            {
                double evasion = target is Npc npc ? npc.Def.Evasion : 0;
                double accuracy = Rules.PlayerAccuracy + (Stat(player, target, StatIds.Accuracy) + StyleStat(player, target, StatIds.AccuracyFor(ability.Style))) / 100.0;
                double hitChance = Math.Max(0.05, Math.Min(1.0, accuracy - evasion));
                if (World.Random.NextDouble() < hitChance)
                {
                    int percent = World.Random.Next(ability.DamageMin, ability.DamageMax + 1);
                    double damage = (player.Stats.Get(StatIds.AbilityDamage) + StyleStat(player, target, StatIds.PowerFor(ability.Style))) * percent / 100.0;
                    double bonus = Stat(player, target, StatIds.DamageBonus) + StyleStat(player, target, StatIds.DamageFor(ability.Style));
                    if (ability.LowBloodBonus > 0) bonus += ability.LowBloodBonus * (1 - player.Blood / (double)Math.Max(1, player.MaxBlood));
                    damage *= Math.Max(0, 1 + (Rules.BileDamageDealtPercent(player.Bile) + bonus) / 100.0);
                    dealt = Damage(target, (int)Math.Round(damage), player, HitKind.Normal, ability.Style);
                    if (ability.BleedPercent > 0 && dealt > 0) AddBleed(target, dealt * ability.BleedPercent / 100, ability.BleedTicks, player, ability.Style);
                }
                else
                {
                    Damage(target, 0, player, HitKind.Miss, ability.Style);
                }
            }

            // Resources move whether or not it landed: the humours were spent either way.
            switch (ability.Type)
            {
                case AbilityType.Builder:
                    AddPhlegm(player, ability.PhlegmGain + player.Stats.Get(StatIds.BuilderPhlegm));
                    break;
                case AbilityType.Spender:
                case AbilityType.Ultimate:
                    if (freeUltimate) Of(player).Scars = 0;
                    else AddPhlegm(player, -ability.PhlegmCost);
                    break;
            }
            ItemSystem? items = World.Find<ItemSystem>();
            if (items != null)
                foreach (ItemCost reagent in ability.Reagents) items.Take(player, reagent.Item, reagent.Count);

            ShiftBile(player, ability.BileShift);
            if (ability.BileTowardZero > 0) SettleBile(player, ability.BileTowardZero);

            int heal = dealt * (ability.HealPercentOfDamage + Stat(player, target, StatIds.Lifesteal)) / 100 + player.MaxBlood * ability.HealPercentOfMaxBlood / 100;
            if (heal > 0) Heal(player, heal);

            if (ability.Effect != EffectType.None && ability.EffectTicks > 0)
                ApplyEffect(player, ability.Effect, ability.EffectMagnitude, ability.EffectTicks);
            if (ability.Buff != null) ApplyBuff(player, ability.Buff);

            SkillSystem? skills = World.Find<SkillSystem>();
            if (skills != null)
                foreach (Rth.Shared.Progress.XpReward xp in ability.Xp) skills.AddXp(player, xp.Skill, xp.Amount);
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

        private static int StyleStat(Player player, string? stat) => stat == null ? 0 : player.Stats.Get(stat);

        private int StyleStat(Player player, Entity target, string? stat) => stat == null ? 0 : Stat(player, target, stat);

        public void ApplyEffect(Entity entity, EffectType type, int magnitude, int ticks)
        {
            entity.Effects.RemoveAll(e => e.Buff == null && e.Type == type);
            entity.Effects.Add(new ActiveEffect { Type = type, Magnitude = magnitude, EndTick = World.CurrentTick + (uint)ticks });
            if (entity is Player p) World.RecalculateStats(p);
        }

        // ------------------------------------------------------------------ damage, healing, death

        /// <summary>Applies damage after the target's wards; returns the amount actually taken.</summary>
        public int Damage(Entity target, int amount, Entity? source, HitKind kind, CombatStyle style = CombatStyle.None)
        {
            if (target.IsDead) return 0;

            if (target is Player defender && amount > 0 && kind != HitKind.Bleed)
            {
                // Warding and wards lower it; choleric raises it (a negative ward).
                double ward = defender.Stats.Get(StatIds.Ward) - Rules.BileDamageTakenPercent(defender.Bile);
                double scaled = amount * Math.Max(0, 1 - Math.Min(90, ward) / 100.0);
                int deflect = StyleStat(defender, StatIds.DeflectFor(style));
                if (deflect > 0)
                {
                    scaled *= 1 - Math.Min(90, deflect) / 100.0;
                    kind = HitKind.Deflected;
                }
                amount = TakeBlow(defender, (int)Math.Round(scaled), ref kind);
            }

            amount = Math.Min(amount, target.Blood);
            target.Blood -= amount;
            target.HealthChanged |= amount > 0;
            World.AddHit(new HitEvent { TargetId = target.Id, SourceId = source?.Id ?? 0, Amount = amount, Kind = kind });
            if (amount > 0)
            {
                World.Events.Publish(new DamageDealt(target, source, amount, style));
                if (target is Player hurt) Scar(hurt);
            }

            if (source != null)
            {
                target.LastCombatTick = World.CurrentTick;
                source.LastCombatTick = World.CurrentTick;
                Retaliate(target, source);
            }

            if (target.Blood <= 0) Kill(target, source);
            return amount;
        }

        /// <summary>Being hit makes NPCs fight back, and idle players auto-retaliate (even with a kept target).</summary>
        private void Retaliate(Entity target, Entity source)
        {
            if (target is Npc npc)
            {
                if (npc.TargetId != 0 || !npc.Def.Attacks || npc.Returning) return;
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
            victim.Bleeds.Clear();
            if (victim is Player scarred) Of(scarred).Scars = 0;
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
            if (!(player.Interaction is AttackInteraction)) FireSelfAbility(player, World.CurrentTick); // in combat with no target
            DropKeptTarget(player);
            DropStallOutOfCombat(player);
            TickResources(player);
            TickBleeds(player);
            if (player.IsDead) return;
            if (player.Effects.RemoveAll(e => World.CurrentTick >= e.EndTick) > 0) World.RecalculateStats(player);
            if (!InCombat(player)) Of(player).Scars = 0;
        }

        /// <summary>
        /// A target kept after walking away lasts until the player drops out of combat (no hits
        /// either way for <see cref="CombatRules.CombatTimeoutTicks"/>), or until it dies or leaves.
        /// </summary>
        private void DropKeptTarget(Player player)
        {
            if (player.TargetId == 0) return;
            if (player.Interaction is AttackInteraction attack && attack.TargetId == player.TargetId) return;
            Entity? target = World.FindEntity(player.TargetId);
            bool gone = target == null || target.IsDead || !World.CanSee(player, target);
            if (gone || !player.InCombat(World.CurrentTick, Rules.CombatTimeoutTicks)) player.ClearCombat();
        }

        /// <summary>
        /// A stalled ability lasts while the player stays in combat (it carries over to a new target);
        /// dropping out of combat loses it, with its cooldown still spent.
        /// </summary>
        private void DropStallOutOfCombat(Player player)
        {
            if (player.StalledAbility == null || InCombat(player)) return;
            string name = AbilityOrNull(player.StalledAbility)?.Name ?? "stalled ability";
            player.StalledAbility = null;
            World.Tell(player, TextKind.Info, "Out of combat, your " + name + " fades unused.");
        }

        public override void OnNpcs()
        {
            foreach (Npc npc in World.Npcs)
            {
                npc.Effects.RemoveAll(e => World.CurrentTick >= e.EndTick);
                if (!npc.IsDead) TickBleeds(npc);
            }
        }

        private void TickResources(Player player)
        {
            CombatRules rules = Rules;
            if (InCombat(player)) return;

            // Out of combat the body settles: bile drifts back, phlegm fades, blood returns.
            SettleBile(player, rules.BileDecayPerTick);
            AddPhlegm(player, -rules.PhlegmDecayPerTick);
            if (player.Blood < player.MaxBlood)
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
                Devotion = (int)Math.Ceiling(p.Devotion),
                MaxDevotion = p.MaxDevotion,
                InCombat = InCombat(p),
                TargetId = p.TargetId,
                GlobalCooldownReadyTick = Math.Max(p.GlobalCooldownReadyTick, p.ActionDelayUntilTick),
                QueuedAbility = p.QueuedAbility ?? "",
                StalledAbility = p.StalledAbility ?? "",
            };
            foreach (KeyValuePair<string, uint> cd in p.Cooldowns)
                if (cd.Value > now) state.Cooldowns.Add(new CooldownState { AbilityId = cd.Key, ReadyTick = cd.Value });
            ReligionSystem? religion = World.Find<ReligionSystem>();
            if (religion != null) state.Hymns.AddRange(religion.Singing(p));
            state.Statuses.AddRange(World.StatusesOf(p));
            p.Connection.Send(state);
        }

        /// <summary>The bile statuses (any lean brings a pole's buff and debuff) and timed ability effects.</summary>
        public void AddStatuses(Player player, List<StatusState> statuses)
        {
            if (player.IsDead) return;
            int bile = player.Bile;
            if (Rules.IsCholeric(bile))
            {
                statuses.Add(new StatusState { Id = StatusIds.Infused, Magnitude = Rules.InfusedPercent(bile) });
                statuses.Add(new StatusState { Id = StatusIds.Choleric, Magnitude = Rules.CholericPercent(bile) });
            }
            if (Rules.IsMelancholic(bile))
            {
                statuses.Add(new StatusState { Id = StatusIds.Warding, Magnitude = Rules.WardingPercent(bile) });
                statuses.Add(new StatusState { Id = StatusIds.Melancholic, Magnitude = Rules.MelancholicPercent(bile) });
            }
            foreach (ActiveEffect e in player.Effects)
                statuses.Add(new StatusState { Id = e.Status, Magnitude = e.Buff != null ? BuffMagnitude(e.Buff) : e.Magnitude, EndTick = e.EndTick });
            int bleeding = player.Bleeds.Sum(b => b.Remaining);
            if (bleeding > 0) statuses.Add(new StatusState { Id = StatusIds.Bleeding, Magnitude = bleeding });
            int scars = Of(player).Scars;
            if (scars > 0) statuses.Add(new StatusState { Id = StatusIds.Scar, Magnitude = scars });
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

        /// <summary>
        /// Walking away (or doing something else) stops attacking but keeps the target, so an
        /// ability can take the fight up again; engaging counts as combat up to this tick.
        /// </summary>
        public override void End(World world, Player player)
        {
            if (player.TargetId == TargetId) player.LastCombatTick = world.CurrentTick;
            player.QueuedAbility = null;
        }
    }
}
