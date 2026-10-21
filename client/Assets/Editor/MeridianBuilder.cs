using System.Collections.Generic;
using Rth.MapTools;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Meridian, the capital of Concordia and the Rationalists, where Candora comes down to the sea,
    /// rebuilt at game scale (docs/regions/meridian-redesign.md; the mood and asset notes are in
    /// docs/regions/meridian.md). A city of five tiers climbing a mountain from the sea: the Strand
    /// on the quay, Meridian Square and the Rows (+5 m), the Academy Terraces (+14), the Crag
    /// (+24, with a knoll at +30), and west across the Gorge the dwarves' Undergate Heights (+20),
    /// with the Forum Terrace (+14) on the north-east promontory and the Pass Road going east. The
    /// brass meridian runs straight up the middle of all of it towards the Orrery.
    ///
    /// Phase 1 of the redesign is the ground and the routes (this file). Every tier has two ways up
    /// or more, and every route is a loop: the Harbour Stair, Line Street's stair, the Arcade
    /// Stair, the Span Head stair and the Crag stair; the East Ramp, the pine slope and the Ore
    /// Road's three switchbacks; the low bridge, the Span and the harbour bridge over the Cut. The
    /// ground is a <see cref="Terraces"/> map, which puts a MapWall on every drop. Phase 2 is every
    /// building as a shell you can enter (MeridianBuilder.Buildings.cs).
    /// </summary>
    public static partial class MeridianBuilder
    {
        public const string ScenePath = "Assets/Scenes/Meridian.unity";

        // The region: 192 x 192 tiles, its south-west corner here, far to the south-west where the
        // range meets the sea. Everything below is laid out in local tiles.
        private const int OX = -512, OZ = -256;
        private static readonly Vector2Int Size = new Vector2Int(192, 192);

        // The tiers' heights, in metres.
        private const float Strand = 0f, Square = 5f, Academy = 14f, Heights = 20f, Crag = 24f, Knoll = 30f;

        // The plan, in local tiles.
        private const int Sea = 14;                    // the quay's edge: the sea south of it
        private const int GorgeX0 = 48, GorgeX1 = 60;  // the Cut's gorge, x 48..59
        private const float Line = 96.5f;              // the meridian, the whole length of the city
        private const int LineX0 = 92, LineX1 = 101;   // the meridian's stairs and street, x 92..100
        private const int ParS = 64, ParN = 70;        // the Parallel, z 64..69
        private const int Mountain = 178;              // the mountain's foot: rock north of this row

        private static Mats M;
        private static Terraces Ground;

        [MenuItem("R'th/Dev/Rebuild Meridian Scene", priority = 107)]
        private static void BuildMenu()
        {
            if (WorldKit.ConfirmRebuild(ScenePath)) Build();
        }

        // ------------------------------------------------------------------ coordinates

        private static int X(int lx) => OX + lx;
        private static int Z(int lz) => OZ + lz;
        private static Vector3 P(float lx, float y, float lz) => new Vector3(OX + lx, y, OZ + lz);

        /// <summary>Local tiles [x0, x1) x [z0, z1) as an absolute rectangle.</summary>
        private static RectInt R(int x0, int z0, int x1, int z1) => new RectInt(OX + x0, OZ + z0, x1 - x0, z1 - z0);

        /// <summary>A point on the ground at a local position (on the tile's height).</summary>
        private static Vector3 On(float lx, float lz, float lift = 0f) => Ground.On(OX + lx, OZ + lz, lift);

        private static void Tag(Transform t, string id, string note) => WorldKit.Asset(t, "meridian/" + id, note);

        /// <summary>Every material the city uses, made once per build.</summary>
        private sealed class Mats
        {
            public Material Quay, Paving, Cobble, Grass, Granite, Ashlar, Brass, Rock, Snow, Pine, PineFloor, Sea, DwarfYard, Iron,
                Timber, Bark, Foam, Mist, Verdigris, Shadow, Slate, Glass, LitGlass, BlueTrim, DwarfStone, Bronze, Planks, Canvas, Straw,
                Fire, Lamp, Forge, MoonGlow;
        }

        public static void Build()
        {
            var kit = new WorldKit(seed: 9021);
            M = new Mats
            {
                Quay = kit.Mat("ConcordQuay", new Color(0.48f, 0.5f, 0.52f)),
                Paving = kit.Mat("ConcordPaving", new Color(0.66f, 0.68f, 0.7f)),
                Cobble = kit.Mat("ConcordCobble", new Color(0.5f, 0.52f, 0.55f)),
                Grass = kit.Mat("ConcordGrass", new Color(0.38f, 0.5f, 0.34f)),
                Granite = kit.Mat("ConcordGranite", new Color(0.52f, 0.55f, 0.58f)),
                Ashlar = kit.Mat("ConcordAshlar", new Color(0.8f, 0.81f, 0.8f)),
                Brass = kit.Mat("ConcordBrass", new Color(0.8f, 0.64f, 0.3f)),
                Rock = kit.Mat("ConcordRock", new Color(0.45f, 0.47f, 0.5f)),
                Snow = kit.Mat("ConcordSnow", new Color(0.94f, 0.95f, 0.97f)),
                Pine = kit.Mat("ConcordPine", new Color(0.14f, 0.26f, 0.2f)),
                PineFloor = kit.Mat("ConcordNeedles", new Color(0.4f, 0.38f, 0.3f)),
                Sea = kit.Mat("ConcordSea", new Color(0.14f, 0.3f, 0.42f)),
                DwarfYard = kit.Mat("DwarfYard", new Color(0.36f, 0.35f, 0.36f)),
                Iron = kit.Mat("ConcordIron", new Color(0.2f, 0.21f, 0.23f)),
                Timber = kit.Mat("Timber", new Color(0.4f, 0.28f, 0.18f)), // shared
                Bark = kit.Mat("Bark", new Color(0.36f, 0.25f, 0.15f)), // shared
                Foam = kit.Mat("ConcordFoam", new Color(0.86f, 0.92f, 0.95f)),
                Verdigris = kit.Mat("ConcordVerdigris", new Color(0.32f, 0.62f, 0.54f)),
                Shadow = kit.Mat("ConcordShadow", new Color(0.08f, 0.09f, 0.11f)),
                Slate = kit.Mat("ConcordSlate", new Color(0.3f, 0.33f, 0.38f)),
                Glass = kit.Mat("ConcordGlass", new Color(0.18f, 0.24f, 0.3f)),
                LitGlass = FxKit.Glow("ConcordWindowLit", new Color(1f, 0.86f, 0.62f), 0.5f),
                BlueTrim = kit.Mat("ConcordBlueTrim", new Color(0.38f, 0.46f, 0.56f)),
                DwarfStone = kit.Mat("DwarfStone", new Color(0.26f, 0.25f, 0.27f)),
                Bronze = kit.Mat("Bronze", new Color(0.6f, 0.45f, 0.2f)), // shared
                Planks = kit.Mat("Planks", new Color(0.62f, 0.45f, 0.28f)), // shared
                Canvas = kit.Mat("ConcordCanvas", new Color(0.85f, 0.82f, 0.74f)),
                Straw = kit.Mat("ConcordStraw", new Color(0.82f, 0.7f, 0.42f)),
                Fire = FxKit.Glow("ConcordHearthFire", new Color(1f, 0.5f, 0.15f), 1.5f),
                Lamp = FxKit.Glow("ConcordLamp", new Color(1f, 0.9f, 0.62f), 1.2f),
                Forge = FxKit.Glow("DwarfForgeGlow", new Color(1f, 0.42f, 0.12f), 1.1f),
                MoonGlow = FxKit.Glow("MoonGlow", new Color(0.5f, 1f, 0.62f), 1.4f),
                Mist = FxKit.ParticleMat("ConcordSeaMist", new Color(0.86f, 0.9f, 0.96f, 0.08f), additive: false),
            };

            MapArea area = WorldKit.BeginTerrainRegion("meridian", "Meridian", new Vector2Int(OX, OZ), Size);
            // A clear cold morning on the coast: a white-blue sun, cool ambient, and air so clean the
            // fog only starts far out, so the Orrery on its peak is always in sight.
            area.music = "meridian";
            area.ownAtmosphere = true;
            area.sunIntensity = 1.25f;
            area.sunColor = new Color(0.92f, 0.96f, 1f);
            area.ambientSky = new Color(0.7f, 0.78f, 0.9f);
            area.ambientEquator = new Color(0.58f, 0.63f, 0.7f);
            area.ambientGround = new Color(0.32f, 0.34f, 0.38f);
            area.fog = new Color(0.72f, 0.8f, 0.88f);
            area.fogStart = 70f;
            area.fogEnd = 160f;
            Transform map = area.transform;

            Ground = new Terraces(new Vector2Int(OX, OZ), Size);
            LayTiers();
            LayConnectors();
            Ground.Build(map, M.Granite, M.Ashlar);

            BuildSea(map);
            BuildRock(map);
            BuildGorge(map);
            BuildBridges(map);
            BuildStreets(map);
            BuildOreRoad(map);
            BuildPineSlope(kit, map);
            BuildFunicularAndOrrery(map);
            BuildBuildings(map);

            Transform fx = WorldKit.Group(map, "Atmosphere");
            FxKit.Mist(fx, P(96f, 0.3f, 6f), new Vector2(200f, 16f), M.Mist, Color.white);
            FxKit.Mist(fx, P(54f, 4f, 100f), new Vector2(10f, 80f), M.Mist, Color.white);
            FxKit.Volume(fx, "MeridianVolume", P(96f, 25f, 96f), new Vector3(200f, 80f, 200f), bloom: 0.35f, vignette: 0.12f, saturation: -6f);

            WorldKit.SaveRegion(area, ScenePath);
        }

        // ------------------------------------------------------------------ the ground

        /// <summary>The five tiers, the Forum Terrace, the Pass Road and the Ore Road's bends: the flat ground.</summary>
        private static void LayTiers()
        {
            // The Strand: the quay along the whole shore east of the Cut, the west quay under the
            // cliffs, and the mole that carries the harbour beacon.
            Ground.Flat(R(GorgeX1, Sea, 192, 48), Strand, M.Quay);
            Ground.Flat(R(8, Sea, GorgeX0, 24), Strand, M.Quay);
            Ground.Flat(R(126, 0, 134, Sea), Strand, M.Quay);

            // Meridian Square and the Rows (+5), running on east as the Pass Road's ground.
            Ground.Flat(R(GorgeX1, 48, 150, 96), Square, M.Paving);
            Ground.Flat(R(150, 48, 192, 88), Square, M.Cobble);

            // The Academy Terraces (+14) and, east of them at the same height, the Forum Terrace.
            Ground.Flat(R(GorgeX1, 96, 132, 136), Academy, M.Grass);
            Ground.Flat(R(132, 88, 192, 128), Academy, M.Paving);

            // The Crag (+24) under the mountain, and its knoll (+30) where the beacon stands.
            Ground.Flat(R(GorgeX1, 136, 150, Mountain), Crag, M.Granite);
            Ground.Flat(R(150, 160, 192, Mountain), Crag, M.PineFloor);
            Ground.Flat(R(118, 162, 144, Mountain), Knoll, M.Granite);

            // Undergate Heights (+20), west of the Gorge, and the Span Head that meets the Span on the east side.
            Ground.Flat(R(8, 78, GorgeX0, 140), Heights, M.DwarfYard);
            Ground.Flat(R(GorgeX1, 106, 66, 118), Heights, M.Granite);

            // The Ore Road's two bends: at the low bridge (+5), and under the cliff (+11.5).
            Ground.Flat(R(36, ParS, GorgeX0, 72), Square, M.DwarfYard);
            Ground.Flat(R(8, ParS, 16, 78), 11.5f, M.DwarfYard);
        }

        /// <summary>The stairs, ramps and bridges between the tiers.</summary>
        private static void LayConnectors()
        {
            // The Harbour Stair: Strand to Square on the meridian, with a landing halfway.
            Ground.Stair(R(LineX0, 34, LineX1, 48), WorldKit.Side.North, Strand, Square, M.Ashlar, landingEvery: 6);
            // The East Ramp: Strand to the Pass Road.
            Ground.Ramp(R(181, 30, 189, 48), WorldKit.Side.North, Strand, Square, M.Cobble);
            // Line Street's stair: Square to Academy, the processional climb with two landings.
            Ground.Stair(R(LineX0, 92, LineX1, 116), WorldKit.Side.North, Square, Academy, M.Ashlar, landingEvery: 7);
            // The Arcade Stair: the Rows' east end up to the Forum Terrace.
            Ground.Stair(R(140, 70, 146, 92), WorldKit.Side.North, Square, Academy, M.Ashlar, landingEvery: 10);
            // The Span Head stair: from the Academy's west lawns up to the Span.
            Ground.Stair(R(66, 108, 82, 116), WorldKit.Side.West, Academy, Heights, M.Ashlar);
            // The Crag stair: Academy to the Crag, cut into both.
            Ground.Stair(R(114, 124, 120, 148), WorldKit.Side.North, Academy, Crag, M.Ashlar, landingEvery: 7);
            // The knoll's ramp, from the Crag up to the beacon.
            Ground.Ramp(R(98, 164, 118, 172), WorldKit.Side.East, Crag, Knoll, M.Granite);
            // The pine slope: from the Forum Terrace up through the trees to the Crag's east end.
            Ground.Ramp(R(150, 128, 192, 160), WorldKit.Side.North, Academy, Crag, M.PineFloor);

            // The Ore Road: three switchbacks from the west quay up the cliff to Undergate Heights.
            Ground.Ramp(R(40, 24, GorgeX0, ParS), WorldKit.Side.North, Strand, Square, M.DwarfYard);
            Ground.Ramp(R(16, ParS, 36, ParN), WorldKit.Side.West, Square, 11.5f, M.DwarfYard);
            Ground.Ramp(R(16, 72, 44, 78), WorldKit.Side.East, 11.5f, Heights, M.DwarfYard);

            // Over the Cut: the harbour bridge (quay level), the low bridge on the Parallel (+5) and the Span (+20).
            Ground.Deck(R(GorgeX0, 16, GorgeX1, 22), Strand, M.Quay);
            Ground.Deck(R(GorgeX0, ParS, GorgeX1, ParN), Square, M.Cobble);
            Ground.Deck(R(GorgeX0, 108, GorgeX1, 114), Heights, M.Granite);
        }

        // ------------------------------------------------------------------ scenery round the ground

        private static void BuildSea(Transform map)
        {
            Transform g = WorldKit.Group(map, "The sea");
            WorldKit.FarWater(g, "Sea", new Rect(OX - 200f, OZ - 200f, Size.x + 400f, 200f + Sea), M.Sea);
            foreach (Transform t in g) t.position += Vector3.down * 1.2f;
            WorldKit.Prop("Breakwater", g, P(142f, -0.4f, 3f), new Vector3(24f, 1.6f, 3f), M.Granite);
            Tag(g, "quay_edge", "Granite quay: big squared blocks, iron rings, numbered berths painted on the edge, steps down to the water.");
        }

        /// <summary>Rock where there is no ground: the west cliffs, the cliff the Ore Road climbs, the mountain behind it all.</summary>
        private static void BuildRock(Transform map)
        {
            Transform g = WorldKit.Group(map, "Rock");
            // The west cliffs, rising from the sea to the mountain.
            for (int z = 0; z < Size.y; z += 12)
            {
                float top = Mathf.Lerp(8f, 34f, z / (float)Size.y);
                WorldKit.Prop("West cliff", g, P(3f, (Terraces.Bottom + top) / 2f, z + 6f), new Vector3(10f, top - Terraces.Bottom, 13f), M.Rock, (z * 7) % 12 - 6f);
                WorldKit.Prop("Headland", g, P(-14f, top * 0.6f, z + 6f), new Vector3(24f, top * 1.2f, 14f), M.Rock, (z * 3) % 16 - 8f);
            }
            // The cliff between the west quay and the Heights, in steps that follow the road up it.
            for (int z = 24; z < ParS; z += 4)
                WorldKit.Prop("Cliff", g, P(24f, (Terraces.Bottom + 2f + (z - 24) * 0.25f) / 2f, z + 2f), new Vector3(32f, 2f + (z - 24) * 0.25f - Terraces.Bottom, 4f), M.Rock);
            WorldKit.Prop("Cliff", g, P(26f, (Terraces.Bottom + 12f) / 2f, 71f), new Vector3(20f, 12f - Terraces.Bottom, 2f), M.Rock);
            WorldKit.Prop("Cliff", g, P(46f, (Terraces.Bottom + Heights) / 2f, 75f), new Vector3(4f, Heights - Terraces.Bottom, 6f), M.Rock);
            // The mountain: north of the Heights, and behind everything.
            WorldKit.Prop("Mountain", g, P(28f, 20f, 159f), new Vector3(40f, 48f, 38f), M.Rock, 4f);
            for (int x = -20; x < Size.x + 40; x += 16)
                for (int i = 0; i < 3; i++)
                {
                    float y = 34f + i * 10f, z = Mountain + 6f + i * 12f;
                    WorldKit.Prop("Slope", g, P(x, y / 2f, z), new Vector3(18f, y, 14f), i == 2 ? M.Snow : M.Rock, ((x * 11 + i * 17) % 24) - 12f);
                }
            foreach (Vector3 peak in new[] { new Vector3(20f, 0f, 240f), new Vector3(130f, 0f, 250f), new Vector3(210f, 0f, 230f), new Vector3(-40f, 0f, 200f) })
            {
                WorldKit.Prop("Peak", g, P(peak.x, 45f, peak.z), new Vector3(70f, 90f, 60f), M.Rock, peak.x % 30f);
                WorldKit.Prop("Snowcap", g, P(peak.x, 92f, peak.z), new Vector3(40f, 16f, 34f), M.Snow, peak.x % 30f + 20f);
            }
            Tag(g, "mountain_rock", "Candora's grey granite: fractured cliff modules for the sea cliffs, the road cliff and the mountain behind, snow above 60 m. Scenery, never walked on.");
        }

        /// <summary>The Cut: a mountain stream falling through its gorge to the sea, three falls on the way.</summary>
        private static void BuildGorge(Transform map)
        {
            Transform g = WorldKit.Group(map, "The Gorge");
            float mid = (GorgeX0 + GorgeX1) / 2f;
            // The gorge floor, in steps under each run of the stream.
            (int z0, int z1, float top)[] floor = { (0, 76, -1f), (76, 124, 2.4f), (124, 156, 12.8f), (156, Size.y, 25.8f) };
            foreach ((int z0, int z1, float top) in floor)
                WorldKit.Prop("Gorge floor", g, P(mid, (Terraces.Bottom + top) / 2f, (z0 + z1) / 2f), new Vector3(GorgeX1 - GorgeX0, top - Terraces.Bottom, z1 - z0), M.Rock);
            var path = new List<Vector3>
            {
                P(mid, 32f, 196f), P(mid, 26f, 170f), P(mid, 26f, 156f), P(mid, 13f, 155.6f), P(mid, 13f, 124f), P(mid, 2.5f, 123.6f),
                P(mid, 2.5f, 76f), P(mid, -0.9f, 75.7f), P(mid, -0.9f, 30f), P(mid, -1.1f, 8f),
            };
            Tag(WorldKit.Stream(g, "The Cut", path, 5f, M.Sea, M.Foam), "the_cut",
                "The Cut: a fast, cold mountain stream in a granite gorge, three falls with spray and foam pools; freshwater fishing in the lowest pool.");
            // Boulders in the stream bed.
            foreach (Vector3 b in new[] { new Vector3(51f, 0f, 40f), new Vector3(57f, 0f, 52f), new Vector3(52f, 3f, 92f), new Vector3(56f, 3f, 104f), new Vector3(51f, 13f, 140f) })
                WorldKit.Prop("Boulder", g, P(b.x, b.y, b.z), new Vector3(2.2f, 1.6f, 1.8f), M.Rock, b.z * 7f);
        }

        private static void BuildBridges(Transform map)
        {
            Transform g = WorldKit.Group(map, "Bridges");
            Tag(WorldKit.Viaduct(g, "The Span", R(GorgeX0, 108, GorgeX1, 114), Heights, true, 2.4f, 2, M.Ashlar), "the_span",
                "Hero piece. The Span: a tall two-arched ashlar viaduct 17 m over the Cut's lower pool, a parapet each side, a brass plaque at each end. The best view in the city.");
            Tag(WorldKit.Viaduct(g, "Low bridge", R(GorgeX0, ParS, GorgeX1, ParN), Square, true, -1f, 1, M.Granite), "low_bridge",
                "The low bridge: one granite arch carrying the Parallel over the Cut at the Square's level, iron lamp standards at each end.");
            Tag(WorldKit.Viaduct(g, "Harbour bridge", R(GorgeX0, 16, GorgeX1, 22), Strand, true, -1.2f, 2, M.Granite), "harbour_bridge",
                "The harbour bridge: two low granite arches over the mouth of the Cut, joining the quay to the west quay and the Ore Road.");
        }

        private static void BuildStreets(Transform map)
        {
            Transform g = WorldKit.Group(map, "Streets");
            // The Parallel, east-west across the city, and the meridian's street up the middle.
            Ground.Patch("The Parallel", g, X(GorgeX1), Z(ParS), X(150), Z(ParN), M.Cobble);
            Ground.Patch("Line Street", g, X(LineX0), Z(48), X(LineX1), Z(92), M.Cobble);
            Ground.Patch("Meridian Square", g, X(77), Z(48), X(117), Z(ParS), M.Paving);
            Tag(g, "street_paving", "Granite setts laid in straight courses on the spines (the meridian, the Parallel); the other streets follow the terraces.");

            // The meridian itself: a brass line let into the ground the whole way up, on every step.
            Transform line = WorldKit.Group(map, "The meridian line");
            for (int z = Sea; z < Mountain; z++)
            {
                float? h = Ground.Height(X(96), Z(z));
                if (h == null) continue;
                WorldKit.Prop("Brass line", line, new Vector3(OX + Line, h.Value + 0.012f, OZ + z + 0.5f), new Vector3(0.18f, 0.02f, 1.02f), M.Brass);
                if (z % 8 == 0) WorldKit.Prop("Mark", line, new Vector3(OX + Line, h.Value + 0.014f, OZ + z + 0.5f), new Vector3(0.7f, 0.02f, 0.12f), M.Brass);
            }
            Tag(line, "meridian_line", "The meridian: a brass strip 18 cm wide set in the paving, with a cross-mark every 8 m, dead straight from the quay to the Orrery Gate, up every stair. Modular strip.");

            // Clipped trees in stone tubs along the Parallel: at the Rows' garden gaps, then every 8 m out along the Pass Road.
            var tubs = new List<int> { 69, 80, 111, 122 };
            for (int x = 154; x < 182; x += 8) tubs.Add(x);
            foreach (int x in tubs)
            {
                foreach (int z in new[] { ParS - 1, ParN })
                {
                    Transform t = WorldKit.Group(g, "Tub tree");
                    Vector3 foot = On(x + 0.5f, z + 0.5f);
                    WorldKit.Box("Tub", t, foot + Vector3.up * 0.35f, new Vector3(0.9f, 0.7f, 0.9f), M.Granite).AddComponent<MapBlocker>();
                    WorldKit.Prop("Stem", t, foot + Vector3.up * 1.3f, new Vector3(0.12f, 1.4f, 0.12f), M.Bark);
                    WorldKit.Prop("Clipped crown", t, foot + Vector3.up * 2.4f, new Vector3(1.3f, 1.3f, 1.3f), M.Pine, 45f);
                    Tag(t, "tub_tree", "A clipped evergreen ball on a straight stem, in a square granite tub. Lines the Parallel at 8 m intervals.");
                }
            }
        }

        /// <summary>The Ore Road's cart-line, beside the road all the way down from the Heights.</summary>
        private static void BuildOreRoad(Transform map)
        {
            Transform g = WorldKit.Group(map, "The Ore Road");
            (Vector2 a, Vector2 b)[] legs =
            {
                (new Vector2(46.5f, 24.01f), new Vector2(46.5f, 63.99f)), (new Vector2(35.99f, 69.2f), new Vector2(16.01f, 69.2f)), (new Vector2(16.01f, 76.8f), new Vector2(43.99f, 76.8f)),
            };
            foreach ((Vector2 a, Vector2 b) in legs)
            {
                Transform rails = WorldKit.Rails(g, On(a.x, a.y, 0.05f), On(b.x, b.y, 0.05f), 1.1f, M.Iron, M.Timber);
                Tag(rails, "cart_rails", "Dwarven cart-line: narrow-gauge iron rails on stone sleepers, down the Ore Road to the quay.");
            }
            Tag(g, "ore_road", "The Ore Road: a switchback cart road cut into the cliff, dark dwarven setts, a lamp-post at each bend, ore carts.");
        }

        private static void BuildPineSlope(WorldKit kit, Transform map)
        {
            Transform g = WorldKit.Group(map, "The pine slope");
            for (int i = 0; i < 40; i++)
            {
                float x = kit.Range(152f, 190f), z = kit.Range(130f, 176f);
                if (x > 162f && x < 170f) continue; // the path up the middle
                Transform pine = WorldKit.Group(g, "Pine");
                float h = kit.Range(5f, 8f);
                Vector3 foot = On(x, z);
                WorldKit.Prop("Trunk", pine, foot + Vector3.up * 0.8f, new Vector3(0.3f, 1.6f, 0.3f), M.Bark);
                for (int k = 0; k < 3; k++)
                    WorldKit.Prop("Tier", pine, foot + Vector3.up * (1.5f + k * h / 4f), new Vector3(2.4f - k * 0.7f, h / 3.5f, 2.4f - k * 0.7f), M.Pine, k * 30f);
                Tag(pine, "mountain_pine", "Pale mountain pine, 6-9 m, tiered, on the slope above the Forum Terrace (woodcutting later).");
            }
        }

        // ------------------------------------------------------------------ the funicular and the Orrery

        private static void BuildFunicularAndOrrery(Transform map)
        {
            // The funicular's rails up the Crag's face on the meridian (its stations come with the
            // buildings), and the Orrery on its peak at the end of the line: scenery, its own region later.
            Transform g = WorldKit.Group(map, "The funicular");
            Tag(WorldKit.Rails(g, P(Line, Academy + 0.05f, 128f), P(Line, Crag + 0.05f, 140f), 1.6f, M.Iron, M.Timber), "funicular_rails",
                "Funicular track: two iron rails on timber sleepers on a stone bed, straight up the Crag at about 40 degrees. Modular.");

            Transform orrery = WorldKit.Group(map, "The Orrery (distant)");
            WorldKit.Prop("Peak", orrery, P(Line, 40f, 214f), new Vector3(48f, 80f, 36f), M.Rock, 6f);
            WorldKit.Prop("Peak top", orrery, P(Line, 80f, 216f), new Vector3(40f, 6f, 30f), M.Granite);
            WorldKit.Cylinder("Drum", orrery, P(Line, 88f, 216f), new Vector3(22f, 6f, 22f), M.Ashlar, collider: false);
            WorldKit.Dome(orrery, P(Line, 94f, 216f), 10.5f, 11f, M.Verdigris, M.Brass, 8);
            WorldKit.Prop("Slit", orrery, P(Line, 100f, 206.5f), new Vector3(2f, 9f, 0.4f), M.Shadow).transform.rotation = Quaternion.Euler(-35f, 0f, 0f);
            WorldKit.Beam("Great telescope", orrery, P(Line, 98f, 212f), P(Line, 106f, 202f), 1.6f, 1.6f, M.Brass);
            foreach (int s in new[] { -1, 1 })
            {
                WorldKit.Cylinder("Side drum", orrery, P(Line + s * 16f, 84f, 218f), new Vector3(8f, 4f, 8f), M.Ashlar, collider: false);
                WorldKit.Dome(orrery, P(Line + s * 16f, 88f, 218f), 3.6f, 4f, M.Verdigris, M.Brass, 5);
            }
            FxKit.PointLight(orrery, "Lamps", P(Line, 90f, 204f), new Color(0.85f, 0.92f, 1f), 3f, 20f);
            Tag(orrery, "the_orrery_far", "Hero silhouette. The Orrery on its peak at the head of the meridian: a great drum and a 22 m verdigris dome with an open slit and a huge brass telescope, two lesser domes. Seen from everywhere in Meridian.");
        }
    }
}
