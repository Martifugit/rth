using System.Collections.Generic;
using System.IO;
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

        [MenuItem("R'th/World/Open Whole World", priority = 20)]
        private static void OpenWholeWorld()
        {
            Scene keepActive = SceneManager.GetActiveScene();
            var opened = new List<Scene>();
            foreach (string name in RegionSceneNames())
            {
                string path = FindScenePath(name);
                if (path == null)
                {
                    Debug.LogWarning($"[R'th] Region scene '{name}' was exported but can't be found in the project.");
                    continue;
                }
                opened.Add(Open(path));
            }
            // The Game scene's sun and fog make regions look as they will in play.
            string game = FindScenePath(GameScene);
            if (game != null) Open(game);

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

        [MenuItem("R'th/World/Open Whole World", true)]
        [MenuItem("R'th/World/Close Other Scenes", true)]
        private static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static Scene Open(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded) return scene;
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        private static IEnumerable<string> RegionSceneNames()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "server", "data", "maps"));
            if (!Directory.Exists(dir)) yield break;
            var seen = new HashSet<string>();
            foreach (string file in Directory.GetFiles(dir, "*.region.json"))
            {
                RegionInfo info = JsonUtility.FromJson<RegionInfo>(File.ReadAllText(file));
                if (info != null && !string.IsNullOrEmpty(info.scene) && seen.Add(info.scene)) yield return info.scene;
            }
        }

        private static string FindScenePath(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return path;
            }
            return null;
        }
    }
}
