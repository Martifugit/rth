using System;
using System.Collections.Generic;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// Worn equipment as a figure: head at the top, then cape and neck, weapon, body and offhand,
    /// legs, and hands, feet and ring. Empty slots show what goes there. Click an item to take it
    /// off, right-click for its options. Beneath the figure, a row of buttons (<see cref="Buttons"/>):
    /// your combat stats and how others see you, both in the character window.
    /// </summary>
    public sealed class WornPanel : UiPanel
    {
        public override string Id => "worn";
        public override string Title => "Worn equipment";
        public override string Icon => "worn";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 1;
        public override Key Hotkey => Key.E;

        public override Vector2 MinSize => new Vector2(3 * 40f + 24f, 5 * 40f + 60f + ButtonRowHeight);
        public override bool Scrolls => false;

        private const float ButtonRowHeight = 40f;

        /// <summary>A button in the row under the figure.</summary>
        public sealed class WornButton
        {
            public string Icon;
            public string Tooltip;
            public Action<UiRoot> Click;
        }

        /// <summary>
        /// The buttons under the figure, in order. Add one here for a new view of your gear (items
        /// kept on death, a price check); the row is built from this list.
        /// </summary>
        public static readonly List<WornButton> Buttons = new List<WornButton>
        {
            new WornButton { Icon = "character", Tooltip = "Combat stats", Click = ui => ui.OpenModal<Modals.CharacterModal>(Modals.StatsTab.TabId) },
            new WornButton { Icon = "eye", Tooltip = "Inspect: what other players see", Click = ui => ui.session.Inspect(ui.session.LocalPlayerId) },
        };

        private Figure _figure;

        protected override void Build(VisualElement root)
        {
            // The figure takes the space above the buttons, and grows and shrinks with the window.
            VisualElement area = UiKit.El(root, "worn__figure-area");
            area.style.flexGrow = 1;
            area.style.flexShrink = 1;
            area.style.flexBasis = 0;
            area.style.minHeight = 0;
            area.style.justifyContent = Justify.Center;
            _figure = new Figure(Ui, Session, area, null);
            area.RegisterCallback<GeometryChangedEvent>(_ =>
                _figure.Fit(Mathf.Clamp(Mathf.Min(area.contentRect.width / 3f, (area.contentRect.height - 8f) / 5f), 40f, 76f)));

            VisualElement row = UiKit.El(root, "footer-row", "worn__buttons");
            foreach (WornButton b in Buttons)
            {
                WornButton button = b;
                UiKit.IconButton(row, button.Icon, button.Tooltip, () => button.Click(Ui));
            }
        }

        public override void Refresh() => _figure.Refresh(Session);

        /// <summary>
        /// The equipment figure, reusable (the bank and the inspect page show it too). By default it
        /// shows your own gear and a click uses the item's first action (remove);
        /// <paramref name="onClick"/> replaces that (deposit). A read-only figure only shows what
        /// it is given (<see cref="Show"/>): someone else's gear, with tooltips and no clicks.
        /// </summary>
        public sealed class Figure
        {
            private float _cell = 50f;

            private static readonly Dictionary<EquipSlot, Vector2Int> Layout = new Dictionary<EquipSlot, Vector2Int>
            {
                [EquipSlot.Head] = new Vector2Int(1, 0),
                [EquipSlot.Cape] = new Vector2Int(0, 1),
                [EquipSlot.Neck] = new Vector2Int(1, 1),
                [EquipSlot.Weapon] = new Vector2Int(0, 2),
                [EquipSlot.Body] = new Vector2Int(1, 2),
                [EquipSlot.Offhand] = new Vector2Int(2, 2),
                [EquipSlot.Legs] = new Vector2Int(1, 3),
                [EquipSlot.Hands] = new Vector2Int(0, 4),
                [EquipSlot.Feet] = new Vector2Int(1, 4),
                [EquipSlot.Ring] = new Vector2Int(2, 4),
            };

            public readonly VisualElement Root;
            private readonly Dictionary<EquipSlot, (VisualElement Slot, VisualElement Icon)> _slots = new Dictionary<EquipSlot, (VisualElement, VisualElement)>();
            private readonly Dictionary<EquipSlot, string> _shown = new Dictionary<EquipSlot, string>();
            private readonly VisualElement _spine;
            private readonly VisualElement _shoulders;
            private readonly Func<EquipSlot, ItemDef> _own;
            private Func<EquipSlot, ItemDef> _source;

            public Figure(UiRoot ui, GameSession session, VisualElement parent, Action<EquipSlot> onClick, bool readOnly = false)
            {
                _own = slot => Def(session, slot);
                _source = _own;
                Root = UiKit.El(parent);
                Root.style.alignSelf = Align.Center;

                // A faint body behind the slots: spine and shoulders.
                _spine = Line(Root);
                _shoulders = Line(Root);

                foreach (KeyValuePair<EquipSlot, Vector2Int> place in Layout)
                {
                    EquipSlot slot = place.Key;
                    VisualElement box = UiKit.El(Root, "slot");
                    box.style.position = Position.Absolute;
                    VisualElement icon = UiKit.El(box, "slot__icon");
                    icon.pickingMode = PickingMode.Ignore;
                    box.RegisterCallback<PointerDownEvent>(e =>
                    {
                        ItemDef def = _source(slot);
                        if (def == null || readOnly) return;
                        if (e.button == 0)
                        {
                            if (onClick != null) onClick(slot);
                            else GameText.DefaultAction(session, ContainerKind.Equipment, (int)slot, def);
                        }
                        else if (e.button == 1)
                        {
                            ui.Menu(e.position, def.Name, GameText.ItemOptions(session, ContainerKind.Equipment, (int)slot, def));
                        }
                    });
                    UiKit.Tooltip(box, () => _source(slot) is ItemDef d ? GameText.Describe(d, 1) : UiKit.Colour(slot + ": nothing worn", UiKit.Muted));
                    _slots[slot] = (box, icon);
                }
                Fit(_cell);
            }

            /// <summary>Lays the figure out with cells <paramref name="cell"/> pixels square.</summary>
            public void Fit(float cell)
            {
                _cell = cell;
                Root.style.width = 3 * cell;
                Root.style.height = 5 * cell;
                Place(_spine, 1.5f * cell - 1f, 0.5f * cell, 2f, 4f * cell);
                Place(_shoulders, 0.5f * cell, 2.5f * cell - 1f, 2f * cell, 2f);
                float slot = cell - 8f;
                foreach (KeyValuePair<EquipSlot, Vector2Int> place in Layout)
                {
                    (VisualElement box, VisualElement icon) = _slots[place.Key];
                    Place(box, place.Value.x * cell + 4f, place.Value.y * cell + 4f, slot, slot);
                    icon.style.width = icon.style.height = slot * 0.8f;
                }
            }

            private static VisualElement Line(VisualElement parent)
            {
                VisualElement line = UiKit.El(parent);
                line.style.position = Position.Absolute;
                line.style.backgroundColor = new Color(0.79f, 0.64f, 0.37f, 0.25f);
                return line;
            }

            private static void Place(VisualElement e, float left, float top, float width, float height)
            {
                e.style.left = left;
                e.style.top = top;
                e.style.width = width;
                e.style.height = height;
            }

            private static ItemDef Def(GameSession session, EquipSlot slot)
            {
                int index = (int)slot;
                if (index >= session.Equipment.Count) return null;
                ItemSlot s = session.Equipment[index];
                return s.IsEmpty ? null : session.Item(s.ItemId);
            }

            /// <summary>Shows your own gear.</summary>
            public void Refresh(GameSession session) => Show(_own);

            /// <summary>Shows the gear <paramref name="source"/> gives for each slot (null: nothing worn).</summary>
            public void Show(Func<EquipSlot, ItemDef> source)
            {
                _source = source;
                foreach (KeyValuePair<EquipSlot, (VisualElement Slot, VisualElement Icon)> s in _slots)
                {
                    ItemDef def = source(s.Key);
                    string id = def?.Id ?? "";
                    if (_shown.TryGetValue(s.Key, out string was) && was == id) continue;
                    _shown[s.Key] = id;
                    if (def != null) UiKit.SetItem(s.Value.Icon, def);
                    else UiKit.SetIcon(s.Value.Icon, IconLibrary.Get(IconLibrary.SlotIcon(s.Key)), UiKit.Muted);
                    s.Value.Slot.EnableInClassList("slot--empty", def == null);
                }
            }
        }
    }
}
