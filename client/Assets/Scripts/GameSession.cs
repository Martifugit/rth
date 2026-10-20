using System;
using System.Threading.Tasks;
using Rth.Shared.Net;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rth.Client
{
    /// <summary>
    /// Connection lifecycle (login, play, reconnect) and the player's input. Everything here is an
    /// intent sent to the server; what actually happens arrives back as world updates.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        private const string NamePref = "rth.name";
        private const string HostPref = "rth.host";
        private const int MaxReconnectAttempts = 10;
        private const float ReconnectDelaySeconds = 2f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public WorldView world;
        public CameraRig cameraRig;
        public int port = Protocol.DefaultPort;

        private enum State
        {
            Login,
            Connecting,
            InGame,
            Reconnecting,
        }

        private State _state = State.Login;
        private GameClient _client;
        private Task _connectTask;
        private string _name = "";
        private string _host = "127.0.0.1";
        private string _status = "";
        private bool _run;
        private int _reconnectAttempts;
        private float _nextReconnectTime;
        private Rect _hudRect;
        private Transform _clickMarker;
        private float _clickMarkerHideTime;

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

        public bool IsInGame => _state == State.InGame;

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
                    world.Begin(ok.PlayerId, ok.TickMillis / 1000f);
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
                if (message is WorldUpdateMessage update) world.Apply(update);

            if (!_client.IsConnected)
            {
                // Dropped mid-game: keep trying to get back in with the same name.
                DropClient();
                world.Clear();
                _state = State.Reconnecting;
                _reconnectAttempts = 0;
                _status = "Connection lost. Reconnecting...";
                _nextReconnectTime = Time.unscaledTime + 0.5f;
            }
        }

        private void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) _run = !_run;

            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            Vector2 pointer = mouse.position.ReadValue();
            if (_hudRect.Contains(new Vector2(pointer.x, Screen.height - pointer.y))) return;

            Camera cam = Camera.main;
            if (cam == null || !Physics.Raycast(cam.ScreenPointToRay(pointer), out RaycastHit hit, 500f)) return;

            WalkTo(TileMath.FromWorld(hit.point + cam.transform.forward * 0.01f));
        }

        /// <summary>Asks the server to walk (or run, if toggled) the local player to a tile.</summary>
        public void WalkTo(TileCoord tile)
        {
            if (_state != State.InGame) return;
            _client.Send(new WalkToMessage { X = tile.X, Y = tile.Y, Run = _run });
            ShowClickMarker(tile);
        }

        private void Logout()
        {
            _client?.Send(new LogoutMessage());
            DropClient();
            world.Clear();
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
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, new Color(1f, 0.85f, 0.1f));
            marker.GetComponent<Renderer>().SetPropertyBlock(block);
            marker.SetActive(false);
            return marker.transform;
        }

        private void ShowClickMarker(TileCoord tile)
        {
            _clickMarker.position = TileMath.Center(tile) + Vector3.up * 0.03f;
            _clickMarker.gameObject.SetActive(true);
            _clickMarkerHideTime = Time.unscaledTime + 0.6f;
        }

        private void OnGUI()
        {
            if (_state == State.InGame) DrawHud();
            else DrawLogin();
        }

        private void DrawHud()
        {
            EntityView local = world.Local;
            _hudRect = new Rect(10f, 10f, 260f, 92f);
            GUILayout.BeginArea(_hudRect, GUI.skin.box);
            GUILayout.Label(local != null ? local.DisplayName + " at " + local.Tile : "Loading...");
            if (GUILayout.Button(_run ? "Run: ON  (R)" : "Run: off  (R)")) _run = !_run;
            if (GUILayout.Button("Log out")) Logout();
            GUILayout.EndArea();
        }

        private void DrawLogin()
        {
            _hudRect = new Rect((Screen.width - 300f) / 2f, (Screen.height - 190f) / 2f, 300f, 190f);
            GUILayout.BeginArea(_hudRect, GUI.skin.box);
            GUILayout.Label("R'th");

            bool busy = _state == State.Connecting || _state == State.Reconnecting;
            GUI.enabled = !busy;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(50f));
            GUI.SetNextControlName("name");
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
