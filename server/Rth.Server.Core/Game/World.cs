using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Net;
using Rth.Server.Persistence;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// The whole game state. Everything except <see cref="AcceptConnection"/> runs on the tick
    /// thread; <see cref="Tick"/> advances the world by exactly one tick.
    /// </summary>
    public sealed class World
    {
        private readonly ServerConfig _config;
        private readonly IPlayerStore _store;
        private readonly Action<string> _log;

        private readonly ConcurrentQueue<IConnection> _newConnections = new ConcurrentQueue<IConnection>();
        private readonly List<IConnection> _pendingConnections = new List<IConnection>();
        private readonly List<(IConnection Connection, LoginMessage Login)> _loginQueue = new List<(IConnection, LoginMessage)>();
        private readonly Dictionary<int, Player> _players = new Dictionary<int, Player>();
        private int _nextPlayerId = 1;

        public World(ServerConfig config, MapData map, IPlayerStore store)
        {
            _config = config;
            _store = store;
            _log = config.Log;
            Map = map;
        }

        public MapData Map { get; }
        public uint CurrentTick { get; private set; }
        public IEnumerable<Player> Players => _players.Values;
        public int PlayerCount => _players.Count;

        public Player? FindPlayer(string name)
        {
            string key = PlayerNames.ToKey(name);
            return _players.Values.FirstOrDefault(p => p.Key == key);
        }

        /// <summary>Thread-safe: called by the network layer when a client connects.</summary>
        public void AcceptConnection(IConnection connection) => _newConnections.Enqueue(connection);

        public void Tick()
        {
            CurrentTick++;
            ProcessTimers();      // 1. world/global timers
            ProcessInput();       // 2. client input queued since last tick
            ProcessNpcs();        // 3. NPCs
            ProcessPlayers();     // 4. player queues, actions, combat, movement
            ProcessLogouts();     // 5a. logouts...
            ProcessLogins();      // 5b. ...then logins
            ProcessZones();       // 6. zone and ground-item updates
            SendUpdates();        // 7. tell each client what changed near them
            Cleanup();            // 8. per-tick cleanup
        }

        private void ProcessTimers()
        {
            if (_config.AutosaveIntervalTicks > 0 && CurrentTick % (uint)_config.AutosaveIntervalTicks == 0)
                SaveAll();
        }

        private void ProcessInput()
        {
            while (_newConnections.TryDequeue(out IConnection connection))
                _pendingConnections.Add(connection);

            // Connections not logged in yet may only send a login.
            for (int i = _pendingConnections.Count - 1; i >= 0; i--)
            {
                IConnection connection = _pendingConnections[i];
                if (!connection.IsOpen)
                {
                    _pendingConnections.RemoveAt(i);
                    continue;
                }
                if (connection.TryReceive(out Message message) && message is LoginMessage login)
                {
                    _pendingConnections.RemoveAt(i);
                    _loginQueue.Add((connection, login));
                }
            }

            foreach (Player player in _players.Values)
            {
                if (!player.Connection.IsOpen)
                {
                    player.LogoutRequested = true;
                    continue;
                }
                for (int n = 0; n < _config.MaxMessagesPerTick && player.Connection.TryReceive(out Message message); n++)
                {
                    switch (message)
                    {
                        case WalkToMessage walk:
                            HandleWalk(player, walk);
                            break;
                        case LogoutMessage _:
                            player.LogoutRequested = true;
                            break;
                    }
                }
            }
        }

        private void HandleWalk(Player player, WalkToMessage walk)
        {
            var target = new TileCoord(walk.X, walk.Y);
            List<TileCoord> path = Pathfinder.FindPath(Map, player.Plane, player.Position, target, _config.PathSearchRadius);
            player.Waypoints.Clear();
            foreach (TileCoord tile in path) player.Waypoints.Enqueue(tile);
            player.Running = walk.Run;
        }

        private void ProcessNpcs()
        {
            // No NPCs until Milestone 2.
        }

        private void ProcessPlayers()
        {
            foreach (Player player in _players.Values)
                ProcessMovement(player);
        }

        private void ProcessMovement(Player player)
        {
            int steps = player.Running ? 2 : 1;
            for (int i = 0; i < steps && player.Waypoints.Count > 0; i++)
            {
                TileCoord next = player.Waypoints.Peek();
                TileCoord from = player.Position;
                if (!Map.CanStep(player.Plane, from.X, from.Y, next.X - from.X, next.Y - from.Y))
                {
                    // The path is no longer valid (e.g. something now blocks it): stop here.
                    player.Waypoints.Clear();
                    break;
                }
                player.Waypoints.Dequeue();
                player.Position = next;
                player.StepsThisTick.Add(next);
            }
        }

        private void ProcessLogouts()
        {
            foreach (Player player in _players.Values.Where(p => p.LogoutRequested).ToList())
                RemovePlayer(player);
        }

        private void ProcessLogins()
        {
            foreach ((IConnection connection, LoginMessage login) in _loginQueue)
            {
                if (!connection.IsOpen) continue;
                string? error = TryLogin(connection, login);
                if (error != null)
                {
                    _log("Login refused for " + connection.Description + ": " + error);
                    connection.Send(new LoginRejectedMessage { Reason = error });
                    connection.Close();
                }
            }
            _loginQueue.Clear();
        }

        /// <summary>Logs a player in, or returns why not.</summary>
        private string? TryLogin(IConnection connection, LoginMessage login)
        {
            if (login.ProtocolVersion != Protocol.Version)
                return "Client and server versions don't match (client " + login.ProtocolVersion + ", server " + Protocol.Version + ").";
            string? name = PlayerNames.ToDisplayName(login.Name);
            if (name == null)
                return "Names are 1-" + PlayerNames.MaxLength + " letters, digits, spaces, '-' or '_'.";
            string key = PlayerNames.ToKey(name);

            // Logging in again replaces the old session, e.g. after a dropped connection.
            Player? existing = _players.Values.FirstOrDefault(p => p.Key == key);
            if (existing != null) RemovePlayer(existing);
            else if (_players.Count >= _config.MaxPlayers) return "The world is full.";

            PlayerSave? save = _store.Load(key);
            var player = new Player(_nextPlayerId++, key, save?.Name ?? name, connection);
            if (save != null && Map.Contains(save.Plane, save.X, save.Y) && !Map.IsBlocked(save.Plane, save.X, save.Y))
            {
                player.Plane = save.Plane;
                player.Position = new TileCoord(save.X, save.Y);
            }
            else
            {
                player.Plane = Map.SpawnPlane;
                player.Position = Map.Spawn;
            }

            _players.Add(player.Id, player);
            connection.Send(new LoginOkMessage { PlayerId = player.Id, TickMillis = _config.TickMillis });
            _log(player.Name + " logged in at " + player.Position + (save == null ? " (new player)." : "."));
            return null;
        }

        private void RemovePlayer(Player player)
        {
            Save(player);
            _players.Remove(player.Id);
            player.Connection.Close();
            _log(player.Name + " logged out at " + player.Position + ".");
        }

        private void ProcessZones()
        {
            // No zone or ground-item state yet.
        }

        private void SendUpdates()
        {
            foreach (Player viewer in _players.Values)
            {
                var update = new WorldUpdateMessage { Tick = CurrentTick };

                foreach (Player other in _players.Values)
                {
                    if (!CanSee(viewer, other)) continue;
                    if (viewer.KnownPlayers.Add(other.Id))
                    {
                        update.Added.Add(new EntityAdded { Id = other.Id, Name = other.Name, Plane = other.Plane, Position = other.Position });
                    }
                    else if (other.StepsThisTick.Count > 0)
                    {
                        update.Moved.Add(new EntityMoved { Id = other.Id, Running = other.Running, Steps = new List<TileCoord>(other.StepsThisTick) });
                    }
                }

                foreach (int id in viewer.KnownPlayers.ToList())
                {
                    if (_players.TryGetValue(id, out Player other) && CanSee(viewer, other)) continue;
                    viewer.KnownPlayers.Remove(id);
                    update.Removed.Add(id);
                }

                viewer.Connection.Send(update);
            }
        }

        private bool CanSee(Player viewer, Player other) =>
            viewer.Plane == other.Plane && viewer.Position.ChebyshevDistance(other.Position) <= _config.ViewRadius;

        private void Cleanup()
        {
            foreach (Player player in _players.Values)
                player.StepsThisTick.Clear();
        }

        private void Save(Player player)
        {
            try
            {
                _store.Save(player.Key, new PlayerSave
                {
                    Name = player.Name,
                    Plane = player.Plane,
                    X = player.Position.X,
                    Y = player.Position.Y,
                });
            }
            catch (Exception e)
            {
                _log("Failed to save " + player.Name + ": " + e.Message);
            }
        }

        public void SaveAll()
        {
            foreach (Player player in _players.Values) Save(player);
        }

        /// <summary>Saves and disconnects everyone. Call on the tick thread, or after the tick loop has stopped.</summary>
        public void Shutdown()
        {
            foreach (Player player in _players.Values.ToList()) RemovePlayer(player);
            foreach (IConnection connection in _pendingConnections) connection.Close();
            _pendingConnections.Clear();
            while (_newConnections.TryDequeue(out IConnection connection)) connection.Close();
        }
    }
}
