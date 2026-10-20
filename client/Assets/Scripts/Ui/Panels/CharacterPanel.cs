using System.Collections.Generic;
using Rth.Shared.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>Your character at a glance: name, total level, and every combat stat your gear and levels give.</summary>
    public sealed class CharacterPanel : UiPanel
    {
        public override string Id => "character";
        public override string Title => "Character";
        public override string Icon => "character";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 2;

        private Label _name;
        private Label _total;
        private readonly Dictionary<string, Label> _stats = new Dictionary<string, Label>();

        protected override void Build(VisualElement root)
        {
            VisualElement head = UiKit.El(root, "card", "row");
            UiKit.Icon(head, IconLibrary.Get("character"), 40f).style.unityBackgroundImageTintColor = UiKit.IconTint;
            VisualElement who = UiKit.El(head);
            who.style.marginLeft = 10f;
            _name = UiKit.Text(who, "", "title-large");
            _total = UiKit.Text(who, "", "muted", "small");

            UiKit.Heading(root, "Combat stats").style.marginTop = 12f;
            VisualElement table = UiKit.El(root, "well");
            foreach (string stat in StatIds.All)
            {
                VisualElement row = UiKit.El(table, "row");
                row.style.paddingTop = row.style.paddingBottom = 2f;
                UiKit.Text(row, GameText.StatLabel(stat));
                UiKit.El(row, "spacer");
                _stats[stat] = UiKit.Text(row, "", "value", "bold");
            }
        }

        public override void Refresh()
        {
            _name.text = Session.PlayerName;
            int total = 0;
            if (Session.Content != null)
                foreach (Rth.Shared.Skills.SkillDef s in Session.Content.Skills) total += Session.Level(s.Id);
            _total.text = "Total level " + total;
            foreach (KeyValuePair<string, Label> stat in _stats)
            {
                Session.Stats.TryGetValue(stat.Key, out int value);
                stat.Value.text = value + GameText.StatUnit(stat.Key);
            }
        }
    }
}
