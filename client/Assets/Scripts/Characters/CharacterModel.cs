using System.Collections.Generic;
using UnityEngine;

namespace Rth.Client.Characters
{
    /// <summary>
    /// A skinned character put together from parts on one skeleton, the way equipment works in
    /// Old School: the body is a set of kit parts (head, torso, arms, hands, legs, feet, hair_*),
    /// equipment is more skinned meshes bound to the same bones (or rigid ones held at a bone),
    /// and a piece of equipment can hide the kit parts it covers.
    ///
    /// Colours are by channel: every material slot in the art is named after one (skin, hair,
    /// primary, secondary, leather, ...), and <see cref="SetColor"/> colours that channel on every
    /// part, so one mesh serves many colourways.
    /// </summary>
    public sealed class CharacterModel : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private const string ResourceFolder = "Characters/";

        private readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
        // Each bone's rotation relative to the model in the rest (T) pose.
        private readonly Dictionary<string, Quaternion> _rest = new Dictionary<string, Quaternion>();
        private readonly Dictionary<string, Renderer> _kit = new Dictionary<string, Renderer>();
        private readonly Dictionary<Renderer, string[]> _slots = new Dictionary<Renderer, string[]>();
        private readonly Dictionary<string, Color> _colors = new Dictionary<string, Color>();
        private readonly Dictionary<string, int> _hidden = new Dictionary<string, int>();   // by equipment
        private readonly HashSet<string> _baseOff = new HashSet<string>();                 // by appearance
        private readonly Dictionary<GameObject, string[]> _attached = new Dictionary<GameObject, string[]>();

        public Animator Animator { get; private set; }
        /// <summary>Top of the model at scale 1, from the art.</summary>
        public float Height { get; private set; } = 1.8f;

        /// <summary>The kit part names this body has (e.g. "torso", "hair_short").</summary>
        public IEnumerable<string> KitParts => _kit.Keys;

        /// <summary>Makes a body of the given type (e.g. "HumanMale"); null if there is no such art.</summary>
        public static CharacterModel Load(string bodyType, Transform parent)
        {
            var prefab = Resources.Load<GameObject>(ResourceFolder + bodyType);
            if (prefab == null) return null;
            GameObject go = Instantiate(prefab, parent, false);
            go.name = bodyType;
            var model = go.AddComponent<CharacterModel>();
            model.Init();
            return model;
        }

        private void Init()
        {
            Animator = GetComponent<Animator>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (_bones.ContainsKey(t.name)) continue;
                _bones[t.name] = t;
                _rest[t.name] = Quaternion.Inverse(transform.rotation) * t.rotation;
            }

