using System.Collections.Generic;
using System.IO;
using Rth.MapTools;
using Rth.MapTools.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Lays out Phase 1 of the world (docs/world-map.md): Tolling, the riverside town where the
    /// player wakes. West bank: the town square and bell tower, the Synod treasury, Corvin's
    /// apothecary, the skinning yard, cottages and fields. North: Widdermill and its
    /// millpond. South: reed beds full of fen rats. East bank: woods and Sour Hollow. South-east:
    /// Wennow; and Palmerway leaving east. Once built, the scene is the source of truth;
    /// rebuilding replaces edits. A region scene: the Game scene supplies camera, light and HUD.
    /// </summary>
    public static class TollingBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tolling.unity";
        private const int Size = 96;

        [MenuItem("R'th/Dev/Rebuild Tolling Scene", priority = 101)]
        private static void BuildMenu()
        {
            if (WorldKit.ConfirmRebuild(ScenePath)) Build();
        }

        public static void Build()
        {
            var kit = new WorldKit(seed: 1729);

            Material grass = kit.Mat("Grass", new Color(0.36f, 0.55f, 0.22f));
            Material meadow = kit.Mat("Meadow", new Color(0.42f, 0.6f, 0.26f));
            Material wheat = kit.Mat("Wheat", new Color(0.78f, 0.68f, 0.33f));
            Material furrow = kit.Mat("Furrow", new Color(0.45f, 0.36f, 0.22f));
            Material dirt = kit.Mat("Dirt", new Color(0.55f, 0.42f, 0.26f));
            Material cobble = kit.Mat("Cobble", new Color(0.55f, 0.53f, 0.5f));
            Material water = kit.Mat("Water", new Color(0.22f, 0.42f, 0.66f));
            Material bay = kit.Mat("BayWater", new Color(0.17f, 0.33f, 0.55f));
            Material sand = kit.Mat("Sand", new Color(0.82f, 0.75f, 0.55f));
            Material plaster = kit.Mat("Plaster", new Color(0.86f, 0.80f, 0.66f));
            Material timber = kit.Mat("Timber", new Color(0.4f, 0.28f, 0.18f));
            Material stone = kit.Mat("Stone", new Color(0.52f, 0.52f, 0.50f));
            Material planks = kit.Mat("Planks", new Color(0.62f, 0.45f, 0.28f));
            Material thatch = kit.Mat("Thatch", new Color(0.66f, 0.55f, 0.3f));
            Material slate = kit.Mat("Slate", new Color(0.3f, 0.32f, 0.38f));
            Material synodRed = kit.Mat("SynodRed", new Color(0.55f, 0.1f, 0.12f));
            Material iron = kit.Mat("Iron", new Color(0.25f, 0.25f, 0.27f));
            Material copper = kit.Mat("Copper", new Color(0.72f, 0.42f, 0.22f));
            Material wood = kit.Mat("Fence", new Color(0.45f, 0.33f, 0.20f));
            Material bark = kit.Mat("Bark", new Color(0.36f, 0.25f, 0.15f));
            Material leaves = kit.Mat("Leaves", new Color(0.20f, 0.42f, 0.18f));
            Material darkLeaves = kit.Mat("DarkLeaves", new Color(0.15f, 0.33f, 0.16f));
            Material reed = kit.Mat("Reed", new Color(0.55f, 0.6f, 0.3f));
            Material sour = kit.Mat("SourGround", new Color(0.62f, 0.6f, 0.25f));
            Material deadWood = kit.Mat("DeadWood", new Color(0.33f, 0.3f, 0.24f));
            Material hide = kit.Mat("Hide", new Color(0.62f, 0.52f, 0.38f));
            Material glaze = kit.Mat("Glaze", new Color(0.42f, 0.5f, 0.46f));
            Material lye = kit.Mat("Lye", new Color(0.78f, 0.85f, 0.62f));
            Material bronze = kit.Mat("Bronze", new Color(0.6f, 0.45f, 0.2f));

            // Tolling's south-west corner is the world's origin; every other region is placed from it.
            MapArea area = WorldKit.BeginRegion("tolling", "Tolling", Vector2Int.zero, new Vector2Int(Size, Size), grass);
            Transform map = area.transform;

            // ---------------------------------------------------------- water: the river, Wennow, the millpond
            var riverLine = new List<Vector2>
            {
                new Vector2(41.5f, 97f), new Vector2(42.5f, 84f), new Vector2(45.5f, 72f), new Vector2(44.5f, 60f),
                new Vector2(45.5f, 50f), new Vector2(48.5f, 40f), new Vector2(55f, 31f), new Vector2(66f, 24f),
                new Vector2(80f, 18f), new Vector2(98f, 12f),
            };
            HashSet<Vector2Int> river = WorldKit.Along(riverLine, 1.6f, Size);

            // The town bridge crosses at z 49-50; the mill race is left open for the wheel.
            var bridge = new HashSet<Vector2Int>();
            foreach (Vector2Int t in river)
                if (t.y == 49 || t.y == 50) bridge.Add(t);
            river.ExceptWith(bridge);

            var bayTiles = new HashSet<Vector2Int>();
            var shore = new HashSet<Vector2Int>();
            for (int z = 0; z < Size; z++)
            for (int x = 0; x < Size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, z + 0.5f), new Vector2(104f, -6f));
                if (d < 30f) bayTiles.Add(new Vector2Int(x, z));
                else if (d < 33f) shore.Add(new Vector2Int(x, z));
            }
            river.ExceptWith(bayTiles);

            var pond = new HashSet<Vector2Int>();
            for (int z = 69; z <= 79; z++)
            for (int x = 27; x <= 37; x++)
                if (Vector2.Distance(new Vector2(x + 0.5f, z + 0.5f), new Vector2(32f, 74f)) < 4.2f) pond.Add(new Vector2Int(x, z));

            WorldKit.Water(map, "River", river, water);
            WorldKit.Water(map, "Bay", bayTiles, bay);
            WorldKit.Water(map, "Millpond", pond, water);
            Transform shoreGroup = WorldKit.Group(map, "Shore");
            foreach (Vector2Int t in shore)
                if (!river.Contains(t)) WorldKit.Box("Sand", shoreGroup, new Vector3(t.x + 0.5f, 0.001f, t.y + 0.5f), new Vector3(1f, 0.01f, 1f), sand);

            // The bridge: planks over the water, rails along both sides (decoration).
            Transform bridgeGroup = WorldKit.Group(map, "Town bridge");
            foreach (Vector2Int t in bridge)
                WorldKit.Box("Planks", bridgeGroup, new Vector3(t.x + 0.5f, 0.06f, t.y + 0.5f), new Vector3(1f, 0.12f, 1f), planks);
            int bx0 = int.MaxValue, bx1 = int.MinValue;
            foreach (Vector2Int t in bridge)
            {
                bx0 = Mathf.Min(bx0, t.x);
                bx1 = Mathf.Max(bx1, t.x + 1);
            }
            WorldKit.Box("Rail S", bridgeGroup, new Vector3((bx0 + bx1) / 2f, 0.5f, 49.05f), new Vector3(bx1 - bx0, 0.08f, 0.08f), wood, collider: false);
            WorldKit.Box("Rail N", bridgeGroup, new Vector3((bx0 + bx1) / 2f, 0.5f, 50.95f), new Vector3(bx1 - bx0, 0.08f, 0.08f), wood, collider: false);

            // ---------------------------------------------------------- roads and ground
            Transform ground = WorldKit.Group(map, "Ground cover");
            WorldKit.Patch("Town square", ground, 29, 45, 40, 56, cobble);
            var roads = new HashSet<Vector2Int>();
            void AddRoad(params Vector2[] points) => roads.UnionWith(WorldKit.Along(points, 0.9f, Size));
            AddRoad(new Vector2(40f, 50f), new Vector2(bx0, 50f));                                     // square to bridge
            AddRoad(new Vector2(bx1, 50f), new Vector2(58f, 51f), new Vector2(66f, 58f), new Vector2(71f, 64f)); // east to Sour Hollow
            AddRoad(new Vector2(58f, 51f), new Vector2(62f, 40f), new Vector2(72f, 28f), new Vector2(81f, 18f));              // down to Wennow
            AddRoad(new Vector2(62f, 40f), new Vector2(76f, 42f), new Vector2(88f, 45f), new Vector2(96f, 46f));              // east: Palmerway to Cruor
            AddRoad(new Vector2(35f, 56f), new Vector2(35f, 68f), new Vector2(38.5f, 72.5f));           // north to the mill
            AddRoad(new Vector2(31f, 45f), new Vector2(31f, 38f));                                       // south to the skinning yard
            AddRoad(new Vector2(29f, 50f), new Vector2(18f, 50f), new Vector2(8f, 52f));                // west to the fields
            roads.ExceptWith(river);
            roads.ExceptWith(bridge);
            roads.ExceptWith(bayTiles);
            WorldKit.Road(map, "Roads", roads, dirt);

            // ---------------------------------------------------------- fields to the west
            Transform fields = WorldKit.Group(map, "Fields");
            Field(fields, 6, 36, 18, 47, wheat, furrow, wood, gapAt: 12);
            Field(fields, 6, 53, 18, 66, wheat, furrow, wood, gapAt: 12);
            Field(fields, 20, 68, 27, 82, wheat, furrow, wood, gapAt: 23);
            WorldKit.Patch("Meadow", fields, 6, 68, 18, 90, meadow);

            // ---------------------------------------------------------- the town
            Transform town = WorldKit.Group(map, "Town");

            // The bell tower. Its bell tolls for every sleeper, and once for each one who woke.
            Transform tower = WorldKit.Group(town, "Bell tower");
            GameObject towerBody = WorldKit.Box("Tower", tower, new Vector3(32f, 2.5f, 58f), new Vector3(2f, 5f, 2f), stone);
            towerBody.AddComponent<MapBlocker>();
            WorldKit.Box("Belfry roof", tower, new Vector3(32f, 5.6f, 58f), new Vector3(2.4f, 0.4f, 2.4f), slate, collider: false);
            WorldKit.Cylinder("Bell", tower, new Vector3(32f, 4.6f, 56.95f), new Vector3(0.5f, 0.25f, 0.5f), bronze, collider: false);

            WorldKit.Box("Well", town, new Vector3(36.5f, 0.35f, 52.5f), new Vector3(0.9f, 0.7f, 0.9f), stone).AddComponent<MapBlocker>();

            // The Synod treasury: Clerk Ambrose and the strongbox.
            kit.House(town, "Synod treasury", 22, 52, 28, 57, WorldKit.Side.East, 54, plaster, cobble, slate);
            Transform box = WorldKit.Station(town, "Synod strongbox", "synod_strongbox", 23, 55);
            WorldKit.Box("Chest", box, box.position + new Vector3(0f, 0.35f, 0f), new Vector3(0.8f, 0.7f, 0.6f), synodRed);
            WorldKit.Box("Band", box, box.position + new Vector3(0f, 0.55f, 0f), new Vector3(0.84f, 0.08f, 0.64f), iron);
            WorldKit.Box("Banner", town, new Vector3(28.15f, 1.4f, 55.5f), new Vector3(0.05f, 0.9f, 0.6f), synodRed, collider: false);

            // Corvin's apothecary, with the still behind the counter.
            kit.House(town, "Sallow's apothecary", 35, 39, 42, 45, WorldKit.Side.North, 38, plaster, planks, thatch);
            WorldKit.Box("Counter", town, new Vector3(38.5f, 0.45f, 41.5f), new Vector3(3f, 0.9f, 0.5f), timber).AddComponent<MapBlocker>();
            Transform still = WorldKit.Station(town, "Apothecary's still", "apothecary_still", 36, 40);
            WorldKit.Cylinder("Pot", still, still.position + new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.3f, 0.6f), copper);
            WorldKit.Box("Coil", still, still.position + new Vector3(0.2f, 0.75f, 0f), new Vector3(0.12f, 0.5f, 0.12f), copper);

            // Cottages: the Marsh family's by the mill road, and two more.
            kit.House(town, "Marsh cottage", 27, 63, 31, 67, WorldKit.Side.East, 64, plaster, planks, thatch);
            kit.House(town, "Cottage", 23, 41, 28, 45, WorldKit.Side.North, 25, plaster, planks, thatch);
            kit.House(town, "Cottage", 37, 58, 41, 62, WorldKit.Side.South, 38, plaster, planks, thatch);

            // The training yard, with the straw penitent.
            WorldKit.Patch("Training yard", town, 19, 58, 26, 63, dirt);
            WorldKit.Fence(town, "Yard fence N", 19, 63, 26, 63, wood);
            WorldKit.Fence(town, "Yard fence W", 19, 58, 19, 63, wood);

            // ---------------------------------------------------------- the skinning yard
            Transform yard = WorldKit.Group(map, "Skinning yard");
            WorldKit.Patch("Yard floor", yard, 22, 29, 33, 38, dirt);
            WorldKit.Fence(yard, "Fence N", 22, 38, 33, 38, wood, 30, 31);
            WorldKit.Fence(yard, "Fence S", 22, 29, 33, 29, wood);
            WorldKit.Fence(yard, "Fence W", 22, 29, 22, 38, wood);
            WorldKit.Fence(yard, "Fence E", 33, 29, 33, 38, wood, 33);
            Transform rack = WorldKit.Station(yard, "Skinning rack", "skinning_rack", 26, 33);
            WorldKit.Box("Post L", rack, rack.position + new Vector3(-0.35f, 0.6f, 0f), new Vector3(0.12f, 1.2f, 0.12f), wood);
            WorldKit.Box("Post R", rack, rack.position + new Vector3(0.35f, 0.6f, 0f), new Vector3(0.12f, 1.2f, 0.12f), wood);
            WorldKit.Box("Bar", rack, rack.position + new Vector3(0f, 1.15f, 0f), new Vector3(0.9f, 0.1f, 0.1f), wood);
            WorldKit.Box("Hide", rack, rack.position + new Vector3(0f, 0.75f, 0.02f), new Vector3(0.6f, 0.65f, 0.03f), hide);
            for (int i = 0; i < 3; i++) // drying frames along the south fence
                WorldKit.Box("Drying hide", yard, new Vector3(24.5f + i * 2.5f, 0.6f, 29.5f), new Vector3(1f, 0.8f, 0.04f), hide, collider: false);

            // ---------------------------------------------------------- Widdermill
            Transform mill = WorldKit.Group(map, "Widdermill");
            kit.House(mill, "Mill house", 37, 70, 42, 76, WorldKit.Side.West, 72, plaster, planks, thatch);
            WorldKit.Cylinder("Wheel", mill, new Vector3(43f, 1.2f, 73f), new Vector3(2.2f, 0.15f, 2.2f), timber, collider: false).transform.rotation = Quaternion.Euler(0f, 0f, 90f);

            // ---------------------------------------------------------- reed beds (fen rats)
            Transform reeds = WorldKit.Group(map, "Reed beds");
            foreach (Vector2Int t in WorldKit.Along(riverLine, 3.6f, Size))
            {
                if (river.Contains(t) || bridge.Contains(t) || roads.Contains(t) || bayTiles.Contains(t)) continue;
                if (t.y < 26 || t.y > 46 || !kit.Chance(0.35)) continue;
                kit.Reeds(reeds, t.x, t.y, reed);
            }

            // ---------------------------------------------------------- Sour Hollow
            Transform hollow = WorldKit.Group(map, "Sour Hollow");
            WorldKit.Patch("Sour ground", hollow, 66, 60, 78, 71, sour);
            for (int i = 0; i < 4; i++)
                WorldKit.Box("Dead tree", hollow, new Vector3(67.5f + i * 3f, 0.9f, 70.2f - (i % 2) * 9f), new Vector3(0.25f, 1.8f, 0.25f), deadWood).AddComponent<MapBlocker>();
            GameObject cask = WorldKit.Cylinder("Leaking cask", hollow, new Vector3(72.5f, 0.35f, 66.5f), new Vector3(0.8f, 0.4f, 0.8f), deadWood);
            cask.transform.rotation = Quaternion.Euler(80f, 20f, 0f);
            cask.AddComponent<MapBlocker>();
            Transform vat = WorldKit.Station(hollow, "Lye vat", "lye_vat", 75, 62);
            WorldKit.Cylinder("Vat", vat, vat.position + new Vector3(0f, 0.35f, 0f), new Vector3(0.85f, 0.35f, 0.85f), glaze);
            WorldKit.Cylinder("Lye", vat, vat.position + new Vector3(0f, 0.68f, 0f), new Vector3(0.75f, 0.02f, 0.75f), lye, collider: false);

            // ---------------------------------------------------------- trees
            // RuneScape scale: a handful of deliberate groves, not a forest. Every tree here is a
            // future woodcutting resource, so each one is placed on purpose.
            Transform trees = WorldKit.Group(map, "Trees");
            Material willowLeaves = kit.Mat("WillowLeaves", new Color(0.38f, 0.52f, 0.25f));
            (string kind, int x, int z, Material crown, float scale)[] grove =
            {
                // The old oaks at the meadow's north-west corner.
                ("Oak", 8, 86, leaves, 1.25f), ("Oak", 11, 88, leaves, 1.1f), ("Oak", 12, 84, leaves, 1.15f),
                // Willows at the millpond and the mill.
                ("Willow", 38, 79, willowLeaves, 1f), ("Willow", 35, 81, willowLeaves, 1.05f),
                // A yew over the sleepers' graves; a lone oak behind the treasury.
                ("Yew", 35, 16, darkLeaves, 1.1f), ("Oak", 20, 54, leaves, 1f),
                // The east woods: a few groves either side of the road to Sour Hollow.
                ("Tree", 52, 56, leaves, 1f), ("Tree", 54, 59, darkLeaves, 1f), ("Tree", 51, 61, leaves, 0.95f), ("Tree", 56, 62, leaves, 1.05f),
                ("Tree", 60, 80, darkLeaves, 1f), ("Tree", 63, 83, leaves, 1.1f), ("Tree", 65, 79, darkLeaves, 0.95f),
                ("Tree", 61, 86, leaves, 1f), ("Tree", 67, 85, darkLeaves, 1.05f),
                ("Tree", 63, 66, darkLeaves, 1f), ("Tree", 81, 70, leaves, 1f), ("Tree", 80, 63, darkLeaves, 0.95f),
                ("Tree", 66, 44, leaves, 1f), ("Tree", 69, 46, leaves, 1.05f), ("Tree", 64, 48, darkLeaves, 1f),
                // A few loners in the south meadow and along the field edges.
                ("Oak", 45, 20, leaves, 1.15f), ("Tree", 40, 13, leaves, 1f), ("Tree", 52, 9, darkLeaves, 1f),
                ("Tree", 4, 58, leaves, 1f), ("Tree", 4, 40, leaves, 0.95f), ("Tree", 19, 33, leaves, 1f),
            };
            foreach ((string kind, int x, int z, Material crown, float scale) in grove)
                kit.Tree(trees, x, z, bark, crown, scale, kind);
            WorldKit.Fades(tower, 0.3f);
            Transform rocks = WorldKit.Group(map, "Rocks");
            foreach (Vector2Int r in new[] { new Vector2Int(55, 62), new Vector2Int(61, 76), new Vector2Int(84, 52), new Vector2Int(14, 30), new Vector2Int(50, 86) })
                WorldKit.Rock(rocks, r.x, r.y, stone);

            // ---------------------------------------------------------- the sleepers' graves
            // Those who fell asleep and never woke. The bell tolls for each of them.
            Transform graves = WorldKit.Group(map, "Sleepers' graves");
            Material grave = kit.Mat("Gravestone", new Color(0.6f, 0.6f, 0.58f));
            Material turf = kit.Mat("GraveTurf", new Color(0.3f, 0.45f, 0.2f));
            WorldKit.Patch("Turf", graves, 22, 12, 34, 21, turf);
            WorldKit.Fence(graves, "Fence N", 22, 21, 34, 21, wood, 27, 28);
            WorldKit.Fence(graves, "Fence S", 22, 12, 34, 12, wood);
            WorldKit.Fence(graves, "Fence W", 22, 12, 22, 21, wood);
            WorldKit.Fence(graves, "Fence E", 34, 12, 34, 21, wood);
            for (int gz = 13; gz <= 19; gz += 2)
            for (int gx = 23; gx <= 32; gx += 2)
            {
                if (gx == 27 || (gz == 19 && kit.Chance(0.3))) continue; // the path up the middle, and a few fresh plots
                WorldKit.Box("Headstone", graves, new Vector3(gx + 0.5f, 0.35f, gz + 0.75f), new Vector3(0.5f, 0.7f, 0.15f), grave).AddComponent<MapBlocker>();
            }
            // A path from the south road, round the yard's east fence, to the graveyard gate.
            HashSet<Vector2Int> gravePath = WorldKit.Along(new[] { new Vector2(31.5f, 38.5f), new Vector2(34.5f, 37f), new Vector2(34.5f, 26f), new Vector2(28f, 21.5f) }, 0.7f, Size);
            gravePath.RemoveWhere(t => (t.x >= 22 && t.x <= 33 && t.y >= 29 && t.y <= 37) || t.y < 21); // not through the yard or graves
            WorldKit.Road(map, "Grave path", gravePath, dirt);

            // ---------------------------------------------------------- people and creatures
            Transform folk = WorldKit.Group(map, "Townsfolk");
            WorldKit.Spawn(folk, "clerk_ambrose", 25, 54, 0);
            WorldKit.Spawn(folk, "corvin_sallow", 38, 42, 1);
            WorldKit.Spawn(folk, "wenna_thole", 27, 34, 0);
            WorldKit.Spawn(folk, "edda_marsh", 35, 71, 3);
            WorldKit.Spawn(folk, "straw_penitent", 22, 60, 0);

            Transform creatures = WorldKit.Group(map, "Creatures");
            foreach (Vector2Int r in new[] { new Vector2Int(44, 41), new Vector2Int(47, 35), new Vector2Int(50, 30), new Vector2Int(43, 33), new Vector2Int(55, 27), new Vector2Int(42, 46) })
                WorldKit.Spawn(creatures, "fen_rat", r.x, r.y, 3);
            foreach (Vector2Int m in new[] { new Vector2Int(69, 68), new Vector2Int(74, 69), new Vector2Int(70, 62), new Vector2Int(77, 66) })
                WorldKit.Spawn(creatures, "vitriol_mite", m.x, m.y, 2);

            // Players wake in the square, under the bell.
            var spawn = new GameObject("Spawn");
            spawn.transform.SetParent(map);
            spawn.transform.position = new Vector3(33.5f, 0f, 53.5f);
            spawn.AddComponent<MapSpawn>();

            WorldKit.SaveRegion(area, ScenePath);
        }

        private static bool InRect(int x, int z, int x0, int z0, int x1, int z1) => x >= x0 && x < x1 && z >= z0 && z < z1;

        /// <summary>A fenced field with a gate: wheat strips over furrows.</summary>
        private static void Field(Transform parent, int x0, int z0, int x1, int z1, Material wheat, Material furrow, Material fence, int gapAt)
        {
            Transform field = WorldKit.Group(parent, "Field");
            WorldKit.Patch("Furrows", field, x0, z0, x1, z1, furrow);
            for (int z = z0; z < z1; z += 2)
                WorldKit.Box("Wheat", field, new Vector3((x0 + x1) / 2f, 0.18f, z + 0.5f), new Vector3(x1 - x0 - 0.4f, 0.35f, 0.7f), wheat, collider: false);
            WorldKit.Fence(field, "Fence N", x0, z1, x1, z1, fence, gapAt);
            WorldKit.Fence(field, "Fence S", x0, z0, x1, z0, fence);
            WorldKit.Fence(field, "Fence W", x0, z0, x0, z1, fence);
            WorldKit.Fence(field, "Fence E", x1, z0, x1, z1, fence, (z0 + z1) / 2);
        }
    }
}
