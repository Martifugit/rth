using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// One region of the world as text data (world/regions/&lt;id&gt;.json): its rectangle, title
    /// and light, the materials it paints with, and its <see cref="Pieces"/>, the things in it.
    /// This is the source of truth for everything in the region that is not hand-made in Unity.
    /// <see cref="RegionCompiler"/> turns it into the server's map files without Unity; Sync World
    /// draws it in the region's scene. See docs/workflow.md.
    /// </summary>
    public sealed class RegionData
    {
        /// <summary>Unique, lower case: the export name (server/data/maps/&lt;id&gt;.rthmap).</summary>
        public string Id = "";
        /// <summary>Shown to players as they walk in.</summary>
        public string Title = "";
        /// <summary>The Unity scene that draws it: "Assets/Scenes/Tolling.unity".</summary>
        public string Scene = "";
        /// <summary>Absolute tile of the south-west corner, and the size: multiples of 8 (whole zones).</summary>
        public int[] Origin = { 0, 0 };
        public int[] Size = { 32, 32 };
        /// <summary>The floor the region itself is on (almost always 0).</summary>
        public int Plane;
        /// <summary>An instanced place (a boss arena): built far off, entered by a door, left off the world map.</summary>
        public bool Instanced;
        /// <summary>A track in the client's Resources/Music, or empty.</summary>
        public string Music = "";
        /// <summary>The region's own light and fog (a crypt, a cave); null for the open world's.</summary>
        public RegionAtmosphere? Atmosphere;
        /// <summary>Flat ground over the whole rectangle in this material; null when the ground is hand-made or a terrain.</summary>
        public string? Ground;
        /// <summary>Materials by name: a colour ("#5C8C38") or a palette name. Pieces name these.</summary>
        public SortedDictionary<string, string> Materials = new SortedDictionary<string, string>(StringComparer.Ordinal);
        /// <summary>Seeds the look of pieces that don't spell out their parts (a tree's crown).</summary>
        public int Seed = 1;
        public List<Piece> Pieces = new List<Piece>();

        [JsonIgnore] public int OriginX => Origin.Length > 0 ? Origin[0] : 0;
        [JsonIgnore] public int OriginY => Origin.Length > 1 ? Origin[1] : 0;
        [JsonIgnore] public int Width => Size.Length > 0 ? Size[0] : 0;
        [JsonIgnore] public int Height => Size.Length > 1 ? Size[1] : 0;

        public Piece? Find(string id) => Pieces.Find(p => p.Id == id);
    }

    /// <summary>The kinds of <see cref="Piece"/>. A new kind is a constant here, a case in <see cref="RegionCompiler"/> and one in Sync World.</summary>
    public static class PieceKind
    {
        /// <summary>Walkable ground over <see cref="Piece.Rect"/> (a square, a field, an upper floor with <see cref="Piece.Plane"/>).</summary>
        public const string Patch = "patch";
        /// <summary>Walkable ground along <see cref="Piece.Line"/>, <see cref="Piece.Width"/> wide.</summary>
        public const string Road = "road";
        /// <summary>Blocks its tiles: <see cref="Piece.Rect"/>, or along <see cref="Piece.Line"/>.</summary>
        public const string Water = "water";
        /// <summary>Blocks the tile edges along a grid line <see cref="Piece.From"/>-<see cref="Piece.To"/>, except <see cref="Piece.Gaps"/>.</summary>
        public const string Wall = "wall";
        /// <summary>A wall that is low and see-through: rails on posts.</summary>
        public const string Fence = "fence";
        /// <summary>Floor, four walls with a door (<see cref="Piece.Door"/> side at <see cref="Piece.DoorAt"/>), and a roof that hides inside.</summary>
        public const string House = "house";
        /// <summary>Blocks <see cref="Piece.Rect"/> with nothing drawn (an invisible barrier).</summary>
        public const string Blocker = "blocker";
        /// <summary>A tree on <see cref="Piece.At"/>: blocks its tile; drawn from its parts or grown from the seed.</summary>
        public const string Tree = "tree";
        /// <summary>A rock on <see cref="Piece.At"/>: blocks its tile.</summary>
        public const string Rock = "rock";
        /// <summary>Anything else on <see cref="Piece.At"/>: drawn from its parts or <see cref="Piece.Model"/>; blocks <see cref="Piece.Footprint"/> if given.</summary>
        public const string Prop = "prop";
        /// <summary>A creature or person (<see cref="Piece.Npc"/>) living at <see cref="Piece.At"/>.</summary>
        public const string Npc = "npc";
        /// <summary>Where new players appear.</summary>
        public const string Spawn = "spawn";

        public static readonly string[] All = { Patch, Road, Water, Wall, Fence, House, Blocker, Tree, Rock, Prop, Npc, Spawn };

        /// <summary>Kinds placed at one tile (<see cref="Piece.At"/>), which can be moved and turned in the Editor.</summary>
        public static bool IsPlaced(string kind) => kind == Tree || kind == Rock || kind == Prop || kind == Npc || kind == Spawn;
    }

    /// <summary>
    /// One thing in a region. Which fields matter depends on <see cref="Kind"/> (see
    /// <see cref="PieceKind"/>). Coordinates are absolute tiles; rectangles are [x0, y0, x1, y1)
    /// with the far edges excluded. Any piece placed at a tile can also be an interactive object
    /// (<see cref="Object"/>).
    /// </summary>
    public sealed class Piece
    {
        /// <summary>Unique in the region and stable: the Editor writes moves back by it.</summary>
        public string Id = "";
        public string Kind = "";
        /// <summary>The object's name in the scene; defaults to the id.</summary>
        public string? Name;
        /// <summary>The floor it is on; null for the region's plane.</summary>
        public int? Plane;

        // ---------------------------------------------------------------- where
        /// <summary>The tile it stands on, for placed kinds. Its parts are drawn around the tile's centre.</summary>
        public int[]? At;
        /// <summary>Turned around the tile's centre, in degrees (clockwise from above).</summary>
        public float? Rot;
        public int[]? Rect;
        /// <summary>A polyline in tiles (x, y pairs; fractions allowed), for roads and rivers.</summary>
        public List<float[]>? Line;
        /// <summary>Half the width of a road or river: tiles whose centre is this close to the line.</summary>
        public float? Width;
        public int[]? From;
        public int[]? To;
        /// <summary>Tiles along a wall or fence left open (gates, doors).</summary>
        public List<int>? Gaps;

        // ---------------------------------------------------------------- what it is
        /// <summary>A material name from the region's <see cref="RegionData.Materials"/>.</summary>
        public string? Material;
        /// <summary>A house's wall, floor and roof materials.</summary>
        public string? Walls;
        public string? Floor;
        public string? Roof;
        /// <summary>A house's door: the side ("N", "E", "S" or "W") and the tile along it.</summary>
        public string? Door;
        public int? DoorAt;
        public int? Storeys;
        /// <summary>A wall's height in metres (a storey by default).</summary>
        public float? Height;
        public float? Scale;
        /// <summary>A tree's kind ("Oak", "Willow"...): its name, and later its model.</summary>
        public string? Variant;
        /// <summary>
        /// Tiles it blocks, relative to <see cref="At"/>: [dx0, dy0, dx1, dy1). Trees, rocks and
        /// objects block their own tile unless this says otherwise; [0, 0, 0, 0] blocks nothing.
        /// </summary>
        public int[]? Footprint;
        /// <summary>A model to draw (a prefab under Assets/), instead of or as well as its parts.</summary>
        public string? Model;
        /// <summary>What it looks like: primitives around the tile's centre.</summary>
        public List<Part>? Parts;

        // ---------------------------------------------------------------- what it does
        /// <summary>Makes it interactive: an id in server/data/content/objects.json.</summary>
        public string? Object;
        /// <summary>For a "climb" object: where it leads, [plane, x, y].</summary>
        public int[]? ClimbTo;
        /// <summary>An id in server/data/content/npcs.json.</summary>
        public string? Npc;
        /// <summary>Tiles it may wander from home; null for the creature's default.</summary>
        public int? Wander;

        [JsonIgnore] public int X => At != null && At.Length > 0 ? At[0] : 0;
        [JsonIgnore] public int Y => At != null && At.Length > 1 ? At[1] : 0;

        /// <summary>Moves every coordinate of the piece by whole tiles.</summary>
        public void Translate(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return;
            if (At != null) At = new[] { At[0] + dx, At[1] + dy };
            if (Rect != null) Rect = new[] { Rect[0] + dx, Rect[1] + dy, Rect[2] + dx, Rect[3] + dy };
            if (From != null) From = new[] { From[0] + dx, From[1] + dy };
            if (To != null) To = new[] { To[0] + dx, To[1] + dy };
            if (Line != null) Line = Line.ConvertAll(p => new[] { p[0] + dx, p[1] + dy });
            if (Gaps != null && From != null && To != null)
                Gaps = Gaps.ConvertAll(g => g + (From[1] == To[1] ? dx : dy));
            if (DoorAt.HasValue && Door != null) DoorAt += Door == "N" || Door == "S" ? dx : dy;
            // ClimbTo is where it leads, not where it is: a moved ladder still lands on the same floor.
        }
    }

    /// <summary>
    /// A primitive in a piece's look: a box or cylinder, its centre relative to the piece's tile
    /// centre (x east, y up, z north, in metres), its size, its turn (Euler degrees) and material.
    /// </summary>
    public sealed class Part
    {
        /// <summary>"box" or "cylinder".</summary>
        public string Shape = "box";
        public string? Name;
        public float[] At = { 0f, 0f, 0f };
        public float[] Size = { 1f, 1f, 1f };
        public float[]? Rot;
        public string Material = "";
    }
}
