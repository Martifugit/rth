using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// A placed light (a candle heap, a lantern): it wavers like a flame if <see cref="amount"/> is
    /// above 0, and fades out with distance as the fog closes in, so far lights don't shine through
    /// the fog (or from regions the player can't see). Off entirely beyond the fog's end.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class Flicker : MonoBehaviour
    {
        [Range(0f, 1f)] public float amount = 0.15f;

        private Light _light;
        private float _base, _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
            Vector3 p = transform.position;
            _seed = Mathf.Abs(p.x * 12.9898f + p.z * 78.233f) % 100f;
        }

        private void Update()
        {
            float fade = DistanceFade(transform.position);
            _light.enabled = fade > 0.001f;
            if (!_light.enabled) return;
            float wave = amount > 0f ? 1f + amount * (Mathf.PerlinNoise(Time.time * 2.3f + _seed, _seed) * 2f - 1f) : 1f;
            _light.intensity = _base * wave * fade;
        }

        /// <summary>1 near the camera, falling to 0 at the fog's end (or at the far clip without fog).</summary>
        public static float DistanceFade(Vector3 at)
        {
            Camera cam = Camera.main;
            if (cam == null) return 1f;
            float d = Vector3.Distance(cam.transform.position, at);
            float end = RenderSettings.fog ? RenderSettings.fogEndDistance : cam.farClipPlane;
            float start = RenderSettings.fog ? Mathf.Min(RenderSettings.fogStartDistance, end * 0.8f) : end * 0.7f;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, d));
        }
    }
}
