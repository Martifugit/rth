using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rth.MapTools;
using Rth.Shared.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// The layout step of docs/world-building.md: bakes each open region's ground from the world
    /// layout (Assets/World/layout.json) plus the region's own <see cref="MapTerrainStamp"/>s,
    /// into its <see cref="MapTerrain"/> (made if missing). Everything placed in the region rides
    /// the ground: each piece moves up or down by however much the ground under its middle
    /// changed, so re-baking after the layout changes keeps the hand-made detail where it was.
    /// </summary>
    public static class LayoutBaker
    {
        public const string LayoutPath = "Assets/World/layout.json";
        private const string PalettePath = "Assets/Resources/Art/palette.json";

        [MenuItem("R'th/World/Bake Terrain From Layout", priority = 30)]
        private static void BakeMenu()
        {
            MapArea[] areas = Object.FindObjectsByType<MapArea>(FindObjectsInactive.Exclude);
            if (areas.Length == 0)
            {
                EditorUtility.DisplayDialog("Bake Terrain From Layout", "No region (MapArea) is open.", "OK");
                return;
            }
            WorldLayout layout = Load();
            if (layout == null) return;
            foreach (MapArea area in areas)
            {
                Transform flat = area.transform.Find("Ground");
                if (flat != null && flat.gameObject.activeSelf &&
                    !EditorUtility.DisplayDialog("Bake " + area.title,
                        area.title + " has flat ground today. Baking switches that off and moves everything placed onto the new ground; " +
                        "patches drawn as boxes (roads, fields) stay where they are. Flatten towns with Level stamps first.", "Bake", "Skip"))
                    continue;
                Bake(area, layout);
            }
        }

        public static WorldLayout Load()
        {
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (file == null)
            {
                Debug.LogError("[R'th] No world layout at " + LayoutPath + " (tools/world-layout/layout_from_sketch.py makes a first one).");
                return null;
            }
            return WorldLayout.Parse(file.text);
        }

        public static MapTerrain Bake(MapArea area, WorldLayout layout)
        {
            List<TerrainStamp> stamps = area.GetComponentsInChildren<MapTerrainStamp>().Select(s => s.ToStamp()).ToList();
            Heightfield field = layout.Bake(area.origin.x, area.origin.y, area.size.x, area.size.y, layout.Seed, stamps);

            Dictionary<string, Color> palette = Palette();
            string[] grounds = layout.GroundColours(area.origin.x, area.origin.y, area.size.x, area.size.y);
            var colours = new Color32[grounds.Length];
            var blocked = new bool[grounds.Length];
            for (int i = 0; i < grounds.Length; i++)
            {
                colours[i] = palette.TryGetValue(grounds[i], out Color c) ? c : Color.magenta;
                BiomeDef biome = layout.BiomeAt(area.origin.x + i % area.size.x, area.origin.y + i / area.size.x);
                blocked[i] = biome.Water || !biome.Walkable;
            }

            MapTerrain terrain = area.GetComponentInChildren<MapTerrain>();
            Heightfield before = terrain != null ? terrain.Field : null;
            if (terrain == null)
            {
                var kit = new WorldKit(0);
                terrain = WorldKit.Terrain(area.transform, field, colours, kit.Mat("TerrainGround", Color.white), Color.white);
                Undo.RegisterCreatedObjectUndo(terrain.gameObject, "Bake terrain");
            }
            Undo.RecordObject(terrain, "Bake terrain");

            // Everything placed rides the ground: each piece moves by the change under its middle.
            foreach (Transform placed in Pieces(area))
            {
                Vector3 mid = Middle(placed);
                float old = before != null && before.Contains(mid.x, mid.z) ? before.Sample(mid.x, mid.z) : 0f;
                float now = field.Sample(mid.x, mid.z);
                if (Mathf.Abs(now - old) < 0.001f) continue;
                Undo.RecordObject(placed, "Bake terrain");
                placed.position += Vector3.up * (now - old);
            }

            Transform flat = area.transform.Find("Ground");
            if (flat != null)
            {
                Undo.RecordObject(flat.gameObject, "Bake terrain");
                flat.gameObject.SetActive(false);
            }
            terrain.Set(field, colours, blocked);
            EditorUtility.SetDirty(terrain);
            EditorSceneManager.MarkSceneDirty(area.gameObject.scene);
            Debug.Log($"[R'th] Baked {area.title}'s ground from the layout with {stamps.Count} stamps.");
            return terrain;
        }

        /// <summary>
        /// The outermost objects that draw or collide (a box, a tree, a wall run): moving one moves
        /// whatever hangs under it. Not the ground itself.
        /// </summary>
        private static IEnumerable<Transform> Pieces(MapArea area)
        {
            Transform flat = area.transform.Find("Ground");
            foreach (Transform t in area.GetComponentsInChildren<Transform>(true))
            {
                if (t == area.transform || t == flat || !Solid(t) || t.GetComponentInParent<MapTerrain>() != null) continue;
                bool inside = false;
                for (Transform above = t.parent; above != null && above != area.transform && !inside; above = above.parent)
                    inside = Solid(above);
                if (!inside) yield return t;
            }
        }

        private static bool Solid(Transform t) => t.GetComponent<Renderer>() != null || t.GetComponent<Collider>() != null;

        private static Vector3 Middle(Transform t)
        {
            bool any = false;
            var bounds = new Bounds(t.position, Vector3.zero);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }
            foreach (Collider c in t.GetComponentsInChildren<Collider>(true))
            {
                if (!any) bounds = c.bounds;
                else bounds.Encapsulate(c.bounds);
                any = true;
            }
            return bounds.center;
        }

        private static Dictionary<string, Color> Palette()
        {
            var colours = new Dictionary<string, Color>();
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(PalettePath);
            if (file == null) return colours;
            foreach (JToken c in JObject.Parse(file.text)["colours"])
                if (ColorUtility.TryParseHtmlString((string)c["hex"], out Color colour)) colours[(string)c["name"]] = colour;
            return colours;
        }
    }
}
