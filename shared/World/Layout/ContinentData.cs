using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Rth.Shared.World.Layout
{
    /// <summary>
    /// The continent's rough layout as authored (world/layout/continent.json; docs/layout.md): shapes
    /// and lines in absolute tiles (Tolling's south-west corner is (0, 0); east +x, north +y).
    /// <see cref="ContinentCompiler"/> turns it into a grid of 8 × 8 tile zones, each with a ground
    /// and a power, and writes the zone grid in the <see cref="WorldLayout"/> format the terrain bake
    /// reads. Tooling and presentation: the server only loads it to send players the world map
    /// (<see cref="WorldChart"/>).
    /// </summary>
    public sealed class ContinentData
    {
        public string About = "";
        public int ZoneTiles = 8;
        public int RegionTiles = 96;
        /// <summary>The layout's rectangle [x0, y0, x1, y1], far edges excluded; multiples of <see cref="RegionTiles"/>.</summary>
        public int[] Bounds = new int[4];
        /// <summary>
        /// A tuning knob: every point (and every area's width) is stretched by this factor about
        /// <see cref="Anchor"/> before compiling, so 1.1 makes the world 10% wider and taller and
        /// every distance 10% longer. River, road and pass widths are not scaled.
        /// </summary>
        public float Scale = 1f;
        public float[] Anchor = { 48, 48 };
        /// <summary>Seed for the rough coastlines and the terrain's base noise.</summary>
        public int Seed = 1;
        public float SeaLevel;
        /// <summary>Ground ids for open water and for rivers, bridges and passes.</summary>
        public string SeaGround = "sea";
        public string RiverGround = "river";
        public string BridgeGround = "bridge";
        public string PassGround = "pass";
        /// <summary>Power for land no realm shape claims.</summary>
        public string DefaultPower = "wild";

        public Dictionary<string, GroundDef> Grounds = new Dictionary<string, GroundDef>();
        public Dictionary<string, PowerDef> Powers = new Dictionary<string, PowerDef>();
        /// <summary>Land, as shapes; everything else is sea.</summary>
        public List<ShapeEntry> Land = new List<ShapeEntry>();
        /// <summary>Sea cut back out of the land (sounds, inlets), after <see cref="Land"/>.</summary>
        public List<ShapeEntry> Seas = new List<ShapeEntry>();
        /// <summary>Who holds the land, painted in order (later shapes win: salients go last).</summary>
        public List<ShapeEntry> Realms = new List<ShapeEntry>();
        /// <summary>Ground kinds painted over each power's default ground, in order.</summary>
        public List<ShapeEntry> Areas = new List<ShapeEntry>();
        public SpineDef Spine = new SpineDef();
        public List<RiverDef> Rivers = new List<RiverDef>();
        public List<PlaceDef> Places = new List<PlaceDef>();
        public List<RoadDef> Roads = new List<RoadDef>();
        /// <summary>Notes on a region's role, by "rx,ry" (region column and row; Tolling is "0,0").</summary>
        public Dictionary<string, string> RegionNotes = new Dictionary<string, string>();
        /// <summary>Size tiers: the tiles a settlement of each tier covers, roughly (built area).</summary>
        public Dictionary<string, TierDef> Tiers = new Dictionary<string, TierDef>();
        public LayoutTargets Targets = new LayoutTargets();
        /// <summary>The generated land's first terrain (<see cref="LandHeights"/>); none: flat.</summary>
        public TerrainDef? Terrain;

        public static ContinentData Parse(string json) =>
            JsonConvert.DeserializeObject<ContinentData>(json) ?? throw new FormatException("Empty layout.");
    }

    public sealed class GroundDef
    {
        /// <summary>The zone grid's character for this ground (one per ground).</summary>
        public string Letter = "";
        public string Name = "";
        /// <summary>The terrain bake's ground colour: a name in client/Assets/Resources/Art/palette.json.</summary>
        public string Palette = "Grass";
        /// <summary>The colour on the layout render (#rrggbb); the palette colour when empty.</summary>
        public string Map = "";
        public bool Water;
        public bool Walkable = true;
        /// <summary>The bake's base noise amplitude, in tiles.</summary>
        public float Rough = 1f;
        /// <summary>The macro height, in tiles, away from the coast.</summary>
        public float Height = 2f;
    }

    public sealed class PowerDef
    {
        public string Name = "";
        public string Colour = "#808080";
        /// <summary>Ground for this power's land before any area is painted.</summary>
        public string Ground = "";
        /// <summary>False for frontiers: land beyond the first build's core.</summary>
        public bool Core = true;
        /// <summary>The share of the core's land docs/lore-rework/powers.md gives it, [min, max] (a guide).</summary>
        public float[]? Share;
        /// <summary>Where the render writes the power's name, [x, y].</summary>
        public float[]? Label;
    }

    public sealed class TierDef
    {
        /// <summary>Rough built area in tiles (docs/research/world-density.md's ladder).</summary>
        public int Built;
        /// <summary>Rough width across, in tiles.</summary>
        public int Across;
    }

    /// <summary>
    /// One shape: a polygon (<see cref="Poly"/>, optionally with a rough edge), a band along a line
    /// (<see cref="Line"/> or <see cref="Along"/> the spine, <see cref="HalfWidth"/> each side), or an
    /// ellipse [cx, cy, rx, ry]. <see cref="MinY"/>/<see cref="MaxY"/> clip it.
    /// </summary>
    public class Shape
    {
        public List<float[]>? Poly;
        public List<float[]>? Line;
        /// <summary>"spine": the band follows the spine's line.</summary>
        public string? Along;
        public float HalfWidth;
        public float[]? Ellipse;
        /// <summary>How far, in tiles, a polygon's edges wander from their straight lines.</summary>
        public float Rough;
        public float? MinY;
        public float? MaxY;
    }

    public sealed class ShapeEntry : Shape
    {
        public string Id = "";
        public string Name = "";
        /// <summary>Realms: the power holding the shape.</summary>
        public string? Power;
        /// <summary>Areas: the ground painted.</summary>
        public string? Ground;
        /// <summary>Land: "ship" for land reached only by boat (islands); exempt from the reach check.</summary>
        public string? By;
    }

    /// <summary>How the blockout raises its land (<see cref="LandHeights"/>), in tiles.</summary>
    public sealed class TerrainDef
    {
        /// <summary>The height each ground rises to, by ground id; grounds not listed lie at 0.</summary>
        public Dictionary<string, float> Heights = new Dictionary<string, float>();
        /// <summary>Radius of the smoothing (a box blur run three times): how far hills spread into foothills.</summary>
        public int Smooth = 6;
        /// <summary>Added, sharply, over <see cref="RidgeGrounds"/>, so the crest stands as a ridge.</summary>
        public float Ridge = 2.5f;
        public List<string> RidgeGrounds = new List<string>();
        /// <summary>A pass's highest point, at the middle of its line; it climbs evenly from each end.</summary>
        public float PassSaddle = 3.5f;
        /// <summary>Tiles beyond a pass's half width over which the land returns to its own height.</summary>
        public float PassBlend = 8f;
        /// <summary>Tiles over which land falls to 0 at the edge of a flat built region (Tolling).</summary>
        public float BuiltBlend = 16f;
    }

    public sealed class SpineDef
    {
        public string Name = "";
        /// <summary>The spine's crest line, south to north.</summary>
        public List<float[]> Line = new List<float[]>();
        /// <summary>Where the spine can be crossed: walkable cuts through the crest, also used as roads.</summary>
        public List<PassDef> Passes = new List<PassDef>();
    }

    public sealed class PassDef
    {
        public string Id = "";
        public string Name = "";
        public List<float[]> Line = new List<float[]>();
        public float HalfWidth = 4f;
    }

    public sealed class RiverDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>From the source to the mouth: the river flows along the line's order.</summary>
        public List<float[]> Line = new List<float[]>();
        public float HalfWidth = 3f;
        /// <summary>Where it ends: "sea", "lake:&lt;area id&gt;" or "river:&lt;river id&gt;".</summary>
        public string Into = "sea";
    }

    public sealed class PlaceDef
    {
        public string Id = "";
        /// <summary>The name in docs; p- placeholders are fine here and never reach players.</summary>
        public string Name = "";
        /// <summary>city, town, village, hamlet, or landmark.</summary>
        public string Tier = "village";
        public float[] At = new float[2];
        /// <summary>A power's seat: travel times are measured between these.</summary>
        public bool Capital;
        /// <summary>Rough width across in tiles, when it differs from the tier's.</summary>
        public int? Across;
        public string Note = "";
        /// <summary>Kept off the players' world map (a secret place), whatever its name.</summary>
        public bool Hidden;

        public bool IsSettlement => Tier != "landmark";
    }

    public sealed class RoadDef
    {
        public string Id = "";
        /// <summary>"main", "road" or "ferry" (over water, not drawn on the ground).</summary>
        public string Kind = "road";
        /// <summary>Stops in order: a place id, or a point [x, y] the road bends through.</summary>
        public List<JToken> Line = new List<JToken>();
    }

    public sealed class LayoutTargets
    {
        /// <summary>The core's land in tiles (the first build), [min, max].</summary>
        public float[] CoreTiles = { 1.2e6f, 1.5e6f };
        /// <summary>Route distance from each capital to its nearest other capital, [min, max] tiles.</summary>
        public float[] NearestCapital = { 200, 700 };
        /// <summary>Share of walkable land that may be unreachable from Tolling (off islands) before warning.</summary>
        public float UnreachableShare = 0.02f;
        /// <summary>Towns and villages per 100,000 tiles of core land (docs/areas/targets.json T-LADDER).</summary>
        public float[] Towns = { 0.5f, 1.5f };
        public float[] Villages = { 1.5f, 4f };
        /// <summary>Built share of the core's land (T-LAND-BUILT), estimated from the tiers.</summary>
        public float[] Built = { 0.1f, 0.2f };
        /// <summary>Cruor's size against the next city's (T-CITY-DOMINANCE, by size only).</summary>
        public float[] CityDominance = { 1.5f, 3f };
    }
}
