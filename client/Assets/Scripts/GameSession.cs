using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rth.Client
{
    /// <summary>
    /// Connection lifecycle (login, play, reconnect), the player's input, and the latest state the
    /// server reported. Everything here is an intent sent to the server; what actually happens
    /// arrives back as world updates. The in-game interface is drawn by <see cref="CombatHud"/>.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        private const string NamePref = "rth.name";
        private const string HostPref = "rth.host";
        private const int MaxReconnectAttempts = 10;
        private const float ReconnectDelaySeconds = 2f;
        private const int MaxLogLines = 50;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public WorldView world;
        public CameraRig cameraRig;
        public int port = Protocol.DefaultPort;

        public struct LogLine
        {
            public TextKind Kind;
            public string Text;
        }

        private enum State
        {
            Login,
            Connecting,
            InGame,
            Reconnecting,
        }

        private readonly Dictionary<string, AbilityDef> _abilities = new Dictionary<string, AbilityDef>();
        private State _state = State.Login;
        private GameClient _client;
        private Task _connectTask;
        private string _name = "";
        private string _host = "127.0.0.1";
        private string _status = "";
        private int _reconnectAttempts;
        private float _nextReconnectTime;
        private Transform _clickMarker;
        private float _clickMarkerHideTime;
        private float _tickSeconds = 0.6f;
        private float _stateReceivedTime;

        public bool IsInGame => _state == State.InGame;
        public bool Running { get; private set; }
        public string PlayerName => _name;

        /// <summary>The player's resources as of the last tick, or null before the first one.</summary>
        public PlayerStateMessage PlayerState { get; private set; }
        public ContentInfoMessage Content { get; private set; }
        public List<LogLine> Log { get; } = new List<LogLine>();
        /// <summary>The Assayer's last words, shown until dismissed.</summary>
        public string AssayerText { get; set; }

        /// <summary>The server tick right now, extrapolated from the last state message (fractional).</summary>
        public float EstimatedTick =>
            PlayerState == null ? 0f : PlayerState.Tick + (Time.time - _stateReceivedTime) / _tickSeconds;

        public float TickSeconds => _tickSeconds;

        public AbilityDef Ability(string id) => id != null && _abilities.TryGetValue(id, out AbilityDef a) ? a : null;

        private void Start()
        {
            _name = PlayerPrefs.GetString(NamePref, "");
            _host = PlayerPrefs.GetString(HostPref, "127.0.0.1");
            _clickMarker = CreateClickMarker();
        }

        private void Update()
        {
            switch (_state)
            {
                case State.Connecting:
                    PumpLogin();
                    break;
                case State.Reconnecting:
                    if (_connectTask == null && _client == null)
                    {
                        if (Time.unscaledTime >= _nextReconnectTime) BeginConnect();
                    }
                    else
                    {
                        PumpLogin();
                    }
                    break;
                case State.InGame:
                    PumpWorld();
                    if (_state == State.InGame) HandleInput();
                    break;
            }

            if (_clickMarker != null && _clickMarker.gameObject.activeSelf && Time.unscaledTime > _clickMarkerHideTime)
                _clickMarker.gameObject.SetActive(false);
            if (cameraRig != null && world != null && world.Local != null) cameraRig.target = world.Local.transform;
        }

        // ------------------------------------------------------------------ connection

        public void LogIn(string name, string host)
        {
            if (_state != State.Login || name.Trim().Length == 0) return;
            _name = name;
            _host = host;
            PlayerPrefs.SetString(NamePref, _name);
            PlayerPrefs.SetString(HostPref, _host);
            _status = "Connecting...";
            _state = State.Connecting;
            BeginConnect();
        }

        private void BeginConnect()
        {
            _client = new GameClient();
            GameClient client = _client;
            string host = _host;
            int p = port;
            _connectTask = Task.Run(() => client.Connect(host, p));
        }

        /// <summary>Waits for the TCP connection, sends the login, and waits for the answer.</summary>
        private void PumpLogin()
        {
            if (_connectTask != null)
            {
                if (!_connectTask.IsCompleted) return;
                if (_connectTask.IsFaulted)
                {
                    Exception error = _connectTask.Exception?.GetBaseException();
                    _connectTask = null;
                    ConnectFailed("Couldn't reach the server at " + _host + ":" + port + " (" + error?.Message + ").");
                    return;
                }
                _connectTask = null;
                _client.Send(new LoginMessage { Name = _name });
            }

            while (_client.TryReceive(out Message message))
            {
                if (message is LoginOkMessage ok)
                {
                    _tickSeconds = ok.TickMillis / 1000f;
                    world.Begin(ok.PlayerId, _tickSeconds);
                    _state = State.InGame;
                    _status = "";
                    _reconnectAttempts = 0;
                    PumpWorld();
                    return;
                }
                if (message is LoginRejectedMessage rejected)
                {
                    DropClient();
                    _state = State.Login;
                    _status = rejected.Reason;
                    return;
                }
            }

            if (!_client.IsConnected) ConnectFailed(_client.DisconnectReason ?? "Disconnected.");
        }

        private void ConnectFailed(string reason)
        {
            DropClient();
            if (_state == State.Reconnecting && ++_reconnectAttempts < MaxReconnectAttempts)
            {
                _status = "Connection lost. Reconnecting (attempt " + (_reconnectAttempts + 1) + ")...";
                _nextReconnectTime = Time.unscaledTime + ReconnectDelaySeconds;
                return;
            }
            _state = State.Login;
            _status = reason;
        }

        private void PumpWorld()
        {
            while (_client.TryReceive(out Message message))
            {
                switch (message)
                {
                    case WorldUpdateMessage update:
                        world.Apply(update);
                        break;
                    case PlayerStateMessage state:
                        PlayerState = state;
                        _stateReceivedTime = Time.time;
                        world.TargetId = state.TargetId;
                        break;
                    case ContentInfoMessage content:
                        Content = content;
                        _abilities.Clear();
                        foreach (AbilityDef a in content.Abilities) _abilities[a.Id] = a;
                        break;
                    case GameTextMessage text:
                        AddLog(text.Kind, text.Text);
                        if (text.Kind == TextKind.Assayer) AssayerText = text.Text;
                        break;
                }
            }

            if (!_client.IsConnected)
            {
                // Dropped mid-game: keep trying to get back in with the same name.
                DropClient();
                world.Clear();
                PlayerState = null;
                _state = State.Reconnecting;
                _reconnectAttempts = 0;
                _status = "Connection lost. Reconnecting...";
                _nextReconnectTime = Time.unscaledTime + 0.5f;
            }
        }

        private void AddLog(TextKind kind, string text)
        {
            Log.Add(new LogLine { Kind = kind, Text = text });
            if (Log.Count > MaxLogLines) Log.RemoveAt(0);
        }

        // ------------------------------------------------------------------ input

        private void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) ToggleRun();
                if (keyboard.zKey.wasPressedThisFrame) ToggleDeflect(CombatStyle.Melee);
                if (keyboard.xKey.wasPressedThisFrame) ToggleDeflect(CombatStyle.Ranged);
                if (keyboard.cKey.wasPressedThisFrame) ToggleDeflect(CombatStyle.Occult);
                if (Content != null)
                {
                    Key[] slots = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
                    for (int i = 0; i < slots.Length && i < Content.ActionBar.Count; i++)
                        if (keyboard[slots[i]].wasPressedThisFrame) UseAbility(Content.ActionBar[i]);
                }
            }

            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            Vector2 pointer = mouse.position.ReadValue();
            if (CombatHud.IsPointerOverHud(pointer)) return;

            Camera cam = Camera.main;
            if (cam == null || !Physics.Raycast(cam.ScreenPointToRay(pointer), out RaycastHit hit, 500f)) return;

            EntityView entity = hit.collider.GetComponentInParent<EntityView>();
            if (entity != null && entity.Id != world.LocalId && entity.Kind == EntityKind.Npc && !entity.IsDying)
            {
                Attack(entity.Id);
                return;
            }
            WalkTo(TileMath.FromWorld(hit.point + cam.transform.forward * 0.01f));
        }

        /// <summary>Asks the server to walk (or run, if toggled) the local player to a tile.</summary>
        public void WalkTo(TileCoord tile)
        {
            if (_state != State.InGame) return;
            _client.Send(new WalkToMessage { X = tile.X, Y = tile.Y, Run = Running });
            ShowClickMarker(tile, new Color(1f, 0.85f, 0.1f));
        }

        public void Attack(int entityId)
        {
            if (_state != State.InGame) return;
            _client.Send(new AttackMessage { TargetId = entityId });
            EntityView target = world.Find(entityId);
            if (target != null) ShowClickMarker(target.Tile, new Color(0.9f, 0.15f, 0.1f));
        }

        public void UseAbility(string abilityId)
        {
            if (_state != State.InGame) return;
            _client.Send(new UseAbilityMessage { AbilityId = abilityId });
        }

        public void ToggleDeflect(CombatStyle style)
        {
            if (_state != State.InGame) return;
            bool on = PlayerState != null && PlayerState.Deflect == style;
            _client.Send(new SetDeflectMessage { Style = on ? CombatStyle.None : style });
        }

        public void ToggleRun() => Running = !Running;

        public void Logout()
        {
            _client?.Send(new LogoutMessage());
            DropClient();
            world.Clear();
            PlayerState = null;
            AssayerText = null;
            _state = State.Login;
            _status = "Logged out.";
        }

        private void DropClient()
        {
            _client?.Dispose();
            _client = null;
            _connectTask = null;
        }

        private void OnDestroy()
        {
            if (_client != null && _client.IsConnected) _client.Send(new LogoutMessage());
            DropClient();
        }

        private Transform CreateClickMarker()
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = "Click Marker";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            marker.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            marker.SetActive(false);
            return marker.transform;
        }

        private void ShowClickMarker(TileCoord tile, Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            _clickMarker.GetComponent<Renderer>().SetPropertyBlock(block);
            _clickMarker.position = TileMath.Center(tile) + Vector3.up * 0.03f;
            _clickMarker.gameObject.SetActive(true);
            _clickMarkerHideTime = Time.unscaledTime + 0.6f;
        }

        // ------------------------------------------------------------------ login screen

        private void OnGUI()
        {
            if (_state != State.InGame) DrawLogin();
        }

        private void DrawLogin()
        {
            var rect = new Rect((Screen.width - 300f) / 2f, (Screen.height - 190f) / 2f, 300f, 190f);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("R'th");

            bool busy = _state == State.Connecting || _state == State.Reconnecting;
            GUI.enabled = !busy;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(50f));
            _name = GUILayout.TextField(_name, 12);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Server", GUILayout.Width(50f));
            _host = GUILayout.TextField(_host, 64);
            GUILayout.EndHorizontal();

            bool enter = Event.current.type == EventType.KeyDown &&
                         (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            if ((GUILayout.Button("Log in") || enter) && !busy) LogIn(_name, _host);
            GUI.enabled = true;

            if (busy && _state == State.Reconnecting && GUILayout.Button("Cancel"))
            {
                DropClient();
                _state = State.Login;
                _status = "";
            }
            GUILayout.Label(_status);
            GUILayout.EndArea();
        }
    }
}
