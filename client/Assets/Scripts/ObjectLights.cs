using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Placed objects whose type has a <c>glow</c> (objects.json: a soul lantern's pale light) light
    /// their surroundings: a soft point light over each one in the streamed regions, breathing a
    /// little. A used-up one (its <see cref="NodeLook"/> showing it depleted) burns low.
    /// </summary>
    public sealed class ObjectLights : MonoBehaviour
    {
        public GameSession session;

        private const float Range = 7f, Intensity = 1.6f, Low = 0.12f;

        private float _nextScan;
        private readonly Dictionary<MapObject, Light> _lights = new Dictionary<MapObject, Light>();

        private void Update()
        {
            if (session == null || !session.IsInGame || session.Content == null) return;
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 2f;
                Scan();
            }
            float breath = 1f + 0.08f * Mathf.Sin(Time.time * 1.7f);
            foreach (KeyValuePair<MapObject, Light> pair in _lights)
            {
                if (pair.Key == null || pair.Value == null) continue;
                NodeLook look = pair.Key.GetComponent<NodeLook>();
                bool spent = look != null && look.Depleted;
                float fade = Flicker.DistanceFade(pair.Value.transform.position);
                pair.Value.enabled = fade > 0.001f;
                pair.Value.intensity = (spent ? Low : Intensity) * breath * fade;
            }
        }

        private void Scan()
        {
            var glows = new Dictionary<string, Color>();
            foreach (ObjectDef d in session.Content.Objects)
                if (!string.IsNullOrWhiteSpace(d.Glow) && Palette.TryParse(d.Glow, out Color c)) glows[d.Id] = c;
            if (glows.Count == 0) return;

            foreach (MapObject o in FindObjectsByType<MapObject>(FindObjectsInactive.Exclude))
            {
                if (_lights.ContainsKey(o) || !glows.TryGetValue(o.objectType, out Color colour)) continue;
                if (o.GetComponentInChildren<Light>() != null) continue; // lit already (a campfire set down)
                var go = new GameObject("Glow");
                go.transform.SetParent(o.transform, false);
                go.transform.localPosition = new Vector3(0f, 1.9f, 0f);
                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = Range;
                light.color = colour;
                light.intensity = Intensity;
                light.shadows = LightShadows.None;
                _lights[o] = light;
            }
            var gone = new List<MapObject>();
            foreach (MapObject o in _lights.Keys) if (o == null) gone.Add(o);
            foreach (MapObject o in gone) _lights.Remove(o);
        }
    }
}
