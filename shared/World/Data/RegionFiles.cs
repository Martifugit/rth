using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// Reads and writes the world data files (world/regions/*.json) in one stable layout, so that
    /// whoever saves a region (an agent, the CLI, the Editor writing back a move) produces the same
    /// text and diffs show only what changed: one piece per line (its parts one per line under it),
    /// numbers rounded to four decimals, no empty fields.
    /// </summary>
    public static class RegionFiles
    {
        public const string RegionsFolder = "world/regions";
        public const string HandmadeSuffix = ".handmade.json";

        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            NullValueHandling = NullValueHandling.Ignore,
        });

        /// <summary>The region files in a regions folder (not the hand-made overlays), sorted.</summary>
        public static string[] RegionPaths(string regionsDirectory)
        {
            if (!Directory.Exists(regionsDirectory)) return Array.Empty<string>();
            string[] files = Directory.GetFiles(regionsDirectory, "*.json")
                .Where(f => !f.EndsWith(HandmadeSuffix, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files;
        }

        public static string HandmadePathFor(string regionPath) => Path.ChangeExtension(regionPath, null) + HandmadeSuffix;

        public static RegionData LoadRegion(string path) => ParseRegion(File.ReadAllText(path), path);

        public static RegionData ParseRegion(string json, string source = "region")
        {
            try
            {
                RegionData region = JsonConvert.DeserializeObject<RegionData>(json) ?? throw new FormatException("empty file");
                region.Pieces ??= new List<Piece>();
                region.Materials ??= new SortedDictionary<string, string>(StringComparer.Ordinal);
                // The parser sorts a new dictionary by the machine's culture ('-' after '+' on some);
                // sort legends by character, as the blockout writes them, so every machine formats alike.
                foreach (Piece piece in region.Pieces)
                    if (piece.Legend != null) piece.Legend = new SortedDictionary<string, TileKind>(piece.Legend, StringComparer.Ordinal);
                return region;
            }
            catch (JsonException e)
            {
                throw new FormatException(source + ": " + e.Message, e);
            }
        }

        public static HandmadeData? LoadHandmade(string regionPath)
        {
            string path = HandmadePathFor(regionPath);
            return File.Exists(path) ? JsonConvert.DeserializeObject<HandmadeData>(File.ReadAllText(path)) : null;
        }

        public static void SaveRegion(RegionData region, string path) => WriteIfChanged(path, Format(region));

        public static void SaveHandmade(HandmadeData handmade, string regionPath) => WriteIfChanged(HandmadePathFor(regionPath), Format(handmade));

        /// <summary>Writes only when the text differs, so tools that save everything leave untouched files alone.</summary>
        public static bool WriteIfChanged(string path, string text)
        {
            if (File.Exists(path) && File.ReadAllText(path) == text) return false;
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return true;
        }

        // ------------------------------------------------------------------ layout

        public static string Format(RegionData region)
        {
            JObject root = JObject.FromObject(region, Serializer);
            var sb = new StringBuilder("{\n");
            var lines = new List<string>();
            foreach (JProperty p in root.Properties())
            {
                if (p.Name == "pieces") continue;
                if (p.Name == "terrain")
                {
                    // The heights one row to a line, so they read (and diff) as a picture of the ground.
                    var t = (JObject)p.Value;
                    var rowList = (JArray?)t["rows"] ?? new JArray();
                    t.Remove("rows");
                    string head = string.Join(", ", t.Properties().Select(q => Q(q.Name) + ": " + Compact(q.Value)));
                    lines.Add("  \"terrain\": {" + (head.Length > 0 ? " " + head + "," : "") + " \"rows\": [" +
                              (rowList.Count == 0 ? "]" : "\n" + string.Join(",\n", rowList.Select(r => "    " + Compact(r))) + "\n  ]") + " }");
                    continue;
                }
                if (p.Name == "materials")
                {
                    var mats = ((JObject)p.Value).Properties().Select(m => "    " + Q(m.Name) + ": " + Compact(m.Value)).ToList();
                    lines.Add("  \"materials\": {" + (mats.Count == 0 ? "}" : "\n" + string.Join(",\n", mats) + "\n  }"));
                    continue;
                }
                lines.Add("  " + Q(p.Name) + ": " + Compact(p.Value));
            }
            var pieces = new List<string>();
            foreach (JObject piece in ((JArray)root["pieces"]!).Cast<JObject>())
            {
                JToken? parts = piece["parts"];
                piece.Remove("parts");
                JToken? rows = piece["rows"];
                piece.Remove("rows");
                string head = "    { " + string.Join(", ", piece.Properties().Select(p => Q(p.Name) + ": " + Compact(p.Value)));
                if (parts is JArray list && list.Count > 0)
                    head += ", \"parts\": [\n" + string.Join(",\n", list.Select(part => "      " + Compact(part))) + "\n    ]";
                // A tiles piece's rows go one to a line, so its ground reads (and diffs) as a picture.
                if (rows is JArray rowList)
                    head += ", \"rows\": [" + (rowList.Count == 0 ? "]" : "\n" + string.Join(",\n", rowList.Select(row => "      " + Compact(row))) + "\n    ]");
                head += " }";
                pieces.Add(head);
            }
            lines.Add("  \"pieces\": [" + (pieces.Count == 0 ? "]" : "\n" + string.Join(",\n", pieces) + "\n  ]"));
            sb.Append(string.Join(",\n", lines)).Append("\n}\n");
            return sb.ToString();
        }

        public static string Format(HandmadeData handmade)
        {
            JObject root = JObject.FromObject(handmade, Serializer);
            var lines = new List<string>();
            foreach (JProperty p in root.Properties())
            {
                if (p.Name == "planes")
                {
                    var planes = new List<string>();
                    foreach (JObject plane in ((JArray)p.Value).Cast<JObject>())
                    {
                        string rows = string.Join(",\n", ((JArray)plane["rows"]!).Select(r => "        " + Compact(r)));
                        string walls = string.Join(",\n", ((JArray)plane["walls"]!).Select(w => "        " + Compact(w)));
                        planes.Add("    {\n      \"plane\": " + plane["plane"] +
                                   ",\n      \"rows\": [" + (rows.Length == 0 ? "]" : "\n" + rows + "\n      ]") +
                                   ",\n      \"walls\": [" + (walls.Length == 0 ? "]" : "\n" + walls + "\n      ]") + "\n    }");
                    }
                    lines.Add("  \"planes\": [" + (planes.Count == 0 ? "]" : "\n" + string.Join(",\n", planes) + "\n  ]"));
                    continue;
                }
                if (p.Value is JArray array && array.Count > 0 && array[0] is JObject)
                {
                    lines.Add("  " + Q(p.Name) + ": [\n" + string.Join(",\n", array.Select(e => "    " + Compact(e))) + "\n  ]");
                    continue;
                }
                lines.Add("  " + Q(p.Name) + ": " + Compact(p.Value));
            }
            return "{\n" + string.Join(",\n", lines) + "\n}\n";
        }

        private static string Q(string s) => JsonConvert.ToString(s);

        /// <summary>One line of JSON, with numbers written the same on every runtime.</summary>
        public static string Compact(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var props = ((JObject)token).Properties().Select(p => Q(p.Name) + ": " + Compact(p.Value)).ToList();
                    return props.Count == 0 ? "{}" : "{ " + string.Join(", ", props) + " }";
                case JTokenType.Array:
                    return "[" + string.Join(", ", ((JArray)token).Select(Compact)) + "]";
                case JTokenType.Integer:
                    return ((long)token).ToString(CultureInfo.InvariantCulture);
                case JTokenType.Float:
                    return Number((double)token);
                case JTokenType.String:
                    return Q((string)token!);
                case JTokenType.Boolean:
                    return (bool)token ? "true" : "false";
                case JTokenType.Null:
                    return "null";
                default:
                    return token.ToString(Formatting.None);
            }
        }

        /// <summary>Rounded to four decimals, no trailing zeros, "-0" as "0".</summary>
        public static string Number(double value)
        {
            double r = Math.Round(value, 4, MidpointRounding.AwayFromZero);
            if (r == 0) r = 0;
            return r.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
