using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// The world data in a checkout of the repository: every region file, compiled, checked against
    /// the game content, and exported to the server's maps folder. The CLI (tools/world), the tests
    /// and Sync World all go through this, so they agree on what "valid" and "up to date" mean.
    /// </summary>
    public sealed class WorldWorkspace
    {
        public sealed class Entry
        {
            public string Path = "";
            public RegionData Region = new RegionData();
            public HandmadeData? Handmade;
            public RegionCompiler.Result Compiled = new RegionCompiler.Result();
        }

        public string Root { get; }
        public string RegionsDirectory => System.IO.Path.Combine(Root, "world", "regions");
        public string MapsDirectory => System.IO.Path.Combine(Root, "server", "data", "maps");
        public string ContentDirectory => System.IO.Path.Combine(Root, "server", "data", "content");
        public List<Entry> Regions { get; } = new List<Entry>();
        /// <summary>Files that couldn't be read at all.</summary>
        public List<string> LoadErrors { get; } = new List<string>();

        private WorldWorkspace(string root) => Root = root;

        /// <summary>
        /// Loads and compiles every region under <paramref name="root"/>/world/regions, or only the
        /// one called <paramref name="only"/> (saving or exporting one region in the Editor needn't
        /// compile the whole world).
        /// </summary>
        public static WorldWorkspace Load(string root, string? only = null)
        {
            var ws = new WorldWorkspace(System.IO.Path.GetFullPath(root));
            foreach (string path in RegionFiles.RegionPaths(ws.RegionsDirectory))
            {
                if (only != null && !string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), only, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var entry = new Entry { Path = path, Region = RegionFiles.LoadRegion(path), Handmade = RegionFiles.LoadHandmade(path) };
                    entry.Compiled = RegionCompiler.Compile(entry.Region, entry.Handmade);
                    ws.Regions.Add(entry);
                }
                catch (Exception e) when (e is FormatException || e is IOException || e is Newtonsoft.Json.JsonException)
                {
                    ws.LoadErrors.Add(System.IO.Path.GetFileName(path) + ": " + e.Message);
                }
            }
            return ws;
        }

        /// <summary>The repository root above <paramref name="start"/> (the folder holding Rth.slnx), or null.</summary>
        public static string? FindRoot(string start)
        {
            for (DirectoryInfo? d = new DirectoryInfo(start); d != null; d = d.Parent)
                if (File.Exists(System.IO.Path.Combine(d.FullName, "Rth.slnx"))) return d.FullName;
            return null;
        }

        public Entry? Find(string id) => Regions.Find(e => string.Equals(e.Region.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>Writes every region's map files. Returns the files that changed.</summary>
        public List<string> Export()
        {
            var changed = new List<string>();
            Directory.CreateDirectory(MapsDirectory);
            foreach (Entry e in Regions)
            {
                if (e.Compiled.Errors.Count > 0) continue;
                foreach (KeyValuePair<string, byte[]> file in RegionCompiler.Files(e.Compiled))
                {
                    string path = System.IO.Path.Combine(MapsDirectory, file.Key);
                    if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(file.Value)) continue;
                    File.WriteAllBytes(path, file.Value);
                    changed.Add(path);
                }
            }
            return changed;
        }

        /// <summary>
        /// Everything wrong with the world data: unreadable files, compile errors, ids the content
        /// doesn't know, overlapping regions, and map files that are out of date with the data.
        /// </summary>
        public List<string> Check(List<string>? warnings = null)
        {
            var errors = new List<string>(LoadErrors);
            HashSet<string> objectIds = ContentIds("objects.json");
            HashSet<string> npcIds = ContentIds("npcs.json");
            var regionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Entry e in Regions)
            {
                string name = e.Region.Id;
                if (!regionIds.Add(name)) errors.Add($"Two regions are called '{name}'.");
                string expected = System.IO.Path.GetFileNameWithoutExtension(e.Path);
                if (!string.Equals(expected, name, StringComparison.Ordinal)) errors.Add($"{System.IO.Path.GetFileName(e.Path)}: its id is '{name}'; name the file {name}.json.");
                errors.AddRange(e.Compiled.Errors.Select(m => name + ": " + m));
                warnings?.AddRange(e.Compiled.Warnings.Select(m => name + ": " + m));
                foreach (ObjectEntry o in e.Compiled.Objects)
                    if (objectIds.Count > 0 && !objectIds.Contains(o.Type)) errors.Add($"{name}: object '{o.Type}' at ({o.X}, {o.Y}) isn't in objects.json.");
                foreach (NpcEntry n in e.Compiled.Npcs)
                    if (npcIds.Count > 0 && !npcIds.Contains(n.Npc)) errors.Add($"{name}: npc '{n.Npc}' at ({n.X}, {n.Y}) isn't in npcs.json.");
                if (e.Compiled.Errors.Count == 0) errors.AddRange(Stale(e));
            }
            // Maps without data were exported straight from a scene the old way (or are left over).
            if (warnings != null && Directory.Exists(MapsDirectory))
                foreach (string map in Directory.GetFiles(MapsDirectory, "*.rthmap"))
                {
                    string id = System.IO.Path.GetFileNameWithoutExtension(map);
                    if (Find(id) == null)
                        warnings.Add($"server/data/maps/{id}.rthmap has no world/regions/{id}.json: it comes from a scene, not the data (convert it, or delete it if it's left over).");
                }
            for (int i = 0; i < Regions.Count; i++)
                for (int j = i + 1; j < Regions.Count; j++)
                {
                    RegionData a = Regions[i].Region, b = Regions[j].Region;
                    if (a.OriginX < b.OriginX + b.Width && b.OriginX < a.OriginX + a.Width && a.OriginY < b.OriginY + b.Height && b.OriginY < a.OriginY + a.Height)
                        errors.Add($"Regions '{a.Id}' and '{b.Id}' overlap.");
                    else if (warnings != null && Seam(Regions[i], Regions[j]) is string seam) warnings.Add(seam);
                }
            return errors;
        }

        /// <summary>
        /// Where two regions side by side disagree on the height of the corners along their shared
        /// border (a region without terrain is flat at 0): the ground would step there. Null if they
        /// agree, aren't neighbours, or neither has terrain.
        /// </summary>
        public static string? Seam(Entry ea, Entry eb)
        {
            RegionData a = ea.Region, b = eb.Region;
            Heightfield? ta = ea.Compiled.Terrain, tb = eb.Compiled.Terrain;
            if (ta == null && tb == null || a.Instanced || b.Instanced || a.Plane != b.Plane) return null;
            var corners = new List<(int x, int y)>();
            void Edge(int x0, int y0, int x1, int y1)
            {
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++) corners.Add((x, y));
            }
            int ax1 = a.OriginX + a.Width, ay1 = a.OriginY + a.Height, bx1 = b.OriginX + b.Width, by1 = b.OriginY + b.Height;
            int oy0 = Math.Max(a.OriginY, b.OriginY), oy1 = Math.Min(ay1, by1), ox0 = Math.Max(a.OriginX, b.OriginX), ox1 = Math.Min(ax1, bx1);
            if ((ax1 == b.OriginX || bx1 == a.OriginX) && oy0 < oy1) Edge(ax1 == b.OriginX ? ax1 : a.OriginX, oy0, ax1 == b.OriginX ? ax1 : a.OriginX, oy1);
            else if ((ay1 == b.OriginY || by1 == a.OriginY) && ox0 < ox1) Edge(ox0, ay1 == b.OriginY ? ay1 : a.OriginY, ox1, ay1 == b.OriginY ? ay1 : a.OriginY);
            else return null;
            int differ = 0;
            (int x, int y, float ha, float hb) first = default;
            foreach ((int x, int y) in corners)
            {
                float ha = ta?.Corner(x, y) ?? 0f, hb = tb?.Corner(x, y) ?? 0f;
                if (Math.Abs(ha - hb) < 0.005f) continue;
                if (differ++ == 0) first = (x, y, ha, hb);
            }
            return differ == 0 ? null
                : FormattableString.Invariant($"The ground steps at the border of '{a.Id}' and '{b.Id}': {differ} corners differ, the first at ({first.x}, {first.y}): {first.ha:0.##} against {first.hb:0.##}.");
        }

        /// <summary>Map files of a region that differ from what its data compiles to.</summary>
        public IEnumerable<string> Stale(Entry e)
        {
            foreach (KeyValuePair<string, byte[]> file in RegionCompiler.Files(e.Compiled))
            {
                string path = System.IO.Path.Combine(MapsDirectory, file.Key);
                if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(file.Value))
                    yield return $"server/data/maps/{file.Key} is out of date with world/regions/{e.Region.Id}.json: run `dotnet run --project tools/world -- export`.";
            }
        }

        private HashSet<string> ContentIds(string file)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            string path = System.IO.Path.Combine(ContentDirectory, file);
            if (!File.Exists(path)) return ids;
            foreach (JToken t in JArray.Parse(File.ReadAllText(path)))
                if (t["id"] is JValue id && id.Value is string s) ids.Add(s);
            return ids;
        }
    }
}
