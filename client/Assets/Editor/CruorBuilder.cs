using System.Collections.Generic;
using Rth.MapTools;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// SUPERSEDED (2026-10-23): the owner judged this layout far too dense. Cruor is to be laid out
    /// again (docs/regions/cruor.md, docs/regions/cruor-architecture.md); don't extend this builder,
    /// and note that rebuilding rewrites docs/regions/cruor-assets.md without its superseded banner.
    /// Cruor, the first see of Blood and the richest city in the basin (docs/regions/cruor.md).
    /// A walled river city on the Cradle's northern rim, facing the bay and what lies in it: a
    /// late golden haze, pale limestone and red tile, banners and braziers everywhere, and every
    /// street leading down to the water. The pilgrims' road comes in at the west gate and becomes
    /// the Processional Way, over the river to the Plaza of the Keeping; the plaza runs from the
    /// cathedral (the Vigil) down past the First Monument to the Watch, a terrace over the bay
    /// where the faithful look at the Quaere. Around it: the Letting House where the faithful give
    /// blood, the blood-knights' yard, the Pontiff's palace, the market, the lower town and the
    /// quays. Every placeholder is tagged with the asset it stands for (WorldKit.Asset), and
    /// saving writes the list to docs/regions/cruor-assets.md. Once built, the scene is the
    /// source of truth.
    /// </summary>
    public static class CruorBuilder
    {
        public const string ScenePath = "Assets/Scenes/Cruor.unity";

        // The region: 144 x 136 tiles, its south-west corner here. Everything below is laid out in
        // local tiles (0..143 east, 0..135 north) and offset by this when placed.
        private const int OX = 384, OZ = 40;
        private static readonly Vector2Int Size = new Vector2Int(144, 136);

        // The plan, in local tiles.
        private const int Shore = 22;          // the quay's edge: the bay is south of this line
        private const int WallW = 8, WallE = 136, WallN = 128; // city wall lines (tile rows/columns)
        private const int ProcS = 72, ProcN = 77; // the Processional Way, z 72..76
        private const int PlazaW = 60, PlazaE = 105, PlazaS = 38, PlazaN = 87;
        private const int RunW = 81, RunE = 84; // the red runner down the plaza's axis
        private const float Axis = 82.5f;      // the plaza's and the cathedral's centre line
        private static readonly Vector2Int Monument = new Vector2Int(82, 62);

        // The river comes down from the north through a water gate and runs out into the bay.
        private static readonly Vector2[] River =
        {
            new Vector2(44f, 140f), new Vector2(42f, 120f), new Vector2(37f, 104f), new Vector2(35f, 88f), new Vector2(36f, 74f),
            new Vector2(40f, 60f), new Vector2(46f, 46f), new Vector2(50f, 34f), new Vector2(52f, 22f), new Vector2(53f, 12f),
        };

        private static Mats M;

        [MenuItem("R'th/Dev/Rebuild Cruor Scene", priority = 104)]
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

        private static void Tag(Transform t, string id, string note) => WorldKit.Asset(t, "cruor/" + id, note);

        /// <summary>Every material the city uses, made once per build.</summary>
        private sealed class Mats
        {
            public Material Cobble, Grass, Dirt, Flag, Plaza, Limestone, DarkStone, Plaster, Timber, RoofTile, Slate, Red, Runner,
                BannerCloth, Gold, Bronze, Iron, Window, WindowLit, StainedGlass, Bay, River, Planks, Canvas, Hull, Straw, Goods,
                Awning, AwningAlt, BloodGlow, Coals, Flame, Ember, SeaMist, Mote, Beam, Hedge, Bark, Leaves, Cypress, QuaereStone,
                QuaereGlow, Wax;

            public WorldKit.Facade Townhouse, Townhouse2, Grand, Warehouse, Church, Fort;
        }

        public static void Build()
        {
            var kit = new WorldKit(seed: 5150);
            M = new Mats
            {
                Cobble = kit.Mat("CruorCobble", new Color(0.52f, 0.47f, 0.41f)),
                Grass = kit.Mat("CruorGrass", new Color(0.43f, 0.53f, 0.26f)),
                Dirt = kit.Mat("Dirt", new Color(0.55f, 0.42f, 0.26f)), // shared with Tolling
                Flag = kit.Mat("CruorFlagstone", new Color(0.63f, 0.58f, 0.51f)),
                Plaza = kit.Mat("CruorPlaza", new Color(0.74f, 0.68f, 0.58f)),
                Limestone = kit.Mat("CruorLimestone", new Color(0.8f, 0.74f, 0.63f)),
                DarkStone = kit.Mat("CruorDarkStone", new Color(0.44f, 0.39f, 0.36f)),
                Plaster = kit.Mat("CruorPlaster", new Color(0.87f, 0.8f, 0.67f)),
                Timber = kit.Mat("Timber", new Color(0.4f, 0.28f, 0.18f)), // shared
                RoofTile = kit.Mat("CruorRoofTile", new Color(0.64f, 0.23f, 0.16f)),
                Slate = kit.Mat("CruorSlate", new Color(0.3f, 0.27f, 0.31f)),
                Red = kit.Mat("SynodRed", new Color(0.55f, 0.1f, 0.12f)), // shared
                Runner = kit.Mat("CruorRunner", new Color(0.5f, 0.05f, 0.08f)),
                BannerCloth = kit.Mat("CruorBanner", new Color(0.62f, 0.07f, 0.1f)),
                Gold = kit.Mat("CruorGold", new Color(0.86f, 0.66f, 0.26f)),
                Bronze = kit.Mat("Bronze", new Color(0.6f, 0.45f, 0.2f)), // shared
                Iron = kit.Mat("CruorIron", new Color(0.15f, 0.14f, 0.14f)),
                Window = kit.Mat("CruorWindow", new Color(0.17f, 0.14f, 0.16f)),
                Bay = kit.Mat("CruorBay", new Color(0.21f, 0.37f, 0.5f)),
                River = kit.Mat("CruorRiver", new Color(0.24f, 0.4f, 0.47f)),
                Planks = kit.Mat("CruorPlanks", new Color(0.46f, 0.34f, 0.22f)),
                Canvas = kit.Mat("CruorCanvas", new Color(0.86f, 0.8f, 0.67f)),
                Hull = kit.Mat("CruorHull", new Color(0.33f, 0.22f, 0.14f)),
                Straw = kit.Mat("CruorStraw", new Color(0.76f, 0.66f, 0.36f)),
                Goods = kit.Mat("CruorGoods", new Color(0.62f, 0.46f, 0.3f)),
                Awning = kit.Mat("CruorAwningRed", new Color(0.66f, 0.16f, 0.14f)),
                AwningAlt = kit.Mat("CruorAwningCream", new Color(0.88f, 0.82f, 0.66f)),
                Hedge = kit.Mat("CruorHedge", new Color(0.22f, 0.36f, 0.18f)),
                Bark = kit.Mat("Bark", new Color(0.36f, 0.25f, 0.15f)), // shared
                Leaves = kit.Mat("Leaves", new Color(0.2f, 0.42f, 0.18f)), // shared
                Cypress = kit.Mat("CruorCypress", new Color(0.16f, 0.28f, 0.15f)),
                QuaereStone = kit.Mat("QuaereStone", new Color(0.15f, 0.16f, 0.2f)),
                Wax = kit.Mat("CandleWax", new Color(0.9f, 0.86f, 0.72f)), // shared with the crypt
                WindowLit = FxKit.Glow("CruorWindowLit", new Color(1f, 0.72f, 0.42f), 0.6f),
                StainedGlass = FxKit.Glow("CruorStainedGlass", new Color(0.9f, 0.08f, 0.1f), 1.8f),
                BloodGlow = FxKit.Glow("CruorBloodGlow", new Color(0.4f, 0.02f, 0.04f), 0.5f),
                QuaereGlow = FxKit.Glow("QuaereSeam", new Color(0.45f, 0.7f, 0.75f), 0.8f),
                Coals = FxKit.Glow("BrazierCoals", new Color(1f, 0.35f, 0.08f), 0.9f), // shared with the crypt
                Flame = FxKit.ParticleMat("FxFlame", new Color(1f, 0.55f, 0.22f, 0.6f), additive: true), // shared
                Ember = FxKit.ParticleMat("FxEmber", new Color(1f, 0.55f, 0.2f, 1f), additive: true), // shared
                SeaMist = FxKit.ParticleMat("CruorSeaMist", new Color(0.92f, 0.84f, 0.74f, 0.08f), additive: false),
                Mote = FxKit.ParticleMat("FxMote", new Color(0.8f, 0.85f, 1f, 0.6f), additive: true), // shared
                Beam = FxKit.ParticleMat("CruorLightBeam", new Color(1f, 0.82f, 0.6f, 0.1f), additive: true),
            };
            M.Townhouse = new WorldKit.Facade { Walls = M.Plaster, Roof = M.RoofTile, Trim = M.Timber, Windows = M.Window, Gable = M.Plaster, Pitch = 42f };
            M.Townhouse2 = new WorldKit.Facade { Walls = M.Limestone, Roof = M.RoofTile, Trim = M.DarkStone, Windows = M.Window, Pitch = 40f };
            M.Grand = new WorldKit.Facade { Walls = M.Limestone, Roof = M.RoofTile, Trim = M.Red, Windows = M.WindowLit, Pitch = 36f };
            M.Warehouse = new WorldKit.Facade { Walls = M.DarkStone, Roof = M.RoofTile, Trim = M.Timber, Windows = M.Window, Pitch = 30f };
            M.Church = new WorldKit.Facade { Walls = M.Limestone, Roof = M.Slate, Trim = M.DarkStone, Gable = M.Limestone, Pitch = 52f };
            M.Fort = new WorldKit.Facade { Walls = M.Limestone, Roof = M.Flag, Trim = M.DarkStone, Windows = M.Window };

            MapArea area = WorldKit.BeginRegion("cruor", "Cruor", new Vector2Int(OX, OZ), Size, M.Cobble);
            // The late, gold haze of the concept art: a warm sun low over the bay, long warm fog, so
            // the cathedral and the Quaere stand out of the distance rather than vanishing in it.
            area.music = "cruor";
            area.ownAtmosphere = true;
            area.sunIntensity = 1.2f;
            area.sunColor = new Color(1f, 0.82f, 0.6f);
            area.ambientSky = new Color(0.8f, 0.68f, 0.56f);
            area.ambientEquator = new Color(0.64f, 0.52f, 0.43f);
            area.ambientGround = new Color(0.32f, 0.24f, 0.2f);
            area.fog = new Color(0.87f, 0.73f, 0.56f);
            area.fogStart = 55f;
            area.fogEnd = 118f;
            Transform map = area.transform;

            BuildWater(kit, map);
            BuildQuaere(map);
            BuildOutside(kit, map);
            BuildWalls(map);
            BuildProcessional(map);
            BuildPlaza(map);
            BuildMonument(map);
            BuildWatch(map);
            BuildVigil(map);
            BuildLettingHouse(map);
            BuildKnightsYard(map);
            BuildPalace(kit, map);
            BuildQuays(kit, map);
            BuildEastRoad(map);
            BuildMarket(kit, map);
            BuildTown(kit, map);
            BuildAqueduct(map);
            BuildPeople(map);

            // A light haze over the bay, and post-processing so the stained glass and fires glow.
            Transform fx = WorldKit.Group(map, "Atmosphere");
            FxKit.Mist(fx, P(72f, 0.3f, 10f), new Vector2(150f, 22f), M.SeaMist, Color.white);
            FxKit.Volume(fx, "CruorVolume", P(72f, 20f, 68f), new Vector3(150f, 60f, 150f), bloom: 0.55f, vignette: 0.18f, saturation: 8f);

            WorldKit.SaveRegion(area, ScenePath);
        }

        // ------------------------------------------------------------------ the bay and the river

        private static HashSet<Vector2Int> _jetties, _bridges;

        private static void BuildWater(WorldKit kit, Transform map)
        {
            var bounds = new RectInt(OX, OZ, Size.x, Size.y);
            // Jetties out into the bay and bridges over the river are kept out of the water so they can be walked.
            _jetties = new HashSet<Vector2Int>();
            foreach (int jx in new[] { 20, 112, 124 })
                for (int z = 7; z < Shore; z++)
                    for (int x = jx; x < jx + 2; x++)
                        _jetties.Add(T(x, z));
            _bridges = new HashSet<Vector2Int>();
            void Bridge(int x0, int z0, int x1, int z1)
            {
                for (int z = z0; z < z1; z++)
                    for (int x = x0; x < x1; x++)
                        _bridges.Add(T(x, z));
            }
            Bridge(30, ProcS, 43, ProcN);  // the Pilgrims' Bridge, on the Processional Way
            Bridge(32, 108, 43, 111);      // the north footbridge
            Bridge(39, 50, 50, 52);        // the lower town's bridge
            Bridge(46, Shore, 57, Shore + 5); // the quay road over the river mouth

            var bay = new HashSet<Vector2Int>();
            for (int z = 0; z < Shore; z++)
                for (int x = 0; x < Size.x; x++)
                    bay.Add(T(x, z));
            HashSet<Vector2Int> river = WorldKit.Along(Offset(River), 2.6f, bounds);
            river.ExceptWith(bay);
            bay.ExceptWith(_jetties);
            river.ExceptWith(_bridges);
            WorldKit.Water(map, "The Cradle", bay, M.Bay);
            WorldKit.Water(map, "River", river, M.River);
            // Stone banks along the river: a low kerb on each side, so it reads as a channel through a city.
            Transform banks = WorldKit.Group(map, "River banks");
            foreach (Vector2Int t in river)
                foreach (Vector2Int d in new[] { Vector2Int.left, Vector2Int.right })
                {
                    Vector2Int n = t + d;
                    if (river.Contains(n) || bay.Contains(n) || _bridges.Contains(n) || !bounds.Contains(n)) continue;
                    WorldKit.Prop("Bank", banks, new Vector3(n.x + 0.5f - d.x * 0.4f, 0.12f, n.y + 0.5f), new Vector3(0.2f, 0.24f, 1f), M.DarkStone);
                }

            // The sea and the river carry on past the region's edge, into the fog.
            WorldKit.FarWater(map, "The Cradle beyond", new Rect(OX - 120f, OZ - 160f, Size.x + 240f, 160f), M.Bay);
            WorldKit.FarWater(map, "The river beyond", new Rect(OX + 40f, OZ + Size.y, 8f, 40f), M.River);
            WorldKit.FarWater(map, "The bay shore beyond, west", new Rect(OX - 120f, OZ, 120f, Shore), M.Bay);
            WorldKit.FarWater(map, "The bay shore beyond, east", new Rect(OX + Size.x, OZ, 120f, Shore), M.Bay);
        }

        private static Vector2[] Offset(Vector2[] line)
        {
            var o = new Vector2[line.Length];
            for (int i = 0; i < line.Length; i++) o[i] = line[i] + new Vector2(OX, OZ);
            return o;
        }

        /// <summary>
        /// The Quaere, far out in the Cradle: the shape every faith in the basin is built around. Only
        /// a silhouette from here, dark and wrong-shaped, taller than the cathedral, with a seam of
        /// pale light down it. Nothing about it is walkable; it is meant to be looked at from the Watch.
        /// </summary>
        private static void BuildQuaere(Transform map)
        {
            Transform q = WorldKit.QuaereSilhouette(map, P(66f, 0f, -48f), 1f, M.QuaereStone, M.QuaereGlow, M.SeaMist);
            Tag(q, "the_quaere_far", "Hero silhouette, seen only from across the bay (40+ tiles, through fog). Undefined on purpose: dark leaning masses, " +
                "no clean shape, a pale seam of light. One low-poly mesh; it must read at a glance from anywhere on the shore.");
        }

        // ------------------------------------------------------------------ outside the walls

        private static void BuildOutside(WorldKit kit, Transform map)
        {
            Transform outside = WorldKit.Group(map, "Outside the walls");
            // Grass on the three landward strips, and the roads coming in to the gates.
            WorldKit.Patch("Grass", outside, X(0), Z(Shore), X(WallW), Z(Size.y), M.Grass);
            WorldKit.Patch("Grass", outside, X(WallW), Z(WallN + 1), X(Size.x), Z(Size.y), M.Grass);
            WorldKit.Patch("Grass", outside, X(WallE + 1), Z(Shore), X(Size.x), Z(WallN + 1), M.Grass);
            WorldKit.Patch("Palmerway", outside, X(0), Z(ProcS), X(WallW), Z(ProcN), M.Dirt, 0.008f);
            WorldKit.Patch("North road", outside, X(97), Z(WallN + 1), X(101), Z(Size.y), M.Dirt, 0.008f);
            WorldKit.Patch("Bay road", outside, X(WallE + 1), Z(46), X(Size.x), Z(50), M.Dirt, 0.008f);
            // Fields north of the wall: strips of crops.
            for (int i = 0; i < 6; i++)
                WorldKit.Patch("Field", outside, X(10 + i * 14), Z(WallN + 2), X(10 + i * 14 + 11), Z(Size.y - 1),
                    i % 2 == 0 ? M.Straw : M.Hedge, 0.006f);

            // The pilgrims' camp along the road: tents, a fire, and a roadside needle where they leave a drop on arrival.
            Transform camp = WorldKit.Group(outside, "Pilgrims' camp");
            foreach (RectInt r in new[] { new RectInt(1, 62, 3, 2), new RectInt(3, 58, 2, 3), new RectInt(1, 80, 3, 2), new RectInt(4, 83, 2, 3), new RectInt(0, 88, 3, 2) })
            {
                Transform tent = WorldKit.Tent(camp, X(r.x), Z(r.y), X(r.xMax), Z(r.yMax), M.Canvas, M.Timber);
                Tag(tent, "pilgrim_tent", "Small canvas ridge tent, patched, a red cloth tied to the pole. Pilgrims sleep outside the walls until they've given blood.");
            }
            WorldKit.Brazier(camp, P(5.5f, 0f, 67.5f), 0.4f, M.Iron, M.Coals, M.Flame, M.Ember);
            Transform needle = WorldKit.Station(camp, "Roadside needle", "cruorian_monument", X(5), Z(78));
            WorldKit.Box("Plinth", needle, needle.position + new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.9f), M.DarkStone);
            WorldKit.Prop("Needle", needle, needle.position + new Vector3(0f, 1.6f, 0f), new Vector3(0.3f, 2f, 0.3f), M.Red);
            WorldKit.Prop("Tip", needle, needle.position + new Vector3(0f, 2.75f, 0f), new Vector3(0.18f, 0.3f, 0.18f), M.Gold, 45f);
            Tag(needle, "roadside_needle", "A small Cruorian monument (the Palmerway needle's little sister): red stone needle on a plinth, gold tip.");

            Transform trees = WorldKit.Group(outside, "Trees");
            foreach (Vector2Int t in new[] { new Vector2Int(2, 100), new Vector2Int(5, 110), new Vector2Int(1, 120), new Vector2Int(6, 45), new Vector2Int(2, 36), new Vector2Int(139, 70), new Vector2Int(141, 90), new Vector2Int(138, 110) })
                Cypress(kit, trees, t.x, t.y);
        }

        /// <summary>A tall, narrow dark cypress (the city's tree: gardens, graves, roadsides). Blocks its tile.</summary>
        private static void Cypress(WorldKit kit, Transform parent, int lx, int lz)
        {
            Transform t = WorldKit.Group(parent, "Cypress");
            var c = new Vector3(X(lx) + 0.5f, 0f, Z(lz) + 0.5f);
            WorldKit.Box("Trunk", t, c + Vector3.up * 0.5f, new Vector3(0.25f, 1f, 0.25f), M.Bark).AddComponent<MapBlocker>();
            float h = kit.Range(4.5f, 6.5f);
            for (int i = 0; i < 4; i++)
            {
                float w = 1f - i * 0.18f;
                WorldKit.Prop("Crown", t, c + Vector3.up * (1f + h * (i + 0.5f) / 4f), new Vector3(w, h / 4f + 0.2f, w), M.Cypress, kit.Range(0f, 90f));
            }
            WorldKit.Fades(t, 0.25f);
            Tag(t, "cypress", "Tall narrow cypress, near-black green. The city's tree: lines gardens, graves and roads. Blocks one tile.");
        }

        // ------------------------------------------------------------------ the walls and gates

        private static void BuildWalls(Transform map)
        {
            Transform walls = WorldKit.Group(map, "City walls");
            const float H = 7f;
            int[] Range(int from, int to)
            {
                var list = new List<int>();
                for (int i = from; i <= to; i++) list.Add(i);
                return list.ToArray();
            }
            Transform w = WorldKit.CityWall(walls, "West wall", X(WallW), Z(Shore), X(WallW), Z(WallN + 1), H, M.Limestone, M.Flag, Range(Z(ProcS), Z(ProcN - 1)));
            Tag(w, "city_wall", "Curtain wall, pale limestone with a darker footing, battlements on both sides of a wall-walk. Modular: a straight 4 m piece, " +
                "a corner, an end. 7 m high, one tile thick.");
            w = WorldKit.CityWall(walls, "North wall", X(WallW), Z(WallN), X(WallE + 1), Z(WallN), H, M.Limestone, M.Flag, Range(X(40), X(46)).Concat(Range(X(97), X(100))));
            Tag(w, "city_wall", null);
            w = WorldKit.CityWall(walls, "East wall", X(WallE), Z(Shore), X(WallE), Z(WallN + 1), H, M.Limestone, M.Flag, Range(Z(46), Z(49)));
            Tag(w, "city_wall", null);

            // Towers: round-topped corner towers with red spires, square wall towers with battlements.
            (int x, int z, bool spire)[] towers =
            {
                (7, 21, true), (7, 126, true), (134, 126, true), (134, 21, true),
                (7, 46, false), (7, 100, false), (20, 126, false), (64, 126, false), (118, 126, false), (134, 100, false), (134, 74, false),
            };
            foreach ((int x, int z, bool spire) in towers)
            {
                Transform t = WorldKit.Tower(walls, spire ? "Corner tower" : "Wall tower", X(x), Z(z), 3, spire ? 11f : 9.5f, M.Limestone,
                    spire ? M.RoofTile : M.Flag, M.Window, spire ? WorldKit.RoofKind.Spire : WorldKit.RoofKind.Battlements, M.DarkStone);
                Tag(t, spire ? "corner_tower" : "wall_tower", spire
                    ? "Corner tower, 3 x 3 tiles, 11 m to the eaves, arrow slits, corbelled top and a tall red-tiled spire with a gold finial."
                    : "Square wall tower, 3 x 3 tiles, 9.5 m, arrow slits, corbels, battlements. Banners hang from it on feast days.");
                if (!spire) WorldKit.Banner(t, new Vector3(X(x) + 1.5f, 8.5f, Z(z) + (z > 120 ? 3.05f : -0.05f)), z > 120 ? 0f : 180f, 4.5f, 1.1f, M.BannerCloth, M.Gold, M.Iron);
            }

            Gate(walls, "West gate (the Pilgrims' Gate)", new Vector3(X(WallW) + 0.5f, 0f, Z(ProcS)), new Vector3(X(WallW) + 0.5f, 0f, Z(ProcN)), alongX: false, outward: -1);
            Gate(walls, "North gate", new Vector3(X(97), 0f, Z(WallN) + 0.5f), new Vector3(X(101), 0f, Z(WallN) + 0.5f), alongX: true, outward: 1);
            Gate(walls, "East gate (the Bay Gate)", new Vector3(X(WallE) + 0.5f, 0f, Z(46)), new Vector3(X(WallE) + 0.5f, 0f, Z(50)), alongX: false, outward: 1);

            // The water gate: an arch over the river where it comes in under the north wall, an iron grille in it.
            Transform waterGate = WorldKit.Group(walls, "Water gate");
            WorldKit.Arch(waterGate, new Vector3(X(40), 0f, Z(WallN) + 0.5f), new Vector3(X(47), 0f, Z(WallN) + 0.5f), 0f, 3.5f, H, 1f, M.Limestone);
            for (float x = X(40) + 0.6f; x < X(47); x += 0.6f)
                WorldKit.Prop("Grille bar", waterGate, new Vector3(x, 1.5f, Z(WallN) + 0.5f), new Vector3(0.08f, 3f, 0.08f), M.Iron);
            WorldKit.Prop("Grille rail", waterGate, new Vector3(X(43) + 0.5f, 1.2f, Z(WallN) + 0.5f), new Vector3(7f, 0.1f, 0.1f), M.Iron);
            Tag(waterGate, "water_gate", "Arched opening in the wall over the river, with a lowered iron grille (the river runs under it).");
        }

        /// <summary>A gatehouse over an opening in a wall from a to b: two tall towers, an arch with a raised portcullis, banners and braziers outside.</summary>
        private static void Gate(Transform parent, string name, Vector3 a, Vector3 b, bool alongX, int outward)
        {
            Transform g = WorldKit.Group(parent, name);
            Vector3 dir = (b - a).normalized;
            Vector3 outDir = alongX ? new Vector3(0f, 0f, outward) : new Vector3(outward, 0f, 0f);
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 edge = side < 0 ? a - dir * 0.5f : b + dir * 0.5f;
                Vector3 c = edge + dir * side * 1.5f;
                int tx = Mathf.FloorToInt(c.x - 1.5f), tz = Mathf.FloorToInt(c.z - 1.5f);
                Transform t = WorldKit.Tower(g, "Gate tower", tx, tz, 3, 12f, M.Limestone, M.RoofTile, M.Window, WorldKit.RoofKind.Spire, M.DarkStone);
                WorldKit.Banner(t, c + outDir * 1.55f + Vector3.up * 10.5f, alongX ? (outward > 0 ? 0f : 180f) : (outward > 0 ? 90f : -90f), 6f, 1.3f, M.BannerCloth, M.Gold, M.Iron);
                WorldKit.Brazier(g, c + outDir * 2.6f, 1.3f, M.Iron, M.Coals, M.Flame, M.Ember);
                WorldKit.BlockTiles(g, "Brazier", Mathf.FloorToInt((c + outDir * 2.6f).x), Mathf.FloorToInt((c + outDir * 2.6f).z),
                    Mathf.FloorToInt((c + outDir * 2.6f).x) + 1, Mathf.FloorToInt((c + outDir * 2.6f).z) + 1);
            }
            WorldKit.Arch(g, a, b, 0f, 4.2f, 9f, 1.4f, M.Limestone);
            WorldKit.Prop("Gate walk", g, (a + b) / 2f + Vector3.up * 9f, alongX ? new Vector3(Vector3.Distance(a, b), 0.3f, 1.6f) : new Vector3(1.6f, 0.3f, Vector3.Distance(a, b)), M.Flag);
            WorldKit.Battlements(g, a + Vector3.up * 9.15f + outDir * 0.7f, b + Vector3.up * 9.15f + outDir * 0.7f, M.Limestone, 0.5f, 0.6f, 0.25f);
            // The portcullis, raised: only its teeth show under the arch.
            for (float t = 0.3f; t < Vector3.Distance(a, b); t += 0.5f)
                WorldKit.Prop("Portcullis tooth", g, a + dir * t + Vector3.up * 3.9f + outDir * 0.3f, new Vector3(0.1f, 0.9f, 0.1f), M.Iron);
            Tag(g, "gatehouse", "Gatehouse: a 4 m arched opening between two 12 m towers with red spires, a raised portcullis, a wall-walk over the arch, " +
                "long red banners outside and braziers either side of the road.");
        }

        // ------------------------------------------------------------------ the Processional Way

        private static void BuildProcessional(Transform map)
        {
            Transform proc = WorldKit.Group(map, "The Processional Way");
            WorldKit.Patch("Paving", proc, X(WallW + 1), Z(ProcS), X(PlazaW), Z(ProcN), M.Flag, 0.006f);
            WorldKit.Prop("Runner", proc, P((WallW + 1 + PlazaW) / 2f, 0.012f, (ProcS + ProcN) / 2f), new Vector3(PlazaW - WallW - 1, 0.01f, 2.6f), M.Runner);
            Tag(proc, "processional_paving", "Large worn flagstones, a long red runner down the middle (cloth, pinned at the edges with bronze).");

            // Banner poles every six tiles along both kerbs, statues at the bridge ends.
            foreach (int x in new[] { 12, 18, 24, 46, 52, 58 })
                foreach (int z in new[] { ProcS - 1, ProcN })
                {
                    Transform pole = WorldKit.BannerPole(proc, X(x), Z(z), 6f, z < ProcS ? 0f : 180f, M.Iron, M.BannerCloth, M.Gold, M.Gold);
                    Tag(pole, "banner_pole", "Iron pole, 6 m, gold finial, a swallow-tailed red banner with Cruor's gold chalice.");
                }
            Transform bridge = WorldKit.StoneBridge(proc, "The Pilgrims' Bridge", X(30), Z(ProcS), X(43), Z(ProcN), alongX: true, M.Flag, M.Limestone, arches: 3);
            Tag(bridge, "pilgrims_bridge", "Three-arched stone bridge, 13 m by 5 m, limestone parapets with coping. A statue stands at each corner.");
            foreach (Vector2Int s in new[] { new Vector2Int(29, ProcS - 1), new Vector2Int(29, ProcN), new Vector2Int(43, ProcS - 1), new Vector2Int(43, ProcN) })
            {
                Transform st = WorldKit.KnightStatue(proc, X(s.x), Z(s.y), s.y < ProcS ? 0f : 180f, 1.25f, M.Limestone, M.DarkStone, M.Red);
                Tag(st, "knight_statue", "A blood-knight in plate, greatsword grounded, hands on the pommel, red-painted tabard and crest. Limestone, on a plinth. ~3 m.");
            }
            Transform sign = WorldKit.Station(proc, "Proclamation", "cruor_proclamation", X(WallW + 2), Z(ProcN));
            WorldKit.Box("Board", sign, sign.position + new Vector3(0f, 1.1f, 0f), new Vector3(0.9f, 1.2f, 0.12f), M.Timber);
            WorldKit.Prop("Notice", sign, sign.position + new Vector3(0f, 1.15f, -0.07f), new Vector3(0.7f, 0.9f, 0.02f), M.Canvas);
            Tag(sign, "notice_board", "Timber notice board under a little roof, layered with Synod proclamations.");
        }

        // ------------------------------------------------------------------ the Plaza of the Keeping

        private static void BuildPlaza(Transform map)
        {
            Transform plaza = WorldKit.Group(map, "Plaza of the Keeping");
            WorldKit.Patch("Paving", plaza, X(PlazaW), Z(PlazaS), X(PlazaE), Z(PlazaN), M.Plaza, 0.006f);
            // A border of dark stone a tile in, so the square reads as one great floor.
            foreach ((Vector3 a, Vector3 b) in new[]
                     {
                         (P(PlazaW + 1, 0.012f, PlazaS + 1), P(PlazaE - 1, 0.012f, PlazaS + 1)), (P(PlazaW + 1, 0.012f, PlazaN - 1), P(PlazaE - 1, 0.012f, PlazaN - 1)),
                         (P(PlazaW + 1, 0.012f, PlazaS + 1), P(PlazaW + 1, 0.012f, PlazaN - 1)), (P(PlazaE - 1, 0.012f, PlazaS + 1), P(PlazaE - 1, 0.012f, PlazaN - 1)),
                     })
                WorldKit.Beam("Border", plaza, a, b, 0.4f, 0.01f, M.DarkStone);
            // The runner: from the cathedral door down to the monument, and on from it to the Watch.
            WorldKit.Patch("Runner", plaza, X(RunW), Z(Monument.y + 5), X(RunE), Z(PlazaN), M.Runner, 0.012f);
            WorldKit.Patch("Runner", plaza, X(RunW), Z(PlazaS), X(RunE), Z(Monument.y - 4), M.Runner, 0.012f);
            Tag(plaza, "plaza_paving", "Pale great-flagstone paving with a dark border; a red runner on the axis from the cathedral door to the Watch.");

            // Braziers and statues line the runner.
            Transform line = WorldKit.Group(plaza, "Avenue");
            foreach (int z in new[] { 42, 50, 72, 80 })
                foreach (int x in new[] { RunW - 3, RunE + 2 })
                {
                    Transform b = WorldKit.Brazier(line, P(x + 0.5f, 0f, z + 0.5f), 1.5f, M.Iron, M.Coals, M.Flame, M.Ember);
                    WorldKit.BlockTiles(b, "Footprint", X(x), Z(z), X(x) + 1, Z(z) + 1);
                    Tag(b, "brazier_standing", "Tall iron brazier on a tripod stand, a wide bowl of coals; always lit in the city. Blocks one tile.");
                }
            foreach (int z in new[] { 46, 76, 84 })
                foreach (int x in new[] { RunW - 5, RunE + 4 })
                {
                    Transform st = WorldKit.KnightStatue(line, X(x), Z(z), x < Axis ? 90f : -90f, 1.5f, M.Limestone, M.DarkStone, M.Red);
                    Tag(st, "knight_statue", null);
                }
            // Banner poles along the plaza's long sides.
            foreach (int z in new[] { 44, 52, 70, 78 })
            {
                Tag(WorldKit.BannerPole(line, X(PlazaW + 2), Z(z), 6f, 90f, M.Iron, M.BannerCloth, M.Gold, M.Gold), "banner_pole", null);
                Tag(WorldKit.BannerPole(line, X(PlazaE - 3), Z(z), 6f, -90f, M.Iron, M.BannerCloth, M.Gold, M.Gold), "banner_pole", null);
            }

            // Buildings lining the square: the chapter house and the hospice on the west, houses of
            // the canons on the east beyond the street.
            Transform sides = WorldKit.Group(plaza, "Plaza buildings");
            Tag(Building(sides, "Chapter house", 48, 79, 60, 87, 9f, M.Grand), "chapter_house", "The Synod's chapter house: grand limestone hall, red string courses, tall lit windows, red-tiled roof. 12 x 8 tiles, 9 m.");
            Tag(Building(sides, "Synod hospice", 52, 39, 60, 51, 8f, M.Grand), "hospice", "The Synod's hospice for the bled: long limestone range, lit windows, red tile. 8 x 12 tiles, 8 m.");
            Tag(Building(sides, "Canons' house", 44, 65, 52, 71, 7.5f, M.Townhouse2), "townhouse_stone", "Stone townhouse of the better sort: limestone, dark trim, red-tiled roof. 3 storeys.");
            Tag(Building(sides, "Canons' house", 53, 65, 59, 71, 7.5f, M.Townhouse2), "townhouse_stone", null);
            Tag(Building(sides, "Canons' house", 40, 79, 47, 87, 7.5f, M.Townhouse2), "townhouse_stone", null);
            Tag(Building(sides, "Harbour master's house", 57, 28, 65, 36, 7f, M.Townhouse2), "townhouse_stone", null);
        }

        private static void BuildMonument(Transform map)
        {
            // The First Monument: the needle every Cruorian monument is drawn from, on a round stepped
            // plinth in the middle of the plaza, with four founts the faithful bleed into. Its founts
            // are working monument founts (Religion), so the city is somewhere to train.
            Transform g = WorldKit.Group(map, "The First Monument");
            Vector3 c = P(Monument.x + 0.5f, 0f, Monument.y + 0.5f);
            WorldKit.Cylinder("Step", g, c + Vector3.up * 0.05f, new Vector3(10.5f, 0.05f, 10.5f), M.DarkStone, collider: false);
            WorldKit.Cylinder("Step", g, c + Vector3.up * 0.14f, new Vector3(8.6f, 0.05f, 8.6f), M.Limestone, collider: false);
            // The object stands on the plinth's south edge, so there is a tile beside it to read from.
            Transform monument = WorldKit.Station(g, "Monument", "cruor_first_monument", X(Monument.x), Z(Monument.y - 1));
            WorldKit.Box("Plinth", monument, c + Vector3.up * 0.9f, new Vector3(2.9f, 1.6f, 2.9f), M.DarkStone); // blocks the middle 3 x 3
            WorldKit.Prop("Plinth cap", monument, c + Vector3.up * 1.8f, new Vector3(3.2f, 0.2f, 3.2f), M.Limestone);
            WorldKit.Prop("Needle", monument, c + Vector3.up * 8.4f, new Vector3(1.3f, 13f, 1.3f), M.Red);
            foreach (float y in new[] { 3f, 7.5f, 12f })
                WorldKit.Prop("Band", monument, c + Vector3.up * y, new Vector3(1.45f, 0.25f, 1.45f), M.Bronze);
            WorldKit.Prop("Tip", monument, c + Vector3.up * 15.4f, new Vector3(0.8f, 0.8f, 0.8f), M.Gold, 45f).transform.rotation = Quaternion.Euler(35f, 45f, 0f);
            // Blood runs in channels from the founts to the plinth, and glows faintly.
            foreach (Vector2Int d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
            {
                Vector2Int f = Monument + d * 4;
                Transform fount = WorldKit.Station(g, "Fount", "monument_fount", X(f.x), Z(f.y));
                Vector3 fc = fount.position;
                WorldKit.Cylinder("Pedestal", fount, fc + new Vector3(0f, 0.35f, 0f), new Vector3(0.5f, 0.35f, 0.5f), M.Limestone);
                WorldKit.Cylinder("Bowl", fount, fc + new Vector3(0f, 0.75f, 0f), new Vector3(0.85f, 0.08f, 0.85f), M.Bronze, collider: false);
                WorldKit.Cylinder("Blood", fount, fc + new Vector3(0f, 0.84f, 0f), new Vector3(0.68f, 0.02f, 0.68f), M.BloodGlow, collider: false);
                FxKit.Drips(fount, fc + new Vector3(0f, 1.3f, 0f), FxKit.ParticleMat("FxBlood", new Color(0.85f, 0.06f, 0.08f, 1f), additive: false));
                WorldKit.Beam("Channel", g, fc + new Vector3(0f, 0.02f, 0f), c + new Vector3(d.x, 0.02f, d.y) * 1.5f, 0.18f, 0.02f, M.BloodGlow);
            }
            FxKit.PointLight(g, "Blood glow", c + Vector3.up * 1.4f, new Color(1f, 0.15f, 0.12f), 2.2f, 6f, flicker: 0.1f);
            FxKit.Motes(g, c + Vector3.up * 2f, 2.5f, M.Mote, new Color(1f, 0.6f, 0.5f, 0.6f));
            Tag(g, "first_monument", "Hero piece. The original Cruorian monument: a 13 m red stone needle banded in bronze with a gold tip, on a round " +
                "two-step plinth; four bronze founts of blood on pedestals around it, linked to the plinth by glowing channels in the floor.");
        }

        // ------------------------------------------------------------------ the Watch

        private static void BuildWatch(Transform map)
        {
            // The terrace at the foot of the plaza, over the bay: a long balustrade where the faithful
            // keep watch on the Quaere, kneeling stones along it, and a beacon tower at each end.
            Transform g = WorldKit.Group(map, "The Watch");
            WorldKit.Patch("Terrace", g, X(66), Z(Shore), X(100), Z(PlazaS), M.Flag, 0.006f);
            for (int i = 0; i < 3; i++) // shallow steps down from the plaza
                WorldKit.Prop("Step", g, P(Axis, 0.03f + i * 0.03f, PlazaS - 0.3f + i * 0.35f), new Vector3(40f, 0.06f, 0.35f), M.Limestone);
            // The balustrade on the quay edge, blocking it (the bay is below).
            WorldKit.Wall(g, "Balustrade", X(66), Z(Shore), X(100), Z(Shore), M.Limestone, 1f, 0.35f);
            for (float x = X(66) + 0.5f; x < X(100); x += 1f)
                WorldKit.Prop("Baluster", g, new Vector3(x, 0.45f, Z(Shore) - 0.25f), new Vector3(0.25f, 0.9f, 0.25f), M.Limestone);
            WorldKit.Prop("Rail", g, new Vector3(X(83), 1.05f, Z(Shore)), new Vector3(34f, 0.15f, 0.5f), M.Limestone);
            Transform rail = WorldKit.Station(g, "The Watch (rail)", "watch_rail", X(82), Z(Shore));
            Object.DestroyImmediate(rail.GetComponent<MapBlocker>()); // you stand on it to keep vigil; the balustrade blocks the edge
            for (int x = 68; x < 99; x += 3)
                if (x < RunW - 1 || x > RunE)
                    WorldKit.Prop("Kneeling stone", g, P(x + 0.5f, 0.08f, Shore + 1.5f), new Vector3(1.6f, 0.16f, 0.5f), M.DarkStone);
            Tag(g, "the_watch", "A long terrace over the bay: flagstones, a limestone balustrade, kneeling stones worn hollow along it. Players kneel here to watch the Quaere.");
            foreach (int x in new[] { 63, 100 })
            {
                Transform t = WorldKit.Tower(g, "Beacon tower", X(x), Z(Shore), 3, 13f, M.Limestone, M.Flag, M.Window, WorldKit.RoofKind.Battlements, M.DarkStone);
                WorldKit.Brazier(t, P(x + 1.5f, 13f, Shore + 1.5f), 0.6f, M.Iron, M.Coals, M.Flame, M.Ember);
                Tag(t, "beacon_tower", "Square sea tower, 13 m, battlemented, with a great beacon brazier burning on top: seen from across the bay.");
            }
        }

        // ------------------------------------------------------------------ the Vigil

        private static void BuildVigil(Transform map)
        {
            // The cathedral, facing south over the plaza to the bay (it was built to face what lies in it).
            // An enterable nave with a red runner to the high altar under a crossing spire; aisles,
            // transepts and an apse around it; a west front (on the south) of two great spired towers,
            // a deep portal and a rose window that glows red; stained glass the length of the nave.
            Transform g = WorldKit.Group(map, "The Vigil (cathedral)");
            const int nx0 = 75, nx1 = 90, nz0 = 94, nz1 = 120; // the nave you walk in
            const float nh = 14f;

            Transform nave = WorldKit.Hall(g, "Nave", X(nx0), Z(nz0), X(nx1), Z(nz1), nh, M.Church, M.Flag,
                new WorldKit.Door(WorldKit.Side.South, X(81), X(83)));
            Tag(nave, "vigil_nave", "Hero building, the nave: 15 x 26 tiles, 14 m walls, steep slate roof. Inside: red runner, pews, high altar, " +
                "shafts of light from the clerestory, red light through stained glass.");
            WorldKit.Patch("Runner", nave, X(RunW), Z(nz0), X(RunE), Z(nz1 - 3), M.Runner, 0.03f);
            // Pews either side of the runner, in rows (each row blocks its tiles).
            for (int z = nz0 + 4; z <= nz1 - 8; z += 2)
                foreach ((int a, int b) in new[] { (nx0 + 1, RunW - 1), (RunE + 1, nx1 - 1) })
                {
                    Transform pew = WorldKit.Group(nave, "Pew");
                    WorldKit.BlockTiles(pew, "Footprint", X(a), Z(z), X(b), Z(z) + 1);
                    WorldKit.Prop("Seat", pew, P((a + b) / 2f, 0.45f, z + 0.5f), new Vector3(b - a - 0.2f, 0.1f, 0.5f), M.Timber);
                    WorldKit.Prop("Back", pew, P((a + b) / 2f, 0.75f, z + 0.85f), new Vector3(b - a - 0.2f, 0.6f, 0.08f), M.Timber);
                    Tag(pew, "pew", "Plain dark-oak pew, about 4 m long.");
                }
            // The high altar, a gold reredos behind it, and a great chalice.
            Transform altar = WorldKit.Station(nave, "High altar", "vigil_altar", X(82), Z(nz1 - 3));
            WorldKit.Box("Altar", altar, P(Axis, 0.55f, nz1 - 2.5f), new Vector3(2.9f, 1.1f, 0.95f), M.Limestone);
            WorldKit.Prop("Altar cloth", altar, P(Axis, 1.12f, nz1 - 2.5f), new Vector3(3.1f, 0.05f, 1.05f), M.Red);
            WorldKit.Prop("Reredos", altar, P(Axis, 3.5f, nz1 - 0.4f), new Vector3(7f, 7f, 0.3f), M.Gold);
            WorldKit.Prop("Reredos panel", altar, P(Axis, 4f, nz1 - 0.6f), new Vector3(2.4f, 4.5f, 0.1f), M.Red);
            WorldKit.Cylinder("Chalice bowl", altar, P(Axis, 1.6f, nz1 - 2.5f), new Vector3(0.6f, 0.22f, 0.6f), M.Gold, collider: false);
            WorldKit.Cylinder("Chalice stem", altar, P(Axis, 1.3f, nz1 - 2.5f), new Vector3(0.12f, 0.15f, 0.12f), M.Gold, collider: false);
            WorldKit.Cylinder("Blood", altar, P(Axis, 1.82f, nz1 - 2.5f), new Vector3(0.5f, 0.01f, 0.5f), M.BloodGlow, collider: false);
            for (int i = -3; i <= 3; i++)
                if (i != 0)
                    WorldKit.Cylinder("Candle", altar, P(Axis + i * 0.6f, 1.3f + Mathf.Abs(i) * 0.05f, nz1 - 2.2f), new Vector3(0.1f, 0.18f + Mathf.Abs(i) * 0.05f, 0.1f), M.Wax, collider: false);
            Tag(altar, "high_altar", "High altar: limestone block, red cloth, a great gold chalice brimming with blood, candles; a 7 m gold reredos with a red panel behind.");
            FxKit.PointLight(nave, "Altar candles", P(Axis, 2.2f, nz1 - 3.2f), new Color(1f, 0.7f, 0.4f), 3f, 7f, flicker: 0.15f);
            // Light falls down the nave from the clerestory, and the stained glass casts red.
            Transform light = WorldKit.Group(nave, "Light");
            foreach (int z in new[] { 100, 110 })
            {
                (Light l, Renderer _) = FxKit.Shaft(light, P(Axis, 0f, z), 13f, 1.4f, new Color(1f, 0.85f, 0.65f), M.Beam);
                l.intensity = 30f;
                FxKit.Dust(light, P(Axis, 3f, z), new Vector3(2.5f, 5f, 2.5f), M.Mote);
            }
            foreach (int z in new[] { 98, 106, 114 })
                foreach (float x in new[] { nx0 + 1.2f, nx1 - 1.2f })
                    FxKit.PointLight(light, "Red light", P(x, 5f, z), new Color(1f, 0.15f, 0.12f), 1.6f, 7f);

            // Stained glass: tall red lancets down both sides above the aisle roofs, glowing.
            Transform glass = WorldKit.Group(g, "Stained glass");
            for (int z = nz0 + 2; z < nz1 - 1; z += 3)
                foreach (float x in new[] { nx0 - 0.05f, nx1 + 0.05f })
                {
                    WorldKit.Prop("Lancet", glass, P(x, 10.5f, z + 0.5f), new Vector3(0.12f, 4f, 1f), M.StainedGlass);
                    WorldKit.Prop("Lancet head", glass, P(x, 12.65f, z + 0.5f), new Vector3(0.12f, 0.7f, 0.7f), M.StainedGlass).transform.rotation = Quaternion.Euler(45f, 0f, 0f);
                }
            Tag(glass, "stained_glass_lancet", "Tall pointed window of red stained glass with dark tracery; lit from within so it glows at a distance. ~4.5 m.");

            // Aisles (lower, lean-to roofs), transepts and the apse: solid, around the nave.
            Transform body = WorldKit.Group(g, "Aisles, transepts and apse");
            var aisle = new WorldKit.Facade { Walls = M.Limestone, Roof = M.Slate, Trim = M.DarkStone, Windows = M.StainedGlass, Pitch = 25f };
            Tag(Building(body, "West aisle", 70, 96, nx0, 118, 7f, aisle, WorldKit.RoofKind.Flat), "vigil_aisle", "Cathedral aisle: 7 m limestone wall with red glass windows and a low roof against the nave.");
            Tag(Building(body, "East aisle", nx1, 96, 95, 118, 7f, aisle, WorldKit.RoofKind.Flat), "vigil_aisle", null);
            Tag(Building(body, "West transept", 66, 106, 70, 114, 12f, M.Church), "vigil_transept", "Cathedral transept arm: 12 m, steep slate gable with a rose window in the end wall.");
            Tag(Building(body, "East transept", 95, 106, 99, 114, 12f, M.Church), "vigil_transept", null);
            Tag(Building(body, "Apse", 77, nz1, 88, 126, 12f, M.Church), "vigil_apse", "Cathedral apse behind the altar: 12 m, steep slate roof, tall red windows.");
            // Flying buttresses over the aisles, with pinnacles on their piers.
            Transform buttresses = WorldKit.Group(g, "Buttresses");
            for (int z = nz0 + 3; z < nz1; z += 4)
                foreach (int side in new[] { -1, 1 })
                {
                    float outer = side < 0 ? 69.6f : 95.4f, wall = side < 0 ? nx0 : nx1;
                    WorldKit.Prop("Pier", buttresses, P(outer, 5f, z), new Vector3(0.9f, 10f, 0.9f), M.Limestone);
                    WorldKit.Spire(buttresses, P(outer, 10f, z), 0.9f, 2.6f, M.Limestone, null, 4);
                    WorldKit.Beam("Flyer", buttresses, P(outer, 8.6f, z), P(wall, 12.5f, z), 0.45f, 0.6f, M.Limestone);
                }
            WorldKit.Fades(buttresses, 0.2f);
            Tag(buttresses, "flying_buttress", "Flying buttress: a pier with a pinnacle outside the aisle, an arched flyer to the nave wall at 12.5 m.");

            // The crossing tower and spire over the transept.
            Transform crossing = WorldKit.Group(g, "Crossing tower");
            WorldKit.Prop("Lantern", crossing, P(Axis, nh + 4f, 110f), new Vector3(6f, 8f, 6f), M.Limestone);
            WorldKit.Battlements(crossing, P(Axis - 3f, nh + 8f, 107f), P(Axis + 3f, nh + 8f, 107f), M.Limestone);
            WorldKit.Battlements(crossing, P(Axis - 3f, nh + 8f, 113f), P(Axis + 3f, nh + 8f, 113f), M.Limestone);
            WorldKit.Spire(crossing, P(Axis, nh + 8f, 110f), 4.5f, 16f, M.Slate, M.Gold, 7);
            foreach (int s in new[] { -1, 1 })
            {
                WorldKit.Prop("Lantern glass", crossing, P(Axis + s * 3.03f, nh + 4f, 110f), new Vector3(0.08f, 4f, 2f), M.StainedGlass);
                WorldKit.Prop("Lantern glass", crossing, P(Axis, nh + 4f, 110f + s * 3.03f), new Vector3(2f, 4f, 0.08f), M.StainedGlass);
            }
            WorldKit.Fades(crossing, 0.15f).guardRadius = 4f;
            Tag(crossing, "vigil_crossing_spire", "Crossing tower: an 8 m square lantern with red glass, battlements, and a 16 m slate spire with a gold cross: the tallest thing in Cruor.");

            // The south front: two great towers, the portal, the rose window, and steps down to the plaza.
            Transform front = WorldKit.Group(g, "South front");
            foreach (int tx in new[] { 69, 90 })
            {
                Transform t = WorldKit.Tower(front, "Front tower", X(tx), Z(88), 6, 22f, M.Limestone, M.Slate, M.StainedGlass, WorldKit.RoofKind.Spire, M.DarkStone);
                WorldKit.Banner(t, P(tx + 3f, 18f, 87.9f), 180f, 9f, 2f, M.BannerCloth, M.Gold, M.Iron);
                WorldKit.Fades(t, 0.15f).guardRadius = 4f;
                Tag(t, "vigil_front_tower", "Cathedral front tower: 6 x 6 tiles, 22 m, buttressed corners, red glass slits, a 13 m slate spire; a 9 m red banner hangs on its face.");
            }
            // The screen between the towers: a high gable over the portal.
            WorldKit.Prop("Front screen", front, P(Axis, nh + 3f, 93.6f), new Vector3(nx1 - nx0 - 0.2f, 6f, 0.6f), M.Limestone);
            WorldKit.Prop("Front gable", front, P(Axis, nh + 6f, 93.6f), new Vector3(7f, 7f, 0.6f), M.Limestone).transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            // The rose window over the portal, glowing red behind dark tracery.
            Vector3 rose = P(Axis, 10.5f, 93.62f);
            GameObject roseGlass = WorldKit.Cylinder("Rose window", front, rose, new Vector3(5f, 0.05f, 5f), M.StainedGlass, collider: false);
            roseGlass.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 8f;
                WorldKit.Prop("Tracery", front, rose + new Vector3(0f, 0f, -0.08f), new Vector3(5f, 0.14f, 0.06f), M.DarkStone).transform.rotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            }
            WorldKit.Cylinder("Rose rim", front, rose + new Vector3(0f, 0f, 0.05f), new Vector3(5.6f, 0.1f, 5.6f), M.DarkStone, collider: false).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            FxKit.PointLight(front, "Rose glow", rose + new Vector3(0f, -2f, -3f), new Color(1f, 0.15f, 0.12f), 2.5f, 9f);
            // The portal: three receding arches round the door.
            for (int i = 0; i < 3; i++)
                WorldKit.Arch(front, P(RunW - 0.2f - i * 0.7f, 0f, 93.7f - i * 0.45f), P(RunE + 0.2f + i * 0.7f, 0f, 93.7f - i * 0.45f), 2.2f + i * 0.3f, 2f + i * 0.6f, 6.5f, 0.45f, i == 1 ? M.DarkStone : M.Limestone, 9);
            Transform doors = WorldKit.Group(front, "Doors (open)");
            foreach (int s in new[] { -1, 1 })
                WorldKit.Prop("Door leaf", doors, P(Axis + s * 1.9f, 1.9f, 93.4f), new Vector3(0.12f, 3.8f, 1.5f), M.Red, s * 25f);
            for (int i = 0; i < 3; i++)
                WorldKit.Prop("Step", front, P(Axis, 0.04f + i * 0.04f, 89.2f + i * 0.9f), new Vector3(15f - i, 0.08f, 0.9f), M.Limestone);
            Tag(front, "vigil_front", "The cathedral's south front: a high gable screen between the towers, a 5 m rose window of red glass, a deep three-arch portal with red doors open, and broad steps.");
        }

        // ------------------------------------------------------------------ the Letting House

        private static void BuildLettingHouse(Transform map)
        {
            // Where the faithful give their blood to the Keeping: a long white hall off the plaza, rows
            // of letting chairs each with a bowl, channels in the floor running to a covered cistern at
            // the far end that glows red. Pilgrims queue outside the door.
            Transform g = WorldKit.Group(map, "The Letting House");
            var f = new WorldKit.Facade { Walls = M.Plaster, Roof = M.RoofTile, Trim = M.Red, Gable = M.Plaster, Pitch = 40f };
            Transform hall = WorldKit.Hall(g, "Hall", X(49), Z(52), X(60), Z(64), 6f, f, M.Flag, new WorldKit.Door(WorldKit.Side.East, Z(57), Z(58)));
            Tag(hall, "letting_house", "Long whitewashed hall, red string course, red-tiled roof, wide double door onto the plaza; clean, bright, clinical.");
            for (int i = 0; i < 6; i++)
            {
                int z = 53 + (i % 3) * 3 + (i >= 3 ? 1 : 0), x = i < 3 ? 51 : 56;
                z = i < 3 ? 53 + i * 3 : 54 + (i - 3) * 3;
                Transform chair = WorldKit.Station(hall, "Letting chair", "letting_chair", X(x), Z(z));
                Vector3 c = chair.position;
                WorldKit.Box("Seat", chair, c + new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.9f, 0.7f), M.Timber);
                WorldKit.Prop("Arm rest", chair, c + new Vector3(0.45f, 0.75f, 0f), new Vector3(0.2f, 0.08f, 0.8f), M.Timber);
                WorldKit.Cylinder("Bowl", chair, c + new Vector3(0.45f, 0.15f, 0.3f), new Vector3(0.35f, 0.05f, 0.35f), M.Bronze, collider: false);
                WorldKit.Beam("Channel", hall, c + new Vector3(0.45f, 0.03f, 0.3f), P(x < 54 ? 53.5f : 55.5f, 0.03f, z + 0.5f), 0.12f, 0.02f, M.BloodGlow);
                Tag(chair, "letting_chair", "Leather letting chair with an arm strap, a copper bowl beneath that drains into a floor channel.");
            }
            // The central channel and the cistern at the west end.
            WorldKit.Prop("Floor channel", hall, P(54.5f, 0.035f, 58f), new Vector3(0.3f, 0.02f, 11f), M.BloodGlow);
            Transform cistern = WorldKit.Group(hall, "Cistern");
            WorldKit.BlockTiles(cistern, "Footprint", X(50), Z(57), X(52), Z(59));
            WorldKit.Cylinder("Rim", cistern, P(51f, 0.35f, 58f), new Vector3(1.9f, 0.35f, 1.9f), M.Limestone, collider: false);
            WorldKit.Cylinder("Blood", cistern, P(51f, 0.71f, 58f), new Vector3(1.6f, 0.02f, 1.6f), M.BloodGlow, collider: false);
            WorldKit.Prop("Grille", cistern, P(51f, 0.73f, 58f), new Vector3(1.4f, 0.02f, 0.1f), M.Iron);
            FxKit.PointLight(cistern, "Cistern glow", P(51f, 1.5f, 58f), new Color(1f, 0.12f, 0.1f), 2f, 5f, flicker: 0.1f);
            Tag(cistern, "letting_cistern", "Round covered cistern collecting the city's given blood; an iron grille over a glowing red surface.");
            FxKit.PointLight(hall, "Lamps", P(54.5f, 4f, 58f), new Color(1f, 0.85f, 0.7f), 2.5f, 9f);
            // The queue's rope line outside the door.
            for (int z = 56; z <= 59; z += 3)
                WorldKit.Prop("Rope post", g, P(61.5f, 0.45f, z + 0.5f), new Vector3(0.12f, 0.9f, 0.12f), M.Bronze);
            WorldKit.Beam("Rope", g, P(61.5f, 0.8f, 56.5f), P(61.5f, 0.8f, 59.5f), 0.04f, 0.04f, M.Red);
        }

        // ------------------------------------------------------------------ the knights' yard

        private static void BuildKnightsYard(Transform map)
        {
            // The blood-knights' barracks: a walled yard east of the plaza with a sparring ground,
            // dummies, weapon racks, the barracks hall, the armoury and stables.
            Transform g = WorldKit.Group(map, "Knights' yard");
            WorldKit.Patch("Sparring ground", g, X(110), Z(58), X(127), Z(73), M.Dirt, 0.008f);
            int[] gate = { Z(68), Z(69), Z(70) };
            Tag(WorldKit.CityWall(g, "Yard wall W", X(108), Z(56), X(108), Z(85), 3.5f, M.Limestone, M.Flag, gate), "yard_wall", "Low battlemented wall round the knights' yard, 3.5 m.");
            Tag(WorldKit.CityWall(g, "Yard wall S", X(108), Z(56), X(WallE), Z(56), 3.5f, M.Limestone, M.Flag), "yard_wall", null);
            Tag(WorldKit.CityWall(g, "Yard wall N", X(108), Z(85), X(WallE), Z(85), 3.5f, M.Limestone, M.Flag), "yard_wall", null);
            foreach (int z in new[] { 67, 71 })
                Tag(WorldKit.BannerPole(g, X(106), Z(z), 5f, -90f, M.Iron, M.BannerCloth, M.Gold, M.Gold), "banner_pole", null);
            for (int i = 0; i < 5; i++)
            {
                Transform d = WorldKit.Dummy(g, X(112 + i * 3), Z(60), 0f, M.Timber, M.Straw);
                Tag(d, "training_dummy", "Straw training dummy on a post with a crossbar, a sack head; hacked about.");
            }
            for (int i = 0; i < 4; i++)
                Tag(WorldKit.WeaponRack(g, X(110 + i * 2), Z(74), 0f, M.Timber, M.Iron), "weapon_rack", "Timber rack of practice swords and maces.");
            // A roped sparring ring in the middle.
            Transform ring = WorldKit.Group(g, "Sparring ring");
            var rc = P(119f, 0f, 66.5f);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f, b = (i + 1) / 8f * Mathf.PI * 2f;
                Vector3 pa = rc + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4f, pb = rc + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * 4f;
                WorldKit.Prop("Post", ring, pa + Vector3.up * 0.5f, new Vector3(0.14f, 1f, 0.14f), M.Timber);
                if (i != 4) WorldKit.Beam("Rope", ring, pa + Vector3.up * 0.85f, pb + Vector3.up * 0.85f, 0.04f, 0.04f, M.Red);
            }
            Tag(ring, "sparring_ring", "Octagonal roped ring on the sand, 8 m across, red rope, one side open.");
            Tag(Building(g, "Barracks", 120, 75, WallE, 85, 8f, M.Fort, WorldKit.RoofKind.Battlements), "barracks", "Barracks hall: plain fortified limestone block, battlemented roof, small windows. 16 x 10 tiles, 8 m.");
            Tag(Building(g, "Armoury", 109, 76, 118, 85, 6f, M.Warehouse), "armoury", "Armoury: squat dark-stone store, heavy door, red-tiled roof.");
            var stable = new WorldKit.Facade { Walls = M.Timber, Roof = M.RoofTile, Trim = M.DarkStone, Pitch = 30f };
            Tag(Building(g, "Stables", 129, 57, WallE, 72, 4f, stable), "stables", "Timber stable range with a red-tiled roof, half-doors along the yard side.");
            WorldKit.Brazier(g, P(127.5f, 0f, 74.5f), 1.2f, M.Iron, M.Coals, M.Flame, M.Ember);
            WorldKit.BlockTiles(g, "Brazier", X(127), Z(74), X(128), Z(75));
        }

        // ------------------------------------------------------------------ the Pontiff's palace

        private static void BuildPalace(WorldKit kit, Transform map)
        {
            // The Pontiff of Cruor's palace in the north-east: a grand red-roofed range with corner
            // towers behind a formal garden of cypress and clipped hedges round a fountain.
            Transform g = WorldKit.Group(map, "The Pontiff's palace");
            Tag(Building(g, "Palace", 108, 108, 131, 125, 11f, M.Grand), "pontiff_palace", "The Pontiff's palace: a grand three-storey limestone range, red string courses, lit windows, red-tiled roof. 23 x 17 tiles, 11 m.");
            foreach ((int x, int z) in new[] { (106, 106), (130, 106), (106, 124), (130, 124) })
                Tag(WorldKit.Tower(g, "Palace tower", X(x), Z(z), 3, 14f, M.Limestone, M.RoofTile, M.WindowLit, WorldKit.RoofKind.Spire, M.Red), "palace_tower",
                    "Palace corner tower: 14 m, lit windows, a tall red-tiled spire with a gold finial.");
            WorldKit.Patch("Garden", g, X(104), Z(89), X(WallE), Z(105), M.Grass, 0.008f);
            WorldKit.Patch("Garden walk", g, X(117), Z(89), X(120), Z(108), M.Flag, 0.012f);
            WorldKit.Patch("Garden walk", g, X(104), Z(96), X(WallE), Z(98), M.Flag, 0.012f);
            Transform garden = WorldKit.Group(g, "Garden");
            foreach ((int x0, int z0, int x1, int z1) in new[] { (106, 91, 115, 94), (122, 91, 133, 94), (106, 100, 115, 104), (122, 100, 133, 104) })
            {
                Transform hedge = WorldKit.Group(garden, "Hedge");
                WorldKit.Box("Hedge", hedge, P((x0 + x1) / 2f, 0.5f, (z0 + z1) / 2f), new Vector3(x1 - x0 - 0.1f, 1f, z1 - z0 - 0.1f), M.Hedge).AddComponent<MapBlocker>();
                Tag(hedge, "clipped_hedge", "Clipped box hedge, 1 m high, in square beds.");
            }
            foreach (int x in new[] { 105, 115, 121, 132 })
                foreach (int z in new[] { 90, 99 })
                    Cypress(kit, garden, x, z);
            Transform fountain = WorldKit.Group(garden, "Fountain");
            WorldKit.BlockTiles(fountain, "Footprint", X(117), Z(96), X(120), Z(98));
            WorldKit.Cylinder("Basin", fountain, P(118.5f, 0.3f, 97f), new Vector3(3f, 0.3f, 3f), M.Limestone, collider: false);
            WorldKit.Cylinder("Water", fountain, P(118.5f, 0.58f, 97f), new Vector3(2.7f, 0.02f, 2.7f), M.River, collider: false);
            WorldKit.Cylinder("Column", fountain, P(118.5f, 1.2f, 97f), new Vector3(0.4f, 0.8f, 0.4f), M.Limestone, collider: false);
            WorldKit.Cylinder("Upper bowl", fountain, P(118.5f, 2f, 97f), new Vector3(1.2f, 0.1f, 1.2f), M.Limestone, collider: false);
            Tag(fountain, "garden_fountain", "Two-tier limestone fountain in a round basin.");
        }

        // ------------------------------------------------------------------ the quays

        private static void BuildQuays(WorldKit kit, Transform map)
        {
            Transform g = WorldKit.Group(map, "The quays");
            WorldKit.Patch("Quay", g, X(WallW + 1), Z(Shore), X(46), Z(Shore + 5), M.Flag, 0.006f);
            WorldKit.Patch("Quay", g, X(57), Z(Shore), X(66), Z(Shore + 5), M.Flag, 0.006f);
            WorldKit.Patch("Quay", g, X(100), Z(Shore), X(WallE), Z(Shore + 5), M.Flag, 0.006f);
            WorldKit.Prop("Quay edge", g, P(72f, -0.2f, Shore - 0.15f), new Vector3(WallE - WallW, 0.6f, 0.3f), M.DarkStone);
            Tag(g, "quay", "Stone quay: big flagstones, a dark stone edge, iron mooring rings and bollards.");
            Transform bridge = WorldKit.StoneBridge(g, "Quay bridge", X(46), Z(Shore), X(57), Z(Shore + 5), alongX: true, M.Flag, M.Limestone, arches: 2);
            Tag(bridge, "quay_bridge", "Low two-arched stone bridge carrying the quay road over the river mouth.");
            foreach (int x in new[] { 14, 30, 60, 104, 118, 130 })
            {
                Transform bollard = WorldKit.Group(g, "Bollard");
                WorldKit.Box("Bollard", bollard, P(x + 0.5f, 0.35f, Shore + 0.5f), new Vector3(0.35f, 0.7f, 0.35f), M.Iron).AddComponent<MapBlocker>();
                Tag(bollard, "bollard", "Iron mooring bollard.");
            }

            // Jetties out into the bay, with boats alongside.
            foreach (int jx in new[] { 20, 112, 124 })
            {
                Transform j = WorldKit.Group(g, "Jetty");
                WorldKit.Box("Deck", j, P(jx + 1f, 0.1f, (7 + Shore) / 2f), new Vector3(2f, 0.15f, Shore - 7), M.Planks);
                for (int z = 7; z < Shore; z += 3)
                    foreach (float x in new[] { jx + 0.1f, jx + 1.9f })
                        WorldKit.Cylinder("Pile", j, P(x, -0.2f, z + 0.5f), new Vector3(0.25f, 0.6f, 0.25f), M.Hull, collider: false);
                Tag(j, "jetty", "Timber jetty on piles, 2 tiles wide, 15 m out into the bay.");
            }
            Tag(WorldKit.Boat(g, P(17.5f, 0f, 14f), 7f, 2.2f, 0f, M.Hull, M.Planks, M.Canvas), "fishing_boat", "Small fishing boat, single mast, furled sail.");
            Tag(WorldKit.Boat(g, P(24f, 0f, 12f), 6f, 2f, 10f, M.Hull, M.Planks, M.Canvas), "fishing_boat", null);
            Tag(WorldKit.Boat(g, P(109f, 0f, 13f), 14f, 4f, 0f, M.Hull, M.Planks, M.BannerCloth), "galley", "Grain barge / galley, 14 m, one mast with a red furled sail, sterncastle.");
            Tag(WorldKit.Boat(g, P(128.5f, 0f, 12f), 13f, 3.8f, 0f, M.Hull, M.Planks, M.Canvas), "galley", null);
            Tag(WorldKit.Boat(g, P(140f, 0f, 6f), 16f, 4.5f, 70f, M.Hull, M.Planks, M.Canvas), "galley", null);
            Tag(WorldKit.Crane(g, X(111), Z(Shore + 2), 180f, M.Timber, M.Iron), "quay_crane", "Timber treadwheel crane, 5 m post, jib out over the water, rope and a hanging load.");
            Tag(WorldKit.Crane(g, X(126), Z(Shore + 2), 180f, M.Timber, M.Iron), "quay_crane", null);
            foreach (Vector2Int c in new[] { new Vector2Int(102, 26), new Vector2Int(116, 25), new Vector2Int(121, 26), new Vector2Int(133, 25), new Vector2Int(11, 25), new Vector2Int(34, 26) })
                kit.Clutter(g, X(c.x), Z(c.y), M.Planks, M.Hull);

            // Warehouses along the east quay.
            Transform wh = WorldKit.Group(g, "Warehouses");
            foreach ((int x0, int x1) in new[] { (104, 112), (115, 123), (126, WallE) })
            {
                Transform w = Building(wh, "Warehouse", x0, 29, x1, 40, 7f, M.Warehouse);
                WorldKit.Prop("Loading door", w, P((x0 + x1) / 2f, 1.4f, 28.97f), new Vector3(2.4f, 2.8f, 0.06f), M.Timber);
                WorldKit.Prop("Hoist beam", w, P((x0 + x1) / 2f, 6.2f, 28.5f), new Vector3(0.25f, 0.25f, 1.2f), M.Timber);
                Tag(w, "warehouse", "Grain warehouse: dark stone, low-pitched red roof, a big timber loading door with a hoist beam above. 8-10 x 11 tiles, 7 m.");
            }
        }

        // ------------------------------------------------------------------ the bay road: counting-house and consulate

        private static void BuildEastRoad(Transform map)
        {
            Transform g = WorldKit.Group(map, "The bay road");
            WorldKit.Patch("Road", g, X(PlazaE), Z(44), X(WallE), Z(50), M.Flag, 0.006f);
            // The counting-house of Tithe in Cruor: Cruor runs on Tithian loans, and this is where they're written.
            var f = new WorldKit.Facade { Walls = M.Plaster, Roof = M.Slate, Trim = M.Gold, Gable = M.Plaster, Pitch = 34f };
            Transform house = WorldKit.Hall(g, "Tithe counting-house", X(109), Z(50), X(119), Z(56), 5f, f, M.Flag, new WorldKit.Door(WorldKit.Side.South, X(113), X(114)));
            Tag(house, "tithe_counting_house", "Tithe's counting-house in Cruor: whitewashed (Tithe white, not Cruor red), slate roof, gold trim; a counter and ledgers inside. The city's bank.");
            Transform box = WorldKit.Station(house, "Strongbox", "synod_strongbox", X(111), Z(54));
            WorldKit.Box("Chest", box, box.position + new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.8f, 0.7f), M.Iron);
            WorldKit.Prop("Bands", box, box.position + new Vector3(0f, 0.45f, 0f), new Vector3(0.95f, 0.1f, 0.75f), M.Gold);
            Transform counter = WorldKit.Group(house, "Counter");
            WorldKit.BlockTiles(counter, "Footprint", X(113), Z(54), X(118), Z(55));
            WorldKit.Prop("Counter", counter, P(115.5f, 0.55f, 54.5f), new Vector3(5f, 1.1f, 0.8f), M.Timber);
            for (int i = 0; i < 4; i++)
                WorldKit.Prop("Ledger", counter, P(113.6f + i * 1.2f, 1.15f, 54.5f), new Vector3(0.5f, 0.1f, 0.35f), M.Red);
            Tag(counter, "counting_counter", "Long counter with chained ledgers.");

            // The Concordian consulate: a narrow tower with a brass sky-glass on its roof, and one at the door.
            Tag(Building(g, "Concordian consulate", 122, 50, 128, 55, 6f, M.Townhouse2), "concord_consulate",
                "Concordia's consulate: tidy stone house in Rationalist blue-grey trim (blockout uses Cruor stone), brass fittings.");
            Transform tower = WorldKit.Tower(g, "Consulate tower", X(128), Z(50), 3, 11f, M.Limestone, M.Flag, M.Window, WorldKit.RoofKind.Flat, M.DarkStone);
            WorldKit.Beam("Telescope", tower, P(129.5f, 11.8f, 51.5f), P(129.5f, 13.4f, 49.2f), 0.45f, 0.45f, M.Bronze);
            WorldKit.Prop("Mount", tower, P(129.5f, 11.4f, 51.5f), new Vector3(0.4f, 0.8f, 0.4f), M.Bronze);
            Tag(tower, "concord_observation_tower", "Narrow observation tower with a big brass telescope on the roof, aimed over the bay. Out of place in Cruor on purpose.");
            Transform glass = WorldKit.Station(g, "Sky-glass", "concord_skyglass", X(125), Z(49));
            WorldKit.Prop("Tripod", glass, glass.position + new Vector3(0f, 0.6f, 0f), new Vector3(0.1f, 1.2f, 0.1f), M.Bronze);
            WorldKit.Box("Glass", glass, glass.position + new Vector3(0f, 1.3f, -0.1f), new Vector3(0.18f, 0.18f, 0.9f), M.Bronze).transform.rotation = Quaternion.Euler(-15f, 0f, 0f);
            Tag(glass, "sky_glass", "Brass telescope on a tripod in the street, aimed at the Quaere.");
        }

        // ------------------------------------------------------------------ the market

        private static void BuildMarket(WorldKit kit, Transform map)
        {
            // Just inside the Pilgrims' Gate: stalls selling bandages, relic-phials, bread and candles to pilgrims.
            Transform g = WorldKit.Group(map, "Pilgrims' market");
            WorldKit.Patch("Market square", g, X(WallW + 1), Z(ProcN), X(32), Z(92), M.Flag, 0.006f);
            for (int i = 0; i < 6; i++)
            {
                Transform s = WorldKit.Stall(g, X(11 + i * 3), Z(80), 2, 2, WorldKit.Side.North, M.Timber, i % 2 == 0 ? M.Awning : M.AwningAlt, M.Goods);
                Tag(s, "market_stall", "Market stall: timber counter, four posts, a striped awning (red or cream) sloping to the front, goods on the counter.");
                Tag(WorldKit.Stall(g, X(11 + i * 3), Z(86), 2, 2, WorldKit.Side.South, M.Timber, i % 2 == 0 ? M.AwningAlt : M.Awning, M.Goods), "market_stall", null);
            }
            Transform well = WorldKit.Group(g, "Market well");
            WorldKit.BlockTiles(well, "Footprint", X(28), Z(88), X(30), Z(90));
            WorldKit.Cylinder("Well", well, P(29f, 0.45f, 89f), new Vector3(1.8f, 0.45f, 1.8f), M.Limestone, collider: false);
            foreach (int s in new[] { -1, 1 })
                WorldKit.Prop("Upright", well, P(29f + s * 0.8f, 1.3f, 89f), new Vector3(0.15f, 1.7f, 0.15f), M.Timber);
            WorldKit.Prop("Winch", well, P(29f, 2.1f, 89f), new Vector3(1.8f, 0.15f, 0.15f), M.Timber);
            GableRoofSmall(well, P(29f, 2.2f, 89f));
            Tag(well, "town_well", "Round stone well with a timber winch frame and a little tiled roof.");
            kit.Clutter(g, X(10), Z(84), M.Planks, M.Hull);
            kit.Clutter(g, X(30), Z(80), M.Planks, M.Hull);

            // The tavern on the square: the Opened Vein, where pilgrims go after they've given.
            var f = new WorldKit.Facade { Walls = M.Plaster, Roof = M.RoofTile, Trim = M.Timber, Gable = M.Plaster, Pitch = 42f };
            Transform tavern = WorldKit.Hall(g, "Tavern (the Opened Vein)", X(10), Z(93), X(22), Z(100), 4.5f, f, M.Planks, new WorldKit.Door(WorldKit.Side.South, X(15), X(16)));
            Transform bar = WorldKit.Group(tavern, "Bar");
            WorldKit.BlockTiles(bar, "Footprint", X(11), Z(97), X(16), Z(98));
            WorldKit.Prop("Bar", bar, P(13.5f, 0.55f, 97.5f), new Vector3(5f, 1.1f, 0.8f), M.Timber);
            for (int i = 0; i < 3; i++)
            {
                Transform table = WorldKit.Group(tavern, "Table");
                WorldKit.BlockTiles(table, "Footprint", X(17 + (i % 2) * 2), Z(94 + i * 2), X(18 + (i % 2) * 2), Z(95 + i * 2));
                WorldKit.Prop("Table", table, P(17.5f + (i % 2) * 2, 0.4f, 94.5f + i * 2), new Vector3(0.9f, 0.8f, 0.9f), M.Timber);
            }
            FxKit.PointLight(tavern, "Hearth", P(21f, 1f, 96.5f), new Color(1f, 0.55f, 0.25f), 2.5f, 6f, flicker: 0.3f);
            WorldKit.Prop("Sign", tavern, P(15f, 3.2f, 92.6f), new Vector3(1.2f, 0.8f, 0.06f), M.Red);
            Tag(tavern, "tavern", "The Opened Vein: half-timbered tavern, plaster and red tile, a hanging sign of a red drop; inside a bar, tables, a hearth.");
        }

        private static void GableRoofSmall(Transform parent, Vector3 at) =>
            WorldKit.GableRoof(parent, at.x - 1.1f, at.z - 0.7f, at.x + 1.1f, at.z + 0.7f, at.y, M.RoofTile, null, 35f, 0.1f);

        // ------------------------------------------------------------------ the lower town and the rows

        private static void BuildTown(WorldKit kit, Transform map)
        {
            // Narrow plastered townhouses packed along lanes: the lower town south of the Processional
            // Way, the north-west rows, and the houses between the river and the cathedral.
            Transform g = WorldKit.Group(map, "Townhouses");
            (int x0, int z0, int x1, int z1)[] houses =
            {
                // the lower town
                (10, 29, 16, 36), (18, 29, 25, 36), (10, 40, 16, 48), (19, 40, 26, 48), (28, 40, 34, 47), (10, 52, 16, 60), (19, 52, 26, 60), (28, 51, 34, 58),
                (10, 63, 18, 71), (21, 63, 28, 71), (30, 62, 35, 70),
                // the north-west rows
                (10, 102, 17, 110), (20, 102, 27, 110), (10, 113, 17, 124), (20, 113, 30, 124), (24, 93, 31, 100),
                // between the river and the cathedral
                (48, 88, 55, 95), (61, 88, 66, 97), (47, 104, 55, 112), (61, 104, 66, 112), (47, 116, 55, 126), (61, 116, 66, 126), (40, 90, 44, 99),
                // the upper town's street to the north gate
                (102, 88, 104, 97),
            };
            foreach ((int x0, int z0, int x1, int z1) in houses)
            {
                bool stone = kit.Chance(0.35);
                float eave = Storey(kit, stone);
                Transform h = Building(g, stone ? "Stone townhouse" : "Townhouse", x0, z0, x1, z1, eave, stone ? M.Townhouse2 : M.Townhouse);
                if (stone) Tag(h, "townhouse_stone", null);
                else Tag(h, "townhouse_plaster", "Narrow townhouse: whitewashed plaster over timber, timber storey bands, small dark windows, steep red-tiled roof. 2-3 storeys.");
            }
            // The shuttered apothecary in the lower town (an occult house's foothold, behind a painted-over sign).
            var shopFacade = new WorldKit.Facade { Walls = M.Plaster, Roof = M.RoofTile, Trim = M.Timber, Windows = M.Window, Gable = M.Plaster };
            Transform shop = Building(g, "Shuttered apothecary", 28, 29, 34, 35, 5f, shopFacade);
            Transform door = WorldKit.Station(shop, "Shuttered shop", "cruor_shuttered_shop", X(30), Z(28));
            Object.DestroyImmediate(door.GetComponent<MapBlocker>()); // you knock from the lane
            WorldKit.Prop("Shutters", shop, P(31f, 1.3f, 28.95f), new Vector3(2.5f, 1.6f, 0.08f), M.Hull);
            WorldKit.Prop("Painted-over sign", shop, P(31f, 3f, 28.9f), new Vector3(1.4f, 0.6f, 0.06f), M.Plaster);
            Tag(shop, "shuttered_apothecary", "A small apothecary with new shutters and a painted-over sign; one window faintly green at night. Somebody occult lives here.");

            // Drying racks and nets on the west quay, for the fishermen.
            Transform racks = WorldKit.Group(map, "Fish racks");
            foreach (int x in new[] { 26, 36, 40 })
            {
                Transform r = WorldKit.Group(racks, "Drying rack");
                WorldKit.BlockTiles(r, "Footprint", X(x), Z(Shore + 3), X(x + 2), Z(Shore + 4));
                foreach (int s in new[] { 0, 2 })
                    WorldKit.Prop("Post", r, P(x + s, 0.8f, Shore + 3.5f), new Vector3(0.1f, 1.6f, 0.1f), M.Timber);
                WorldKit.Prop("Pole", r, P(x + 1f, 1.5f, Shore + 3.5f), new Vector3(2.1f, 0.06f, 0.06f), M.Timber);
                WorldKit.Prop("Net", r, P(x + 1f, 1.1f, Shore + 3.5f), new Vector3(1.9f, 0.8f, 0.03f), M.Goods);
                Tag(r, "drying_rack", "Fish-drying rack with nets hung over a pole.");
            }

            // Street lamps along the main streets (glowing lanterns, no lights of their own).
            Transform lamps = WorldKit.Group(map, "Street lamps");
            foreach (Vector2 p in new[]
                     {
                         new Vector2(15f, 71.5f), new Vector2(27f, 71.5f), new Vector2(49f, 77.5f), new Vector2(55f, 71.5f), new Vector2(101.5f, 90f), new Vector2(101.5f, 104f),
                         new Vector2(101.5f, 118f), new Vector2(108f, 49.8f), new Vector2(120f, 49.8f), new Vector2(132f, 44.2f), new Vector2(12f, 26.8f), new Vector2(40f, 26.8f),
                     })
                Tag(WorldKit.Lamp(lamps, P(p.x, 0f, p.y), M.Iron, M.WindowLit), "street_lamp", "Iron street lamp, 2.8 m, a hanging lantern with warm glass.");
        }

        private static float Storey(WorldKit kit, bool stone) => stone ? kit.Range(6.5f, 8.2f) : kit.Range(4.8f, 7.6f);

        // ------------------------------------------------------------------ the aqueduct

        private static void BuildAqueduct(Transform map)
        {
            // Water for the city's fountains comes in from the hills on arches over the north wall,
            // down to the conduit house west of the cathedral. Streets run under the arches.
            Transform g = WorldKit.Group(map, "Aqueduct");
            const float x = 58.5f, top = 10f;
            int[] piers = { 102, 108, 114, 120, 126, 132, 138 };
            foreach (int z in piers)
            {
                Transform pier = WorldKit.Group(g, "Pier");
                if (z < Size.y) WorldKit.Box("Pier", pier, P(x, top / 2f, z + 0.5f), new Vector3(1.8f, top, 0.9f), M.Limestone).AddComponent<MapBlocker>();
                else WorldKit.Prop("Pier", pier, P(x, top / 2f, z + 0.5f), new Vector3(1.8f, top, 0.9f), M.Limestone);
                WorldKit.Prop("Impost", pier, P(x, 6.2f, z + 0.5f), new Vector3(2f, 0.3f, 1.1f), M.DarkStone);
            }
            for (int i = 0; i + 1 < piers.Length; i++)
                WorldKit.Arch(g, P(x, 0f, piers[i] + 0.95f), P(x, 0f, piers[i + 1] + 0.05f), 6.2f, 2.4f, top, 1.8f, M.Limestone, 7);
            WorldKit.Prop("Channel", g, P(x, top + 0.3f, (piers[0] + piers[piers.Length - 1]) / 2f + 0.5f), new Vector3(1.9f, 0.6f, piers[piers.Length - 1] - piers[0] + 1f), M.Limestone);
            WorldKit.Prop("Water", g, P(x, top + 0.58f, (piers[0] + piers[piers.Length - 1]) / 2f + 0.5f), new Vector3(1.2f, 0.05f, piers[piers.Length - 1] - piers[0] + 1f), M.River);
            WorldKit.Fades(g, 0.2f);
            Tag(g, "aqueduct", "Aqueduct: limestone piers every 6 m carrying round arches 8.6 m high and an open water channel at 10 m. Modular: pier + arch span.");
            // The conduit house where it ends: a squat tower, and a fountain outside it.
            Transform conduit = Building(g, "Conduit house", 56, 98, 61, 102, 11f, M.Townhouse2, WorldKit.RoofKind.Flat);
            Tag(conduit, "conduit_house", "Conduit house: a squat stone water tower where the aqueduct ends, 11 m, a spout into a fountain below.");
            Transform fountain = WorldKit.Group(g, "Conduit fountain");
            WorldKit.BlockTiles(fountain, "Footprint", X(57), Z(96), X(60), Z(98));
            WorldKit.Prop("Trough", fountain, P(58.5f, 0.35f, 97f), new Vector3(3f, 0.7f, 1.8f), M.Limestone);
            WorldKit.Prop("Water", fountain, P(58.5f, 0.66f, 97f), new Vector3(2.7f, 0.04f, 1.5f), M.River);
            WorldKit.Prop("Spout", fountain, P(58.5f, 2.6f, 97.9f), new Vector3(0.3f, 0.2f, 0.6f), M.Bronze);
            Tag(fountain, "conduit_fountain", "Long stone trough under a bronze spout from the conduit house: where the quarter draws water.");
        }

        // ------------------------------------------------------------------ people

        private static void BuildPeople(Transform map)
        {
            // Not content yet, only life: knights at the gates and in the yard, pilgrims on the road and
            // in the plaza, cantors at the monument and in the nave, sisters in the Letting House,
            // dockhands on the quays. All townsfolk (nobody to fight here).
            Transform g = WorldKit.Group(map, "People");
            (string npc, int x, int z, int wander)[] people =
            {
                ("blood_knight", 10, 71, 0), ("blood_knight", 11, 78, 0), ("blood_knight", 96, 126, 0), ("blood_knight", 101, 126, 0),
                ("blood_knight", 134, 45, 0), ("blood_knight", 134, 50, 0), ("blood_knight", 70, 60, 4), ("blood_knight", 95, 70, 4),
                ("blood_knight", 116, 64, 3), ("blood_knight", 122, 64, 3), ("blood_knight", 114, 70, 2),
                ("cruor_pilgrim", 3, 70, 3), ("cruor_pilgrim", 6, 90, 3), ("cruor_pilgrim", 20, 74, 6), ("cruor_pilgrim", 50, 74, 6),
                ("cruor_pilgrim", 74, 56, 6), ("cruor_pilgrim", 90, 66, 6), ("cruor_pilgrim", 63, 58, 2), ("cruor_pilgrim", 63, 60, 2),
                ("cruor_pilgrim", 80, 26, 4), ("cruor_pilgrim", 90, 25, 4), ("cruor_pilgrim", 20, 88, 4),
                ("synod_cantor", 77, 62, 2), ("synod_cantor", 88, 62, 2), ("synod_cantor", 82, 104, 2), ("synod_cantor", 82, 90, 2),
                ("letting_sister", 53, 55, 1), ("letting_sister", 55, 61, 1),
                ("cruor_dockhand", 108, 25, 4), ("cruor_dockhand", 122, 24, 4), ("cruor_dockhand", 22, 24, 4),
            };
            foreach ((string npc, int x, int z, int wander) in people)
                WorldKit.Spawn(g, npc, X(x), Z(z), wander);
        }
    }

    internal static class CruorBuilderExtensions
    {
        public static int[] Concat(this int[] a, int[] b)
        {
            var all = new int[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }
    }
}
