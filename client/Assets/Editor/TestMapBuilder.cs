using System.IO;
using Rth.MapTools;
using Rth.MapTools.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Lays out the Milestone 1 test map from primitives: a 32x32 field with a hut (door on the
    /// south side), a pond, trees, rocks and a fence with a gap. Once built, the scene is the
    /// source of truth: edit it in Unity and export, don't rebuild over your changes.
    /// </summary>
    public static class TestMapBuilder
    {
        private const string ScenePath = "Assets/Scenes/TestMap.unity";
        private const string MaterialDir = "Assets/Art/Materials";

        [MenuItem("R'th/Dev/Rebuild Test Map Scene", priority = 100)]
        private static void BuildMenu()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild Test Map", "This replaces " + ScenePath + " and any edits made to it.", "Rebuild", "Cancel"))
                return;
            Build();
        }

        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Material grass = MakeMaterial("Grass", new Color(0.36f, 0.55f, 0.22f));
            Material dirt = MakeMaterial("Dirt", new Color(0.55f, 0.42f, 0.26f));
            Material planks = MakeMaterial("Planks", new Color(0.62f, 0.45f, 0.28f));
            Material plaster = MakeMaterial("Plaster", new Color(0.86f, 0.80f, 0.66f));
            Material water = MakeMaterial("Water", new Color(0.22f, 0.42f, 0.66f));
            Material bark = MakeMaterial("Bark", new Color(0.36f, 0.25f, 0.15f));
            Material leaves = MakeMaterial("Leaves", new Color(0.20f, 0.42f, 0.18f));
            Material stone = MakeMaterial("Stone", new Color(0.52f, 0.52f, 0.50f));
            Material wood = MakeMaterial("Fence", new Color(0.45f, 0.33f, 0.20f));

            var root = new GameObject("Map");
            var area = root.AddComponent<MapArea>();
            area.exportName = "test";
            area.origin = Vector2Int.zero;
            area.size = new Vector2Int(32, 32);

            // Ground, with a cosmetic dirt path from the spawn to the hut door.
            Box("Ground", root.transform, new Vector3(16f, -0.1f, 16f), new Vector3(32f, 0.2f, 32f), grass);
            Box("Path", root.transform, new Vector3(15.5f, 0.005f, 12f), new Vector3(1f, 0.01f, 4f), dirt);

            // Hut: x 12..18, z 14..19, door on the south wall at tile x = 15.
            var hut = new GameObject("Hut").transform;
            hut.SetParent(root.transform);
            Box("Floor", hut, new Vector3(15f, 0.01f, 16.5f), new Vector3(6f, 0.02f, 5f), planks);
            Wall("Wall S1", hut, new Vector3(12f, 0f, 14f), new Vector3(15f, 0f, 14f), plaster);
            Wall("Wall S2", hut, new Vector3(16f, 0f, 14f), new Vector3(18f, 0f, 14f), plaster);
            Wall("Wall N", hut, new Vector3(12f, 0f, 19f), new Vector3(18f, 0f, 19f), plaster);
            Wall("Wall W", hut, new Vector3(12f, 0f, 14f), new Vector3(12f, 0f, 19f), plaster);
            Wall("Wall E", hut, new Vector3(18f, 0f, 14f), new Vector3(18f, 0f, 19f), plaster);
            Blocker(Box("Table", hut, new Vector3(16.5f, 0.35f, 17.5f), new Vector3(0.9f, 0.7f, 0.9f), planks));

            // Pond: x 4..9, z 22..27.
            Blocker(Box("Pond", root.transform, new Vector3(6.5f, -0.02f, 24.5f), new Vector3(5f, 0.06f, 5f), water));

            // Trees and rocks, each on its own tile.
            int[,] trees = { { 3, 5 }, { 6, 9 }, { 9, 4 }, { 24, 24 }, { 27, 20 }, { 22, 28 }, { 28, 27 }, { 3, 15 } };
            for (int i = 0; i < trees.GetLength(0); i++) Tree(root.transform, trees[i, 0], trees[i, 1], bark, leaves);
            int[,] rocks = { { 20, 13 }, { 21, 13 }, { 25, 15 }, { 10, 26 }, { 4, 29 } };
            for (int i = 0; i < rocks.GetLength(0); i++)
                Blocker(Box("Rock", root.transform, new Vector3(rocks[i, 0] + 0.5f, 0.3f, rocks[i, 1] + 0.5f), new Vector3(0.8f, 0.6f, 0.8f), stone));

            // Fence along z = 8 from x = 20 to 30, with a gap at tile x = 24.
            var fence = new GameObject("Fence").transform;
            fence.SetParent(root.transform);
            Fence("Fence W", fence, 20f, 24f, 8f, wood);
            Fence("Fence E", fence, 25f, 30f, 8f, wood);

            var spawn = new GameObject("Spawn");
            spawn.transform.SetParent(root.transform);
            spawn.transform.position = new Vector3(15.5f, 0f, 9.5f);
            spawn.AddComponent<MapSpawn>();

            SetUpCameraAndLight(out CameraRig rig);
            var game = new GameObject("Game");
            var world = game.AddComponent<WorldView>();
            var session = game.AddComponent<GameSession>();
            session.world = world;
            session.cameraRig = rig;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            MapExporter.Export(area);
            Debug.Log("[R'th] Built " + ScenePath);
        }

        private static void SetUpCameraAndLight(out CameraRig rig)
        {
            Camera cam = Camera.main;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.72f, 0.88f);
            cam.fieldOfView = 45f;
            rig = cam.gameObject.AddComponent<CameraRig>();
            rig.idleFocus = new Vector3(15.5f, 0f, 9.5f);

            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
                sun.shadows = LightShadows.Soft;
                sun.color = new Color(1f, 0.96f, 0.88f);
            }
        }

        private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static void Blocker(GameObject go) => go.AddComponent<MapBlocker>();

        /// <summary>A plaster wall along a grid line from <paramref name="a"/> to <paramref name="b"/>.</summary>
        private static void Wall(string name, Transform parent, Vector3 a, Vector3 b, Material material)
        {
            const float thickness = 0.2f;
            const float height = 1.6f;
            Vector3 center = (a + b) / 2f + Vector3.up * (height / 2f);
            Vector3 size = a.z == b.z
                ? new Vector3(Mathf.Abs(b.x - a.x) + thickness, height, thickness)
                : new Vector3(thickness, height, Mathf.Abs(b.z - a.z) + thickness);
            Box(name, parent, center, size, material).AddComponent<MapWall>();
        }

        /// <summary>A rail fence along z = <paramref name="z"/>, from x0 to x1.</summary>
        private static void Fence(string name, Transform parent, float x0, float x1, float z, Material material)
        {
            GameObject rail = Box(name, parent, new Vector3((x0 + x1) / 2f, 0.55f, z), new Vector3(x1 - x0, 0.12f, 0.1f), material);
            rail.AddComponent<MapWall>();
            for (float x = x0; x <= x1 + 0.01f; x += 1f)
            {
                GameObject post = Box("Post", rail.transform.parent, new Vector3(x, 0.4f, z), new Vector3(0.14f, 0.8f, 0.14f), material);
                Object.DestroyImmediate(post.GetComponent<Collider>()); // the rail is the wall; posts are decoration
                post.transform.SetParent(rail.transform, true);
            }
        }

        private static void Tree(Transform parent, int x, int y, Material bark, Material leaves)
        {
            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.position = new Vector3(x + 0.5f, 0f, y + 0.5f);
            tree.AddComponent<MapBlocker>();

            GameObject trunk = Box("Trunk", tree.transform, tree.transform.position + Vector3.up * 0.8f, new Vector3(0.35f, 1.6f, 0.35f), bark);
            trunk.transform.SetParent(tree.transform, true);
            for (int i = 0; i < 2; i++)
            {
                float s = 1.5f - i * 0.5f;
                GameObject crown = Box("Leaves", tree.transform, tree.transform.position + Vector3.up * (1.8f + i * 0.6f), new Vector3(s, 0.8f, s), leaves);
                crown.transform.rotation = Quaternion.Euler(0f, 45f * i + 20f, 0f);
                Object.DestroyImmediate(crown.GetComponent<Collider>()); // only the trunk sets the footprint
            }
        }

        private static Material MakeMaterial(string name, Color color)
        {
            Directory.CreateDirectory(MaterialDir);
            string path = MaterialDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
