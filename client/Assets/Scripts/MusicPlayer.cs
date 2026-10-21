using System.Collections.Generic;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Plays the current region's music (its region.json "music": a track in Resources/Music),
    /// crossfading when it changes. A track can come in layers, <c>name</c>, <c>name_2</c>,
    /// <c>name_3</c>..., played in sync: the first always, each further one fading in as
    /// <see cref="Intensity"/> (0 to 1) rises past its share, so an encounter can make the music
    /// swell (a choir joining as the Saint stirs). Volume is a player setting.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        private const string VolumeKey = "audio.music";
        private const float FadeSeconds = 2.5f;

        public RegionStreamer regions;

        /// <summary>How intense the moment is (0 calm, 1 everything): how many layers play.</summary>
        public float Intensity { get; set; }

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 0.6f);
            set => PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
        }

        private sealed class Track
        {
            public string Name;
            public readonly List<AudioSource> Layers = new List<AudioSource>();
            public float Fade; // 0..1, how present the whole track is
        }

        private Track _current;
        private readonly List<Track> _fading = new List<Track>();
        private string _wanted;

        private void Update()
        {
            string wanted = regions != null ? regions.Current?.Music : null;
            if (string.IsNullOrEmpty(wanted)) wanted = null;
            if (wanted != _wanted)
            {
                _wanted = wanted;
                if (_current != null) _fading.Add(_current);
                _current = wanted != null ? Load(wanted) : null;
            }

            float step = Time.unscaledDeltaTime / FadeSeconds;
            if (_current != null)
            {
                _current.Fade = Mathf.MoveTowards(_current.Fade, 1f, step);
                Mix(_current);
            }
            for (int i = _fading.Count - 1; i >= 0; i--)
            {
                Track t = _fading[i];
                t.Fade = Mathf.MoveTowards(t.Fade, 0f, step);
                Mix(t);
                if (t.Fade > 0f) continue;
                foreach (AudioSource s in t.Layers) Destroy(s);
                _fading.RemoveAt(i);
            }
        }

        /// <summary>Each layer's share: the first always plays, the rest fade in one after another as intensity rises.</summary>
        private void Mix(Track t)
        {
            int extra = t.Layers.Count - 1;
            for (int i = 0; i < t.Layers.Count; i++)
            {
                float layer = i == 0 ? 1f : Mathf.Clamp01(Intensity * extra - (i - 1));
                AudioSource s = t.Layers[i];
                s.volume = Mathf.MoveTowards(s.volume, Volume * t.Fade * layer, Time.unscaledDeltaTime);
            }
        }

        private Track Load(string name)
        {
            var track = new Track { Name = name };
            for (int i = 1; i < 10; i++)
            {
                var clip = Resources.Load<AudioClip>("Music/" + (i == 1 ? name : name + "_" + i));
                if (clip == null) break;
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.clip = clip;
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.volume = 0f;
                s.Play();
                if (track.Layers.Count > 0) s.timeSamples = track.Layers[0].timeSamples; // layers stay in step
                track.Layers.Add(s);
            }
            if (track.Layers.Count == 0) Debug.LogWarning("[R'th] No music '" + name + "' in Resources/Music.");
            return track;
        }
    }
}
