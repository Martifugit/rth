using System.Collections.Generic;
using System.Linq;
using Rth.MapTools;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Buildings you walk into, on any ground height and with up to four floors: an
    /// <see cref="Shell"/> (walls with doors, a walkable floor per storey, a roof that hides while
    /// you are inside), stairs and ladders between its floors, open pavilions on posts, the one
    /// identical row house, and furniture helpers so every room gets something in it without
    /// hand-placing boxes. Every upper floor is a <see cref="MapLevel"/>, following the pattern in
    /// LevelTest.unity: its outer walls always show, its slab and furniture only once you
    /// are up there.
    /// </summary>
    public sealed partial class WorldKit
    {
        /// <summary>Floor to floor in a building you can enter: two storeys make 8 m.</summary>
        public const float FloorHeight = 4f;

        /// <summary>
        /// A building you can enter, as built: where it stands and a group per floor (0 is the
        /// ground floor, on the region's plane; 1 and up are <see cref="MapLevel"/>s), with helpers to
        /// put stairs, ladders and furniture on any floor. Tile arguments are absolute tiles.
        /// </summary>
        public sealed class Shell
        {
            public readonly string Name;
            public readonly Transform Root;
            public readonly RectInt Tiles;
            public readonly float Ground;
            /// <summary>Floor to floor; for a pavilion with a deck, the deck's height.</summary>
            public readonly float Storey;
            /// <summary>Open on posts (a market, a stand) rather than walled.</summary>
            public readonly bool Open;
            private readonly List<Transform> _floors = new List<Transform>();

            internal Shell(string name, Transform root, RectInt tiles, float ground, float storey, bool open)
            {
                Name = name;
                Root = root;
                Tiles = tiles;
                Ground = ground;
                Storey = storey;
                Open = open;
            }

            public int Floors => _floors.Count;

            /// <summary>The group that holds a floor's slab, furniture, objects and spawns.</summary>
            public Transform Floor(int level) => _floors[level];

            internal void AddFloor(Transform t) => _floors.Add(t);

            /// <summary>The height of a floor's surface.</summary>
            public float Y(int level) => Ground + level * Storey;

            public Vector3 At(int level, float x, float z, float lift = 0f) => new Vector3(x, Y(level) + lift, z);

            // -------------------------------------------------------------- getting between floors

            /// <summary>
            /// A straight flight from <paramref name="level"/> to the floor above, its foot on tile
            /// (x, z), climbing towards <paramref name="up"/> over <paramref name="run"/> tiles. The
            /// flight blocks the tiles it covers; you use it from the tile before its foot and arrive
            /// on the tile past its head, where the stair down stands railed round its well.
            /// </summary>
            public void Stairs(int level, int x, int z, Side up, Material wood, Material well, int run = 3)
            {
                Vector2Int d = Step(up);
                Transform flight = ClimbObject(Floor(level), "Stairs up", "stairs_up", At(level, x + 0.5f, z + 0.5f));
                const int steps = 10;
                Vector3 start = new Vector3(x + 0.5f - d.x * 0.5f, 0f, z + 0.5f - d.y * 0.5f);
                for (int i = 0; i < steps; i++)
                {
                    float a = run * (i + 0.5f) / steps;
                    float top = Storey * (i + 1) / steps;
                    Vector3 c = start + new Vector3(d.x, 0f, d.y) * a;
                    Vector3 size = d.x != 0 ? new Vector3(run / (float)steps, top, 0.9f) : new Vector3(0.9f, top, run / (float)steps);
                    Box("Step", flight, new Vector3(c.x, Y(level) + top / 2f, c.z), size, wood);
                }

                // Upstairs: the head of the flight, and the well over the rest of it.
                var head = new Vector2Int(x + d.x * (run - 1), z + d.y * (run - 1));
                Transform down = ClimbObject(Floor(level + 1), "Stairs down", "stairs_down", At(level + 1, head.x + 0.5f, head.y + 0.5f));
                Box("Newel", down, At(level + 1, head.x + 0.5f - d.x * 0.4f, head.y + 0.5f - d.y * 0.4f, 0.5f), new Vector3(0.15f, 1f, 0.15f), wood);
                var wellGo = new GameObject("Stairwell");
                wellGo.transform.SetParent(Floor(level + 1), false);
                wellGo.AddComponent<MapBlocker>();
                int wx0 = Mathf.Min(x, head.x - d.x), wx1 = Mathf.Max(x, head.x - d.x), wz0 = Mathf.Min(z, head.y - d.y), wz1 = Mathf.Max(z, head.y - d.y);
                Box("Well", wellGo.transform, At(level + 1, (wx0 + wx1 + 1) / 2f, (wz0 + wz1 + 1) / 2f, 0.02f), new Vector3(wx1 - wx0 + 0.8f, 0.04f, wz1 - wz0 + 0.8f), well);
                Vector3 across = d.x != 0 ? new Vector3(0f, 0f, 0.45f) : new Vector3(0.45f, 0f, 0f);
                Vector3 a0 = At(level + 1, x + 0.5f - d.x * 0.5f, z + 0.5f - d.y * 0.5f, 0.9f), a1 = At(level + 1, head.x + 0.5f - d.x * 0.5f, head.y + 0.5f - d.y * 0.5f, 0.9f);
                Beam("Rail", wellGo.transform, a0 + across, a1 + across, 0.08f, 0.08f, wood);
                Beam("Rail", wellGo.transform, a0 - across, a1 - across, 0.08f, 0.08f, wood);

                flight.GetComponent<MapObject>().climbTo = Landing(Floor(level + 1), head.x + d.x, head.y + d.y, Y(level + 1));
                down.GetComponent<MapObject>().climbTo = Landing(Floor(level), x - d.x, z - d.y, Y(level));
            }

            /// <summary>
            /// A ladder on tile (x, z) against the wall on <paramref name="wall"/>, from
            /// <paramref name="level"/> to the floor above, coming out through a hatch on the same
            /// tile. You use it, and land, from the tile in front of it on both floors.
            /// </summary>
            public void Ladder(int level, int x, int z, Side wall, Material wood)
            {
                Vector2Int d = Step(wall);
                Transform up = ClimbObject(Floor(level), "Ladder up", "ladder_up", At(level, x + 0.5f, z + 0.5f));
                Vector3 foot = At(level, x + 0.5f + d.x * 0.3f, z + 0.5f + d.y * 0.3f);
                Vector3 across = d.x != 0 ? new Vector3(0f, 0f, 0.25f) : new Vector3(0.25f, 0f, 0f);
                float h = Storey + 1f;
                Box("Rails", up, foot + Vector3.up * (h / 2f), d.x != 0 ? new Vector3(0.12f, h, 0.6f) : new Vector3(0.6f, h, 0.12f), wood);
                for (float r = 0.4f; r < h; r += 0.45f)
                    Prop("Rung", up, foot + Vector3.up * r - Vector3.Scale(new Vector3(d.x, 0f, d.y), new Vector3(0.08f, 0f, 0.08f)), d.x != 0 ? new Vector3(0.06f, 0.06f, 0.5f) : new Vector3(0.5f, 0.06f, 0.06f), wood);
                Prop("Rail", up, foot + across + Vector3.up * (h / 2f), new Vector3(0.07f, h, 0.07f), wood);
                Prop("Rail", up, foot - across + Vector3.up * (h / 2f), new Vector3(0.07f, h, 0.07f), wood);

                Transform down = ClimbObject(Floor(level + 1), "Ladder down", "ladder_down", At(level + 1, x + 0.5f, z + 0.5f));
                Box("Hatch", down, At(level + 1, x + 0.5f, z + 0.5f, 0.03f), new Vector3(0.9f, 0.06f, 0.9f), wood);
                Prop("Ladder top", down, At(level + 1, x + 0.5f + d.x * 0.3f, z + 0.5f + d.y * 0.3f, 0.5f), d.x != 0 ? new Vector3(0.1f, 1f, 0.6f) : new Vector3(0.6f, 1f, 0.1f), wood);

                up.GetComponent<MapObject>().climbTo = Landing(Floor(level + 1), x - d.x, z - d.y, Y(level + 1));
                down.GetComponent<MapObject>().climbTo = Landing(Floor(level), x - d.x, z - d.y, Y(level));
            }

            // -------------------------------------------------------------- furniture

            /// <summary>
            /// A box of furniture over tiles [x0,x1) x [z0,z1) on a floor, standing on it, inset a
            /// little; it blocks its tiles unless <paramref name="blocks"/> is false (then it is only
            /// looked at, and has no collider, so it never becomes ground).
            /// </summary>
            public GameObject Thing(int level, string name, int x0, int z0, int x1, int z1, float height, Material m, bool blocks = true)
            {
                GameObject go = Box(name, Floor(level), At(level, (x0 + x1) / 2f, (z0 + z1) / 2f, height / 2f), new Vector3(x1 - x0 - 0.2f, height, z1 - z0 - 0.2f), m, collider: blocks);
                if (blocks) go.AddComponent<MapBlocker>();
                return go;
            }

            public GameObject Table(int level, int x, int z, int w, int d, Material wood) => Thing(level, "Table", x, z, x + w, z + d, 0.8f, wood);

            public GameObject Counter(int level, int x0, int z0, int x1, int z1, Material wood) => Thing(level, "Counter", x0, z0, x1, z1, 1.1f, wood);

            public GameObject Bed(int level, int x, int z, bool alongX, Material wood) =>
                Thing(level, "Bed", x, z, x + (alongX ? 2 : 1), z + (alongX ? 1 : 2), 0.6f, wood);

            public GameObject Crates(int level, int x, int z, Material wood) => Thing(level, "Crates", x, z, x + 1, z + 1, 1f, wood);

            /// <summary>Benches in a row over tiles [x0,x1) x [z0,z1) (one tile deep), low enough to see over.</summary>
            public GameObject Bench(int level, int x0, int z0, int x1, int z1, Material wood) => Thing(level, "Bench", x0, z0, x1, z1, 0.5f, wood);

            /// <summary>Shelves against the inside of a wall, over the tiles <paramref name="from"/>..<paramref name="to"/> along it (inclusive).</summary>
            public GameObject Shelves(int level, Side wall, int from, int to, Material wood, float height = 2.4f)
            {
                RectInt r = AlongWall(wall, from, to);
                Vector3 c = At(level, r.center.x, r.center.y, height / 2f);
                Vector3 size = wall == Side.South || wall == Side.North ? new Vector3(r.width - 0.1f, height, 0.55f) : new Vector3(0.55f, height, r.height - 0.1f);
                Vector2Int d = Step(wall);
                c += new Vector3(d.x, 0f, d.y) * 0.2f;
                GameObject go = Box("Shelves", Floor(level), c, size, wood);
                go.AddComponent<MapBlocker>();
                return go;
            }

            /// <summary>A hearth against the inside of a wall at tile <paramref name="at"/> along it, with a fire and a chimney breast to the ceiling.</summary>
            public GameObject Hearth(int level, Side wall, int at, Material stone, Material fire)
            {
                RectInt r = AlongWall(wall, at, at + 1);
                Vector2Int d = Step(wall);
                GameObject go = Box("Hearth", Floor(level), At(level, r.center.x, r.center.y, 0.6f) + new Vector3(d.x, 0f, d.y) * 0.15f,
                    wall == Side.South || wall == Side.North ? new Vector3(1.8f, 1.2f, 0.7f) : new Vector3(0.7f, 1.2f, 1.8f), stone);
                go.AddComponent<MapBlocker>();
                Prop("Chimney breast", go.transform, At(level, r.center.x, r.center.y, 1.2f + (Storey - 1.2f) / 2f) + new Vector3(d.x, 0f, d.y) * 0.3f,
                    wall == Side.South || wall == Side.North ? new Vector3(1.4f, Storey - 1.2f, 0.4f) : new Vector3(0.4f, Storey - 1.2f, 1.4f), stone);
                if (fire != null) Prop("Fire", go.transform, At(level, r.center.x, r.center.y, 0.3f) - new Vector3(d.x, 0f, d.y) * 0.1f, new Vector3(0.6f, 0.4f, 0.6f), fire);
                return go;
            }

            /// <summary>
            /// An interactive object (a station, a sign, a case) on a floor: a <see cref="MapObject"/> of
            /// that type with a stand-in box <paramref name="height"/> tall, blocking its tile.
            /// </summary>
            public Transform Object(int level, string name, string type, int x, int z, Material look, float height = 1f)
            {
                Transform t = ClimbObject(Floor(level), name, type, At(level, x + 0.5f, z + 0.5f));
                Box("Stand-in", t, At(level, x + 0.5f, z + 0.5f, height / 2f), new Vector3(0.8f, height, 0.8f), look);
                return t;
            }

            /// <summary>The row of tiles just inside a wall, from..to (inclusive) along it.</summary>
            private RectInt AlongWall(Side wall, int from, int to)
            {
                switch (wall)
                {
                    case Side.South: return new RectInt(from, Tiles.yMin, to - from + 1, 1);
                    case Side.North: return new RectInt(from, Tiles.yMax - 1, to - from + 1, 1);
                    case Side.West: return new RectInt(Tiles.xMin, from, 1, to - from + 1);
                    default: return new RectInt(Tiles.xMax - 1, from, 1, to - from + 1);
                }
            }
        }

        /// <summary>One tile towards a side.</summary>
        public static Vector2Int Step(Side side)
        {
            switch (side)
            {
                case Side.North: return new Vector2Int(0, 1);
                case Side.South: return new Vector2Int(0, -1);
                case Side.East: return new Vector2Int(1, 0);
                default: return new Vector2Int(-1, 0);
            }
        }

        /// <summary>A <see cref="MapObject"/> of a type at a point, blocking whatever colliders go under it.</summary>
        private static Transform ClimbObject(Transform parent, string name, string type, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.AddComponent<MapObject>().objectType = type;
            go.AddComponent<MapBlocker>();
            return go.transform;
        }

        private static Transform Landing(Transform parent, int x, int z, float y)
        {
            var go = new GameObject("Landing");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x + 0.5f, y, z + 0.5f);
            return go.transform;
        }

        private static Transform AddLevel(Transform parent, string name, int level, bool hiddenFromBelow)
        {
            Transform t = Group(parent, name);
            MapLevel l = t.gameObject.AddComponent<MapLevel>();
            l.level = level;
            l.hiddenFromBelow = hiddenFromBelow;
            return t;
        }

        // ------------------------------------------------------------------ the shell

        /// <summary>
        /// A building you can enter over tiles <paramref name="r"/>, standing on ground at
        /// <paramref name="ground"/>, with <paramref name="floors"/> storeys (1-4) of
        /// <see cref="FloorHeight"/>: a floor and walls with the <paramref name="doors"/> on the ground,
        /// and for each storey above a <see cref="MapLevel"/> with its outer walls (always drawn) and
        /// its slab (only drawn once you are up there). With <paramref name="gallery"/> &gt; 0 the
        /// upper floors are a railed gallery that wide round the walls instead of a full floor. Storey
        /// bands, windows and a roof of <paramref name="roof"/> kind that hides while you are inside.
        /// Stairs and furniture are added through the returned <see cref="Shell"/>.
        /// </summary>
        public static Shell Enterable(Transform parent, string name, RectInt r, float ground, int floors, Facade f, Material floor,
            RoofKind roof, int gallery, params Door[] doors)
        {
            int x0 = r.xMin, z0 = r.yMin, x1 = r.xMax, z1 = r.yMax;
            const float h = FloorHeight;
            Transform root = Group(parent, name);
            var shell = new Shell(name, root, r, ground, h, open: false);

            Transform g0 = Group(root, "Ground floor");
            shell.AddFloor(g0);
            Box("Floor", g0, new Vector3((x0 + x1) / 2f, ground + 0.01f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.02f, z1 - z0), floor);
            Transform walls = Group(g0, "Walls");
            ShellWalls(walls, x0, z0, x1, z1, h, f, doors, 0);
            walls.position = Vector3.up * ground;
            Fades(walls, 0.3f);

            for (int n = 1; n < floors; n++)
            {
                Transform outer = AddLevel(root, "Walls, floor " + n, n, hiddenFromBelow: false);
                ShellWalls(outer, x0, z0, x1, z1, h, f, new Door[0], n);
                outer.position = Vector3.up * (ground + n * h);
                Fades(outer, 0.3f);

                Transform level = AddLevel(root, "Floor " + n, n, hiddenFromBelow: true);
                shell.AddFloor(level);
                float y = ground + n * h - 0.1f;
                if (gallery <= 0)
                    Box("Slab", level, new Vector3((x0 + x1) / 2f, y, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.2f, z1 - z0), floor);
                else
                {
                    int g = gallery;
                    Box("Gallery S", level, new Vector3((x0 + x1) / 2f, y, z0 + g / 2f), new Vector3(x1 - x0, 0.2f, g), floor);
                    Box("Gallery N", level, new Vector3((x0 + x1) / 2f, y, z1 - g / 2f), new Vector3(x1 - x0, 0.2f, g), floor);
                    Box("Gallery W", level, new Vector3(x0 + g / 2f, y, (z0 + z1) / 2f), new Vector3(g, 0.2f, z1 - z0 - 2 * g), floor);
                    Box("Gallery E", level, new Vector3(x1 - g / 2f, y, (z0 + z1) / 2f), new Vector3(g, 0.2f, z1 - z0 - 2 * g), floor);
                    Transform rail = Group(level, "Gallery rail");
                    Wall(rail, "Rail", x0 + g, z0 + g, x1 - g, z0 + g, f.Trim ?? f.Walls, 1f, 0.12f);
                    Wall(rail, "Rail", x0 + g, z1 - g, x1 - g, z1 - g, f.Trim ?? f.Walls, 1f, 0.12f);
                    Wall(rail, "Rail", x0 + g, z0 + g, x0 + g, z1 - g, f.Trim ?? f.Walls, 1f, 0.12f);
                    Wall(rail, "Rail", x1 - g, z0 + g, x1 - g, z1 - g, f.Trim ?? f.Walls, 1f, 0.12f);
                    rail.position = Vector3.up * (ground + n * h);
                }
            }

            Transform roofGroup = Group(root, "Roof");
            Roof(roofGroup, x0, z0, x1, z1, floors * h, f, roof);
            roofGroup.position = Vector3.up * ground;
            Fades(roofGroup, 0.15f);
            roofGroup.gameObject.AddComponent<RoofHider>().interior = new RectInt(x0, z0, x1 - x0, z1 - z0);
            return shell;
        }

        /// <summary>One storey's four walls (with door gaps and lintels), its band or cornice and its windows, built from y = 0.</summary>
        private static void ShellWalls(Transform g, int x0, int z0, int x1, int z1, float h, Facade f, IList<Door> doors, int storey)
        {
            int[] GapsOn(Side side) => doors.Where(d => d.Side == side).SelectMany(d => Enumerable.Range(d.From, d.To - d.From + 1)).ToArray();
            Wall(g, "Wall S", x0, z0, x1, z0, f.Walls, h, 0.2f, GapsOn(Side.South));
            Wall(g, "Wall N", x0, z1, x1, z1, f.Walls, h, 0.2f, GapsOn(Side.North));
            Wall(g, "Wall W", x0, z0, x0, z1, f.Walls, h, 0.2f, GapsOn(Side.West));
            Wall(g, "Wall E", x1, z0, x1, z1, f.Walls, h, 0.2f, GapsOn(Side.East));
            foreach (Door d in doors)
            {
                bool alongX = d.Side == Side.South || d.Side == Side.North;
                float line = d.Side == Side.South ? z0 : d.Side == Side.North ? z1 : d.Side == Side.West ? x0 : x1;
                float mid = (d.From + d.To + 1) / 2f;
                float span = d.To - d.From + 1;
                float doorTop = Mathf.Min(h - 0.4f, 2.4f + span * 0.3f);
                Prop("Lintel", g, alongX ? new Vector3(mid, (doorTop + h) / 2f, line) : new Vector3(line, (doorTop + h) / 2f, mid),
                    alongX ? new Vector3(span, h - doorTop, 0.3f) : new Vector3(0.3f, h - doorTop, span), f.Walls);
                if (f.Trim != null)
                    Prop("Door frame", g, alongX ? new Vector3(mid, doorTop + 0.1f, line) : new Vector3(line, doorTop + 0.1f, mid),
                        alongX ? new Vector3(span + 0.4f, 0.2f, 0.36f) : new Vector3(0.36f, 0.2f, span + 0.4f), f.Trim);
            }
            if (f.Trim != null)
            {
                // A plinth under the ground floor, and a band at the top of every storey (the last one is the cornice).
                if (storey == 0)
                    foreach ((Vector3 a, Vector3 c) in Edges(x0 - 0.1f, z0 - 0.1f, x1 + 0.1f, z1 + 0.1f, 0.2f))
                        Beam("Plinth", g, a, c, 0.36f, 0.4f, f.Trim);
                foreach ((Vector3 a, Vector3 c) in Edges(x0 - 0.1f, z0 - 0.1f, x1 + 0.1f, z1 + 0.1f, h))
                    Beam("Cornice", g, a, c, 0.4f, 0.18f, f.Trim);
            }
            if (f.Windows == null) return;
            var doorTiles = new HashSet<(Side, int)>(doors.SelectMany(d => Enumerable.Range(d.From - 1, d.To - d.From + 3).Select(t => (d.Side, t))));
            float wy = storey == 0 ? 1.7f : 1.9f;
            for (int x = x0 + 1; x < x1 - 1; x += 2)
            {
                if (!doorTiles.Contains((Side.South, x))) Prop("Window", g, new Vector3(x + 0.5f, wy, z0 - 0.08f), new Vector3(0.7f, 1.3f, 0.1f), f.Windows);
                if (!doorTiles.Contains((Side.North, x))) Prop("Window", g, new Vector3(x + 0.5f, wy, z1 + 0.08f), new Vector3(0.7f, 1.3f, 0.1f), f.Windows);
            }
            for (int z = z0 + 1; z < z1 - 1; z += 2)
            {
                if (!doorTiles.Contains((Side.West, z))) Prop("Window", g, new Vector3(x0 - 0.08f, wy, z + 0.5f), new Vector3(0.1f, 1.3f, 0.7f), f.Windows);
                if (!doorTiles.Contains((Side.East, z))) Prop("Window", g, new Vector3(x1 + 0.08f, wy, z + 0.5f), new Vector3(0.1f, 1.3f, 0.7f), f.Windows);
            }
        }

        // ------------------------------------------------------------------ pavilions

        /// <summary>
        /// An open hall on posts over tiles <paramref name="r"/> (a market, a firing line, a stand):
        /// a floor, a post on the corner tiles and every <paramref name="spacing"/> tiles round the
        /// edge (each blocks its tile; the rest is open), beams, and a gabled roof at
        /// <paramref name="height"/> that hides while you are under it. With <paramref name="deck"/>
        /// &gt; 0 it also carries a railed deck at that height as floor 1 (reach it with
        /// <see cref="Shell.Ladder"/>), always drawn.
        /// </summary>
        public static Shell Pavilion(Transform parent, string name, RectInt r, float ground, float height, Material post, Material roof, Material floor,
            int spacing = 4, float deck = 0f)
        {
            int x0 = r.xMin, z0 = r.yMin, x1 = r.xMax, z1 = r.yMax;
            Transform root = Group(parent, name);
            var shell = new Shell(name, root, r, ground, deck > 0f ? deck : height, open: true);
            Transform g0 = Group(root, "Ground floor");
            shell.AddFloor(g0);
            Box("Floor", g0, new Vector3((x0 + x1) / 2f, ground + 0.01f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.02f, z1 - z0), floor);
            var posts = new HashSet<Vector2Int>();
            for (int x = x0; x < x1; x++)
                if ((x - x0) % spacing == 0 || x == x1 - 1) { posts.Add(new Vector2Int(x, z0)); posts.Add(new Vector2Int(x, z1 - 1)); }
            for (int z = z0; z < z1; z++)
                if ((z - z0) % spacing == 0 || z == z1 - 1) { posts.Add(new Vector2Int(x0, z)); posts.Add(new Vector2Int(x1 - 1, z)); }
            Transform frame = Group(root, "Posts");
            foreach (Vector2Int p in posts)
                Box("Post", frame, new Vector3(p.x + 0.5f, ground + height / 2f, p.y + 0.5f), new Vector3(0.35f, height, 0.35f), post).AddComponent<MapBlocker>();
            foreach ((Vector3 a, Vector3 c) in Edges(x0 + 0.35f, z0 + 0.35f, x1 - 0.35f, z1 - 0.35f, ground + height - 0.15f))
                Beam("Beam", frame, a, c, 0.25f, 0.3f, post);
            Fades(frame, 0.4f);

            if (deck > 0f)
            {
                Transform d = AddLevel(root, "Deck", 1, hiddenFromBelow: false);
                shell.AddFloor(d);
                Box("Deck", d, new Vector3((x0 + x1) / 2f, ground + deck - 0.1f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.2f, z1 - z0), floor);
                Transform rail = Group(d, "Rail");
                Wall(rail, "Rail", x0, z0, x1, z0, post, 1f, 0.1f);
                Wall(rail, "Rail", x0, z1, x1, z1, post, 1f, 0.1f);
                Wall(rail, "Rail", x0, z0, x0, z1, post, 1f, 0.1f);
                Wall(rail, "Rail", x1, z0, x1, z1, post, 1f, 0.1f);
                rail.position = Vector3.up * (ground + deck);
                Fades(d, 0.4f);
            }

            Transform roofGroup = GableRoof(root, x0, z0, x1, z1, ground + height, roof, null, 28f);
            Fades(roofGroup, 0.15f);
            roofGroup.gameObject.AddComponent<RoofHider>().interior = new RectInt(x0, z0, x1 - x0, z1 - z0);
            return shell;
        }

        // ------------------------------------------------------------------ the row house

        /// <summary>
        /// The one identical house (8 x 8, two storeys) that a Concordian row is made of, its door in
        /// the middle of the front (south when <paramref name="facesSouth"/>, else north). Downstairs a
        /// hearth on the back wall, a table and a dresser; a stair up the east wall to a bedroom with a
        /// bed and a chest. A brass number plate by the door; <paramref name="observatory"/> puts a
        /// little copper dome on the roof ridge, the only copper a row allows.
        /// </summary>
        public static Shell RowHouse(Transform parent, string name, int x0, int z0, float ground, bool facesSouth, int number, Facade f,
            Material floor, Material wood, Material stone, Material fire, Material well, Material brass, Material copper = null)
        {
            const int size = 8;
            Side front = facesSouth ? Side.South : Side.North, back = facesSouth ? Side.North : Side.South;
            int Z(int v) => facesSouth ? z0 + v : z0 + size - 1 - v; // v: rows back from the front wall
            Shell s = Enterable(parent, name, new RectInt(x0, z0, size, size), ground, 2, f, floor, RoofKind.Gable, 0, new Door(front, x0 + 3, x0 + 4));

            s.Hearth(0, back, x0 + 1, stone, fire);
            s.Table(0, x0 + 3, Mathf.Min(Z(4), Z(5)), 2, 2, wood);
            s.Shelves(0, Side.West, Mathf.Min(Z(1), Z(2)), Mathf.Max(Z(1), Z(2)), wood, 1.8f);
            s.Stairs(0, x0 + 7, Z(1), back, wood, well);
            s.Bed(1, x0 + 1, Mathf.Min(Z(5), Z(6)), false, wood);
            s.Thing(1, "Chest", x0 + 4, Z(6), x0 + 5, Z(6) + 1, 0.6f, wood);
            s.Thing(1, "Wardrobe", x0, Z(1), x0 + 1, Z(1) + 1, 2.2f, wood);

            float plateZ = facesSouth ? z0 - 0.12f : z0 + size + 0.12f;
            Prop("Number " + number, s.Root, new Vector3(x0 + 5.6f, ground + 2f, plateZ), new Vector3(0.4f, 0.3f, 0.04f), brass);
            if (copper != null)
            {
                Transform dome = Group(s.Root, "Rooftop observatory");
                Cylinder("Drum", dome, new Vector3(x0 + 2f, ground + 2 * FloorHeight + 3.4f, z0 + 4f), new Vector3(1.6f, 0.5f, 1.6f), stone, collider: false);
                Dome(dome, new Vector3(x0 + 2f, ground + 2 * FloorHeight + 3.9f, z0 + 4f), 0.85f, 1f, copper, brass, 4);
                Fades(dome, 0.15f);
                dome.SetParent(s.Root.Find("Roof"), true);
            }
            return s;
        }

        // ------------------------------------------------------------------ checks

        /// <summary>
        /// The brief's rules for buildings in a city: no two on the same tier closer than three tiles
        /// (an alley a player squeezes down), and all of them together on at most a quarter of the walkable
        /// ground. With <paramref name="groundAt"/> (a tile's ground height, or null where there is
        /// none) it also checks each building stands on level ground at its own height. Logs a
        /// warning for each break and a line with the share.
        /// </summary>
        public static void CheckBuildings(IList<Shell> shells, int walkableTiles, System.Func<int, int, float?> groundAt = null, int minGap = 3, float maxShare = 0.25f)
        {
            if (groundAt != null)
                foreach (Shell s in shells)
                    for (int x = s.Tiles.xMin; x < s.Tiles.xMax; x++)
                        for (int z = s.Tiles.yMin; z < s.Tiles.yMax; z++)
                        {
                            float? g = groundAt(x, z);
                            if (g == null || Mathf.Abs(g.Value - s.Ground) > 0.05f)
                            {
                                Debug.LogWarning($"[R'th] '{s.Name}' stands at {s.Ground} m but the ground at ({x}, {z}) is {(g == null ? "missing" : g.Value + " m")}.");
                                x = s.Tiles.xMax;
                                break;
                            }
                        }
            for (int i = 0; i < shells.Count; i++)
            {
                for (int j = i + 1; j < shells.Count; j++)
                {
                    if (Mathf.Abs(shells[i].Ground - shells[j].Ground) > Terraces.MaxStep) continue; // on different tiers, kept apart by the ground itself
                    RectInt a = shells[i].Tiles, b = shells[j].Tiles;
                    int dx = Mathf.Max(b.xMin - a.xMax, a.xMin - b.xMax);
                    int dz = Mathf.Max(b.yMin - a.yMax, a.yMin - b.yMax);
                    if (dx < minGap && dz < minGap)
                        Debug.LogWarning($"[R'th] '{shells[i].Name}' and '{shells[j].Name}' are {(dx < 0 && dz < 0 ? "overlapping" : Mathf.Max(dx, dz) + " tiles apart")}; buildings need {minGap}.");
                }
            }
            int covered = shells.Sum(s => s.Tiles.width * s.Tiles.height);
            float share = covered / (float)Mathf.Max(1, walkableTiles);
            string line = $"[R'th] {shells.Count} buildings ({shells.Count(s => s.Floors > 1)} with upper floors, {shells.Sum(s => s.Floors - 1)} upper floors) cover {covered} of {walkableTiles} walkable tiles ({share:P1}).";
            if (share > maxShare) Debug.LogWarning(line + $" The most is {maxShare:P0}.");
            else Debug.Log(line);
        }
    }
}
