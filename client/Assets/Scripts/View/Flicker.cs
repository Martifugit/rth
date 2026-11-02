using UnityEngine;

namespace Rth.Client
{
    /// <summary>A light that wavers like a flame: a slow, uneven swell around its set intensity.</summary>
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
            float t = Time.time * 2.3f + _seed;
            float n = Mathf.PerlinNoise(t, _seed) * 2f - 1f;
            _light.intensity = _base * (1f + amount * n);
        }
    }
}
