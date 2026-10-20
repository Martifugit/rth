using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>
    /// The in-game interface: a UI Toolkit document holding movable windows of panels. Finds
    /// every <see cref="UiPanel"/> by itself, lays them out by <see cref="UiDefaultLayout"/> or
    /// the player's saved layout, refreshes the visible ones each frame, and answers whether the
    /// pointer is over the interface (so clicks there don't reach the world) or the player is
    /// typing (so keys don't trigger hotkeys).
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        private const string LayoutPref = "rth.ui.layout.v2";
        private const string LockedPref = "rth.ui.locked";
        private const string ScalePref = "rth.ui.scale";

        public static UiRoot Instance { get; private set; }

        public GameSession session;
        public WorldView world;
        public CameraRig cameraRig;
        public RegionStreamer regions;

        private UIDocument _document;
        private PanelSettings _settings;
        private VisualElement _root;
        private VisualElement _windowLayer;
        private Label _tooltip;
        private Label _hover;
        private VisualElement _ghost;
        private VisualElement _ghostIcon;
        private Label _ghostText;
        private readonly List<UiPanel> _panels = new List<UiPanel>();
        private readonly List<UiWindow> _windows = new List<UiWindow>();
        private Vector2 _lastScreen;
        private bool _built;

        public IReadOnlyList<UiWindow> Windows => _windows;
        public IReadOnlyList<UiPanel> Panels => _panels;

        public T Panel<T>() where T : UiPanel => _panels.OfType<T>().FirstOrDefault();

        public bool Locked
        {
            get => PlayerPrefs.GetInt(LockedPref, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(LockedPref, value ? 1 : 0);
                _root?.EnableInClassList("rth-root--locked", value);
            }
        }

        public float Scale
        {
            get => PlayerPrefs.GetFloat(ScalePref, 1f);
            set
            {
                float s = Mathf.Clamp(value, 0.75f, 2f);
                PlayerPrefs.SetFloat(ScalePref, s);
                if (_settings != null) _settings.scale = s;
            }
        }

        /// <summary>The panel's size in its own pixels (screen pixels divided by the interface scale).</summary>
        public Vector2 ScreenSize => _root?.panel != null ? _root.panel.visualTree.layout.size : new Vector2(Screen.width, Screen.height);

        private void Awake() => Instance = this;

        // A script reload during Play resets statics but keeps this object: register again.
        private void OnEnable() => Instance = this;

        private void Start()
        {
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _settings.name = "R'th interface";
            _settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/RthTheme");
            _settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _settings.scale = Scale;
            _settings.sortingOrder = 0;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = _settings;
            _root = _document.rootVisualElement;
            var sheet = Resources.Load<StyleSheet>("UI/Rth");
            if (sheet != null) _root.styleSheets.Add(sheet);
            _root.AddToClassList("rth-root");
            var font = Resources.Load<Font>("UI/Fonts/PixelifySans");
            if (font != null) _root.style.unityFontDefinition = FontDefinition.FromFont(font);
            _root.pickingMode = PickingMode.Ignore;
            _root.EnableInClassList("rth-root--locked", Locked);
            // IMGUI windows and menus (bank, shop, dialogue, right-click) draw over these windows:
            // a click meant for them must not also reach the window underneath.
            _root.RegisterCallback<PointerDownEvent>(GuardImgui, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerUpEvent>(GuardImgui, TrickleDown.TrickleDown);
            _root.RegisterCallback<ClickEvent>(GuardImgui, TrickleDown.TrickleDown);

            _windowLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _windowLayer.style.position = Position.Absolute;
            _windowLayer.style.left = _windowLayer.style.top = _windowLayer.style.right = _windowLayer.style.bottom = 0;
            _root.Add(_windowLayer);

            // What a left-click would do, top left, as in the classic games.
            _hover = UiKit.Text(_root, "", "hover-text");
            _hover.pickingMode = PickingMode.Ignore;

            _tooltip = UiKit.Text(_root, "", "tooltip");
            _tooltip.pickingMode = PickingMode.Ignore;
            _tooltip.style.display = DisplayStyle.None;
            _ghost = UiKit.El(_root, "drag-ghost");
            _ghost.pickingMode = PickingMode.Ignore;
            _ghost.style.display = DisplayStyle.None;
            _ghostIcon = UiKit.Icon(_ghost, null, 22f);
            _ghostIcon.style.marginRight = 4f;
            _ghostText = UiKit.Text(_ghost, "");

            foreach (Type type in typeof(UiPanel).Assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(UiPanel).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null) continue;
                var panel = (UiPanel)Activator.CreateInstance(type);
                panel.Create(this);
                _panels.Add(panel);
            }
            _panels.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
        }

        private void Update()
        {
            if (_root == null) return;
            bool inGame = session != null && session.IsInGame;
            _root.style.display = inGame ? DisplayStyle.Flex : DisplayStyle.None;
            if (!inGame)
            {
                HideTooltip();
                return;
            }

            // The panel only knows its size after its first layout pass.
            Vector2 screen = ScreenSize;
            if (screen.x < 1f) return;
            if (!_built)
            {
                _built = true;
                if (!LoadLayout()) DefaultLayout();
                _lastScreen = screen;
            }
            if (screen != _lastScreen)
            {
                _lastScreen = screen;
                foreach (UiWindow w in _windows) w.Apply(screen);
            }

            _hover.text = PopupMenu.IsOpen ? "" : session.HoverText;

            foreach (UiPanel panel in _panels)
            {
                bool? serverOpen = panel.ServerOpen;
                if (serverOpen == true && panel.Window == null) Open(panel);
                else if (serverOpen == false && panel.Window != null) Close(panel, byPlayer: false);
                panel.Observe();
                if (panel.IsVisible) panel.Refresh();
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !IsTyping)
                foreach (UiPanel panel in _panels)
                    if (panel.Hotkey != Key.None && keyboard[panel.Hotkey].wasPressedThisFrame) Toggle(panel);
        }

        // ------------------------------------------------------------------ input questions

        /// <summary>Whether a screen position (bottom-left origin, as the Input System reports) is over a window.</summary>
        public static bool IsPointerOverUi(Vector2 screenPosition)
        {
            UiRoot ui = Instance;
            if (ui == null || ui._root?.panel == null || ui._root.resolvedStyle.display == DisplayStyle.None) return false;
            Vector2 p = RuntimePanelUtils.ScreenToPanel(ui._root.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return ui._root.panel.Pick(p) != null;
        }

        /// <summary>Whether a text field has focus, so keys are typing rather than hotkeys.</summary>
        public static bool IsTyping
        {
            get
            {
                Focusable focused = Instance?._root?.panel?.focusController?.focusedElement;
                for (var e = focused as VisualElement; e != null; e = e.parent)
                    if (e is TextField) return true;
                return false;
            }
        }

        private void GuardImgui<T>(T e) where T : EventBase, IPointerEvent
        {
            if (CombatHud.ClaimsContain(PanelToGui(e.position))) e.StopImmediatePropagation();
        }

        /// <summary>Opens a right-click menu at a panel position.</summary>
        public void Menu(Vector2 panelPosition, string title, List<(string Label, Action Action)> options) =>
            PopupMenu.Open(PanelToGui(panelPosition), title, options);

        /// <summary>Converts an IMGUI position (top-left origin, screen pixels) to a panel position.</summary>
        public Vector2 GuiToPanel(Vector2 gui) => gui / (_settings != null ? _settings.scale : 1f);

        /// <summary>Shows a menu element at a panel position, kept on screen, above everything.</summary>
        public void ShowMenu(VisualElement menu, Vector2 at)
        {
            HideTooltip();
            _root.Add(menu);
            menu.style.position = Position.Absolute;
            menu.style.left = at.x;
            menu.style.top = at.y;
            menu.BringToFront();
            // Once laid out, nudge it back on screen and centre it under the pointer like the classic menu.
            menu.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                Vector2 screen = ScreenSize;
                Vector2 size = menu.layout.size;
                menu.style.left = Mathf.Clamp(at.x - size.x / 2f, 0f, Mathf.Max(0f, screen.x - size.x));
                menu.style.top = Mathf.Clamp(at.y - 4f, 0f, Mathf.Max(0f, screen.y - size.y));
            });
        }

        /// <summary>Converts a panel position (top-left origin, panel pixels) to an IMGUI position for <see cref="PopupMenu"/>.</summary>
        public Vector2 PanelToGui(Vector2 panelPosition) => panelPosition * (_settings != null ? _settings.scale : 1f);

        // ------------------------------------------------------------------ tooltip and drag ghost

        public void ShowTooltip(string text, Vector2 at)
        {
            if (string.IsNullOrEmpty(text) || _ghost.style.display == DisplayStyle.Flex)
            {
                HideTooltip();
                return;
            }
            _tooltip.text = text;
            _tooltip.style.display = DisplayStyle.Flex;
            _tooltip.BringToFront();
            Vector2 screen = ScreenSize;
            float w = Mathf.Max(60f, _tooltip.resolvedStyle.width);
            float h = Mathf.Max(18f, _tooltip.resolvedStyle.height);
            float x = at.x + 16f + w > screen.x ? at.x - 12f - w : at.x + 16f;
            float y = Mathf.Clamp(at.y + 16f, 0f, Mathf.Max(0f, screen.y - h));
            _tooltip.style.left = Mathf.Max(0f, x);
            _tooltip.style.top = y;
        }

        public void HideTooltip()
        {
            if (_tooltip != null) _tooltip.style.display = DisplayStyle.None;
        }

        public void ShowDragGhost(string text, Texture2D icon, Vector2 at)
        {
            HideTooltip();
            _ghostText.text = text;
            _ghostText.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            UiKit.SetIcon(_ghostIcon, icon);
            _ghostIcon.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None;
            _ghost.style.display = DisplayStyle.Flex;
            _ghost.BringToFront();
            _ghost.style.left = at.x + 8f;
            _ghost.style.top = at.y + 8f;
        }

        public void HideDragGhost() => _ghost.style.display = DisplayStyle.None;

        // ------------------------------------------------------------------ opening and closing panels

        public void Toggle(UiPanel panel)
        {
            if (panel.Window == null) Open(panel);
            else if (panel.Window.Active != panel)
            {
                panel.Window.Activate(panel);
                panel.Window.Root.BringToFront();
            }
            else Close(panel);
        }

        /// <summary>
        /// Opens a panel: into a window already holding panels from its default group, or a new
        /// window where its group belongs.
        /// </summary>
        public void Open(UiPanel panel)
        {
            if (panel.Window != null)
            {
                panel.Window.Activate(panel);
                panel.Window.Root.BringToFront();
                return;
            }
            UiWindow home = _windows.FirstOrDefault(w => !w.IsBare && w.Panels.Any(p => p.DefaultWindow == panel.DefaultWindow));
            if (home != null && !panel.Bare)
            {
                home.Add(panel);
                home.Root.BringToFront();
            }
            else if (_placements.TryGetValue(panel.Id, out SavedWindow last))
            {
                CreateWindow(last.anchor, last.offset, last.size, panel);
            }
            else
            {
                UiDefaultLayout.Placement place = UiDefaultLayout.Windows.TryGetValue(panel.DefaultWindow, out UiDefaultLayout.Placement p)
                    ? p : UiDefaultLayout.Centred(panel);
                CreateWindow(place.Anchor, place.Offset, place.Size, panel);
            }
            SaveLayout();
        }

        public void Close(UiPanel panel, bool byPlayer = true)
        {
            UiWindow window = panel.Window;
            if (window == null) return;
            if (window.Panels.Count == 1) Remember(window);
            window.Remove(panel);
            if (window.Panels.Count == 0) RemoveWindow(window);
            if (byPlayer) panel.OnClosedByPlayer();
            SaveLayout();
        }

        public void CloseWindow(UiWindow window)
        {
            if (window.Panels.Count == 1) Remember(window);
            foreach (UiPanel p in window.Panels.ToList())
            {
                window.Remove(p);
                p.OnClosedByPlayer();
            }
            RemoveWindow(window);
            SaveLayout();
        }

        /// <summary>Where a panel last stood on its own, so it reopens there.</summary>
        private readonly Dictionary<string, SavedWindow> _placements = new Dictionary<string, SavedWindow>();

        private void Remember(UiWindow window) =>
            _placements[window.Panels[0].Id] = new SavedWindow { panels = new List<string> { window.Panels[0].Id }, anchor = window.Anchor, offset = window.Offset, size = window.Size };

        private UiWindow CreateWindow(Vector2 anchor, Vector2 offset, Vector2 size, params UiPanel[] panels)
        {
            var window = new UiWindow(this) { Anchor = anchor, Offset = offset, Size = size };
            foreach (UiPanel p in panels) window.Add(p, activate: false);
            if (window.Panels.Count > 0) window.Activate(window.Panels[0]);
            _windows.Add(window);
            _windowLayer.Add(window.Root);
            window.Apply(ScreenSize);
            return window;
        }

        private void RemoveWindow(UiWindow window)
        {
            _windows.Remove(window);
            window.Root.RemoveFromHierarchy();
        }

        /// <summary>A tab dropped somewhere: onto another window's tab strip joins it; anywhere else gets its own window.</summary>
        public void DropPanel(UiPanel panel, Vector2 at)
        {
            UiWindow from = panel.Window;
            UiWindow target = _windows.FirstOrDefault(w => w != from && w.AcceptsDropAt(at));
            if (target != null)
            {
                target.Add(panel);
                target.Root.BringToFront();
            }
            else if (from != null && from.Panels.Count == 1)
            {
                // Its own window: just move it.
                from.Reanchor(ScreenSize, at - new Vector2(20f, 10f));
                from.Apply(ScreenSize);
                return;
            }
            else
            {
                Vector2 size = from != null ? from.Size : panel.DefaultSize;
                UiWindow window = CreateWindow(Vector2.zero, Vector2.zero, size, panel);
                window.Reanchor(ScreenSize, at - new Vector2(20f, 10f));
                window.Apply(ScreenSize);
            }
            if (from != null && from.Panels.Count == 0) RemoveWindow(from);
            SaveLayout();
        }

        // ------------------------------------------------------------------ layouts

        public void ResetLayout()
        {
            _placements.Clear();
            foreach (UiWindow w in _windows.ToList())
            {
                foreach (UiPanel p in w.Panels.ToList()) w.Remove(p);
                RemoveWindow(w);
            }
            DefaultLayout();
            SaveLayout();
        }

        private void DefaultLayout()
        {
            foreach (IGrouping<string, UiPanel> group in _panels.Where(p => p.OpenByDefault && p.ServerOpen == null).GroupBy(p => p.DefaultWindow))
            {
                UiPanel first = group.First();
                UiDefaultLayout.Placement place = UiDefaultLayout.Windows.TryGetValue(group.Key, out UiDefaultLayout.Placement p)
                    ? p : UiDefaultLayout.Centred(first);
                CreateWindow(place.Anchor, place.Offset, place.Size, group.ToArray());
            }
        }

        [Serializable]
        private sealed class SavedWindow
        {
            public List<string> panels = new List<string>();
            public string active;
            public Vector2 anchor;
            public Vector2 offset;
            public Vector2 size;
        }

        [Serializable]
        private sealed class SavedLayout
        {
            public List<SavedWindow> windows = new List<SavedWindow>();
            /// <summary>Every panel that existed when saved: a panel added since opens by default.</summary>
            public List<string> known = new List<string>();
            /// <summary>Where closed panels last stood.</summary>
            public List<SavedWindow> placements = new List<SavedWindow>();
        }

        public void SaveLayout()
        {
            if (!_built) return;
            var layout = new SavedLayout { known = _panels.Select(p => p.Id).ToList(), placements = _placements.Values.ToList() };
            foreach (UiWindow w in _windows)
            {
                // Server windows reopen when the server says, not at login.
                if (w.Panels.Any(p => p.ServerOpen != null))
                {
                    if (w.Panels.Count == 1) Remember(w);
                    continue;
                }
                layout.windows.Add(new SavedWindow
                {
                    panels = w.Panels.Select(p => p.Id).ToList(),
                    active = w.Active?.Id,
                    anchor = w.Anchor,
                    offset = w.Offset,
                    size = w.Size,
                });
            }
            layout.placements = _placements.Values.ToList();
            PlayerPrefs.SetString(LayoutPref, JsonUtility.ToJson(layout));
        }

        private bool LoadLayout()
        {
            string json = PlayerPrefs.GetString(LayoutPref, "");
            if (string.IsNullOrEmpty(json)) return false;
            SavedLayout layout;
            try
            {
                layout = JsonUtility.FromJson<SavedLayout>(json);
            }
            catch (ArgumentException)
            {
                return false;
            }
            if (layout?.windows == null) return false;

            var byId = _panels.ToDictionary(p => p.Id);
            if (layout.placements != null)
                foreach (SavedWindow placed in layout.placements)
                    if (placed.panels.Count == 1) _placements[placed.panels[0]] = placed;
            foreach (SavedWindow saved in layout.windows)
            {
                UiPanel[] panels = saved.panels.Where(byId.ContainsKey).Select(id => byId[id]).Where(p => p.Window == null).ToArray();
                if (panels.Length == 0) continue;
                UiWindow window = CreateWindow(saved.anchor, saved.offset, saved.size, panels);
                if (saved.active != null && byId.TryGetValue(saved.active, out UiPanel active) && active.Window == window) window.Activate(active);
            }
            foreach (UiPanel panel in _panels)
                if (panel.OpenByDefault && panel.ServerOpen == null && panel.Window == null && !layout.known.Contains(panel.Id)) Open(panel);
            return true;
        }

        private void OnApplicationQuit() => SaveLayout();
    }
}
