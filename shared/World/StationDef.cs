using System.Collections.Generic;

namespace Rth.Shared.World
{
    /// <summary>
    /// What a station (an object with a "process" action) does to the recipes made at it
    /// (docs/skills.md 2.4): a range burns less than a campfire, a good forge works faster. Every
    /// field is optional.
    /// </summary>
    public sealed class StationDef
    {
        /// <summary>Multiplies the ticks each recipe takes here (0.8: a fifth faster).</summary>
        public double TickScale = 1;
        /// <summary>Multiplies the chance a recipe goes wrong here (a range 0.7, a campfire 1).</summary>
        public double FailScale = 1;
        /// <summary>The forge's draw (Smithing's active layer), or null.</summary>
        public DrawDef? Draw;
    }

    /// <summary>
    /// A station's draw (docs/skills.md, Smithing): a fire every worker at the station shares. It
    /// burns down by itself; working the bellows (the "bellows" action) or feeding it fuel (using a
    /// <see cref="Fuel"/> item on it) builds it up again, up to <see cref="Max"/> ticks of burning.
    /// While it burns above <see cref="HotAbove"/>, recipes here take <see cref="HotTickScale"/> of
    /// their time, for everyone. Working it gives <see cref="Xp"/> in the station's skill.
    /// </summary>
    public sealed class DrawDef
    {
        public const string BellowsAction = "bellows", StateKey = "draw";

        /// <summary>The skill working it trains (the recipes' skill, usually).</summary>
        public string Skill = "";
        /// <summary>The most it holds, in ticks of burning.</summary>
        public int Max = 200;
        /// <summary>Ticks added by one pump of the bellows.</summary>
        public int Bellows = 25;
        /// <summary>Ticks between pumps for one player (the bellows need a moment).</summary>
        public int BellowsTicks = 3;
        /// <summary>What can be fed to it, and how many ticks of burning each adds (better charcoal holds longer).</summary>
        public List<DrawFuel> Fuel = new List<DrawFuel>();
        /// <summary>Above this many ticks left it counts as roaring.</summary>
        public int HotAbove = 0;
        /// <summary>Recipes take this share of their ticks while it roars (0.6: much faster).</summary>
        public double HotTickScale = 0.6;
        /// <summary>XP for a pump of the bellows, and for each fuel fed.</summary>
        public double Xp = 1;
    }

    /// <summary>One fuel a draw takes: an item id or "tag:x", and the ticks of burning it adds.</summary>
    public sealed class DrawFuel
    {
        public string Item = "";
        public int Ticks = 60;
    }

    /// <summary>A station whose draw is burning, as clients near it are told ("draw" in the server state).</summary>
    public sealed class DrawState
    {
        public string Type = "";
        public int Plane;
        public int X;
        public int Y;
        /// <summary>How full the draw is, 0 to 1.</summary>
        public double Level;
        /// <summary>Whether it is roaring (recipes here are faster).</summary>
        public bool Hot;
    }
}
