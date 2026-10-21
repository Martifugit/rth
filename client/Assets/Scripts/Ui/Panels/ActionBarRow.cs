using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// One row of 14 action bar slots, shared by the main and secondary bars. Shows each slot's
    /// ability, focus or item, its key, cooldown and whether it can be used. Click to use; drag an
    /// ability from the book, a focus from the focuses tab or an item from the pack onto a slot to
    /// place it; drag between slots to swap; drag a slot off the bars to clear it, or right-click
    /// it. While the bars are locked none of that dragging works and slots show no tooltips.
    /// </summary>
    public sealed class ActionBarRow
    {
        public const float SlotSize = 42f;

        private readonly UiRoot _ui;
        private readonly int _bar;
        private readonly Slot[] _slots = new Slot[ActionBars.SlotCount];
        private int _drawnVersion = -1;
        private const float FlashSeconds = 0.18f;
        private bool _wasOnGlobalCooldown;
        private float _flashUntil;
        private ActionBars _drawnFor;

        private sealed class Slot
        {
            public VisualElement Root;
            public VisualElement Icon;
            public RadialCooldown Cooldown;
            public Label Timer;
            public Label Count;
        }

        private GameSession Session => _ui.session;
        private bool BarsLocked => Session.Bars == null || Session.Bars.Locked;

        public ActionBarRow(UiRoot ui, VisualElement parent, int bar)
        {
            _ui = ui;
            _bar = bar;
            VisualElement row = UiKit.El(parent, "row");
            row.style.justifyContent = Justify.Center;
            for (int i = 0; i < ActionBars.SlotCount; i++)
            {
                int index = i;
                var slot = new Slot { Root = UiKit.El(row, "slot", "ability") };
                slot.Icon = UiKit.El(slot.Root, "slot__icon");
                slot.Icon.style.width = slot.Icon.style.height = 30f;
                slot.Icon.pickingMode = PickingMode.Ignore;
                slot.Cooldown = new RadialCooldown();
                slot.Root.Add(slot.Cooldown);
                slot.Count = UiKit.Text(slot.Root, "", "slot__count");
                slot.Count.pickingMode = PickingMode.Ignore;
                if (bar == 0) UiKit.Text(slot.Root, ActionBars.MainKeyLabels[i], "ability__key").pickingMode = PickingMode.Ignore;
                slot.Timer = UiKit.Text(slot.Root, "", "ability__timer");
                slot.Timer.pickingMode = PickingMode.Ignore;

                _ui.DragSource(slot.Root, () =>
                {
                    string value = Session.Bars?.Get(_bar, index);
                    if (value == null || BarsLocked) return null;
                    (Texture2D icon, Color tint) = IconFor(value);
                    return new UiRoot.Drag
                    {
                        Data = new BarSlotDrag { Bar = _bar, Slot = index },
                        Icon = icon,
                        Tint = tint,
                        OnNowhere = () => Session.Bars?.Set(_bar, index, null), // dragged off the bars: cleared
                    };
                }, () => Session.Bars?.Use(Session, _bar, index));
                slot.Root.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 1 || Session.Bars == null) return;
                    var options = new List<(string, System.Action)>();
                    if (Session.Bars.Get(_bar, index) != null && !BarsLocked) options.Add(("Remove from bar", () => Session.Bars.Set(_bar, index, null)));
                    options.Add((BarsLocked ? "Unlock action bars" : "Lock action bars", () => Session.Bars.Locked = !Session.Bars.Locked));
                    _ui.Menu(e.position, "Action bar", options);
                });
                _ui.AddDropTarget(slot.Root, data => !BarsLocked && Accepts(data), data => Drop(index, data));
                UiKit.Tooltip(slot.Root, () => BarsLocked ? null : Describe(Session.Bars?.Get(_bar, index)));
                _slots[i] = slot;
            }
        }

        private static bool Accepts(object data) =>
            data is AbilityDrag || data is FocusDrag || data is BarSlotDrag || (data is ItemDrag item && item.From == ContainerKind.Inventory);

        private void Drop(int index, object data)
        {
            ActionBars bars = Session.Bars;
            if (bars == null) return;
            switch (data)
            {
                case AbilityDrag a:
                    bars.Set(_bar, index, ActionBars.Ability(a.AbilityId));
                    break;
                case FocusDrag f:
                    bars.Set(_bar, index, ActionBars.Focus(f.FocusId));
                    break;
                case ItemDrag i:
                    bars.Set(_bar, index, ActionBars.Item(i.ItemId));
                    break;
                case BarSlotDrag s when s.Bar != _bar || s.Slot != index:
                    bars.Swap(s.Bar, s.Slot, _bar, index);
                    break;
            }
        }

        private (Texture2D, Color) IconFor(string value)
        {
            if (ActionBars.IsAbility(value, out string ability))
            {
                AbilityDef def = Session.Ability(ability);
                return (IconLibrary.ForAbility(def), IconLibrary.AbilityTint(def));
            }
            if (ActionBars.IsFocus(value, out string focus))
            {
                Focuses.Focus f = Focuses.Get(focus);
                return (f == null ? null : IconLibrary.Get(f.Icon), Focuses.Tint);
            }
            if (ActionBars.IsItem(value, out string item))
            {
                ItemDef def = Session.Item(item);
                return (IconLibrary.ForItem(def), IconLibrary.ItemTint(def));
            }
            return (null, Color.white);
        }

        private string Describe(string value)
        {
            if (ActionBars.IsAbility(value, out string ability))
            {
                AbilityDef a = Session.Ability(ability);
                return a == null ? null : GameText.Describe(a, Session.TickSeconds, Session.AbilityProblem(a));
            }
            if (ActionBars.IsFocus(value, out string focus))
            {
                Focuses.Focus f = Focuses.Get(focus);
                return f == null ? null : Focuses.Describe(f);
            }
            if (ActionBars.IsItem(value, out string item))
            {
                ItemDef def = Session.Item(item);
                return def == null ? null : GameText.Describe(def, Session.CountInPack(item));
            }
            return UiKit.Colour("Empty. Drag an ability, a focus or an item from your pack here.", UiKit.Muted);
        }

        public void Refresh()
        {
            ActionBars bars = Session.Bars;
            PlayerStateMessage state = Session.PlayerState;
            if (bars == null || state == null) return;

            if (bars != _drawnFor || bars.Version != _drawnVersion)
            {
                _drawnFor = bars;
                _drawnVersion = bars.Version;
                for (int i = 0; i < _slots.Length; i++)
                {
                    (Texture2D icon, Color tint) = IconFor(bars.Get(_bar, i));
                    UiKit.SetIcon(_slots[i].Icon, icon, tint);
                }
            }

            float now = Session.EstimatedTick;
            int gcd = Mathf.Max(1, Session.Content?.GlobalCooldownTicks ?? 3);
            // When the global cooldown runs out (the tick after its last), ready slots blink once.
            bool onGlobal = state.GlobalCooldownReadyTick > now;
            if (_wasOnGlobalCooldown && !onGlobal) _flashUntil = Time.unscaledTime + FlashSeconds;
            _wasOnGlobalCooldown = onGlobal;
            bool flashing = Time.unscaledTime < _flashUntil;
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                string value = bars.Get(_bar, i);
                float remaining = 0f;
                float total = gcd;
                bool usable = true;
                bool queued = false;
                bool stalled = false;
                bool held = false;
                bool isAbility = false;
                slot.Count.text = "";
                if (ActionBars.IsAbility(value, out string id) && Session.Ability(id) is AbilityDef ability)
                {
                    isAbility = true;
                    usable = Session.AbilityProblem(ability) == null;
                    queued = state.QueuedAbility == ability.Id;
                    stalled = state.StalledAbility == ability.Id; // used out of range: fires on reaching the target
                    // The slot's cooldown: its ability's own when that runs longer, else the global one.
                    remaining = state.GlobalCooldownReadyTick - now;
                    foreach (CooldownState cd in state.Cooldowns)
                    {
                        if (cd.AbilityId != ability.Id || cd.ReadyTick - now <= remaining) continue;
                        remaining = cd.ReadyTick - now;
                        total = Mathf.Max(ability.CooldownTicks, gcd);
                    }
                }
                else if (ActionBars.IsFocus(value, out string focus))
                {
                    held = Focuses.Get(focus)?.IsHeld(state) ?? false;
                }
                else if (ActionBars.IsItem(value, out string item))
                {
                    int count = Session.CountInPack(item);
                    usable = count > 0;
                    slot.Count.text = count > 1 ? UiKit.FormatCount(count) : "";
                }
                slot.Root.EnableInClassList("ability--ready", flashing && isAbility && usable && remaining <= 0f);
                slot.Root.EnableInClassList("ability--unusable", !usable);
                slot.Root.EnableInClassList("ability--queued", queued);
                slot.Root.EnableInClassList("ability--stalled", stalled);
                slot.Root.EnableInClassList("slot--on", held);
                slot.Cooldown.Remaining = remaining > 0f ? remaining / total : 0f;
                float seconds = remaining * Session.TickSeconds;
                slot.Timer.text = seconds > 2.4f ? Mathf.CeilToInt(seconds).ToString() : "";
            }
        }
    }
}
