using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Rth.Shared.World.Layout
{
    /// <summary>
    /// The layout in a checkout (docs/layout.md): world/layout/continent.json in; the zone grid
    /// (zones.json, the terrain bake's input) and the region table (regions.txt) out. Built regions
    /// come from world/regions (instanced ones aside) and the area plan's targets from
    /// docs/areas/targets.json, so both have one source.
    /// </summary>
    public sealed class LayoutWorkspace
    {
        public string Root = "";
        public ContinentData Data = new ContinentData();
        public ContinentMap Map = new ContinentMap();
        public LayoutReport Report = new LayoutReport();
        public List<BuiltRegion> Built = new List<BuiltRegion>();

        public string Directory => Path.Combine(Root, "world", "layout");
        public string ContinentPath => Path.Combine(Directory, "continent.json");
        public string ZonesPath => Path.Combine(Directory, "zones.json");
        public string RegionsPath => Path.Combine(Directory, "regions.txt");

        public static LayoutWorkspace Load(string root)
        {
            var ws = new LayoutWorkspace { Root = root };
            ws.Data = ContinentData.Parse(File.ReadAllText(ws.ContinentPath));
            ApplyAreaTargets(ws.Data, Path.Combine(root, "docs", "areas", "targets.json"));
            ws.Built = BuiltRegions(Path.Combine(root, "world", "regions"));
            ws.Map = ContinentCompiler.Compile(ws.Data);
            ws.Report = LayoutReport.Run(ws.Map, ws.Built);
            CheckPalette(ws, Path.Combine(root, "client", "Assets", "Resources", "Art", "palette.json"));
            return ws;
        }

        /// <summary>Every ground's bake colour must be a palette name, or the bake paints it magenta.</summary>
        private static void CheckPalette(LayoutWorkspace ws, string path)
        {
            if (!File.Exists(path)) return;
            var names = new HashSet<string>((JObject.Parse(File.ReadAllText(path))["colours"] ?? new JArray()).Select(c => (string?)c["name"] ?? ""));
            var missing = ws.Data.Grounds.Where(g => !names.Contains(g.Value.Palette)).Select(g => $"{g.Key} ({g.Value.Palette})").ToList();
            ws.Report.Results.Add(new CheckResult
            {
                Id = "palette",
                Status = missing.Count == 0 ? CheckStatus.Ok : CheckStatus.Error,
                Text = missing.Count == 0 ? "every ground's colour is in the art palette" : "grounds whose palette colour doesn't exist: " + string.Join(", ", missing),
            });
        }

        /// <summary>The generated files, keyed by path, as they should be on disk.</summary>
        public Dictionary<string, string> Outputs()
        {
            var files = new Dictionary<string, string>();
            if (Map.Count == 0) return files;
            files[ZonesPath] = Map.ToLayoutJson();
            files[RegionsPath] = Report.RegionTable(Map).Replace("\r\n", "\n");
            return files;
        }

        /// <summary>Generated files whose content on disk differs from what the layout gives.</summary>
        public List<string> Stale() =>
            Outputs().Where(f => !File.Exists(f.Key) || File.ReadAllText(f.Key).Replace("\r\n", "\n") != f.Value).Select(f => f.Key).ToList();

        public List<string> Write()
        {
            var written = new List<string>();
            foreach (string path in Stale())
            {
                File.WriteAllText(path, Outputs()[path]);
                written.Add(path);
            }
            return written;
        }

        private static void ApplyAreaTargets(ContinentData data, string path)
        {
            if (!File.Exists(path)) return;
            JObject targets = JObject.Parse(File.ReadAllText(path));
            foreach (JToken t in targets["targets"] ?? new JArray())
            {
                string id = (string?)t["id"] ?? "";
                JToken? range = t["range"];
                if (range == null) continue;
                float[] Pair(JToken r) => new[] { (float)r[0]!, (float)r[1]! };
                switch (id)
                {
                    case "T-LADDER":
                        data.Targets.Towns = Pair(range["towns"]!);
                        data.Targets.Villages = Pair(range["villages"]!);
                        break;
                    case "T-LAND-BUILT":
                        data.Targets.Built = Pair(range);
                        break;
                    case "T-CITY-DOMINANCE":
                        data.Targets.CityDominance = Pair(range);
                        break;
                }
            }
        }

        private static List<BuiltRegion> BuiltRegions(string dir)
        {
            var built = new List<BuiltRegion>();
            if (!System.IO.Directory.Exists(dir)) return built;
            foreach (string file in System.IO.Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                if (file.EndsWith(".handmade.json", StringComparison.Ordinal)) continue;
                JObject r = JObject.Parse(File.ReadAllText(file));
                // Instanced places lie off the map; generated regions (the blockout) are drawn from the layout, not built.
                if ((bool?)r["instanced"] == true || r["generated"] != null) continue;
                int x = (int)r["origin"]![0]!, y = (int)r["origin"]![1]!;
                built.Add(new BuiltRegion { Id = (string?)r["id"] ?? Path.GetFileNameWithoutExtension(file), X0 = x, Y0 = y, X1 = x + (int)r["size"]![0]!, Y1 = y + (int)r["size"]![1]! });
            }
            return built;
        }
    }
}
