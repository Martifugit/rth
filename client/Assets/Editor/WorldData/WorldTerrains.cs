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
    /// The world's ground is Unity terrain (docs/world-guide.md section 4): every region with ground
    /// has a terrain its own size (Assets/World/Terrain/&lt;region&gt;.asset), drawn in its scene and
    /// in the game, sculpted and painted with Unity's own terrain tools. The terrains connect, so a
    /// stroke runs on across region borders. Each kind of ground (world/grounds.json) is a terrain
    /// layer; stones are layers too ("stones-..."). Water is its own surface
    /// (Assets/World/Water/&lt;region&gt;.prefab), over ground painted with a water layer.
    ///
    /// The server walks tiles, so saving a region scene writes its ground into its data
    /// (<see cref="WriteGround"/>): each tile is the ground painted heaviest on it, each tile corner the
    /// terrain's height there. Water and rock block, and so does ground too steep to walk.
    /// </summary>
    [InitializeOnLoad]
    public static class WorldTerrains
    {
        public const string Folder = "Assets/World/Terrain";
        public const string WaterFolder = "Assets/World/Water";
        private const string LayerFolder = Folder + "/Layers";
        private const float Low = TerrainText.Lowest, High = TerrainText.Highest;
        /// <summary>The water layer (Unity's built-in "Water"): what water surfaces are on, for standing pieces on them.</summary>
        public const int WaterLayer = 4;

        private static GroundTable _table;
        private static System.DateTime _tableTime;

        static WorldTerrains()
        {
            TerrainCallbacks.heightmapChanged += (t, rect, synced) => OnChanged(t);
            TerrainCallbacks.textureChanged += (t, name, rect, synced) => OnChanged(t);
        }

        public static string DataPath(string region) => $"{Folder}/{region}.asset";
        public static string WaterPath(string region) => $"{WaterFolder}/{region}.prefab";
        private static string WaterMeshPath(string region) => $"{WaterFolder}/{region}.asset";
        public static string LayerPath(string layer) => $"{LayerFolder}/{layer}.terrainlayer";

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

        // ------------------------------------------------------------------ in the region's scene

        /// <summary>The region's terrain and water, drawn under <paramref name="parent"/> (the scene's Data root). False if it has no terrain yet.</summary>
        public static bool Place(Transform parent, RegionData region)
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath(region.Id));
            if (data == null) return false;
            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            Setup(go.GetComponent<Terrain>(), region);

            var water = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPath(region.Id));
            if (water != null)
            {
                var placed = (GameObject)PrefabUtility.InstantiatePrefab(water, parent);
                placed.name = "Water";
            }
            return true;
        }

        /// <summary>
        /// Where a region's terrain lies: a square of a power of two tiles (so a height sample sits
        /// on every tile corner) on that size's grid if the region fits in one of its squares (the
        /// open land's regions: their 128-tile cell), else from the region's own corner.
        /// </summary>
        public static (int x, int y, int size) Square(RegionData region)
        {
            int size = 32;
            while (size < Mathf.Max(region.Width, region.Height)) size *= 2;
            int ax = Mathf.FloorToInt(region.OriginX / (float)size) * size, ay = Mathf.FloorToInt(region.OriginY / (float)size) * size;
            bool fits = region.OriginX + region.Width <= ax + size && region.OriginY + region.Height <= ay + size;
            return fits ? (ax, ay, size) : (region.OriginX, region.OriginY, size);
        }

        /// <summary>How every terrain is set up: on its square, joined to its neighbours (the same group), drawn instanced.</summary>
        private static void Setup(Terrain terrain, RegionData region)
        {
            (int x, int y, int _) = Square(region);
            terrain.transform.position = new Vector3(x, Low, y);
            terrain.allowAutoConnect = true;
            terrain.groupingID = 0;
            terrain.drawInstanced = true;
            terrain.drawTreesAndFoliage = false;
        }

        private static void OnChanged(Terrain terrain)
        {
            if (terrain == null || EditorApplication.isPlaying || !AssetDatabase.GetAssetPath(terrain.terrainData).StartsWith(Folder + "/")) return;
            // So Ctrl+S saves it: the region's scene counts as changed, and saving it writes the ground.
            Scene scene = terrain.gameObject.scene;
            if (scene.IsValid() && !scene.isDirty) EditorSceneManager.MarkSceneDirty(scene);
        }

        // ------------------------------------------------------------------ the ground into the data

        /// <summary>
        /// Writes the region's ground from its terrain into its data (tiles and heights, for the
        /// server), saving the terrain first. True if the data changed.
        /// </summary>
        public static bool WriteGround(RegionData region)
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath(region.Id));
            if (data == null || GroundEdit.Tiles(region) == null) return false;
            AssetDatabase.SaveAssetIfDirty(data);
            var reader = new Reader(data, region, Table);
            return WorldGround.Apply(region, Table, reader.Ground, reader.Corner);
        }

        /// <summary>A terrain read the way the server needs it: a ground per tile and a height per corner.</summary>
        private sealed class Reader
        {
            private readonly TerrainData _data;
            private readonly RegionData _region;
            private readonly float[,,] _alpha;
            private readonly bool[,] _solid;
            private readonly int _res, _holes;
            private readonly string[] _layers;
            private readonly int _x0, _y0, _size;

            public Reader(TerrainData data, RegionData region, GroundTable table)
            {
                _data = data;
                _region = region;
                (_x0, _y0, _size) = Square(region);
                _res = data.alphamapResolution;
                _alpha = data.alphamapLayers > 0 ? data.GetAlphamaps(0, 0, _res, _res) : new float[_res, _res, 0];
                _holes = data.holesResolution;
                _solid = data.GetHoles(0, 0, _holes, _holes);
                // Only grounds count; stones and layers no ground of the table names are looks only.
                _layers = data.terrainLayers.Select(l => l != null && table.Grounds.ContainsKey(l.name) ? l.name : null).ToArray();
            }

            public float? Corner(int x, int y)
            {
                if (x < _region.OriginX || y < _region.OriginY || x > _region.OriginX + _region.Width || y > _region.OriginY + _region.Height) return null;
                float u = (x - _x0) / (float)_size, v = (y - _y0) / (float)_size;
                return Low + _data.GetInterpolatedHeight(u, v);
            }

            /// <summary>The ground painted heaviest at the tile's middle, or null for a hole.</summary>
            public string Ground(int x, int y)
            {
                if (x < _region.OriginX || y < _region.OriginY || x >= _region.OriginX + _region.Width || y >= _region.OriginY + _region.Height) return null;
                float u = (x + 0.5f - _x0) / _size, v = (y + 0.5f - _y0) / _size;
                if (!_solid[Mathf.Clamp((int)(v * _holes), 0, _holes - 1), Mathf.Clamp((int)(u * _holes), 0, _holes - 1)]) return null;
                // Bilinear between the paint texels round the middle.
                float fx = u * _res - 0.5f, fy = v * _res - 0.5f;
                int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, _res - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, _res - 1);
                int x1 = Mathf.Min(x0 + 1, _res - 1), y1 = Mathf.Min(y0 + 1, _res - 1);
                float tx = Mathf.Clamp01(fx - x0), ty = Mathf.Clamp01(fy - y0);
                string best = null;
                float most = -1f;
                for (int l = 0; l < _layers.Length; l++)
                {
                    if (_layers[l] == null) continue;
                    float w = Mathf.Lerp(Mathf.Lerp(_alpha[y0, x0, l], _alpha[y0, x1, l], tx), Mathf.Lerp(_alpha[y1, x0, l], _alpha[y1, x1, l], tx), ty);
                    if (w > most) { most = w; best = _layers[l]; }
                }
                return best;
            }
        }

        [MenuItem("R'th/World/Terrain/Write All Ground To Data", priority = 23)]
        public static void WriteAll()
        {
            var written = new List<string>();
            foreach (string path in RegionFiles.RegionPaths(Path.Combine(WorldSync.RepoRoot, "world", "regions")))
            {
                RegionData region = RegionFiles.LoadRegion(path);
                if (!WriteGround(region)) continue;
                RegionFiles.SaveRegion(region, path);
                written.Add(region.Id);
            }
            foreach (string id in written) WorldWorkspace.Load(WorldSync.RepoRoot, id).Export();
            Debug.Log($"[R'th] The ground of {written.Count} regions was written from their terrains: {string.Join(", ", written.Take(12))}{(written.Count > 12 ? "..." : "")}.");
        }

        public static void WriteAllFromCommandLine() => Batch(WriteAll);

        // ------------------------------------------------------------------ making them (once, from the data)

        [MenuItem("R'th/World/Terrain/Create Missing Terrains", priority = 20)]
        private static void CreateMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Create();
        }

        public static void CreateFromCommandLine() => Batch(Create);

        private static void Batch(System.Action run)
        {
            try
            {
                run();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[R'th] " + e);
                EditorApplication.Exit(1);
            }
        }

        public static void Create()
        {
            GroundTable table = Table;
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", Folder));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", WaterFolder));
            var regions = RegionFiles.RegionPaths(Path.Combine(WorldSync.RepoRoot, "world", "regions")).Select(RegionFiles.LoadRegion)
                .Where(r => GroundEdit.Tiles(r) != null).ToList();
            Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int n = 0;
            try
            {
                foreach (RegionData region in regions)
                {
                    EditorUtility.DisplayProgressBar("Create Missing Terrains", region.Id, 0.5f);
                    if (AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath(region.Id)) != null) continue; // the terrain is the truth once made
                    var data = new TerrainData();
                    AssetDatabase.CreateAsset(data, DataPath(region.Id));
                    Seed(data, region, table);
                    n++;
                    EditorUtility.SetDirty(data);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Debug.Log($"[R'th] Made {n} terrains in {Folder} for regions that had none.");
        }

        /// <summary>
        /// A region's first terrain, from its data (a new region): its heights, its tiles' grounds
        /// blended softly from tile to tile with each tile's own ground heaviest at its middle, and
        /// holes where it has none. Made once; from then on the terrain is the truth.
        /// </summary>
        private static void Seed(TerrainData data, RegionData region, GroundTable table)
        {
            Piece piece = GroundEdit.Tiles(region);
            int w = region.Width, h = region.Height;
            // A height sample on every tile corner of its square, and a paint texel on every tile.
            (int sqx, int sqy, int size) = Square(region);
            int res = size + 1;
            data.heightmapResolution = res;
            data.alphamapResolution = size;
            data.baseMapResolution = Mathf.Max(64, size);
            data.size = new Vector3(size, High - Low, size);

            // The data's heights on every corner (written from a terrain, or the region's own first heights).
            Heightfield field = TerrainText.ReadOrFlat(region);
            var shaped = new float[(w + 1) * (h + 1)];
            for (int y = 0; y <= h; y++)
                for (int x = 0; x <= w; x++)
                    shaped[y * (w + 1) + x] = field.Corner(region.OriginX + x, region.OriginY + y);
            var heights = new float[res, res];
            for (int j = 0; j < res; j++)
                for (int i = 0; i < res; i++)
                {
                    // Outside the region (the square's spare tiles, holes) the nearest edge's height carries on.
                    int cx = Mathf.Clamp(sqx + i - region.OriginX, 0, w), cy = Mathf.Clamp(sqy + j - region.OriginY, 0, h);
                    heights[j, i] = Mathf.Clamp01((shaped[cy * (w + 1) + cx] - Low) / (High - Low));
                }
            data.SetHeights(0, 0, heights);

            // The grounds and stones it uses, a terrain layer each.
            var layers = new List<string>();
            int LayerOf(string id)
            {
                int i = layers.IndexOf(id);
                if (i < 0) { layers.Add(id); i = layers.Count - 1; }
                return i;
            }
            var groundOf = new Dictionary<string, int>();
            var stoneOf = new Dictionary<string, int>();
            foreach (KeyValuePair<string, TileKind> e in piece.Legend)
                if (table.GroundOf(region, e.Value) is string g) groundOf[e.Key] = LayerOf(g);
            foreach (KeyValuePair<string, StoneKind> e in piece.StoneLegend ?? new SortedDictionary<string, StoneKind>())
                if (table.StoneOf(region, e.Value.Material) is string s) stoneOf[e.Key] = LayerOf(GroundTable.StonePrefix + s);
            data.terrainLayers = layers.Select(LayerAsset).ToArray();

            // Each texel (a tile): its own ground, softened by its neighbours' so no tile's edge shows;
            // its own stays the heaviest, so the tile reads back as it was.
            int ares = size;
            var alpha = new float[ares, ares, Mathf.Max(1, layers.Count)];
            var solid = new bool[ares, ares];
            const float reach = 1.5f;
            for (int j = 0; j < ares; j++)
                for (int i = 0; i < ares; i++)
                {
                    float px = sqx + i + 0.5f, py = sqy + j + 0.5f;
                    solid[j, i] = GroundEdit.At(piece, Mathf.FloorToInt(px), Mathf.FloorToInt(py)) is char own && own != ' ';
                    float total = 0f;
                    for (int ty = Mathf.FloorToInt(py - reach); ty <= Mathf.FloorToInt(py + reach); ty++)
                        for (int tx = Mathf.FloorToInt(px - reach); tx <= Mathf.FloorToInt(px + reach); tx++)
                        {
                            if (!(GroundEdit.At(piece, tx, ty) is char c) || !groundOf.TryGetValue(c.ToString(), out int layer)) continue;
                            float d = Mathf.Max(Mathf.Abs(px - (tx + 0.5f)), Mathf.Abs(py - (ty + 0.5f)));
                            if (d >= reach) continue;
                            float wgt = (1f - d / reach) * (1f - d / reach);
                            float share = 0f;
                            if (GroundEdit.StoneAt(piece, tx, ty) is StoneKind stones && piece.Stones != null)
                            {
                                string sc = piece.Stones[piece.Rect[3] - 1 - ty].Length > tx - piece.Rect[0] ? piece.Stones[piece.Rect[3] - 1 - ty][tx - piece.Rect[0]].ToString() : " ";
                                if (stoneOf.TryGetValue(sc, out int sl)) { share = Mathf.Clamp01(stones.Share); alpha[j, i, sl] += wgt * share; }
                            }
                            alpha[j, i, layer] += wgt * (1f - share);
                            total += wgt;
                        }
                    if (total <= 0f) { alpha[j, i, 0] = 1f; continue; }
                    for (int l = 0; l < layers.Count; l++) alpha[j, i, l] /= total;
                }
            if (layers.Count > 0) data.SetAlphamaps(0, 0, alpha);
            data.SetHoles(0, 0, solid);
        }

        /// <summary>The terrain layer of a ground or stones, made (a softly mottled swatch of its colour) if it isn't there yet.</summary>
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
            string png = $"{LayerFolder}/{layer}.png";
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", png), Swatch(colour, layer.GetHashCode()));
            AssetDatabase.ImportAsset(png);
            asset = new TerrainLayer { diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(png), tileSize = new Vector2(4f, 4f) };
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>A 64-pixel tiling swatch: the colour, gently mottled so a field of it isn't flat.</summary>
        private static byte[] Swatch(Color colour, int seed)
        {
            const int size = 64;
            var rng = new System.Random(seed);
            var coarse = new float[9, 9];
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) coarse[y, x] = (float)rng.NextDouble();
            for (int i = 0; i <= 8; i++) { coarse[8, i] = coarse[0, i % 8]; coarse[i, 8] = coarse[i % 8, 0]; } // tiles seamlessly
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x / 8f, fy = y / 8f;
                    int ix = (int)fx, iy = (int)fy;
                    float tx = fx - ix, ty = fy - iy;
                    float n = Mathf.Lerp(Mathf.Lerp(coarse[iy, ix], coarse[iy, ix + 1], tx), Mathf.Lerp(coarse[iy + 1, ix], coarse[iy + 1, ix + 1], tx), ty);
                    float shade = 0.94f + 0.12f * n + 0.03f * ((float)rng.NextDouble() - 0.5f);
                    pixels[y * size + x] = new Color(colour.r * shade, colour.g * shade, colour.b * shade, 1f);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return png;
        }
    }
}
