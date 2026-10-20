using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using UnityEngine;

namespace Rth.Client.Ui
{
    /// <summary>
    /// Icons by id. A texture at <c>Resources/Icons/&lt;id&gt;</c> wins (real art, one icon at a
    /// time); otherwise the icon is drawn from the pixel art in <c>Resources/UI/pixelicons.txt</c>,
    /// with its B/L/D pixels tinted (each item tints its icon with its own colour). Textures are
    /// cached per id and tint, and use point filtering so the pixels stay crisp.
    /// </summary>
    public static class IconLibrary
    {
        public const int Size = 16;

        private static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
        {
            ['k'] = new Color32(26, 20, 16, 255), ['x'] = new Color32(0, 0, 0, 255), ['w'] = new Color32(242, 236, 216, 255),
            ['g'] = new Color32(154, 154, 150, 255), ['G'] = new Color32(90, 90, 88, 255), ['y'] = new Color32(232, 192, 64, 255),
            ['Y'] = new Color32(160, 120, 32, 255), ['r'] = new Color32(200, 48, 28, 255), ['R'] = new Color32(122, 24, 16, 255),
            ['b'] = new Color32(138, 90, 48, 255), ['n'] = new Color32(78, 50, 24, 255), ['s'] = new Color32(216, 180, 132, 255),
            ['u'] = new Color32(90, 136, 216, 255), ['U'] = new Color32(44, 76, 144, 255), ['e'] = new Color32(90, 168, 72, 255),
            ['E'] = new Color32(44, 100, 36, 255), ['p'] = new Color32(138, 88, 184, 255), ['P'] = new Color32(74, 42, 102, 255),
            ['o'] = new Color32(232, 138, 32, 255), ['c'] = new Color32(154, 216, 224, 255),
        };

        private static Dictionary<string, string[]> _art;
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static readonly Color DefaultTint = new Color(0.62f, 0.58f, 0.5f);

        /// <summary>The icon for an id, tinted (only its B/L/D pixels change). Null if there's no such icon.</summary>
        public static Texture2D Get(string id, Color? tint = null)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Color t = tint ?? DefaultTint;
            string key = id + "|" + ColorUtility.ToHtmlStringRGB(t);
            if (Cache.TryGetValue(key, out Texture2D cached)) return cached;

            Texture2D texture = Resources.Load<Texture2D>("Icons/" + id);
            if (texture == null && Art().TryGetValue(id, out string[] rows)) texture = Draw(id, rows, t);
            Cache[key] = texture;
            return texture;
        }

        /// <summary>An item's icon: its own <c>icon</c>, else a guess from what it is, tinted with its colour.</summary>
        public static Texture2D ForItem(ItemDef def)
        {
            if (def == null) return null;
            Color tint = WorldView.ParseColor(def.Color);
            return Get(!string.IsNullOrEmpty(def.Icon) ? def.Icon : GuessItemIcon(def), tint) ?? Get("pouch", tint);
        }

        public static Texture2D ForAbility(AbilityDef def) =>
            def == null ? null : Get(!string.IsNullOrEmpty(def.Icon) ? def.Icon : def.BileShift > 0 ? "flare" : def.BileShift < 0 ? "draught" : "balance");

        /// <summary>The silhouette shown in an empty equipment slot.</summary>
        public static Texture2D ForSlot(EquipSlot slot) => Get(SlotIcon(slot), new Color(0.32f, 0.28f, 0.22f));

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
            _ => "pouch",
        };

        private static string GuessItemIcon(ItemDef def)
        {
            if (def.Equipment != null) return SlotIcon(def.Equipment.Slot);
            if (def.Consumable != null) return def.Consumable.Verb == "Drink" ? "potion" : "bread";
            return "pouch";
        }

        private static Dictionary<string, string[]> Art()
        {
            if (_art != null) return _art;
            _art = new Dictionary<string, string[]>();
            var text = Resources.Load<TextAsset>("UI/pixelicons");
            if (text == null) return _art;
            string id = null;
            var rows = new List<string>();
            foreach (string raw in text.text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.StartsWith("#") || line.Trim().Length == 0) continue;
                if (line.StartsWith("@ "))
                {
                    if (id != null) _art[id] = rows.ToArray();
                    id = line.Substring(2).Trim();
                    rows.Clear();
                    continue;
                }
                rows.Add(line);
            }
            if (id != null) _art[id] = rows.ToArray();
            return _art;
        }

        private static Texture2D Draw(string id, string[] rows, Color tint)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "icon " + id,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            Color32 baseColor = tint;
            Color32 light = Color.Lerp(tint, Color.white, 0.35f);
            Color32 dark = Color.Lerp(tint, Color.black, 0.45f);
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                string row = y < rows.Length ? rows[y] : "";
                for (int x = 0; x < Size; x++)
                {
                    char c = x < row.Length ? row[x] : '.';
                    Color32 color = c == 'B' ? baseColor : c == 'L' ? light : c == 'D' ? dark
                        : Palette.TryGetValue(c, out Color32 p) ? p : new Color32(0, 0, 0, 0);
                    pixels[(Size - 1 - y) * Size + x] = color; // textures start at the bottom row
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
