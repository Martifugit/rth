using System.Collections.Generic;
using System.Linq;
using Rth.MapTools;
using Rth.Shared.World;
using Rth.Shared.World.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Edit Terrain (docs/workflow.md): the whole surface world's ground as one Unity Terrain, so
    /// Unity's own terrain tools (raise and lower, set height, smooth, stamp...) shape it. It stands
    /// in for the tile ground while it is on (the tile ground is hidden), is never saved into a scene
    /// or a build, and holds exactly the data's heights: one heightmap sample per tile corner. It is
    /// drawn see-through (TerrainEditor.shader: ground colours, facets, tile grid, region borders, cliffs red).
    /// Saving (Ctrl+S, or R'th > World > Save Terrain) writes the heights back to the data through
    /// <see cref="WorldTerrain.Save"/>: built regions' terrain, generated regions' raise
    /// (world/layout/sculpt), then the blockout and the server's maps; open region scenes are
    /// redrawn at once, the rest at the next Sync World. Ground without tiles (Tolling, Palmerway)
    /// is drawn hatched and keeps its heights.
    /// </summary>
    [InitializeOnLoad]
    public static class TerrainEditor
    {
        private const string MenuPath = "R'th/World/Edit Terrain";
        private const string ObjectName = "R'th terrain editor (not saved)";
        /// <summary>The world's heights stay within these (world-scale.md 3); the terrain spans them.</summary>
        private const float Low = -4f, High = 16f;
        /// <summary>The largest standard heightmap: 2048 tiles a side, a sample on every corner.</summary>
        private const int Resolution = 2049;

        private static Terrain _terrain;
        /// <summary>Heights not yet saved, kept while the terrain is rebuilt (another scene opened, Play).</summary>
        private static float[,] _unsaved;
        private static bool _applying, _rebuildQueued;

        private static bool Editing
        {
            get => SessionState.GetBool("RthTerrainEditing", false);
            set => SessionState.SetBool("RthTerrainEditing", value);
        }

        private static bool Dirty
        {
            get => SessionState.GetBool("RthTerrainDirty", false);
            set => SessionState.SetBool("RthTerrainDirty", value);
        }

        static TerrainEditor()
        {
            TerrainCallbacks.heightmapChanged += OnHeightmapChanged;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorSceneManager.sceneOpened += (scene, mode) => QueueRebuild();
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.wantsToQuit += OnQuit;
            EditorApplication.delayCall += Reattach;
        }

        // ------------------------------------------------------------------ menus

        [MenuItem(MenuPath, priority = 5)]
        private static void Toggle()
        {
            if (Editing) Stop(ask: true);
            else Start();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Editing);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem("R'th/World/Save Terrain", priority = 6)]
        private static void SaveMenu() => Save(redrawLater: false);

        [MenuItem("R'th/World/Save Terrain", true)]
        private static bool SaveValidate() => _terrain != null;

        // ------------------------------------------------------------------ on and off

        private static void Start()
        {
            Editing = true;
            Dirty = false;
            _unsaved = null;
            Build();
            if (_terrain != null) Selection.activeGameObject = _terrain.gameObject;
        }

        /// <summary>Ends editing; with <paramref name="ask"/>, unsaved heights are saved, dropped or kept editing.</summary>
        private static void Stop(bool ask)
        {
            if (ask && Dirty && _terrain != null)
            {
                int choice = EditorUtility.DisplayDialogComplex("Edit Terrain", "Save your terrain changes to the world data?", "Save", "Cancel", "Discard");
                if (choice == 1) return;
                if (choice == 0) Save(redrawLater: false);
            }
            Editing = false;
            Dirty = false;
            _unsaved = null;
            Destroy();
        }

        private static void Reattach()
        {
            _terrain = Resources.FindObjectsOfTypeAll<Terrain>().FirstOrDefault(t => t != null && t.name == ObjectName && t.gameObject.scene.IsValid());
            if (Editing && _terrain == null && !EditorApplication.isPlayingOrWillChangePlaymode) Build();
            TileGround.ShowSurface = _terrain == null;
        }

        /// <summary>After scenes open or close (Sync World opens many): once, when the Editor is idle again.</summary>
        private static void QueueRebuild()
        {
            if (!Editing || _rebuildQueued) return;
            _rebuildQueued = true;
            EditorApplication.delayCall += () =>
            {
                _rebuildQueued = false;
                if (Editing && _terrain == null && !EditorApplication.isPlayingOrWillChangePlaymode) Build();
            };
        }

        // ------------------------------------------------------------------ the terrain

        private static void Build()
        {
            Destroy();
            WorldTerrain world;
            try
            {
                EditorUtility.DisplayProgressBar("Edit Terrain", "Reading the world's heights...", 0.3f);
                world = WorldTerrain.Load(WorldSync.RepoRoot);
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Editing = false;
                EditorUtility.DisplayDialog("Edit Terrain", "Couldn't read the world's heights: " + e.Message, "OK");
                return;
            }
            Heightfield f = world.Field;
            if (f.Width >= Resolution || f.Depth >= Resolution)
            {
                EditorUtility.ClearProgressBar();
                Editing = false;
                EditorUtility.DisplayDialog("Edit Terrain", $"The world is {f.Width} x {f.Depth} tiles; one terrain holds at most {Resolution - 1}.", "OK");
                return;
            }
            try
            {
                EditorUtility.DisplayProgressBar("Edit Terrain", "Building the terrain...", 0.6f);
                var data = new TerrainData { hideFlags = HideFlags.DontSave, name = "World terrain" };
                data.heightmapResolution = Resolution;
                data.size = new Vector3(Resolution - 1, High - Low, Resolution - 1);

                float[,] heights = _unsaved ?? new float[Resolution, Resolution];
                if (_unsaved == null)
                    for (int j = 0; j < Resolution; j++)
                        for (int i = 0; i < Resolution; i++)
                        {
                            int x = f.OriginX + i, y = f.OriginY + j;
                            float h = i <= f.Width && j <= f.Depth ? f.Corner(x, y) : 0f;
                            heights[j, i] = (h - Low) / (High - Low);
                        }
                _applying = true;
                data.SetHeights(0, 0, heights);
                _unsaved = null;

                // Tiles in no region are holes; ground drawn another way (Tolling) stands, locked.
                var solid = new bool[Resolution - 1, Resolution - 1];
                for (int j = 0; j < Resolution - 1; j++)
                    for (int i = 0; i < Resolution - 1; i++)
                        solid[j, i] = world.AreaAt(f.OriginX + i, f.OriginY + j) != null;
                data.SetHoles(0, 0, solid);
                _applying = false;

                GameObject go = Terrain.CreateTerrainGameObject(data);
                go.name = ObjectName;
                go.hideFlags = HideFlags.DontSave;
                go.layer = 2; // Ignore Raycast: the game's clicks and the ground lookups never hit it
                go.transform.position = new Vector3(f.OriginX, Low, f.OriginY);
                _terrain = go.GetComponent<Terrain>();
                _terrain.hideFlags = HideFlags.DontSave;
                go.GetComponent<TerrainCollider>().hideFlags = HideFlags.DontSave;
                _terrain.materialTemplate = TerrainMaterial(world);
                _terrain.drawInstanced = false; // the shader reads the mesh's own positions and UVs
                _terrain.heightmapPixelError = 1f;
                _terrain.basemapDistance = 4000f;
                _terrain.drawTreesAndFoliage = false;
                TileGround.ShowSurface = false;
            }
            finally
            {
                _applying = false;
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// The see-through look (TerrainEditor.shader). Its opacity is kept between sessions: change it
        /// on the material (the terrain's Settings tab, Material).
        /// </summary>
        private static Material TerrainMaterial(WorldTerrain world)
        {
            Shader shader = Shader.Find("Hidden/Rth/TerrainEditor");
            if (shader == null)
            {
                Debug.LogError("[R'th] Edit Terrain: Hidden/Rth/TerrainEditor is missing; drawn plain.");
                shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            }
            var material = new Material(shader) { hideFlags = HideFlags.DontSave, name = "World terrain" };
            material.mainTexture = MapTexture(world);
            if (material.HasProperty("_Opacity")) material.SetFloat("_Opacity", EditorPrefs.GetFloat(OpacityPref, 0.9f));
            return material;
        }

        private const string OpacityPref = "RthTerrainOpacity2"; // 2: the old default was too see-through

        /// <summary>The ground's colours, a pixel a tile: each tile's kind in its region's material colour; locked ground grey.</summary>
        private static Texture2D MapTexture(WorldTerrain world)
        {
            Heightfield f = world.Field;
            var tex = new Texture2D(Resolution - 1, Resolution - 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave, name = "World terrain map", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[(Resolution - 1) * (Resolution - 1)];
            var colours = new Dictionary<(RegionData, string), Color32>();
            for (int j = 0; j < Resolution - 1; j++)
                for (int i = 0; i < Resolution - 1; i++)
                {
                    var tile = world.Tile(f.OriginX + i, f.OriginY + j);
                    if (tile == null)
                    {
                        if (world.AreaAt(f.OriginX + i, f.OriginY + j) != null) pixels[j * (Resolution - 1) + i] = new Color32(150, 146, 136, 191);
                        continue;
                    }
                    (TileKind kind, WorldTerrain.Area area) = tile.Value;
                    if (!colours.TryGetValue((area.Region, kind.Material), out Color32 c))
                    {
                        c = area.Region.Materials.TryGetValue(kind.Material, out string value) && Palette.TryParse(value, out Color colour)
                            ? (Color32)colour : new Color32(204, 51, 204, 255);
                        colours[(area.Region, kind.Material)] = c;
                    }
                    pixels[j * (Resolution - 1) + i] = c;
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static void Destroy()
        {
            Terrain[] terrains = Resources.FindObjectsOfTypeAll<Terrain>().Where(t => t != null && t.name == ObjectName && t.gameObject.scene.IsValid()).ToArray();
            foreach (Terrain t in terrains)
            {
                TerrainData data = t.terrainData;
                Material material = t.materialTemplate;
                if (data != null) Object.DestroyImmediate(data);
                if (material != null)
                {
                    if (material.HasProperty("_Opacity")) EditorPrefs.SetFloat(OpacityPref, material.GetFloat("_Opacity"));
                    if (material.mainTexture != null) Object.DestroyImmediate(material.mainTexture);
                    Object.DestroyImmediate(material);
                }
                Object.DestroyImmediate(t.gameObject);
            }
            _terrain = null;
            TileGround.ShowSurface = true;
        }

        // ------------------------------------------------------------------ saving

        private static void OnHeightmapChanged(Terrain terrain, RectInt changed, bool synced)
        {
            if (_applying || terrain == null || terrain != _terrain) return;
            if (!Dirty)
            {
                Dirty = true;
                // So Ctrl+S saves: the scene it sits in counts as changed.
                if (terrain.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            }
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            if (_terrain == null || !Dirty || _terrain.gameObject.scene != scene) return;
            Save(redrawLater: true); // the data now; redrawing saves scenes, so it waits until this save is done
        }

        /// <summary>Writes the terrain's heights to the world data, then redraws the open region scenes it changed.</summary>
        public static void Save(bool redrawLater)
        {
            if (_terrain == null) return;
            TerrainData data = _terrain.terrainData;
            Vector3 at = _terrain.transform.position;
            int x0 = Mathf.RoundToInt(at.x), y0 = Mathf.RoundToInt(at.z);
            float[,] heights = data.GetHeights(0, 0, Resolution, Resolution);
            var edited = new Heightfield(x0, y0, Resolution - 1, Resolution - 1);
            for (int j = 0; j < Resolution; j++)
                for (int i = 0; i < Resolution; i++)
                    edited.SetCorner(x0 + i, y0 + j, Low + heights[j, i] * (High - Low));

            string file = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "rth-terrain.heights"));
            WorldTerrain.WriteHeights(file, edited);
            var messages = new List<string>();
            var errors = new List<string>();
            var regions = new List<string>();
            try
            {
                EditorUtility.DisplayProgressBar("Save Terrain", "Writing the heights to the world data and exporting the maps (tools/world)...", 0.4f);
                int exit = RunWorldTool($"terrain-save \"{file}\"", messages, errors);
                string line = messages.LastOrDefault(m => m.StartsWith("regions:"));
                if (line != null)
                {
                    messages.Remove(line);
                    regions.AddRange(line.Substring("regions:".Length).Split(',').Select(r => r.Trim()).Where(r => r.Length > 0));
                }
                if (exit != 0 && line == null) throw new System.Exception(string.Join("\n", errors.DefaultIfEmpty($"tools/world exited with {exit}")));
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("[R'th] Save Terrain failed: " + e.Message);
                EditorUtility.DisplayDialog("Save Terrain", "Couldn't save the terrain (your changes are still here): " + e.Message, "OK");
                return;
            }
            var result = (Regions: regions, Messages: messages, Errors: errors);
            Dirty = false;
            EditorUtility.ClearProgressBar();
            foreach (string m in result.Messages.Where(m => !m.StartsWith("wrote "))) Debug.Log("[R'th] Save Terrain: " + m);
            foreach (string e in result.Errors) Debug.LogError("[R'th] Save Terrain: " + e);
            void After()
            {
                try
                {
                    Redraw(result.Regions);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
                // Pinned corners went back to the data's heights: show what was kept.
                if (Editing && result.Messages.Any(m => m.Contains("left as they are"))) Build();
            }
            if (redrawLater) EditorApplication.delayCall += After;
            else After();
            string summary = result.Regions.Count == 0
                ? "No heights changed."
                : $"Saved the heights of {result.Regions.Count} regions: {string.Join(", ", result.Regions.Take(12))}{(result.Regions.Count > 12 ? "..." : "")}.";
            Debug.Log("[R'th] Save Terrain: " + summary + (result.Regions.Count > 0 ? " Open region scenes are redrawn; the rest at the next Sync World." : ""));
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.ShowNotification(new GUIContent(result.Errors.Count > 0 ? "Terrain saved with errors (see the Console)" : "Terrain saved"));
        }

        /// <summary>
        /// Runs <c>dotnet run --project tools/world -- &lt;args&gt;</c> from the repository root and waits for it:
        /// the world tool runs on .NET, as it does from the command line (see <see cref="WorldTerrain.Save"/>).
        /// </summary>
        internal static int RunWorldTool(string args, List<string> output, List<string> errors)
        {
            var start = new System.Diagnostics.ProcessStartInfo("dotnet", $"run --project tools/world -- {args}")
            {
                WorkingDirectory = WorldSync.RepoRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = new System.Diagnostics.Process { StartInfo = start };
            process.OutputDataReceived += (s, e) => { if (e.Data != null) lock (output) output.Add(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (errors) errors.Add(e.Data.StartsWith("error: ") ? e.Data.Substring(7) : e.Data); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit(600000))
            {
                process.Kill();
                throw new System.Exception("tools/world took more than ten minutes.");
            }
            process.WaitForExit(); // let the output finish arriving
            return process.ExitCode;
        }

        /// <summary>Redraws the changed regions whose scenes are open, as Sync World would.</summary>
        internal static void Redraw(List<string> regions)
        {
            if (regions.Count == 0) return;
            var open = new HashSet<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                foreach (MapArea area in s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapArea>(true)))
                    if (regions.Contains(area.exportName)) open.Add(area.exportName);
            }
            if (open.Count == 0) return;
            var report = new WorldSync.Report();
            SyncState state = SyncState.Load();
            try
            {
                foreach (string id in open)
                {
                    EditorUtility.DisplayProgressBar("Redrawing", "Redrawing " + id, 0.8f);
                    WorldWorkspace.Entry entry = WorldWorkspace.Load(WorldSync.RepoRoot, id).Find(id);
                    if (entry == null) continue;
                    int errors = report.Lines.Count(l => l.StartsWith("ERROR"));
                    WorldSync.SyncRegion(entry, report, screenshots: false);
                    if (report.Lines.Count(l => l.StartsWith("ERROR")) == errors) state.Record(entry);
                    else state.Forget(id);
                }
            }
            finally
            {
                // Its progress bar blocks the whole Editor until cleared.
                EditorUtility.ClearProgressBar();
            }
            state.Save();
        }

        // ------------------------------------------------------------------ keeping it through scene changes, Play and quitting

        private static void OnSceneClosing(Scene scene, bool removing)
        {
            if (_terrain == null || _terrain.gameObject.scene != scene) return;
            // The terrain goes with its scene; keep unsaved heights for the one rebuilt after.
            if (Dirty) _unsaved = _terrain.terrainData.GetHeights(0, 0, Resolution, Resolution);
            Destroy();
            QueueRebuild();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode && _terrain != null)
            {
                if (Dirty) _unsaved = _terrain.terrainData.GetHeights(0, 0, Resolution, Resolution);
                Destroy();
            }
            else if (change == PlayModeStateChange.EnteredEditMode) QueueRebuild();
        }

        private static bool OnQuit()
        {
            if (!Dirty || _terrain == null) return true;
            int choice = EditorUtility.DisplayDialogComplex("Edit Terrain", "Save your terrain changes to the world data before quitting?", "Save", "Cancel", "Don't save");
            if (choice == 1) return false;
            if (choice == 0) Save(redrawLater: false);
            return true;
        }
    }
}
