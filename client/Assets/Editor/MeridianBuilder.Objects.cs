using System.Collections.Generic;
using System.Linq;
using Rth.MapTools;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Phase 3 of the Meridian redesign: what there is to do outdoors. The quay's crane and
    /// fishing spots, the Observers' Walk along the Crag's edge with its view points, the
    /// gathering spots (ironstone and frostiron on the Heights, pale pines on the slope, herbs in
    /// the Physic Garden), and the three skill shortcuts (brief 7.4): rubble on the Ore Road
    /// (Mining), the thicket from the pine slope to the Crag (Woodcutting) and the ford over the
    /// Cut (Fishing). Rooms are furnished in MeridianBuilder.Buildings.cs. The gathering spots are
    /// objects with a message until their skills exist; giving their type a <c>node</c> in
    /// objects.json makes them work.
    /// </summary>
    public static partial class MeridianBuilder
    {
        /// <summary>The "something every 15 tiles" rule: no walkable tile further than this from something to do.</summary>
        private const int InterestReach = 8;

        private static void BuildOutdoors(Transform map)
        {
            BuildQuay(WorldKit.Group(map, "The Strand"));
            BuildObserversWalk(WorldKit.Group(map, "The Observers' Walk"));
            BuildNodes(WorldKit.Group(map, "Gathering"));
            BuildLandmarks(WorldKit.Group(map, "Landmarks"));
            BuildShortcuts(WorldKit.Group(map, "Shortcuts"));
        }

        private static void BuildQuay(Transform g)
        {
            Transform crane = Place(g, "Quay crane", "meridian_crane", 112, 16, M.Iron, 1.2f);
            WorldKit.Prop("Post", crane, On(112.5f, 16.5f, 3f), new Vector3(0.5f, 6f, 0.5f), M.Timber);
            WorldKit.Beam("Jib", crane, On(112.5f, 16.5f, 5.6f), On(112.5f, 11.5f, 4.2f), 0.3f, 0.3f, M.Timber);
            Tag(crane, "quay_crane", "A timber jib crane on an iron turntable at the quay's edge, a hook on a chain, the safe load chalked on the arm.");

            foreach ((int x, int z) in new[] { (126, 9), (133, 11), (172, 14), (186, 14) })
                Spot(g, "Shoal", "meridian_sea_spot", x, z);
            Spot(g, "Pool", "cut_pool", 47, 14);

            // Berth posts along the quay's edge, the west quay's too, and a milestone where the road east begins.
            foreach (int x in new[] { 14, 28, 66, 80, 94, 108, 120, 142, 156 })
                Tag(Place(g, "Berth post", "berth_post", x, Sea, M.Granite, 0.9f), "berth_post", "A squat granite berth post with an iron mooring ring and a painted number.");
            Tag(Place(g, "Milestone", "pass_milestone", 180, 30, M.Granite, 1.1f), "milestone", "A granite milestone, the distances cut and painted black.");
        }

        /// <summary>A fishing spot on the quay's edge: a low, pale ripple marker standing on the tile.</summary>
        private static void Spot(Transform g, string name, string type, int x, int z)
        {
            Transform t = WorldKit.Station(g, name, type, X(x), Z(z));
            t.position = On(x + 0.5f, z + 0.5f);
            WorldKit.Box("Ripple", t, On(x + 0.5f, z + 0.5f, 0.05f), new Vector3(0.9f, 0.1f, 0.9f), M.Foam);
            Tag(t, "fishing_spot", "A fishing spot: a ripple on the water at the quay's edge, a few gulls. Becomes a gathering node with Fishing.");
        }

        /// <summary>
        /// The Observers' Walk: a paved belvedere along the Crag's south edge east of the Crag
        /// stair, brass view plates on the parapet, a bench and the pocket orrery; at its east end
        /// the thicket down to the pine slope, and round the corner the perch over the pass. A
        /// second view plate stands west of the funicular, over the harbour.
        /// </summary>
        private static void BuildObserversWalk(Transform g)
        {
            Tag(Ground.Patch("Observers' Walk", g, X(121), Z(136), X(150), Z(140), M.Paving).transform, "observers_walk",
                "The Observers' Walk: a paved belvedere on the Crag's lip, its parapet lower than elsewhere so the view is clear.");
            foreach ((string type, int x, int z) in new[] { ("walk_view_academy", 126, 136), ("walk_view_forum", 142, 136), ("walk_view_harbour", 86, 136), ("walk_view_gorge", 60, 166) })
                Tag(Place(g, "View point", type, x, z, M.Brass, 1.1f), "view_plate", "A view plate: a brass plate on a short granite post at the parapet, the skyline engraved on it with every building named.");
            Use(Block(g, "Bench", 131, 139, 134, 140, 0.5f, M.Granite), "walk_bench");
            Transform twin = WorldKit.Armillary(g, On(137.5f, 139.5f), 0.6f, M.Brass, M.Granite, M.Brass, M.MoonGlow);
            Use(Block(twin, "Post", 137, 139, 138, 140, 1f, M.Granite), "orrery_twin");
            Tag(twin, "pocket_orrery", "The pocket orrery: a small brass armillary on a granite post, twin of the Little Orrery, its green moon cracked.");
            Place(g, "Perch", "pass_perch", 149, 152, M.Rock, 0.5f);
            Tag(Place(g, "Waymark", "crag_waymark", 113, 148, M.Granite, 1.4f), "waymark", "A granite waymark, arrows and names cut in each face.");
            Tag(Place(g, "Cairn", "knoll_cairn", 112, 174, M.Granite, 1.2f), "surveyors_cairn", "A cairn of squared, identical stones, each cut with a surveyor's name and year.");
        }

        private static void BuildNodes(Transform g)
        {
            // Ironstone along the Heights' gorge edge, frostiron at the foot of the mountain.
            foreach ((int x, int z) in new[] { (44, 82), (46, 87), (42, 91), (46, 95) })
                Tag(Rock(g, "Ironstone", "cutting_rock", x, z, M.Bark), "ironstone_rock", "An ironstone boulder: rough grey rock streaked rust-brown. A mining node once Mining exists.");
            foreach ((int x, int z) in new[] { (38, 138), (41, 136), (44, 138) })
                Tag(Rock(g, "Frostiron seam", "frostiron_seam", x, z, M.Snow), "frostiron_rock", "A frostiron seam: dark rock with a blue-white vein, rimed with frost. A high-level mining node.");
        }

        private static Transform Rock(Transform g, string name, string type, int x, int z, Material streak)
        {
            Transform t = WorldKit.Station(g, name, type, X(x), Z(z));
            t.position = On(x + 0.5f, z + 0.5f);
            // The collider stays square to the grid so it blocks only its own tile; the look turns.
            WorldKit.Box("Boulder", t, On(x + 0.5f, z + 0.5f, 0.55f), new Vector3(0.8f, 1.1f, 0.8f), M.Rock);
            WorldKit.Prop("Shoulder", t, On(x + 0.5f, z + 0.5f, 0.45f), new Vector3(0.95f, 0.9f, 0.6f), M.Rock, x * 37f);
            WorldKit.Prop("Streak", t, On(x + 0.5f, z + 0.5f, 0.7f), new Vector3(0.6f, 0.25f, 0.82f), streak, x * 37f);
            return t;
        }

        /// <summary>
        /// Things to read or look at on the open ground between the buildings, so no route goes
        /// long without one: the Span's plaques, the stairs' landings, the Academy lawn's statue,
        /// the Under-Treaty stone on the Heights, the Butts' scoreboard and the Crag's weather posts.
        /// </summary>
        private static void BuildLandmarks(Transform g)
        {
            foreach (int x in new[] { 60, 47 })
                Tag(Place(g, "Span plaque", "span_plaque", x, 107, M.Brass, 1.1f), "span_plaque", "A brass plaque on a granite post at the end of the Span, the dimensions in Concordian and a much longer dwarven text.");
            Tag(Place(g, "Scoreboard", "butts_scoreboard", 184, 101, M.Slate, 2f), "butts_scoreboard", "A slate scoreboard on two posts, six lanes ruled in white.");
            Tag(Place(g, "Drinking fountain", "stair_fountain", 90, 40, M.Granite, 1f), "drinking_fountain", "A granite drinking fountain: a bowl on a pedestal with a brass spout and two brass plates.");
            Tag(Place(g, "Top landing", "line_street_view", 91, 114, M.Brass, 1.1f), "view_plate", "A view plate: a brass plate on a short granite post at the parapet, the skyline engraved on it with every building named.");
            Tag(Place(g, "Rail telescope", "arcade_telescope", 145, 80, M.Brass, 1.3f), "rail_telescope", "A brass telescope on a swivel, bolted to the rail, with a coin slot that has been soldered shut.");
            Transform statue = Place(g, "Statue", "rationalist_statue", 86, 112, M.Granite, 1.2f);
            WorldKit.Prop("Figure", statue, On(86.5f, 112.5f, 2.6f), new Vector3(0.7f, 2.2f, 0.5f), M.Ashlar);
            Tag(statue, "rationalist_statue", "The first Rationalist: a robed figure with a set square on a plain granite plinth, the face left uncut.");
            Transform stele = Place(g, "Treaty stone", "under_treaty_stele", 36, 118, M.DwarfStone, 3.2f);
            Tag(stele, "under_treaty_stele", "The Under-Treaty stone: a tall slab of dark stone, the treaty cut in Concordian and dwarven side by side, bronze-banded.");
            foreach ((int x, int z) in new[] { (72, 164), (86, 152), (104, 157), (128, 152) })
                Tag(Place(g, "Weather post", "crag_instrument", x, z, M.Iron, 2.4f), "weather_post", "A weather post: an iron pole with a wind vane, a rain gauge and a thermometer in a louvred box.");
        }

        /// <summary>
        /// The skill shortcuts: a climb object at each end, its landing on the far side, gated by
        /// the level in objects.json. A pair is two objects, as with stairs.
        /// </summary>
        private static void BuildShortcuts(Transform g)
        {
            // Rubble from the Ore Road's first bend (+5) up past the last leg to the Heights (+20).
            Transform rubble = WorldKit.Group(g, "Ore Road rubble");
            Shortcut(rubble, "Rubble", "ore_rubble", (46, 71), (46, 80), (46, 79), (46, 70), M.Rock);
            WorldKit.Beam("Scree", rubble, On(46.5f, 71.5f, 0.3f), On(46.5f, 78.5f, 0.3f), 1.2f, 0.6f, M.Rock);
            Tag(rubble, "ore_road_rubble", "A slope of fallen rock and scree up the cliff between the Ore Road's bends, a few iron hand-holds driven in.");

            // The thicket from the pine slope up to the Observers' Walk's east end.
            Transform thicket = WorldKit.Group(g, "Pine thicket");
            Shortcut(thicket, "Thicket", "pine_thicket", (151, 137), (148, 137), (149, 138), (151, 138), M.Pine);
            Tag(thicket, "pine_thicket", "A dense tangle of young pine and scrub on the steep bank between the pine slope and the Crag.");

            // The ford: stepping stones over the Cut between the falls, Heights (+20) to the Crag (+24).
            Transform ford = WorldKit.Group(g, "The ford");
            Shortcut(ford, "Stepping stones", "cut_ford", (47, 137), (60, 137), (60, 138), (46, 138), M.Granite);
            for (int x = GorgeX0; x < GorgeX1; x += 2)
                WorldKit.Prop("Stepping stone", ford, P(x + 1f, 13f, 137.5f + (x % 4 == 0 ? 0.3f : -0.3f)), new Vector3(1.2f, 0.6f, 1.1f), M.Granite, x * 23f);
            Tag(ford, "cut_ford", "Stepping stones over the Cut between the falls: flat-topped granite blocks, wet and mossy, white water round them.");
        }

        /// <summary>One shortcut: an object at <paramref name="a"/> landing at <paramref name="aTo"/>, and its way back from <paramref name="b"/> to <paramref name="bTo"/>.</summary>
        private static void Shortcut(Transform parent, string name, string type, (int x, int z) a, (int x, int z) aTo, (int x, int z) b, (int x, int z) bTo, Material look)
        {
            foreach (((int x, int z) at, (int x, int z) to) in new[] { (a, aTo), (b, bTo) })
            {
                Transform t = Place(parent, name, type, at.x, at.z, look, 1f);
                var landing = new GameObject("Landing");
                landing.transform.SetParent(t, false);
                landing.transform.position = On(to.x + 0.5f, to.z + 0.5f);
                t.GetComponent<MapObject>().climbTo = landing.transform;
            }
        }

        /// <summary>
        /// Warns about ground (the ground floor; upper floors are small rooms) that is more than
        /// <see cref="InterestReach"/> tiles from any object worth using, so a walk never goes 15
        /// tiles without something to stop for. Stairs and ladders don't count. Logs where the gaps
        /// are, by 16-tile block.
        /// </summary>
        private static void CheckInterest(Transform map)
        {
            var points = new List<Vector2Int>();
            foreach (MapObject o in map.GetComponentsInChildren<MapObject>(true))
            {
                if (o.objectType.StartsWith("stairs_") || o.objectType.StartsWith("ladder_")) continue;
                MapLevel level = o.GetComponentInParent<MapLevel>(true);
                if (level != null && level.level != 0) continue;
                points.Add(o.Tile);
            }

            int ground = 0;
            var gaps = new Dictionary<Vector2Int, int>();
            for (int x = OX; x < OX + Size.x; x++)
                for (int z = OZ; z < OZ + Size.y; z++)
                {
                    if (Ground.Height(x, z) == null) continue;
                    ground++;
                    bool near = points.Any(p => Mathf.Abs(p.x - x) <= InterestReach && Mathf.Abs(p.y - z) <= InterestReach);
                    if (near) continue;
                    var block = new Vector2Int((x - OX) / 16 * 16, (z - OZ) / 16 * 16);
                    gaps[block] = gaps.TryGetValue(block, out int n) ? n + 1 : 1;
                }

            int far = gaps.Values.Sum();
            Debug.Log($"[R'th] Meridian: {points.Count} things to use on the ground; {far} of {ground} ground tiles ({100f * far / ground:0.0}%) are more than {InterestReach} tiles from one.");
            foreach (KeyValuePair<Vector2Int, int> gap in gaps.OrderByDescending(p => p.Value).Take(10))
                Debug.LogWarning($"[R'th] Meridian: {gap.Value} tiles with nothing near in the block at local ({gap.Key.x}, {gap.Key.y}).");
        }
    }
}
