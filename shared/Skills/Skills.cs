using System;
using System.Collections.Generic;

namespace Rth.Shared.Skills
{
    /// <summary>A skill (butchering, occultism...), from server/data/content/skills.json.</summary>
    public sealed class SkillDef
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        /// <summary>Stat id to amount gained per level above 1 (e.g. occultism raises ability damage).</summary>
        public Dictionary<string, int> StatsPerLevel = new Dictionary<string, int>();
    }

    /// <summary>
    /// How much XP each level needs. One curve for every skill, defined in data: level 2 costs
    /// <see cref="FirstLevelXp"/> and each level after costs <see cref="Growth"/> times the one before.
    /// </summary>
    public sealed class XpCurve
    {
        public int MaxLevel = 99;
        public double FirstLevelXp = 60;
        public double Growth = 1.105;

        /// <summary>Total XP needed to reach a level (level 1 is 0 XP).</summary>
        public long XpForLevel(int level)
        {
            if (level <= 1) return 0;
            level = Math.Min(level, MaxLevel);
            double total = Math.Abs(Growth - 1) < 1e-9
                ? FirstLevelXp * (level - 1)
                : FirstLevelXp * (Math.Pow(Growth, level - 1) - 1) / (Growth - 1);
            return (long)Math.Floor(total);
        }

        public int LevelForXp(long xp)
        {
            int level = 1;
            while (level < MaxLevel && xp >= XpForLevel(level + 1)) level++;
            return level;
        }
    }

    /// <summary>"Needs level N in skill S": used by abilities, equipment, recipes, and later quests and areas.</summary>
    public sealed class SkillRequirement
    {
        public string Skill = "";
        public int Level = 1;

        /// <summary>The first unmet requirement as a sentence, or null if all are met.</summary>
        public static string? Check(IEnumerable<SkillRequirement>? requirements, Func<string, int> levelOf, Func<string, string> nameOf, string what)
        {
            if (requirements == null) return null;
            foreach (SkillRequirement r in requirements)
                if (levelOf(r.Skill) < r.Level)
                    return "You need " + nameOf(r.Skill).ToLowerInvariant() + " level " + r.Level + " to " + what + ".";
            return null;
        }
    }
}
