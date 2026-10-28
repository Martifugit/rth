using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Bakes each kit model's smoothed normals into its fourth UV channel as it imports: every vertex
    /// gets the average normal of all the vertices at its position. Our models are flat-shaded, so
    /// their own normals split at every edge; the outline (Resources/Shaders/Highlight) pushes the
    /// shell out along these instead, and so stays closed round corners.
    /// </summary>
    public sealed class OutlineNormalsImporter : AssetPostprocessor
    {
        private static readonly string[] Folders =
        {
            "Assets/Resources/Nature/", "Assets/Resources/Caves/", "Assets/Art/Buildings/", "Assets/Resources/Items/", "Assets/Resources/Creatures/",
        };

        public const int Channel = 3;

        private void OnPostprocessModel(GameObject root)
        {
            if (!System.Array.Exists(Folders, f => assetPath.StartsWith(f))) return;
            foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null) Bake(f.sharedMesh);
            foreach (SkinnedMeshRenderer r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.sharedMesh != null) Bake(r.sharedMesh);
        }

        public static void Bake(Mesh mesh)
        {
            Vector3[] positions = mesh.vertices, normals = mesh.normals;
            if (normals == null || normals.Length != positions.Length) return;
            var sums = new Dictionary<Vector3Int, Vector3>();
            Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f));
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3Int k = Key(positions[i]);
                sums[k] = (sums.TryGetValue(k, out Vector3 s) ? s : Vector3.zero) + normals[i];
            }
            var smooth = new List<Vector3>(positions.Length);
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 n = sums[Key(positions[i])];
                smooth.Add(n.sqrMagnitude > 1e-8f ? n.normalized : normals[i]);
            }
            mesh.SetUVs(Channel, smooth);
        }
    }
}
