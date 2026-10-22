using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Rth.MapTools;
using Rth.MapTools.Editor;
using Rth.Shared.World.Data;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// What a region's scene contains and looks like, written by Sync World for anyone without
    /// Unity: world/snapshots/&lt;id&gt;.json (the hand-made content's groups with their tile
    /// bounds and markers, and counts of the data pieces) and world/screens/&lt;id&gt;/ (a top-down
    /// map and views from the game's camera angle). world/snapshots/index.json lists the regions.
    /// </summary>
    public static class WorldSnapshot
    {
        private const int MaxDepth = 3;

        private static string Dir(params string[] parts) => Path.Combine(new[] { WorldSync.RepoRoot, "world" }.Concat(parts).ToArray());

        public static void WriteIndex(WorldWorkspace ws)
        {
            var sb = new StringBuilder("{\n  \"note\": \"Written by Sync World. The regions, their rectangles and scenes; see <id>.json for what each scene holds and world/screens/<id>/ for how it looks.\",\n  \"regions\": [\n");
            sb.Append(string.Join(",\n", ws.Regions.Select(e =>
                $"    {{ \"id\": {Q(e.Region.Id)}, \"title\": {Q(e.Region.Title)}, \"scene\": {Q(e.Region.Scene)}, \"origin\": [{e.Region.OriginX}, {e.Region.OriginY}], " +
                $"\"size\": [{e.Region.Width}, {e.Region.Height}], \"pieces\": {e.Region.Pieces.Count}, \"objects\": {e.Compiled.Objects.Count}, \"npcs\": {e.Compiled.Npcs.Count} }}")));
            sb.Append("\n  ]\n}\n");
            RegionFiles.WriteIfChanged(Dir("snapshots", "index.json"), sb.ToString());
        }

        public static void Write(RegionData region, MapArea area, DataRoot data, HandmadeRoot handmade, bool screenshots, WorldSync.Report report)
        {
            var sb = new StringBuilder("{\n");
            sb.Append("  \"note\": \"Written by Sync World from ").Append(region.Scene).Append(". Read-only: change world/regions/").Append(region.Id).Append(".json, or the scene's Handmade root in Unity.\",\n");
            sb.Append("  \"region\": ").Append(Q(region.Id)).Append(",\n");
            sb.Append("  \"origin\": [").Append(region.OriginX).Append(", ").Append(region.OriginY).Append("], \"size\": [").Append(region.Width).Append(", ").Append(region.Height).Append("],\n");
            IEnumerable<string> kinds = region.Pieces.GroupBy(p => p.Kind).OrderBy(g => g.Key).Select(g => Q(g.Key) + ": " + g.Count());
            sb.Append("  \"dataPieces\": { ").Append(string.Join(", ", kinds)).Append(" },\n");
            sb.Append("  \"handmade\": [\n");
            var lines = new List<string>();
            foreach (Transform child in handmade.transform) Describe(child, child.name, 1, lines);
            sb.Append(string.Join(",\n", lines)).Append("\n  ]\n}\n");
            RegionFiles.WriteIfChanged(Dir("snapshots", region.Id + ".json"), sb.ToString());
            if (screenshots) WorldScreens.Capture(region, area, Dir("screens", region.Id), report);
        }

        /// <summary>One line per group: its path, tile bounds, how many objects it holds and the map markers among them.</summary>
        private static void Describe(Transform t, string path, int depth, List<string> lines)
        {
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
            Collider[] colliders = t.GetComponentsInChildren<Collider>();
            string tiles = "null";
            if (renderers.Length > 0 || colliders.Length > 0)
            {
                Bounds b = renderers.Length > 0 ? renderers[0].bounds : colliders[0].bounds;
                foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                foreach (Collider c in colliders) b.Encapsulate(c.bounds);
                tiles = $"[{Mathf.FloorToInt(b.min.x)}, {Mathf.FloorToInt(b.min.z)}, {Mathf.CeilToInt(b.max.x)}, {Mathf.CeilToInt(b.max.z)}]";
            }
            var markers = new List<string>();
            void Count<T>(string name) where T : Component
            {
                int n = t.GetComponentsInChildren<T>().Length;
                if (n > 0) markers.Add(Q(name) + ": " + n);
            }
            Count<MapBlocker>("blockers");
            Count<MapWall>("walls");
            Count<MapObject>("objects");
            Count<MapNpcSpawn>("npcs");
            Count<MapLevel>("levels");
            Count<Light>("lights");
            string own = string.Join(", ", t.GetComponents<MonoBehaviour>().Where(m => m != null).Select(m => m.GetType().Name).Distinct());
            lines.Add($"    {{ \"path\": {Q(path)}, \"tiles\": {tiles}, \"objects\": {t.GetComponentsInChildren<Transform>().Length - 1}" +
                      (markers.Count > 0 ? ", " + string.Join(", ", markers) : "") +
                      (own.Length > 0 ? $", \"components\": {Q(own)}" : "") + " }");
            if (depth >= MaxDepth) return;
            foreach (Transform child in t)
                if (child.childCount > 0 || child.GetComponent<MonoBehaviour>() != null)
                    Describe(child, path + "/" + child.name, depth + 1, lines);
        }

        private static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    /// <summary>
    /// Pictures of a region for anyone without Unity: a top-down map of the whole region and two
    /// views from about the game camera's angle, lit by a temporary sun.
    /// </summary>
    public static class WorldScreens
    {
        public static void Capture(RegionData region, MapArea area, string directory, WorldSync.Report report)
        {
            Directory.CreateDirectory(directory);
            var sunGo = new GameObject("Snapshot sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = region.Atmosphere?.SunIntensity ?? 1.1f;
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var ambient = RenderSettings.ambientLight;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);
            var camGo = new GameObject("Snapshot camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.1f);
            try
            {
                var centre = new Vector3(region.OriginX + region.Width / 2f, 0f, region.OriginY + region.Height / 2f);
                // Top-down: north up, 8 pixels a tile (at most 1024 a side).
                int ppt = Mathf.Clamp(1024 / Mathf.Max(region.Width, region.Height), 2, 8);
                cam.orthographic = true;
                cam.orthographicSize = region.Height / 2f;
                cam.transform.position = centre + Vector3.up * 200f;
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 400f;
                Render(cam, region.Width * ppt, region.Height * ppt, Path.Combine(directory, "top.png"));

                // From the south and from the east, at about the game camera's pitch and distance.
                cam.orthographic = false;
                cam.fieldOfView = 45f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 300f;
                float distance = Mathf.Max(region.Width, region.Height) * 0.7f;
                foreach ((string name, float yaw) in new[] { ("south", 0f), ("east", -90f) })
                {
                    Quaternion rot = Quaternion.Euler(45f, yaw, 0f);
                    cam.transform.rotation = rot;
                    cam.transform.position = centre - rot * Vector3.forward * distance;
                    Render(cam, 1280, 720, Path.Combine(directory, "from-" + name + ".png"));
                }
            }
            catch (System.Exception e)
            {
                report.Info($"{region.Id}: no screenshots ({e.Message}); they need a graphics device (don't use -nographics).");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(sunGo);
                RenderSettings.ambientLight = ambient;
            }
        }

        private static void Render(Camera cam, int width, int height, string path)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.aspect = (float)width / height;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = previous;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
