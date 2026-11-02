using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// The shared palette of named colours (Resources/Art/palette.json, docs/assets.md). Recolours
    /// name colours from it ("CopperOre") or give a #RRGGBB.
    /// </summary>
    public static class Palette
    {
        private static Dictionary<string, Color> _colours;
        private static HashSet<string> _souls;

        /// <summary>
        /// Colours marked <c>"soul": true</c> (SoulPale, SoulGlow): drawn with the glowing, see-through
        /// soul shader (Resources/Shaders/Soul) wherever they appear on a model.
        /// </summary>
        public static bool IsSoul(string name)
        {
            if (_colours == null) Load();
            return !string.IsNullOrWhiteSpace(name) && _souls.Contains(name.Trim());
        }

        public static IReadOnlyDictionary<string, Color> All
        {
            get
            {
                if (_colours == null) Load();
                return _colours;
            }
        }

        /// <summary>A palette name or "#RRGGBB".</summary>
        public static bool TryParse(string value, out Color colour)
        {
            colour = Color.white;
            if (string.IsNullOrWhiteSpace(value)) return false;
            value = value.Trim();
            if (value.StartsWith("#")) return ColorUtility.TryParseHtmlString(value, out colour);
            return All.TryGetValue(value, out colour);
        }

        private static void Load()
        {
            _colours = new Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase);
            _souls = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var file = Resources.Load<TextAsset>("Art/palette");
            if (file == null) return;
            foreach (JToken c in JObject.Parse(file.text)["colours"] ?? new JArray())
            {
                if (ColorUtility.TryParseHtmlString((string)c["hex"], out Color colour)) _colours[(string)c["name"]] = colour;
                if ((bool?)c["soul"] == true) _souls.Add((string)c["name"]);
            }
        }
    }
}
