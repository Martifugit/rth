using Rth.MapTools;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// The Unquiet Saint's crypt (docs/ideas/The Unquiet Saint Religion Skilling Boss Spec.md), an
    /// instanced region built far from the world. The playable nave is the spec's 18 x 28 tiles;
    /// everything around it is there to make it feel huge: piers fourteen metres tall, side aisles
    /// lost in the dark, red banners, and at the north end a colossus of the martyr bowed over his
    /// own body, pouring blood from a chalice onto the bier. You come in at the south door and run
    /// the length of the nave to pray at him, which is how the encounter will start.
    /// Only the nave floor has a collider, so nothing else is walkable (the exporter treats any
    /// collider as ground). Once built, the scene is the source of truth.
    /// </summary>
    public static class SaintCryptBuilder
    {
        public const string ScenePath = "Assets/Scenes/SaintsCrypt.unity";
        private static readonly Vector2Int Origin = new Vector2Int(1016, 1016);
        private static readonly Vector2Int Size = new Vector2Int(56, 48);

        // The nave, in absolute tiles: x 1035..1052, y 1026..1053. Layout below is nave-local.
        private const int NaveX = 1035, NaveY = 1026, NaveW = 18, NaveL = 28;
        private const int DaisFrom = 24; // the last four rows are the dais
        private const float Mid = NaveX + NaveW / 2f; // 1044: the nave's centre line
        private const float PierHeight = 14f;

        private static readonly Vector2Int Altar = new Vector2Int(8, 13);
        private static readonly Vector2Int Offertory = new Vector2Int(10, 13);
        private static readonly Vector2Int Bier = new Vector2Int(8, 26);
        private static readonly Vector2Int[] NearFounts = { new Vector2Int(4, 9), new Vector2Int(13, 9), new Vector2Int(4, 17), new Vector2Int(13, 17) };
        private static readonly Vector2Int[] FarFounts = { new Vector2Int(1, 2), new Vector2Int(16, 2), new Vector2Int(1, 22), new Vector2Int(16, 22) };
        // Two columns by three rows: every floor tile is within 5 of one.
        private static readonly Vector2Int[] Sigils =
        {
            new Vector2Int(4, 4), new Vector2Int(13, 4), new Vector2Int(4, 12), new Vector2Int(13, 12), new Vector2Int(4, 20), new Vector2Int(13, 20),
        };
        // The four holy symbols' pillars, flanking the dais; each symbol has its own shape.
        private static readonly (Vector2Int Tile, string Shape)[] Pillars =
        {
            (new Vector2Int(2, 27), "disc"), (new Vector2Int(5, 27), "diamond"), (new Vector2Int(12, 27), "cross"), (new Vector2Int(15, 27), "bar"),
        };

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
            Material earth = kit.Mat("CryptEarth", new Color(0.05f, 0.045f, 0.045f));
            Material floor = kit.Mat("CryptFloor", new Color(0.3f, 0.27f, 0.26f));
            Material flag = kit.Mat("CryptFlagstone", new Color(0.24f, 0.21f, 0.2f));
            Material stone = kit.Mat("CryptWall", new Color(0.34f, 0.31f, 0.29f));
            Material darkStone = kit.Mat("CryptDarkStone", new Color(0.17f, 0.15f, 0.15f));
            Material dais = kit.Mat("CryptDais", new Color(0.4f, 0.36f, 0.33f));
            Material synodRed = kit.Mat("SynodRed", new Color(0.55f, 0.1f, 0.12f));
            Material banner = kit.Mat("CryptBanner", new Color(0.42f, 0.05f, 0.07f));
            Material bronze = kit.Mat("Bronze", new Color(0.6f, 0.45f, 0.2f)); // shared with Tolling
            Material iron = kit.Mat("CryptIron", new Color(0.14f, 0.13f, 0.13f));
            Material bone = kit.Mat("SaintBone", new Color(0.78f, 0.74f, 0.64f));
            Material robe = kit.Mat("SaintRobe", new Color(0.3f, 0.07f, 0.08f));
            Material wax = kit.Mat("CandleWax", new Color(0.9f, 0.86f, 0.72f));
            Material ring = kit.Mat("BlockoutRing", new Color(0.32f, 0.26f, 0.14f));
            Material sigil = kit.Mat("BlockoutSigil", new Color(0.6f, 0.56f, 0.38f));

            Material bloodGlow = FxKit.Glow("FountBloodGlow", new Color(0.3f, 0.015f, 0.03f), 0.35f);
            Material channelGlow = FxKit.Glow("BloodChannelGlow", new Color(0.35f, 0.02f, 0.04f), 0.6f);
            Material eyes = FxKit.Glow("SaintEyes", new Color(1f, 0.15f, 0.1f), 2.5f);
            Material coals = FxKit.Glow("BrazierCoals", new Color(1f, 0.35f, 0.08f), 1.6f);
            Material flame = FxKit.ParticleMat("FxFlame", new Color(1f, 0.6f, 0.25f, 0.9f), additive: true);
            Material ember = FxKit.ParticleMat("FxEmber", new Color(1f, 0.55f, 0.2f, 1f), additive: true);
            Material mist = FxKit.ParticleMat("FxMist", new Color(0.32f, 0.25f, 0.26f, 0.12f), additive: false);
            Material mote = FxKit.ParticleMat("FxMote", new Color(0.8f, 0.85f, 1f, 0.6f), additive: true);
            Material blood = FxKit.ParticleMat("FxBlood", new Color(0.5f, 0.03f, 0.05f, 1f), additive: false);
            Material beam = FxKit.ParticleMat("FxLightBeam", new Color(0.5f, 0.6f, 0.8f, 0.13f), additive: true);

            MapArea area = WorldKit.BeginRegion("saints_crypt", "The Saint's Crypt", Origin, Size, earth);
            area.instanced = true;
            area.ownAtmosphere = true;
            area.sunIntensity = 0.04f;
            area.ambientSky = new Color(0.22f, 0.19f, 0.25f);
            area.ambientEquator = new Color(0.17f, 0.13f, 0.13f);
            area.ambientGround = new Color(0.04f, 0.03f, 0.03f);
            area.fog = new Color(0.02f, 0.015f, 0.018f);
            area.fogStart = 20f;
            area.fogEnd = 75f;
            Transform map = area.transform;
            // The region's ground is the dark beyond; only the nave floor below can be walked on.
            Object.DestroyImmediate(map.Find("Ground").GetComponent<Collider>());

            BuildNave(map, floor, flag, ring, sigil, darkStone);
            BuildArcades(map, stone, darkStone, banner, iron, coals, flame, ember);
            BuildEntrance(map, darkStone, dais, iron, coals, flame, ember);
            BuildDais(map, dais, stone, synodRed, bronze, bone, wax);
            BuildColossus(map, darkStone, stone, robe, bone, bronze, eyes, blood, iron, coals, flame, ember);
            BuildCentre(map, dais, synodRed, bronze, channelGlow, beam, mote);
            BuildFounts(map, stone, bronze, bloodGlow, blood);

            // Mist over the whole nave, and post-processing so glows glow and the edges close in.
            Transform fx = WorldKit.Group(map, "Atmosphere");
            FxKit.Mist(fx, new Vector3(Mid, 0.35f, NaveY + NaveL / 2f), new Vector2(NaveW + 6f, NaveL + 4f), mist, Color.white);
            FxKit.Volume(fx, "SaintsCryptVolume", new Vector3(Mid, 15f, NaveY + NaveL / 2f + 4f), new Vector3(70f, 40f, 62f), bloom: 1.4f, vignette: 0.42f, saturation: -12f);

            WorldKit.SaveRegion(area, ScenePath);
        }

        // ------------------------------------------------------------------ the nave floor

        private static void BuildNave(Transform map, Material floor, Material flag, Material ring, Material sigil, Material darkStone)
        {
            Transform nave = WorldKit.Group(map, "Nave");
            // The one walkable surface (it has the collider).
            WorldKit.Patch("Floor", nave, NaveX, NaveY, NaveX + NaveW, NaveY + NaveL, floor);
            // A flagstone aisle up the middle, the way Believer's projectiles come.
            NoCollider(WorldKit.Patch("Aisle", nave, NaveX + 7, NaveY, NaveX + 11, NaveY + DaisFrom, flag, 0.006f));
            // The side aisles, a step down and out of reach: dark floor under the arcades.
            foreach (float x in new[] { NaveX - 5f, NaveX + NaveW + 5f })
                WorldKit.Box("Side aisle", nave, new Vector3(x, -0.35f, NaveY + NaveL / 2f + 2f), new Vector3(10f, 0.1f, NaveL + 12f), darkStone, collider: false);
            // A kerb along each long edge, where the nave drops to the aisles.
            foreach (float x in new[] { NaveX - 0.1f, NaveX + NaveW + 0.1f })
                WorldKit.Box("Kerb", nave, new Vector3(x, -0.1f, NaveY + NaveL / 2f), new Vector3(0.3f, 0.5f, NaveL), darkStone, collider: false);

            Transform sigils = WorldKit.Group(map, "Safe sigils");
            foreach (Vector2Int s in Sigils)
            {
                WorldKit.Cylinder("Sigil", sigils, Center(s, 0.012f), new Vector3(0.9f, 0.005f, 0.9f), sigil, collider: false);
                WorldKit.Box("Sigil mark", sigils, Center(s, 0.02f), new Vector3(0.12f, 0.005f, 0.7f), darkStone, collider: false, yaw: 45f);
                WorldKit.Box("Sigil mark", sigils, Center(s, 0.02f), new Vector3(0.12f, 0.005f, 0.7f), darkStone, collider: false, yaw: -45f);
            }
            // Blockout: the 3x3 rings Let there be light uses, drawn faintly round every fount.
            foreach (Vector2Int f in NearFounts) NoCollider(WorldKit.Patch("Ring footprint", nave, At(f).x - 1, At(f).y - 1, At(f).x + 2, At(f).y + 2, ring, 0.008f));
            foreach (Vector2Int f in FarFounts) NoCollider(WorldKit.Patch("Ring footprint", nave, At(f).x - 1, At(f).y - 1, At(f).x + 2, At(f).y + 2, ring, 0.008f));
        }

        private static void NoCollider(GameObject go) => Object.DestroyImmediate(go.GetComponent<Collider>());

        // ------------------------------------------------------------------ piers, arches, banners, braziers

        private static void BuildArcades(Transform map, Material stone, Material darkStone, Material banner, Material iron, Material coals, Material flame, Material ember)
        {
            Transform arcades = WorldKit.Group(map, "Arcades");
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side < 0 ? NaveX - 1f : NaveX + NaveW + 1f;
                float outer = side < 0 ? NaveX - 10.5f : NaveX + NaveW + 10.5f;
                for (int i = 0; i < 8; i++)
                {
                    float y = NaveY + 0.5f + i * 4f;
                    // One pier: a plinth, a shaft far taller than anyone, and a capital.
                    Transform pier = WorldKit.Group(arcades, "Pier");
                    WorldKit.Box("Plinth", pier, new Vector3(x, 0.4f, y), new Vector3(2.2f, 0.8f, 2.2f), darkStone, collider: false);
                    WorldKit.Box("Shaft", pier, new Vector3(x, PierHeight / 2f, y), new Vector3(1.5f, PierHeight, 1.5f), stone, collider: false);
                    WorldKit.Box("Capital", pier, new Vector3(x, PierHeight + 0.3f, y), new Vector3(2.1f, 0.6f, 2.1f), darkStone, collider: false);
                    WorldKit.Fades(pier, 0.25f);
                    if (i == 7) continue;
                    // Arches between piers, stepped in to suggest a point, high in the dark.
                    Transform arch = WorldKit.Group(arcades, "Arch");
                    for (int step = 0; step < 4; step++)
                    {
                        float rise = PierHeight + 0.6f + step * 0.7f;
                        float span = 4f - step * 0.9f;
                        WorldKit.Box("Voussoir", arch, new Vector3(x, rise, y + 2f), new Vector3(1.3f, 0.7f, span), stone, collider: false);
                    }
                    WorldKit.Fades(arch, 0.25f);
                    // A long red banner hanging in the arch, on the aisle side.
                    WorldKit.Box("Banner", arcades, new Vector3(x + side * 0.9f, PierHeight - 4.5f, y + 2f), new Vector3(0.05f, 8f, 1.6f), banner, collider: false);
                }
                // The outer wall of the aisle, taller still, and blind.
                WorldKit.Box("Aisle wall", arcades, new Vector3(outer, 9f, NaveY + NaveL / 2f + 2f), new Vector3(1f, 18f, NaveL + 12f), darkStone, collider: false);
                // Braziers on tall iron stands in the aisles, seen through the arches.
                for (int i = 0; i < 3; i++)
                    Brazier(arcades, new Vector3(x + side * 3.2f, 0f, NaveY + 6.5f + i * 8f), 1.8f, iron, coals, flame, ember);
            }
        }

        private static void Brazier(Transform parent, Vector3 foot, float height, Material iron, Material coals, Material flame, Material ember)
        {
            Transform b = WorldKit.Group(parent, "Brazier");
            WorldKit.Cylinder("Stand", b, foot + new Vector3(0f, height / 2f, 0f), new Vector3(0.18f, height / 2f, 0.18f), iron, collider: false);
            WorldKit.Cylinder("Bowl", b, foot + new Vector3(0f, height + 0.1f, 0f), new Vector3(0.9f, 0.14f, 0.9f), iron, collider: false);
            WorldKit.Cylinder("Coals", b, foot + new Vector3(0f, height + 0.24f, 0f), new Vector3(0.7f, 0.03f, 0.7f), coals, collider: false);
            FxKit.Fire(b, foot + new Vector3(0f, height + 0.25f, 0f), 1f, flame);
            FxKit.Embers(b, foot + new Vector3(0f, height + 0.4f, 0f), 6f, ember);
            FxKit.PointLight(b, "Firelight", foot + new Vector3(0f, height + 0.9f, 0f), new Color(1f, 0.5f, 0.2f), 6f, 9f, flicker: 0.35f);
        }

        // ------------------------------------------------------------------ the way in

        private static void BuildEntrance(Transform map, Material darkStone, Material dais, Material iron, Material coals, Material flame, Material ember)
        {
            Transform entry = WorldKit.Group(map, "Entrance");
            // The south wall: a cliff of stone with one tall, narrow door. Fades when it's in the way.
            float y = NaveY - 0.4f, west = NaveX - 11f, east = NaveX + NaveW + 11f;
            WorldKit.Box("South wall W", entry, new Vector3((west + Mid - 1f) / 2f, 9f, y), new Vector3(Mid - 1f - west, 18f, 0.8f), darkStone, collider: false);
            WorldKit.Box("South wall E", entry, new Vector3((Mid + 1f + east) / 2f, 9f, y), new Vector3(east - Mid - 1f, 18f, 0.8f), darkStone, collider: false);
            WorldKit.Box("Above the door", entry, new Vector3(Mid, 12f, y), new Vector3(2.4f, 12f, 0.8f), darkStone, collider: false);
            WorldKit.Fades(entry, 0.15f);
            // The stair beyond the door, climbing out of sight.
            Transform stairGroup = WorldKit.Group(map, "Stair");
            for (int i = 0; i < 10; i++)
                WorldKit.Box("Step", stairGroup, new Vector3(Mid, 0.15f + i * 0.3f, NaveY - 1.2f - i * 0.55f), new Vector3(2f, 0.3f, 0.6f), dais, collider: false);
            Transform stair = WorldKit.Station(stairGroup, "Stair", "crypt_stair", NaveX + 8, NaveY - 1);
            WorldKit.Box("Threshold", stair, new Vector3(Mid - 0.5f, 0.05f, NaveY - 0.5f), new Vector3(0.9f, 0.1f, 0.9f), dais); // the blocker's collider
            // Two braziers either side of the door, the first thing you see coming down.
            Transform doorFires = WorldKit.Group(map, "Door braziers"); // not under the wall, which fades
            Brazier(doorFires, new Vector3(Mid - 2.5f, 0f, NaveY + 0.5f), 1.4f, iron, coals, flame, ember);
            Brazier(doorFires, new Vector3(Mid + 2.5f, 0f, NaveY + 0.5f), 1.4f, iron, coals, flame, ember);
        }

        // ------------------------------------------------------------------ the dais: the martyr, the symbols

        private static void BuildDais(Transform map, Material dais, Material stone, Material synodRed, Material bronze, Material bone, Material wax)
        {
            Transform group = WorldKit.Group(map, "Dais");
            WorldKit.Box("Dais", group, new Vector3(Mid, 0.06f, NaveY + DaisFrom + (NaveL - DaisFrom) / 2f), new Vector3(NaveW - 0.4f, 0.12f, NaveL - DaisFrom), dais, collider: false);
            WorldKit.Box("Step", group, new Vector3(Mid, 0.03f, NaveY + DaisFrom - 0.3f), new Vector3(10f, 0.06f, 0.6f), dais, collider: false);

            // The martyr on his bier. Praying here is how the encounter will begin.
            Vector2Int bier = At(Bier);
            Transform saint = WorldKit.Station(group, "The martyr", "unquiet_saint", bier.x, bier.y);
            var c = new Vector3(Mid, 0f, bier.y + 0.5f);
            WorldKit.Box("Bier", saint, c + new Vector3(0f, 0.55f, 0f), new Vector3(1.9f, 0.9f, 1.2f), stone);
            WorldKit.Box("Pall", saint, c + new Vector3(0f, 1.02f, 0f), new Vector3(2.1f, 0.06f, 1.35f), synodRed, collider: false);
            WorldKit.Box("Body", saint, c + new Vector3(0f, 1.18f, 0f), new Vector3(1.5f, 0.24f, 0.4f), bone, collider: false);
            WorldKit.Box("Head", saint, c + new Vector3(-0.85f, 1.2f, 0f), new Vector3(0.3f, 0.28f, 0.28f), bone, collider: false);
            // Candles crowding the bier.
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Vector3 at = c + new Vector3(Mathf.Cos(a) * 1.6f, 0f, Mathf.Sin(a) * 1.1f);
                float h = 0.3f + (i % 3) * 0.15f;
                WorldKit.Cylinder("Candle", group, at + new Vector3(0f, 0.12f + h / 2f, 0f), new Vector3(0.1f, h / 2f, 0.1f), wax, collider: false);
            }
            FxKit.PointLight(group, "Candlelight", c + new Vector3(0f, 1.5f, -0.6f), new Color(1f, 0.7f, 0.4f), 3f, 5f, flicker: 0.15f);

            // The holy symbols high on four pillars either side, unlit until Are you a believer?
            foreach ((Vector2Int tile, string shape) in Pillars)
            {
                Vector2Int t = At(tile);
                Transform pillar = WorldKit.Station(group, "Holy symbol (" + shape + ")", "holy_symbol", t.x, t.y);
                var p = new Vector3(t.x + 0.5f, 0f, t.y + 0.5f);
                WorldKit.Box("Base", pillar, p + new Vector3(0f, 0.3f, 0f), new Vector3(1f, 0.6f, 1f), stone);
                WorldKit.Cylinder("Pillar", pillar, p + new Vector3(0f, 4f, 0f), new Vector3(0.7f, 4f, 0.7f), stone, collider: false);
                Vector3 s = p + new Vector3(0f, 8.6f, -0.2f);
                switch (shape)
                {
                    case "disc":
                        WorldKit.Cylinder("Symbol", pillar, s, new Vector3(1.5f, 0.05f, 1.5f), bronze, collider: false).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                        break;
                    case "diamond":
                        WorldKit.Box("Symbol", pillar, s, new Vector3(1.1f, 1.1f, 0.1f), bronze, collider: false).transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                        break;
                    case "cross":
                        WorldKit.Box("Symbol", pillar, s, new Vector3(0.3f, 1.6f, 0.1f), bronze, collider: false);
                        WorldKit.Box("Symbol", pillar, s + new Vector3(0f, 0.25f, 0f), new Vector3(1.1f, 0.3f, 0.1f), bronze, collider: false);
                        break;
                    default:
                        WorldKit.Box("Symbol", pillar, s, new Vector3(1.5f, 0.35f, 0.1f), bronze, collider: false);
                        WorldKit.Box("Symbol", pillar, s + new Vector3(0f, -0.55f, 0f), new Vector3(0.9f, 0.35f, 0.1f), bronze, collider: false);
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ the colossus

        private static void BuildColossus(Transform map, Material darkStone, Material stone, Material robe, Material bone, Material bronze, Material eyes, Material blood,
            Material iron, Material coals, Material flame, Material ember)
        {
            // The martyr as Cruor remembers him, twenty metres of him, behind the dais and bowed over
            // it, a chalice tipped in his hands so that blood falls onto his own body on the bier.
            Transform colossus = WorldKit.Group(map, "Colossus");
            float baseY = NaveY + NaveL + 4.5f; // behind the dais, out of reach
            WorldKit.Box("Plinth", colossus, new Vector3(Mid, 1.5f, baseY), new Vector3(9f, 3f, 7f), darkStone, collider: false);
            WorldKit.Box("Plinth step", colossus, new Vector3(Mid, 0.5f, baseY - 3f), new Vector3(12f, 1f, 3f), darkStone, collider: false);
            // The robe falls in tiers, widest at the hem.
            float[] widths = { 6.5f, 5.8f, 5.2f, 4.6f };
            for (int i = 0; i < widths.Length; i++)
                WorldKit.Box("Robe", colossus, new Vector3(Mid, 4.5f + i * 3f, baseY), new Vector3(widths[i], 3f, widths[i] * 0.6f), robe, collider: false);
            Transform upper = WorldKit.Group(colossus, "Upper body");
            WorldKit.Box("Chest", upper, new Vector3(Mid, 16f, baseY - 0.4f), new Vector3(4.4f, 3.6f, 2.6f), robe, collider: false);
            WorldKit.Box("Shoulders", upper, new Vector3(Mid, 17.6f, baseY - 0.6f), new Vector3(5.6f, 1.2f, 2.4f), robe, collider: false);
            // The head bowed over the nave, hooded, with eyes that will open as the Wake bar fills.
            WorldKit.Box("Hood", upper, new Vector3(Mid, 19.3f, baseY - 1.9f), new Vector3(2.6f, 2.8f, 2.6f), robe, collider: false);
            WorldKit.Box("Face", upper, new Vector3(Mid, 19f, baseY - 3.05f), new Vector3(1.6f, 2f, 0.4f), stone, collider: false);
            WorldKit.Box("Eye", upper, new Vector3(Mid - 0.38f, 19.4f, baseY - 3.28f), new Vector3(0.3f, 0.12f, 0.06f), eyes, collider: false);
            WorldKit.Box("Eye", upper, new Vector3(Mid + 0.38f, 19.4f, baseY - 3.28f), new Vector3(0.3f, 0.12f, 0.06f), eyes, collider: false);
            upper.RotateAround(new Vector3(Mid, 14f, baseY), Vector3.right, 22f); // bowing forward over the dais
            FxKit.PointLight(colossus, "Eyes", new Vector3(Mid, 18.4f, baseY - 6f), new Color(1f, 0.15f, 0.1f), 2.5f, 7f, flicker: 0.1f);

            // Arms forward from the shoulders, the hands meeting on a chalice tipped over the bier.
            var chalice = new Vector3(Mid, 9.5f, NaveY + Bier.y + 0.5f);
            for (int side = -1; side <= 1; side += 2)
            {
                var shoulder = new Vector3(Mid + side * 2.4f, 16.6f, baseY - 2.4f);
                Vector3 hand = chalice + new Vector3(side * 0.7f, 0.6f, 0f);
                GameObject arm = WorldKit.Box("Arm", colossus, (shoulder + hand) / 2f, new Vector3(1.1f, 1.1f, Vector3.Distance(shoulder, hand)), robe, collider: false);
                arm.transform.rotation = Quaternion.LookRotation(hand - shoulder);
                WorldKit.Box("Hand", colossus, hand, new Vector3(0.9f, 0.9f, 0.9f), bone, collider: false);
            }
            Transform cup = WorldKit.Group(colossus, "Chalice");
            WorldKit.Cylinder("Cup", cup, chalice + new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.5f, 1.2f), bronze, collider: false);
            WorldKit.Cylinder("Stem", cup, chalice + new Vector3(0f, -0.4f, 0f), new Vector3(0.3f, 0.4f, 0.3f), bronze, collider: false);
            cup.RotateAround(chalice, Vector3.right, 35f); // tipped towards the bier
            FxKit.BloodStream(colossus, chalice + new Vector3(0f, -0.1f, -0.7f), 8.2f, blood);
            // Fires at his feet throw their light up the robe, and two spots from the dais find his face.
            for (int side = -1; side <= 1; side += 2)
            {
                Brazier(colossus, new Vector3(Mid + side * 5.5f, 1f, baseY - 3.5f), 1.2f, iron, coals, flame, ember);
                var spot = new GameObject("Uplight");
                spot.transform.SetParent(colossus, false);
                spot.transform.position = new Vector3(Mid + side * 4f, 0.4f, NaveY + NaveL - 0.5f);
                spot.transform.LookAt(new Vector3(Mid, 17f, baseY - 2.5f));
                var light = spot.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = new Color(1f, 0.45f, 0.25f);
                light.intensity = 60f;
                light.range = 30f;
                light.spotAngle = 38f;
                light.shadows = LightShadows.None;
            }
            // Behind him, the north wall: the apse, black and very high.
            WorldKit.Box("Apse", colossus, new Vector3(Mid, 13f, baseY + 5f), new Vector3(30f, 26f, 1f), darkStone, collider: false);
        }

        // ------------------------------------------------------------------ the altar, the channel, the shaft of light

        private static void BuildCentre(Transform map, Material dais, Material synodRed, Material bronze, Material channelGlow, Material beam, Material mote)
        {
            Transform centre = WorldKit.Group(map, "Altar");
            Vector2Int altar = At(Altar);
            Transform altarStation = WorldKit.Station(centre, "Altar", "crypt_altar", altar.x, altar.y);
            WorldKit.Box("Altar", altarStation, altarStation.position + new Vector3(0f, 0.5f, 0f), new Vector3(0.98f, 1f, 0.98f), dais);
            WorldKit.Box("Cloth", altarStation, altarStation.position + new Vector3(0f, 1.01f, 0f), new Vector3(1.05f, 0.03f, 0.55f), synodRed, collider: false);
            Vector2Int offertory = At(Offertory);
            Transform basin = WorldKit.Station(centre, "Offertory", "crypt_offertory", offertory.x, offertory.y);
            WorldKit.Cylinder("Stand", basin, basin.position + new Vector3(0f, 0.4f, 0f), new Vector3(0.4f, 0.4f, 0.4f), dais);
            WorldKit.Cylinder("Basin", basin, basin.position + new Vector3(0f, 0.84f, 0f), new Vector3(0.85f, 0.06f, 0.85f), bronze, collider: false);

            // The blood from the chalice runs off the dais and down a channel to the altar.
            float from = NaveY + Altar.y + 1f, to = NaveY + DaisFrom + 1.5f;
            WorldKit.Box("Blood channel", centre, new Vector3(Mid - 0.5f, 0.012f, (from + to) / 2f), new Vector3(0.28f, 0.01f, to - from), channelGlow, collider: false);

            // One cold shaft of light from somewhere far above, onto the altar, with dust in it.
            var floorAt = new Vector3(altar.x + 0.5f, 0f, altar.y + 0.5f);
            FxKit.Shaft(centre, floorAt, 26f, 1.6f, new Color(0.65f, 0.75f, 1f), beam);
            FxKit.Dust(centre, floorAt + new Vector3(0f, 4f, 0f), new Vector3(2.6f, 8f, 2.6f), mote);
        }

        // ------------------------------------------------------------------ founts

        private static void BuildFounts(Transform map, Material stone, Material bronze, Material bloodGlow, Material blood)
        {
            Transform founts = WorldKit.Group(map, "Founts");
            foreach ((Vector2Int[] set, string name, float size) in new[] { (NearFounts, "Near fount", 1f), (FarFounts, "Far fount", 1.25f) })
                foreach (Vector2Int f in set)
                {
                    Vector2Int t = At(f);
                    Transform fount = WorldKit.Station(founts, name, "crypt_fount", t.x, t.y);
                    Vector3 p = fount.position;
                    WorldKit.Box("Pedestal", fount, p + new Vector3(0f, 0.4f, 0f), new Vector3(0.6f, 0.8f, 0.6f) * size, stone);
                    for (int r = 0; r < 4; r++)
                        WorldKit.Box("Rib", fount, p + new Vector3(0f, 0.4f * size, 0f), new Vector3(0.9f * size, 0.6f * size, 0.14f), stone, collider: false, yaw: r * 45f);
                    WorldKit.Cylinder("Bowl", fount, p + new Vector3(0f, 0.86f * size, 0f), new Vector3(1f * size, 0.1f, 1f * size), bronze, collider: false);
                    WorldKit.Cylinder("Blood", fount, p + new Vector3(0f, 0.97f * size, 0f), new Vector3(0.8f * size, 0.02f, 0.8f * size), bloodGlow, collider: false);
                    FxKit.Drips(fount, p + new Vector3(0f, 1.4f * size, 0f), blood);
                    FxKit.PointLight(fount, "Glow", p + new Vector3(0f, 1.3f * size, 0f), new Color(0.8f, 0.08f, 0.1f), 0.6f, 2.5f, flicker: 0.08f);
                }
        }
    }
}
