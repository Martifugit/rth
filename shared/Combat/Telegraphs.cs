using System.Collections.Generic;

namespace Rth.Shared.Combat
{
    /// <summary>
    /// The "telegraphs" server state: creatures' special attacks winding up near the player, so
    /// the client can mark where each will land and when. Sent while any is near, and once more,
    /// empty, when the last has landed.
    /// </summary>
    public sealed class TelegraphsState
    {
        public const string Key = "telegraphs";

        public List<Telegraph> Telegraphs = new List<Telegraph>();
    }

    /// <summary>One special attack on its way.</summary>
    public sealed class Telegraph
    {
        /// <summary>The creature using it.</summary>
        public int NpcId;
        /// <summary>The move's id and name (npcs.json).</summary>
        public string Move = "";
        public string Name = "";
        public int Plane;
        /// <summary>The middle of the square it lands on.</summary>
        public int X;
        public int Y;
        /// <summary>Tiles from the middle to the square's edge (0: one tile).</summary>
        public int Radius;
        /// <summary>Above 0, it follows this entity instead of a fixed square (a heavy blow at its target).</summary>
        public int TargetId;
        public uint StartTick;
        /// <summary>The tick it lands on.</summary>
        public uint LandTick;
        public CombatStyle Style;
    }
}
