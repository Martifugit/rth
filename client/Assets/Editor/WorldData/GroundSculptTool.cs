using Rth.Shared.World;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Sculpts a region's ground in the Scene view (docs/world-guide.md section 4): select the
    /// region's tile ground (Data > ground) and pick this tool. Drag to raise, lower, flatten to the
    /// height where the stroke started, or smooth, within a soft round brush. Tiles too steep to walk
    /// (cliffs, which block) are shown red around the brush. Saving the scene writes the heights to
    /// the region's data (world/regions/&lt;id&gt;.json); the next Sync World stands the region's trees,
    /// rocks and props on the new ground.
    /// </summary>
    [EditorTool("Sculpt Ground", typeof(TileGround))]
    public sealed class GroundSculptTool : EditorTool
    {
        private enum Mode { Raise, Lower, Flatten, Smooth }

        private static Mode _mode = Mode.Raise;
        private static float _radius = 4f;
        private static float _strength = 0.5f;

        private float _flattenTo;
        private int _undoGroup;
        private Vector3 _brush;
        private bool _hasBrush;

        public override GUIContent toolbarIcon => new GUIContent("Sculpt", "Sculpt Ground: raise, lower, flatten or smooth the region's terrain");

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView) || !(target is TileGround ground) || EditorApplication.isPlaying) return;
            Event e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);

            _hasBrush = Pick(ground, HandleUtility.GUIPointToWorldRay(e.mousePosition), out _brush);
            if (_hasBrush)
            {
                DrawSteep(ground);
                Handles.color = _mode == Mode.Lower ? new Color(1f, 0.5f, 0.3f) : new Color(0.4f, 0.9f, 1f);
                Handles.DrawWireDisc(_brush, Vector3.up, _radius);
                Handles.DrawWireDisc(_brush, Vector3.up, _radius * 0.5f);
            }

            bool painting = e.button == 0 && !e.alt;
            if (e.type == EventType.MouseDown && painting && _hasBrush)
            {
                _undoGroup = Undo.GetCurrentGroup();
                _flattenTo = ground.Field.Sample(_brush.x, _brush.z);
                Stroke(ground);
                GUIUtility.hotControl = control;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && painting && GUIUtility.hotControl == control && _hasBrush)
            {
                Stroke(ground);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == control)
            {
                Undo.CollapseUndoOperations(_undoGroup);
                GUIUtility.hotControl = 0;
                e.Use();
            }
            else if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket))
            {
                _radius = Mathf.Clamp(_radius + (e.keyCode == KeyCode.RightBracket ? 1f : -1f), 1f, 16f);
                e.Use();
            }
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) window.Repaint();

            Panel(ground);
        }

        private static void Panel(TileGround ground)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 230, 150), "Sculpt Ground", GUI.skin.window);
            _mode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "Raise", "Lower", "Flatten", "Smooth" });
            GUILayout.Label($"Radius {_radius:0} tiles  ([ and ])");
            _radius = Mathf.Round(GUILayout.HorizontalSlider(_radius, 1f, 16f));
            GUILayout.Label($"Strength {_strength:0.00}");
            _strength = GUILayout.HorizontalSlider(_strength, 0.05f, 1f);
            GUILayout.Label(ground.sculpted ? "Changed: save the scene to keep it." : "Red tiles are too steep to walk.");
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        /// <summary>Where the mouse ray meets the ground: a few steps between the ray and the heights.</summary>
        private static bool Pick(TileGround ground, Ray ray, out Vector3 point)
        {
            Heightfield f = ground.Field;
            point = default;
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;
            float y = f.Sample(f.OriginX + f.Width / 2f, f.OriginY + f.Depth / 2f);
            for (int i = 0; i < 6; i++)
            {
                float t = (y - ray.origin.y) / ray.direction.y;
                if (t < 0) return false;
                point = ray.origin + ray.direction * t;
                y = f.Sample(point.x, point.z);
            }
            point.y = y;
            return f.Contains(point.x, point.z);
        }

        private void Stroke(TileGround ground)
        {
            Undo.RecordObject(ground, "Sculpt ground");
            Heightfield f = ground.Field;
            float[] before = f.Corners;
            float[] after = f.Corners;
            int w = ground.width;
            float H(int i, int j) => before[Mathf.Clamp(j, 0, ground.height) * (w + 1) + Mathf.Clamp(i, 0, w)];
            int r = Mathf.CeilToInt(_radius);
            int ci = Mathf.RoundToInt(_brush.x) - ground.originX, cj = Mathf.RoundToInt(_brush.z) - ground.originY;
            for (int j = Mathf.Max(0, cj - r); j <= Mathf.Min(ground.height, cj + r); j++)
                for (int i = Mathf.Max(0, ci - r); i <= Mathf.Min(w, ci + r); i++)
                {
                    float d = Vector2.Distance(new Vector2(ground.originX + i, ground.originY + j), new Vector2(_brush.x, _brush.z));
                    if (d > _radius) continue;
                    float weight = Mathf.SmoothStep(0f, 1f, 1f - d / _radius) * _strength;
                    float h = H(i, j);
                    switch (_mode)
                    {
                        case Mode.Raise: h += 0.15f * weight; break;
                        case Mode.Lower: h -= 0.15f * weight; break;
                        case Mode.Flatten: h += (_flattenTo - h) * weight * 0.5f; break;
                        case Mode.Smooth: h += ((H(i - 1, j) + H(i + 1, j) + H(i, j - 1) + H(i, j + 1)) / 4f - h) * weight * 0.5f; break;
                    }
                    after[j * (w + 1) + i] = Mathf.Clamp(h, -4f, 16f); // the whole world stays within about 16 tiles (world-scale.md 3)
                }
            ground.SetCorners(after);
            ground.sculpted = true;
            EditorUtility.SetDirty(ground);
            EditorSceneManager.MarkSceneDirty(ground.gameObject.scene);
        }

        /// <summary>Tiles too steep to walk near the brush, outlined in red.</summary>
        private void DrawSteep(TileGround ground)
        {
            Heightfield f = ground.Field;
            int r = Mathf.CeilToInt(_radius * 3f);
            int cx = Mathf.FloorToInt(_brush.x), cy = Mathf.FloorToInt(_brush.z);
            var quad = new Vector3[4];
            for (int y = Mathf.Max(f.OriginY, cy - r); y < Mathf.Min(f.OriginY + f.Depth, cy + r); y++)
                for (int x = Mathf.Max(f.OriginX, cx - r); x < Mathf.Min(f.OriginX + f.Width, cx + r); x++)
                {
                    if (!f.IsSteep(x, y, ground.cliffStep)) continue;
                    quad[0] = new Vector3(x, f.Corner(x, y) + 0.05f, y);
                    quad[1] = new Vector3(x, f.Corner(x, y + 1) + 0.05f, y + 1);
                    quad[2] = new Vector3(x + 1, f.Corner(x + 1, y + 1) + 0.05f, y + 1);
                    quad[3] = new Vector3(x + 1, f.Corner(x + 1, y) + 0.05f, y);
                    Handles.DrawSolidRectangleWithOutline(quad, new Color(1f, 0.15f, 0.1f, 0.35f), new Color(1f, 0.15f, 0.1f, 0.8f));
                }
        }
    }
}
