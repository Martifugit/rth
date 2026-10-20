using System.Collections.Generic;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rth.Client
{
    /// <summary>
    /// Streams the world's region scenes in and out around the local player, like the classic
    /// client's build area: every region within <see cref="loadDistance"/> tiles is loaded
    /// (additively, in the background), and one is only unloaded once it is further than
    /// <see cref="unloadDistance"/>, so walking along a border doesn't thrash. The server says
    /// which regions exist and where (<see cref="SetRegions"/>); each is a scene in the build.
    /// Region scenes hold only the map; the camera, light and game objects live in the Game scene.
    /// </summary>
    public sealed class RegionStreamer : MonoBehaviour
    {
        private const float TitleSeconds = 3.5f;

        public WorldView world;

        [Tooltip("Load regions within this many tiles of the player. Beyond the fog, so nothing pops in view.")]
        public int loadDistance = 48;

        [Tooltip("Unload regions further than this. Larger than loadDistance, so borders don't thrash.")]
        public int unloadDistance = 72;

        private enum Status
        {
            Unloaded,
            Loading,
            Loaded,
            Unloading,
        }

        private sealed class Entry
        {
            public WorldRegion Region;
            public Status Status;
            public AsyncOperation Operation;
        }

        private readonly List<Entry> _regions = new List<Entry>();
        private readonly HashSet<string> _missingScenes = new HashSet<string>();
        private WorldRegion _current;
        private float _titleTime = -100f;
        private GUIStyle _titleStyle;
        private GUIStyle _loadingStyle;

        public IReadOnlyList<WorldRegion> Regions
        {
            get
            {
                var list = new List<WorldRegion>();
                foreach (Entry e in _regions) list.Add(e.Region);
                return list;
            }
        }

        /// <summary>The region the local player stands in, or null.</summary>
        public WorldRegion Current => _current;

        /// <summary>
        /// Takes the server's region list. Scenes already open (the one you pressed Play in, or
        /// ones kept from before a reconnect) are adopted rather than loaded twice.
        /// </summary>
        public void SetRegions(IEnumerable<WorldRegion> regions)
        {
            var old = new Dictionary<string, Entry>();
            foreach (Entry e in _regions) old[e.Region.Name] = e;
            _regions.Clear();
            foreach (WorldRegion region in regions)
            {
                var entry = new Entry { Region = region };
                if (old.TryGetValue(region.Name, out Entry previous))
                {
                    entry.Status = previous.Status;
                    entry.Operation = previous.Operation;
                    old.Remove(region.Name);
                }
                else if (SceneManager.GetSceneByName(region.Scene).isLoaded)
                {
                    entry.Status = Status.Loaded;
                }
                _regions.Add(entry);
            }
            // Regions the server no longer has: let them go.
            foreach (Entry gone in old.Values)
                if (gone.Status == Status.Loaded) SceneManager.UnloadSceneAsync(gone.Region.Scene);
        }

        /// <summary>
        /// Whether the ground under a tile is loaded and can be shown. Tiles in no region (the edge
        /// of the world) count as ready: there is nothing to wait for.
        /// </summary>
        public bool IsReady(TileCoord tile)
        {
            foreach (Entry e in _regions)
                if (e.Region.Contains(tile.X, tile.Y)) return e.Status == Status.Loaded;
            return true;
        }

        private bool TryGetFocus(out TileCoord tile)
        {
            EntityView local = world != null ? world.Local : null;
            if (local == null)
            {
                tile = default;
                return false;
            }
            tile = local.Tile;
            return true;
        }

        private void Update()
        {
            foreach (Entry e in _regions)
            {
                if (e.Operation == null || !e.Operation.isDone) continue;
                e.Operation = null;
                e.Status = e.Status == Status.Loading ? Status.Loaded : Status.Unloaded;
            }

            if (!TryGetFocus(out TileCoord focus)) return;

            foreach (Entry e in _regions)
            {
                if (e.Operation != null) continue; // finish what's in flight first
                int distance = e.Region.DistanceTo(focus.X, focus.Y);
                if (e.Status == Status.Unloaded && distance <= loadDistance) Load(e);
                else if (e.Status == Status.Loaded && distance > unloadDistance) Unload(e);
            }

            WorldRegion here = null;
            foreach (Entry e in _regions)
                if (e.Region.Contains(focus.X, focus.Y)) here = e.Region;
            if (here != null && here != _current && (_current == null || here.Name != _current.Name))
            {
                // Announce the region on entering, as classic area names do; not on first login.
                if (_current != null && !string.IsNullOrEmpty(here.Title)) _titleTime = Time.time;
                _current = here;
            }
        }

        private void Load(Entry e)
        {
            if (!Application.CanStreamedLevelBeLoaded(e.Region.Scene))
            {
                if (_missingScenes.Add(e.Region.Scene))
                    Debug.LogWarning("[R'th] Region '" + e.Region.Name + "' needs scene '" + e.Region.Scene + "', which isn't in the build. Export it from its scene (R'th > Export Map).");
                return;
            }
            e.Operation = SceneManager.LoadSceneAsync(e.Region.Scene, LoadSceneMode.Additive);
            e.Status = Status.Loading;
        }

        private void Unload(Entry e)
        {
            e.Operation = SceneManager.UnloadSceneAsync(e.Region.Scene);
            e.Status = e.Operation != null ? Status.Unloading : Status.Unloaded;
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (TryGetFocus(out TileCoord focus) && !IsReady(focus))
            {
                GUI.depth = -100;
                GUI.color = new Color(0f, 0f, 0f, 0.85f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(0f, Screen.height * 0.45f, Screen.width, 40f), "Loading - please wait.", _loadingStyle);
                return;
            }

            float age = Time.time - _titleTime;
            if (_current == null || age > TitleSeconds) return;
            float alpha = Mathf.Clamp01(Mathf.Min(age / 0.5f, (TitleSeconds - age) / 1f));
            var rect = new Rect(0f, Screen.height * 0.18f, Screen.width, 40f);
            _titleStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.8f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), _current.Title, _titleStyle);
            _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.45f, alpha);
            GUI.Label(rect, _current.Title, _titleStyle);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28, fontStyle = FontStyle.Bold };
            _loadingStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            _loadingStyle.normal.textColor = Color.white;
        }
    }
}
