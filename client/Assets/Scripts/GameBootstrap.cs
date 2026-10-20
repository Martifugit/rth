using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rth.Client
{
    /// <summary>
    /// The Game scene holds the camera, light and game objects; region scenes hold only the map.
    /// Starting from a region scene (pressing Play while editing one) loads the Game scene beside
    /// it, so every region can be played directly. The Game scene is made active so its lighting
    /// and fog apply to every region.
    /// </summary>
    public static class GameBootstrap
    {
        public const string GameScene = "Game";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindAnyObjectByType<GameSession>() != null)
            {
                MakeActive(SceneManager.GetSceneByName(GameScene));
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(GameScene))
            {
                Debug.LogError("[R'th] The Game scene isn't in the build. Run R'th > Dev > Rebuild Game Scene.");
                return;
            }
            SceneManager.sceneLoaded += OnLoaded;
            SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Additive);
        }

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameScene) return;
            SceneManager.sceneLoaded -= OnLoaded;
            MakeActive(scene);
        }

        private static void MakeActive(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
        }
    }
}
