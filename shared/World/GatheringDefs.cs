using System.Collections.Generic;

namespace Rth.Shared.World
{
    /// <summary>
    /// One thing a node can give on a success, by level (a fishing spot's perch and pike, a patch's
    /// herbs): every catch the player has the level for is in the running, picked by
    /// <see cref="Weight"/>, and gives its own <see cref="Xp"/>. A node with catches gives one of
    /// them instead of its <see cref="NodeDef.Items"/> and <see cref="NodeDef.Xp"/>.
    /// </summary>
    public sealed class NodeCatch
    {
        public string Item = "";
        public int Count = 1;
        public int Level = 1;
        public double Xp;
        public double Weight = 1;
    }

    /// <summary>
    /// A node that comes and goes (docs/skills.md: fishing spots): each on its own, knowing nothing
    /// of any other, is there for <see cref="MinTicks"/> to <see cref="MaxTicks"/>, then gone (dormant:
    /// unseen, unworkable) for <see cref="AwayMinTicks"/> to <see cref="AwayMaxTicks"/>, and one used
    /// up goes at once. Before it comes back it may (<see cref="BubbleChance"/>) bubble for
    /// <see cref="BubbleTicks"/>: a player can cast into the bubbles and wait, and is there when the
    /// fish arrive, which <see cref="Reading"/> rewards.
    /// </summary>
    public sealed class NodeRoamDef
    {
        public int MinTicks = 100;
        public int MaxTicks = 300;
        public int AwayMinTicks = 50;
        public int AwayMaxTicks = 150;
        public double BubbleChance = 0.5;
        public int BubbleTicks = 16;
        /// <summary>Said to whoever was working it when it goes.</summary>
        public string Message = "The fish move off.";
        /// <summary>Said on casting into the bubbles before the fish are there.</summary>
        public string BubbleMessage = "You cast into the bubbling water and wait.";
        public NodeReadingDef? Reading;
    }

    /// <summary>
    /// Reading the water (Fishing's active layer): whoever is already working a node when it comes
    /// back gains a stack of <see cref="Status"/>, each <see cref="PerStack"/> more on the chance of a
    /// catch (a share of it: 0.2 is a fifth more), up to <see cref="MaxStacks"/>. A stack wears off
    /// every <see cref="DecayTicks"/> without a new one, so keeping them high means finding bubbles.
    /// </summary>
    public sealed class NodeReadingDef
    {
        public string Status = "fish_whisperer";
        public double PerStack = 0.2;
        public int MaxStacks = 5;
        public int DecayTicks = 60;
        public string Message = "The fish come to your line: you read the water right.";
    }

    /// <summary>
    /// A node worked by leaving a trap (docs/skills.md: eels): working it sets a
    /// <see cref="Item"/> (taken from the pack) as the player's own; it fills a catch every
    /// <see cref="Ticks"/>, up to <see cref="Max"/>; working it again collects what it holds (each
    /// catch rolls the node's success chance) and gives the trap back. One trap a node.
    /// </summary>
    public sealed class NodeTrapDef
    {
        /// <summary>The trap item set (an id, or "tag:x").</summary>
        public string Item = "";
        public int Ticks = 50;
        public int Max = 5;
        /// <summary>Client only: the model shown on the node while a trap is set (a path under Resources).</summary>
        public string Model = "Nature/EelTraps";
    }

    /// <summary>
    /// Tending (docs/skills.md, Woodcutting: tending the stool): every <see cref="EveryTicks"/> one
    /// node of each group needs tending for <see cref="NeedTicks"/> (it shows). Tending it (the
    /// "tend" action) gives <see cref="Xp"/> and, for <see cref="TendedTicks"/>, everyone working it
    /// gets <see cref="SuccessBonus"/> on each attempt and its chance to be cut back is multiplied
    /// by <see cref="DepleteScale"/>.
    /// </summary>
    public sealed class NodeTendingDef
    {
        public const string Action = "tend";

        public int GroupRadius = 8;
        /// <summary>On average, ticks of someone chopping in the stand before one of its trees needs tending (none while one needs or enjoys it).</summary>
        public int EveryTicks = 100;
        public int NeedTicks = 80;
        public int TendedTicks = 200;
        public double SuccessBonus = 0.15;
        public double DepleteScale = 0.25;
        public double Xp = 10;
        /// <summary>Said to players near it when a node starts needing tending, or null. {where}: which way it lies from them.</summary>
        public string? NeedMessage;
        public string TendedMessage = "You clear the suckers and brambles from its foot.";
        /// <summary>A status (statuses.json) shown while chopping a tended tree, {value}: the chance it adds in percent; null: none.</summary>
        public string? Status;
    }

    /// <summary>
    /// The gathering states that change how placed nodes look, for one player ("gathering" in the
    /// server state): roaming nodes that aren't there now, traps, and nodes needing or enjoying
    /// tending, all within sight.
    /// </summary>
    public sealed class GatheringWorldState
    {
        public const string StateKey = "gathering";

        public List<NodeRef> Dormant = new List<NodeRef>();
        public List<TrapState> Traps = new List<TrapState>();
        public List<NodeRef> NeedsTending = new List<NodeRef>();
        /// <summary>Dormant nodes bubbling: about to come back, workable already (cast and wait).</summary>
        public List<NodeRef> Bubbling = new List<NodeRef>();
        public List<NodeRef> Tended = new List<NodeRef>();
    }

    /// <summary>A placed node, by type and tile.</summary>
    public sealed class NodeRef
    {
        public string Type = "";
        public int Plane;
        public int X;
        public int Y;
    }

    /// <summary>A trap set on a node: whether it is the player's own, and (if so) how full it is.</summary>
    public sealed class TrapState
    {
        public string Type = "";
        public int Plane;
        public int X;
        public int Y;
        public bool Mine;
        /// <summary>For the player's own trap: 0 to 1 of its catches.</summary>
        public double Full;
    }
}
