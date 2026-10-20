using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// Worn equipment as a figure: head at the top, then cape and neck, weapon, body and offhand,
    /// legs, and hands, feet and ring. Empty slots show what goes there. Click an item to remove
    /// it, right-click for its options. Below, the totals your gear gives.
    /// </summary>
    public sealed class WornPanel : UiPanel
    {
        private const float Cell = 44f;

        public override string Id => "worn";
        public override string Title => "Worn equipment";
        public override string Icon => "worn";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 1;
        public override Key Hotkey => Key.E;

        // Where each slot sits in the figure (column, row), in EquipSlot order.
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

        private readonly Dictionary<EquipSlot, (VisualElement Socket, VisualElement Icon)> _sockets = new Dictionary<EquipSlot, (VisualElement, VisualElement)>();
        private readonly Dictionary<string, Label> _stats = new Dictionary<string, Label>();
        private readonly Dictionary<EquipSlot, string> _shown = new Dictionary<EquipSlot, string>();

        protected override void Build(VisualElement root)
        {
            var scroll = new ScrollView();
            scroll.AddToClassList("scroll");
            root.Add(scroll);

            VisualElement figure = UiKit.El(scroll);
            figure.style.width = 3 * Cell;
            figure.style.height = 5 * Cell;
            figure.style.alignSelf = Align.Center;
            figure.style.marginTop = 2f;

            // The body's spine: a faint line joining head, neck, body and legs, as on the classic tab.
            VisualElement spine = UiKit.El(figure);
            spine.style.position = Position.Absolute;
            spine.style.left = 1.5f * Cell - 1f;
            spine.style.top = 0.5f * Cell;
            spine.style.width = 2f;
            spine.style.height = 4f * Cell;
            spine.style.backgroundColor = new Color(0.09f, 0.07f, 0.05f);
            VisualElement arms = UiKit.El(figure);
            arms.style.position = Position.Absolute;
            arms.style.left = 0.5f * Cell;
            arms.style.top = 2.5f * Cell - 1f;
            arms.style.width = 2f * Cell;
            arms.style.height = 2f;
            arms.style.backgroundColor = new Color(0.09f, 0.07f, 0.05f);

            foreach (KeyValuePair<EquipSlot, Vector2Int> place in Layout)
            {
                EquipSlot slot = place.Key;
                VisualElement socket = UiKit.El(figure, "socket");
                socket.style.position = Position.Absolute;
                socket.style.left = place.Value.x * Cell + 1f;
                socket.style.top = place.Value.y * Cell + 1f;
                VisualElement icon = UiKit.El(socket, "slot__icon");
                icon.pickingMode = PickingMode.Ignore;
                socket.RegisterCallback<PointerDownEvent>(e =>
                {
                    ItemDef def = Def(slot);
                    if (def == null) return;
                    if (e.button == 0) GameText.DefaultAction(Session, ContainerKind.Equipment, (int)slot, def);
                    else if (e.button == 1) Ui.Menu(e.position, def.Name, GameText.ItemOptions(Session, ContainerKind.Equipment, (int)slot, def));
                });
                UiKit.Tooltip(socket, () => Def(slot) is ItemDef d ? GameText.Describe(d, 1) : UiKit.Colour(slot + ": empty", UiKit.Muted));
                _sockets[slot] = (socket, icon);
            }

            UiKit.Text(scroll, "Bonuses", "heading").style.unityTextAlign = TextAnchor.MiddleCenter;
            VisualElement table = UiKit.El(scroll, "well");
            foreach (string stat in StatIds.All)
            {
                VisualElement row = UiKit.El(table, "row");
                UiKit.Text(row, GameText.StatLabel(stat), "small");
                UiKit.El(row, "spacer");
                _stats[stat] = UiKit.Text(row, "", "value", "small");
            }
        }

        private ItemDef Def(EquipSlot slot)
        {
            int index = (int)slot;
            if (index >= Session.Equipment.Count) return null;
            ItemSlot s = Session.Equipment[index];
            return s.IsEmpty ? null : Session.Item(s.ItemId);
        }

        public override void Refresh()
        {
            foreach (KeyValuePair<EquipSlot, (VisualElement Socket, VisualElement Icon)> s in _sockets)
            {
                ItemDef def = Def(s.Key);
                string id = def?.Id ?? "";
                if (_shown.TryGetValue(s.Key, out string was) && was == id) continue;
                _shown[s.Key] = id;
                UiKit.SetIcon(s.Value.Icon, def != null ? IconLibrary.ForItem(def) : IconLibrary.ForSlot(s.Key));
                s.Value.Socket.EnableInClassList("socket--empty", def == null);
            }
            foreach (KeyValuePair<string, Label> stat in _stats)
            {
                Session.Stats.TryGetValue(stat.Key, out int value);
                stat.Value.text = value + GameText.StatUnit(stat.Key);
            }
        }
    }
}
