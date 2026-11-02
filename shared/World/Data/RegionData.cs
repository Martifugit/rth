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
        /// <summary>
        /// Client only: the region's own post-processing (bloom so glowing things glow, a vignette, a
        /// colour grade), drawn as a local volume over the region (the client's RegionPostFx).
        /// </summary>
        public RegionPostFxDef? PostFx;
        /// <summary>Flat ground over the whole rectangle in this material; null when the ground is hand-made or a terrain.</summary>
        public string? Ground;
        /// <summary>Materials by name: a colour ("#5C8C38") or a palette name. Pieces name these.</summary>
        public SortedDictionary<string, string> Materials = new SortedDictionary<string, string>(StringComparer.Ordinal);
        /// <summary>Seeds the look of pieces that don't spell out their parts (a tree's crown).</summary>
        public int Seed = 1;
        /// <summary>
        /// Made by a generator, by name ("blockout": `tools/world blockout`, docs/layout.md), which
        /// rewrites or deletes the file whenever it runs. Remove it to take the region over by hand.
        /// </summary>
        public string? Generated;
        /// <summary>
        /// The ground's height, corner by corner, over the whole region; null for flat ground. Drawn
        /// by the region's tiles pieces, stood on by everything placed, sculpted in Unity and written
        /// back on saving. Tiles steeper than its cliff step are blocked.
        /// </summary>
        public RegionTerrain? Terrain;
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
        /// <summary>
        /// Ground tile by tile over <see cref="Piece.Rect"/>: <see cref="Piece.Rows"/> of characters
        /// from the north, each one a <see cref="Piece.Legend"/> entry (ground, blocked ground or water).
        /// </summary>
        public const string Tiles = "tiles";
        /// <summary>
        /// A building put together from a kit's pieces (<see cref="Piece.Building"/>) over
        /// <see cref="Piece.Rect"/>: walls along its edges, open at its doorways, a floor and a roof
        /// that hides inside. It stands on the terrain.
        /// </summary>
        public const string Building = "building";

        public static readonly string[] All = { Patch, Road, Water, Wall, Fence, House, Blocker, Tree, Rock, Prop, Npc, Spawn, Tiles, Building };

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
        /// <summary>A light it gives off (client only): a candle heap, a brazier.</summary>
        public PieceLight? Light;
        /// <summary>Drawn by nothing: only its markers (a hidden trigger's object, a creature with no stand-in).</summary>
        public bool? Hidden;
        /// <summary>A model to draw (a prefab under Assets/), instead of or as well as its parts; for a fence, one span centred on its tile.</summary>
        public string? Model;
        /// <summary>
        /// A fence's post (a model standing on a grid corner: a stake, a buck, a wall's pier), stood
        /// once on each corner, shared with any other fence meeting there, so corners close.
        /// </summary>
        public string? Post;
        /// <summary>Where a fence's posts stand: "every" corner of its line (the default), or only the "ends" of its runs (a wall).</summary>
        public string? Posts;
        /// <summary>What it looks like: primitives around the tile's centre.</summary>
        public List<Part>? Parts;
        /// <summary>A tiles piece's characters by what they are; a space is no ground at all.</summary>
        public SortedDictionary<string, TileKind>? Legend;
        /// <summary>A tiles piece's ground: one string a row from the north, a character a tile from the west.</summary>
        public List<string>? Rows;
        /// <summary>
        /// A tiles piece painted the RuneScape way (<see cref="Ground.GroundPainter"/>): underlays
        /// blended, overlays and water crisp with diagonal half tiles. Otherwise each kind is a flat material.
        /// </summary>
        public bool? Paint;
        /// <summary>How many tiles of the neighbours' ground <see cref="Surround"/> holds all round, for blending across borders.</summary>
        public int? Margin;
        /// <summary>
        /// The neighbours' tiles round a painted tiles piece, only for painting (they aren't this region's
        /// ground): <see cref="Margin"/> full rows along the north (the rectangle grown by the margin each
        /// side), then a row of the west and east parts (2 × margin characters) for each of
        /// <see cref="Rows"/>, then the margin's rows along the south.
        /// </summary>
        public List<string>? Surround;
        /// <summary>
        /// A painted tiles piece's stones (<see cref="Ground.GroundStones"/>): small flat chips lying
        /// over the ground, painted on their own wherever they belong (along a path, round a rock),
        /// whatever ground is under them. One string a row from the north like <see cref="Rows"/>,
        /// a character a tile from <see cref="StoneLegend"/>; a space is none. Null: no stones.
        /// </summary>
        public List<string>? Stones;
        /// <summary>What each character of <see cref="Stones"/> is: the stones' colour and how thickly they lie.</summary>
        public SortedDictionary<string, StoneKind>? StoneLegend;
        /// <summary>A building piece's kit, faces and roof.</summary>
        public BuildingSpec? Building;

        // ---------------------------------------------------------------- what it does
        /// <summary>Makes it interactive: an id in server/data/content/objects.json.</summary>
        public string? Object;
        /// <summary>For a "climb" object: where it leads, [plane, x, y].</summary>
        public int[]? ClimbTo;
        /// <summary>An id in server/data/content/npcs.json.</summary>
        public string? Npc;
        /// <summary>Tiles it may wander from home; null for the creature's default.</summary>
        public int? Wander;
        /// <summary>For a creature: tiles it walks between, [x, y] each (absolute, like <see cref="At"/>).</summary>
        public List<int[]>? Patrol;
        /// <summary>For a patrol: "loop" (default) or "bounce".</summary>
        public string? PatrolMode;

        [JsonIgnore] public int X => At != null && At.Length > 0 ? At[0] : 0;
        [JsonIgnore] public int Y => At != null && At.Length > 1 ? At[1] : 0;

        /// <summary>Moves every coordinate of the piece by whole tiles.</summary>
        public void Translate(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return;
            if (At != null) At = new[] { At[0] + dx, At[1] + dy };
            if (Patrol != null) Patrol = Patrol.ConvertAll(p => new[] { p[0] + dx, p[1] + dy });
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
    /// A region's ground heights as text (<see cref="RegionData.Terrain"/>): <see cref="Rows"/> from
    /// the north, one per row of tile corners (the region's height + 1), each holding that row's
    /// corners from the west (its width + 1) in hundredths of a tile, separated by spaces;
    /// <c>h*n</c> repeats a height n times. <see cref="TerrainText"/> reads and writes it.
    /// </summary>
    public sealed class RegionTerrain
    {
        /// <summary>A tile rising more than this many tiles across is a cliff, and blocked.</summary>
        public float CliffStep = 1f;
        public List<string> Rows = new List<string>();
    }

    /// <summary>One character of a tiles piece's <see cref="Piece.Legend"/>.</summary>
    public sealed class TileKind
    {
        /// <summary>A material name from the region's <see cref="RegionData.Materials"/>.</summary>
        public string Material = "";
        /// <summary>Not walkable (rock, the edge of the built world). Water always blocks.</summary>
        public bool? Blocks;
        /// <summary>Drawn as water, and blocks.</summary>
        public bool? Water;
        /// <summary>Where its top is drawn, in metres (a rock shelf above, water below); the server is flat.</summary>
        public float? Height;
        /// <summary>A material name for this kind's cliff faces (its tiles too steep to walk); none: the ground's rock.</summary>
        public string? Cliff;
        /// <summary>
        /// On a painted piece (<see cref="Piece.Paint"/>): "over" for a made surface (paving, planks),
        /// drawn crisp in its own colour; "soft" for a path, faded fully into the ground round it;
        /// otherwise natural ground, blended with its neighbours.
        /// </summary>
        public string? Layer;
        /// <summary>Land beyond the first build: drawn hazed (on a painted piece), and blocked.</summary>
        public bool? Beyond;

        [JsonIgnore] public bool IsOverlay => Layer == "over";

        [JsonIgnore] public bool IsSoft => Layer == "soft";

        [JsonIgnore] public bool Walkable => Blocks != true && Water != true;
    }

    /// <summary>One character of a tiles piece's <see cref="Piece.StoneLegend"/>: stones of one colour, this thick.</summary>
    public sealed class StoneKind
    {
        /// <summary>A material name from the region's <see cref="RegionData.Materials"/>.</summary>
        public string Material = "";
        /// <summary>How thickly they lie: about <see cref="Ground.GroundStones.PerTile"/> times this a tile (0..1).</summary>
        public float Share;
    }

    /// <summary>
    /// A primitive in a piece's look: a box or cylinder, its centre relative to the piece's tile
    /// centre (x east, y up, z north, in metres), its size, its turn (Euler degrees) and material.
    /// </summary>
    /// <summary>A region's post-processing (<see cref="RegionData.PostFx"/>). Zero (or white) leaves an effect off.</summary>
    public sealed class RegionPostFxDef
    {
        /// <summary>Bloom intensity (URP's), e.g. 1.5; 0: none.</summary>
        public float Bloom;
        /// <summary>How bright a pixel must be to bloom (linear; soul and candle flames are bright).</summary>
        public float BloomThreshold = 0.9f;
        public string? BloomTint;
        /// <summary>Vignette intensity, 0..1.</summary>
        public float Vignette;
        public string? VignetteColor;
        /// <summary>A colour filter over everything (#RRGGBB): the region's tone.</summary>
        public string? Tint;
        /// <summary>-100..100.</summary>
        public float Saturation;
        /// <summary>-100..100.</summary>
        public float Contrast;
        /// <summary>Post exposure in stops.</summary>
        public float Exposure;
    }

    /// <summary>A point light carried by a placed piece (a candle heap, a brazier): drawn by the client only.</summary>
    public sealed class PieceLight
    {
        public string Color = "#ffffff";
        public float Intensity = 1f;
        /// <summary>Reach in tiles.</summary>
        public float Range = 5f;
        /// <summary>Height above the piece's foot, in tiles.</summary>
        public float Height = 1f;
        /// <summary>A slow flicker (0: steady; 0.15: a candle's).</summary>
        public float Flicker;
    }

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
