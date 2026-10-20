using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>
    /// A movable window holding one or more panels as tabs. Drag the title bar to move it (edges
    /// snap to the screen and to other windows), the corner to resize it, a tab to pull that panel
    /// out into its own window or drop it onto another window. A window with a single bare panel
    /// (bars, the minimap) has no frame, just a handle that shows on hover.
    ///
    /// Position is kept relative to an anchor (a corner, an edge's middle, or the centre), so
    /// layouts survive a change of resolution.
    /// </summary>
    public sealed class UiWindow
    {
        private const float SnapDistance = 8f;
        private const float TabDragThreshold = 6f;

        private readonly UiRoot _ui;
        private readonly VisualElement _titleBar;
        private readonly VisualElement _tabs;
        private readonly VisualElement _body;
        private readonly Dictionary<UiPanel, Label> _tabLabels = new Dictionary<UiPanel, Label>();

        public readonly VisualElement Root;
        public readonly List<UiPanel> Panels = new List<UiPanel>();
        public UiPanel Active { get; private set; }

        /// <summary>0, 0.5 or 1 on each axis: left/centre/right and top/middle/bottom.</summary>
        public Vector2 Anchor;
        /// <summary>Offset from the anchored position, in panel pixels.</summary>
        public Vector2 Offset;
        public Vector2 Size;

        private bool _moving;
        private bool _resizing;
        private Vector2 _pointerStart;
        private Vector2 _rectStart;
        private Vector2 _sizeStart;
        private UiPanel _tabPressed;
        private Vector2 _tabPressedAt;
        private bool _tabDragging;

        public bool IsBare => Panels.Count == 1 && Panels[0].Bare;

        public UiWindow(UiRoot ui)
        {
            _ui = ui;
            Root = new VisualElement();
            Root.AddToClassList("window");
            Root.RegisterCallback<PointerDownEvent>(_ => Root.BringToFront(), TrickleDown.TrickleDown);

            _titleBar = UiKit.El(Root, "window__titlebar");
            _tabs = UiKit.El(_titleBar, "window__tabs");
            Label close = UiKit.Text(_titleBar, "x", "window__button", "window__close");
            close.RegisterCallback<ClickEvent>(_ => _ui.CloseWindow(this));
            _titleBar.RegisterCallback<PointerDownEvent>(BeginMove);

            _body = UiKit.El(Root, "window__body");

            VisualElement grip = UiKit.El(Root, "window__grip");
            grip.RegisterCallback<PointerDownEvent>(BeginResize);

            VisualElement handle = UiKit.El(Root, "window__handle");
            handle.RegisterCallback<PointerDownEvent>(BeginMove);

            foreach (VisualElement e in new[] { _titleBar, grip, handle })
            {
                e.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                e.RegisterCallback<PointerUpEvent>(OnPointerUp);
            }
        }

        // ------------------------------------------------------------------ panels and tabs

        public void Add(UiPanel panel, bool activate = true)
        {
            if (Panels.Contains(panel)) return;
            panel.Window?.Remove(panel);
            Panels.Add(panel);
            panel.Window = this;

            Label tab = UiKit.Text(_tabs, panel.Tab, "tab");
            UiKit.Tooltip(tab, () => panel.Title);
            tab.RegisterCallback<PointerDownEvent>(e => PressTab(panel, e));
            tab.RegisterCallback<PointerMoveEvent>(e => DragTab(tab, e));
            tab.RegisterCallback<PointerUpEvent>(e => ReleaseTab(tab, panel, e));
            _tabLabels[panel] = tab;

            if (activate || Active == null) Activate(panel);
            UpdateFrame();
        }

        public void Remove(UiPanel panel)
        {
            if (!Panels.Remove(panel)) return;
            panel.Window = null;
            panel.Root.RemoveFromHierarchy();
            if (_tabLabels.TryGetValue(panel, out Label tab)) tab.RemoveFromHierarchy();
            _tabLabels.Remove(panel);
            if (Active == panel) Activate(Panels.Count > 0 ? Panels[0] : null);
            UpdateFrame();
        }

        public void Activate(UiPanel panel)
        {
            if (Active != null) Active.Root.RemoveFromHierarchy();
            Active = panel;
            if (panel != null) _body.Add(panel.Root);
            foreach (KeyValuePair<UiPanel, Label> t in _tabLabels)
                t.Value.EnableInClassList("tab--active", t.Key == panel);
        }

        private void UpdateFrame()
        {
            bool bare = IsBare;
            Root.EnableInClassList("window--bare", bare);
            _titleBar.style.display = bare ? DisplayStyle.None : DisplayStyle.Flex;
            // A bare window has no frame to click: only what its panel draws catches the pointer.
            Root.pickingMode = _body.pickingMode = bare ? PickingMode.Ignore : PickingMode.Position;
        }

        public Vector2 MinSize
        {
            get
            {
                var min = new Vector2(80f, 40f);
                foreach (UiPanel p in Panels) min = Vector2.Max(min, p.MinSize);
                return min;
            }
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Puts the window where its anchor and offset say, kept on screen.</summary>
        public void Apply(Vector2 screen)
        {
            Size = Vector2.Max(MinSize, Vector2.Min(Size, screen));
            Vector2 pos = new Vector2(Anchor.x * (screen.x - Size.x), Anchor.y * (screen.y - Size.y)) + Offset;
            pos.x = Mathf.Clamp(pos.x, 0f, Mathf.Max(0f, screen.x - Size.x));
            pos.y = Mathf.Clamp(pos.y, 0f, Mathf.Max(0f, screen.y - Size.y));
            Root.style.left = pos.x;
            Root.style.top = pos.y;
            Root.style.width = Size.x;
            Root.style.height = Size.y;
        }

        public Rect Rect => new Rect(Root.resolvedStyle.left, Root.resolvedStyle.top, Size.x, Size.y);

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
            if (_ui.Locked || e.button != 0 || e.target != e.currentTarget && !(e.target is VisualElement t && t.ClassListContains("window__tabs"))) return;
            _moving = true;
            _pointerStart = e.position;
            _rectStart = new Vector2(Root.resolvedStyle.left, Root.resolvedStyle.top);
            ((VisualElement)e.currentTarget).CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void BeginResize(PointerDownEvent e)
        {
            if (_ui.Locked || e.button != 0) return;
            _resizing = true;
            _pointerStart = e.position;
            _sizeStart = Size;
            ((VisualElement)e.currentTarget).CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (!_moving && !_resizing) return;
            Vector2 screen = _ui.ScreenSize;
            Vector2 delta = (Vector2)e.position - _pointerStart;
            if (_moving)
            {
                Vector2 pos = Snap(_rectStart + delta, Size, screen);
                Root.style.left = pos.x;
                Root.style.top = pos.y;
            }
            else
            {
                Size = Vector2.Max(MinSize, _sizeStart + delta);
                Root.style.width = Size.x;
                Root.style.height = Size.y;
            }
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (!_moving && !_resizing) return;
            ((VisualElement)e.currentTarget).ReleasePointer(e.pointerId);
            _moving = _resizing = false;
            Reanchor(_ui.ScreenSize, new Vector2(Root.resolvedStyle.left, Root.resolvedStyle.top));
            Apply(_ui.ScreenSize);
            _ui.SaveLayout();
        }

        /// <summary>Pulls each edge onto a nearby screen edge or another window's edge.</summary>
        private Vector2 Snap(Vector2 pos, Vector2 size, Vector2 screen)
        {
            var xs = new List<float> { 0f, screen.x };
            var ys = new List<float> { 0f, screen.y };
            foreach (UiWindow other in _ui.Windows)
            {
                if (other == this) continue;
                Rect r = other.Rect;
                xs.Add(r.xMin);
                xs.Add(r.xMax);
                ys.Add(r.yMin);
                ys.Add(r.yMax);
            }
            pos.x = SnapAxis(pos.x, size.x, xs);
            pos.y = SnapAxis(pos.y, size.y, ys);
            return pos;
        }

        private static float SnapAxis(float start, float length, List<float> lines)
        {
            float best = SnapDistance;
            float result = start;
            foreach (float line in lines)
            {
                if (Mathf.Abs(start - line) < best)
                {
                    best = Mathf.Abs(start - line);
                    result = line;
                }
                if (Mathf.Abs(start + length - line) < best)
                {
                    best = Mathf.Abs(start + length - line);
                    result = line - length;
                }
            }
            return result;
        }

        // ------------------------------------------------------------------ tabs: click, pull out, merge

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

        private void DragTab(Label tab, PointerMoveEvent e)
        {
            if (_tabPressed == null || _ui.Locked) return;
            if (!_tabDragging && ((Vector2)e.position - _tabPressedAt).magnitude > TabDragThreshold) _tabDragging = true;
            if (_tabDragging) _ui.ShowDragGhost(_tabPressed.Title, e.position);
        }

        private void ReleaseTab(Label tab, UiPanel panel, PointerUpEvent e)
        {
            tab.ReleasePointer(e.pointerId);
            bool dragged = _tabDragging;
            _tabPressed = null;
            _tabDragging = false;
            _ui.HideDragGhost();
            if (dragged) _ui.DropPanel(panel, e.position);
        }

        /// <summary>Whether a point (panel space) is over this window's tab strip, where panels can be dropped.</summary>
        public bool AcceptsDropAt(Vector2 point) => !IsBare && _titleBar.worldBound.Contains(point);
    }
}