            float top = 0f;
            foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Body kit meshes are "body_<part>" (a mesh can't share a bone's name); hair is "hair_<style>".
                _kit[smr.name.StartsWith("body_") ? smr.name.Substring(5) : smr.name] = smr;
                Register(smr);
                if (smr.sharedMesh != null) top = Mathf.Max(top, MeshTop(smr));
            }
            if (top > 0f) Height = top;
        }

        // Highest point of a skinned part in model space (its rest pose).
        private float MeshTop(SkinnedMeshRenderer smr)
        {
            Bounds b = smr.sharedMesh.bounds;
            Matrix4x4 toModel = transform.worldToLocalMatrix * smr.transform.localToWorldMatrix;
            return toModel.MultiplyPoint3x4(new Vector3(b.center.x, b.max.y, b.center.z)).y;
        }

        /// <summary>Remembers which channel each material slot is, and swaps in the shared matte material.</summary>
        private void Register(Renderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            var channels = new string[materials.Length];
            var matte = new Material[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                channels[i] = materials[i] != null ? ChannelOf(materials[i].name) : "";
                matte[i] = EntityBody.Matte;
            }
            renderer.sharedMaterials = matte;
            _slots[renderer] = channels;
            foreach (KeyValuePair<string, Color> c in _colors) Apply(renderer, c.Key, c.Value);
        }

        // Blender exports "skin", Unity may add suffixes ("skin (Instance)", "skin.001").
        private static string ChannelOf(string materialName)
        {
            int cut = materialName.IndexOfAny(new[] { ' ', '.' });
            return (cut >= 0 ? materialName.Substring(0, cut) : materialName).ToLowerInvariant();
        }

        /// <summary>Colours a channel on every part, now and on parts attached later.</summary>
        public void SetColor(string channel, Color color)
        {
            _colors[channel] = color;
            foreach (Renderer r in _slots.Keys) Apply(r, channel, color);
        }

        private void Apply(Renderer renderer, string channel, Color color)
        {
            string[] channels = _slots[renderer];
            for (int i = 0; i < channels.Length; i++)
            {
                if (channels[i] != channel) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, i);
                block.SetColor(BaseColor, color);
                renderer.SetPropertyBlock(block, i);
            }
        }

        /// <summary>Shows only the hair style given (a kit part "hair_*"), or none for null.</summary>
        public void SetHair(string style)
        {
            foreach (KeyValuePair<string, Renderer> part in _kit)
                if (part.Key.StartsWith("hair_")) SetBase(part.Key, part.Key == "hair_" + style);
        }

        // Appearance choice, separate from what equipment hides.
        private void SetBase(string part, bool on)
        {
            if (on) _baseOff.Remove(part); else _baseOff.Add(part);
            Refresh(part);
        }

        private void Refresh(string part)
        {
            if (!_kit.TryGetValue(part, out Renderer r)) return;
            r.enabled = !_baseOff.Contains(part) && (!_hidden.TryGetValue(part, out int n) || n == 0);
        }

        /// <summary>
        /// Puts on a piece of equipment art: each skinned mesh in it is bound to this skeleton by
        /// bone name; anything else is held at <paramref name="socket"/> (a bone name, usually
        /// "grip.R" or "grip.L", the middle of each closed fist). Held art is modelled as if held
        /// by the body in its rest T-pose: origin at the grip, the handle running along +Z
        /// (forward), in the body's own axes. The kit parts in <paramref name="hides"/> disappear
        /// until it comes off. <paramref name="hold"/> turns held art within the fist (in the
        /// art's own axes), for art that should sit in the hand otherwise than modelled.
        /// </summary>
        public GameObject Attach(GameObject prefab, string[] hides = null, string socket = null, Quaternion? hold = null)
        {
            var holder = new GameObject(prefab.name);
            if (socket != null && _bones.TryGetValue(socket, out Transform bone))
            {
                holder.transform.SetParent(bone, false);
                holder.transform.localRotation = Quaternion.Inverse(_rest[socket]) * (hold ?? Quaternion.identity);
            }
            else holder.transform.SetParent(transform, false);
            GameObject art = Instantiate(prefab, holder.transform, false);

            foreach (SkinnedMeshRenderer source in art.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = new Transform[source.bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    bones[i] = source.bones[i] != null && _bones.TryGetValue(source.bones[i].name, out Transform b) ? b : null;
                source.bones = bones;
                if (source.rootBone != null && _bones.TryGetValue(source.rootBone.name, out Transform root)) source.rootBone = root;
                // Its own copy of the skeleton is no longer needed.
                source.transform.SetParent(holder.transform, false);
            }
            foreach (Animator a in art.GetComponentsInChildren<Animator>(true)) Destroy(a);
            foreach (Renderer r in holder.GetComponentsInChildren<Renderer>(true)) Register(r);

            hides ??= new string[0];
            foreach (string part in hides)
            {
                _hidden[part] = (_hidden.TryGetValue(part, out int n) ? n : 0) + 1;
                Refresh(part);
            }
            _attached[holder] = hides;
            return holder;
        }

        /// <summary>
        /// Colours one attachment's materials by name (its own palette colours and recolours),
        /// leaving the rest of the character alone. <paramref name="colourOf"/> gets each
        /// material's name (lower case) and returns null to leave it be.
        /// </summary>
        public void ColorAttached(GameObject attached, System.Func<string, Color?> colourOf)
        {
            if (attached == null) return;
            foreach (Renderer r in attached.GetComponentsInChildren<Renderer>(true))
            {
                if (!_slots.TryGetValue(r, out string[] channels)) continue;
                for (int i = 0; i < channels.Length; i++)
                {
                    Color? colour = colourOf(channels[i]);
                    if (colour == null) continue;
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block, i);
                    block.SetColor(BaseColor, colour.Value);
                    r.SetPropertyBlock(block, i);
                }
            }
        }

        /// <summary>Takes off what <see cref="Attach"/> put on.</summary>
        public void Detach(GameObject attached)
        {
            if (attached == null || !_attached.TryGetValue(attached, out string[] hides)) return;
            _attached.Remove(attached);
            foreach (Renderer r in attached.GetComponentsInChildren<Renderer>(true)) _slots.Remove(r);
            foreach (string part in hides)
            {
                if (_hidden.TryGetValue(part, out int n)) _hidden[part] = Mathf.Max(0, n - 1);
                Refresh(part);
            }
            Destroy(attached);
        }

        public Transform Bone(string name) => _bones.TryGetValue(name, out Transform t) ? t : null;
    }
}
