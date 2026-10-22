using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.Net;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rth.Client
{
    /// <summary>
    /// Recolours world objects from their definitions: an object type with a "recolor" in
    /// objects.json (material name to a palette colour or #RRGGBB) gets a <see cref="Recolor"/>
    /// on every placement, as content arrives and as region scenes stream in.
    /// </summary>
    public static class ObjectRecolors
    {
        private static ClientContent _content;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            _content = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>Content arrived (login or hot reload): recolour everything loaded.</summary>
        public static void Apply(ClientContent content)
        {
            _content = content;
            for (int i = 0; i < SceneManager.sceneCount; i++) ApplyTo(SceneManager.GetSceneAt(i));
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyTo(scene);

        private static void ApplyTo(Scene scene)
        {
            if (_content == null || !scene.isLoaded) return;
            var byType = new Dictionary<string, Dictionary<string, string>>();
            foreach (ObjectDef d in _content.Objects)
                if (d.Recolor != null && d.Recolor.Count > 0) byType[d.Id] = d.Recolor;
            if (byType.Count == 0) return;

            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MapObject o in root.GetComponentsInChildren<MapObject>(true))
                {
                    if (!byType.TryGetValue(o.objectType, out Dictionary<string, string> recolor)) continue;
                    var swaps = new List<KeyValuePair<string, Color>>();
                    foreach (KeyValuePair<string, string> s in recolor)
                        if (Palette.TryParse(s.Value, out Color c)) swaps.Add(new KeyValuePair<string, Color>(s.Key, c));
                        else Debug.LogWarning($"[R'th] {o.objectType}: \"{s.Value}\" is not a palette colour or #RRGGBB.");
                    Recolor.On(o.gameObject, swaps);
                }
        }
    }
}
