using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Builds the Game scene: the camera, sun, fog and the game objects (session, world view, HUD,
    /// region streamer). It is the first scene in the build; regions stream in around it. Fog
    /// hides the edge of what is loaded, the way the classic client's draw distance did.
    /// </summary>
    public static class GameSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";
        private static readonly Color Sky = new Color(0.55f, 0.72f, 0.88f);

        [MenuItem("R'th/Dev/Rebuild Game Scene", priority = 100)]
        private static void BuildMenu()
        {
            if (WorldKit.ConfirmRebuild(ScenePath)) Build();
        }

        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = camObject.AddComponent<Camera>();
            camObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
            cam.fieldOfView = 45f;
            cam.farClipPlane = 120f;
            var rig = camObject.AddComponent<CameraRig>();
            camObject.AddComponent<CameraOcclusion>();
            rig.idleFocus = new Vector3(33.5f, 0f, 53.5f); // Tolling's square, behind the login screen

            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.1f;

            // Distance fog in the sky colour: regions load beyond it, so nothing pops into view.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Sky;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 85f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.24f);
            RenderSettings.sun = sun;

            // Unbuilt land: plain ground under the whole world, as the classic client filled map
            // squares it had no data for. Regions sit on top of it; past their edges the land just
            // carries on into the fog instead of stopping at the sky. No collider: nobody walks
            // or clicks on it (the server blocks every tile outside a region anyway).
            var kit = new WorldKit(seed: 0);
            GameObject horizon = GameObject.CreatePrimitive(PrimitiveType.Plane);
            horizon.name = "Unbuilt land";
            Object.DestroyImmediate(horizon.GetComponent<Collider>());
            horizon.transform.position = new Vector3(0f, -0.3f, 0f);
            horizon.transform.localScale = new Vector3(600f, 1f, 600f); // 6000 tiles across
            horizon.GetComponent<Renderer>().sharedMaterial = kit.Mat("UnbuiltLand", new Color(0.33f, 0.5f, 0.21f));
            horizon.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var game = new GameObject("Game");
            var world = game.AddComponent<WorldView>();
            var session = game.AddComponent<GameSession>();
            var regions = game.AddComponent<RegionStreamer>();
            regions.world = world;
            session.world = world;
            session.cameraRig = rig;
            session.regions = regions;
            var hud = game.AddComponent<CombatHud>();
            hud.session = session;
            hud.world = world;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);

            // First in the build: the game starts here and streams regions in.
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.path != ScenePath && File.Exists(s.path)) scenes.Add(s); // drop scenes that were deleted
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[R'th] Built " + ScenePath);
        }
    }
}
