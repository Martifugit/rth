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
        /// <summary>Kinds of stones to scatter over the ground (gravel, chalk chips), painted on their own (<see cref="Ground.GroundStones"/>).</summary>
        public Dictionary<string, StoneDef> Stones = new Dictionary<string, StoneDef>();
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
        /// <summary>Landforms raised or cut into the land: hills, ridges, escarpments, mesas, craters, dunes (docs/world-plan.md).</summary>
        public List<ReliefDef> Relief = new List<ReliefDef>();
        /// <summary>Spots kept free for content still to design: dungeon mouths, ruins, boss arenas (docs/world-plan.md section 6).</summary>
        public List<SiteDef> Sites = new List<SiteDef>();

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
        /// <summary>
        /// How it is painted (docs/world-plan.md section 7): "under" (natural ground, blended: the
        /// default), "soft" (a path: natural ground that fades fully into whatever lies round it,
        /// however different its colour) or "over" (a made surface: paving, planks, drawn crisp).
        /// </summary>
        public string Layer = "under";
        /// <summary>Stones the blockout scatters over it (a gravel path), unevenly: which kind and how thick on the whole.</summary>
        public ScatterDef? Stones;
        /// <summary>Patches of other grounds inside this one, so a large area isn't one colour.</summary>
        public List<MixDef> Mix = new List<MixDef>();
        /// <summary>Drawn along the sea's edge (a beach); none: this ground.</summary>
        public string? Shore;
        /// <summary>Drawn along a lake's or river's edge; none: <see cref="Shore"/>.</summary>
        public string? Bank;
        /// <summary>Drawn where the ground is steep but still walkable (scree, broken rock); none: this ground.</summary>
        public string? Scree;
        /// <summary>The colour of this ground's cliff faces (a palette name); none: the default rock.</summary>
        public string? Cliff;
        /// <summary>Drawn only by the generator (a mix, a shore, a road), never painted by an area or a power.</summary>
        public bool Detail;
        /// <summary>Open ground that settlements farm: fields are laid on it round villages and towns.</summary>
        public bool Farm;
        /// <summary>What it gives way to higher up (mountain pasture to rock): each band's ground above its height, the line ragged.</summary>
        public List<BandDef> Above = new List<BandDef>();
    }

    /// <summary>A kind of stones (<see cref="ContinentData.Stones"/>): its name in the brush's list and its palette colour.</summary>
    public sealed class StoneDef
    {
        public string Name = "";
        public string Palette = "Gravel";
    }

    /// <summary>Stones over a ground (<see cref="GroundDef.Stones"/>): an id in <see cref="ContinentData.Stones"/> and how thickly they lie on the whole (0..1).</summary>
    public sealed class ScatterDef
    {
        public string Kind = "gravel";
        public float Share = 0.25f;
    }

    /// <summary>A ground drawn above a height (<see cref="GroundDef.Above"/>).</summary>
    public sealed class BandDef
    {
        public float Height;
        public string Ground = "";
    }

    /// <summary>A patch of another ground: where slow noise passes its threshold, so about <see cref="Share"/> of the land.</summary>
    public sealed class MixDef
    {
        public string Ground = "";
        public float Share = 0.25f;
        /// <summary>The patches' size, in tiles.</summary>
        public float Scale = 24f;
    }

    /// <summary>
    /// A landform (docs/world-plan.md): a shape and how it raises the land, in tiles.
    /// <list type="bullet">
    /// <item><c>hill</c>: a dome over the shape (an ellipse or polygon), <see cref="Height"/> at its middle;</item>
    /// <item><c>cone</c>: a straight-sided cone, with a <see cref="Crater"/> at its top if given;</item>
    /// <item><c>mesa</c>: a flat top with steep sides <see cref="Edge"/> tiles wide (a negative height: a basin);</item>
    /// <item><c>ridge</c>: a band along its line, highest at the line;</item>
    /// <item><c>range</c>: mountains along its line: a massif, the crest, spurs across it every
    /// <see cref="Scale"/> tiles with valleys between, lesser ridges beside the crest;</item>
    /// <item><c>scarp</c>: a step along its line, the land on its <see cref="Side"/> raised by <see cref="Height"/>
    /// for <see cref="Reach"/> tiles, rising over <see cref="Edge"/> tiles (a cliff when narrow), except at its
    /// <see cref="Ramps"/>, where it climbs gently;</item>
    /// <item><c>crater</c>: a bowl inside a raised rim;</item>
    /// <item><c>dunes</c>: long sand ridges across the shape;</item>
    /// <item><c>tors</c>: knobs of rock scattered over the shape, steep enough to stand out as crags;</item>
    /// <item><c>canyons</c>: a table of rock <see cref="Height"/> high split by branching cracks down to the floor;</item>
    /// <item><c>cliffs</c>: a cliff coast: inside the shape the land keeps its height to the water's edge;</item>
    /// <item><c>terraces</c>: the land inside stepped into flat shelves <see cref="Step"/> high.</item>
    /// </list>
    /// </summary>
    public sealed class ReliefDef : Shape
    {
        public string Id = "";
        public string Name = "";
        public string Kind = "hill";
        public float Height = 2f;
        /// <summary>mesa, scarp, crater: the width of the slope, in tiles.</summary>
        public float Edge = 6f;
        /// <summary>scarp: "left" or "right" of the line's direction is the high side.</summary>
        public string Side = "right";
        /// <summary>scarp: how far from the line the raise reaches before fading out.</summary>
        public float Reach = 120f;
        /// <summary>scarp and mesa: where it can be climbed.</summary>
        public List<RampDef> Ramps = new List<RampDef>();
        /// <summary>cone: the crater's radius at the top, in tiles (0: none), and its depth.</summary>
        public float Crater;
        public float CraterDepth = 2f;
        /// <summary>dunes and tors: the pattern's spacing, in tiles; dunes: the direction they run, in degrees from east.</summary>
        public float Scale = 16f;
        public float Angle;
        /// <summary>terraces: the height of each shelf.</summary>
        public float Step = 1.2f;
        /// <summary>range: how far it spreads to the left and right of its line's direction, in tiles (none: halfWidth both ways).</summary>
        public float[]? Spread;
        public string Note = "";
    }

    /// <summary>A way up a scarp: a point on its line, the ramp's half width along it and its length across.</summary>
    public sealed class RampDef
    {
        public float[] At = new float[2];
        public float HalfWidth = 5f;
        public float Length = 18f;
        public string Name = "";
    }

    /// <summary>A spot kept for content to come (docs/world-plan.md section 6): never on the players' map.</summary>
    public sealed class SiteDef
    {
        public string Id = "";
        /// <summary>dungeon, cave, ruin, unfinished, moved, moonfall, demigod, cell, monument, seal, thin, spoil, boss, lookout, skilling.</summary>
        public string Kind = "ruin";
        public float[] At = new float[2];
        /// <summary>early, mid or late (docs/areas/rules.md, "Stage").</summary>
        public string Stage = "";
        public string Note = "";
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
        /// <summary>The ground its roads are made of, by road kind ("main", "road", "track"): each power builds its own (docs/world-plan.md rule 6).</summary>
        public Dictionary<string, string> Roads = new Dictionary<string, string>();
        /// <summary>The paving of its settlements' squares.</summary>
        public string? Square;
        /// <summary>The worn ground round its settlements and along its roads.</summary>
        public string? Worn;
        /// <summary>The grounds of the fields round its villages and towns; none: no fields.</summary>
        public List<string> Fields = new List<string>();
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
        /// <summary>Peaks over the ridge grounds: up to this much more, in a ridged pattern (sharp crests, deep cols).</summary>
        public float Peaks;
        /// <summary>The peaks' spacing, in tiles.</summary>
        public float PeakScale = 20f;
        /// <summary>Unevenness of raised walkable land, in tiles either way, growing with its height (flat land stays flat).</summary>
        public float Rough;
        /// <summary>Ridge tiles higher than this are drawn as snow (still blocked); none: no snow.</summary>
        public float? SnowAbove;
        /// <summary>A pass's highest point, at the middle of its line; it climbs evenly from each end.</summary>
        public float PassSaddle = 3.5f;
        /// <summary>Tiles beyond a pass's half width over which the land returns to its own height.</summary>
        public float PassBlend = 8f;
        /// <summary>Tiles over which land falls to 0 at the edge of a flat built region (Tolling).</summary>
        public float BuiltBlend = 16f;
        /// <summary>The rolling of open land, in tiles either way, by ground id (grounds not listed: <see cref="RollDefault"/>).</summary>
        public Dictionary<string, float> Roll = new Dictionary<string, float>();
        public float RollDefault = 0.6f;
        /// <summary>The rolling's spacing, in tiles.</summary>
        public float RollScale = 40f;
        /// <summary>Tiles over which land falls to the sea.</summary>
        public float CoastBlend = 10f;
        /// <summary>How far a river's or lake's valley reaches beyond its water, in tiles.</summary>
        public float Valley = 10f;
        /// <summary>How far a bank stands above its water, in tiles.</summary>
        public float Bank = 0.25f;
        /// <summary>Land above this is drawn as snow (any ground); none: only the ridge's snow.</summary>
        public float? SnowLine;
        /// <summary>How far the snow line (both) comes down for every 100 tiles north of Tolling (it rises as far to the south).</summary>
        public float SnowFall;
        /// <summary>Walkable slopes steeper than this (rise across a tile) are drawn as the ground's scree.</summary>
        public float ScreeSlope = 0.55f;
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
        /// <summary>Half the width at its source (it widens to <see cref="HalfWidth"/> at its mouth); none: the same all along.</summary>
        public float? SourceHalfWidth;
        /// <summary>How far, in tiles, it winds either side of its line.</summary>
        public float Meander = 6f;
        /// <summary>
        /// Where it rises: "spring" (a small pool, the default), "tarn" (a mountain lake), or "none"
        /// (it leaves a lake or the layout's water already there). Rivers start as a stream and widen.
        /// </summary>
        public string Source = "spring";
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
        /// <summary>The radius of the ground levelled for it, when it differs from its tier's (a terraced city levels less).</summary>
        public float? Flat;
        /// <summary>New players start here: the blockout puts the player spawn on this tile.</summary>
        public bool Start;

        public bool IsSettlement => Tier != "landmark";
    }

    public sealed class RoadDef
    {
        public string Id = "";
        /// <summary>"main", "road", "track" or "ferry" (over water, not drawn on the ground).</summary>
        public string Kind = "road";
        /// <summary>
        /// Routed over the land between its stops the cheapest way (gentle slopes, round water, over
        /// rivers at narrow points), as roads are made (docs/world-plan.md section 7); false: straight lines.
        /// </summary>
        public bool Route = true;
        /// <summary>Stops in order: a place id, or a point [x, y] the road bends through.</summary>
        public List<JToken> Line = new List<JToken>();
        /// <summary>How far, in tiles, the road drifts either side of its route in easy bends (none: by its kind; 0: as routed).</summary>
        public float? Wind;
        /// <summary>The length, in tiles, of its long bends.</summary>
        public float Wave = 55f;

        /// <summary>The drift a road of this kind gets when it names none: paths wander, highways less.</summary>
        public float WindOrDefault => Wind ?? (Kind == "main" ? 5f : 6f);
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
