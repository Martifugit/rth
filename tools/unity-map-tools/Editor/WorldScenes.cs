using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    /// The commands work on the selected scene: a scene header picked in the Hierarchy, else the
    /// scene of the selected object, else the active one. They sit at the top of the R'th menu with
    /// Alt+Shift shortcuts (rebindable under Edit > Shortcuts, "Main Menu/R'th") and on a scene
    /// header's right-click menu in the Hierarchy.
    /// </summary>
    [InitializeOnLoad]
    public static class WorldScenes
    {
        private const string GameScene = "Game";

        private const string MenuArea = "R'th/Open Selected Area &#o";
        private const string MenuAll = "R'th/Open All Scenes &#w";
        private const string MenuClose = "R'th/Close Other Scenes &#x";
        private const string MenuAround = "R'th/Open Regions Around Selected";

        [System.Serializable]
        private sealed class RegionInfo
        {
            public string scene;
        }

        /// <summary>Above this many scenes the Editor gets slow; Open All Scenes asks first.</summary>
        private const int ManyScenes = 24;

        static WorldScenes()
        {
            SceneHierarchyHooks.addItemsToSceneHeaderContextMenu += (menu, scene) =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var picked = new List<Scene> { scene };
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("R'th: Open This Area"), false, () => OpenArea(picked));
                menu.AddItem(new GUIContent("R'th: Open Regions Around This One"), false, () => OpenAround(picked));
                menu.AddItem(new GUIContent("R'th: Close Other Scenes"), false, () => CloseOthers(picked));
                menu.AddItem(new GUIContent("R'th: Open All Scenes"), false, OpenAll);
            };
        }

        /// <summary>
        /// The scenes the user means: the scene headers selected in the Hierarchy, else the scene
        /// of the selected object, else the active scene. The Hierarchy keeps its scene selection
        /// to itself, so it is read by reflection (and skipped if Unity renames it).
        /// </summary>
        private static List<Scene> SelectedScenes()
        {
            var scenes = new List<Scene>();
            try
            {
                const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                System.Type window = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
                object last = window?.GetProperty("lastInteractedHierarchyWindow", any)?.GetValue(null);
                // Unset until the Hierarchy is clicked after a script reload: any open one will do.
                if (last == null && window != null) last = Resources.FindObjectsOfTypeAll(window).FirstOrDefault();
                MethodInfo get = window?.GetMethod("GetSelectedScenes", any, null, new[] { typeof(List<Scene>) }, null);
                if (last != null && get != null) get.Invoke(last, new object[] { scenes });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[R'th] Couldn't read the Hierarchy's selected scenes ({e.Message}); using the selected object's scene.");
                scenes.Clear();
            }
            scenes.RemoveAll(s => !s.IsValid() || !s.isLoaded);
            if (scenes.Count == 0 && Selection.activeGameObject != null && Selection.activeGameObject.scene.IsValid())
                scenes.Add(Selection.activeGameObject.scene);
            if (scenes.Count == 0) scenes.Add(SceneManager.GetActiveScene());
            return scenes;
        }

        /// <summary>
        /// Opens every region of the area the selected scene's region belongs to (world/areas.json,
        /// docs/world-areas.md): the unit the world is built in, so an area's work is done with all
        /// of it in view. Several selected scenes open each of their areas.
        /// </summary>
        [MenuItem(MenuArea, priority = -20)]
        private static void OpenArea() => OpenArea(SelectedScenes());

        private static void OpenArea(List<Scene> selected)
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
            var found = new List<Rth.Shared.World.Data.WorldAreas.Area>();
            foreach (Scene scene in selected)
            {
                string region = sceneOf.FirstOrDefault(e => e.Value == scene.name).Key;
                Rth.Shared.World.Data.WorldAreas.Area area = region == null ? null : areas.AreaOf(region);
                if (area == null)
                    Debug.LogWarning(region == null
                        ? $"[R'th] '{scene.name}' isn't an exported region scene (no server/data/maps/*.region.json names it)."
                        : $"[R'th] Region '{region}' ('{scene.name}') isn't in any area of world/areas.json.");
                else if (!found.Contains(area)) found.Add(area);
            }
            if (found.Count == 0) return;
            Open(found.SelectMany(a => a.Regions).Where(sceneOf.ContainsKey).Select(r => sceneOf[r]).Distinct().ToList(), selected);
            foreach (Rth.Shared.World.Data.WorldAreas.Area area in found)
            {
                Rth.Shared.World.Data.WorldAreas.Package next = areas.Next(area);
                Debug.Log($"[R'th] {area.Name}: stage {areas.StageOf(area)}" + (next != null ? $", next {next.Id} {next.Name}: {next.Done}" : ", finished."));
            }
        }

        [MenuItem(MenuAll, priority = -19)]
        private static void OpenAll()
        {
            List<string> scenes = Regions().Select(r => r.Scene).ToList();
            if (scenes.Count > ManyScenes && !EditorUtility.DisplayDialog("Open All Scenes",
                    $"This opens {scenes.Count} region scenes at once, which makes the Editor slow. " +
                    "Open Selected Area opens only the area of the scene you've selected.", "Open all", "Cancel"))
                return;
            Open(scenes, SelectedScenes());
        }

        /// <summary>Closes every scene but the selected ones (asking to save changes first); the first of them becomes active.</summary>
        [MenuItem(MenuClose, priority = -18)]
        private static void CloseOthers() => CloseOthers(SelectedScenes());

        private static void CloseOthers(List<Scene> keep)
        {
            var others = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!keep.Contains(s)) others.Add(s);
            }
            if (others.Count == 0) return;
            if (!EditorSceneManager.SaveModifiedScenesIfUserWantsTo(others.ToArray())) return;
            Scene first = keep.FirstOrDefault(s => s.IsValid() && s.isLoaded);
            if (first.IsValid()) SceneManager.SetActiveScene(first);
            foreach (Scene s in others) EditorSceneManager.CloseScene(s, true);
        }

        [MenuItem(MenuAround, priority = -17)]
        private static void OpenAround() => OpenAround(SelectedScenes());

        private static void OpenAround(List<Scene> selected)
        {
            // The selected scene's region, or failing that the Scene view's centre; one region (96 tiles) around it.
            MapArea here = null;
            foreach (GameObject root in selected[0].GetRootGameObjects())
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
                Debug.LogWarning("[R'th] Select a region scene first.");
                return;
            }
            Open(Regions().Where(r => r.Rect.Overlaps(around)).Select(r => r.Scene).ToList(), selected);
        }

        [MenuItem(MenuArea, true)]
        [MenuItem(MenuAll, true)]
        [MenuItem(MenuClose, true)]
        [MenuItem(MenuAround, true)]
        private static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

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

        /// <summary>Opens the named region scenes beside what is open; the first selected region stays (or becomes) the active scene.</summary>
        private static void Open(List<string> names, List<Scene> selected)
        {
            Scene keepActive = selected.FirstOrDefault(s => s.IsValid() && s.name != GameScene);
            if (!keepActive.IsValid()) keepActive = SceneManager.GetActiveScene();
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
            if (keepActive.IsValid() && keepActive.isLoaded && keepActive.name != GameScene) SceneManager.SetActiveScene(keepActive);
            else
                foreach (Scene s in opened)
                    if (s.IsValid()) { SceneManager.SetActiveScene(s); break; }
            Debug.Log($"[R'th] Opened {opened.Count} region scenes. Edits save to each region's own scene.");
        }

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
