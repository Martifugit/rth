using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Rth.Shared.World;

namespace Rth.Server.Content
{
    /// <summary>
    /// Everything the map exporter places besides collision: creature spawns and interactive
    /// objects, read from the files beside the map (test.spawns.json, test.objects.json).
    /// A new kind of placement gets a field here and a file from the exporter.
    /// </summary>
    public sealed class MapPlacements
    {
        public List<NpcSpawn> Npcs = new List<NpcSpawn>();
        public List<PlacedObject> Objects = new List<PlacedObject>();

        public static MapPlacements Load(string mapPath)
        {
            string stem = Path.ChangeExtension(mapPath, null);
            return new MapPlacements
            {
                Npcs = ReadList<NpcSpawn>(stem + ".spawns.json"),
                Objects = ReadList<PlacedObject>(stem + ".objects.json"),
            };
        }

        private static List<T> ReadList<T>(string path) =>
            File.Exists(path) ? JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path)) ?? new List<T>() : new List<T>();
    }
}
