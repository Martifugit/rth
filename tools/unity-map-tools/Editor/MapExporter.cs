using System.Collections.Generic;
using System.IO;
using Rth.Shared.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rth.MapTools.Editor
{
    /// <summary>
    /// Turns a <see cref="MapArea"/> in a scene into the collision map the server loads. This is
    /// the only path from scene to server: never edit the .rthmap by hand.
    ///
    /// Rules, per tile (x, y) covering world X [x, x+1], Z [y, y+1]:
    ///  - No collider under the tile centre (ignoring walls and blockers): blocked, there is no ground.
    ///  - Overlapped by the colliders of a <see cref="MapBlocker"/>: blocked.
    ///  - <see cref="MapWall"/> colliders snap to the nearest grid line and block those tile edges.
    ///  - A tile of a <see cref="MapTerrain"/> steeper than its cliff step (a cliff face), or marked impassable: blocked.
    /// </summary>
    public static class MapExporter
    {
        /// <summary>How far bounds must reach into a tile to count as covering it.</summary>
        private const float OverlapEpsilon = 0.1f;

        [MenuItem("R'th/Export Map", priority = 1)]
        private static void ExportMenu()
        {
            MapArea[] areas = Object.FindObjectsByType<MapArea>(FindObjectsInactive.Exclude);
            if (areas.Length == 0)
            {
                EditorUtility.DisplayDialog("Export Map", "No MapArea in the open scene.", "OK");
                return;
            }
            foreach (MapArea area in areas) Export(area);
        }

        /// <summary>For batch mode: opens each scene given after -rthScenes and exports its map areas.</summary>
        public static void ExportFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-rthScenes");
            if (i < 0 || i + 1 >= args.Length) throw new System.ArgumentException("Pass -rthScenes Assets/Scenes/A.unity[,B.unity]");
            foreach (string scene in args[i + 1].Split(','))
            {
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
                foreach (MapArea area in Object.FindObjectsByType<MapArea>(FindObjectsInactive.Exclude))
                    Export(area);
            }
        }

        public static string OutputPathFor(MapArea area) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "server", "data", "maps", area.exportName + ".rthmap"));

        public static string Export(MapArea area)
        {
            if (area.origin.x % 8 != 0 || area.origin.y % 8 != 0 || area.size.x % 8 != 0 || area.size.y % 8 != 0)
                Debug.LogError($"[R'th] Region '{area.exportName}' must start and end on whole zones: origin and size multiples of 8. The server will refuse it.", area);
            MapData map = Build(area);
            string path = OutputPathFor(area);
            map.Save(path);
            int npcs = ExportNpcSpawns(area, map, Path.ChangeExtension(path, null) + ".spawns.json");
            int objects = ExportObjects(area, Path.ChangeExtension(path, null) + ".objects.json");
            ExportRegionInfo(area, Path.ChangeExtension(path, null) + ".region.json");
            AddSceneToBuild(area.gameObject.scene.path);
            CollisionPreview.Show(map, area.plane);
            Debug.Log($"[R'th] Exported '{area.exportName}' ({map.Width}x{map.Height}, spawn {map.Spawn}, {npcs} NPCs, {objects} objects) to {path}");
            return path;
        }

        /// <summary>
        /// Writes what the server and clients need to know about the region besides collision:
        /// which scene draws it, its title, and whether it has a spawn point.
        /// </summary>
        private static string Atmosphere(MapArea area)
        {
            string Hex(Color c) => "\"#" + ColorUtility.ToHtmlStringRGB(c) + "\"";
            string F(float f) => f.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return "  \"atmosphere\": { \"sunIntensity\": " + F(area.sunIntensity) +
                   ", \"ambientSky\": " + Hex(area.ambientSky) + ", \"ambientEquator\": " + Hex(area.ambientEquator) +
                   ", \"ambientGround\": " + Hex(area.ambientGround) + ", \"fog\": " + Hex(area.fog) +
                   ", \"fogStart\": " + F(area.fogStart) + ", \"fogEnd\": " + F(area.fogEnd) + " }";
        }

        private static void ExportRegionInfo(MapArea area, string path)
        {
            string Quote(string s) => "\"" + (s ?? "").Replace("\\", "").Replace("\"", "'") + "\"";
            bool hasSpawn = area.GetComponentInChildren<MapSpawn>() != null;
            string title = string.IsNullOrWhiteSpace(area.title) ? area.exportName : area.title.Trim();
            File.WriteAllText(path,
                "{\n" +
                "  \"scene\": " + Quote(area.gameObject.scene.name) + ",\n" +
                "  \"title\": " + Quote(title) + ",\n" +
                "  \"hasSpawn\": " + (hasSpawn ? "true" : "false") + ",\n" +
                "  \"instanced\": " + (area.instanced ? "true" : "false") + ",\n" +
                "  \"music\": " + Quote(area.music) + (area.ownAtmosphere ? ",\n" + Atmosphere(area) : "") + "\n" +
                "}\n");
        }

        /// <summary>Region scenes are streamed in by name at runtime, so each must be in the build.</summary>
        private static void AddSceneToBuild(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return;
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[R'th] Added " + scenePath + " to the build, so the client can stream it in.");
        }

        /// <summary>Writes the interactive objects as JSON next to the map. Returns how many.</summary>
        private static int ExportObjects(MapArea area, string path)
        {
            var sb = new System.Text.StringBuilder("[\n");
            int count = 0;
            foreach (MapObject obj in area.GetComponentsInChildren<MapObject>())
            {
                if (string.IsNullOrWhiteSpace(obj.objectType))
                {
                    Debug.LogWarning($"[R'th] MapObject '{obj.name}' has no objectType; skipped.", obj);
                    continue;
                }
                Vector2Int t = obj.Tile;
                if (count++ > 0) sb.Append(",\n");
                sb.Append("  { \"type\": \"").Append(obj.objectType.Trim().Replace("\"", ""))
                  .Append("\", \"plane\": ").Append(area.plane)
                  .Append(", \"x\": ").Append(t.x)
                  .Append(", \"y\": ").Append(t.y)
                  .Append(" }");
            }
            sb.Append("\n]\n");
            File.WriteAllText(path, sb.ToString());
            return count;
        }

        /// <summary>Writes the creature spawns as JSON next to the map. Returns how many.</summary>
        private static int ExportNpcSpawns(MapArea area, MapData map, string path)
        {
            var sb = new System.Text.StringBuilder("[\n");
            int count = 0;
            foreach (MapNpcSpawn spawn in area.GetComponentsInChildren<MapNpcSpawn>())
            {
                Vector3 p = spawn.transform.position;
                int x = Mathf.FloorToInt(p.x);
                int y = Mathf.FloorToInt(p.z);
                if (string.IsNullOrWhiteSpace(spawn.npcId))
                {
                    Debug.LogWarning($"[R'th] MapNpcSpawn '{spawn.name}' has no npcId; skipped.", spawn);
                    continue;
                }
                if (map.IsBlocked(area.plane, x, y))
                    Debug.LogWarning($"[R'th] NPC spawn '{spawn.name}' stands on a blocked tile ({x}, {y}).", spawn);
                if (count++ > 0) sb.Append(",\n");
                sb.Append("  { \"npc\": \"").Append(spawn.npcId.Trim().Replace("\"", ""))
                  .Append("\", \"plane\": ").Append(area.plane)
                  .Append(", \"x\": ").Append(x)
                  .Append(", \"y\": ").Append(y)
                  .Append(", \"wander\": ").Append(spawn.wanderRadius)
                  .Append(" }");
            }
            sb.Append("\n]\n");
            File.WriteAllText(path, sb.ToString());
            return count;
        }

        public static MapData Build(MapArea area)
        {
            Physics.SyncTransforms();
            int plane = area.plane;
            var map = new MapData(area.origin.x, area.origin.y, area.size.x, area.size.y, plane + 1);

            MarkMissingGround(area, map, plane);
            MarkCliffs(area, map, plane);

            foreach (MapBlocker blocker in area.GetComponentsInChildren<MapBlocker>())
            {
                if (!TryGetBounds(blocker, out Bounds b))
                {
                    Debug.LogWarning($"[R'th] MapBlocker '{blocker.name}' has no collider; ignored.", blocker);
                    continue;
                }
                foreach (int x in Overlapped(b.min.x, b.max.x))
                    foreach (int y in Overlapped(b.min.z, b.max.z))
                        map.Block(plane, x, y);
            }

            foreach (MapWall wall in area.GetComponentsInChildren<MapWall>())
            {
                if (!TryGetBounds(wall, out Bounds b))
                {
                    Debug.LogWarning($"[R'th] MapWall '{wall.name}' has no collider; ignored.", wall);
                    continue;
                }
                if (b.size.x >= b.size.z)
                {
                    // Runs east-west along the line z = line: blocks the north edge of the row below it.
                    int line = Mathf.RoundToInt(b.center.z);
                    foreach (int x in Overlapped(b.min.x, b.max.x)) map.AddWall(plane, x, line - 1, WallSide.North);
                }
                else
                {
                    int line = Mathf.RoundToInt(b.center.x);
                    foreach (int y in Overlapped(b.min.z, b.max.z)) map.AddWall(plane, line - 1, y, WallSide.East);
                }
            }

            MapSpawn spawn = area.GetComponentInChildren<MapSpawn>();
            if (spawn != null)
            {
                Vector3 p = spawn.transform.position;
                map.SpawnPlane = plane;
                map.Spawn = new TileCoord(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
                if (map.IsBlocked(map.SpawnPlane, map.Spawn.X, map.Spawn.Y))
                    Debug.LogError($"[R'th] The spawn tile {map.Spawn} is blocked.", area);
            }
            // No MapSpawn is normal: most regions aren't where new players start.

            return map;
        }

        private static void MarkMissingGround(MapArea area, MapData map, int plane)
        {
            for (int y = area.origin.y; y < area.origin.y + area.size.y; y++)
            {
                for (int x = area.origin.x; x < area.origin.x + area.size.x; x++)
                {
                    var from = new Vector3(x + 0.5f, 500f, y + 0.5f);
                    bool ground = false;
                    foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, 1000f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Transform t = hit.collider.transform;
                        if (!t.IsChildOf(area.transform)) continue;
                        if (t.GetComponentInParent<MapWall>() != null || t.GetComponentInParent<MapBlocker>() != null) continue;
                        ground = true;
                        break;
                    }
                    if (!ground) map.Block(plane, x, y);
                }
            }
        }

        private static void MarkCliffs(MapArea area, MapData map, int plane)
        {
            foreach (MapTerrain terrain in area.GetComponentsInChildren<MapTerrain>())
            {
                Heightfield field = terrain.Field;
                for (int y = field.OriginY; y < field.OriginY + field.Depth; y++)
                    for (int x = field.OriginX; x < field.OriginX + field.Width; x++)
                        if (map.Contains(plane, x, y) && (field.IsSteep(x, y, terrain.cliffStep) || terrain.IsBlocked(x, y))) map.Block(plane, x, y);
            }
        }

        private static bool TryGetBounds(Component root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Collider c in root.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                if (!any) bounds = c.bounds;
                else bounds.Encapsulate(c.bounds);
                any = true;
            }
            return any;
        }

        /// <summary>Whole tiles that the span [min, max] reaches into by more than the epsilon.</summary>
        private static IEnumerable<int> Overlapped(float min, float max)
        {
            int first = Mathf.FloorToInt(min + OverlapEpsilon);
            int last = Mathf.CeilToInt(max - OverlapEpsilon) - 1;
            for (int i = first; i <= last; i++) yield return i;
        }
    }
}
