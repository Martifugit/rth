using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rth.Client
{
    /// <summary>
    /// Marks what the player is busy with: the server's "focus" state (FocusSystem) names the object
    /// or creature the current interaction is aimed at (a door to open, a tree to chop, someone to
    /// talk to or fight), and this lays a glow over it (Resources/Shaders/Highlight) until the
    /// interaction ends. The glow is a copy of each of the thing's meshes (skinned ones following
    /// their bones) drawn with the highlight material; it never collides and casts no shadow.
    /// </summary>
    public sealed class FocusHighlight : MonoBehaviour
    {
        public GameSession session;
        public WorldView world;

        private static Material _material;
        private int _version = -1;
        private string _current = "";
        private Transform _target;
        private float _nextCheck;
        private readonly List<GameObject> _glows = new List<GameObject>();

        private void Update()
        {
            if (session == null || !session.IsInGame)
            {
                Clear();
                return;
            }
            bool recheck = Time.unscaledTime >= _nextCheck;
            if (!recheck && session.StateVersion == _version) return;
            _nextCheck = Time.unscaledTime + 0.5f;
            _version = session.StateVersion;

            InteractionFocus focus = session.ServerState<InteractionFocus>(InteractionFocus.StateKey);
            string key = focus.Kind + "|" + focus.Type + "|" + focus.Plane + "|" + focus.X + "|" + focus.Y + "|" + focus.Id;
            Transform target = Find(focus);
            // Bodies are rebuilt (a model loads, an NPC respawns): redo the glow when its target changes.
            if (key == _current && target == _target && _glows.TrueForAll(g => g != null)) return;
            Clear();
            _current = key;
            _target = target;
            if (target != null) Glow(target);
        }

        private Transform Find(InteractionFocus focus)
        {
            if (focus.Kind == InteractionFocus.Entity)
            {
                EntityView e = world != null ? world.Find(focus.Id) : null;
                return e != null && !e.IsDying ? e.transform : null;
            }
            if (focus.Kind != InteractionFocus.Object) return null;
            foreach (MapObject o in FindObjectsByType<MapObject>(FindObjectsInactive.Exclude))
            {
                Vector2Int t = o.Tile;
                if (o.objectType == focus.Type && t.x == focus.X && t.y == focus.Y && MapLevel.Of(o) == focus.Plane) return o.transform;
            }
            return null;
        }

        private void Glow(Transform target)
        {
            if (_material == null)
            {
                Shader shader = Resources.Load<Shader>("Shaders/Highlight");
                if (shader == null) return;
                _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
            foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r.name == "Glow") continue;
                var glow = new GameObject("Glow") { hideFlags = HideFlags.DontSave };
                glow.transform.SetParent(r.transform, false);
                if (r is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    var copy = glow.AddComponent<SkinnedMeshRenderer>();
                    copy.sharedMesh = skinned.sharedMesh;
                    copy.bones = skinned.bones;
                    copy.rootBone = skinned.rootBone;
                    copy.localBounds = skinned.localBounds;
                    copy.updateWhenOffscreen = true;
                    Finish(copy, skinned.sharedMesh.subMeshCount);
                }
                else if (r is MeshRenderer && r.GetComponent<MeshFilter>() is MeshFilter filter && filter.sharedMesh != null)
                {
                    glow.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    Finish(glow.AddComponent<MeshRenderer>(), filter.sharedMesh.subMeshCount);
                }
                else
                {
                    Destroy(glow);
                    continue;
                }
                _glows.Add(glow);
            }
        }

        private static void Finish(Renderer r, int subMeshes)
        {
            var materials = new Material[Mathf.Max(1, subMeshes)];
            for (int i = 0; i < materials.Length; i++) materials[i] = _material;
            r.sharedMaterials = materials;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private void Clear()
        {
            foreach (GameObject g in _glows)
                if (g != null) Destroy(g);
            _glows.Clear();
            _current = "";
            _target = null;
        }

        private void OnDisable() => Clear();
    }
}
