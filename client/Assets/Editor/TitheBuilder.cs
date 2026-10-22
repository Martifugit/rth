using System.Collections.Generic;
using Rth.MapTools;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Tithe, the second see of Blood and the creditor of the basin (docs/regions/tithe.md). A
    /// salt-white port on the south lip of the bay's mouth, under a noon glare: whitewash, flat
    /// roofs and domes, ink-black trim and gold, shade sails over the streets, deep blue water on
    /// two sides. The Mouth runs along the north, and every ship that leaves the bay passes the
    /// chain tower and pays at the toll house. Inland the city is a counting-house: the Exchange
    /// with its great Balance, the Ledger House where every soul's account is kept, the Treasury,
    /// and the Chantry, a domed church where Tithe sings instead of preaching. Outside the walls,
    /// salt pans to the sea and the Saltery, where the dead are salted for the flats of Ascia.
    /// Every placeholder is tagged with the asset it stands for (WorldKit.Asset), and saving writes
    /// the list to docs/regions/tithe-assets.md. Once built, the scene is the source of truth.
    /// </summary>
    public static class TitheBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tithe.unity";

        // The region: 144 x 144 tiles, its south-west corner here (south-east of Cruor, across the
        // bay). Everything below is laid out in local tiles and offset by this when placed.
        private const int OX = 608, OZ = -160;
        private static readonly Vector2Int Size = new Vector2Int(144, 144);

        // The plan, in local tiles.
        private const int Mouth = 118;          // the bay's mouth: water from this row north
        private const int Sea = 129;            // the open sea: water from this column east (right under the sea wall)
        private const int WallW = 6, WallS = 20, WallE = 128; // the landward walls and the sea wall
        private const int GateWz0 = 66, GateWz1 = 70;          // the Bay Gate, z 66..69 in the west wall
        private const int GateSx0 = 60, GateSx1 = 64;          // the Salt Gate, x 60..63 in the south wall
        private static readonly RectInt Harbour = new RectInt(30, 98, 32, 16); // the basin, x 30..61, z 98..113
        private const int HarbourMouthX0 = 42, HarbourMouthX1 = 50;            // the gap in the mole
        private static readonly RectInt Exchange = new RectInt(62, 64, 38, 30); // the square, x 62..99, z 64..93
        private static readonly Vector2Int Balance = new Vector2Int(80, 78);
        private const float Axis = 80.5f;       // the Chantry's and the Exchange's north-south line

        private static Mats M;

        [MenuItem("R'th/Dev/Rebuild Tithe Scene", priority = 105)]
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

        private static void Tag(Transform t, string id, string note) => WorldKit.Asset(t, "tithe/" + id, note);

        /// <summary>Every material the city uses, made once per build.</summary>
        private sealed class Mats
        {
            public Material Paving, Crust, Rock, Dirt, Whitewash, SaltStone, Ink, InkBlue, Gold, Bronze, Iron, Timber, Terrace, Tile, DomeWhite,
                DomeBlue, Window, WindowLit, Sea, Shallows, Salt, Brine, Canvas, Sail, Hull, Planks, Goods, Bark, Leaves, Scrub, QuaereStone,
                QuaereGlow, Coals, Flame, Ember, Haze, Mote, Beam, Wax, Lamp;

            public WorldKit.Facade House, House2, Shipping, Official, Church, Warehouse, Fort;
        }

        public static void Build()
        {
            var kit = new WorldKit(seed: 7310);
            M = new Mats
            {
                Paving = kit.Mat("TithePaving", new Color(0.8f, 0.78f, 0.72f)),
                Crust = kit.Mat("TitheSaltCrust", new Color(0.86f, 0.84f, 0.78f)),
                Rock = kit.Mat("TitheRock", new Color(0.62f, 0.58f, 0.52f)),
                Dirt = kit.Mat("Dirt", new Color(0.55f, 0.42f, 0.26f)), // shared with Tolling
                Whitewash = kit.Mat("TitheWhitewash", new Color(0.94f, 0.93f, 0.89f)),
                SaltStone = kit.Mat("TitheSaltStone", new Color(0.86f, 0.84f, 0.77f)),
                Ink = kit.Mat("TitheInk", new Color(0.12f, 0.13f, 0.17f)),
                InkBlue = kit.Mat("TitheInkBlue", new Color(0.16f, 0.23f, 0.38f)),
                Gold = kit.Mat("TitheGold", new Color(0.86f, 0.7f, 0.32f)),
                Bronze = kit.Mat("Bronze", new Color(0.6f, 0.45f, 0.2f)), // shared
                Iron = kit.Mat("TitheIron", new Color(0.16f, 0.16f, 0.17f)),
                Timber = kit.Mat("Timber", new Color(0.4f, 0.28f, 0.18f)), // shared
                Terrace = kit.Mat("TitheRoofTerrace", new Color(0.8f, 0.77f, 0.7f)),
                Tile = kit.Mat("TitheTile", new Color(0.73f, 0.6f, 0.45f)),
                DomeWhite = kit.Mat("TitheDomeWhite", new Color(0.97f, 0.96f, 0.93f)),
                DomeBlue = kit.Mat("TitheDomeBlue", new Color(0.2f, 0.31f, 0.5f)),
                Window = kit.Mat("TitheWindow", new Color(0.1f, 0.11f, 0.14f)),
                Sea = kit.Mat("TitheSea", new Color(0.1f, 0.33f, 0.48f)),
                Shallows = kit.Mat("TitheShallows", new Color(0.16f, 0.46f, 0.52f)),
                Salt = kit.Mat("TitheSalt", new Color(0.98f, 0.97f, 0.95f)),
                Brine = kit.Mat("TitheBrine", new Color(0.84f, 0.74f, 0.74f)),
                Canvas = kit.Mat("TitheCanvas", new Color(0.93f, 0.91f, 0.85f)),
                Sail = kit.Mat("TitheSail", new Color(0.88f, 0.85f, 0.76f)),
                Hull = kit.Mat("TitheHull", new Color(0.3f, 0.22f, 0.16f)),
                Planks = kit.Mat("TithePlanks", new Color(0.55f, 0.45f, 0.33f)),
                Goods = kit.Mat("TitheGoods", new Color(0.66f, 0.52f, 0.36f)),
                Bark = kit.Mat("Bark", new Color(0.36f, 0.25f, 0.15f)), // shared
                Leaves = kit.Mat("Leaves", new Color(0.2f, 0.42f, 0.18f)), // shared
                Scrub = kit.Mat("TitheScrub", new Color(0.52f, 0.54f, 0.4f)),
                QuaereStone = kit.Mat("QuaereStone", new Color(0.15f, 0.16f, 0.2f)), // shared with Cruor
                Wax = kit.Mat("CandleWax", new Color(0.9f, 0.86f, 0.72f)), // shared
                WindowLit = FxKit.Glow("TitheWindowLit", new Color(1f, 0.86f, 0.62f), 0.5f),
                Lamp = FxKit.Glow("TitheLamp", new Color(1f, 0.9f, 0.7f), 0.8f),
                QuaereGlow = FxKit.Glow("QuaereSeam", new Color(0.45f, 0.7f, 0.75f), 0.8f), // shared with Cruor
                Coals = FxKit.Glow("BrazierCoals", new Color(1f, 0.35f, 0.08f), 0.9f), // shared
                Flame = FxKit.ParticleMat("FxFlame", new Color(1f, 0.55f, 0.22f, 0.6f), additive: true), // shared
                Ember = FxKit.ParticleMat("FxEmber", new Color(1f, 0.55f, 0.2f, 1f), additive: true), // shared
                Haze = FxKit.ParticleMat("TitheSaltHaze", new Color(1f, 1f, 1f, 0.07f), additive: false),
                Mote = FxKit.ParticleMat("FxMote", new Color(0.8f, 0.85f, 1f, 0.6f), additive: true), // shared
                Beam = FxKit.ParticleMat("TitheLightBeam", new Color(1f, 0.98f, 0.92f, 0.12f), additive: true),
            };
            M.House = new WorldKit.Facade { Walls = M.Whitewash, Roof = M.Terrace, Trim = M.SaltStone, Windows = M.Window };
            M.House2 = new WorldKit.Facade { Walls = M.Whitewash, Roof = M.DomeBlue, Trim = M.InkBlue, Windows = M.Window };
            M.Shipping = new WorldKit.Facade { Walls = M.Whitewash, Roof = M.Terrace, Trim = M.InkBlue, Windows = M.Window };
            M.Official = new WorldKit.Facade { Walls = M.SaltStone, Roof = M.DomeWhite, Trim = M.Ink, Gable = M.SaltStone, Windows = M.Window, Pitch = 22f };
            M.Church = new WorldKit.Facade { Walls = M.Whitewash, Roof = M.DomeWhite, Trim = M.Gold, Gable = M.Whitewash, Windows = M.WindowLit, Pitch = 25f };
            M.Warehouse = new WorldKit.Facade { Walls = M.SaltStone, Roof = M.Tile, Trim = M.Timber, Windows = M.Window, Pitch = 20f };
            M.Fort = new WorldKit.Facade { Walls = M.SaltStone, Roof = M.Paving, Trim = M.Ink, Windows = M.Window };

            MapArea area = WorldKit.BeginRegion("tithe", "Tithe", new Vector2Int(OX, OZ), Size, M.Paving);
            // Noon on the salt coast: a white, high sun, bright cool shadows and a pale salt haze that
            // bleaches the distance. Everything Cruor's gold evening isn't.
            area.music = "tithe";
            area.ownAtmosphere = true;
            area.sunIntensity = 1.55f;
            area.sunColor = new Color(1f, 0.97f, 0.9f);
            area.ambientSky = new Color(0.84f, 0.89f, 0.95f);
            area.ambientEquator = new Color(0.76f, 0.78f, 0.79f);
            area.ambientGround = new Color(0.56f, 0.54f, 0.5f);
            area.fog = new Color(0.88f, 0.9f, 0.89f);
            area.fogStart = 50f;
            area.fogEnd = 120f;
            Transform map = area.transform;

            BuildWater(map);
            BuildFarShore(map);
            BuildOutside(kit, map);
            BuildWalls(map);
            BuildHarbour(kit, map);
            BuildTollQuay(kit, map);
            BuildChain(map);
            BuildExchange(map);
            BuildLedgerHouse(map);
            BuildTreasury(map);
            BuildChantry(map);
            BuildCloister(kit, map);
            BuildSaltQuarter(kit, map);
            BuildTown(kit, map);
            BuildPeople(map);

            // Salt haze over the pans and the water, and post-processing: a bleached, bright look.
            Transform fx = WorldKit.Group(map, "Atmosphere");
            FxKit.Mist(fx, P(72f, 0.3f, 10f), new Vector2(140f, 20f), M.Haze, Color.white);
            FxKit.Mist(fx, P(72f, 0.3f, 130f), new Vector2(150f, 26f), M.Haze, Color.white);
            FxKit.Volume(fx, "TitheVolume", P(72f, 20f, 72f), new Vector3(150f, 60f, 150f), bloom: 0.7f, vignette: 0.1f, saturation: -12f);

            WorldKit.SaveRegion(area, ScenePath);
        }

        // ------------------------------------------------------------------ water

        private static void BuildWater(Transform map)
        {
            var mouth = new HashSet<Vector2Int>();
            var sea = new HashSet<Vector2Int>();
            var harbour = new HashSet<Vector2Int>();
            for (int z = 0; z < Size.y; z++)
                for (int x = 0; x < Size.x; x++)
                {
                    if (z >= Mouth) mouth.Add(T(x, z));
                    else if (x >= Sea) sea.Add(T(x, z));
                    else if (Harbour.Contains(new Vector2Int(x, z)) || (z >= Harbour.yMax && x >= HarbourMouthX0 && x < HarbourMouthX1)) harbour.Add(T(x, z));
                }
            WorldKit.Water(map, "The Mouth", mouth, M.Sea);
            WorldKit.Water(map, "The sea", sea, M.Sea);
            WorldKit.Water(map, "The harbour", harbour, M.Shallows);
            WorldKit.FarWater(map, "The Mouth beyond", new Rect(OX - 160f, OZ + Size.y, Size.x + 320f, 160f), M.Sea);
            WorldKit.FarWater(map, "The sea beyond", new Rect(OX + Size.x, OZ - 160f, 160f, Size.y + 160f), M.Sea);
            WorldKit.FarWater(map, "The bay beyond", new Rect(OX - 160f, OZ + 40f, 160f, Size.y - 40f), M.Sea);
            // The stone edges of every quay: a dark lip at the waterline.
            Transform edges = WorldKit.Group(map, "Quay edges");
            WorldKit.Prop("Mouth quay edge", edges, P(WallE / 2f, -0.2f, Mouth - 0.15f), new Vector3(WallE, 0.6f, 0.3f), M.Rock);
            WorldKit.Prop("Harbour edge S", edges, P(Harbour.center.x, -0.2f, Harbour.y - 0.15f), new Vector3(Harbour.width, 0.6f, 0.3f), M.Rock);
            WorldKit.Prop("Harbour edge W", edges, P(Harbour.x - 0.15f, -0.2f, Harbour.center.y + 2f), new Vector3(0.3f, 0.6f, Harbour.height + 4f), M.Rock);
            WorldKit.Prop("Harbour edge E", edges, P(Harbour.xMax + 0.15f, -0.2f, Harbour.center.y + 2f), new Vector3(0.3f, 0.6f, Harbour.height + 4f), M.Rock);
            Tag(edges, "quay_edge", "Dressed stone quay edge: big salt-white blocks, a dark weed line at the water, iron rings.");
        }

        /// <summary>
        /// Across the Mouth: the far headland with its own chain tower, the slack chain lying between
        /// them on floats, and the Quaere far out in the bay to the west. Scenery only.
        /// </summary>
        private static void BuildFarShore(Transform map)
        {
            Transform g = WorldKit.Group(map, "Across the Mouth");
            WorldKit.Prop("Far headland", g, P(110f, 1.5f, 196f), new Vector3(180f, 3f, 64f), M.Rock);
            WorldKit.Prop("Far cliff", g, P(120f, 4f, 176f), new Vector3(70f, 6f, 16f), M.Rock, 4f);
            Transform tower = WorldKit.Group(g, "The North Tower");
            WorldKit.Prop("Body", tower, P(123.5f, 10f, 172f), new Vector3(6f, 20f, 6f), M.SaltStone);
            WorldKit.Prop("Corbels", tower, P(123.5f, 19.8f, 172f), new Vector3(6.5f, 0.6f, 6.5f), M.Ink);
            WorldKit.Battlements(tower, P(120.5f, 20f, 169f), P(126.5f, 20f, 169f), M.SaltStone);
            WorldKit.Battlements(tower, P(120.5f, 20f, 175f), P(126.5f, 20f, 175f), M.SaltStone);
            WorldKit.Banner(tower, P(123.5f, 18f, 168.95f), 180f, 7f, 1.6f, M.Ink, M.Gold, M.Iron);
            Tag(tower, "north_chain_tower", "The North Tower across the Mouth: the chain tower's twin on the far headland, seen only from the quays (50 m away, in haze). Same kit as the Tollkeep.");
            Transform q = WorldKit.QuaereSilhouette(g, P(-10f, 0f, 180f), 0.9f, M.QuaereStone, M.QuaereGlow, M.Haze);
            Tag(q, "the_quaere_far", "Hero silhouette, seen only from across the bay. Shared with Cruor (one mesh); from Tithe it stands to the north-west, low in the haze.");
        }

        // ------------------------------------------------------------------ outside the walls

        private static void BuildOutside(WorldKit kit, Transform map)
        {
            Transform outside = WorldKit.Group(map, "Outside the walls");
            // Bare salt-crusted ground: the strip west of the wall and the coast south of it.
            WorldKit.Patch("Salt crust", outside, X(0), Z(0), X(Sea), Z(WallS), M.Crust);
            WorldKit.Patch("Salt crust", outside, X(0), Z(WallS), X(WallW), Z(Mouth), M.Crust);
            WorldKit.Patch("The bay road", outside, X(0), Z(GateWz0), X(WallW), Z(GateWz1), M.Dirt, 0.008f);
            WorldKit.Patch("The salt road", outside, X(GateSx0), Z(0), X(GateSx1), Z(WallS), M.Dirt, 0.008f);
            WorldKit.Patch("The Ascia road", outside, X(GateSx1), Z(1), X(Sea), Z(4), M.Dirt, 0.008f);
            foreach (Vector2Int r in new[] { new Vector2Int(1, 30), new Vector2Int(3, 44), new Vector2Int(0, 58), new Vector2Int(2, 82), new Vector2Int(4, 96), new Vector2Int(1, 108), new Vector2Int(128, 2) })
                WorldKit.Rock(outside, X(r.x), Z(r.y), M.Rock, 1.1f);
            foreach (Vector2Int s in new[] { new Vector2Int(2, 36), new Vector2Int(4, 52), new Vector2Int(1, 74), new Vector2Int(3, 90), new Vector2Int(58, 18), new Vector2Int(65, 18) })
                Tag(Scrub(outside, s.x, s.y), "salt_scrub", "Low grey-green saltbush, knee-high, the only thing that grows here. Walkable.");

            // The salt pans of Salinas, running down to the sea: shallow brine pools in rows behind
            // low clay rims, white heaps of raked salt along the paths between them.
            Transform pans = WorldKit.Group(outside, "Salt pans");
            var rows = new List<RectInt>();
            for (int x = 8; x + 6 <= 56; x += 8)
                foreach (int z in new[] { 6, 12 })
                    rows.Add(new RectInt(x, z, 6, 4));
            for (int x = 82; x + 6 <= 126; x += 8)
                foreach (int z in new[] { 6, 12 })
                    rows.Add(new RectInt(x, z, 6, 4));
            foreach (RectInt r in rows)
            {
                Transform pan = WorldKit.Group(pans, "Salt pan");
                WorldKit.BlockTiles(pan, "Footprint", X(r.x), Z(r.y), X(r.xMax), Z(r.yMax));
                WorldKit.Prop("Brine", pan, P(r.center.x, 0.03f, r.center.y), new Vector3(r.width - 0.3f, 0.04f, r.height - 0.3f), (r.x / 8) % 3 == 0 ? M.Salt : M.Brine);
                foreach ((Vector3 a, Vector3 b) in new[]
                         {
                             (P(r.x, 0.1f, r.y), P(r.xMax, 0.1f, r.y)), (P(r.x, 0.1f, r.yMax), P(r.xMax, 0.1f, r.yMax)),
                             (P(r.x, 0.1f, r.y), P(r.x, 0.1f, r.yMax)), (P(r.xMax, 0.1f, r.y), P(r.xMax, 0.1f, r.yMax)),
                         })
                    WorldKit.Beam("Rim", pan, a, b, 0.3f, 0.2f, M.Rock);
                Tag(pan, "salt_pan", "Salt pan: a shallow brine pool 6 x 4 m behind a low clay rim; pink brine or a white crust, depending on its stage. Modular.");
            }
            foreach (int x in new[] { 10, 22, 34, 46, 86, 98, 110, 122 })
            {
                Transform heap = SaltHeap(pans, x, 11);
                Tag(heap, "salt_heap", "A conical heap of raked white salt, waist to head high, a wooden rake stuck in it.");
            }
            Transform heapObj = WorldKit.Station(pans, "Salt heap", "tithe_salt_heap", X(54), Z(11));
            WorldKit.Cylinder("Heap", heapObj, P(54.5f, 0.45f, 11.5f), new Vector3(0.95f, 0.45f, 0.95f), M.Salt);
            WorldKit.Cylinder("Peak", heapObj, P(54.5f, 1.05f, 11.5f), new Vector3(0.5f, 0.2f, 0.5f), M.Salt, collider: false);
            Transform cart = WorldKit.Group(pans, "Salt cart");
            WorldKit.Prop("Bed", cart, P(58.5f, 0.8f, 13f), new Vector3(1.2f, 0.4f, 2.2f), M.Planks);
            foreach (int s in new[] { -1, 1 })
                WorldKit.Cylinder("Wheel", cart, P(58.5f + s * 0.7f, 0.5f, 13f), new Vector3(1f, 0.05f, 1f), M.Timber, collider: false).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            WorldKit.Prop("Load", cart, P(58.5f, 1.15f, 13f), new Vector3(1f, 0.35f, 2f), M.Salt);
            WorldKit.BlockTiles(cart, "Footprint", X(58), Z(12), X(59), Z(14));
            Tag(cart, "salt_cart", "Two-wheeled handcart heaped with salt sacks.");

            // The Saltery, by the road to Ascia: where Tithe's dead are salted and wrapped before they
            // go out to the flats, to be present when the debt is called in. Quiet, white, very clean.
            Transform saltery = WorldKit.Group(outside, "The Saltery");
            var f = new WorldKit.Facade { Walls = M.Whitewash, Roof = M.DomeWhite, Trim = M.Ink, Windows = null };
            Transform house = Building(saltery, "Saltery", 68, 7, 79, 16, 5f, f, WorldKit.RoofKind.Dome);
            Tag(house, "saltery", "The Saltery: a low, windowless whitewashed house with a white dome and a black door; salt sacks stacked by the wall. Where the dead are salted.");
            Transform door = WorldKit.Station(saltery, "Saltery door", "tithe_saltery_door", X(73), Z(6));
            Object.DestroyImmediate(door.GetComponent<MapBlocker>()); // you knock from the road
            WorldKit.Prop("Door", saltery, P(73.5f, 1.2f, 6.95f), new Vector3(1.2f, 2.4f, 0.08f), M.Ink);
            Transform bier = WorldKit.Group(saltery, "Bier cart");
            WorldKit.BlockTiles(bier, "Footprint", X(80), Z(5), X(82), Z(6));
            WorldKit.Prop("Bed", bier, P(81f, 0.7f, 5.5f), new Vector3(2.2f, 0.2f, 0.9f), M.Planks);
            for (int i = 0; i < 2; i++)
                WorldKit.Prop("Shroud", bier, P(80.5f + i, 0.95f, 5.5f), new Vector3(0.45f, 0.3f, 0.8f), M.Canvas);
            Tag(bier, "bier_cart", "Low cart with two shrouded, salt-packed bodies, waiting for the road to Ascia.");
            for (int i = 0; i < 4; i++)
                WorldKit.Prop("Salt sacks", saltery, P(80f + i * 0.6f, 0.3f, 15f), new Vector3(0.5f, 0.6f, 0.7f), M.Canvas);
            // A waymarker on the Ascia road.
            Transform marker = WorldKit.Group(outside, "Ascia waymarker");
            WorldKit.Box("Stone", marker, P(127.5f, 0.9f, 4.5f), new Vector3(0.5f, 1.8f, 0.5f), M.SaltStone).AddComponent<MapBlocker>();
            WorldKit.Prop("Band", marker, P(127.5f, 1.5f, 4.5f), new Vector3(0.55f, 0.12f, 0.55f), M.Ink);
            Tag(marker, "waymarker", "Square salt-stone waymarker, black band, cut with the way to Ascia.");
        }

        private static Transform Scrub(Transform parent, int lx, int lz)
        {
            Transform t = WorldKit.Group(parent, "Saltbush");
            WorldKit.Prop("Bush", t, P(lx + 0.5f, 0.25f, lz + 0.5f), new Vector3(0.9f, 0.5f, 0.8f), M.Scrub, lx * 13f);
            WorldKit.Prop("Bush", t, P(lx + 0.3f, 0.2f, lz + 0.7f), new Vector3(0.6f, 0.4f, 0.6f), M.Scrub, lz * 7f);
            return t;
        }

        private static Transform SaltHeap(Transform parent, int lx, int lz)
        {
            Transform heap = WorldKit.Group(parent, "Salt heap");
            WorldKit.BlockTiles(heap, "Footprint", X(lx), Z(lz), X(lx) + 1, Z(lz) + 1);
            for (int i = 0; i < 4; i++)
                WorldKit.Cylinder("Tier", heap, P(lx + 0.5f, 0.2f + i * 0.35f, lz + 0.5f), new Vector3(1f - i * 0.22f, 0.18f, 1f - i * 0.22f), M.Salt, collider: false);
            WorldKit.Beam("Rake", heap, P(lx + 0.8f, 0.2f, lz + 0.2f), P(lx + 0.4f, 1.8f, lz + 0.6f), 0.05f, 0.05f, M.Timber);
            return heap;
        }

        // ------------------------------------------------------------------ the walls and gates

        private static void BuildWalls(Transform map)
        {
            Transform walls = WorldKit.Group(map, "City walls");
            const float H = 8f;
            int[] Span(int from, int to)
            {
                var list = new List<int>();
                for (int i = from; i < to; i++) list.Add(i);
                return list.ToArray();
            }
            const string WallNote = "Landward curtain wall, salt-white stone, a black string course, battlements both sides. Modular: straight 4 m piece, corner, end. 8 m, one tile thick.";
            Tag(WorldKit.CityWall(walls, "West wall", X(WallW), Z(WallS), X(WallW), Z(Mouth), H, M.SaltStone, M.Paving, Span(Z(GateWz0), Z(GateWz1))), "city_wall", WallNote);
            Tag(WorldKit.CityWall(walls, "South wall", X(WallW), Z(WallS), X(WallE + 1), Z(WallS), H, M.SaltStone, M.Paving, Span(X(GateSx0), X(GateSx1))), "city_wall", null);
            Tag(WorldKit.CityWall(walls, "Sea wall", X(WallE), Z(WallS), X(WallE), Z(Mouth), 6f, M.SaltStone, M.Paving), "sea_wall",
                "Sea wall along the open coast, 6 m, a sloped footing in the surf.");

            (int x, int z, bool round)[] towers =
            {
                (4, 18, true), (126, 18, true), (4, 116, true),
                (4, 40, false), (4, 92, false), (30, 18, false), (96, 18, false), (126, 50, false), (126, 80, false),
            };
            foreach ((int x, int z, bool big) in towers)
            {
                Transform t = WorldKit.Tower(walls, big ? "Corner tower" : "Wall tower", X(x), Z(z), big ? 4 : 3, big ? 12f : 10f, M.SaltStone,
                    M.Paving, M.Ink, WorldKit.RoofKind.Battlements, M.Ink);
                Tag(t, big ? "corner_tower" : "wall_tower", big
                    ? "Corner bastion, 4 x 4 tiles, 12 m, battered base, black-banded corbels and battlements; a black banner on feast days."
                    : "Square wall tower, 3 x 3, 10 m, black slits, battlements.");
            }
            Gate(walls, "The Bay Gate", new Vector3(X(WallW) + 0.5f, 0f, Z(GateWz0)), new Vector3(X(WallW) + 0.5f, 0f, Z(GateWz1)), alongX: false, outward: -1);
            Gate(walls, "The Salt Gate", new Vector3(X(GateSx0), 0f, Z(WallS) + 0.5f), new Vector3(X(GateSx1), 0f, Z(WallS) + 0.5f), alongX: true, outward: -1);
        }

        /// <summary>A gatehouse: two square towers with domed tops, a pointed arch, a black-and-gold banner on each tower, and a toll board outside.</summary>
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
                Transform t = WorldKit.Tower(g, "Gate tower", tx, tz, 3, 11f, M.SaltStone, M.DomeWhite, M.Ink, WorldKit.RoofKind.Dome, M.Ink);
                WorldKit.Banner(t, c + outDir * 1.55f + Vector3.up * 10f, alongX ? (outward > 0 ? 0f : 180f) : (outward > 0 ? 90f : -90f), 6f, 1.3f, M.Ink, M.Gold, M.Iron);
            }
            WorldKit.Arch(g, a, b, 0f, 4.4f, 9f, 1.4f, M.SaltStone);
            WorldKit.Prop("Gate walk", g, (a + b) / 2f + Vector3.up * 9f, alongX ? new Vector3(Vector3.Distance(a, b), 0.3f, 1.6f) : new Vector3(1.6f, 0.3f, Vector3.Distance(a, b)), M.Paving);
            WorldKit.Battlements(g, a + Vector3.up * 9.15f + outDir * 0.7f, b + Vector3.up * 9.15f + outDir * 0.7f, M.SaltStone, 0.5f, 0.6f, 0.25f);
            WorldKit.Prop("Gold scale", g, (a + b) / 2f + outDir * 0.75f + Vector3.up * 7.2f, alongX ? new Vector3(1.6f, 1.2f, 0.08f) : new Vector3(0.08f, 1.2f, 1.6f), M.Gold);
            Tag(g, "gatehouse", "Gatehouse: a 4 m pointed arch between two 11 m salt-white towers with small white domes, a gold scales emblem over the arch, black banners with gold scales.");
        }

        // ------------------------------------------------------------------ the harbour

        private static void BuildHarbour(WorldKit kit, Transform map)
        {
            Transform g = WorldKit.Group(map, "The harbour");
            WorldKit.Patch("West quay", g, X(WallW + 1), Z(Harbour.y), X(Harbour.x), Z(Mouth), M.Paving, 0.006f);
            WorldKit.Patch("South quay", g, X(Harbour.x), Z(Harbour.y - 4), X(Harbour.xMax + 4), Z(Harbour.y), M.Paving, 0.006f);
            WorldKit.Patch("Mole", g, X(Harbour.x), Z(Harbour.yMax), X(Harbour.xMax + 4), Z(Mouth), M.Paving, 0.006f);
            Tag(g, "harbour_paving", "Harbour paving: big pale flags, iron mooring rings let into the edge, worn smooth by carts.");
            // The light at the harbour mouth, and a small tower on the other side of it.
            Transform light = WorldKit.Tower(g, "Harbour light", X(HarbourMouthX0 - 3), Z(Harbour.yMax), 3, 18f, M.Whitewash, M.Paving, M.Ink, WorldKit.RoofKind.Flat, M.Ink);
            WorldKit.Prop("Lantern", light, P(HarbourMouthX0 - 1.5f, 19f, Harbour.yMax + 1.5f), new Vector3(1.6f, 1.8f, 1.6f), M.Lamp);
            WorldKit.Dome(light, P(HarbourMouthX0 - 1.5f, 19.9f, Harbour.yMax + 1.5f), 1f, 1.2f, M.DomeWhite, M.Gold, 4);
            FxKit.PointLight(light, "Harbour light", P(HarbourMouthX0 - 1.5f, 19f, Harbour.yMax + 1.5f), new Color(1f, 0.9f, 0.7f), 3f, 14f, flicker: 0.05f);
            Tag(light, "harbour_light", "Harbour lighthouse: slim whitewashed tower, 18 m, black banding, a glazed lantern with a little white dome; lit day and night.");
            Transform post = WorldKit.Tower(g, "Mole tower", X(HarbourMouthX1), Z(Harbour.yMax), 3, 9f, M.SaltStone, M.Paving, M.Ink, WorldKit.RoofKind.Battlements, M.Ink);
            Tag(post, "mole_tower", "Squat guard tower at the harbour mouth, 9 m, battlemented.");

            // Ships in the basin: merchantmen moored stern-on to the quays, a Tithian galley.
            Tag(WorldKit.Boat(g, P(34f, 0f, 106f), 12f, 3.6f, 0f, M.Hull, M.Planks, M.Sail), "merchantman", "Round-bellied merchant ship, 12-16 m, one or two masts with furled pale sails; cargo nets on deck.");
            Tag(WorldKit.Boat(g, P(39f, 0f, 104f), 14f, 4f, 0f, M.Hull, M.Planks, M.Sail), "merchantman", null);
            Tag(WorldKit.Boat(g, P(52f, 0f, 103f), 16f, 3.2f, 0f, M.Hull, M.Planks, M.Ink), "tithe_galley", "Tithe's war galley: long and low, black hull band, a black sail with the gold scales furled on the yard, oars shipped.");
            Tag(WorldKit.Boat(g, P(58f, 0f, 108f), 9f, 2.8f, 0f, M.Hull, M.Planks, M.Sail), "merchantman", null);
            foreach (Vector2Int b in new[] { new Vector2Int(33, 97), new Vector2Int(41, 97), new Vector2Int(51, 97), new Vector2Int(57, 97), new Vector2Int(29, 104), new Vector2Int(62, 105) })
            {
                Transform bollard = WorldKit.Group(g, "Bollard");
                WorldKit.Box("Bollard", bollard, P(b.x + 0.5f, 0.35f, b.y + 0.5f), new Vector3(0.35f, 0.7f, 0.35f), M.Iron).AddComponent<MapBlocker>();
                Tag(bollard, "bollard", "Iron mooring bollard.");
            }
            Tag(WorldKit.Crane(g, X(36), Z(96), 0f, M.Timber, M.Iron), "quay_crane", "Timber treadwheel crane with a jib over the water and a hanging net of bales.");
            Tag(WorldKit.Crane(g, X(48), Z(96), 0f, M.Timber, M.Iron), "quay_crane", null);
            foreach (Vector2Int c in new[] { new Vector2Int(31, 95), new Vector2Int(44, 94), new Vector2Int(55, 95), new Vector2Int(26, 100), new Vector2Int(27, 110), new Vector2Int(63, 100) })
                kit.Clutter(g, X(c.x), Z(c.y), M.Planks, M.Hull);

            // Galley sheds on the west quay: long open-fronted boat sheds for the fleet.
            foreach (int z in new[] { 98, 106 })
            {
                Transform shed = Building(g, "Galley shed", WallW + 2, z, 22, z + 7, 6f, M.Warehouse, WorldKit.RoofKind.Gable);
                WorldKit.Prop("Open front", shed, P(22.04f, 2.4f, z + 3.5f), new Vector3(0.08f, 4.2f, 5.5f), M.Window);
                Tag(shed, "galley_shed", "Galley shed: long salt-stone shed, low tiled roof, its whole east end open on the quay; a hull on a slipway inside.");
            }
        }

        // ------------------------------------------------------------------ the toll quay and the Toll House

        private static void BuildTollQuay(WorldKit kit, Transform map)
        {
            // Every ship leaving the bay heaves to here, under the chain tower, and pays.
            Transform g = WorldKit.Group(map, "The toll quay");
            WorldKit.Patch("Toll quay", g, X(Harbour.xMax + 4), Z(112), X(WallE), Z(Mouth), M.Paving, 0.006f);
            WorldKit.Patch("Street", g, X(Harbour.xMax), Z(Exchange.yMax), X(WallE), Z(100), M.Paving, 0.006f);
            foreach (int x in new[] { 70, 78, 98, 106 })
            {
                Transform bollard = WorldKit.Group(g, "Bollard");
                WorldKit.Box("Bollard", bollard, P(x + 0.5f, 0.35f, Mouth - 0.5f), new Vector3(0.35f, 0.7f, 0.35f), M.Iron).AddComponent<MapBlocker>();
                Tag(bollard, "bollard", null);
            }
            // Ships waiting their turn in the Mouth.
            Tag(WorldKit.Boat(g, P(74f, 0f, 121.5f), 16f, 4.6f, 90f, M.Hull, M.Planks, M.Sail), "merchantman", null);
            Tag(WorldKit.Boat(g, P(102f, 0f, 121.5f), 14f, 4.2f, 90f, M.Hull, M.Planks, M.Sail), "merchantman", null);
            Tag(WorldKit.Boat(g, P(60f, 0f, 131f), 18f, 5f, 80f, M.Hull, M.Planks, M.Sail), "merchantman", null);
            Tag(WorldKit.Boat(g, P(96f, 0f, 136f), 15f, 4.5f, 100f, M.Hull, M.Planks, M.Canvas), "merchantman", null);
            Tag(WorldKit.Boat(g, P(30f, 0f, 128f), 9f, 3f, 70f, M.Hull, M.Planks, M.Ink), "toll_cutter", "Toll cutter: a fast black-sailed boat that runs out to ships that don't stop.");

            // The Toll House: where ships' masters pay. A long hall facing the quay; inside, the great
            // scales, the tollmen's counters, and the board of what everything costs.
            var f = new WorldKit.Facade { Walls = M.SaltStone, Roof = M.DomeWhite, Trim = M.Ink, Gable = M.SaltStone, Pitch = 22f };
            Transform house = Hall(g, "The Toll House", 78, 100, 96, 112, 7f, f, M.Paving, new WorldKit.Door(WorldKit.Side.North, X(86), X(88)));
            Tag(house, "toll_house", "The Toll House: long salt-stone hall, 18 x 12 tiles, 7 m, low white roof, black string course, a wide door onto the quay with a gold scales above.");
            WorldKit.Prop("Gold scale", house, P(87.5f, 5.6f, 112.1f), new Vector3(1.8f, 1.3f, 0.08f), M.Gold);
            Transform scales = WorldKit.Group(house, "The great scales");
            WorldKit.BlockTiles(scales, "Footprint", X(85), Z(104), X(90), Z(106));
            WorldKit.Prop("Post", scales, P(87.5f, 1.8f, 105f), new Vector3(0.3f, 3.6f, 0.3f), M.Bronze);
            WorldKit.Prop("Beam", scales, P(87.5f, 3.5f, 105f), new Vector3(5f, 0.18f, 0.18f), M.Bronze);
            foreach (int s in new[] { -1, 1 })
            {
                WorldKit.Beam("Chain", scales, P(87.5f + s * 2.3f, 3.45f, 105f), P(87.5f + s * 2.3f, 1.1f, 105f), 0.04f, 0.04f, M.Iron);
                WorldKit.Cylinder("Pan", scales, P(87.5f + s * 2.3f, 1f, 105f), new Vector3(1.6f, 0.06f, 1.6f), M.Bronze, collider: false);
            }
            WorldKit.Prop("Bales", scales, P(85.2f, 1.25f, 105f), new Vector3(0.9f, 0.5f, 0.9f), M.Goods);
            Tag(scales, "great_scales", "The Toll House scales: a 5 m bronze balance beam on a post, two great pans on chains; cargo samples weighed here.");
            Transform counters = WorldKit.Group(house, "Counters");
            foreach (int x in new[] { 80, 92 })
            {
                WorldKit.BlockTiles(counters, "Footprint", X(x), Z(102), X(x + 3), Z(103));
                WorldKit.Prop("Counter", counters, P(x + 1.5f, 0.55f, 102.5f), new Vector3(3f, 1.1f, 0.8f), M.Timber);
                WorldKit.Prop("Ledger", counters, P(x + 1.5f, 1.15f, 102.5f), new Vector3(0.6f, 0.08f, 0.4f), M.Ink);
            }
            Tag(counters, "toll_counter", "Tollman's counter: dark timber, a chained ledger, brass coin weights.");
            FxKit.PointLight(house, "Lamps", P(87f, 5f, 106f), new Color(1f, 0.92f, 0.78f), 2.5f, 10f);
            Transform board = WorldKit.Station(g, "The toll board", "tithe_toll_board", X(84), Z(113));
            WorldKit.Box("Board", board, board.position + new Vector3(0f, 1.2f, 0f), new Vector3(0.95f, 1.6f, 0.15f), M.Ink);
            WorldKit.Prop("Lettering", board, board.position + new Vector3(0f, 1.25f, 0.09f), new Vector3(0.75f, 1.3f, 0.02f), M.Gold);
            Tag(board, "toll_board", "A tall black board, gilt lettering: the toll on every cargo, rewritten each season.");

            // Shipping houses along the street behind: the merchant houses of the Mouth.
            Transform houses = WorldKit.Group(g, "Shipping houses");
            foreach ((int x0, int x1) in new[] { (66, 71), (72, 77), (98, 104), (105, 110) })
            {
                Transform h = Building(houses, "Shipping house", x0, 101, x1, 111, kit.Range(10f, 12.5f), M.Shipping);
                Hoist(h, (x0 + x1) / 2f, 111f, 9f);
                Tag(h, "shipping_house", "Shipping house: tall, narrow merchant house, 4 storeys of whitewash with ink-blue bands, a flat roof terrace with an awning, a hoist beam over the loading door.");
            }
        }

        /// <summary>A hoist beam and a hanging bale over a loading door on the given face (z = face).</summary>
        private static void Hoist(Transform parent, float lx, float face, float y)
        {
            WorldKit.Prop("Hoist beam", parent, P(lx, y, face + 0.6f), new Vector3(0.22f, 0.22f, 1.2f), M.Timber);
            WorldKit.Beam("Rope", parent, P(lx, y, face + 1.1f), P(lx, y - 2.5f, face + 1.1f), 0.04f, 0.04f, M.Iron);
            WorldKit.Prop("Bale", parent, P(lx, y - 2.8f, face + 1.1f), new Vector3(0.6f, 0.5f, 0.6f), M.Goods);
            WorldKit.Prop("Loading door", parent, P(lx, y - 1.4f, face + 0.03f), new Vector3(1.2f, 2f, 0.06f), M.Timber);
        }

        // ------------------------------------------------------------------ the chain

        private static void BuildChain(Transform map)
        {
            // The Tollkeep: the great tower at the corner of the Mouth that holds one end of the chain.
            // At peace the chain lies slack across the channel on a line of floats; in war it is
            // wound up from the capstan house and the bay is shut.
            Transform g = WorldKit.Group(map, "The Tollkeep and the chain");
            Transform keep = WorldKit.Tower(g, "The Tollkeep", X(120), Z(106), 7, 24f, M.SaltStone, M.Paving, M.Ink, WorldKit.RoofKind.Battlements, M.Ink);
            WorldKit.Banner(keep, P(123.5f, 22f, 105.95f), 180f, 10f, 2.2f, M.Ink, M.Gold, M.Iron);
            WorldKit.Banner(keep, P(119.95f, 22f, 109.5f), -90f, 10f, 2.2f, M.Ink, M.Gold, M.Iron);
            WorldKit.Brazier(keep, P(123.5f, 24f, 109.5f), 0.8f, M.Iron, M.Coals, M.Flame, M.Ember);
            WorldKit.Fades(keep, 0.15f).guardRadius = 5f;
            Tag(keep, "tollkeep", "Hero building. The Tollkeep: a 7 x 7 tile, 24 m chain tower at the corner of the Mouth, battered base, black banding, battlements and a beacon; " +
                "two 10 m black banners with gold scales. The chain comes out of a hawse in its north face.");

            Transform house = Building(g, "Capstan house", 108, 104, 118, 112, 7f, M.Fort, WorldKit.RoofKind.Battlements);
            Tag(house, "capstan_house", "Capstan house beside the Tollkeep: squat fortified block, 7 m, battlemented; inside, the great capstan that winds the chain.");
            Transform winch = WorldKit.Station(g, "Chain capstan", "chain_winch", X(118), Z(113));
            WorldKit.Cylinder("Drum", winch, P(118.5f, 0.6f, 113.5f), new Vector3(0.9f, 0.6f, 0.9f), M.Timber);
            for (int i = 0; i < 4; i++)
                WorldKit.Prop("Bar", winch, P(118.5f, 1f, 113.5f), new Vector3(2f, 0.1f, 0.1f), M.Timber, i * 45f);
            Tag(winch, "chain_capstan", "Spoked timber capstan on the quay, bars for eight men, the chain's tail wound round it.");

            // The chain: out of the hawse, down over the quay edge, then lying across the Mouth on floats.
            Transform chain = WorldKit.Group(g, "The chain");
            var hawse = P(123.5f, 7f, 113f);
            var edge = P(123.5f, 0.6f, Mouth + 0.2f);
            ChainLinks(chain, hawse, edge, 0.5f);
            WorldKit.Box("Hawse post", chain, P(123.5f, 0.6f, Mouth - 0.5f), new Vector3(0.8f, 1.2f, 0.8f), M.Iron).AddComponent<MapBlocker>();
            for (float z = Mouth + 3f; z < 168f; z += 4f)
            {
                WorldKit.Cylinder("Float", chain, P(123.5f, 0.05f, z), new Vector3(1.1f, 0.15f, 1.1f), M.Timber, collider: false);
                ChainLinks(chain, P(123.5f, 0.1f, z - 4f), P(123.5f, 0.1f, z), 0f);
            }
            Tag(chain, "harbour_chain", "The chain across the Mouth: links as long as a forearm, black iron; slack at peace, lying on a line of timber floats 4 m apart. Modular: a 4 m run + a float.");
        }

        private static void ChainLinks(Transform parent, Vector3 a, Vector3 b, float sag)
        {
            int n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.45f));
            for (int i = 0; i < n; i++)
            {
                float t0 = i / (float)n, t1 = (i + 1) / (float)n;
                Vector3 p0 = Vector3.Lerp(a, b, t0) + Vector3.down * sag * Mathf.Sin(t0 * Mathf.PI);
                Vector3 p1 = Vector3.Lerp(a, b, t1) + Vector3.down * sag * Mathf.Sin(t1 * Mathf.PI);
                WorldKit.Beam("Link", parent, p0, p1, i % 2 == 0 ? 0.22f : 0.08f, i % 2 == 0 ? 0.08f : 0.22f, M.Iron);
            }
        }

        // ------------------------------------------------------------------ the Exchange

        private static void BuildExchange(Transform map)
        {
            // The city's heart is a market for money: a long square with a loggia down each side where
            // the changers sit, and in the middle the Balance, the public scale every Tithian is
            // weighed against in the end.
            Transform g = WorldKit.Group(map, "The Exchange");
            WorldKit.Patch("Paving", g, X(Exchange.x), Z(Exchange.y), X(Exchange.xMax), Z(Exchange.yMax), M.Paving, 0.006f);
            for (int x = Exchange.x + 2; x < Exchange.xMax - 1; x += 4)
                WorldKit.Prop("Paving band", g, P(x + 0.5f, 0.012f, Exchange.center.y), new Vector3(0.3f, 0.01f, Exchange.height - 4f), M.Ink);
            Tag(g, "exchange_paving", "The Exchange's paving: pale salt-stone in a grid of thin black marble bands, like ruled paper.");

            // The Balance.
            Transform balance = WorldKit.Group(g, "The Balance");
            Vector3 c = P(Balance.x + 0.5f, 0f, Balance.y + 0.5f);
            WorldKit.Box("Plinth", balance, c + Vector3.up * 0.6f, new Vector3(2.9f, 1.2f, 2.9f), M.SaltStone).AddComponent<MapBlocker>();
            // You read the Balance at the bronze plaque on the plinth's north face (the side facing the harbour).
            Transform plaque = WorldKit.Station(balance, "The Balance (plaque)", "tithe_balance", X(Balance.x), Z(Balance.y + 2));
            WorldKit.Box("Plaque", plaque, P(Balance.x + 0.5f, 0.5f, Balance.y + 2.3f), new Vector3(1f, 1f, 0.4f), M.Bronze);
            WorldKit.Prop("Plinth band", balance, c + Vector3.up * 1.15f, new Vector3(3.1f, 0.15f, 3.1f), M.Ink);
            WorldKit.Prop("Column", balance, c + Vector3.up * 4.2f, new Vector3(0.6f, 6f, 0.6f), M.Bronze);
            WorldKit.Prop("Beam", balance, c + Vector3.up * 7.3f, new Vector3(8f, 0.3f, 0.35f), M.Bronze);
            WorldKit.Prop("Pointer", balance, c + Vector3.up * 8.2f, new Vector3(0.15f, 1.6f, 0.15f), M.Gold);
            foreach (int s in new[] { -1, 1 })
            {
                Vector3 hang = c + new Vector3(s * 3.8f, 7.2f, 0f);
                foreach (float dz in new[] { -0.6f, 0.6f })
                    WorldKit.Beam("Chain", balance, hang, hang + new Vector3(0f, -3.4f, dz), 0.05f, 0.05f, M.Iron);
                WorldKit.Cylinder("Pan", balance, hang + Vector3.down * 3.5f, new Vector3(2.2f, 0.08f, 2.2f), M.Bronze, collider: false);
            }
            // One pan holds a gold coin-stack, the other a salt heap: and they hang level.
            WorldKit.Cylinder("Coins", balance, c + new Vector3(-3.8f, 3.85f, 0f), new Vector3(0.7f, 0.15f, 0.7f), M.Gold, collider: false);
            WorldKit.Cylinder("Salt", balance, c + new Vector3(3.8f, 3.9f, 0f), new Vector3(0.9f, 0.2f, 0.9f), M.Salt, collider: false);
            Tag(balance, "the_balance", "Hero piece. The Balance: a 9 m bronze public scale on a salt-stone plinth, an 8 m beam, two great pans on chains (gold coins in one, salt in the other) hanging level; gold pointer.");

            // The loggias down the long sides, with changers' tables in their shade.
            Transform loggias = WorldKit.Group(g, "Loggias");
            Tag(WorldKit.Colonnade(loggias, "North loggia", T(Exchange.x + 2, Exchange.yMax - 2), T(Exchange.xMax - 3, Exchange.yMax - 2), 5f, M.Whitewash, M.SaltStone, 3, 1.6f),
                "loggia", "Loggia: a run of slim white columns 5 m high carrying a salt-stone entablature and a flat roof; shade for the changers. Modular: column + 3 m bay.");
            Tag(WorldKit.Colonnade(loggias, "South loggia", T(Exchange.x + 2, Exchange.y + 1), T(Exchange.xMax - 3, Exchange.y + 1), 5f, M.Whitewash, M.SaltStone, 3, -1.6f), "loggia", null);
            Transform tables = WorldKit.Group(g, "Changers' tables");
            for (int x = Exchange.x + 3; x < Exchange.xMax - 4; x += 6)
            {
                if (Mathf.Abs(x + 1 - Axis) < 3f) continue;
                foreach (int z in new[] { Exchange.yMax - 1, Exchange.y })
                {
                    Transform t = WorldKit.Group(tables, "Changer's table");
                    WorldKit.BlockTiles(t, "Footprint", X(x), Z(z), X(x + 2), Z(z + 1));
                    WorldKit.Prop("Table", t, P(x + 1f, 0.45f, z + 0.5f), new Vector3(1.8f, 0.9f, 0.8f), M.Timber);
                    WorldKit.Prop("Cloth", t, P(x + 1f, 0.91f, z + 0.5f), new Vector3(1.9f, 0.03f, 0.9f), M.InkBlue);
                    WorldKit.Cylinder("Coins", t, P(x + 0.6f, 0.98f, z + 0.5f), new Vector3(0.18f, 0.06f, 0.18f), M.Gold, collider: false);
                    WorldKit.Prop("Little scale", t, P(x + 1.4f, 1.1f, z + 0.5f), new Vector3(0.5f, 0.3f, 0.1f), M.Bronze);
                    Tag(t, "changer_table", "Money-changer's table: dark wood, an ink-blue cloth, stacks of coin from every realm, a little brass scale.");
                }
            }
            // Black banners with the gold scales on poles round the Balance.
            foreach (Vector2Int p in new[] { new Vector2Int(74, 72), new Vector2Int(86, 72), new Vector2Int(74, 85), new Vector2Int(86, 85) })
                Tag(WorldKit.BannerPole(g, X(p.x), Z(p.y), 6f, p.y < Balance.y ? 0f : 180f, M.Iron, M.Ink, M.Gold, M.Gold), "banner_pole",
                    "Iron pole, 6 m, gold finial, a long black banner with Tithe's gold scales.");
            // Shade sails over the street from the square down to the quay.
            Transform sails = WorldKit.Group(g, "Shade sails");
            for (float z = 95.5f; z < 112f; z += 4f)
                WorldKit.ShadeSail(sails, P(Harbour.xMax + 0.2f, 6.5f, z), P(77.8f, 6.5f, z + 1f), 2.6f, z % 8f < 4f ? M.Canvas : M.InkBlue);
            Tag(sails, "shade_sail", "Shade sail: a square of canvas (white or ink blue) strung on ropes between houses over a street, sagging. Everywhere in Tithe.");
        }

        // ------------------------------------------------------------------ the Ledger House

        private static void BuildLedgerHouse(Transform map)
        {
            // Where every soul's account is kept, and the auditors sit. Severe outside, a forest of
            // shelves inside, and at the far end the Ledger itself on a lectern.
            Transform g = WorldKit.Group(map, "The Ledger House");
            Transform hall = Hall(g, "Hall", 44, 70, Exchange.x, 90, 9f, M.Official, M.Paving, new WorldKit.Door(WorldKit.Side.East, Z(79), Z(81)));
            Tag(hall, "ledger_house", "The Ledger House: a severe salt-stone hall, 18 x 20 tiles, 9 m, black string course, few windows, a low white roof; a tall door onto the Exchange with the gold scales over it.");
            WorldKit.Prop("Gold scale", hall, P(Exchange.x + 0.12f, 6.8f, 80.5f), new Vector3(0.08f, 1.4f, 2f), M.Gold);
            Transform shelves = WorldKit.Group(hall, "Shelves");
            for (int z = 72; z <= 87; z += 3)
                foreach ((int a, int b) in new[] { (46, 52), (54, 59) })
                {
                    if (z == 78 || z == 81) continue; // the aisle from the door
                    Transform s = WorldKit.Group(shelves, "Shelf");
                    WorldKit.BlockTiles(s, "Footprint", X(a), Z(z), X(b), Z(z + 1));
                    WorldKit.Prop("Case", s, P((a + b) / 2f, 1.6f, z + 0.5f), new Vector3(b - a - 0.1f, 3.2f, 0.7f), M.Timber);
                    for (int r = 0; r < 4; r++)
                        WorldKit.Prop("Ledgers", s, P((a + b) / 2f, 0.45f + r * 0.75f, z + 0.5f), new Vector3(b - a - 0.4f, 0.5f, 0.74f), r % 2 == 0 ? M.Ink : M.Goods);
                    Tag(s, "ledger_shelf", "Tall dark bookcase packed with identical black ledgers, a ladder rail along the top. ~3 m high.");
                }
            Transform desks = WorldKit.Group(hall, "Auditors' desks");
            foreach (int z in new[] { 79, 81 })
                foreach (int x in new[] { 52, 56 })
                {
                    WorldKit.BlockTiles(desks, "Footprint", X(x), Z(z == 79 ? 78 : 82), X(x + 1), Z(z == 79 ? 79 : 83));
                    WorldKit.Prop("Desk", desks, P(x + 0.5f, 0.5f, (z == 79 ? 78 : 82) + 0.5f), new Vector3(0.9f, 1f, 0.8f), M.Timber);
                    WorldKit.Prop("Book", desks, P(x + 0.5f, 1.02f, (z == 79 ? 78 : 82) + 0.5f), new Vector3(0.5f, 0.05f, 0.35f), M.Ink);
                }
            Tag(desks, "auditor_desk", "Auditor's sloped writing desk with an open ledger, an inkwell and a candle.");
            Transform ledger = WorldKit.Station(hall, "The Ledger", "tithe_ledger", X(46), Z(80));
            WorldKit.Box("Lectern", ledger, P(46.5f, 0.6f, 80.5f), new Vector3(0.8f, 1.2f, 1.4f), M.Ink);
            WorldKit.Prop("Book", ledger, P(46.7f, 1.3f, 80.5f), new Vector3(0.7f, 0.15f, 1.6f), M.Canvas).transform.rotation = Quaternion.Euler(0f, 0f, 15f);
            WorldKit.Cylinder("Candle", ledger, P(46.3f, 1.4f, 79.5f), new Vector3(0.1f, 0.2f, 0.1f), M.Wax, collider: false);
            FxKit.PointLight(ledger, "Candle", P(46.5f, 2f, 80f), new Color(1f, 0.8f, 0.5f), 2f, 5f, flicker: 0.15f);
            Tag(ledger, "the_ledger", "Hero prop. The Ledger: a vast open book (1.6 m across) on a black lectern, its pages ruled in columns, a single candle beside it.");
            FxKit.PointLight(hall, "Lamps", P(53f, 6f, 80f), new Color(1f, 0.9f, 0.75f), 2f, 12f);
        }

        // ------------------------------------------------------------------ the Treasury

        private static void BuildTreasury(Transform map)
        {
            // Tithe's treasury: the bank of the basin. Players bank here.
            Transform g = WorldKit.Group(map, "The Treasury");
            var f = new WorldKit.Facade { Walls = M.SaltStone, Roof = M.DomeWhite, Trim = M.Gold, Gable = M.SaltStone, Pitch = 22f };
            Transform hall = Hall(g, "Hall", Exchange.xMax, 72, 114, 86, 8f, f, M.Paving, new WorldKit.Door(WorldKit.Side.West, Z(78), Z(80)));
            Tag(hall, "treasury", "The Treasury: salt-stone hall, 14 x 14 tiles, 8 m, gold string course, a small white dome over the middle; the door has bronze leaves studded like coins.");
            WorldKit.Dome(hall, P(107f, 10.2f, 79f), 3.2f, 3.6f, M.DomeWhite, M.Gold).gameObject.AddComponent<RoofHider>().interior = new RectInt(X(Exchange.xMax), Z(72), 14, 14);
            Transform box = WorldKit.Station(hall, "Strongbox", "synod_strongbox", X(102), Z(84));
            WorldKit.Box("Chest", box, box.position + new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.8f, 0.7f), M.Iron);
            WorldKit.Prop("Bands", box, box.position + new Vector3(0f, 0.45f, 0f), new Vector3(0.95f, 0.1f, 0.75f), M.Gold);
            Transform counter = WorldKit.Group(hall, "Counter");
            WorldKit.BlockTiles(counter, "Footprint", X(108), Z(74), X(109), Z(85));
            WorldKit.Prop("Counter", counter, P(108.5f, 0.6f, 79.5f), new Vector3(0.8f, 1.2f, 11f), M.Timber);
            WorldKit.Prop("Grille", counter, P(108.5f, 1.8f, 79.5f), new Vector3(0.06f, 1.2f, 11f), M.Bronze);
            Tag(counter, "treasury_counter", "Long counter with a bronze grille, gaps for each clerk; ledgers and coin trays.");
            Transform vault = WorldKit.Group(hall, "Vault door");
            GameObject door = WorldKit.Cylinder("Door", vault, P(113.85f, 2f, 79.5f), new Vector3(3.2f, 0.12f, 3.2f), M.Iron, collider: false);
            door.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            WorldKit.Prop("Bolt", vault, P(113.7f, 2f, 79.5f), new Vector3(0.1f, 0.3f, 2.4f), M.Gold);
            Tag(vault, "vault_door", "Round iron vault door in the back wall, 3 m across, gold bolts. Closed.");
            FxKit.PointLight(hall, "Lamps", P(104f, 5f, 79f), new Color(1f, 0.88f, 0.65f), 2.5f, 10f);
        }

        // ------------------------------------------------------------------ the Chantry

        private static void BuildChantry(Transform map)
        {
            // Tithe's cathedral: it doesn't preach, it sings. A white vaulted hall under a great white
            // dome, choir stalls in tiers down both sides facing each other across the floor, and at
            // the far end the altar with an open ledger and a pair of scales. White light falls from
            // the dome's eye onto the floor.
            Transform g = WorldKit.Group(map, "The Chantry");
            const int x0 = 70, x1 = 91, z0 = 24, z1 = 52;
            const float h = 12f;
            WorldKit.Patch("Forecourt", g, X(64), Z(z1), X(97), Z(Exchange.y), M.Paving, 0.006f);
            Transform hall = Hall(g, "Hall", x0, z0, x1, z1, h, M.Church, M.Paving, new WorldKit.Door(WorldKit.Side.North, X(79), X(81)));
            Tag(hall, "chantry_hall", "Hero building, the Chantry: a whitewashed vaulted hall, 21 x 28 tiles, 12 m walls, low white roof, a gold cornice; tall lit windows.");
            WorldKit.Prop("Floor inlay", hall, P(Axis, 0.035f, 38f), new Vector3(3f, 0.01f, 26f), M.Ink);

            // Choir stalls in three stepped rows each side, facing the floor.
            Transform stalls = WorldKit.Group(hall, "Choir stalls");
            foreach (int side in new[] { -1, 1 })
                for (int row = 0; row < 3; row++)
                {
                    int x = side < 0 ? 76 - row : 84 + row;
                    Transform s = WorldKit.Group(stalls, "Stall row");
                    WorldKit.BlockTiles(s, "Footprint", X(x), Z(31), X(x + 1), Z(47));
                    WorldKit.Prop("Bench", s, P(x + 0.5f, 0.3f + row * 0.35f, 39f), new Vector3(0.8f, 0.6f + row * 0.7f, 16f), M.Timber);
                    WorldKit.Prop("Back", s, P(x + 0.5f - side * 0.4f, 1.1f + row * 0.35f, 39f), new Vector3(0.1f, 1f, 16f), M.Timber);
                }
            Tag(stalls, "choir_stalls", "Choir stalls: three tiers of dark oak benches each side, stepped up like a lecture hall, facing each other across the floor; 16 m long.");

            // The altar at the south end: an open ledger and a pair of scales, under a gold screen.
            Transform altar = WorldKit.Station(hall, "Altar", "chantry_altar", X(80), Z(26));
            WorldKit.Box("Altar", altar, P(Axis, 0.55f, 26.5f), new Vector3(2.9f, 1.1f, 0.95f), M.SaltStone);
            WorldKit.Prop("Altar cloth", altar, P(Axis, 1.12f, 26.5f), new Vector3(3.1f, 0.05f, 1.05f), M.Ink);
            WorldKit.Prop("Open ledger", altar, P(Axis, 1.2f, 26.5f), new Vector3(1.4f, 0.08f, 0.8f), M.Canvas);
            WorldKit.Prop("Scale beam", altar, P(Axis, 2.2f, 26.2f), new Vector3(2f, 0.08f, 0.08f), M.Gold);
            WorldKit.Prop("Scale post", altar, P(Axis, 1.7f, 26.2f), new Vector3(0.08f, 1f, 0.08f), M.Gold);
            WorldKit.Prop("Screen", altar, P(Axis, 4f, z0 + 0.3f), new Vector3(9f, 8f, 0.3f), M.Gold);
            WorldKit.Prop("Screen panel", altar, P(Axis, 4.5f, z0 + 0.48f), new Vector3(3f, 5f, 0.06f), M.Ink);
            for (int i = -3; i <= 3; i++)
                if (i != 0)
                    WorldKit.Cylinder("Candle", altar, P(Axis + i * 0.5f, 1.3f, 27f), new Vector3(0.1f, 0.18f, 0.1f), M.Wax, collider: false);
            Tag(altar, "chantry_altar", "The Chantry's altar: plain salt-stone block, a black cloth, an open ledger and a small gold balance; a 9 m gold screen behind with a black panel.");
            FxKit.PointLight(altar, "Candles", P(Axis, 2f, 27.5f), new Color(1f, 0.85f, 0.6f), 2f, 6f, flicker: 0.1f);

            // The great dome over the middle of the hall, on a drum of windows, and the light from its eye.
            Transform dome = WorldKit.Group(g, "The dome");
            var dc = P(Axis, h + 3.5f, 38f);
            WorldKit.Cylinder("Drum", dome, dc + Vector3.up * 1.6f, new Vector3(15f, 1.6f, 15f), M.Whitewash, collider: false);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                WorldKit.Prop("Drum window", dome, dc + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 1.6f, 7.52f), new Vector3(0.9f, 1.8f, 0.06f), M.WindowLit, a);
            }
            WorldKit.Dome(dome, dc + Vector3.up * 3.2f, 7.4f, 9f, M.DomeWhite, M.Gold, 8);
            dome.gameObject.AddComponent<RoofHider>().interior = new RectInt(X(x0), Z(z0), x1 - x0, z1 - z0);
            WorldKit.Fades(dome, 0.15f).guardRadius = 6f;
            Tag(dome, "chantry_dome", "Hero piece. The Chantry's dome: a 15 m white dome on a drum ringed with lit windows, a small lantern and a gold finial: the highest point in Tithe, seen from the sea.");
            (Light l, Renderer _) = FxKit.Shaft(hall, P(Axis, 0f, 38f), h + 4f, 1.8f, new Color(1f, 0.98f, 0.92f), M.Beam);
            l.intensity = 40f;
            FxKit.Dust(hall, P(Axis, 3f, 38f), new Vector3(3f, 6f, 3f), M.Mote);

            // The north front: a portico of columns under a pediment, and steps down to the forecourt.
            Transform front = WorldKit.Group(g, "Portico");
            Tag(WorldKit.Colonnade(front, "Portico columns", T(x0, z1 + 2), T(x1 - 1, z1 + 2), 8f, M.Whitewash, M.SaltStone, 4, -1.8f), "portico_column",
                "The Chantry's portico: six slim white columns 8 m high on a step, a salt-stone entablature.");
            WorldKit.GableRoof(front, X(x0) - 0.4f, Z(z1), X(x1) + 0.4f, Z(z1 + 3), 8.6f, M.DomeWhite, M.Whitewash, 18f, 0.3f);
            WorldKit.Prop("Pediment scale", front, P(Axis, 9.6f, z1 + 3.2f), new Vector3(2.2f, 1.1f, 0.08f), M.Gold);
            for (int i = 0; i < 3; i++)
                WorldKit.Prop("Step", front, P(Axis, 0.04f + i * 0.04f, z1 + 3.6f + i * 0.9f), new Vector3(x1 - x0 - i * 1.5f, 0.08f, 0.9f), M.SaltStone);
            Tag(front, "chantry_portico", "The Chantry's north front: a pedimented portico, a gold scales in the pediment, broad shallow steps.");
            // Two slender bell-less towers flanking the hall's north end: the choirs sing from their tops at the hours.
            foreach (int tx in new[] { x0 - 3, x1 })
            {
                Transform t = WorldKit.Tower(g, "Choir tower", X(tx), Z(z1 - 3), 3, 20f, M.Whitewash, M.DomeWhite, M.Ink, WorldKit.RoofKind.Dome, M.Gold);
                WorldKit.Fades(t, 0.15f).guardRadius = 3f;
                Tag(t, "choir_tower", "Choir tower: a slim whitewashed tower, 20 m, an open loggia at the top where the choir sings the hours, a small white dome and a gold finial. No bells.");
            }
        }

        // ------------------------------------------------------------------ the cloister and the Pontiff

        private static void BuildCloister(WorldKit kit, Transform map)
        {
            // East of the Chantry: the cloister of the choir, a square garth of white gravel round one
            // old salt-cedar, arcades on all four sides; the Pontiff of Tithe's house beyond it,
            // austere for the second most powerful priest in the world; and the song school.
            Transform g = WorldKit.Group(map, "The cloister");
            const int cx0 = 95, cz0 = 28, cx1 = 111, cz1 = 46;
            WorldKit.Patch("Walks", g, X(cx0), Z(cz0), X(cx1), Z(cz1), M.Paving, 0.006f);
            WorldKit.Patch("Garth", g, X(cx0 + 3), Z(cz0 + 3), X(cx1 - 3), Z(cz1 - 3), M.Salt, 0.01f);
            Tag(WorldKit.Colonnade(g, "Arcade S", T(cx0 + 2, cz0 + 2), T(cx1 - 3, cz0 + 2), 3.5f, M.Whitewash, M.SaltStone, 2, -1.8f), "cloister_arcade",
                "Cloister arcade: short white columns every 2 m, 3.5 m high, a lean-to tiled roof over the walk. Modular.");
            Tag(WorldKit.Colonnade(g, "Arcade N", T(cx0 + 2, cz1 - 3), T(cx1 - 3, cz1 - 3), 3.5f, M.Whitewash, M.SaltStone, 2, 1.8f), "cloister_arcade", null);
            Tag(WorldKit.Colonnade(g, "Arcade W", T(cx0 + 2, cz0 + 4), T(cx0 + 2, cz1 - 5), 3.5f, M.Whitewash, M.SaltStone, 2, -1.8f), "cloister_arcade", null);
            Tag(WorldKit.Colonnade(g, "Arcade E", T(cx1 - 3, cz0 + 4), T(cx1 - 3, cz1 - 5), 3.5f, M.Whitewash, M.SaltStone, 2, 1.8f), "cloister_arcade", null);
            Transform tree = WorldKit.Group(g, "Salt-cedar");
            var tc = P((cx0 + cx1) / 2f, 0f, (cz0 + cz1) / 2f);
            WorldKit.Box("Trunk", tree, tc + Vector3.up * 1.2f, new Vector3(0.45f, 2.4f, 0.45f), M.Bark).AddComponent<MapBlocker>();
            WorldKit.Beam("Limb", tree, tc + Vector3.up * 2f, tc + new Vector3(1.4f, 3.4f, 0.6f), 0.25f, 0.25f, M.Bark);
            WorldKit.Beam("Limb", tree, tc + Vector3.up * 2.2f, tc + new Vector3(-1.2f, 3.6f, -0.8f), 0.22f, 0.22f, M.Bark);
            foreach (Vector3 o in new[] { new Vector3(1.4f, 3.8f, 0.6f), new Vector3(-1.2f, 4f, -0.8f), new Vector3(0f, 4.4f, 0f) })
                WorldKit.Prop("Feathery crown", tree, tc + o, new Vector3(2.4f, 1.1f, 2.2f), M.Scrub, kit.Range(0f, 90f));
            Tag(tree, "salt_cedar", "An old salt-cedar (tamarisk): twisted grey trunk, a feathery grey-green crown. The only tree inside the walls.");

            Transform pontiff = Building(g, "The Pontiff's house", 96, 49, 114, 58, 9f, M.Official, WorldKit.RoofKind.Gable);
            WorldKit.Banner(pontiff, P(105f, 8.6f, 48.9f), 180f, 5f, 1.4f, M.Ink, M.Gold, M.Iron);
            Tag(pontiff, "pontiff_house", "The Pontiff of Tithe's house: a plain salt-stone range, 9 m, black string courses, a low white roof; one black banner. Austere on purpose.");
            Tag(Building(g, "Song school", 114, 26, 126, 42, 7f, M.House, WorldKit.RoofKind.Flat), "song_school",
                "The song school: whitewashed, flat-roofed, a row of tall windows; where Tithe's children learn to sing the account.");
        }

        // ------------------------------------------------------------------ the salt quarter and granaries

        private static void BuildSaltQuarter(WorldKit kit, Transform map)
        {
            // Inside the Salt Gate: the salt warehouses and the salt market on one side of the street,
            // the granaries on the other. Tithe sells salt to the world and buys every loaf it eats.
            Transform g = WorldKit.Group(map, "The salt quarter");
            WorldKit.Patch("Salt Street", g, X(GateSx0), Z(WallS + 1), X(GateSx1), Z(Exchange.y), M.Paving, 0.006f);
            WorldKit.Patch("Bay Road", g, X(WallW + 1), Z(GateWz0), X(Exchange.x), Z(GateWz1), M.Paving, 0.006f);
            foreach ((int x0, int z0, int x1, int z1) in new[] { (36, 22, 48, 32), (50, 22, 58, 32), (36, 34, 48, 42) })
                Tag(Building(g, "Salt warehouse", x0, z0, x1, z1, 7f, M.Warehouse, WorldKit.RoofKind.Gable), "salt_warehouse",
                    "Salt warehouse: long salt-stone shed, low tiled roof, big doors; white salt dust on everything near it.");
            // The salt market: heaps and stalls round a public weighbridge.
            WorldKit.Patch("Salt market", g, X(36), Z(44), X(58), Z(62), M.Crust, 0.008f);
            for (int i = 0; i < 4; i++)
            {
                Tag(WorldKit.Stall(g, X(38 + i * 5), Z(46), 2, 2, WorldKit.Side.North, M.Timber, i % 2 == 0 ? M.Canvas : M.InkBlue, M.Salt), "salt_stall",
                    "Salt stall: a timber counter under a canvas or ink-blue awning, sacks and white cakes of salt.");
                Tag(WorldKit.Stall(g, X(38 + i * 5), Z(58), 2, 2, WorldKit.Side.South, M.Timber, i % 2 == 0 ? M.InkBlue : M.Canvas, M.Salt), "salt_stall", null);
            }
            Transform bridge = WorldKit.Group(g, "Weighbridge");
            WorldKit.Prop("Plate", bridge, P(47f, 0.03f, 53f), new Vector3(4f, 0.06f, 3f), M.Iron);
            WorldKit.Box("Weigh-post", bridge, P(49.5f, 1.2f, 51.5f), new Vector3(0.6f, 2.4f, 0.6f), M.Timber).AddComponent<MapBlocker>();
            WorldKit.Prop("Arm", bridge, P(48.5f, 2.3f, 51.5f), new Vector3(2.2f, 0.12f, 0.12f), M.Bronze);
            Tag(bridge, "weighbridge", "Public weighbridge: an iron plate set in the paving for carts, a timber post with a bronze steelyard arm.");
            foreach (int x in new[] { 40, 52 })
                Tag(SaltHeap(g, x, 52), "salt_heap", null);

            // The granaries by the Bay Gate: the heartland's grain comes in here, under guard.
            foreach ((int x0, int z0, int x1, int z1) in new[] { (8, 22, 22, 34), (8, 36, 22, 48), (24, 22, 34, 34), (8, 50, 22, 62), (24, 50, 34, 62) })
            {
                Transform gr = Building(g, "Granary", x0, z0, x1, z1, 8f, M.Warehouse, WorldKit.RoofKind.Gable);
                WorldKit.Prop("Vent", gr, P((x0 + x1) / 2f, 6f, z1 + 0.05f), new Vector3(1.2f, 0.6f, 0.08f), M.Window);
                Tag(gr, "granary", "Granary: a raised salt-stone store on stone feet (vermin), low tiled roof, small vents; heartland grain, Tithe's weakness, under guard.");
            }
            kit.Clutter(g, X(24), Z(40), M.Planks, M.Hull);
            kit.Clutter(g, X(30), Z(44), M.Planks, M.Hull);
        }

        // ------------------------------------------------------------------ the town

        private static void BuildTown(WorldKit kit, Transform map)
        {
            // Tall whitewashed houses packed close, flat roofs with terraces and awnings, a few blue
            // domes; shade sails across the lanes; lamps at the corners.
            Transform g = WorldKit.Group(map, "Townhouses");
            (int x0, int z0, int x1, int z1)[] houses =
            {
                // west of the Exchange, between the Bay Road and the harbour
                (8, 72, 15, 80), (17, 72, 24, 80), (26, 72, 33, 79), (35, 72, 42, 80), (8, 82, 15, 92), (17, 82, 24, 92), (26, 82, 33, 92), (35, 82, 42, 92),
                // north-east, between the Treasury and the capstan house
                (102, 88, 108, 98), (110, 88, 116, 98), (118, 88, 126, 98), (116, 72, 126, 86),
                // east of the forecourt
                (98, 60, 104, 70), (106, 60, 112, 70), (114, 60, 120, 70), (122, 46, 127, 58), (116, 46, 121, 58),
                // south-west, by the Salt Gate
                (64, 22, 69, 32), (64, 34, 69, 46),
            };
            foreach ((int x0, int z0, int x1, int z1) in houses)
            {
                bool dome = kit.Chance(0.25);
                float eave = kit.Range(6.5f, 11f);
                Transform h = Building(g, dome ? "Domed house" : "Townhouse", x0, z0, x1, z1, eave, dome ? M.House2 : M.House, dome ? WorldKit.RoofKind.Dome : WorldKit.RoofKind.Flat);
                if (!dome)
                {
                    // A roof terrace awning on most flat roofs.
                    float ax = x0 + 1.5f, az = z0 + 1.5f;
                    foreach (Vector3 o in new[] { new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(0f, 0f, 2f), new Vector3(2f, 0f, 2f) })
                        WorldKit.Prop("Awning post", h, P(ax + o.x, eave + 0.9f, az + o.z), new Vector3(0.08f, 1.8f, 0.08f), M.Timber);
                    WorldKit.Prop("Awning", h, P(ax + 1f, eave + 1.85f, az + 1f), new Vector3(2.4f, 0.04f, 2.4f), kit.Chance(0.5) ? M.Canvas : M.InkBlue);
                    Tag(h, "townhouse", "Tithian townhouse: tall whitewashed cube, small dark windows, a flat roof terrace behind a parapet with a canvas or ink-blue awning. 2-4 storeys.");
                }
                else Tag(h, "townhouse_domed", "Tithian house with a blue dome: whitewashed cube, ink-blue bands, a stepped blue dome on the roof.");
            }
            // Shade sails across the Bay Road and Salt Street.
            Transform sails = WorldKit.Group(g, "Shade sails");
            for (float x = 10f; x < 42f; x += 5f)
                WorldKit.ShadeSail(sails, P(x, 6f, GateWz0 - 0.2f), P(x + 1.5f, 6f, GateWz1 + 2.2f), 2.4f, x % 10f < 5f ? M.Canvas : M.InkBlue);
            Tag(sails, "shade_sail", null);
            Transform lamps = WorldKit.Group(map, "Street lamps");
            foreach (Vector2 p in new[]
                     {
                         new Vector2(12f, 65.8f), new Vector2(30f, 65.8f), new Vector2(50f, 70.2f), new Vector2(59.8f, 30f), new Vector2(59.8f, 50f),
                         new Vector2(64.2f, 56f), new Vector2(96.8f, 56f), new Vector2(66f, 97f), new Vector2(100f, 97f), new Vector2(116f, 100.2f), new Vector2(26f, 96f),
                     })
                Tag(WorldKit.Lamp(lamps, P(p.x, 0f, p.y), M.Iron, M.Lamp), "street_lamp", "Slim black iron lamp post, a white-glass lantern.");
        }

        // ------------------------------------------------------------------ people

        private static void BuildPeople(Transform map)
        {
            // Life, not content yet: tollmen and marines on the quays and gates, auditors in the Ledger
            // House and the Exchange, choristers at the Chantry, merchants and sailors, salt rakers.
            Transform g = WorldKit.Group(map, "People");
            (string npc, int x, int z, int wander)[] people =
            {
                ("tithe_marine", 8, 64, 0), ("tithe_marine", 8, 71, 0), ("tithe_marine", 60, 17, 0), ("tithe_marine", 63, 17, 0),
                ("tithe_marine", 119, 114, 2), ("tithe_marine", 28, 66, 3), ("tithe_marine", 30, 48, 3),
                ("tithe_tollman", 84, 115, 3), ("tithe_tollman", 81, 104, 2), ("tithe_tollman", 93, 104, 2), ("tithe_tollman", 110, 115, 3),
                ("tithe_auditor", 50, 80, 2), ("tithe_auditor", 57, 76, 2), ("tithe_auditor", 70, 75, 4), ("tithe_auditor", 90, 82, 4),
                ("tithe_chorister", 78, 36, 2), ("tithe_chorister", 82, 42, 2), ("tithe_chorister", 101, 35, 3), ("tithe_chorister", 80, 58, 3),
                ("tithe_merchant", 68, 88, 4), ("tithe_merchant", 92, 70, 4), ("tithe_merchant", 77, 91, 3), ("tithe_merchant", 104, 79, 2),
                ("tithe_sailor", 40, 96, 4), ("tithe_sailor", 55, 116, 4), ("tithe_sailor", 70, 116, 4), ("tithe_sailor", 26, 108, 3),
                ("salt_raker", 20, 11, 4), ("salt_raker", 44, 11, 4), ("salt_raker", 96, 11, 4), ("salt_raker", 116, 11, 4), ("salt_raker", 46, 54, 3),
            };
            foreach ((string npc, int x, int z, int wander) in people)
                WorldKit.Spawn(g, npc, X(x), Z(z), wander);
        }
    }
}
