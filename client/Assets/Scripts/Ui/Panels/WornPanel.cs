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
    /// off, right-click for its options. Your stats are on the Character tab.
    /// </summary>
    public sealed class WornPanel : UiPanel
    {
        public override string Id => "worn";
        public override string Title => "Worn equipment";
        public override string Icon => "worn";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 1;
        public override Key Hotkey => Key.E;

        private Figure _figure;

        protected override void Build(VisualElement root)
        {
            _figure = new Figure(Ui, Session, root, null);
            _figure.Root.style.marginTop = 10f;
        }

        public override void Refresh() => _figure.Refresh(Session);

        /// <summary>
        /// The equipment figure, reusable (the bank shows it too). By default a click uses the
        /// item's first action (remove); <paramref name="onClick"/> replaces that (deposit).
        /// </summary>
        public sealed class Figure
        {
            private const float Cell = 50f;

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

            public Figure(UiRoot ui, GameSession session, VisualElement parent, Action<EquipSlot> onClick)
            {
                Root = UiKit.El(parent);
                Root.style.width = 3 * Cell;
                Root.style.height = 5 * Cell;
                Root.style.alignSelf = Align.Center;

                // A faint body behind the slots: spine and shoulders.
                Line(Root, 1.5f * Cell - 1f, 0.5f * Cell, 2f, 4f * Cell);
                Line(Root, 0.5f * Cell, 2.5f * Cell - 1f, 2f * Cell, 2f);

                foreach (KeyValuePair<EquipSlot, Vector2Int> place in Layout)
                {
                    EquipSlot slot = place.Key;
                    VisualElement box = UiKit.El(Root, "slot");
                    box.style.position = Position.Absolute;
                    box.style.left = place.Value.x * Cell + 4f;
                    box.style.top = place.Value.y * Cell + 4f;
                    VisualElement icon = UiKit.El(box, "slot__icon");
                    icon.pickingMode = PickingMode.Ignore;
                    box.RegisterCallback<PointerDownEvent>(e =>
                    {
                        ItemDef def = Def(session, slot);
                        if (def == null) return;
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
                    UiKit.Tooltip(box, () => Def(session, slot) is ItemDef d ? GameText.Describe(d, 1) : UiKit.Colour(slot + ": nothing worn", UiKit.Muted));
                    _slots[slot] = (box, icon);
                }
            }

            private static void Line(VisualElement parent, float left, float top, float width, float height)
            {
                VisualElement line = UiKit.El(parent);
                line.style.position = Position.Absolute;
                line.style.left = left;
                line.style.top = top;
                line.style.width = width;
                line.style.height = height;
                line.style.backgroundColor = new Color(0.79f, 0.64f, 0.37f, 0.25f);
            }

            private static ItemDef Def(GameSession session, EquipSlot slot)
            {
                int index = (int)slot;
                if (index >= session.Equipment.Count) return null;
                ItemSlot s = session.Equipment[index];
                return s.IsEmpty ? null : session.Item(s.ItemId);
            }

            public void Refresh(GameSession session)
            {
                foreach (KeyValuePair<EquipSlot, (VisualElement Slot, VisualElement Icon)> s in _slots)
                {
                    ItemDef def = Def(session, s.Key);
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
