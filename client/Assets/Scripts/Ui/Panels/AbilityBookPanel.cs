using System.Collections.Generic;
using Rth.Client.Ui.Modals;
using Rth.Shared.Combat;
using Rth.Shared.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// A combat style's abilities as a book of icons: builders, spenders and ultimates. Locked
    /// ones are dimmed with the level they need. Click to use, drag onto an action bar to place
    /// it, hover for the full card. Occultism is the only style so far; each style gets a book.
    /// </summary>
    public sealed class AbilityBookPanel : UiPanel
    {
        public override string Id => "abilities";
        public override string Title => "Occultism abilities";
        public override string Icon => "book";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 3;
        public override Key Hotkey => Key.A;

        private ScrollView _list;
        private Label _level;
        private int _builtFor = -1;
        private readonly List<(string Id, VisualElement Slot, Label Level)> _slots = new List<(string, VisualElement, Label)>();

        protected override void Build(VisualElement root)
        {
            VisualElement top = UiKit.El(root, "row");
            _level = UiKit.Text(top, "", "muted", "small");
            UiKit.El(top, "spacer");
            UiKit.Button(top, "Guide", () => Ui.OpenModal<GuideModal>("abilities"));
            _list = UiKit.Scroll(root);
            Label hint = UiKit.Text(root, "Drag an ability onto an action bar.", "muted", "small");
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        public override void Refresh()
        {
            if (Session.Content == null) return;
            if (Session.Content.Abilities.Count != _builtFor) Rebuild();
            _level.text = "Occultism level " + Session.Level("occultism");
            foreach ((string id, VisualElement slot, Label level) in _slots)
            {
                AbilityDef a = Session.Ability(id);
                if (a == null) continue;
                int needed = LevelNeeded(a);
                bool locked = needed > Session.Level(SkillOf(a));
                slot.EnableInClassList("slot--locked", locked);
                level.text = locked ? needed.ToString() : "";
            }
        }

        public static int LevelNeeded(AbilityDef a)
        {
            int level = 1;
            foreach (SkillRequirement r in a.Requirements) level = Mathf.Max(level, r.Level);
            return level;
        }

        public static string SkillOf(AbilityDef a)
        {
            foreach (SkillRequirement r in a.Requirements) return r.Skill;
            return "occultism";
        }

        private void Rebuild()
        {
            _builtFor = Session.Content.Abilities.Count;
            _list.Clear();
            _slots.Clear();
            foreach ((AbilityType type, string title) in new[] { (AbilityType.Builder, "Builders"), (AbilityType.Spender, "Spenders"), (AbilityType.Ultimate, "Ultimates") })
            {
                UiKit.Heading(_list, title);
                VisualElement grid = UiKit.El(_list, "slot-grid");
                foreach (AbilityDef ability in Session.Content.Abilities)
                {
                    if (ability.Type != type) continue;
                    string id = ability.Id;
                    VisualElement slot = UiKit.El(grid, "slot");
                    VisualElement icon = UiKit.El(slot, "slot__icon");
                    icon.pickingMode = PickingMode.Ignore;
                    UiKit.SetIcon(icon, IconLibrary.ForAbility(ability), IconLibrary.AbilityTint(ability));
                    Label level = UiKit.Text(slot, "", "slot__level");
                    level.pickingMode = PickingMode.Ignore;

                    Ui.DragSource(slot, () =>
                    {
                        AbilityDef a = Session.Ability(id);
                        return new UiRoot.Drag { Data = new AbilityDrag { AbilityId = id }, Icon = IconLibrary.ForAbility(a), Tint = IconLibrary.AbilityTint(a) };
                    }, () => Session.UseAbility(id));
                    UiKit.Tooltip(slot, () =>
                    {
                        AbilityDef a = Session.Ability(id);
                        return a == null ? null : GameText.Describe(a, Session.TickSeconds, Session.AbilityProblem(a));
                    });
                    _slots.Add((id, slot, level));
                }
            }
        }
    }
}
