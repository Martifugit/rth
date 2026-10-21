using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using Rth.Server.Content;
using Rth.Server.Net;
using Rth.Server.Persistence;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// The whole game state and the tick. The world owns entities, connections, movement,
    /// visibility and saving; everything else is a <see cref="GameSystem"/> plugged in at
    /// construction (combat, NPCs, items...). Everything except <see cref="AcceptConnection"/> and
    /// <see cref="QueueContent"/> runs on the tick thread.
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
        private readonly Dictionary<int, Npc> _npcs = new Dictionary<int, Npc>();
        private readonly List<GameSystem> _systems = new List<GameSystem>();
        private readonly Dictionary<Type, Action<Player, Message>> _messageHandlers = new Dictionary<Type, Action<Player, Message>>();
        private GameContent? _pendingContent;
        private int _nextEntityId = 1;

        // Things that happened this tick, sent to whoever can see them in phase 7.
        private readonly List<HitEvent> _hits = new List<HitEvent>();
        private readonly List<ActionEvent> _actions = new List<ActionEvent>();
        private readonly List<int> _deaths = new List<int>();

        /// <summary>Creates a world with the standard systems.</summary>
        public World(ServerConfig config, CollisionMap map, IPlayerStore store, GameContent content, MapPlacements? placements = null)
            : this(config, map, store, content, StandardSystems(placements ?? new MapPlacements()))
        {
        }

        /// <summary>Creates a world with exactly the given systems, in order (useful for tests and tools).</summary>
        public World(ServerConfig config, CollisionMap map, IPlayerStore store, GameContent content, IEnumerable<GameSystem> systems)
        {
            _config = config;
            _store = store;
            _log = config.Log;
            Random = config.RandomSeed.HasValue ? new Random(config.RandomSeed.Value) : new Random();
            Map = map;
            Content = content;
            OnMessage<WalkToMessage>(HandleWalk);
            OnMessage<LogoutMessage>((player, _) => player.LogoutRequested = true);
            foreach (GameSystem system in systems) AddSystem(system);
        }

        /// <summary>
        /// The systems a normal server runs. Order matters: systems run in this order within each
        /// phase, and a system may use any system listed before it while attaching.
        /// </summary>
        public static IEnumerable<GameSystem> StandardSystems(MapPlacements placements) => new GameSystem[]
        {
            new SkillSystem(),
            new FlagSystem(),
            new ItemSystem(),
            new CombatSystem(),
            new DeathSystem(),
            new NpcSystem(placements.Npcs),
            new GroundItemSystem(),
            new LootSystem(),
            new ObjectSystem(placements.Objects),
            new RecipeSystem(),
            new InterfaceSystem(),
            new DialogueSystem(),
            new BankSystem(),
            new ShopSystem(),
            new PointsSystem(),
            new TaskSystem(),
            new QuestSystem(),
            new ChatSystem(),
            new RegionSystem(),
            new ClientStateSystem(),
            new AchievementSystem(),
            new BestiarySystem(),
        };

        public ServerConfig Config => _config;
        /// <summary>The world's collision: a <see cref="WorldMap"/> of every region, or one map in tests.</summary>
        public CollisionMap Map { get; }

        /// <summary>The regions clients stream in. Empty when the world is a single bare map.</summary>
        public IReadOnlyList<WorldRegion> Regions => Map is WorldMap world ? world.Regions : (IReadOnlyList<WorldRegion>)Array.Empty<WorldRegion>();

        /// <summary>Which entities are in which zone; rebuilt every tick in the zone phase.</summary>
        public ZoneIndex Zones { get; } = new ZoneIndex();
        public GameContent Content { get; private set; }
        public CombatRules Rules => Content.Rules;
        public GameEvents Events { get; } = new GameEvents();
        public Random Random { get; }
        public uint CurrentTick { get; private set; }
        public IEnumerable<Player> Players => _players.Values;
        public IEnumerable<Npc> Npcs => _npcs.Values;
        public int PlayerCount => _players.Count;

        // ------------------------------------------------------------------ systems

        private void AddSystem(GameSystem system)
        {
            _systems.Add(system);
            system.Attach(this);
        }

        /// <summary>The system of a given type. Throws if it isn't installed.</summary>
        public T Get<T>() where T : GameSystem =>
            _systems.OfType<T>().FirstOrDefault() ?? throw new InvalidOperationException(typeof(T).Name + " is not installed.");

        public T? Find<T>() where T : GameSystem => _systems.OfType<T>().FirstOrDefault();

        /// <summary>Handles a client message type. One handler per type; later registrations replace earlier ones.</summary>
        public void OnMessage<T>(Action<Player, T> handler) where T : Message =>
            _messageHandlers[typeof(T)] = (player, message) => handler(player, (T)message);

        // ------------------------------------------------------------------ entities

        public int NextEntityId() => _nextEntityId++;

        public Player? FindPlayer(string name)
        {
            string key = PlayerNames.ToKey(name);
            return _players.Values.FirstOrDefault(p => p.Key == key);
        }

        /// <summary>A player or NPC that is currently in the world (not despawned).</summary>
        public Entity? FindEntity(int id)
        {
            if (_players.TryGetValue(id, out Player? player)) return player;
            if (_npcs.TryGetValue(id, out Npc? npc) && !npc.Despawned) return npc;
            return null;
        }

        public void AddNpc(Npc npc) => _npcs.Add(npc.Id, npc);

        public IEnumerable<Entity> AllEntities() => _players.Values.Cast<Entity>().Concat(_npcs.Values);

        private IEnumerable<Entity> VisibleEntities()
        {
            foreach (Player p in _players.Values) yield return p;
            foreach (Npc n in _npcs.Values)
                if (!n.Despawned) yield return n;
        }

        public bool CanSee(Player viewer, Entity other) => CanSee(viewer, other.Plane, other.Position);

        public bool CanSee(Player viewer, int plane, TileCoord tile) =>
            viewer.Plane == plane && viewer.Position.ChebyshevDistance(tile) <= _config.ViewRadius;

        /// <summary>Players and live NPCs within a radius (Chebyshev) of a tile, found through the zone index.</summary>
        public IEnumerable<Entity> EntitiesNear(int plane, TileCoord tile, int radius)
        {
            foreach (Entity entity in Zones.Near(plane, tile, radius))
            {
                if (entity.Plane != plane || entity.Position.ChebyshevDistance(tile) > radius) continue;
                if (FindEntity(entity.Id) != entity) continue; // left the world since the index was built
                yield return entity;
            }
        }

        // ------------------------------------------------------------------ helpers for systems

        public void Tell(Player player, TextKind kind, string text) =>
            player.Connection.Send(new GameTextMessage { Kind = kind, Text = text });

        public void Log(string line) => _log(line);

        public void AddHit(HitEvent hit) => _hits.Add(hit);
        public void AddAction(ActionEvent action) => _actions.Add(action);
        public void AddDeath(int entityId) => _deaths.Add(entityId);

        public void SetPath(Entity entity, TileCoord target)
        {
            List<TileCoord> path = Pathfinder.FindPath(Map, entity.Plane, entity.Position, target, _config.PathSearchRadius);
            entity.Waypoints.Clear();
            foreach (TileCoord tile in path) entity.Waypoints.Enqueue(tile);
        }

        /// <summary>
        /// Whether an entity stands straight beside a tile (not diagonal) with no wall between: how
        /// melee fighters stand next to their target and players stand at workstations.
        /// </summary>
        public bool IsBeside(Entity entity, int plane, TileCoord tile)
        {
            if (entity.Plane != plane) return false;
            int dx = tile.X - entity.Position.X;
            int dy = tile.Y - entity.Position.Y;
            return Math.Abs(dx) + Math.Abs(dy) == 1 && Map.EdgeOpen(plane, entity.Position.X, entity.Position.Y, dx, dy);
        }

        /// <summary>
        /// Paths to the nearest reachable tile straight beside <paramref name="tile"/>. Returns false
        /// if none can be reached (or the entity is already beside it, in which case it stays put).
        /// </summary>
        public bool PathBeside(Entity entity, int plane, TileCoord tile)
        {
            entity.Waypoints.Clear();
            if (IsBeside(entity, plane, tile)) return false;
            List<TileCoord>? best = null;
            foreach ((int dx, int dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                TileCoord beside = tile.Offset(dx, dy);
                if (Map.IsBlocked(plane, beside.X, beside.Y) || !Map.EdgeOpen(plane, beside.X, beside.Y, -dx, -dy)) continue;
                List<TileCoord> path = Pathfinder.FindPath(Map, plane, entity.Position, beside, _config.PathSearchRadius);
                if (path.Count == 0 || path[path.Count - 1] != beside) continue;
                if (best == null || path.Count < best.Count) best = path;
            }
            if (best == null) return false;
            foreach (TileCoord step in best) entity.Waypoints.Enqueue(step);
            return true;
        }

        /// <summary>Walks up to one tick's worth of steps; <paramref name="stopWhen"/> is checked before each step.</summary>
        public void ProcessMovement(Entity entity, Func<bool>? stopWhen = null)
        {
            int steps = entity.Running ? 2 : 1;
            for (int i = 0; i < steps && entity.Waypoints.Count > 0; i++)
            {
                if (stopWhen != null && stopWhen())
                {
                    entity.Waypoints.Clear();
                    break;
                }
                TileCoord next = entity.Waypoints.Peek();
                TileCoord from = entity.Position;
                if (!Map.CanStep(entity.Plane, from.X, from.Y, next.X - from.X, next.Y - from.Y))
                {
                    // The path is no longer valid (e.g. something now blocks it): stop here.
                    entity.Waypoints.Clear();
                    break;
                }
                entity.Waypoints.Dequeue();
                entity.Position = next;
                entity.StepsThisTick.Add(next);
            }
        }

        /// <summary>
        /// Rebuilds a player's stats from every <see cref="IStatContributor"/>, then applies the
        /// ones the world itself uses (max blood and breath).
        /// </summary>
        public void RecalculateStats(Player player)
        {
            player.Stats.Clear();
            foreach (GameSystem system in _systems)
                if (system is IStatContributor contributor) contributor.Contribute(player, player.Stats);
            player.MaxBlood = Math.Max(1, player.Stats.Get(StatIds.MaxBlood));
            player.MaxBreath = Math.Max(0, player.Stats.Get(StatIds.MaxBreath));
            if (player.Blood > player.MaxBlood) player.Blood = player.MaxBlood;
            if (player.Breath > player.MaxBreath) player.Breath = player.MaxBreath;
            player.StatsChanged = true;
        }

        /// <summary>Every status the player is under now, from each <see cref="IStatusSource"/>.</summary>
        public List<StatusState> StatusesOf(Player player)
        {
            var statuses = new List<StatusState>();
            foreach (GameSystem system in _systems)
                if (system is IStatusSource source) source.AddStatuses(player, statuses);
            return statuses;
        }

        /// <summary>Starts an interaction, ending whatever the player was doing before.</summary>
        public void StartInteraction(Player player, Interaction interaction)
        {
            StopInteraction(player);
            player.Interaction = interaction;
        }

        public void StopInteraction(Player player)
        {
            Interaction? current = player.Interaction;
            if (current == null) return;
            player.Interaction = null;
            current.End(this, player);
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>Thread-safe: called by the network layer when a client connects.</summary>
        public void AcceptConnection(IConnection connection) => _newConnections.Enqueue(connection);

        /// <summary>Thread-safe: hot-reloaded content is swapped in at the start of the next tick.</summary>
        public void QueueContent(GameContent content) => Interlocked.Exchange(ref _pendingContent, content);

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
            GameContent? reloaded = Interlocked.Exchange(ref _pendingContent, null);
            if (reloaded != null)
            {
                Content = reloaded;
                foreach (GameSystem system in _systems) system.OnContentChanged();
                ContentInfoMessage info = Content.ToMessage();
                foreach (Player player in _players.Values) player.Connection.Send(info);
            }

            foreach (GameSystem system in _systems) system.OnTimers();

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
                    if (_messageHandlers.TryGetValue(message.GetType(), out Action<Player, Message>? handler))
                        handler(player, message);
                }
            }
        }

        private void HandleWalk(Player player, WalkToMessage walk)
        {
            if (player.IsDead) return;
            // Walking away is how you stop doing anything else.
            StopInteraction(player);
            SetPath(player, new TileCoord(walk.X, walk.Y));
            player.Running = walk.Run;
        }

        private void ProcessNpcs()
        {
            foreach (GameSystem system in _systems) system.OnNpcs();
        }

        private void ProcessPlayers()
        {
            foreach (Player player in _players.Values)
            {
                foreach (GameSystem system in _systems) system.BeforePlayer(player);

                if (!player.IsDead)
                {
                    Interaction? interaction = player.Interaction;
                    if (interaction != null)
                    {
                        if (!interaction.Tick(this, player) && player.Interaction == interaction) StopInteraction(player);
                    }
                    else
                    {
                        ProcessMovement(player);
                    }
                }

                foreach (GameSystem system in _systems) system.AfterPlayer(player);
            }
        }

        // ------------------------------------------------------------------ 5. logouts and logins

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
            var player = new Player(NextEntityId(), key, save?.Name ?? name, connection);
            // Positions are absolute, so a save is good anywhere in the world, unless the land
            // under it has gone (a region was rebuilt or removed): then start at the spawn.
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
            // Keep every section, including ones no installed system understands, so nothing is lost.
            if (save != null)
                foreach (KeyValuePair<string, JToken> section in save.Sections) player.SavedSections[section.Key] = section.Value;

            _players.Add(player.Id, player);
            connection.Send(new LoginOkMessage { PlayerId = player.Id, TickMillis = _config.TickMillis });
            connection.Send(Content.ToMessage());
            connection.Send(new RegionsMessage { Regions = Regions.ToList() });
            foreach (GameSystem system in _systems) system.OnLogin(player, save);
            _log(player.Name + " logged in at " + player.Position + (save == null ? " (new player)." : "."));
            return null;
        }

        private void RemovePlayer(Player player)
        {
            StopInteraction(player);
            Save(player);
            _players.Remove(player.Id);
            foreach (GameSystem system in _systems) system.OnLogout(player);
            player.Connection.Close();
            _log(player.Name + " logged out at " + player.Position + ".");
        }

        private void ProcessZones()
        {
            foreach (GameSystem system in _systems) system.OnZones();
            Zones.Rebuild(VisibleEntities());
        }

        // ------------------------------------------------------------------ 7. updates

        private void SendUpdates()
        {
            foreach (Player viewer in _players.Values)
            {
                var update = new WorldUpdateMessage { Tick = CurrentTick };
                var seen = new HashSet<int>();

                foreach (Entity other in Zones.Near(viewer.Plane, viewer.Position, _config.ViewRadius))
                {
                    if (!CanSee(viewer, other)) continue;
                    seen.Add(other.Id);
                    if (viewer.KnownEntities.Add(other.Id) || other.Teleported)
                    {
                        update.Added.Add(other.Describe());
                        continue;
                    }
                    if (other.StepsThisTick.Count > 0)
                        update.Moved.Add(new EntityMoved { Id = other.Id, Running = other.Running, Steps = new List<TileCoord>(other.StepsThisTick) });
                    if (other.HealthChanged)
                        update.Health.Add(new HealthEvent { Id = other.Id, Percent = other.HealthPercent });
                }

                foreach (int id in viewer.KnownEntities.ToList())
                {
                    if (seen.Contains(id)) continue;
                    viewer.KnownEntities.Remove(id);
                    update.Removed.Add(id);
                }

                foreach (HitEvent hit in _hits)
                    if (seen.Contains(hit.TargetId)) update.Hits.Add(hit);
                foreach (ActionEvent action in _actions)
                    if (seen.Contains(action.SourceId) || seen.Contains(action.TargetId)) update.Actions.Add(action);
                foreach (int id in _deaths)
                    if (seen.Contains(id)) update.Deaths.Add(id);

                foreach (GameSystem system in _systems) system.WriteUpdate(viewer, update, seen);

                viewer.Connection.Send(update);
                foreach (GameSystem system in _systems) system.SendState(viewer);
                if (viewer.StatsChanged)
                {
                    viewer.StatsChanged = false;
                    viewer.Connection.Send(new StatsMessage { Stats = new Dictionary<string, int>(viewer.Stats.Values) });
                }
            }
        }

        private void Cleanup()
        {
            foreach (Entity entity in AllEntities())
            {
                entity.StepsThisTick.Clear();
                entity.Teleported = false;
                entity.HealthChanged = false;
            }
            _hits.Clear();
            _actions.Clear();
            _deaths.Clear();
            foreach (GameSystem system in _systems) system.OnCleanup();
        }

        // ------------------------------------------------------------------ persistence

        private void Save(Player player)
        {
            try
            {
                var save = new PlayerSave
                {
                    Name = player.Name,
                    Map = (Map as WorldMap)?.RegionAt(player.Position.X, player.Position.Y)?.Name,
                    // Logging out while dead counts as having respawned.
                    Plane = player.IsDead ? Map.SpawnPlane : player.Plane,
                    X = player.IsDead ? Map.Spawn.X : player.Position.X,
                    Y = player.IsDead ? Map.Spawn.Y : player.Position.Y,
                };
                foreach (KeyValuePair<string, JToken> section in player.SavedSections) save.Sections[section.Key] = section.Value;
                foreach (GameSystem system in _systems) system.OnSave(player, save);
                _store.Save(player.Key, save);
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
