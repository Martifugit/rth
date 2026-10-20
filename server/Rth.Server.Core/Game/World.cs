using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Rth.Server.Content;
using Rth.Server.Net;
using Rth.Server.Persistence;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// The whole game state. Everything except <see cref="AcceptConnection"/> and
    /// <see cref="QueueContent"/> runs on the tick thread; <see cref="Tick"/> advances the world by
    /// exactly one tick. Combat is in World.Combat.cs, NPC behaviour in World.Npcs.cs.
    /// </summary>
    public sealed partial class World
    {
        private readonly ServerConfig _config;
        private readonly IPlayerStore _store;
        private readonly Action<string> _log;
        private readonly Random _random;

        private readonly ConcurrentQueue<IConnection> _newConnections = new ConcurrentQueue<IConnection>();
        private readonly List<IConnection> _pendingConnections = new List<IConnection>();
        private readonly List<(IConnection Connection, LoginMessage Login)> _loginQueue = new List<(IConnection, LoginMessage)>();
        private readonly Dictionary<int, Player> _players = new Dictionary<int, Player>();
        private readonly Dictionary<int, Npc> _npcs = new Dictionary<int, Npc>();
        private GameContent? _pendingContent;
        private int _nextEntityId = 1;

        // Things that happened this tick, sent to whoever can see them in phase 7.
        private readonly List<HitEvent> _hits = new List<HitEvent>();
        private readonly List<ActionEvent> _actions = new List<ActionEvent>();
        private readonly List<int> _deaths = new List<int>();

        public World(ServerConfig config, MapData map, IPlayerStore store, GameContent content, IEnumerable<NpcSpawn>? spawns = null)
        {
            _config = config;
            _store = store;
            _log = config.Log;
            _random = config.RandomSeed.HasValue ? new Random(config.RandomSeed.Value) : new Random();
            Map = map;
            Content = content;
            foreach (NpcSpawn spawn in spawns ?? Enumerable.Empty<NpcSpawn>()) AddNpc(spawn);
        }

        public MapData Map { get; }
        public GameContent Content { get; private set; }
        public CombatRules Rules => Content.Rules;
        public uint CurrentTick { get; private set; }
        public IEnumerable<Player> Players => _players.Values;
        public IEnumerable<Npc> Npcs => _npcs.Values;
        public int PlayerCount => _players.Count;

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

        // ------------------------------------------------------------------ 1. timers

        private void ProcessTimers()
        {
            GameContent? reloaded = Interlocked.Exchange(ref _pendingContent, null);
            if (reloaded != null) ApplyContent(reloaded);

            if (_config.AutosaveIntervalTicks > 0 && CurrentTick % (uint)_config.AutosaveIntervalTicks == 0)
                SaveAll();
        }

        private void ApplyContent(GameContent content)
        {
            Content = content;
            foreach (Npc npc in _npcs.Values)
            {
                if (!content.Npcs.TryGetValue(npc.Def.Id, out NpcDef? def)) continue; // removed type: keep the old one
                npc.Def = def;
                npc.MaxBlood = def.Blood;
                npc.Blood = Math.Min(npc.Blood, def.Blood);
            }
            ContentInfoMessage info = content.ToMessage();
            foreach (Player player in _players.Values) player.Connection.Send(info);
        }

        // ------------------------------------------------------------------ 2. input

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
                        case AttackMessage attack:
                            HandleAttack(player, attack.TargetId);
                            break;
                        case UseAbilityMessage use:
                            HandleUseAbility(player, use.AbilityId);
                            break;
                        case SetDeflectMessage deflect:
                            HandleDeflect(player, deflect.Style);
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
            if (player.IsDead) return;
            // Walking away is how you stop fighting.
            player.ClearCombat();
            player.QueuedAbility = null;
            SetPath(player, new TileCoord(walk.X, walk.Y));
            player.Running = walk.Run;
        }

        private void SetPath(Entity entity, TileCoord target)
        {
            List<TileCoord> path = Pathfinder.FindPath(Map, entity.Plane, entity.Position, target, _config.PathSearchRadius);
            entity.Waypoints.Clear();
            foreach (TileCoord tile in path) entity.Waypoints.Enqueue(tile);
        }

        internal void Tell(Player player, TextKind kind, string text) =>
            player.Connection.Send(new GameTextMessage { Kind = kind, Text = text });

        // ------------------------------------------------------------------ 4. players

        private void ProcessPlayers()
        {
            foreach (Player player in _players.Values)
            {
                if (player.IsDead)
                {
                    if (CurrentTick >= player.RespawnAtTick) RespawnPlayer(player);
                    continue;
                }
                ProcessPlayerCombat(player);
                TickResources(player);
                ExpireEffects(player);
            }
        }

        /// <summary>Walks up to one tick's worth of steps; <paramref name="stopWhen"/> is checked after each step.</summary>
        private void ProcessMovement(Entity entity, Func<bool>? stopWhen = null)
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
            var player = new Player(_nextEntityId++, key, save?.Name ?? name, connection)
            {
                MaxBlood = Rules.PlayerMaxBlood,
                MaxBreath = Rules.PlayerMaxBreath,
            };
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
            player.Blood = Clamp(save?.Blood ?? player.MaxBlood, 1, player.MaxBlood);
            player.Breath = Clamp(save?.Breath ?? player.MaxBreath, 0, player.MaxBreath);
            player.Bile = Rules.ClampBile(save?.Bile ?? 0);
            player.Deaths = save?.Deaths ?? 0;

            _players.Add(player.Id, player);
            connection.Send(new LoginOkMessage { PlayerId = player.Id, TickMillis = _config.TickMillis });
            connection.Send(Content.ToMessage());
            _log(player.Name + " logged in at " + player.Position + (save == null ? " (new player)." : "."));
            return null;
        }

        private void RemovePlayer(Player player)
        {
            Save(player);
            _players.Remove(player.Id);
            foreach (Entity other in AllEntities())
                if (other.TargetId == player.Id) other.ClearCombat();
            player.Connection.Close();
            _log(player.Name + " logged out at " + player.Position + ".");
        }

        private IEnumerable<Entity> AllEntities() => _players.Values.Cast<Entity>().Concat(_npcs.Values);

        private void ProcessZones()
        {
            // No zone or ground-item state yet.
        }

        // ------------------------------------------------------------------ 7. updates

        private void SendUpdates()
        {
            foreach (Player viewer in _players.Values)
            {
                var update = new WorldUpdateMessage { Tick = CurrentTick };
                var seen = new HashSet<int>();

                foreach (Entity other in VisibleEntities())
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

                viewer.Connection.Send(update);
                viewer.Connection.Send(DescribeState(viewer));
            }
        }

        private IEnumerable<Entity> VisibleEntities()
        {
            foreach (Player p in _players.Values) yield return p;
            foreach (Npc n in _npcs.Values)
                if (!n.Despawned) yield return n;
        }

        private bool CanSee(Player viewer, Entity other) =>
            viewer.Plane == other.Plane && viewer.Position.ChebyshevDistance(other.Position) <= _config.ViewRadius;

        private PlayerStateMessage DescribeState(Player p)
        {
            var state = new PlayerStateMessage
            {
                Tick = CurrentTick,
                Blood = p.Blood,
                MaxBlood = p.MaxBlood,
                Phlegm = p.Phlegm,
                Bile = p.Bile,
                Breath = p.Breath,
                MaxBreath = p.MaxBreath,
                Deflect = p.Deflect,
                InCombat = p.InCombat(CurrentTick, Rules.CombatTimeoutTicks),
                TargetId = p.TargetId,
                GlobalCooldownReadyTick = p.GlobalCooldownReadyTick,
                QueuedAbility = p.QueuedAbility ?? "",
            };
            foreach (KeyValuePair<string, uint> cd in p.Cooldowns)
                if (cd.Value > CurrentTick) state.Cooldowns.Add(new CooldownState { AbilityId = cd.Key, ReadyTick = cd.Value });
            foreach (ActiveEffect e in p.Effects)
                state.Effects.Add(new EffectState { Effect = e.Type, Magnitude = e.Magnitude, EndTick = e.EndTick });
            return state;
        }

        // ------------------------------------------------------------------ 8. cleanup

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
        }

        // ------------------------------------------------------------------ persistence

        private void Save(Player player)
        {
            try
            {
                _store.Save(player.Key, new PlayerSave
                {
                    Name = player.Name,
                    // Logging out while dead counts as having respawned.
                    Plane = player.IsDead ? Map.SpawnPlane : player.Plane,
                    X = player.IsDead ? Map.Spawn.X : player.Position.X,
                    Y = player.IsDead ? Map.Spawn.Y : player.Position.Y,
                    Blood = player.IsDead ? player.MaxBlood : player.Blood,
                    Breath = player.Breath,
                    Bile = player.IsDead ? 0 : player.Bile,
                    Deaths = player.Deaths,
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

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}
