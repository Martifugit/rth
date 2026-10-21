using Rth.Shared.World;
using UnityEditor;
using UnityEngine;
using CollisionFlags = Rth.Shared.World.CollisionFlags; // UnityEngine has its own CollisionFlags

namespace Rth.MapTools.Editor
{
    /// <summary>
    /// Draws the last exported collision map of each region in the Scene view: red tiles are
    /// blocked, yellow lines are walls. Use it to check the export matches what the scene looks like.
    /// </summary>
    [InitializeOnLoad]
    public static class CollisionPreview
    {
        private const string PrefKey = "Rth.CollisionPreview";
        private const string LevelKey = "Rth.CollisionPreviewLevel";
        // One per region (keyed by origin), so a whole world opened side by side previews in full.
        private static readonly System.Collections.Generic.Dictionary<(int, int), (MapData Map, int Plane)> _maps =
            new System.Collections.Generic.Dictionary<(int, int), (MapData, int)>();

        static CollisionPreview()
        {
            SceneView.duringSceneGui += Draw;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem("R'th/Show Collision Preview", priority = 2)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            SceneView.RepaintAll();
        }

        [MenuItem("R'th/Show Collision Preview", true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked("R'th/Show Collision Preview", Enabled);
            return true;
        }

        /// <summary>The floor the preview draws (cycle it with the menu); a region with fewer floors shows its top one.</summary>
        private static int Level
        {
            get => EditorPrefs.GetInt(LevelKey, 0);
            set => EditorPrefs.SetInt(LevelKey, value);
        }

        [MenuItem("R'th/Show Next Collision Level", priority = 3)]
        private static void NextLevel()
        {
            Level = (Level + 1) % (MapLevel.MaxLevel + 1);
            Debug.Log("[R'th] Collision preview shows level " + Level + ".");
            SceneView.RepaintAll();
        }

        public static void Show(MapData map, int plane)
        {
            _maps[(map.OriginX, map.OriginY)] = (map, plane);
            SceneView.RepaintAll();
        }

        private static void Draw(SceneView view)
        {
            if (!Enabled || Event.current.type != EventType.Repaint) return;
            // The chosen level, or the region's own plane when that is the higher one.
            foreach ((MapData map, int plane) in _maps.Values) Draw(map, Mathf.Min(Mathf.Max(plane, Level), map.Planes - 1));
        }

        private static void Draw(MapData map, int plane)
        {
            const float h = 0.03f;
            var blocked = new Color(1f, 0.1f, 0.1f, 0.35f);
            for (int y = map.OriginY; y < map.OriginY + map.Height; y++)
            {
                for (int x = map.OriginX; x < map.OriginX + map.Width; x++)
                {
                    CollisionFlags f = map.GetFlags(plane, x, y);
                    if ((f & CollisionFlags.Blocked) != 0)
                    {
                        Handles.DrawSolidRectangleWithOutline(new[]
                        {
                            new Vector3(x, h, y), new Vector3(x + 1, h, y), new Vector3(x + 1, h, y + 1), new Vector3(x, h, y + 1),
                        }, blocked, Color.clear);
                    }
                    Handles.color = Color.yellow;
                    if ((f & CollisionFlags.WallNorth) != 0) Handles.DrawAAPolyLine(4f, new Vector3(x, h, y + 1), new Vector3(x + 1, h, y + 1));
                    if ((f & CollisionFlags.WallEast) != 0) Handles.DrawAAPolyLine(4f, new Vector3(x + 1, h, y), new Vector3(x + 1, h, y + 1));
                }
            }

            Handles.color = Color.cyan;
            Handles.DrawWireCube(new Vector3(map.Spawn.X + 0.5f, 0.5f, map.Spawn.Y + 0.5f), Vector3.one);
        }
    }
}
