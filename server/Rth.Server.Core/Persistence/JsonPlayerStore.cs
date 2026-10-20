using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Rth.Server.Persistence
{
    /// <summary>
    /// One human-readable JSON file per player. Writes go to a temporary file that then replaces
    /// the old save, so a crash mid-write never leaves a half-written save behind.
    /// </summary>
    public sealed class JsonPlayerStore : IPlayerStore
    {
        private readonly string _directory;

        public JsonPlayerStore(string directory)
        {
            _directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(_directory);
        }

        // Windows refuses these as file names whatever the extension.
        private static readonly string[] ReservedNames =
        {
            "con", "prn", "aux", "nul",
            "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
            "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
        };

        private string PathFor(string key)
        {
            string file = System.Array.IndexOf(ReservedNames, key) >= 0 ? "_" + key : key;
            return Path.Combine(_directory, file + ".json");
        }

        public PlayerSave? Load(string key)
        {
            string path = PathFor(key);
            if (!File.Exists(path)) return null;
            return JsonConvert.DeserializeObject<PlayerSave>(File.ReadAllText(path, Encoding.UTF8));
        }

        public void Save(string key, PlayerSave save)
        {
            string path = PathFor(key);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(save, Formatting.Indented), Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
    }
}
