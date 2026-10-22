using System.Collections.Generic;
using System.Linq;
using Rth.Client.Ui.Panels;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Modals
{
    /// <summary>The guide: what each skill unlocks, and every ability in detail (G).</summary>
    public sealed class GuideModal : UiModal
    {
        public override string Id => "guide";
        public override string Title => "Guide";
        public override string Icon => "guide";
        public override Key Hotkey => Key.G;

        protected override IEnumerable<ModalTab> CreateTabs() => new ModalTab[] { new SkillGuideTab(), new AbilityGuideTab() };
    }

    /// <summary>
    /// Each skill's guide. On the left, every skill as an icon to pick, with the chosen skill's
    /// name and level above. On the right, what it unlocks as one list in level order: each row
    /// an icon, the level, the name and a small badge for its kind. A line marks your level, so
    /// what you have and what comes next read at a glance. Chips above the list narrow it to one
    /// kind (only the kinds this skill has, with counts; click again for all). Unlocks are
    /// gathered from the content itself (abilities, equipment, tools, recipes and gathering that
    /// need the skill), so the guide is always as complete as the game.
    /// </summary>
    public sealed class SkillGuideTab : ModalTab
    {
        public override string Id => "skills";
        public override string Title => "Skills";
        public override string Icon => "skills";

        private enum Kind
        {
            Abilities,
            Equipment,
            Recipes,
            Gathering,
        }

        private struct Unlock
        {
            public int Level;
            public Kind Kind;
            /// <summary>The badge's word ("Ability", "Tool"...) and its style modifier.</summary>
            public string Badge;
            public Texture2D Icon;
            public Color Tint;
            public string Text;
            public string Tooltip;
        }

        private Label _skillName;
        private Label _skillLevel;
        private VisualElement _picker;
        private Label _summary;
        private VisualElement _chips;
        private ScrollView _list;
        private readonly Dictionary<string, VisualElement> _pickerSlots = new Dictionary<string, VisualElement>();
        private string _skill;
        private Kind? _filter;
        private int _builtFor = -1;

        protected override void Build(VisualElement root)
        {
            VisualElement columns = UiKit.El(root, "row");
            columns.style.flexGrow = 1;
            columns.style.alignItems = Align.Stretch;

            VisualElement left = UiKit.El(columns, "well");
            left.style.width = 196f;
            left.style.marginRight = 12f;
            left.style.alignItems = Align.Center;
            // Name and level on one line.
            VisualElement title = UiKit.El(left, "row");
            title.style.marginBottom = 8f;
            title.style.alignSelf = Align.Stretch;
            title.style.justifyContent = Justify.Center;
            _skillName = UiKit.Text(title, "", "heading", "nowrap");
            _skillName.style.marginTop = _skillName.style.marginBottom = 0f;
            _skillName.style.flexShrink = 0;
            _skillName.style.letterSpacing = 0f; // Unity measures text without it, which cut long names short
            _skillLevel = UiKit.Text(title, "", "title-large", "nowrap");
            _skillLevel.style.marginLeft = 8f;
            _skillLevel.style.flexShrink = 0;
            _picker = UiKit.El(left, "slot-grid");
            _picker.style.width = 3 * 44f;

            VisualElement right = UiKit.El(columns);
            right.style.flexGrow = 1;
            _summary = UiKit.Text(right, "", "muted", "small", "prose");
            _summary.style.marginBottom = 6f;
            _chips = UiKit.El(right, "guide-chips");
            VisualElement well = UiKit.El(right, "well");
            well.style.flexGrow = 1;
            // The rows run edge to edge inside the frame.
            well.style.paddingLeft = well.style.paddingRight = well.style.paddingTop = well.style.paddingBottom = 0f;
            well.style.overflow = Overflow.Hidden;
            _list = UiKit.Scroll(well);
        }

        public override void OnShow(string focus)
        {
            if (Session.Content == null) return;
            if (_builtFor != Session.Content.Skills.Count) BuildPicker();
            if (focus != null && Session.Content.Skills.Any(s => s.Id == focus)) _skill = focus;
            if (_skill == null && Session.Content.Skills.Count > 0) _skill = Session.Content.Skills[0].Id;
            Redraw();
        }

        private void BuildPicker()
        {
            _builtFor = Session.Content.Skills.Count;
            _picker.Clear();
            _pickerSlots.Clear();
            foreach (SkillDef skill in Session.Content.Skills)
            {
                string id = skill.Id;
                VisualElement slot = UiKit.El(_picker, "slot");
                VisualElement icon = UiKit.El(slot, "slot__icon");
                icon.pickingMode = PickingMode.Ignore;
                UiKit.SetIcon(icon, IconLibrary.Get(skill.Icon), UiKit.IconTint);
                slot.RegisterCallback<ClickEvent>(_ =>
                {
                    if (_skill != id) _filter = null;
                    _skill = id;
                    Redraw();
                });
                UiKit.Tooltip(slot, () => skill.Name + " " + Session.Level(id));
                _pickerSlots[id] = slot;
            }
        }

        private void Redraw()
        {
            SkillDef skill = Session.Content?.Skills.FirstOrDefault(s => s.Id == _skill);
            if (skill == null) return;
            foreach (KeyValuePair<string, VisualElement> slot in _pickerSlots) slot.Value.EnableInClassList("slot--on", slot.Key == _skill);

            XpCurve curve = Session.Content.XpCurve;
            Session.SkillXp.TryGetValue(skill.Id, out long xp);
            int cap = curve.MaxFor(skill);
            int level = Session.Level(skill.Id);
            _skillName.text = skill.Name;
            _skillLevel.text = level.ToString();
            _summary.text = xp.ToString("N0") + " XP" + (level < cap ? ",  " + (curve.XpForLevel(level + 1, cap) - xp).ToString("N0") + " to level " + (level + 1) : "") +
                            (string.IsNullOrEmpty(skill.Description) ? "" : ".  " + skill.Description);

            List<Unlock> all = UnlocksFor(skill.Id);
            DrawChips(all);
            List<Unlock> unlocks = all.Where(u => _filter == null || u.Kind == _filter).OrderBy(u => u.Level).ThenBy(u => u.Text).ToList();
            _list.Clear();
            if (unlocks.Count == 0) UiKit.Text(_list, "Nothing needs this skill yet.", "muted").style.marginLeft = 6f;
            bool marked = false;
            for (int i = 0; i < unlocks.Count; i++)
            {
                Unlock u = unlocks[i];
                bool reached = level >= u.Level;
                // A line at your level, between what you have and what comes next.
                if (!reached && !marked)
                {
                    marked = true;
                    if (i > 0) YourLevel(level);
                }
                VisualElement row = UiKit.El(_list, "row", "guide-unlock");
                row.EnableInClassList("guide-unlock--locked", !reached);
                VisualElement slot = UiKit.El(row, "slot", "guide-unlock__slot");
                VisualElement icon = UiKit.El(slot, "slot__icon");
                icon.style.width = icon.style.height = 24f;
                UiKit.SetIcon(icon, u.Icon, u.Tint);
                icon.pickingMode = PickingMode.Ignore;
                UiKit.Text(row, u.Level.ToString(), "guide-unlock__level");
                Label text = UiKit.Text(row, u.Text, "guide-unlock__name");
                text.style.flexGrow = 1;
                text.style.flexShrink = 1;
                UiKit.Text(row, u.Badge, "guide-badge", "guide-badge--" + u.Kind.ToString().ToLowerInvariant());
                string tip = u.Tooltip;
                if (tip != null) UiKit.Tooltip(row, () => tip);
            }
        }

        /// <summary>The kinds this skill unlocks, as chips with counts; none when there is only one kind.</summary>
        private void DrawChips(List<Unlock> all)
        {
            _chips.Clear();
            List<IGrouping<Kind, Unlock>> kinds = all.GroupBy(u => u.Kind).OrderBy(g => g.Key).ToList();
            if (_filter != null && kinds.All(g => g.Key != _filter)) _filter = null;
            _chips.style.display = kinds.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            if (kinds.Count <= 1) return;
            Chip("All", all.Count, null);
            foreach (IGrouping<Kind, Unlock> g in kinds) Chip(g.Key.ToString(), g.Count(), g.Key);
        }

        private void Chip(string text, int count, Kind? kind)
        {
            VisualElement chip = UiKit.El(_chips, "guide-chip");
            chip.EnableInClassList("guide-chip--on", _filter == kind);
            UiKit.Text(chip, text, "guide-chip__text").pickingMode = PickingMode.Ignore;
            UiKit.Text(chip, count.ToString(), "guide-chip__count").pickingMode = PickingMode.Ignore;
            chip.RegisterCallback<ClickEvent>(_ =>
            {
                _filter = _filter == kind ? null : kind;
                Redraw();
            });
        }

        private void YourLevel(int level)
        {
            VisualElement line = UiKit.El(_list, "guide-yours");
            UiKit.El(line, "guide-yours__rule");
            UiKit.Text(line, "Your level: " + level, "guide-yours__text");
            UiKit.El(line, "guide-yours__rule");
        }

        private List<Unlock> UnlocksFor(string skill)
        {
            ClientContent content = Session.Content;
            var unlocks = new List<Unlock>();
            foreach (AbilityDef a in content.Abilities)
                foreach (SkillRequirement r in a.Requirements)
                    if (r.Skill == skill)
                        unlocks.Add(new Unlock
                        {
                            Level = r.Level, Kind = Kind.Abilities, Badge = a.Type.ToString(), Icon = IconLibrary.ForAbility(a), Tint = IconLibrary.AbilityTint(a),
                            Text = a.Name, Tooltip = GameText.Describe(a, Session.TickSeconds, null, Session.ItemName),
                        });
            foreach (ItemDef item in content.Items)
                if (item.Equipment != null)
                    foreach (SkillRequirement r in item.Equipment.Requirements)
                        if (r.Skill == skill)
                            unlocks.Add(new Unlock
                            {
                                Level = r.Level, Kind = Kind.Equipment, Badge = "Equipment", Icon = IconLibrary.ForItem(item), Tint = IconLibrary.ItemTint(item),
                                Text = item.Name, Tooltip = GameText.Describe(item, 1),
                            });
            foreach (ItemDef item in content.Items)
                if (item.Tool != null)
                    foreach (SkillRequirement r in item.Tool.Requirements)
                        if (r.Skill == skill)
                            unlocks.Add(new Unlock
                            {
                                Level = r.Level, Kind = Kind.Equipment, Badge = "Tool", Icon = IconLibrary.ForItem(item), Tint = IconLibrary.ItemTint(item),
                                Text = item.Name, Tooltip = GameText.Describe(item, 1),
                            });
            foreach (Rth.Shared.World.ObjectDef obj in content.Objects)
                if (obj.Node != null && obj.Node.Skill == skill)
                    unlocks.Add(new Unlock
                    {
                        Level = obj.Node.Level, Kind = Kind.Gathering, Badge = "Gathering", Icon = IconLibrary.Get(content.Skills.FirstOrDefault(s => s.Id == skill)?.Icon ?? skill), Tint = UiKit.IconTint,
                        Text = Capitalise(obj.Node.Verb) + " " + obj.Name.ToLowerInvariant(),
                        Tooltip = UiKit.Title(obj.Name) + (obj.Node.Tool != null ? "\nNeeds " + (obj.Node.ToolTier > 1 ? "a tier-" + obj.Node.ToolTier + " " : "a ") + obj.Node.Tool + "." : "")
                            + "\n" + UiKit.Colour(obj.Node.Xp.ToString("0.#") + " XP each", UiKit.Value),
                    });
            foreach (RecipeInfo recipe in content.Recipes)
                if (recipe.Skill == skill)
                {
                    ItemDef input = recipe.Inputs.Count > 0 ? Session.Item(recipe.Inputs[0].Item) : null;
                    string station = content.Objects.FirstOrDefault(o => o.Id == recipe.Station)?.Name ?? recipe.Station;
                    unlocks.Add(new Unlock
                    {
                        Level = recipe.Level,
                        Kind = Kind.Recipes,
                        Badge = "Recipe",
                        Icon = input != null ? IconLibrary.ForItem(input) : IconLibrary.Get("pouch"),
                        Tint = input != null ? IconLibrary.ItemTint(input) : UiKit.IconTint,
                        Text = Capitalise(recipe.Verb) + " " + recipe.Name,
                        Tooltip = UiKit.Title(Capitalise(recipe.Verb) + " " + recipe.Name) + "\nAt the " + station.ToLowerInvariant() + ".\n" + UiKit.Colour(recipe.Xp.ToString("0.#") + " XP each", UiKit.Value),
                    });
                }
            return unlocks;
        }

        private static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    /// <summary>Every ability of a style as a full card: what it does, what it costs, what it needs.</summary>
    public sealed class AbilityGuideTab : ModalTab
    {
        public override string Id => "abilities";
        public override string Title => "Abilities";
        public override string Icon => "book";

        private MasterDetail _view;

        protected override void Build(VisualElement root) => _view = new MasterDetail(root, Draw, 200f);

        public override void OnShow(string focus)
        {
            if (Session.Content == null) return;
            var styles = Session.Content.Abilities.Where(a => a.Type != AbilityType.Auto).Select(a => a.Style).Distinct()
                .Select(s => new MasterDetail.Entry { Id = s.ToString(), Text = GameText.StyleName(s), Icon = "book" }).ToList();
            _view.SetEntries(styles, "Combat styles");
        }

        private void Draw(string styleName)
        {
            VisualElement d = _view.Detail;
            UiKit.Text(d, (styleName == CombatStyle.Occult.ToString() ? "Occultism" : styleName).ToUpperInvariant(), "title-large");
            UiKit.Text(d, styleName == CombatStyle.Melee.ToString()
                ? "Builders build phlegm; spenders and ultimates spend it. Many hit harder the less blood you have. Cruor's verses (Religion) are read over scripture, paid for in blood."
                : "Builders build phlegm; spenders and ultimates spend it. Many push bile one way or the other.", "muted", "prose");
            UiKit.Divider(d);
            foreach (AbilityType type in new[] { AbilityType.Builder, AbilityType.Spender, AbilityType.Ultimate, AbilityType.Verse })
            {
                List<AbilityDef> list = Session.Content.Abilities.Where(a => a.Style.ToString() == styleName && a.Type == type).ToList();
                if (list.Count == 0) continue;
                UiKit.Heading(d, type + "s").style.marginTop = 8f;
                foreach (AbilityDef a in list)
                {
                    VisualElement card = UiKit.El(d, "card", "row");
                    card.style.alignItems = Align.FlexStart;
                    card.style.marginBottom = 4f;
                    VisualElement slot = UiKit.El(card, "slot");
                    VisualElement icon = UiKit.El(slot, "slot__icon");
                    UiKit.SetIcon(icon, IconLibrary.ForAbility(a), IconLibrary.AbilityTint(a));
                    VisualElement text = UiKit.El(card);
                    text.style.flexGrow = 1;
                    text.style.flexShrink = 1;
                    text.style.marginLeft = 10f;
                    Label described = UiKit.Text(text, GameText.Describe(a, Session.TickSeconds, null, Session.ItemName), "prose", "small");
                    described.style.fontSize = 13;
                }
            }
        }
    }
}
