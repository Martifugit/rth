using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>Worn equipment, one row per slot (click to remove, right-click for options), and the totals it gives.</summary>
    public sealed class WornPanel : UiPanel
    {
        public override string Id => "worn";
        public override string Title => "Worn equipment";
        public override string Tab => "Worn";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 1;
        public override Key Hotkey => Key.E;

        private readonly List<(VisualElement Row, VisualElement Icon, Label Name)> _rows = new List<(VisualElement, VisualElement, Label)>();
        private Label _stats;

        protected override void Build(VisualElement root)
        {
            var scroll = new ScrollView();
            scroll.AddToClassList("scroll");
            root.Add(scroll);
            string[] names = System.Enum.GetNames(typeof(EquipSlot));
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                VisualElement row = UiKit.El(scroll, "row");
                row.style.height = 22f;
                row.style.marginBottom = 1f;
                row.style.backgroundColor = new Color(0.17f, 0.15f, 0.12f);
                VisualElement icon = UiKit.El(row);
                icon.style.width = icon.style.height = 16f;
                icon.style.marginLeft = icon.style.marginRight = 3f;
                Label label = UiKit.Text(row, names[i] + ": -");
                label.style.fontSize = 11;
                row.RegisterCallback<PointerDownEvent>(e =>
                {
                    ItemDef def = Def(index);
                    if (def == null) return;
                    if (e.button == 0) GameText.DefaultAction(Session, ContainerKind.Equipment, index, def);
                    else if (e.button == 1) Ui.Menu(e.position, def.Name, GameText.ItemOptions(Session, ContainerKind.Equipment, index, def));
                });
                UiKit.Tooltip(row, () => Def(index) is ItemDef d ? GameText.Describe(d, 1) : null);
                _rows.Add((row, icon, label));
            }
            UiKit.Text(scroll, "Bonuses", "heading").style.marginTop = 6f;
            _stats = UiKit.Text(scroll, "");
            _stats.style.fontSize = 11;
        }

        private ItemDef Def(int index)
        {
            if (index >= Session.Equipment.Count) return null;
            ItemSlot slot = Session.Equipment[index];
            return slot.IsEmpty ? null : Session.Item(slot.ItemId);
        }

        public override void Refresh()
        {
            string[] names = System.Enum.GetNames(typeof(EquipSlot));
            for (int i = 0; i < _rows.Count; i++)
            {
                ItemDef def = Def(i);
                ItemSlot slot = i < Session.Equipment.Count ? Session.Equipment[i] : default;
                _rows[i].Name.text = names[i] + ": " + (def?.Name ?? (slot.IsEmpty ? "-" : slot.ItemId));
                _rows[i].Icon.style.backgroundColor = def != null ? WorldView.ParseColor(def.Color) : new Color(0f, 0f, 0f, 0f);
            }

            var lines = new List<string>();
            foreach (string stat in StatIds.All)
            {
                Session.Stats.TryGetValue(stat, out int value);
                lines.Add(GameText.StatLabel(stat) + ": " + value + GameText.StatUnit(stat));
            }
            _stats.text = string.Join("\n", lines);
        }
    }
}
