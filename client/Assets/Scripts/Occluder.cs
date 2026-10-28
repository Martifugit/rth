using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rth.Client
{
    /// <summary>
    /// Something that may stand between the camera and the player (a tree, a roof, a wall, a
    /// tower). <see cref="CameraOcclusion"/> asks it to fade when it's in the way; it fades smoothly
    /// to <see cref="minAlpha"/> and back, swapping to see-through copies of its materials only
    /// while faded. Roofs can also be forced fully hidden (see <see cref="RoofHider"/>).
    /// </summary>
    public sealed class Occluder : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Dictionary<Material, Material> FadeCopies = new Dictionary<Material, Material>();

        /// <summary>Every active occluder, for <see cref="CameraOcclusion"/> to test.</summary>
        public static readonly List<Occluder> All = new List<Occluder>();

        [Tooltip("How see-through it gets when blocking the view (0 = invisible).")]
        public float minAlpha = 0.25f;
        [Tooltip("Alpha change per second; higher fades faster.")]
        public float fadeSpeed = 3.5f;
        [Tooltip("Also fade when hiding anything this far around the player, not just the player (arenas: the fight is all around you). 0 = only the player.")]
        public float guardRadius;

        private Renderer[] _renderers;
        private Material[][] _opaque;
        private Material[][] _faded;
        private MaterialPropertyBlock _block;
        private float _alpha = 1f;
        private bool _usingFade;
        private bool _blocking;

        /// <summary>Set by roofs while the player is inside (or roofs are switched off).</summary>
        public bool ForceHidden { get; set; }

        /// <summary>World-space bounds of everything it draws.</summary>
        public Bounds Bounds { get; private set; }

        public void SetBlocking(bool blocking) => _blocking = blocking;

        private List<Vector3> _triangles;   // world space, three corners each; built on first need
        private List<Bounds> _loose;          // renderers whose mesh can't be read: their bounds stand in

        /// <summary>
        /// Whether the ray meets what it actually draws (its triangles, not the box round it) closer
        /// than <paramref name="within"/>: a tree's canopy box reaches down beside its trunk, where
        /// there is only air. Occluders don't move, so the triangles are taken once.
        /// </summary>
        public bool Hits(Ray ray, float within)
        {
            if (!Bounds.IntersectRay(ray, out float enter) || enter >= within) return false;
            if (_triangles == null) TakeTriangles();
            foreach (Bounds b in _loose)
                if (b.IntersectRay(ray, out float d) && d < within) return true;
            for (int i = 0; i < _triangles.Count; i += 3)
                if (RayTriangle(ray, _triangles[i], _triangles[i + 1], _triangles[i + 2], out float d) && d < within) return true;
            return false;
        }

        private void TakeTriangles()
        {
            _triangles = new List<Vector3>();
            _loose = new List<Bounds>();
            foreach (Renderer r in _renderers)
            {
                if (r == null) continue;
                Mesh mesh = r is MeshRenderer ? r.GetComponent<MeshFilter>()?.sharedMesh : null;
                // The Editor can always read a mesh; a build only one imported with Read/Write.
                if (mesh == null || (!mesh.isReadable && !Application.isEditor)) { _loose.Add(r.bounds); continue; }
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;
                Matrix4x4 m = r.localToWorldMatrix;
                for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(v[i]);
                foreach (int index in t) _triangles.Add(v[index]);
            }
        }

        /// <summary>Möller–Trumbore, both faces.</summary>
        private static bool RayTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0f;
            Vector3 e1 = b - a, e2 = c - a;
            Vector3 p = Vector3.Cross(ray.direction, e2);
            float det = Vector3.Dot(e1, p);
            if (det > -1e-7f && det < 1e-7f) return false;
            float inv = 1f / det;
            Vector3 s = ray.origin - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f) return false;
            Vector3 q = Vector3.Cross(s, e1);
            float w = Vector3.Dot(ray.direction, q) * inv;
            if (w < 0f || u + w > 1f) return false;
            distance = Vector3.Dot(e2, q) * inv;
            return distance > 0f;
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _opaque = new Material[_renderers.Length][];
            _faded = new Material[_renderers.Length][];
            _block = new MaterialPropertyBlock();
            if (_renderers.Length == 0) return;
            Bounds b = _renderers[0].bounds;
            for (int i = 0; i < _renderers.Length; i++)
            {
                b.Encapsulate(_renderers[i].bounds);
                _opaque[i] = _renderers[i].sharedMaterials;
                _faded[i] = new Material[_opaque[i].Length];
                for (int m = 0; m < _opaque[i].Length; m++) _faded[i][m] = FadeCopyOf(_opaque[i][m]);
            }
            Bounds = b;
        }

        /// <summary>
        /// Swaps materials (a <see cref="Recolor"/>) on the renderers under <paramref name="within"/>,
        /// whether or not it is faded right now, so fading back restores the new ones.
        /// Returns false before Awake, when there is nothing captured to change yet.
        /// </summary>
        public bool Remap(Transform within, System.Func<Material, Material> map)
        {
            if (_renderers == null) return false;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null || !r.transform.IsChildOf(within)) continue;
                for (int m = 0; m < _opaque[i].Length; m++)
                {
                    _opaque[i][m] = map(_opaque[i][m]);
                    _faded[i][m] = FadeCopyOf(_opaque[i][m]);
                }
                r.sharedMaterials = _usingFade ? _faded[i] : _opaque[i];
            }
            return true;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Update()
        {
            float target = ForceHidden ? 0f : _blocking ? minAlpha : 1f;
            if (Mathf.Approximately(_alpha, target)) return;
            _alpha = Mathf.MoveTowards(_alpha, target, fadeSpeed * Time.deltaTime);
            Apply();
        }

        private void Apply()
        {
            bool visible = _alpha > 0.001f;
            bool fade = visible && _alpha < 0.999f;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue; // destroyed with an unloaded region
                r.enabled = visible;
                if (fade != _usingFade) r.sharedMaterials = fade ? _faded[i] : _opaque[i];
                if (fade)
                {
                    for (int m = 0; m < _opaque[i].Length; m++)
                    {
                        Color c = _opaque[i][m] != null && _opaque[i][m].HasProperty(BaseColor) ? _opaque[i][m].GetColor(BaseColor) : Color.white;
                        c.a = _alpha;
                        _block.SetColor(BaseColor, c);
                        r.SetPropertyBlock(_block, m);
                    }
                }
                else if (_usingFade)
                {
                    r.SetPropertyBlock(null);
                }
            }
            _usingFade = fade;
        }

        /// <summary>
        /// The cache outlives a Play session (domain reload on Play is off) but its copies don't:
        /// Unity destroys them on leaving Play mode, and a destroyed material draws pink.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            FadeCopies.Clear();
            All.Clear();
        }

        /// <summary>A transparent twin of a URP Lit material, made once and shared.</summary>
        private static Material FadeCopyOf(Material source)
        {
            if (source == null) return null;
            if (FadeCopies.TryGetValue(source, out Material copy) && copy != null) return copy; // != null: not destroyed
            copy = new Material(source) { name = source.name + " (fade)" };
            copy.SetFloat("_Surface", 1f);
            copy.SetFloat("_Blend", 0f);
            copy.SetOverrideTag("RenderType", "Transparent");
            copy.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            copy.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            copy.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            copy.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            copy.SetFloat("_ZWrite", 0f);
            copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            copy.renderQueue = (int)RenderQueue.Transparent;
            FadeCopies[source] = copy;
            return copy;
        }
    }
}
