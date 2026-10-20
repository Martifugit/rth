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
    /// ability or item, its key, cooldown and whether it can be used. Click to use; drag an
    /// ability from the book or an item from the pack onto a slot to place it; drag between
    /// slots to swap; drag a slot off the bars to clear it, or right-click it.
    /// </summary>
    public sealed class ActionBarRow
    {
        public const float SlotSize = 42f;

        private readonly UiRoot _ui;
        private readonly int _bar;
        private readonly Slot[] _slots = new Slot[ActionBars.SlotCount];
        private int _drawnVersion = -1;
        private ActionBars _drawnFor;

        private sealed class Slot
        {
            public VisualElement Root;
            public VisualElement Icon;
            public VisualElement Cooldown;
            public Label Timer;
            public Label Count;
            public Vector2 PressedAt;
            public bool Pressed;
        }

        private GameSession Session => _ui.session;

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
                slot.Cooldown = UiKit.El(slot.Root, "ability__cooldown");
                slot.Cooldown.pickingMode = PickingMode.Ignore;
                slot.Count = UiKit.Text(slot.Root, "", "slot__count");
                slot.Count.pickingMode = PickingMode.Ignore;
                if (bar == 0) UiKit.Text(slot.Root, ActionBars.MainKeyLabels[i], "ability__key").pickingMode = PickingMode.Ignore;
                slot.Timer = UiKit.Text(slot.Root, "", "ability__timer");
                slot.Timer.pickingMode = PickingMode.Ignore;

                slot.Root.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button == 0)
                    {
                        slot.Pressed = true;
                        slot.PressedAt = e.position;
                    }
                    else if (e.button == 1 && Session.Bars?.Get(_bar, index) != null)
                    {
                        _ui.Menu(e.position, "Action bar", new List<(string, System.Action)> { ("Remove from bar", () => Session.Bars.Set(_bar, index, null)) });
                    }
                });
                slot.Root.RegisterCallback<PointerMoveEvent>(e =>
                {
                    if (!slot.Pressed || _ui.IsDragging || _ui.Locked) return;
                    if (((Vector2)e.position - slot.PressedAt).magnitude < 6f || Session.Bars?.Get(_bar, index) == null) return;
                    slot.Pressed = false;
                    (Texture2D icon, Color tint) = IconFor(Session.Bars.Get(_bar, index));
                    _ui.BeginDrag(new UiRoot.Drag
                    {
                        Data = new BarSlotDrag { Bar = _bar, Slot = index },
                        Icon = icon,
                        Tint = tint,
                        OnNowhere = () => Session.Bars?.Set(_bar, index, null), // dragged off the bars: cleared
                    });
                });
                slot.Root.RegisterCallback<PointerUpEvent>(e =>
                {
                    if (e.button != 0 || !slot.Pressed) return;
                    slot.Pressed = false;
                    Session.Bars?.Use(Session, _bar, index);
                });
                slot.Root.RegisterCallback<PointerLeaveEvent>(_ => slot.Pressed = false);
                _ui.AddDropTarget(slot.Root, Accepts, data => Drop(index, data));
                UiKit.Tooltip(slot.Root, () => Describe(Session.Bars?.Get(_bar, index)));
                _slots[i] = slot;
            }
        }

        private static bool Accepts(object data) =>
            data is AbilityDrag || data is BarSlotDrag || (data is ItemDrag item && item.From == ContainerKind.Inventory);

        private void Drop(int index, object data)
        {
            ActionBars bars = Session.Bars;
            if (bars == null) return;
            switch (data)
            {
                case AbilityDrag a:
                    bars.Set(_bar, index, ActionBars.Ability(a.AbilityId));
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
            if (ActionBars.IsItem(value, out string item))
            {
                ItemDef def = Session.Item(item);
                return def == null ? null : GameText.Describe(def, Session.CountInPack(item));
            }
            return UiKit.Colour("Empty. Drag an ability from the book, or an item from your pack.", UiKit.Muted);
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
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                string value = bars.Get(_bar, i);
                float remaining = 0f;
                float total = 3f;
                bool usable = true;
                bool queued = false;
                slot.Count.text = "";
                if (ActionBars.IsAbility(value, out string id) && Session.Ability(id) is AbilityDef ability)
                {
                    usable = Session.AbilityProblem(ability) == null;
                    queued = state.QueuedAbility == ability.Id;
                    // Cooldown: whichever is later, the ability's own or the global one.
                    float ready = state.GlobalCooldownReadyTick;
                    foreach (CooldownState cd in state.Cooldowns)
                        if (cd.AbilityId == ability.Id) ready = Mathf.Max(ready, cd.ReadyTick);
                    remaining = ready - now;
                    total = Mathf.Max(ability.CooldownTicks, 3);
                }
                else if (ActionBars.IsItem(value, out string item))
                {
                    int count = Session.CountInPack(item);
                    usable = count > 0;
                    slot.Count.text = count > 1 ? UiKit.FormatCount(count) : "";
                }
                slot.Root.EnableInClassList("ability--unusable", !usable);
                slot.Root.EnableInClassList("ability--queued", queued);
                slot.Cooldown.style.height = Length.Percent(remaining > 0f ? Mathf.Clamp01(remaining / total) * 100f : 0f);
                float seconds = remaining * Session.TickSeconds;
                slot.Timer.text = seconds > 0.95f ? Mathf.CeilToInt(seconds).ToString() : "";
            }
        }
    }
}
