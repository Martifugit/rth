using System.Collections.Generic;
using Rth.MapTools;
using UnityEngine;
using RoofKind = Rth.Client.Editor.WorldKit.RoofKind;
using Side = Rth.Client.Editor.WorldKit.Side;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Phase 2 of the Meridian redesign: every building in the brief (section 6) as a shell you
    /// can walk into, its floors joined by stairs or ladders, each room with something in it, and
    /// the Rows' twelve houses from one template. Interiors' stations, shops and readables are
    /// phase 3; who stands where is phase 4.
    /// </summary>
    public static partial class MeridianBuilder
    {
        private static readonly List<WorldKit.Shell> Shells = new List<WorldKit.Shell>();
        private static WorldKit.Facade Harbour, Civic, Copper, Inn, Row, Scholar, Observatory, Dwarf, Road, Tower;

        private static void BuildBuildings(Transform map)
        {
            Shells.Clear();
            Harbour = new WorldKit.Facade { Walls = M.Granite, Roof = M.Slate, Trim = M.Ashlar, Windows = M.Glass, Pitch = 34f };
            Civic = new WorldKit.Facade { Walls = M.Ashlar, Roof = M.Slate, Trim = M.Granite, Windows = M.Glass, Pitch = 30f };
            Copper = new WorldKit.Facade { Walls = M.Ashlar, Roof = M.Verdigris, Trim = M.Granite, Windows = M.Glass };
            Observatory = new WorldKit.Facade { Walls = M.Ashlar, Roof = M.Verdigris, Trim = M.Granite, Windows = M.LitGlass };
            Row = new WorldKit.Facade { Walls = M.Ashlar, Roof = M.Slate, Trim = M.BlueTrim, Windows = M.Glass, Pitch = 38f };
            Scholar = new WorldKit.Facade { Walls = M.Ashlar, Roof = M.Slate, Gable = M.Ashlar, Trim = M.Brass, Windows = M.Glass, Pitch = 36f };
            Dwarf = new WorldKit.Facade { Walls = M.DwarfStone, Roof = M.Iron, Trim = M.Bronze, Windows = M.Forge, Pitch = 26f };
            Road = new WorldKit.Facade { Walls = M.Granite, Roof = M.Slate, Trim = M.Timber, Windows = M.Glass, Pitch = 36f };
            Inn = new WorldKit.Facade { Walls = M.Granite, Roof = M.Slate, Trim = M.Timber, Windows = M.LitGlass, Pitch = 34f };
            Tower = new WorldKit.Facade { Walls = M.Granite, Roof = M.Granite, Trim = M.Ashlar, Windows = M.Glass };

            BuildStrand(WorldKit.Group(map, "The Strand"));
            BuildSquare(WorldKit.Group(map, "Meridian Square"));
            BuildRows(WorldKit.Group(map, "The Rows"));
            BuildAcademy(WorldKit.Group(map, "The Academy Terraces"));
            BuildForum(WorldKit.Group(map, "The Forum Terrace"));
            BuildCrag(WorldKit.Group(map, "The Crag"));
            BuildHeights(WorldKit.Group(map, "Undergate Heights"));
            BuildPassGate(WorldKit.Group(map, "The Pass Road gate"));

            WorldKit.CheckBuildings(Shells, Ground.GroundTiles, (x, z) => Ground.Height(x, z));
        }

        // ------------------------------------------------------------------ helpers, in local tiles

        private static WorldKit.Shell Building(Transform parent, string name, int x0, int z0, int x1, int z1, float ground, int floors, WorldKit.Facade f,
            RoofKind roof, int gallery, params WorldKit.Door[] doors)
        {
            WorldKit.Shell s = WorldKit.Enterable(parent, name, R(x0, z0, x1, z1), ground, floors, f, M.Planks, roof, gallery, doors);
            Shells.Add(s);
            return s;
        }

        private static WorldKit.Shell Pavilion(Transform parent, string name, int x0, int z0, int x1, int z1, float ground, float height, Material floor, int spacing = 4, float deck = 0f)
        {
            WorldKit.Shell s = WorldKit.Pavilion(parent, name, R(x0, z0, x1, z1), ground, height, M.Timber, M.Slate, floor, spacing, deck);
            Shells.Add(s);
            return s;
        }

        /// <summary>A door on a wall over the local tiles from..to along it.</summary>
        private static WorldKit.Door Door(Side side, int from, int to) => new WorldKit.Door(side, Along(side, from), Along(side, to));

        private static int Along(Side side, int t) => side == Side.South || side == Side.North ? X(t) : Z(t);

        private static void Up(WorldKit.Shell s, int level, int x, int z, Side up) => s.Stairs(level, X(x), Z(z), up, M.Timber, M.Shadow);

        private static void Ladder(WorldKit.Shell s, int level, int x, int z, Side wall) => s.Ladder(level, X(x), Z(z), wall, M.Timber);

        private static GameObject Thing(WorldKit.Shell s, int level, string name, int x0, int z0, int x1, int z1, float h, Material m) =>
            s.Thing(level, name, X(x0), Z(z0), X(x1), Z(z1), h, m);

        private static void Table(WorldKit.Shell s, int level, int x, int z, int w, int d) => s.Table(level, X(x), Z(z), w, d, M.Timber);

        private static void Counter(WorldKit.Shell s, int level, int x0, int z0, int x1, int z1) => s.Counter(level, X(x0), Z(z0), X(x1), Z(z1), M.Timber);

        private static void Shelves(WorldKit.Shell s, int level, Side wall, int from, int to, float h = 2.4f) =>
            s.Shelves(level, wall, Along(wall, from), Along(wall, to), M.Timber, h);

        private static void Hearth(WorldKit.Shell s, int level, Side wall, int at) => s.Hearth(level, wall, Along(wall, at), M.Granite, M.Fire);

        private static void Bed(WorldKit.Shell s, int level, int x, int z, bool alongX) => s.Bed(level, X(x), Z(z), alongX, M.Timber);

        private static void Crate(WorldKit.Shell s, int level, int x, int z) => s.Crates(level, X(x), Z(z), M.Timber);

        private static void Bench(WorldKit.Shell s, int level, int x0, int z0, int x1, int z1) => s.Bench(level, X(x0), Z(z0), X(x1), Z(z1), M.Timber);

        private static Transform Obj(WorldKit.Shell s, int level, string name, string type, int x, int z, Material look, float h = 1f) =>
            s.Object(level, name, type, X(x), Z(z), look, h);

        /// <summary>
        /// Makes a piece of furniture interactive: an object of <paramref name="type"/> on the tile
        /// under its middle, so keep it one or two tiles deep for there to be somewhere to stand.
        /// </summary>
        private static GameObject Use(GameObject thing, string type)
        {
            thing.AddComponent<MapObject>().objectType = type;
            return thing;
        }

        /// <summary>Something standing on the open ground over local tiles [x0,x1) x [z0,z1), blocking them.</summary>
        private static GameObject Block(Transform parent, string name, int x0, int z0, int x1, int z1, float h, Material m)
        {
            Vector3 foot = On((x0 + x1) / 2f, (z0 + z1) / 2f);
            GameObject go = WorldKit.Box(name, parent, foot + Vector3.up * (h / 2f), new Vector3(x1 - x0 - 0.2f, h, z1 - z0 - 0.2f), m);
            go.AddComponent<MapBlocker>();
            return go;
        }

        /// <summary>An interactive object on the open ground at a local tile, with a stand-in box.</summary>
        private static Transform Place(Transform parent, string name, string type, int x, int z, Material look, float h = 1.6f)
        {
            Transform t = WorldKit.Station(parent, name, type, X(x), Z(z));
            t.position = On(x + 0.5f, z + 0.5f);
            WorldKit.Box("Stand-in", t, On(x + 0.5f, z + 0.5f, h / 2f), new Vector3(0.8f, h, 0.3f), look);
            return t;
        }

        /// <summary>
        /// A beacon tower: an 8 x 8 base you walk into and three floors above it, a stair up each,
        /// to the lamp room at the top with the lamp and a telescope; battlements and a glass lantern
        /// on the roof.
        /// </summary>
        private static WorldKit.Shell Beacon(Transform parent, string name, int x0, int z0, float ground, Side door, string telescope)
        {
            int at = door == Side.South || door == Side.North ? x0 + 3 : z0 + 3;
            WorldKit.Shell s = Building(parent, name, x0, z0, x0 + 8, z0 + 8, ground, 4, Tower, RoofKind.Battlements, 0, Door(door, at, at + 1));
            Up(s, 0, x0 + 1, z0 + 1, Side.North);
            Up(s, 1, x0 + 6, z0 + 6, Side.South);
            Up(s, 2, x0 + 1, z0 + 6, Side.South);
            Crate(s, 0, x0 + 5, z0 + 1);
            Crate(s, 0, x0 + 6, z0 + 1);
            Thing(s, 0, "Oil casks", x0 + 5, z0 + 5, x0 + 7, z0 + 6, 1f, M.Timber);
            Use(s.Shelves(0, Side.East, Z(z0 + 3), Z(z0 + 4), M.Timber, 1.8f), "beacon_log");
            Table(s, 1, x0 + 3, z0 + 3, 2, 2);
            Bed(s, 1, x0 + 3, z0 + 6, true);
            Crate(s, 2, x0 + 3, z0 + 1);
            Shelves(s, 2, Side.East, z0 + 1, z0 + 2, 1.8f);
            Use(Thing(s, 3, "Lamp", x0 + 3, z0 + 3, x0 + 5, z0 + 5, 1.6f, M.Lamp), "beacon_lamp");
            Use(Thing(s, 3, "Telescope", x0 + 6, z0 + 1, x0 + 7, z0 + 2, 1.3f, M.Brass), telescope);
            Transform lantern = WorldKit.Group(s.Root, "Lantern");
            Vector3 top = P(x0 + 4f, ground + 4 * WorldKit.FloorHeight, z0 + 4f);
            WorldKit.Prop("Lantern", lantern, top + Vector3.up * 1.4f, new Vector3(2.2f, 2.4f, 2.2f), M.Lamp);
            WorldKit.Prop("Cap", lantern, top + Vector3.up * 2.9f, new Vector3(2.8f, 0.5f, 2.8f), M.Verdigris);
            FxKit.PointLight(lantern, "Beacon light", top + Vector3.up * 1.4f, new Color(1f, 0.85f, 0.55f), 2.5f, 26f);
            Tag(s.Root, "beacon_tower", "A square granite lamp tower, 16 m to its battlements, a glass lantern with a verdigris cap on top; inside a stair per floor to the lamp room. The harbour, the knoll and the pass each have one.");
            return s;
        }

        // ------------------------------------------------------------------ the Strand (0 m)

        private static void BuildStrand(Transform g)
        {
            Beacon(g, "Harbour beacon", 126, 0, Strand, Side.North, "harbour_telescope");

            // The Fish Market: an open hall on posts at the head of the mole, its stalls and a cooking range.
            WorldKit.Shell market = Pavilion(g, "Fish Market", 122, 18, 138, 30, Strand, 5f, M.Quay, 5);
            Use(Thing(market, 0, "Stall", 125, 21, 129, 22, 1f, M.Timber), "fish_stall");
            Use(Thing(market, 0, "Stall", 131, 21, 135, 22, 1f, M.Timber), "fish_stall");
            Use(Thing(market, 0, "Stall", 125, 25, 129, 26, 1f, M.Timber), "fish_stall");
            Use(Thing(market, 0, "Cooking range", 132, 25, 135, 27, 1.1f, M.Iron), "meridian_range");
            Tag(market.Root, "fish_market", "The Fish Market: a slate roof on timber posts over granite flags, scrubbed stalls with ice and baskets, a cooking range at the back.");

            // The Plumb Line: the inn on the corner of Line Street, rooms to let upstairs.
            WorldKit.Shell inn = Building(g, "The Plumb Line", 101, 31, 117, 45, Strand, 2, Inn, RoofKind.Gable, 0,
                Door(Side.South, 108, 109), Door(Side.West, 32, 33));
            Counter(inn, 0, 104, 40, 111, 41);
            Use(inn.Hearth(0, Side.North, X(113), M.Granite, M.Fire), "plumb_hearth");
            Table(inn, 0, 104, 34, 2, 2);
            Table(inn, 0, 109, 34, 2, 2);
            Use(Thing(inn, 0, "Cooking range", 111, 43, 113, 44, 1.1f, M.Iron), "meridian_range");
            Obj(inn, 0, "Barometer", "plumb_barometer", 106, 32, M.Brass, 1.6f);
            Up(inn, 0, 115, 33, Side.North);
            Use(inn.Bed(1, X(102), Z(41), true, M.Timber), "inn_bed");
            Use(inn.Bed(1, X(106), Z(41), true, M.Timber), "inn_bed");
            Use(inn.Bed(1, X(102), Z(32), true, M.Timber), "inn_bed");
            Table(inn, 1, 108, 36, 2, 2);
            Tag(inn.Root, "plumb_line_inn", "The Plumb Line: a granite harbour inn with a painted plumb-bob sign, a long bar, a hearth (the only unmeasured fire in the city) and a barometer by the door; rooms upstairs.");

            WorldKit.Shell office = Building(g, "Harbourmaster's Office", 120, 33, 130, 43, Strand, 2, Harbour, RoofKind.Gable, 0, Door(Side.South, 124, 125));
            Counter(office, 0, 122, 39, 128, 40);
            Use(office.Shelves(0, Side.West, Z(35), Z(37), M.Timber, 2f), "shipping_board");
            Up(office, 0, 129, 34, Side.North);
            Use(office.Table(1, X(123), Z(37), 3, 2, M.Timber), "harbour_log");
            Shelves(office, 1, Side.North, 121, 124);
            Tag(office.Root, "harbourmaster_office", "The Harbourmaster's Office: a square granite office, the shipping board by the door, a chart room upstairs with a map table and the log.");

            WorldKit.Shell chandlery = Building(g, "Chandlery", 133, 33, 143, 43, Strand, 2, Harbour, RoofKind.Gable, 0, Door(Side.South, 137, 138));
            Counter(chandlery, 0, 135, 38, 141, 39);
            Use(chandlery.Shelves(0, Side.North, X(134), X(141), M.Timber), "chandlery_shelves");
            Crate(chandlery, 0, 134, 34);
            Up(chandlery, 0, 142, 34, Side.North);
            Bed(chandlery, 1, 134, 40, false);
            Use(chandlery.Table(1, X(137), Z(40), 2, 1, M.Timber), "lodger_table");
            Thing(chandlery, 1, "Chest", 134, 34, 135, 35, 0.6f, M.Timber);
            Tag(chandlery.Root, "chandlery", "The Chandlery: rope, lamp oil, tar and tools on every wall; a narrow stair to a back room someone is living in.");

            WorldKit.Shell loft = Building(g, "Net Loft and Boatyard", 62, 31, 76, 43, Strand, 1, Harbour, RoofKind.Gable, 0, Door(Side.South, 67, 70));
            Use(Thing(loft, 0, "Workbench", 64, 40, 68, 41, 0.9f, M.Timber), "net_bench");
            Use(Thing(loft, 0, "Loom", 71, 38, 74, 40, 1.6f, M.Timber), "sail_loom");
            Shelves(loft, 0, Side.West, 33, 37, 2f);
            Use(Block(g, "Keel on stocks", 65, 23, 74, 25, 1.6f, M.Timber), "boatyard_keel");
            WorldKit.Prop("Ribs", g.transform, On(69.5f, 23.5f, 2.2f), new Vector3(8f, 1.4f, 2.6f), M.Bark);
            Tag(loft.Root, "net_loft", "The Net Loft and Boatyard: a long granite shed with net racks and a loom; outside on the slip a keel on stocks with its first ribs.");

            WorldKit.Shell stevedores = Building(g, "Stevedores' Hall", 146, 31, 160, 43, Strand, 2, Harbour, RoofKind.Gable, 0,
                Door(Side.South, 148, 150), Door(Side.South, 155, 157));
            Crate(stevedores, 0, 147, 40);
            Crate(stevedores, 0, 148, 40);
            Crate(stevedores, 0, 147, 41);
            Thing(stevedores, 0, "Bales", 151, 39, 154, 41, 1.4f, M.Canvas);
            Use(Thing(stevedores, 0, "Ledger desk", 157, 40, 159, 41, 1f, M.Timber), "stevedore_ledger");
            Use(Thing(stevedores, 0, "Cellar hatch", 152, 33, 154, 35, 0.1f, M.Iron), "cold_cellar");
            Ladder(stevedores, 0, 159, 36, Side.East);
            Crate(stevedores, 1, 147, 41);
            Crate(stevedores, 1, 149, 41);
            Use(Thing(stevedores, 1, "Hoard chest", 152, 41, 153, 42, 0.7f, M.Iron), "loft_hoard");
            Tag(stevedores.Root, "stevedores_hall", "The Stevedores' Hall: two open warehouse bays under one roof, hoists over the doors, cargo stacked by number; the cold cellar's hatch; a loft up a ladder.");

            WorldKit.Shell shrine = Building(g, "Sailors' Shrine", 163, 38, 168, 43, Strand, 1, Civic, RoofKind.Gable, 0, Door(Side.South, 165, 165));
            Use(Thing(shrine, 0, "Altar", 164, 41, 167, 42, 1f, M.Ashlar), "sailors_altar");
            Thing(shrine, 0, "Synod lamp", 165, 42, 166, 43, 1.8f, M.Lamp);
            Use(shrine.Shelves(0, Side.West, Z(39), Z(40), M.Timber, 1.4f), "votive_ships");
            Tag(shrine.Root, "sailors_shrine", "The Sailors' Shrine: a tiny whitewashed chapel tucked between the warehouses, a plain altar under the one Synod lamp the city tolerates, votive ship models on the walls.");
        }

        // ------------------------------------------------------------------ Meridian Square (+5 m)

        private static void BuildSquare(Transform g)
        {
            WorldKit.Shell exchange = Building(g, "The Exchange", 60, 51, 74, 63, Square, 1, Civic, RoofKind.Flat, 0, Door(Side.East, 56, 57));
            Use(exchange.Counter(0, X(66), Z(54), X(67), Z(60), M.Timber), "exchange_scales");
            Use(exchange.Shelves(0, Side.North, X(61), X(64), M.Timber, 2f), "exchange_notices");
            Table(exchange, 0, 69, 52, 2, 2);
            Table(exchange, 0, 69, 59, 2, 2);
            Tag(exchange.Root, "the_exchange", "The Exchange: a flat-roofed ashlar hall, a long counter, weights and scales, and the notice board of tasks and errands.");

            WorldKit.Shell clocks = Building(g, "The Clockmaker", 105, 51, 115, 59, Square, 1, Civic, RoofKind.Gable, 0, Door(Side.North, 109, 110));
            Counter(clocks, 0, 107, 54, 113, 55);
            Shelves(clocks, 0, Side.South, 106, 113, 2f);
            Use(Thing(clocks, 0, "Chiming clock", 106, 57, 107, 58, 2.4f, M.Timber), "chiming_clock");
            Tag(clocks.Root, "clockmaker", "The Clockmaker: a narrow shop full of ticking, a counter of movements under glass, and a tall chiming clock that talks.");

            WorldKit.Shell measures = Building(g, "Hall of Measures", 118, 51, 134, 63, Square, 2, Civic, RoofKind.Gable, 0,
                Door(Side.West, 56, 57), Door(Side.South, 125, 126));
            Counter(measures, 0, 121, 59, 129, 60);
            Obj(measures, 0, "Strongbox", "exchequer_strongbox", 122, 61, M.Iron, 0.9f);
            Obj(measures, 0, "Standards case", "standard_measures", 126, 54, M.Glass, 1.3f);
            Up(measures, 0, 132, 52, Side.North);
            Table(measures, 1, 121, 55, 2, 2);
            Table(measures, 1, 125, 55, 2, 2);
            Use(Thing(measures, 1, "Vault door", 125, 62, 127, 63, 2.4f, M.Iron), "exchequer_vault");
            Tag(measures.Root, "hall_of_measures", "The Hall of Measures: pale ashlar, a pediment over the square, the standards case and the Exchequer's counter inside; the gallery and the vault door upstairs.");

            WorldKit.Shell instruments = Building(g, "The Instrument Maker", 137, 51, 149, 63, Square, 1, Civic, RoofKind.Gable, 0, Door(Side.North, 142, 143));
            Use(Thing(instruments, 0, "Workbench", 139, 53, 143, 54, 0.9f, M.Timber), "instrument_bench");
            Shelves(instruments, 0, Side.East, 53, 60);
            Counter(instruments, 0, 139, 58, 144, 59);
            Tag(instruments.Root, "instrument_maker", "The Instrument Maker: crossbows, sights and bolts on racks, a bench of half-made locks, a counter by the door.");

            // The square itself: the gnomon on the meridian, its hour-stones, the Little Orrery, a fountain.
            Transform gnomon = WorldKit.Group(g, "The gnomon");
            Vector3 foot = On(Line, 57f);
            WorldKit.Box("Plinth", gnomon, foot + Vector3.up * 0.4f, new Vector3(2f, 0.8f, 2f), M.Granite).AddComponent<MapBlocker>();
            WorldKit.Prop("Obelisk", gnomon, foot + Vector3.up * 7f, new Vector3(1f, 12.5f, 1f), M.Granite);
            WorldKit.Prop("Tip", gnomon, foot + Vector3.up * 13.5f, new Vector3(0.5f, 0.6f, 0.5f), M.Brass);
            Place(gnomon, "Gnomon plaque", "meridian_gnomon", 96, 55, M.Brass, 0.9f);
            for (int h = 0; h < 9; h++)
            {
                float a = Mathf.Lerp(-70f, 70f, h / 8f) * Mathf.Deg2Rad;
                Vector3 p = On(Line + Mathf.Sin(a) * 9f, 57f - Mathf.Cos(a) * 7f);
                GameObject stone = WorldKit.Box("Hour-stone", gnomon, p + Vector3.up * 0.3f, new Vector3(0.6f, 0.6f, 0.4f), M.Granite, yaw: -a * Mathf.Rad2Deg);
                stone.AddComponent<MapBlocker>();
                Use(stone, "hour_stone");
            }
            Tag(gnomon, "gnomon", "Hero piece. The gnomon: a 13 m granite obelisk on the meridian with a brass tip, hour-lines in brass fanning across the paving to nine hour-stones.");
            Transform orrery = WorldKit.Armillary(g, On(84.5f, 58.5f), 1.4f, M.Brass, M.Granite, M.Brass, M.MoonGlow);
            Use(Block(orrery, "Little Orrery base", 84, 58, 85, 59, 0.5f, M.Granite), "little_orrery");
            Tag(orrery, "little_orrery", "The Little Orrery: a brass armillary on a granite drum, the moons on their rings, one of them green and cracked.");
            Transform fountain = WorldKit.Group(g, "Fountain");
            Use(Block(fountain, "Basin", 81, 51, 84, 53, 0.6f, M.Granite), "square_fountain");
            WorldKit.Prop("Water", fountain, On(82.5f, 52f, 0.55f), new Vector3(2.6f, 0.05f, 2.6f), M.Sea);
            Tag(fountain, "square_fountain", "A plain square granite basin with a single jet: Concordian, so it is exactly one metre high.");
        }

        // ------------------------------------------------------------------ the Rows

        /// <summary>
        /// Twelve identical houses in two Rows, west and east of Line Street north of the Parallel:
        /// a front line facing the avenue and a back line facing the lane under the Academy, with
        /// three-tile gardens between every house. Only the brass number differs (and No. 7, inside,
        /// later). Nos. 3 and 10 have the rooftop observatories.
        /// </summary>
        private static void BuildRows(Transform g)
        {
            int number = 1;
            foreach (int[] row in new[] { new[] { 60, 71, 82 }, new[] { 102, 113, 124 } })
            {
                foreach (bool front in new[] { true, false })
                {
                    foreach (int x in row)
                    {
                        int n = number++;
                        WorldKit.Shell s = WorldKit.RowHouse(g, "No. " + n, X(x), Z(front ? 71 : 82), Square, front, n, Row,
                            M.Planks, M.Timber, M.Granite, M.Fire, M.Shadow, M.Brass, n == 3 || n == 10 ? M.Verdigris : null);
                        Shells.Add(s);
                        Tag(s.Root, "row_house", "The Concordian row house: 8 x 8, two storeys of ashlar, slate roof, blue-grey door and sills, brass number plate. One interior used twelve times.");
                    }
                }
            }
            Transform gardens = WorldKit.Group(g, "Gardens");
            foreach (int x in new[] { 68, 79, 110, 121 })
            {
                Ground.Patch("Garden", gardens, X(x), Z(71), X(x + 3), Z(90), M.Grass);
                foreach (int z in new[] { 74, 85 })
                {
                    GameObject bench = WorldKit.Box("Bench", gardens, On(x + 0.5f, z + 0.5f, 0.25f), new Vector3(0.5f, 0.5f, 0.9f), M.Timber);
                    bench.AddComponent<MapBlocker>();
                    Use(bench, "garden_bench");
                }
            }
            Tag(gardens, "row_gardens", "The Rows' gardens: three-tile strips of lawn between the houses with a bench each, every one mown to the same height.");
        }

        // ------------------------------------------------------------------ the Academy Terraces (+14 m)

        private static void BuildAcademy(Transform g)
        {
            foreach ((string name, int x, string papers) in new[] { ("Scholars' Lodgings (west)", 60, "lodging_books"), ("Scholars' Lodgings (east)", 73, "lodging_letters") })
            {
                WorldKit.Shell s = Building(g, name, x, 96, x + 10, 104, Academy, 2, Scholar, RoofKind.Gable, 0, Door(Side.North, x + 4, x + 5));
                Up(s, 0, x + 8, 97, Side.North);
                Table(s, 0, x + 2, 99, 2, 2);
                Use(s.Shelves(0, Side.West, Z(97), Z(100), M.Timber), "library_shelves");
                Bed(s, 1, x + 1, 101, false);
                Use(s.Table(1, X(x + 4), Z(97), 2, 1, M.Timber), papers);
                Use(s.Shelves(1, Side.South, X(x + 1), X(x + 4), M.Timber), "library_shelves");
                Tag(s.Root, "scholars_lodging", "Scholars' Lodgings: a small ashlar house with lit windows and books stacked on the sills; a study upstairs.");
            }

            WorldKit.Shell hall = Building(g, "The Academy Hall", 60, 122, 78, 136, Academy, 2, Scholar, RoofKind.Gable, 3, Door(Side.South, 68, 69));
            Use(hall.Bench(0, X(64), Z(127), X(75), Z(128), M.Timber), "concord_bench");
            Bench(hall, 0, 64, 129, 75, 130);
            Use(Thing(hall, 0, "Lectern", 68, 132, 70, 133, 1.2f, M.Timber), "academy_lectern");
            Up(hall, 0, 61, 123, Side.North);
            Bench(hall, 1, 64, 134, 75, 135);
            Use(Thing(hall, 1, "Study door", 76, 129, 78, 131, 2.4f, M.BlueTrim), "professors_study");
            Transform clock = WorldKit.Group(hall.Root, "Clock");
            WorldKit.Prop("Clock face", clock, P(69f, Academy + 6.4f, 121.85f), new Vector3(2.2f, 2.2f, 0.12f), M.Ashlar);
            WorldKit.Prop("Hands", clock, P(69f, Academy + 6.6f, 121.75f), new Vector3(0.12f, 1f, 0.05f), M.Brass);
            WorldKit.Dome(clock, P(69f, Academy + 13f, 129f), 1.4f, 1.6f, M.Verdigris, M.Brass, 4);
            Tag(hall.Root, "academy_hall", "Hero piece. The Academy Hall: the great lecture hall under a clock and a verdigris cupola, benches facing a lectern, a gallery round three sides.");

            WorldKit.Shell library = Building(g, "The Library", 101, 96, 117, 110, Academy, 2, Scholar, RoofKind.Gable, 3,
                Door(Side.North, 108, 109), Door(Side.East, 102, 103));
            Up(library, 0, 102, 100, Side.North);
            Use(library.Shelves(0, Side.South, X(104), X(114), M.Timber), "library_shelves");
            Use(library.Shelves(0, Side.West, Z(104), Z(108), M.Timber), "library_shelves");
            Table(library, 0, 106, 101, 2, 2);
            Table(library, 0, 110, 101, 2, 2);
            Use(Thing(library, 0, "Bestiary desk", 112, 105, 114, 106, 0.9f, M.Timber), "bestiary_desk");
            Use(library.Shelves(1, Side.North, X(102), X(115), M.Timber), "library_shelves");
            Thing(library, 1, "Reading desk", 115, 100, 116, 102, 0.9f, M.Timber);
            Use(library.Shelves(1, Side.East, Z(104), Z(106), M.Timber), "forbidden_shelf");
            Tag(library.Root, "academy_library", "The Library: walk-in shelves to the roof, a mezzanine gallery on all sides, reading tables under lamps; one shelf upstairs is roped off.");

            WorldKit.Shell cabinet = Building(g, "The Cabinet of Natural Philosophy", 120, 96, 134, 108, Academy, 2, Scholar, RoofKind.Gable, 0,
                Door(Side.North, 126, 127), Door(Side.West, 101, 102));
            Obj(cabinet, 0, "Moon fragment", "moon_fragment", 127, 101, M.MoonGlow, 1.1f);
            Thing(cabinet, 0, "Moon glow", 126, 102, 129, 103, 0.3f, M.MoonGlow);
            Use(Thing(cabinet, 0, "Specimen case", 122, 98, 124, 99, 1.2f, M.Glass), "specimen_case");
            Use(Thing(cabinet, 0, "Crag-goat", 131, 98, 132, 100, 1.4f, M.Bark), "crag_goat");
            Up(cabinet, 0, 133, 97, Side.North);
            Use(Thing(cabinet, 1, "Specimen cases", 122, 104, 126, 105, 1.2f, M.Glass), "specimen_case");
            Use(Thing(cabinet, 1, "Moon-glass case", 128, 104, 131, 105, 1.4f, M.MoonGlow), "moon_glass_case");
            Table(cabinet, 1, 124, 99, 2, 2);
            Tag(cabinet.Root, "cabinet", "The Cabinet of Natural Philosophy: a museum hall, the green moon fragment behind a brass rail, cases of specimens, a stuffed crag-goat; the moon-glass case upstairs.");

            // The Physic Garden: herb beds round paths, and its dispensary against the Crag stair.
            Transform garden = WorldKit.Group(g, "Physic Garden");
            foreach ((int x, int z) in new[] { (102, 114), (108, 114), (102, 118), (108, 118) })
                Use(Block(garden, "Herb bed", x, z, x + 4, z + 2, 0.4f, M.Pine), "alpine_herb_bed");
            Tag(garden, "physic_garden", "The Physic Garden: raised stone herb beds in a grid, alpine herbs labelled in brass.");
            WorldKit.Shell dispensary = Building(g, "Dispensary", 107, 124, 114, 132, Academy, 1, Scholar, RoofKind.Gable, 0, Door(Side.South, 110, 111));
            Use(Thing(dispensary, 0, "Physic bench", 108, 129, 111, 130, 0.9f, M.Timber), "physic_bench");
            Shelves(dispensary, 0, Side.North, 108, 112);
            Tag(dispensary.Root, "dispensary", "The Dispensary: jars on every shelf, a physician's bench with a balance.");

            // Observatory Row: three little towers, a ladder up to a dome room with a telescope each.
            foreach (int z in new[] { 111, 120, 130 })
            {
                WorldKit.Shell o = Building(g, "Observatory " + (z == 111 ? "I" : z == 120 ? "II" : "III"), 123, z, 129, z + 6, Academy, 2, Observatory, RoofKind.Dome, 0, Door(Side.West, z + 2, z + 2));
                Ladder(o, 0, 128, z + 2, Side.East);
                Use(o.Table(0, X(124), Z(z + 4), 2, 1, M.Timber), "star_charts");
                Use(Thing(o, 1, "Telescope", 125, z + 3, 126, z + 4, 1.4f, M.Brass), z == 130 ? "aligned_telescope" : "observatory_telescope");
                Use(Thing(o, 1, "Star charts", 124, z + 5, 126, z + 6, 0.9f, M.Timber), "star_charts");
                Tag(o.Root, "observatory_tower", "A little observatory tower: two storeys of ashlar under a verdigris dome with a shutter slit, a brass telescope in the dome room.");
            }

            WorldKit.Shell station = Building(g, "Funicular lower station", 90, 124, 104, 136, Academy, 1, Civic, RoofKind.Gable, 0, Door(Side.South, 95, 98));
            Counter(station, 0, 91, 126, 94, 127);
            Thing(station, 0, "Funicular car", 95, 130, 99, 135, 2.6f, M.BlueTrim);
            Obj(station, 0, "Funicular gate", "orrery_lift", 97, 129, M.Brass, 1.2f);
            Tag(station.Root, "funicular_lower", "The funicular's lower station: a glazed shed on the meridian, the ticket window, the car waiting on its rails.");
        }

        // ------------------------------------------------------------------ the Forum Terrace (+14 m)

        private static void BuildForum(Transform g)
        {
            WorldKit.Shell concord = Building(g, "The Concord", 137, 98, 163, 118, Academy, 2, Copper, RoofKind.Dome, 3, Door(Side.South, 147, 152));
            Use(concord.Bench(0, X(143), Z(104), X(157), Z(105), M.Timber), "concord_bench");
            Use(concord.Bench(0, X(142), Z(107), X(158), Z(108), M.Timber), "concord_bench");
            Use(concord.Bench(0, X(141), Z(110), X(159), Z(111), M.Timber), "concord_bench");
            Thing(concord, 0, "Dais", 146, 114, 154, 115, 0.6f, M.Granite);
            Obj(concord, 0, "Rostrum", "concord_rostrum", 150, 113, M.Timber, 1.2f);
            Up(concord, 0, 138, 101, Side.North);
            Up(concord, 0, 162, 101, Side.North);
            Use(concord.Bench(1, X(141), Z(116), X(159), Z(117), M.Timber), "concord_bench");
            Bench(concord, 1, 137, 106, 138, 114);
            Bench(concord, 1, 162, 106, 163, 114);
            Transform portico = WorldKit.Colonnade(g, "Portico", new Vector2Int(X(140), Z(96)), new Vector2Int(X(160), Z(96)), 7f, M.Ashlar, M.Granite, 4, 2f);
            portico.position += Vector3.up * Academy;
            Tag(concord.Root, "the_concord", "Hero piece. The Concord: the assembly's auditorium, a columned portico, a verdigris dome over three arcs of benches facing the rostrum, a gallery all round.");

            WorldKit.Shell lodge = Building(g, "Marksmen's Lodge", 166, 90, 178, 100, Academy, 2, Civic, RoofKind.Gable, 0, Door(Side.North, 171, 172));
            Counter(lodge, 0, 168, 93, 174, 94);
            Use(lodge.Shelves(0, Side.West, Z(92), Z(97), M.Timber), "crossbow_rack");
            Up(lodge, 0, 177, 91, Side.North);
            Use(lodge.Table(1, X(168), Z(96), 2, 2, M.Timber), "range_ledger");
            Shelves(lodge, 1, Side.South, 167, 172);
            Tag(lodge.Root, "marksmens_lodge", "The Marksmen's Lodge: ammunition by the crate, racks of crossbows, the range office and a balcony over the lanes.");

            // The Butts: the firing line under a roof, six lanes, the targets and the earth bank.
            WorldKit.Shell line = Pavilion(g, "The firing line", 166, 104, 191, 108, Academy, 4f, M.Paving, 6);
            Tag(line.Root, "butts_firing_line", "The Butts' firing line: a long slate roof on posts over six numbered stands.");
            Transform butts = WorldKit.Group(g, "The Butts");
            for (int i = 0; i < 6; i++)
                Use(Block(butts, "Target", 168 + i * 4, 123, 169 + i * 4, 124, 1.6f, M.Straw), "butts_target");
            Block(butts, "Earth bank", 166, 126, 192, 128, 2.5f, M.PineFloor);
            Tag(butts, "butts_targets", "The Butts' targets: straw bosses on easels with painted rings, against a turf bank; numbered lane posts.");
            WorldKit.Shell stand = Pavilion(g, "Judges' stand", 182, 93, 188, 99, Academy, 6.5f, M.Planks, 5, 3.5f);
            Ladder(stand, 0, 184, 94, Side.South);
            Use(Thing(stand, 1, "Judges' bench", 183, 97, 187, 98, 0.5f, M.Timber), "judges_bench");
            Tag(stand.Root, "judges_stand", "The judges' stand: a timber platform on posts above the lanes under a shade roof, a bench for three.");
        }

        // ------------------------------------------------------------------ the Crag (+24, +30 m)

        private static void BuildCrag(Transform g)
        {
            WorldKit.Shell station = Building(g, "Funicular upper station", 90, 136, 104, 146, Crag, 1, Civic, RoofKind.Gable, 0, Door(Side.North, 95, 98));
            Thing(station, 0, "Funicular car", 95, 137, 99, 141, 2.6f, M.BlueTrim);
            Obj(station, 0, "Funicular gate", "orrery_lift", 97, 142, M.Brass, 1.2f);
            Use(Thing(station, 0, "Winding wheel", 100, 143, 103, 145, 3f, M.Brass), "winding_wheel");
            Tag(station.Root, "funicular_upper", "The funicular's upper station: the car arrives through the floor; the great brass winding wheel turns behind a glass wall.");

            WorldKit.Shell almanac = Building(g, "Almanac Office", 64, 140, 80, 154, Crag, 3, Civic, RoofKind.Gable, 0, Door(Side.South, 71, 72));
            Use(almanac.Counter(0, X(66), Z(148), X(76), Z(149), M.Timber), "forecast_desk");
            Shelves(almanac, 0, Side.North, 65, 78);
            Up(almanac, 0, 79, 141, Side.North);
            Use(almanac.Table(1, X(67), Z(144), 3, 2, M.Timber), "forecast_desk");
            Shelves(almanac, 1, Side.North, 66, 72);
            Ladder(almanac, 1, 65, 151, Side.West);
            Use(Thing(almanac, 2, "Semaphore levers", 70, 147, 74, 148, 1.2f, M.Brass), "semaphore_levers");
            Use(Thing(almanac, 2, "Telescope", 76, 150, 77, 151, 1.3f, M.Brass), "almanac_telescope");
            Transform mast = WorldKit.Group(almanac.Root, "Semaphore mast");
            Vector3 mastFoot = P(72f, Crag + 3 * WorldKit.FloorHeight, 147f);
            WorldKit.Prop("Mast", mast, mastFoot + Vector3.up * 6f, new Vector3(0.3f, 12f, 0.3f), M.Timber);
            WorldKit.Beam("Arm", mast, mastFoot + new Vector3(-1.8f, 10.5f, 0f), mastFoot + new Vector3(1.8f, 11.4f, 0f), 0.25f, 0.25f, M.Iron);
            WorldKit.Beam("Arm", mast, mastFoot + new Vector3(-1.4f, 8f, 0f), mastFoot + new Vector3(1.4f, 7.2f, 0f), 0.25f, 0.25f, M.Iron);
            Place(g, "Moonfall almanac", "moonfall_almanac", 68, 138, M.Timber);
            Tag(almanac.Root, "almanac_office", "The Almanac Office: three storeys on the Crag's shoulder, the forecast desk, the semaphore room under a mast with two black arms; the almanac board outside.");

            WorldKit.Shell gate = Building(g, "Orrery Gate lodge", 84, 164, 94, 174, Crag, 2, Civic, RoofKind.Gable, 0, Door(Side.East, 168, 169));
            Use(gate.Table(0, X(86), Z(170), 3, 1, M.Timber), "appointment_book");
            Bench(gate, 0, 86, 165, 89, 166);
            Up(gate, 0, 85, 165, Side.North);
            Use(gate.Table(1, X(88), Z(169), 2, 2, M.Timber), "appointment_book");
            Shelves(gate, 1, Side.North, 90, 93);
            Transform orreryGate = WorldKit.Group(g, "The Orrery Gate");
            Block(orreryGate, "Gate pier", 94, 175, 95, 177, 4f, M.Granite);
            Block(orreryGate, "Gate pier", 99, 175, 100, 177, 4f, M.Granite);
            Use(Block(orreryGate, "Gates", 95, 176, 99, 177, 3.2f, M.Iron), "orrery_gate");
            Tag(orreryGate, "orrery_gate", "The Orrery Gate: two granite piers and brass-bound iron gates across the meridian at the mountain's foot, a plaque: ASCENTS BY APPOINTMENT.");
            Tag(gate.Root, "orrery_gate_lodge", "The Orrery Gate lodge: the appointment office, a bench for those who wait, a window on the Orrery.");

            Beacon(g, "Knoll beacon", 130, 166, Knoll, Side.West, "knoll_telescope");
        }

        // ------------------------------------------------------------------ Undergate Heights (+20 m)

        private static void BuildHeights(Transform g)
        {
            WorldKit.Shell survey = Building(g, "Survey Office", 26, 81, 38, 91, Heights, 2, Civic, RoofKind.Gable, 0, Door(Side.West, 85, 86));
            Use(survey.Table(0, X(29), Z(85), 3, 2, M.Timber), "survey_map_table");
            Use(Thing(survey, 0, "Glowing cores", 34, 88, 36, 90, 0.9f, M.MoonGlow), "glowing_cores");
            Up(survey, 0, 37, 82, Side.North);
            Use(survey.Table(1, X(29), Z(85), 3, 2, M.Timber), "survey_map_table");
            Thing(survey, 1, "Core store", 27, 88, 30, 90, 1.2f, M.Timber);
            Place(g, "Survey notice", "survey_notice", 24, 82, M.Timber);
            Tag(survey.Root, "survey_office", "The Survey Office: the surveyors' plain granite office, crates of glowing cores, a map table upstairs; tents behind it.");

            // Bore Nine: the derrick in its fence, its glowing spoil; sealed.
            Transform bore = WorldKit.Group(g, "Bore Nine");
            WorldKit.Fence(bore, "Fence", X(9), Z(80), X(21), Z(80), M.Iron, X(14), X(15));
            WorldKit.Fence(bore, "Fence", X(9), Z(92), X(21), Z(92), M.Iron);
            WorldKit.Fence(bore, "Fence", X(9), Z(80), X(9), Z(92), M.Iron);
            WorldKit.Fence(bore, "Fence", X(21), Z(80), X(21), Z(92), M.Iron);
            foreach (Transform t in bore) t.position += Vector3.up * Heights;
            Block(bore, "Derrick", 13, 84, 17, 88, 1f, M.Iron);
            WorldKit.Prop("Derrick tower", bore, On(15f, 86f, 6f), new Vector3(2.4f, 12f, 2.4f), M.Timber);
            Use(Block(bore, "Spoil", 10, 89, 13, 91, 1.2f, M.MoonGlow), "glowing_spoil");
            Place(bore, "Shaft cover", "bore_nine_shaft", 15, 83, M.Iron, 0.4f);
            Place(bore, "Bore Nine sign", "bore_nine", 16, 79, M.Timber);
            Tag(bore, "bore_nine", "Bore Nine: a timber derrick over a capped shaft, inside an iron fence; heaps of spoil that glow green at night. Suspended.");

            WorldKit.Shell carts = Building(g, "Cart-master's Office", 8, 97, 18, 105, Heights, 1, Dwarf, RoofKind.Battlements, 0, Door(Side.East, 100, 101));
            Use(carts.Shelves(0, Side.North, X(9), X(14), M.Timber, 2f), "cart_timetable");
            Table(carts, 0, 11, 99, 2, 2);
            Tag(carts.Root, "cart_office", "The Cart-master's Office: a squat dwarven office, the cart-line timetable chalked on slate, a desk with a bronze bell.");

            WorldKit.Shell trading = Building(g, "Dwarven Trading Hall", 8, 109, 24, 123, Heights, 2, Dwarf, RoofKind.Battlements, 0, Door(Side.East, 115, 116));
            Use(trading.Counter(0, X(13), Z(111), X(14), Z(120), M.Timber), "dwarf_counter");
            Shelves(trading, 0, Side.West, 110, 121);
            Up(trading, 0, 22, 110, Side.North);
            Table(trading, 1, 12, 114, 3, 2);
            Thing(trading, 1, "Strongbox", 9, 121, 10, 122, 0.8f, M.Bronze);
            Tag(trading.Root, "dwarf_trading_hall", "The Dwarven Trading Hall: dark stone banded in bronze, battlemented, built to keep out more than weather; a long counter, a counting room upstairs.");

            WorldKit.Shell smeltery = Building(g, "Smeltery", 27, 97, 43, 111, Heights, 1, Dwarf, RoofKind.Gable, 0, Door(Side.South, 33, 36), Door(Side.North, 33, 34));
            Thing(smeltery, 0, "Furnace", 29, 108, 32, 110, 3f, M.Forge);
            Use(Thing(smeltery, 0, "Furnace mouth", 29, 107, 32, 108, 1.2f, M.Forge), "smeltery_furnace");
            Use(Thing(smeltery, 0, "Anvil", 36, 103, 37, 104, 0.8f, M.Iron), "smeltery_anvil");
            Use(Thing(smeltery, 0, "Ore cart", 39, 99, 41, 100, 1f, M.Iron), "ore_cart");
            Transform stack = WorldKit.Group(smeltery.Root, "Chimney");
            WorldKit.Prop("Stack", stack, P(30.5f, Heights + 9f, 108f), new Vector3(1.6f, 10f, 1.6f), M.DwarfStone);
            Tag(smeltery.Root, "smeltery", "The Smeltery: a dwarven forge-hall, a furnace with a tall stack, anvils, ore carts rolling in on the rails.");

            WorldKit.Shell undergate = Building(g, "The Undergate", 20, 128, 34, 140, Heights, 1, Dwarf, RoofKind.Battlements, 0, Door(Side.South, 25, 28));
            Use(Thing(undergate, 0, "Sealed portal", 24, 138, 30, 139, 3.5f, M.Bronze), "undergate_door");
            Use(Thing(undergate, 0, "Stockroom chest", 21, 130, 22, 131, 0.9f, M.Bronze), "dwarf_stockroom");
            Use(Thing(undergate, 0, "Ore cart", 31, 132, 32, 134, 1f, M.Iron), "ore_cart");
            Transform portal = WorldKit.Group(undergate.Root, "Mountain door");
            WorldKit.Prop("Door face", portal, P(27f, Heights + 7f, 140.3f), new Vector3(12f, 14f, 0.6f), M.DwarfStone);
            WorldKit.Prop("Doors", portal, P(27f, Heights + 4.5f, 139.9f), new Vector3(5f, 9f, 0.3f), M.Bronze);
            foreach (float y in new[] { 3f, 6.5f, 10f })
                WorldKit.Prop("Band", portal, P(27f, Heights + y, 139.85f), new Vector3(12.4f, 0.4f, 0.3f), M.Bronze);
            WorldKit.Rails(portal, P(29.5f, Heights + 0.05f, 128.01f), P(29.5f, Heights + 0.05f, 139.9f), 1.1f, M.Iron, M.Timber);
            Tag(undergate.Root, "the_undergate", "Hero piece. The Undergate: the dwarves' portal hall against the mountain, a bronze-bound door into the rock, the cart-line running in under it.");
        }

        // ------------------------------------------------------------------ the Pass Road gate (+5 m)

        private static void BuildPassGate(Transform g)
        {
            WorldKit.Shell yard = Building(g, "Cartwrights' Yard", 152, 50, 166, 62, Square, 1, Road, RoofKind.Gable, 0, Door(Side.North, 157, 160));
            Use(Thing(yard, 0, "Wheels", 154, 52, 156, 53, 1.4f, M.Timber), "cart_wheels");
            Thing(yard, 0, "Axle rack", 159, 52, 164, 53, 1.2f, M.Timber);
            Thing(yard, 0, "Cart", 156, 56, 160, 58, 1.2f, M.Bark);
            Tag(yard.Root, "cartwrights_yard", "The Cartwrights' Yard: a timber-trimmed shed, wheels and axles stacked, a cart on blocks.");

            Beacon(g, "Pass beacon", 170, 52, Square, Side.North, "pass_telescope");

            WorldKit.Shell toll = Building(g, "Toll-house", 181, 54, 191, 62, Square, 2, Road, RoofKind.Gable, 0, Door(Side.North, 185, 186));
            Table(toll, 0, 183, 56, 2, 1);
            Thing(toll, 0, "Toll box", 188, 59, 189, 60, 1f, M.Iron);
            Ladder(toll, 0, 190, 56, Side.East);
            Use(Thing(toll, 1, "Bell", 184, 58, 186, 60, 1.5f, M.Bronze), "toll_bell");
            Bench(toll, 1, 182, 55, 185, 56);
            Place(g, "Toll board", "concord_toll_board", 183, 63, M.Timber);
            WorldKit.Beam("Toll bar", g, P(186.5f, Square + 1.1f, 63.5f), P(186.5f, Square + 5f, 69f), 0.25f, 0.25f, M.BlueTrim);
            Tag(toll.Root, "toll_house", "The toll-house: a granite lodge by the road with a raised barber-striped bar, a bell loft up a ladder.");

            WorldKit.Shell wayhouse = Building(g, "Wayhouse and Stables", 151, 74, 167, 88, Square, 2, Inn, RoofKind.Gable, 0,
                Door(Side.South, 158, 159), Door(Side.West, 79, 80));
            Hearth(wayhouse, 0, Side.North, 153);
            Table(wayhouse, 0, 154, 77, 2, 2);
            Use(wayhouse.Counter(0, X(158), Z(83), X(162), Z(84), M.Timber), "wayhouse_register");
            Obj(wayhouse, 0, "Carters' chest", "wayhouse_chest", 152, 76, M.Iron, 0.8f);
            Thing(wayhouse, 0, "Stall", 162, 76, 164, 79, 1.4f, M.Timber);
            Up(wayhouse, 0, 165, 75, Side.North);
            Bed(wayhouse, 1, 152, 85, true);
            Bed(wayhouse, 1, 156, 85, true);
            Bed(wayhouse, 1, 160, 85, true);
            Table(wayhouse, 1, 154, 77, 2, 2);
            Tag(wayhouse.Root, "wayhouse", "The Wayhouse and Stables: a long travellers' inn under the Forum's cliff, a hearth, horse stalls at one end, rooms upstairs.");

            WorldKit.Shell guild = Building(g, "Surveyors' Guild", 170, 74, 182, 84, Square, 2, Civic, RoofKind.Gable, 0, Door(Side.South, 175, 176));
            Use(guild.Table(0, X(173), Z(78), 4, 2, M.Timber), "guild_world_map");
            Shelves(guild, 0, Side.North, 171, 178);
            Up(guild, 0, 181, 75, Side.North);
            Table(guild, 1, 173, 79, 3, 2);
            Use(Thing(guild, 1, "Diary desk", 171, 82, 173, 83, 0.9f, M.Timber), "diary_desk");
            Tag(guild.Root, "surveyors_guild", "The Surveyors' Guild: the world map on a great table under a skylight, drawers of charts, the achievement diary desk upstairs.");
        }
    }
}
