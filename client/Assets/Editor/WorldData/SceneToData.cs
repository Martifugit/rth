using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rth.MapTools;
using Rth.MapTools.Editor;
using Rth.Shared.World;
using Rth.Shared.World.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Converts a region scene made the old way (by a builder script, or by hand) to world data,
    /// once: everything in it becomes hand-made content, except what agents and data most need to
    /// edit, which becomes data pieces: creatures, the player spawn, interactive objects, trees and
    /// rocks (when their look is plain boxes and cylinders). Then it syncs the region and checks
    /// that the exported map, objects and creatures are exactly what they were.
    /// </summary>
    public static class SceneToData
    {
        [MenuItem("R'th/World/Convert Open Region To Data", priority = 20)]
        private static void ConvertMenu()
        {
            MapArea area = Object.FindAnyObjectByType<MapArea>();
            if (area == null)
            {
                EditorUtility.DisplayDialog("Convert to data", "Open a region scene (one with a MapArea) first.", "OK");
                return;
            }
            WorldSync.Report report = Convert(area);
            EditorUtility.DisplayDialog("Convert to data", report.Summary(), "OK");
        }

        /// <summary>For batch mode: converts each scene after -rthScenes (comma separated).</summary>
        public static void ConvertFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-rthScenes");
            if (i < 0 || i + 1 >= args.Length) throw new System.ArgumentException("Pass -rthScenes Assets/Scenes/A.unity[,B.unity]");
            bool failed = false;
            foreach (string scene in args[i + 1].Split(','))
            {
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
                failed |= Convert(Object.FindAnyObjectByType<MapArea>()).Failed;
            }
            EditorApplication.Exit(failed ? 1 : 0);
        }

        public static WorldSync.Report Convert(MapArea area)
        {
            var report = new WorldSync.Report();
            string path = WorldSync.RegionPath(area.exportName);
            if (area.GetComponentInChildren<DataRoot>(true) != null || File.Exists(path))
            {
                report.Error($"{area.exportName} already has world data ({path}).");
                return report;
            }

            // What the server sees today, to compare with afterwards.
            MapData before = MapExporter.Build(area);
            List<ObjectEntry> objectsBefore = MapExporter.Objects(area, area.transform);
            List<NpcEntry> npcsBefore = MapExporter.Npcs(area, area.transform);

            RegionData region = Header(area);
            (DataRoot _, HandmadeRoot handmade) = WorldSync.EnsureRoots(area);
            var used = new HashSet<string>();
            var lifted = new List<GameObject>();

            foreach (MapSpawn s in handmade.GetComponentsInChildren<MapSpawn>())
            {
                if (s.GetComponentsInChildren<Renderer>().Length > 0) continue;
                region.Pieces.Add(Placed(PieceKind.Spawn, "spawn", s.transform, area, used));
                lifted.Add(s.gameObject);
            }
            foreach (MapNpcSpawn n in handmade.GetComponentsInChildren<MapNpcSpawn>())
            {
                if (n.GetComponentsInChildren<Renderer>().Length > 0) continue;
                Piece p = Placed(PieceKind.Npc, n.npcId, n.transform, area, used);
                p.Npc = n.npcId;
                if (n.wanderRadius != -1) p.Wander = n.wanderRadius;
                region.Pieces.Add(p);
                lifted.Add(n.gameObject);
            }
            foreach (MapObject o in handmade.GetComponentsInChildren<MapObject>())
            {
                Piece p = Thing(PieceKind.Prop, o.objectType, o.gameObject, area, region, used, report);
                if (p == null) continue;
                p.Object = o.objectType;
                if (o.climbTo != null)
                {
                    Vector3 to = o.climbTo.position;
                    p.ClimbTo = new[] { MapLevel.Of(o.climbTo, area.plane), Mathf.FloorToInt(to.x), Mathf.FloorToInt(to.z) };
                    if (o.climbTo.IsChildOf(o.transform)) { /* goes with it */ }
                    else if (o.climbTo.childCount == 0 && o.climbTo.GetComponents<Component>().Length == 1) lifted.Add(o.climbTo.gameObject);
                }
                region.Pieces.Add(p);
                lifted.Add(o.gameObject);
            }
            foreach (MapBlocker b in handmade.GetComponentsInChildren<MapBlocker>())
            {
                if (lifted.Any(l => b.transform.IsChildOf(l.transform))) continue;
                string kind = b.name == "Rock" ? PieceKind.Rock : (b.name == "Oak" || b.name == "Willow" || b.name == "Yew" || b.name == "Tree") ? PieceKind.Tree : null;
                if (kind == null || b.GetComponent<MapObject>() != null) continue;
                Piece p = Thing(kind, b.name.ToLowerInvariant(), b.gameObject, area, region, used, report);
                if (p == null) continue;
                if (kind == PieceKind.Tree) p.Variant = b.name;
                region.Pieces.Add(p);
                lifted.Add(b.gameObject);
            }
            foreach (GameObject go in lifted) Object.DestroyImmediate(go);
            RemoveEmptyGroups(handmade.transform);

            RegionFiles.SaveRegion(region, path);
            report.Info($"{region.Id}: {region.Pieces.Count} pieces lifted into world/regions/{region.Id}.json; everything else is hand-made");

            WorldWorkspace.Entry entry = WorldWorkspace.Load(WorldSync.RepoRoot, region.Id).Find(region.Id);
            WorldSync.SyncRegion(entry, report, screenshots: true);

            // The same map, objects and creatures as before.
            WorldWorkspace.Entry after = WorldWorkspace.Load(WorldSync.RepoRoot, region.Id).Find(region.Id);
            var diffs = new List<string>();
            for (int p = 0; p < before.Planes; p++)
                for (int y = before.OriginY; y < before.OriginY + before.Height; y++)
                    for (int x = before.OriginX; x < before.OriginX + before.Width; x++)
                        if (before.GetFlags(p, x, y) != after.Compiled.Map.GetFlags(p, x, y)) diffs.Add($"({x},{y}) p{p}");
            if (before.Planes != after.Compiled.Map.Planes) diffs.Add("planes");
            if (before.Spawn.X != after.Compiled.Map.Spawn.X || before.Spawn.Y != after.Compiled.Map.Spawn.Y) diffs.Add("spawn");
            if (!Same(objectsBefore.Select(o => $"{o.Type}@{o.Plane},{o.X},{o.Y}>{o.ToPlane},{o.ToX},{o.ToY}"), after.Compiled.Objects.Select(o => $"{o.Type}@{o.Plane},{o.X},{o.Y}>{o.ToPlane},{o.ToX},{o.ToY}"))) diffs.Add("objects");
            if (!Same(npcsBefore.Select(n => $"{n.Npc}@{n.Plane},{n.X},{n.Y}~{n.Wander}"), after.Compiled.Npcs.Select(n => $"{n.Npc}@{n.Plane},{n.X},{n.Y}~{n.Wander}"))) diffs.Add("creatures");
            if (diffs.Count > 0) report.Error($"{region.Id}: the converted region differs from before in {diffs.Count} places: {string.Join(", ", diffs.Take(12))}");
            else report.Info($"{region.Id}: converted; the map, objects and creatures are exactly as before");
            return report;
        }

        /// <summary>Removes groups left empty by lifting their contents into data (a "Creatures" group, say).</summary>
        private static void RemoveEmptyGroups(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                RemoveEmptyGroups(child);
                if (child.childCount == 0 && child.GetComponents<Component>().Length == 1) Object.DestroyImmediate(child.gameObject);
            }
        }

        private static bool Same(IEnumerable<string> a, IEnumerable<string> b) => a.OrderBy(s => s).SequenceEqual(b.OrderBy(s => s));

        private static RegionData Header(MapArea area)
        {
            string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
            var region = new RegionData
            {
                Id = area.exportName,
                Title = area.title,
                Scene = area.gameObject.scene.path,
                Origin = new[] { area.origin.x, area.origin.y },
                Size = new[] { area.size.x, area.size.y },
                Plane = area.plane,
                Instanced = area.instanced,
                Music = area.music ?? "",
            };
            if (area.ownAtmosphere)
                region.Atmosphere = new RegionAtmosphere
                {
                    SunIntensity = area.sunIntensity, Sun = Hex(area.sunColor), AmbientSky = Hex(area.ambientSky),
                    AmbientEquator = Hex(area.ambientEquator), AmbientGround = Hex(area.ambientGround), Fog = Hex(area.fog),
                    FogStart = area.fogStart, FogEnd = area.fogEnd,
                };
            return region;
        }

        private static Piece Placed(string kind, string name, Transform t, MapArea area, HashSet<string> used)
        {
            Vector3 p = t.position;
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.z);
            var piece = new Piece { Id = UniqueId(kind + "-" + Slug(name) + "-" + x + "-" + y, used), Kind = kind, At = new[] { x, y } };
            int plane = MapLevel.Of(t, area.plane);
            if (plane != area.plane) piece.Plane = plane;
            return piece;
        }

        /// <summary>
        /// A thing whose look is only boxes and cylinders (else null, and it stays hand-made): its
        /// tile, its parts around the tile's centre, and its footprint if it isn't just its tile.
        /// </summary>
        private static Piece Thing(string kind, string name, GameObject go, MapArea area, RegionData region, HashSet<string> used, WorldSync.Report report)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "";
                if (!(r is MeshRenderer) || (mesh != "Cube" && mesh != "Cylinder") || r.sharedMaterials.Length != 1) return null;
            }
            if (go.GetComponentsInChildren<Light>().Length > 0 || go.GetComponentsInChildren<ParticleSystem>().Length > 0) return null;
            if (go.GetComponentsInChildren<MapLevel>().Length > 0 || go.GetComponentsInChildren<MapWall>().Length > 0) return null;
            Transform anchor = go.transform;
            Piece piece = Placed(kind, name, anchor, area, used);
            var centre = new Vector3(piece.X + 0.5f, 0f, piece.Y + 0.5f);
            piece.Parts = new List<Part>();
            foreach (Renderer r in renderers)
            {
                Transform t = r.transform;
                Material m = r.sharedMaterial;
                string material = m != null ? m.name : "Placeholder";
                if (!region.Materials.ContainsKey(material))
                    region.Materials[material] = "#" + ColorUtility.ToHtmlStringRGB(m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.magenta);
                Vector3 at = t.position - centre;
                Vector3 rot = t.rotation.eulerAngles;
                var part = new Part
                {
                    Shape = r.GetComponent<MeshFilter>().sharedMesh.name == "Cylinder" ? "cylinder" : "box",
                    Name = t.name,
                    At = new[] { R(at.x), R(at.y), R(at.z) },
                    Size = new[] { R(t.lossyScale.x), R(t.lossyScale.y), R(t.lossyScale.z) },
                    Material = material,
                };
                if (rot.sqrMagnitude > 0.0001f) part.Rot = new[] { R(Wrap(rot.x)), R(Wrap(rot.y)), R(Wrap(rot.z)) };
                piece.Parts.Add(part);
            }

            // Footprint: what the old blocker covered, relative to the tile.
            MapBlocker blocker = go.GetComponent<MapBlocker>();
            List<Vector2Int> tiles = blocker != null ? MapExporter.BlockedTiles(blocker) : null;
            // Trees, rocks and objects (every prop lifted here is one) block their own tile by default.
            if (tiles == null || tiles.Count == 0) piece.Footprint = new[] { 0, 0, 0, 0 };
            else if (!(tiles.Count == 1 && tiles[0] == new Vector2Int(piece.X, piece.Y)))
            {
                int x0 = tiles.Min(t => t.x), y0 = tiles.Min(t => t.y), x1 = tiles.Max(t => t.x) + 1, y1 = tiles.Max(t => t.y) + 1;
                if ((x1 - x0) * (y1 - y0) != tiles.Count)
                {
                    report.Info($"{region.Id}: '{go.name}' blocks an odd shape; left hand-made.");
                    used.Remove(piece.Id);
                    return null;
                }
                piece.Footprint = new[] { x0 - piece.X, y0 - piece.Y, x1 - piece.X, y1 - piece.Y };
            }
            return piece;
        }

        private static float Wrap(float degrees) => degrees > 180f ? degrees - 360f : degrees;

        private static float R(float f) => Mathf.Round(f * 10000f) / 10000f;

        private static string Slug(string s) => new string((s ?? "thing").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

        private static string UniqueId(string id, HashSet<string> used)
        {
            string candidate = id;
            for (int n = 2; !used.Add(candidate); n++) candidate = id + "-" + n;
            return candidate;
        }
    }
}
