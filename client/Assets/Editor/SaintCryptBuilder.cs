using System.Collections.Generic;
using Rth.MapTools;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Blockout of the Unquiet Saint's crypt (docs/ideas/The Unquiet Saint Religion Skilling Boss
    /// Spec.md): one nave, 18 tiles wide and 28 long, the stair down at the south end and the Saint's
    /// bier on a dais at the north. An instanced region, built far from the rest of the world.
    /// Placeholder shapes and colours only: this is for walking the layout and timing the routes.
    /// Once built, the scene is the source of truth.
    /// </summary>
    public static class SaintCryptBuilder
    {
        public const string ScenePath = "Assets/Scenes/SaintsCrypt.unity";
        private static readonly Vector2Int Origin = new Vector2Int(1024, 1024);
        private static readonly Vector2Int Size = new Vector2Int(40, 32);

        // The nave, in absolute tiles: x 1035..1052, y 1026..1053. Layout below is nave-local.
        private const int NaveX = 1035, NaveY = 1026, NaveW = 18, NaveL = 28;
        private const int DaisFrom = 24; // the last four rows are the dais

        private static readonly Vector2Int Altar = new Vector2Int(8, 13);
        private static readonly Vector2Int Offertory = new Vector2Int(10, 13);
        private static readonly Vector2Int[] NearFounts = { new Vector2Int(4, 9), new Vector2Int(13, 9), new Vector2Int(4, 17), new Vector2Int(13, 17) };
        private static readonly Vector2Int[] FarFounts = { new Vector2Int(1, 2), new Vector2Int(16, 2), new Vector2Int(1, 22), new Vector2Int(16, 22) };
        // Two columns by three rows: every floor tile is within 5 of one.
        private static readonly Vector2Int[] Sigils =
        {
            new Vector2Int(4, 4), new Vector2Int(13, 4), new Vector2Int(4, 12), new Vector2Int(13, 12), new Vector2Int(4, 20), new Vector2Int(13, 20),
        };
        private static readonly Vector2Int[] Pillars = { new Vector2Int(2, 24), new Vector2Int(5, 26), new Vector2Int(12, 26), new Vector2Int(15, 24) };

        [MenuItem("R'th/Dev/Rebuild Saint's Crypt Scene", priority = 103)]
        private static void BuildMenu()
        {
            if (WorldKit.ConfirmRebuild(ScenePath)) Build();
        }

        private static Vector2Int At(Vector2Int local) => new Vector2Int(NaveX + local.x, NaveY + local.y);

        private static Vector3 Center(Vector2Int local, float y = 0f) => new Vector3(NaveX + local.x + 0.5f, y, NaveY + local.y + 0.5f);

        public static void Build()
        {
            var kit = new WorldKit(seed: 7310);
            Material earth = kit.Mat("CryptEarth", new Color(0.12f, 0.1f, 0.09f));
            Material floor = kit.Mat("CryptFloor", new Color(0.36f, 0.33f, 0.31f));
            Material flag = kit.Mat("CryptFlagstone", new Color(0.3f, 0.27f, 0.26f));
            Material wall = kit.Mat("CryptWall", new Color(0.42f, 0.38f, 0.35f));
            Material dais = kit.Mat("CryptDais", new Color(0.46f, 0.41f, 0.38f));
            Material synodRed = kit.Mat("SynodRed", new Color(0.55f, 0.1f, 0.12f));
            Material bronze = kit.Mat("Bronze", new Color(0.6f, 0.45f, 0.2f)); // shared with Tolling
            Material bloodPool = kit.Mat("FountBlood", new Color(0.35f, 0.03f, 0.05f));
            Material bone = kit.Mat("SaintBone", new Color(0.82f, 0.78f, 0.68f));
            Material ring = kit.Mat("BlockoutRing", new Color(0.55f, 0.45f, 0.2f));
            Material sigil = kit.Mat("BlockoutSigil", new Color(0.85f, 0.8f, 0.55f));
            Material wax = kit.Mat("CandleWax", new Color(0.9f, 0.86f, 0.72f));

            MapArea area = WorldKit.BeginRegion("saints_crypt", "The Saint's Crypt", Origin, Size, earth);
            area.instanced = true;
            Transform map = area.transform;

            // ---------------------------------------------------------- the nave
            Transform nave = WorldKit.Group(map, "Nave");
            WorldKit.Patch("Floor", nave, NaveX, NaveY, NaveX + NaveW, NaveY + NaveL, floor);
            // A flagstone aisle up the middle, the way Believer's projectiles come.
            WorldKit.Patch("Aisle", nave, NaveX + 7, NaveY, NaveX + 11, NaveY + DaisFrom, flag, 0.006f);
            // Each wall fades on its own when it's between the camera and you; the floor never does.
            Transform west = WorldKit.Group(nave, "Wall W"), east = WorldKit.Group(nave, "Wall E");
            Transform north = WorldKit.Group(nave, "Wall N"), south = WorldKit.Group(nave, "Wall S");
            WorldKit.Wall(west, "Wall W", NaveX, NaveY, NaveX, NaveY + NaveL, wall, 3f, 0.4f);
            WorldKit.Wall(east, "Wall E", NaveX + NaveW, NaveY, NaveX + NaveW, NaveY + NaveL, wall, 3f, 0.4f);
            WorldKit.Wall(north, "Wall N", NaveX, NaveY + NaveL, NaveX + NaveW, NaveY + NaveL, wall, 3f, 0.4f);
            WorldKit.Wall(south, "Wall S", NaveX, NaveY, NaveX + NaveW, NaveY, wall, 3f, 0.4f, NaveX + 8, NaveX + 9); // the stair
            // Half-columns along the side walls, every four tiles: no collision, just rhythm.
            for (int y = 2; y < NaveL; y += 4)
            {
                WorldKit.Box("Column", west, new Vector3(NaveX + 0.15f, 1.5f, NaveY + y), new Vector3(0.5f, 3f, 0.5f), wall, collider: false);
                WorldKit.Box("Column", east, new Vector3(NaveX + NaveW - 0.15f, 1.5f, NaveY + y), new Vector3(0.5f, 3f, 0.5f), wall, collider: false);
            }
            foreach (Transform w in new[] { west, east, north, south }) WorldKit.Fades(w, 0.3f);

            // ---------------------------------------------------------- the stair out
            Transform stairGroup = WorldKit.Group(map, "Stair");
            Transform stair = WorldKit.Station(stairGroup, "Stair", "crypt_stair", NaveX + 8, NaveY - 1);
            for (int i = 0; i < 4; i++)
                WorldKit.Box("Step", stair, new Vector3(NaveX + 9f, 0.15f + i * 0.25f, NaveY - 0.4f - i * 0.45f), new Vector3(2f, 0.3f, 0.45f), dais, collider: i == 0);
            // No MapSpawn: new characters start in Tolling. Instancing will put you at the foot of the stair.

            // ---------------------------------------------------------- the dais and the Saint
            Transform daisGroup = WorldKit.Group(map, "Dais");
            WorldKit.Box("Dais", daisGroup, new Vector3(NaveX + NaveW / 2f, 0.06f, NaveY + DaisFrom + (NaveL - DaisFrom) / 2f), new Vector3(NaveW - 0.8f, 0.12f, NaveL - DaisFrom), dais, collider: false);
            WorldKit.Box("Step", daisGroup, new Vector3(NaveX + NaveW / 2f, 0.03f, NaveY + DaisFrom - 0.3f), new Vector3(8f, 0.06f, 0.6f), dais, collider: false);
            Transform saint = WorldKit.Station(daisGroup, "The Saint", "unquiet_saint", NaveX + 8, NaveY + 26);
            saint.position += new Vector3(0.5f, 0f, 0f); // the bier spans two tiles
            WorldKit.Box("Bier", saint, saint.position + new Vector3(0f, 0.55f, 0f), new Vector3(1.9f, 0.5f, 1.1f), wall);
            WorldKit.Box("Pall", saint, saint.position + new Vector3(0f, 0.82f, 0f), new Vector3(1.8f, 0.06f, 1.0f), synodRed, collider: false);
            WorldKit.Box("Saint", saint, saint.position + new Vector3(0f, 1.0f, 0f), new Vector3(0.45f, 0.3f, 1.6f), bone, collider: false);
            WorldKit.Box("Head", saint, saint.position + new Vector3(0f, 1.05f, 0.85f), new Vector3(0.3f, 0.3f, 0.3f), bone, collider: false);
            for (int side = -1; side <= 1; side += 2)
            {
                WorldKit.Cylinder("Candle", saint, saint.position + new Vector3(side * 1.4f, 0.75f, 0.5f), new Vector3(0.14f, 0.45f, 0.14f), wax, collider: false);
                WorldKit.Cylinder("Candle", saint, saint.position + new Vector3(side * 1.4f, 0.6f, -0.5f), new Vector3(0.14f, 0.3f, 0.14f), wax, collider: false);
            }
            foreach (Vector2Int p in Pillars)
            {
                Vector2Int t = At(p);
                Transform pillar = WorldKit.Station(daisGroup, "Holy symbol", "holy_symbol", t.x, t.y);
                WorldKit.Cylinder("Pillar", pillar, pillar.position + new Vector3(0f, 1.25f, 0f), new Vector3(0.6f, 1.25f, 0.6f), wall);
                WorldKit.Box("Symbol", pillar, pillar.position + new Vector3(0f, 2.3f, -0.32f), new Vector3(0.45f, 0.45f, 0.06f), bronze, collider: false, yaw: 45f);
            }

            // ---------------------------------------------------------- the altar and the offertory
            Transform centre = WorldKit.Group(map, "Altar");
            Vector2Int altar = At(Altar);
            Transform altarStation = WorldKit.Station(centre, "Altar", "crypt_altar", altar.x, altar.y);
            WorldKit.Box("Altar", altarStation, altarStation.position + new Vector3(0f, 0.45f, 0f), new Vector3(0.95f, 0.9f, 0.95f), dais);
            WorldKit.Box("Cloth", altarStation, altarStation.position + new Vector3(0f, 0.91f, 0f), new Vector3(1.0f, 0.03f, 0.5f), synodRed, collider: false);
            Vector2Int offertory = At(Offertory);
            Transform basin = WorldKit.Station(centre, "Offertory", "crypt_offertory", offertory.x, offertory.y);
            WorldKit.Cylinder("Stand", basin, basin.position + new Vector3(0f, 0.35f, 0f), new Vector3(0.35f, 0.35f, 0.35f), dais);
            WorldKit.Cylinder("Basin", basin, basin.position + new Vector3(0f, 0.74f, 0f), new Vector3(0.75f, 0.06f, 0.75f), bronze, collider: false);

            // ---------------------------------------------------------- founts, with their light-ring footprints
            Transform founts = WorldKit.Group(map, "Founts");
            foreach ((Vector2Int[] set, string name) in new[] { (NearFounts, "Near fount"), (FarFounts, "Far fount") })
                foreach (Vector2Int f in set)
                {
                    Vector2Int t = At(f);
                    // Blockout: the 3x3 ring Let there be light uses, drawn faintly on the floor.
                    WorldKit.Patch("Ring footprint", founts, t.x - 1, t.y - 1, t.x + 2, t.y + 2, ring, 0.008f);
                    Transform fount = WorldKit.Station(founts, name, "crypt_fount", t.x, t.y);
                    WorldKit.Cylinder("Pedestal", fount, fount.position + new Vector3(0f, 0.3f, 0f), new Vector3(0.45f, 0.3f, 0.45f), wall);
                    WorldKit.Cylinder("Bowl", fount, fount.position + new Vector3(0f, 0.66f, 0f), new Vector3(0.7f, 0.08f, 0.7f), bronze, collider: false);
                    WorldKit.Cylinder("Blood", fount, fount.position + new Vector3(0f, 0.76f, 0f), new Vector3(0.55f, 0.02f, 0.55f), bloodPool, collider: false);
                }

            // ---------------------------------------------------------- safe sigils
            Transform sigils = WorldKit.Group(map, "Safe sigils");
            foreach (Vector2Int s in Sigils)
                WorldKit.Cylinder("Sigil", sigils, Center(s, 0.012f), new Vector3(0.85f, 0.005f, 0.85f), sigil, collider: false);

            WorldKit.SaveRegion(area, ScenePath);
        }
    }
}
