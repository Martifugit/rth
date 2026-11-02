using System.Collections.Generic;

namespace Rth.Shared.Angling
{
    /// <summary>
    /// The fight the player is in ("anglingFight" server state): everything the client needs to
    /// build the same <see cref="AnglingFight"/> the server runs, and, once it's over, how it ended.
    /// Empty (<see cref="Species"/> "") when there is no fight.
    /// </summary>
    public sealed class AnglingFightState
    {
        public const string StateKey = "anglingFight";

        /// <summary>A number that changes with each new fight, so the client knows a new one began.</summary>
        public int Id;
        public string Species = "";
        /// <summary>The spot it bit at (where the fish is drawn from).</summary>
        public int Plane;
        public int X;
        public int Y;
        public AnglingFightDef Fight = new AnglingFightDef();
        public int Energy;
        public uint Seed;
        public bool Revised;
        public AnglingRigStats Rig;
        /// <summary>Steps the server has stepped (the client's inputs it has replayed).</summary>
        public int Steps;
        public AnglingOutcome Outcome;
        /// <summary>Once landed: its weight, size class, and whether it beat the player's best.</summary>
        public int Grams;
        public string SizeClass = "";
        public bool PersonalBest;

        public AnglingFish Fish() => new AnglingFish { Fight = Fight, Energy = Energy, Seed = Seed, Revised = Revised };
    }

    /// <summary>A trophy spot the player can see working: its species and how long it has left (2 plenty, 1 thinning, 0 nearly gone).</summary>
    public sealed class AnglingSpotView
    {
        public string Type = "";
        public string Species = "";
        public int Plane;
        public int X;
        public int Y;
        public int Phase;
    }

    /// <summary>The active trophy spots near the player that aren't spooked for them ("anglingSpots").</summary>
    public sealed class AnglingSpotsState
    {
        public const string StateKey = "anglingSpots";
        public List<AnglingSpotView> Spots = new List<AnglingSpotView>();
    }

    /// <summary>One slot of the rig: its part (an item id, "" for none) and condition (0-100).</summary>
    public sealed class AnglingSlotState
    {
        public AnglingSlot Slot;
        public string Item = "";
        public double Condition = 100;
    }

    /// <summary>
    /// The rod being rigged ("tackle"): <see cref="Open"/> is the rod whose rig window is open ("":
    /// none). Its slots (the rod's own, in order) with their parts and conditions, the rod's own
    /// condition, and the stats it all makes (with no level bonus).
    /// </summary>
    public sealed class AnglingTackleState
    {
        public const string StateKey = "tackle";
        public const string IntentKind = "tackle";
        public string Open = "";
        public double RodCondition = 100;
        public List<AnglingSlotState> Slots = new List<AnglingSlotState>();
        public AnglingRigStats Stats;
    }

    /// <summary>What a player has learnt and caught of one species.</summary>
    public sealed class AnglingSpeciesLog
    {
        public int Caught;
        public int Revised;
        public int Lost;
        public int BestGrams;
        /// <summary>The moves seen, as bits (1 &lt;&lt; <see cref="AnglingMove"/>).</summary>
        public int Moves;
        /// <summary>Lures it has been landed on (item ids).</summary>
        public List<string> Lures = new List<string>();
        /// <summary>Notes unlocked by people (catch-count notes are worked out from <see cref="Caught"/>).</summary>
        public List<string> Notes = new List<string>();
        /// <summary>Its trophy spots seen, as "plane:x:y".</summary>
        public List<string> Spots = new List<string>();
        /// <summary>A market's lead: the spot named, "plane:x:y", or "".</summary>
        public string Lead = "";
    }

    /// <summary>The fishing log ("anglingLog"): by species id.</summary>
    public sealed class AnglingLogState
    {
        public const string StateKey = "anglingLog";
        public Dictionary<string, AnglingSpeciesLog> Species = new Dictionary<string, AnglingSpeciesLog>();
    }
}
