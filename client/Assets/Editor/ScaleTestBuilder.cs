using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.World;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// The scale test region (docs/world-scale.md, section 7), just north of Tolling: a place to
    /// walk the proposed sizes before anything existing is rescaled. A street with today's
    /// storey on one side and the proposed one on the other, an alley between two-storey houses,
    /// a square with a three-storey landmark, today's trees beside the leafy ones, a 4-tile hill,
    /// a 2-tile cliff with a ramp, three 1.5-tile terraces and a fenced field, on a MapTerrain.
    /// Change the numbers below and rebuild to try others. Built through the normal pipeline, so
    /// it is exported, streamed and walkable like any region.
    /// </summary>
    public static class ScaleTestBuilder
    {
        public const string ScenePath = "Assets/Scenes/ScaleTest.unity";
        private static readonly Vector2Int Origin = new Vector2Int(0, 96);
        private const int Size = 96;

        // The numbers under test.
        private const float TodayStorey = WorldKit.DefaultStorey; // 1.7
        private const float ProposedStorey = 2.3f;                 // decision D2
        private const float HillHeight = 4f;
        private const float HillRadius = 11f;
        private const float CliffHeight = 2f;
        private const float TerraceStep = 1.5f;

        [MenuItem("R'th/Dev/Rebuild Scale Test Scene", priority = 110)]
        private static void BuildMenu()
        {
            if (WorldKit.ConfirmRebuild(ScenePath)) Build();
        }

        // Local tile (lx, ly) is absolute (Origin.x + lx, Origin.y + ly).
        private static int X(int lx) => Origin.x + lx;
        private static int Z(int ly) => Origin.y + ly;

        public static void Build()
        {
            var kit = new WorldKit(seed: 96);
            Material water = kit.Mat("Water", new Color(0.22f, 0.42f, 0.66f));
            Material plaster = kit.Mat("Plaster", new Color(0.86f, 0.80f, 0.66f));
            Material planks = kit.Mat("Planks", new Color(0.62f, 0.45f, 0.28f));
            Material thatch = kit.Mat("Thatch", new Color(0.66f, 0.55f, 0.3f));
            Material slate = kit.Mat("Slate", new Color(0.3f, 0.32f, 0.38f));
            Material stone = kit.Mat("Stone", new Color(0.52f, 0.52f, 0.50f));
            Material brick = kit.Mat("Brick", new Color(0.52f, 0.3f, 0.22f));
            Material wood = kit.Mat("Fence", new Color(0.45f, 0.33f, 0.20f));
            Material wheat = kit.Mat("Wheat", new Color(0.78f, 0.68f, 0.33f));
            Material bark = kit.Mat("Bark", new Color(0.36f, 0.25f, 0.15f));
            Material leaves = kit.Mat("Leaves", new Color(0.20f, 0.42f, 0.18f));
            Material darkLeaves = kit.Mat("DarkLeaves", new Color(0.15f, 0.33f, 0.16f));
            Material ground = kit.Mat("TerrainGround", Color.white); // the MapTerrain's colours come in as a texture

            MapArea area = WorldKit.BeginRegion("scale_test", "Scale test", Origin, new Vector2Int(Size, Size), null);
            Transform map = area.transform;

            // ---------------------------------------------------------- the ground
            var field = new Heightfield(Origin.x, Origin.y, Size, Size);
            field.Fill((x, y) => Height(x - Origin.x, y - Origin.y));
            var colours = new Color32[Size * Size];
            var grassColour = new Color(0.36f, 0.55f, 0.22f);
            Color32 grassC = grassColour, heathC = new Color(0.45f, 0.5f, 0.28f), dirtC = new Color(0.55f, 0.42f, 0.26f), cobbleC = new Color(0.55f, 0.53f, 0.5f);
            Color32 meadowC = new Color(0.42f, 0.6f, 0.26f), furrowC = new Color(0.45f, 0.36f, 0.22f);
            for (int ly = 0; ly < Size; ly++)
                for (int lx = 0; lx < Size; lx++)
                {
                    Color32 c = grassC;
                    if (InHill(lx, ly) || lx >= 74 && ly >= 40) c = heathC;
                    if (lx < 36 && ly >= 80) c = meadowC;
                    if (Road(lx, ly)) c = dirtC;
                    if (lx >= 64 && lx < 81 && ly >= 13 && ly < 31) c = cobbleC;
                    if (lx >= 6 && lx < 22 && ly >= 60 && ly < 74) c = furrowC;
                    colours[ly * Size + lx] = c;
                }
            WorldKit.Terrain(map, field, colours, ground, grassColour);

            // Tolling's river runs on a few tiles past the border into a pool.
            var pool = new HashSet<Vector2Int>();
            for (int ly = 0; ly < 12; ly++)
                for (int lx = 36; lx < 47; lx++)
                    if (ly < 4 && lx >= 40 && lx <= 42 || Vector2.Distance(new Vector2(lx + 0.5f, ly + 0.5f), new Vector2(41.5f, 7f)) < 3.6f)
                        pool.Add(new Vector2Int(X(lx), Z(ly)));
            WorldKit.Water(map, "River pool", pool, water);

            // ---------------------------------------------------------- the storey street (y 22-24)
            Transform street = WorldKit.Group(map, "Storey street");
            int[] plots = { 10, 17, 32, 39 }; // west edges; each house is 5 wide, the road north passes at x 28-29
            foreach (int px in plots)
            {
                // South side: today's 1.7 storey. North side: the proposed one.
                kit.House(street, "Today " + TodayStorey, X(px), Z(16), X(px + 5), Z(21), WorldKit.Side.North, X(px + 2), plaster, planks, thatch, TodayStorey);
                kit.House(street, "Proposed " + ProposedStorey, X(px), Z(25), X(px + 5), Z(30), WorldKit.Side.South, X(px + 2), plaster, planks, thatch, ProposedStorey);
            }

            // ---------------------------------------------------------- the alley (x 52-53) between two-storey houses
            Transform alley = WorldKit.Group(map, "Alley");
            kit.House(alley, "Two storeys W", X(46), Z(25), X(52), Z(32), WorldKit.Side.South, X(48), brick, planks, slate, ProposedStorey, 2);
            kit.House(alley, "Two storeys E", X(54), Z(25), X(60), Z(32), WorldKit.Side.South, X(57), brick, planks, slate, ProposedStorey, 2);

            // ---------------------------------------------------------- the square and its landmark (three storeys)
            Transform square = WorldKit.Group(map, "Square");
            float towerTop = 3 * ProposedStorey;
            Transform tower = WorldKit.Group(square, "Landmark tower");
            WorldKit.Box("Tower", tower, new Vector3(X(71) + 1.5f, towerTop / 2f, Z(20) + 1.5f), new Vector3(3f, towerTop, 3f), stone).AddComponent<MapBlocker>();
            for (int level = 1; level < 3; level++)
                WorldKit.Box("String course", tower, new Vector3(X(71) + 1.5f, level * ProposedStorey, Z(20) + 1.5f), new Vector3(3.2f, 0.15f, 3.2f), slate, collider: false);
            WorldKit.Box("Roof", tower, new Vector3(X(71) + 1.5f, towerTop + 0.35f, Z(20) + 1.5f), new Vector3(3.4f, 0.7f, 3.4f), slate, collider: false);
            WorldKit.Box("Cap", tower, new Vector3(X(71) + 1.5f, towerTop + 0.9f, Z(20) + 1.5f), new Vector3(1.6f, 0.5f, 1.6f), slate, collider: false, yaw: 45f);
            WorldKit.Fades(tower, 0.3f);

            // ---------------------------------------------------------- the grove: today's trees (south row), the leafy ones (north row)
            Transform grove = WorldKit.Group(map, "Grove");
            (string kind, int x, Material crown, float scale)[] today =
            {
                ("Tree", 10, leaves, 1f), ("Tree", 14, darkLeaves, 1.05f), ("Oak", 18, leaves, 1.25f), ("Willow", 22, kit.Mat("WillowLeaves", new Color(0.38f, 0.52f, 0.25f)), 1f),
                ("Yew", 26, darkLeaves, 1.1f), ("Oak", 30, leaves, 1f),
            };
            foreach ((string kind, int x, Material crown, float scale) in today) kit.Tree(grove, X(x), Z(42), bark, crown, scale, kind);
            kit.LeafyTree(grove, X(10), Z(48), LeafyTree.Small);
            kit.LeafyTree(grove, X(14), Z(48), LeafyTree.Small);
            kit.LeafyTree(grove, X(18), Z(47), LeafyTree.Large);
            kit.LeafyTree(grove, X(23), Z(48), LeafyTree.Small);
            kit.LeafyTree(grove, X(27), Z(47), LeafyTree.Large);
            kit.LeafyTree(grove, X(31), Z(48), LeafyTree.Small);

            // A few leafy trees on the hill and the terraces, standing on the ground.
            Transform wild = WorldKit.Group(map, "Trees on the hill");
            foreach (Vector2Int t in new[] { new Vector2Int(56, 44), new Vector2Int(65, 55), new Vector2Int(50, 52) })
                kit.LeafyTree(wild, X(t.x), Z(t.y), LeafyTree.Small, field.TileCentre(X(t.x), Z(t.y)));
            kit.LeafyTree(wild, X(80), Z(68), LeafyTree.Large, field.Sample(X(81), Z(69)));

            // ---------------------------------------------------------- the fenced field
            Transform farm = WorldKit.Group(map, "Field");
            WorldKit.Fence(farm, "Fence S", X(6), Z(60), X(22), Z(60), wood);
            WorldKit.Fence(farm, "Fence N", X(6), Z(74), X(22), Z(74), wood, X(13), X(14));
            WorldKit.Fence(farm, "Fence W", X(6), Z(60), X(6), Z(74), wood);
            WorldKit.Fence(farm, "Fence E", X(22), Z(60), X(22), Z(74), wood);
            for (int ly = 61; ly < 74; ly += 2)
                WorldKit.Box("Wheat", farm, new Vector3(X(14), 0.18f, Z(ly) + 0.5f), new Vector3(15.6f, 0.35f, 0.7f), wheat, collider: false);

            WorldKit.SaveRegion(area, ScenePath);
        }

        // ------------------------------------------------------------------ the shape of the land

        private static bool InHill(int lx, int ly) => Vector2.Distance(new Vector2(lx, ly), new Vector2(60f, 50f)) < HillRadius;

        private static bool Road(int lx, int ly)
        {
            if ((lx == 28 || lx == 29) && ly < 22) return true;                // north from Tolling's meadow
            if (ly >= 21 && ly <= 24 && lx >= 8 && lx < 64) return true;       // the storey street
            if ((lx == 52 || lx == 53) && ly >= 25 && ly < 34) return true;    // the alley
            if (lx >= 66 && lx <= 69 && ly >= 62 && ly < 74) return true;      // the cliff's ramp
            return lx >= 90 && lx <= 93 && ly >= 44 && ly < 78;                 // the terraces' ramps
        }

        /// <summary>Height at a local corner (lx, ly), in tiles.</summary>
        private static float Height(int lx, int ly)
        {
            float h = 0f;

            // A gentle hill: a cosine bell 4 high and 11 across the radius (the steepest tile rises about 0.6).
            float d = Vector2.Distance(new Vector2(lx, ly), new Vector2(60f, 50f));
            if (d < HillRadius) h = Mathf.Max(h, HillHeight * 0.5f * (1f + Mathf.Cos(Mathf.PI * d / HillRadius)));

            // The cliff: a plateau 2 up from row 74, x 40-70, with a ramp up its east end at x 66-70.
            if (lx >= 40 && lx <= 70 && ly >= 62 && ly <= 86)
            {
                if (ly >= 74) h = CliffHeight;
                else if (lx >= 66) h = CliffHeight * Mathf.Clamp01((ly - 62) / 12f);
            }

            // Three terraces, each 1.5 up, rising north from rows 52, 64 and 76; ramps at x 90-94.
            if (lx >= 74 && ly >= 40)
            {
                float t = 0f;
                foreach (int edge in new[] { 52, 64, 76 })
                    t += lx >= 90 && lx <= 94
                        ? TerraceStep * Mathf.Clamp01((ly - (edge - 6)) / 6f)
                        : ly >= edge ? TerraceStep : 0f;
                h = Mathf.Max(h, t);
            }

            // The north-west: untouched land with the reference's base noise (0.6 to 3.75 tiles,
            // here above a zero floor), fading out over 4 tiles so it meets the flat parts.
            if (lx < 36 && ly >= 80)
            {
                float edge = Mathf.Min(Mathf.Min(lx, 36 - lx), Mathf.Min(ly - 80, Size - ly));
                float fade = Mathf.Clamp01(edge / 4f);
                h = Mathf.Max(h, (Heightfield.Noise(X(lx), Z(ly), seed: 96) - 0.6f) * fade);
            }
            return h;
        }
    }
}
