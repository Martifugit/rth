using System.Collections.Generic;

namespace Rth.Server.Content
{
    /// <summary>
    /// A trial (trials.json): a timed run through one or more regions, begun at a gate object and won
    /// by a dialogue action. Leaving its regions, dying or logging out loses it. The long way back
    /// from the realm of the dead is one; a gauntlet or an agility course would be another.
    /// </summary>
    public sealed class TrialDef
    {
        public string Id = "";
        /// <summary>Shown while running ("The long way back").</summary>
        public string Title = "";
        /// <summary>The regions the run happens in. Stepping out of them ends it, lost.</summary>
        public List<string> Regions = new List<string>();
        /// <summary>The object type whose "trial" action begins it (walked up to first); empty for none.</summary>
        public string Gate = "";
        /// <summary>
        /// Walking into the trial's regions begins it (the causeway past the leaning stones), if its
        /// conditions pass; if they don't, the player is turned back to <see cref="ExitTo"/>.
        /// </summary>
        public bool BeginOnEnter;
        /// <summary>Where the run starts: the player is moved here on beginning. Null: where they stand.</summary>
        public TilePlace? Start;
        /// <summary>Where someone found in the trial's regions without a run is sent (logged back in mid-run). Null: where the dead wake.</summary>
        public TilePlace? ExitTo;
        /// <summary>Dialogue conditions that must all pass to begin ("ferryHolds").</summary>
        public List<DialogueCondition> Conditions = new List<DialogueCondition>();
        /// <summary>Said when a condition fails.</summary>
        public string? LockedMessage;
        /// <summary>Nothing may be carried or worn to begin.</summary>
        public bool EmptyHanded;
        /// <summary>No running while it lasts (the cold is in your legs): the way is walked, a tile a tick.</summary>
        public bool WalkOnly;
        public string? EmptyHandedMessage;
        /// <summary>Blood lost steadily while running; while it runs, blood doesn't come back by itself.</summary>
        public TrialDrain? Drain;
        /// <summary>A status shown while running (statuses.json), or null.</summary>
        public string? Status;
        public string? StartMessage;
        public string? FailMessage;
        /// <summary>Said on winning; {time} and {best} are filled in.</summary>
        public string? WinMessage;
    }

    public sealed class TrialDrain
    {
        /// <summary>A share of max blood taken each time (wards don't apply).</summary>
        public double Percent = 1;
        public int EveryTicks = 1;
    }
}
