using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rth.Client
{
    /// <summary>
    /// Outlines what the player points at and what they are busy with (Resources/Shaders/Highlight):
    /// faintly the object or creature under the pointer, and more brightly, breathing, the one the
    /// current interaction is aimed at (the server's "focus" state, FocusSystem: a door to open, a
    /// tree to chop, someone to talk to or fight) until it is done. An outline is a copy of each of
    /// the thing's meshes (skinned ones following their bones) drawn with the highlight material,
    /// keeping cut-out leaves' shapes; it never collides and casts no shadow.
    /// </summary>
    public sealed class FocusHighlight : MonoBehaviour
    {
        public GameSession session;
        public WorldView world;

        private sealed class Mark
        {
            public Material Material;
            public Transform Target;
            public readonly List<GameObject> Outlines = new List<GameObject>();
        }

        private Mark _focus, _hover;
        private int _version = -1;
        private float _nextCheck;
        private Transform _focusTarget;
        private MaterialPropertyBlock _block;

        private void Update()
        {
            if (session == null || !session.IsInGame || !EnsureMarks())
            {
                if (_focus != null) Set(_focus, null);
                if (_hover != null) Set(_hover, null);
                return;
            }
            if (Time.unscaledTime >= _nextCheck || session.StateVersion != _version)
            {
                _nextCheck = Time.unscaledTime + 0.5f;
                _version = session.StateVersion;
                _focusTarget = Find(session.ServerState<InteractionFocus>(InteractionFocus.StateKey));
            }
            Set(_focus, _focusTarget);

            Transform hovered = session.HoveredObject != null ? session.HoveredObject.transform
                : world != null && world.HoveredId != 0 && world.Find(world.HoveredId) is EntityView e && !e.IsDying ? e.transform : null;
            Set(_hover, hovered == _focusTarget ? null : hovered);
        }

        private bool EnsureMarks()
        {
            if (_focus != null) return true;
            Shader shader = Resources.Load<Shader>("Shaders/Highlight");
            if (shader == null) return false;
            var focus = new Material(shader) { hideFlags = HideFlags.DontSave };
            focus.SetFloat("_Halo", 0.75f);
            focus.SetFloat("_Width", 0.05f);
            focus.SetFloat("_Breathe", 0.35f);
            var hover = new Material(shader) { hideFlags = HideFlags.DontSave };
            hover.SetColor("_Color", new Color(1f, 0.95f, 0.8f, 1f));
            hover.SetFloat("_Halo", 0.32f);
            hover.SetFloat("_Width", 0.035f);
            hover.SetFloat("_Breathe", 0f);
            _focus = new Mark { Material = focus };
            _hover = new Mark { Material = hover };
            _block = new MaterialPropertyBlock();
            return true;
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

        /// <summary>Moves a mark onto a target (or off everything), redoing it if the target's body was rebuilt.</summary>
        private void Set(Mark mark, Transform target)
        {
            if (mark.Target == target && mark.Outlines.TrueForAll(g => g != null)) return;
            foreach (GameObject g in mark.Outlines)
                if (g != null) Destroy(g);
            mark.Outlines.Clear();
            mark.Target = target;
            if (target != null) Outline(mark, target);
        }

        private void Outline(Mark mark, Transform target)
        {
            foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r.name == "Outline") continue;
                var outline = new GameObject("Outline") { hideFlags = HideFlags.DontSave };
                outline.transform.SetParent(r.transform, false);
                Renderer copy;
                int subMeshes;
                if (r is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    var s = outline.AddComponent<SkinnedMeshRenderer>();
                    s.sharedMesh = skinned.sharedMesh;
                    s.bones = skinned.bones;
                    s.rootBone = skinned.rootBone;
                    s.localBounds = skinned.localBounds;
                    s.updateWhenOffscreen = true;
                    copy = s;
                    subMeshes = skinned.sharedMesh.subMeshCount;
                }
                else if (r is MeshRenderer && r.GetComponent<MeshFilter>() is MeshFilter filter && filter.sharedMesh != null)
                {
                    outline.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    copy = outline.AddComponent<MeshRenderer>();
                    subMeshes = filter.sharedMesh.subMeshCount;
                }
                else
                {
                    Destroy(outline);
                    continue;
                }
                var materials = new Material[Mathf.Max(1, subMeshes)];
                for (int i = 0; i < materials.Length; i++) materials[i] = mark.Material;
                copy.sharedMaterials = materials;
                copy.shadowCastingMode = ShadowCastingMode.Off;
                copy.receiveShadows = false;
                // Cut-out leaves keep their shapes: each sub-mesh takes its source's texture and threshold.
                Material[] source = r.sharedMaterials;
                for (int i = 0; i < materials.Length && i < source.Length; i++)
                {
                    Material m = source[i];
                    _block.Clear();
                    bool cutout = m != null && m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null;
                    if (cutout)
                    {
                        _block.SetTexture("_BaseMap", m.GetTexture("_BaseMap"));
                        _block.SetVector("_BaseMap_ST", m.GetVector("_BaseMap_ST"));
                        _block.SetFloat("_Cutoff", m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f);
                        copy.SetPropertyBlock(_block, i);
                    }
                }
                mark.Outlines.Add(outline);
            }
        }

        private void OnDisable()
        {
            if (_focus != null) Set(_focus, null);
            if (_hover != null) Set(_hover, null);
        }
    }
}
