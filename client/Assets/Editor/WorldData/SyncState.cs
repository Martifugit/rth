using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Rth.Shared.World.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rth.Client.Editor
{
    /// <summary>
    /// What Sync World last finished, per region, on this machine (client/Library/RthSync.json, not
    /// committed): a fingerprint of the region's data, its hand-made file, its scene and the code
    /// that draws it. A region whose fingerprint still matches, and whose server maps are up to date,
    /// has nothing to sync and is skipped. Pulling someone's changes, editing data, saving a scene or
    /// changing the drawing code all change the fingerprint; Sync World (everything) ignores it.
    /// </summary>
    internal sealed class SyncState
    {
        private static string StatePath => Path.Combine(Application.dataPath, "..", "Library", "RthSync.json");

        private Dictionary<string, string> _done = new Dictionary<string, string>();
        private string _code;

        public static SyncState Load()
        {
            var state = new SyncState();
            try
            {
                if (File.Exists(StatePath))
                    state._done = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(StatePath)) ?? new Dictionary<string, string>();
            }
            catch (JsonException)
            {
                // A broken state file only means everything syncs once.
            }
            return state;
        }

        public void Save() => File.WriteAllText(StatePath, JsonConvert.SerializeObject(_done, Formatting.Indented));

        /// <summary>True if the region is as Sync World left it: same inputs, maps up to date, scene saved.</summary>
        public bool Unchanged(WorldWorkspace.Entry entry, WorldWorkspace ws)
        {
            if (!_done.TryGetValue(entry.Region.Id, out string done) || done != Fingerprint(entry)) return false;
            Scene open = SceneManager.GetSceneByPath(entry.Region.Scene);
            if (open.IsValid() && open.isDirty) return false;
            return !ws.Stale(entry).Any();
        }

        /// <summary>Remembers the region as synced, with its files as they are now.</summary>
        public void Record(WorldWorkspace.Entry entry) => _done[entry.Region.Id] = Fingerprint(entry);

        public void Forget(string id) => _done.Remove(id);

        private string Fingerprint(WorldWorkspace.Entry entry)
        {
            string scene = Path.Combine(Application.dataPath, "..", entry.Region.Scene);
            if (!File.Exists(scene)) return "no scene";
            using (var sha = SHA1.Create())
            {
                var all = new StringBuilder(Code());
                all.Append('|').Append(Hash(sha, Text(entry.Path)));
                all.Append('|').Append(Hash(sha, Text(RegionFiles.HandmadePathFor(entry.Path))));
                all.Append('|').Append(Hash(sha, File.ReadAllBytes(scene)));
                return Hash(sha, Encoding.UTF8.GetBytes(all.ToString()));
            }
        }

        /// <summary>The code and colours that decide how data is drawn: a change to them redraws every region.</summary>
        private string Code()
        {
            if (_code != null) return _code;
            string assets = Application.dataPath;
            var files = new List<string>();
            void Add(string dir, string pattern, SearchOption depth)
            {
                if (Directory.Exists(dir)) files.AddRange(Directory.GetFiles(dir, pattern, depth));
            }
            Add(Path.Combine(assets, "Editor", "WorldData"), "*.cs", SearchOption.AllDirectories);
            Add(Path.Combine(assets, "Editor"), "WorldKit*.cs", SearchOption.TopDirectoryOnly);
            Add(Path.Combine(assets, "Editor"), "LeafyTree.cs", SearchOption.TopDirectoryOnly);
            Add(Path.Combine(assets, "Scripts", "View"), "TileGround.cs", SearchOption.TopDirectoryOnly);
            Add(Path.Combine(assets, "Resources", "Art"), "palette.json", SearchOption.TopDirectoryOnly);
            Add(Path.Combine(WorldSync.RepoRoot, "tools", "unity-map-tools"), "*.cs", SearchOption.AllDirectories);
            using (var sha = SHA1.Create())
            {
                var all = new StringBuilder();
                foreach (string f in files.OrderBy(f => f, System.StringComparer.Ordinal))
                    all.Append(Path.GetFileName(f)).Append('=').Append(Hash(sha, Text(f))).Append(';');
                return _code = Hash(sha, Encoding.UTF8.GetBytes(all.ToString()));
            }
        }

        /// <summary>A text file's bytes with line endings made alike, so a checkout's CRLF doesn't count as a change.</summary>
        private static byte[] Text(string path) =>
            File.Exists(path) ? Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n", "\n")) : new byte[0];

        private static string Hash(HashAlgorithm sha, byte[] bytes) =>
            string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
    }
}
