using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Shows the skilling states the server sends that change how placed objects look: a station's
    /// draw ("draw": the forge glows, brighter and flickering while it roars). The objects are
    /// MapObjects in the streamed region scenes, found by type and tile as NodeViews finds nodes.
    /// </summary>
    public sealed class SkillingViews : MonoBehaviour
    {
        public GameSession session;

        private int _version = -1;
        private float _nextScan;
        private readonly Dictionary<(string, int, int), MapObject> _objects = new Dictionary<(string, int, int), MapObject>();
        private readonly Dictionary<MapObject, ForgeGlow> _glows = new Dictionary<MapObject, ForgeGlow>();

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
                    Vector3 p = o.transform.position;
                    _objects[(o.objectType, Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z))] = o;
                }
            }
            if (!rescan && session.StateVersion == _version) return;
            _version = session.StateVersion;
            Draws();
        }

        private void Draws()
        {
            var wanted = new Dictionary<MapObject, DrawState>();
            foreach (DrawState state in session.ServerState<List<DrawState>>(DrawDef.StateKey))
                if (_objects.TryGetValue((state.Type, state.X, state.Y), out MapObject o) && o != null) wanted[o] = state;
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
