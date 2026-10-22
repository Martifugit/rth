using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rth.Shared.World;
using Rth.Shared.World.Layout;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Modals
{
    /// <summary>The world map (M). Waystone travel will become a second tab.</summary>
    public sealed class WorldMapModal : UiModal
    {
        public override string Id => "worldmap";
        public override string Title => "World map";
        public override string Icon => "map";
        public override Key Hotkey => Key.M;
        public override Vector2 Size => new Vector2(1040f, 680f);

        protected override IEnumerable<ModalTab> CreateTabs() => new ModalTab[] { new WorldMapTab() };
    }

    /// <summary>
    /// The continent, drawn from the layout the world is built from (the server sends it as
    /// <see cref="WorldChart"/> with the content): the ground painted once into a texture
    /// (<see cref="ChartPainter"/>, on a worker thread: a quick draft, then the full picture), and
    /// over it, as vectors so they stay crisp at every zoom, the rivers, roads, passes and ferries,
    /// the built regions (land beyond them is shaded as unexplored), the places by tier and the
    /// labels. Drag to pan, the wheel to zoom (<see cref="WorldMapView"/>), "you are here" with
    /// your facing. Placeholder names never reach the client: those places are unnamed marks.
    /// </summary>
    public sealed class WorldMapTab : ModalTab
    {
        public override string Id => "map";
        public override string Title => "The world";
        public override string Icon => "map";

        private const string NamesPref = "rth.map.names";
        private const string RealmsPref = "rth.map.realms";
        private const string ShadePref = "rth.map.shade";
        private const string LegendPref = "rth.map.legend";

        private VisualElement _view;
        private VisualElement _content;
        private VisualElement _ground;
        private MapInk _ink;
        private VisualElement _labels;
        private VisualElement _you;
        private VisualElement _youPulse;
        private Label _status;
        private Label _where;
        private Label _hover;
        private VisualElement _legend;
        private Label _zoomIn, _zoomOut;
        private UiKit.Check _namesCheck, _realmsCheck, _shadeCheck;

        private WorldChart _chart;
        private WorldMapView _mapView;
        private string _rasterKey;
        /// <summary>The ground at 4, 2 and 1 tiles a pixel (index by tiles a pixel): each zoom reads the one nearest its own scale.</summary>
        private readonly Texture2D[] _textures = new Texture2D[5];
        private Texture2D _shown;
        private int _paintGeneration;
        private readonly System.Collections.Concurrent.ConcurrentQueue<PaintResult> _ready = new System.Collections.Concurrent.ConcurrentQueue<PaintResult>();
        private bool _centreOnOpen = true;
        private float _labelledScale = -1f;
        private int _regionCount = -1;

        private bool _dragging;
        private int _dragPointer = -1;
        private Vector2 _dragLast;
        private Vector2? _pointer;

        private bool _showNames, _showRealms, _shade;

        private readonly List<(VisualElement Label, float X, float Y, float MinScale, float MaxScale, float Lift)> _placed =
            new List<(VisualElement, float, float, float, float, float)>();

        private sealed class PaintResult
        {
            public int Generation;
            public byte[] Rgba;
            public int Width, Height;
            public int TilesPerPixel;
        }

        // ------------------------------------------------------------------ building

        protected override void Build(VisualElement root)
        {
            _showNames = PlayerPrefs.GetInt(NamesPref, 1) == 1;
            _showRealms = PlayerPrefs.GetInt(RealmsPref, 1) == 1;
            _shade = PlayerPrefs.GetInt(ShadePref, 1) == 1;

            _view = UiKit.El(root, "worldmap__view");
            _content = UiKit.El(_view, "worldmap__content");
            _content.pickingMode = PickingMode.Ignore;
            _ground = UiKit.El(_content, "worldmap__ground");
            _ground.pickingMode = PickingMode.Ignore;
            _ink = new MapInk(this);
            _content.Add(_ink);
            _labels = UiKit.El(_content, "worldmap__layer");
            _labels.pickingMode = PickingMode.Ignore;

            _youPulse = UiKit.El(_content, "worldmap__you-pulse");
            _youPulse.pickingMode = PickingMode.Ignore;
            _you = new YouAreHere();
            _content.Add(_you);

            _status = UiKit.Text(_view, "", "worldmap__status");
            _status.pickingMode = PickingMode.Ignore;

            VisualElement tools = UiKit.El(_view, "worldmap__tools");
            _zoomIn = UiKit.Button(tools, "+", () => ZoomBy(+1, ViewCentre), "worldmap__tool");
            UiKit.Tooltip(_zoomIn, () => "Zoom in (wheel)");
            _zoomOut = UiKit.Button(tools, "–", () => ZoomBy(-1, ViewCentre), "worldmap__tool");
            UiKit.Tooltip(_zoomOut, () => "Zoom out (wheel)");
            VisualElement centre = UiKit.IconButton(tools, "compass", "Back to where you are", CentreOnYou);
            centre.AddToClassList("worldmap__tool");
            VisualElement legendButton = UiKit.IconButton(tools, "book", "Legend", () => ShowLegend(_legend.style.display == DisplayStyle.None));
            legendButton.AddToClassList("worldmap__tool");

            _legend = UiKit.El(_view, "worldmap__legend");
            BuildLegend(_legend);
            ShowLegend(PlayerPrefs.GetInt(LegendPref, 0) == 1);

            VisualElement footer = UiKit.El(root, "worldmap__footer");
            _where = UiKit.Text(footer, "");
            UiKit.El(footer, "spacer");
            _hover = UiKit.Text(footer, "", "muted", "small");

            _view.RegisterCallback<WheelEvent>(OnWheel);
            _view.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _view.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _view.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _view.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);
            _view.RegisterCallback<PointerLeaveEvent>(_ => _pointer = null);
            _view.RegisterCallback<GeometryChangedEvent>(_ => Layout());
        }

        private void BuildLegend(VisualElement legend)
        {
            UiKit.Text(legend, "LEGEND", "heading");
            VisualElement columns = UiKit.El(legend, "row");
            VisualElement left = UiKit.El(columns, "worldmap__legend-column");
            VisualElement right = UiKit.El(columns, "worldmap__legend-column");
            void Entry(VisualElement column, string text, Action<Painter2D, Vector2> draw)
            {
                VisualElement row = UiKit.El(column, "worldmap__legend-row");
                row.Add(new Swatch(draw));
                UiKit.Text(row, text, "small");
            }
            Entry(left, "City", (p, c) => MapInk.Place(p, c, "city", true));
            Entry(left, "Town", (p, c) => MapInk.Place(p, c, "town", true));
            Entry(left, "Village", (p, c) => MapInk.Place(p, c, "village", true));
            Entry(left, "Hamlet", (p, c) => MapInk.Place(p, c, "hamlet", true));
            Entry(left, "Landmark", (p, c) => MapInk.Place(p, c, "landmark", true));
            Entry(left, "Not yet named", (p, c) => MapInk.Place(p, c, "village", false));
            Entry(right, "Main road", (p, c) => MapInk.Road(p, new[] { c - new Vector2(11, 0), c + new Vector2(11, 0) }, "main", 1f));
            Entry(right, "Road", (p, c) => MapInk.Road(p, new[] { c - new Vector2(11, 0), c + new Vector2(11, 0) }, "road", 1f));
            Entry(right, "Mountain pass", (p, c) => MapInk.Road(p, new[] { c - new Vector2(11, 0), c + new Vector2(11, 0) }, "pass", 1f));
            Entry(right, "Ferry", (p, c) => MapInk.Road(p, new[] { c - new Vector2(11, 0), c + new Vector2(11, 0) }, "ferry", 1f));
            Entry(right, "River", (p, c) => MapInk.River(p, new[] { c - new Vector2(11, 2), c + new Vector2(0, -1), c + new Vector2(11, 2) }, 3f));
            Entry(right, "Explored land", (p, c) => MapInk.Explored(p, new Rect(c.x - 10, c.y - 6, 20, 12)));

            UiKit.El(legend, "rule");
            _namesCheck = new UiKit.Check(legend, "Place names", () => Toggle(ref _showNames, NamesPref));
            _realmsCheck = new UiKit.Check(legend, "Realm names", () => Toggle(ref _showRealms, RealmsPref));
            _shadeCheck = new UiKit.Check(legend, "Shade unexplored land", () => Toggle(ref _shade, ShadePref));
        }

        private void Toggle(ref bool value, string pref)
        {
            value = !value;
            PlayerPrefs.SetInt(pref, value ? 1 : 0);
            _labelledScale = -1f;
            _ink.MarkDirtyRepaint();
        }

        private void ShowLegend(bool show)
        {
            _legend.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            PlayerPrefs.SetInt(LegendPref, show ? 1 : 0);
        }

        public override void OnShow(string focus)
        {
            _centreOnOpen = true;
            Layout();
        }

        // ------------------------------------------------------------------ every frame

        public override void Refresh()
        {
            WorldChart chart = Session?.Content?.Chart;
            if (chart != _chart) SetChart(chart);

            while (_ready.TryDequeue(out PaintResult painted))
                if (painted.Generation == _paintGeneration) Upload(painted);

            int regions = BuiltRegions().Count;
            if (regions != _regionCount)
            {
                _regionCount = regions;
                _ink.MarkDirtyRepaint();
            }

            _namesCheck.Set(_showNames);
            _realmsCheck.Set(_showRealms);
            _shadeCheck.Set(_shade);
            if (_mapView != null)
            {
                _zoomIn.SetEnabled(_mapView.CanZoomIn);
                _zoomOut.SetEnabled(_mapView.CanZoomOut);
            }

            WorldRegion current = Ui.regions != null ? Ui.regions.Current : null;
            _where.text = current != null ? "You are in " + UiKit.Colour(current.Title, UiKit.Value) : "";
            PlaceYou();
            _hover.text = _pointer.HasValue ? Describe(_pointer.Value) : "";
        }

        private void SetChart(WorldChart chart)
        {
            _chart = chart;
            _labels.Clear();
            _placed.Clear();
            _labelledScale = -1f;
            if (chart == null)
            {
                _mapView = null;
                _content.style.display = DisplayStyle.None;
                _status.text = "The world map isn't available: the server has no continent layout.";
                return;
            }
            _content.style.display = DisplayStyle.Flex;
            if (_mapView == null || _mapView.MapWidth != chart.WidthTiles || _mapView.MapHeight != chart.HeightTiles)
            {
                _mapView = new WorldMapView(chart.WidthTiles, chart.HeightTiles);
                _centreOnOpen = true;
            }
            BuildLabels(chart);

            // The ground is painted again only when it changed (content reloads resend the chart).
            string key = chart.Ground + "|" + chart.Power + "|" + string.Join(",", chart.Grounds.Select(g => g.Colour + g.Height + g.Rough));
            if (key != _rasterKey)
            {
                _rasterKey = key;
                Paint(chart);
            }
            Layout();
            _ink.MarkDirtyRepaint();
        }

        private void BuildLabels(WorldChart chart)
        {
            foreach (ChartPower power in chart.Powers)
            {
                if (string.IsNullOrEmpty(power.Name) || power.Label == null || power.Label.Length < 2) continue;
                Label label = UiKit.Text(_labels, power.Name.ToUpperInvariant(), "worldmap__realm");
                label.pickingMode = PickingMode.Ignore;
                label.userData = "realm";
                _placed.Add((label, power.Label[0], power.Label[1], 0f, 1.01f, -1f));
            }
            foreach (ChartLine line in chart.Rivers.Concat(chart.Roads))
            {
                if (string.IsNullOrEmpty(line.Name) || line.Points.Length < 4) continue;
                (float x, float y) = Midpoint(line.Points);
                Label label = UiKit.Text(_labels, line.Name, "worldmap__feature", line.Kind == "river" ? "worldmap__feature--river" : "worldmap__feature--pass");
                label.pickingMode = PickingMode.Ignore;
                _placed.Add((label, x, y, 1f, float.MaxValue, -1f));
            }
            // Places last, so their names sit over the realms' and rivers'.
            foreach (ChartPlace place in chart.Places)
            {
                if (string.IsNullOrEmpty(place.Name)) continue;
                Label label = UiKit.Text(_labels, place.Name, "worldmap__label", "worldmap__label--" + place.Tier);
                label.pickingMode = PickingMode.Ignore;
                float min = place.Tier switch { "hamlet" => 1f, "landmark" => 0.75f, _ => 0f };
                _placed.Add((label, place.X, place.Y, min, float.MaxValue, MapInk.Radius(place.Tier) + 1f));
            }
        }

        /// <summary>The point halfway along a line, by length.</summary>
        private static (float x, float y) Midpoint(float[] pts)
        {
            float total = 0f;
            for (int k = 0; k + 3 < pts.Length; k += 2) total += Mathf.Sqrt(Sq(pts[k + 2] - pts[k]) + Sq(pts[k + 3] - pts[k + 1]));
            float half = total / 2f;
            for (int k = 0; k + 3 < pts.Length; k += 2)
            {
                float len = Mathf.Sqrt(Sq(pts[k + 2] - pts[k]) + Sq(pts[k + 3] - pts[k + 1]));
                if (half <= len && len > 0f)
                    return (pts[k] + (pts[k + 2] - pts[k]) * half / len, pts[k + 1] + (pts[k + 3] - pts[k + 1]) * half / len);
                half -= len;
            }
            return (pts[0], pts[1]);
        }

        private static float Sq(float v) => v * v;

        // ------------------------------------------------------------------ painting the ground

        private void Paint(WorldChart chart)
        {
            int generation = ++_paintGeneration;
            // Palette names are resolved here: the palette loads from Resources, on the main thread.
            var named = new Dictionary<string, ChartPainter.Rgb>();
            foreach (ChartGround g in chart.Grounds)
                if (ChartPainter.Parse(g.Colour) == null && Palette.TryParse(g.Colour, out Color c))
                {
                    Color32 c32 = c;
                    named[g.Colour] = new ChartPainter.Rgb(c32.r, c32.g, c32.b);
                }
            ChartPainter.Rgb? Resolve(string name) => named.TryGetValue(name, out ChartPainter.Rgb rgb) ? rgb : (ChartPainter.Rgb?)null;

            Task.Run(() =>
            {
                try
                {
                    foreach (int tilesPerPixel in new[] { 4, 2, 1 }) // quick drafts first, then the whole picture
                    {
                        if (generation != _paintGeneration) return;
                        byte[] rgba = ChartPainter.Paint(chart, Resolve, tilesPerPixel, out int w, out int h);
                        _ready.Enqueue(new PaintResult { Generation = generation, Rgba = rgba, Width = w, Height = h, TilesPerPixel = tilesPerPixel });
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }

        private void Upload(PaintResult painted)
        {
            var texture = new Texture2D(painted.Width, painted.Height, TextureFormat.RGBA32, true, false)
            {
                name = "World map",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 2,
            };
            texture.SetPixelData(painted.Rgba, 0);
            texture.Apply(true, true); // mipmaps for zooming out, then free the CPU copy
            if (_textures[painted.TilesPerPixel] != null) UnityEngine.Object.Destroy(_textures[painted.TilesPerPixel]);
            _textures[painted.TilesPerPixel] = texture;
            ShowGround();
        }

        /// <summary>
        /// Shows the painting whose pixels come nearest one to a screen pixel at this zoom (so a
        /// zoomed-out map doesn't shimmer), or the finest one ready while the rest are painted.
        /// </summary>
        private void ShowGround()
        {
            float s = _mapView?.Scale ?? 1f;
            int want = s < 0.4f ? 4 : s < 0.9f ? 2 : 1;
            Texture2D texture = _textures[want] ?? _textures[1] ?? _textures[2] ?? _textures[4];
            _status.text = texture == null ? "Drawing the map\u2026" : "";
            if (texture == _shown) return;
            _shown = texture;
            _ground.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
        }

        // ------------------------------------------------------------------ the view

        private Vector2 ViewCentre => _view.contentRect.size / 2f;

        private void Layout()
        {
            if (_mapView == null || _view == null) return;
            Vector2 size = _view.contentRect.size;
            if (size.x < 1f || size.y < 1f) return;
            _mapView.Resize(size.x, size.y);
            if (_centreOnOpen)
            {
                _centreOnOpen = false; // each time it opens, on you, at the zoom it was left at
                CentreOnYou();
                return;
            }
            Apply();
        }

        private void Apply()
        {
            if (_mapView == null || _chart == null) return;
            float s = _mapView.Scale;
            _content.style.translate = new Translate(_mapView.PanX, _mapView.PanY);
            float w = _chart.WidthTiles * s, h = _chart.HeightTiles * s;
            _content.style.width = w;
            _content.style.height = h;
            ShowGround();
            if (Mathf.Abs(_labelledScale - s) > 1e-4f)
            {
                _labelledScale = s;
                _ink.MarkDirtyRepaint();
                foreach ((VisualElement label, float x, float y, float min, float max, float lift) in _placed)
                {
                    bool realm = (string)label.userData == "realm";
                    bool shown = s >= min && s <= max && (realm ? _showRealms : _showNames);
                    label.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                    if (!shown) continue;
                    Vector2 p = MapPixel(x, y);
                    label.style.left = p.x;
                    label.style.top = lift >= 0f ? p.y - lift : p.y;
                    label.style.translate = new Translate(Length.Percent(-50f), Length.Percent(lift >= 0f ? -100f : -50f));
                }
            }
            PlaceYou();
        }

        private void ZoomBy(int steps, Vector2 pivot)
        {
            if (_mapView != null && _mapView.Zoom(steps, pivot.x, pivot.y)) Apply();
        }

        private void CentreOnYou()
        {
            if (_mapView == null || _chart == null) return;
            EntityView local = Ui.world != null ? Ui.world.Local : null;
            Vector2 tile = local != null
                ? new Vector2(local.transform.position.x, local.transform.position.z)
                : new Vector2(_chart.OriginX + _chart.WidthTiles / 2f, _chart.OriginY + _chart.HeightTiles / 2f);
            _mapView.CentreOn(tile.x - _chart.OriginX, _chart.OriginY + _chart.HeightTiles - tile.y);
            Apply();
        }

        /// <summary>A tile's place on the map, in the content's pixels (north up).</summary>
        internal Vector2 MapPixel(float x, float y)
        {
            float s = _mapView?.Scale ?? 1f;
            return new Vector2((x - _chart.OriginX) * s, (_chart.OriginY + _chart.HeightTiles - y) * s);
        }

        /// <summary>The tile under a point in the view's pixels.</summary>
        private Vector2 TileAt(Vector2 viewPoint)
        {
            (float mx, float my) = _mapView.ToMap(viewPoint.x, viewPoint.y);
            return new Vector2(_chart.OriginX + mx, _chart.OriginY + _chart.HeightTiles - my);
        }

        private void PlaceYou()
        {
            EntityView local = Ui.world != null ? Ui.world.Local : null;
            bool show = local != null && _chart != null && _mapView != null;
            _you.style.display = _youPulse.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            Vector3 position = local.transform.position;
            Vector2 p = MapPixel(position.x, position.z);
            Vector3 forward = local.transform.forward;
            float facing = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg; // clockwise from north
            _you.style.left = p.x;
            _you.style.top = p.y;
            _you.style.rotate = new Rotate(new Angle(facing, AngleUnit.Degree));
            float pulse = Mathf.Repeat(Time.unscaledTime * 0.7f, 1f);
            _youPulse.style.left = p.x;
            _youPulse.style.top = p.y;
            _youPulse.style.scale = new Scale(Vector2.one * (0.4f + pulse * 1.2f));
            _youPulse.style.opacity = 1f - pulse;
        }

        // ------------------------------------------------------------------ input

        private void OnWheel(WheelEvent e)
        {
            ZoomBy(e.delta.y > 0f ? -1 : +1, _view.WorldToLocal(e.mousePosition));
            e.StopPropagation();
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 0 || _mapView == null) return;
            if (e.target != _view) return; // the tools and the legend are buttons, not the map
            _dragging = true;
            _dragPointer = e.pointerId;
            _dragLast = _view.WorldToLocal(e.position);
            _view.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            Vector2 local = _view.WorldToLocal(e.position);
            _pointer = local;
            if (!_dragging || e.pointerId != _dragPointer || _mapView == null) return;
            Vector2 delta = local - _dragLast;
            _dragLast = local;
            _mapView.PanBy(delta.x, delta.y);
            Apply();
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (!_dragging || e.pointerId != _dragPointer) return;
            _dragging = false;
            if (_view.HasPointerCapture(e.pointerId)) _view.ReleasePointer(e.pointerId);
        }

        /// <summary>What is under the pointer: a place, or the ground and who holds it.</summary>
        private string Describe(Vector2 viewPoint)
        {
            if (_chart == null || _mapView == null) return "";
            Vector2 tile = TileAt(viewPoint);
            float reach = 9f / _mapView.Scale;
            ChartPlace near = null;
            float best = reach * reach;
            foreach (ChartPlace place in _chart.Places)
            {
                float d = Sq(place.X - tile.x) + Sq(place.Y - tile.y);
                if (d <= best) { best = d; near = place; }
            }
            string where = $"{Mathf.FloorToInt(tile.x)}, {Mathf.FloorToInt(tile.y)}";
            if (near != null)
            {
                string tier = near.Tier == "landmark" ? "" : " (" + near.Tier + ")";
                return string.IsNullOrEmpty(near.Name)
                    ? $"A {near.Tier}, not yet named  ·  {where}"
                    : UiKit.Colour(near.Name, UiKit.Value) + tier + "  ·  " + where;
            }
            ChartGround ground = _chart.GroundAt(tile.x, tile.y);
            if (ground == null) return "";
            ChartPower power = _chart.PowerAt(tile.x, tile.y);
            WorldRegion region = BuiltRegions().FirstOrDefault(r => tile.x >= r.X && tile.x < r.X + r.Width && tile.y >= r.Y && tile.y < r.Y + r.Height);
            string text = ground.Name;
            if (power != null && !string.IsNullOrEmpty(power.Name)) text += "  ·  " + power.Name;
            if (region != null) text += "  ·  " + UiKit.Colour(region.Title, UiKit.Value);
            else if (!ground.Water) text += "  ·  unexplored";
            return text + "  ·  " + where;
        }

        internal List<WorldRegion> BuiltRegions() =>
            Ui.regions?.Regions?.Where(r => !r.Instanced).ToList() ?? new List<WorldRegion>(); // arenas live far off

        internal WorldChart Chart => _chart;
        internal float Scale => _mapView?.Scale ?? 1f;
        internal bool ShadeUnexplored => _shade;

        // ------------------------------------------------------------------ the vector layer

        /// <summary>
        /// Everything drawn over the painted ground, in the content's pixels at the current zoom:
        /// rivers, roads, the explored regions and the unexplored shade, the places' marks.
        /// Redrawn only when the zoom or what it shows changes.
        /// </summary>
        private sealed class MapInk : VisualElement
        {
            private static readonly Color RiverColour = new Color32(70, 122, 178, 255);
            private static readonly Color RoadCasing = new Color32(48, 33, 20, 235);
            private static readonly Color MainRoad = new Color32(214, 180, 118, 255);
            private static readonly Color MinorRoad = new Color32(84, 60, 36, 230);
            private static readonly Color FerryColour = new Color32(236, 226, 200, 210);
            private static readonly Color ExploredEdge = new Color32(240, 213, 140, 200);
            private static readonly Color Veil = new Color32(10, 8, 6, 115);
            private static readonly Color Gold = new Color32(240, 213, 140, 255);
            private static readonly Color Unnamed = new Color32(186, 166, 126, 255);
            private static readonly Color Ink = new Color32(28, 20, 12, 255);

            private readonly WorldMapTab _tab;

            public MapInk(WorldMapTab tab)
            {
                _tab = tab;
                pickingMode = PickingMode.Ignore;
                AddToClassList("worldmap__layer");
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                WorldChart chart = _tab.Chart;
                if (chart == null) return;
                Painter2D p = context.painter2D;
                float s = _tab.Scale;

                foreach (ChartLine river in chart.Rivers) River(p, Points(river), Mathf.Clamp(river.Width * s, 1.4f, 7f));
                foreach (string kind in new[] { "road", "pass", "ferry", "main" }) // main roads on top
                    foreach (ChartLine road in chart.Roads)
                        if (road.Kind == kind) Road(p, Points(road), kind, s);

                List<WorldRegion> built = _tab.BuiltRegions();
                if (_tab.ShadeUnexplored && built.Count > 0)
                {
                    // The whole map with a hole for each built region.
                    p.fillColor = Veil;
                    p.BeginPath();
                    Rect all = new Rect(0f, 0f, chart.WidthTiles * s, chart.HeightTiles * s);
                    RectPath(p, all);
                    foreach (WorldRegion r in built) RectPath(p, RegionRect(r));
                    p.Fill(FillRule.OddEven);
                }
                foreach (WorldRegion r in built) Explored(p, RegionRect(r));

                foreach (ChartPlace place in chart.Places)
                {
                    if (place.Tier == "hamlet" && s < 0.75f) continue;
                    Place(p, _tab.MapPixel(place.X, place.Y), place.Tier, !string.IsNullOrEmpty(place.Name));
                }
            }

            private Rect RegionRect(WorldRegion r)
            {
                Vector2 nw = _tab.MapPixel(r.X, r.Y + r.Height);
                Vector2 se = _tab.MapPixel(r.X + r.Width, r.Y);
                return Rect.MinMaxRect(nw.x, nw.y, se.x, se.y);
            }

            private Vector2[] Points(ChartLine line)
            {
                var pts = new Vector2[line.Points.Length / 2];
                for (int k = 0; k < pts.Length; k++) pts[k] = _tab.MapPixel(line.Points[k * 2], line.Points[k * 2 + 1]);
                return pts;
            }

            private static void RectPath(Painter2D p, Rect r)
            {
                p.MoveTo(new Vector2(r.xMin, r.yMin));
                p.LineTo(new Vector2(r.xMax, r.yMin));
                p.LineTo(new Vector2(r.xMax, r.yMax));
                p.LineTo(new Vector2(r.xMin, r.yMax));
                p.ClosePath();
            }

            public static float Radius(string tier) => tier switch
            {
                "city" => 6.5f,
                "town" => 5f,
                "village" => 3.5f,
                "hamlet" => 2.5f,
                _ => 4.5f,
            };

            public static void River(Painter2D p, Vector2[] pts, float width)
            {
                p.lineJoin = LineJoin.Round;
                p.lineCap = LineCap.Round;
                p.strokeColor = RiverColour;
                p.lineWidth = width;
                Polyline(p, pts);
                p.Stroke();
            }

            public static void Road(Painter2D p, Vector2[] pts, string kind, float scale)
            {
                p.lineJoin = LineJoin.Round;
                p.lineCap = LineCap.Round;
                switch (kind)
                {
                    case "main":
                        float w = Mathf.Clamp(1.4f + scale, 2.2f, 4.5f);
                        p.strokeColor = RoadCasing;
                        p.lineWidth = w + 2f;
                        Polyline(p, pts);
                        p.Stroke();
                        p.strokeColor = MainRoad;
                        p.lineWidth = w;
                        Polyline(p, pts);
                        p.Stroke();
                        break;
                    case "pass":
                        p.strokeColor = RoadCasing;
                        p.lineWidth = Mathf.Clamp(1.5f + scale * 0.5f, 1.8f, 3f);
                        p.lineCap = LineCap.Butt;
                        Dashed(p, pts, 6f, 4f);
                        p.Stroke();
                        break;
                    case "ferry":
                        p.strokeColor = FerryColour;
                        p.lineWidth = 1.6f;
                        p.lineCap = LineCap.Butt;
                        Dashed(p, pts, 3f, 4f);
                        p.Stroke();
                        break;
                    default:
                        p.strokeColor = MinorRoad;
                        p.lineWidth = Mathf.Clamp(0.8f + scale * 0.6f, 1.3f, 2.6f);
                        Polyline(p, pts);
                        p.Stroke();
                        break;
                }
            }

            public static void Explored(Painter2D p, Rect r)
            {
                p.strokeColor = ExploredEdge;
                p.lineWidth = 1.2f;
                p.lineJoin = LineJoin.Miter;
                p.BeginPath();
                RectPath(p, r);
                p.Stroke();
            }

            /// <summary>A place's mark: a gold disc by size for settlements (paler when unnamed), a diamond for landmarks.</summary>
            public static void Place(Painter2D p, Vector2 c, string tier, bool named)
            {
                float r = Radius(tier);
                p.fillColor = named ? Gold : Unnamed;
                p.strokeColor = Ink;
                p.lineWidth = 1.5f;
                p.BeginPath();
                if (tier == "landmark")
                {
                    p.MoveTo(c + new Vector2(0f, -r));
                    p.LineTo(c + new Vector2(r, 0f));
                    p.LineTo(c + new Vector2(0f, r));
                    p.LineTo(c + new Vector2(-r, 0f));
                    p.ClosePath();
                }
                else p.Arc(c, r, new Angle(0f), new Angle(360f));
                p.Fill();
                p.Stroke();
                if (tier == "city")
                {
                    p.fillColor = Ink;
                    p.BeginPath();
                    p.Arc(c, 2.2f, new Angle(0f), new Angle(360f));
                    p.Fill();
                }
            }

            private static void Polyline(Painter2D p, Vector2[] pts)
            {
                p.BeginPath();
                if (pts.Length == 0) return;
                p.MoveTo(pts[0]);
                for (int k = 1; k < pts.Length; k++) p.LineTo(pts[k]);
            }

            private static void Dashed(Painter2D p, Vector2[] pts, float dash, float gap)
            {
                p.BeginPath();
                float phase = 0f; // how far into the current dash-and-gap
                for (int k = 0; k + 1 < pts.Length; k++)
                {
                    Vector2 a = pts[k], b = pts[k + 1];
                    float len = Vector2.Distance(a, b);
                    float t = 0f;
                    while (t < len)
                    {
                        bool drawing = phase < dash;
                        float step = Mathf.Min(len - t, drawing ? dash - phase : dash + gap - phase);
                        if (drawing)
                        {
                            p.MoveTo(Vector2.Lerp(a, b, t / len));
                            p.LineTo(Vector2.Lerp(a, b, (t + step) / len));
                        }
                        t += step;
                        phase = (phase + step) % (dash + gap);
                    }
                }
            }
        }

        /// <summary>A legend entry's little picture, drawn with the map's own ink.</summary>
        private sealed class Swatch : VisualElement
        {
            public Swatch(Action<Painter2D, Vector2> draw)
            {
                AddToClassList("worldmap__swatch");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += ctx => draw(ctx.painter2D, contentRect.center);
            }
        }

        /// <summary>An arrow in a ring: where you stand and which way you face. Rotated by the tab.</summary>
        private sealed class YouAreHere : VisualElement
        {
            private static readonly Color Fill = new Color32(222, 62, 46, 255);
            private static readonly Color Rim = new Color32(255, 240, 210, 255);

            public YouAreHere()
            {
                AddToClassList("worldmap__you");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Painter2D p = context.painter2D;
                Vector2 c = contentRect.center;
                p.lineJoin = LineJoin.Round;
                p.fillColor = Fill;
                p.strokeColor = Rim;
                p.lineWidth = 2f;
                p.BeginPath();
                p.MoveTo(c + new Vector2(0f, -10f));
                p.LineTo(c + new Vector2(7f, 8f));
                p.LineTo(c + new Vector2(0f, 4f));
                p.LineTo(c + new Vector2(-7f, 8f));
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }
        }
    }
}
