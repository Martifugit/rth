using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Content;
using Rth.Server.Persistence;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.Skills;

namespace Rth.Server.Game
{
    /// <summary>A player's XP per skill, attached as a component.</summary>
    public sealed class PlayerSkills
    {
        public Dictionary<string, double> Xp = new Dictionary<string, double>();
        /// <summary>Temporary boosts (a draught): levels added to a skill until a tick. Not saved.</summary>
        public Dictionary<string, SkillBoost> Boosts = new Dictionary<string, SkillBoost>();
        public bool Dirty = true;
        public string? SentBoosts;
    }

    /// <summary>Levels added to a skill for a while (docs/skills.md 2.5: requirements check the boosted level).</summary>
    public sealed class SkillBoost
    {
        public int Amount;
        public uint Until;
    }

    /// <summary>Published when a player gains a level, so others can react (unlock messages, achievements...).</summary>
    public sealed class LevelUp
    {
        public LevelUp(Player player, string skill, int level)
        {
            Player = player;
            Skill = skill;
            Level = level;
        }

        public Player Player { get; }
        public string Skill { get; }
        public int Level { get; }
    }

    /// <summary>
    /// Skills, XP and levels for every skill defined in skills.json. Other systems award XP with
    /// <see cref="AddXp"/> and check levels with <see cref="Level"/>; combat XP comes from the
    /// <see cref="DamageDealt"/> event, split with Defence by the player's Train defence setting.
    /// Levels can add stats (<c>statsPerLevel</c>). A retired skill's XP moves into the skill that
    /// replaced it when a save is loaded.
    /// </summary>
    public sealed class SkillSystem : GameSystem, IStatContributor
    {
        private const string SaveSection = "skills";
        /// <summary>The server state listing boosted skills and by how many levels ("boosts": {skill: levels}).</summary>
        public const string BoostsKey = "boosts";

        private XpCurve Curve => World.Content.XpCurve;

        protected override void OnAttach() => World.Events.Subscribe<DamageDealt>(OnDamage);

        public PlayerSkills Of(Player player) => player.Component<PlayerSkills>();

        public double Xp(Player player, string skill) => Of(player).Xp.TryGetValue(skill, out double xp) ? xp : 0;

        /// <summary>
        /// The level the player works at: their real level plus any boost (a draught). Requirements,
        /// chances and stats read this (docs/skills.md 2.5); what a player *is* (combat level,
        /// totals, achievements) reads <see cref="BaseLevel"/>.
        /// </summary>
        public int Level(Player player, string skill) => BaseLevel(player, skill) + Boost(player, skill);

        /// <summary>The level their XP gives, with no boost.</summary>
        public int BaseLevel(Player player, string skill) =>
            Curve.LevelForXp((long)Xp(player, skill), Curve.MaxFor(World.Content.Skills.TryGetValue(skill, out SkillDef? def) ? def : null));

        /// <summary>Levels boosted in a skill now (0 for none).</summary>
        public int Boost(Player player, string skill) =>
            Of(player).Boosts.TryGetValue(skill, out SkillBoost? b) && World.CurrentTick < b.Until ? b.Amount : 0;

        /// <summary>
        /// Boosts a skill by <paramref name="amount"/> levels plus <paramref name="percent"/>% of its
        /// real level, for <paramref name="ticks"/>. Boosts don't stack: a bigger one replaces a
        /// smaller, an equal one renews it, a smaller one does nothing.
        /// </summary>
        public void AddBoost(Player player, string skill, int amount, int percent, int ticks)
        {
            if (!World.Content.Skills.ContainsKey(skill) || ticks <= 0) return;
            int levels = amount + BaseLevel(player, skill) * percent / 100;
            if (levels <= 0) return;
            if (Boost(player, skill) > levels)
            {
                World.Tell(player, TextKind.Info, "Your " + Name(skill).ToLowerInvariant() + " is already boosted higher than that.");
                return;
            }
            Of(player).Boosts[skill] = new SkillBoost { Amount = levels, Until = World.CurrentTick + (uint)ticks };
            World.Tell(player, TextKind.Info, "Your " + Name(skill).ToLowerInvariant() + " is boosted to " + Level(player, skill) + ".");
            World.RecalculateStats(player);
        }

