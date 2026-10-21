using System.Collections.Generic;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>Small helpers the effect components share: emission and colour through property blocks.</summary>
    internal static class Fx
    {
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock _block;

        private static MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        public static void Glow(Renderer r, Color colour)
        {
            if (r == null) return;
            r.GetPropertyBlock(Block);
            Block.SetColor(Emission, colour);
            r.SetPropertyBlock(Block);
        }

        public static void Glow(IEnumerable<Renderer> renderers, Color colour)
        {
            if (renderers == null) return;
            foreach (Renderer r in renderers) Glow(r, colour);
        }

        /// <summary>The colour (and alpha) of an unlit, additive or transparent material.</summary>
        public static void Tint(Renderer r, Color colour)
        {
            if (r == null) return;
            r.GetPropertyBlock(Block);
            Block.SetColor(BaseColor, colour);
            r.SetPropertyBlock(Block);
        }

        public static void Rate(ParticleSystem ps, float perSecond)
        {
            if (ps == null) return;
            ParticleSystem.EmissionModule e = ps.emission;
            e.rateOverTimeMultiplier = perSecond;
        }
    }

    /// <summary>
    /// A fount's "light" effect (Let there be light): a ring of light rises round it and brightens
    /// for <c>ramp</c> seconds, sparks streaming up, faster in the last two; then a white flash and
    /// burst (the scorch), and the fount stays dry for <c>dry</c> seconds.
    /// </summary>
    public sealed class FountLight : MonoBehaviour, IObjectEffect
    {
        public Transform ring;
        public Renderer[] blades;
        public Light ringLight;
        public ParticleSystem sparks;
        public ParticleSystem scorch;
        public Renderer blood;
        public ParticleSystem drips;
        public Light glow;
        public Color bloodGlow = new Color(0.3f, 0.015f, 0.03f) * 0.35f;

        private float _start = -1f, _ramp = 10f, _dry = 20f, _dripRate;
        private Color _light = new Color(1f, 0.9f, 0.65f);

        private void Awake()
        {
            _dripRate = drips != null ? drips.emission.rateOverTimeMultiplier : 0f;
            Show(false);
        }

        public void Play(string effect, IReadOnlyDictionary<string, string> args)
        {
            if (effect != "light") return;
            _ramp = Mathf.Max(0.5f, ObjectEffects.Float(args, "ramp", 10f));
            _dry = ObjectEffects.Float(args, "dry", 20f);
            _start = Time.time;
            Show(true);
        }

        private void Show(bool on)
        {
            if (ring != null) ring.gameObject.SetActive(on);
            if (ringLight != null) ringLight.enabled = on;
            Fx.Rate(sparks, 0f);
        }

        private void Update()
        {
            if (_start < 0f) return;
            float t = Time.time - _start;
            if (t < _ramp)
            {
                float k = t / _ramp;
                bool greedy = t > _ramp - 2f; // the 3x window: it shudders
                float pulse = greedy ? 0.85f + 0.15f * Mathf.Sin(Time.time * 30f) : 1f;
                if (ring != null) ring.localScale = new Vector3(1f, Mathf.Lerp(0.15f, 2.4f, k), 1f);
                Color c = _light;
                c.a = Mathf.Lerp(0.03f, 0.32f, k * k) * pulse; // soft: the bloom does the rest
                foreach (Renderer b in blades) Fx.Tint(b, c);
                if (ringLight != null)
                {
                    ringLight.color = _light;
                    ringLight.intensity = Mathf.Lerp(0f, 12f, k) * pulse;
                }
                Fx.Rate(sparks, Mathf.Lerp(4f, greedy ? 120f : 60f, k));
                return;
            }
            float after = t - _ramp;
            if (after < 0.5f)
            {
                // The scorch: a white flash, all at once.
                if (scorch != null && after < Time.deltaTime) scorch.Emit(160);
                foreach (Renderer b in blades) Fx.Tint(b, new Color(1f, 1f, 1f, 0.7f));
                if (ringLight != null)
                {
                    ringLight.color = Color.white;
                    ringLight.intensity = 45f * (1f - after / 0.5f);
                }
                Fx.Rate(sparks, 0f);
                return;
            }
            if (ring != null && ring.gameObject.activeSelf) Show(false);
            // Dry: no blood in the bowl, no drips, no glow, until it fills again.
            bool dry = after < 0.5f + _dry;
            Fx.Glow(blood, dry ? Color.black : bloodGlow);
            if (blood != null) Fx.Tint(blood, dry ? new Color(0.12f, 0.08f, 0.07f) : new Color(0.3f, 0.015f, 0.03f));
            Fx.Rate(drips, dry ? 0f : _dripRate);
            if (glow != null) glow.enabled = !dry;
            if (!dry) _start = -1f;
        }
    }

    /// <summary>A sigil's "safe" effect (Pay what you owe): its rune blazes and a pillar of light marks it for <c>seconds</c>.</summary>
    public sealed class SafeSigil : MonoBehaviour, IObjectEffect
    {
        public Renderer[] inlay;
        public Renderer pillar;
        public Light lightSource;
        public Color rest = new Color(0.85f, 0.72f, 0.48f) * 0.22f;
        public Color lit = new Color(1f, 0.88f, 0.55f);

        private float _start = -1f, _seconds = 4f;

        private void Awake()
        {
            if (pillar != null) pillar.enabled = false;
            if (lightSource != null) lightSource.enabled = false;
        }

        public void Play(string effect, IReadOnlyDictionary<string, string> args)
        {
            if (effect != "safe") return;
            _seconds = ObjectEffects.Float(args, "seconds", 4f);
            _start = Time.time;
            if (pillar != null) pillar.enabled = true;
            if (lightSource != null) lightSource.enabled = true;
        }

        private void Update()
        {
            if (_start < 0f) return;
            float t = Time.time - _start;
            float s = t < 0.25f ? t / 0.25f : t < _seconds ? 1f : 1f - (t - _seconds) / 0.4f;
            if (s <= 0f)
            {
                _start = -1f;
                Fx.Glow(inlay, rest);
                if (pillar != null) pillar.enabled = false;
                if (lightSource != null) lightSource.enabled = false;
                return;
            }
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);
            Fx.Glow(inlay, Color.Lerp(rest, lit * 5f, s));
            Color c = lit;
            c.a = 0.45f * s * pulse;
            Fx.Tint(pillar, c);
            if (lightSource != null)
            {
                lightSource.color = lit;
                lightSource.intensity = 7f * s * pulse;
            }
        }
    }

    /// <summary>
    /// The crypt's mood, on the Saint: "wake" (<c>level</c> 0 to 1) opens his eyes and makes every
    /// fire in the crypt gutter and dim; "hush" (Hold your tongue) drops the fires low for
    /// <c>seconds</c>, then lets them back up.
    /// </summary>
    public sealed class CryptMood : MonoBehaviour, IObjectEffect
    {
        public Renderer[] eyes;
        public Light eyeLight;
        public Color eyeColour = new Color(1f, 0.15f, 0.1f);

        private readonly List<LightFlicker> _fires = new List<LightFlicker>();
        private readonly List<(LightFlicker Fire, float Strength)> _strengths = new List<(LightFlicker, float)>();
        private readonly List<(ParticleSystem System, float Rate)> _flames = new List<(ParticleSystem, float)>();
        private float _wake, _wakeTarget, _hushUntil = -1f, _hush = 1f;

        private void Start()
        {
            foreach (LightFlicker f in FindObjectsByType<LightFlicker>(FindObjectsInactive.Exclude))
                if (f.gameObject.scene == gameObject.scene && (eyeLight == null || f.gameObject != eyeLight.gameObject))
                {
                    _fires.Add(f);
                    _strengths.Add((f, f.strength));
                }
            foreach (ParticleSystem ps in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude))
                if (ps.gameObject.scene == gameObject.scene && (ps.name == "Fire" || ps.name == "Embers"))
                    _flames.Add((ps, ps.emission.rateOverTimeMultiplier));
        }

        public void Play(string effect, IReadOnlyDictionary<string, string> args)
        {
            if (effect == "wake") _wakeTarget = Mathf.Clamp01(ObjectEffects.Float(args, "level", 0f));
            else if (effect == "hush") _hushUntil = Time.time + ObjectEffects.Float(args, "seconds", 3f);
        }

        private void Update()
        {
            _wake = Mathf.MoveTowards(_wake, _wakeTarget, Time.deltaTime * 0.8f);
            _hush = Mathf.MoveTowards(_hush, Time.time < _hushUntil ? 0.2f : 1f, Time.deltaTime * (Time.time < _hushUntil ? 4f : 1.2f));

            Fx.Glow(eyes, eyeColour * Mathf.Lerp(0.25f, 8f, _wake * _wake));
            if (eyeLight != null) eyeLight.intensity = Mathf.Lerp(0.3f, 10f, _wake);
            float dim = Mathf.Lerp(1f, 0.45f, _wake) * _hush;
            foreach ((LightFlicker fire, float strength) in _strengths)
            {
                if (fire == null) continue;
                fire.multiplier = dim;
                fire.strength = Mathf.Min(0.9f, strength * (1f + 1.5f * _wake)); // guttering
            }
            foreach ((ParticleSystem ps, float rate) in _flames) Fx.Rate(ps, rate * _hush);
        }
    }

    /// <summary>The altar's "cleanse": the shaft of light swells for <c>seconds</c>, the bowl blazes, then a burst rises.</summary>
    public sealed class AltarCleanse : MonoBehaviour, IObjectEffect
    {
        public Light shaft;
        public Renderer beam;
        public Renderer bowl;
        public ParticleSystem dust;
        public ParticleSystem burst;
        public Color bowlRest = new Color(0.35f, 0.02f, 0.04f) * 0.6f;

        private float _start = -1f, _seconds = 3f, _shaftBase, _dustBase;
        private Color _beamBase;

        private void Awake()
        {
            _shaftBase = shaft != null ? shaft.intensity : 0f;
            _dustBase = dust != null ? dust.emission.rateOverTimeMultiplier : 0f;
            _beamBase = beam != null && beam.sharedMaterial != null ? beam.sharedMaterial.GetColor("_BaseColor") : Color.white;
        }

        public void Play(string effect, IReadOnlyDictionary<string, string> args)
        {
            if (effect != "cleanse") return;
            _seconds = Mathf.Max(0.5f, ObjectEffects.Float(args, "seconds", 3f));
            _start = Time.time;
        }

        private void Update()
        {
            if (_start < 0f) return;
            float t = Time.time - _start;
            float k = t < _seconds ? t / _seconds : Mathf.Max(0f, 1f - (t - _seconds) / 1.2f);
            if (t >= _seconds && t - Time.deltaTime < _seconds && burst != null) burst.Emit(200);
            if (shaft != null) shaft.intensity = _shaftBase * (1f + 4f * k) + (t >= _seconds && t < _seconds + 0.3f ? 200f : 0f);
            Color c = _beamBase;
            c.a = Mathf.Lerp(_beamBase.a, 0.6f, k);
            Fx.Tint(beam, c);
            Fx.Glow(bowl, Color.Lerp(bowlRest, new Color(1f, 0.85f, 0.6f) * 6f, k));
            Fx.Rate(dust, _dustBase * (1f + 6f * k));
            if (t > _seconds + 1.2f) _start = -1f;
        }
    }

    /// <summary>The offertory's "owed": it glows and pulses, waiting, and the player owing it gets a marker with the count and time.</summary>
    public sealed class OffertoryOwed : MonoBehaviour, IObjectEffect
    {
        public Renderer surface;
        public Light glow;

        private float _until = -1f;

        public void Play(string effect, IReadOnlyDictionary<string, string> args)
        {
            if (effect != "owed") return;
            float seconds = ObjectEffects.Float(args, "seconds", 15f);
            _until = Time.time + seconds;
            if (glow != null) glow.enabled = true;
            WorldView world = FindAnyObjectByType<WorldView>();
            if (world != null && args.TryGetValue("player", out string id) && int.TryParse(id, out int playerId))
                world.ShowMarker(playerId, args.TryGetValue("count", out string count) ? count : "", seconds);
        }

        private void Update()
        {
            bool on = Time.time < _until;
            float pulse = on ? 0.6f + 0.4f * Mathf.Sin(Time.time * 5f) : 0f;
            Fx.Glow(surface, new Color(0.7f, 0.04f, 0.06f) * (0.3f + 4f * pulse));
            if (glow != null)
            {
                glow.enabled = on;
                glow.intensity = 6f * pulse;
            }
        }
    }
}
