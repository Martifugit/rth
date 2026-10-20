using System;
using System.Linq;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Server.Net;
using Rth.Server.Persistence;
using Rth.Server.Timing;
using Rth.Shared.World;

namespace Rth.Server
{
    /// <summary>
    /// A running server: map, world, network listener and tick loop. Hosted by the console app
    /// for real play, and by the Unity Editor (on background threads) during development.
    /// </summary>
    public sealed class ServerHost : IDisposable
    {
        private readonly ServerConfig _config;
        private NetworkServer? _network;
        private TickLoop? _loop;
        private ContentWatcher? _watcher;

        public ServerHost(ServerConfig config)
        {
            _config = config;
        }

        public World? World { get; private set; }
        public bool IsRunning { get; private set; }

        /// <summary>The port actually listened on.</summary>
        public int Port => _network?.Port ?? 0;

        public void Start()
        {
            if (IsRunning) throw new InvalidOperationException("Server already running.");
            MapData map = MapData.Load(_config.MapPath);
            GameContent content = GameContent.Load(_config.ContentDirectory);
            MapPlacements placements = MapPlacements.Load(_config.MapPath);
            var store = new JsonPlayerStore(_config.SaveDirectory);
            var world = new World(_config, map, store, content, placements);

            var network = new NetworkServer(_config.Port, world.AcceptConnection, _config.Log);
            network.Start();

            World = world;
            _network = network;
            if (_config.HotReloadContent) _watcher = new ContentWatcher(_config.ContentDirectory, world.QueueContent, _config.Log);
            _loop = new TickLoop(world.Tick, TimeSpan.FromMilliseconds(_config.TickMillis), new SystemClock(), _config.Log);
            _loop.Start();
            IsRunning = true;
            _config.Log("Server listening on port " + Port + ", map " + map.Width + "x" + map.Height + ", " +
                        world.Npcs.Count() + " NPCs, tick " + _config.TickMillis + " ms.");
        }

        /// <summary>Stops accepting players, finishes the current tick, then saves and disconnects everyone.</summary>
        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            _network?.Stop();
            _watcher?.Dispose();
            _watcher = null;
            _loop?.Stop();
            World?.Shutdown();
            _config.Log("Server stopped.");
        }

        public void Dispose() => Stop();
    }
}
