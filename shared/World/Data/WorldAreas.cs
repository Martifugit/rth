using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// The world split into areas and the standard each is raised to (world/areas.json,
    /// docs/world-areas.md): an area is a few regions with one identity, the unit the world is
    /// built in; the standard is a short list of work packages per stage, ticked per area as they
    /// are done. Every region on the continent belongs to exactly one area. The tools check it,
    /// report where the world stands and draw it as a coverage map; the Editor opens an area's
    /// regions together.
    /// </summary>
    public sealed class WorldAreas
    {
        public const string FileName = "areas.json";

        public sealed class Package
        {
            /// <summary>"1.3": stage 1, the third package.</summary>
            public string Id = "";
            public string Name = "";
            /// <summary>When it counts as done.</summary>
            public string Done = "";
        }

        public sealed class Stage
        {
            public int Number;
            public string Name = "";
            /// <summary>What the stage gets an area to, in a sentence.</summary>
            public string Goal = "";
            public List<Package> Packages = new List<Package>();
        }

        public sealed class Area
        {
            public string Id = "";
            public string Name = "";
            /// <summary>The order areas are raised in, by wave; 0 is held back (frontiers, open sea).</summary>
            public int Wave;
            public string About = "";
            /// <summary>Its regions, by region id.</summary>
            public List<string> Regions = new List<string>();
            /// <summary>The packages ticked: package id to when (a date, and a word on it if useful).</summary>
            public SortedDictionary<string, string> Done = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public string Notes = "";
        }

        public string About = "";
        public List<Stage> Stages = new List<Stage>();
        public List<Area> Areas = new List<Area>();

        public static string PathIn(string root) => Path.Combine(root, "world", FileName);

        public static WorldAreas Load(string path)
        {
            try
            {
                return JsonConvert.DeserializeObject<WorldAreas>(File.ReadAllText(path)) ?? throw new FormatException("empty file");
            }
            catch (JsonException e)
            {
                throw new FormatException(Path.GetFileName(path) + ": " + e.Message, e);
            }
        }

        public IEnumerable<Package> AllPackages => Stages.SelectMany(s => s.Packages);

        /// <summary>The highest stage whose packages are all done (and every one before it); 0: none yet.</summary>
        public int StageOf(Area a)
        {
            int reached = 0;
            foreach (Stage s in Stages.OrderBy(s => s.Number))
            {
                if (s.Packages.Count == 0 || s.Packages.Any(p => !a.Done.ContainsKey(p.Id))) break;
                reached = s.Number;
            }
            return reached;
        }

        /// <summary>The first package not yet done, in order; null when the area is finished.</summary>
        public Package? Next(Area a) => Stages.OrderBy(s => s.Number).SelectMany(s => s.Packages).FirstOrDefault(p => !a.Done.ContainsKey(p.Id));

        /// <summary>The area a region belongs to, or null.</summary>
        public Area? AreaOf(string regionId) => Areas.FirstOrDefault(a => a.Regions.Contains(regionId));

        /// <summary>
        /// What is wrong with the file against the regions that exist: a region in no area or in two,
        /// an area naming a region that doesn't exist, a ticked package the standard doesn't have.
        /// </summary>
        public List<string> Check(IEnumerable<string> regionIds)
        {
            var errors = new List<string>();
            var known = new HashSet<string>(regionIds, StringComparer.Ordinal);
            var owner = new Dictionary<string, string>(StringComparer.Ordinal);
            var packages = new HashSet<string>(AllPackages.Select(p => p.Id), StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Area a in Areas)
            {
                if (!ids.Add(a.Id)) errors.Add($"area {a.Id}: the id is used twice");
                foreach (string r in a.Regions)
                {
                    if (!known.Contains(r)) errors.Add($"area {a.Id}: region '{r}' doesn't exist (world/regions)");
                    else if (owner.TryGetValue(r, out string? other)) errors.Add($"region {r} is in two areas: {other} and {a.Id}");
                    else owner[r] = a.Id;
                }
                foreach (string p in a.Done.Keys)
                    if (!packages.Contains(p)) errors.Add($"area {a.Id}: '{p}' is not a package of the standard");
            }
            foreach (string r in known.Where(r => !owner.ContainsKey(r)).OrderBy(r => r, StringComparer.Ordinal))
                errors.Add($"region {r} is in no area: add it to one in world/{FileName}");
            return errors;
        }
    }
}
