using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>
    /// A window holding one or more panels. Its title bar shows icons only: a lone panel's icon, or a
    /// strip of icon tabs (at most <see cref="MaxTabs"/>); the name is the icon's tooltip. The title
    /// bar (or a lone panel's icon) moves the window, any edge or corner resizes it, and a tab pulled
    /// out of the strip becomes a window of its own or joins another window's strip. Every drag
    /// behaves the same: edges snap flush to the screen and to neighbours (Alt held: no snapping),
    /// guides show the lines it sits on, and the window is drawn where it will land, so nothing
    /// jumps on release. A window with a single bare panel (bars, the minimap) has no frame, just a
    /// move handle at its top left corner.
    ///
    /// <b>Docked</b> windows (the HUD) never overlap each other: <see cref="WindowRules"/> resolves
    /// every move and resize against the others. <b>Floating</b> windows (the bank, a shop, the
    /// conversation) sit in a layer above, open centred, and may overlap. While the interface is
    /// locked (and not in layout editing), nothing moves, resizes or closes.
    ///
    /// Position is kept relative to an anchor (a corner, an edge's middle, or the centre), and the
    /// size the player chose is kept apart from the size the screen allows, so layouts survive a
    /// change of resolution and a window grows back when there is room again.
    /// </summary>
    public sealed class UiWindow
    {
        /// <summary>The most panels one window holds as tabs.</summary>
        public const int MaxTabs = 6;
        private const float TabDragThreshold = 6f;

        /// <summary>
        /// What a framed window's frame takes around its panel (Rth.uss: 3 px of window padding each
        /// side, the 32 px title bar, 8 px of body padding each side). Panels state their content's
        /// minimum; the window adds this.
        /// </summary>
        public static readonly Vector2 FrameChrome = new Vector2(3f * 2f + 8f * 2f, 3f * 2f + 32f + 8f * 2f);

        /// <summary>The least content any panel gets, whatever it says.</summary>
        private static readonly Vector2 LeastContent = new Vector2(100f, 40f);

        /// <summary>Where the pointer holds a window pulled out of a tab: on its title bar, by its icon.</summary>
        public static readonly Vector2 PullOutGrab = new Vector2(20f, 16f);

        private readonly UiRoot _ui;
        private readonly VisualElement _titleBar;
        private readonly VisualElement _titleIcon;
        private readonly VisualElement _tabs;
        private readonly VisualElement _close;
        private readonly VisualElement _body;
        private readonly VisualElement _outline;
        private readonly Label _editLabel;
        private readonly Dictionary<UiPanel, VisualElement> _tabElements = new Dictionary<UiPanel, VisualElement>();

        public readonly VisualElement Root;
        public readonly List<UiPanel> Panels = new List<UiPanel>();
        public UiPanel Active { get; private set; }

        /// <summary>0 to 1 on each axis: left/centre/right and top/middle/bottom.</summary>
        public Vector2 Anchor;
        /// <summary>Offset from the anchored position, in panel pixels.</summary>
        public Vector2 Offset;
        /// <summary>The size it has now: <see cref="Preferred"/>, unless the screen is too small for it.</summary>
        public Vector2 Size;
        /// <summary>The size the player chose (or the layout gave): kept when a small screen squeezes the window, restored when there is room.</summary>
        public Vector2 Preferred;

        private enum Gesture { None, Move, Resize, PullOut }

        private Gesture _gesture;
        private WindowRules.Edge _edges;
        private Vector2 _pointerStart;
        private WindowRules.Box _startBox;
        private WindowRules.Box _landing;
        private bool _landingFits;
        private UiWindow _dockInto;
        private UiPanel _tabPressed;
        private Vector2 _tabPressedAt;

        public bool IsBare => Panels.Count == 1 && Panels[0].Bare;

        /// <summary>Floating windows (server windows: bank, shop, conversation) may overlap and sit above the HUD.</summary>
        public bool IsFloating => Panels.Any(p => p.Floating);

        public bool IsFull => Panels.Count >= MaxTabs;

        /// <summary>A stand-in shown only while editing the layout (a server window, an overlay).</summary>
        public bool IsPlaceholder => Panels.Count == 1 && Panels[0] is PlaceholderPanel;

        /// <summary>The size every panel in it insists on, if any: the window then can't be resized (unless it has <see cref="AllowedSizes"/>).</summary>
        public Vector2? FixedSize
        {
            get
            {
                foreach (UiPanel p in Panels)
                    if (p.FixedSize.HasValue) return p.FixedSize;
                return null;
            }
        }

        /// <summary>The sizes a panel in it allows (resizing snaps between them), if any.</summary>
        public Vector2[] AllowedSizes
        {
            get
            {
                foreach (UiPanel p in Panels)
                    if (p.AllowedSizes != null && p.AllowedSizes.Length > 0) return p.AllowedSizes;
                return null;
            }
        }

        /// <summary>Whether the player can resize it: free sizing, or between allowed sizes.</summary>
        public bool Resizable => AllowedSizes != null || !FixedSize.HasValue;

        public UiWindow(UiRoot ui)
        {
            _ui = ui;
            Root = new VisualElement();
            Root.AddToClassList("window");
            Root.RegisterCallback<PointerDownEvent>(_ => Root.BringToFront(), TrickleDown.TrickleDown);

            _titleBar = UiKit.El(Root, "window__titlebar");
            _titleIcon = UiKit.El(_titleBar, "window__title-icon");
            UiKit.Tooltip(_titleIcon, () => Panels.Count == 1 ? Panels[0].Title : null);
            _tabs = UiKit.El(_titleBar, "window__tabs");
            // Room to grab the title bar between the tabs and the close button.
            UiKit.El(_titleBar, "window__drag-space").pickingMode = PickingMode.Ignore;
            _close = UiKit.El(_titleBar, "window__button", "window__close");
            UiKit.SetIcon(_close, IconLibrary.Get("close"));
            _close.RegisterCallback<ClickEvent>(_ => { if (_ui.CanArrange && !IsPlaceholder) _ui.CloseWindow(this); });
            UiKit.Tooltip(_close, () => "Close");
            _titleBar.RegisterCallback<PointerDownEvent>(BeginMove);

            _body = UiKit.El(Root, "window__body");

            // While editing the layout, the whole window is something to drag (its panel's own
            // clicks wait until editing is done); outside editing this catcher is hidden.
            VisualElement mover = UiKit.El(Root, "window__mover");
            mover.RegisterCallback<PointerDownEvent>(BeginMove);

            var handles = new List<VisualElement> { _titleBar, mover };
            foreach ((WindowRules.Edge edge, string cls) in new[]
                     {
                         (WindowRules.Edge.Left, "window__grip--l"), (WindowRules.Edge.Right, "window__grip--r"),
                         (WindowRules.Edge.Top, "window__grip--t"), (WindowRules.Edge.Bottom, "window__grip--b"),
                         (WindowRules.Edge.TopLeft, "window__grip--tl"), (WindowRules.Edge.TopRight, "window__grip--tr"),
                         (WindowRules.Edge.BottomLeft, "window__grip--bl"), (WindowRules.Edge.BottomRight, "window__grip--br"),
                     })
            {
                bool corner = edge == WindowRules.Edge.TopLeft || edge == WindowRules.Edge.TopRight || edge == WindowRules.Edge.BottomLeft || edge == WindowRules.Edge.BottomRight;
                VisualElement grip = UiKit.El(Root, "window__grip", corner ? "window__grip--corner" : "window__grip--edge", cls);
                WindowRules.Edge e = edge;
                grip.RegisterCallback<PointerDownEvent>(ev => BeginResize(ev, e));
                handles.Add(grip);
            }

            // A frameless window's handle: a small grip of dots, faint until the pointer is near.
            VisualElement handle = UiKit.El(Root, "window__handle");
            for (int i = 0; i < 6; i++) UiKit.El(handle, "window__handle-dot").pickingMode = PickingMode.Ignore;
            handle.RegisterCallback<PointerDownEvent>(BeginMove);
            UiKit.Tooltip(handle, () => _ui.CanArrange ? "Drag to move" + (Panels.Count == 1 ? ": " + Panels[0].Title : "") : null);
            handles.Add(handle);

            // Drawn over everything, never in the way: the outline while editing or while a dragged
            // tab would join this window, and the "full" mark when it can't take another tab.
            _outline = UiKit.El(Root, "window__outline");
            _outline.pickingMode = PickingMode.Ignore;
            _editLabel = UiKit.Text(_outline, "", "window__edit-label");
            _editLabel.pickingMode = PickingMode.Ignore;
            Label full = UiKit.Text(_outline, "FULL", "window__full");
            full.pickingMode = PickingMode.Ignore;

            foreach (VisualElement e in handles)
            {
                e.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                e.RegisterCallback<PointerUpEvent>(OnPointerUp);
                e.RegisterCallback<PointerCaptureOutEvent>(_ => CancelGesture());
            }
        }

        // ------------------------------------------------------------------ panels and tabs

        /// <summary>Adds a panel as a tab, at <paramref name="index"/> among the tabs (the end by default).</summary>
        public void Add(UiPanel panel, bool activate = true, int index = -1)
        {
            if (Panels.Contains(panel)) return;
            panel.Window?.Remove(panel);
            index = index < 0 || index > Panels.Count ? Panels.Count : index;
            Panels.Insert(index, panel);
            panel.Window = this;

            var tab = new VisualElement();
            tab.AddToClassList("tab");
            _tabs.Insert(index, tab);
            UiKit.Icon(tab, IconLibrary.Get(panel.Icon), 20f, "tab__icon").pickingMode = PickingMode.Ignore;
            UiKit.Tooltip(tab, () => panel.Title);
            tab.RegisterCallback<PointerDownEvent>(e => PressTab(panel, e));
            tab.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            tab.RegisterCallback<PointerUpEvent>(OnPointerUp);
            tab.RegisterCallback<PointerCaptureOutEvent>(_ => CancelGesture());
            _tabElements[panel] = tab;

            if (activate || Active == null) Activate(panel);
            UpdateFrame();
        }

        /// <summary>Moves one of its tabs to another place in the strip.</summary>
        public void MoveTab(UiPanel panel, int index)
        {
            int from = Panels.IndexOf(panel);
            if (from < 0) return;
            index = Mathf.Clamp(index > from ? index - 1 : index, 0, Panels.Count - 1);
            if (index == from) return;
            Panels.RemoveAt(from);
            Panels.Insert(index, panel);
            VisualElement tab = _tabElements[panel];
            tab.RemoveFromHierarchy();
            _tabs.Insert(index, tab);
        }

        /// <summary>
        /// Where in the tab strip a point (panel space) falls: the index a dropped tab would take,
        /// and the x of the gap it would go into.
        /// </summary>
        public (int Index, float X) TabSlotAt(Vector2 point)
        {
            if (Panels.Count == 1 || _tabs.resolvedStyle.display == DisplayStyle.None)
            {
                Rect icon = _titleIcon.worldBound;
                bool before = point.x < icon.center.x;
                return (before ? 0 : 1, before ? icon.xMin - 2f : icon.xMax + 2f);
            }
            for (int i = 0; i < Panels.Count; i++)
            {
                Rect r = _tabElements[Panels[i]].worldBound;
                if (point.x < r.center.x) return (i, r.xMin);
            }
            return (Panels.Count, _tabElements[Panels[Panels.Count - 1]].worldBound.xMax);
        }

        /// <summary>The title bar's place on screen (panel space), for drop markers.</summary>
        public Rect TitleBarBound => _titleBar.worldBound;

        public void Remove(UiPanel panel)
        {
            if (!Panels.Remove(panel)) return;
            panel.Window = null;
            panel.Container.RemoveFromHierarchy();
            if (_tabElements.TryGetValue(panel, out VisualElement tab)) tab.RemoveFromHierarchy();
            _tabElements.Remove(panel);
            if (Active == panel) Activate(Panels.Count > 0 ? Panels[0] : null);
            UpdateFrame();
        }

        public void Activate(UiPanel panel)
        {
            if (Active != null) Active.Container.RemoveFromHierarchy();
            Active = panel;
            if (panel != null) _body.Add(panel.Container);
            foreach (KeyValuePair<UiPanel, VisualElement> t in _tabElements)
                t.Value.EnableInClassList("tab--active", t.Key == panel);
        }

        private void UpdateFrame()
        {
            bool bare = IsBare;
            bool single = Panels.Count == 1;
            Root.EnableInClassList("window--bare", bare);
            Root.EnableInClassList("window--fixed", !Resizable);
            Root.EnableInClassList("window--resizable", Resizable);
            // A frameless floating window (the conversation) gets no border: it would box empty space.
            Root.EnableInClassList("window--floating", IsFloating && !bare);
            Root.EnableInClassList("window--placeholder", IsPlaceholder);
            _titleBar.style.display = bare ? DisplayStyle.None : DisplayStyle.Flex;
            _tabs.style.display = single ? DisplayStyle.None : DisplayStyle.Flex;
            _titleIcon.style.display = single ? DisplayStyle.Flex : DisplayStyle.None;
            if (single) UiKit.SetIcon(_titleIcon, IconLibrary.Get(Panels[0].Icon));
            // Only the edit-mode label names the window; the title bar shows icons alone.
            _editLabel.text = string.Join(" / ", Panels.Select(p => p.Title));
            // A bare window has no frame to click: only what its panel draws catches the pointer.
            Root.pickingMode = _body.pickingMode = bare ? PickingMode.Ignore : PickingMode.Position;
        }

        /// <summary>
        /// The smallest the window may be: its panels' content minimum plus the frame around it, and
        /// never narrower than its title bar's icons, tabs and close button need.
        /// </summary>
        /// <summary>The narrowest a tab squeezes to when the window is narrow (its icon still whole).</summary>
        private const float TabLeast = 20f;

        public Vector2 MinSize
        {
            get
            {
                Vector2 content = LeastContent;
                foreach (UiPanel p in Panels) content = Vector2.Max(content, p.MinSize);
                if (IsBare) return content;
                // Title bar: 10 px of padding, the icon (26) or the tabs (each may squeeze to 21), a little to
                // grab (6), the close button (26). Tabs squeeze rather than hold the window wide, so a window
                // of six tabs can still be as narrow as a 4-wide pack.
                float strip = 10f + (Panels.Count <= 1 ? 26f : Panels.Count * (TabLeast + 1f)) + 6f + 26f;
                Vector2 framed = content + FrameChrome;
                return new Vector2(Mathf.Max(framed.x, strip), framed.y);
            }
        }

        /// <summary>The window MinSize a lone panel would have (for a tab pulled out into a window of its own).</summary>
        public static Vector2 MinSizeFor(UiPanel panel)
        {
            Vector2 content = Vector2.Max(LeastContent, panel.MinSize);
            return panel.Bare ? content : new Vector2(Mathf.Max(content.x + FrameChrome.x, 90f), content.y + FrameChrome.y);
        }

        // ------------------------------------------------------------------ placement

        /// <summary>The size the window takes on a screen: its chosen size, as the panel's rules and the screen allow.</summary>
        private Vector2 SizeFor(Vector2 screen)
        {
            Vector2[] allowed = AllowedSizes;
            if (allowed != null)
            {
                (float w, float h) = WindowRules.NearestSize(Preferred.x, Preferred.y, allowed.Select(s => (s.x, s.y)).ToList(), screen.x, screen.y);
                return new Vector2(w, h);
            }
            if (FixedSize.HasValue) return FixedSize.Value;
            return Vector2.Max(MinSize, Vector2.Min(Preferred, screen));
        }

        /// <summary>Puts the window where its anchor and offset say, at the size the screen allows, kept on screen.</summary>
        public void Apply(Vector2 screen)
        {
            Size = SizeFor(screen);
            Vector2 pos = new Vector2(Anchor.x * (screen.x - Size.x), Anchor.y * (screen.y - Size.y)) + Offset;
            pos.x = Mathf.Round(Mathf.Clamp(pos.x, 0f, Mathf.Max(0f, screen.x - Size.x)));
            pos.y = Mathf.Round(Mathf.Clamp(pos.y, 0f, Mathf.Max(0f, screen.y - Size.y)));
            Place(pos);
        }

        private void Place(Vector2 pos)
        {
            Root.style.left = pos.x;
            Root.style.top = pos.y;
            Root.style.width = Size.x;
            Root.style.height = Size.y;
            Position = pos;
        }

        /// <summary>Where the window stands (panel pixels, top left).</summary>
        public Vector2 Position { get; private set; }

        public Rect Rect => new Rect(Position, Size);

        public WindowRules.Box Box => new WindowRules.Box(Position.x, Position.y, Size.x, Size.y);

        /// <summary>Moves the window to a box (position and size), takes that size as the one chosen, and re-anchors it there.</summary>
        public void SetBox(WindowRules.Box box, Vector2 screen)
        {
            Size = Preferred = new Vector2(box.W, box.H);
            Reanchor(screen, new Vector2(box.X, box.Y));
            Apply(screen);
        }

        /// <summary>Moves the window without touching the size chosen for it (settling a layout).</summary>
        public void MoveTo(Vector2 position, Vector2 screen)
        {
            Reanchor(screen, position);
            Apply(screen);
        }

        /// <summary>Re-picks the anchor nearest the window's current place, so it stays put if the screen changes.</summary>
        public void Reanchor(Vector2 screen, Vector2 position)
        {
            Vector2 centre = position + Size / 2f;
            Anchor = new Vector2(Third(centre.x / Mathf.Max(1f, screen.x)), Third(centre.y / Mathf.Max(1f, screen.y)));
            Offset = position - new Vector2(Anchor.x * (screen.x - Size.x), Anchor.y * (screen.y - Size.y));
        }

        private static float Third(float t) => t < 1f / 3f ? 0f : t > 2f / 3f ? 1f : 0.5f;

        // ------------------------------------------------------------------ moving and resizing

        private void BeginMove(PointerDownEvent e)
        {
            if (!_ui.CanArrange || e.button != 0) return;
            // Buttons and tabs in the title bar get their own clicks: don't start a move on them.
            if (e.target is VisualElement target && (target.ClassListContains("window__button") || target.ClassListContains("tab"))) return;
            StartGesture(Gesture.Move, e);
        }

        private void BeginResize(PointerDownEvent e, WindowRules.Edge edges)
        {
            if (!_ui.CanArrange || e.button != 0 || !Resizable) return;
            _edges = edges;
            StartGesture(Gesture.Resize, e);
        }

        private void StartGesture(Gesture gesture, PointerDownEvent e)
        {
            _gesture = gesture;
            _pointerStart = e.position;
            _startBox = Box;
            _landing = _startBox;
            _landingFits = true;
            _dockInto = null;
            ((VisualElement)e.currentTarget).CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (_tabPressed != null && _gesture == Gesture.None)
            {
                // A tab pressed in a strip of several: pulled far enough, it comes out.
                if (!_ui.CanArrange || ((Vector2)e.position - _tabPressedAt).magnitude <= TabDragThreshold) return;
                _gesture = Gesture.PullOut;
            }
            if (_gesture == Gesture.None) return;
            Vector2 at = e.position;
            Vector2 screen = _ui.ScreenSize;
            float snap = _ui.SnapDistance(e.altKey);
            Vector2 delta = at - _pointerStart;

            if (_gesture == Gesture.Resize)
            {
                Resize(delta, snap, screen, e.altKey);
                return;
            }

            if (_gesture == Gesture.PullOut)
            {
                // A window of the panel's own size, held by its title bar, wherever the pointer goes.
                UiPanel panel = _tabPressed;
                _ui.ShowDragGhost(panel.Title, IconLibrary.Get(panel.Icon), UiKit.IconTint, at);
                if (_ui.PreviewDock(panel, at, null)) return;
                Vector2 size = _ui.OwnSize(panel);
                var wanted = new WindowRules.Box(at.x - PullOutGrab.x, at.y - PullOutGrab.y, size.x, size.y);
                (_landing, _landingFits) = _ui.Land(null, wanted, panel.Floating, snap, out List<WindowRules.Guide> pullGuides);
                _ui.ShowLanding(_landing, _landingFits, _landingFits ? null : "No room here", pullGuides);
                return;
            }

            // Moving the window: a lone panel's window may also join another window's tab strip.
            if (Panels.Count == 1 && _ui.PreviewDock(Panels[0], at, this))
            {
                _dockInto = _ui.DockTarget;
                Root.style.opacity = 0.5f;
                Place(new Vector2(_startBox.X, _startBox.Y));
                return;
            }
            _dockInto = null;
            Root.style.opacity = StyleKeyword.Null;
            WindowRules.Box moved = _startBox.At(_startBox.X + delta.x, _startBox.Y + delta.y);
            (_landing, _landingFits) = _ui.Land(this, moved, IsFloating, snap, out List<WindowRules.Guide> guides);
            if (_landingFits)
            {
                // Drawn where it will land: letting go changes nothing.
                Place(new Vector2(_landing.X, _landing.Y));
                _ui.ShowGuides(guides);
            }
            else
            {
                // Nowhere to go here: it follows the pointer, outlined red, and goes back if let go.
                WindowRules.Box free = WindowRules.Clamp(moved, screen.x, screen.y);
                Place(new Vector2(free.X, free.Y));
                _ui.ShowLanding(free, false, "No room here", null);
            }
        }

        /// <summary>
        /// Resizing. Windows sharing a dragged edge (flush against it) move their edge with it, like a
        /// splitter between panes, so a tidy arrangement stays tidy; hold Alt to resize this one alone.
        /// </summary>
        private void Resize(Vector2 delta, float snap, Vector2 screen, bool alone)
        {
            List<UiWindow> neighbours = IsFloating ? new List<UiWindow>() : _ui.Windows.Where(w => w != this && !w.IsFloating).ToList();
            var shared = new List<(UiWindow Window, WindowRules.Edge Facing)>();
            Vector2[] allowed = AllowedSizes;
            if (!alone && allowed == null)
                foreach (var (index, facing) in WindowRules.Sharing(_startBox, _edges, neighbours.Select(w => w.ResizeStart).ToList()))
                {
                    UiWindow w = neighbours[index];
                    if (w.Resizable && w.AllowedSizes == null) shared.Add((w, facing));
                }
            // The others it may not run into: every docked neighbour (where it stood when this began)
            // except those moving their shared edge with it.
            List<WindowRules.Box> others = neighbours.Where(w => !shared.Any(x => x.Window == w)).Select(w => w.ResizeStart).ToList();
            Vector2 min = allowed != null ? new Vector2(allowed.Min(s => s.x), allowed.Min(s => s.y)) : MinSize;
            WindowRules.Box box = WindowRules.Resize(_startBox, _edges, delta.x, delta.y, min.x, min.y, others, screen.x, screen.y, snap);
            if (allowed != null)
                box = WindowRules.NearestAllowed(_startBox, box, _edges, allowed.Select(s => (s.x, s.y)).ToList(), others, screen.x, screen.y);
            if (shared.Count > 0)
            {
                var (fitted, moved) = WindowRules.ResizeShared(box, shared.Select(s => (s.Window.ResizeStart, s.Facing, s.Window.MinSize.x, s.Window.MinSize.y)).ToList());
                box = fitted;
                for (int i = 0; i < shared.Count; i++) shared[i].Window.Preview(moved[i]);
            }
            foreach (UiWindow w in _sharing)
                if (!shared.Any(s => s.Window == w)) w.Preview(w.ResizeStart);
            _sharing.Clear();
            _sharing.AddRange(shared.Select(s => s.Window));
            _landing = box;
            _landingFits = true;
            Size = new Vector2(box.W, box.H);
            Place(new Vector2(box.X, box.Y));
            _ui.ShowGuides(WindowRules.Contacts(box, _ui.SnapTargetsFor(this), screen.x, screen.y));
        }

        /// <summary>The windows moving with this one's resize (they share the dragged edge).</summary>
        private readonly List<UiWindow> _sharing = new List<UiWindow>();

        /// <summary>The window's box when a neighbour began resizing (what a shared edge works from).</summary>
        internal WindowRules.Box ResizeStart => _previewing ? _previewStart : Box;

        private bool _previewing;
        private WindowRules.Box _previewStart;

        /// <summary>Drawn at a box while a neighbour drags an edge they share; nothing is kept until it's let go.</summary>
        internal void Preview(WindowRules.Box box)
        {
            if (!_previewing)
            {
                _previewing = true;
                _previewStart = Box;
            }
            Size = new Vector2(box.W, box.H);
            Place(new Vector2(box.X, box.Y));
        }

        /// <summary>Keeps (or, cancelled, undoes) the box a neighbour's resize gave it.</summary>
        internal void EndPreview(bool keep, Vector2 screen)
        {
            if (!_previewing) return;
            _previewing = false;
            SetBox(keep ? Box : _previewStart, screen);
        }

        private void EndSharing(bool keep, Vector2 screen)
        {
            foreach (UiWindow w in _sharing) w.EndPreview(keep, screen);
            _sharing.Clear();
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            // State first: letting the pointer go may report a lost capture, which must find nothing to undo.
            Gesture gesture = _gesture;
            UiPanel tab = _tabPressed;
            _gesture = Gesture.None;
            _tabPressed = null;
            ((VisualElement)e.currentTarget).ReleasePointer(e.pointerId);
            Root.style.opacity = StyleKeyword.Null;
            _ui.HideDragGhost();
            _ui.HideLanding();
            switch (gesture)
            {
                case Gesture.PullOut:
                    _ui.DropPanel(tab, e.position, _landingFits ? _landing : (WindowRules.Box?)null);
                    return;
                case Gesture.Move when _dockInto != null && Panels.Count == 1:
                    _ui.DropPanel(Panels[0], e.position, null);
                    return;
                case Gesture.Resize:
                    SetBox(_landingFits ? _landing : _startBox, _ui.ScreenSize);
                    EndSharing(_landingFits, _ui.ScreenSize);
                    _ui.SaveLayout();
                    return;
                case Gesture.Move:
                    // Dropped flush beside a window of nearly the same height (or width), it lines up
                    // with it exactly, so rows and columns come out even without pixel pushing.
                    WindowRules.Box dropped = _landingFits ? _landing : _startBox;
                    if (_landingFits && !IsFloating && Resizable && AllowedSizes == null && !e.altKey)
                        dropped = _ui.MatchBeside(this, dropped);
                    SetBox(dropped, _ui.ScreenSize);
                    _ui.SaveLayout();
                    return;
            }
        }

        /// <summary>A gesture lost the pointer (focus left, the window closed): put everything back.</summary>
        private void CancelGesture()
        {
            if (_gesture == Gesture.None && _tabPressed == null) return;
            bool moved = _gesture == Gesture.Move || _gesture == Gesture.Resize;
            _gesture = Gesture.None;
            _tabPressed = null;
            Root.style.opacity = StyleKeyword.Null;
            _ui.HideDragGhost();
            _ui.HideLanding();
            if (moved) SetBox(_startBox, _ui.ScreenSize);
            EndSharing(false, _ui.ScreenSize);
        }

        // ------------------------------------------------------------------ tabs: click, pull out, dock

        private void PressTab(UiPanel panel, PointerDownEvent e)
        {
            if (e.button != 0) return;
            Activate(panel);
            _tabPressed = panel;
            _tabPressedAt = e.position;
            _gesture = Gesture.None;
            _landingFits = false;
            ((VisualElement)e.currentTarget).CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        /// <summary>Whether a point (panel space) is over this window's title bar, where panels can be dropped.</summary>
        public bool AcceptsDropAt(Vector2 point) => !IsBare && _titleBar.worldBound.Contains(point);

        /// <summary>Whether this window could take <paramref name="panel"/> as another tab, if it had room.</summary>
        public bool CouldDock(UiPanel panel) =>
            !IsBare && !IsPlaceholder && Resizable && AllowedSizes == null && FixedSize == null
            && !panel.FixedSize.HasValue && panel.AllowedSizes == null && !panel.Bare && !(panel is PlaceholderPanel) && IsFloating == panel.Floating;

        /// <summary>Whether this window can take <paramref name="panel"/> as another tab.</summary>
        public bool CanDock(UiPanel panel) => !IsFull && CouldDock(panel);

        /// <summary>Marks the window as where a dragged tab would go (an outline that changes nothing's size), or as full.</summary>
        public void MarkDockTarget(bool on, bool full)
        {
            Root.EnableInClassList("window--dock-target", on && !full);
            Root.EnableInClassList("window--full", on && full);
        }
    }
}
