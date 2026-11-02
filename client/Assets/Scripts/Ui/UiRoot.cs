using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>
    /// The in-game interface: a UI Toolkit document holding movable windows of panels, and modals
    /// on top. Finds every <see cref="UiPanel"/> and <see cref="UiModal"/> by itself, lays panels
    /// out by <see cref="UiDefaultLayout"/> or the player's saved layout, refreshes what's visible
    /// each frame, runs drag and drop between slots, and answers whether the pointer is over the
    /// interface (so clicks there don't reach the world) or the player is typing (so keys don't
    /// trigger hotkeys).
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        /// <summary>The single layout saved before layouts were kept per screen size: read once, to migrate.</summary>
        private const string LegacyLayoutPref = "rth.ui.layout.v3";
        /// <summary>A layout per screen-size bucket: this prefix and the bucket ("1920x1088").</summary>
        private const string LayoutPrefix = "rth.ui.layout.v4.";
        /// <summary>The buckets that have a saved layout, oldest first, ";" between them.</summary>
        private const string LayoutIndexPref = "rth.ui.layout.v4.index";
        private const string PresetPref = "rth.ui.preset";
        /// <summary>How many screen sizes keep a layout of their own.</summary>
        private const int MaxBuckets = 8;
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
        /// <summary>Under every window: widgets pinned to things in the world (a mining bar over its rock).</summary>
        public VisualElement WorldLayer { get; private set; }
        private VisualElement _windowLayer;
        private VisualElement _floatLayer;
        private VisualElement _landingLayer;
        private VisualElement _modalLayer;
        private Label _tooltip;
        private Label _hover;
        private VisualElement _ghost;
        private VisualElement _ghostIcon;
        private Label _ghostText;
        private readonly List<UiPanel> _panels = new List<UiPanel>();
        private readonly List<UiModal> _modals = new List<UiModal>();
        private readonly List<UiWindow> _windows = new List<UiWindow>();
        private readonly HashSet<UiPanel> _dismissed = new HashSet<UiPanel>();
        private Vector2 _lastScreen;
        private bool _built;
        private UiModal _openModal;

        public IReadOnlyList<UiWindow> Windows => _windows;
        public IReadOnlyList<UiPanel> Panels => _panels;
        public IReadOnlyList<UiModal> Modals => _modals;

        public T Panel<T>() where T : UiPanel => _panels.OfType<T>().FirstOrDefault();
        public T Modal<T>() where T : UiModal => _modals.OfType<T>().FirstOrDefault();

        /// <summary>Windows locked: they can't be moved, resized, closed or re-tabbed. Action bars have their own lock (<see cref="BarsLocked"/>).</summary>
        public bool Locked
        {
            get => PlayerPrefs.GetInt(LockedPref, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(LockedPref, value ? 1 : 0);
                _root?.EnableInClassList("rth-root--locked", value && !EditMode);
            }
        }

        /// <summary>
        /// Action bars locked: nothing is dragged onto, off or between bar slots, or out of the
        /// ability book and the hymns. Kept with the player (client state), apart from the window lock.
        /// </summary>
        public bool BarsLocked
        {
            get => session?.Bars?.Locked ?? false;
            set
            {
                if (session?.Bars != null) session.Bars.Locked = value;
            }
        }

        /// <summary>
        /// The interface scale: the player's choice (Settings), or until they choose, one that fits
        /// the screen (the layout is made for 1080 pixels high, so 720p gets 0.75, 1440p 1.25).
        /// </summary>
        public float Scale
        {
            get => PlayerPrefs.HasKey(ScalePref) ? PlayerPrefs.GetFloat(ScalePref, 1f) : AutoScale;
            set
            {
                float s = Mathf.Clamp(value, 0.75f, 2f);
                PlayerPrefs.SetFloat(ScalePref, s);
                if (_settings != null) _settings.scale = s;
            }
        }

        /// <summary>Whether the scale follows the screen (the player hasn't chosen one).</summary>
        public bool AutoScaled => !PlayerPrefs.HasKey(ScalePref);

        /// <summary>Goes back to the scale that fits the screen.</summary>
        public void UseAutoScale()
        {
            PlayerPrefs.DeleteKey(ScalePref);
            if (_settings != null) _settings.scale = AutoScale;
        }

        private static float AutoScale => Mathf.Clamp(Mathf.Round(Screen.height / 1080f * 4f) / 4f, 0.75f, 2f);

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
            _root.pickingMode = PickingMode.Ignore;
            _root.EnableInClassList("rth-root--locked", Locked);
            // Anything still drawn with IMGUI (the login box) sits over these windows: clicks meant
            // for it must not reach the window underneath.
            _root.RegisterCallback<PointerDownEvent>(GuardImgui, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerUpEvent>(GuardImgui, TrickleDown.TrickleDown);
            _root.RegisterCallback<ClickEvent>(GuardImgui, TrickleDown.TrickleDown);
            // The UI input binds WASD and the arrows to "navigate", which walks the keyboard focus
            // from window to window and into the chat box. Those keys belong to the game (the
            // camera, ::fly), and no window is driven from the keyboard, so focus never moves that way.
            _root.RegisterCallback<NavigationMoveEvent>(e =>
            {
                _root.focusController?.IgnoreEvent(e);
                e.StopPropagation();
            }, TrickleDown.TrickleDown);

            WorldLayer = Layer();
            _windowLayer = Layer();
            _floatLayer = Layer();
            _landingLayer = Layer();
            _hover = UiKit.Text(_root, "", "hover-text");
            _hover.pickingMode = PickingMode.Ignore;
            _modalLayer = Layer();

            _tooltip = UiKit.Text(_root, "", "tooltip");
            _tooltip.pickingMode = PickingMode.Ignore;
            _tooltip.style.display = DisplayStyle.None;
            _ghost = UiKit.El(_root, "drag-ghost");
            _ghost.pickingMode = PickingMode.Ignore;
            _ghost.style.display = DisplayStyle.None;
            _ghostIcon = UiKit.Icon(_ghost, null, 28f);
            _ghostText = UiKit.Text(_ghost, "");
            _ghostText.style.marginLeft = 4f;
            // The ghost follows the pointer: none of it may catch the pointer, or drops would land on it.
            _ghostIcon.pickingMode = _ghostText.pickingMode = PickingMode.Ignore;

            foreach (Type type in typeof(UiPanel).Assembly.GetTypes())
            {
                if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null) continue;
                if (typeof(UiPanel).IsAssignableFrom(type))
                {
                    var panel = (UiPanel)Activator.CreateInstance(type);
                    panel.Create(this);
                    _panels.Add(panel);
                }
                else if (typeof(UiModal).IsAssignableFrom(type))
                {
                    _modals.Add((UiModal)Activator.CreateInstance(type));
                }
            }
            _panels.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
        }

        private VisualElement Layer()
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
            _root.Add(layer);
            return layer;
        }

        private void Update()
        {
            if (_root == null) return;
            bool inGame = session != null && session.IsInGame;
            _root.style.display = inGame ? DisplayStyle.Flex : DisplayStyle.None;
            if (!inGame)
            {
                HideTooltip();
                CancelDrag();
                return;
            }

            // The panel only knows its size after its first layout pass.
            Vector2 screen = ScreenSize;
            if (screen.x < 1f) return;
            if (!_built)
            {
                _built = true;
                LayOutForScreen(screen);
                _lastScreen = screen;
            }
            if (screen != _lastScreen)
            {
                _lastScreen = screen;
                if (AutoScaled && _settings != null && !Mathf.Approximately(_settings.scale, AutoScale)) _settings.scale = AutoScale;
                string bucket = WindowRules.BucketOf(screen.x, screen.y);
                if (bucket != _bucket && SavedBuckets().Contains(bucket)) ChangeScreen(screen);
                else
                {
                    // No layout of its own yet: this one adapts (anchored, sizes kept as chosen) and becomes it.
                    _bucket = bucket;
                    foreach (UiWindow w in _windows) w.Apply(screen);
                    SettleDocked();
                }
            }

            _hover.text = PopupMenu.IsOpen || _drag != null ? "" : session.HoverText;

            foreach (UiPanel panel in _panels)
            {
                // Server windows follow the server, unless the player has just closed one and the
                // server hasn't caught up yet.
                bool? serverOpen = panel.ServerOpen;
                if (serverOpen == false) _dismissed.Remove(panel);
                if (serverOpen == true && panel.Window == null && !_dismissed.Contains(panel)) Open(panel);
                else if (serverOpen == false && panel.Window != null) Close(panel, byPlayer: false);
                panel.Observe();
                if (panel.IsVisible) panel.Refresh();
            }
            _openModal?.Refresh();

            UpdateDrag();
            UpdatePrompt();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || IsTyping) return;
            if (keyboard.escapeKey.wasPressedThisFrame && _openModal != null) CloseModal();
            else if (keyboard.escapeKey.wasPressedThisFrame && EditMode) SetEditMode(false);
            if (FlyCamera.Active) return; // its WASD, Q and E would open windows
            foreach (UiPanel panel in _panels)
                if (panel.Hotkey != Key.None && keyboard[panel.Hotkey].wasPressedThisFrame) Toggle(panel);
            foreach (UiModal modal in _modals)
                if (modal.Hotkey != Key.None && keyboard[modal.Hotkey].wasPressedThisFrame) ToggleModal(modal);
        }

        // ------------------------------------------------------------------ input questions

        /// <summary>Whether a screen position (bottom-left origin, as the Input System reports) is over a window.</summary>
        public static bool IsPointerOverUi(Vector2 screenPosition)
        {
            UiRoot ui = Instance;
            if (ui == null || ui._root?.panel == null || ui._root.resolvedStyle.display == DisplayStyle.None) return false;
            if (ui._openModal != null) return true; // a modal owns the screen
            Vector2 p = RuntimePanelUtils.ScreenToPanel(ui._root.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return ui._root.panel.Pick(p) != null;
        }

        /// <summary>Takes the keyboard away from whatever window element has it (a text field), so keys reach the game.</summary>
        public void ReleaseKeyboard() => (_root?.panel?.focusController?.focusedElement as VisualElement)?.Blur();

        /// <summary>Whether a text field has focus, so keys are typing rather than hotkeys.</summary>
        public static bool IsTyping => InTextField || WithinAFrame(_typedFrame);

        // The frame a text field last had focus: the key that ends typing (Enter, Escape) must not
        // also work as a hotkey in the frame the field lets go.
        private static int _typedFrame = -10;

        /// <summary>
        /// Whether a frame is this one or the one before. A frame from the future is not: with domain
        /// reload off, statics outlive Play while <see cref="Time.frameCount"/> starts again, and an old
        /// session's late frame must not read as "just now" (it held IsTyping on and swallowed keys).
        /// </summary>
        public static bool WithinAFrame(int frame)
        {
            int age = Time.frameCount - frame;
            return age >= 0 && age <= 1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _typedFrame = -10;

        private static bool InTextField
        {
            get
            {
                Focusable focused = Instance?._root?.panel?.focusController?.focusedElement;
                for (var e = focused as VisualElement; e != null; e = e.parent)
                    if (e is TextField) return true;
                return false;
            }
        }

        private void LateUpdate()
        {
            if (InTextField) _typedFrame = Time.frameCount;
        }

        public static bool IsModalOpen => Instance != null && Instance._openModal != null;

        private void GuardImgui<T>(T e) where T : EventBase, IPointerEvent
        {
            if (CombatHud.ClaimsContain(PanelToGui(e.position))) e.StopImmediatePropagation();
        }

        /// <summary>Opens a right-click menu at a panel position.</summary>
        public void Menu(Vector2 panelPosition, string title, List<(string Label, Action Action)> options) =>
            PopupMenu.Open(PanelToGui(panelPosition), title, options);

        /// <summary>Converts an IMGUI position (top-left origin, screen pixels) to a panel position.</summary>
        public Vector2 GuiToPanel(Vector2 gui) => gui / (_settings != null ? _settings.scale : 1f);

        /// <summary>Converts a panel position (top-left origin, panel pixels) to an IMGUI position for <see cref="PopupMenu"/>.</summary>
        public Vector2 PanelToGui(Vector2 panelPosition) => panelPosition * (_settings != null ? _settings.scale : 1f);

        private Vector2 PointerPanelPosition()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || _root?.panel == null) return Vector2.zero;
            Vector2 screen = mouse.position.ReadValue();
            return RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
        }

        /// <summary>Shows a menu element at a panel position, kept on screen, above everything.</summary>
        public void ShowMenu(VisualElement menu, Vector2 at)
        {
            HideTooltip();
            _root.Add(menu);
            menu.style.position = Position.Absolute;
            menu.style.left = at.x;
            menu.style.top = at.y;
            menu.BringToFront();
            // Once laid out, nudge it back on screen and centre it under the pointer like the classic
            // menu. Only once: moving it changes its geometry, and answering every change loops.
            EventCallback<GeometryChangedEvent> place = null;
            place = _ =>
            {
                menu.UnregisterCallback(place);
                Vector2 screen = ScreenSize;
                Vector2 size = menu.layout.size;
                menu.style.left = Mathf.Clamp(at.x - size.x / 2f, 0f, Mathf.Max(0f, screen.x - size.x));
                menu.style.top = Mathf.Clamp(at.y - 4f, 0f, Mathf.Max(0f, screen.y - size.y));
            };
            menu.RegisterCallback(place);
        }

        // ------------------------------------------------------------------ tooltip

        public void ShowTooltip(string text, Vector2 at)
        {
            if (string.IsNullOrEmpty(text) || _drag != null || _ghost.style.display == DisplayStyle.Flex)
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

        public void ShowDragGhost(string text, Texture2D icon, Color tint, Vector2 at)
        {
            HideTooltip();
            _ghostText.text = text ?? "";
            _ghostText.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            UiKit.SetIcon(_ghostIcon, icon, tint);
            _ghostIcon.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None;
            _ghost.style.display = DisplayStyle.Flex;
            _ghost.BringToFront();
            _ghost.style.left = at.x + 10f;
            _ghost.style.top = at.y + 10f;
        }

        public void HideDragGhost() => _ghost.style.display = DisplayStyle.None;

        // ------------------------------------------------------------------ drag and drop

        /// <summary>
        /// Something being dragged: an item from a slot, an ability from the book, an action bar
        /// slot. Drop targets decide whether they take it; dropping on nothing runs
        /// <see cref="OnNowhere"/> (taking an ability off a bar).
        /// </summary>
        public sealed class Drag
        {
            public object Data;
            public Texture2D Icon;
            public Color Tint = Color.white;
            public Action OnNowhere;
        }

        private sealed class DropTarget
        {
            public Func<object, bool> Accepts;
            public Action<object> Drop;
        }

        private readonly Dictionary<VisualElement, DropTarget> _dropTargets = new Dictionary<VisualElement, DropTarget>();
        private Drag _drag;
        private VisualElement _dropHighlight;

        public bool IsDragging => _drag != null;

        /// <summary>Makes an element a place things can be dropped.</summary>
        public void AddDropTarget(VisualElement element, Func<object, bool> accepts, Action<object> drop) =>
            _dropTargets[element] = new DropTarget { Accepts = accepts, Drop = drop };

        /// <summary>
        /// Makes an element something to drag or click: pressing and moving a few pixels starts the
        /// drag <paramref name="drag"/> describes (null: not draggable now); pressing and releasing
        /// in place is a click.
        /// </summary>
        public void DragSource(VisualElement element, Func<Drag> drag, Action click = null)
        {
            // The press captures the pointer, so the moves that start a drag still arrive after
            // the pointer has left the (small) slot, and a release anywhere ends the press.
            bool pressed = false;
            Vector2 pressedAt = default;
            element.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                pressed = true;
                pressedAt = e.position;
                element.CapturePointer(e.pointerId);
            });
            element.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!pressed || ((Vector2)e.position - pressedAt).magnitude < 6f) return;
                pressed = false;
                element.ReleasePointer(e.pointerId);
                Drag d = IsDragging ? null : drag();
                if (d != null) BeginDrag(d);
            });
            element.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button != 0 || !pressed) return;
                pressed = false;
                element.ReleasePointer(e.pointerId);
                if (element.worldBound.Contains(e.position)) click?.Invoke();
            });
            element.RegisterCallback<PointerCaptureOutEvent>(_ => pressed = false);
        }

        /// <summary>
        /// Starts dragging (call once the pointer has moved a few pixels with the button held).
        /// The drag follows the mouse until the button is released.
        /// </summary>
        public void BeginDrag(Drag drag)
        {
            _drag = drag;
            HideTooltip();
        }

        private void UpdateDrag()
        {
            if (_drag == null) return;
            Mouse mouse = Mouse.current;
            Vector2 at = PointerPanelPosition();
            ShowDragGhost(null, _drag.Icon, _drag.Tint, at - new Vector2(24f, 24f));
            VisualElement target = TargetAt(at, out DropTarget drop);
            Highlight(target != null && drop.Accepts(_drag.Data) ? target : null);
            if (mouse != null && mouse.leftButton.isPressed) return;

            Drag finished = _drag;
            CancelDrag();
            if (target != null && drop.Accepts(finished.Data)) drop.Drop(finished.Data);
            else if (!IsOverAnyWindow(at)) finished.OnNowhere?.Invoke();
        }

        private VisualElement TargetAt(Vector2 at, out DropTarget drop)
        {
            drop = null;
            for (VisualElement e = _root.panel.Pick(at); e != null; e = e.parent)
                if (_dropTargets.TryGetValue(e, out drop)) return e;
            return null;
        }

        private bool IsOverAnyWindow(Vector2 at) => _root.panel.Pick(at) != null;

        private void Highlight(VisualElement target)
        {
            if (_dropHighlight == target) return;
            _dropHighlight?.RemoveFromClassList("slot--drop");
            _dropHighlight = target;
            _dropHighlight?.AddToClassList("slot--drop");
        }

        public void CancelDrag()
        {
            _drag = null;
            Highlight(null);
            HideDragGhost();
        }

        // ------------------------------------------------------------------ number prompt

        private VisualElement _prompt;
        private TextField _promptField;
        private Action<int> _promptDone;

        /// <summary>Asks for a number ("Withdraw how many?"). Enter accepts; Escape cancels.</summary>
        public void PromptNumber(string question, Action<int> done)
        {
            ClosePrompt();
            _promptDone = done;
            _prompt = UiKit.El(_root, "modal-backdrop");
            _prompt.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            VisualElement box = UiKit.El(_prompt, "card");
            box.style.width = 280f;
            box.style.paddingLeft = box.style.paddingRight = box.style.paddingTop = box.style.paddingBottom = 14f;
            UiKit.Heading(box, question);
            _promptField = new TextField { maxLength = 10 };
            _promptField.AddToClassList("input");
            box.Add(_promptField);
            Label hint = UiKit.Text(box, "Enter a number. K and M work: 5k, 1.5m.", "muted", "small");
            hint.style.marginTop = 4f;
            VisualElement buttons = UiKit.El(box, "row");
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 8f;
            UiKit.Button(buttons, "Cancel", ClosePrompt);
            UiKit.Button(buttons, "OK", AcceptPrompt);
            _promptField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) AcceptPrompt();
                else if (e.keyCode == KeyCode.Escape) ClosePrompt();
            }, TrickleDown.TrickleDown);
            _prompt.schedule.Execute(() => _promptField.Focus());
        }

        private void AcceptPrompt()
        {
            int? n = ParseAmount(_promptField?.value);
            Action<int> done = _promptDone;
            ClosePrompt();
            if (n.HasValue && n.Value > 0) done?.Invoke(n.Value);
        }

        private void ClosePrompt()
        {
            _prompt?.RemoveFromHierarchy();
            _prompt = null;
            _promptField = null;
            _promptDone = null;
        }

        private void UpdatePrompt()
        {
            if (_prompt != null) _prompt.BringToFront();
        }

        /// <summary>"250", "5k", "1.5m" to a count.</summary>
        public static int? ParseAmount(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim().ToLowerInvariant();
            double scale = 1;
            if (text.EndsWith("k")) { scale = 1_000; text = text.Substring(0, text.Length - 1); }
            else if (text.EndsWith("m")) { scale = 1_000_000; text = text.Substring(0, text.Length - 1); }
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value)) return null;
            double total = value * scale;
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        // ------------------------------------------------------------------ modals

        /// <summary>Opens a modal (closing any other), on a tab and focused on something if given.</summary>
        public void OpenModal(UiModal modal, string tab = null, string focus = null)
        {
            if (modal == null) return;
            if (_openModal != modal)
            {
                CloseModal();
                if (modal.Root == null) modal.Create(this);
                VisualElement backdrop = UiKit.El(_modalLayer, "modal-backdrop");
                backdrop.pickingMode = PickingMode.Position;
                backdrop.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.target == backdrop) CloseModal(); // a click outside the modal
                });
                backdrop.Add(modal.Root);
                _openModal = modal;
            }
            modal.Show(tab, focus);
            HideTooltip();
        }

        public void OpenModal<T>(string tab = null, string focus = null) where T : UiModal => OpenModal(Modal<T>(), tab, focus);

        public void ToggleModal(UiModal modal)
        {
            if (_openModal == modal) CloseModal();
            else OpenModal(modal);
        }

        public void CloseModal()
        {
            if (_openModal == null) return;
            _modalLayer.Clear();
            _openModal = null;
            HideTooltip();
        }

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
        /// window where it last stood or where its group belongs in the chosen preset.
        /// </summary>
        public void Open(UiPanel panel)
        {
            if (panel.Window != null)
            {
                panel.Window.Activate(panel);
                panel.Window.Root.BringToFront();
                return;
            }
            // Its placeholder (while editing the layout) gives way to the real thing, keeping its place.
            UiWindow stand = _windows.FirstOrDefault(w => w.IsPlaceholder && ((PlaceholderPanel)w.Panels[0]).ForId == panel.Id);
            if (stand != null)
            {
                RememberPlaceholder(stand);
                RemoveWindow(stand);
            }
            UiWindow home = _windows.FirstOrDefault(w => w.CanDock(panel) && w.Panels.Any(p => p.DefaultWindow == panel.DefaultWindow));
            if (home != null)
            {
                home.Add(panel);
                home.Root.BringToFront();
            }
            else
            {
                UiWindow window;
                if (_placements.TryGetValue(panel.Id, out SavedWindow last))
                    window = CreateWindow(last.anchor, last.offset, PreferredOf(last), panel);
                else
                    window = CreateWindowAt(HomeBox(panel), panel);
                FitIn(window);
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
            if (byPlayer) Dismiss(panel);
            SaveLayout();
        }

        public void CloseWindow(UiWindow window)
        {
            if (window.Panels.Count == 1) Remember(window);
            foreach (UiPanel p in window.Panels.ToList())
            {
                window.Remove(p);
                Dismiss(p);
            }
            RemoveWindow(window);
            SaveLayout();
        }

        private void Dismiss(UiPanel panel)
        {
            if (panel.ServerOpen == true) _dismissed.Add(panel); // stays shut until the server agrees
            panel.OnClosedByPlayer();
        }

        /// <summary>Where a panel (or an overlay, "overlay:id") last stood on its own, so it reopens there.</summary>
        private readonly Dictionary<string, SavedWindow> _placements = new Dictionary<string, SavedWindow>();

        private void Remember(UiWindow window)
        {
            if (window.IsPlaceholder)
            {
                RememberPlaceholder(window);
                return;
            }
            _placements[window.Panels[0].Id] = Saved(window, window.Panels[0].Id);
        }

        private void RememberPlaceholder(UiWindow window) =>
            _placements[((PlaceholderPanel)window.Panels[0]).ForId] = Saved(window, ((PlaceholderPanel)window.Panels[0]).ForId);

        private static SavedWindow Saved(UiWindow window, string id) =>
            new SavedWindow { panels = new List<string> { id }, anchor = window.Anchor, offset = window.Offset, size = window.Size, preferred = window.Preferred };

        /// <summary>The size a saved window chose (older saves only knew the size it had).</summary>
        private static Vector2 PreferredOf(SavedWindow saved) => saved.preferred.x > 0f && saved.preferred.y > 0f ? saved.preferred : saved.size;

        private UiWindow CreateWindow(Vector2 anchor, Vector2 offset, Vector2 size, params UiPanel[] panels)
        {
            var window = new UiWindow(this) { Anchor = anchor, Offset = offset, Size = size, Preferred = size };
            foreach (UiPanel p in panels.Take(UiWindow.MaxTabs)) window.Add(p, activate: false);
            if (window.Panels.Count > 0) window.Activate(window.Panels[0]);
            _windows.Add(window);
            (window.IsFloating ? _floatLayer : _windowLayer).Add(window.Root);
            window.Apply(ScreenSize);
            return window;
        }

        /// <summary>A window at a box (panel pixels), anchored to whichever corner or edge it is nearest.</summary>
        private UiWindow CreateWindowAt(WindowRules.Box box, params UiPanel[] panels)
        {
            UiWindow window = CreateWindow(Vector2.zero, Vector2.zero, new Vector2(box.W, box.H), panels);
            window.SetBox(box, ScreenSize);
            return window;
        }

        private void RemoveWindow(UiWindow window)
        {
            _windows.Remove(window);
            window.Root.RemoveFromHierarchy();
            if (_dockTarget == window) _dockTarget = null;
        }

        // ------------------------------------------------------------------ placement: no overlap, snapping, previews

        /// <summary>
        /// Whether windows can be moved, resized, closed and re-tabbed now: unlocked, or editing the
        /// layout (which works even while locked).
        /// </summary>
        public bool CanArrange => !Locked || EditMode;

        /// <summary>The snap distance in panel pixels (the same few screen pixels at every scale); none while Alt is held.</summary>
        public float SnapDistance(bool altHeld) => altHeld ? 0f : WindowRules.SnapDistanceFor(_settings != null ? _settings.scale : 1f);

        /// <summary>What a docked window may not overlap: every other docked window.</summary>
        public List<WindowRules.Box> ObstaclesFor(UiWindow window) =>
            _windows.Where(w => w != window && !w.IsFloating).Select(w => w.Box).ToList();

        /// <summary>What a moving window lines up with: the others of its kind (docked with docked, floating with floating).</summary>
        public List<WindowRules.Box> SnapTargetsFor(UiWindow window) =>
            _windows.Where(w => w != window && w.IsFloating == (window?.IsFloating ?? false)).Select(w => w.Box).ToList();

        /// <summary>
        /// Where a window (or a new one, <paramref name="self"/> null) wanted at a box would land:
        /// snapped flush, then for a docked window moved to the nearest place clear of the others.
        /// Fits is false when there is no room at all. Guides are the lines it ends up flush on.
        /// </summary>
        public (WindowRules.Box Box, bool Fits) Land(UiWindow self, WindowRules.Box wanted, bool floating, float snap, out List<WindowRules.Guide> guides)
        {
            Vector2 screen = ScreenSize;
            List<WindowRules.Box> targets = _windows.Where(w => w != self && w.IsFloating == floating).Select(w => w.Box).ToList();
            WindowRules.Box snapped = WindowRules.Snap(wanted, targets, screen.x, screen.y, snap);
            WindowRules.Box? landing = floating
                ? WindowRules.Clamp(snapped, screen.x, screen.y)
                : WindowRules.NearestFree(snapped, _windows.Where(w => w != self && !w.IsFloating).Select(w => w.Box).ToList(), screen.x, screen.y);
            WindowRules.Box box = landing ?? snapped;
            guides = WindowRules.Contacts(box, targets, screen.x, screen.y);
            return (box, landing.HasValue);
        }

        /// <summary>The size a panel's own window starts at: its fixed size, else its default size (never under its minimum).</summary>
        public Vector2 OwnSize(UiPanel panel)
        {
            if (panel.AllowedSizes != null && panel.AllowedSizes.Length > 0) return panel.FixedSize ?? panel.AllowedSizes[0];
            return panel.FixedSize ?? Vector2.Max(UiWindow.MinSizeFor(panel), panel.DefaultSize);
        }

        /// <summary>Moves a docked window to the nearest place where it overlaps nothing (if it doesn't already fit).</summary>
        private void FitIn(UiWindow window)
        {
            if (window.IsFloating) return;
            Vector2 screen = ScreenSize;
            WindowRules.Box? free = WindowRules.NearestFree(window.Box, ObstaclesFor(window), screen.x, screen.y);
            if (free.HasValue && !free.Value.Equals(window.Box)) window.MoveTo(new Vector2(free.Value.X, free.Value.Y), screen);
        }

        /// <summary>Settles the docked windows so none overlaps another, moving as few as it can; sizes stay as chosen.</summary>
        private void SettleDocked()
        {
            Vector2 screen = ScreenSize;
            List<UiWindow> docked = _windows.Where(w => !w.IsFloating).ToList();
            List<WindowRules.Box> settled = WindowRules.Settle(docked.Select(w => w.Box).ToList(), screen.x, screen.y);
            for (int i = 0; i < docked.Count; i++)
                if (!settled[i].Equals(docked[i].Box)) docked[i].MoveTo(new Vector2(settled[i].X, settled[i].Y), screen);
        }

        private VisualElement _landing;
        private Label _landingLabel;
        private readonly List<VisualElement> _guides = new List<VisualElement>();
        private UiWindow _dockTarget;

        /// <summary>The window a dragged tab (or lone window) would join right now, if any.</summary>
        public UiWindow DockTarget => _dockTarget;

        /// <summary>
        /// Shows where a window will land when it isn't drawn there itself (a tab being pulled out, or
        /// nowhere to go): an outline (red if it won't fit), an optional label, and the guide lines.
        /// </summary>
        public void ShowLanding(WindowRules.Box box, bool fits, string label, List<WindowRules.Guide> guides)
        {
            if (_landing == null)
            {
                _landing = UiKit.El(_landingLayer, "landing");
                _landing.pickingMode = PickingMode.Ignore;
                _landingLabel = UiKit.Text(_landing, "", "landing__label");
                _landingLabel.pickingMode = PickingMode.Ignore;
            }
            SetDockTarget(null, false);
            _landing.style.display = DisplayStyle.Flex;
            _landing.style.left = box.X;
            _landing.style.top = box.Y;
            _landing.style.width = box.W;
            _landing.style.height = box.H;
            _landing.EnableInClassList("landing--blocked", !fits);
            _landing.RemoveFromClassList("landing--marker");
            _landingLabel.text = label ?? "";
            _landingLabel.style.display = string.IsNullOrEmpty(label) ? DisplayStyle.None : DisplayStyle.Flex;
            ShowGuideLines(guides);
        }

        /// <summary>Shows only the guide lines (the window itself is drawn where it will land).</summary>
        public void ShowGuides(List<WindowRules.Guide> guides)
        {
            if (_landing != null) _landing.style.display = DisplayStyle.None;
            SetDockTarget(null, false);
            ShowGuideLines(guides);
        }

        private void ShowGuideLines(List<WindowRules.Guide> guides)
        {
            Vector2 screen = ScreenSize;
            for (int i = 0; i < Mathf.Max(_guides.Count, guides?.Count ?? 0); i++)
            {
                if (i >= _guides.Count)
                {
                    VisualElement g = UiKit.El(_landingLayer, "guide");
                    g.pickingMode = PickingMode.Ignore;
                    _guides.Add(g);
                }
                VisualElement line = _guides[i];
                if (guides == null || i >= guides.Count)
                {
                    line.style.display = DisplayStyle.None;
                    continue;
                }
                WindowRules.Guide guide = guides[i];
                line.style.display = DisplayStyle.Flex;
                // A line on the screen's right or bottom edge is drawn just inside it.
                float at = guide.Vertical ? Mathf.Min(guide.At, screen.x - 1f) : Mathf.Min(guide.At, screen.y - 1f);
                line.style.left = guide.Vertical ? at : 0f;
                line.style.top = guide.Vertical ? 0f : at;
                line.style.width = guide.Vertical ? 1f : screen.x;
                line.style.height = guide.Vertical ? screen.y : 1f;
            }
        }

        public void HideLanding()
        {
            if (_landing != null) _landing.style.display = DisplayStyle.None;
            foreach (VisualElement g in _guides) g.style.display = DisplayStyle.None;
            SetDockTarget(null, false);
        }

        private bool _dockTargetFull;

        private void SetDockTarget(UiWindow window, bool full)
        {
            if (_dockTarget == window && _dockTargetFull == full) return;
            _dockTarget?.MarkDockTarget(false, false);
            _dockTarget = window;
            _dockTargetFull = full;
            _dockTarget?.MarkDockTarget(true, full);
        }

        /// <summary>
        /// The window whose tab strip is under a point that a dragged panel could go into: its own
        /// (to reorder it), or another that takes it. <paramref name="moving"/> is a lone window being
        /// dragged, never its own target. Full is true when the window under the point would take it
        /// but has no room for another tab.
        /// </summary>
        private (UiWindow Window, bool Full) DockAt(UiPanel panel, Vector2 at, UiWindow moving)
        {
            UiWindow from = panel.Window;
            foreach (UiWindow w in _windows)
            {
                if (w == moving || !w.AcceptsDropAt(at)) continue;
                if (w == from && moving == null) return (w, false);
                if (w.CanDock(panel)) return (w, false);
                if (w.IsFull && w.CouldDock(panel)) return (w, true);
            }
            return (null, false);
        }

        /// <summary>
        /// While a tab (or a lone window) is dragged: if the pointer is over a tab strip that would take
        /// it, marks that window with an outline (or as full) and shows where in the strip it would
        /// go, and says true; else clears the mark and says false.
        /// </summary>
        public bool PreviewDock(UiPanel panel, Vector2 at, UiWindow moving)
        {
            (UiWindow dock, bool full) = DockAt(panel, at, moving);
            if (dock == null)
            {
                SetDockTarget(null, false);
                return false;
            }
            HideLanding();
            if (full)
            {
                SetDockTarget(dock, true);
                return true;
            }
            (int _, float x) = dock.TabSlotAt(at);
            Rect bar = dock.TitleBarBound;
            ShowLanding(new WindowRules.Box(x - 2f, bar.y + 3f, 4f, bar.height - 6f), true, null, null);
            _landing.EnableInClassList("landing--marker", true);
            // Joining another window outlines it; moving along its own strip needs only the marker.
            SetDockTarget(dock != panel.Window ? dock : null, false);
            return true;
        }

        /// <summary>
        /// A dragged panel let go: onto a tab strip that takes it, it joins (or moves along) that strip;
        /// otherwise it gets a window of its own at <paramref name="landing"/> (null: nowhere to go,
        /// nothing changes).
        /// </summary>
        public void DropPanel(UiPanel panel, Vector2 at, WindowRules.Box? landing)
        {
            UiWindow from = panel.Window;
            (UiWindow dock, bool full) = DockAt(panel, at, from != null && from.Panels.Count == 1 ? from : null);
            if (dock != null && !full)
            {
                if (dock == from) from.MoveTab(panel, from.TabSlotAt(at).Index); // back in its strip, or moved along it
                else
                {
                    dock.Add(panel, true, dock.TabSlotAt(at).Index);
                    dock.Root.BringToFront();
                    if (from != null && from.Panels.Count == 0) RemoveWindow(from);
                }
                SaveLayout();
                return;
            }
            if (full || !landing.HasValue) return;
            UiWindow window = CreateWindowAt(landing.Value, panel);
            window.Root.BringToFront();
            if (from != null && from.Panels.Count == 0) RemoveWindow(from);
            SaveLayout();
        }

        // ------------------------------------------------------------------ presets and default placements

        /// <summary>The preset the player picked (Reset layout goes back to it).</summary>
        public UiDefaultLayout.Preset Preset => UiDefaultLayout.Find(PlayerPrefs.GetString(PresetPref, UiDefaultLayout.Classic.Id));

        /// <summary>Every window of the chosen preset placed on this screen, at its panels' own sizes where they insist.</summary>
        private Dictionary<string, WindowRules.Box> PresetBoxes(UiDefaultLayout.Preset preset)
        {
            Vector2 screen = ScreenSize;
            return UiDefaultLayout.Resolve(preset, screen.x, screen.y, group =>
            {
                foreach (UiPanel p in _panels)
                {
                    if (p.DefaultWindow != group) continue;
                    if (p.FixedSize.HasValue) return (p.FixedSize.Value.x, p.FixedSize.Value.y);
                    if (p.AllowedSizes != null && p.AllowedSizes.Length > 0) return (p.AllowedSizes[0].x, p.AllowedSizes[0].y);
                }
                return null;
            });
        }

        /// <summary>Where a panel's window goes when nothing remembers it: its group's place in the preset, or centred.</summary>
        private WindowRules.Box HomeBox(UiPanel panel)
        {
            Vector2 screen = ScreenSize;
            Vector2 min = UiWindow.MinSizeFor(panel);
            if (PresetBoxes(Preset).TryGetValue(panel.DefaultWindow, out WindowRules.Box b))
            {
                Vector2 size = panel.FixedSize ?? Vector2.Max(min, new Vector2(b.W, b.H));
                return WindowRules.Clamp(new WindowRules.Box(b.X, b.Y, size.x, size.y), screen.x, screen.y);
            }
            Vector2 own = OwnSize(panel);
            return WindowRules.Anchored(0.5f, 0.5f, 0f, 0f, own.x, own.y, screen.x, screen.y);
        }

        /// <summary>Puts every window where a preset says (and remembers the choice). Server windows and overlays go back to their homes too.</summary>
        public void ApplyPreset(UiDefaultLayout.Preset preset)
        {
            PlayerPrefs.SetString(PresetPref, preset.Id);
            ResetLayout();
        }

        public void ResetLayout()
        {
            bool editing = EditMode;
            if (editing) SetEditMode(false, save: false);
            _placements.Clear();
            foreach (UiWindow w in _windows.ToList())
            {
                foreach (UiPanel p in w.Panels.ToList()) w.Remove(p);
                RemoveWindow(w);
            }
            DefaultLayout();
            SaveLayout();
            if (editing) SetEditMode(true);
        }

        private void DefaultLayout()
        {
            UiDefaultLayout.Preset preset = Preset;
            Dictionary<string, WindowRules.Box> boxes = PresetBoxes(preset);
            foreach (IGrouping<string, UiPanel> group in _panels.Where(p => p.OpenByDefault && p.ServerOpen == null).GroupBy(p => p.DefaultWindow))
            {
                if (preset.Closed.Contains(group.Key)) continue;
                UiPanel[] panels = group.ToArray();
                WindowRules.Box box = boxes.TryGetValue(group.Key, out WindowRules.Box b) ? b : HomeBox(panels[0]);
                CreateWindowAt(box, panels);
                // A group larger than a window holds opens the rest in windows of their own.
                foreach (UiPanel extra in panels.Skip(UiWindow.MaxTabs)) Open(extra);
            }
            SettleDocked();
        }

        // ------------------------------------------------------------------ editing the layout

        /// <summary>Editing the layout: every window shows its frame and handles, and stand-ins show where server windows and overlays go.</summary>
        public bool EditMode { get; private set; }

        private VisualElement _editBar;

        public void ToggleEditMode() => SetEditMode(!EditMode);

        public void SetEditMode(bool on, bool save = true)
        {
            if (on == EditMode) return;
            EditMode = on;
            _root?.EnableInClassList("rth-root--editing", on);
            _root?.EnableInClassList("rth-root--locked", Locked && !on);
            if (on)
            {
                CloseModal();
                ShowPlaceholders();
                ShowEditBar();
            }
            else
            {
                foreach (UiWindow w in _windows.Where(w => w.IsPlaceholder).ToList())
                {
                    RememberPlaceholder(w);
                    RemoveWindow(w);
                }
                _editBar?.RemoveFromHierarchy();
                _editBar = null;
                HideLanding();
                if (save) SaveLayout();
            }
        }

        /// <summary>Stand-ins for every server window not open now, and every overlay, where each will appear.</summary>
        private void ShowPlaceholders()
        {
            Vector2 screen = ScreenSize;
            foreach (UiPanel panel in _panels.Where(p => p.ServerOpen != null && p.Window == null))
            {
                var stand = new PlaceholderPanel(panel);
                stand.Create(this);
                if (_placements.TryGetValue(panel.Id, out SavedWindow last)) CreateWindow(last.anchor, last.offset, PreferredOf(last), stand);
                else CreateWindowAt(HomeBox(panel), stand);
            }
            foreach (Overlay overlay in _overlays.Values)
            {
                var stand = new PlaceholderPanel(OverlayKey(overlay.Id), overlay.Title, overlay.Size);
                stand.Create(this);
                Rect r = OverlayRect(overlay.Id);
                CreateWindowAt(new WindowRules.Box(r.x, r.y, r.width, r.height), stand);
            }
            foreach (UiWindow w in _windows.Where(w => w.IsPlaceholder)) w.Apply(screen);
        }

        private void ShowEditBar()
        {
            // A row across the screen that catches nothing, holding the bar in its middle.
            _editBar = UiKit.El(_root, "edit-bar");
            _editBar.pickingMode = PickingMode.Ignore;
            VisualElement box = UiKit.El(_editBar, "edit-bar__box");
            UiKit.Text(box, "EDITING LAYOUT", "edit-bar__title").pickingMode = PickingMode.Ignore;
            UiKit.Text(box, "Drag any window; edges snap flush (hold Alt to place freely). Stand-ins show where windows that open later will go.", "edit-bar__hint").pickingMode = PickingMode.Ignore;
            UiKit.Button(box, "Done", () => SetEditMode(false));
        }

        // ------------------------------------------------------------------ overlays

        /// <summary>Something drawn outside the windows (the trophy fishing gauge) whose place the player can choose while editing the layout.</summary>
        private sealed class Overlay
        {
            public string Id;
            public string Title;
            public Vector2 Size;
            public UiDefaultLayout.Placement Home;
        }

        private static readonly Dictionary<string, Overlay> _overlays = new Dictionary<string, Overlay>();

        private static string OverlayKey(string id) => "overlay:" + id;

        /// <summary>
        /// Registers a placeable overlay: its id (for saved layouts), a name shown while editing, its
        /// size (panel pixels) and where it goes by default. Calling again just updates it. The owner
        /// then draws it at <see cref="OverlayRect"/>.
        /// </summary>
        public static void RegisterOverlay(string id, string title, Vector2 size, UiDefaultLayout.Placement home) =>
            _overlays[id] = new Overlay { Id = id, Title = title, Size = size, Home = home };

        /// <summary>Where an overlay goes now (panel pixels): where the player put it, else its default place.</summary>
        public Rect OverlayRect(string id)
        {
            if (!_overlays.TryGetValue(id, out Overlay overlay)) return Rect.zero;
            Vector2 screen = ScreenSize;
            string key = OverlayKey(id);
            UiWindow stand = _windows.FirstOrDefault(w => w.IsPlaceholder && ((PlaceholderPanel)w.Panels[0]).ForId == key);
            if (stand != null) return stand.Rect;
            Vector2 size = overlay.Size;
            if (_placements.TryGetValue(key, out SavedWindow saved))
            {
                Vector2 pos = new Vector2(saved.anchor.x * (screen.x - size.x), saved.anchor.y * (screen.y - size.y)) + saved.offset;
                pos.x = Mathf.Round(Mathf.Clamp(pos.x, 0f, Mathf.Max(0f, screen.x - size.x)));
                pos.y = Mathf.Round(Mathf.Clamp(pos.y, 0f, Mathf.Max(0f, screen.y - size.y)));
                return new Rect(pos, size);
            }
            UiDefaultLayout.Placement h = overlay.Home;
            WindowRules.Box b = WindowRules.Clamp(WindowRules.Anchored(h.AnchorX, h.AnchorY, h.OffsetX, h.OffsetY, size.x, size.y, screen.x, screen.y), screen.x, screen.y);
            return new Rect(b.X, b.Y, b.W, b.H);
        }

        // ------------------------------------------------------------------ saved layouts, one per screen size

        [Serializable]
        private sealed class SavedWindow
        {
            public List<string> panels = new List<string>();
            public string active;
            public Vector2 anchor;
            public Vector2 offset;
            /// <summary>The size it had when saved.</summary>
            public Vector2 size;
            /// <summary>The size the player chose (zero in layouts saved before this was kept).</summary>
            public Vector2 preferred;
        }

        [Serializable]
        private sealed class SavedLayout
        {
            public List<SavedWindow> windows = new List<SavedWindow>();
            /// <summary>Every panel that existed when saved: a panel added since opens by default.</summary>
            public List<string> known = new List<string>();
            /// <summary>Where closed panels, server windows and overlays last stood.</summary>
            public List<SavedWindow> placements = new List<SavedWindow>();
        }

        /// <summary>The bucket (screen size in panel pixels, rounded) the layout in use belongs to.</summary>
        private string _bucket;

        private static string BucketKey(string bucket) => LayoutPrefix + bucket;

        private static List<string> SavedBuckets() =>
            PlayerPrefs.GetString(LayoutIndexPref, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        public void SaveLayout()
        {
            if (!_built || _bucket == null) return;
            var layout = new SavedLayout { known = _panels.Select(p => p.Id).ToList() };
            foreach (UiWindow w in _windows)
            {
                if (w.IsPlaceholder)
                {
                    RememberPlaceholder(w);
                    continue;
                }
                // Server windows reopen when the server says, not at login.
                if (w.Panels.Any(p => p.ServerOpen != null))
                {
                    if (w.Panels.Count == 1) Remember(w);
                    continue;
                }
                SavedWindow saved = Saved(w, null);
                saved.panels = w.Panels.Select(p => p.Id).ToList();
                saved.active = w.Active?.Id;
                layout.windows.Add(saved);
            }
            layout.placements = _placements.Values.ToList();
            PlayerPrefs.SetString(BucketKey(_bucket), JsonUtility.ToJson(layout));
            List<string> buckets = SavedBuckets();
            if (!buckets.Contains(_bucket))
            {
                buckets.Add(_bucket);
                // Keep a handful: the oldest screen sizes go first.
                while (buckets.Count > MaxBuckets)
                {
                    PlayerPrefs.DeleteKey(BucketKey(buckets[0]));
                    buckets.RemoveAt(0);
                }
                PlayerPrefs.SetString(LayoutIndexPref, string.Join(";", buckets));
            }
        }

        /// <summary>
        /// Lays the interface out for this screen: its own saved layout; else the saved layout of the
        /// nearest screen size (anchored, so it adapts), else the old single layout (v3, migrated), else
        /// the chosen preset. Only a layout of this screen's own is written back unasked.
        /// </summary>
        private void LayOutForScreen(Vector2 screen)
        {
            _bucket = WindowRules.BucketOf(screen.x, screen.y);
            List<string> buckets = SavedBuckets();
            string json = buckets.Contains(_bucket) ? PlayerPrefs.GetString(BucketKey(_bucket), "") : "";
            bool own = !string.IsNullOrEmpty(json);
            if (!own)
            {
                string nearest = WindowRules.NearestBucket(buckets, screen.x, screen.y);
                json = nearest != null ? PlayerPrefs.GetString(BucketKey(nearest), "") : PlayerPrefs.GetString(LegacyLayoutPref, "");
            }
            if (!LoadLayout(json)) DefaultLayout();
            else if (!own && buckets.Count == 0) SaveLayout(); // the old layout, moved over once
        }

        /// <summary>Tears the windows down and lays them out again for a new screen size (its own layout if it has one).</summary>
        private void ChangeScreen(Vector2 screen)
        {
            bool editing = EditMode;
            if (editing) SetEditMode(false);
            _placements.Clear();
            foreach (UiWindow w in _windows.ToList())
            {
                foreach (UiPanel p in w.Panels.ToList()) w.Remove(p);
                RemoveWindow(w);
            }
            LayOutForScreen(screen);
            if (editing) SetEditMode(true);
        }

        private bool LoadLayout(string json)
        {
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
                UiWindow window = CreateWindow(saved.anchor, saved.offset, PreferredOf(saved), panels);
                if (saved.active != null && byId.TryGetValue(saved.active, out UiPanel active) && active.Window == window) window.Activate(active);
                // A layout saved before windows had a tab limit: the rest open on their own.
                foreach (UiPanel extra in panels.Skip(UiWindow.MaxTabs)) Open(extra);
            }
            foreach (UiPanel panel in _panels)
                if (panel.OpenByDefault && panel.ServerOpen == null && panel.Window == null && !layout.known.Contains(panel.Id)) Open(panel);
            SettleDocked();
            return true;
        }

        private void OnApplicationQuit()
        {
            SetEditMode(false, save: false);
            SaveLayout();
        }
    }
}
