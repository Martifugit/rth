using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rth.MapTools;
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
    /// Paints the ground in the Scene view (docs/world-guide.md section 4): roads, paths, grass,
    /// sand, water, tile by tile, over every open region at once. Pick a ground from the list (the
    /// layout's grounds, and any a region has of its own), then paint with a round brush, draw a
    /// straight stroke (a road: straight or at 45 degrees), fill a patch, or pick up the ground under
    /// the cursor (Ctrl+click). What you paint is drawn at once the way the game draws it: blended
    /// ground, crisp roads and water with their diagonal edges, and the neighbouring regions blend
    /// into it. Saving the scenes (Ctrl+S) writes the tiles to the regions' data (which takes a
    /// generated region over) and the neighbours' rings of tiles to theirs.
    /// R'th > World > Paint Ground (G) starts it from anywhere.
    /// </summary>
    [EditorTool("Paint Ground", typeof(TileGround))]
    public sealed class GroundPaintTool : EditorTool
    {
        private enum Mode { Brush, Line, Fill, Pick }

        private static Mode _mode = Mode.Brush;
        private static int _radius = 1;
        private static int _width = 2;
        private static bool _snap = true;
        private static GroundEdit.Brush _brush;
        private static List<GroundEdit.Brush> _brushes;
        private static Vector2 _scroll;
        private static string _filter = "";

        private (int x, int y)? _hover, _lineStart, _last;
        private int _undoGroup;
        private readonly HashSet<TileGround> _touched = new HashSet<TileGround>();
        /// <summary>Painted but not yet redrawn: a redraw takes a tenth of a second, so a drag redraws a few times a second.</summary>
        private readonly HashSet<TileGround> _stale = new HashSet<TileGround>();
        private double _redrawn;

        public override GUIContent toolbarIcon => new GUIContent("Paint", "Paint Ground: roads, paths and ground, tile by tile");

        [MenuItem("R'th/World/Paint Ground _g", priority = 7)]
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
                EditorUtility.DisplayDialog("Paint Ground", "Open a region with tile ground first (R'th > World > Open This Area).", "OK");
                return;
            }
            // A component's tools are offered once the selection has caught up.
            Selection.activeGameObject = ground.gameObject;
            EditorApplication.delayCall += () => ToolManager.SetActiveTool<GroundPaintTool>();
        }

        [MenuItem("R'th/World/Paint Ground _g", true)]
        private static bool BeginValidate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ------------------------------------------------------------------ the Scene view

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView) || EditorApplication.isPlaying) return;
            Event e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
            if (_brushes == null) LoadBrushes();

            Rect panel = new Rect(10, 10, 270, 470);
            bool overPanel = panel.Contains(e.mousePosition);
            _hover = overPanel ? null : Pick(HandleUtility.GUIPointToWorldRay(e.mousePosition));
            if (_hover is (int hx, int hy)) DrawTiles(Footprint(hx, hy), _brush);

            bool left = e.button == 0 && !e.alt && !overPanel;
            if (e.type == EventType.MouseDown && left && _hover is (int x, int y))
            {
                if (e.control || _mode == Mode.Pick) PickUp(x, y);
                else if (_brush != null)
                {
                    _undoGroup = Undo.GetCurrentGroup();
                    _touched.Clear();
                    if (_mode == Mode.Line) _lineStart = (x, y);
                    else if (_mode == Mode.Fill) { Paint(Flood(x, y)); EndStroke(); }
                    else { Paint(Footprint(x, y)); _last = (x, y); }
                    GUIUtility.hotControl = control;
                }
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == control && _hover is (int dx, int dy))
            {
                if (_mode == Mode.Brush && _last is (int lx, int ly) && (lx, ly) != (dx, dy))
                {
                    // A fast drag skips tiles: paint the run between, as wide as the brush.
                    Paint(GroundEdit.Line(lx, ly, dx, dy, 2 * _radius + 1));
                    _last = (dx, dy);
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == control)
            {
                if (_mode == Mode.Line && _lineStart is (int sx, int sy) && _hover is (int ux, int uy)) Paint(Stroke(sx, sy, ux, uy));
                EndStroke();
                GUIUtility.hotControl = 0;
                e.Use();
            }
            else if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket))
            {
                int step = e.keyCode == KeyCode.RightBracket ? 1 : -1;
                if (_mode == Mode.Line) _width = Mathf.Clamp(_width + step, 1, 8);
                else _radius = Mathf.Clamp(_radius + step, 0, 8);
                e.Use();
            }
            if (_mode == Mode.Line && _lineStart is (int ax, int ay) && GUIUtility.hotControl == control && _hover is (int bx, int by))
                DrawTiles(Stroke(ax, ay, bx, by), _brush);
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) window.Repaint();

            Panel(panel);
        }

        /// <summary>The tiles the brush covers at a tile, as the mode draws them.</summary>
        private static IEnumerable<(int x, int y)> Footprint(int x, int y) =>
            _mode == Mode.Brush ? GroundEdit.Disc(x, y, _radius) : new[] { (x, y) };

        private static HashSet<(int x, int y)> Stroke(int ax, int ay, int bx, int by)
        {
            (int ex, int ey) = _snap ? GroundEdit.Snap45(ax, ay, bx, by) : (bx, by);
            return GroundEdit.Line(ax, ay, ex, ey, _width);
        }

        private void EndStroke()
        {
            _lineStart = null;
            _last = null;
            Redraw();
            if (_touched.Count == 0) return;
            RefreshRings(_touched);
            Undo.CollapseUndoOperations(_undoGroup);
            _touched.Clear();
        }

        private static void Panel(Rect area)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(area, "Paint Ground", GUI.skin.window);
            _mode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "Brush", "Line", "Fill", "Pick" });
            if (_mode == Mode.Brush)
            {
                GUILayout.Label(_radius == 0 ? "One tile  ([ and ])" : $"Radius {_radius} tiles  ([ and ])");
                _radius = Mathf.RoundToInt(GUILayout.HorizontalSlider(_radius, 0, 8));
            }
            else if (_mode == Mode.Line)
            {
                GUILayout.Label($"{_width} tiles wide  ([ and ]); drag from end to end");
                _width = Mathf.RoundToInt(GUILayout.HorizontalSlider(_width, 1, 8));
                _snap = GUILayout.Toggle(_snap, "Straight or 45 degrees");
            }
            else if (_mode == Mode.Fill) GUILayout.Label("Click: the patch of one ground under it");
            else GUILayout.Label("Click: take the ground under it");

            GUILayout.BeginHorizontal();
            Swatch(_brush, 18);
            GUILayout.Label(_brush != null ? _brush.Name : "Pick a ground below", EditorStyles.boldLabel);
            GUILayout.EndHorizontal();
            _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField);
            _scroll = GUILayout.BeginScrollView(_scroll);
            string group = null;
            foreach (GroundEdit.Brush b in _brushes ?? new List<GroundEdit.Brush>())
            {
                if (_filter.Length > 0 && b.Name.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (b.Group != group)
                {
                    group = b.Group;
                    GUILayout.Label(group, EditorStyles.miniBoldLabel);
                }
                GUILayout.BeginHorizontal();
                Swatch(b, 14);
                GUIStyle style = b == _brush ? EditorStyles.boldLabel : EditorStyles.label;
                if (GUILayout.Button(b.Name, style)) _brush = b;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Label(TileGround.Active.Any(g => g != null && !string.IsNullOrEmpty(g.brushEdit))
                ? "Changed: save the scenes (Ctrl+S) to keep it." : "Ctrl+click: take the ground under the cursor.", EditorStyles.miniLabel);
            if (GUILayout.Button("Reload the grounds", EditorStyles.miniButton)) LoadBrushes();
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static void Swatch(GroundEdit.Brush b, int size)
        {
            Rect r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            r.y += 2;
            EditorGUI.DrawRect(r, Colour(b));
        }

        private static Color Colour(GroundEdit.Brush b) =>
            b != null && Palette.TryParse(b.Colour, out Color c) ? c : new Color(0.5f, 0.5f, 0.5f, 0.4f);

        /// <summary>Outlines tiles at the ground's height, in the brush's colour.</summary>
        private static void DrawTiles(IEnumerable<(int x, int y)> tiles, GroundEdit.Brush brush)
        {
            Color c = Colour(brush);
            Color fill = new Color(c.r, c.g, c.b, 0.55f), line = Color.Lerp(c, Color.white, 0.5f);
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

        // ------------------------------------------------------------------ finding the ground

        private static TileGround GroundAt(int x, int y)
        {
            foreach (TileGround g in TileGround.Active)
                if (g != null && x >= g.originX && y >= g.originY && x < g.originX + g.width && y < g.originY + g.height) return g;
            return null;
        }

        /// <summary>The tile under the mouse: a few steps between the ray and the ground's heights.</summary>
        private static (int x, int y)? Pick(Ray ray)
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
            if (!any) return null;
            int tx = Mathf.FloorToInt(p.x), ty = Mathf.FloorToInt(p.z);
            return GroundAt(tx, ty) != null ? (tx, ty) : ((int, int)?)null;
        }

        // ------------------------------------------------------------------ the grounds to paint with

        private static void LoadBrushes()
        {
            var brushes = new List<GroundEdit.Brush>();
            string continent = Path.Combine(WorldSync.RepoRoot, "world", "layout", "continent.json");
            try
            {
                if (File.Exists(continent)) brushes.AddRange(GroundEdit.Brushes(ContinentData.Parse(File.ReadAllText(continent))));
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
            }
            string[] order = { "Ground", "Surfaces", "Water", "Blocked" };
            _brushes = brushes.OrderBy(b => System.Array.IndexOf(order, b.Group)).ThenBy(b => b.Name, System.StringComparer.OrdinalIgnoreCase).ToList();
            if (_brush != null) _brush = _brushes.FirstOrDefault(b => b.Id == _brush.Id && GroundEdit.Same(b.Kind, _brush.Kind)) ?? _brush;
        }

        /// <summary>Ctrl+click: the ground under the cursor becomes the brush.</summary>
        private static void PickUp(int x, int y)
        {
            if (!(GroundAt(x, y) is TileGround g) || !(Model(g) is EditModel m) || !(GroundEdit.At(m.Ground, x, y) is char c)) return;
            if (!m.Ground.Legend.TryGetValue(c.ToString(), out TileKind kind)) return;
            string colour = m.Materials.TryGetValue(kind.Material, out string v) ? v : "";
            _brush = _brushes.FirstOrDefault(b => GroundEdit.Same(b.Kind, kind) && b.Colour == colour);
            if (_brush == null)
            {
                _brush = GroundEdit.BrushOf(m.Region, kind);
                _brushes.Add(_brush);
            }
            if (_mode == Mode.Pick) _mode = Mode.Brush;
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
            public string[] Rows, Surround;
            public EditModel Model;

            public bool Fresh(TileGround g) => ReferenceEquals(Edit, g.brushEdit) && ReferenceEquals(Rows, g.rows) && ReferenceEquals(Surround, g.surround) && Kinds == g.kinds;
        }

        private static readonly Dictionary<TileGround, Entry> Cache = new Dictionary<TileGround, Entry>();

        private static void Remember(TileGround g, EditModel m) =>
            Cache[g] = new Entry { Edit = g.brushEdit, Kinds = g.kinds, Rows = g.rows, Surround = g.surround, Model = m };
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

        private void Paint(IEnumerable<(int x, int y)> tiles)
        {
            if (_brush == null) return;
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
                bool ring = GroundEdit.EnsureRing(m.Ground);
                char c = GroundEdit.CharFor(m.Region, m.Ground, _brush.Kind, _brush.Materials, _brush.Letter);
                bool changed = ring;
                foreach ((int x, int y) in byGround) changed |= GroundEdit.Set(m.Ground, x, y, c);
                if (!changed) continue;
                GroundEdit.Prune(m.Ground);
                Apply(g, m, edited: true, redraw: false);
                _touched.Add(g);
                _stale.Add(g);
            }
            if (EditorApplication.timeSinceStartup - _redrawn > 0.15) Redraw();
        }

        private void Redraw()
        {
            foreach (TileGround g in _stale)
                if (g != null) g.Rebuild();
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
