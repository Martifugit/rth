using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Rth.Client
{
    /// <summary>
    /// What a miner sees of their own work on a rock (the "mining" server state, GatheringSystem):
    /// <list type="bullet">
    /// <item>a bar pinned over the rock (the interface's world layer): it eases forward with each
    /// swing, flashes and sends up "+1 Copper ore" when it fills, shows a running sheen while their
    /// face is cracked, and its border warms with their heat;</item>
    /// <item>on the rock itself (Resources/Shaders/Ember), while a fire burns against it, cracks that
    /// glow as their side of the stone heats (near-white and pulsing at the peak, the moment to
    /// douse), and once doused show dark, fading as the crack is worked out;</item>
    /// <item>a burst of steam where the fire burns when they douse.</item>
    /// </list>
    /// Others see only the fire (NodeViews).
    /// </summary>
    public sealed class MiningView : MonoBehaviour
    {
        public GameSession session;

        private MapObject _rock;
        private string _rockKey = "";
        private VisualElement _bar, _fill, _sheen;
        private float _shown, _target, _heat, _crack, _flash;
        private double _lastProgress;
        private readonly List<GameObject> _embers = new List<GameObject>();
        private Material _emberMaterial;
        private static readonly Color Border = new Color(70 / 255f, 60 / 255f, 48 / 255f), Hot = new Color(1f, 0.45f, 0.12f);

        private void Update()
        {
            MiningState m = session != null && session.IsInGame ? session.ServerState<MiningState>(MiningState.StateKey) : new MiningState();
            if (string.IsNullOrEmpty(m.Type))
            {
                Clear();
                return;
            }
            string key = m.Type + "@" + m.Plane + "," + m.X + "," + m.Y;
            if (key != _rockKey || _rock == null)
            {
                Clear();
                _rockKey = key;
                foreach (MapObject o in FindObjectsByType<MapObject>(FindObjectsInactive.Exclude))
                {
                    Vector2Int t = o.Tile;
                    if (o.objectType == m.Type && t.x == m.X && t.y == m.Y) { _rock = o; break; }
                }
                if (_rock == null) return;
                _shown = _target = (float)m.Progress;
                _lastProgress = m.Progress;
                _heat = Mathf.Max(0f, (float)m.Heat);
                _crack = (float)m.Crack;
                _lastCrack = m.Crack;
            }

            // A new ore: the bar runs to full, flashes, and starts again.
            if (m.Progress < _lastProgress - 0.01)
            {
                _shown = 0f;
                _flash = 1f;
                Pop();
            }
            if (m.Crack > _lastCrack + 0.05) Steam();   // a douse: once, on the server's step up
            _lastCrack = m.Crack;
            _lastProgress = m.Progress;
            _target = (float)m.Progress;
            _shown = Mathf.MoveTowards(_shown, _target, Time.deltaTime * 0.9f);
            _heat = Mathf.MoveTowards(_heat, Mathf.Max(0f, (float)m.Heat), Time.deltaTime * 0.6f);
            _crack = Mathf.MoveTowards(_crack, (float)m.Crack, Time.deltaTime * 0.5f);
            _flash = Mathf.Max(0f, _flash - Time.deltaTime * 2.5f);

            DrawBar(m.Heat >= 0);
            DrawEmbers(m.Heat >= 0 || _crack > 0.01f);
        }

        // ------------------------------------------------------------------ the bar

        private void DrawBar(bool fire)
        {
            Ui.UiRoot ui = Ui.UiRoot.Instance;
            Camera cam = Camera.main;
            if (ui == null || ui.WorldLayer == null || cam == null || _rock == null) return;
            if (_bar == null)
            {
                _bar = new VisualElement { pickingMode = PickingMode.Ignore };
                _bar.AddToClassList("world-bar");
                _fill = new VisualElement { pickingMode = PickingMode.Ignore };
                _fill.AddToClassList("world-bar__fill");
                _sheen = new VisualElement { pickingMode = PickingMode.Ignore };
                _sheen.AddToClassList("world-bar__sheen");
                var gloss = new VisualElement { pickingMode = PickingMode.Ignore };
                gloss.AddToClassList("world-bar__gloss");
                _bar.Add(_fill);
                _bar.Add(_sheen);
                _bar.Add(gloss);
                ui.WorldLayer.Add(_bar);
            }
            Vector3 top = Top() + Vector3.up * 0.45f;
            Vector3 screen = cam.WorldToScreenPoint(top);
            _bar.style.display = screen.z > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            if (screen.z <= 0f || _bar.panel == null) return;
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(_bar.panel, top, cam);
            _bar.style.left = p.x;
            _bar.style.top = p.y;

            _fill.style.width = Length.Percent(Mathf.Clamp01(_shown) * 100f);
            Color fill = Color.Lerp(new Color(0.77f, 0.52f, 0.29f), new Color(1f, 0.82f, 0.5f), Mathf.Max(_flash, _crack > 0.01f ? 0.45f : 0f));
            _fill.style.backgroundColor = fill;
            bool cracked = _crack > 0.01f;
            _sheen.style.display = cracked ? DisplayStyle.Flex : DisplayStyle.None;
            if (cracked) _sheen.style.left = Length.Percent(Mathf.Repeat(Time.time * 70f, 140f) - 20f);
            // The border warms with the heat, and beats at the peak.
            float h = fire ? _heat : 0f;
            float beat = h > 0.95f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 9f) : 0f;
            Color border = Color.Lerp(Color.Lerp(Border, Hot, h * h), Color.white, Mathf.Max(_flash, beat * 0.5f));
            _bar.style.borderTopColor = _bar.style.borderBottomColor = _bar.style.borderLeftColor = _bar.style.borderRightColor = border;
        }

        /// <summary>"+1 Copper ore" rising off the bar as an ore comes.</summary>
        private void Pop()
        {
            Ui.UiRoot ui = Ui.UiRoot.Instance;
            Camera cam = Camera.main;
            if (ui == null || ui.WorldLayer == null || cam == null || _rock == null || _bar == null || _bar.panel == null) return;
            ObjectDef def = session.ObjectDefFor(_rock.objectType);
            string what = def?.Node != null && def.Node.Items.Count > 0 ? session.Item(def.Node.Items[0].Item)?.Name ?? "ore" : "ore";
            var label = new Label("+1 " + what) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("world-pop");
            ui.WorldLayer.Add(label);
            Vector3 at = Top() + Vector3.up * 0.65f;
            float start = Time.time;
            label.schedule.Execute(() =>
            {
                float t = (Time.time - start) / 1.2f;
                if (t >= 1f || cam == null || label.panel == null)
                {
                    label.RemoveFromHierarchy();
                    return;
                }
                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(label.panel, at, cam);
                label.style.left = p.x;
                label.style.top = p.y - 10f - t * 26f;
                label.style.opacity = 1f - t * t;
            }).Every(16).Until(() => label.panel == null);
        }

        /// <summary>Part of the fire laid against the rock (NodeViews), not the rock.</summary>
        public static bool UnderFire(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name == "Fire-setting") return true;
            return false;
        }

        private Vector3 Top()
        {
            float top = _rock.transform.position.y + 1f;
            foreach (Renderer r in _rock.GetComponentsInChildren<Renderer>())
                if (r.enabled && r.gameObject.name != "Outline" && r.gameObject.name != "Ember" && !UnderFire(r.transform)) top = Mathf.Max(top, r.bounds.max.y);
            return new Vector3(_rock.transform.position.x, top, _rock.transform.position.z);
        }

        // ------------------------------------------------------------------ the stone

        private void DrawEmbers(bool show)
        {
            if (!show)
            {
                ClearEmbers();
                return;
            }
            if (_embers.Count == 0 && _rock != null)
            {
                if (_emberMaterial == null)
                {
                    Shader shader = Resources.Load<Shader>("Shaders/Ember");
                    if (shader == null) return;
                    _emberMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                }
                foreach (MeshRenderer r in _rock.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!r.enabled || r.gameObject.name == "Outline" || r.gameObject.name == "Ember" || UnderFire(r.transform)) continue;
                    MeshFilter f = r.GetComponent<MeshFilter>();
                    if (f == null || f.sharedMesh == null) continue;
                    var ember = new GameObject("Ember") { hideFlags = HideFlags.DontSave };
                    ember.transform.SetParent(r.transform, false);
                    ember.AddComponent<MeshFilter>().sharedMesh = f.sharedMesh;
                    var mr = ember.AddComponent<MeshRenderer>();
                    var mats = new Material[f.sharedMesh.subMeshCount];
                    for (int i = 0; i < mats.Length; i++) mats[i] = _emberMaterial;
                    mr.sharedMaterials = mats;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    _embers.Add(ember);
                }
            }
            _emberMaterial.SetFloat("_Heat", _heat);
            _emberMaterial.SetFloat("_Crack", _crack);
            _emberMaterial.SetVector("_FireAt", FireAt());
        }

        private Vector3 _fireAt;
        private double _lastCrack;

        /// <summary>Where the logs burn against the rock (kept once the fire has gone, for the dark cracks); before any, the miner's side.</summary>
        private Vector3 FireAt()
        {
            if (_rock == null) return _fireAt;
            foreach (Transform t in _rock.GetComponentsInChildren<Transform>())
                if (t.name == "Fire-setting") return _fireAt = t.position;
            if (_fireAt == Vector3.zero)
            {
                WorldView world = FindAnyObjectByType<WorldView>();
                Vector3 face = world != null && world.Local != null ? world.Local.transform.position - _rock.transform.position : Vector3.back;
                face.y = 0f;
                return _rock.transform.position + face.normalized * 0.6f;
            }
            return _fireAt;
        }

        /// <summary>A burst of steam from the fire's side of the rock (a douse).</summary>
        private void Steam()
        {
            if (_rock == null) return;
            var go = new GameObject("Steam") { hideFlags = HideFlags.DontSave };
            // Off the logs, where the water hit the hot stone.
            go.transform.position = FireAt() + Vector3.up * 0.25f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.32f);
            main.startColor = new Color(0.95f, 0.95f, 0.93f, 0.4f);
            main.gravityModifier = -0.1f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12), new ParticleSystem.Burst(0.2f, 6) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.2f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f));
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            Shader particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particles != null)
            {
                var m = new Material(particles) { hideFlags = HideFlags.DontSave };
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetTexture("_BaseMap", Resources.Load<Texture2D>("UI/Skin/soft") ?? SoftDot());
                renderer.sharedMaterial = m;
            }
            ps.Play();
        }

        private static Texture2D _soft;

        /// <summary>A soft round dot, for steam puffs.</summary>
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

        // ------------------------------------------------------------------ clearing up

        private void ClearEmbers()
        {
            foreach (GameObject e in _embers)
                if (e != null) Destroy(e);
            _embers.Clear();
        }

        private void Clear()
        {
            ClearEmbers();
            _bar?.RemoveFromHierarchy();
            _bar = null;
            _rock = null;
            _rockKey = "";
            _fireAt = Vector3.zero;
        }

        private void OnDisable() => Clear();
    }
}
