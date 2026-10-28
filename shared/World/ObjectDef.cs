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
        /// <summary>Makes it a gathering node (a rock, a tree, a monument's fount), worked with the "gather" action.</summary>
        public NodeDef? Node;
        /// <summary>Makes it a door: it closes an edge of its tile, and "open" and "close" swing it.</summary>
        public DoorDef? Door;
        /// <summary>
        /// Client only: colours to swap on every placement's model, from a material name to a
        /// palette colour or #RRGGBB ({"Stone": "CopperOre"}), so one rock serves every ore.
        /// </summary>
        public Dictionary<string, string> Recolor = new Dictionary<string, string>();
        /// <summary>
        /// Client only: the model a depleted node shows in place of its own look, a path under
        /// Resources ("Nature/AlderStool": a felled tree's stump, a picked bush). Without one, a
        /// depleted node sinks and dims.
        /// </summary>
        public string? DepletedModel;
    }

    /// <summary>
    /// A gathering node (docs/skills.md 2.3): every <see cref="Ticks"/> an attempt succeeds by
    /// level, giving XP and whatever the node gives (items, points, a roll on an extras table),
    /// until it depletes for a while. Nodes are shared by everyone. One mechanism for ore, trees,
    /// fishing spots, herbs and Cruor's founts.
    /// </summary>
    public sealed class NodeDef
    {
        public string Skill = "";
        /// <summary>The level needed to work it; also where the success chance starts climbing.</summary>
        public int Level = 1;
        /// <summary>Anything else needed to work it (all checked with boosts).</summary>
        public List<Rth.Shared.Skills.SkillRequirement> Requirements = new List<Rth.Shared.Skills.SkillRequirement>();
        /// <summary>The verb for messages ("empower", "mine").</summary>
        public string Verb = "gather";
        /// <summary>The character animation played on each attempt (a clip or role in animations.json).</summary>
        public string Animation = "work";
        /// <summary>Ticks between attempts.</summary>
        public int Ticks = 4;
        /// <summary>Success chance: clamp(BaseChance + PerLevel x (level - Level), MinChance, MaxChance).</summary>
        public double BaseChance = 0.4;
        public double PerLevel = 0.01;
        public double MinChance = 0.05;
        public double MaxChance = 0.95;
        public double Xp;
        /// <summary>The tool kind needed to work it ("pickaxe", "hatchet"), or null for bare hands.</summary>
        public string? Tool;
        /// <summary>The lowest tool tier that works it (0: any).</summary>
        public int ToolTier;
        /// <summary>Said instead of "You need a pickaxe to mine the copper seam." when the tool is missing, or null.</summary>
        public string? NoToolMessage;
        /// <summary>
        /// The chance that a success uses up one of the node's yields: 1 for ore (every success),
        /// 1/8 for a tree that falls by chance. A tool's conservation lowers it further.
        /// </summary>
        public double DepleteChance = 1;
        /// <summary>Items given on every success.</summary>
        public List<Rth.Shared.Items.ItemCost> Items = new List<Rth.Shared.Items.ItemCost>();
        /// <summary>A drop table rolled on every success (gems from ore), or null.</summary>
        public string? Extras;
        /// <summary>A points pool (pledges) paid with <see cref="PointsChance"/> on a success, or null.</summary>
        public string? Points;
        public int PointsAmount = 1;
        public double PointsChance = 1;
        /// <summary>
        /// Successes before it depletes, rolled between these each time it (re)fills. 1 and 1 for a
        /// rock that gives one ore; 20 to 40 for a fount.
        /// </summary>
        public int YieldsMin = 1;
        public int YieldsMax = 1;
        /// <summary>
        /// How long it stays depleted with nobody else about (higher tiers longer). Every other
        /// player near it (within <see cref="NodeDef.CrowdRadius"/>) takes <see cref="RespawnPerPlayer"/>
        /// of it off, down to <see cref="MinRespawnTicks"/>: a busy spot doesn't starve.
        /// </summary>
        public int RespawnTicks = 10;
        /// <summary>The shortest it stays depleted however many are about (null: <see cref="RespawnTicks"/>, no crowd effect).</summary>
        public int? MinRespawnTicks;
        /// <summary>The share of <see cref="RespawnTicks"/> each other player nearby takes off (0.15: 15%).</summary>
        public double RespawnPerPlayer = 0.15;
        public const int CrowdRadius = 15;
        /// <summary>Said when you start working it, or null.</summary>
        public string? StartMessage;
        /// <summary>What your character says out loud as they start, or null.</summary>
        public string? StartSay;
        /// <summary>Blood it takes as you start (a fount cutting you open); never the last point.</summary>
        public int StartDamage;
        /// <summary>Said when it depletes under you.</summary>
        public string DepletedMessage = "There's nothing more to get here for now.";
        /// <summary>Now and then one node of a group wakes for a while and gives more (a hungry fount), or null.</summary>
        public NodeSurgeDef? Surge;
    }

    /// <summary>
    /// A surge: about every <see cref="EveryTicks"/>, one working node in each group (nodes of
    /// one type within <see cref="GroupRadius"/> of each other) surges for <see cref="Ticks"/>,
    /// multiplying the XP and the chance of points.
    /// </summary>
    public sealed class NodeSurgeDef
    {
        public int EveryTicks = 150;
        public int Ticks = 15;
        public int GroupRadius = 8;
        public double XpMultiplier = 4;
        public double PointsMultiplier = 4;
        /// <summary>Said to players nearby when one wakes, or null.</summary>
        public string? Message;
    }

    /// <summary>
    /// A door (objects.json "door"): it stands on a tile and closes one of that tile's edges
    /// (<see cref="PlacedObject.Edge"/>). The map is exported with the edge open; the server closes
    /// it at startup and opens and closes it with the "open" and "close" actions, for everyone.
    /// </summary>
    public sealed class DoorDef
    {
        /// <summary>The actions that swing a door, and the server state listing the doors standing open.</summary>
        public const string OpenAction = "open-door", CloseAction = "close-door", StateKey = "doors";

        /// <summary>Ticks an opened door stays open before it swings shut by itself; null or 0: until someone closes it.</summary>
        public int? CloseAfter;
    }

    /// <summary>A door standing open, as clients are told ("doors" in the player's server state).</summary>
    public sealed class DoorState
    {
        public int Plane;
        public int X;
        public int Y;
        public string Edge = "";
    }

    /// <summary>
    /// What a player's current interaction is aimed at, as clients are told ("focus" in the
    /// player's server state), so the client can mark it: an object on a tile, or an entity (an NPC
    /// talked to or fought). Empty <see cref="Kind"/>: nothing.
    /// </summary>
    public sealed class InteractionFocus
    {
        public const string StateKey = "focus", Object = "object", Entity = "entity";

        public string Kind = "";
        /// <summary>An object's type, plane and tile.</summary>
        public string? Type;
        public int Plane;
        public int X;
        public int Y;
        /// <summary>An entity's id.</summary>
        public int Id;

        public static InteractionFocus OnObject(PlacedObject o) => new InteractionFocus { Kind = Object, Type = o.Type, Plane = o.Plane, X = o.X, Y = o.Y };

        public static InteractionFocus OnEntity(int id) => new InteractionFocus { Kind = Entity, Id = id };
    }

    /// <summary>A node that isn't in its ordinary state, as clients are told ("nodes" in the player's server state).</summary>
    public sealed class NodeState
    {
        public string Type = "";
        public int Plane;
        public int X;
        public int Y;
        public bool Depleted;
        public bool Surging;
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
        /// <summary>Skill levels it needs: a rubble pile cleared with Mining, a ford waded with Fishing.</summary>
        public List<Rth.Shared.Skills.SkillRequirement> Requirements = new List<Rth.Shared.Skills.SkillRequirement>();
        /// <summary>Said when the action is used (signposts, notices). Needs no handler.</summary>
        public string? Message;
        /// <summary>
        /// A character animation (a clip or role in the client's animations.json) the player plays
        /// facing the thing, once they've walked up to it: kneeling at a shrine, bowing to a king.
        /// Needs no handler; with one, the handler runs too.
        /// </summary>
        public string? Animation;
    }

    /// <summary>One placed object, as exported from the scene (<c>&lt;map&gt;.objects.json</c>).</summary>
    public sealed class PlacedObject
    {
        public string Type = "";
        public int Plane;
        public int X;
        public int Y;
        /// <summary>
        /// For a stair, ladder or trapdoor (the "climb" action): the floor and tile it leads to,
        /// the landing marked in the scene. Null for everything else.
        /// </summary>
        public int? ToPlane;
        public int? ToX;
        public int? ToY;
        /// <summary>For a door: the edge of its tile it closes ("N", "E", "S" or "W"). Null for everything else.</summary>
        public string? Edge;

        public TileCoord Tile => new TileCoord(X, Y);

        /// <summary>Where a climb ends up, if this object leads anywhere.</summary>
        public (int Plane, TileCoord Tile)? Landing =>
            ToPlane.HasValue && ToX.HasValue && ToY.HasValue ? (ToPlane.Value, new TileCoord(ToX.Value, ToY.Value)) : ((int, TileCoord)?)null;
    }
}
