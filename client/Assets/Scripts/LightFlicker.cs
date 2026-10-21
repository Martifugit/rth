using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// A fire's light: its intensity wavers around where it was set, a little faster and deeper
    /// for <see cref="strength"/> and <see cref="speed"/>. Each light starts at its own phase so a
    /// row of braziers doesn't pulse together.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightFlicker : MonoBehaviour
    {
        [Range(0f, 1f)] public float strength = 0.3f;
        public float speed = 6f;

        private Light _light;
        private float _base;
        private float _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
            _seed = Random.value * 100f;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * speed);
            _light.intensity = _base * (1f - strength + strength * 2f * n);
        }
    }
}
