using System.Collections.Generic;
using Rth.MapTools;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Palmerway: the region east of Tolling, where the road to Cruor leaves the town's
    /// outskirts. Mostly open heath, which is what most of the world between places should be:
    /// a road, Wennow's north shore with its reeds, a few deliberate trees and rocks (future
    /// resources), a waystone, the shell of an old toll-house, and the barred waygate where the
    /// built world currently ends. Once built, the scene is the source of truth.
    /// </summary>
    public static class PalmerwayBuilder
    {
        public const string ScenePath = "Assets/Scenes/Palmerway.unity";
        private static readonly Vector2Int Origin = new Vector2Int(96, 0);
        private static readonly Vector2Int Size = new Vector2Int(96, 96);

        [MenuItem("R'th/Dev/Rebuild Palmerway Scene", priority = 102)]
        private static void BuildMenu()
        {
            if (WorldKit.ConfirmRebuild(ScenePath)) Build();
        }

        public static void Build()
        {
            var kit = new WorldKit(seed: 4111);
            Material grass = kit.Mat("Grass", new Color(0.36f, 0.55f, 0.22f));
            Material heath = kit.Mat("Heath", new Color(0.45f, 0.5f, 0.28f));
            Material dirt = kit.Mat("Dirt", new Color(0.55f, 0.42f, 0.26f));
            Material bay = kit.Mat("BayWater", new Color(0.17f, 0.33f, 0.55f));
            Material sand = kit.Mat("Sand", new Color(0.82f, 0.75f, 0.55f));
            Material stone = kit.Mat("Stone", new Color(0.52f, 0.52f, 0.50f));
            Material mossy = kit.Mat("MossyStone", new Color(0.45f, 0.5f, 0.42f));
            Material timber = kit.Mat("Timber", new Color(0.4f, 0.28f, 0.18f));
            Material wood = kit.Mat("Fence", new Color(0.45f, 0.33f, 0.20f));
            Material synodRed = kit.Mat("SynodRed", new Color(0.55f, 0.1f, 0.12f));
            Material bark = kit.Mat("Bark", new Color(0.36f, 0.25f, 0.15f));
            Material leaves = kit.Mat("Leaves", new Color(0.20f, 0.42f, 0.18f));
            Material darkLeaves = kit.Mat("DarkLeaves", new Color(0.15f, 0.33f, 0.16f));
            Material willowLeaves = kit.Mat("WillowLeaves", new Color(0.38f, 0.52f, 0.25f));
            Material reed = kit.Mat("Reed", new Color(0.55f, 0.6f, 0.3f));

            MapArea area = WorldKit.BeginRegion("palmerway", "Palmerway", Origin, Size, grass);
            Transform map = area.transform;
            var bounds = new RectInt(Origin, Size);

            // ---------------------------------------------------------- Wennow's north shore
            // The same circle Tolling draws, so the water meets across the border.
            var wash = new HashSet<Vector2Int>();
            var shore = new HashSet<Vector2Int>();
            for (int z = bounds.yMin; z < bounds.yMax; z++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, z + 0.5f), new Vector2(104f, -6f));
                if (d < 30f) wash.Add(new Vector2Int(x, z));
                else if (d < 33f) shore.Add(new Vector2Int(x, z));
            }
            WorldKit.Water(map, "Wennow", wash, bay);
            Transform shoreGroup = WorldKit.Group(map, "Shore");
            foreach (Vector2Int t in shore)
                WorldKit.Box("Sand", shoreGroup, new Vector3(t.x + 0.5f, 0.001f, t.y + 0.5f), new Vector3(1f, 0.01f, 1f), sand);

            Transform reeds = WorldKit.Group(map, "Reeds");
            for (int z = bounds.yMin; z < bounds.yMax; z++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, z + 0.5f), new Vector2(104f, -6f));
                if (d >= 33f && d < 36.5f && kit.Chance(0.3)) kit.Reeds(reeds, x, z, reed);
            }

            // ---------------------------------------------------------- heath and the road
            Transform ground = WorldKit.Group(map, "Ground cover");
            WorldKit.Patch("Heath", ground, 146, 66, 186, 94, heath);
            WorldKit.Patch("Heath", ground, 100, 78, 128, 94, heath);
            WorldKit.Patch("Heath", ground, 160, 8, 190, 34, heath);

            var road = WorldKit.Along(new[]
            {
                new Vector2(96f, 46f), new Vector2(110f, 47.5f), new Vector2(126f, 52f), new Vector2(142f, 53f),
                new Vector2(158f, 50.5f), new Vector2(172f, 49.5f), new Vector2(182f, 49.5f),
            }, 0.9f, bounds);
            road.ExceptWith(wash);
            WorldKit.Road(map, "Palmerway", road, dirt);

            // ---------------------------------------------------------- the waystone
            Transform waystone = WorldKit.Station(map, "Waystone", "waystone", 128, 55);
            WorldKit.Box("Stone", waystone, waystone.position + new Vector3(0f, 0.7f, 0f), new Vector3(0.6f, 1.4f, 0.35f), mossy);
            WorldKit.Box("Cap", waystone, waystone.position + new Vector3(0f, 1.45f, 0f), new Vector3(0.7f, 0.12f, 0.45f), stone, collider: false);

            // ---------------------------------------------------------- the old toll-house
            // Tolling's toll was collected here once, before the town was too small to bother with.
            // Roofless and broken: walls with gaps, no door.
            Transform tollHouse = WorldKit.Group(map, "Old toll-house");
            WorldKit.Patch("Floor", tollHouse, 140, 56, 146, 61, dirt);
            WorldKit.Wall(tollHouse, "Wall S", 140, 56, 146, 56, stone, 1.1f, 0.25f, 142, 143);
            WorldKit.Wall(tollHouse, "Wall W", 140, 56, 140, 61, stone, 0.8f, 0.25f, 58);
            WorldKit.Wall(tollHouse, "Wall N", 140, 61, 146, 61, stone, 1.4f, 0.25f, 144);
            WorldKit.Wall(tollHouse, "Wall E", 146, 56, 146, 61, stone, 0.6f, 0.25f, 57, 58);
            WorldKit.Box("Fallen beam", tollHouse, new Vector3(143f, 0.12f, 59.2f), new Vector3(3.2f, 0.22f, 0.25f), timber, collider: false, yaw: 18f);
            WorldKit.Fades(tollHouse, 0.35f);

            // ---------------------------------------------------------- the waygate
            // Where the built world currently ends: the wardens' palisade, the whole width of the region, with the gate barred.
            Transform gateGroup = WorldKit.Group(map, "Waygate");
            WorldKit.Fence(gateGroup, "Palisade", 183, 0, 183, 96, wood, 49); // the whole width: east of here is unbuilt
            for (int z = 0; z <= 96; z += 2)
                if (z != 48 && z != 50)
                    WorldKit.Box("Stake", gateGroup, new Vector3(183f, 0.9f, z), new Vector3(0.22f, 1.8f, 0.22f), wood, collider: false);
            Transform gate = WorldKit.Station(gateGroup, "Waygate", "palmerway_waygate", 182, 49); // the tile west of the line, so it is used from the road
            WorldKit.Box("Gate", gate, gate.position + new Vector3(0.5f, 0.8f, 0f), new Vector3(0.25f, 1.6f, 1f), timber); // on the palisade line
            WorldKit.Box("Seal", gate, gate.position + new Vector3(0.35f, 1f, 0f), new Vector3(0.05f, 0.35f, 0.35f), synodRed, collider: false);

            // ---------------------------------------------------------- trees and rocks
            // A large forest in this world is thirty trees; open country between places has a few.
            Transform trees = WorldKit.Group(map, "Trees");
            (string kind, int x, int z, Material crown, float scale)[] grove =
            {
                ("Oak", 112, 64, leaves, 1.15f), ("Oak", 115, 67, leaves, 1f), ("Tree", 110, 70, darkLeaves, 1f),
                ("Tree", 152, 28, darkLeaves, 1f), ("Tree", 155, 25, leaves, 1.05f), ("Tree", 157, 29, darkLeaves, 0.95f),
                ("Willow", 121, 30, willowLeaves, 1f),
                ("Tree", 135, 40, leaves, 1f), ("Oak", 150, 66, leaves, 1.2f), ("Tree", 166, 36, leaves, 1f), ("Tree", 171, 63, darkLeaves, 1f),
            };
            foreach ((string kind, int x, int z, Material crown, float scale) in grove)
                kit.Tree(trees, x, z, bark, crown, scale, kind);

            Transform rocks = WorldKit.Group(map, "Rocks");
            foreach (Vector2Int r in new[] { new Vector2Int(122, 74), new Vector2Int(160, 80), new Vector2Int(172, 30), new Vector2Int(138, 31), new Vector2Int(104, 58) })
                WorldKit.Rock(rocks, r.x, r.y, stone);

            // ---------------------------------------------------------- creatures
            Transform creatures = WorldKit.Group(map, "Creatures");
            foreach (Vector2Int r in new[] { new Vector2Int(116, 33), new Vector2Int(127, 28), new Vector2Int(133, 22) })
                WorldKit.Spawn(creatures, "fen_rat", r.x, r.y, 3);

            WorldKit.SaveRegion(area, ScenePath);
        }
    }
}
