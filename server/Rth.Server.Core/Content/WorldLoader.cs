using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rth.Shared.World;

namespace Rth.Server.Content
{
    /// <summary>
    /// Loads every region the map exporter wrote into one world, as Lost City does: all of it at
    /// startup, because collision is small. Per region, in the maps folder:
    ///   name.rthmap         collision (required)
    ///   name.region.json    scene, title, whether it has a spawn (optional; defaults from the name)
    ///   name.spawns.json    creature spawns (optional)
    ///   name.objects.json   interactive objects (optional)
    /// Unbuilt space between regions is simply absent, and so blocked.
    /// </summary>
    public static class WorldLoader
    {
        public sealed class Result
        {
            public WorldMap Map = new WorldMap();
            public MapPlacements Placements = new MapPlacements();
        }

        /// <param name="spawnRegion">Where new players start; if missing, the first region with a spawn.</param>
        public static Result Load(string directory, string? spawnRegion)
        {
            var result = new Result();
            string[] files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.rthmap") : Array.Empty<string>();
            Array.Sort(files, StringComparer.OrdinalIgnoreCase); // a stable order, whatever the file system does
            if (files.Length == 0) throw new FileNotFoundException("No maps (*.rthmap) in " + Path.GetFullPath(directory) + ". Export them from Unity (R'th > Export Map).");

            var loaded = new List<(WorldRegion Info, MapData Map)>();
            foreach (string file in files)
            {
                MapData map = MapData.Load(file);
                WorldRegion info = ReadInfo(file, map);
                result.Map.AddRegion(info, map);
                MapPlacements placements = MapPlacements.Load(file);
                result.Placements.Npcs.AddRange(placements.Npcs);
                result.Placements.Objects.AddRange(placements.Objects);
                loaded.Add((info, map));
            }

            MapData spawn = (loaded.FirstOrDefault(r => spawnRegion != null && string.Equals(r.Info.Name, spawnRegion, StringComparison.OrdinalIgnoreCase)).Map
                             ?? loaded.FirstOrDefault(r => r.Info.HasSpawn).Map
                             ?? loaded[0].Map);
            result.Map.SpawnPlane = spawn.SpawnPlane;
            result.Map.Spawn = spawn.Spawn;
            return result;
        }

        private static WorldRegion ReadInfo(string mapFile, MapData map)
        {
            string name = Path.GetFileNameWithoutExtension(mapFile);
            string infoFile = Path.ChangeExtension(mapFile, null) + ".region.json";
            WorldRegion info = File.Exists(infoFile)
                ? JsonConvert.DeserializeObject<WorldRegion>(File.ReadAllText(infoFile)) ?? new WorldRegion()
                : new WorldRegion { HasSpawn = true };
            info.Name = name;
            if (string.IsNullOrWhiteSpace(info.Scene)) info.Scene = Capitalise(name);
            if (string.IsNullOrWhiteSpace(info.Title)) info.Title = Capitalise(name).Replace('_', ' ');
            info.X = map.OriginX;
            info.Y = map.OriginY;
            info.Width = map.Width;
            info.Height = map.Height;
            return info;
        }

        private static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
