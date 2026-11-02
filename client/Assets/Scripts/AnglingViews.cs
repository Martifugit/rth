using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.Angling;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Trophy spots on the water (docs/fishing.md 3.1), from the "anglingSpots" state. Like a
    /// fishing spot, a trophy spot is only its effects: its scene parts are an Editor marker and
    /// hidden in play, and while it is active (and not spooked for you) it shows the fishing spots'
    /// bubbles and rings (<see cref="SkillingEffects.Bubbles"/>), in its species' way ("quick":
    /// small, quick rings; "bubbles": strings of bubbles; "line": rings drifting long; anything
    /// else: slow, wide rings), thinning as its time runs out. An inactive spot can't be clicked.
    /// </summary>
    public sealed class AnglingViews : MonoBehaviour
    {
        public GameSession session;

        private float _nextScan;
        private int _version = -1;
        private readonly Dictionary<(string, int, int), MapObject> _objects = new Dictionary<(string, int, int), MapObject>();
        private readonly HashSet<MapObject> _lookless = new HashSet<MapObject>();
        private readonly Dictionary<MapObject, (GameObject Fx, int Phase)> _effects = new Dictionary<MapObject, (GameObject, int)>();

        private void Update()
        {
            if (session == null || !session.IsInGame || session.Content?.Angling == null) return;
            var bySpot = new Dictionary<string, AnglingSpeciesDef>();
            foreach (AnglingSpeciesDef s in session.Content.Angling.Species) bySpot[s.SpotType] = s;
            if (bySpot.Count == 0) return;

            bool rescan = Time.unscaledTime >= _nextScan;
            if (rescan)
            {
                _nextScan = Time.unscaledTime + 2f;
                _objects.Clear();
                foreach (MapObject o in FindObjectsByType<MapObject>(FindObjectsInactive.Exclude))
                {
                    if (!bySpot.ContainsKey(o.objectType)) continue;
                    Vector3 p = o.transform.position;
                    _objects[(o.objectType, Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z))] = o;
                    if (_lookless.Add(o))
                        foreach (Renderer r in o.GetComponentsInChildren<Renderer>()) r.enabled = false;
                }
                _lookless.RemoveWhere(o => o == null);
            }
            if (!rescan && session.StateVersion == _version) return;
            _version = session.StateVersion;

            var active = new Dictionary<MapObject, AnglingSpotView>();
            foreach (AnglingSpotView v in session.ServerState<AnglingSpotsState>(AnglingSpotsState.StateKey).Spots)
                if (_objects.TryGetValue((v.Type, v.X, v.Y), out MapObject o) && o != null) active[o] = v;

            foreach (MapObject o in new List<MapObject>(_effects.Keys))
                if (o == null || !active.TryGetValue(o, out AnglingSpotView v) || v.Phase != _effects[o].Phase)
                {
                    if (_effects[o].Fx != null) Destroy(_effects[o].Fx);
                    _effects.Remove(o);
                }
            foreach (MapObject o in _objects.Values)
            {
                if (o == null) continue;
                bool on = active.TryGetValue(o, out AnglingSpotView view);
                foreach (Collider c in o.GetComponentsInChildren<Collider>()) c.enabled = on;
                if (!on || _effects.ContainsKey(o)) continue;
                _effects[o] = (Make(o, bySpot[o.objectType].Ripple, view.Phase), view.Phase);
            }
        }

        /// <summary>A fishing spot's effects, in the species' way and as strong as the time it has left.</summary>
        private static GameObject Make(MapObject spot, string ripple, int phase)
        {
            GameObject fx = SkillingEffects.Bubbles(spot);
            float strength = 0.45f + 0.275f * Mathf.Clamp(phase, 0, 2);
            ParticleSystem bubbles = fx.GetComponent<ParticleSystem>();
            Transform ringsGo = fx.transform.Find("Rings");
            ParticleSystem rings = ringsGo != null ? ringsGo.GetComponent<ParticleSystem>() : null;
            float bubbleRate, ringRate, ringLife = 3.1f, ringSize = 0.85f;
            switch (ripple)
            {
                case "quick": bubbleRate = 4f; ringRate = 2.6f; ringLife = 1.8f; ringSize = 0.55f; break;
                case "bubbles": bubbleRate = 16f; ringRate = 1.0f; break;
                case "line": bubbleRate = 3f; ringRate = 1.4f; ringLife = 3.6f; ringSize = 1.1f; break;
                default: bubbleRate = 4f; ringRate = 0.9f; ringLife = 4f; ringSize = 1.3f; break;
            }
            if (bubbles != null)
            {
                ParticleSystem.EmissionModule e = bubbles.emission;
                e.rateOverTime = bubbleRate * strength;
            }
            if (rings != null)
            {
                ParticleSystem.EmissionModule e = rings.emission;
                e.rateOverTime = ringRate * strength;
                ParticleSystem.MainModule m = rings.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(ringLife * 0.85f, ringLife * 1.15f);
                m.startSize = new ParticleSystem.MinMaxCurve(ringSize * 0.8f, ringSize * 1.2f);
                if (ripple == "line")
                {
                    // Rings strung out in a line, as if something long lay under them.
                    ParticleSystem.ShapeModule shape = rings.shape;
                    shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
                    shape.radius = 0.7f;
                }
            }
            return fx;
        }

        private void OnDisable()
        {
            foreach (var pair in _effects)
                if (pair.Value.Fx != null) Destroy(pair.Value.Fx);
            _effects.Clear();
        }
    }
}
