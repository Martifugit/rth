using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.Angling;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Trophy spots on the water (docs/fishing.md 3.1), from the "anglingSpots" state: a spot is
    /// only there while it is active and not spooked for you (hidden, and not clickable,
    /// otherwise). A visible spot's rings move in its species' way ("quick", "bubbles", "line",
    /// anything else slow and wide) and weaken as its time runs out (plenty, thinning, nearly gone).
    /// Region objects are MapObjects in the streamed scenes, found by type and tile.
    /// </summary>
    public sealed class AnglingViews : MonoBehaviour
    {
        public GameSession session;

        private float _nextScan;
        private readonly Dictionary<(string, int, int), MapObject> _objects = new Dictionary<(string, int, int), MapObject>();
        private readonly Dictionary<MapObject, (Renderer[] Renderers, Collider[] Colliders)> _hidden = new Dictionary<MapObject, (Renderer[], Collider[])>();
        private readonly Dictionary<Transform, Vector3> _baseScale = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Vector3> _basePosition = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<MapObject, (string Ripple, int Phase)> _shown = new Dictionary<MapObject, (string, int)>();

        private void Update()
        {
            if (session == null || !session.IsInGame || session.Content?.Angling == null) return;
            Dictionary<string, AnglingSpeciesDef> bySpot = new Dictionary<string, AnglingSpeciesDef>();
            foreach (AnglingSpeciesDef s in session.Content.Angling.Species) bySpot[s.SpotType] = s;
            if (bySpot.Count == 0) return;

            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 2f;
                _objects.Clear();
                foreach (MapObject o in FindObjectsByType<MapObject>(FindObjectsInactive.Exclude))
                {
                    if (!bySpot.ContainsKey(o.objectType)) continue;
                    Vector3 p = o.transform.position;
                    _objects[(o.objectType, Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z))] = o;
                }
            }

            var active = new Dictionary<MapObject, AnglingSpotView>();
            foreach (AnglingSpotView v in session.ServerState<AnglingSpotsState>(AnglingSpotsState.StateKey).Spots)
                if (_objects.TryGetValue((v.Type, v.X, v.Y), out MapObject o) && o != null) active[o] = v;

            _shown.Clear();
            foreach (MapObject o in _objects.Values)
            {
                if (o == null) continue;
                if (active.TryGetValue(o, out AnglingSpotView view))
                {
                    Show(o);
                    _shown[o] = (bySpot[o.objectType].Ripple, view.Phase);
                }
                else if (!_hidden.ContainsKey(o)) Hide(o);
            }
            foreach (MapObject o in new List<MapObject>(_hidden.Keys))
                if (o == null) _hidden.Remove(o);

            float t = Time.time;
            foreach (KeyValuePair<MapObject, (string Ripple, int Phase)> pair in _shown) Animate(pair.Key.transform, pair.Value.Ripple, pair.Value.Phase, t);
        }

        /// <summary>Each child (a ring) pulses in the species' way; weaker as the spot's time runs out.</summary>
        private void Animate(Transform spot, string ripple, int phase, float t)
        {
            float strength = 0.45f + 0.275f * Mathf.Clamp(phase, 0, 2);
            int i = 0;
            foreach (Transform ring in spot.GetComponentsInChildren<Transform>())
            {
                if (ring == spot || ring.GetComponent<Renderer>() == null) continue;
                if (!_baseScale.ContainsKey(ring))
                {
                    _baseScale[ring] = ring.localScale;
                    _basePosition[ring] = ring.localPosition;
                }
                Vector3 scale = _baseScale[ring], position = _basePosition[ring];
                float offset = i++ * 0.37f;
                float pulse;
                switch (ripple)
                {
                    case "quick":
                        pulse = Mathf.Repeat(t * 1.6f + offset, 1f);
                        ring.localScale = new Vector3(scale.x * (0.5f + pulse) * strength, scale.y, scale.z * (0.5f + pulse) * strength);
                        break;
                    case "bubbles":
                        pulse = Mathf.Repeat(t * 0.8f + offset, 1f);
                        ring.localScale = new Vector3(scale.x * (0.6f + 0.6f * pulse) * strength, scale.y, scale.z * (0.6f + 0.6f * pulse) * strength);
                        ring.localPosition = position + new Vector3(Mathf.Sin(t * 3f + offset) * 0.08f, 0f, Mathf.Cos(t * 2.3f + offset) * 0.08f);
                        break;
                    case "line":
                        pulse = Mathf.Repeat(t * 0.5f + offset, 1f);
                        ring.localScale = new Vector3(scale.x * (0.7f + 0.5f * pulse) * strength, scale.y, scale.z * 0.6f * strength);
                        ring.localPosition = position + new Vector3((pulse - 0.5f) * 0.5f, 0f, 0f);
                        break;
                    default:
                        pulse = Mathf.Repeat(t * 0.35f + offset, 1f);
                        ring.localScale = new Vector3(scale.x * (0.6f + 0.9f * pulse) * strength, scale.y, scale.z * (0.6f + 0.9f * pulse) * strength);
                        break;
                }
            }
        }

        private void Hide(MapObject o)
        {
            Renderer[] renderers = System.Array.FindAll(o.GetComponentsInChildren<Renderer>(), r => r.enabled);
            Collider[] colliders = System.Array.FindAll(o.GetComponentsInChildren<Collider>(), c => c.enabled);
            foreach (Renderer r in renderers) r.enabled = false;
            foreach (Collider c in colliders) c.enabled = false;
            _hidden[o] = (renderers, colliders);
        }

        private void Show(MapObject o)
        {
            if (!_hidden.TryGetValue(o, out (Renderer[] Renderers, Collider[] Colliders) was)) return;
            foreach (Renderer r in was.Renderers) if (r != null) r.enabled = true;
            foreach (Collider c in was.Colliders) if (c != null) c.enabled = true;
            _hidden.Remove(o);
        }
    }
}
