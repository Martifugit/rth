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
    /// states ("gathering": a fishing spot is only its effects (its scene parts, if any, are an Editor
    /// marker and hidden in play): it bubbles while it's there, is gone while it isn't, shimmers just
    /// before it comes back, and flares when you cast into the shimmer; a trap shows on its node; a
    /// tree that needs tending sheds withered leaves under a slow amber outline, one that has been
    /// tended glows with rising pollen while it lasts, and tending it gives a burst (SkillingEffects)).
    /// Region objects are MapObjects
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
        private readonly Dictionary<MapObject, GameObject> _withering = new Dictionary<MapObject, GameObject>();
        private readonly Dictionary<MapObject, GameObject> _thriving = new Dictionary<MapObject, GameObject>();
        private readonly Dictionary<MapObject, GameObject> _bubbles = new Dictionary<MapObject, GameObject>();
        private readonly Dictionary<MapObject, GameObject> _arriving = new Dictionary<MapObject, GameObject>();
        private readonly HashSet<MapObject> _lookless = new HashSet<MapObject>();
        private (string, int, int) _castAt;
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
            Cast();
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

            // A node that comes and goes is drawn by its effects alone.
            foreach (MapObject o in _objects.Values)
                if (o != null && !_lookless.Contains(o) && Def(o.objectType)?.Node?.Roam != null)
                {
                    Transform look = o.transform.Find("Look");
                    if (look != null)
                        foreach (Renderer r in look.GetComponentsInChildren<Renderer>()) r.enabled = false;
                    _lookless.Add(o);
                }
            _lookless.RemoveWhere(o => o == null);

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
            // Water shimmering where fish are about to come: the spot still unseen, but there to cast into.
            var bubbling = new HashSet<MapObject>();
            foreach (NodeRef r in state.Bubbling)
            {
                MapObject o = Find(r.Type, r.X, r.Y);
                if (o != null) bubbling.Add(o);
            }
            foreach (KeyValuePair<MapObject, (Renderer[] Renderers, Collider[] Colliders)> pair in _hidden)
                foreach (Collider c in pair.Value.Colliders)
                    if (c != null) c.enabled = bubbling.Contains(pair.Key);
            Keep(_arriving, bubbling, SkillingEffects.Arriving);
            // A spot that is there bubbles: every node that comes and goes and isn't gone now.
            var here = new HashSet<MapObject>();
            foreach (MapObject o in _objects.Values)
                if (o != null && !dormant.Contains(o) && Def(o.objectType)?.Node?.Roam != null) here.Add(o);
            Keep(_bubbles, here, SkillingEffects.Bubbles);

            var extras = new Dictionary<(MapObject, string), string>();
            foreach (TrapState t in state.Traps)
            {
                MapObject o = Find(t.Type, t.X, t.Y);
                NodeDef node = Def(t.Type)?.Node;
                if (o != null && node?.Trap != null) extras[(o, "trap")] = node.Trap.Model;
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
                _extras[pair.Key] = Extra(pair.Key.Item1, pair.Value, 0.7f, new Vector3(0.05f, 0f, -0.45f));   // a trap at the foot
            }

            HashSet<MapObject> needy = Tending(state.NeedsTending);
            HashSet<MapObject> tended = Tending(state.Tended);
            NeedOutlines(needy);
            // A tree that needed tending and now has had it: the moment it was tended.
            foreach (MapObject o in tended)
                if (_withering.ContainsKey(o) && !needy.Contains(o)) SkillingEffects.TendedBurst(o);
            Keep(_withering, needy, SkillingEffects.Withering);
            Keep(_thriving, tended, SkillingEffects.Thriving);
        }

        /// <summary>
        /// The moment the player casts into shimmering water (their interaction is on a spot about to
        /// come, and they have stopped beside it): a flare, once per spot, so they know it took.
        /// </summary>
        private void Cast()
        {
            InteractionFocus focus = session.ServerState<InteractionFocus>(InteractionFocus.StateKey);
            if (focus == null || focus.Kind != InteractionFocus.Object || focus.Type == null)
            {
                _castAt = default;
                return;
            }
            var key = (focus.Type, focus.X, focus.Y);
            if (key == _castAt) return;
            MapObject spot = Find(focus.Type, focus.X, focus.Y);
            if (spot == null || !_arriving.ContainsKey(spot)) return;
            EntityView me = session.world != null ? session.world.Find(session.LocalPlayerId) : null;
            if (me == null || me.IsWalking) return;
            Vector3 d = me.transform.position - spot.transform.position;
            if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.z)) > 2.2f) return;
            _castAt = key;
            SkillingEffects.Cast(spot);
        }

        private HashSet<MapObject> Tending(List<NodeRef> refs)
        {
            var found = new HashSet<MapObject>();
            foreach (NodeRef r in refs)
            {
                MapObject o = Find(r.Type, r.X, r.Y);
                if (o != null && Def(r.Type)?.Node?.Tending != null) found.Add(o);
            }
            return found;
        }

        /// <summary>One effect on each wanted object: made where it's missing, taken off where it's no longer wanted.</summary>
        private static void Keep(Dictionary<MapObject, GameObject> shown, HashSet<MapObject> wanted, System.Func<MapObject, GameObject> make)
        {
            foreach (MapObject o in new List<MapObject>(shown.Keys))
                if (o == null || !wanted.Contains(o))
                {
                    if (shown[o] != null) Destroy(shown[o]);
                    shown.Remove(o);
                }
            foreach (MapObject o in wanted)
                if (!shown.TryGetValue(o, out GameObject g) || g == null) shown[o] = make(o);
        }

        /// <summary>A slow, breathing amber outline on each tree that needs tending (FocusHighlight's outlines).</summary>
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
                _needMaterial.SetColor("_Color", new Color(1f, 0.72f, 0.32f, 1f));
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

        /// <summary>A model set on a node (a trap at its foot), or a small marker if the model is missing.</summary>
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
