using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rth.MapTools;
using Rth.MapTools.Editor;
using Rth.Shared.World;
using Rth.Shared.World.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Sync World (docs/workflow.md): turns the world data (world/regions) into scenes and server
    /// maps, and the scenes' hand-made content back into data. For each region it:
    ///  1. opens (or creates) the region's scene and applies the region's header to its MapArea;
    ///  2. redraws the Data root from the region's pieces;
    ///  3. reads the Handmade root's collision, objects, creatures and spawn into &lt;id&gt;.handmade.json;
    ///  4. exports the server's map files through the C# compiler (the same one tools/world uses);
    ///  5. checks that exporting the whole scene the Unity way gives exactly the same map;
    ///  6. writes the snapshot and screenshots (world/snapshots, world/screens).
    /// Saving a region scene writes moves, turns and deletions of data pieces back to the data and
    /// re-exports, so the Editor and the data never disagree for long.
    /// </summary>
    [InitializeOnLoad]
    public static class WorldSync
    {
        /// <summary>True while Sync World is saving scenes itself (the save hook then stays out of the way).</summary>
        private static bool _busy;

        static WorldSync()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
            MapExporter.DataExport = ExportIfData;
        }

        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        public static string RegionPath(string id) => Path.Combine(RepoRoot, "world", "regions", id + ".json");

        // ------------------------------------------------------------------ menus and batch mode

        [MenuItem("R'th/World/Sync World", priority = 0)]
        private static void SyncAllMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Report report = SyncAll(null, screenshots: true);
            EditorUtility.DisplayDialog("Sync World", report.Summary(), "OK");
        }

        [MenuItem("R'th/World/Sync World (no screenshots)", priority = 1)]
        private static void SyncQuickMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Report report = SyncAll(null, screenshots: false);
            EditorUtility.DisplayDialog("Sync World", report.Summary(), "OK");
        }

        /// <summary>Every region, changed or not: after a Unity upgrade, or when a scene looks wrong.</summary>
        [MenuItem("R'th/World/Sync World (everything)", priority = 2)]
        private static void SyncEverythingMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Report report = SyncAll(null, screenshots: false, everything: true);
            EditorUtility.DisplayDialog("Sync World", report.Summary(), "OK");
        }

        /// <summary>
        /// For batch mode (tools/unity-sync.ps1): syncs every region that changed, or those after
        /// -rthRegions (comma separated); -rthAll syncs every region; -rthNoScreens skips the
        /// screenshots. Exits 1 if anything failed.
        /// </summary>
        public static void SyncFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-rthRegions");
            string[] only = i >= 0 && i + 1 < args.Length ? args[i + 1].Split(',') : null;
            Report report = SyncAll(only, screenshots: System.Array.IndexOf(args, "-rthNoScreens") < 0,
                everything: System.Array.IndexOf(args, "-rthAll") >= 0);
            EditorApplication.Exit(report.Failed ? 1 : 0);
        }

        // ------------------------------------------------------------------ sync

        public sealed class Report
        {
            public readonly List<string> Lines = new List<string>();
            public bool Failed;

            public void Info(string s)
            {
                Lines.Add(s);
                Debug.Log("[R'th] Sync: " + s);
            }

            public void Error(string s)
            {
                Failed = true;
                Lines.Add("ERROR " + s);
                Debug.LogError("[R'th] Sync: " + s);
            }

            public string Summary() => (Failed ? "Sync World found problems:\n\n" : "Sync World is done.\n\n") +
                                       string.Join("\n", Lines.Where(l => l.StartsWith("ERROR") || l.StartsWith("synced") || l.StartsWith("unchanged")).Take(30));
        }

        /// <summary>
        /// Syncs the regions that changed since this machine last synced them (<see cref="SyncState"/>),
        /// or those in <paramref name="only"/>, or with <paramref name="everything"/> all of them.
        /// </summary>
        public static Report SyncAll(string[] only, bool screenshots, bool everything = false)
        {
            var report = new Report();
            WorldWorkspace ws = WorldWorkspace.Load(RepoRoot);
            foreach (string e in ws.LoadErrors) report.Error(e);
            string[] scenesBefore = Enumerable.Range(0, SceneManager.sceneCount).Select(n => SceneManager.GetSceneAt(n).path).Where(p => !string.IsNullOrEmpty(p)).ToArray();
            SyncState state = SyncState.Load();
            int unchanged = 0;
            _busy = true;
            try
            {
                foreach (WorldWorkspace.Entry entry in ws.Regions)
                {
                    if (only != null && !only.Contains(entry.Region.Id)) continue;
                    if (only == null && !everything && state.Unchanged(entry, ws))
                    {
                        unchanged++;
                        continue;
                    }
                    int errorsBefore = report.Lines.Count(l => l.StartsWith("ERROR"));
                    SyncRegion(entry, report, screenshots);
                    if (report.Lines.Count(l => l.StartsWith("ERROR")) == errorsBefore) state.Record(entry);
                    else state.Forget(entry.Region.Id);
                    state.Save(); // after each region, so a sync stopped half way keeps what it did
                }
            }
            finally
            {
                _busy = false;
            }
            if (unchanged > 0) report.Info($"unchanged since the last sync: {unchanged} regions (R'th > World > Sync World (everything) redoes them)");

            ws = WorldWorkspace.Load(RepoRoot);
            var warnings = new List<string>();
            foreach (string e in ws.Check(warnings)) report.Error(e);
            foreach (string w in warnings) report.Info("warning: " + w);
            WorldSnapshot.WriteIndex(ws);
            AssetDatabase.SaveAssets();
            if (scenesBefore.Length > 0 && !Application.isBatchMode)
            {
                EditorSceneManager.OpenScene(scenesBefore[0], OpenSceneMode.Single);
                foreach (string s in scenesBefore.Skip(1)) EditorSceneManager.OpenScene(s, OpenSceneMode.Additive);
            }
            report.Info(report.Failed ? "Sync World finished with errors." : "Sync World finished cleanly.");
            File.WriteAllText(Path.Combine(Application.dataPath, "..", "Temp", "rth-sync-report.txt"), string.Join("\n", report.Lines) + "\n");
            return report;
        }

        public static void SyncRegion(WorldWorkspace.Entry entry, Report report, bool screenshots)
        {
            RegionData region = entry.Region;
            // Errors that the hand-made content can't fix stop the sync; the rest are checked after capturing it.
            var pieceErrors = RegionCompiler.Compile(region, new HandmadeData()).Errors.Where(e => !e.Contains("is blocked")).ToList();
            if (pieceErrors.Count > 0)
            {
                foreach (string e in pieceErrors) report.Error(region.Id + ": " + e);
                report.Error(region.Id + ": not synced until its data compiles.");
                return;
            }
            if (string.IsNullOrWhiteSpace(region.Scene) || !region.Scene.StartsWith("Assets/") || !region.Scene.EndsWith(".unity"))
            {
                report.Error(region.Id + ": scene must be a path like Assets/Scenes/Name.unity.");
                return;
            }

            MoveSceneIfMoved(region, report);
            // An open scene is used as it is (it may hold edits not yet saved, such as a conversion's).
            Scene scene = SceneManager.GetSceneByPath(region.Scene);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = File.Exists(Path.Combine(Application.dataPath, "..", region.Scene))
                    ? EditorSceneManager.OpenScene(region.Scene, OpenSceneMode.Single)
                    : NewRegionScene(region, report);
            MapArea area = FindArea(scene, region, report);
            if (area == null) return;

            ApplyHeader(region, area);
            (DataRoot data, HandmadeRoot handmade) = EnsureRoots(area);
            PieceBuilder.Build(region, area, data);
            Capture(region, area, data, report);
            EditorSceneManager.MarkSceneDirty(scene);
            bool wasBusy = _busy;
            _busy = true; // the save hook would only redo what this just did
            try
            {
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                _busy = wasBusy;
            }
            MapExporter.AddSceneToBuild(scene.path);

            ExportAndVerify(region.Id, area, report);
            // Generated regions (the blockout) are pictured by `tools/world render`; three screenshots each would only bloat the repository.
            WorldSnapshot.Write(region, area, data, handmade, screenshots && region.Generated == null, report);
            report.Info($"synced {region.Id}: {region.Pieces.Count} pieces, scene {region.Scene}");
        }

        /// <summary>
        /// The data names a scene path that doesn't exist yet, but a scene of the same name exists
        /// elsewhere (a region taken over from the generated land moves to its area's folder):
        /// move that scene, so its hand work comes along and it keeps its place in the build.
        /// </summary>
        private static void MoveSceneIfMoved(RegionData region, Report report)
        {
            if (File.Exists(Path.Combine(Application.dataPath, "..", region.Scene))) return;
            string name = Path.GetFileNameWithoutExtension(region.Scene);
            string from = AssetDatabase.FindAssets(name + " t:Scene").Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
            if (from == null) return;
            Scene open = SceneManager.GetSceneByPath(from);
            if (open.IsValid() && open.isLoaded && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(open, true);
            string folder = Path.GetDirectoryName(region.Scene).Replace('\\', '/');
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", folder));
            AssetDatabase.Refresh();
            string error = AssetDatabase.MoveAsset(from, region.Scene);
            if (string.IsNullOrEmpty(error)) report.Info($"{region.Id}: moved {from} to {region.Scene}");
            else report.Error($"{region.Id}: couldn't move {from} to {region.Scene}: {error}");
        }

        private static Scene NewRegionScene(RegionData region, Report report)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(string.IsNullOrWhiteSpace(region.Title) ? region.Id : region.Title);
            root.AddComponent<MapArea>();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Application.dataPath, "..", region.Scene)));
            EditorSceneManager.SaveScene(scene, region.Scene);
            report.Info($"{region.Id}: created {region.Scene}");
            return scene;
        }

        private static MapArea FindArea(Scene scene, RegionData region, Report report)
        {
            MapArea[] areas = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapArea>(true)).ToArray();
            if (areas.Length == 1) return areas[0];
            report.Error($"{region.Id}: {region.Scene} must hold exactly one MapArea (it has {areas.Length}).");
            return null;
        }

        /// <summary>The data decides the region's rectangle, title, music and light.</summary>
        private static void ApplyHeader(RegionData region, MapArea area)
        {
            area.exportName = region.Id;
            area.title = region.Title;
            area.origin = new Vector2Int(region.OriginX, region.OriginY);
            area.size = new Vector2Int(region.Width, region.Height);
            area.plane = region.Plane;
            area.instanced = region.Instanced;
            area.music = region.Music ?? "";
            area.ownAtmosphere = region.Atmosphere != null;
            if (region.Atmosphere != null)
            {
                RegionAtmosphere a = region.Atmosphere;
                area.sunIntensity = a.SunIntensity;
                area.sunColor = Hex(a.Sun, area.sunColor);
                area.ambientSky = Hex(a.AmbientSky, area.ambientSky);
                area.ambientEquator = Hex(a.AmbientEquator, area.ambientEquator);
                area.ambientGround = Hex(a.AmbientGround, area.ambientGround);
                area.fog = Hex(a.Fog, area.fog);
                area.fogStart = a.FogStart;
                area.fogEnd = a.FogEnd;
            }
        }

        private static Color Hex(string s, Color fallback) => ColorUtility.TryParseHtmlString(s.StartsWith("#") ? s : "#" + s, out Color c) ? c : fallback;

        /// <summary>The Data root first and the Handmade root second under the MapArea; anything else is moved into Handmade.</summary>
        public static (DataRoot, HandmadeRoot) EnsureRoots(MapArea area)
        {
            DataRoot data = area.GetComponentInChildren<DataRoot>(true);
            HandmadeRoot handmade = area.GetComponentInChildren<HandmadeRoot>(true);
            if (data == null) data = WorldKit.Group(area.transform, "Data").gameObject.AddComponent<DataRoot>();
            if (handmade == null) handmade = WorldKit.Group(area.transform, "Handmade").gameObject.AddComponent<HandmadeRoot>();
            data.transform.SetSiblingIndex(0);
            handmade.transform.SetSiblingIndex(1);
            for (int i = area.transform.childCount - 1; i >= 2; i--)
                area.transform.GetChild(i).SetParent(handmade.transform, true);
            return (data, handmade);
        }

        /// <summary>Reads everything outside the Data root (the hand-made content) into the region's handmade.json.</summary>
        public static void Capture(RegionData region, MapArea area, DataRoot data, Report report)
        {
            bool wasActive = data.gameObject.activeSelf;
            data.gameObject.SetActive(false);
            try
            {
                CollisionLayers layers = MapExporter.BuildLayers(area, area.transform);
                var handmade = new HandmadeData
                {
                    Objects = MapExporter.Objects(area, area.transform),
                    Npcs = MapExporter.Npcs(area, area.transform),
                    Spawn = MapExporter.Spawn(area, area.transform),
                };
                for (int p = 0; p < layers.Planes; p++)
                {
                    LayerText text = layers.ToText(p);
                    if (text.Walls.Count > 0 || text.Rows.Any(r => r.Length > 0)) handmade.Planes.Add(text);
                }
                string path = RegionPath(region.Id);
                bool empty = handmade.Planes.Count == 0 && handmade.Objects.Count == 0 && handmade.Npcs.Count == 0 && handmade.Spawn == null;
                string handmadePath = RegionFiles.HandmadePathFor(path);
                if (empty)
                {
                    if (File.Exists(handmadePath)) File.Delete(handmadePath);
                }
                else if (RegionFiles.WriteIfChanged(handmadePath, RegionFiles.Format(handmade)))
                    report?.Info($"{region.Id}: hand-made content changed ({handmade.Objects.Count} objects, {handmade.Npcs.Count} creatures)");
            }
            finally
            {
                data.gameObject.SetActive(wasActive);
            }
        }

        /// <summary>
        /// Exports the region's map files from its data (through the C# compiler) and checks that
        /// the Unity exporter, reading the whole scene, finds the same collision, objects and creatures.
        /// </summary>
        public static void ExportAndVerify(string id, MapArea area, Report report)
        {
            WorldWorkspace ws = WorldWorkspace.Load(RepoRoot, id); // this region alone: the whole world takes seconds
            WorldWorkspace.Entry entry = ws.Find(id);
            if (entry == null || entry.Compiled.Errors.Count > 0)
            {
                foreach (string e in entry?.Compiled.Errors ?? new List<string> { "region not found" }) report.Error(id + ": " + e);
                return;
            }
            foreach (string path in ws.Export()) report.Info("wrote " + Path.GetFileName(path));

            MapData unity = MapExporter.Build(area);
            MapData data = entry.Compiled.Map;
            var differences = new List<string>();
            if (unity.Planes != data.Planes) differences.Add($"planes: scene {unity.Planes}, data {data.Planes}");
            for (int p = 0; p < Mathf.Min(unity.Planes, data.Planes); p++)
                for (int y = data.OriginY; y < data.OriginY + data.Height; y++)
                    for (int x = data.OriginX; x < data.OriginX + data.Width; x++)
                        if (unity.GetFlags(p, x, y) != data.GetFlags(p, x, y))
                            differences.Add($"({x}, {y}) plane {p}: scene {unity.GetFlags(p, x, y)}, data {data.GetFlags(p, x, y)}");
            if (unity.Spawn.X != data.Spawn.X || unity.Spawn.Y != data.Spawn.Y || unity.SpawnPlane != data.SpawnPlane)
                differences.Add($"spawn: scene {unity.Spawn}, data {data.Spawn}");
            string Key(ObjectEntry o) => $"{o.Type}@{o.Plane},{o.X},{o.Y}>{o.ToPlane},{o.ToX},{o.ToY}";
            string NKey(NpcEntry n) => $"{n.Npc}@{n.Plane},{n.X},{n.Y}~{n.Wander}";
            if (!Same(MapExporter.Objects(area, area.transform).Select(Key), entry.Compiled.Objects.Select(Key))) differences.Add("objects differ");
            if (!Same(MapExporter.Npcs(area, area.transform).Select(NKey), entry.Compiled.Npcs.Select(NKey))) differences.Add("creatures differ");
            if (differences.Count == 0) return;
            report.Error($"{id}: the scene and the data disagree on {differences.Count} things (first: {string.Join("; ", differences.Take(8))}). " +
                         "A piece's look may collide where its data doesn't, or the kit and the compiler differ.");
        }

        private static bool Same(IEnumerable<string> a, IEnumerable<string> b) => a.OrderBy(s => s).SequenceEqual(b.OrderBy(s => s));

        // ------------------------------------------------------------------ Play, Export Map and saving

        /// <summary>Export Map and Play on a region with data: capture the hand-made content and export through the data.</summary>
        private static string ExportIfData(MapArea area)
        {
            if (area.GetComponentInChildren<DataRoot>(true) == null || !File.Exists(RegionPath(area.exportName))) return null;
            DataRoot data = area.GetComponentInChildren<DataRoot>(true);
            RegionData region = RegionFiles.LoadRegion(RegionPath(area.exportName));
            var report = new Report();
            Capture(region, area, data, report);
            ExportAndVerify(region.Id, area, report);
            return Path.Combine(RepoRoot, "server", "data", "maps", region.Id + ".rthmap");
        }

        /// <summary>
        /// Saving a region scene: data pieces moved (by whole tiles), turned or deleted in the Editor
        /// are written back to the region's data; then the hand-made content is captured and the
        /// maps exported.
        /// </summary>
        private static void OnSceneSaving(Scene scene, string path)
        {
            if (_busy) return;
            foreach (MapArea area in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapArea>(true)))
            {
                DataRoot data = area.GetComponentInChildren<DataRoot>(true);
                string regionPath = RegionPath(area.exportName);
                if (data == null || !File.Exists(regionPath)) continue;
                try
                {
                    RegionData region = RegionFiles.LoadRegion(regionPath);
                    List<string> changes = WriteBack(region, data);
                    if (changes.Count > 0)
                    {
                        RegionFiles.SaveRegion(region, regionPath);
                        Debug.Log($"[R'th] {region.Id}: wrote back to world/regions/{region.Id}.json: {string.Join("; ", changes)}");
                    }
                    var report = new Report();
                    Capture(region, area, data, report);
                    ExportAndVerify(region.Id, area, report);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[R'th] {area.exportName}: couldn't write the scene back to its data: {e.Message}");
                }
            }
        }

        /// <summary>Applies the Editor's moves, turns and deletions of data pieces to the region's data. Returns what changed.</summary>
        public static List<string> WriteBack(RegionData region, DataRoot data)
        {
            var changes = new List<string>();
            var present = new HashSet<string>();
            foreach (DataPiece tag in data.GetComponentsInChildren<DataPiece>(true))
            {
                present.Add(tag.pieceId);
                Piece piece = region.Find(tag.pieceId);
                if (piece == null) continue;
                Vector3 moved = tag.transform.position - tag.placedPosition;
                int dx = Mathf.RoundToInt(moved.x), dy = Mathf.RoundToInt(moved.z);
                if (Mathf.Abs(moved.x - dx) > 0.05f || Mathf.Abs(moved.z - dy) > 0.05f)
                    Debug.LogWarning($"[R'th] {region.Id}: '{piece.Id}' was moved by a fraction of a tile; written back as ({dx}, {dy}).", tag);
                if (dx != 0 || dy != 0)
                {
                    piece.Translate(dx, dy);
                    changes.Add($"moved {piece.Id} by ({dx}, {dy})");
                }
                float turned = Mathf.DeltaAngle(tag.placedYaw, tag.transform.eulerAngles.y);
                if (tag.canTurn && Mathf.Abs(turned) > 0.5f)
                {
                    float rot = Mathf.Repeat((piece.Rot ?? 0f) + turned, 360f);
                    piece.Rot = Mathf.Round(rot * 10f) / 10f;
                    if (piece.Rot == 0f) piece.Rot = null;
                    changes.Add($"turned {piece.Id} to {piece.Rot ?? 0f}°");
                }
                tag.placedPosition = tag.transform.position;
                tag.placedYaw = tag.transform.eulerAngles.y;
            }
            foreach (string id in data.builtIds.ToList())
            {
                if (present.Contains(id)) continue;
                if (region.Pieces.RemoveAll(p => p.Id == id) > 0) changes.Add("deleted " + id);
                data.builtIds.Remove(id);
            }

            // Ground sculpted in the Editor: its heights become the region's terrain.
            foreach (TileGround ground in data.GetComponentsInChildren<TileGround>(true))
            {
                if (!ground.sculpted) continue;
                Heightfield field = TerrainText.ReadOrFlat(region);
                Heightfield sculpted = ground.Field;
                for (int y = ground.originY; y <= ground.originY + ground.height; y++)
                    for (int x = ground.originX; x <= ground.originX + ground.width; x++)
                        field.SetCorner(x, y, TerrainText.Hundredths(sculpted.Corner(x, y)) * TerrainText.Step);
                region.Terrain = TerrainText.Write(field, region.Terrain?.CliffStep ?? ground.cliffStep);
                ground.sculpted = false;
                changes.Add("sculpted the ground");
            }

            // Hand work on a generated region takes it over, so the generator never undoes it (docs/world-guide.md 1).
            if (changes.Count > 0 && region.Generated != null)
            {
                changes.Add($"taken over from the {region.Generated} generator");
                region.Generated = null;
            }
            return changes;
        }
    }
}
