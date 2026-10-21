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
    /// the server as client state, so a layout follows the player. A new character's main bar
    /// starts as the content's default. Locked bars can't be rearranged and show no tooltips.
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

        private ActionBars(GameSession session, string[][] bars)
        {
            _session = session;
            _bars = bars;
        }

        public static ActionBars Load(GameSession session)
        {
            string[][] bars = null;
            if (session.ClientState.TryGetValue(StateKey, out string json))
            {
                try
                {
                    bars = JsonConvert.DeserializeObject<string[][]>(json);
                }
                catch (JsonException)
                {
                    bars = null;
                }
            }
            if (bars == null)
            {
                bars = new string[BarCount][];
                bars[0] = new string[SlotCount];
                for (int i = 0; i < SlotCount && i < session.Content.ActionBar.Count; i++) bars[0][i] = "a:" + session.Content.ActionBar[i];
            }
            var normalised = new string[BarCount][];
            for (int b = 0; b < BarCount; b++)
            {
                normalised[b] = new string[SlotCount];
                if (b < bars.Length && bars[b] != null)
                    for (int s = 0; s < SlotCount && s < bars[b].Length; s++)
                        normalised[b][s] = bars[b][s] != null && Renamed.TryGetValue(bars[b][s], out string now) ? now : bars[b][s];
            }
            return new ActionBars(session, normalised);
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
            Version++;
            _session.SetClientState(StateKey, JsonConvert.SerializeObject(_bars));
        }

        /// <summary>Swaps two slots (dragging one slot onto another), across bars too.</summary>
        public void Swap(int barA, int slotA, int barB, int slotB)
        {
            string a = _bars[barA][slotA];
            _bars[barA][slotA] = _bars[barB][slotB];
            _bars[barB][slotB] = a;
            Version++;
            _session.SetClientState(StateKey, JsonConvert.SerializeObject(_bars));
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
