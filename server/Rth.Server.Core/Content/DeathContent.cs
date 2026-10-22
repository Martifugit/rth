using System.Collections.Generic;

namespace Rth.Server.Content
{
    /// <summary>A tile in the world, named in data.</summary>
    public sealed class TilePlace
    {
        public int Plane;
        public int X;
        public int Y;
    }

    /// <summary>
    /// What happens when a player dies, from death.json: where they wake (the realm of the dead),
    /// the ferryman who holds what they died with, his fee, the obol, and what he says.
    /// </summary>
    public sealed class DeathRules
    {
        /// <summary>Where a dead player wakes. Null, or a tile that isn't open ground, means the world's spawn.</summary>
        public TilePlace? Wake;
        /// <summary>Where the ferryman takes a player back to. Null means the world's spawn (the respawn point).</summary>
        public TilePlace? ReturnTo;

        /// <summary>The name his lines are spoken under.</summary>
        public string Ferryman = "The ferryman";
        /// <summary>The conversation opened when a player wakes (dialogues.json), or null for none.</summary>
        public string? Dialogue;

        /// <summary>The item the fee is paid in.</summary>
        public string Currency = "marks";
        /// <summary>The fee is this percentage of the value of everything held (rounded up)...</summary>
        public double FeePercent = 10;
        /// <summary>...unless that value is below this, when he takes you back with it for nothing.</summary>
        public int FreeBelow = 500;
        /// <summary>How long unpaid gear is kept, in real hours.</summary>
        public double HoldHours = 24;

        /// <summary>An item that, carried in the pack at death, is taken as the whole fee. Null for none.</summary>
        public string? Obol;

        public FerrymanLines Lines = new FerrymanLines();
    }

    /// <summary>What the ferryman says as a player dies, by situation. {name} and {deaths} are filled in.</summary>
    public sealed class FerrymanLines
    {
        public List<string> First = new List<string>();
        /// <summary>He noticed an obol and took it.</summary>
        public List<string> Obol = new List<string>();
        /// <summary>Nothing worth a fee (or nothing at all).</summary>
        public List<string> Free = new List<string>();
        public List<string> Again = new List<string>();
        /// <summary>Sometimes said instead, from the tenth death on.</summary>
        public List<string> Frequent = new List<string>();
    }
}
