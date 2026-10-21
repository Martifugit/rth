using System.Collections.Generic;
using Rth.Shared.Items;

namespace Rth.Shared.Progress
{
    /// <summary>
    /// One entry in the achievement diary (achievements.json): a goal in an area, at a tier, met
    /// by an objective the server counts, with an optional reward. Sent to clients as content.
    /// </summary>
    public sealed class AchievementDef
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        /// <summary>Where it belongs in the diary: "Tolling".</summary>
        public string Area = "";
        /// <summary>"Easy", "Medium", "Hard"...</summary>
        public string Tier = "Easy";
        public Objective Objective = new Objective();
        public Reward Reward = new Reward();
    }

    /// <summary>
    /// Something to count. Kinds: "kill" (NPC type), "recipe" (recipe id), "level" (skill reaching
    /// Count), "quest" (quest completed), "object" ("type:action"), "talk" (NPC type), "region"
    /// (entering a region), "obtain" (an item received from any source).
    /// </summary>
    public sealed class Objective
    {
        public string Kind = "";
        public string Target = "";
        public int Count = 1;
    }

    public sealed class Reward
    {
        public List<XpReward> Xp = new List<XpReward>();
        public List<ItemCost> Items = new List<ItemCost>();
    }

    public sealed class XpReward
    {
        public string Skill = "";
        public double Amount;
    }

    /// <summary>A player's progress on the diary, as the client sees it.</summary>
    public sealed class AchievementProgress
    {
        /// <summary>Achievement id to progress (0..Count); completed ones are in <see cref="Done"/>.</summary>
        public Dictionary<string, int> Progress = new Dictionary<string, int>();
        public List<string> Done = new List<string>();
    }

    /// <summary>A player's bestiary, as the client sees it: kills and what each creature has given them.</summary>
    public sealed class BestiaryState
    {
        public Dictionary<string, BestiaryEntry> Creatures = new Dictionary<string, BestiaryEntry>();
        /// <summary>Boss id to the player's completions, times and drop log.</summary>
        public Dictionary<string, BossRecord> Bosses = new Dictionary<string, BossRecord>();
    }

    public sealed class BestiaryEntry
    {
        public int Kills;
        /// <summary>Item id to total obtained from this creature (its drops and its carcass).</summary>
        public Dictionary<string, long> Obtained = new Dictionary<string, long>();
    }
}
