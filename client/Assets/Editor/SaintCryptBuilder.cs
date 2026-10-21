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
        private static readonly Vector2Int Origin = new Vector2Int(1016, 1008);
        private static readonly Vector2Int Size = new Vector2Int(56, 56);

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
            (new Vector2Int(2, 27), "wound"), (new Vector2Int(5, 27), "chalice"), (new Vector2Int(12, 27), "sun"), (new Vector2Int(15, 27), "crown"),
        };
        // The walls stand this far out from the playable nave on each side; the band between is floor
        // a step down, with the piers against the walls so they stay out of the camera's way.
        private const float Margin = 4.5f;
        // The grand stair down into the nave, south of it: this many tiles long. Scenery only.
        private const int StairLen = 12;
        // Walls, piers and arches fade whenever they'd hide anything this close to you: a lot happens down here.
        private const float Guard = 5f;

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
            Material coals = FxKit.Glow("BrazierCoals", new Color(1f, 0.35f, 0.08f), 0.9f);
            Material flame = FxKit.ParticleMat("FxFlame", new Color(1f, 0.55f, 0.22f, 0.6f), additive: true);
            Material ember = FxKit.ParticleMat("FxEmber", new Color(1f, 0.55f, 0.2f, 1f), additive: true);
            Material mist = FxKit.ParticleMat("FxMist", new Color(0.32f, 0.25f, 0.26f, 0.12f), additive: false);
            Material mote = FxKit.ParticleMat("FxMote", new Color(0.8f, 0.85f, 1f, 0.6f), additive: true);
            Material blood = FxKit.ParticleMat("FxBlood", new Color(0.85f, 0.06f, 0.08f, 1f), additive: false);
            Material bloodShine = FxKit.ParticleMat("FxBloodShine", new Color(0.9f, 0.08f, 0.1f, 0.35f), additive: true);
            Material holyInlay = FxKit.Glow("HolyInlay", new Color(1f, 0.88f, 0.62f), 0.7f);
            Material halo = FxKit.ParticleMat("FxHalo", new Color(1f, 1f, 1f, 0.2f), additive: true);
            Material spark = FxKit.ParticleMat("FxSpark", Color.white, additive: true);
            Material sigilInlay = FxKit.Glow("SigilInlay", new Color(0.85f, 0.72f, 0.48f), 0.22f);
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

            BuildNave(map, floor, flag, ring, sigil, darkStone, bronze, sigilInlay);
            BuildArcades(map, stone, darkStone, banner, iron, coals, flame, ember);
            BuildEntrance(map, darkStone, dais, iron, coals, flame, ember);
            BuildDais(map, dais, stone, darkStone, synodRed, bronze, bone, wax, holyInlay, halo, spark, mote, channelGlow);
            BuildColossus(map, darkStone, stone, robe, bone, bronze, eyes, blood, bloodShine, iron, coals, flame, ember);
            BuildCentre(map, dais, darkStone, synodRed, bronze, wax, channelGlow, beam, mote);
            BuildFounts(map, stone, bronze, bloodGlow, blood);

            // Mist over the whole nave, and post-processing so glows glow and the edges close in.
            Transform fx = WorldKit.Group(map, "Atmosphere");
            FxKit.Mist(fx, new Vector3(Mid, 0.35f, NaveY + NaveL / 2f), new Vector2(NaveW + 6f, NaveL + 4f), mist, Color.white);
            FxKit.Volume(fx, "SaintsCryptVolume", new Vector3(Mid, 15f, NaveY + NaveL / 2f + 4f), new Vector3(70f, 40f, 62f), bloom: 1.4f, vignette: 0.42f, saturation: -12f);

            WorldKit.SaveRegion(area, ScenePath);
        }

        // ------------------------------------------------------------------ the nave floor

        private static void BuildNave(Transform map, Material floor, Material flag, Material ring, Material sigil, Material darkStone, Material bronze, Material sigilInlay)
        {
            Transform nave = WorldKit.Group(map, "Nave");
            // The one walkable surface (it has the collider).
            WorldKit.Patch("Floor", nave, NaveX, NaveY, NaveX + NaveW, NaveY + NaveL, floor);
            // A flagstone aisle up the middle, the way Believer's projectiles come.
            NoCollider(WorldKit.Patch("Aisle", nave, NaveX + 7, NaveY, NaveX + 11, NaveY + DaisFrom, flag, 0.006f));
            // The side floors, a step down and out of reach, running to the walls.
            foreach (float x in new[] { NaveX - Margin / 2f, NaveX + NaveW + Margin / 2f })
                WorldKit.Box("Side floor", nave, new Vector3(x, -0.25f, NaveY + NaveL / 2f + 2f), new Vector3(Margin, 0.1f, NaveL + 8f), flag, collider: false);
            // The floor of the stair hall, south of the nave.
            WorldKit.Box("Stair hall floor", nave, new Vector3(Mid, -0.25f, NaveY - StairLen / 2f), new Vector3(NaveW + 2f * Margin, 0.1f, StairLen), flag, collider: false);
            // A kerb along each long edge, where the nave steps down to them.
            foreach (float x in new[] { NaveX - 0.1f, NaveX + NaveW + 0.1f })
                WorldKit.Box("Kerb", nave, new Vector3(x, -0.08f, NaveY + NaveL / 2f), new Vector3(0.25f, 0.3f, NaveL), darkStone, collider: false);

            // The safe sigils: placed objects you stand on (no blocker), so the encounter can light
            // them. A dark engraved disc in a bronze ring, with a faintly glowing rune of a drop.
            Transform sigils = WorldKit.Group(map, "Safe sigils");
            foreach (Vector2Int s in Sigils)
            {
                Vector2Int t = At(s);
                var go = new GameObject("Sigil");
                go.transform.SetParent(sigils, false);
                go.transform.position = Center(s);
                go.AddComponent<MapObject>().objectType = "crypt_sigil";
                Vector3 c = Center(s);
                WorldKit.Cylinder("Engraving", go.transform, c + new Vector3(0f, 0.01f, 0f), new Vector3(0.94f, 0.004f, 0.94f), darkStone, collider: false);
                Ring(go.transform, c + new Vector3(0f, 0.016f, 0f), 0.43f, 16, new Vector3(0.12f, 0.012f, 0.05f), bronze);
                // The rune: a drop (a rotated square and its tip) inside a cross of four short strokes.
                WorldKit.Box("Inlay", go.transform, c + new Vector3(0f, 0.02f, -0.04f), new Vector3(0.2f, 0.006f, 0.2f), sigilInlay, collider: false, yaw: 45f);
                WorldKit.Box("Inlay", go.transform, c + new Vector3(0f, 0.02f, 0.12f), new Vector3(0.1f, 0.006f, 0.1f), sigilInlay, collider: false, yaw: 45f);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI / 2f;
                    WorldKit.Box("Inlay", go.transform, c + new Vector3(Mathf.Cos(a) * 0.3f, 0.02f, Mathf.Sin(a) * 0.3f), new Vector3(0.05f, 0.006f, 0.14f), sigilInlay, collider: false, yaw: -i * 90f + 90f);
                }
            }
            // Where Let there be light rises around each fount: a circle of bronze studs set in the floor.
            foreach (Vector2Int f in NearFounts) LightRing(nave, Center(f), ring, darkStone);
            foreach (Vector2Int f in FarFounts) LightRing(nave, Center(f), ring, darkStone);
        }

        private static void LightRing(Transform parent, Vector3 centre, Material stud, Material inlaid)
        {
            Transform ring = WorldKit.Group(parent, "Light ring");
            WorldKit.Cylinder("Inlaid floor", ring, centre + new Vector3(0f, 0.004f, 0f), new Vector3(2.9f, 0.002f, 2.9f), inlaid, collider: false);
            Ring(ring, centre + new Vector3(0f, 0.012f, 0f), 1.45f, 24, new Vector3(0.12f, 0.02f, 0.12f), stud);
        }

        /// <summary>Small boxes in a flat circle on the floor, each turned to follow it.</summary>
        private static void Ring(Transform parent, Vector3 centre, float radius, int count, Vector3 size, Material material)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                WorldKit.Box("Ring", parent, centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius), size, material, collider: false, yaw: -a * Mathf.Rad2Deg + 90f);
            }
        }

        private static void NoCollider(GameObject go) => Object.DestroyImmediate(go.GetComponent<Collider>());

        // ------------------------------------------------------------------ piers, arches, banners, braziers

        private static void BuildArcades(Transform map, Material stone, Material darkStone, Material banner, Material iron, Material coals, Material flame, Material ember)
        {
            Transform arcades = WorldKit.Group(map, "Arcades");
            for (int side = -1; side <= 1; side += 2)
            {
                float wall = side < 0 ? NaveX - Margin : NaveX + NaveW + Margin; // the wall's face
                float x = wall - side * 0.55f; // piers stand half out of it
                for (int i = 0; i < 8; i++)
                {
                    float y = NaveY + 0.5f + i * 4f;
                    // One pier: a plinth, a shaft far taller than anyone, and a capital.
                    Transform pier = WorldKit.Group(arcades, "Pier");
                    WorldKit.Box("Plinth", pier, new Vector3(x, 0.4f, y), new Vector3(1.9f, 0.8f, 2.1f), darkStone, collider: false);
                    WorldKit.Box("Shaft", pier, new Vector3(x, PierHeight / 2f, y), new Vector3(1.3f, PierHeight, 1.5f), stone, collider: false);
                    WorldKit.Box("Capital", pier, new Vector3(x, PierHeight + 0.3f, y), new Vector3(1.8f, 0.6f, 2.1f), darkStone, collider: false);
                    WorldKit.Fades(pier, 0.05f).guardRadius = Guard;
                    if (i == 7) continue;
                    // Blind arches between the piers, stepped in to suggest a point, high in the dark.
                    Transform arch = WorldKit.Group(arcades, "Arch");
                    for (int step = 0; step < 4; step++)
                        WorldKit.Box("Voussoir", arch, new Vector3(x, PierHeight + 0.6f + step * 0.7f, y + 2f), new Vector3(1.1f, 0.7f, 4f - step * 0.9f), stone, collider: false);
                    WorldKit.Fades(arch, 0.05f).guardRadius = Guard;
                    // Between each pair: a long red banner high on the wall, and a sconce's fire below it.
                    WorldKit.Box("Banner", arcades, new Vector3(wall - side * 0.06f, PierHeight - 5f, y + 2f), new Vector3(0.05f, 7f, 1.5f), banner, collider: false);
                    Sconce(arcades, new Vector3(wall - side * 0.35f, 2.6f, y + 2f), side, iron, coals, flame, ember);
                }
                // The wall itself, taller still, and blind, running back along the stair hall too.
                // It fades almost away whenever it stands between the camera and you.
                float from = NaveY - StairLen - 0.5f, to = NaveY + NaveL + 8f;
                Transform wallGroup = WorldKit.Group(arcades, "Side wall");
                WorldKit.Box("Wall", wallGroup, new Vector3(wall + side * 0.5f, 10f, (from + to) / 2f), new Vector3(1f, 20f, to - from), darkStone, collider: false);
                WorldKit.Fades(wallGroup, 0.06f).guardRadius = Guard;
            }
        }

        /// <summary>A fire in an iron bowl on a bracket from the wall, facing into the nave.</summary>
        private static void Sconce(Transform parent, Vector3 at, int side, Material iron, Material coals, Material flame, Material ember)
        {
            Transform s = WorldKit.Group(parent, "Sconce");
            WorldKit.Box("Bracket", s, at + new Vector3(side * 0.25f, -0.25f, 0f), new Vector3(0.6f, 0.1f, 0.12f), iron, collider: false);
            WorldKit.Cylinder("Bowl", s, at, new Vector3(0.55f, 0.1f, 0.55f), iron, collider: false);
            WorldKit.Cylinder("Coals", s, at + new Vector3(0f, 0.1f, 0f), new Vector3(0.42f, 0.02f, 0.42f), coals, collider: false);
            FxKit.Fire(s, at + new Vector3(0f, 0.1f, 0f), 0.7f, flame);
            FxKit.Embers(s, at + new Vector3(0f, 0.25f, 0f), 5f, ember);
            FxKit.PointLight(s, "Firelight", at + new Vector3(-side * 0.6f, 0.6f, 0f), new Color(1f, 0.5f, 0.2f), 4.5f, 8f, flicker: 0.35f);
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
            // The grand stair: a wide flight climbing south from the foot of the nave to a high, far
            // door, in two runs with a landing between, a balustrade either side, kneeling stone
            // penitents on the landing and pale light falling down it from the door.
            Transform stairGroup = WorldKit.Group(map, "Grand stair");
            const float rise = 0.3f, tread = 0.5f, width = 7f;
            const int run = 10;
            float z = NaveY - 0.25f, h = 0f;
            float landingZ = 0f, landingH = 0f;
            for (int flight = 0; flight < 2; flight++)
            {
                for (int i = 0; i < run; i++)
                {
                    h += rise;
                    z -= tread;
                    WorldKit.Box("Step", stairGroup, new Vector3(Mid, h / 2f, z), new Vector3(width, h, tread + 0.02f), dais, collider: false);
                    foreach (int side in new[] { -1, 1 }) // the balustrade rises with the steps
                        WorldKit.Box("Balustrade", stairGroup, new Vector3(Mid + side * (width / 2f + 0.25f), h + 0.45f, z), new Vector3(0.5f, 0.9f + rise, tread + 0.02f), darkStone, collider: false);
                }
                if (flight == 1) break;
                // The landing.
                z -= 1f;
                landingZ = z + 0.25f;
                landingH = h;
                WorldKit.Box("Landing", stairGroup, new Vector3(Mid, h / 2f, landingZ), new Vector3(width + 4f, h, 2f), dais, collider: false);
                foreach (int side in new[] { -1, 1 })
                {
                    Penitent(stairGroup, new Vector3(Mid + side * (width / 2f + 1.2f), h, landingZ), side, darkStone);
                    Brazier(stairGroup, new Vector3(Mid + side * (width / 2f - 0.6f), h, landingZ), 1.2f, iron, coals, flame, ember);
                }
            }
            float top = z - tread / 2f;
            // The door at the top, tall and narrow, with cold light behind it falling down the stair.
            Transform wallGroup = WorldKit.Group(map, "South wall");
            float y = top - 0.5f, west = NaveX - Margin - 1f, east = NaveX + NaveW + Margin + 1f;
            WorldKit.Box("South wall W", wallGroup, new Vector3((west + Mid - 1.4f) / 2f, 11f, y), new Vector3(Mid - 1.4f - west, 22f, 1f), darkStone, collider: false);
            WorldKit.Box("South wall E", wallGroup, new Vector3((Mid + 1.4f + east) / 2f, 11f, y), new Vector3(east - Mid - 1.4f, 22f, 1f), darkStone, collider: false);
            WorldKit.Box("Above the door", wallGroup, new Vector3(Mid, h + 4.5f + (22f - h - 4.5f) / 2f, y), new Vector3(2.8f, 22f - h - 4.5f, 1f), darkStone, collider: false);
            WorldKit.Box("Below the door", wallGroup, new Vector3(Mid, h / 2f, y), new Vector3(2.8f, h, 1f), darkStone, collider: false);
            WorldKit.Fades(wallGroup, 0.05f).guardRadius = Guard;
            Renderer doorLight = FxKit.Halo(stairGroup, new Vector3(Mid, h + 2.2f, y + 0.4f), 3f, Door());
            doorLight.transform.localScale = new Vector3(2.6f, 4.5f, 1f);
            var fall = new GameObject("Light from the door");
            fall.transform.SetParent(stairGroup, false);
            fall.transform.position = new Vector3(Mid, h + 3.5f, y + 0.6f);
            fall.transform.LookAt(new Vector3(Mid, 0f, NaveY + 1f));
            var light = fall.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(0.7f, 0.78f, 1f);
            light.intensity = 25f;
            light.range = StairLen + 12f;
            light.spotAngle = 40f;
            light.shadows = LightShadows.None;

            // The way out, at the foot of the stair (the walkable floor ends here).
            Transform stair = WorldKit.Station(stairGroup, "Stair", "crypt_stair", NaveX + 8, NaveY - 1);
            WorldKit.Box("Threshold", stair, new Vector3(Mid - 0.5f, 0.05f, NaveY - 0.5f), new Vector3(0.9f, 0.1f, 0.9f), dais); // the blocker's collider
            // Two braziers at the foot, the first fires you pass coming down.
            Transform footFires = WorldKit.Group(map, "Foot braziers");
            Brazier(footFires, new Vector3(Mid - 2.5f, 0f, NaveY + 0.5f), 1.4f, iron, coals, flame, ember);
            Brazier(footFires, new Vector3(Mid + 2.5f, 0f, NaveY + 0.5f), 1.4f, iron, coals, flame, ember);
        }

        /// <summary>A stone penitent kneeling on a pedestal, head bowed towards the nave.</summary>
        private static void Penitent(Transform parent, Vector3 at, int side, Material stone)
        {
            Transform p = WorldKit.Group(parent, "Penitent");
            WorldKit.Box("Pedestal", p, at + new Vector3(0f, 0.4f, 0f), new Vector3(1.3f, 0.8f, 1.3f), stone, collider: false);
            Vector3 b = at + new Vector3(0f, 0.8f, 0f);
            WorldKit.Box("Shin", p, b + new Vector3(0f, 0.15f, 0.2f), new Vector3(0.45f, 0.3f, 0.8f), stone, collider: false);
            WorldKit.Box("Thigh", p, b + new Vector3(0f, 0.55f, -0.05f), new Vector3(0.5f, 0.6f, 0.4f), stone, collider: false);
            GameObject body = WorldKit.Box("Body", p, b + new Vector3(0f, 1.2f, 0.1f), new Vector3(0.6f, 0.8f, 0.4f), stone, collider: false);
            body.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            GameObject head = WorldKit.Box("Head", p, b + new Vector3(0f, 1.72f, 0.3f), new Vector3(0.32f, 0.36f, 0.34f), stone, collider: false);
            head.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            p.RotateAround(at, Vector3.up, side * -25f); // turned a little towards the stair
        }

        private static Material Door() => FxKit.ParticleMat("FxDoorLight", new Color(0.75f, 0.82f, 1f, 0.55f), additive: true);

        // ------------------------------------------------------------------ the dais: the martyr, the symbols

        private static void BuildDais(Transform map, Material dais, Material stone, Material darkStone, Material synodRed, Material bronze, Material bone, Material wax,
            Material holyInlay, Material halo, Material spark, Material mote, Material bloodPool)
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
            // Where the chalice's stream lands: a pool of blood on the pall, catching the light.
            WorldKit.Cylinder("Pool", saint, c + new Vector3(0.1f, 1.06f, 0f), new Vector3(1.1f, 0.01f, 0.8f), bloodPool, collider: false);
            // Candles crowding the bier.
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Vector3 at = c + new Vector3(Mathf.Cos(a) * 1.6f, 0f, Mathf.Sin(a) * 1.1f);
                float h = 0.3f + (i % 3) * 0.15f;
                WorldKit.Cylinder("Candle", group, at + new Vector3(0f, 0.12f + h / 2f, 0f), new Vector3(0.1f, h / 2f, 0.1f), wax, collider: false);
            }
            FxKit.PointLight(group, "Candlelight", c + new Vector3(0f, 1.5f, -0.6f), new Color(1f, 0.7f, 0.4f), 3f, 5f, flicker: 0.15f);

            // The holy symbols high on four pillars either side: a faint holy glow at rest, flaring
            // in combat styles' colours for Are you a believer? (HolySymbol, driven by the server).
            foreach ((Vector2Int tile, string shape) in Pillars)
                SymbolPillar(group, At(tile), shape, stone, darkStone, bronze, holyInlay, halo, spark, mote);
        }

        private static void SymbolPillar(Transform parent, Vector2Int t, string shape, Material stone, Material darkStone, Material bronze,
            Material inlay, Material halo, Material spark, Material mote)
        {
            Transform pillar = WorldKit.Station(parent, "Holy symbol (" + shape + ")", "holy_symbol", t.x, t.y);
            var p = new Vector3(t.x + 0.5f, 0f, t.y + 0.5f);
            // A stepped base (its collider blocks the tile), a fluted shaft, a capital with a bracket.
            WorldKit.Box("Base", pillar, p + new Vector3(0f, 0.3f, 0f), new Vector3(0.96f, 0.6f, 0.96f), darkStone);
            WorldKit.Box("Plinth", pillar, p + new Vector3(0f, 0.12f, 0f), new Vector3(1.35f, 0.24f, 1.35f), darkStone, collider: false);
            WorldKit.Cylinder("Shaft", pillar, p + new Vector3(0f, 2.1f, 0f), new Vector3(0.62f, 1.7f, 0.62f), stone, collider: false);
            for (int f = 0; f < 8; f++)
            {
                float a = f / 8f * Mathf.PI * 2f;
                WorldKit.Box("Flute", pillar, p + new Vector3(Mathf.Cos(a) * 0.32f, 2.1f, Mathf.Sin(a) * 0.32f), new Vector3(0.1f, 3.4f, 0.1f), stone, collider: false, yaw: -a * Mathf.Rad2Deg);
            }
            WorldKit.Box("Collar", pillar, p + new Vector3(0f, 3.9f, 0f), new Vector3(0.95f, 0.3f, 0.95f), darkStone, collider: false);
            WorldKit.Box("Capital", pillar, p + new Vector3(0f, 4.25f, 0f), new Vector3(1.2f, 0.4f, 1.2f), stone, collider: false);
            WorldKit.Box("Post", pillar, p + new Vector3(0f, 4.8f, 0f), new Vector3(0.2f, 0.8f, 0.2f), bronze, collider: false);

            // The symbol faces down the nave (south), a bronze frame with a glowing inlay.
            var symbol = new GameObject("Symbol").transform;
            symbol.SetParent(pillar, false);
            symbol.position = p + new Vector3(0f, 5.8f, -0.1f);
            var glowing = new System.Collections.Generic.List<Renderer>();
            void Frame(Vector2 at, Vector2 size, float turn) => Plate(symbol, at, size, 0.12f, 0f, turn, bronze);
            void Inlay(Vector2 at, Vector2 size, float turn) => glowing.Add(Plate(symbol, at, size, 0.06f, -0.08f, turn, inlay).GetComponent<Renderer>());
            switch (shape)
            {
                case "wound": // a lozenge frame round a falling drop
                    Frame(Vector2.zero, new Vector2(1.25f, 1.25f), 45f);
                    Frame(Vector2.zero, new Vector2(0.95f, 0.95f), 45f);
                    Inlay(new Vector2(0f, -0.12f), new Vector2(0.42f, 0.42f), 45f);
                    Inlay(new Vector2(0f, 0.2f), new Vector2(0.22f, 0.22f), 45f);
                    Inlay(new Vector2(0f, 0.38f), new Vector2(0.1f, 0.1f), 45f);
                    break;
                case "chalice": // Cruor's cup inside a ring
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i / 16f * Mathf.PI * 2f;
                        Frame(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.82f, new Vector2(0.34f, 0.14f), a * Mathf.Rad2Deg + 90f);
                    }
                    Inlay(new Vector2(0f, 0.25f), new Vector2(0.72f, 0.3f), 0f);
                    Inlay(new Vector2(0f, 0.06f), new Vector2(0.48f, 0.14f), 0f);
                    Inlay(new Vector2(0f, -0.22f), new Vector2(0.12f, 0.42f), 0f);
                    Inlay(new Vector2(0f, -0.47f), new Vector2(0.5f, 0.1f), 0f);
                    break;
                case "sun": // the Keeping: a disc with eight rays
                    Frame(Vector2.zero, new Vector2(0.95f, 0.95f), 0f);
                    Frame(Vector2.zero, new Vector2(0.95f, 0.95f), 45f);
                    Inlay(Vector2.zero, new Vector2(0.42f, 0.42f), 45f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2f;
                        Inlay(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.82f, new Vector2(0.4f, 0.1f), a * Mathf.Rad2Deg);
                    }
                    break;
                default: // a crown of thorns
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2f;
                        Frame(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.55f, new Vector2(0.3f, 0.12f), a * Mathf.Rad2Deg + 90f);
                        Inlay(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.85f, new Vector2(0.36f, 0.07f), a * Mathf.Rad2Deg);
                    }
                    Inlay(Vector2.zero, new Vector2(0.24f, 0.24f), 45f);
                    break;
            }
            Renderer haloRenderer = FxKit.Halo(symbol, symbol.position + new Vector3(0f, 0f, 0.15f), 3.4f, halo);
            Light light = FxKit.PointLight(symbol, "Glow", symbol.position + new Vector3(0f, 0f, -1.1f), new Color(1f, 0.88f, 0.62f), 1.2f, 5f);
            ParticleSystem burst = FxKit.Burst(symbol, symbol.position + new Vector3(0f, 0f, -0.3f), spark);
            FxKit.Motes(pillar, p + new Vector3(0f, 4.6f, 0f), 0.6f, mote, new Color(1f, 0.9f, 0.7f, 0.7f));

            var holy = pillar.gameObject.AddComponent<HolySymbol>();
            holy.inlay = glowing.ToArray();
            holy.halo = haloRenderer;
            holy.glow = light;
            holy.burst = burst;
        }

        /// <summary>A thin plate in a symbol's plane (facing south), at a 2D position, turned about its centre.</summary>
        private static GameObject Plate(Transform symbol, Vector2 at, Vector2 size, float depth, float forward, float turn, Material material)
        {
            GameObject plate = WorldKit.Box("Plate", symbol, symbol.position + new Vector3(at.x, at.y, forward), new Vector3(size.x, size.y, depth), material, collider: false);
            plate.transform.rotation = Quaternion.Euler(0f, 0f, turn);
            return plate;
        }

        // ------------------------------------------------------------------ the colossus

        private static void BuildColossus(Transform map, Material darkStone, Material stone, Material robe, Material bone, Material bronze, Material eyes, Material blood,
            Material bloodShine, Material iron, Material coals, Material flame, Material ember)
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
            // The blood fountain: the stream, a brighter sheen running down it, and lights that pick
            // it out of the dark from either side and glow where it lands.
            Vector3 lip = chalice + new Vector3(0f, -0.1f, -0.7f);
            FxKit.BloodStream(colossus, lip, 8.2f, blood);
            FxKit.BloodStream(colossus, lip, 8.2f, bloodShine);
            for (int side = -1; side <= 1; side += 2)
            {
                var rim = new GameObject("Stream light");
                rim.transform.SetParent(colossus, false);
                rim.transform.position = lip + new Vector3(side * 5f, -2f, -4f);
                rim.transform.LookAt(lip + new Vector3(0f, -4f, 0f));
                var l = rim.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = new Color(1f, 0.55f, 0.45f);
                l.intensity = 35f;
                l.range = 14f;
                l.spotAngle = 30f;
                l.shadows = LightShadows.None;
            }
            FxKit.PointLight(colossus, "Stream glow", lip + new Vector3(0f, -4f, -0.3f), new Color(1f, 0.1f, 0.12f), 3f, 4f, flicker: 0.15f);
            FxKit.PointLight(colossus, "Pool glow", lip + new Vector3(0f, -7.6f, -0.4f), new Color(1f, 0.12f, 0.12f), 4f, 4.5f, flicker: 0.2f);
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

        private static void BuildCentre(Transform map, Material dais, Material darkStone, Material synodRed, Material bronze, Material wax, Material channelGlow, Material beam, Material mote)
        {
            Transform centre = WorldKit.Group(map, "Altar");
            Vector2Int altar = At(Altar);
            Transform altarStation = WorldKit.Station(centre, "Altar", "crypt_altar", altar.x, altar.y);
            Vector3 a = altarStation.position;
            // A stepped plinth, a carved block (its collider blocks the tile), a heavy top, a red runner
            // hanging over both ends, and the Requiem's bowl with the channel's blood in it.
            WorldKit.Box("Plinth", altarStation, a + new Vector3(0f, 0.08f, 0f), new Vector3(1.6f, 0.16f, 1.5f), darkStone, collider: false);
            WorldKit.Box("Step", altarStation, a + new Vector3(0f, 0.22f, 0f), new Vector3(1.3f, 0.14f, 1.2f), dais, collider: false);
            WorldKit.Box("Block", altarStation, a + new Vector3(0f, 0.6f, 0f), new Vector3(0.96f, 0.8f, 0.9f), dais);
            foreach (float z in new[] { -0.46f, 0.46f })
                WorldKit.Box("Carving", altarStation, a + new Vector3(0f, 0.6f, z), new Vector3(0.66f, 0.46f, 0.04f), darkStone, collider: false);
            WorldKit.Box("Top", altarStation, a + new Vector3(0f, 1.06f, 0f), new Vector3(1.3f, 0.12f, 1.15f), dais, collider: false);
            WorldKit.Box("Runner", altarStation, a + new Vector3(0f, 1.125f, 0f), new Vector3(0.5f, 0.02f, 1.2f), synodRed, collider: false);
            foreach (float z in new[] { -0.6f, 0.6f })
                WorldKit.Box("Runner end", altarStation, a + new Vector3(0f, 0.88f, z), new Vector3(0.5f, 0.5f, 0.03f), synodRed, collider: false);
            WorldKit.Cylinder("Bowl", altarStation, a + new Vector3(0f, 1.2f, 0f), new Vector3(0.6f, 0.07f, 0.6f), bronze, collider: false);
            WorldKit.Cylinder("Requiem", altarStation, a + new Vector3(0f, 1.27f, 0f), new Vector3(0.48f, 0.01f, 0.48f), channelGlow, collider: false);
            foreach (Vector2 corner in new[] { new Vector2(-0.5f, -0.45f), new Vector2(0.5f, -0.45f), new Vector2(-0.5f, 0.45f), new Vector2(0.5f, 0.45f) })
                WorldKit.Cylinder("Candle", altarStation, a + new Vector3(corner.x, 1.25f, corner.y), new Vector3(0.08f, 0.12f, 0.08f), wax, collider: false);
            FxKit.PointLight(centre, "Altar candles", a + new Vector3(0f, 1.8f, -0.5f), new Color(1f, 0.72f, 0.45f), 1.8f, 4f, flicker: 0.12f);
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
