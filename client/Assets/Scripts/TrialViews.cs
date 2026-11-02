using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.Progress;
using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// What a trial (the long way back) shows in the world:
    /// <list type="bullet">
    /// <item>objects this player has used up this attempt (the "trial" state's <c>Used</c>: a kindled
    /// soul lantern) take their type's depleted look (objects.json "depletedModel"), through the same
    /// <see cref="NodeLook"/> the gathering nodes use;</item>
    /// <item>sprung tile triggers near the player (the "triggers" state: grasping hands) rise out of the
    /// ground in their trigger's model, hold, and sink again. Hidden ones are drawn by nothing else.</item>
    /// </list>
    /// </summary>
    public sealed class TrialViews : MonoBehaviour
    {
        public GameSession session;

        private const float Rise = 0.35f, Sink = 0.6f;

        private int _version = -1;
        private float _nextScan;
        private readonly Dictionary<(string, int, int), MapObject> _objects = new Dictionary<(string, int, int), MapObject>();
        private readonly Dictionary<MapObject, NodeLook> _spent = new Dictionary<MapObject, NodeLook>();
        private readonly Dictionary<(int, int, uint), Sprung> _sprung = new Dictionary<(int, int, uint), Sprung>();

        private sealed class Sprung
        {
            public GameObject Model;
            public float Born;
            public float Until;
        }

        private void Update()
        {
            if (session == null || !session.IsInGame) return;
            AnimateSprung();
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
            ShowSpent();
            ShowSprung();
        }

        private string DepletedModel(string type)
        {
            if (session.Content == null) return null;
            foreach (ObjectDef d in session.Content.Objects)
                if (d.Id == type) return d.DepletedModel;
            return null;
        }

        private TriggerDef Trigger(string type)
        {
            if (session.Content == null) return null;
            foreach (ObjectDef d in session.Content.Objects)
                if (d.Id == type) return d.Trigger;
            return null;
        }

        private void ShowSpent()
        {
            TrialState trial = session.ServerState<TrialState>("trial");
            var used = new HashSet<(int, int)>();
            if (trial != null && !string.IsNullOrEmpty(trial.Trial))
                foreach (int[] u in trial.Used)
                    if (u != null && u.Length >= 3) used.Add((u[1], u[2]));

            var wanted = new HashSet<MapObject>();
            foreach (KeyValuePair<(string Type, int X, int Y), MapObject> pair in _objects)
                if (pair.Value != null && used.Contains((pair.Key.X, pair.Key.Y))) wanted.Add(pair.Value);

            foreach (KeyValuePair<MapObject, NodeLook> pair in new List<KeyValuePair<MapObject, NodeLook>>(_spent))
                if (pair.Key == null || !wanted.Contains(pair.Key))
                {
                    if (pair.Value != null) pair.Value.Restore();
                    _spent.Remove(pair.Key);
                }
            foreach (MapObject o in wanted)
            {
                if (_spent.ContainsKey(o)) continue;
                NodeLook look = o.gameObject.GetComponent<NodeLook>() ?? o.gameObject.AddComponent<NodeLook>();
                look.Show(true, false, DepletedModel(o.objectType));
                _spent[o] = look;
            }
        }

        private void ShowSprung()
        {
            TriggersState state = session.ServerState<TriggersState>("triggers");
            if (state == null) return;
            foreach (SprungTrigger s in state.Sprung)
            {
                var key = (s.X, s.Y, s.Tick);
                if (_sprung.ContainsKey(key)) continue;
                TriggerDef def = Trigger(s.Type);
                GameObject model = null;
                if (def != null && !string.IsNullOrWhiteSpace(def.Model))
                {
                    var prefab = Resources.Load<GameObject>(def.Model);
                    if (prefab != null) model = Instantiate(prefab);
                }
                if (model == null)
                {
                    model = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(model.GetComponent<Collider>());
                    model.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
                }
                model.name = "Sprung " + s.Type;
                float ground = GroundHeight.Centre(new TileCoord(s.X, s.Y), s.Plane).y;
                model.transform.position = new Vector3(s.X + 0.5f, ground - 1.2f, s.Y + 0.5f);
                model.transform.rotation = Quaternion.Euler(0f, (s.X * 73 + s.Y * 37) % 360, 0f);
                float shown = (def?.ShowTicks ?? 4) * 0.6f;
                _sprung[key] = new Sprung { Model = model, Born = Time.time, Until = Time.time + shown };
            }
        }

        /// <summary>Up out of the ash, hold, back down; then gone.</summary>
        private void AnimateSprung()
        {
            if (_sprung.Count == 0) return;
            var done = new List<(int, int, uint)>();
            foreach (KeyValuePair<(int X, int Y, uint Tick), Sprung> pair in _sprung)
            {
                Sprung s = pair.Value;
                if (s.Model == null)
                {
                    done.Add(pair.Key);
                    continue;
                }
                float now = Time.time;
                float up = Mathf.Clamp01((now - s.Born) / Rise);
                float down = Mathf.Clamp01((now - s.Until) / Sink);
                float lift = Mathf.SmoothStep(0f, 1f, up) * (1f - Mathf.SmoothStep(0f, 1f, down));
                Vector3 p = s.Model.transform.position;
                float ground = GroundHeight.Centre(new TileCoord(pair.Key.X, pair.Key.Y), 0).y;
                s.Model.transform.position = new Vector3(p.x, ground - 1.2f * (1f - lift), p.z);
                if (now > s.Until + Sink)
                {
                    Destroy(s.Model);
                    done.Add(pair.Key);
                }
            }
            foreach (var key in done) _sprung.Remove(key);
        }

        private void OnDisable()
        {
            foreach (Sprung s in _sprung.Values) if (s.Model != null) Destroy(s.Model);
            _sprung.Clear();
        }
    }
}
