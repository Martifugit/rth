using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Gives a model other colours without another model: every renderer below it that uses a
    /// material named in <see cref="swaps"/> gets a copy of that material in the new colour (one
    /// copy per material and colour, shared). One rock serves every ore, one sword every metal.
    /// Put it on a placement or a prefab, or name the swaps on the object's definition
    /// (objects.json "recolor"; <see cref="ObjectRecolors"/>). Purely visual.
    /// </summary>
    [DefaultExecutionOrder(-50)] // before the Occluder captures its materials
    public sealed class Recolor : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Dictionary<(Material, Color), Material> Copies = new Dictionary<(Material, Color), Material>();
        private static readonly Dictionary<Material, Material> Originals = new Dictionary<Material, Material>();

        [Serializable]
        public struct Swap
        {
            [Tooltip("The material's name, as it is on the model (\"Stone\").")]
            public string material;
            public Color color;
        }

        public List<Swap> swaps = new List<Swap>();

        private void Awake() => Apply();

        /// <summary>Applies <see cref="swaps"/> to every renderer below this object.</summary>
        public void Apply()
        {
            if (swaps.Count == 0) return;
            System.Func<Material, Material> map = Map;
            var done = new HashSet<Renderer>();
            // Occluders keep their own copies of the materials to fade between, so change those first.
            var occluders = new List<Occluder>(GetComponentsInParent<Occluder>(true));
            occluders.AddRange(GetComponentsInChildren<Occluder>(true));
            foreach (Occluder o in occluders)
                if (o.Remap(transform, map))
                    foreach (Renderer r in o.GetComponentsInChildren<Renderer>(true))
                        if (r.transform.IsChildOf(transform)) done.Add(r);
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (done.Contains(r)) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Map(mats[i]);
                r.sharedMaterials = mats;
            }
        }

        private Material Map(Material material)
        {
            if (material == null) return null;
            Material source = Originals.TryGetValue(material, out Material original) && original != null ? original : material;
            foreach (Swap s in swaps)
                if (string.Equals(s.material, source.name, StringComparison.OrdinalIgnoreCase)) return CopyOf(source, s.color);
            return material;
        }

        /// <summary>Adds (or replaces) swaps on an object and applies them.</summary>
        public static Recolor On(GameObject target, IEnumerable<KeyValuePair<string, Color>> swaps)
        {
            Recolor recolor = target.GetComponent<Recolor>();
            if (recolor == null) recolor = target.AddComponent<Recolor>();
            foreach (KeyValuePair<string, Color> s in swaps)
            {
                recolor.swaps.RemoveAll(x => string.Equals(x.material, s.Key, StringComparison.OrdinalIgnoreCase));
                recolor.swaps.Add(new Swap { material = s.Key, color = s.Value });
            }
            recolor.Apply();
            return recolor;
        }

        private static Material CopyOf(Material source, Color color)
        {
            if (Copies.TryGetValue((source, color), out Material copy) && copy != null) return copy;
            copy = new Material(source) { name = source.name + " (recolour)" };
            if (copy.HasProperty(BaseColor))
            {
                Color old = source.HasProperty(BaseColor) ? source.GetColor(BaseColor) : Color.white;
                color.a = old.a;
                copy.SetColor(BaseColor, color);
            }
            Copies[(source, color)] = copy;
            Originals[copy] = source;
            return copy;
        }

        /// <summary>Copies die with Play mode, but the cache outlives it (domain reload on Play is off).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Copies.Clear();
            Originals.Clear();
        }
    }
}
