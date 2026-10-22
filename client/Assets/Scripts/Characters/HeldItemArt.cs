using Rth.Shared.Items;
using UnityEngine;

namespace Rth.Client.Characters
{
    /// <summary>
    /// What a character holds: the item's model (Resources/Items/&lt;model&gt;, made in
    /// tools/art/blender/items) coloured by its palette-named materials and the item's recolours,
    /// or for items without one a stand-in of a few boxes in the shape of the item's kind (its
    /// icon: sword, dagger, knife, mace), tinted by the item's colour. Both follow the held-art
    /// convention (tools/character-models): origin at the grip, the handle running along +Z, so
    /// <see cref="CharacterModel.Attach"/> can put it in a fist.
    /// </summary>
    public static class HeldItemArt
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// How held art sits in the fist: modelled for the T-posed hand (handle along +Z, business
        /// side up, +Y), it is rolled a quarter turn about its handle, so that with the arm at rest
        /// the weapon points forward with its blade upright and its business side (an edge, an
        /// axe's blade, a hammer's face) facing down, not lying flat with its edge to the side.
        /// Upright holds belong to particular weapons (a scythe, a staff), not to all of them.
        /// </summary>
        public static readonly Quaternion Hold = Quaternion.Euler(0f, 0f, -90f);

        /// <summary>A new, unparented object for the item, or null if it has nothing to hold.</summary>
        public static GameObject Build(ItemDef item)
        {
            if (item?.Equipment == null || item.Equipment.Slot != EquipSlot.Weapon) return null;
            GameObject model = HasModel(item) ? Resources.Load<GameObject>("Items/" + item.Model) : null;
            if (model != null)
            {
                GameObject copy = Object.Instantiate(model);
                copy.name = item.Id;
                return copy;
            }
            var root = new GameObject(item.Id);

            switch (item.Icon)
            {
                case "mace":
                    Box(root, "Haft", new Vector3(0f, 0f, 0.18f), new Vector3(0.035f, 0.035f, 0.62f));
                    Box(root, "Head", new Vector3(0f, 0f, 0.5f), new Vector3(0.11f, 0.11f, 0.14f));
                    Box(root, "Flange", new Vector3(0f, 0f, 0.5f), new Vector3(0.16f, 0.03f, 0.1f));
                    Box(root, "Flange", new Vector3(0f, 0f, 0.5f), new Vector3(0.03f, 0.16f, 0.1f));
                    break;
                case "dagger":
                case "knife":
                    Box(root, "Grip", new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.03f, 0.11f));
                    Box(root, "Guard", new Vector3(0f, 0f, 0.06f), new Vector3(0.09f, 0.02f, 0.02f));
                    Box(root, "Blade", new Vector3(0f, 0f, 0.19f), new Vector3(0.05f, 0.016f, 0.26f));
                    break;
                default: // a sword
                    Box(root, "Grip", new Vector3(0f, 0f, 0f), new Vector3(0.045f, 0.045f, 0.14f));
                    Box(root, "Pommel", new Vector3(0f, 0f, -0.075f), new Vector3(0.045f, 0.045f, 0.04f));
                    Box(root, "Guard", new Vector3(0f, 0f, 0.075f), new Vector3(0.22f, 0.045f, 0.045f));
                    Box(root, "Blade", new Vector3(0f, 0f, 0.43f), new Vector3(0.075f, 0.022f, 0.7f));
                    break;
            }
            return root;
        }

        private static bool HasModel(ItemDef item) => !string.IsNullOrEmpty(item.Model);

        /// <summary>
        /// The colour of one of a model's materials (named after a palette colour) on this item:
        /// its recolour if it has one, else the palette colour itself; null if neither is known.
        /// </summary>
        public static Color? ModelColour(ItemDef item, string material)
        {
            string name = material;
            foreach (System.Collections.Generic.KeyValuePair<string, string> swap in item.Recolor)
                if (string.Equals(swap.Key, material, System.StringComparison.OrdinalIgnoreCase)) name = swap.Value;
            return Palette.TryParse(name, out Color colour) ? colour : (Color?)null;
        }

        /// <summary>
        /// Colours held art once it's attached (attaching copies it and swaps in the shared matte
        /// material). A model gets its materials' colours (<see cref="ModelColour"/>); a stand-in
        /// gets handles in wood, fittings darker, the rest in the item's colour.
        /// </summary>
        public static void Tint(GameObject held, ItemDef item, CharacterModel model)
        {
            if (HasModel(item) && Resources.Load<GameObject>("Items/" + item.Model) != null)
            {
                model.ColorAttached(held, material => ModelColour(item, material));
                return;
            }
            Color metal = ColorUtility.TryParseHtmlString(item.Color, out Color c) ? c : new Color(0.7f, 0.7f, 0.75f);
            var wood = new Color(0.32f, 0.22f, 0.13f);
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in held.GetComponentsInChildren<Renderer>(true))
            {
                Color colour = r.name == "Grip" || r.name == "Haft" ? wood : r.name == "Guard" || r.name == "Pommel" ? metal * 0.75f : metal;
                colour.a = 1f;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, colour);
                r.SetPropertyBlock(block);
            }
        }

        private static void Box(GameObject parent, string name, Vector3 center, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(box.GetComponent<Collider>()); // before the art is copied
            box.name = name;
            box.transform.SetParent(parent.transform, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = EntityBody.Matte;
        }
    }
}
