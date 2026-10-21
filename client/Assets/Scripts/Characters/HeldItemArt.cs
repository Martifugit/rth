using Rth.Shared.Items;
using UnityEngine;

namespace Rth.Client.Characters
{
    /// <summary>
    /// Stand-in art for what a character holds until items have models: a few boxes in the shape
    /// of the item's kind (its icon: sword, dagger, knife, mace), tinted by the item's colour.
    /// Built the way held art is modelled (tools/character-models): origin at the grip, the
    /// handle running along +Z, so <see cref="CharacterModel.Attach"/> can put it in a fist.
    /// </summary>
    public static class HeldItemArt
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>A new, unparented object for the item, or null if it has nothing to hold.</summary>
        public static GameObject Build(ItemDef item)
        {
            if (item?.Equipment == null || item.Equipment.Slot != EquipSlot.Weapon) return null;
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

        /// <summary>
        /// Colours held art once it's attached (attaching copies it and swaps in the shared matte
        /// material): handles in wood, fittings darker, the rest in the item's colour.
        /// </summary>
        public static void Tint(GameObject held, ItemDef item)
        {
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
