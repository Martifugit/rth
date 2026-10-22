using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Rth.MapTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Checks the art against docs/assets.md: triangles per model by kind, colours per model,
    /// material colours off the palette, and world textures (size, filtering, how many). Budgets
    /// are data (Assets/Art/art-budgets.json). It reports; it never changes anything.
    ///
    /// Models are the open scenes' units (anything with an ArtKind, Occluder, MapObject,
    /// MapBlocker, MapWall or RoofHider, or a renderer at the top of a region) and every model
    /// file and mesh asset under the texture folders. Results go to the Console and to
    /// Logs/art-validation.md.
    /// </summary>
    public static class ArtValidator
    {
        private const string BudgetsPath = "Assets/Art/art-budgets.json";
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private sealed class Kind
        {
            public string Name;
            public int Target, Cap, Colours;
            public List<string> Match = new List<string>();
        }

        private sealed class Row
        {
            public string What, Kind;
            public int Triangles, Colours;
            public string Problem;
            public bool Error;
        }

        [MenuItem("R'th/Art/Validate Art", priority = 200)]
        public static void ValidateMenu()
        {
            int errors = Validate(out string report);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "art-validation.md"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report);
            if (errors > 0) Debug.LogError($"[R'th] Art validation: {errors} over a hard cap. Report: {path}\n{report}");
            else Debug.Log($"[R'th] Art validation done. Report: {path}\n{report}");
        }

        /// <summary>Batch mode: -executeMethod Rth.Client.Editor.ArtValidator.ValidateFromCommandLine [-rthScenes A.unity,B.unity]. Exits 1 on errors.</summary>
        public static void ValidateFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-rthScenes");
            if (i >= 0 && i + 1 < args.Length)
            {
                bool first = true;
                foreach (string scene in args[i + 1].Split(','))
                {
                    EditorSceneManager.OpenScene(scene, first ? OpenSceneMode.Single : OpenSceneMode.Additive);
                    first = false;
                }
            }
            int errors = Validate(out string report);
            System.Console.WriteLine(report);
            EditorApplication.Exit(errors > 0 ? 1 : 0);
        }

        public static int Validate(out string report)
        {
            var budgets = AssetDatabase.LoadAssetAtPath<TextAsset>(BudgetsPath);
            if (budgets == null)
            {
                report = "No " + BudgetsPath;
                return 1;
            }
            JObject json = JObject.Parse(budgets.text);
            List<Kind> kinds = json["kinds"].Select(k => new Kind
            {
                Name = (string)k["kind"],
                Target = (int)k["target"],
                Cap = (int)k["cap"],
                Colours = (int)k["colours"],
                Match = k["match"].Select(m => (string)m).ToList(),
            }).ToList();
            Dictionary<string, Color> palette = LoadPalette((string)json["palette"]);
            float tolerance = (float?)json["paletteTolerance"] ?? 0.04f;

            var rows = new List<Row>();
            var offPalette = new SortedDictionary<string, string>();

            // Scene models.
            foreach (GameObject root in SceneModels())
                rows.Add(Measure(root, PathOf(root.transform), kinds, palette, tolerance, offPalette));

            // Model files and mesh assets.
            JToken tex = json["textures"];
            string[] folders = tex["folders"].Select(f => (string)f).ToArray();
            string[] ignore = tex["ignore"].Select(f => (string)f).ToArray();
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject t:Mesh", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (ignore.Any(prefix => path.StartsWith(prefix))) continue;
                Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset is GameObject go) rows.Add(Measure(go, path, kinds, palette, tolerance, offPalette));
                else if (asset is Mesh mesh) rows.Add(Judge(path, Classify(path, null, kinds), Triangles(mesh), mesh.subMeshCount, kinds));
            }

            // Textures.
            var textureLines = new List<string>();
            int maxSize = (int)tex["maxSize"];
            int maxCount = (int)tex["maxCount"];
            int textures = 0, errors = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (ignore.Any(prefix => path.StartsWith(prefix))) continue;
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (t == null) continue;
                textures++;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (t.width > maxSize || t.height > maxSize)
                {
                    errors++;
                    textureLines.Add($"| {path} | {t.width} × {t.height} | **over {maxSize} px** |");
                }
                else if (importer != null && importer.filterMode != FilterMode.Point)
                    textureLines.Add($"| {path} | {t.width} × {t.height} | not point filtered |");
            }
            if (textures > maxCount) textureLines.Add($"| (all) | {textures} textures | **over {maxCount}** |");

            errors += rows.Count(r => r.Error);
            var sb = new StringBuilder();
            sb.AppendLine("# Art validation").AppendLine();
            sb.AppendLine($"{rows.Count} models, {textures} world textures; budgets from {BudgetsPath}.").AppendLine();
            List<Row> flagged = rows.Where(r => r.Problem != null).OrderByDescending(r => r.Error).ThenByDescending(r => r.Triangles).ToList();
            if (flagged.Count > 0)
            {
                sb.AppendLine("| Model | Kind | Triangles | Colours | Problem |").AppendLine("| --- | --- | --- | --- | --- |");
                foreach (Row r in flagged) sb.AppendLine($"| {r.What} | {r.Kind} | {r.Triangles} | {r.Colours} | {(r.Error ? "**" + r.Problem + "**" : r.Problem)} |");
                sb.AppendLine();
            }
            else sb.AppendLine("Every model is within its budget.").AppendLine();
            foreach (IGrouping<string, Row> g in rows.GroupBy(r => r.Kind).OrderBy(g => g.Key))
            {
                List<int> tris = g.Select(r => r.Triangles).OrderBy(t => t).ToList();
                sb.AppendLine($"- {g.Key}: {tris.Count} models, triangles median {tris[tris.Count / 2]}, max {tris[tris.Count - 1]}");
            }
            sb.AppendLine();
            if (textureLines.Count > 0)
            {
                sb.AppendLine("| Texture | Size | Problem |").AppendLine("| --- | --- | --- |");
                foreach (string l in textureLines) sb.AppendLine(l);
                sb.AppendLine();
            }
            if (offPalette.Count > 0)
            {
                sb.AppendLine("Materials whose colour is not in the palette (warnings): ");
                foreach (KeyValuePair<string, string> m in offPalette) sb.AppendLine($"- {m.Key}: {m.Value}");
            }
            report = sb.ToString();
            return errors;
        }

        /// <summary>The units builders make: anything carrying one of the markers, without counting markers inside markers twice.</summary>
        private static IEnumerable<GameObject> SceneModels()
        {
            var seen = new HashSet<GameObject>();
            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (!IsModel(t)) continue;
                        Transform above = t.parent;
                        while (above != null && !IsModel(above)) above = above.parent;
                        if (above != null) continue; // part of a bigger model
                        if (seen.Add(t.gameObject)) yield return t.gameObject;
                    }
            }
        }

        private static bool IsModel(Transform t) =>
            t.GetComponent<ArtKind>() != null || t.GetComponent<Occluder>() != null || t.GetComponent<MapObject>() != null ||
            t.GetComponent<MapBlocker>() != null || t.GetComponent<MapWall>() != null || t.GetComponent<RoofHider>() != null;

        private static Row Measure(GameObject root, string what, List<Kind> kinds, Dictionary<string, Color> palette, float tolerance,
            SortedDictionary<string, string> offPalette)
        {
            int triangles = 0;
            var materials = new HashSet<Material>();
            var vertexColours = new HashSet<Color32>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                MeshFilter filter = r.GetComponent<MeshFilter>();
                Mesh mesh = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh : filter != null ? filter.sharedMesh : null;
                if (mesh == null || r is ParticleSystemRenderer || r.GetComponent<MapTerrain>() != null) continue; // ground isn't a model
                triangles += Triangles(mesh);
                foreach (Material m in r.sharedMaterials)
                    if (m != null) materials.Add(m);
                if (mesh.isReadable)
                    foreach (Color32 c in mesh.colors32) vertexColours.Add(c);
            }
            foreach (Material m in materials)
            {
                if (palette.Count == 0 || !m.HasProperty(BaseColor) || m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) continue;
                Color c = m.GetColor(BaseColor);
                if (!palette.Values.Any(p => Mathf.Abs(p.r - c.r) + Mathf.Abs(p.g - c.g) + Mathf.Abs(p.b - c.b) <= tolerance * 3f))
                    offPalette[m.name] = "#" + ColorUtility.ToHtmlStringRGB(c);
            }
            int colours = Mathf.Max(materials.Count, vertexColours.Count);
            return Judge(what, Classify(what, root.GetComponent<ArtKind>(), kinds), triangles, colours, kinds);
        }

        private static Row Judge(string what, string kindName, int triangles, int colours, List<Kind> kinds)
        {
            Kind kind = kinds.FirstOrDefault(k => k.Name == kindName) ?? kinds[kinds.Count - 1];
            var row = new Row { What = what, Kind = kind.Name, Triangles = triangles, Colours = colours };
            if (triangles > kind.Cap)
            {
                row.Problem = $"over the cap ({kind.Cap})";
                row.Error = true;
            }
            else if (triangles > kind.Target) row.Problem = $"over the target ({kind.Target})";
            else if (colours > kind.Colours) row.Problem = $"{colours} colours (at most {kind.Colours})";
            return row;
        }

        private static string Classify(string what, ArtKind marker, List<Kind> kinds)
        {
            if (marker != null && !string.IsNullOrEmpty(marker.kind)) return marker.kind;
            foreach (Kind k in kinds)
                if (k.Match.Any(m => what.IndexOf(m, System.StringComparison.OrdinalIgnoreCase) >= 0)) return k.Name;
            return kinds[kinds.Count - 1].Name;
        }

        private static int Triangles(Mesh mesh)
        {
            long n = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) n += mesh.GetIndexCount(s) / 3;
            return (int)n;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return t.gameObject.scene.name + ": " + path;
        }

        private static Dictionary<string, Color> LoadPalette(string path)
        {
            var colours = new Dictionary<string, Color>();
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (file == null) return colours;
            foreach (JToken c in JObject.Parse(file.text)["colours"])
                if (ColorUtility.TryParseHtmlString((string)c["hex"], out Color colour)) colours[(string)c["name"]] = colour;
            return colours;
        }
    }
}
