using System;
using System.IO;
using System.Threading;

namespace Rth.Server.Content
{
    /// <summary>
    /// Development hot-reload: watches the content folder and, a moment after the last change,
    /// loads the files on a background thread and hands valid content to the world. Broken edits
    /// are logged and ignored, so the running game keeps its last good content.
    /// </summary>
    public sealed class ContentWatcher : IDisposable
    {
        private const int SettleMillis = 300;

        private readonly string _directory;
        private readonly Action<GameContent> _onLoaded;
        private readonly Action<string> _log;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _timer;

        public ContentWatcher(string directory, Action<GameContent> onLoaded, Action<string> log)
        {
            _directory = directory;
            _onLoaded = onLoaded;
            _log = log;
            _timer = new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
            _watcher = new FileSystemWatcher(directory, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Renamed += OnChanged;
            _watcher.EnableRaisingEvents = true;
        }

        // Editors often write a file in several steps; wait for things to settle.
        private void OnChanged(object sender, FileSystemEventArgs e) => _timer.Change(SettleMillis, Timeout.Infinite);

        private void Reload()
        {
            try
            {
                GameContent content = GameContent.Load(_directory);
                _onLoaded(content);
                _log("Content reloaded.");
            }
            catch (Exception e) when (e is ContentException || e is IOException)
            {
                _log("Content reload failed, keeping the old content:" + Environment.NewLine + e.Message);
            }
        }

        public void Dispose()
        {
            _watcher.Dispose();
            _timer.Dispose();
        }
    }
}
