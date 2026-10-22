using UnityEngine;

namespace Rth.Client.Characters
{
    /// <summary>
    /// A creature drawn from its model (Resources/Creatures/&lt;model&gt;, made in
    /// tools/art/blender), moved as one rigid shape until it has animations. Each material is
    /// named after a palette colour and drawn in that colour with the shared matte material.
    /// </summary>
    public sealed class CreatureBody : ShapeBody
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>False when there is no such model (the caller falls back to a primitive).</summary>
        public bool Build(string model, float scale)
        {
            var prefab = Resources.Load<GameObject>("Creatures/" + model);
            if (prefab == null) return false;

            var shape = new GameObject("Shape").transform;
            shape.SetParent(transform, false);
            shape.localScale = Vector3.one * scale;
            GameObject art = Instantiate(prefab, shape, false);
            art.name = model;

            var bounds = new Bounds(transform.position, Vector3.zero);
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in art.GetComponentsInChildren<Renderer>())
            {
                Material[] imported = r.sharedMaterials;
                var materials = new Material[imported.Length];
                for (int i = 0; i < imported.Length; i++) materials[i] = Matte;
                r.sharedMaterials = materials;
                for (int i = 0; i < imported.Length; i++)
                {
                    block.Clear();
                    block.SetColor(BaseColor, ColourOf(imported[i]));
                    r.SetPropertyBlock(block, i);
                }
                bounds.Encapsulate(r.bounds);
            }

            Vector3 size = bounds.size;
            SetShape(shape, size.y, Mathf.Max(size.x, size.z) * 0.5f);
            return true;
        }

        /// <summary>The palette colour a material is named after, else its own colour.</summary>
        private static Color ColourOf(Material material)
        {
            if (material == null) return Color.magenta;
            string name = material.name.Replace(" (Instance)", "").Trim();
            if (Palette.TryParse(name, out Color colour)) return colour;
            return material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : material.color;
        }
    }
}
