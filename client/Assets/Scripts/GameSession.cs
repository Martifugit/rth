using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Skills;
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
        private readonly Dictionary<string, ItemDef> _items = new Dictionary<string, ItemDef>();
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
        public ClientContent Content { get; private set; }
        public List<ItemSlot> Inventory { get; } = new List<ItemSlot>();
        public List<ItemSlot> Equipment { get; } = new List<ItemSlot>();
        public Dictionary<string, int> Stats { get; } = new Dictionary<string, int>();
        public Dictionary<string, long> SkillXp { get; } = new Dictionary<string, long>();

        /// <summary>The line of conversation on screen, or null when nobody's talking.</summary>
        public DialogueMessage Dialogue { get; private set; }
        /// <summary>The window the server has open (bank, shop), and its title.</summary>
        public InterfaceKind OpenInterface { get; private set; }
        public string InterfaceTitle { get; private set; } = "";
        public List<ItemSlot> Bank { get; } = new List<ItemSlot>();
        public ShopMessage Shop { get; private set; }
        public TaskMessage CurrentTask { get; private set; }
        public List<QuestEntry> Quests { get; } = new List<QuestEntry>();

        public int Level(string skill) =>
            Content == null ? 1 : Content.XpCurve.LevelForXp(SkillXp.TryGetValue(skill, out long xp) ? xp : 0);

        public string SkillName(string skill)
        {
            if (Content != null)
                foreach (SkillDef s in Content.Skills)
                    if (s.Id == skill) return s.Name;
            return skill;
        }

        /// <summary>Why an ability can't be used right now (level, reagents, phlegm, bile), or null.</summary>
        public string AbilityProblem(AbilityDef ability) =>
            PlayerState == null ? null : ability.CheckRequirements(PlayerState.Phlegm, PlayerState.Bile, Level, SkillName, CountInPack, id => Item(id)?.Name ?? id);

        public int CountInPack(string itemId)
        {
            int total = 0;
            foreach (ItemSlot s in Inventory)
                if (s.ItemId == itemId) total += s.Count;
            return total;
        }

        public NpcInfo Npc(string typeId)
        {
            if (Content == null || string.IsNullOrEmpty(typeId)) return null;
            foreach (NpcInfo n in Content.Npcs)
                if (n.Id == typeId) return n;
            return null;
        }
        /// <summary>What a left-click would do on whatever is under the pointer ("Attack Fen rat").</summary>
        public string HoverText { get; private set; } = "";
        public List<LogLine> Log { get; } = new List<LogLine>();
        /// <summary>The Assayer's last words, shown until dismissed.</summary>
        public string AssayerText { get; set; }

        /// <summary>The server tick right now, extrapolated from the last state message (fractional).</summary>
        public float EstimatedTick =>
            PlayerState == null ? 0f : PlayerState.Tick + (Time.time - _stateReceivedTime) / _tickSeconds;

        public float TickSeconds => _tickSeconds;

        public AbilityDef Ability(string id) => id != null && _abilities.TryGetValue(id, out AbilityDef a) ? a : null;

        public ItemDef Item(string id) => !string.IsNullOrEmpty(id) && _items.TryGetValue(id, out ItemDef d) ? d : null;

        private void LateUpdate() => CombatHud.ClearClaims();

        private void Start()
        {
            if (GetComponent<PopupMenu>() == null) gameObject.AddComponent<PopupMenu>();
            if (GetComponent<TownWindows>() == null) gameObject.AddComponent<TownWindows>().session = this;
            world.ItemLookup = Item;
            _name = PlayerPrefs.GetString(NamePref, "");
            _host = PlayerPrefs.GetString(HostPref, "127.0.0.1");
            _clickMarker = CreateClickMarker();
        }

        private void Update()
        {
            // A script reload during Play keeps our state but not the connection: reconnect.
            if ((_state == State.InGame || _state == State.Connecting) && _client == null)
            {
                world.Clear();
                PlayerState = null;
                _state = State.Reconnecting;
                _reconnectAttempts = 0;
                _status = "Reconnecting...";
                _nextReconnectTime = Time.unscaledTime;
            }

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
                    case ContentInfoMessage info:
                        Content = info.Parse();
                        _abilities.Clear();
                        foreach (AbilityDef a in Content.Abilities) _abilities[a.Id] = a;
                        _items.Clear();
                        foreach (ItemDef i in Content.Items) _items[i.Id] = i;
                        break;
                    case ContainerMessage container:
                        List<ItemSlot> target = container.Kind == ContainerKind.Equipment ? Equipment
                            : container.Kind == ContainerKind.Bank ? Bank
                            : Inventory;
                        target.Clear();
                        target.AddRange(container.Slots);
                        break;
                    case DialogueMessage line:
                        Dialogue = line.Open ? line : null;
                        break;
                    case InterfaceMessage window:
                        OpenInterface = window.Kind;
                        InterfaceTitle = window.Title;
                        if (window.Kind != InterfaceKind.None) PopupMenu.Close();
                        break;
                    case ShopMessage shop:
                        Shop = shop;
                        break;
                    case TaskMessage task:
                        CurrentTask = task;
                        break;
                    case QuestsMessage quests:
                        Quests.Clear();
                        Quests.AddRange(quests.Quests);
                        break;
                    case StatsMessage stats:
                        Stats.Clear();
                        foreach (KeyValuePair<string, int> s in stats.Stats) Stats[s.Key] = s.Value;
                        break;
                    case SkillsMessage skills:
                        SkillXp.Clear();
                        foreach (KeyValuePair<string, long> s in skills.Xp) SkillXp[s.Key] = s.Value;
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
                Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
                if (Dialogue != null)
                {
                    // In conversation the number keys pick options and space continues.
                    if (Dialogue.Options.Count == 0 && keyboard.spaceKey.wasPressedThisFrame) ChooseDialogue(-1);
                    for (int i = 0; i < digits.Length && i < Dialogue.Options.Count; i++)
                        if (keyboard[digits[i]].wasPressedThisFrame) ChooseDialogue(i);
                }
                else if (Content != null)
                {
                    for (int i = 0; i < digits.Length && i < Content.ActionBar.Count; i++)
                        if (keyboard[digits[i]].wasPressedThisFrame) UseAbility(Content.ActionBar[i]);
                }
                if (keyboard.escapeKey.wasPressedThisFrame && (Dialogue != null || OpenInterface != InterfaceKind.None)) CloseInterface();
            }

            if (mouse == null) return;
            Vector2 pointer = mouse.position.ReadValue();
            bool overHud = CombatHud.IsPointerOverHud(pointer) || PopupMenu.IsOpen;
            string title = "";
            List<(string Label, Action Action)> options = overHud ? null : WorldOptionsAt(pointer, out title);
            HoverText = options != null && options.Count > 0 ? options[0].Label : "";
            if (options == null || options.Count == 0) return;

            // Left-click does the first option; right-click lists them all.
            if (mouse.leftButton.wasPressedThisFrame) options[0].Action();
            else if (mouse.rightButton.wasPressedThisFrame) PopupMenu.Open(new Vector2(pointer.x, Screen.height - pointer.y), title, options);
        }

        /// <summary>Everything you could do with what's under the pointer, best first: attack, take, walk.</summary>
        private List<(string Label, Action Action)> WorldOptionsAt(Vector2 pointer, out string title)
        {
            title = "Choose option";
            var options = new List<(string Label, Action Action)>();
            Camera cam = Camera.main;
            if (cam == null) return options;

            RaycastHit[] hits = Physics.RaycastAll(cam.ScreenPointToRay(pointer), 500f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            EntityView npc = null;
            GroundItemView item = null;
            Rth.MapTools.MapObject mapObject = null;
            Vector3? ground = null;
            foreach (RaycastHit h in hits)
            {
                EntityView e = h.collider.GetComponentInParent<EntityView>();
                if (e != null)
                {
                    if (npc == null && e.Kind == EntityKind.Npc && !e.IsDying) npc = e;
                    continue;
                }
                GroundItemView g = h.collider.GetComponent<GroundItemView>();
                if (g != null)
                {
                    if (item == null) item = g;
                    continue;
                }
                Rth.MapTools.MapObject o = h.collider.GetComponentInParent<Rth.MapTools.MapObject>();
                if (o != null)
                {
                    if (mapObject == null) mapObject = o;
                    continue;
                }
                if (ground == null) ground = h.point + cam.transform.forward * 0.01f;
            }

            if (npc != null)
            {
                int id = npc.Id;
                NpcInfo info = Npc(npc.TypeId);
                if (info == null || info.Attackable) options.Add(("Attack " + npc.DisplayName, () => Attack(id)));
                if (info != null)
                {
                    foreach (ActionDef action in info.Actions)
                    {
                        string actionId = action.Id;
                        options.Add((action.Label + " " + npc.DisplayName, () => NpcAction(id, actionId)));
                    }
                    options.Add(("Examine " + npc.DisplayName, () => NpcAction(id, "examine")));
                }
            }
            ObjectDef objectDef = mapObject != null ? ObjectDefFor(mapObject.objectType) : null;
            if (objectDef != null)
            {
                string type = objectDef.Id;
                TileCoord tile = new TileCoord(mapObject.Tile.x, mapObject.Tile.y);
                foreach (ActionDef action in objectDef.Actions)
                {
                    string actionId = action.Id;
                    options.Add((action.Label + " " + objectDef.Name, () => ObjectAction(type, tile, actionId)));
                }
                options.Add(("Examine " + objectDef.Name, () => ObjectAction(type, tile, "examine")));
            }
            TileCoord? itemTile = item != null ? item.Tile : ground.HasValue ? TileMath.FromWorld(ground.Value) : (TileCoord?)null;
            if (itemTile.HasValue)
            {
                var piles = new List<GroundItemView>(world.GroundItemsOn(itemTile.Value));
                piles.Sort((a, b) => (b == item).CompareTo(a == item)); // the one under the pointer first
                foreach (GroundItemView pile in piles)
                {
                    int id = pile.Id;
                    string name = Item(pile.ItemId)?.Name ?? pile.ItemId;
                    options.Add(("Take " + name + (pile.Count > 1 ? " (" + pile.Count + ")" : ""), () => Pickup(id)));
                }
            }
            if (ground.HasValue)
            {
                TileCoord tile = TileMath.FromWorld(ground.Value);
                options.Add(("Walk here", () => WalkTo(tile)));
            }
            return options;
        }

        public void NpcAction(int npcId, string actionId)
        {
            if (_state != State.InGame) return;
            _client.Send(new NpcActionMessage { NpcId = npcId, ActionId = actionId });
            EntityView view = world.Find(npcId);
            if (view != null && actionId != "examine") ShowClickMarker(view.Tile, new Color(0.9f, 0.85f, 0.2f));
        }

        /// <summary>Picks a dialogue option, or continues with -1.</summary>
        public void ChooseDialogue(int option)
        {
            if (_state != State.InGame || Dialogue == null) return;
            _client.Send(new DialogueChoiceMessage { Option = option });
        }

        public void CloseInterface()
        {
            if (_state != State.InGame) return;
            _client.Send(new CloseInterfaceMessage());
            Dialogue = null;
        }

        /// <summary>Slot -1 deposits the whole pack; count 0 means all of that item.</summary>
        public void BankDeposit(int slot, int count) => Send(new BankDepositMessage { Slot = slot, Count = count });
        public void BankWithdraw(int slot, int count) => Send(new BankWithdrawMessage { Slot = slot, Count = count });
        public void ShopBuy(int index, int count) => Send(new ShopBuyMessage { Index = index, Count = count });
        public void ShopSell(int slot, int count) => Send(new ShopSellMessage { Slot = slot, Count = count });

        private void Send(Message message)
        {
            if (_state == State.InGame) _client.Send(message);
        }

        private ObjectDef ObjectDefFor(string type)
        {
            if (Content == null) return null;
            foreach (ObjectDef d in Content.Objects)
                if (d.Id == type) return d;
            return null;
        }

        /// <summary>Uses an action on a world object (walk beside it, then e.g. butcher).</summary>
        public void ObjectAction(string objectType, TileCoord tile, string actionId)
        {
            if (_state != State.InGame) return;
            _client.Send(new ObjectActionMessage { ObjectType = objectType, Plane = 0, X = tile.X, Y = tile.Y, ActionId = actionId });
            if (actionId != "examine") ShowClickMarker(tile, new Color(0.9f, 0.15f, 0.1f));
        }

        public void Pickup(int groundItemId)
        {
            if (_state != State.InGame) return;
            _client.Send(new PickupMessage { GroundItemId = groundItemId });
            GroundItemView view = world.FindGroundItem(groundItemId);
            if (view != null) ShowClickMarker(view.Tile, new Color(0.9f, 0.15f, 0.1f));
        }

        /// <summary>Uses one of an item's actions (see <see cref="ItemActions"/>).</summary>
        public void UseItem(ContainerKind container, int slot, string actionId)
        {
            if (_state != State.InGame) return;
            _client.Send(new ItemActionMessage { Container = container, Slot = slot, ActionId = actionId });
        }

        public void MoveItem(int from, int to)
        {
            if (_state != State.InGame) return;
            _client.Send(new MoveItemMessage { From = from, To = to });
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
            Inventory.Clear();
            Equipment.Clear();
            Stats.Clear();
            SkillXp.Clear();
            Bank.Clear();
            Dialogue = null;
            OpenInterface = InterfaceKind.None;
            Shop = null;
            CurrentTask = null;
            Quests.Clear();
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
