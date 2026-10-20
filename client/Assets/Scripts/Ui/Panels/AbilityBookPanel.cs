using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// A combat style's abilities as a book of icons, basics, thresholds and ultimates, like a
    /// spellbook. Locked ones are dimmed with the level they need; hover for the full card,
    /// click to use. Occultism is the only style so far; each style gets a book.
    /// </summary>
    public sealed class AbilityBookPanel : UiPanel
    {
        public override string Id => "abilities";
        public override string Title => "Occultism abilities";
        public override string Icon => "book";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 2;
        public override Key Hotkey => Key.A;

        private ScrollView _list;
        private Label _level;
        private int _builtFor = -1;
        private readonly List<(string Id, VisualElement Socket, Label Level)> _sockets = new List<(string, VisualElement, Label)>();

        protected override void Build(VisualElement root)
        {
            _level = UiKit.Text(root, "", "muted", "small");
            _level.style.unityTextAlign = TextAnchor.MiddleCenter;
            _list = new ScrollView();
            _list.AddToClassList("scroll");
            root.Add(_list);
        }

        public override void Refresh()
        {
            if (Session.Content == null) return;
            if (Session.Content.Abilities.Count != _builtFor) Rebuild();
            _level.text = "Occultism level " + Session.Level("occultism");
            foreach ((string id, VisualElement socket, Label level) in _sockets)
            {
                AbilityDef a = Session.Ability(id);
                if (a == null) continue;
                int needed = LevelNeeded(a);
                bool locked = needed > Session.Level(SkillOf(a));
                socket.EnableInClassList("socket--locked", locked);
                level.text = locked ? needed.ToString() : "";
                socket.EnableInClassList("socket--on", Session.PlayerState != null && Session.PlayerState.QueuedAbility == id);
            }
        }

        private static int LevelNeeded(AbilityDef a)
        {
            int level = 1;
            foreach (SkillRequirement r in a.Requirements) level = Mathf.Max(level, r.Level);
            return level;
        }

        private static string SkillOf(AbilityDef a)
        {
            foreach (SkillRequirement r in a.Requirements) return r.Skill;
            return "occultism";
        }

        private void Rebuild()
        {
            _builtFor = Session.Content.Abilities.Count;
            _list.Clear();
            _sockets.Clear();
            foreach ((AbilityType type, string title) in new[] { (AbilityType.Basic, "Basics"), (AbilityType.Threshold, "Thresholds"), (AbilityType.Ultimate, "Ultimates") })
            {
                UiKit.Text(_list, title, "heading", "small");
                VisualElement grid = UiKit.El(_list, "slot-grid");
                foreach (AbilityDef ability in Session.Content.Abilities)
                {
                    if (ability.Type != type) continue;
                    string id = ability.Id;
                    VisualElement socket = UiKit.El(grid, "socket");
                    socket.style.backgroundColor = Color.Lerp(GameText.AbilityColor(ability), Color.black, 0.5f);
                    UiKit.Icon(socket, IconLibrary.ForAbility(ability), 32f, "slot__icon").pickingMode = PickingMode.Ignore;
                    Label level = UiKit.Text(socket, "", "socket__level");
                    level.pickingMode = PickingMode.Ignore;
                    socket.RegisterCallback<ClickEvent>(_ => Session.UseAbility(id));
                    UiKit.Tooltip(socket, () =>
                    {
                        AbilityDef a = Session.Ability(id);
                        return a == null ? null : GameText.Describe(a, Session.TickSeconds, Session.AbilityProblem(a));
                    });
                    _sockets.Add((id, socket, level));
                }
            }
        }
    }
}
