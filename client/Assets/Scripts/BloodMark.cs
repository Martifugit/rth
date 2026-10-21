using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// A mark set on someone (the Saint's "pay what you owe"): a thorned seal of blood over their
    /// head that beats like a heart, drains as the time runs out and drips; a ring at their feet
    /// ripples with each beat, and a red light rides with it. <see cref="WorldView"/> writes the
    /// count in the seal's middle. Purely visual; it removes itself when the time is up.
    /// </summary>
    public sealed class BloodMark : MonoBehaviour
    {
        private static readonly Color Blood = new Color(0.85f, 0.04f, 0.07f);
        private static readonly Color Bright = new Color(1f, 0.12f, 0.1f);
        private const float Size = 0.9f;
        private const int Thorns = 8;
        private const float Beat = 1.1f;

        /// <summary>How far above the head the seal floats (WorldView puts the count there).</summary>
        public const float Lift = 1.05f;

        private static Material _dots, _lines;

        private float _start, _seconds;
        private Transform _seal, _spin;
        private LineRenderer _ring, _countdown, _ground, _ripple;
        private LineRenderer[] _thorns;
        private ParticleSystem _drips;
        private Light _light;

        /// <summary>Marks <paramref name="view"/> for <paramref name="seconds"/>; a new mark replaces the old.</summary>
        public static void Attach(EntityView view, float seconds)
        {
            BloodMark old = view.GetComponentInChildren<BloodMark>();
            if (old != null) Destroy(old.gameObject);
            var go = new GameObject("Blood mark");
            go.transform.SetParent(view.transform, false);
            var mark = go.AddComponent<BloodMark>();
            mark._start = Time.time;
            mark._seconds = Mathf.Max(0.5f, seconds);
            mark.Build(view.Height);
        }

        private void Build(float height)
        {
            _seal = new GameObject("Seal").transform;
            _seal.SetParent(transform, false);
            _seal.localPosition = Vector3.up * (height + Lift);
            _spin = new GameObject("Spin").transform;
            _spin.SetParent(_seal, false);

            _ring = Line(_seal, "Ring", 0.05f, true);
            Circle(_ring, 0.46f, 1f, false);
            _countdown = Line(_seal, "Countdown", 0.07f, false);
            _thorns = new LineRenderer[Thorns];
            for (int i = 0; i < Thorns; i++)
            {
                _thorns[i] = Line(_spin, "Thorn", 0.09f, false);
                _thorns[i].widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f); // tapering to a point
                float a = i * Mathf.PI * 2f / Thorns;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                _thorns[i].positionCount = 2;
                _thorns[i].SetPosition(0, dir * 0.47f);
                _thorns[i].SetPosition(1, dir * (i % 2 == 0 ? 0.68f : 0.58f));
            }

            _ground = Line(transform, "Ground ring", 0.06f, true);
            Circle(_ground, 0.7f, 1f, true);
            _ripple = Line(transform, "Ripple", 0.04f, true);

            _drips = Drips(_seal);

            _light = new GameObject("Mark light").AddComponent<Light>();
            _light.transform.SetParent(_seal, false);
            _light.type = LightType.Point;
            _light.color = Blood;
            _light.range = 3.5f;
        }

        private void Update()
        {
            float age = Time.time - _start;
            float left = 1f - age / _seconds;
            if (left <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Pops in, then beats twice a second-ish like a heart (a strong beat and a softer one).
            float pop = age < 0.25f ? Mathf.Sin(age / 0.25f * Mathf.PI * 0.75f) * 1.2f : 1f;
            float fade = left * _seconds < 0.3f ? left * _seconds / 0.3f : 1f;
            float t = age % Beat;
            float beat = Mathf.Exp(-t * 14f) + 0.55f * Mathf.Exp(-Mathf.Max(0f, t - 0.22f) * 14f) * (t > 0.22f ? 1f : 0f);
            float urgency = Mathf.Lerp(1f, 1.8f, 1f - left); // it beats harder as the time runs out

            Camera cam = Camera.main;
            if (cam != null) _seal.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
            _seal.localScale = Vector3.one * Size * pop * fade * (1f + 0.1f * beat * urgency);
            _spin.localRotation = Quaternion.Euler(0f, 0f, -age * 30f);

            Color ring = Blood * (1.4f + 1.2f * beat * urgency);
            _ring.startColor = _ring.endColor = ring;
            foreach (LineRenderer thorn in _thorns) thorn.startColor = thorn.endColor = ring;
            Circle(_countdown, 0.37f, left, false);
            _countdown.startColor = _countdown.endColor = Bright * (1.6f + beat);

            Color ground = Blood * (0.6f + 1.4f * beat * urgency);
            ground.a = fade;
            _ground.startColor = _ground.endColor = ground;
            // A ripple runs out across the floor from each strong beat.
            float r = t / Beat;
            Circle(_ripple, Mathf.Lerp(0.7f, 1.6f, r), 1f, true);
            Color ripple = Blood * 1.5f;
            ripple.a = (1f - r) * fade;
            _ripple.startColor = _ripple.endColor = ripple;

            _light.intensity = (2f + 4f * beat * urgency) * fade;
        }

        private static LineRenderer Line(Transform parent, string name, float width, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.sharedMaterial = Lines();
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            return line;
        }

        /// <summary>A circle (or the first <paramref name="fraction"/> of one, from the top, clockwise).</summary>
        private static void Circle(LineRenderer line, float radius, float fraction, bool flat)
        {
            int points = Mathf.Max(2, Mathf.CeilToInt(56 * fraction) + 1);
            line.positionCount = line.loop ? points - 1 : points;
            for (int i = 0; i < line.positionCount; i++)
            {
                float a = Mathf.PI * 0.5f - i / (float)(points - 1) * fraction * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * radius, y = Mathf.Sin(a) * radius;
                line.SetPosition(i, flat ? new Vector3(x, 0.05f, y) : new Vector3(x, y, 0f));
            }
        }

        private static ParticleSystem Drips(Transform seal)
        {
            var go = new GameObject("Drips");
            go.transform.SetParent(seal, false);
            go.transform.localPosition = Vector3.down * 0.42f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = Blood * 1.6f;
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 7f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.5f, 0.02f, 0.05f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Dots();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.08f;
            ps.Play();
            return ps;
        }

        // Additive unlit materials, made once: a soft dot so drips read as drops, and a band soft
        // across its width so lines glow rather than look drawn.
        private static Material Dots() => _dots != null ? _dots : _dots = Additive("Blood mark drops", Soft(32, true));
        private static Material Lines() => _lines != null ? _lines : _lines = Additive("Blood mark lines", Soft(32, false));

        private static Material Additive(string name, Texture2D texture)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            m.SetTexture("_BaseMap", texture);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        /// <summary>A white texture fading out from its middle: round (a dot) or across its height only (a band).</summary>
        private static Texture2D Soft(int size, bool round)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var centre = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = round
                    ? Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre) / (size / 2f)
                    : Mathf.Abs(y + 0.5f - centre.y) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, round ? a * a : Mathf.Sqrt(a)));
            }
            tex.Apply();
            return tex;
        }
    }
}
