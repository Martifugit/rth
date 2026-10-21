using System.Collections.Generic;

namespace Rth.Shared.Progress
{
    /// <summary>
    /// A boss: an encounter you can run, win or fail (a fight, or a skilling boss like the Unquiet
    /// Saint). From server/data/content/bosses.json. Its runs are timed, and the bestiary keeps
    /// each player's completions, best time and drop log.
    /// </summary>
    public sealed class BossDef
    {
        public string Id = "";
        public string Name = "";
        public string Icon = "";
        public string Description = "";
        /// <summary>The region it is fought in (an instanced arena), or empty.</summary>
        public string Region = "";
        /// <summary>A drop table rolled for each completion (its reward chest), or null.</summary>
        public string? Rewards;
    }

    /// <summary>One player's history with one boss.</summary>
    public sealed class BossRecord
    {
        public int Completions;
        public int Attempts;
        /// <summary>The fastest completion in ticks (0 = none yet).</summary>
        public int BestTicks;
        public int LastTicks;
        /// <summary>Item id to total obtained from this boss's rewards.</summary>
        public Dictionary<string, long> Obtained = new Dictionary<string, long>();
    }

    /// <summary>The run in progress, as the client shows it (the "bossRun" state; empty <see cref="Boss"/> for none).</summary>
    public sealed class BossRunState
    {
        public string Boss = "";
        public uint StartTick;
        public int BestTicks;
        /// <summary>The encounter's big bars (progress, and how close it is to failing), left to right; empty for none.</summary>
        public List<EncounterBar> Bars = new List<EncounterBar>();
    }

    /// <summary>One of an encounter's bars across the top of the screen (the Saint's Requiem and Wake).</summary>
    public sealed class EncounterBar
    {
        public string Id = "";
        public string Label = "";
        /// <summary>0 to 1.</summary>
        public float Value;
        /// <summary>#RRGGBB.</summary>
        public string Colour = "#c0a060";
        /// <summary>A bar that fails the run when full: drawn as a threat.</summary>
        public bool Danger;
    }
}
