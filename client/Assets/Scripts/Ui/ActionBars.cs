using System.Collections.Generic;
using Newtonsoft.Json;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine.InputSystem;

namespace Rth.Client.Ui
{
    /// <summary>
    /// The player's action bars: a main bar and secondary bars of 14 slots each. A slot holds an
    /// ability ("a:gall_spark"), a hymn ("h:bulwark") or an item ("i:riverbread", used from
    /// the pack). Arranged by dragging from the ability book, the religion tab or the pack; kept on
    /// the server as client state, so a layout follows the player. Each weapon style has its own
    /// main bar, which swaps in with the weapon (RuneScape 3's bar per style); one never arranged
    /// starts as the content's default for that style. Locked bars can't be rearranged and show
    /// no tooltips.
    /// </summary>
    public sealed class ActionBars
    {
        public const int SlotCount = 14;
        public const int BarCount = 3;
        private const string StateKey = "bars";
        private const string LockKey = "bars.locked";

        /// <summary>Slots from before hymns: breath's focuses, and the hymns that replaced them.</summary>
        private static readonly Dictionary<string, string> Renamed = new Dictionary<string, string>
        {
            ["f:deflect_melee"] = "h:bulwark",
            ["f:deflect_ranged"] = "h:glare",
            ["f:deflect_occult"] = "h:salt_ward",
        };

        /// <summary>Main bar keys: 1 to 0, - and =; the last two slots have no key.</summary>
        public static readonly Key[] MainKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7,
            Key.Digit8, Key.Digit9, Key.Digit0, Key.Minus, Key.Equals, Key.None, Key.None,
        };

        public static readonly string[] MainKeyLabels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "", "" };

        private readonly GameSession _session;
        private string[][] _bars;

        /// <summary>Counts changes, so bar views know when to redraw.</summary>
        public int Version { get; private set; }

        /// <summary>The weapon style whose main bar is bar 0.</summary>
        public CombatStyle Style { get; }

        private ActionBars(GameSession session, string[][] bars, CombatStyle style)
        {
            _session = session;
            _bars = bars;
            Style = style;
        }

        public static ActionBars Load(GameSession session)
        {
            CombatStyle style = session.WeaponStyle;
            string[][] bars = Read<string[][]>(session, StateKey) ?? new string[BarCount][];
            string[] main = Read<string[]>(session, CombatOptions.MainBarKey(style));
            bool unarmed = style == (session.Ability(session.Content.AutoAttack)?.Style ?? CombatStyle.Occult);
            if (main == null && !(unarmed && bars.Length > 0 && bars[0] != null))
            {
                // Never arranged for this style: the content's default.
                List<string> fallback = session.Content.ActionBars.TryGetValue(style, out List<string> defaults) ? defaults : session.Content.ActionBar;
                main = new string[SlotCount];
                for (int i = 0; i < SlotCount && i < fallback.Count; i++) main[i] = "a:" + fallback[i];
            }
            var normalised = new string[BarCount][];
            for (int b = 0; b < BarCount; b++)
            {
                string[] source = b == 0 && main != null ? main : b < bars.Length ? bars[b] : null;
                normalised[b] = new string[SlotCount];
                if (source != null)
                    for (int s = 0; s < SlotCount && s < source.Length; s++)
                        normalised[b][s] = source[s] != null && Renamed.TryGetValue(source[s], out string now) ? now : source[s];
            }
            return new ActionBars(session, normalised, style);
        }

        private static T Read<T>(GameSession session, string key) where T : class
        {
            if (!session.ClientState.TryGetValue(key, out string json)) return null;
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Stores the bars a change touched: the style's own main bar, or the secondary bars.</summary>
        private void Save(int barA, int barB = -1)
        {
            Version++;
            if (barA == 0 || barB == 0) _session.SetClientState(CombatOptions.MainBarKey(Style), JsonConvert.SerializeObject(_bars[0]));
            if (barA > 0 || barB > 0)
            {
                // Bar 0 of the shared layout is kept as it was (an older occult main bar), so it isn't lost.
                string[][] shared = Read<string[][]>(_session, StateKey) ?? new string[BarCount][];
                var copy = new string[BarCount][];
                copy[0] = shared.Length > 0 ? shared[0] : null;
                for (int b = 1; b < BarCount; b++) copy[b] = _bars[b];
                _session.SetClientState(StateKey, JsonConvert.SerializeObject(copy));
            }
        }

        public string Get(int bar, int slot) => _bars[bar][slot];

        /// <summary>Locked bars ignore dragging (to, from and between slots) and show no tooltips.</summary>
        public bool Locked
        {
            get => _session.ClientState.TryGetValue(LockKey, out string v) && v == "true";
            set
            {
                _session.SetClientState(LockKey, value ? "true" : "false");
                Version++;
            }
        }

        public void Set(int bar, int slot, string value)
        {
            _bars[bar][slot] = string.IsNullOrEmpty(value) ? null : value;
            Save(bar);
        }

        /// <summary>Swaps two slots (dragging one slot onto another), across bars too.</summary>
        public void Swap(int barA, int slotA, int barB, int slotB)
        {
            string a = _bars[barA][slotA];
            _bars[barA][slotA] = _bars[barB][slotB];
            _bars[barB][slotB] = a;
            Save(barA, barB);
        }

        public static string Ability(string id) => "a:" + id;
        public static string Item(string id) => "i:" + id;
        public static string Hymn(string id) => "h:" + id;

        public static bool IsAbility(string slot, out string id) => Is(slot, "a:", out id);
        public static bool IsItem(string slot, out string id) => Is(slot, "i:", out id);
        public static bool IsHymn(string slot, out string id) => Is(slot, "h:", out id);

        private static bool Is(string slot, string prefix, out string id)
        {
            id = slot != null && slot.StartsWith(prefix) ? slot.Substring(prefix.Length) : null;
            return id != null;
        }

        /// <summary>Uses a slot: an ability, a hymn (sing or stop), or the first of an item in the pack (eat, drink, wield).</summary>
        public void Use(GameSession session, int bar, int slot)
        {
            string value = _bars[bar][slot];
            if (IsAbility(value, out string ability))
            {
                session.UseAbility(ability);
                return;
            }
            if (IsHymn(value, out string hymn))
            {
                Hymns.Toggle(session, hymn);
                return;
            }
            if (!IsItem(value, out string item)) return;
            List<ItemSlot> pack = session.Inventory;
            for (int i = 0; i < pack.Count; i++)
            {
                if (pack[i].ItemId != item) continue;
                ItemDef def = session.Item(item);
                List<ItemAction> actions = ItemActions.For(def, ContainerKind.Inventory);
                if (def != null && actions.Count > 0) session.UseItem(ContainerKind.Inventory, i, actions[0].Id);
                return;
            }
        }
    }
}
