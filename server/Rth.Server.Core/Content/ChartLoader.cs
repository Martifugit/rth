using System;
using System.IO;
using Newtonsoft.Json;
using Rth.Shared.World.Layout;

namespace Rth.Server.Content
{
    /// <summary>
    /// Loads the players' world map from the continent layout (world/layout/continent.json,
    /// docs/layout.md): compiled at startup, so the map always matches the layout the world is
    /// built from. A missing or broken layout only costs the world map, never the server.
    /// </summary>
    public static class ChartLoader
    {
        public const string LayoutFile = "continent.json";

        public static WorldChart? Load(string? directory, Action<string> log)
        {
            if (string.IsNullOrEmpty(directory)) return null;
            string path = Path.Combine(directory, LayoutFile);
            if (!File.Exists(path))
            {
                log("No continent layout at " + Path.GetFullPath(path) + ": players get no world map.");
                return null;
            }
            try
            {
                ContinentMap map = ContinentCompiler.Compile(ContinentData.Parse(File.ReadAllText(path)));
                if (map.Count == 0)
                {
                    log("The continent layout doesn't compile (" + string.Join("; ", map.Errors) + "): players get no world map.");
                    return null;
                }
                WorldChart chart = WorldChart.From(map);
                log("World map: " + chart.WidthTiles + " x " + chart.HeightTiles + " tiles, " + chart.Places.Count + " places.");
                return chart;
            }
            catch (Exception e) when (e is JsonException || e is FormatException || e is IOException || e is ArgumentException)
            {
                log("The continent layout can't be read (" + e.Message + "): players get no world map.");
                return null;
            }
        }
    }
}
