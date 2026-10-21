using System.Collections.Generic;
using Rth.Shared.Skills;

namespace Rth.Shared.Religion
{
    /// <summary>
    /// A hymn: one of Tithe's prayers (server/data/content/hymns.json), sung for any combat style.
    /// While it's sung it adds its stats and drains devotion every tick. Everything a hymn does is
    /// a stat (deflection, boosts, life steal), so a new hymn needs no code.
    /// </summary>
    public sealed class HymnDef
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        /// <summary>Icon id (the client's icon library).</summary>
        public string Icon = "";
        /// <summary>Devotion drained each tick it is sung (before the singer's devotion bonus).</summary>
        public double Drain;
        /// <summary>Overheads (the deflections, life steal) exclude each other: only one is sung at a time.</summary>
        public bool Overhead;
        /// <summary>
        /// Hymns in the same line replace each other (a style's tiers, say), so singing a higher one
        /// stops the lower. Empty: a line of its own.
        /// </summary>
        public string Line = "";
        /// <summary>Stat id to amount while sung, as on equipment.</summary>
        public Dictionary<string, int> Stats = new Dictionary<string, int>();
        /// <summary>Religion level and anything else needed to sing it.</summary>
        public List<SkillRequirement> Requirements = new List<SkillRequirement>();

        /// <summary>Whether singing <paramref name="other"/> stops this one.</summary>
        public bool ExcludedBy(HymnDef other) =>
            other.Id != Id && ((Overhead && other.Overhead) || (Line.Length > 0 && Line == other.Line));
    }

    /// <summary>hymns.json: the hymns, and how Religion is trained by singing them.</summary>
    public sealed class HymnsFile
    {
        /// <summary>The skill that hymns need levels in and train.</summary>
        public string Skill = "religion";
        /// <summary>Religion XP for each point of devotion drained while in combat (a trickle).</summary>
        public double XpPerDevotion = 0.1;
        public List<HymnDef> Hymns = new List<HymnDef>();
    }
}
