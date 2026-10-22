using System.Collections.Generic;
using System.IO;
using Rth.Shared.World;
using Rth.Shared.World.Data;
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
    ///  - A <see cref="MapLevel"/> puts everything under it on another plane (floor): a tile is walkable
    ///    on that plane only where a ground collider of that level is under its centre, and its
    ///    blockers, walls, objects and spawns land on that plane.
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

        /// <summary>
        /// Set by Sync World: exports a region that has world data (world/regions/&lt;id&gt;.json) its
        /// own way, through the data and the C# compiler, and returns the map path; null for a region
        /// without data, which this exporter writes straight from the scene.
        /// </summary>
        public static System.Func<MapArea, string> DataExport;

        public static string Export(MapArea area)
        {
            if (DataExport != null)
            {
                string exported = DataExport(area);
                if (exported != null) return exported;
            }
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
            ReportLandings(area, map);
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
            return "  \"atmosphere\": { \"sunIntensity\": " + F(area.sunIntensity) + ", \"sun\": " + Hex(area.sunColor) +
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
        public static void AddSceneToBuild(string scenePath)
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
            List<ObjectEntry> objects = Objects(area, area.transform);
            File.WriteAllText(path, RegionCompiler.ObjectsJson(objects));
            return objects.Count;
        }

        /// <summary>The interactive objects under <paramref name="scope"/> (the whole region, or one root of it).</summary>
        public static List<ObjectEntry> Objects(MapArea area, Transform scope)
        {
            var list = new List<ObjectEntry>();
            foreach (MapObject obj in scope.GetComponentsInChildren<MapObject>())
            {
                if (string.IsNullOrWhiteSpace(obj.objectType))
                {
                    Debug.LogWarning($"[R'th] MapObject '{obj.name}' has no objectType; skipped.", obj);
                    continue;
                }
                Vector2Int t = obj.Tile;
                var entry = new ObjectEntry { Type = obj.objectType.Trim(), Plane = MapLevel.Of(obj, area.plane), X = t.x, Y = t.y };
                if (obj.climbTo != null)
                {
                    Vector3 to = obj.climbTo.position;
                    entry.ToPlane = MapLevel.Of(obj.climbTo, area.plane);
                    entry.ToX = Mathf.FloorToInt(to.x);
                    entry.ToY = Mathf.FloorToInt(to.z);
                }
                list.Add(entry);
            }
            return list;
        }

        /// <summary>Writes the creature spawns as JSON next to the map. Returns how many.</summary>
        private static int ExportNpcSpawns(MapArea area, MapData map, string path)
        {
            List<NpcEntry> npcs = Npcs(area, area.transform);
            foreach (NpcEntry n in npcs)
                if (map.IsBlocked(n.Plane, n.X, n.Y))
                    Debug.LogWarning($"[R'th] NPC spawn '{n.Npc}' stands on a blocked tile ({n.X}, {n.Y}) on level {n.Plane}.", area);
            File.WriteAllText(path, RegionCompiler.SpawnsJson(npcs));
            return npcs.Count;
        }

        /// <summary>The creature spawns under <paramref name="scope"/>.</summary>
        public static List<NpcEntry> Npcs(MapArea area, Transform scope)
        {
            var list = new List<NpcEntry>();
            foreach (MapNpcSpawn spawn in scope.GetComponentsInChildren<MapNpcSpawn>())
            {
                if (string.IsNullOrWhiteSpace(spawn.npcId))
                {
                    Debug.LogWarning($"[R'th] MapNpcSpawn '{spawn.name}' has no npcId; skipped.", spawn);
                    continue;
                }
                Vector3 p = spawn.transform.position;
                list.Add(new NpcEntry
                {
                    Npc = spawn.npcId.Trim(), Plane = MapLevel.Of(spawn, area.plane),
                    X = Mathf.FloorToInt(p.x), Y = Mathf.FloorToInt(p.z), Wander = spawn.wanderRadius,
                });
            }
            return list;
        }

        /// <summary>The player spawn under <paramref name="scope"/> as [plane, x, y], or null.</summary>
        public static int[] Spawn(MapArea area, Transform scope)
        {
            MapSpawn spawn = scope.GetComponentInChildren<MapSpawn>();
            if (spawn == null) return null;
            Vector3 p = spawn.transform.position;
            return new[] { MapLevel.Of(spawn, area.plane), Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z) };
        }

        public static MapData Build(MapArea area)
        {
            CollisionLayers layers = BuildLayers(area, area.transform);
            int[] spawn = Spawn(area, area.transform);
            MapData map = layers.ToMapData(spawn != null ? spawn[0] : 0, spawn != null ? new TileCoord(spawn[1], spawn[2]) : (TileCoord?)null);
            if (spawn != null && map.IsBlocked(map.SpawnPlane, map.Spawn.X, map.Spawn.Y))
                Debug.LogError($"[R'th] The spawn tile {map.Spawn} is blocked.", area);
            // No MapSpawn is normal: most regions aren't where new players start.
            return map;
        }

        /// <summary>
        /// The collision of everything under <paramref name="scope"/> (the whole region, or one
        /// root of it, as Sync World does for the hand-made root): ground, blocked tiles and walls,
        /// kept apart so they can be merged with the region's data.
        /// </summary>
        public static CollisionLayers BuildLayers(MapArea area, Transform scope)
        {
            Physics.SyncTransforms();
            int plane = area.plane;
            int planes = plane + 1;
            foreach (MapLevel floor in scope.GetComponentsInChildren<MapLevel>(true)) planes = Mathf.Max(planes, floor.level + 1);
            var layers = new CollisionLayers(area.origin.x, area.origin.y, area.size.x, area.size.y, planes);

            MarkGround(area, scope, layers);
            MarkCliffs(area, scope, layers);

            foreach (MapBlocker blocker in scope.GetComponentsInChildren<MapBlocker>())
            {
                if (!TryGetBounds(blocker, out Bounds b))
                {
                    Debug.LogWarning($"[R'th] MapBlocker '{blocker.name}' has no collider; ignored.", blocker);
                    continue;
                }
                int level = MapLevel.Of(blocker, plane);
                foreach (int x in Overlapped(b.min.x, b.max.x))
                    foreach (int y in Overlapped(b.min.z, b.max.z))
                        layers.Block(level, x, y);
            }

            foreach (MapWall wall in scope.GetComponentsInChildren<MapWall>())
            {
                if (!TryGetBounds(wall, out Bounds b))
                {
                    Debug.LogWarning($"[R'th] MapWall '{wall.name}' has no collider; ignored.", wall);
                    continue;
                }
                int level = MapLevel.Of(wall, plane);
                if (b.size.x >= b.size.z)
                {
                    // Runs east-west along the line z = line: blocks the north edge of the row below it.
                    int line = Mathf.RoundToInt(b.center.z);
                    foreach (int x in Overlapped(b.min.x, b.max.x)) layers.AddWall(level, x, line - 1, WallSide.North);
                }
                else
                {
                    int line = Mathf.RoundToInt(b.center.x);
                    foreach (int y in Overlapped(b.min.z, b.max.z)) layers.AddWall(level, line - 1, y, WallSide.East);
                }
            }
            return layers;
        }

        /// <summary>
        /// Marks the ground on each plane. Ground is any collider under the tile's centre that isn't
        /// a wall or blocker, counted on the plane of the <see cref="MapLevel"/> it belongs to (the
        /// region's own plane if it has none), so a floor above a room is ground on its own plane and
        /// leaves the room's plane alone. Tiles with none are blocked when the layers are flattened.
        /// </summary>
        private static void MarkGround(MapArea area, Transform scope, CollisionLayers layers)
        {
            for (int y = area.origin.y; y < area.origin.y + area.size.y; y++)
            {
                for (int x = area.origin.x; x < area.origin.x + area.size.x; x++)
                {
                    var from = new Vector3(x + 0.5f, 500f, y + 0.5f);
                    foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, 1000f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Transform t = hit.collider.transform;
                        if (!t.IsChildOf(scope)) continue;
                        if (t.GetComponentInParent<MapWall>() != null || t.GetComponentInParent<MapBlocker>() != null) continue;
                        int level = MapLevel.Of(hit.collider, area.plane);
                        if (level >= 0 && level < layers.Planes) layers.SetGround(level, x, y);
                    }
                }
            }
        }

        /// <summary>Blocks cliff faces and impassable tiles of each terrain, on the plane the terrain is on.</summary>
        private static void MarkCliffs(MapArea area, Transform scope, CollisionLayers layers)
        {
            foreach (MapTerrain terrain in scope.GetComponentsInChildren<MapTerrain>())
            {
                Heightfield field = terrain.Field;
                int plane = MapLevel.Of(terrain, area.plane);
                for (int y = field.OriginY; y < field.OriginY + field.Depth; y++)
                    for (int x = field.OriginX; x < field.OriginX + field.Width; x++)
                        if (layers.Contains(plane, x, y) && (field.IsSteep(x, y, terrain.cliffStep) || terrain.IsBlocked(x, y))) layers.Block(plane, x, y);
            }
        }

        /// <summary>Warns about stairs and ladders that lead nowhere: a landing nobody can stand on.</summary>
        private static void ReportLandings(MapArea area, MapData map)
        {
            foreach (MapObject obj in area.GetComponentsInChildren<MapObject>())
            {
                if (obj.climbTo == null) continue;
                Vector3 to = obj.climbTo.position;
                int x = Mathf.FloorToInt(to.x);
                int y = Mathf.FloorToInt(to.z);
                int level = MapLevel.Of(obj.climbTo, area.plane);
                if (level >= map.Planes || !map.Contains(level, x, y)) continue; // lands in another region, which can't be checked here
                if (map.IsBlocked(level, x, y))
                    Debug.LogWarning($"[R'th] '{obj.name}' climbs to ({x}, {y}) on level {level}, which is blocked there (no floor, or something stands on it).", obj);
            }
        }

        /// <summary>The tiles a blocker covers, by the same rule the exporter uses (null if it has no collider).</summary>
        public static List<Vector2Int> BlockedTiles(Component blocker)
        {
            if (!TryGetBounds(blocker, out Bounds b)) return null;
            var tiles = new List<Vector2Int>();
            foreach (int x in Overlapped(b.min.x, b.max.x))
                foreach (int y in Overlapped(b.min.z, b.max.z))
                    tiles.Add(new Vector2Int(x, y));
            return tiles;
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
