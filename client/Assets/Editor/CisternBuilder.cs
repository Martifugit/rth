using System.Collections.Generic;
using Rth.MapTools;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Cistern, the capital of Sere (docs/regions/cistern.md): a mud-brick city in the rain shadow
    /// of Candora, built round the deepest well in the west. A dusty amber afternoon; ochre walls,
    /// turquoise tile, saffron cloth; windcatchers over every roof; mountains to the west and north
    /// with the caravan road climbing to Lucern Pass. Water is everything here and the city shows
    /// it: the Deepwell, a great stepwell in the middle of town with a wheel lifting water to the
    /// Queen's garden first; the Water Office where the ration is measured out; the Great Cistern
    /// under its dome; a dry fountain in the square. Caravans come in through the Pass Gate to
    /// the caravanserai, and the covered bazaar runs to the Heartland Gate. Outside: fig orchards,
    /// and the line of a qanat coming down from the hills. Every placeholder is tagged with the asset
    /// it stands for (WorldKit.Asset); saving writes the list to docs/regions/cistern-assets.md.
    /// </summary>
    public static class CisternBuilder
    {
        public const string ScenePath = "Assets/Scenes/Cistern.unity";

        // The region: 144 x 144 tiles, its south-west corner here, far west of Tolling where the
        // heartland dries out into Sere. Everything below is laid out in local tiles.
        private const int OX = -320, OZ = -24;
        private static readonly Vector2Int Size = new Vector2Int(144, 144);

        // The plan, in local tiles.
        private const int WallW = 16, WallS = 16, WallE = 128, WallN = 128;
        private const int GateEz0 = 68, GateEz1 = 72;   // the Heartland Gate, z 68..71 in the east wall
        private const int GateNx0 = 48, GateNx1 = 52;   // the Pass Gate, x 48..51 in the north wall
        private static readonly RectInt Court = new RectInt(56, 56, 32, 32);  // the Well Court
        private static readonly RectInt Well = new RectInt(62, 62, 20, 20);   // the Deepwell's pit
        private static readonly RectInt Palace = new RectInt(56, 92, 44, 34); // the palace compound
        private const float Axis = 72f;                 // the Well Court's and the palace's centre line

        private static Mats M;

        [MenuItem("R'th/Dev/Rebuild Cistern Scene", priority = 106)]
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
            WorldKit.RoofKind roof = WorldKit.RoofKind.Flat) =>
            WorldKit.Building(parent, name, X(x0), Z(z0), X(x1), Z(z1), eave, f, roof);

        private static Transform Hall(Transform parent, string name, int x0, int z0, int x1, int z1, float height, WorldKit.Facade f, Material floor,
            params WorldKit.Door[] doors) =>
            WorldKit.Hall(parent, name, X(x0), Z(z0), X(x1), Z(z1), height, f, floor, doors);

        private static void Tag(Transform t, string id, string note) => WorldKit.Asset(t, "cistern/" + id, note);

        /// <summary>Every material the city uses, made once per build.</summary>
        private sealed class Mats
        {
            public Material Dust, Packed, Paving, MudBrick, Sandstone, Lime, Turquoise, Lapis, Saffron, Madder, Gold, Bronze, Iron, Timber, Terrace,
                Window, WindowLit, Water, WaterDeep, Clay, Rock, Snow, Fig, Palm, Bark, Canvas, Goods, Salt, Hide, Coals, Flame, Ember, DustFx, Mote,
                Beam, Red, Shadow;

            public WorldKit.Facade House, House2, Grand, Store, Fort;
        }

        public static void Build()
        {
            var kit = new WorldKit(seed: 4417);
            M = new Mats
            {
                Dust = kit.Mat("SereDust", new Color(0.74f, 0.6f, 0.42f)),
                Packed = kit.Mat("SerePackedEarth", new Color(0.66f, 0.52f, 0.36f)),
                Paving = kit.Mat("SerePaving", new Color(0.8f, 0.68f, 0.5f)),
                MudBrick = kit.Mat("SereMudBrick", new Color(0.72f, 0.52f, 0.36f)),
                Sandstone = kit.Mat("SereSandstone", new Color(0.83f, 0.67f, 0.47f)),
                Lime = kit.Mat("SereLimePlaster", new Color(0.9f, 0.84f, 0.72f)),
                Turquoise = kit.Mat("SereTurquoiseTile", new Color(0.16f, 0.58f, 0.6f)),
                Lapis = kit.Mat("SereLapisTile", new Color(0.15f, 0.24f, 0.5f)),
                Saffron = kit.Mat("SereSaffron", new Color(0.9f, 0.6f, 0.15f)),
                Madder = kit.Mat("SereMadder", new Color(0.62f, 0.2f, 0.14f)),
                Gold = kit.Mat("SereGold", new Color(0.86f, 0.68f, 0.3f)),
                Bronze = kit.Mat("Bronze", new Color(0.6f, 0.45f, 0.2f)), // shared
                Iron = kit.Mat("SereIron", new Color(0.18f, 0.16f, 0.15f)),
                Timber = kit.Mat("Timber", new Color(0.4f, 0.28f, 0.18f)), // shared
                Terrace = kit.Mat("SereRoofTerrace", new Color(0.7f, 0.56f, 0.4f)),
                Window = kit.Mat("SereWindow", new Color(0.14f, 0.1f, 0.08f)),
                Clay = kit.Mat("SereClay", new Color(0.66f, 0.4f, 0.26f)),
                Rock = kit.Mat("SereRock", new Color(0.56f, 0.46f, 0.38f)),
                Snow = kit.Mat("SereSnow", new Color(0.93f, 0.92f, 0.9f)),
                Fig = kit.Mat("SereFigLeaves", new Color(0.33f, 0.43f, 0.2f)),
                Palm = kit.Mat("SerePalm", new Color(0.36f, 0.46f, 0.22f)),
                Bark = kit.Mat("Bark", new Color(0.36f, 0.25f, 0.15f)), // shared
                Canvas = kit.Mat("SereCanvas", new Color(0.86f, 0.78f, 0.62f)),
                Goods = kit.Mat("SereGoods", new Color(0.64f, 0.48f, 0.3f)),
                Salt = kit.Mat("SereRockSalt", new Color(0.9f, 0.82f, 0.8f)),
                Hide = kit.Mat("SereHide", new Color(0.5f, 0.38f, 0.26f)),
                Red = kit.Mat("SynodRed", new Color(0.55f, 0.1f, 0.12f)), // shared
                Shadow = kit.Mat("SereWellShadow", new Color(0.2f, 0.15f, 0.12f)),
                Water = FxKit.Glow("SereWater", new Color(0.18f, 0.62f, 0.62f), 0.35f),
                WaterDeep = FxKit.Glow("SereWellWater", new Color(0.12f, 0.5f, 0.55f), 0.6f),
                WindowLit = FxKit.Glow("SereWindowLit", new Color(1f, 0.72f, 0.38f), 0.5f),
                Coals = FxKit.Glow("BrazierCoals", new Color(1f, 0.35f, 0.08f), 0.9f), // shared
                Flame = FxKit.ParticleMat("FxFlame", new Color(1f, 0.55f, 0.22f, 0.6f), additive: true), // shared
                Ember = FxKit.ParticleMat("FxEmber", new Color(1f, 0.55f, 0.2f, 1f), additive: true), // shared
                DustFx = FxKit.ParticleMat("SereDustHaze", new Color(0.9f, 0.74f, 0.52f, 0.09f), additive: false),
                Mote = FxKit.ParticleMat("FxMote", new Color(0.8f, 0.85f, 1f, 0.6f), additive: true), // shared
                Beam = FxKit.ParticleMat("SereLightBeam", new Color(1f, 0.8f, 0.55f, 0.1f), additive: true),
            };
            M.House = new WorldKit.Facade { Walls = M.MudBrick, Roof = M.Terrace, Trim = M.Sandstone, Windows = M.Window };
            M.House2 = new WorldKit.Facade { Walls = M.Lime, Roof = M.Terrace, Trim = M.Sandstone, Windows = M.Window };
            M.Grand = new WorldKit.Facade { Walls = M.Sandstone, Roof = M.Packed, Trim = M.Lapis, Windows = M.WindowLit, Gable = M.Sandstone, Pitch = 20f };
            M.Store = new WorldKit.Facade { Walls = M.MudBrick, Roof = M.Terrace, Trim = M.Timber, Windows = null, Gable = M.MudBrick, Pitch = 18f };
            M.Fort = new WorldKit.Facade { Walls = M.MudBrick, Roof = M.Packed, Trim = M.Sandstone, Windows = M.Window };

            MapArea area = WorldKit.BeginRegion("cistern", "Cistern", new Vector2Int(OX, OZ), Size, M.Dust);
            // A dusty amber afternoon in the rain shadow: a warm low sun, deep warm shadow, and fog the
            // colour of the dust, so the mountains behind stand up as pale shapes.
            area.music = "cistern";
            area.ownAtmosphere = true;
            area.sunIntensity = 1.35f;
            area.sunColor = new Color(1f, 0.8f, 0.55f);
            area.ambientSky = new Color(0.85f, 0.72f, 0.56f);
            area.ambientEquator = new Color(0.7f, 0.56f, 0.42f);
            area.ambientGround = new Color(0.4f, 0.3f, 0.22f);
            area.fog = new Color(0.82f, 0.68f, 0.5f);
            area.fogStart = 45f;
            area.fogEnd = 112f;
            Transform map = area.transform;

            WorldKit.CutGround(area, new RectInt(X(Well.x), Z(Well.y), Well.width, Well.height));
            BuildMountains(map);
            BuildOutside(kit, map);
            BuildWalls(map);
            BuildDeepwell(map);
            BuildPalace(kit, map);
            BuildWaterOffice(map);
            BuildBazaar(map);
            BuildGreatCistern(map);
            BuildFountainSquare(kit, map);
            BuildCaravanserai(kit, map);
            BuildForeigners(map);
            BuildTown(kit, map);
            BuildPeople(map);

            Transform fx = WorldKit.Group(map, "Atmosphere");
            FxKit.Mist(fx, P(72f, 0.4f, 72f), new Vector2(150f, 150f), M.DustFx, Color.white);
            FxKit.Volume(fx, "CisternVolume", P(72f, 20f, 72f), new Vector3(150f, 60f, 150f), bloom: 0.45f, vignette: 0.2f, saturation: 4f);

            WorldKit.SaveRegion(area, ScenePath);
        }

        // ------------------------------------------------------------------ the mountains

        /// <summary>
        /// Candora behind the city: a wall of dry brown ridges to the west and north with snow on the
        /// tops, the caravan road climbing into the pass, and a Concordian beacon tower lit on a
        /// shoulder of the mountain. Scenery only, beyond the region's edge.
        /// </summary>
        private static void BuildMountains(Transform map)
        {
            Transform g = WorldKit.Group(map, "Candora (distant)");
            (Vector3 at, Vector3 size, float yaw)[] ridges =
            {
                (new Vector3(-40f, 0f, 30f), new Vector3(40f, 34f, 60f), 10f), (new Vector3(-45f, 0f, 95f), new Vector3(46f, 44f, 56f), -8f),
                (new Vector3(-30f, 0f, 160f), new Vector3(50f, 52f, 50f), 30f), (new Vector3(20f, 0f, 190f), new Vector3(60f, 40f, 40f), 5f),
                (new Vector3(95f, 0f, 195f), new Vector3(70f, 46f, 44f), -12f), (new Vector3(160f, 0f, 185f), new Vector3(60f, 30f, 40f), 18f),
                (new Vector3(-60f, 0f, -20f), new Vector3(50f, 26f, 50f), 25f),
            };
            foreach ((Vector3 at, Vector3 size, float yaw) in ridges)
            {
                Transform ridge = WorldKit.Group(g, "Ridge");
                Vector3 c = P(at.x, 0f, at.z);
                // A ridge is a stack of shrinking, tilted slabs; the top one carries snow.
                for (int i = 0; i < 4; i++)
                {
                    float k = 1f - i * 0.22f;
                    WorldKit.Prop("Mass", ridge, c + Vector3.up * (size.y * (i + 0.5f) / 4f), new Vector3(size.x * k, size.y / 4f + 1f, size.z * k), i == 3 ? M.Snow : M.Rock, yaw + i * 7f);
                }
            }
            // The pass road going on up, and the beacon on its shoulder.
            WorldKit.Prop("Pass road beyond", g, P(50f, 0.02f, 166f), new Vector3(4f, 0.02f, 44f), M.Packed);
            Transform beacon = WorldKit.Group(g, "Concordian beacon tower");
            WorldKit.Prop("Tower", beacon, P(62f, 22f, 182f), new Vector3(3f, 8f, 3f), M.Lime);
            WorldKit.Prop("Lantern", beacon, P(62f, 27f, 182f), new Vector3(2f, 2f, 2f), M.WindowLit);
            FxKit.PointLight(beacon, "Beacon", P(62f, 27f, 182f), new Color(1f, 0.85f, 0.6f), 4f, 20f, flicker: 0.05f);
            Tag(beacon, "concord_beacon_far", "A Concordian beacon tower high on the pass, lit: seen from the city as a spark on the mountain. Far silhouette only.");
            Tag(g, "candora_backdrop", "Candora's foothills: dry brown ridges with snowy tops, a wall to the west and north. Backdrop meshes (several), never reached.");
        }

        // ------------------------------------------------------------------ outside the walls

        private static void BuildOutside(WorldKit kit, Transform map)
        {
            Transform outside = WorldKit.Group(map, "Outside the walls");
            WorldKit.Patch("The Heartland road", outside, X(WallE + 1), Z(GateEz0), X(Size.x), Z(GateEz1), M.Packed, 0.008f);
            WorldKit.Patch("The Pass road", outside, X(GateNx0), Z(WallN + 1), X(GateNx1), Z(Size.y), M.Packed, 0.008f);
            Transform sign = WorldKit.Station(outside, "Signpost", "lucern_signpost", X(GateNx1), Z(134));
            WorldKit.Box("Post", sign, sign.position + new Vector3(0f, 1.1f, 0f), new Vector3(0.15f, 2.2f, 0.15f), M.Timber);
            WorldKit.Prop("Arm", sign, sign.position + new Vector3(-0.5f, 1.9f, 0.3f), new Vector3(1.1f, 0.25f, 0.06f), M.Timber, 30f);
            Tag(sign, "signpost", "Weathered timber signpost, arms in three scripts.");
            foreach (Vector2Int r in new[] { new Vector2Int(4, 30), new Vector2Int(9, 52), new Vector2Int(2, 88), new Vector2Int(12, 104), new Vector2Int(30, 138), new Vector2Int(80, 136), new Vector2Int(120, 140), new Vector2Int(140, 120) })
                WorldKit.Rock(outside, X(r.x), Z(r.y), M.Rock, 1.2f);

            // The qanat: a line of shaft mounds coming straight down from the mountains to the walls,
            // over the tunnel that feeds the Deepwell.
            Transform qanat = WorldKit.Group(outside, "Qanat");
            for (int i = 0; i < 6; i++)
            {
                int x = 2 + i * 3, z = 142 - i * 3;
                Transform mound = WorldKit.Group(qanat, "Shaft mound");
                WorldKit.BlockTiles(mound, "Footprint", X(x), Z(z), X(x + 1), Z(z + 1));
                WorldKit.Cylinder("Mound", mound, P(x + 0.5f, 0.25f, z + 0.5f), new Vector3(2f, 0.25f, 2f), M.Packed, collider: false);
                WorldKit.Cylinder("Shaft", mound, P(x + 0.5f, 0.5f, z + 0.5f), new Vector3(0.6f, 0.03f, 0.6f), M.Shadow, collider: false);
            }
            Tag(qanat, "qanat_mound", "Qanat shaft mound: a ring of spoil round a dark shaft, every 4 m in a straight line from the mountains to the city. Modular.");

            // Fig orchards to the east and south, in rows with irrigation runnels; the saltfigs Sere sells to the basin.
            Transform orchard = WorldKit.Group(outside, "Fig orchards");
            WorldKit.Patch("Orchard earth", orchard, X(20), Z(0), X(Size.x), Z(WallS), M.Packed, 0.006f);
            WorldKit.Patch("Orchard earth", orchard, X(WallE + 1), Z(WallS), X(Size.x), Z(GateEz0 - 2), M.Packed, 0.006f);
            var runnels = new HashSet<Vector2Int>();
            foreach (int z in new[] { 4, 10 })
                for (int x = 20; x < Size.x; x++)
                    if (x != 60 && x != 61 && x != 100 && x != 101 && x != 134 && x != 135) runnels.Add(T(x, z));
            WorldKit.Water(orchard, "Runnels", runnels, M.Water);
            Tag(orchard, "irrigation_runnel", "Irrigation runnel: a narrow clay-lined channel of bright water between orchard rows, little sluice boards at the crossings.");
            for (int x = 22; x < Size.x - 1; x += 4)
                foreach (int z in new[] { 1, 7, 13 })
                    if (x != 58 && x != 98 && x != 134) Tag(FigTree(kit, orchard, x, z), "fig_tree", "Saltfig tree: a low spreading fig, grey bark, big dark leaves, purple fruit dusted white. Blocks one tile.");
            for (int z = 20; z < GateEz0 - 3; z += 5)
                foreach (int x in new[] { 131, 136, 141 })
                    Tag(FigTree(kit, orchard, x, z), "fig_tree", null);
        }

        private static Transform FigTree(WorldKit kit, Transform parent, int lx, int lz)
        {
            Transform t = WorldKit.Group(parent, "Fig tree");
            var c = P(lx + 0.5f, 0f, lz + 0.5f);
            WorldKit.Box("Trunk", t, c + Vector3.up * 0.6f, new Vector3(0.25f, 1.2f, 0.25f), M.Bark).AddComponent<MapBlocker>();
            float s = kit.Range(1.6f, 2.2f);
            WorldKit.Prop("Crown", t, c + Vector3.up * 1.5f, new Vector3(s, 0.9f, s), M.Fig, kit.Range(0f, 90f));
            WorldKit.Prop("Crown top", t, c + Vector3.up * 2.05f, new Vector3(s * 0.65f, 0.5f, s * 0.65f), M.Fig, kit.Range(0f, 90f));
            WorldKit.Fades(t, 0.3f);
            return t;
        }

        // ------------------------------------------------------------------ the walls and gates

        private static void BuildWalls(Transform map)
        {
            Transform walls = WorldKit.Group(map, "City walls");
            const float H = 9f;
            int[] Span(int from, int to)
            {
                var list = new List<int>();
                for (int i = from; i < to; i++) list.Add(i);
                return list.ToArray();
            }
            Tag(WorldKit.CityWall(walls, "West wall", X(WallW), Z(WallS), X(WallW), Z(WallN + 1), H, M.MudBrick, M.Packed), "city_wall",
                "Mud-brick curtain wall, 9 m, battered (thicker at the foot), rounded stepped merlons, beam-ends sticking out in rows. Modular.");
            Tag(WorldKit.CityWall(walls, "North wall", X(WallW), Z(WallN), X(WallE + 1), Z(WallN), H, M.MudBrick, M.Packed, Span(X(GateNx0), X(GateNx1))), "city_wall", null);
            Tag(WorldKit.CityWall(walls, "South wall", X(WallW), Z(WallS), X(WallE + 1), Z(WallS), H, M.MudBrick, M.Packed), "city_wall", null);
            Tag(WorldKit.CityWall(walls, "East wall", X(WallE), Z(WallS), X(WallE), Z(WallN + 1), H, M.MudBrick, M.Packed, Span(Z(GateEz0), Z(GateEz1))), "city_wall", null);
            // Beam-ends in rows along the outer faces: the look of Sere's mud walls.
            for (int x = WallW + 2; x < WallE; x += 2)
                foreach ((float z, float y) in new[] { (WallS - 0.15f, 3f), (WallS - 0.15f, 6f), (WallN + 1.15f, 3f), (WallN + 1.15f, 6f) })
                    WorldKit.Prop("Beam end", walls, P(x, y, z), new Vector3(0.18f, 0.18f, 0.4f), M.Timber);

            (int x, int z)[] towers = { (14, 14), (126, 14), (14, 126), (126, 126), (14, 44), (14, 96), (44, 14), (96, 14), (126, 44), (126, 96), (90, 126) };
            foreach ((int x, int z) in towers)
            {
                Transform t = WorldKit.Tower(walls, "Wall tower", X(x), Z(z), 4, 12f, M.MudBrick, M.Packed, M.Window, WorldKit.RoofKind.Battlements, M.Sandstone);
                Tag(t, "wall_tower", "Mud-brick tower, 4 x 4 tiles, 12 m, tapering, rounded merlons, a sandstone band; beam-ends and spouts.");
            }
            Gate(walls, "The Heartland Gate", new Vector3(X(WallE) + 0.5f, 0f, Z(GateEz0)), new Vector3(X(WallE) + 0.5f, 0f, Z(GateEz1)), alongX: false, outward: 1);
            Gate(walls, "The Pass Gate", new Vector3(X(GateNx0), 0f, Z(WallN) + 0.5f), new Vector3(X(GateNx1), 0f, Z(WallN) + 0.5f), alongX: true, outward: 1);
        }

        /// <summary>A gatehouse: two tall mud-brick towers with a tiled arch between, saffron banners, a turquoise band.</summary>
        private static void Gate(Transform parent, string name, Vector3 a, Vector3 b, bool alongX, int outward)
        {
            Transform g = WorldKit.Group(parent, name);
            Vector3 dir = (b - a).normalized;
            Vector3 outDir = alongX ? new Vector3(0f, 0f, outward) : new Vector3(outward, 0f, 0f);
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 c = (side < 0 ? a - dir * 0.5f : b + dir * 0.5f) + dir * side * 1.5f;
                int tx = Mathf.FloorToInt(c.x - 1.5f), tz = Mathf.FloorToInt(c.z - 1.5f);
                Transform t = WorldKit.Tower(g, "Gate tower", tx, tz, 3, 13f, M.MudBrick, M.Packed, M.Window, WorldKit.RoofKind.Battlements, M.Turquoise);
                WorldKit.Banner(t, c + outDir * 1.55f + Vector3.up * 11.5f, alongX ? (outward > 0 ? 0f : 180f) : (outward > 0 ? 90f : -90f), 6f, 1.3f, M.Saffron, M.Turquoise, M.Iron);
            }
            WorldKit.Arch(g, a, b, 0f, 4.6f, 10f, 1.4f, M.Sandstone);
            WorldKit.Prop("Tile band", g, (a + b) / 2f + outDir * 0.72f + Vector3.up * 7.6f, alongX ? new Vector3(Vector3.Distance(a, b) + 1f, 0.6f, 0.06f) : new Vector3(0.06f, 0.6f, Vector3.Distance(a, b) + 1f), M.Turquoise);
            WorldKit.Prop("Gate walk", g, (a + b) / 2f + Vector3.up * 10f, alongX ? new Vector3(Vector3.Distance(a, b), 0.3f, 1.6f) : new Vector3(1.6f, 0.3f, Vector3.Distance(a, b)), M.Packed);
            foreach (int s in new[] { -1, 1 })
            {
                Vector3 jar = (s < 0 ? a - dir * 3.5f : b + dir * 3.5f) + outDir * 1.6f;
                WorldKit.Cylinder("Water jar", g, jar + Vector3.up * 0.5f, new Vector3(0.8f, 0.5f, 0.8f), M.Clay, collider: false);
            }
            Tag(g, "gatehouse", "Gatehouse: two 13 m mud-brick towers with turquoise bands, a sandstone arch 4.6 m high with a band of tile over it, saffron banners with the turquoise well-ring; water jars for travellers.");
        }

        // ------------------------------------------------------------------ the Deepwell

        private static void BuildDeepwell(Transform map)
        {
            // The heart of the city: a square stepwell twenty tiles across and nine metres deep, its
            // terraces stepping down to bright water. A colonnade rings the court above it. On the
            // north side a great wheel lifts water to a channel that runs to the Queen's garden: the
            // palace drinks first.
            Transform g = WorldKit.Group(map, "The Deepwell");
            WorldKit.Patch("Well Court", g, X(Court.x), Z(Court.y), X(Court.xMax), Z(Court.yMax), M.Paving, 0.006f);
            Tag(g, "well_court_paving", "Well Court paving: big sandstone flags with a turquoise-tile border round the pit.");

            // Terraces: five rings, each 1.4 tiles in from the last and 1.8 m lower, then the water.
            Transform pit = WorldKit.Group(g, "Stepwell");
            const float inset = 1.4f, drop = 1.8f, bottom = -11f;
            float x0 = X(Well.x), z0 = Z(Well.y), x1 = X(Well.xMax), z1 = Z(Well.yMax);
            for (int k = 0; k < 5; k++)
            {
                float a = k * inset, b = (k + 1) * inset, top = -k * drop - 0.2f;
                float h = top - bottom;
                Material stone = k % 2 == 0 ? M.Sandstone : M.Lime;
                foreach ((float cx, float cz, float sx, float sz) in new[]
                         {
                             ((x0 + x1) / 2f, z0 + (a + b) / 2f, x1 - x0 - 2f * a, b - a), ((x0 + x1) / 2f, z1 - (a + b) / 2f, x1 - x0 - 2f * a, b - a),
                             (x0 + (a + b) / 2f, (z0 + z1) / 2f, b - a, z1 - z0 - 2f * b), (x1 - (a + b) / 2f, (z0 + z1) / 2f, b - a, z1 - z0 - 2f * b),
                         })
                    WorldKit.Prop("Terrace", pit, new Vector3(cx, top - h / 2f, cz), new Vector3(sx, h, sz), stone);
                // A flight of steps down the middle of each side, from this terrace to the next.
                foreach ((Vector3 at, float yaw) in new[]
                         {
                             (new Vector3((x0 + x1) / 2f, 0f, z0 + b), 0f), (new Vector3((x0 + x1) / 2f, 0f, z1 - b), 180f),
                             (new Vector3(x0 + b, 0f, (z0 + z1) / 2f), 90f), (new Vector3(x1 - b, 0f, (z0 + z1) / 2f), -90f),
                         })
                    for (int s = 0; s < 4; s++)
                    {
                        Vector3 inward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                        WorldKit.Prop("Step", pit, at + inward * (s * 0.35f - 0.2f) + Vector3.up * (top - drop * (s + 1) / 5f), new Vector3(2.4f, 0.3f, 0.4f), M.Sandstone, yaw);
                    }
            }
            float waterInset = 5 * inset;
            WorldKit.Prop("Water", pit, new Vector3((x0 + x1) / 2f, -5 * drop - 0.4f, (z0 + z1) / 2f), new Vector3(x1 - x0 - 2f * waterInset, 0.1f, z1 - z0 - 2f * waterInset), M.WaterDeep);
            // Small pavilions on the corners of the first terrace, and niches lit with lamps.
            foreach (Vector2 c in new[] { new Vector2(x0 + 0.7f, z0 + 0.7f), new Vector2(x1 - 0.7f, z0 + 0.7f), new Vector2(x0 + 0.7f, z1 - 0.7f), new Vector2(x1 - 0.7f, z1 - 0.7f) })
            {
                WorldKit.Prop("Kiosk post", pit, new Vector3(c.x, 0.9f, c.y), new Vector3(0.3f, 2.2f, 0.3f), M.Sandstone);
                WorldKit.Dome(pit, new Vector3(c.x, 2f, c.y), 0.8f, 1f, M.Turquoise, M.Gold, 4);
            }
            FxKit.PointLight(pit, "Water glow", new Vector3((x0 + x1) / 2f, -6f, (z0 + z1) / 2f), new Color(0.4f, 0.9f, 0.85f), 3f, 14f);
            FxKit.Mist(pit, new Vector3((x0 + x1) / 2f, -8.5f, (z0 + z1) / 2f), new Vector2(8f, 8f), M.DustFx, new Color(0.8f, 1f, 1f));
            // The rim: a turquoise-tiled kerb and a low parapet (the pit's tiles have no ground; they're blocked).
            foreach ((Vector3 a, Vector3 b) in new[]
                     {
                         (new Vector3(x0, 0.35f, z0), new Vector3(x1, 0.35f, z0)), (new Vector3(x0, 0.35f, z1), new Vector3(x1, 0.35f, z1)),
                         (new Vector3(x0, 0.35f, z0), new Vector3(x0, 0.35f, z1)), (new Vector3(x1, 0.35f, z0), new Vector3(x1, 0.35f, z1)),
                     })
            {
                WorldKit.Beam("Parapet", pit, a, b, 0.4f, 0.7f, M.Sandstone);
                WorldKit.Beam("Tile kerb", pit, a + Vector3.down * 0.3f, b + Vector3.down * 0.3f, 1.2f, 0.06f, M.Turquoise);
            }
            Tag(pit, "deepwell", "Hero piece. The Deepwell: a square stepwell 20 m across and 9 m deep, five sandstone and lime terraces stepping down to bright turquoise water, flights of steps in the middle of each side, " +
                "small tiled kiosks on the corners, a parapet round the rim. Modular: terrace corner, terrace run, stair flight, kiosk.");
            Transform head = WorldKit.Station(g, "Well-head", "cistern_deepwell", X(72), Z(Well.y - 1));
            WorldKit.Box("Kerb", head, P(72.5f, 0.45f, Well.y - 0.5f), new Vector3(1.6f, 0.9f, 0.6f), M.Sandstone);
            WorldKit.Prop("Rope frame", head, P(72.5f, 1.6f, Well.y - 0.5f), new Vector3(1.6f, 0.1f, 0.1f), M.Timber);

            // The wheel on the north side and the Queen's Channel.
            Transform wheel = WorldKit.WaterWheel(g, P(77.5f, -4.6f, Well.yMax - 1.4f), 4.6f, 1.2f, 0f, M.Timber, M.Clay, 16);
            Tag(wheel, "noria", "The Deepwell's noria: a 9 m timber wheel standing in the pit, clay pots lashed round its rim, turning slowly to lift water to the channel at the rim. Should turn (a slow loop).");
            Transform channel = WorldKit.Group(g, "The Queen's Channel");
            WorldKit.Prop("Trough", channel, P(77.5f, 0.4f, Well.yMax + 0.5f), new Vector3(1f, 0.8f, 1.2f), M.Sandstone);
            float cz0 = Well.yMax + 1f, cz1 = Palace.y + 4f, ch = 3.4f;
            WorldKit.Beam("Rising chute", channel, P(77.5f, 0.8f, Well.yMax + 1f), P(77.5f, ch, Well.yMax + 3f), 0.8f, 0.4f, M.Sandstone);
            WorldKit.Prop("Channel", channel, P(77.5f, ch, (Well.yMax + 3f + cz1) / 2f), new Vector3(1f, 0.5f, cz1 - Well.yMax - 3f), M.Sandstone);
            WorldKit.Prop("Channel water", channel, P(77.5f, ch + 0.27f, (Well.yMax + 3f + cz1) / 2f), new Vector3(0.6f, 0.04f, cz1 - Well.yMax - 3f), M.Water);
            foreach (int z in new[] { 86, 90 })
            {
                WorldKit.Box("Pier", channel, P(77.5f, ch / 2f, z + 0.5f), new Vector3(0.9f, ch, 0.9f), M.Sandstone).AddComponent<MapBlocker>();
            }
            Tag(channel, "queens_channel", "The Queen's Channel: a sandstone trough on piers 3.4 m up, carrying the wheel's water from the Deepwell over the street into the palace garden.");

            // The colonnade round the court, and the watering jars at its feet.
            Transform cols = WorldKit.Group(g, "Colonnade");
            const string ColNote = "Well Court colonnade: slim sandstone columns 5 m high, a band of turquoise tile on the entablature, a flat timber-and-earth roof over the walk. Modular.";
            Tag(WorldKit.Colonnade(cols, "S", T(Court.x + 2, Court.y + 2), T(Court.xMax - 3, Court.y + 2), 5f, M.Sandstone, M.Sandstone, 3, -1.8f), "court_colonnade", ColNote);
            Tag(WorldKit.Colonnade(cols, "N", T(Court.x + 2, Court.yMax - 3), T(Court.xMax - 3, Court.yMax - 3), 5f, M.Sandstone, M.Sandstone, 3, 1.8f), "court_colonnade", null);
            Tag(WorldKit.Colonnade(cols, "W", T(Court.x + 2, Court.y + 5), T(Court.x + 2, Court.yMax - 6), 5f, M.Sandstone, M.Sandstone, 3, -1.8f), "court_colonnade", null);
            Tag(WorldKit.Colonnade(cols, "E", T(Court.xMax - 3, Court.y + 5), T(Court.xMax - 3, Court.yMax - 6), 5f, M.Sandstone, M.Sandstone, 3, 1.8f), "court_colonnade", null);
            // A band of turquoise tile along each entablature.
            foreach ((Vector3 a, Vector3 b) in new[]
                     {
                         (P(Court.x + 2, 5.25f, Court.y + 1.9f), P(Court.xMax - 2, 5.25f, Court.y + 1.9f)), (P(Court.x + 2, 5.25f, Court.yMax - 1.9f), P(Court.xMax - 2, 5.25f, Court.yMax - 1.9f)),
                         (P(Court.x + 1.9f, 5.25f, Court.y + 5), P(Court.x + 1.9f, 5.25f, Court.yMax - 5)), (P(Court.xMax - 1.9f, 5.25f, Court.y + 5), P(Court.xMax - 1.9f, 5.25f, Court.yMax - 5)),
                     })
                WorldKit.Beam("Tile band", cols, a, b, 0.05f, 0.3f, M.Turquoise);
            foreach (Vector2Int j in new[] { new Vector2Int(60, 60), new Vector2Int(84, 60), new Vector2Int(60, 84) })
                Tag(Jars(g, j.x, j.y), "water_jars", "A cluster of tall clay water jars (1-1.4 m), some with cloth over their mouths, a ladle on a string.");
        }

        private static Transform Jars(Transform parent, int lx, int lz)
        {
            Transform t = WorldKit.Group(parent, "Water jars");
            WorldKit.BlockTiles(t, "Footprint", X(lx), Z(lz), X(lx) + 1, Z(lz) + 1);
            foreach (Vector3 o in new[] { new Vector3(0.3f, 0f, 0.3f), new Vector3(0.7f, 0f, 0.35f), new Vector3(0.5f, 0f, 0.72f) })
                WorldKit.Cylinder("Jar", t, P(lx + o.x, 0.55f, lz + o.z), new Vector3(0.38f, 0.55f, 0.38f), M.Clay, collider: false);
            return t;
        }

        // ------------------------------------------------------------------ the palace

        private static void BuildPalace(WorldKit kit, Transform map)
        {
            // Queen Saffiya's palace: a walled garden of fig trees and a long pool (water as power,
            // in a city that rations it), and behind it the palace, a great tiled iwan in its face,
            // turquoise domes and windcatchers above.
            Transform g = WorldKit.Group(map, "The palace");
            int px0 = Palace.x, pz0 = Palace.y, px1 = Palace.xMax, pz1 = Palace.yMax;
            int gx0 = 70, gx1 = 75; // the garden gate, on the axis
            int[] Gate()
            {
                var list = new List<int>();
                for (int x = gx0; x < gx1; x++) list.Add(X(x));
                return list.ToArray();
            }
            const string WallNote = "Palace wall: 5 m mud-brick under lime plaster, a turquoise-tiled coping.";
            Tag(WorldKit.CityWall(g, "Palace wall S", X(px0), Z(pz0), X(px1 + 1), Z(pz0), 5f, M.Lime, M.Turquoise, Gate()), "palace_wall", WallNote);
            Tag(WorldKit.CityWall(g, "Palace wall W", X(px0), Z(pz0), X(px0), Z(pz1), 5f, M.Lime, M.Turquoise), "palace_wall", null);
            Tag(WorldKit.CityWall(g, "Palace wall E", X(px1), Z(pz0), X(px1), Z(pz1), 5f, M.Lime, M.Turquoise), "palace_wall", null);
            // The garden gate: a tiled portal with a turquoise arch.
            Transform portal = WorldKit.Group(g, "Garden gate");
            WorldKit.Arch(portal, P(gx0, 0f, pz0 + 0.5f), P(gx1, 0f, pz0 + 0.5f), 0f, 3.8f, 7f, 1.3f, M.Turquoise, 11);
            foreach (int x in new[] { gx0 - 1, gx1 })
                WorldKit.Prop("Gate pier", portal, P(x + 0.5f, 3.5f, pz0 + 0.5f), new Vector3(1f, 7f, 1.3f), M.Sandstone);
            Tag(portal, "palace_gate", "The palace garden gate: a 7 m sandstone portal with a pointed arch faced in turquoise tile, saffron hangings, guards either side.");

            // The garden: earth, paths, the long pool and its fig trees.
            WorldKit.Patch("Garden", g, X(px0 + 1), Z(pz0 + 1), X(px1), Z(108), M.Packed, 0.006f);
            WorldKit.Patch("Garden walk", g, X(gx0), Z(pz0 + 1), X(gx1), Z(108), M.Paving, 0.01f);
            var pool = new HashSet<Vector2Int>();
            for (int z = 96; z < 106; z++)
                for (int x = 71; x < 74; x++)
                    pool.Add(T(x, z));
            WorldKit.Water(g, "The long pool", pool, M.Water);
            Transform kerb = WorldKit.Prop("Pool kerb", g, P(72.5f, 0.12f, 101f), new Vector3(3.4f, 0.24f, 10.4f), M.Turquoise).transform;
            Tag(kerb, "long_pool", "The long pool: a reflecting pool 3 x 10 m with a turquoise kerb, fed from the Queen's Channel by a bronze spout. The only still water in Cistern.");
            Transform figs = WorldKit.Group(g, "Garden trees");
            for (int z = 95; z < 107; z += 3)
                foreach (int x in new[] { 66, 79, 86, 60, 92 })
                    Tag(FigTree(kit, figs, x, z), "fig_tree", null);
            WorldKit.Prop("Spout", g, P(76f, 1.3f, 96.5f), new Vector3(2f, 0.2f, 0.25f), M.Bronze);

            // The palace itself.
            Transform palace = Building(g, "The palace", 60, 110, 96, 125, 12f, M.Grand, WorldKit.RoofKind.Flat);
            WorldKit.Prop("Iwan frame", palace, P(Axis + 0.5f, 8f, 109.7f), new Vector3(9f, 16f, 0.8f), M.Lapis);
            WorldKit.Arch(palace, P(Axis - 3f, 0f, 109.5f), P(Axis + 4f, 0f, 109.5f), 4f, 4.5f, 13f, 1f, M.Turquoise, 13);
            WorldKit.Prop("Iwan recess", palace, P(Axis + 0.5f, 4.2f, 109.9f), new Vector3(6.6f, 8.4f, 0.2f), M.Shadow);
            WorldKit.Prop("Iwan doors", palace, P(Axis + 0.5f, 1.8f, 109.85f), new Vector3(2.4f, 3.6f, 0.1f), M.Gold);
            WorldKit.Dome(palace, P(Axis + 0.5f, 12f, 117.5f), 6.5f, 8f, M.Turquoise, M.Gold, 7);
            foreach (float x in new[] { 64f, 92f })
                WorldKit.Dome(palace, P(x, 12f, 117.5f), 3f, 3.6f, M.Turquoise, M.Gold, 5);
            foreach (Vector2 w in new[] { new Vector2(62f, 112f), new Vector2(94f, 112f), new Vector2(62f, 123f), new Vector2(94f, 123f) })
                WorldKit.Windcatcher(palace, P(w.x, 12f, w.y), 1.6f, 7f, M.Sandstone, M.Timber, M.Turquoise);
            WorldKit.Fades(palace, 0.2f).guardRadius = 5f;
            Tag(palace, "palace", "Hero building. Queen Saffiya's palace: a 36 x 15 tile sandstone range, 12 m, lapis-tiled bands, a great iwan (a 16 m tiled frame round a deep arched recess) on the axis, " +
                "a 13 m turquoise dome, two smaller domes, four windcatchers.");
            Transform door = WorldKit.Station(g, "Palace doors", "saffiya_doors", X(72), Z(109));
            Object.DestroyImmediate(door.GetComponent<MapBlocker>()); // the palace wall behind already blocks; you stand at the iwan
            WorldKit.Patch("Palace forecourt", g, X(px0 + 1), Z(108), X(px1), Z(110), M.Paving, 0.01f);
            foreach (int x in new[] { 66, 79 })
                Tag(WorldKit.BannerPole(g, X(x), Z(108), 6f, 180f, M.Iron, M.Saffron, M.Turquoise, M.Gold), "banner_pole", "Iron pole, 6 m, a gold finial, a saffron banner with Sere's turquoise well-ring.");
        }

        // ------------------------------------------------------------------ the Water Office

        private static void BuildWaterOffice(Transform map)
        {
            // Where the ration is measured out: a hall of great measuring jars, clerks at their benches,
            // and the Rolls that say who may draw how much. A queue with empty jars outside, always.
            Transform g = WorldKit.Group(map, "The Water Office");
            var f = new WorldKit.Facade { Walls = M.Lime, Roof = M.Terrace, Trim = M.Turquoise, Gable = M.Lime, Pitch = 18f };
            Transform hall = Hall(g, "Hall", 36, 62, 52, 76, 6.5f, f, M.Paving, new WorldKit.Door(WorldKit.Side.East, Z(68), Z(70)));
            Tag(hall, "water_office", "The Water Office: a lime-plastered hall, 16 x 14 tiles, 6.5 m, turquoise-tile cornice, a low roof; a wide door onto the street to the Well Court.");
            foreach (int z in new[] { 63, 66, 71, 74 })
            {
                Transform measure = WorldKit.Group(hall, "Measuring jar");
                WorldKit.BlockTiles(measure, "Footprint", X(37), Z(z), X(39), Z(z + 2));
                WorldKit.Cylinder("Jar", measure, P(38f, 0.9f, z + 1f), new Vector3(1.6f, 0.9f, 1.6f), M.Clay, collider: false);
                WorldKit.Prop("Marks", measure, P(38f, 1.2f, z + 0.18f), new Vector3(0.1f, 1.6f, 0.04f), M.Turquoise);
                Tag(measure, "measuring_jar", "Great measuring jar: a 1.8 m clay jar with turquoise level marks glazed down its side.");
            }
            Transform benches = WorldKit.Group(hall, "Clerks' benches");
            foreach (int z in new[] { 64, 73 })
            {
                WorldKit.BlockTiles(benches, "Footprint", X(42), Z(z), X(49), Z(z + 1));
                WorldKit.Prop("Bench", benches, P(45.5f, 0.45f, z + 0.5f), new Vector3(7f, 0.9f, 0.8f), M.Timber);
                for (int i = 0; i < 3; i++)
                    WorldKit.Prop("Tally sticks", benches, P(43f + i * 2.4f, 0.95f, z + 0.5f), new Vector3(0.6f, 0.1f, 0.4f), M.Goods);
            }
            Tag(benches, "clerks_bench", "Long clerks' bench with tally sticks, wax tablets and a brass measuring cup.");
            Transform rolls = WorldKit.Station(hall, "The Rolls", "cistern_water_rolls", X(41), Z(69));
            WorldKit.Box("Lectern", rolls, P(41.5f, 0.6f, 69.5f), new Vector3(0.8f, 1.2f, 0.8f), M.Timber);
            WorldKit.Cylinder("Scroll", rolls, P(41.5f, 1.3f, 69.5f), new Vector3(0.25f, 0.5f, 0.25f), M.Canvas, collider: false).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            Tag(rolls, "water_rolls", "The Rolls: long scrolls on a lectern listing every household's water ration, re-inked each dry season.");
            FxKit.PointLight(hall, "Lamps", P(44f, 4f, 69f), new Color(1f, 0.82f, 0.55f), 2f, 9f);
            foreach (int z in new[] { 64, 74 })
                Tag(Jars(g, 53, z), "water_jars", null);
        }

        // ------------------------------------------------------------------ the bazaar

        private static void BuildBazaar(Transform map)
        {
            // The covered bazaar runs from the Well Court to the Heartland Gate: a long vaulted hall
            // with skylight domes, stalls down both sides, salt slabs from the mountains, figs,
            // cloth, copper, and water sold by the cup.
            Transform g = WorldKit.Group(map, "The bazaar");
            WorldKit.Patch("Street", g, X(Court.xMax), Z(66), X(92), Z(74), M.Paving, 0.006f);
            WorldKit.Patch("Street", g, X(126), Z(GateEz0), X(WallE), Z(GateEz1), M.Paving, 0.006f);
            Transform hall = Hall(g, "Covered bazaar", 92, 66, 126, 74, 6f, M.Store, M.Paving,
                new WorldKit.Door(WorldKit.Side.West, Z(68), Z(71)), new WorldKit.Door(WorldKit.Side.East, Z(68), Z(71)));
            Tag(hall, "covered_bazaar", "The covered bazaar: a 34 x 8 tile mud-brick hall, 6 m, a low vault with a row of small skylight domes; arched doors at both ends. Inside: stalls in bays down both sides.");
            Transform sky = WorldKit.Group(hall, "Skylights");
            for (float x = 95f; x < 126f; x += 5f)
            {
                WorldKit.Dome(sky, P(x, 7.4f, 70f), 1.2f, 1.4f, M.Lime, null, 4);
                FxKit.PointLight(sky, "Skylight", P(x, 4.5f, 70f), new Color(1f, 0.85f, 0.6f), 1.2f, 5f);
            }
            sky.gameObject.AddComponent<RoofHider>().interior = new RectInt(X(92), Z(66), 34, 8);
            Material[] awn = { M.Saffron, M.Madder, M.Turquoise, M.Canvas };
            Material[] goods = { M.Salt, M.Goods, M.Fig, M.Bronze, M.Clay, M.Hide };
            int i = 0;
            for (int x = 94; x <= 122; x += 4)
            {
                Tag(WorldKit.Stall(g, X(x), Z(72), 2, 2, WorldKit.Side.South, M.Timber, awn[i % 4], goods[i % 6]), "bazaar_stall",
                    "Bazaar stall in a vaulted bay: a raised timber platform, a cloth awning (saffron, madder, turquoise or undyed), goods piled on it: rock salt, figs, copper, cloth, jars.");
                Tag(WorldKit.Stall(g, X(x), Z(66), 2, 2, WorldKit.Side.North, M.Timber, awn[(i + 2) % 4], goods[(i + 3) % 6]), "bazaar_stall", null);
                i++;
            }
        }

        // ------------------------------------------------------------------ the Great Cistern

        private static void BuildGreatCistern(Transform map)
        {
            // The city's reserve, the thing it's named for: a vast vaulted tank under a dome, kept
            // cool by four windcatchers; a portal with stairs going down into the dark on the west.
            Transform g = WorldKit.Group(map, "The Great Cistern");
            const int x0 = 98, z0 = 36, x1 = 114, z1 = 52;
            Transform body = WorldKit.Group(g, "Body");
            WorldKit.Box("Drum", body, P((x0 + x1) / 2f, 1.75f, (z0 + z1) / 2f), new Vector3(x1 - x0 - 0.1f, 3.5f, z1 - z0 - 0.1f), M.MudBrick).AddComponent<MapBlocker>();
            WorldKit.Dome(body, P((x0 + x1) / 2f, 3.5f, (z0 + z1) / 2f), 7.4f, 10f, M.MudBrick, null, 8);
            foreach (Vector2 c in new[] { new Vector2(x0 + 1.5f, z0 + 1.5f), new Vector2(x1 - 1.5f, z0 + 1.5f), new Vector2(x0 + 1.5f, z1 - 1.5f), new Vector2(x1 - 1.5f, z1 - 1.5f) })
                WorldKit.Windcatcher(body, P(c.x, 3.5f, c.y), 2.2f, 13f, M.MudBrick, M.Timber, M.Sandstone);
            WorldKit.Fades(body, 0.2f).guardRadius = 5f;
            Tag(body, "great_cistern", "Hero building. The Great Cistern: a 16 x 16 tile underground tank under a 10 m stepped mud-brick dome, four 16 m windcatchers at its corners. The skyline's other landmark.");
            // The portal on the west, stairs going down into the dark.
            Transform portal = WorldKit.Group(g, "Portal");
            WorldKit.Arch(portal, P(x0 - 0.5f, 0f, 42.5f), P(x0 - 0.5f, 0f, 45.5f), 0f, 3.2f, 5.5f, 1f, M.Sandstone);
            WorldKit.Prop("Darkness", portal, P(x0 + 0.05f, 1.5f, 44f), new Vector3(0.1f, 3f, 2.8f), M.Shadow);
            WorldKit.Prop("Tile frame", portal, P(x0 - 1.02f, 4.5f, 44f), new Vector3(0.06f, 1.4f, 4.2f), M.Turquoise);
            Tag(portal, "cistern_portal", "Cistern portal: a sandstone arch faced in turquoise tile over a stair that goes down into cold darkness.");
            Transform door = WorldKit.Station(g, "Cistern stair", "great_cistern", X(x0 - 1), Z(44));
            Object.DestroyImmediate(door.GetComponent<MapBlocker>()); // you stand at the top of the stair
            foreach (int z in new[] { 40, 48 })
                Tag(Jars(g, x0 - 2, z), "water_jars", null);
        }

        // ------------------------------------------------------------------ the fountain square

        private static void BuildFountainSquare(WorldKit kit, Transform map)
        {
            // South of the Well Court: the old fountain square, its great basin dry and cracked since
            // the wells were rationed. Water-sellers work it instead, with skins and cups.
            Transform g = WorldKit.Group(map, "Fountain square");
            WorldKit.Patch("Square", g, X(60), Z(30), X(85), Z(52), M.Paving, 0.006f);
            WorldKit.Patch("Street", g, X(70), Z(52), X(75), Z(Court.y), M.Paving, 0.006f);
            Transform fountain = WorldKit.Group(g, "The dry fountain");
            Vector3 c = P(72.5f, 0f, 40.5f);
            WorldKit.BlockTiles(fountain, "Basin", X(69), Z(37), X(76), Z(44));
            // Looked at from its north step, facing the Well Court.
            Transform step = WorldKit.Station(fountain, "Fountain step", "dry_fountain", X(72), Z(44));
            WorldKit.Box("Step", step, P(72.5f, 0.15f, 44.5f), new Vector3(1.4f, 0.3f, 0.8f), M.Sandstone);
            WorldKit.Cylinder("Basin", fountain, c + Vector3.up * 0.4f, new Vector3(7f, 0.4f, 7f), M.Sandstone, collider: false);
            WorldKit.Cylinder("Dry bed", fountain, c + Vector3.up * 0.55f, new Vector3(6.3f, 0.3f, 6.3f), M.Packed, collider: false);
            for (int i = 0; i < 5; i++)
                WorldKit.Prop("Crack", fountain, c + new Vector3(0f, 0.86f, 0f), new Vector3(0.06f, 0.02f, 5.5f - i * 0.6f), M.Shadow, i * 37f);
            WorldKit.Cylinder("Column", fountain, c + Vector3.up * 1.8f, new Vector3(0.7f, 1.4f, 0.7f), M.Sandstone, collider: false);
            WorldKit.Cylinder("Bowl", fountain, c + Vector3.up * 3.3f, new Vector3(2.4f, 0.15f, 2.4f), M.Turquoise, collider: false);
            Tag(fountain, "dry_fountain", "The dry fountain: a 7 m octagonal sandstone basin, its bed cracked earth, a turquoise-tiled upper bowl on a column with old green water stains. Dry.");
            foreach (Vector2Int s in new[] { new Vector2Int(62, 32), new Vector2Int(62, 48), new Vector2Int(80, 32), new Vector2Int(80, 48) })
                Tag(WorldKit.Stall(g, X(s.x), Z(s.y), 2, 2, s.y < 40 ? WorldKit.Side.North : WorldKit.Side.South, M.Timber, M.Saffron, M.Clay), "water_stall",
                    "Water-seller's stall: an awning over a row of sweating clay jars and hanging waterskins, brass cups on chains.");
            foreach (Vector2Int b in new[] { new Vector2Int(66, 44), new Vector2Int(78, 36) })
            {
                Transform br = WorldKit.Brazier(g, P(b.x + 0.5f, 0f, b.y + 0.5f), 1.2f, M.Iron, M.Coals, M.Flame, M.Ember);
                WorldKit.BlockTiles(br, "Footprint", X(b.x), Z(b.y), X(b.x) + 1, Z(b.y) + 1);
                Tag(br, "brazier", "Iron brazier on a tripod, for the evening market.");
            }
            Transform sails = WorldKit.Group(g, "Shade sails");
            for (int x = 62; x < 82; x += 7)
                for (int z = 31; z < 50; z += 8)
                    if (Mathf.Abs(z + 2.5f - 40.5f) > 4f)
                        WorldKit.ShadeSail(sails, P(x, 5f, z), P(x + 4f, 5f, z + 5f), 2.6f, (x + z) % 2 == 0 ? M.Saffron : M.Canvas, 0.4f);
            Tag(sails, "shade_sail", "Shade sail: saffron or undyed cloth on ropes high across the square.");
        }

        // ------------------------------------------------------------------ the caravanserai

        private static void BuildCaravanserai(WorldKit kit, Transform map)
        {
            // Inside the Pass Gate: the caravanserai, where the trains from Lucern Pass unload. A square
            // court of arcaded rooms round a well, bales and salt slabs stacked, pack beasts tethered.
            Transform g = WorldKit.Group(map, "The caravanserai");
            WorldKit.Patch("Pass road", g, X(GateNx0), Z(Court.yMax), X(GateNx1), Z(WallN), M.Packed, 0.008f);
            WorldKit.Patch("Court", g, X(22), Z(102), X(40), Z(120), M.Packed, 0.006f);
            var f = M.Fort;
            Tag(Building(g, "Range N", 18, 120, 44, 124, 6f, f, WorldKit.RoofKind.Battlements), "caravanserai_range",
                "Caravanserai range: a one-storey mud-brick block of vaulted rooms, 6 m, battlemented parapet; arched openings onto the court. Modular.");
            Tag(Building(g, "Range S", 18, 98, 44, 102, 6f, f, WorldKit.RoofKind.Battlements), "caravanserai_range", null);
            Tag(Building(g, "Range W", 18, 102, 22, 120, 6f, f, WorldKit.RoofKind.Battlements), "caravanserai_range", null);
            Tag(Building(g, "Range E (north)", 40, 112, 44, 120, 6f, f, WorldKit.RoofKind.Battlements), "caravanserai_range", null);
            Tag(Building(g, "Range E (south)", 40, 102, 44, 108, 6f, f, WorldKit.RoofKind.Battlements), "caravanserai_range", null);
            Transform portal = WorldKit.Group(g, "Portal");
            WorldKit.Arch(portal, P(44.5f, 0f, 108f), P(44.5f, 0f, 112f), 0f, 3.8f, 8f, 1.2f, M.Sandstone);
            WorldKit.Prop("Tile band", portal, P(45.15f, 6.6f, 110f), new Vector3(0.06f, 0.6f, 5f), M.Turquoise);
            Tag(portal, "caravanserai_portal", "The caravanserai's great portal: an 8 m arch tall enough for loaded beasts, turquoise band over it.");
            // Arcades on the court's faces.
            Tag(WorldKit.Colonnade(g, "Arcade N", T(23, 118), T(38, 118), 3.5f, M.Sandstone, M.Sandstone, 3, 1.5f), "caravanserai_arcade", "Arcade along the court: plain sandstone piers and a flat roof over the walk.");
            Tag(WorldKit.Colonnade(g, "Arcade S", T(23, 103), T(38, 103), 3.5f, M.Sandstone, M.Sandstone, 3, -1.5f), "caravanserai_arcade", null);
            Transform well = WorldKit.Group(g, "Court well");
            WorldKit.BlockTiles(well, "Footprint", X(30), Z(110), X(32), Z(112));
            WorldKit.Cylinder("Well", well, P(31f, 0.45f, 111f), new Vector3(1.8f, 0.45f, 1.8f), M.Sandstone, collider: false);
            WorldKit.Prop("Frame", well, P(31f, 1.6f, 111f), new Vector3(1.8f, 0.12f, 0.12f), M.Timber);
            Tag(well, "court_well", "Round sandstone well-head with a timber frame and a leather bucket.");
            // Pack beasts and loads.
            foreach (Vector2Int b in new[] { new Vector2Int(25, 106), new Vector2Int(25, 109), new Vector2Int(25, 114), new Vector2Int(36, 114) })
            {
                Transform beast = WorldKit.Group(g, "Pack beast");
                WorldKit.BlockTiles(beast, "Footprint", X(b.x), Z(b.y), X(b.x + 2), Z(b.y + 1));
                WorldKit.Prop("Body", beast, P(b.x + 1f, 1.2f, b.y + 0.5f), new Vector3(1.8f, 0.9f, 0.8f), M.Hide);
                WorldKit.Prop("Neck", beast, P(b.x + 2f, 1.9f, b.y + 0.5f), new Vector3(0.3f, 1.2f, 0.3f), M.Hide);
                foreach (float lx in new[] { 0.4f, 1.6f })
                    WorldKit.Prop("Leg", beast, P(b.x + lx, 0.4f, b.y + 0.5f), new Vector3(0.2f, 0.8f, 0.6f), M.Hide);
                WorldKit.Prop("Load", beast, P(b.x + 1f, 1.8f, b.y + 0.5f), new Vector3(1.4f, 0.5f, 1.1f), M.Canvas);
                Tag(beast, "pack_beast", "Pack beast of the caravans (creature design to come): tall, long-necked, shaggy, loaded with bales and salt slabs. Animated idle.");
            }
            foreach (Vector2Int c in new[] { new Vector2Int(33, 106), new Vector2Int(37, 108), new Vector2Int(29, 116) })
            {
                Transform stack = WorldKit.Group(g, "Salt slabs");
                WorldKit.BlockTiles(stack, "Footprint", X(c.x), Z(c.y), X(c.x + 1), Z(c.y + 1));
                for (int k = 0; k < 4; k++)
                    WorldKit.Prop("Slab", stack, P(c.x + 0.5f, 0.15f + k * 0.28f, c.y + 0.5f), new Vector3(0.9f, 0.25f, 0.6f), M.Salt, k * 8f);
                Tag(stack, "salt_slabs", "Stack of pinkish rock-salt slabs, cut in the mountains, roped for the beasts.");
            }
            kit.Clutter(g, X(35), Z(118), M.Goods, M.Clay);
            kit.Clutter(g, X(23), Z(119), M.Goods, M.Clay);
        }

        // ------------------------------------------------------------------ foreigners

        private static void BuildForeigners(Transform map)
        {
            // Sere plays the powers off against each other, and their outposts sit side by side in the
            // south-west: Tithe's counting-house (the Queen borrows) and a little Synod mission.
            Transform g = WorldKit.Group(map, "Foreign houses");
            var f = new WorldKit.Facade { Walls = M.Lime, Roof = M.Lime, Trim = M.Iron, Gable = M.Lime, Pitch = 22f };
            Transform house = Hall(g, "Tithe counting-house", 22, 40, 34, 50, 5f, f, M.Paving, new WorldKit.Door(WorldKit.Side.North, X(27), X(28)));
            Tag(house, "tithe_counting_house", "Tithe's counting-house in Cistern: whitewashed, black trim, a gold scales over the door. Out of place in mud-brick on purpose. The city's bank.");
            WorldKit.Prop("Gold scale", house, P(28f, 4f, 50.1f), new Vector3(1.4f, 1f, 0.08f), M.Gold);
            Transform box = WorldKit.Station(house, "Strongbox", "synod_strongbox", X(24), Z(47));
            WorldKit.Box("Chest", box, box.position + new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.8f, 0.7f), M.Iron);
            WorldKit.Prop("Bands", box, box.position + new Vector3(0f, 0.45f, 0f), new Vector3(0.95f, 0.1f, 0.75f), M.Gold);
            Transform counter = WorldKit.Group(house, "Counter");
            WorldKit.BlockTiles(counter, "Footprint", X(26), Z(43), X(32), Z(44));
            WorldKit.Prop("Counter", counter, P(29f, 0.55f, 43.5f), new Vector3(6f, 1.1f, 0.8f), M.Timber);
            Tag(counter, "counting_counter", "Long counter with chained ledgers.");
            var chapel = new WorldKit.Facade { Walls = M.Lime, Roof = M.Red, Trim = M.Red, Gable = M.Lime, Windows = M.Window, Pitch = 45f };
            Transform mission = Building(g, "Synod mission", 38, 40, 46, 50, 6f, chapel, WorldKit.RoofKind.Gable);
            WorldKit.Spire(mission, P(42f, 6f + 3.6f, 49f), 1.4f, 4f, M.Red, M.Gold, 4);
            Tag(mission, "synod_mission", "The Synod's mission: a small lime-washed chapel with a steep red roof and a little red spire, Cruor's style in a mud-brick street.");
        }

        // ------------------------------------------------------------------ the town

        private static void BuildTown(WorldKit kit, Transform map)
        {
            // Courtyard houses of mud-brick and lime plaster, blank walls to the lanes, flat roofs with
            // a windcatcher on almost every one.
            Transform g = WorldKit.Group(map, "Houses");
            (int x0, int z0, int x1, int z1)[] houses =
            {
                // south-west
                (18, 18, 28, 28), (30, 18, 40, 28), (18, 30, 28, 38), (48, 18, 56, 28), (48, 30, 56, 40), (18, 52, 28, 60), (30, 52, 40, 60), (42, 52, 52, 60),
                // south
                (60, 18, 68, 28), (76, 18, 86, 28), (88, 18, 96, 28), (88, 30, 96, 40), (88, 42, 96, 52), (118, 18, 126, 28), (100, 18, 114, 30),
                // east
                (116, 34, 126, 44), (116, 46, 126, 56), (100, 56, 112, 64), (114, 58, 126, 64), (96, 76, 106, 86), (108, 76, 116, 86), (118, 76, 126, 86),
                (102, 90, 112, 100), (114, 90, 126, 100), (102, 104, 112, 114), (114, 104, 126, 114), (102, 116, 112, 126), (114, 116, 124, 126),
                // west
                (18, 62, 32, 70), (18, 72, 32, 80), (18, 82, 32, 92), (36, 80, 46, 90), (36, 92, 46, 96),
            };
            foreach ((int x0, int z0, int x1, int z1) in houses)
            {
                bool lime = kit.Chance(0.4);
                float eave = kit.Range(4.5f, 8f);
                Transform h = Building(g, lime ? "Plastered house" : "Mud-brick house", x0, z0, x1, z1, eave, lime ? M.House2 : M.House, WorldKit.RoofKind.Flat);
                // A windcatcher over the main room, and sometimes a second.
                WorldKit.Windcatcher(h, P(x0 + 1.5f, eave, z1 - 1.5f), 1.4f, kit.Range(3.5f, 5.5f), lime ? M.Lime : M.MudBrick, M.Timber);
                if (kit.Chance(0.35)) WorldKit.Windcatcher(h, P(x1 - 1.5f, eave, z0 + 1.5f), 1.2f, kit.Range(3f, 4.5f), lime ? M.Lime : M.MudBrick, M.Timber);
                // Beam-ends and a door in the blank lane wall.
                WorldKit.Prop("Door", h, P((x0 + x1) / 2f, 1.1f, z0 - 0.03f), new Vector3(1.1f, 2.2f, 0.06f), M.Timber);
                if (kit.Chance(0.5)) WorldKit.Prop("Awning", h, P((x0 + x1) / 2f, 2.6f, z0 - 0.5f), new Vector3(2f, 0.05f, 1f), kit.Chance(0.5) ? M.Saffron : M.Madder);
                Tag(h, lime ? "house_plastered" : "house_mudbrick", lime
                    ? "Courtyard house, lime-plastered mud-brick: blank walls to the lane, a carved timber door, a flat roof with a windcatcher (or two)."
                    : "Courtyard house, bare mud-brick: blank walls to the lane, beam-ends, a carved timber door, a flat roof with a windcatcher (or two).");
            }
            Transform lamps = WorldKit.Group(map, "Lamps");
            foreach (Vector2 p in new[]
                     {
                         new Vector2(55.8f, 66f), new Vector2(55.8f, 76f), new Vector2(89f, 65.8f), new Vector2(89f, 74.2f), new Vector2(69.8f, 54f), new Vector2(75.2f, 54f),
                         new Vector2(47.8f, 95f), new Vector2(52.2f, 110f), new Vector2(127f, 65.8f), new Vector2(59f, 51.8f), new Vector2(86f, 51.8f),
                     })
                Tag(WorldKit.Lamp(lamps, P(p.x, 0f, p.y), M.Iron, M.WindowLit), "lamp", "Pierced brass lantern on an iron bracket post, throwing star-shaped light.");
        }

        // ------------------------------------------------------------------ people

        private static void BuildPeople(Transform map)
        {
            // Guards at the gates and the palace, a queue at the Water Office, water-sellers in the
            // square, caravaneers, courtiers in the garden, clerks, and fig-pickers in the orchards.
            Transform g = WorldKit.Group(map, "People");
            (string npc, int x, int z, int wander)[] people =
            {
                ("sere_guard", 130, 66, 0), ("sere_guard", 130, 73, 0), ("sere_guard", 47, 130, 0), ("sere_guard", 52, 130, 0),
                ("sere_guard", 69, 91, 0), ("sere_guard", 75, 91, 0), ("sere_guard", 72, 108, 2),
                ("water_clerk", 44, 67, 2), ("water_clerk", 45, 71, 2), ("water_clerk", 54, 69, 1),
                ("water_seller", 54, 66, 2), ("water_seller", 54, 72, 2), ("water_seller", 64, 40, 4), ("water_seller", 82, 44, 4), ("water_seller", 72, 34, 4),
                ("caravaneer", 28, 108, 3), ("caravaneer", 34, 112, 3), ("caravaneer", 50, 100, 4), ("caravaneer", 50, 120, 3), ("caravaneer", 100, 70, 6),
                ("sere_courtier", 69, 100, 3), ("sere_courtier", 82, 98, 3), ("sere_courtier", 76, 104, 2),
                ("water_seller", 66, 58, 3), ("water_seller", 86, 76, 3), ("caravaneer", 110, 69, 6),
                ("fig_picker", 40, 7, 4), ("fig_picker", 80, 13, 4), ("fig_picker", 112, 2, 4), ("fig_picker", 133, 40, 3),
            };
            foreach ((string npc, int x, int z, int wander) in people)
                WorldKit.Spawn(g, npc, X(x), Z(z), wander);
        }
    }
}
