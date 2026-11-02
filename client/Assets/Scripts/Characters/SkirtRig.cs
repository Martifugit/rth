using UnityEngine;
using NVec = System.Numerics.Vector3;

namespace Rth.Client
{
    /// <summary>
    /// Drives a character's skirt chains (bones "skirt.&lt;k&gt;.1" and "skirt.&lt;k&gt;.2" under the
    /// hips) after the animation each frame, by <see cref="SkirtRules"/>, so a skirt or robe stays
    /// outside where the hidden legs are in any animation. Add it to the character's root once its
    /// skeleton is built; <see cref="capsuleRadii"/> (thigh, shin) come from the character build
    /// (the rig's "skirt_capsules"). Does nothing on a skeleton without skirt chains.
    /// </summary>
    public sealed class SkirtRig : MonoBehaviour
    {
        [Tooltip("Capsule radius round the thighs and shins, metres (the rig's skirt_capsules).")]
        public Vector2 capsuleRadii = new Vector2(0.07f, 0.07f);

        Transform hips;
        Transform[] upper, lower;
        Transform[] thighs = new Transform[2], shins = new Transform[2], feet = new Transform[2];
        Vector3[] restDir1, restDir2;                   // each segment's direction at rest, in the hips' space
        Quaternion[] restRot1, restRot2;                // each segment's rotation at rest, in the hips' space
        Vector3[] restOut;                              // each chain's outward direction at rest, in the hips' space
        float[] len1, len2;

        void Awake()
        {
            hips = Find(transform, "hips");
            if (hips == null) { enabled = false; return; }
            int n = 0;
            while (Find(hips, $"skirt.{n}.1") != null) n++;
            if (n == 0) { enabled = false; return; }
            upper = new Transform[n]; lower = new Transform[n];
            restDir1 = new Vector3[n]; restDir2 = new Vector3[n];
            restRot1 = new Quaternion[n]; restRot2 = new Quaternion[n];
            restOut = new Vector3[n]; len1 = new float[n]; len2 = new float[n];
            var inv = Quaternion.Inverse(hips.rotation);
            for (int k = 0; k < n; k++)
            {
                upper[k] = Find(hips, $"skirt.{k}.1");
                lower[k] = Find(upper[k], $"skirt.{k}.2");
                var tip = lower[k].childCount > 0 ? lower[k].GetChild(0).position
                        : lower[k].position + (lower[k].position - upper[k].position);
                restDir1[k] = inv * (lower[k].position - upper[k].position).normalized;
                restDir2[k] = inv * (tip - lower[k].position).normalized;
                len1[k] = (lower[k].position - upper[k].position).magnitude;
                len2[k] = (tip - lower[k].position).magnitude;
                restRot1[k] = inv * upper[k].rotation;
                restRot2[k] = inv * lower[k].rotation;
                var flat = inv * (upper[k].position - hips.position);
                flat -= Vector3.Project(flat, inv * Vector3.up);
                restOut[k] = flat.normalized;
            }
            string[] side = { "L", "R" };
            for (int i = 0; i < 2; i++)
            {
                thighs[i] = Find(transform, "thigh." + side[i]);
                shins[i] = Find(transform, "shin." + side[i]);
                feet[i] = Find(transform, "foot." + side[i]);
            }
        }

        void LateUpdate()
        {
            var caps = new SkirtRules.Capsule[4];
            for (int i = 0; i < 2; i++)
            {
                caps[i] = SkirtRules.Capsule.OfBone(N(thighs[i].position), N(shins[i].position), capsuleRadii.x, true);
                caps[2 + i] = SkirtRules.Capsule.OfBone(N(shins[i].position), N(feet[i].position), capsuleRadii.y, false);
            }
            int n = upper.Length;
            var down = N(hips.rotation * restDown);     // the hips' down: the chains hang along it at rest
            var outs = new NVec[n];
            var heads = new NVec[n];
            for (int k = 0; k < n; k++)
            {
                outs[k] = N(hips.rotation * restOut[k]);
                heads[k] = N(upper[k].position);
            }
            var up = N(Vector3.up);
            var a1 = SkirtRules.Ring(heads, outs, len1, down, up, caps);
            var knees = new NVec[n];
            for (int k = 0; k < n; k++)
            {
                var d = SkirtRules.Along(down, outs[k], a1[k]);
                Point(upper[k], restDir1[k], restRot1[k], U(d));
                knees[k] = heads[k] + d * len1[k];
            }
            var a2 = SkirtRules.Ring(knees, outs, len2, down, up, caps);
            for (int k = 0; k < n; k++)
                Point(lower[k], restDir2[k], restRot2[k], U(SkirtRules.Along(down, outs[k], a2[k])));
        }

        /// <summary>The hips' down at rest: the chains hang along it.</summary>
        Vector3 restDown => restDir1 != null && restDir1.Length > 0 ? AverageDown() : Vector3.down;

        Vector3 AverageDown()
        {
            var sum = Vector3.zero;
            foreach (var d in restDir1) sum += d;
            return sum.normalized;
        }

        void Point(Transform bone, Vector3 restDir, Quaternion restRot, Vector3 dir)
        {
            var nowRest = hips.rotation * restDir;
            bone.rotation = Quaternion.FromToRotation(nowRest, dir) * (hips.rotation * restRot);
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

        static NVec N(Vector3 v) => new NVec(v.x, v.y, v.z);
        static Vector3 U(NVec v) => new Vector3(v.X, v.Y, v.Z);
    }
}
