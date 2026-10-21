using System.Collections.Generic;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// A holy symbol's light. At rest it glows faintly in a warm neutral, breathing slowly, enough
    /// to feel holy. The "flare" effect (Are you a believer?'s telegraph) waits <c>delay</c>
    /// seconds, then flares up in <c>colour</c> (a combat style's), holds for <c>hold</c> and
    /// sinks back: the inlay, the halo behind it and its light together, with a burst of sparks.
    /// </summary>
    public sealed class HolySymbol : MonoBehaviour, IObjectEffect
    {
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public Renderer[] inlay;
        public Renderer halo;
        public Light glow;
        public ParticleSystem burst;
        public Color neutral = new Color(1f, 0.88f, 0.62f);
        [Tooltip("Emission strength at rest.")]
        public float restGlow = 0.7f;
        public float flareGlow = 6f;

        /// <summary>How long a strike flies (the server's UnquietSaintFx.HolyStrikeFlight).</summary>
        private const float StrikeFlight = 0.55f;

        private MaterialPropertyBlock _block;
        private float _phase;
        private Vector3 _haloScale;

        private struct Flare
        {
            public float Start;
            public float Hold;
            public Color Colour;
        }

        private readonly List<Flare> _flares = new List<Flare>();

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _phase = Random.value * 10f;
            if (halo != null) _haloScale = halo.transform.localScale;
        }

        private readonly List<(float Time, Color Colour, int Target)> _strikes = new List<(float, Color, int)>();

        public void Play(string effect, IReadOnlyDictionary<string, string> args)
        {
            if (effect == "strike")
            {
                // A strike thrown from the symbol at a player, after its delay (Believer's onslaught).
                int target = args.TryGetValue("player", out string id) && int.TryParse(id, out int n) ? n : 0;
                _strikes.Add((Time.time + ObjectEffects.Float(args, "delay", 0f), ObjectEffects.Colour(args, "colour", neutral), target));
                return;
            }
            if (effect != "flare") return;
            _flares.Add(new Flare
            {
                Start = Time.time + ObjectEffects.Float(args, "delay", 0f),
                Hold = ObjectEffects.Float(args, "hold", 0.6f),
                Colour = ObjectEffects.Colour(args, "colour", neutral),
            });
        }

        private void Update()
        {
            for (int i = _strikes.Count - 1; i >= 0; i--)
            {
                if (Time.time < _strikes[i].Time) continue;
                (float _, Color thrown, int target) = _strikes[i];
                _strikes.RemoveAt(i);
                WorldView world = FindAnyObjectByType<WorldView>();
                EntityView victim = world != null ? world.Find(target) : null;
                if (victim == null) continue;
                Vector3 from = halo != null ? halo.transform.position + Vector3.back * 0.4f : transform.position + Vector3.up * 5f;
                Projectile.Launch(from, victim, thrown * 1.6f, StrikeFlight, 0.55f, 6f);
                _flares.Add(new Flare { Start = Time.time, Hold = 0.15f, Colour = thrown }); // the symbol kicks as it throws
            }

            // How strongly a flare is on now (0 at rest, 1 at its height) and in what colour.
            float strength = 0f;
            Color colour = neutral;
            for (int i = _flares.Count - 1; i >= 0; i--)
            {
                Flare f = _flares[i];
                float t = Time.time - f.Start;
                if (t < 0f) continue;
                const float rise = 0.12f, fall = 0.5f;
                if (t > rise + f.Hold + fall)
                {
                    _flares.RemoveAt(i);
                    continue;
                }
                if (t < Time.deltaTime && burst != null) Burst(f.Colour);
                float s = t < rise ? t / rise : t < rise + f.Hold ? 1f : 1f - (t - rise - f.Hold) / fall;
                if (s > strength)
                {
                    strength = s;
                    colour = f.Colour;
                }
            }

            float breathe = 0.85f + 0.15f * Mathf.Sin(Time.time * 1.3f + _phase);
            Color now = Color.Lerp(neutral, colour, strength);
            float level = Mathf.Lerp(restGlow * breathe, flareGlow, strength);
            foreach (Renderer r in inlay)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(Emission, now * level);
                r.SetPropertyBlock(_block);
            }
            if (halo != null)
            {
                halo.GetPropertyBlock(_block);
                Color h = now;
                h.a = Mathf.Lerp(0.18f * breathe, 0.9f, strength);
                _block.SetColor(BaseColor, h);
                halo.SetPropertyBlock(_block);
                halo.transform.localScale = _haloScale * Mathf.Lerp(1f, 1.5f, strength);
            }
            if (glow != null)
            {
                glow.color = now;
                glow.intensity = Mathf.Lerp(1.2f * breathe, 14f, strength);
                glow.range = Mathf.Lerp(5f, 14f, strength);
            }
        }

        private void Burst(Color colour)
        {
            ParticleSystem.EmitParams p = default;
            p.startColor = colour;
            burst.Emit(p, 40);
        }
    }
}
