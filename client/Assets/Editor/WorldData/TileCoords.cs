using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// The tile under the mouse, in the Scene view's bottom right corner: the ground (or Edit
    /// Terrain's terrain) under the cursor, else the flat plane at height 0. A tile (x, y) covers
    /// world X [x, x+1] and Z [y, y+1], so it is the hit point's X and Z rounded down.
    /// </summary>
    [InitializeOnLoad]
    public static class TileCoords
    {
        private static GUIStyle _style;

        static TileCoords()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView view)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                view.Repaint();
                return;
            }
            if (e.type != EventType.Repaint) return;
            float ppp = EditorGUIUtility.pixelsPerPoint;
            Rect area = new Rect(0, 0, view.camera.pixelWidth / ppp, view.camera.pixelHeight / ppp);
            if (!area.Contains(e.mousePosition)) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 point;
            if (Physics.Raycast(ray, out RaycastHit hit, 100000f, ~0, QueryTriggerInteraction.Ignore)) point = hit.point;
            else if (Mathf.Abs(ray.direction.y) > 1e-5f && -ray.origin.y / ray.direction.y > 0) point = ray.GetPoint(-ray.origin.y / ray.direction.y);
            else return;

            string text = $"({Mathf.FloorToInt(point.x)}, {Mathf.FloorToInt(point.z)})";
            _style ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 13,
                normal = { textColor = Color.white },
            };
            Vector2 size = _style.CalcSize(new GUIContent(text));
            var box = new Rect(area.width - size.x - 22, area.height - size.y - 16, size.x + 12, size.y + 6);
            Handles.BeginGUI();
            EditorGUI.DrawRect(box, new Color(0f, 0f, 0f, 0.6f));
            GUI.Label(new Rect(box.x, box.y, box.width - 6, box.height), text, _style);
            Handles.EndGUI();
        }
    }
}
