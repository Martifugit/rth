using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rth.Client
{
    /// <summary>
    /// A region's own post-processing (its data's <c>postFx</c>: bloom so souls and candles glow, a
    /// vignette, a colour grade), set by Sync World on the region's Data root. When the scene loads it
    /// builds a local volume over the region's rectangle (from far below to high above, so the camera
    /// is inside it while the player is), blended in over a few tiles at the edges. Settings > Graphics
    /// can still turn post-processing off.
    /// </summary>
    public sealed class RegionPostFx : MonoBehaviour
    {
        [Tooltip("The region's rectangle in tiles: x, y, width, height.")]
        public RectInt area;
        public float bloom;
        public float bloomThreshold = 0.9f;
        public Color bloomTint = Color.white;
        [Range(0f, 1f)] public float vignette;
        public Color vignetteColor = Color.black;
        public Color tint = Color.white;
        public float saturation;
        public float contrast;
        public float exposure;

        private const float Blend = 6f;

        private VolumeProfile _profile;

        private void OnEnable()
        {
            if (area.width <= 0 || area.height <= 0) return;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = name + " post-fx";
            if (bloom > 0f)
            {
                Bloom b = _profile.Add<Bloom>(true);
                b.intensity.Override(bloom);
                b.threshold.Override(bloomThreshold);
                b.tint.Override(bloomTint);
                b.scatter.Override(0.75f);
            }
            if (vignette > 0f)
            {
                Vignette v = _profile.Add<Vignette>(true);
                v.intensity.Override(vignette);
                v.color.Override(vignetteColor);
                v.smoothness.Override(0.45f);
            }
            if (tint != Color.white || saturation != 0f || contrast != 0f || exposure != 0f)
            {
                ColorAdjustments c = _profile.Add<ColorAdjustments>(true);
                c.colorFilter.Override(tint);
                c.saturation.Override(saturation);
                c.contrast.Override(contrast);
                c.postExposure.Override(exposure);
            }

            var go = new GameObject("Post-fx volume") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(area.x + area.width * 0.5f, 20f, area.y + area.height * 0.5f);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(area.width, 120f, area.height);
            go.layer = 2; // Ignore Raycast: clicks pass through it
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.blendDistance = Blend;
            volume.priority = 5f;
            volume.sharedProfile = _profile;
        }

        private void OnDisable()
        {
            Transform volume = transform.Find("Post-fx volume");
            if (volume != null) Destroy(volume.gameObject);
            if (_profile != null) Destroy(_profile);
            _profile = null;
        }
    }
}
