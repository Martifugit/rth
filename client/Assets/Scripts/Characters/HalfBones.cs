using System.Collections.Generic;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Turns each half bone ("half.&lt;bone&gt;": a copy of a joint's bone on the same parent, with
    /// the same rest direction) half as far as that bone, after the animation, so rigid armour over
    /// a joint (a pauldron, a couter, a poleyn) sits over the bend from either side
    /// (tools/character-models/posing.py, drive_halves; keep the two the same). Add it to the
    /// character's root; it does nothing on a skeleton without half bones.
    /// </summary>
    public sealed class HalfBones : MonoBehaviour
    {
        readonly List<(Transform half, Transform bone, Quaternion boneRest)> pairs = new();

        void Awake()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("half.")) continue;
                var bone = Find(transform, t.name.Substring("half.".Length));
                if (bone != null) pairs.Add((t, bone, bone.localRotation));   // at rest when built
            }
            enabled = pairs.Count > 0;
        }

        void LateUpdate()
        {
            foreach (var (half, bone, rest) in pairs)
                half.localRotation = Quaternion.Slerp(rest, bone.localRotation, 0.5f);
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var t = Find(c, name);
                if (t != null) return t;
            }
            return null;
        }
    }
}
