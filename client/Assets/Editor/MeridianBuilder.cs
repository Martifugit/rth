using System.Collections.Generic;
using Rth.MapTools;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Meridian, the capital of Concordia and the Rationalists (docs/regions/meridian.md), where
    /// Candora comes down to the sea. A clear, cold morning: grey granite and pale ashlar, slate
    /// roofs, blue-grey trim, brass instruments and verdigris copper domes. A city laid out on a
    /// grid with no wall (its neutrality is its wall), and the meridian itself, a brass line, run
    /// straight through it from the harbour to the mountain foot. On that line: the gnomon in
    /// Meridian Square, the Concord where the agreement sits, and the funicular up to the Orrery on
    /// the crag above, where moonfalls are forecast. Around it: the Academy with a moon fragment in
    /// its court, the Hall of Measures, the Almanac Office, the Butts where marksmen shoot, the toll
    /// on the pass road, and in the north-west corner the dwarves' Undergate and the survey's deep
    /// bore, glowing faintly green. Every placeholder is tagged with the asset it stands for
    /// (WorldKit.Asset); saving writes the list to docs/regions/meridian-assets.md.
    /// </summary>
    public static class MeridianBuilder
    {
        public const string ScenePath = "Assets/Scenes/Meridian.unity";

        // The region: 144 x 144 tiles, its south-west corner here, far to the south-west where the
        // range meets the sea. Everything below is laid out in local tiles.
        private const int OX = -512, OZ = -256;
        private static readonly Vector2Int Size = new Vector2Int(144, 144);

        // The plan, in local tiles.
        private const int Shore = 20;           // the quay's edge: the sea south of it
        private const int Cliff = 12;           // the cliffs west of this column
        private const int Foot = 118;           // the mountain's foot: rock north of this row
        private const int ParS = 60, ParN = 64; // the Parallel, the east-west avenue, z 60..63
        private const int PassX0 = 120, PassX1 = 124; // the pass road, x 120..123
        private const float Line = 72.5f;       // the meridian: x = 72.5, the whole length of the city
        private static readonly RectInt Square = new RectInt(56, 48, 33, 29); // Meridian Square, x 56..88, z 48..76

        private static Mats M;

        [MenuItem("R'th/Dev/Rebuild Meridian Scene", priority = 107)]
        private static void BuildMenu()
        {
            if (WorldKit.ConfirmRebuild(ScenePath)) Build();
        }

        // ------------------------------------------------------------------ coordinates

        private static int X(int lx) => OX + lx;
        private static int Z(int lz) => OZ + lz;
        private static Vector3 P(float lx, float y, float lz) => new Vector3(OX + lx, y, OZ + lz);
        private static Vector2Int T(int lx, int lz) => new Vector2Int(OX + lx, OZ + lz);

        private static Transform Building(Transform parent, string name, int x0, int z0, int x1, int z1, float eave, WorldKit.Facade f,
            WorldKit.RoofKind roof = WorldKit.RoofKind.Gable) =>
            WorldKit.Building(parent, name, X(x0), Z(z0), X(x1), Z(z1), eave, f, roof);

        private static Transform Hall(Transform parent, string name, int x0, int z0, int x1, int z1, float height, WorldKit.Facade f, Material floor,
            params WorldKit.Door[] doors) =>
            WorldKit.Hall(parent, name, X(x0), Z(z0), X(x1), Z(z1), height, f, floor, doors);

        private static void Tag(Transform t, string id, string note) => WorldKit.Asset(t, "meridian/" + id, note);

        /// <summary>Every material the city uses, made once per build.</summary>
        private sealed class Mats
        {
            public Material Paving, Cobble, Grass, Granite, Ashlar, BlueGrey, Slate, Brass, Verdigris, Rock, Snow, Pine, Sea, Window, WindowLit,
                Lamp, Fragment, DwarfStone, Bronze, Iron, Timber, Canvas, Hull, Planks, Goods, Bark, Shadow, Coals, Flame, Ember, Mist, Mote, Beam,
                Target, Spoil;

            public WorldKit.Facade Row, Row2, Civic, Store, Dwarf;
        }

        public static void Build()
        {
            var kit = new WorldKit(seed: 9021);
            M = new Mats
            {
                Paving = kit.Mat("ConcordPaving", new Color(0.66f, 0.68f, 0.7f)),
                Cobble = kit.Mat("ConcordCobble", new Color(0.5f, 0.52f, 0.55f)),
                Grass = kit.Mat("ConcordGrass", new Color(0.38f, 0.5f, 0.34f)),
                Granite = kit.Mat("ConcordGranite", new Color(0.52f, 0.55f, 0.58f)),
                Ashlar = kit.Mat("ConcordAshlar", new Color(0.8f, 0.81f, 0.8f)),
                BlueGrey = kit.Mat("ConcordBlueGrey", new Color(0.36f, 0.44f, 0.54f)),
                Slate = kit.Mat("ConcordSlate", new Color(0.27f, 0.3f, 0.35f)),
                Brass = kit.Mat("ConcordBrass", new Color(0.8f, 0.64f, 0.3f)),
                Verdigris = kit.Mat("ConcordVerdigris", new Color(0.32f, 0.62f, 0.54f)),
                Rock = kit.Mat("ConcordRock", new Color(0.45f, 0.47f, 0.5f)),
                Snow = kit.Mat("ConcordSnow", new Color(0.94f, 0.95f, 0.97f)),
                Pine = kit.Mat("ConcordPine", new Color(0.14f, 0.26f, 0.2f)),
                Sea = kit.Mat("ConcordSea", new Color(0.14f, 0.3f, 0.42f)),
                Window = kit.Mat("ConcordWindow", new Color(0.12f, 0.14f, 0.18f)),
                DwarfStone = kit.Mat("DwarfGranite", new Color(0.3f, 0.3f, 0.32f)),
                Bronze = kit.Mat("Bronze", new Color(0.6f, 0.45f, 0.2f)), // shared
                Iron = kit.Mat("ConcordIron", new Color(0.2f, 0.21f, 0.23f)),
                Timber = kit.Mat("Timber", new Color(0.4f, 0.28f, 0.18f)), // shared
                Canvas = kit.Mat("ConcordCanvas", new Color(0.85f, 0.86f, 0.84f)),
                Hull = kit.Mat("ConcordHull", new Color(0.28f, 0.24f, 0.2f)),
                Planks = kit.Mat("ConcordPlanks", new Color(0.5f, 0.44f, 0.36f)),
                Goods = kit.Mat("ConcordGoods", new Color(0.58f, 0.5f, 0.4f)),
                Bark = kit.Mat("Bark", new Color(0.36f, 0.25f, 0.15f)), // shared
                Shadow = kit.Mat("ConcordShadow", new Color(0.08f, 0.09f, 0.11f)),
                Target = kit.Mat("ConcordTarget", new Color(0.82f, 0.78f, 0.66f)),
                WindowLit = FxKit.Glow("ConcordWindowLit", new Color(0.85f, 0.92f, 1f), 0.4f),
                Lamp = FxKit.Glow("ConcordLamp", new Color(0.9f, 0.95f, 1f), 0.9f),
                Fragment = FxKit.Glow("MoonFragment", new Color(0.35f, 1f, 0.75f), 1.6f),
                Spoil = FxKit.Glow("BoreSpoil", new Color(0.25f, 0.6f, 0.45f), 0.5f),
                Coals = FxKit.Glow("BrazierCoals", new Color(1f, 0.35f, 0.08f), 0.9f), // shared
                Flame = FxKit.ParticleMat("FxFlame", new Color(1f, 0.55f, 0.22f, 0.6f), additive: true), // shared
                Ember = FxKit.ParticleMat("FxEmber", new Color(1f, 0.55f, 0.2f, 1f), additive: true), // shared
                Mist = FxKit.ParticleMat("ConcordSeaMist", new Color(0.86f, 0.9f, 0.96f, 0.08f), additive: false),
                Mote = FxKit.ParticleMat("FxMote", new Color(0.8f, 0.85f, 1f, 0.6f), additive: true), // shared
                Beam = FxKit.ParticleMat("ConcordLightBeam", new Color(0.9f, 0.95f, 1f, 0.1f), additive: true),
            };
            M.Row = new WorldKit.Facade { Walls = M.Ashlar, Roof = M.Slate, Trim = M.BlueGrey, Windows = M.Window, Gable = M.Ashlar, Pitch = 36f };
            M.Row2 = new WorldKit.Facade { Walls = M.Granite, Roof = M.Slate, Trim = M.Ashlar, Windows = M.Window, Gable = M.Granite, Pitch = 36f };
            M.Civic = new WorldKit.Facade { Walls = M.Ashlar, Roof = M.Slate, Trim = M.Brass, Windows = M.WindowLit, Gable = M.Ashlar, Pitch = 26f };
            M.Store = new WorldKit.Facade { Walls = M.Granite, Roof = M.Slate, Trim = M.Timber, Windows = M.Window, Gable = M.Granite, Pitch = 28f };
            M.Dwarf = new WorldKit.Facade { Walls = M.DwarfStone, Roof = M.Granite, Trim = M.Bronze, Windows = M.WindowLit };

            MapArea area = WorldKit.BeginRegion("meridian", "Meridian", new Vector2Int(OX, OZ), Size, M.Paving);
            // A clear cold morning on the coast: a white-blue sun, cool ambient, and air so clean the
            // fog only starts far out, so the Orrery on its crag is always in sight.
            area.music = "meridian";
            area.ownAtmosphere = true;
            area.sunIntensity = 1.25f;
            area.sunColor = new Color(0.92f, 0.96f, 1f);
            area.ambientSky = new Color(0.7f, 0.78f, 0.9f);
            area.ambientEquator = new Color(0.58f, 0.63f, 0.7f);
            area.ambientGround = new Color(0.32f, 0.34f, 0.38f);
            area.fog = new Color(0.72f, 0.8f, 0.88f);
            area.fogStart = 60f;
            area.fogEnd = 140f;
            Transform map = area.transform;

            BuildSeaAndCliffs(map);
            BuildMountain(kit, map);
            BuildStreets(kit, map);
            BuildSquare(map);
            BuildConcord(map);
            BuildFunicularAndOrrery(map);
            BuildAcademy(map);
            BuildMeasuresAndAlmanac(map);
            BuildHarbour(kit, map);
            BuildButts(map);
            BuildPass(map);
            BuildUndergate(kit, map);
            BuildTown(kit, map);
            BuildPeople(map);

            Transform fx = WorldKit.Group(map, "Atmosphere");
            FxKit.Mist(fx, P(72f, 0.3f, 10f), new Vector2(150f, 20f), M.Mist, Color.white);
            FxKit.Volume(fx, "MeridianVolume", P(72f, 20f, 72f), new Vector3(150f, 60f, 150f), bloom: 0.35f, vignette: 0.12f, saturation: -6f);

            WorldKit.SaveRegion(area, ScenePath);
        }

        // ------------------------------------------------------------------ the sea and the cliffs

        private static void BuildSeaAndCliffs(Transform map)
        {
            // The sea to the south, inside a breakwater; the range's last cliffs to the west.
            var sea = new HashSet<Vector2Int>();
            for (int z = 0; z < Shore; z++)
                for (int x = Cliff; x < Size.x; x++)
                {
                    bool mole = (x >= 100 && x < 103 && z >= 4) || (z >= 4 && z < 7 && x >= 86 && x < 103);
                    if (!mole) sea.Add(T(x, z));
                }
            WorldKit.Water(map, "The sea", sea, M.Sea);
            WorldKit.FarWater(map, "The sea beyond", new Rect(OX - 160f, OZ - 160f, Size.x + 320f, 160f), M.Sea);
            WorldKit.FarWater(map, "The sea beyond, east", new Rect(OX + Size.x, OZ, 160f, Shore), M.Sea);
            WorldKit.FarWater(map, "The sea beyond, west", new Rect(OX - 160f, OZ, 160f, 60f), M.Sea);

            Transform cliffs = WorldKit.Group(map, "West cliffs");
            WorldKit.BlockTiles(cliffs, "Footprint", X(0), Z(0), X(Cliff), Z(Size.y));
            for (int z = 0; z < Size.y; z += 12)
                for (int i = 0; i < 3; i++)
                    WorldKit.Prop("Cliff", cliffs, P(5f - i * 2f, 6f + i * 7f, z + 6f), new Vector3(12f - i * 2f, 12f + i * 4f, 13f), M.Rock, (z * 7 + i * 13) % 20 - 10f);
            for (int z = -10; z < Size.y + 20; z += 24)
                WorldKit.Prop("Headland", cliffs, P(-14f, 8f + (z % 48 == 0 ? 6f : 0f), z + 12f), new Vector3(22f, 18f + (z % 48 == 0 ? 10f : 0f), 26f), M.Rock, (z * 3) % 16 - 8f);
            Tag(cliffs, "sea_cliff", "Grey granite sea cliffs where the range drops into the sea: stepped, fractured faces 15-30 m, gulls' ledges. Modular.");
        }

        // ------------------------------------------------------------------ the mountain

        private static void BuildMountain(WorldKit kit, Transform map)
        {
            // Candora's last slope behind the city: rock from the mountain's foot up into the snow,
            // pines in the gullies. Only the pass road and the Undergate go into it.
            Transform g = WorldKit.Group(map, "The mountain");
            WorldKit.BlockTiles(g, "Foot", X(Cliff), Z(Foot), X(PassX0), Z(Size.y));
            WorldKit.BlockTiles(g, "Foot", X(PassX1), Z(Foot), X(Size.x), Z(Size.y));
            for (int x = Cliff; x < Size.x + 40; x += 14)
                for (int i = 0; i < 4; i++)
                {
                    if (i == 0 && x + 7 > PassX0 - 2 && x - 7 < PassX1 + 2) continue; // leave the pass road's cutting open
                    float y = 4f + i * 9f, z = Foot + 6f + i * 14f;
                    WorldKit.Prop("Slope", g, P(x, y, z), new Vector3(16f, 8f + i * 10f, 14f + i * 4f), i >= 3 ? M.Snow : M.Rock, ((x * 11 + i * 17) % 24) - 12f);
                }
            foreach (Vector3 peak in new[] { new Vector3(20f, 0f, 190f), new Vector3(110f, 0f, 200f), new Vector3(170f, 0f, 180f), new Vector3(-30f, 0f, 150f) })
            {
                WorldKit.Prop("Peak", g, P(peak.x, 30f, peak.z), new Vector3(60f, 60f, 50f), M.Rock, peak.x % 30f);
                WorldKit.Prop("Snowcap", g, P(peak.x, 62f, peak.z), new Vector3(34f, 14f, 30f), M.Snow, peak.x % 30f + 20f);
            }
            // Pines on the lower slopes.
            for (int i = 0; i < 26; i++)
            {
                float x = kit.Range(Cliff + 2f, Size.x - 2f), z = kit.Range(Foot + 2f, Foot + 22f);
                if (x > PassX0 - 3 && x < PassX1 + 3) continue;
                Transform pine = WorldKit.Group(g, "Pine");
                float h = kit.Range(5f, 8f), y = (z - Foot) * 0.6f;
                WorldKit.Prop("Trunk", pine, P(x, y + 0.8f, z), new Vector3(0.3f, 1.6f, 0.3f), M.Bark);
                for (int k = 0; k < 3; k++)
                    WorldKit.Prop("Tier", pine, P(x, y + 1.5f + k * h / 4f, z), new Vector3(2.4f - k * 0.7f, h / 3.5f, 2.4f - k * 0.7f), M.Pine, k * 30f);
                Tag(pine, "mountain_pine", "Dark mountain pine, 6-9 m, tiered; scenery on the slopes (not reachable).");
            }
            Tag(g, "mountain_slope", "Candora's slope behind the city: grey rock steps rising into snow. Backdrop modules, never walked on.");
        }

        // ------------------------------------------------------------------ streets and the meridian line

        private static void BuildStreets(WorldKit kit, Transform map)
        {
            Transform g = WorldKit.Group(map, "Streets");
            WorldKit.Patch("The Parallel", g, X(Cliff), Z(ParS), X(Size.x), Z(ParN), M.Cobble, 0.006f);
            WorldKit.Patch("Line Street", g, X(70), Z(Shore), X(76), Z(Foot), M.Cobble, 0.005f);
            WorldKit.Patch("Pass road", g, X(PassX0), Z(ParN), X(PassX1), Z(Size.y), M.Cobble, 0.006f);
            Tag(g, "street_paving", "Granite setts laid in straight courses; kerbs of dressed ashlar. Every street is straight.");
            // The meridian itself: a brass line let into the paving the whole length of the city.
            Transform line = WorldKit.Group(map, "The meridian line");
            WorldKit.Prop("Brass line", line, P(Line, 0.02f, (Shore + Foot) / 2f), new Vector3(0.18f, 0.02f, Foot - Shore), M.Brass);
            for (int z = Shore + 2; z < Foot; z += 8)
                WorldKit.Prop("Mark", line, P(Line, 0.021f, z), new Vector3(0.7f, 0.02f, 0.12f), M.Brass);
            Tag(line, "meridian_line", "The meridian: a brass strip 18 cm wide set in the paving, with a cross-mark every 8 m, running dead straight from the harbour to the mountain. Modular strip.");
            // Clipped trees in stone tubs along the Parallel.
            for (int x = Cliff + 4; x < Size.x - 2; x += 8)
            {
                if (x > Square.x - 2 && x < Square.xMax + 1) continue;
                foreach (int z in new[] { ParS - 1, ParN })
                {
                    Transform t = WorldKit.Group(g, "Tub tree");
                    WorldKit.Box("Tub", t, P(x + 0.5f, 0.35f, z + 0.5f), new Vector3(0.9f, 0.7f, 0.9f), M.Granite).AddComponent<MapBlocker>();
                    WorldKit.Prop("Stem", t, P(x + 0.5f, 1.3f, z + 0.5f), new Vector3(0.12f, 1.4f, 0.12f), M.Bark);
                    WorldKit.Prop("Clipped crown", t, P(x + 0.5f, 2.4f, z + 0.5f), new Vector3(1.3f, 1.3f, 1.3f), M.Pine, 45f);
                    Tag(t, "tub_tree", "A clipped evergreen ball on a straight stem, in a square granite tub. Lines the Parallel at 8 m intervals.");
                }
            }
        }

        // ------------------------------------------------------------------ Meridian Square

        private static void BuildSquare(Transform map)
        {
            // The square the meridian crosses: a sundial the size of a square. The gnomon's noon
            // shadow falls along the brass line; hour lines fan out across the paving.
            Transform g = WorldKit.Group(map, "Meridian Square");
            WorldKit.Patch("Paving", g, X(Square.x), Z(Square.y), X(Square.xMax), Z(Square.yMax), M.Paving, 0.008f);
            Vector3 foot = P(Line, 0f, 68f);
            Transform gnomon = WorldKit.Group(g, "The gnomon");
            WorldKit.Box("Plinth", gnomon, foot + Vector3.up * 0.5f, new Vector3(1.9f, 1f, 1.9f), M.Granite).AddComponent<MapBlocker>();
            WorldKit.Prop("Shaft", gnomon, foot + Vector3.up * 7f, new Vector3(1.1f, 12f, 1.1f), M.Ashlar);
            WorldKit.Prop("Tip", gnomon, foot + Vector3.up * 13.4f, new Vector3(0.75f, 0.75f, 0.75f), M.Brass, 45f).transform.rotation = Quaternion.Euler(35f, 45f, 0f);
            WorldKit.Prop("Noon shadow", gnomon, P(Line, 0.03f, 72.5f), new Vector3(1f, 0.01f, 8f), M.Shadow);
            for (int h = -5; h <= 5; h++)
            {
                if (h == 0) continue;
                float a = h * 15f;
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                WorldKit.Prop("Hour line", gnomon, foot + dir * 7f + Vector3.up * 0.025f, new Vector3(0.12f, 0.01f, 9f), M.Brass, a);
                WorldKit.Prop("Hour stone", gnomon, foot + dir * 11.8f + Vector3.up * 0.1f, new Vector3(0.6f, 0.2f, 0.4f), M.Granite, a);
            }
            Transform plaque = WorldKit.Station(gnomon, "Gnomon plaque", "meridian_gnomon", X(72), Z(66));
            WorldKit.Box("Plaque", plaque, P(72.5f, 0.45f, 66.6f), new Vector3(0.9f, 0.9f, 0.3f), M.Brass);
            Tag(gnomon, "gnomon", "Hero piece. The gnomon: a 13 m ashlar obelisk with a brass tip on a granite plinth, the brass meridian under it, hour lines fanning across the square to granite hour-stones.");

            // The Little Orrery: an armillary sphere on a plinth, two moons in it, one cracked.
            Transform orrery = WorldKit.Group(g, "The Little Orrery");
            WorldKit.Box("Plinth", orrery, P(62.5f, 0.6f, 54.5f), new Vector3(2.9f, 1.2f, 2.9f), M.Granite).AddComponent<MapBlocker>();
            WorldKit.Armillary(orrery, P(62.5f, 1.2f, 54.5f), 2.2f, M.Brass, M.Granite, M.Brass, M.Verdigris);
            Tag(orrery, "little_orrery", "The Little Orrery: a 6 m brass armillary sphere on a granite plinth, the world at its heart and two moons on arms, the smaller one green and cracked.");
            // Benches and lamps round the edge, a stone fountain on the far side.
            foreach (int x in new[] { 60, 66, 79, 85 })
                foreach (int z in new[] { Square.y + 1, Square.yMax - 2 })
                {
                    Transform bench = WorldKit.Group(g, "Bench");
                    WorldKit.BlockTiles(bench, "Footprint", X(x), Z(z), X(x + 2), Z(z + 1));
                    WorldKit.Prop("Seat", bench, P(x + 1f, 0.45f, z + 0.5f), new Vector3(1.9f, 0.12f, 0.5f), M.Granite);
                    Tag(bench, "stone_bench", "Plain granite bench, 2 m.");
                }
            Transform fountain = WorldKit.Group(g, "Fountain");
            WorldKit.BlockTiles(fountain, "Footprint", X(81), Z(52), X(85), Z(56));
            WorldKit.Prop("Basin", fountain, P(83f, 0.35f, 54f), new Vector3(4f, 0.7f, 4f), M.Granite);
            WorldKit.Prop("Water", fountain, P(83f, 0.68f, 54f), new Vector3(3.6f, 0.04f, 3.6f), M.Sea);
            WorldKit.Prop("Jet", fountain, P(83f, 1.3f, 54f), new Vector3(0.3f, 1.2f, 0.3f), M.Ashlar);
            Tag(fountain, "square_fountain", "Square granite basin with a single plain jet: water used as a level.");
            foreach (Vector2 p in new[] { new Vector2(Square.x + 0.2f, 50f), new Vector2(Square.x + 0.2f, 74f), new Vector2(Square.xMax - 0.2f, 50f), new Vector2(Square.xMax - 0.2f, 74f) })
                Tag(WorldKit.Lamp(g, P(p.x, 0f, p.y), M.Iron, M.Lamp), "street_lamp", "Slim iron lamp standard, a glass globe with a cold white flame (Concordian lamp oil).");
        }

        // ------------------------------------------------------------------ the Concord

        private static void BuildConcord(Transform map)
        {
            // The assembly of the agreement: a round-ended hall on the meridian, benches in arcs facing
            // the rostrum, a verdigris dome with an eye over the floor. Concordia isn't a nation; this
            // is where its members agree, at length.
            Transform g = WorldKit.Group(map, "The Concord");
            const int x0 = 60, z0 = 80, x1 = 86, z1 = 99;
            Transform hall = Hall(g, "Hall", x0, z0, x1, z1, 10f, M.Civic, M.Paving, new WorldKit.Door(WorldKit.Side.South, X(71), X(73)));
            Tag(hall, "concord_hall", "Hero building. The Concord: a 26 x 19 tile ashlar hall, 10 m, brass cornice, tall clear windows; inside, benches in arcs facing a rostrum under a dome.");
            // The portico: plain columns, a flat entablature, the word CONCORD in brass.
            Tag(WorldKit.Colonnade(g, "Portico", T(x0 + 2, z0 - 2), T(x1 - 3, z0 - 2), 8f, M.Ashlar, M.Ashlar, 3, 1.6f), "concord_portico",
                "The Concord's portico: eight plain ashlar columns, 8 m, a flat entablature with CONCORD in brass letters. No pediment, no ornament.");
            WorldKit.Prop("Lettering", g, P(Line, 7.9f, z0 - 2.45f), new Vector3(8f, 0.5f, 0.06f), M.Brass);
            // Benches in arcs round the rostrum, leaving an aisle down the meridian.
            Transform benches = WorldKit.Group(hall, "Benches");
            Vector2 c = new Vector2(Line, z1 - 3f);
            var blocked = new HashSet<Vector2Int>();
            foreach (float r in new[] { 5f, 8f, 11f })
            {
                for (float a = 200f; a <= 340f; a += 4f)
                {
                    if (Mathf.Abs(a - 270f) < 12f || Mathf.Abs(a - 228f) < 7f || Mathf.Abs(a - 312f) < 7f) continue; // the aisles
                    float rad = a * Mathf.Deg2Rad;
                    var p = new Vector2(c.x + Mathf.Cos(rad) * r, c.y + Mathf.Sin(rad) * r);
                    var tile = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
                    if (tile.x <= x0 || tile.x >= x1 - 1 || tile.y <= z0) continue;
                    blocked.Add(tile);
                    WorldKit.Prop("Bench", benches, P(p.x, 0.25f + (r - 5f) * 0.12f, p.y), new Vector3(0.9f, 0.5f + (r - 5f) * 0.24f, 0.5f), M.Timber, 90f - a);
                }
            }
            foreach (Vector2Int t in blocked)
                WorldKit.BlockTiles(benches, "Footprint", X(t.x), Z(t.y), X(t.x) + 1, Z(t.y) + 1);
            Tag(benches, "concord_benches", "Assembly benches in three rising arcs with three aisles: plain dark wood with numbered brass seat plates. Modular arc segments.");
            Transform rostrum = WorldKit.Station(hall, "Rostrum", "concord_rostrum", X(72), Z(z1 - 2));
            WorldKit.Box("Rostrum", rostrum, P(Line, 0.6f, z1 - 1.5f), new Vector3(1.6f, 1.2f, 1f), M.Granite);
            WorldKit.Prop("Lectern", rostrum, P(Line, 1.5f, z1 - 1.6f), new Vector3(0.7f, 0.6f, 0.5f), M.Brass);
            Tag(rostrum, "rostrum", "The rostrum: a granite block with a brass lectern and a sand-glass. Speakers get one glass.");
            // The dome and the light from its eye.
            Transform dome = WorldKit.Group(g, "Dome");
            WorldKit.Dome(dome, P(Line, 10f + 4.5f, (z0 + z1) / 2f), 7f, 7f, M.Verdigris, M.Brass, 7);
            dome.gameObject.AddComponent<RoofHider>().interior = new RectInt(X(x0), Z(z0), x1 - x0, z1 - z0);
            Tag(dome, "concord_dome", "The Concord's dome: copper gone verdigris, 14 m across, a glazed eye at the top. The houses are slate; only the instruments of the city get copper.");
            (Light l, Renderer _) = FxKit.Shaft(hall, P(Line, 0f, (z0 + z1) / 2f), 14f, 1.4f, new Color(0.9f, 0.95f, 1f), M.Beam);
            l.intensity = 30f;
        }

        // ------------------------------------------------------------------ the funicular and the Orrery

        private static void BuildFunicularAndOrrery(Transform map)
        {
            // At the head of the meridian, the lower station of the funicular that climbs the crag to
            // the Orrery, the observatory where the moonfall forecasts are made. The Orrery is only
            // scenery here; it gets its own region later.
            Transform g = WorldKit.Group(map, "The funicular");
            Transform station = Building(g, "Lower station", 66, 104, 80, 114, 8f, M.Civic, WorldKit.RoofKind.Gable);
            Tag(station, "funicular_station", "Funicular lower station: an ashlar shed, 14 x 10 tiles, 8 m, a glazed gable facing the slope where the cars come in, a big brass winding wheel visible through it.");
            WorldKit.WaterWheel(station, P(Line, 6f, 114.2f), 2.4f, 0.4f, 0f, M.Brass, null, 10);
            Transform gate = WorldKit.Station(g, "Funicular gate", "orrery_lift", X(72), Z(103));
            WorldKit.Box("Turnstile", gate, P(72.5f, 0.55f, 103.5f), new Vector3(0.9f, 1.1f, 0.5f), M.Brass);
            var top = P(Line, 62f, 190f);
            Tag(WorldKit.Rails(g, P(Line, 0f, 114f), top, 1.6f, M.Iron, M.Timber), "funicular_rails",
                "Funicular track: two iron rails on timber sleepers on a stone bed, straight up the crag at about 35 degrees. Modular.");
            Transform car = WorldKit.Group(g, "Car");
            Vector3 mid = Vector3.Lerp(P(Line, 0f, 114f), top, 0.42f);
            WorldKit.Prop("Car", car, mid + Vector3.up * 1.5f, new Vector3(2.2f, 2.6f, 4f), M.BlueGrey).transform.rotation = Quaternion.LookRotation(top - P(Line, 0f, 114f)) * Quaternion.Euler(0f, 0f, 0f);
            Tag(car, "funicular_car", "Funicular car: a stepped blue-grey timber cabin with brass rails and big windows, built level on a sloping chassis.");

            // The Orrery on its crag.
            Transform orrery = WorldKit.Group(map, "The Orrery (distant)");
            WorldKit.Prop("Crag", orrery, P(Line, 28f, 196f), new Vector3(44f, 56f, 34f), M.Rock, 6f);
            WorldKit.Prop("Crag top", orrery, P(Line, 60f, 198f), new Vector3(36f, 8f, 28f), M.Granite);
            WorldKit.Cylinder("Drum", orrery, P(Line, 70f, 198f), new Vector3(22f, 6f, 22f), M.Ashlar, collider: false);
            WorldKit.Dome(orrery, P(Line, 76f, 198f), 10.5f, 11f, M.Verdigris, M.Brass, 8);
            WorldKit.Prop("Slit", orrery, P(Line, 82f, 188.5f), new Vector3(2f, 9f, 0.4f), M.Shadow, 0f).transform.rotation = Quaternion.Euler(-35f, 0f, 0f);
            WorldKit.Beam("Great telescope", orrery, P(Line, 80f, 194f), P(Line, 88f, 184f), 1.6f, 1.6f, M.Brass);
            foreach (int s in new[] { -1, 1 })
            {
                WorldKit.Cylinder("Side drum", orrery, P(Line + s * 16f, 66f, 200f), new Vector3(8f, 4f, 8f), M.Ashlar, collider: false);
                WorldKit.Dome(orrery, P(Line + s * 16f, 70f, 200f), 3.6f, 4f, M.Verdigris, M.Brass, 5);
            }
            FxKit.PointLight(orrery, "Lamps", P(Line, 72f, 186f), new Color(0.85f, 0.92f, 1f), 3f, 20f);
            Tag(orrery, "the_orrery_far", "Hero silhouette. The Orrery on its crag above the city: a great drum and a 22 m verdigris dome with an open slit and a huge brass telescope, two lesser domes. Seen from everywhere in Meridian.");
        }

        // ------------------------------------------------------------------ the Academy

        private static void BuildAcademy(Transform map)
        {
            // West of the square: the Academy, four ranges round a court. In the court, behind a
            // brass rail, a fragment of the cracked moon, still glowing; telescopes on tripods.
            Transform g = WorldKit.Group(map, "The Academy");
            WorldKit.Patch("Court", g, X(26), Z(70), X(50), Z(94), M.Grass, 0.006f);
            WorldKit.Patch("Court walks", g, X(26), Z(81), X(50), Z(84), M.Paving, 0.01f);
            WorldKit.Patch("Court walks", g, X(36), Z(70), X(40), Z(94), M.Paving, 0.01f);
            const string Note = "Academy range: two storeys of ashlar over a granite ground floor, slate roof, tall windows in strict rows, a brass clock in the gable of the east range.";
            Tag(Building(g, "West range", 22, 66, 26, 98, 10f, M.Civic), "academy_range", Note);
            Tag(Building(g, "North range", 26, 94, 54, 98, 10f, M.Civic), "academy_range", null);
            Tag(Building(g, "South range", 26, 66, 54, 70, 10f, M.Civic), "academy_range", null);
            Tag(Building(g, "East range (south)", 50, 70, 54, 79, 10f, M.Civic), "academy_range", null);
            Tag(Building(g, "East range (north)", 50, 87, 54, 94, 10f, M.Civic), "academy_range", null);
            Tag(WorldKit.Arch(g, P(54.5f, 0f, 79f), P(54.5f, 0f, 87f), 0f, 4f, 10f, 1.2f, M.Ashlar), "academy_gate", "The Academy gate: a plain round arch through the east range, 8 m wide, the clock above it.");
            WorldKit.Cylinder("Clock", g, P(54.62f, 8.2f, 83f), new Vector3(2f, 0.05f, 2f), M.Brass, collider: false).transform.rotation = Quaternion.Euler(0f, 0f, 90f);

            // The fragment.
            Transform frag = WorldKit.Group(g, "The fragment");
            Vector3 c = P(38f, 0f, 82.5f);
            WorldKit.BlockTiles(frag, "Footprint", X(36), Z(81), X(40), Z(84));
            WorldKit.Prop("Bed", frag, c + Vector3.up * 0.2f, new Vector3(3.6f, 0.4f, 2.6f), M.Granite);
            WorldKit.Prop("Stone", frag, c + Vector3.up * 1f, new Vector3(1.6f, 1.3f, 1.2f), M.DwarfStone, 30f).transform.rotation = Quaternion.Euler(14f, 30f, 8f);
            WorldKit.Prop("Seam", frag, c + Vector3.up * 1.1f, new Vector3(1.7f, 0.12f, 0.2f), M.Fragment, 30f).transform.rotation = Quaternion.Euler(14f, 30f, 28f);
            WorldKit.Prop("Shard", frag, c + new Vector3(0.6f, 1.6f, 0.2f), new Vector3(0.3f, 0.7f, 0.3f), M.Fragment, 10f).transform.rotation = Quaternion.Euler(20f, 10f, -15f);
            foreach ((Vector3 a, Vector3 b) in new[]
                     {
                         (c + new Vector3(-2f, 0.9f, -1.5f), c + new Vector3(2f, 0.9f, -1.5f)), (c + new Vector3(-2f, 0.9f, 1.5f), c + new Vector3(2f, 0.9f, 1.5f)),
                         (c + new Vector3(-2f, 0.9f, -1.5f), c + new Vector3(-2f, 0.9f, 1.5f)), (c + new Vector3(2f, 0.9f, -1.5f), c + new Vector3(2f, 0.9f, 1.5f)),
                     })
                WorldKit.Beam("Rail", frag, a, b, 0.06f, 0.06f, M.Brass);
            FxKit.PointLight(frag, "Anima", c + Vector3.up * 1.5f, new Color(0.35f, 1f, 0.75f), 2.5f, 7f, flicker: 0.2f);
            FxKit.Motes(frag, c + Vector3.up * 1.5f, 1.5f, M.Mote, new Color(0.5f, 1f, 0.8f, 0.6f));
            Transform look = WorldKit.Station(frag, "Fragment rail", "moon_fragment", X(38), Z(80));
            WorldKit.Box("Label", look, P(38.5f, 0.5f, 80.6f), new Vector3(0.8f, 1f, 0.15f), M.Brass);
            Tag(frag, "moon_fragment", "Hero prop. A fragment of the cracked moon: a dark, glassy boulder the size of a cart with a seam of green light through it, on a granite bed behind a brass rail. Glows, hums.");
            foreach (Vector2Int t in new[] { new Vector2Int(30, 74), new Vector2Int(46, 74), new Vector2Int(30, 90), new Vector2Int(46, 90) })
            {
                Transform scope = WorldKit.Group(g, "Telescope");
                WorldKit.BlockTiles(scope, "Footprint", X(t.x), Z(t.y), X(t.x) + 1, Z(t.y) + 1);
                WorldKit.Prop("Tripod", scope, P(t.x + 0.5f, 0.7f, t.y + 0.5f), new Vector3(0.12f, 1.4f, 0.12f), M.Timber);
                WorldKit.Beam("Tube", scope, P(t.x + 0.1f, 1.2f, t.y + 0.9f), P(t.x + 0.9f, 1.9f, t.y + 0.1f), 0.22f, 0.22f, M.Brass);
                Tag(scope, "tripod_telescope", "Brass telescope on a timber tripod.");
            }
        }

        // ------------------------------------------------------------------ the Hall of Measures and the Almanac Office

        private static void BuildMeasuresAndAlmanac(Transform map)
        {
            // South of the square on either side of Line Street: the Hall of Measures, where the
            // standard ell and weights are kept (and the Exchequer, the city's bank), and the Almanac
            // Office, which posts the moonfall forecasts on the board outside.
            Transform g = WorldKit.Group(map, "Hall of Measures");
            Transform hall = Hall(g, "Hall", 56, 30, 70, 46, 8f, M.Civic, M.Paving, new WorldKit.Door(WorldKit.Side.North, X(62), X(64)));
            Tag(hall, "hall_of_measures", "The Hall of Measures: ashlar, 14 x 16 tiles, 8 m, a brass cornice; inside, the standards on the walls and the Exchequer's counter.");
            Transform std = WorldKit.Station(hall, "The standards", "standard_measures", X(57), Z(38));
            WorldKit.Box("Case", std, P(57.5f, 1.2f, 38.5f), new Vector3(0.8f, 2.4f, 0.9f), M.Timber);
            WorldKit.Prop("Standard ell", hall, P(56.2f, 2.4f, 38.5f), new Vector3(0.06f, 0.08f, 6f), M.Brass);
            for (int i = 0; i < 6; i++)
                WorldKit.Cylinder("Weight", hall, P(57.6f, 0.15f + i * 0.0f, 33.5f + i * 0.7f), new Vector3(0.2f + i * 0.08f, 0.15f + i * 0.04f, 0.2f + i * 0.08f), M.Brass, collider: false);
            Tag(std, "standards_case", "The standards: a glazed case with the standard ell (a brass bar), a nest of brass weights, a balance; the reference every measure in Concordia is checked against.");
            Transform box = WorldKit.Station(hall, "Strongbox", "synod_strongbox", X(68), Z(32));
            WorldKit.Box("Chest", box, box.position + new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.8f, 0.7f), M.Iron);
            WorldKit.Prop("Bands", box, box.position + new Vector3(0f, 0.45f, 0f), new Vector3(0.95f, 0.1f, 0.75f), M.Brass);
            Transform counter = WorldKit.Group(hall, "Exchequer counter");
            WorldKit.BlockTiles(counter, "Footprint", X(62), Z(34), X(69), Z(35));
            WorldKit.Prop("Counter", counter, P(65.5f, 0.55f, 34.5f), new Vector3(7f, 1.1f, 0.8f), M.Timber);
            Tag(counter, "exchequer_counter", "The Exchequer's counter: plain timber, brass scales and an abacus for each clerk.");
            FxKit.PointLight(hall, "Lamps", P(63f, 5f, 38f), new Color(0.9f, 0.95f, 1f), 2f, 10f);

            Transform almanac = WorldKit.Group(map, "The Almanac Office");
            Transform office = Building(almanac, "Almanac Office", 76, 30, 90, 46, 9f, M.Civic, WorldKit.RoofKind.Gable);
            Tag(office, "almanac_office", "The Almanac Office: ashlar, 9 m, slate roof; a semaphore mast on the ridge to signal forecasts to the Orrery and the beacon towers.");
            WorldKit.Prop("Mast", office, P(83f, 16f, 38f), new Vector3(0.25f, 8f, 0.25f), M.Timber);
            foreach (float a in new[] { 40f, -25f })
                WorldKit.Prop("Semaphore arm", office, P(83f, 19f, 38f) + Quaternion.Euler(0f, 0f, a) * new Vector3(1.2f, 0f, 0f), new Vector3(2.4f, 0.25f, 0.1f), M.BlueGrey).transform.rotation = Quaternion.Euler(0f, 0f, a);
            Transform board = WorldKit.Station(almanac, "Almanac board", "moonfall_almanac", X(82), Z(47));
            WorldKit.Box("Board", board, P(82.5f, 1.4f, 47.5f), new Vector3(2.8f, 2.4f, 0.2f), M.Slate);
            WorldKit.Prop("Chart", board, P(82.5f, 1.5f, 47.62f), new Vector3(2.4f, 1.9f, 0.02f), M.Canvas);
            Tag(board, "almanac_board", "The almanac board: a slate board with the season's moonfall forecast pinned up as a chart (sky sectors, dates, likelihoods), updated by semaphore.");
        }

        // ------------------------------------------------------------------ the harbour

        private static void BuildHarbour(WorldKit kit, Transform map)
        {
            Transform g = WorldKit.Group(map, "The harbour");
            WorldKit.Patch("Quay", g, X(Cliff), Z(Shore), X(Size.x), Z(Shore + 6), M.Paving, 0.006f);
            WorldKit.Patch("Breakwater", g, X(100), Z(4), X(103), Z(Shore), M.Granite, 0.006f);
            WorldKit.Patch("Breakwater", g, X(86), Z(4), X(100), Z(7), M.Granite, 0.006f);
            WorldKit.Prop("Quay edge", g, P((Cliff + Size.x) / 2f, -0.2f, Shore - 0.15f), new Vector3(Size.x - Cliff, 0.6f, 0.3f), M.Granite);
            Tag(g, "quay", "Granite quay: big squared blocks, iron rings, numbered berths painted on the edge.");
            Transform light = WorldKit.Tower(g, "Harbour beacon", X(86), Z(4), 3, 16f, M.Ashlar, M.Paving, M.BlueGrey, WorldKit.RoofKind.Flat, M.BlueGrey);
            WorldKit.Prop("Lantern", light, P(87.5f, 17f, 5.5f), new Vector3(1.6f, 1.8f, 1.6f), M.Lamp);
            WorldKit.Dome(light, P(87.5f, 17.9f, 5.5f), 1f, 1.2f, M.Verdigris, M.Brass, 4);
            FxKit.PointLight(light, "Beacon", P(87.5f, 17f, 5.5f), new Color(0.9f, 0.95f, 1f), 3f, 16f);
            Tag(light, "harbour_beacon", "Harbour beacon: a slim ashlar tower, 16 m, blue-grey bands, a glazed lantern with a copper cap; the same lamp as the pass beacons.");
            Tag(WorldKit.Boat(g, P(66f, 0f, 12f), 13f, 3.8f, 90f, M.Hull, M.Planks, M.Canvas), "trader", "Coastal trader: a neat two-masted ship, furled pale sails, blue-grey trim.");
            Tag(WorldKit.Boat(g, P(90f, 0f, 13f), 11f, 3.4f, 85f, M.Hull, M.Planks, M.Canvas), "trader", null);
            Tag(WorldKit.Boat(g, P(40f, 0f, 14f), 6f, 2f, 100f, M.Hull, M.Planks, M.Canvas), "fishing_boat", "Small fishing boat, single mast.");
            Tag(WorldKit.Boat(g, P(30f, 0f, 11f), 6f, 2f, 80f, M.Hull, M.Planks, M.Canvas), "fishing_boat", null);
            Tag(WorldKit.Crane(g, X(60), Z(Shore + 2), 180f, M.Timber, M.Iron), "quay_crane", "Iron-banded timber crane, a geared winch with a brass dial.");
            foreach (int x in new[] { 34, 52, 80, 96, 112 })
            {
                Transform bollard = WorldKit.Group(g, "Bollard");
                WorldKit.Box("Bollard", bollard, P(x + 0.5f, 0.35f, Shore + 0.5f), new Vector3(0.35f, 0.7f, 0.35f), M.Iron).AddComponent<MapBlocker>();
                Tag(bollard, "bollard", "Iron bollard.");
            }
            foreach (Vector2Int c in new[] { new Vector2Int(44, 23), new Vector2Int(62, 24), new Vector2Int(108, 23), new Vector2Int(118, 24) })
                kit.Clutter(g, X(c.x), Z(c.y), M.Planks, M.Hull);
            foreach ((int x0, int x1) in new[] { (14, 26), (28, 40), (92, 104), (106, 118), (120, 132) })
                Tag(Building(g, "Warehouse", x0, 28, x1, 38, 7f, M.Store), "warehouse", "Granite warehouse with a slate roof, numbered doors, a painted tally board.");
        }

        // ------------------------------------------------------------------ the Butts

        private static void BuildButts(Transform map)
        {
            // The shooting ground east of the square: the Rationalists fight from a long way off, and
            // practise it in public. A roofed firing line, lanes, targets at the far end.
            Transform g = WorldKit.Group(map, "The Butts");
            WorldKit.Patch("Turf", g, X(96), Z(68), X(140), Z(96), M.Grass, 0.006f);
            Transform stand = WorldKit.Group(g, "Firing line");
            for (int z = 70; z <= 94; z += 4)
                WorldKit.Prop("Post", stand, P(98f, 1.5f, z), new Vector3(0.2f, 3f, 0.2f), M.Timber);
            WorldKit.Prop("Roof", stand, P(98f, 3.1f, 82f), new Vector3(3f, 0.15f, 25f), M.Slate);
            for (int z = 71; z < 94; z += 4)
                WorldKit.Prop("Lane mark", g, P(118f, 0.012f, z + 1.5f), new Vector3(36f, 0.01f, 0.1f), M.Ashlar);
            Tag(stand, "firing_line", "Firing line: a long open shelter on timber posts with a slate roof, a rail with numbered lane plates.");
            foreach (int z in new[] { 72, 76, 80, 84, 88, 92 })
            {
                Transform target = WorldKit.Group(g, "Target");
                WorldKit.BlockTiles(target, "Footprint", X(136), Z(z), X(137), Z(z + 1));
                WorldKit.Prop("Frame", target, P(136.5f, 1f, z + 0.5f), new Vector3(0.15f, 2f, 1.2f), M.Timber);
                GameObject face = WorldKit.Cylinder("Face", target, P(136.3f, 1.3f, z + 0.5f), new Vector3(1.1f, 0.05f, 1.1f), M.Target, collider: false);
                face.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                GameObject bull = WorldKit.Cylinder("Rings", target, P(136.25f, 1.3f, z + 0.5f), new Vector3(0.5f, 0.05f, 0.5f), M.BlueGrey, collider: false);
                bull.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                Tag(target, "range_target", "Round straw target on a timber frame, painted in concentric blue-grey rings with numbered scoring bands.");
            }
            WorldKit.Prop("Earth bank", g, P(139f, 1.2f, 82f), new Vector3(2f, 2.4f, 26f), M.Grass);
        }

        // ------------------------------------------------------------------ the pass road and the toll

        private static void BuildPass(Transform map)
        {
            // The road up into the range: every crossing pays. A toll bar, the toll-house, and a beacon
            // tower, the first of the line of them up the pass.
            Transform g = WorldKit.Group(map, "The pass toll");
            Tag(Building(g, "Toll-house", 124, 104, 132, 112, 6f, M.Row2), "toll_house", "Concordian toll-house: small granite house with a blue-grey door and a window onto the road with a brass bell.");
            WorldKit.Prop("Bar post", g, P(PassX0 - 0.3f, 0.6f, 110.5f), new Vector3(0.4f, 1.2f, 0.4f), M.Granite);
            WorldKit.Prop("Toll bar (raised)", g, P(PassX0 - 0.2f, 2.6f, 110.5f), new Vector3(0.18f, 4f, 0.18f), M.BlueGrey);
            Transform board = WorldKit.Station(g, "Toll board", "concord_toll_board", X(PassX1), Z(102));
            WorldKit.Box("Board", board, P(PassX1 + 0.5f, 1.2f, 102.5f), new Vector3(0.9f, 1.6f, 0.15f), M.Slate);
            Tag(board, "toll_board", "A slate board on posts with the crossing tolls in white paint, by weight and by head.");
            Transform beacon = WorldKit.Tower(g, "Beacon tower", X(114), Z(106), 3, 14f, M.Ashlar, M.Paving, M.BlueGrey, WorldKit.RoofKind.Flat, M.BlueGrey);
            WorldKit.Prop("Lantern", beacon, P(115.5f, 15f, 107.5f), new Vector3(1.6f, 1.8f, 1.6f), M.Lamp);
            WorldKit.Dome(beacon, P(115.5f, 15.9f, 107.5f), 1f, 1.2f, M.Verdigris, M.Brass, 4);
            FxKit.PointLight(beacon, "Beacon", P(115.5f, 15f, 107.5f), new Color(0.9f, 0.95f, 1f), 3f, 16f);
            Tag(beacon, "beacon_tower", "Concordian beacon tower: slim ashlar, 14 m, a glazed lamp under a copper cap; one every few miles up every pass, lit all night.");
        }

        // ------------------------------------------------------------------ the Undergate and the bore

        private static void BuildUndergate(WorldKit kit, Transform map)
        {
            // North-west, against the mountain: the Undergate, where the dwarves' cart-line comes out of
            // the rock under the Under-Treaty, with their trading hall and ore yard. Beside it the
            // survey office and Bore Nine, the Rationalists' deep bore, fenced, its spoil glowing green.
            Transform g = WorldKit.Group(map, "The Undergate");
            WorldKit.Patch("Ore yard", g, X(Cliff + 1), Z(100), X(36), Z(Foot), M.Cobble, 0.006f);
            Transform portal = WorldKit.Group(g, "Portal");
            WorldKit.Arch(portal, P(20f, 0f, Foot + 0.5f), P(26f, 0f, Foot + 0.5f), 0f, 3.6f, 9f, 2f, M.DwarfStone, 7);
            WorldKit.Prop("Darkness", portal, P(23f, 1.8f, Foot + 1.2f), new Vector3(5.6f, 3.6f, 0.2f), M.Shadow);
            foreach (int s in new[] { -1, 1 })
                WorldKit.Prop("Guardian pier", portal, P(23f + s * 4f, 4.5f, Foot + 0.5f), new Vector3(2f, 9f, 2.2f), M.DwarfStone);
            WorldKit.Prop("Lintel rune", portal, P(23f, 7.6f, Foot - 0.55f), new Vector3(4f, 0.6f, 0.06f), M.Bronze);
            Tag(portal, "undergate", "The Undergate: a squat, massive dwarven portal cut into the mountain, dark granite piers, a bronze rune-band over a 6 m opening; rails run into the dark.");
            Tag(WorldKit.Rails(g, P(23f, 0f, Foot + 2f), P(23f, 0f, 101f), 1.2f, M.Iron, M.Timber), "cart_rails", "Dwarven cart-line: narrow-gauge iron rails on stone sleepers.");
            foreach (float z in new[] { 106f, 110.5f })
            {
                Transform cart = WorldKit.Group(g, "Ore cart");
                WorldKit.BlockTiles(cart, "Footprint", X(22), Mathf.FloorToInt(Z(0) + z), X(24), Mathf.FloorToInt(Z(0) + z) + 2);
                WorldKit.Prop("Tub", cart, P(23f, 0.8f, z + 1f), new Vector3(1.3f, 0.8f, 1.8f), M.Iron);
                WorldKit.Prop("Ore", cart, P(23f, 1.25f, z + 1f), new Vector3(1.1f, 0.3f, 1.6f), M.Rock);
                Tag(cart, "ore_cart", "Dwarven ore cart: an iron tub on four small wheels, heaped with ore.");
            }
            Transform hall = Building(g, "Dwarven trading hall", Cliff + 2, 100, 20, 112, 7f, M.Dwarf, WorldKit.RoofKind.Battlements);
            Tag(hall, "dwarf_trading_hall", "The dwarves' trading hall: low, massive dark granite, bronze-bound door and shutters, a battlemented roof; built to keep out more than weather.");
            foreach (Vector2Int c in new[] { new Vector2Int(28, 104), new Vector2Int(30, 112), new Vector2Int(33, 108) })
                kit.Clutter(g, X(c.x), Z(c.y), M.Planks, M.Iron);

            Transform survey = WorldKit.Group(map, "The survey and Bore Nine");
            Tag(Building(survey, "Survey office", 38, 102, 47, 112, 7f, M.Row2, WorldKit.RoofKind.Gable), "survey_office",
                "The survey office: a granite house with a slate roof, maps in the windows, crates of core samples stacked outside.");
            Transform notice = WorldKit.Station(survey, "Notice", "survey_notice", X(42), Z(100));
            WorldKit.Box("Board", notice, P(42.5f, 1.1f, 100.5f), new Vector3(0.9f, 1.4f, 0.12f), M.Timber);
            WorldKit.Prop("Paper", notice, P(42.5f, 1.2f, 100.58f), new Vector3(0.6f, 0.8f, 0.02f), M.Canvas);
            Tag(notice, "notice_board", "A timber notice board with one fresh paper on it.");
            for (int i = 0; i < 3; i++)
                WorldKit.Prop("Core crate", survey, P(47.6f + i * 0.0f, 0.3f + i * 0.6f, 103f), new Vector3(0.9f, 0.55f, 1.4f), M.Planks);
            WorldKit.Prop("Glowing cores", survey, P(47.6f, 1.95f, 103f), new Vector3(0.7f, 0.1f, 1.2f), M.Spoil);

            Transform bore = WorldKit.Group(survey, "Bore Nine");
            WorldKit.BlockTiles(bore, "Footprint", X(50), Z(104), X(57), Z(113));
            Vector3 head = P(53.5f, 0f, 108.5f);
            foreach (Vector2 c in new[] { new Vector2(-2.5f, -2.5f), new Vector2(2.5f, -2.5f), new Vector2(-2.5f, 2.5f), new Vector2(2.5f, 2.5f) })
                WorldKit.Beam("Leg", bore, head + new Vector3(c.x, 0f, c.y), head + Vector3.up * 14f, 0.3f, 0.3f, M.Timber);
            for (float y = 3f; y < 13f; y += 3f)
            {
                float w = 5f * (1f - y / 14f);
                WorldKit.Prop("Brace", bore, head + Vector3.up * y, new Vector3(w, 0.15f, 0.15f), M.Iron);
                WorldKit.Prop("Brace", bore, head + Vector3.up * y, new Vector3(0.15f, 0.15f, w), M.Iron);
            }
            WorldKit.WaterWheel(bore, head + new Vector3(0f, 14.6f, 0f), 1.4f, 0.3f, 90f, M.Iron, null, 8);
            WorldKit.Prop("Bore casing", bore, head + Vector3.up * 1f, new Vector3(1f, 2f, 1f), M.Iron);
            WorldKit.Cylinder("Spoil heap", bore, P(55.5f, 0.6f, 111.5f), new Vector3(2.4f, 0.6f, 2.4f), M.Spoil, collider: false);
            FxKit.PointLight(bore, "Spoil glow", P(55.5f, 1.2f, 111.5f), new Color(0.35f, 1f, 0.75f), 1.6f, 6f, flicker: 0.15f);
            // A fence round it, with a sign.
            WorldKit.Fence(bore, "Fence", X(49), Z(103), X(58), Z(103), M.Timber);
            Transform sign = WorldKit.Station(bore, "Bore sign", "bore_nine", X(53), Z(102));
            WorldKit.Box("Sign", sign, P(53.5f, 0.9f, 102.5f), new Vector3(0.9f, 0.7f, 0.1f), M.Slate);
            Tag(bore, "bore_nine", "Bore Nine: a 15 m timber-and-iron derrick over an iron bore casing, a winding wheel on top, a heap of spoil that glows faint green; fenced, with a survey sign.");
        }

        // ------------------------------------------------------------------ the town

        private static void BuildTown(WorldKit kit, Transform map)
        {
            // Terraced rows on the grid: three storeys of ashlar or granite, slate roofs, blue-grey
            // doors; a little copper observatory dome on some roofs.
            Transform g = WorldKit.Group(map, "Rows");
            (int x0, int z0, int x1, int z1)[] rows =
            {
                // south-west, between the quay and the Parallel
                (14, 42, 32, 50), (34, 42, 54, 50), (14, 52, 32, 58), (34, 52, 54, 58),
                // south-east
                (92, 42, 110, 50), (112, 42, 132, 50), (92, 52, 110, 58), (112, 52, 132, 58), (134, 42, 142, 58),
                // east of the square, north of the Parallel
                (92, 66, 94, 98), (92, 100, 110, 108), (92, 110, 110, 116), (132, 100, 142, 116),
                // north of the Parallel, west of the Academy
                (14, 66, 20, 98),
                // by the Concord
                (88, 80, 90, 98),
            };
            foreach ((int x0, int z0, int x1, int z1) in rows)
            {
                if (x1 - x0 < 3) continue;
                bool alongX = x1 - x0 >= z1 - z0;
                int len = alongX ? x1 - x0 : z1 - z0;
                int n = Mathf.Max(1, len / 6);
                for (int i = 0; i < n; i++)
                {
                    int a = (alongX ? x0 : z0) + i * len / n, b = (alongX ? x0 : z0) + (i + 1) * len / n;
                    bool granite = kit.Chance(0.4);
                    float eave = kit.Range(7.5f, 9.5f);
                    Transform h = alongX
                        ? Building(g, "Row house", a, z0, b, z1, eave, granite ? M.Row2 : M.Row)
                        : Building(g, "Row house", x0, a, x1, b, eave, granite ? M.Row2 : M.Row);
                    if (kit.Chance(0.2))
                    {
                        float cx = alongX ? (a + b) / 2f : (x0 + x1) / 2f, cz = alongX ? (z0 + z1) / 2f : (a + b) / 2f;
                        WorldKit.Dome(h, P(cx, eave + 2.8f, cz), 1.1f, 1.3f, M.Verdigris, M.Brass, 4);
                        Tag(h, "row_house_observatory", "Row house with a little rooftop observatory: a copper dome on a drum, gone verdigris.");
                    }
                    else Tag(h, granite ? "row_house_granite" : "row_house_ashlar", "Terraced row house: three storeys of " + (granite ? "grey granite" : "pale ashlar") +
                        ", slate roof, blue-grey door and window frames, identical to its neighbours. Modular.");
                }
            }
            Transform lamps = WorldKit.Group(map, "Lamps");
            foreach (Vector2 p in new[]
                     {
                         new Vector2(20f, 59.8f), new Vector2(40f, 59.8f), new Vector2(100f, 59.8f), new Vector2(120f, 59.8f), new Vector2(69.8f, 30f), new Vector2(76.2f, 30f),
                         new Vector2(69.8f, 102f), new Vector2(76.2f, 102f), new Vector2(PassX0 - 0.2f, 80f), new Vector2(PassX1 + 0.2f, 96f), new Vector2(30f, 99f),
                     })
                Tag(WorldKit.Lamp(lamps, P(p.x, 0f, p.y), M.Iron, M.Lamp), "street_lamp", null);
        }

        // ------------------------------------------------------------------ people

        private static void BuildPeople(Transform map)
        {
            Transform g = WorldKit.Group(map, "People");
            (string npc, int x, int z, int wander)[] people =
            {
                ("concord_warden", 119, 108, 0), ("concord_warden", 124, 114, 2), ("concord_warden", 70, 78, 0), ("concord_warden", 75, 78, 0),
                ("concord_warden", 48, 108, 2), ("concord_warden", 140, 62, 3),
                ("concord_scholar", 64, 58, 5), ("concord_scholar", 78, 64, 5), ("concord_scholar", 32, 82, 4), ("concord_scholar", 44, 86, 4),
                ("concord_scholar", 72, 90, 3), ("concord_scholar", 68, 86, 3), ("concord_scholar", 70, 101, 2),
                ("almanac_clerk", 78, 48, 2), ("almanac_clerk", 65, 38, 2),
                ("concord_marksman", 100, 72, 0), ("concord_marksman", 100, 80, 0), ("concord_marksman", 100, 88, 0), ("concord_marksman", 104, 92, 3),
                ("dwarf_carter", 25, 104, 3), ("dwarf_carter", 21, 115, 2), ("dwarf_carter", 30, 109, 3),
                ("concord_surveyor", 48, 101, 2), ("concord_surveyor", 58, 108, 2),
                ("meridian_fisher", 36, 22, 4), ("meridian_fisher", 101, 12, 3), ("meridian_fisher", 120, 22, 4),
                ("concord_scholar", 84, 70, 4), ("concord_scholar", 58, 72, 4), ("concord_scholar", 20, 62, 6),
            };
            foreach ((string npc, int x, int z, int wander) in people)
                WorldKit.Spawn(g, npc, X(x), Z(z), wander);
        }
    }
}
