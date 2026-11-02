using System.Collections.Generic;

namespace Rth.Shared.Progress
{
    /// <summary>
    /// A player's run at a trial (the "trial" server state): which trial, when it began, their best
    /// time, and the objects whose once-per-attempt gift they have had (a kindled lantern). An empty
    /// <see cref="Trial"/> means no run.
    /// </summary>
    public sealed class TrialState
    {
        public string Trial = "";
        public string Title = "";
        public uint StartTick;
        public int BestTicks;
        /// <summary>Objects used up this attempt, [plane, x, y] each.</summary>
        public List<int[]> Used = new List<int[]>();
    }
}
