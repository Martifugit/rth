using Rth.MapTools;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// A small instanced test region for height and floors (<c>::tele leveltest</c>): a hill climbed by
    /// a stair of twelve steps to a plateau, a smooth ramp to a pad, a two-storey hut with an inner
    /// stair, and a lookout platform reached by a ladder. Nothing here is content; it exists to
    /// walk, to check that characters follow the ground, that floors above switch off while you are
    /// below, that clicks and the camera work on every floor, and that the exported collision
    /// matches. It is also the pattern for building anything with a slope or an upper floor:
    /// terraces and stairs are ground at height (their edges need MapWalls, since the exporter
    /// ignores height), and an upper floor is a MapLevel with its own slab, walls and landings.
    /// </summary>
    public static class LevelTestBuilder
    {
        public const string ScenePath = "Assets/Scenes/LevelTest.unity";
        private static readonly Vector2Int Origin = new Vector2Int(1080, 1008);
        private static readonly Vector2Int Size = new Vector2Int(48, 48);

        private const float Storey = 3f;

        [MenuItem("R'th/Dev/Rebuild Level Test Scene", priority = 108)]
        private static void BuildMenu()
        {
            if (WorldKit.ConfirmRebuild(ScenePath)) Build();
        }

        private static int X(int lx) => Origin.x + lx;
        private static int Z(int lz) => Origin.y + lz;

        public static void Build()
        {
            var kit = new WorldKit(seed: 4410);
            Material grass = kit.Mat("LevelTestGrass", new Color(0.36f, 0.5f, 0.3f));
            Material stone = kit.Mat("LevelTestStone", new Color(0.55f, 0.55f, 0.57f));
            Material step = kit.Mat("LevelTestStep", new Color(0.7f, 0.68f, 0.62f));
            Material walls = kit.Mat("LevelTestWall", new Color(0.82f, 0.78f, 0.68f));
            Material floorMat = kit.Mat("LevelTestFloor", new Color(0.5f, 0.38f, 0.26f));
            Material roofMat = kit.Mat("LevelTestRoof", new Color(0.3f, 0.34f, 0.4f));
            Material wood = kit.Mat("LevelTestWood", new Color(0.45f, 0.33f, 0.2f));

            MapArea area = WorldKit.BeginRegion("leveltest", "Level Test", Origin, Size, grass);
            area.instanced = true;
            Transform root = area.transform;

            Hill(root, step, stone);
            Ramp(root, step, stone);
            Hut(root, walls, floorMat, roofMat, wood);
            Lookout(root, floorMat, wood);

            WorldKit.SaveRegion(area, ScenePath);
        }

        // ------------------------------------------------------------------ the hill

        /// <summary>Twelve steps of 25 cm up to a plateau 3 m high, with walls along every edge but the foot of the stair.</summary>
        private static void Hill(Transform root, Material step, Material stone)
        {
            Transform hill = WorldKit.Group(root, "Hill");
            for (int i = 0; i < 12; i++)
            {
                float top = (i + 1) * 0.25f;
                WorldKit.Box("Step " + (i + 1), hill, new Vector3(X(10 + i) + 0.5f, top / 2f, Z(6) + 2.5f), new Vector3(1f, top, 5f), step);
            }
            WorldKit.Box("Plateau", hill, new Vector3(X(28), 1.5f, Z(8)), new Vector3(12f, 3f, 12f), stone);

            // The stair's two sides, then the plateau's edges (the stair meets its west side).
            WorldKit.Wall(hill, "Stair south", X(10), Z(6), X(22), Z(6), stone, 0.4f);
            WorldKit.Wall(hill, "Stair north", X(10), Z(11), X(22), Z(11), stone, 0.4f);
            WorldKit.Wall(hill, "Plateau south", X(22), Z(2), X(34), Z(2), stone, 3f);
            WorldKit.Wall(hill, "Plateau north", X(22), Z(14), X(34), Z(14), stone, 3f);
            WorldKit.Wall(hill, "Plateau east", X(34), Z(2), X(34), Z(14), stone, 3f);
            WorldKit.Wall(hill, "Plateau west", X(22), Z(2), X(22), Z(14), stone, 3f, 0.2f, Z(6), Z(7), Z(8), Z(9), Z(10)); // open where the stair arrives
        }

        // ------------------------------------------------------------------ the ramp

        /// <summary>A smooth ramp, one metre in three tiles, up to a pad two metres high.</summary>
        private static void Ramp(Transform root, Material step, Material stone)
        {
            Transform ramp = WorldKit.Group(root, "Ramp");
            const float run = 6f, rise = 2f;
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(run * run + rise * rise);
            var topMid = new Vector3(X(36) + run / 2f, rise / 2f, Z(8));
            var normal = new Vector3(-Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad), 0f);
            GameObject slope = WorldKit.Box("Slope", ramp, topMid - normal * 0.1f, new Vector3(length, 0.2f, 12f), step);
            slope.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            WorldKit.Box("Pad", ramp, new Vector3(X(42) + 2.5f, 1f, Z(8)), new Vector3(5f, 2f, 12f), stone);

            WorldKit.Wall(ramp, "Ramp south", X(36), Z(2), X(47), Z(2), stone, 2f);
            WorldKit.Wall(ramp, "Ramp north", X(36), Z(14), X(47), Z(14), stone, 2f);
            WorldKit.Wall(ramp, "Pad east", X(47), Z(2), X(47), Z(14), stone, 2f);
        }

        // ------------------------------------------------------------------ the hut

        /// <summary>
        /// Two floors: a ground room with a door and a stair, and above it a room with a floor slab,
        /// walls that always show, and furniture that only shows once you are up there.
        /// </summary>
        private static void Hut(Transform root, Material walls, Material floorMat, Material roofMat, Material wood)
        {
            const int x0 = 8, z0 = 20, x1 = 18, z1 = 28; // tiles [x0, x1) x [z0, z1)
            Transform hut = WorldKit.Group(root, "Hut");

            // Ground floor.
            WorldKit.Patch("Floor", hut, X(x0), Z(z0), X(x1), Z(z1), floorMat, 0.02f);
            Transform ground = WorldKit.Group(hut, "Ground walls");
            WorldKit.Wall(ground, "South", X(x0), Z(z0), X(x1), Z(z0), walls, Storey, 0.2f, X(12)); // the door, at tile x = 12
            WorldKit.Wall(ground, "North", X(x0), Z(z1), X(x1), Z(z1), walls, Storey);
            WorldKit.Wall(ground, "West", X(x0), Z(z0), X(x0), Z(z1), walls, Storey);
            WorldKit.Wall(ground, "East", X(x1), Z(z0), X(x1), Z(z1), walls, Storey);
            WorldKit.Fades(ground, 0.3f);

            // The stair: up from the ground floor, and its twin coming down.
            Transform up = WorldKit.Station(hut, "Stairs up", "stairs_up", X(9), Z(26));
            WorldKit.Box("Stair", up, new Vector3(X(9) + 0.5f, Storey / 2f, Z(26) + 0.5f), new Vector3(0.9f, Storey, 0.9f), wood);

            // The upper floor's outer walls: always drawn, so the hut looks two storeys from outside.
            Transform shell = WorldKit.Group(hut, "Upper walls");
            shell.gameObject.AddComponent<MapLevel>().hiddenFromBelow = false;
            UpperWall(shell, "South", X(x0), Z(z0), X(x1), Z(z0), walls);
            UpperWall(shell, "North", X(x0), Z(z1), X(x1), Z(z1), walls);
            UpperWall(shell, "West", X(x0), Z(z0), X(x0), Z(z1), walls);
            UpperWall(shell, "East", X(x1), Z(z0), X(x1), Z(z1), walls);
            WorldKit.Fades(shell, 0.3f);

            // The upper floor itself: the slab, the stair coming down, and a bed's worth of furniture.
            Transform floor = WorldKit.Group(hut, "Upper floor");
            floor.gameObject.AddComponent<MapLevel>();
            WorldKit.Box("Slab", floor, new Vector3((X(x0) + X(x1)) / 2f, Storey - 0.1f, (Z(z0) + Z(z1)) / 2f), new Vector3(x1 - x0, 0.2f, z1 - z0), floorMat);
            Transform down = WorldKit.Station(floor, "Stairs down", "stairs_down", X(9), Z(26));
            down.position = new Vector3(X(9) + 0.5f, Storey, Z(26) + 0.5f);
            WorldKit.Box("Stair", down, new Vector3(X(9) + 0.5f, Storey + 0.5f, Z(26) + 0.5f), new Vector3(0.9f, 1f, 0.9f), wood);
            WorldKit.Box("Bed", floor, new Vector3(X(15) + 0.5f, Storey + 0.3f, Z(22) + 1f), new Vector3(1.2f, 0.6f, 2f), wood).AddComponent<MapBlocker>();
            WorldKit.Spawn(floor, "concord_surveyor", X(14), Z(25), 1);

            // Where each stair lands: standing beside the other end.
            up.GetComponent<MapObject>().climbTo = Landing(floor, "Landing upstairs", X(10), Z(26), Storey);
            down.GetComponent<MapObject>().climbTo = Landing(hut, "Landing downstairs", X(10), Z(26), 0f);

            // The roof hides while you are inside, on either floor.
            Transform roof = WorldKit.Group(hut, "Roof");
            WorldKit.Box("Roof slab", roof, new Vector3((X(x0) + X(x1)) / 2f, 2f * Storey + 0.15f, (Z(z0) + Z(z1)) / 2f), new Vector3(x1 - x0 + 0.6f, 0.3f, z1 - z0 + 0.6f), roofMat, collider: false);
            WorldKit.Fades(roof, 0.2f);
            roof.gameObject.AddComponent<RoofHider>().interior = new RectInt(X(x0), Z(z0), x1 - x0, z1 - z0);

            WorldKit.Spawn(hut, "meridian_fisher", X(14), Z(23), 1);
        }

        private static void UpperWall(Transform parent, string name, int x0, int z0, int x1, int z1, Material material)
        {
            bool alongX = z0 == z1;
            float len = alongX ? x1 - x0 : z1 - z0;
            Vector3 centre = new Vector3(alongX ? (x0 + x1) / 2f : x0, Storey + Storey / 2f, alongX ? z0 : (z0 + z1) / 2f);
            Vector3 size = alongX ? new Vector3(len + 0.2f, Storey, 0.2f) : new Vector3(0.2f, Storey, len + 0.2f);
            WorldKit.Box("Wall " + name, parent, centre, size, material).AddComponent<MapWall>();
        }

        private static Transform Landing(Transform parent, string name, int x, int z, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x + 0.5f, y, z + 0.5f);
            return go.transform;
        }

        // ------------------------------------------------------------------ the lookout

        /// <summary>An open platform on posts, four metres up, reached by a ladder on its east side.</summary>
        private static void Lookout(Transform root, Material floorMat, Material wood)
        {
            const int x0 = 30, z0 = 20; // a 3 x 3 platform over tiles [30, 33) x [20, 23)
            const float height = 4f;
            Transform lookout = WorldKit.Group(root, "Lookout");
            foreach ((int px, int pz) in new[] { (x0, z0), (x0 + 3, z0), (x0, z0 + 3), (x0 + 3, z0 + 3) })
                WorldKit.Box("Post", lookout, new Vector3(X(px), height / 2f, Z(pz)), new Vector3(0.25f, height, 0.25f), wood, collider: false);

            Transform deck = WorldKit.Group(lookout, "Deck");
            deck.gameObject.AddComponent<MapLevel>().hiddenFromBelow = false; // open air: always seen from the ground
            WorldKit.Fades(deck, 0.3f);
            WorldKit.Box("Platform", deck, new Vector3(X(x0) + 1.5f, height - 0.1f, Z(z0) + 1.5f), new Vector3(3f, 0.2f, 3f), floorMat);
            // A rail round the deck (the ladder's side is left open at the ladder itself).
            WorldKit.Wall(deck, "Rail south", X(x0), Z(z0), X(x0 + 3), Z(z0), wood, 0.1f);
            WorldKit.Wall(deck, "Rail north", X(x0), Z(z0 + 3), X(x0 + 3), Z(z0 + 3), wood, 0.1f);
            WorldKit.Wall(deck, "Rail west", X(x0), Z(z0), X(x0), Z(z0 + 3), wood, 0.1f);
            foreach (Transform rail in deck)
                if (rail.name.StartsWith("Rail"))
                    rail.position += Vector3.up * height;

            // The ladder up stands outside the east edge; the one down is the deck's, beside where you land.
            Transform up = WorldKit.Station(lookout, "Ladder up", "ladder_up", X(x0 + 3), Z(z0 + 1));
            WorldKit.Box("Ladder", up, new Vector3(X(x0 + 3) + 0.5f, height / 2f, Z(z0 + 1) + 0.5f), new Vector3(0.3f, height, 0.8f), wood);
            Transform down = WorldKit.Station(deck, "Ladder down", "ladder_down", X(x0 + 2), Z(z0 + 2));
            down.position = new Vector3(X(x0 + 2) + 0.5f, height, Z(z0 + 2) + 0.5f);
            WorldKit.Box("Ladder", down, new Vector3(X(x0 + 2) + 0.5f, height + 0.5f, Z(z0 + 2) + 0.5f), new Vector3(0.3f, 1f, 0.8f), wood);
            up.GetComponent<MapObject>().climbTo = Landing(deck, "Landing on deck", X(x0 + 2), Z(z0 + 1), height);
            down.GetComponent<MapObject>().climbTo = Landing(lookout, "Landing on ground", X(x0 + 3), Z(z0 + 2), 0f);
        }
    }
}
