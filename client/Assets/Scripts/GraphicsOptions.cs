using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rth.Client
{
    /// <summary>
    /// The graphics settings (Settings > Graphics), kept in PlayerPrefs on this machine. Draw distance
    /// is read where it matters (<see cref="RegionAtmosphereView"/> pushes the fog and far clip out,
    /// <see cref="RegionStreamer"/> loads regions further); the rest is applied here, to the sun and
    /// to a copy of the render pipeline asset, so the project's asset is never changed by playing.
    /// What the Editor's quality settings were is put back when Play stops.
    /// </summary>
    public static class GraphicsOptions
    {
        public static readonly string[] DrawDistanceNames = { "Short", "Normal", "Far", "Very far", "Furthest" };
        private static readonly float[] DrawDistanceFactors = { 0.75f, 1f, 1.5f, 2.25f, 3.5f };

        public static readonly string[] ShadowNames = { "Off", "Low", "Medium", "High", "Ultra" };

        public static readonly string[] AntiAliasingNames = { "Off", "2x", "4x", "8x" };
        private static readonly int[] MsaaSamples = { 1, 2, 4, 8 };

        public static readonly string[] RenderScaleNames = { "50%", "75%", "100%", "150%", "200%" };
        private static readonly float[] RenderScales = { 0.5f, 0.75f, 1f, 1.5f, 2f };

        public static readonly string[] FrameLimitNames = { "Sync to display", "30", "60", "120", "144", "Unlimited" };
        private static readonly int[] FrameLimits = { 0, 30, 60, 120, 144, -1 };

        /// <summary>The fly camera sees at least this far, so a place can be judged from the air.</summary>
        private const int FlyDrawDistance = 3;

        public static int DrawDistance { get => Get("rth.gfx.drawDistance", 1, DrawDistanceNames); set => Set("rth.gfx.drawDistance", value, DrawDistanceNames); }
        public static int Shadows { get => Get("rth.gfx.shadows", 2, ShadowNames); set => Set("rth.gfx.shadows", value, ShadowNames); }
        public static int AntiAliasing { get => Get("rth.gfx.msaa", 0, AntiAliasingNames); set => Set("rth.gfx.msaa", value, AntiAliasingNames); }
        public static int RenderScale { get => Get("rth.gfx.renderScale", 2, RenderScaleNames); set => Set("rth.gfx.renderScale", value, RenderScaleNames); }
        public static int FrameLimit { get => Get("rth.gfx.frameLimit", 0, FrameLimitNames); set => Set("rth.gfx.frameLimit", value, FrameLimitNames); }

        public static bool PostProcessing
        {
            get => PlayerPrefs.GetInt("rth.gfx.post", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("rth.gfx.post", value ? 1 : 0);
                Apply();
            }
        }

        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set => Screen.fullScreen = value;
        }

        /// <summary>How much further than the scene's own fog the world is drawn (1 is the fog the Game scene was built with).</summary>
        public static float DrawDistanceFactor =>
            DrawDistanceFactors[FlyCamera.Active ? Mathf.Max(DrawDistance, FlyDrawDistance) : DrawDistance];

        private static int Get(string key, int fallback, string[] names) => Mathf.Clamp(PlayerPrefs.GetInt(key, fallback), 0, names.Length - 1);

        private static void Set(string key, int value, string[] names)
        {
            PlayerPrefs.SetInt(key, Mathf.Clamp(value, 0, names.Length - 1));
            Apply();
        }

        // ------------------------------------------------------------------ applying

        private static UniversalRenderPipelineAsset _copy;
        private static RenderPipelineAsset _originalPipeline;
        private static int _originalVSync;
        private static int _originalFrameRate;
        private static bool _captured;

        /// <summary>Applies every setting but draw distance (which is read each frame). Safe to call often.</summary>
        public static void Apply()
        {
            Capture();
            if (_copy != null)
            {
                int shadows = Shadows;
                _copy.shadowDistance = shadows switch { 1 => 35f, 2 => 50f, 3 => 80f, 4 => 150f, _ => 0f };
                _copy.shadowCascadeCount = shadows <= 1 ? 2 : 4;
                _copy.mainLightShadowmapResolution = shadows switch { 1 => 1024, 2 => 2048, _ => 4096 };
                _copy.msaaSampleCount = MsaaSamples[AntiAliasing];
                _copy.renderScale = RenderScales[RenderScale];
            }
            Light sun = RenderSettings.sun;
            if (sun != null) sun.shadows = Shadows switch { 0 => LightShadows.None, 1 => LightShadows.Hard, _ => LightShadows.Soft };

            int limit = FrameLimits[FrameLimit];
            QualitySettings.vSyncCount = limit == 0 ? 1 : 0;
            Application.targetFrameRate = limit > 0 ? limit : -1;

            Camera cam = Camera.main;
            if (cam != null) cam.GetUniversalAdditionalCameraData().renderPostProcessing = PostProcessing;
        }

        /// <summary>Swaps in a copy of the pipeline asset to change, once, and remembers what to put back.</summary>
        private static void Capture()
        {
            if (_captured) return;
            _captured = true;
            _originalVSync = QualitySettings.vSyncCount;
            _originalFrameRate = Application.targetFrameRate;
            _originalPipeline = QualitySettings.renderPipeline;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                _copy = Object.Instantiate(urp);
                _copy.name = urp.name + " (settings)";
                _copy.hideFlags = HideFlags.DontSave;
                QualitySettings.renderPipeline = _copy;
            }
            Application.quitting += Restore;
        }

        /// <summary>Puts the quality settings back as they were (the Editor keeps them past Play otherwise).</summary>
        private static void Restore()
        {
            Application.quitting -= Restore;
            if (!_captured) return;
            if (_copy != null)
            {
                QualitySettings.renderPipeline = _originalPipeline;
                Object.Destroy(_copy);
                _copy = null;
            }
            QualitySettings.vSyncCount = _originalVSync;
            Application.targetFrameRate = _originalFrameRate;
            _captured = false;
        }
    }
}
