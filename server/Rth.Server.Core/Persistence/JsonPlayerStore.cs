using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace Rth.Server.Persistence
{
    /// <summary>
    /// One human-readable JSON file per player. Writes go to a temporary file that then replaces
    /// the old save, so a crash mid-write never leaves a half-written save behind.
    /// </summary>
    /// <remarks>
    /// With <c>writeBehind</c>, a save is turned into text on the caller's thread (so it is a
    /// consistent snapshot of the tick) and written to disk by a background thread, so an
    /// autosave of everyone never stalls the tick on file IO. A load sees a save still waiting to
    /// be written; <see cref="Flush"/> waits for every write (shutdown calls it through
    /// <see cref="Dispose"/>).
    /// </remarks>
    public sealed class JsonPlayerStore : IPlayerStore, IDisposable
    {
        private readonly string _directory;
        private readonly bool _writeBehind;
        private readonly Action<string> _log;

        private readonly object _lock = new object();
        private readonly Dictionary<string, string> _pending = new Dictionary<string, string>();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly ManualResetEvent _idle = new ManualResetEvent(true);
        private Thread? _writer;
        private volatile bool _stopping;

        public JsonPlayerStore(string directory, bool writeBehind = false, Action<string>? log = null)
        {
            _directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(_directory);
            _writeBehind = writeBehind;
            _log = log ?? (_ => { });
            if (_writeBehind)
            {
                _writer = new Thread(WriteLoop) { IsBackground = true, Name = "Rth save writer" };
                _writer.Start();
            }
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
            string file = Array.IndexOf(ReservedNames, key) >= 0 ? "_" + key : key;
            return Path.Combine(_directory, file + ".json");
        }

        public PlayerSave? Load(string key)
        {
            lock (_lock)
            {
                if (_pending.TryGetValue(key, out string? waiting)) return JsonConvert.DeserializeObject<PlayerSave>(waiting);
            }
            string path = PathFor(key);
            if (!File.Exists(path)) return null;
            return JsonConvert.DeserializeObject<PlayerSave>(File.ReadAllText(path, Encoding.UTF8));
        }

        public void Save(string key, PlayerSave save)
        {
            string json = JsonConvert.SerializeObject(save, Formatting.Indented);
            if (!_writeBehind || _stopping)
            {
                Write(key, json);
                return;
            }
            lock (_lock)
            {
                _pending[key] = json; // a newer save of the same player replaces one not yet written
                _idle.Reset();
            }
            _wake.Set();
        }

        /// <summary>Blocks until every save handed over so far is on disk.</summary>
        public void Flush()
        {
            if (!_writeBehind) return;
            _wake.Set();
            _idle.WaitOne();
        }

        public void Dispose()
        {
            if (_writer == null) return;
            Flush();
            _stopping = true;
            _wake.Set();
            _writer.Join();
            _writer = null;
        }

        private void WriteLoop()
        {
            while (true)
            {
                _wake.WaitOne();
                while (true)
                {
                    KeyValuePair<string, string> next;
                    lock (_lock)
                    {
                        if (_pending.Count == 0)
                        {
                            _idle.Set();
                            break;
                        }
                        using (Dictionary<string, string>.Enumerator e = _pending.GetEnumerator())
                        {
                            e.MoveNext();
                            next = e.Current;
                        }
                    }
                    try
                    {
                        Write(next.Key, next.Value);
                    }
                    catch (Exception e)
                    {
                        _log("Failed to write the save of " + next.Key + ": " + e.Message);
                    }
                    lock (_lock)
                    {
                        // Only forget it if no newer save arrived while it was being written.
                        if (_pending.TryGetValue(next.Key, out string? now) && ReferenceEquals(now, next.Value)) _pending.Remove(next.Key);
                    }
                }
                if (_stopping) return;
            }
        }

        private void Write(string key, string json)
        {
            string path = PathFor(key);
            string temp = path + ".tmp";
            File.WriteAllText(temp, json, Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
    }
}
