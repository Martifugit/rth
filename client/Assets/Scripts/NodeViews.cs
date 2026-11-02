using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Shows the state of gathering nodes the server says aren't ordinary (the "nodes" server
    /// state): a depleted node shows its object type's depleted model (objects.json
    /// "depletedModel": a felled tree's stump, a picked bush) or else sinks and dims; a surging
    /// one (a hungry fount) glows and pulses. The nodes themselves are MapObjects in the streamed
    /// region scenes.
    /// </summary>
    public sealed class NodeViews : MonoBehaviour
    {
        public GameSession session;

        private int _version = -1;
        private float _nextScan;
        private readonly Dictionary<(string, int, int), MapObject> _objects = new Dictionary<(string, int, int), MapObject>();
        private readonly Dictionary<MapObject, NodeLook> _looks = new Dictionary<MapObject, NodeLook>();

        private void Update()
        {
            if (session == null || !session.IsInGame) return;
            // Regions stream in and out, so look for their objects again now and then.
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

            var depletedModels = new Dictionary<string, string>();
            if (session.Content != null)
                foreach (ObjectDef d in session.Content.Objects)
                    if (!string.IsNullOrWhiteSpace(d.DepletedModel)) depletedModels[d.Id] = d.DepletedModel;

            var wanted = new Dictionary<MapObject, NodeState>();
            foreach (NodeState state in session.ServerState<List<NodeState>>("nodes"))
                if (_objects.TryGetValue((state.Type, state.X, state.Y), out MapObject o) && o != null) wanted[o] = state;

            foreach (KeyValuePair<MapObject, NodeLook> pair in new List<KeyValuePair<MapObject, NodeLook>>(_looks))
                if (pair.Key == null || !wanted.ContainsKey(pair.Key))
                {
                    if (pair.Value != null) pair.Value.Restore();
                    _looks.Remove(pair.Key);
                }
            foreach (KeyValuePair<MapObject, NodeState> pair in wanted)
            {
                if (!_looks.TryGetValue(pair.Key, out NodeLook look) || look == null)
                    _looks[pair.Key] = look = pair.Key.gameObject.AddComponent<NodeLook>();
                depletedModels.TryGetValue(pair.Key.objectType, out string depletedModel);
                look.Show(pair.Value.Depleted, pair.Value.Surging, depletedModel, pair.Value.Burning, new Vector2Int(pair.Value.FireX, pair.Value.FireY));
            }
        }
    }

    /// <summary>
    /// One node's changed look, undone by <see cref="Restore"/>. A depleted node with a depleted
    /// model hides its own renderers and shows that model in their place; one without sinks and dims.
    /// A fire set against it (fire-setting) burns at its foot, with a flickering light.
    /// </summary>
    public sealed class NodeLook : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        private Renderer[] _renderers;
        private Light _glow;
        private bool _depleted;

        /// <summary>Showing its depleted look now (used up, picked, felled).</summary>
        public bool Depleted => _depleted;
        private bool _surging;
        private Vector3 _baseScale;
        private GameObject _stand;      // the depleted model, while it stands in
        private GameObject _fire;
        private Light _fireLight;

        public void Show(bool depleted, bool surging, string depletedModel = null, bool burning = false, Vector2Int fireTowards = default)
        {
            if (_renderers == null)
            {
                _renderers = GetComponentsInChildren<Renderer>();
                _baseScale = transform.localScale;
            }
            _depleted = depleted;
            _surging = surging;
            GameObject model = depleted && !string.IsNullOrWhiteSpace(depletedModel) ? Resources.Load<GameObject>(depletedModel) : null;
            if (depleted && !string.IsNullOrWhiteSpace(depletedModel) && model == null)
                Debug.LogWarning($"[R'th] depleted model '{depletedModel}' isn't under Resources.");
            Swap(model);
            transform.localScale = depleted && model == null ? new Vector3(_baseScale.x, _baseScale.y * 0.7f, _baseScale.z) : _baseScale;
            if (surging && _glow == null)
            {
                var go = new GameObject("Hungry glow");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * 0.8f;
                _glow = go.AddComponent<Light>();
                _glow.type = LightType.Point;
                _glow.color = new Color(0.9f, 0.12f, 0.15f);
                _glow.range = 3.5f;
            }
            if (!surging && _glow != null)
            {
                Destroy(_glow.gameObject);
                _glow = null;
            }
            Fire(burning, fireTowards);
            Tint();
        }

        /// <summary>
        /// A fire set against the rock (fire-setting), or none: logs burning on the ground at the face
        /// it was laid from (just outside the rock, towards that tile), with flames, a thread of
        /// smoke and a flickering light.
        /// </summary>
        private void Fire(bool burning, Vector2Int towards)
        {
            if (burning && _fire == null)
            {
                Vector3 centre = transform.position;
                var dir = new Vector3(towards.x + 0.5f - Mathf.Floor(centre.x) - 0.5f, 0f, towards.y + 0.5f - Mathf.Floor(centre.z) - 0.5f);
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
                Vector3 at = new Vector3(Mathf.Floor(centre.x) + 0.5f, 0f, Mathf.Floor(centre.z) + 0.5f) + dir.normalized * 0.5f;
                int plane = Rth.MapTools.MapLevel.Of(this);
                at.y = GroundHeight.At(at.x, at.z, plane, centre.y);
                _fire = new GameObject("Fire-setting");
                _fire.transform.SetParent(transform, true);
                _fire.transform.position = at;
                _fire.transform.rotation = Quaternion.LookRotation(-dir.normalized);
                GameObject model = Resources.Load<GameObject>("Art/Assets/Lights/Common/Campfire");
                if (model != null)
                {
                    GameObject logs = Instantiate(model, _fire.transform, false);
                    logs.transform.localScale = Vector3.one * 0.42f;
                }
                Flames(_fire.transform);
                var light = new GameObject("Firelight");
                light.transform.SetParent(_fire.transform, false);
                light.transform.localPosition = Vector3.up * 0.45f;
                _fireLight = light.AddComponent<Light>();
                _fireLight.type = LightType.Point;
                _fireLight.color = new Color(1f, 0.55f, 0.2f);
                _fireLight.range = 3f;
            }
            if (!burning && _fire != null)
            {
                Destroy(_fire);
                _fire = null;
                _fireLight = null;
            }
        }

        private static Material _flameMaterial, _smokeMaterial;

        /// <summary>Flames licking up off the logs, and a thin smoke above them.</summary>
        private static void Flames(Transform parent)
        {
            if (_flameMaterial == null)
            {
                _flameMaterial = Particles(true);
                _smokeMaterial = Particles(false);
            }
            Emit(parent, "Flames", _flameMaterial, 22f, 0.35f, 0.6f, 0.12f, 0.26f, 0.5f, 1.0f, new Color(1f, 0.62f, 0.2f, 0.9f), new Color(1f, 0.25f, 0.05f, 0f), 0.08f);
            Emit(parent, "Smoke", _smokeMaterial, 5f, 1.6f, 2.6f, 0.25f, 0.5f, 0.35f, 0.7f, new Color(0.35f, 0.33f, 0.31f, 0.35f), new Color(0.5f, 0.5f, 0.5f, 0f), 0.12f, 0.35f);
        }

        private static void Emit(Transform parent, string name, Material material, float rate, float lifeMin, float lifeMax, float sizeMin, float sizeMax,
            float speedMin, float speedMax, Color start, Color end, float radius, float lift = 0.05f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * lift;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startColor = start;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = radius;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(start.a * 0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            colour.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, name == "Flames" ? 0.2f : 2.2f));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            ps.Play();
        }

        /// <summary>A soft particle material: added light for flames, blended for smoke.</summary>
        private static Material Particles(bool additive)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(shader) { hideFlags = HideFlags.DontSave };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetTexture("_BaseMap", SoftDot());
            return m;
        }

        private static Texture2D _soft;

        private static Texture2D SoftDot()
        {
            if (_soft != null) return _soft;
            const int n = 32;
            _soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    _soft.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            _soft.Apply();
            return _soft;
        }

        private void Update()
        {
            if (_glow != null) _glow.intensity = 1.6f + Mathf.Sin(Time.time * 5f) * 0.8f;
            if (_fireLight != null) _fireLight.intensity = 1.4f + Mathf.PerlinNoise(Time.time * 6f, 0.3f) * 1.2f;
        }

        /// <summary>Show <paramref name="model"/> in place of the node's own renderers, or (null) the node again.</summary>
        private void Swap(GameObject model)
        {
            if (_stand != null && (model == null || _stand.name != model.name))
            {
                Destroy(_stand);
                _stand = null;
            }
            if (model != null && _stand == null)
            {
                _stand = Instantiate(model, transform, false);
                _stand.name = model.name;
            }
            if (_renderers == null) return;
            foreach (Renderer r in _renderers)
                if (r != null) r.enabled = _stand == null;
        }

        private void Tint()
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                Color baseColour = r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColor) ? r.sharedMaterial.GetColor(BaseColor) : Color.white;
                block.SetColor(BaseColor, _depleted && _stand == null ? baseColour * 0.45f : baseColour);
                block.SetColor(Emission, _surging ? new Color(0.6f, 0.05f, 0.08f) : Color.black);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>Back to the ordinary look, and remove this component.</summary>
        public void Restore()
        {
            _depleted = false;
            _surging = false;
            Swap(null);
            if (_renderers != null)
            {
                transform.localScale = _baseScale;
                foreach (Renderer r in _renderers)
                    if (r != null) r.SetPropertyBlock(null);
            }
            if (_glow != null) Destroy(_glow.gameObject);
            Fire(false, default);
            Destroy(this);
        }
    }
}
