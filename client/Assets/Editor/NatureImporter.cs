using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// The kit models made in Blender (Assets/Resources/Nature from tools/art/blender/nature/basin.py,
    /// Assets/Resources/Caves from tools/art/blender/caves/entrances.py) get the game's materials by
    /// name, as the scripts name them:
    /// <list type="bullet">
    /// <item>a world texture's name (Assets/Art/Textures/&lt;name&gt;.png: Foliage, Thorn, Wattle...):
    /// that texture, alpha-clipped so leaf hems cut out, drawn on both sides, matte;</item>
    /// <item>a palette colour's name (Bark, Ash, Scree): that colour, matte, wearing its surface
    /// pattern when Resources/Art/surfaces.json gives it one (Stone: Fieldstone).</item>
    /// </list>
    /// The materials are kept in Assets/Art/Materials/Nature and shared by every model.
    /// </summary>
    public sealed class NatureImporter : AssetPostprocessor
    {
        public static readonly string[] Folders = { "Assets/Resources/Nature/", "Assets/Resources/Caves/" };
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
            importer.materialLocation = ModelImporterMaterialLocation.External; // so OnAssignMaterialModel is asked
        }

        private Material OnAssignMaterialModel(Material imported, Renderer renderer)
        {
            if (!Ours(assetPath)) return null;
            return For(imported.name);
        }

        private static bool Ours(string path) => System.Array.Exists(Folders, f => path.StartsWith(f));

        /// <summary>The shared material for a name from the kits' scripts.</summary>
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

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtTextureImporter.Folder + name + ".png");
            if (texture != null)
            {
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", texture);
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cull", 0f); // both sides: a hem shows from behind as well
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                if (!Palette.TryParse(name, out Color colour))
                {
                    Debug.LogWarning($"[R'th] nature material '{name}' is neither a world texture nor a palette colour.");
                    colour = Color.magenta;
                }
                material.SetColor("_BaseColor", colour);
                material.SetTexture("_BaseMap", Surfaces().TryGetValue(name, out string surface)
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
    }
}
