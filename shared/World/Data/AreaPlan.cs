using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// An area's plan as data (world/plans/&lt;area&gt;.json, docs/areas/plan-template.md): what is
    /// physically there and why, before any ground is changed. Every place says how a player first finds
    /// it and what catches their eye; every building, node, creature, station, dungeon, height and space
    /// kept for later says why it is there (what it supports, why it fits the place); the plan names
    /// its ties to every neighbouring area. <see cref="Check"/> holds a plan to that; <see cref="Built"/>
    /// compares the regions as built with it. Stage 0 of the area standard (world/areas.json) is this
    /// plan, the design document beside it, and the owner's approval; the tools refuse ground work in
    /// an area without that approval.
    /// </summary>
    public sealed class AreaPlan
    {
        public const string Folder = "plans";

        /// <summary>The kinds of content a plan lists.</summary>
        public static readonly string[] ContentKinds = { "node", "creature", "station", "npc", "object", "view", "shortcut" };

        /// <summary>What a height may be for. A height with no job isn't planned.</summary>
        public static readonly string[] HeightJobs =
        {
            "destination", // it makes somewhere to go: a gorge with a cave at its end
            "shortcut",    // a way that saves walking: steps, a stair, a jump down
            "nodes",       // a face or pit that holds resources: a quarry, a gorge wall
            "view",        // a top worth climbing to, with a view point
            "gate",        // it closes ground off but for one or two routes: a cliff with a combe
            "threshold",   // a crest that marks leaving one place for the next
            "reserve",     // ground kept for later content
            "water",       // it holds or shapes water: a lake's bed, a river's banks
        };

        public sealed class Place
        {
            public string Id = "";
            public string Name = "";
            /// <summary>town, village, hamlet, farm, site, landmark, camp...</summary>
            public string Kind = "";
            public int[]? At;
            public int[]? Rect;
            /// <summary>The levels it is for, [from, to].</summary>
            public int[]? Levels;
            /// <summary>Why it is here: what it gives players, and why it fits this spot.</summary>
            public string Why = "";
            /// <summary>How a player first finds it: seen from where, sent by whom, on the way to what.</summary>
            public string FoundBy = "";
            /// <summary>The first thing that catches their eye about it.</summary>
            public string Hook = "";
        }

        public sealed class Building
        {
            /// <summary>The building piece's id once it is built.</summary>
            public string Id = "";
            public string Place = "";
            public int[]? Rect;
            public int Storeys = 1;
            /// <summary>What it is used for, in a word or two (the bank, the smithy, a house).</summary>
            public string Use = "";
            /// <summary>What is inside: stations, people, objects, upper floors, a way down.</summary>
            public List<string> Contents = new List<string>();
            public string Why = "";
        }

        public sealed class Content
        {
            public string Id = "";
            public string Kind = "";
            /// <summary>An id in objects.json (nodes, stations, objects).</summary>
            public string? Object;
            /// <summary>An id in npcs.json (creatures, people).</summary>
            public string? Npc;
            /// <summary>Not in the content files yet: proposed.</summary>
            public bool New;
            public int Count = 1;
            /// <summary>Where it goes, [x0, y0, x1, y1) in tiles.</summary>
            public int[]? Rect;
            public string Place = "";
            /// <summary>The skill it trains or serves, by its id in skills.json.</summary>
            public string? Skill;
            public int[]? Levels;
            /// <summary>Why it is here: why this resource or creature belongs in this place.</summary>
            public string Why = "";
            /// <summary>What it supports: "quest:the-bell", an activity, a task, another area's need.</summary>
            public List<string> Supports = new List<string>();
        }

        public sealed class Room
        {
            public string Name = "";
            public string Contents = "";
        }

        public sealed class Dungeon
        {
            public string Id = "";
            public string Name = "";
            /// <summary>The way in, on the surface.</summary>
            public int[]? Entrance;
            /// <summary>The climb object at the way in.</summary>
            public string? Object;
            public int[]? Size;
            public int[]? Levels;
            public List<Room> Rooms = new List<Room>();
            public string Why = "";
            public List<string> Supports = new List<string>();
        }

        public sealed class Route
        {
            public string Id = "";
            /// <summary>main, road, track, path, river.</summary>
            public string Kind = "";
            public List<float[]> Points = new List<float[]>();
            public string Why = "";
        }

        public sealed class Height
        {
            public string Id = "";
            public string Name = "";
            /// <summary>One of <see cref="HeightJobs"/>.</summary>
            public string Job = "";
            /// <summary>Its outline, or its line for a ridge or a cliff.</summary>
            public List<float[]> Outline = new List<float[]>();
            /// <summary>Its height in tiles over the land round it (negative for a pit or a gorge).</summary>
            public float Rise;
            /// <summary>Its section in words: how it rises, what is on its faces and its top.</summary>
            public string Profile = "";
            /// <summary>The routes up, down or through it, by route id or in words.</summary>
            public List<string> Routes = new List<string>();
            /// <summary>What is seen from its top (within the fog's 85 tiles).</summary>
            public List<string> Sees = new List<string>();
            public string Why = "";
        }

        public sealed class Reserve
        {
            public string Id = "";
            public int[]? Rect;
            /// <summary>The content it is kept for.</summary>
            public string For = "";
            public string Why = "";
        }

        public sealed class Tie
        {
            public string Area = "";
            /// <summary>What crosses between the two: roads, rivers, views, errands, resources one needs from the other.</summary>
            public string Ties = "";
        }

        public string Area = "";
        /// <summary>draft, review or agreed.</summary>
        public string Status = "draft";
        /// <summary>The design document, from the repository root.</summary>
        public string Doc = "";
        /// <summary>The wider region it belongs to, and its part in it.</summary>
        public string Region = "";
        public List<Place> Places = new List<Place>();
        public List<Building> Buildings = new List<Building>();
        public List<Content> Contents = new List<Content>();
        public List<Dungeon> Dungeons = new List<Dungeon>();
        public List<Route> Routes = new List<Route>();
        public List<Height> Heights = new List<Height>();
        public List<Reserve> Reserved = new List<Reserve>();
        public List<Tie> Neighbours = new List<Tie>();

        public static string PathFor(string root, string area) => System.IO.Path.Combine(root, "world", Folder, area + ".json");

        public static AreaPlan Parse(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<AreaPlan>(json) ?? throw new FormatException("empty plan");
            }
            catch (JsonException e)
            {
                throw new FormatException(e.Message, e);
            }
        }

        /// <summary>What the plan's content trains, skill by skill: the content ids under each.</summary>
        public SortedDictionary<string, List<string>> Skills()
        {
            var bySkill = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (Content c in Contents.Where(c => !string.IsNullOrEmpty(c.Skill)))
            {
                if (!bySkill.TryGetValue(c.Skill!, out List<string>? list)) bySkill[c.Skill!] = list = new List<string>();
                list.Add(c.Id);
            }
            return bySkill;
        }

        /// <summary>What the checks need to know about the rest of the world.</summary>
        public sealed class World
        {
            public WorldAreas Areas = new WorldAreas();
            /// <summary>Every surface region's rectangle, by id.</summary>
            public Dictionary<string, (int x, int y, int w, int h)> Regions = new Dictionary<string, (int, int, int, int)>(StringComparer.Ordinal);
            public HashSet<string> Objects = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> Npcs = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> Skills = new HashSet<string>(StringComparer.Ordinal);
            /// <summary>Quest ids in quests.json and quest documents in docs/quests.</summary>
            public HashSet<string> Quests = new HashSet<string>(StringComparer.Ordinal);
            /// <summary>Files that exist, from the repository root, for <see cref="Doc"/>.</summary>
            public Func<string, bool> FileExists = _ => true;
        }

        /// <summary>The areas sharing a border with this one (a region of each touching a region of the other).</summary>
        public static List<string> NeighboursOf(string areaId, World w)
        {
            WorldAreas.Area? area = w.Areas.Areas.FirstOrDefault(a => a.Id == areaId);
            if (area == null) return new List<string>();
            var mine = area.Regions.Where(w.Regions.ContainsKey).Select(r => w.Regions[r]).ToList();
            bool Touch((int x, int y, int w, int h) a, (int x, int y, int w, int h) b)
            {
                bool xs = a.x <= b.x + b.w && b.x <= a.x + a.w, ys = a.y <= b.y + b.h && b.y <= a.y + a.h;
                bool sideBySide = (a.x + a.w == b.x || b.x + b.w == a.x) && a.y < b.y + b.h && b.y < a.y + a.h;
                bool aboveBelow = (a.y + a.h == b.y || b.y + b.h == a.y) && a.x < b.x + b.w && b.x < a.x + a.w;
                return xs && ys && (sideBySide || aboveBelow);
            }
            return w.Areas.Areas.Where(o => o.Id != areaId && o.Wave > 0 &&
                    o.Regions.Where(w.Regions.ContainsKey).Any(r => mine.Any(m => Touch(m, w.Regions[r]))))
                .Select(o => o.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// What the plan lacks, as errors: anything without a why, a place without how it is found and
        /// what catches the eye, content that names an object, creature, skill or quest that doesn't exist
        /// (unless marked new), positions outside the area, a height without a job, a neighbouring area
        /// with no ties.
        /// </summary>
        public List<string> Check(World w)
        {
            var errors = new List<string>();
            void Need(bool ok, string message) { if (!ok) errors.Add(message); }
            bool Has(string? s) => !string.IsNullOrWhiteSpace(s);

            WorldAreas.Area? area = w.Areas.Areas.FirstOrDefault(a => a.Id == Area);
            if (area == null) { errors.Add($"'{Area}' is not an area in world/areas.json"); return errors; }
            Need(Has(Doc) && w.FileExists(Doc), $"the design document '{Doc}' doesn't exist");
            Need(Has(Region), "it doesn't say which wider region it is part of (region)");
            Need(new[] { "draft", "review", "agreed" }.Contains(Status), $"status '{Status}' is not draft, review or agreed");

            var rects = area.Regions.Where(w.Regions.ContainsKey).Select(r => w.Regions[r]).ToList();
            bool Inside(int x, int y) => rects.Any(r => x >= r.x && y >= r.y && x < r.x + r.w && y < r.y + r.h);
            void InArea(string what, int[]? at)
            {
                if (at == null) return;
                if (at.Length == 2) Need(Inside(at[0], at[1]), $"{what}: ({at[0]}, {at[1]}) is outside the area");
                else if (at.Length == 4)
                    Need(at[0] < at[2] && at[1] < at[3] && Inside(at[0], at[1]) && Inside(at[2] - 1, at[3] - 1),
                        $"{what}: [{string.Join(", ", at)}] is not a rectangle inside the area");
                else errors.Add($"{what}: a position is [x, y] or a rectangle [x0, y0, x1, y1]");
            }
            void Supports(string what, List<string> list)
            {
                foreach (string s in list.Where(s => s.StartsWith("quest:", StringComparison.Ordinal)))
                    Need(w.Quests.Contains(s.Substring(6)), $"{what}: supports {s}, which is neither in quests.json nor docs/quests");
            }

            // Ids are unique within their kind: a place and its building may share one.
            var ids = new HashSet<string>(StringComparer.Ordinal);
            void Id(string kind, string id)
            {
                if (!Has(id)) errors.Add($"a {kind} has no id");
                else if (!ids.Add(kind + ":" + id)) errors.Add($"{kind} '{id}' is used twice");
            }

            var places = new HashSet<string>(Places.Select(p => p.Id), StringComparer.Ordinal);
            foreach (Place p in Places)
            {
                Id("place", p.Id);
                string what = "place " + p.Id;
                Need(Has(p.Name) && Has(p.Kind), $"{what}: name and kind");
                Need(p.At != null || p.Rect != null, $"{what}: where it is (at or rect)");
                InArea(what, p.At);
                InArea(what, p.Rect);
                Need(p.Levels != null && p.Levels.Length == 2, $"{what}: the levels it is for, [from, to]");
                Need(Has(p.Why), $"{what}: why it is here");
                Need(Has(p.FoundBy), $"{what}: how a player first finds it (foundBy)");
                Need(Has(p.Hook), $"{what}: the first thing that catches the eye (hook)");
            }
            foreach (Building b in Buildings)
            {
                Id("building", b.Id);
                string what = "building " + b.Id;
                Need(places.Contains(b.Place), $"{what}: its place '{b.Place}' isn't in the plan");
                Need(b.Rect != null, $"{what}: its rect");
                InArea(what, b.Rect);
                Need(b.Storeys >= 1, $"{what}: storeys");
                Need(Has(b.Use), $"{what}: what it is used for");
                Need(b.Contents.Count > 0, $"{what}: what is inside");
                Need(Has(b.Why), $"{what}: why it is here");
            }
            foreach (Content c in Contents)
            {
                Id("content", c.Id);
                string what = "content " + c.Id;
                Need(ContentKinds.Contains(c.Kind), $"{what}: kind '{c.Kind}' is not one of {string.Join(", ", ContentKinds)}");
                Need(Has(c.Object) || Has(c.Npc), $"{what}: the object or npc it is");
                if (Has(c.Object) && !c.New) Need(w.Objects.Contains(c.Object!), $"{what}: object '{c.Object}' isn't in objects.json (mark it \"new\")");
                if (Has(c.Npc) && !c.New) Need(w.Npcs.Contains(c.Npc!), $"{what}: npc '{c.Npc}' isn't in npcs.json (mark it \"new\")");
                Need(c.Count >= 1, $"{what}: a count");
                Need(c.Rect != null, $"{what}: where it goes (rect)");
                InArea(what, c.Rect);
                Need(!Has(c.Place) || places.Contains(c.Place), $"{what}: its place '{c.Place}' isn't in the plan");
                if (c.Kind == "node" || c.Kind == "creature")
                {
                    Need(Has(c.Skill), $"{what}: the skill it trains");
                    Need(c.Levels != null && c.Levels.Length == 2, $"{what}: the levels it is for");
                }
                if (Has(c.Skill)) Need(w.Skills.Contains(c.Skill!), $"{what}: '{c.Skill}' isn't a skill in skills.json");
                Need(Has(c.Why), $"{what}: why it belongs here");
                Supports(what, c.Supports);
            }
            foreach (Dungeon d in Dungeons)
            {
                Id("dungeon", d.Id);
                string what = "dungeon " + d.Id;
                Need(Has(d.Name), $"{what}: its name");
                Need(d.Entrance != null, $"{what}: its entrance on the surface");
                InArea(what, d.Entrance);
                Need(d.Size != null && d.Size.Length == 2, $"{what}: its size [w, h]");
                Need(d.Levels != null && d.Levels.Length == 2, $"{what}: the levels it is for");
                Need(d.Rooms.Count > 0 && d.Rooms.All(r => Has(r.Name) && Has(r.Contents)), $"{what}: its rooms and what is in each");
                Need(Has(d.Why), $"{what}: why it is here");
                Supports(what, d.Supports);
            }
            foreach (Route r in Routes)
            {
                Id("route", r.Id);
                Need(Has(r.Kind) && r.Points.Count >= 2, $"route {r.Id}: its kind and at least two points");
                Need(Has(r.Why), $"route {r.Id}: why it goes this way");
            }
            foreach (Height h in Heights)
            {
                Id("height", h.Id);
                string what = "height " + h.Id;
                Need(HeightJobs.Contains(h.Job), $"{what}: its job, one of {string.Join(", ", HeightJobs)}");
                Need(h.Outline.Count >= 2, $"{what}: its outline or line");
                Need(Has(h.Profile), $"{what}: its profile (how it rises, its faces, its top)");
                Need(h.Routes.Count > 0, $"{what}: the routes up, down or through it");
                if (h.Job == "view") Need(h.Sees.Count > 0, $"{what}: what is seen from it");
                Need(Has(h.Why), $"{what}: why it is here");
            }
            foreach (Reserve r in Reserved)
            {
                Id("reserve", r.Id);
                Need(r.Rect != null, $"reserve {r.Id}: its rect");
                InArea("reserve " + r.Id, r.Rect);
                Need(Has(r.For) && Has(r.Why), $"reserve {r.Id}: what it is kept for, and why here");
            }
            var tied = new HashSet<string>(Neighbours.Where(t => Has(t.Ties)).Select(t => t.Area), StringComparer.Ordinal);
            foreach (string n in NeighboursOf(Area, w))
                Need(tied.Contains(n), $"neighbouring area '{n}': no ties (neighbours)");
            return errors;
        }

        /// <summary>
        /// The regions as built against the plan: buildings missing or not where planned, and content
        /// counted by its object or npc inside its rect. Lines to read, not errors: the plan leads until
        /// stage 3 is done.
        /// </summary>
        public List<string> Built(IEnumerable<RegionData> regions)
        {
            var notes = new List<string>();
            List<Piece> pieces = regions.SelectMany(r => r.Pieces).ToList();
            foreach (Building b in Buildings)
            {
                Piece? p = pieces.FirstOrDefault(x => x.Id == b.Id);   // a building piece, a house, or a landmark drawn as a prop
                if (p == null) notes.Add($"building {b.Id}: not built");
                else if (b.Rect != null && p.Rect != null && !b.Rect.SequenceEqual(p.Rect))
                    notes.Add($"building {b.Id}: built at [{string.Join(", ", p.Rect)}], planned at [{string.Join(", ", b.Rect)}]");
            }
            foreach (Content c in Contents.Where(c => c.Rect != null))
            {
                int[] r = c.Rect!;
                int built = pieces.Count(p => p.At != null && p.X >= r[0] && p.Y >= r[1] && p.X < r[2] && p.Y < r[3] &&
                                              ((c.Object != null && p.Object == c.Object) || (c.Npc != null && p.Npc == c.Npc)));
                if (built != c.Count) notes.Add($"{c.Kind} {c.Id}: {built} of {c.Count} built");
            }
            foreach (Dungeon d in Dungeons.Where(d => d.Entrance != null && d.Object != null))
                if (!pieces.Any(p => p.Object == d.Object && p.At != null && Math.Abs(p.X - d.Entrance![0]) <= 2 && Math.Abs(p.Y - d.Entrance[1]) <= 2))
                    notes.Add($"dungeon {d.Id}: no '{d.Object}' at its entrance");
            return notes;
        }
    }
}
