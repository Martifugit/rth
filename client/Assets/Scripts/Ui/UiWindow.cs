using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>
    /// A window holding one or more panels. With one panel the title bar shows its icon and name;
    /// with several, a strip of icon tabs (at most <see cref="MaxTabs"/>). The title bar moves the
    /// window, any corner resizes it, and a tab (or a lone panel's icon) dragged elsewhere docks
    /// the panel into another window or undocks it into its own; while dragging, an outline shows
    /// where it will land. A window with a single bare panel (bars, the minimap) has no frame, just
    /// a hover handle.
    ///
    /// <b>Docked</b> windows (the HUD) never overlap each other: <see cref="WindowRules"/> resolves
    /// every move and resize against the others. <b>Floating</b> windows (the bank, a shop, the
    /// conversation) sit in a layer above, open centred, and may overlap. While the interface is
    /// locked, nothing moves, resizes or closes.
    ///
    /// Position is kept relative to an anchor (a corner, an edge's middle, or the centre), so
    /// layouts survive a change of resolution.
    /// </summary>
    public sealed class UiWindow
    {
        /// <summary>The most panels one window holds as tabs.</summary>
        public const int MaxTabs = 6;
        private const float TabDragThreshold = 6f;

        private readonly UiRoot _ui;
        private readonly VisualElement _titleBar;
        private readonly VisualElement _titleIcon;
        private readonly Label _title;
        private readonly VisualElement _tabs;
        private readonly VisualElement _close;
        private readonly VisualElement _body;
        private readonly Dictionary<UiPanel, VisualElement> _tabElements = new Dictionary<UiPanel, VisualElement>();

        public readonly VisualElement Root;
        public readonly List<UiPanel> Panels = new List<UiPanel>();
        public UiPanel Active { get; private set; }

        /// <summary>0, 0.5 or 1 on each axis: left/centre/right and top/middle/bottom.</summary>
        public Vector2 Anchor;
        /// <summary>Offset from the anchored position, in panel pixels.</summary>
        public Vector2 Offset;
        public Vector2 Size;

        private enum Gesture { None, Move, Resize }

        private Gesture _gesture;
        private WindowRules.Corner _corner;
        private Vector2 _pointerStart;
        private WindowRules.Box _startBox;
        private WindowRules.Box _landing;
        private bool _landingFits;
        private UiPanel _tabPressed;
        private Vector2 _tabPressedAt;
        private bool _tabDragging;

        public bool IsBare => Panels.Count == 1 && Panels[0].Bare;

        /// <summary>Floating windows (server windows: bank, shop, conversation) may overlap and sit above the HUD.</summary>
        public bool IsFloating => Panels.Any(p => p.Floating);

        public bool IsFull => Panels.Count >= MaxTabs;

        /// <summary>The size every panel in it insists on, if any: the window then can't be resized.</summary>
        public Vector2? FixedSize
        {
            get
            {
                foreach (UiPanel p in Panels)
                    if (p.FixedSize.HasValue) return p.FixedSize;
                return null;
            }
        }

        public UiWindow(UiRoot ui)
        {
            _ui = ui;
            Root = new VisualElement();
            Root.AddToClassList("window");
            Root.RegisterCallback<PointerDownEvent>(_ => Root.BringToFront(), TrickleDown.TrickleDown);

            _titleBar = UiKit.El(Root, "window__titlebar");
            _titleIcon = UiKit.El(_titleBar, "window__title-icon");
            _title = UiKit.Text(_titleBar, "", "window__title");
            _title.pickingMode = PickingMode.Ignore;
            _tabs = UiKit.El(_titleBar, "window__tabs");
            // Room to grab the title bar between the tabs and the close button.
            UiKit.El(_titleBar, "window__drag-space").pickingMode = PickingMode.Ignore;
            _close = UiKit.El(_titleBar, "window__button", "window__close");
            UiKit.SetIcon(_close, IconLibrary.Get("close"));
            _close.RegisterCallback<ClickEvent>(_ => { if (!_ui.Locked) _ui.CloseWindow(this); });
            UiKit.Tooltip(_close, () => "Close");
            _titleBar.RegisterCallback<PointerDownEvent>(BeginMove);

            // A lone panel's icon is its tab: drag it onto another window to combine them.
            _titleIcon.RegisterCallback<PointerDownEvent>(e => { if (Panels.Count == 1) PressTab(Panels[0], e); });
            _titleIcon.RegisterCallback<PointerMoveEvent>(DragTab);
            _titleIcon.RegisterCallback<PointerUpEvent>(e => { if (Panels.Count == 1) ReleaseTab(_titleIcon, Panels[0], e); });

            _body = UiKit.El(Root, "window__body");

            var grips = new List<VisualElement>();
            foreach ((WindowRules.Corner corner, string cls) in new[]
                     {
                         (WindowRules.Corner.TopLeft, "window__grip--tl"), (WindowRules.Corner.TopRight, "window__grip--tr"),
                         (WindowRules.Corner.BottomLeft, "window__grip--bl"), (WindowRules.Corner.BottomRight, "window__grip--br"),
                     })
            {
                VisualElement grip = UiKit.El(Root, "window__grip", cls);
                WindowRules.Corner c = corner;
                grip.RegisterCallback<PointerDownEvent>(e => BeginResize(e, c));
                grips.Add(grip);
            }

            VisualElement handle = UiKit.El(Root, "window__handle");
            handle.RegisterCallback<PointerDownEvent>(BeginMove);
            UiKit.Tooltip(handle, () => _ui.Locked ? null : "Drag to move");

            foreach (VisualElement e in grips.Append(_titleBar).Append(handle))
            {
                e.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                e.RegisterCallback<PointerUpEvent>(OnPointerUp);
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
            UiKit.Icon(tab, IconLibrary.Get(panel.Icon), 22f, "tab__icon").pickingMode = PickingMode.Ignore;
            UiKit.Tooltip(tab, () => panel.Title);
            tab.RegisterCallback<PointerDownEvent>(e => PressTab(panel, e));
            tab.RegisterCallback<PointerMoveEvent>(DragTab);
            tab.RegisterCallback<PointerUpEvent>(e => ReleaseTab(tab, panel, e));
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
                Rect bar = _titleBar.worldBound;
                bool before = point.x < bar.center.x;
                return (before ? 0 : 1, before ? bar.xMin + 4f : _title.worldBound.xMax);
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
            if (Panels.Count == 1 && panel != null) _title.text = panel.Title;
        }

        private void UpdateFrame()
        {
            bool bare = IsBare;
            bool single = Panels.Count == 1;
            Root.EnableInClassList("window--bare", bare);
            Root.EnableInClassList("window--fixed", FixedSize.HasValue);
            Root.EnableInClassList("window--floating", IsFloating);
            _titleBar.style.display = bare ? DisplayStyle.None : DisplayStyle.Flex;
            _tabs.style.display = single ? DisplayStyle.None : DisplayStyle.Flex;
            _titleIcon.style.display = _title.style.display = single ? DisplayStyle.Flex : DisplayStyle.None;
            if (single)
            {
                UiKit.SetIcon(_titleIcon, IconLibrary.Get(Panels[0].Icon));
                _title.text = Panels[0].Title;
            }
            // A bare window has no frame to click: only what its panel draws catches the pointer.
            Root.pickingMode = _body.pickingMode = bare ? PickingMode.Ignore : PickingMode.Position;
        }

        public Vector2 MinSize
        {
            get
            {
                var min = new Vector2(120f, 80f);
                foreach (UiPanel p in Panels) min = Vector2.Max(min, p.MinSize);
                return min;
            }
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Puts the window where its anchor and offset say, kept on screen.</summary>
        public void Apply(Vector2 screen)
        {
            Size = FixedSize ?? Vector2.Max(MinSize, Vector2.Min(Size, screen));
            Vector2 pos = new Vector2(Anchor.x * (screen.x - Size.x), Anchor.y * (screen.y - Size.y)) + Offset;
            pos.x = Mathf.Clamp(pos.x, 0f, Mathf.Max(0f, screen.x - Size.x));
            pos.y = Mathf.Clamp(pos.y, 0f, Mathf.Max(0f, screen.y - Size.y));
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

        /// <summary>Moves the window to a box (position and size) and re-anchors it there.</summary>
        public void SetBox(WindowRules.Box box, Vector2 screen)
        {
            Size = new Vector2(box.W, box.H);
            Reanchor(screen, new Vector2(box.X, box.Y));
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
            if (_ui.Locked || e.button != 0) return;
            // Buttons and tabs in the title bar get their own clicks: don't start a move on them.
            if (e.target is VisualElement target && (target.ClassListContains("window__button") || target.ClassListContains("tab"))) return;
            StartGesture(Gesture.Move, e);
        }

        private void BeginResize(PointerDownEvent e, WindowRules.Corner corner)
        {
            if (_ui.Locked || e.button != 0 || FixedSize.HasValue) return;
            _corner = corner;
            StartGesture(Gesture.Resize, e);
        }

        private void StartGesture(Gesture gesture, PointerDownEvent e)
        {
            _gesture = gesture;
            _pointerStart = e.position;
            _startBox = Box;
            _landing = _startBox;
            _landingFits = true;
            ((VisualElement)e.currentTarget).CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (_gesture == Gesture.None) return;
            Vector2 screen = _ui.ScreenSize;
            Vector2 delta = (Vector2)e.position - _pointerStart;
            List<WindowRules.Box> others = _ui.ObstaclesFor(this);
            var guides = new List<WindowRules.Guide>();
            if (_gesture == Gesture.Move)
            {
                WindowRules.Box moved = _startBox.At(_startBox.X + delta.x, _startBox.Y + delta.y);
                WindowRules.Box snapped = WindowRules.Snap(moved, _ui.SnapTargetsFor(this), screen.x, screen.y, guides);
                WindowRules.Box? free = IsFloating ? WindowRules.Clamp(snapped, screen.x, screen.y) : WindowRules.NearestFree(snapped, others, screen.x, screen.y);
                // Flush beside a neighbour, a resizable window takes its height (or width): easy alignment.
                if (free.HasValue && !IsFloating && !FixedSize.HasValue)
                {
                    Vector2 min = MinSize;
                    free = WindowRules.MatchNeighbour(free.Value, others, min.x, min.y, screen.x, screen.y);
                }
                _landingFits = free.HasValue;
                _landing = free ?? _startBox;
                // The window follows the pointer; the outline shows where it will settle.
                Place(new Vector2(snapped.X, snapped.Y));
            }
            else
            {
                Vector2 min = MinSize;
                _landing = WindowRules.Resize(_startBox, _corner, delta.x, delta.y, min.x, min.y,
                    IsFloating ? new List<WindowRules.Box>() : others, screen.x, screen.y);
                _landingFits = true;
                Size = new Vector2(_landing.W, _landing.H);
                Place(new Vector2(_landing.X, _landing.Y));
            }
            _ui.ShowLanding(_landing, _landingFits, null, guides);
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (_gesture == Gesture.None) return;
            ((VisualElement)e.currentTarget).ReleasePointer(e.pointerId);
            _gesture = Gesture.None;
            _ui.HideLanding();
            SetBox(_landingFits ? _landing : _startBox, _ui.ScreenSize);
            _ui.SaveLayout();
        }

        // ------------------------------------------------------------------ tabs: click, pull out, dock

        private void PressTab(UiPanel panel, PointerDownEvent e)
        {
            if (e.button != 0) return;
            Activate(panel);
            _tabPressed = panel;
            _tabPressedAt = e.position;
            _tabDragging = false;
            ((VisualElement)e.currentTarget).CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void DragTab(PointerMoveEvent e)
        {
            if (_tabPressed == null || _ui.Locked) return;
            if (!_tabDragging && ((Vector2)e.position - _tabPressedAt).magnitude > TabDragThreshold) _tabDragging = true;
            if (!_tabDragging) return;
            _ui.ShowDragGhost(_tabPressed.Title, IconLibrary.Get(_tabPressed.Icon), UiKit.IconTint, e.position);
            _ui.PreviewPanelDrop(_tabPressed, e.position);
        }

        private void ReleaseTab(VisualElement tab, UiPanel panel, PointerUpEvent e)
        {
            tab.ReleasePointer(e.pointerId);
            bool dragged = _tabDragging;
            _tabPressed = null;
            _tabDragging = false;
            _ui.HideDragGhost();
            _ui.HideLanding();
            if (dragged) _ui.DropPanel(panel, e.position);
        }

        /// <summary>Whether a point (panel space) is over this window's title bar, where panels can be dropped.</summary>
        public bool AcceptsDropAt(Vector2 point) => !IsBare && _titleBar.worldBound.Contains(point);

        /// <summary>Whether this window can take <paramref name="panel"/> as another tab.</summary>
        public bool CanDock(UiPanel panel) =>
            !IsBare && !IsFull && !FixedSize.HasValue && !panel.FixedSize.HasValue && !panel.Bare && IsFloating == panel.Floating;
    }
}
