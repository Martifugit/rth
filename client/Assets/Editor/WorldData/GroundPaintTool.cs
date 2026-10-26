using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rth.MapTools;
using Rth.Shared.World;
using Rth.Shared.World.Data;
using Rth.Shared.World.Ground;
using Rth.Shared.World.Layout;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Shapes and paints the ground in the Scene view (docs/world-guide.md section 4), over every
    /// open region at once. <b>Shape</b>: raise, lower, flatten or smooth the heights with a round
    /// brush (tiles too steep to walk show red). <b>Paint</b>: roads, paths, grass, sand, water, tile
    /// by tile, from the layout's grounds and any a region has of its own: a round brush with a soft
    /// edge, a straight stroke (a road, straight or at 45 degrees), fill a patch, smooth ragged
    /// edges, or pick up the ground under the cursor (Ctrl+click). <b>Stones</b> (the Paint tab's
    /// other layer): gravel, chalk chips or rubble scattered over any ground, on their own, as thick
    /// or thin as you like and, if wanted, unevenly; the same brushes, and Smooth spreads them out.
    /// It is drawn as the game draws it while you work. Saving the scenes (Ctrl+S) writes heights and tiles to the regions' data
    /// (which takes a generated region over) and the neighbours' rings of tiles to theirs.
    /// R'th > World > Shape and Paint Ground (G) starts it from anywhere.
    /// </summary>
    [EditorTool("Ground", typeof(TileGround))]
    public sealed class GroundPaintTool : EditorTool
    {
        private enum Tab { Shape, Paint }
        private enum ShapeMode { Raise, Lower, Flatten, Smooth }
        private enum Mode { Brush, Line, Fill, Smooth, Pick }
        private enum Layer { Ground, Stones }

        private static Tab _tab = Tab.Paint;
        private static ShapeMode _shapeMode = ShapeMode.Raise;
        private static float _shapeRadius = 4f;
        private static float _strength = 0.5f;
        private static Mode _mode = Mode.Brush;
        private static float _radius = 1.5f;
        private static float _softness = 0.4f;
        private static int _width = 2;
        private static bool _snap = true;
        private static GroundEdit.Brush _brush;
        private static List<GroundEdit.Brush> _brushes;
        private static Layer _layer = Layer.Ground;
        /// <summary>The stones to paint with; <see cref="NoStones"/> clears them.</summary>
        private static GroundEdit.StoneBrush _stoneBrush;
        private static List<GroundEdit.StoneBrush> _stoneBrushes;
        private static readonly GroundEdit.StoneBrush NoStones = new GroundEdit.StoneBrush { Id = "", Name = "Clear stones" };
        /// <summary>How thick, as an index into <see cref="GroundStones.Levels"/>.</summary>
        private static int _thickness = 1;
        private static bool _uneven = true;
        private static Vector2 _scroll;
        private static string _filter = "";
        private static GUIStyle _panel;

        private Vector3? _point;
        private (int x, int y)? _hover, _lineStart;
        private Vector3? _last;
        private int _undoGroup, _seed;
        private float _flattenTo;
        private readonly HashSet<TileGround> _touched = new HashSet<TileGround>();
        /// <summary>The thickness each tile was given in this stroke of stones.</summary>
        private readonly Dictionary<(int, int), int> _strokeStones = new Dictionary<(int, int), int>();
        /// <summary>Changed but not yet redrawn: a redraw takes a tenth of a second, so a drag redraws a few times a second.</summary>
        private readonly HashSet<TileGround> _stale = new HashSet<TileGround>();
        /// <summary>Heights shaped in this stroke and not yet given to their ground (it redraws when given them).</summary>
        private readonly Dictionary<TileGround, float[]> _heights = new Dictionary<TileGround, float[]>();
        private double _redrawn;

        public override GUIContent toolbarIcon => new GUIContent("Ground", "Shape and paint the ground: heights, roads, paths and grass");

        [MenuItem("R'th/World/Shape and Paint Ground _g", priority = 7)]
        private static void Begin()
        {
            TileGround ground = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<TileGround>() : null;
            if (ground == null && SceneView.lastActiveSceneView != null)
            {
                Vector3 p = SceneView.lastActiveSceneView.pivot;
                ground = GroundAt(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z)) ?? TileGround.Active.FirstOrDefault(g => g != null);
            }
            if (ground == null)
            {
                EditorUtility.DisplayDialog("Shape and Paint Ground", "Open a region with tile ground first (R'th > Open Selected Area).", "OK");
                return;
            }
            // A component's tools are offered once the selection has caught up.
            Selection.activeGameObject = ground.gameObject;
            EditorApplication.delayCall += () => ToolManager.SetActiveTool<GroundPaintTool>();
        }

        [MenuItem("R'th/World/Shape and Paint Ground _g", true)]
        private static bool BeginValidate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ------------------------------------------------------------------ the Scene view

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView) || EditorApplication.isPlaying) return;
            Event e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
            if (_brushes == null) LoadBrushes();

            Rect panel = new Rect(10, 10, 300, _tab == Tab.Shape ? 214 : 610);
            bool overPanel = panel.Contains(e.mousePosition);
            _point = overPanel ? null : Pick(HandleUtility.GUIPointToWorldRay(e.mousePosition));
            _hover = _point is Vector3 at ? (Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.z)) : ((int, int)?)null;
            if (_point is Vector3 p) DrawBrush(p);

            bool left = e.button == 0 && !e.alt && !overPanel;
            if (e.type == EventType.MouseDown && left && _point is Vector3 down && _hover is (int x, int y))
            {
                if (_tab == Tab.Paint && (e.control || _mode == Mode.Pick)) PickUp(x, y);
                else if (_tab == Tab.Shape || (_layer == Layer.Stones ? _stoneBrush != null : _brush != null) || _mode == Mode.Smooth)
                {
                    _undoGroup = Undo.GetCurrentGroup();
                    _touched.Clear();
                    _seed = Random.Range(1, int.MaxValue);
                    if (_tab == Tab.Shape)
                    {
                        _flattenTo = WorldCorner(Mathf.RoundToInt(down.x), Mathf.RoundToInt(down.z)) ?? down.y;
                        Shape(down);
                    }
                    else if (_mode == Mode.Line) _lineStart = (x, y);
                    else if (_mode == Mode.Fill) { PaintAll(Flood(x, y)); EndStroke(); }
                    else Dab(down);
                    _last = down;
                    GUIUtility.hotControl = control;
                }
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == control && _point is Vector3 drag)
            {
                if (_tab == Tab.Shape) Shape(drag);
                else if ((_mode == Mode.Brush || _mode == Mode.Smooth) && _last is Vector3 from)
                {
                    // A fast drag skips tiles: dab along the way, a third of the brush apart.
                    float step = Mathf.Max(0.5f, _radius / 3f);
                    int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, drag) / step));
                    for (int i = 1; i <= n; i++) Dab(Vector3.Lerp(from, drag, (float)i / n));
                }
                _last = drag;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == control)
            {
                if (_tab == Tab.Paint && _mode == Mode.Line && _lineStart is (int sx, int sy) && _hover is (int ux, int uy)) PaintAll(Stroke(sx, sy, ux, uy));
                EndStroke();
                GUIUtility.hotControl = 0;
                e.Use();
            }
            else if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket))
            {
                float step = e.keyCode == KeyCode.RightBracket ? 1f : -1f;
                if (_tab == Tab.Shape) _shapeRadius = Mathf.Clamp(_shapeRadius + step, 1f, 16f);
                else if (_mode == Mode.Line) _width = Mathf.Clamp(_width + (int)step, 1, 8);
                else _radius = Mathf.Clamp(_radius + step * 0.5f, 0f, 10f);
                e.Use();
            }
            if (_tab == Tab.Paint && _mode == Mode.Line && _lineStart is (int ax, int ay) && GUIUtility.hotControl == control && _hover is (int bx, int by))
                DrawTiles(Stroke(ax, ay, bx, by), BrushColour(), 0.55f);
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) window.Repaint();

            Panel(panel);
        }

        /// <summary>
        /// The tiles a round brush covers at a point (tile middles within its radius; radius 0 is the
        /// tile under it), with how surely each is painted: 1 inside, falling to 0 over the soft edge.
        /// </summary>
        private static IEnumerable<(int x, int y, float weight)> Round(Vector3 p, float radius, float softness)
        {
            if (radius <= 0f)
            {
                yield return (Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z), 1f);
                yield break;
            }
            float inner = radius * (1f - softness);
            for (int y = Mathf.FloorToInt(p.z - radius); y <= Mathf.CeilToInt(p.z + radius); y++)
                for (int x = Mathf.FloorToInt(p.x - radius); x <= Mathf.CeilToInt(p.x + radius); x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(p.x, p.z));
                    if (d > radius) continue;
                    yield return (x, y, d <= inner ? 1f : 1f - (d - inner) / Mathf.Max(0.01f, radius - inner));
                }
        }

        /// <summary>Whether a soft-edged tile is painted in this stroke: the same answer all stroke long, so dabs don't fill the edge in.</summary>
        private bool Taken(int x, int y, float weight)
        {
            if (weight >= 1f) return true;
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)_seed;
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xffff) / 65535f < weight;
        }

        /// <summary>One dab of the paint brush: its tiles, or in Smooth, each of them made what most of its neighbours are.</summary>
        private void Dab(Vector3 p)
        {
            if (_layer == Layer.Stones)
            {
                if (_mode == Mode.Smooth) SpreadStones(Round(p, Mathf.Max(1f, _radius), 0f).Select(t => (t.x, t.y)).ToList());
                // The soft edge thins the stones out rather than leaving tiles bare.
                else PaintStones(Round(p, _radius, _softness).Select(t => (t.x, t.y, Mathf.CeilToInt((_thickness + 1) * t.weight) - 1)));
                return;
            }
            if (_mode == Mode.Smooth)
            {
                var targets = new Dictionary<(int, int), GroundEdit.Brush>();
                foreach ((int x, int y, float _) in Round(p, Mathf.Max(1f, _radius), 0f))
                    if (Majority(x, y) is GroundEdit.Brush b) targets[(x, y)] = b;
                Paint(targets.Keys, (x, y) => targets[(x, y)]);
                return;
            }
            Paint(Round(p, _radius, _softness).Where(t => Taken(t.x, t.y, t.weight)).Select(t => (t.x, t.y)));
        }

        /// <summary>
        /// What most of a tile and its eight neighbours are, if that differs from the tile and holds
        /// at least five of the nine: a speck goes, a ragged edge straightens, a real shape stays.
        /// </summary>
        private static GroundEdit.Brush Majority(int x, int y)
        {
            if (!(BrushAt(x, y) is GroundEdit.Brush own)) return null;
            var counts = new List<(GroundEdit.Brush brush, int n)>();
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (!(BrushAt(x + ox, y + oy) is GroundEdit.Brush b)) continue;
                    int i = counts.FindIndex(c => SameBrush(c.brush, b));
                    if (i < 0) counts.Add((b, 1));
                    else counts[i] = (counts[i].brush, counts[i].n + 1);
                }
            (GroundEdit.Brush best, int most) = counts.OrderByDescending(c => c.n).FirstOrDefault();
            return best != null && most >= 5 && !SameBrush(best, own) ? best : null;
        }

        private static bool SameBrush(GroundEdit.Brush a, GroundEdit.Brush b) => GroundEdit.Same(a.Kind, b.Kind) && a.Colour == b.Colour;

        /// <summary>A line or a fill: the ground brush's tiles, or stones as thick as the brush over all of them.</summary>
        private void PaintAll(IEnumerable<(int x, int y)> tiles)
        {
            if (_layer == Layer.Stones) PaintStones(tiles.Select(t => (t.x, t.y, _thickness)));
            else Paint(tiles);
        }

        private static HashSet<(int x, int y)> Stroke(int ax, int ay, int bx, int by)
        {
            (int ex, int ey) = _snap ? GroundEdit.Snap45(ax, ay, bx, by) : (bx, by);
            return GroundEdit.Line(ax, ay, ex, ey, _width);
        }

        private void EndStroke()
        {
            _lineStart = null;
            _last = null;
            _strokeStones.Clear();
            Redraw();
            _heights.Clear();
            if (_touched.Count == 0) return;
            if (_tab == Tab.Paint && _layer == Layer.Ground) RefreshRings(_touched);
            Undo.CollapseUndoOperations(_undoGroup);
            _touched.Clear();
        }

        // ------------------------------------------------------------------ the panel

        private static void Panel(Rect area)
        {
            _panel ??= new GUIStyle(GUI.skin.window) { padding = new RectOffset(12, 12, 28, 12) };
            Handles.BeginGUI();
            GUILayout.BeginArea(area, "Ground", _panel);
            float labels = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 74;
            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Shape", "Paint" }, GUILayout.Height(24));
            GUILayout.Space(10);
            if (_tab == Tab.Shape) ShapePanel();
            else PaintPanel();
            EditorGUIUtility.labelWidth = labels;
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static void ShapePanel()
        {
            _shapeMode = (ShapeMode)GUILayout.Toolbar((int)_shapeMode, new[] { "Raise", "Lower", "Flatten", "Smooth" });
            GUILayout.Space(10);
            _shapeRadius = Mathf.Round(EditorGUILayout.Slider("Radius", _shapeRadius, 1f, 16f));
            GUILayout.Space(4);
            _strength = EditorGUILayout.Slider("Strength", _strength, 0.05f, 1f);
            GUILayout.Space(10);
            GUILayout.Label("[ and ] change the size. Red tiles are too steep to walk.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(Changed() ? "Changed: save the scenes (Ctrl+S) to keep it." : " ", EditorStyles.miniLabel);
        }

        private static void PaintPanel()
        {
            Layer layer = (Layer)GUILayout.Toolbar((int)_layer, new[] { "Ground", "Stones" });
            if (layer != _layer) { _layer = layer; _scroll = Vector2.zero; }
            GUILayout.Space(6);
            _mode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "Brush", "Line", "Fill", "Smooth", "Pick" });
            GUILayout.Space(10);
            switch (_mode)
            {
                case Mode.Brush:
                    _radius = Mathf.Round(EditorGUILayout.Slider("Radius", _radius, 0f, 10f) * 2f) / 2f;
                    GUILayout.Space(4);
                    _softness = EditorGUILayout.Slider("Soft edge", _softness, 0f, 1f);
                    break;
                case Mode.Line:
                    _width = EditorGUILayout.IntSlider("Width", _width, 1, 8);
                    GUILayout.Space(4);
                    _snap = EditorGUILayout.Toggle("Straight/45°", _snap);
                    break;
                case Mode.Smooth:
                    _radius = Mathf.Round(EditorGUILayout.Slider("Radius", _radius, 1f, 10f) * 2f) / 2f;
                    GUILayout.Space(4);
                    GUILayout.Label(_layer == Layer.Stones
                        ? "Drag: the stones spread out, each tile taking the mean of the tiles round it."
                        : "Drag over ragged edges and specks: each tile becomes what most round it are.", EditorStyles.wordWrappedMiniLabel);
                    break;
                case Mode.Fill:
                    GUILayout.Label(_layer == Layer.Stones ? "Click: stones over the patch of one ground under it (a whole path)." : "Click: the patch of one ground under it.", EditorStyles.wordWrappedMiniLabel);
                    break;
                default:
                    GUILayout.Label(_layer == Layer.Stones ? "Click: take the stones under it." : "Click: take the ground under it.", EditorStyles.wordWrappedMiniLabel);
                    break;
            }
            if (_layer == Layer.Stones)
            {
                GUILayout.Space(4);
                _thickness = EditorGUILayout.IntSlider("Thickness", _thickness + 1, 1, GroundStones.Levels.Length) - 1;
                GUILayout.Space(4);
                _uneven = EditorGUILayout.Toggle(new GUIContent("Uneven", "Patches of thicker and thinner stones with bare gaps, as the blockout scatters them, rather than the same everywhere."), _uneven);
                GUILayout.Space(8);
                StonePanel();
                return;
            }
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            Swatch(_brush, 20);
            GUILayout.Space(6);
            GUILayout.Label(_brush != null ? _brush.Name : "Pick a ground below", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField);
            GUILayout.Space(6);
            _scroll = GUILayout.BeginScrollView(_scroll);
            string group = null;
            foreach (GroundEdit.Brush b in _brushes ?? new List<GroundEdit.Brush>())
            {
                if (_filter.Length > 0 && b.Name.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (b.Group != group)
                {
                    if (group != null) GUILayout.Space(6);
                    group = b.Group;
                    GUILayout.Label(group, EditorStyles.miniBoldLabel);
                }
                GUILayout.BeginHorizontal();
                GUILayout.Space(2);
                Swatch(b, 14);
                GUILayout.Space(6);
                GUIStyle style = b == _brush ? EditorStyles.boldLabel : EditorStyles.label;
                if (GUILayout.Button(b.Name, style)) _brush = b;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Space(6);
            GUILayout.Label(Changed() ? "Changed: save the scenes (Ctrl+S) to keep it." : "Ctrl+click: take the ground under the cursor. [ and ]: size.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4);
            if (GUILayout.Button("Reload the grounds", EditorStyles.miniButton)) LoadBrushes();
        }

        private static void StonePanel()
        {
            GUILayout.BeginHorizontal();
            StoneSwatch(_stoneBrush, 20);
            GUILayout.Space(6);
            GUILayout.Label(_stoneBrush != null ? _stoneBrush.Name : "Pick stones below", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (GroundEdit.StoneBrush b in new[] { NoStones }.Concat(_stoneBrushes ?? new List<GroundEdit.StoneBrush>()))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(2);
                StoneSwatch(b, 14);
                GUILayout.Space(6);
                if (GUILayout.Button(b.Name, b == _stoneBrush ? EditorStyles.boldLabel : EditorStyles.label)) _stoneBrush = b;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Space(6);
            GUILayout.Label(Changed() ? "Changed: save the scenes (Ctrl+S) to keep it." : "Ctrl+click: take the stones under the cursor. [ and ]: size.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4);
            if (GUILayout.Button("Reload the grounds", EditorStyles.miniButton)) LoadBrushes();
        }

        private static void StoneSwatch(GroundEdit.StoneBrush b, int size)
        {
            Rect r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            r.y += 2;
            EditorGUI.DrawRect(r, StoneColour(b));
            if (b == NoStones) EditorGUI.DrawRect(new Rect(r.x, r.center.y - 1, r.width, 2), new Color(0.8f, 0.25f, 0.2f));
        }

        private static Color StoneColour(GroundEdit.StoneBrush b) =>
            b != null && b != NoStones && Palette.TryParse(b.Colour, out Color c) ? c : new Color(0.5f, 0.5f, 0.5f, 0.4f);

        /// <summary>The colour the brush's tiles are shown in: the ground's, or the stones' (grey to clear them).</summary>
        private static Color? BrushColour() =>
            _layer == Layer.Stones ? (_stoneBrush != null ? StoneColour(_stoneBrush) : (Color?)null) : (_brush != null ? Colour(_brush) : (Color?)null);

        private static bool Changed() => TileGround.Active.Any(g => g != null && (!string.IsNullOrEmpty(g.brushEdit) || g.sculpted));

        private static void Swatch(GroundEdit.Brush b, int size)
        {
            Rect r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            r.y += 2;
            EditorGUI.DrawRect(r, Colour(b));
        }

        private static Color Colour(GroundEdit.Brush b) =>
            b != null && Palette.TryParse(b.Colour, out Color c) ? c : new Color(0.5f, 0.5f, 0.5f, 0.4f);

        // ------------------------------------------------------------------ drawing the brush

        private void DrawBrush(Vector3 p)
        {
            if (_tab == Tab.Shape)
            {
                DrawSteep(p, _shapeRadius * 3f);
                Handles.color = _shapeMode == ShapeMode.Lower ? new Color(1f, 0.5f, 0.3f) : new Color(0.4f, 0.9f, 1f);
                Circle(p, _shapeRadius);
                Circle(p, _shapeRadius * 0.5f);
                return;
            }
            if (_mode == Mode.Line || _mode == Mode.Fill || _mode == Mode.Pick)
            {
                if (_hover is (int x, int y)) DrawTiles(new[] { (x, y) }, BrushColour(), 0.55f);
                return;
            }
            float r = _mode == Mode.Smooth ? Mathf.Max(1f, _radius) : _radius;
            float soft = _mode == Mode.Smooth ? 0f : _softness;
            foreach ((int x, int y, float w) in Round(p, r, soft)) DrawTiles(new[] { (x, y) }, _mode == Mode.Smooth ? null : BrushColour(), 0.15f + 0.4f * w);
            Handles.color = Color.white;
            if (r > 0f) Circle(p, r);
        }

        /// <summary>A circle lying on the ground round a point (following its heights).</summary>
        private static void Circle(Vector3 p, float radius)
        {
            const int n = 48;
            var points = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float x = p.x + Mathf.Cos(a) * radius, z = p.z + Mathf.Sin(a) * radius;
                points[i] = new Vector3(x, (TileGround.TryHeightAt(x, z, out float h) ? h : p.y) + 0.08f, z);
            }
            Handles.DrawAAPolyLine(3f, points);
        }

        /// <summary>Fills tiles at the ground's height, in the brush's colour.</summary>
        private static void DrawTiles(IEnumerable<(int x, int y)> tiles, Color? colour, float alpha)
        {
            Color c = colour ?? new Color(0.9f, 0.9f, 0.9f);
            Color fill = new Color(c.r, c.g, c.b, alpha), line = new Color(1f, 1f, 1f, alpha * 0.35f);
            var quad = new Vector3[4];
            foreach ((int x, int y) in tiles)
            {
                if (!(GroundAt(x, y) is TileGround g)) continue;
                var f = g.Field;
                quad[0] = new Vector3(x, f.Corner(x, y) + 0.06f, y);
                quad[1] = new Vector3(x, f.Corner(x, y + 1) + 0.06f, y + 1);
                quad[2] = new Vector3(x + 1, f.Corner(x + 1, y + 1) + 0.06f, y + 1);
                quad[3] = new Vector3(x + 1, f.Corner(x + 1, y) + 0.06f, y);
                Handles.DrawSolidRectangleWithOutline(quad, fill, line);
            }
        }

        /// <summary>Tiles too steep to walk near the brush, in red.</summary>
        private static void DrawSteep(Vector3 p, float reach)
        {
            var quad = new Vector3[4];
            int r = Mathf.CeilToInt(reach), cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.z);
            for (int y = cy - r; y < cy + r; y++)
                for (int x = cx - r; x < cx + r; x++)
                {
                    if (!(GroundAt(x, y) is TileGround g) || !g.Field.IsSteep(x, y, g.cliffStep)) continue;
                    Heightfield f = g.Field;
                    quad[0] = new Vector3(x, f.Corner(x, y) + 0.05f, y);
                    quad[1] = new Vector3(x, f.Corner(x, y + 1) + 0.05f, y + 1);
                    quad[2] = new Vector3(x + 1, f.Corner(x + 1, y + 1) + 0.05f, y + 1);
                    quad[3] = new Vector3(x + 1, f.Corner(x + 1, y) + 0.05f, y);
                    Handles.DrawSolidRectangleWithOutline(quad, new Color(1f, 0.15f, 0.1f, 0.35f), new Color(1f, 0.15f, 0.1f, 0.8f));
                }
        }

        // ------------------------------------------------------------------ finding the ground

        private static TileGround GroundAt(int x, int y)
        {
            foreach (TileGround g in TileGround.Active)
                if (g != null && x >= g.originX && y >= g.originY && x < g.originX + g.width && y < g.originY + g.height) return g;
            return null;
        }

        /// <summary>The point under the mouse: a few steps between the ray and the ground's heights.</summary>
        private static Vector3? Pick(Ray ray)
        {
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return null;
            float y = 0f;
            Vector3 p = default;
            bool any = false;
            for (int i = 0; i < 8; i++)
            {
                float t = (y - ray.origin.y) / ray.direction.y;
                if (t < 0) return null;
                p = ray.origin + ray.direction * t;
                if (TileGround.TryHeightAt(p.x, p.z, out float h)) { y = h; any = true; }
            }
            if (!any || GroundAt(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z)) == null) return null;
            p.y = y;
            return p;
        }

        // ------------------------------------------------------------------ the grounds to paint with

        private static void LoadBrushes()
        {
            var brushes = new List<GroundEdit.Brush>();
            var stones = new List<GroundEdit.StoneBrush>();
            string continent = Path.Combine(WorldSync.RepoRoot, "world", "layout", "continent.json");
            try
            {
                if (File.Exists(continent))
                {
                    ContinentData data = ContinentData.Parse(File.ReadAllText(continent));
                    brushes.AddRange(GroundEdit.Brushes(data));
                    stones.AddRange(GroundEdit.StoneBrushes(data));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[R'th] Paint Ground: couldn't read the layout's grounds: " + e.Message);
            }
            // Ground the open regions have of their own.
            foreach (TileGround g in TileGround.Active)
            {
                if (g == null || !(Model(g) is EditModel m)) continue;
                foreach (TileKind k in m.Ground.Legend.Values)
                    if (k.Beyond != true && !brushes.Any(b => GroundEdit.Same(b.Kind, k) && b.Colour == (m.Materials.TryGetValue(k.Material, out string c) ? c : "")))
                        brushes.Add(GroundEdit.BrushOf(m.Region, k));
                foreach (StoneKind k in m.Ground.StoneLegend?.Values ?? Enumerable.Empty<StoneKind>())
                    if (StoneBrushOf(m, k) is GroundEdit.StoneBrush own && !stones.Any(b => b.Colour == own.Colour)) stones.Add(own);
            }
            _stoneBrushes = stones;
            if (_stoneBrush != null && _stoneBrush != NoStones) _stoneBrush = stones.FirstOrDefault(b => b.Colour == _stoneBrush.Colour) ?? _stoneBrush;
            string[] order = { "Ground", "Surfaces", "Water", "Blocked" };
            _brushes = brushes.OrderBy(b => System.Array.IndexOf(order, b.Group)).ThenBy(b => b.Name, System.StringComparer.OrdinalIgnoreCase).ToList();
            if (_brush != null) _brush = _brushes.FirstOrDefault(b => b.Id == _brush.Id && GroundEdit.Same(b.Kind, _brush.Kind)) ?? _brush;
        }

        /// <summary>A region's own stones as a brush (its material's colour), or null if it names none.</summary>
        private static GroundEdit.StoneBrush StoneBrushOf(EditModel m, StoneKind k) =>
            m.Materials.TryGetValue(k.Material, out string colour)
                ? new GroundEdit.StoneBrush { Id = k.Material, Name = k.Material.StartsWith("LandStones") ? k.Material.Substring(10) : k.Material, Material = k.Material, Colour = colour }
                : null;

        /// <summary>Ctrl+click: the ground (or in the Stones layer, the stones) under the cursor becomes the brush.</summary>
        private static void PickUp(int x, int y)
        {
            if (_layer == Layer.Stones)
            {
                if (!(GroundAt(x, y) is TileGround sg) || !(Model(sg) is EditModel sm)) return;
                if (!(GroundEdit.StoneAt(sm.Ground, x, y) is StoneKind k)) _stoneBrush = NoStones;
                else if (StoneBrushOf(sm, k) is GroundEdit.StoneBrush picked)
                {
                    _stoneBrush = _stoneBrushes.FirstOrDefault(b => b.Colour == picked.Colour);
                    if (_stoneBrush == null) _stoneBrushes.Add(_stoneBrush = picked);
                    _thickness = Mathf.Max(0, GroundStones.Level(k.Share));
                }
                if (_mode == Mode.Pick || _mode == Mode.Smooth) _mode = Mode.Brush;
                return;
            }
            if (!(BrushAt(x, y) is GroundEdit.Brush at)) return;
            _brush = _brushes.FirstOrDefault(b => SameBrush(b, at));
            if (_brush == null)
            {
                at.Letter = null;
                _brushes.Add(_brush = at);
            }
            if (_mode == Mode.Pick || _mode == Mode.Smooth) _mode = Mode.Brush;
        }

        // ------------------------------------------------------------------ shaping

        /// <summary>A corner's height as it stands in this stroke, from whichever open ground has it.</summary>
        private float? WorldCorner(int x, int y)
        {
            foreach (TileGround g in TileGround.Active)
            {
                if (g == null || x < g.originX || y < g.originY || x > g.originX + g.width || y > g.originY + g.height) continue;
                int i = (y - g.originY) * (g.width + 1) + (x - g.originX);
                return _heights.TryGetValue(g, out float[] h) ? h[i] : g.Field.Corner(x, y);
            }
            return null;
        }

        /// <summary>
        /// One step of the shaping brush: raise, lower, flatten to the height where the stroke began,
        /// or smooth, within a soft round brush. A corner on a border is shared, so every ground that
        /// has it gets the same height.
        /// </summary>
        private void Shape(Vector3 p)
        {
            float radius = _shapeRadius;
            var changes = new List<(int x, int y, float h)>();
            for (int y = Mathf.FloorToInt(p.z - radius); y <= Mathf.CeilToInt(p.z + radius); y++)
                for (int x = Mathf.FloorToInt(p.x - radius); x <= Mathf.CeilToInt(p.x + radius); x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(p.x, p.z));
                    if (d > radius || !(WorldCorner(x, y) is float h)) continue;
                    float weight = Mathf.SmoothStep(0f, 1f, 1f - d / radius) * _strength;
                    switch (_shapeMode)
                    {
                        case ShapeMode.Raise: h += 0.15f * weight; break;
                        case ShapeMode.Lower: h -= 0.15f * weight; break;
                        case ShapeMode.Flatten: h += (_flattenTo - h) * weight * 0.5f; break;
                        case ShapeMode.Smooth:
                        {
                            float sum = 0f;
                            int n = 0;
                            foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                                if (WorldCorner(nx, ny) is float hn) { sum += hn; n++; }
                            if (n > 0) h += (sum / n - h) * weight * 0.5f;
                            break;
                        }
                    }
                    changes.Add((x, y, Mathf.Clamp(h, -4f, 16f))); // the whole world stays within about 16 tiles (world-scale.md 3)
                }
            foreach ((int x, int y, float h) in changes)
                foreach (TileGround g in TileGround.Active)
                {
                    if (g == null || x < g.originX || y < g.originY || x > g.originX + g.width || y > g.originY + g.height) continue;
                    if (!_heights.TryGetValue(g, out float[] heights)) _heights[g] = heights = g.Field.Corners;
                    heights[(y - g.originY) * (g.width + 1) + (x - g.originX)] = h;
                    _stale.Add(g);
                    _touched.Add(g);
                }
            if (EditorApplication.timeSinceStartup - _redrawn > 0.15) Redraw();
        }

        // ------------------------------------------------------------------ each ground's data while painting

        /// <summary>A ground's tiles piece and its region's colours, as the brush changes them.</summary>
        private sealed class EditModel
        {
            public string RegionId = "";
            public Piece Ground;
            public SortedDictionary<string, string> Materials = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            [JsonIgnore] public RegionData Region => new RegionData { Id = RegionId, Materials = Materials };
        }

        /// <summary>A ground's model, and the fields it was read from or written to (compared by reference: undo puts back other arrays).</summary>
        private sealed class Entry
        {
            public string Edit, Kinds;
            public string[] Rows, Surround, Stones;
            public EditModel Model;

            public bool Fresh(TileGround g) => ReferenceEquals(Edit, g.brushEdit) && ReferenceEquals(Rows, g.rows) && ReferenceEquals(Surround, g.surround) &&
                                               ReferenceEquals(Stones, g.stoneRows) && Kinds == g.kinds;
        }

        private static readonly Dictionary<TileGround, Entry> Cache = new Dictionary<TileGround, Entry>();

        private static void Remember(TileGround g, EditModel m) =>
            Cache[g] = new Entry { Edit = g.brushEdit, Kinds = g.kinds, Rows = g.rows, Surround = g.surround, Stones = g.stoneRows, Model = m };
        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

        /// <summary>
        /// The ground's data: what the brush wrote (<see cref="TileGround.brushEdit"/>, which undo
        /// restores), else its region's file. Null if it has none (or the scene and its data
        /// disagree: Sync World first).
        /// </summary>
        private static EditModel Model(TileGround g)
        {
            if (Cache.TryGetValue(g, out Entry hit) && hit.Fresh(g)) return hit.Model;
            EditModel m = null;
            if (!string.IsNullOrEmpty(g.brushEdit)) m = JsonConvert.DeserializeObject<EditModel>(g.brushEdit, Json);
            else
            {
                MapArea area = g.GetComponentInParent<MapArea>();
                string path = area != null ? WorldSync.RegionPath(area.exportName) : null;
                if (path == null || !File.Exists(path)) return null;
                RegionData region = RegionFiles.LoadRegion(path);
                Piece piece = GroundEdit.Tiles(region);
                if (piece == null || piece.Rect[0] != g.originX || piece.Rect[1] != g.originY || !piece.Rows.SequenceEqual(g.rows)) return null;
                m = new EditModel { RegionId = region.Id, Ground = piece, Materials = new SortedDictionary<string, string>(region.Materials, System.StringComparer.Ordinal) };
            }
            Remember(g, m);
            return m;
        }

        /// <summary>Writes the model back into the ground (kept for saving, and for undo) and redraws it.</summary>
        private static void Apply(TileGround g, EditModel m, bool edited, bool redraw = true)
        {
            Undo.RecordObject(g, "Paint ground");
            PieceBuilder.SetTiles(g, new WorldKit(0), m.Region, m.Ground);
            if (g.corners != null && g.corners.Length > 0)
            {
                List<string> keys = m.Ground.Legend.Keys.ToList();
                g.cliffMaterials = keys.Select(k => m.Ground.Legend[k].Cliff is string own ? PieceBuilder.Material(new WorldKit(0), m.Region, own) : null).ToArray();
            }
            if (edited) g.brushEdit = JsonConvert.SerializeObject(m, Json);
            Remember(g, m);
            if (redraw) g.Rebuild();
            EditorUtility.SetDirty(g);
            EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
        }

        // ------------------------------------------------------------------ painting

        /// <summary>Paints tiles with the brush, or each with its own (<paramref name="brushAt"/>: smoothing).</summary>
        private void Paint(IEnumerable<(int x, int y)> tiles, System.Func<int, int, GroundEdit.Brush> brushAt = null)
        {
            if (_brush == null && brushAt == null) return;
            foreach (var byGround in tiles.GroupBy(t => GroundAt(t.x, t.y)))
            {
                TileGround g = byGround.Key;
                if (g == null) continue;
                EditModel m = Model(g);
                if (m == null)
                {
                    Debug.LogWarning($"[R'th] Paint Ground: {g.gameObject.scene.name}'s ground doesn't match its data; run Sync World on it first.");
                    continue;
                }
                m = JsonConvert.DeserializeObject<EditModel>(JsonConvert.SerializeObject(m, Json), Json); // undo keeps the one before
                bool changed = GroundEdit.EnsureRing(m.Ground);
                var chars = new Dictionary<GroundEdit.Brush, char>();
                foreach ((int x, int y) in byGround)
                {
                    GroundEdit.Brush b = brushAt != null ? brushAt(x, y) : _brush;
                    if (b == null) continue;
                    if (!chars.TryGetValue(b, out char c)) chars[b] = c = GroundEdit.CharFor(m.Region, m.Ground, b.Kind, b.Materials, b.Letter);
                    changed |= GroundEdit.Set(m.Ground, x, y, c);
                }
                if (!changed) continue;
                GroundEdit.Prune(m.Ground);
                Apply(g, m, edited: true, redraw: false);
                _touched.Add(g);
                _stale.Add(g);
            }
            if (EditorApplication.timeSinceStartup - _redrawn > 0.15) Redraw();
        }

        /// <summary>
        /// Paints stones at a thickness per tile (an index into <see cref="GroundStones.Levels"/>;
        /// below 0 leaves the tile as it is), or clears them with <see cref="NoStones"/>. Uneven, the
        /// thickness drifts in patches with bare gaps (<see cref="GroundStones.Spread"/>).
        /// </summary>
        private void PaintStones(IEnumerable<(int x, int y, int level)> tiles)
        {
            if (_stoneBrush == null) return;
            // A tile keeps the thickest it was given in this stroke: a drag's soft edges don't thin what its middle laid.
            var levels = new Dictionary<(int, int), int>();
            foreach ((int x, int y, int level) in tiles)
            {
                if (level < 0 || _strokeStones.TryGetValue((x, y), out int had) && had >= level) continue;
                levels[(x, y)] = _strokeStones[(x, y)] = level;
            }
            EditStones(levels.Keys, (m, x, y) =>
            {
                if (_stoneBrush == NoStones) return ' ';
                int level = levels[(x, y)];
                if (_uneven) level = GroundStones.Spread(x, y, GroundStones.Levels[level], null);
                return level < 0 ? (char?)null : GroundEdit.StoneCharFor(m.Region, m.Ground, _stoneBrush, level);
            });
        }

        /// <summary>Smooth, for stones: each tile takes the mean thickness of the nine round it, in the colour most of them have.</summary>
        private void SpreadStones(List<(int x, int y)> tiles)
        {
            var targets = new Dictionary<(int, int), (GroundEdit.StoneBrush brush, int level)?>();
            foreach ((int x, int y) in tiles)
            {
                float sum = 0f;
                var colours = new List<(GroundEdit.StoneBrush b, float share)>();
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        if (!(GroundAt(x + ox, y + oy) is TileGround g) || !(Model(g) is EditModel m) || !(GroundEdit.StoneAt(m.Ground, x + ox, y + oy) is StoneKind k)) continue;
                        sum += k.Share;
                        if (StoneBrushOf(m, k) is GroundEdit.StoneBrush b) colours.Add((b, k.Share));
                    }
                int level = GroundStones.Level(sum / 9f);
                GroundEdit.StoneBrush most = colours.GroupBy(c => c.b.Colour).OrderByDescending(gr => gr.Sum(c => c.share)).Select(gr => gr.First().b).FirstOrDefault();
                targets[(x, y)] = level < 0 || most == null ? ((GroundEdit.StoneBrush, int)?)null : (most, level);
            }
            EditStones(targets.Keys, (m, x, y) => targets[(x, y)] is (GroundEdit.StoneBrush b, int level) ? GroundEdit.StoneCharFor(m.Region, m.Ground, b, level) : ' ');
        }

        /// <summary>Sets the stones of tiles, each ground's model copied first so undo keeps the one before; <paramref name="charAt"/> null leaves a tile.</summary>
        private void EditStones(IEnumerable<(int x, int y)> tiles, System.Func<EditModel, int, int, char?> charAt)
        {
            foreach (var byGround in tiles.GroupBy(t => GroundAt(t.x, t.y)))
            {
                TileGround g = byGround.Key;
                if (g == null) continue;
                EditModel m = Model(g);
                if (m == null)
                {
                    Debug.LogWarning($"[R'th] Paint Ground: {g.gameObject.scene.name}'s ground doesn't match its data; run Sync World on it first.");
                    continue;
                }
                m = JsonConvert.DeserializeObject<EditModel>(JsonConvert.SerializeObject(m, Json), Json); // undo keeps the one before
                bool changed = false;
                foreach ((int x, int y) in byGround)
                    if (charAt(m, x, y) is char c) changed |= GroundEdit.SetStone(m.Ground, x, y, c);
                if (!changed) continue;
                GroundEdit.PruneStones(m.Ground);
                Apply(g, m, edited: true, redraw: false);
                _touched.Add(g);
                _stale.Add(g);
            }
            if (EditorApplication.timeSinceStartup - _redrawn > 0.15) Redraw();
        }

        /// <summary>The ground at a tile as a brush (its kind, colours and character), or null.</summary>
        private static GroundEdit.Brush BrushAt(int x, int y)
        {
            if (!(GroundAt(x, y) is TileGround g) || !(Model(g) is EditModel m) || !(GroundEdit.At(m.Ground, x, y) is char c)) return null;
            if (!m.Ground.Legend.TryGetValue(c.ToString(), out TileKind kind)) return null;
            GroundEdit.Brush b = GroundEdit.BrushOf(m.Region, kind);
            b.Letter = c;
            return b;
        }

        private void Redraw()
        {
            foreach (TileGround g in _stale)
            {
                if (g == null) continue;
                if (_heights.TryGetValue(g, out float[] heights))
                {
                    Undo.RecordObject(g, "Shape ground");
                    g.SetCorners((float[])heights.Clone()); // redraws
                    g.sculpted = true;
                    EditorUtility.SetDirty(g);
                    EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
                }
                else g.Rebuild();
            }
            _stale.Clear();
            _redrawn = EditorApplication.timeSinceStartup;
        }

        /// <summary>The patch of one ground around a tile, over region borders (at most 20 000 tiles).</summary>
        private static List<(int x, int y)> Flood(int x, int y)
        {
            var result = new List<(int, int)>();
            if (!(Kind(x, y) is (TileKind start, string colour))) return result;
            var seen = new HashSet<(int, int)> { (x, y) };
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue((x, y));
            while (queue.Count > 0 && result.Count < 20000)
            {
                var t = queue.Dequeue();
                result.Add(t);
                foreach (var n in new[] { (t.x + 1, t.y), (t.x - 1, t.y), (t.x, t.y + 1), (t.x, t.y - 1) })
                    if (seen.Add(n) && Kind(n.Item1, n.Item2) is (TileKind k, string c) && c == colour && GroundEdit.Same(k, start)) queue.Enqueue(n);
            }
            return result;
        }

        private static (TileKind kind, string colour)? Kind(int x, int y)
        {
            if (!(GroundAt(x, y) is TileGround g) || !(Model(g) is EditModel m) || !(GroundEdit.At(m.Ground, x, y) is char c)) return null;
            if (!m.Ground.Legend.TryGetValue(c.ToString(), out TileKind kind)) return null;
            return (kind, m.Materials.TryGetValue(kind.Material, out string colour) ? colour : "");
        }

        /// <summary>
        /// The open neighbours of what was painted blend into it: their rings of tiles are taken
        /// again from the open grounds (drawn now; their data is written when the scenes are saved).
        /// </summary>
        private static void RefreshRings(IEnumerable<TileGround> painted)
        {
            var open = TileGround.Active.Where(g => g != null).ToList();
            var models = open.Select(g => (g, m: Model(g))).Where(p => p.m != null).ToList();
            var world = new GroundEdit.WorldTiles(models.Select(p => new RegionData { Id = p.m.RegionId, Materials = p.m.Materials, Pieces = { p.m.Ground } }));
            foreach ((TileGround g, EditModel m) in models)
            {
                if (!painted.Any(p => Near(p, g))) continue;
                EditModel copy = JsonConvert.DeserializeObject<EditModel>(JsonConvert.SerializeObject(m, Json), Json);
                RegionData region = copy.Region;
                region.Pieces.Add(copy.Ground);
                if (!GroundEdit.RefreshSurround(region, world.At)) continue;
                copy.Materials = region.Materials;
                // A neighbour's ring is drawing, not hand work: kept in its scene, written to its data on saving.
                Apply(g, copy, edited: !string.IsNullOrEmpty(g.brushEdit));
                if (string.IsNullOrEmpty(g.brushEdit)) RingsChanged.Add(copy.RegionId);
            }
        }

        private static bool Near(TileGround a, TileGround b)
        {
            int m = Mathf.Max(b.margin, 2);
            return a.originX < b.originX + b.width + m && b.originX - m < a.originX + a.width &&
                   a.originY < b.originY + b.height + m && b.originY - m < a.originY + a.height;
        }

        // ------------------------------------------------------------------ saving

        /// <summary>Regions painted beside since the last save, whose rings of tiles their data must follow.</summary>
        private static readonly HashSet<string> RingsChanged = new HashSet<string>();

        /// <summary>
        /// Saving a painted ground (WorldSync.WriteBack): its tiles, kinds and ring become the
        /// region's ground. True if it had been painted.
        /// </summary>
        public static bool WriteBack(RegionData region, TileGround g)
        {
            if (string.IsNullOrEmpty(g.brushEdit)) return false;
            var m = JsonConvert.DeserializeObject<EditModel>(g.brushEdit, Json);
            Piece piece = GroundEdit.Tiles(region);
            if (piece == null) return false;
            piece.Rows = m.Ground.Rows;
            piece.Legend = new SortedDictionary<string, TileKind>(m.Ground.Legend, System.StringComparer.Ordinal);
            piece.Margin = m.Ground.Margin;
            piece.Surround = m.Ground.Surround;
            piece.Stones = m.Ground.Stones;
            piece.StoneLegend = m.Ground.StoneLegend != null ? new SortedDictionary<string, StoneKind>(m.Ground.StoneLegend, System.StringComparer.Ordinal) : null;
            foreach (KeyValuePair<string, string> c in m.Materials) region.Materials[c.Key] = c.Value;
            g.brushEdit = "";
            Cache.Remove(g);
            RingsChanged.Add(region.Id);
            return true;
        }

        /// <summary>The blockout ran after saving (a region was taken over) and took every ring from the world itself.</summary>
        internal static void RingsFollowed() => RingsChanged.Clear();

        /// <summary>
        /// After saving (<see cref="WorldSync.AfterSaving"/>): every region next to what was painted takes its ring of tiles from its
        /// neighbours' data, so the ground blends across its borders the same way when it is next
        /// drawn (open or not). The ring is drawing, so a generated region stays generated.
        /// </summary>
        internal static void FollowRings()
        {
            if (RingsChanged.Count == 0) return;
            string dir = Path.Combine(WorldSync.RepoRoot, "world", "regions");
            var regions = new List<RegionData>();
            var paths = new Dictionary<string, string>();
            foreach (string path in RegionFiles.RegionPaths(dir))
            {
                try
                {
                    RegionData r = RegionFiles.LoadRegion(path);
                    regions.Add(r);
                    paths[r.Id] = path;
                }
                catch (System.FormatException) { }
            }
            var near = new HashSet<string>();
            foreach (RegionData a in regions.Where(r => RingsChanged.Contains(r.Id)))
                foreach (RegionData b in regions)
                    if (b.OriginX <= a.OriginX + a.Width + 2 && a.OriginX <= b.OriginX + b.Width + 2 &&
                        b.OriginY <= a.OriginY + a.Height + 2 && a.OriginY <= b.OriginY + b.Height + 2) near.Add(b.Id);
            RingsChanged.Clear();
            List<string> changed = GroundEdit.RefreshSurrounds(regions, near);
            foreach (string id in changed) RegionFiles.SaveRegion(regions.First(r => r.Id == id), paths[id]);
            if (changed.Count > 0)
                Debug.Log($"[R'th] Paint Ground: the rings of {changed.Count} neighbouring regions follow the new ground ({string.Join(", ", changed)}); " +
                          "open ones are already drawn, the rest at the next Sync World.");
        }
    }
}
