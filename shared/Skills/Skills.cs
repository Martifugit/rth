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
        /// <summary>Icon id (the client's icon library).</summary>
        public string Icon = "";
        /// <summary>Stat id to amount gained per level above 1 (e.g. occultism raises ability damage).</summary>
        public Dictionary<string, int> StatsPerLevel = new Dictionary<string, int>();
        /// <summary>This skill's highest level; 0 means the curve's (100).</summary>
        public int MaxLevel;
    }

    /// <summary>
    /// How much XP each level needs: RuneScape's growth formula (each level costs a quarter of
    /// level + 300 x 2^(level / 7) more than the one before), running to level 100 rather than
    /// 99. Level 2 is 83 XP, 50 is 101,333, 99 is 13,034,431 and 100 is 14,391,160. Skills can cap
    /// lower with <see cref="SkillDef.MaxLevel"/>.
    /// </summary>
    public sealed class XpCurve
    {
        /// <summary>The cap for skills that don't set their own.</summary>
        public int MaxLevel = 100;

        /// <summary>No skill holds more XP than this.</summary>
        public const long MaxXp = 200_000_000;

        private const int TableSize = 127; // the formula, far enough past any cap
        private static readonly long[] Table = BuildTable();

        private static long[] BuildTable()
        {
            var table = new long[TableSize + 1];
            double points = 0;
            for (int level = 1; level < TableSize; level++)
            {
                points += Math.Floor(level + 300 * Math.Pow(2, level / 7.0));
                table[level + 1] = (long)Math.Floor(points / 4);
            }
            return table;
        }

        /// <summary>Total XP needed to reach a level (level 1 is 0 XP). Capped at the curve's max level.</summary>
        public long XpForLevel(int level) => XpForLevel(level, MaxLevel);

        public long XpForLevel(int level, int cap)
        {
            level = Math.Min(level, Math.Min(cap, TableSize));
            return level <= 1 ? 0 : Table[level];
        }

        public int LevelForXp(long xp) => LevelForXp(xp, MaxLevel);

        /// <summary>The level an amount of XP gives, up to a cap (a skill's own max level).</summary>
        public int LevelForXp(long xp, int cap)
        {
            cap = Math.Min(cap, TableSize);
            int level = 1;
            while (level < cap && xp >= Table[level + 1]) level++;
            return level;
        }

        /// <summary>A skill's cap: its own if it sets one, else the curve's.</summary>
        public int MaxFor(SkillDef? skill) => skill != null && skill.MaxLevel > 0 ? skill.MaxLevel : MaxLevel;
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
