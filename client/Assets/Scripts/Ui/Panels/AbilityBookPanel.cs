using System.Collections.Generic;
using System.Linq;
using Rth.Client.Ui.Modals;
using Rth.Shared.Combat;
using Rth.Shared.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// A combat style's abilities as a book of icons: builders, spenders and ultimates, with a
    /// quiet tab per style (it opens on the style of the weapon in hand) and the guide's link on
    /// the same row. Locked ones are dimmed with the level they need (the style's level itself is
    /// in the skills panel). Click to use, drag onto an action bar to place it, hover for the full
    /// card; one line at the bottom says so. Cruor's verses are in the Religion panel, not here.
    /// </summary>
    public sealed class AbilityBookPanel : UiPanel
    {
        public override string Id => "abilities";
        public override string Title => "Abilities";
        public override string Icon => "book";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 3;
        public override Key Hotkey => Key.A;
        // The list scrolls by itself, so the hint stays pinned to the bottom of the window.
        public override bool Scrolls => false;

        private UiKit.SubTabs _tabs;
        private ScrollView _list;
        private int _builtFor = -1;
        private CombatStyle _style = CombatStyle.None;
        private CombatStyle _builtStyle = CombatStyle.None;
        private CombatStyle _weaponStyle = CombatStyle.None;
        private readonly List<(string Id, VisualElement Slot, Label Level)> _slots = new List<(string, VisualElement, Label)>();

        protected override void Build(VisualElement root)
        {
            _tabs = new UiKit.SubTabs(root);
            _tabs.Link("guide", "Ability guide", () => Ui.OpenModal<GuideModal>("abilities"));
            _list = UiKit.Scroll(root);
            _list.style.flexShrink = 1;
            UiKit.Text(root, "Drag an ability onto an action bar.", "panel-hint");
        }

        public override void Refresh()
        {
            if (Session.Content == null) return;
            // Follow the weapon: picking up a sword turns the book to melee.
            CombatStyle weapon = Session.WeaponStyle;
            if (weapon != _weaponStyle)
            {
                _weaponStyle = weapon;
                _style = weapon;
            }
            if (Session.Content.Abilities.Count != _builtFor || _style != _builtStyle) Rebuild();

            _tabs.Set(_style.ToString());
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
            return SkillOfStyle(a.Style);
        }

        /// <summary>The skill a style trains (skills.json's damage XP ids).</summary>
        public static string SkillOfStyle(CombatStyle style) => style switch
        {
            CombatStyle.Melee => "melee",
            CombatStyle.Ranged => "ranged",
            _ => "occultism",
        };

        private static bool InBook(AbilityDef a) => a.Type == AbilityType.Builder || a.Type == AbilityType.Spender || a.Type == AbilityType.Ultimate;

        private void Rebuild()
        {
            _builtFor = Session.Content.Abilities.Count;
            List<CombatStyle> styles = Session.Content.Abilities.Where(InBook).Select(a => a.Style).Distinct().OrderBy(s => s).ToList();
            if (!styles.Contains(_style) && styles.Count > 0) _style = styles[0];
            _builtStyle = _style;

            _tabs.Clear();
            foreach (CombatStyle style in styles)
            {
                CombatStyle s = style;
                _tabs.Add(s.ToString(), GameText.StyleName(s), () => _style = s);
            }

            _list.Clear();
            _slots.Clear();
            foreach ((AbilityType type, string title) in new[] { (AbilityType.Builder, "Builders"), (AbilityType.Spender, "Spenders"), (AbilityType.Ultimate, "Ultimates") })
            {
                UiKit.Heading(_list, title);
                VisualElement grid = UiKit.El(_list, "slot-grid");
                foreach (AbilityDef ability in Session.Content.Abilities)
                {
                    if (ability.Type != type || ability.Style != _style) continue;
                    _slots.Add(AbilitySlot(Ui, Session, grid, ability));
                }
            }
        }

        /// <summary>One ability as a draggable, clickable slot with its tooltip (also used for verses).</summary>
        public static (string, VisualElement, Label) AbilitySlot(UiRoot ui, GameSession session, VisualElement grid, AbilityDef ability)
        {
            string id = ability.Id;
            VisualElement slot = UiKit.Slots.Make(grid, out VisualElement icon);
            UiKit.SetIcon(icon, IconLibrary.ForAbility(ability), IconLibrary.AbilityTint(ability));
            Label level = UiKit.Text(slot, "", "slot__level");
            level.pickingMode = PickingMode.Ignore;

            ui.DragSource(slot, () =>
            {
                if (ui.Locked) return null; // a locked interface: nothing is dragged out of the book
                AbilityDef a = session.Ability(id);
                return new UiRoot.Drag { Data = new AbilityDrag { AbilityId = id }, Icon = IconLibrary.ForAbility(a), Tint = IconLibrary.AbilityTint(a) };
            }, () => session.UseAbility(id));
            UiKit.Tooltip(slot, () =>
            {
                AbilityDef a = session.Ability(id);
                return a == null ? null : GameText.Describe(a, session.TickSeconds, session.AbilityProblem(a), session.ItemName);
            });
            return (id, slot, level);
        }
    }
}
