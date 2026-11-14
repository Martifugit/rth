using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rth.MapTools;
using Rth.Shared.World;
using Rth.Shared.World.Data;
using Rth.Shared.World.Ground;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rth.Client.Editor
{
    /// <summary>
    /// The world's ground is Unity terrain (docs/world-guide.md section 4): one terrain per
    /// <see cref="GroundChunk.Size"/>-tile square of the world, each in its own scene
    /// (Assets/World/Terrain/Terrain_i_j.unity, its TerrainData beside it), sculpted and painted with
    /// Unity's own terrain tools. A heightmap sample sits on every tile corner and a paint texel on
    /// every tile; each kind of ground (world/grounds.json) is a terrain layer, and a tile is the
    /// layer painted heaviest there, so a soft brush still paints crisp tiles. Stones are layers too
    /// (named "stones-..."): how heavily they are painted is how thickly they lie.
    ///
    /// The terrains open by themselves beside the region scenes they lie under. The game's ground
    /// (<see cref="TileGround"/>) is drawn from them as you work; the terrain itself is hidden
    /// unless R'th > World > Show Unity Terrain is on. Saving (Ctrl+S) writes each region's tiles,
    /// stones and heights from the terrains into world/regions, which the server's maps are
    /// compiled from.
    /// </summary>
    [InitializeOnLoad]
    public static class WorldTerrains
    {
        public const string Folder = "Assets/World/Terrain";
        private const string LayerFolder = Folder + "/Layers";
        private const string ShowMenu = "R'th/World/Show Unity Terrain";
        private const float Low = TerrainText.Lowest, High = TerrainText.Highest;
        private const int Size = GroundChunk.Size;
        /// <summary>How far a region's painted ring of the neighbours' tiles reaches; changes this near a region redraw it.</summary>
        private const int Ring = 4;

        private static readonly HashSet<(int i, int j)> Changed = new HashSet<(int, int)>();
        private static readonly HashSet<(int i, int j)> Saved = new HashSet<(int, int)>();
        private static double _changedAt;
        private static bool _opening;
        private static GroundTable _table;
        private static System.DateTime _tableTime;

        static WorldTerrains()
        {
            TerrainCallbacks.heightmapChanged += (t, rect, synced) => OnChanged(t);
            TerrainCallbacks.textureChanged += (t, name, rect, synced) => OnChanged(t);
            Undo.undoRedoPerformed += RefreshAllOpen;
            EditorApplication.update += Update;
            EditorSceneManager.sceneOpened += (scene, mode) => QueueOpenTerrains();
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.delayCall += ApplyShow;
        }

        // ------------------------------------------------------------------ names and places

        public static string Name(int i, int j) => $"Terrain_{Part(i)}_{Part(j)}";
        private static string Part(int n) => n < 0 ? "m" + (-n) : n.ToString();
        public static string ScenePath(int i, int j) => $"{Folder}/{Name(i, j)}.unity";
        public static string DataPath(int i, int j) => $"{Folder}/{Name(i, j)}.asset";
        public static string LayerPath(string layer) => $"{LayerFolder}/{layer}.terrainlayer";

        /// <summary>The chunk a terrain stands for, from where it stands.</summary>
        public static bool TryChunkOf(Terrain t, out (int i, int j) key)
        {
            key = default;
            if (t == null || t.terrainData == null || !AssetDatabase.GetAssetPath(t.terrainData).StartsWith(Folder + "/")) return false;
            Vector3 p = t.transform.position;
            key = (GroundChunk.IndexOf(Mathf.RoundToInt(p.x)), GroundChunk.IndexOf(Mathf.RoundToInt(p.z)));
            return true;
        }

        public static GroundTable Table
        {
            get
            {
                string path = GroundTable.PathIn(WorldSync.RepoRoot);
                System.DateTime time = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default;
                if (_table == null || time != _tableTime)
                {
                    _table = File.Exists(path) ? GroundTable.Load(path) : new GroundTable();
                    _tableTime = time;
                }
                return _table;
            }
        }

        // ------------------------------------------------------------------ terrain data and chunks

        /// <summary>Reads a terrain's ground: heights on every corner, holes, and the weight of each known layer on every tile.</summary>
        public static GroundChunk ToChunk(TerrainData data, int i, int j, GroundTable table)
        {
            var chunk = new GroundChunk(i, j);
            float[,] heights = data.GetHeights(0, 0, Size + 1, Size + 1);
            for (int y = 0; y <= Size; y++)
                for (int x = 0; x <= Size; x++)
                    chunk.Heights[y, x] = Low + heights[y, x] * (High - Low);
            bool[,] solid = data.GetHoles(0, 0, Size, Size);
            float[,,] alpha = data.alphamapLayers > 0 ? data.GetAlphamaps(0, 0, Size, Size) : new float[Size, Size, 0];
            TerrainLayer[] layers = data.terrainLayers;
            var known = new List<int>();
            for (int l = 0; l < layers.Length; l++)
            {
                string name = layers[l] != null ? layers[l].name : "";
                bool ok = table.Grounds.ContainsKey(name) ||
                          name.StartsWith(GroundTable.StonePrefix) && table.Stones.ContainsKey(name.Substring(GroundTable.StonePrefix.Length));
                if (ok) known.Add(l);
                else WarnOnce($"[R'th] {Name(i, j)}: its terrain layer '{name}' is no ground of world/grounds.json; it is left out.");
            }
            foreach (int l in known) chunk.Layer(layers[l].name);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    chunk.Solid[y, x] = solid[y, x];
                    for (int k = 0; k < known.Count; k++) chunk.Weights[y, x, k] = alpha[y, x, known[k]];
                }
            return chunk;
        }

        /// <summary>Writes a chunk into a terrain: its layers (made if missing), heights, paint and holes.</summary>
        public static void FromChunk(TerrainData data, GroundChunk chunk)
        {
            data.heightmapResolution = Size + 1;
            data.alphamapResolution = Size;
            data.baseMapResolution = Size;
            data.size = new Vector3(Size, High - Low, Size);
            data.terrainLayers = chunk.Layers.Select(LayerAsset).ToArray();
            var heights = new float[Size + 1, Size + 1];
            for (int y = 0; y <= Size; y++)
                for (int x = 0; x <= Size; x++)
                    heights[y, x] = Mathf.Clamp01((chunk.Heights[y, x] - Low) / (High - Low));
            data.SetHeights(0, 0, heights);
            if (chunk.Layers.Count > 0)
            {
                var alpha = new float[Size, Size, chunk.Layers.Count];
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        bool any = false;
                        for (int l = 0; l < chunk.Layers.Count; l++) any |= (alpha[y, x, l] = chunk.Weights[y, x, l]) > 0f;
                        if (!any) alpha[y, x, 0] = 1f; // a hole still needs its weights to add up
                    }
                data.SetAlphamaps(0, 0, alpha);
            }
            data.SetHoles(0, 0, chunk.Solid);
        }

        /// <summary>The terrain layer of a ground or stones, made (a flat swatch of its colour) if it isn't there yet.</summary>
        private static TerrainLayer LayerAsset(string layer)
        {
            string path = LayerPath(layer);
            var asset = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (asset != null) return asset;
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", LayerFolder));
            GroundTable table = Table;
            string material = layer.StartsWith(GroundTable.StonePrefix)
                ? table.Stones.TryGetValue(layer.Substring(GroundTable.StonePrefix.Length), out string s) ? s : null
                : table.Grounds.TryGetValue(layer, out TileKind k) ? k.Material : null;
            Color colour = material != null && table.Materials.TryGetValue(material, out string value) && Palette.TryParse(value, out Color c) ? c : Color.magenta;
            var swatch = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            swatch.SetPixels(Enumerable.Repeat(colour, 16).ToArray());
            swatch.Apply();
            string png = $"{LayerFolder}/{layer}.png";
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", png), swatch.EncodeToPNG());
            Object.DestroyImmediate(swatch);
            AssetDatabase.ImportAsset(png);
            if (AssetImporter.GetAtPath(png) is TextureImporter importer)
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            asset = new TerrainLayer { diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(png), tileSize = Vector2.one };
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>The world as the terrains hold it now (unsaved strokes included), for writing regions from.</summary>
        private sealed class Live
        {
            private readonly Dictionary<(int, int), GroundChunk> _chunks = new Dictionary<(int, int), GroundChunk>();
            private readonly GroundTable _t;
            public readonly WorldGround.ChunkWorld World;

            public Live(GroundTable table)
            {
                _t = table;
                World = new WorldGround.ChunkWorld(new LazyChunks(this));
            }

            public GroundChunk Get((int i, int j) key)
            {
                if (_chunks.TryGetValue(key, out GroundChunk c)) return c;
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath(key.i, key.j));
                return _chunks[key] = data != null ? ToChunk(data, key.i, key.j, _t) : null;
            }

            /// <summary>Chunks read on first use: a region and its ring touch a few at most.</summary>
            private sealed class LazyChunks : IReadOnlyDictionary<(int i, int j), GroundChunk>
            {
                private readonly Live _live;
                public LazyChunks(Live live) => _live = live;
                public bool TryGetValue((int i, int j) key, out GroundChunk value) => (value = _live.Get(key)) != null;
                public GroundChunk this[(int i, int j) key] => _live.Get(key);
                public bool ContainsKey((int i, int j) key) => _live.Get(key) != null;
                public IEnumerable<(int i, int j)> Keys => _live._chunks.Where(e => e.Value != null).Select(e => e.Key);
                public IEnumerable<GroundChunk> Values => _live._chunks.Values.Where(v => v != null);
                public int Count => Keys.Count();
                public IEnumerator<KeyValuePair<(int i, int j), GroundChunk>> GetEnumerator() =>
                    _live._chunks.Where(e => e.Value != null).GetEnumerator();
                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            }
        }

        // ------------------------------------------------------------------ making them (once, from the data)

        [MenuItem("R'th/World/Terrain/Create Terrains From Data", priority = 20)]
        private static void CreateMenu()
        {
            if (!EditorUtility.DisplayDialog("Create Terrains From Data",
                    "Makes the world's terrains from the regions' ground (world/regions), replacing any already in " + Folder + ". Do this once; after it the terrains are the ground's truth.",
                    "Create", "Cancel")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Create();
        }

        /// <summary>For batch mode: -executeMethod Rth.Client.Editor.WorldTerrains.CreateFromCommandLine</summary>
        public static void CreateFromCommandLine()
        {
            try
            {
                Create();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[R'th] Creating the terrains failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static void Create()
        {
            var regions = RegionFiles.RegionPaths(Path.Combine(WorldSync.RepoRoot, "world", "regions")).Select(RegionFiles.LoadRegion).ToList();
            GroundTable table = Table;
            Dictionary<(int i, int j), GroundChunk> chunks = WorldGround.ChunksFromRegions(regions, table, out HashSet<(int x, int y)> disagreements);
            if (disagreements.Count > 0) Debug.LogWarning($"[R'th] {disagreements.Count} corners on region borders had two heights; the terrains keep one.");
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", Folder));
            string[] reopen = Enumerable.Range(0, SceneManager.sceneCount).Select(k => SceneManager.GetSceneAt(k).path).Where(p => !string.IsNullOrEmpty(p)).ToArray();
            int n = 0;
            try
            {
                AssetDatabase.StartAssetEditing(); // the layers' swatches import as one batch
                foreach (string layer in chunks.Values.SelectMany(c => c.Layers).Distinct()) LayerAsset(layer);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            try
            {
                foreach (GroundChunk chunk in chunks.Values.OrderBy(c => c.J).ThenBy(c => c.I))
                {
                    EditorUtility.DisplayProgressBar("Create Terrains From Data", Name(chunk.I, chunk.J), (float)n++ / chunks.Count);
                    var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath(chunk.I, chunk.J));
                    if (data == null)
                    {
                        data = new TerrainData();
                        AssetDatabase.CreateAsset(data, DataPath(chunk.I, chunk.J));
                    }
                    FromChunk(data, chunk);
                    EditorUtility.SetDirty(data);

                    Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    GameObject go = Terrain.CreateTerrainGameObject(data);
                    go.name = Name(chunk.I, chunk.J);
                    Setup(go.GetComponent<Terrain>(), chunk.I, chunk.J);
                    EditorSceneManager.SaveScene(scene, ScenePath(chunk.I, chunk.J));
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            if (reopen.Length > 0 && !Application.isBatchMode)
            {
                EditorSceneManager.OpenScene(reopen[0], OpenSceneMode.Single);
                foreach (string s in reopen.Skip(1)) EditorSceneManager.OpenScene(s, OpenSceneMode.Additive);
            }
            Debug.Log($"[R'th] Created {chunks.Count} terrains in {Folder} ({table.Grounds.Count} grounds, {table.Stones.Count} kinds of stones).");
        }

        /// <summary>How every world terrain is set up: placed on its square, joined to its neighbours, out of the game's way.</summary>
        private static void Setup(Terrain terrain, int i, int j)
        {
            terrain.transform.position = new Vector3(i * Size, Low, j * Size);
            terrain.gameObject.layer = 2; // Ignore Raycast: the game's clicks and ground lookups never hit it
            terrain.allowAutoConnect = true;
            terrain.groupingID = 0;
            terrain.drawTreesAndFoliage = false;
            terrain.heightmapPixelError = 1f;
            terrain.basemapDistance = 4000f;
            terrain.drawHeightmap = Show;
        }

        // ------------------------------------------------------------------ opening them beside the regions

        private static void QueueOpenTerrains()
        {
            if (_opening || Application.isBatchMode || WorldSync.Busy || EditorApplication.isPlayingOrWillChangePlaymode) return;
            _opening = true;
            EditorApplication.delayCall += () =>
            {
                _opening = false;
                OpenTerrainsUnderRegions();
            };
        }

        /// <summary>Opens the terrains under every open region scene (and the ring round them), so the ground is there to shape.</summary>
        [MenuItem("R'th/World/Terrain/Open Terrains Under Open Regions", priority = 21)]
        public static void OpenTerrainsUnderRegions()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var wanted = new HashSet<(int, int)>();
            foreach (MapArea area in OpenAreas())
                foreach ((int i, int j) in ChunksOver(area.origin.x - Ring, area.origin.y - Ring, area.origin.x + area.size.x + Ring, area.origin.y + area.size.y + Ring))
                    wanted.Add((i, j));
            foreach ((int i, int j) in wanted)
            {
                string path = ScenePath(i, j);
                if (!File.Exists(Path.Combine(Application.dataPath, "..", path))) continue;
                Scene s = SceneManager.GetSceneByPath(path);
                if (s.IsValid() && s.isLoaded) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
            ApplyShow();
        }

        private static IEnumerable<MapArea> OpenAreas()
        {
            for (int n = 0; n < SceneManager.sceneCount; n++)
            {
                Scene s = SceneManager.GetSceneAt(n);
                if (!s.isLoaded) continue;
                foreach (MapArea area in s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapArea>(true)))
                    yield return area;
            }
        }

        private static IEnumerable<(int i, int j)> ChunksOver(int x0, int y0, int x1, int y1)
        {
            for (int j = GroundChunk.IndexOf(y0); j <= GroundChunk.IndexOf(y1 - 1); j++)
                for (int i = GroundChunk.IndexOf(x0); i <= GroundChunk.IndexOf(x1 - 1); i++)
                    yield return (i, j);
        }

        private static IEnumerable<Terrain> OpenTerrains() =>
            Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include).Where(t => TryChunkOf(t, out _));

        // ------------------------------------------------------------------ showing the terrain or the game's ground

        private static bool Show
        {
            get => EditorPrefs.GetBool("RthShowUnityTerrain", false);
            set => EditorPrefs.SetBool("RthShowUnityTerrain", value);
        }

        [MenuItem(ShowMenu, priority = 22)]
        private static void ToggleShow()
        {
            Show = !Show;
            ApplyShow();
        }

        [MenuItem(ShowMenu, true)]
        private static bool ToggleShowValidate()
        {
            Menu.SetChecked(ShowMenu, Show);
            return true;
        }

        /// <summary>Either the terrain (Unity's own look, its layers' colours) or the game's ground drawn from it.</summary>
        private static void ApplyShow()
        {
            bool show = Show && !EditorApplication.isPlaying;
            foreach (Terrain t in OpenTerrains()) t.drawHeightmap = show;
            TileGround.ShowSurface = !show;
        }

        // ------------------------------------------------------------------ drawing the game's ground as you work

        private static void OnChanged(Terrain terrain)
        {
            if (!TryChunkOf(terrain, out (int i, int j) key) || EditorApplication.isPlaying) return;
            Changed.Add(key);
            _changedAt = EditorApplication.timeSinceStartup;
            // So Ctrl+S saves it: the scene the terrain sits in counts as changed.
            if (terrain.gameObject.scene.IsValid() && !terrain.gameObject.scene.isDirty) EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        }

        private static void Update()
        {
            if (Changed.Count == 0 || EditorApplication.timeSinceStartup - _changedAt < 0.25) return;
            var keys = Changed.ToList();
            Changed.Clear();
            Redraw(keys);
        }

        private static void RefreshAllOpen()
        {
            var keys = new List<(int, int)>();
            foreach (Terrain t in OpenTerrains()) if (TryChunkOf(t, out (int, int) k)) keys.Add(k);
            if (keys.Count > 0) Redraw(keys);
        }

        /// <summary>Redraws the game's ground of every open region the changed terrains lie under (or ring).</summary>
        private static void Redraw(List<(int i, int j)> keys)
        {
            GroundTable table = Table;
            var live = new Live(table);
            foreach (MapArea area in OpenAreas())
            {
                if (!Touches(area.origin.x, area.origin.y, area.size.x, area.size.y, keys)) continue;
                string path = WorldSync.RegionPath(area.exportName);
                if (!File.Exists(path)) continue;
                TileGround ground = area.GetComponentsInChildren<TileGround>(true).FirstOrDefault();
                if (ground == null) continue;
                try
                {
                    RegionData region = RegionFiles.LoadRegion(path);
                    if (!(GroundEdit.Tiles(region) is Piece piece)) continue;
                    WorldGround.Apply(region, table, live.World.Tile, live.World.Corner);
                    PieceBuilder.DrawTiles(ground, new WorldKit(region.Seed), region, piece, TerrainText.Read(region));
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[R'th] {area.exportName}: couldn't draw its ground from the terrain: {e.Message}");
                }
            }
        }

        private static bool Touches(int x, int y, int w, int h, IEnumerable<(int i, int j)> keys) =>
            keys.Any(k => k.i * Size < x + w + Ring && (k.i + 1) * Size > x - Ring && k.j * Size < y + h + Ring && (k.j + 1) * Size > y - Ring);

        // ------------------------------------------------------------------ saving

        private static void OnSceneSaving(Scene scene, string path)
        {
            if (WorldSync.Busy) return;
            foreach (Terrain t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>(true)))
            {
                if (!TryChunkOf(t, out (int i, int j) key)) continue;
                if (EditorUtility.IsDirty(t.terrainData)) AssetDatabase.SaveAssetIfDirty(t.terrainData);
                Saved.Add(key);
            }
            if (Saved.Count == 0) return;
            // Once every scene of the save is written (Ctrl+S saves them one by one).
            EditorApplication.delayCall -= WriteSaved;
            EditorApplication.delayCall += WriteSaved;
        }

        private static void WriteSaved()
        {
            var keys = Saved.ToList();
            Saved.Clear();
            WriteRegions(keys);
        }

        /// <summary>
        /// Writes the ground of every region under (or ringing) the terrains into its data, exports
        /// the maps of those that changed and redraws the open ones. Returns the regions written.
        /// </summary>
        public static List<string> WriteRegions(ICollection<(int i, int j)> keys)
        {
            GroundTable table = Table;
            var live = new Live(table);
            var written = new List<string>();
            foreach (string path in RegionFiles.RegionPaths(Path.Combine(WorldSync.RepoRoot, "world", "regions")))
            {
                RegionData region;
                try
                {
                    region = RegionFiles.LoadRegion(path);
                }
                catch (System.FormatException e)
                {
                    Debug.LogError($"[R'th] {Path.GetFileName(path)}: {e.Message}");
                    continue;
                }
                if (GroundEdit.Tiles(region) == null || !Touches(region.OriginX, region.OriginY, region.Width, region.Height, keys)) continue;
                if (!WorldGround.Apply(region, table, live.World.Tile, live.World.Corner)) continue;
                RegionFiles.SaveRegion(region, path);
                written.Add(region.Id);
            }
            if (written.Count == 0) return written;
            foreach (string id in written)
                foreach (string m in WorldWorkspace.Load(WorldSync.RepoRoot, id).Export()) { }
            Debug.Log($"[R'th] The ground of {written.Count} regions was written from the terrains: {string.Join(", ", written.Take(12))}{(written.Count > 12 ? "..." : "")}.");
            RedrawRegions(written);
            return written;
        }

        /// <summary>Writes every region's ground from the terrains (as saving them all would).</summary>
        [MenuItem("R'th/World/Terrain/Write All Ground To Data", priority = 23)]
        public static void WriteAll()
        {
            var keys = new HashSet<(int, int)>();
            foreach (string guid in AssetDatabase.FindAssets("t:TerrainData", new[] { Folder }))
            {
                string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                string[] p = name.Split('_');
                if (p.Length == 3 && p[0] == "Terrain") keys.Add((ParsePart(p[1]), ParsePart(p[2])));
            }
            WriteRegions(keys);
        }

        private static int ParsePart(string s) => s.StartsWith("m") ? -int.Parse(s.Substring(1)) : int.Parse(s);

        /// <summary>For batch mode: -executeMethod Rth.Client.Editor.WorldTerrains.WriteAllFromCommandLine</summary>
        public static void WriteAllFromCommandLine()
        {
            try
            {
                WriteAll();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[R'th] Writing the ground failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// A region scene being saved: its ground is written from the terrains first, so what the
        /// scene draws and what the data holds agree when Sync World checks them.
        /// </summary>
        public static bool WriteGround(RegionData region)
        {
            if (GroundEdit.Tiles(region) == null) return false;
            var keys = ChunksOver(region.OriginX - Ring, region.OriginY - Ring, region.OriginX + region.Width + Ring, region.OriginY + region.Height + Ring).ToList();
            if (!keys.Any(k => File.Exists(Path.Combine(Application.dataPath, "..", DataPath(k.i, k.j))))) return false;
            var live = new Live(Table);
            return WorldGround.Apply(region, Table, live.World.Tile, live.World.Corner);
        }

        /// <summary>Redraws the given regions whose scenes are open, as Sync World would.</summary>
        internal static void RedrawRegions(List<string> regions)
        {
            var open = new HashSet<string>(OpenAreas().Select(a => a.exportName).Where(regions.Contains));
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
                EditorUtility.ClearProgressBar(); // its progress bar blocks the whole Editor until cleared
            }
            state.Save();
        }

        // ------------------------------------------------------------------ Play

        private static void OnPlayMode(PlayModeStateChange change)
        {
            // The terrains are for working on the world; in Play they would only get in the game's way.
            if (change == PlayModeStateChange.EnteredPlayMode)
                foreach (Terrain t in OpenTerrains()) t.gameObject.SetActive(false);
            else if (change == PlayModeStateChange.EnteredEditMode) ApplyShow();
        }

        private static readonly HashSet<string> Warned = new HashSet<string>();

        private static void WarnOnce(string message)
        {
            if (Warned.Add(message)) Debug.LogWarning(message);
        }
    }
}
