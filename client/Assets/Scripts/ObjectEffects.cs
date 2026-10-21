using System.Collections.Generic;
using System.Globalization;
using Rth.MapTools;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>Art on a placed object that can play effects the server asks for (see <see cref="Rth.Shared.Net.ObjectEffectEvent"/>).</summary>
    public interface IObjectEffect
    {
        /// <summary>Plays an effect by name; <paramref name="args"/> are its "key=value" pairs.</summary>
        void Play(string effect, IReadOnlyDictionary<string, string> args);
    }

    /// <summary>
    /// Finds the placed objects in the streamed region scenes by type and tile, and passes effects
    /// to their <see cref="IObjectEffect"/> components.
    /// </summary>
    public static class ObjectEffects
    {
        private static readonly Dictionary<(string, int, int), MapObject> Index = new Dictionary<(string, int, int), MapObject>();
        private static float _scannedAt = -100f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Index.Clear();
            _scannedAt = -100f;
        }

        /// <summary>The scene object for a placed object, or null if its region isn't loaded.</summary>
        public static MapObject Find(string type, int x, int y)
        {
            if (Index.TryGetValue((type, x, y), out MapObject found) && found != null) return found;
            // Regions stream in and out: look again, but not every frame.
            if (Time.unscaledTime - _scannedAt < 0.5f) return null;
            _scannedAt = Time.unscaledTime;
            Index.Clear();
            foreach (MapObject o in Object.FindObjectsByType<MapObject>(FindObjectsInactive.Exclude))
            {
                Vector3 p = o.transform.position;
                Index[(o.objectType, Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z))] = o;
            }
            return Index.TryGetValue((type, x, y), out found) ? found : null;
        }

        public static void Play(string type, int x, int y, string effect, string arg)
        {
            MapObject target = Find(type, x, y);
            if (target == null) return;
            IReadOnlyDictionary<string, string> args = Parse(arg);
            foreach (IObjectEffect art in target.GetComponentsInChildren<IObjectEffect>()) art.Play(effect, args);
        }

        /// <summary>"colour=#ff0000;delay=0.7" to a dictionary.</summary>
        public static Dictionary<string, string> Parse(string arg)
        {
            var result = new Dictionary<string, string>();
            foreach (string pair in (arg ?? "").Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0) result[pair.Substring(0, eq).Trim()] = pair.Substring(eq + 1).Trim();
            }
            return result;
        }

        public static float Float(IReadOnlyDictionary<string, string> args, string key, float fallback) =>
            args.TryGetValue(key, out string v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : fallback;

        public static Color Colour(IReadOnlyDictionary<string, string> args, string key, Color fallback) =>
            args.TryGetValue(key, out string v) && ColorUtility.TryParseHtmlString(v, out Color c) ? c : fallback;
    }
}
