using Rth.Shared.World;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rth.Client
{
    /// <summary>
    /// Eases the sun, ambient light, fog and sky into the current region's own atmosphere (a crypt
    /// is nearly black) and back to the open world's when you leave. The open world's is whatever
    /// the Game scene set up, captured at start.
    /// </summary>
    public sealed class RegionAtmosphereView : MonoBehaviour
    {
        public RegionStreamer regions;

        private const float EaseSeconds = 1.2f;

        private struct Look
        {
            public float Sun;
            public Color Sky, Equator, Ground, Fog;
            public float FogStart, FogEnd;

            public static Look Lerp(Look a, Look b, float t) => new Look
            {
                Sun = Mathf.Lerp(a.Sun, b.Sun, t),
                Sky = Color.Lerp(a.Sky, b.Sky, t),
                Equator = Color.Lerp(a.Equator, b.Equator, t),
                Ground = Color.Lerp(a.Ground, b.Ground, t),
                Fog = Color.Lerp(a.Fog, b.Fog, t),
                FogStart = Mathf.Lerp(a.FogStart, b.FogStart, t),
                FogEnd = Mathf.Lerp(a.FogEnd, b.FogEnd, t),
            };
        }

        private Look _world, _now;
        private Light _sun;
        private bool _captured;

        private void Start()
        {
            Capture();
            // Regions may carry post-processing volumes (a crypt's bloom and vignette); the camera must render them.
            Camera cam = Camera.main;
            if (cam != null) cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        private void Capture()
        {
            _sun = RenderSettings.sun;
            Camera cam = Camera.main;
            _world = new Look
            {
                Sun = _sun != null ? _sun.intensity : 1f,
                Sky = RenderSettings.ambientSkyColor,
                Equator = RenderSettings.ambientEquatorColor,
                Ground = RenderSettings.ambientGroundColor,
                Fog = RenderSettings.fogColor,
                FogStart = RenderSettings.fogStartDistance,
                FogEnd = RenderSettings.fogEndDistance,
            };
            _now = _world;
            _captured = true;
        }

        private void OnDisable()
        {
            if (_captured) Apply(_world); // leave the Game scene as it was found
        }

        private void Update()
        {
            if (!_captured) Capture();
            Look target = For(regions != null ? regions.Current?.Atmosphere : null);
            _now = Look.Lerp(_now, target, Mathf.Clamp01(Time.deltaTime / EaseSeconds * 3f));
            Apply(_now);
        }

        private Look For(RegionAtmosphere a)
        {
            if (a == null) return _world;
            return new Look
            {
                Sun = a.SunIntensity,
                Sky = Parse(a.AmbientSky, _world.Sky),
                Equator = Parse(a.AmbientEquator, _world.Equator),
                Ground = Parse(a.AmbientGround, _world.Ground),
                Fog = Parse(a.Fog, _world.Fog),
                FogStart = a.FogStart,
                FogEnd = a.FogEnd,
            };
        }

        private static Color Parse(string hex, Color fallback) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : fallback;

        private void Apply(Look look)
        {
            if (_sun != null) _sun.intensity = look.Sun;
            RenderSettings.ambientSkyColor = look.Sky;
            RenderSettings.ambientEquatorColor = look.Equator;
            RenderSettings.ambientGroundColor = look.Ground;
            RenderSettings.fogColor = look.Fog;
            RenderSettings.fogStartDistance = look.FogStart;
            RenderSettings.fogEndDistance = look.FogEnd;
            Camera cam = Camera.main;
            if (cam != null) cam.backgroundColor = look.Fog;
        }
    }
}
