using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.Items;
using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Shows the skilling states the server sends: a station's draw ("draw": the forge glows,
    /// brighter and flickering while it roars); objects set down while the game runs ("placed": a
    /// campfire, drawn from its type's model, clickable like any MapObject); and the gathering
    /// states ("gathering": roaming spots that aren't there are hidden, a trap shows on its node; a
    /// tree that needs tending has brambles round its trunk and a slow green-gold outline, and one
    /// that has been tended gives off drifting motes while it lasts). Region objects are MapObjects
    /// in the streamed scenes, found by type and tile as NodeViews finds nodes.
    /// </summary>
    public sealed class SkillingViews : MonoBehaviour
    {
        public GameSession session;

        private int _version = -1;
        private float _nextScan;
        private readonly Dictionary<(string, int, int), MapObject> _objects = new Dictionary<(string, int, int), MapObject>();
        private readonly Dictionary<MapObject, ForgeGlow> _glows = new Dictionary<MapObject, ForgeGlow>();
        private readonly Dictionary<(string, int, int, int), GameObject> _placed = new Dictionary<(string, int, int, int), GameObject>();
        private readonly Dictionary<MapObject, (Renderer[] Renderers, Collider[] Colliders)> _hidden = new Dictionary<MapObject, (Renderer[], Collider[])>();
        private readonly Dictionary<(MapObject, string), GameObject> _extras = new Dictionary<(MapObject, string), GameObject>();
        private readonly Dictionary<MapObject, List<GameObject>> _needOutlines = new Dictionary<MapObject, List<GameObject>>();
        private readonly Dictionary<MapObject, GameObject> _motes = new Dictionary<MapObject, GameObject>();
        private Material _needMaterial;
        private MaterialPropertyBlock _block;

        private void Update()
        {
            if (session == null || !session.IsInGame) return;
            bool rescan = Time.unscaledTime >= _nextScan;
            if (rescan)
            {
                _nextScan = Time.unscaledTime + 2f;
                _objects.Clear();
                foreach (MapObject o in FindObjectsByType<MapObject>(FindObjectsInactive.Exclude))
                {
                    if (o.GetComponent<PlacedMarker>() != null) continue;   // our own campfires aren't region objects
                    Vector3 p = o.transform.position;
                    _objects[(o.objectType, Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z))] = o;
                }
            }
            if (!rescan && session.StateVersion == _version) return;
            _version = session.StateVersion;
            Draws();
            Placed();
            Gathering();
        }

        private ObjectDef Def(string type)
        {
            if (session.Content == null) return null;
            foreach (ObjectDef d in session.Content.Objects)
                if (d.Id == type) return d;
            return null;
        }

        /// <summary>Whether the server says this node needs tending now (the "gathering" state).</summary>
        public static bool NeedsTending(GameSession session, string type, int plane, int x, int y)
        {
            foreach (NodeRef r in session.ServerState<GatheringWorldState>(GatheringWorldState.StateKey).NeedsTending)
                if (r.Type == type && r.Plane == plane && r.X == x && r.Y == y) return true;
            return false;
        }

        private MapObject Find(string type, int x, int y) => _objects.TryGetValue((type, x, y), out MapObject o) && o != null ? o : null;

        // ------------------------------------------------------------------ draws

        private void Draws()
        {
            var wanted = new Dictionary<MapObject, DrawState>();
            foreach (DrawState state in session.ServerState<List<DrawState>>(DrawDef.StateKey))
            {
                MapObject o = Find(state.Type, state.X, state.Y);
                if (o != null) wanted[o] = state;
            }
            foreach (KeyValuePair<MapObject, ForgeGlow> pair in new List<KeyValuePair<MapObject, ForgeGlow>>(_glows))
                if (pair.Key == null || !wanted.ContainsKey(pair.Key))
                {
                    if (pair.Value != null) Destroy(pair.Value.gameObject);
                    _glows.Remove(pair.Key);
                }
            foreach (KeyValuePair<MapObject, DrawState> pair in wanted)
            {
                if (!_glows.TryGetValue(pair.Key, out ForgeGlow glow) || glow == null)
                {
                    var go = new GameObject("Draw glow");
                    go.transform.SetParent(pair.Key.transform, false);
                    go.transform.localPosition = Vector3.up * 0.9f;
                    _glows[pair.Key] = glow = go.AddComponent<ForgeGlow>();
                }
                glow.Set((float)pair.Value.Level, pair.Value.Hot);
            }
        }

        // ------------------------------------------------------------------ objects set down (campfires)

        private void Placed()
        {
            var wanted = new HashSet<(string, int, int, int)>();
            foreach (PlacedState s in session.ServerState<List<PlacedState>>(PlacedState.StateKey))
            {
                var key = (s.Type, s.Plane, s.X, s.Y);
                wanted.Add(key);
                if (_placed.TryGetValue(key, out GameObject existing) && existing != null) continue;
                _placed[key] = MakePlaced(s);
            }
            foreach (KeyValuePair<(string, int, int, int), GameObject> pair in new List<KeyValuePair<(string, int, int, int), GameObject>>(_placed))
                if (!wanted.Contains(pair.Key))
                {
                    if (pair.Value != null) Destroy(pair.Value);
                    _placed.Remove(pair.Key);
                }
        }

        private GameObject MakePlaced(PlacedState s)
        {
            ObjectDef def = Def(s.Type);
            var go = new GameObject("Placed " + s.Type);
            go.transform.position = GroundHeight.Centre(new TileCoord(s.X, s.Y), s.Plane);
            go.AddComponent<PlacedMarker>();
            MapObject marker = go.AddComponent<MapObject>();
            marker.objectType = s.Type;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.3f, 0f);
            box.size = new Vector3(0.8f, 0.6f, 0.8f);
            GameObject model = def != null && !string.IsNullOrWhiteSpace(def.Model) ? Resources.Load<GameObject>(def.Model) : null;
            if (model != null) Instantiate(model, go.transform, false);
            else
            {
                if (def != null && !string.IsNullOrWhiteSpace(def.Model)) Debug.LogWarning($"[R'th] object model '{def.Model}' isn't under Resources.");
                GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(stand.GetComponent<Collider>());
                stand.transform.SetParent(go.transform, false);
                stand.transform.localScale = new Vector3(0.6f, 0.1f, 0.6f);
                stand.transform.localPosition = Vector3.up * 0.1f;
            }
            if (def != null && !string.IsNullOrWhiteSpace(def.Glow) && ColorUtility.TryParseHtmlString(def.Glow, out Color colour))
            {
                var light = new GameObject("Glow");
                light.transform.SetParent(go.transform, false);
                light.transform.localPosition = Vector3.up * 0.5f;
                Light l = light.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = colour;
                l.range = 4f;
                l.intensity = 1.6f;
                light.AddComponent<LightFlicker>();
            }
            return go;
        }

        // ------------------------------------------------------------------ roaming, traps, tending

        private void Gathering()
        {
            GatheringWorldState state = session.ServerState<GatheringWorldState>(GatheringWorldState.StateKey);

            var dormant = new HashSet<MapObject>();
            foreach (NodeRef r in state.Dormant)
            {
                MapObject o = Find(r.Type, r.X, r.Y);
                if (o != null) dormant.Add(o);
            }
            foreach (MapObject o in new List<MapObject>(_hidden.Keys))
                if (o == null || !dormant.Contains(o)) Show(o);
            foreach (MapObject o in dormant)
                if (!_hidden.ContainsKey(o)) Hide(o);

            var extras = new Dictionary<(MapObject, string), string>();
            foreach (TrapState t in state.Traps)
            {
                MapObject o = Find(t.Type, t.X, t.Y);
                NodeDef node = Def(t.Type)?.Node;
                if (o != null && node?.Trap != null) extras[(o, "trap")] = node.Trap.Model;
            }
            foreach (NodeRef r in state.NeedsTending)
            {
                MapObject o = Find(r.Type, r.X, r.Y);
                NodeDef node = Def(r.Type)?.Node;
                if (o != null && node?.Tending != null) extras[(o, "tend")] = node.Tending.NeedModel;
            }
            foreach (KeyValuePair<(MapObject, string), GameObject> pair in new List<KeyValuePair<(MapObject, string), GameObject>>(_extras))
                if (pair.Key.Item1 == null || !extras.ContainsKey(pair.Key))
                {
                    if (pair.Value != null) Destroy(pair.Value);
                    _extras.Remove(pair.Key);
                }
            foreach (KeyValuePair<(MapObject, string), string> pair in extras)
            {
                if (_extras.TryGetValue(pair.Key, out GameObject had) && had != null) continue;
                bool tend = pair.Key.Item2 == "tend";
                // Brambles wrap the trunk itself, big enough to see under the canopy; a trap sits at the foot.
                _extras[pair.Key] = Extra(pair.Key.Item1, pair.Value, tend ? 1.15f : 0.7f, tend ? Vector3.zero : new Vector3(0.05f, 0f, -0.45f));
            }

            var needy = new HashSet<MapObject>();
            foreach (KeyValuePair<(MapObject, string), string> pair in extras)
                if (pair.Key.Item2 == "tend") needy.Add(pair.Key.Item1);
            NeedOutlines(needy);

            var tended = new HashSet<MapObject>();
            foreach (NodeRef r in state.Tended)
            {
                MapObject o = Find(r.Type, r.X, r.Y);
                if (o != null && Def(r.Type)?.Node?.Tending != null) tended.Add(o);
            }
            foreach (MapObject o in new List<MapObject>(_motes.Keys))
                if (o == null || !tended.Contains(o))
                {
                    if (_motes[o] != null) Destroy(_motes[o]);
                    _motes.Remove(o);
                }
            foreach (MapObject o in tended)
                if (!_motes.TryGetValue(o, out GameObject m) || m == null) _motes[o] = Motes(o);
        }

        /// <summary>A slow, breathing green-gold outline on each tree that needs tending (FocusHighlight's outlines).</summary>
        private void NeedOutlines(HashSet<MapObject> needy)
        {
            foreach (MapObject o in new List<MapObject>(_needOutlines.Keys))
                if (o == null || !needy.Contains(o))
                {
                    foreach (GameObject g in _needOutlines[o]) if (g != null) Destroy(g);
                    _needOutlines.Remove(o);
                }
            if (needy.Count == 0) return;
            if (_needMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("Shaders/Highlight");
                if (shader == null) return;
                _needMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                _needMaterial.SetColor("_Color", new Color(0.72f, 0.95f, 0.45f, 1f));
                _needMaterial.SetFloat("_Halo", 0.9f);
                _needMaterial.SetFloat("_Width", 0.03f);
                _needMaterial.SetFloat("_Breathe", 0.7f);
                _block = new MaterialPropertyBlock();
            }
            foreach (MapObject o in needy)
                if (!_needOutlines.TryGetValue(o, out List<GameObject> had) || had.Exists(g => g == null))
                {
                    if (had != null) foreach (GameObject g in had) if (g != null) Destroy(g);
                    _needOutlines[o] = FocusHighlight.Outlines(o.transform, _needMaterial, _block);
                }
        }

        /// <summary>Pale green-gold motes drifting up through a tended tree's crown.</summary>
        private static GameObject Motes(MapObject tree)
        {
            var bounds = new Bounds(tree.transform.position + Vector3.up * 2f, Vector3.one * 2f);
            bool first = true;
            foreach (Renderer r in tree.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r.name == "Outline") continue;
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            var go = new GameObject("Tended motes") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(tree.transform, false);
            go.transform.position = bounds.center + Vector3.up * bounds.extents.y * 0.2f;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 1f, 0.55f, 0.9f), new Color(1f, 0.92f, 0.5f, 0.9f));
            main.gravityModifier = -0.015f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 7f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.6f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.8f);
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (unlit != null) renderer.sharedMaterial = new Material(unlit) { hideFlags = HideFlags.DontSave };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return go;
        }

        /// <summary>A model set on a node (a trap at its foot, brambles round a trunk), or a small marker if the model is missing.</summary>
        private static GameObject Extra(MapObject on, string path, float scale, Vector3 offset)
        {
            GameObject model = string.IsNullOrWhiteSpace(path) ? null : Resources.Load<GameObject>(path);
            GameObject go = model != null ? Instantiate(model) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            go.name = "Skilling extra";
            go.transform.SetParent(on.transform, true);
            Vector3 foot = on.transform.position + offset;
            go.transform.position = foot;
            go.transform.localScale = model != null ? Vector3.one * scale : Vector3.one * 0.25f;
            return go;
        }

        private void Hide(MapObject o)
        {
            // Only what is showing now: another view (NodeViews' stand-ins) may have hidden the rest.
            Renderer[] renderers = System.Array.FindAll(o.GetComponentsInChildren<Renderer>(), r => r.enabled);
            Collider[] colliders = System.Array.FindAll(o.GetComponentsInChildren<Collider>(), c => c.enabled);
            foreach (Renderer r in renderers) r.enabled = false;
            foreach (Collider c in colliders) c.enabled = false;
            _hidden[o] = (renderers, colliders);
        }

        private void Show(MapObject o)
        {
            if (o != null && _hidden.TryGetValue(o, out (Renderer[] Renderers, Collider[] Colliders) was))
            {
                foreach (Renderer r in was.Renderers) if (r != null) r.enabled = true;
                foreach (Collider c in was.Colliders) if (c != null) c.enabled = true;
            }
            _hidden.Remove(o);
        }
    }

    /// <summary>Marks an object the client made for something set down (a campfire), not part of a region scene.</summary>
    public sealed class PlacedMarker : MonoBehaviour
    {
    }

    /// <summary>A station's fire light: brighter the fuller its draw, flickering while it roars.</summary>
    public sealed class ForgeGlow : MonoBehaviour
    {
        private Light _light;
        private float _level;
        private bool _hot;

        public void Set(float level, bool hot)
        {
            if (_light == null)
            {
                _light = gameObject.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.color = new Color(1f, 0.5f, 0.18f);
                _light.shadows = LightShadows.None;
            }
            _level = Mathf.Clamp01(level);
            _hot = hot;
        }

        private void Update()
        {
            if (_light == null) return;
            float flicker = _hot ? 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 6f, 0.37f) : 1f;
            _light.intensity = (0.4f + 2.2f * _level) * flicker;
            _light.range = 2.5f + 3f * _level;
        }
    }
}
