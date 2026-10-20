using System.Collections.Generic;

namespace Rth.Shared.World
{
    /// <summary>
    /// A kind of interactive world object (a skinning rack, a still, later rocks and doors), from
    /// server/data/content/objects.json. Instances are placed in the Unity scene with a MapObject
    /// marker and exported with the map. Each action's id is dispatched to whichever server system
    /// registered it ("process" is the recipe system's).
    /// </summary>
    public sealed class ObjectDef
    {
        public string Id = "";
        public string Name = "";
        public string Examine = "";
        public List<ActionDef> Actions = new List<ActionDef>();
    }

    public sealed class ActionDef
    {
        /// <summary>What the server dispatches on ("process", later "mine", "open"...).</summary>
        public string Id = "";
        /// <summary>What the menu shows ("Butcher", "Distil").</summary>
        public string Label = "";
        /// <summary>Only works once the player has this flag (set by dialogue or quests), e.g. a secret still.</summary>
        public string? RequiresFlag;
        /// <summary>Said when the flag is missing.</summary>
        public string? LockedMessage;
        /// <summary>Said when the action is used (signposts, notices). Needs no handler.</summary>
        public string? Message;
    }

    /// <summary>One placed object, as exported from the scene (<c>&lt;map&gt;.objects.json</c>).</summary>
    public sealed class PlacedObject
    {
        public string Type = "";
        public int Plane;
        public int X;
        public int Y;

        public TileCoord Tile => new TileCoord(X, Y);
    }
}
