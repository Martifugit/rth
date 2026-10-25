using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rth.Shared.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rth.MapTools.Editor
{
    /// <summary>
    /// Opens the world's region scenes side by side in the Editor, so borders can be checked and
    /// edited in place. Regions sit at their absolute tile positions, so opening them additively is
    /// all it takes. The set of regions is the exported one (server/data/maps/*.region.json), the
    /// same list the server loads. Each scene still saves to its own file.
    /// </summary>
    public static class WorldScenes
    {
        private const string GameScene = "Game";

        [System.Serializable]
        private sealed class RegionInfo
        {
            public string scene;
        }

        /// <summary>Above this many scenes the Editor gets slow; Open Whole World asks first.</summary>
        private const int ManyScenes = 24;

        [MenuItem("R'th/World/Open Regions Around This One", priority = 19)]
        private static void OpenAround()
        {
            // The active scene's region, or failing that the Scene view's centre; one region (96 tiles) around it.
            MapArea here = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if ((here = root.GetComponentInChildren<MapArea>(true)) != null) break;
            RectInt around;
            if (here != null) around = new RectInt(here.origin.x - 96, here.origin.y - 96, here.size.x + 192, here.size.y + 192);
            else if (SceneView.lastActiveSceneView != null)
            {
                Vector3 p = SceneView.lastActiveSceneView.pivot;
                around = new RectInt(Mathf.FloorToInt(p.x) - 144, Mathf.FloorToInt(p.z) - 144, 288, 288);
            }
            else
            {
                Debug.LogWarning("[R'th] Open a region scene first.");
                return;
            }
            Open(Regions().Where(r => r.Rect.Overlaps(around)).Select(r => r.Scene).ToList());
        }

        /// <summary>
        /// Opens every region of the area the active scene's region belongs to (world/areas.json,
        /// docs/world-areas.md): the unit the world is built in, so an area's work is done with all
        /// of it in view.
        /// </summary>
        [MenuItem("R'th/World/Open This Area", priority = 18)]
        private static void OpenArea()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string file = Rth.Shared.World.Data.WorldAreas.PathIn(root);
            if (!File.Exists(file))
            {
                Debug.LogWarning("[R'th] No world/areas.json in this checkout.");
                return;
            }
            Rth.Shared.World.Data.WorldAreas areas = Rth.Shared.World.Data.WorldAreas.Load(file);
            Dictionary<string, string> sceneOf = RegionScenes();
            string active = SceneManager.GetActiveScene().name;
            string region = sceneOf.FirstOrDefault(e => e.Value == active).Key;
            Rth.Shared.World.Data.WorldAreas.Area area = region == null ? null : areas.AreaOf(region);
            if (area == null)
            {
                Debug.LogWarning($"[R'th] The active scene ('{active}') isn't a region in any area: open one of an area's regions first.");
                return;
            }
            Open(area.Regions.Where(sceneOf.ContainsKey).Select(r => sceneOf[r]).ToList());
            Rth.Shared.World.Data.WorldAreas.Package next = areas.Next(area);
            Debug.Log($"[R'th] {area.Name}: stage {areas.StageOf(area)}" + (next != null ? $", next {next.Id} {next.Name}: {next.Done}" : ", finished."));
        }

        /// <summary>Each exported region's scene name, by region id (the map files are named after the region).</summary>
        private static Dictionary<string, string> RegionScenes()
        {
            var scenes = new Dictionary<string, string>();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "server", "data", "maps"));
            if (!Directory.Exists(dir)) return scenes;
            foreach (string file in Directory.GetFiles(dir, "*.region.json"))
            {
                RegionInfo info = JsonUtility.FromJson<RegionInfo>(File.ReadAllText(file));
                if (info == null || string.IsNullOrEmpty(info.scene)) continue;
                string id = Path.GetFileName(file);
                scenes[id.Substring(0, id.Length - ".region.json".Length)] = info.scene;
            }
            return scenes;
        }

        [MenuItem("R'th/World/Open Whole World", priority = 20)]
        private static void OpenWholeWorld()
        {
            List<string> scenes = Regions().Select(r => r.Scene).ToList();
            if (scenes.Count > ManyScenes && !EditorUtility.DisplayDialog("Open Whole World",
                    $"This opens {scenes.Count} region scenes at once, which makes the Editor slow. " +
                    "Open Regions Around This One opens only the neighbours of the scene you're in.", "Open all", "Cancel"))
                return;
            Open(scenes);
        }

        private static void Open(List<string> names)
        {
            Scene keepActive = SceneManager.GetActiveScene();
            Dictionary<string, string> paths = ScenePaths();
            var opened = new List<Scene>();
            foreach (string name in names)
            {
                if (!paths.TryGetValue(name, out string path))
                {
                    Debug.LogWarning($"[R'th] Region scene '{name}' was exported but can't be found in the project.");
                    continue;
                }
                opened.Add(Open(path));
            }
            // The Game scene's sun and fog make regions look as they will in play.
            if (paths.TryGetValue(GameScene, out string game)) Open(game);

            // New objects go into the active scene, so keep that a region, never the Game scene.
            if (keepActive.name == GameScene || !keepActive.IsValid())
                foreach (Scene s in opened)
                    if (s.IsValid()) { SceneManager.SetActiveScene(s); break; }
            Debug.Log($"[R'th] Opened {opened.Count} region scenes. Edits save to each region's own scene.");
        }

        [MenuItem("R'th/World/Close Other Scenes", priority = 21)]
        private static void CloseOthers()
        {
            Scene active = SceneManager.GetActiveScene();
            var others = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s != active) others.Add(s);
            }
            if (!EditorSceneManager.SaveModifiedScenesIfUserWantsTo(others.ToArray())) return;
            foreach (Scene s in others) EditorSceneManager.CloseScene(s, true);
        }

        [MenuItem("R'th/World/Open This Area", true)]
        [MenuItem("R'th/World/Open Regions Around This One", true)]
        [MenuItem("R'th/World/Open Whole World", true)]
        [MenuItem("R'th/World/Close Other Scenes", true)]
        private static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static Scene Open(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded) return scene;
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        /// <summary>Every exported region: its scene's name and its rectangle (from its map's header).</summary>
        private static IEnumerable<(string Scene, RectInt Rect)> Regions()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "server", "data", "maps"));
            if (!Directory.Exists(dir)) yield break;
            var seen = new HashSet<string>();
            foreach (string file in Directory.GetFiles(dir, "*.region.json"))
            {
                RegionInfo info = JsonUtility.FromJson<RegionInfo>(File.ReadAllText(file));
                if (info == null || string.IsNullOrEmpty(info.scene) || !seen.Add(info.scene)) continue;
                string map = file.Substring(0, file.Length - ".region.json".Length) + ".rthmap";
                if (!File.Exists(map)) continue;
                MapData m = MapData.Load(map);
                yield return (info.scene, new RectInt(m.OriginX, m.OriginY, m.Width, m.Height));
            }
        }

        /// <summary>Every scene in the project by name (one search, not one per region).</summary>
        private static Dictionary<string, string> ScenePaths()
        {
            var paths = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!paths.ContainsKey(name)) paths[name] = path;
            }
            return paths;
        }
    }
}
