using System.Collections.Generic;

namespace Rth.Shared.Items
{
    /// <summary>
    /// An item that is worked on by itself, with no station (grimy herbs cleaned, logs whittled):
    /// it offers <see cref="Verb"/> in its menu, which makes the recipes with no station whose
    /// first input it is (one at once, several through the make window).
    /// </summary>
    public sealed class ProcessDef
    {
        /// <summary>The menu word: "Clean", "Whittle".</summary>
        public string Verb = "Process";
    }

    /// <summary>
    /// The make window (docs/skills.md 2.4, make-X), as the server sends it ("make" in the player's
    /// server state): what can be made here, how many of each the pack allows, and why not. Empty
    /// <see cref="Options"/>: closed. The client answers with the intent "make" ("recipe|count",
    /// count 0 for as many as possible) or "make-close".
    /// </summary>
    public sealed class MakeState
    {
        public const string StateKey = "make", Intent = "make", CloseIntent = "make-close";

        /// <summary>Changes every time a window opens, so the client can tell a new one from an update.</summary>
        public int Serial;
        /// <summary>The window's title ("Anvil", "Clean").</summary>
        public string Title = "";
        /// <summary>The verb on its button ("smith", "cook").</summary>
        public string Verb = "";
        /// <summary>The station it is for (an object type and tile), or empty for an item worked by itself.</summary>
        public string Station = "";
        public int Plane;
        public int X;
        public int Y;
        public List<MakeOption> Options = new List<MakeOption>();

        public bool IsOpen => Options.Count > 0;
    }

    /// <summary>One recipe in the make window.</summary>
    public sealed class MakeOption
    {
        public string Recipe = "";
        /// <summary>What it makes, for the list ("copper dagger").</summary>
        public string Name = "";
        /// <summary>A heading it sits under ("Daggers", "Bars"), or empty.</summary>
        public string Group = "";
        /// <summary>The item shown for it (its first output, else its first input) and how many one make gives.</summary>
        public string Item = "";
        public int Count = 1;
        public string Skill = "";
        public int Level = 1;
        public double Xp;
        public List<ItemCost> Inputs = new List<ItemCost>();
        /// <summary>How many the pack has the inputs for (0: none).</summary>
        public int Makeable;
        /// <summary>Why it can't be made even with the inputs (level, a missing tool), or null.</summary>
        public string? Problem;
        /// <summary>The chance one goes wrong at the player's level (burnt), 0 to 1.</summary>
        public double FailChance;
    }
}
