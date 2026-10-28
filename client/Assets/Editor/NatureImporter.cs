using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// The kit models made in Blender (Assets/Resources/Art/&lt;Layer&gt;/&lt;Kind&gt;/&lt;Style&gt;, each folder exported
    /// by its family script under tools/art/blender, docs/assets.md section 10) get the game's materials
    /// by name, as the scripts name them. Held items and creatures are left alone: their bodies colour
    /// them by palette name as they're drawn (HeldItemArt, CreatureBody).
    /// <list type="bullet">
    /// <item>a world texture's name (Assets/Art/Textures/&lt;name&gt;.png: Foliage, Thorn, Wattle...):
    /// that texture, alpha-clipped so leaf hems cut out, drawn on both sides, matte;</item>
    /// <item>a palette colour's name (Bark, Ash, Scree): that colour, matte, wearing its surface
    /// pattern when Resources/Art/surfaces.json gives it one (Stone: Fieldstone).</item>
    /// </list>
    /// The materials are kept in Assets/Art/Materials/Nature and shared by every model. Unity forbids
    /// making assets while a model imports, so a model first imports with its own materials; once the
    /// import is done the shared ones are made and the model's materials remapped onto them, which
    /// imports it once more.
    /// </summary>
    public sealed class NatureImporter : AssetPostprocessor
    {
        public static readonly string[] Folders = { "Assets/Resources/Art/Parts/", "Assets/Resources/Art/Assets/" };
        private static readonly string[] Except = { "Assets/Resources/Art/Assets/Held/", "Assets/Resources/Art/Assets/Creatures/" };
        private const string MaterialDir = "Assets/Art/Materials/Nature";

        private void OnPreprocessModel()
        {
            if (!Ours(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (!Ours(path) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
                var remaps = importer.GetExternalObjectMap();
                bool changed = false;
                foreach (string name in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name).Distinct())
                {
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                    Material shared = For(name);
                    if (remaps.TryGetValue(id, out Object current) && current == shared) continue;
                    importer.AddRemap(id, shared);
                    changed = true;
                }
                if (changed) importer.SaveAndReimport();
            }
        }

        private static bool Ours(string path) =>
            System.Array.Exists(Folders, f => path.StartsWith(f)) && !System.Array.Exists(Except, f => path.StartsWith(f));

        /// <summary>The shared material for a name from the kits' scripts (made, or brought up to date).</summary>
        public static Material For(string name)
        {
            name = name.Trim();
            Directory.CreateDirectory(MaterialDir);
            string path = MaterialDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetFloat("_Cull", 0f); // both sides: blades, fronds, leaves and cloth are single faces

            // A palette colour with a surface pattern (Plaster, Thatch) is that, even when a texture shares its name.
            Dictionary<string, string> surfaces = Surfaces();
            var texture = surfaces.ContainsKey(name) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(ArtTextureImporter.Folder + name + ".png");
            if (texture != null)
            {
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", texture);
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                if (!Palette.TryParse(name, out Color colour))
                {
                    Debug.LogWarning($"[R'th] kit material '{name}' is neither a world texture nor a palette colour.");
                    colour = Color.magenta;
                }
                material.SetColor("_BaseColor", colour);
                material.SetFloat("_AlphaClip", 0f);
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = -1;
                material.SetTexture("_BaseMap", surfaces.TryGetValue(name, out string surface)
                    ? AssetDatabase.LoadAssetAtPath<Texture2D>(ArtTextureImporter.Folder + surface + ".png")
                    : null);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Palette name to surface texture (Resources/Art/surfaces.json).</summary>
        private static Dictionary<string, string> Surfaces()
        {
            var map = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            var file = Resources.Load<TextAsset>("Art/surfaces");
            if (file == null) return map;
            foreach (JProperty p in ((JObject)JObject.Parse(file.text)["surfaces"] ?? new JObject()).Properties())
                if ((string)p.Value["texture"] is string texture) map[p.Name] = texture;
            return map;
        }

        /// <summary>Reimport every kit model, so each takes the shared materials.</summary>
        [MenuItem("R'th/Art/Reimport Kit Models")]
        public static void ReimportAll()
        {
            foreach (string folder in Folders)
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder.TrimEnd('/') }))
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }
    }
}
