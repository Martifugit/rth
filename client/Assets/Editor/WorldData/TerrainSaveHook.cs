using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rth.Shared.World.Data;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Whenever a region's terrain is saved (Ctrl+S, Save Project, or anything else that saves it), its
    /// ground is written into the region's data and its map exported, so the server always walks the
    /// ground you see, whether or not the region's scene was saved too.
    /// </summary>
    public sealed class TerrainSaveHook : AssetModificationProcessor
    {
        private static readonly HashSet<string> Pending = new HashSet<string>();

        private static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (string path in paths)
            {
                if (!path.StartsWith(WorldTerrains.Folder + "/") || !path.EndsWith(".asset")) continue;
                string id = Path.GetFileNameWithoutExtension(path);
                if (File.Exists(WorldSync.RegionPath(id))) Pending.Add(id);
            }
            if (Pending.Count > 0)
            {
                // Once the save is done (the terrains are read as they now are).
                EditorApplication.delayCall -= Write;
                EditorApplication.delayCall += Write;
            }
            return paths;
        }

        private static void Write()
        {
            var ids = Pending.ToList();
            Pending.Clear();
            var written = new List<string>();
            foreach (string id in ids)
            {
                try
                {
                    RegionData region = RegionFiles.LoadRegion(WorldSync.RegionPath(id));
                    if (!WorldTerrains.WriteGround(region)) continue;
                    RegionFiles.SaveRegion(region, WorldSync.RegionPath(id));
                    WorldWorkspace.Load(WorldSync.RepoRoot, id).Export();
                    WorldWater.Rebuild(region);
                    written.Add(id);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[R'th] {id}: couldn't write its ground from its terrain: {e.Message}");
                }
            }
            if (written.Count > 0) Debug.Log($"[R'th] Saved terrain: the ground of {string.Join(", ", written)} written to the data and its maps exported.");
        }
    }
}
