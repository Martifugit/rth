using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rth.MapTools;
using Rth.Shared.World;
using Rth.Shared.World.Data;
using Rth.Shared.World.Ground;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// A region's water surface (Assets/World/Water/&lt;region&gt;.prefab), built from where its ground is
    /// painted with a water layer (its tiles in the data) and its terrain's heights. The surface stands
    /// just above the water's level where water is, and dips under the bank where land is; between the
    /// two it follows a softened outline of the water (how much water lies round each corner), so the
    /// waterline curves and runs along diagonals instead of stepping along the tiles. The ground under
    /// it is never changed, so nothing about walking does. Still water comes out as flat rectangles,
    /// running water as one mesh carrying which way it flows. Saving a region scene rebuilds its water.
    /// </summary>
    public static class WorldWater
    {
        /// <summary>How far round a corner (in tiles, to the tiles' middles) its water is counted.</summary>
        private const float Reach = 2f;
        /// <summary>A corner with this much water round it is water; the waterline is where the share crosses it.</summary>
        private const float Line = 0.45f;
        /// <summary>How far the surface stands above the water's level, and the deepest it goes under the bank.</summary>
        private const float Above = 0.04f, Under = 0.35f;
        /// <summary>The bed under open water, as the terrains were first made (it sinks this far there).</summary>
        private const float OpenWaterDip = 0.7f;

        [MenuItem("R'th/World/Terrain/Rebuild All Water", priority = 24)]
        public static void RebuildAll()
        {
            var regions = RegionFiles.RegionPaths(Path.Combine(WorldSync.RepoRoot, "world", "regions")).Select(RegionFiles.LoadRegion).ToList();
            var world = new GroundEdit.WorldTiles(regions);
            int n = 0;
            foreach (RegionData r in regions)
                if (Rebuild(r, world)) n++;
            AssetDatabase.SaveAssets();
            Debug.Log($"[R'th] Rebuilt the water of {n} regions.");
        }

        public static void RebuildAllFromCommandLine()
        {
            try
            {
                RebuildAll();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[R'th] " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Rebuilds one region's water (its neighbours read from disk). True if it has any.</summary>
        public static bool Rebuild(RegionData region)
        {
            var regions = RegionFiles.RegionPaths(Path.Combine(WorldSync.RepoRoot, "world", "regions")).Select(RegionFiles.LoadRegion)
                .Where(r => r.Id != region.Id).Append(region).ToList();
            bool has = Rebuild(region, new GroundEdit.WorldTiles(regions));
            AssetDatabase.SaveAssets();
            return has;
        }

        private static bool Rebuild(RegionData region, GroundEdit.WorldTiles world)
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(WorldTerrains.DataPath(region.Id));
            if (data == null || GroundEdit.Tiles(region) == null) return false;
            int x0 = region.OriginX, y0 = region.OriginY, w = region.Width, h = region.Height;
            (int sx, int sy, int size) = WorldTerrains.Square(region);

            // The water's tiles, over the region and a margin (the neighbours' too, so borders meet).
            const int m = 3;
            int cols = w + 2 * m, rows = h + 2 * m;
            var water = new TileKind[cols * rows];
            var colourOf = new Dictionary<string, string>();
            bool any = false;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int ax = x0 - m + x, ay = y0 - m + y;
                    bool inside = ax >= x0 && ay >= y0 && ax < x0 + w && ay < y0 + h;
                    // The region's own tiles from itself (an instanced place isn't in the world's), the margin from its neighbours.
                    var t = inside ? Own(region, ax, ay) : world.At(ax, ay);
                    if (t.HasValue && t.Value.kind.Water == true)
                    {
                        water[y * cols + x] = t.Value.kind;
                        if (t.Value.materials.TryGetValue(t.Value.kind.Material, out string colour)) colourOf[t.Value.kind.Material] = colour;
                        if (x >= m && y >= m && x < m + w && y < m + h) any = true;
                    }
                }
            if (!any)
            {
                AssetDatabase.DeleteAsset(WorldTerrains.WaterPath(region.Id));
                AssetDatabase.DeleteAsset(MeshPath(region.Id));
                return false;
            }
            bool Wet(int cx, int cy) => cx >= 0 && cy >= 0 && cx < cols && cy < rows && water[cy * cols + cx] != null;

            // Per corner (on the margin's grid): how much water lies round it, and the ground's height.
            int cc = cols + 1, cr = rows + 1;
            var share = new float[cc * cr];
            var ground = new float[cc * cr];
            for (int y = 0; y < cr; y++)
                for (int x = 0; x < cc; x++)
                {
                    float wet = 0f, all = 0f;
                    for (int ty = y - 2; ty < y + 2; ty++)
                        for (int tx = x - 2; tx < x + 2; tx++)
                        {
                            float d = Mathf.Sqrt((tx + 0.5f - x) * (tx + 0.5f - x) + (ty + 0.5f - y) * (ty + 0.5f - y));
                            if (d > Reach) continue;
                            float wgt = 1f - d / (Reach + 0.5f);
                            all += wgt;
                            if (Wet(tx, ty)) wet += wgt;
                        }
                    share[y * cc + x] = all > 0f ? wet / all : 0f;
                    float u = (x0 - m + x - sx) / (float)size, v = (y0 - m + y - sy) / (float)size;
                    ground[y * cc + x] = TerrainText.Lowest + data.GetInterpolatedHeight(Mathf.Clamp01(u), Mathf.Clamp01(v));
                }

            // The water's level at each of its corners: where it meets land the ground's own height
            // there (the bank, never sunk), carried out over the water from the nearest bank; open water
            // no bank reaches stands where the bed was sunk from.
            var level = new float[cc * cr];
            var known = new bool[cc * cr];
            var queue = new Queue<int>();
            bool Touches(int x, int y, bool wetWanted)
            {
                for (int oy = -1; oy <= 0; oy++)
                    for (int ox = -1; ox <= 0; ox++)
                        if (Wet(x + ox, y + oy) == wetWanted) return true;
                return false;
            }
            for (int y = 0; y < cr; y++)
                for (int x = 0; x < cc; x++)
                    if (Touches(x, y, true) && Touches(x, y, false))
                    {
                        int i = y * cc + x;
                        level[i] = ground[i];
                        known[i] = true;
                        queue.Enqueue(i);
                    }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % cc, y = i / cc;
                foreach ((int nx, int ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (nx < 0 || ny < 0 || nx >= cc || ny >= cr) continue;
                    int j = ny * cc + nx;
                    if (known[j] || !Touches(nx, ny, true)) continue;
                    level[j] = level[i];
                    known[j] = true;
                    queue.Enqueue(j);
                }
            }
            for (int i = 0; i < level.Length; i++) if (!known[i]) level[i] = ground[i] + OpenWaterDip;

            // The surface at each corner: above the level on the water's side of the line, under the bank beyond it.
            float Surface(int i) => share[i] >= Line
                ? level[i] + Above
                : Mathf.Min(level[i] + Above, ground[i]) - Under * (Line - share[i]) / Line - 0.02f;

            // Which way it runs: down the level, smoothed.
            var fx = new float[cc * cr];
            var fy = new float[cc * cr];
            for (int y = 1; y < cr - 1; y++)
                for (int x = 1; x < cc - 1; x++)
                {
                    int i = y * cc + x;
                    fx[i] = -(level[i + 1] - level[i - 1]) / 2f;
                    fy[i] = -(level[i + cc] - level[i - cc]) / 2f;
                }

            // A square for every tile of the region with water at any corner, in its water's material.
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var bySub = new Dictionary<string, List<int>>();
            var materialOf = new Dictionary<string, Material>();
            for (int ty = m; ty < m + h; ty++)
                for (int tx = m; tx < m + w; tx++)
                {
                    int[] corners = { ty * cc + tx, (ty + 1) * cc + tx, (ty + 1) * cc + tx + 1, ty * cc + tx + 1 };
                    if (corners.All(i => share[i] < Line * 0.5f)) continue;
                    TileKind kind = water[ty * cols + tx] ?? Nearest(water, cols, rows, tx, ty);
                    if (kind == null) continue;
                    string key = kind.Material;
                    if (!bySub.TryGetValue(key, out List<int> tris))
                    {
                        bySub[key] = tris = new List<int>();
                        materialOf[key] = Material(colourOf.TryGetValue(key, out string colour) ? colour : null);
                    }
                    int b = verts.Count;
                    foreach (int i in corners)
                    {
                        int cx = i % cc, cy = i / cc;
                        verts.Add(new Vector3(x0 - m + cx, Surface(i), y0 - m + cy));
                        var flow = new Vector2(fx[i], fy[i]);
                        float speed = Mathf.Clamp01(flow.magnitude * 25f);
                        Vector2 dir = flow.sqrMagnitude > 1e-10f ? flow.normalized : Vector2.zero;
                        float shallow = Mathf.Clamp01(1f - (share[i] - Line) / 0.4f);
                        colors.Add(new Color(shallow, dir.x * 0.5f + 0.5f, dir.y * 0.5f + 0.5f, speed));
                    }
                    tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                }

            var built = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            built.SetVertices(verts);
            built.SetColors(colors);
            List<string> keys = bySub.Keys.ToList();
            built.subMeshCount = keys.Count;
            for (int s = 0; s < keys.Count; s++) built.SetTriangles(bySub[keys[s]], s);
            Mesh mesh = Simplify(built);
            Object.DestroyImmediate(built);
            mesh.name = region.Id + " water";

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", WorldTerrains.WaterFolder));
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(region.Id));
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
                EditorUtility.SetDirty(mesh);
            }
            else AssetDatabase.CreateAsset(mesh, MeshPath(region.Id));

            var go = new GameObject("Water") { layer = WorldTerrains.WaterLayer };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = keys.Select(k => materialOf[k]).ToArray();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<MapWater>();
            PrefabUtility.SaveAsPrefabAsset(go, WorldTerrains.WaterPath(region.Id));
            Object.DestroyImmediate(go);
            return true;
        }

        /// <summary>A tile of the region's own ground: its kind and its materials' colours, or null.</summary>
        private static (TileKind kind, IReadOnlyDictionary<string, string> materials, char letter)? Own(RegionData region, int x, int y)
        {
            Piece piece = GroundEdit.Tiles(region);
            if (!(GroundEdit.At(piece, x, y) is char c) || c == ' ' || !piece.Legend.TryGetValue(c.ToString(), out TileKind kind)) return null;
            var materials = new Dictionary<string, string>();
            if (region.Materials.TryGetValue(kind.Material, out string colour)) materials[kind.Material] = colour;
            return (kind, materials, c);
        }

        private static string MeshPath(string region) => $"{WorldTerrains.WaterFolder}/{region}.asset";

        /// <summary>The water kind of the nearest water tile (for the bank's tiles the surface runs into).</summary>
        private static TileKind Nearest(TileKind[] water, int cols, int rows, int tx, int ty)
        {
            for (int r = 1; r <= 3; r++)
                for (int y = ty - r; y <= ty + r; y++)
                    for (int x = tx - r; x <= tx + r; x++)
                        if (x >= 0 && y >= 0 && x < cols && y < rows && water[y * cols + x] != null) return water[y * cols + x];
            return null;
        }

        /// <summary>The water material for a kind: deep in its colour, the shallows lighter (Assets/World/Water/Materials).</summary>
        private static Material Material(string colour)
        {
            Color deep = colour != null && Palette.TryParse(colour, out Color c) ? c : new Color(0.22f, 0.36f, 0.48f);
            string folder = WorldTerrains.WaterFolder + "/Materials";
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", folder));
            string path = $"{folder}/Water_{ColorUtility.ToHtmlStringRGB(deep)}.mat";
            var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (asset != null) return asset;
            asset = new Material(Resources.Load<Shader>("Shaders/Water")) { name = Path.GetFileNameWithoutExtension(path) };
            asset.SetColor("_Color", deep);
            // Shallows lighten toward a pale green, less so for dark water (a pale band over black water reads as mist).
            float bright = Mathf.Clamp(deep.grayscale * 4f, 0.2f, 1f);
            asset.SetColor("_Shallow", Color.Lerp(deep, new Color(0.62f, 0.74f, 0.66f), 0.45f * bright));
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>
        /// The surface as few pieces as it takes: still water as flat rectangles (its shallows found by
        /// the shader from the depth), the rest as one mesh sharing its corners. The surface as built has
        /// a square of four corners for every tile, in each submesh.
        /// </summary>
        private static Mesh Simplify(Mesh source)
        {
            Vector3[] v = source.vertices;
            Color[] c = source.colors;
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var shared = new Dictionary<(int, int, int), int>();
            var submeshes = new List<List<int>>();
            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] tris = source.GetTriangles(s);
                var tris2 = new List<int>();
                var still = new Dictionary<int, HashSet<(int x, int y)>>();
                for (int t = 0; t < tris.Length; t += 6)
                {
                    int a = tris[t];
                    bool level = true;
                    for (int k = 1; k < 4; k++) level &= Mathf.Abs(v[a + k].y - v[a].y) < 0.005f;
                    for (int k = 0; k < 4; k++) level &= c[a + k].a < 0.01f && c[a + k].r < 0.01f;
                    if (level)
                    {
                        int hgt = Mathf.RoundToInt(v[a].y * 100f);
                        if (!still.TryGetValue(hgt, out var tiles)) still[hgt] = tiles = new HashSet<(int, int)>();
                        tiles.Add((Mathf.RoundToInt(Mathf.Min(v[a].x, v[a + 2].x)), Mathf.RoundToInt(Mathf.Min(v[a].z, v[a + 2].z))));
                        continue;
                    }
                    int Corner(int i)
                    {
                        var key = (Mathf.RoundToInt(v[i].x), Mathf.RoundToInt(v[i].z), s);
                        if (shared.TryGetValue(key, out int at)) return at;
                        verts.Add(v[i]);
                        colors.Add(c[i]);
                        return shared[key] = verts.Count - 1;
                    }
                    for (int k = 0; k < 6; k++) tris2.Add(Corner(tris[t + k]));
                }
                foreach (KeyValuePair<int, HashSet<(int x, int y)>> e in still)
                {
                    float y = e.Key / 100f;
                    var left = new HashSet<(int x, int y)>(e.Value);
                    foreach ((int x, int z) start in e.Value.OrderBy(p => p.y).ThenBy(p => p.x))
                    {
                        if (!left.Contains(start)) continue;
                        int w = 1;
                        while (left.Contains((start.x + w, start.z))) w++;
                        int d = 1;
                        while (Enumerable.Range(0, w).All(i => left.Contains((start.x + i, start.z + d)))) d++;
                        for (int j = 0; j < d; j++) for (int i = 0; i < w; i++) left.Remove((start.x + i, start.z + j));
                        int b = verts.Count;
                        verts.Add(new Vector3(start.x, y, start.z));
                        verts.Add(new Vector3(start.x, y, start.z + d));
                        verts.Add(new Vector3(start.x + w, y, start.z + d));
                        verts.Add(new Vector3(start.x + w, y, start.z));
                        for (int k = 0; k < 4; k++) colors.Add(new Color(0f, 0.5f, 0.5f, 0f));
                        tris2.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                    }
                }
                submeshes.Add(tris2);
            }
            var mesh = new Mesh { indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetNormals(Enumerable.Repeat(Vector3.up, verts.Count).ToList());
            mesh.subMeshCount = submeshes.Count;
            for (int s = 0; s < submeshes.Count; s++) mesh.SetTriangles(submeshes[s], s);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
