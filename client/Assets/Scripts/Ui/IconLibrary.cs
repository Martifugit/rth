using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using UnityEngine;

namespace Rth.Client.Ui
{
    /// <summary>
    /// Icons by id, from <c>Resources/Icons/&lt;id&gt;.png</c>: white silhouettes (game-icons.net,
    /// CC BY 3.0, see CREDITS.md; rebuilt by tools/icons/fetch_icons.py) that the interface tints,
    /// so one icon serves every colour of item. Replacing a PNG with real art of the same id just
    /// works. Unknown ids fall back to a pouch rather than nothing.
    /// </summary>
    public static class IconLibrary
    {
        private const string Fallback = "pouch";
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>The icon for an id; the fallback if there's no such icon.</summary>
        public static Texture2D Get(string id)
        {
            if (string.IsNullOrEmpty(id)) id = Fallback;
            // A cached texture can be destroyed (leaving Play mode with domain reload off): reload it.
            if (Cache.TryGetValue(id, out Texture2D cached) && cached != null) return cached;
            Texture2D texture = Resources.Load<Texture2D>("Icons/" + id);
            if (texture == null && id != Fallback) texture = Get(Fallback);
            Cache[id] = texture;
            return texture;
        }

        /// <summary>An item's icon: its own <c>icon</c>, else a guess from what it is.</summary>
        public static Texture2D ForItem(ItemDef def) =>
            def == null ? null : Get(!string.IsNullOrEmpty(def.Icon) ? def.Icon : GuessItemIcon(def));

        /// <summary>The colour an item's icon is drawn in: its own colour, lifted a little so dark items still read.</summary>
        public static Color ItemTint(ItemDef def)
        {
            Color c = WorldView.ParseColor(def?.Color);
            return Color.Lerp(c, Color.white, 0.12f);
        }

        public static Texture2D ForAbility(AbilityDef def) =>
            def == null ? null : Get(!string.IsNullOrEmpty(def.Icon) ? def.Icon : def.BileShift > 0 ? "flare" : def.BileShift < 0 ? "draught" : "balance");

        /// <summary>The colour an ability's icon is drawn in: blood for melee (pale for verses), else by its bile pole.</summary>
        public static Color AbilityTint(AbilityDef def) =>
            def == null ? Color.white
            : def.Type == AbilityType.Verse ? new Color(1f, 0.72f, 0.66f)
            : def.Style == CombatStyle.Melee ? new Color(1f, 0.42f, 0.4f)
            : def.BileShift > 0 ? new Color(1f, 0.82f, 0.36f)
            : def.BileShift < 0 ? new Color(0.78f, 0.6f, 1f)
            : new Color(0.75f, 0.9f, 0.7f);

        public static string SlotIcon(EquipSlot slot) => slot switch
        {
            EquipSlot.Head => "hood",
            EquipSlot.Cape => "cape",
            EquipSlot.Neck => "amulet",
            EquipSlot.Weapon => "dagger",
            EquipSlot.Body => "robe",
            EquipSlot.Offhand => "shield",
            EquipSlot.Legs => "legs",
            EquipSlot.Hands => "gloves",
            EquipSlot.Feet => "boots",
            EquipSlot.Ring => "ring",
            _ => Fallback,
        };

        private static string GuessItemIcon(ItemDef def)
        {
            if (def.Equipment != null) return SlotIcon(def.Equipment.Slot);
            if (def.Consumable != null) return def.Consumable.Verb == "Drink" ? "potion" : "bread";
            return Fallback;
        }
    }
}
