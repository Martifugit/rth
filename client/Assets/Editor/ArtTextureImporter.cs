using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// World textures (anything under Assets/Art/Textures) are pixel art: point filtered, no
    /// mipmaps (they would eat the cut-out edges of leaves), alpha as transparency, uncompressed,
    /// at most 128 pixels. See docs/assets.md.
    /// </summary>
    public sealed class ArtTextureImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Art/Textures/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 128;
            importer.sRGBTexture = true;
        }
    }
}
