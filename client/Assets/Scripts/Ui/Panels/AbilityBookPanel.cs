using System.Collections.Generic;
using Rth.Shared.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The abilities of a combat style, grouped basic / threshold / ultimate. Click one to use
    /// it. Occultism is the only style so far; each new style gets a book of its own.
    /// </summary>
    public sealed class AbilityBookPanel : UiPanel
    {
        public override string Id => "abilities";
        public override string Title => "Abilities: Occultism";
        public override string Tab => "Book";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 2;
        public override Key Hotkey => Key.A;

        private ScrollView _list;
        private int _builtFor = -1;
        private readonly List<(string Id, VisualElement Row)> _rows = new List<(string, VisualElement)>();

        protected override void Build(VisualElement root)
        {
            _list = new ScrollView();
            _list.AddToClassList("scroll");
            root.Add(_list);
        }

        public override void Refresh()
        {
            if (Session.Content == null) return;
            int count = Session.Content.Abilities.Count;
            if (count != _builtFor) Rebuild();
            foreach ((string id, VisualElement row) in _rows)
            {
                AbilityDef a = Session.Ability(id);
                bool usable = a != null && Session.AbilityProblem(a) == null;
                row.style.opacity = usable ? 1f : 0.55f;
            }
        }

        private void Rebuild()
        {
            _builtFor = Session.Content.Abilities.Count;
            _list.Clear();
            _rows.Clear();
            foreach (AbilityType type in new[] { AbilityType.Basic, AbilityType.Threshold, AbilityType.Ultimate })
            {
                UiKit.Text(_list, type + "s", "heading").style.marginTop = 4f;
                foreach (AbilityDef ability in Session.Content.Abilities)
                {
                    if (ability.Type != type) continue;
                    string id = ability.Id;
                    VisualElement row = UiKit.El(_list, "row");
                    row.style.marginBottom = 2f;
                    row.style.backgroundColor = new Color(0.17f, 0.15f, 0.12f);
                    VisualElement icon = UiKit.El(row);
                    icon.style.width = icon.style.height = 24f;
                    icon.style.marginRight = 5f;
                    icon.style.backgroundColor = GameText.AbilityColor(ability);
                    Label name = UiKit.Text(row, ability.Name);
                    name.style.flexGrow = 1;
                    string level = "";
                    foreach (Rth.Shared.Skills.SkillRequirement r in ability.Requirements) level = "lvl " + r.Level;
                    UiKit.Text(row, level, "muted").style.fontSize = 10;
                    row.RegisterCallback<ClickEvent>(_ => Session.UseAbility(id));
                    UiKit.Tooltip(row, () =>
                    {
                        AbilityDef a = Session.Ability(id);
                        if (a == null) return null;
                        string problem = Session.AbilityProblem(a);
                        return GameText.Describe(a, Session.TickSeconds) + (problem != null ? "\n\n" + problem : "");
                    });
                    _rows.Add((id, row));
                }
            }
        }
    }
}