        public override void OnTimers()
        {
            foreach (Player player in World.Players)
            {
                Dictionary<string, SkillBoost> boosts = Of(player).Boosts;
                if (boosts.Count == 0) continue;
                List<string> over = boosts.Where(b => World.CurrentTick >= b.Value.Until).Select(b => b.Key).ToList();
                if (over.Count == 0) continue;
                foreach (string skill in over)
                {
                    boosts.Remove(skill);
                    World.Tell(player, TextKind.Info, "Your " + Name(skill).ToLowerInvariant() + " boost wears off.");
                }
                World.RecalculateStats(player);
            }
        }

        public string Name(string skill) => World.Content.SkillName(skill);

        /// <summary>Why the player doesn't meet these requirements, or null if they do.</summary>
        public string? Check(Player player, IEnumerable<SkillRequirement>? requirements, string what) =>
            SkillRequirement.Check(requirements, s => Level(player, s), Name, what);

        public void AddXp(Player player, string skill, double amount)
        {
            if (amount <= 0 || !World.Content.Skills.ContainsKey(skill)) return;
            PlayerSkills skills = Of(player);
            int before = BaseLevel(player, skill);
            skills.Xp[skill] = Math.Min(XpCurve.MaxXp, Xp(player, skill) + amount);
            skills.Dirty = true;

            int after = BaseLevel(player, skill);
            if (after <= before) return;
            World.Tell(player, TextKind.Info, "Your " + Name(skill).ToLowerInvariant() + " is now level " + after + ".");
            World.RecalculateStats(player);
            World.Events.Publish(new LevelUp(player, skill, after));
        }

        private void OnDamage(DamageDealt e)
        {
            if (!(e.Source is Player player) || e.Target == player) return;
            string? defence = World.Content.DefenceSkill;
            double share = defence == null ? 0 : CombatOptions.DefenceShare(World.Find<CombatSystem>()?.Options(player).TrainDefence ?? TrainDefence.Balanced);
            foreach (DamageXpRule rule in World.Content.DamageXp)
            {
                if (rule.Style != e.Style) continue;
                double xp = e.Amount * rule.XpPerDamage;
                AddXp(player, rule.Skill, xp * (1 - share));
                if (defence != null) AddXp(player, defence, xp * share);
            }
        }

        public void Contribute(Player player, StatBlock stats)
        {
            foreach (SkillDef skill in World.Content.Skills.Values)
            {
                if (skill.StatsPerLevel.Count == 0) continue;
                int levels = Level(player, skill.Id) - 1;
                foreach (KeyValuePair<string, int> stat in skill.StatsPerLevel) stats.Add(stat.Key, stat.Value * levels);
            }
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            PlayerSkills skills = Of(player);
            Dictionary<string, double>? saved = save?.Section<Dictionary<string, double>>(SaveSection);
            if (saved != null)
                foreach (KeyValuePair<string, double> s in saved) skills.Xp[s.Key] = s.Value; // unknown skills are kept
            foreach (KeyValuePair<string, string> retired in World.Content.RetiredSkills)
            {
                if (!skills.Xp.TryGetValue(retired.Key, out double xp)) continue;
                skills.Xp.Remove(retired.Key);
                skills.Xp[retired.Value] = Math.Min(XpCurve.MaxXp, Xp(player, retired.Value) + xp);
            }
            skills.Dirty = true;
        }

        public override void OnSave(Player player, PlayerSave save) => save.SetSection(SaveSection, Of(player).Xp);

        public override void SendState(Player player)
        {
            PlayerSkills skills = Of(player);
            var boosted = skills.Boosts.Where(b => World.CurrentTick < b.Value.Until).ToDictionary(b => b.Key, b => b.Value.Amount);
            JsonStateMessage boostState = JsonStateMessage.Of(BoostsKey, boosted);
            if (boostState.Json != skills.SentBoosts)
            {
                skills.SentBoosts = boostState.Json;
                player.Connection.Send(boostState);
            }
            if (!skills.Dirty) return;
            skills.Dirty = false;
            var message = new SkillsMessage();
            foreach (KeyValuePair<string, double> s in skills.Xp) message.Xp[s.Key] = (long)s.Value;
            player.Connection.Send(message);
        }
    }
}
