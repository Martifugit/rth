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
    /// name and level above. On the right, what it unlocks level by level, filterable by kind
    /// and sortable. Unlocks are gathered from the content itself (abilities, equipment and
    /// recipes that need the skill), so the guide is always as complete as the game.
    /// </summary>
    public sealed class SkillGuideTab : ModalTab
    {
        public override string Id => "skills";
        public override string Title => "Skills";
        public override string Icon => "skills";

        private enum Kind
        {
            All,
            Abilities,
            Equipment,
            Recipes,
        }

        private struct Unlock
        {
            public int Level;
            public Kind Kind;
            public Texture2D Icon;
            public Color Tint;
            public string Text;
            public string Tooltip;
        }

        private Label _skillName;
        private Label _skillLevel;
        private VisualElement _picker;
        private Label _summary;
        private ScrollView _list;
        private readonly Dictionary<string, VisualElement> _pickerSlots = new Dictionary<string, VisualElement>();
        private readonly List<(Kind Kind, Label Button)> _filterButtons = new List<(Kind, Label)>();
        private Label _sortButton;
        private string _skill;
        private Kind _filter = Kind.All;
        private bool _descending;
        private int _builtFor = -1;

        protected override void Build(VisualElement root)
        {
            VisualElement columns = UiKit.El(root, "row");
            columns.style.flexGrow = 1;
            columns.style.alignItems = Align.Stretch;

            VisualElement left = UiKit.El(columns, "well");
            left.style.width = 168f;
            left.style.marginRight = 12f;
            left.style.alignItems = Align.Center;
            _skillName = UiKit.Text(left, "", "heading");
            _skillName.style.marginBottom = 0f;
            _skillLevel = UiKit.Text(left, "", "title-large");
            _skillLevel.style.marginBottom = 8f;
            _picker = UiKit.El(left, "slot-grid");
            _picker.style.width = 3 * 44f;

            VisualElement right = UiKit.El(columns);
            right.style.flexGrow = 1;
            VisualElement bar = UiKit.El(right, "row");
            bar.style.marginBottom = 6f;
            foreach (Kind k in new[] { Kind.All, Kind.Abilities, Kind.Equipment, Kind.Recipes })
            {
                Kind kind = k;
                Label button = UiKit.Text(bar, k.ToString().ToUpperInvariant(), "chat-tab");
                button.RegisterCallback<ClickEvent>(_ =>
                {
                    _filter = kind;
                    Redraw();
                });
                _filterButtons.Add((k, button));
            }
            UiKit.El(bar, "spacer");
            _sortButton = UiKit.Button(bar, "", () =>
            {
                _descending = !_descending;
                Redraw();
            });
            _summary = UiKit.Text(right, "", "muted", "small");
            _summary.style.marginBottom = 4f;
            VisualElement well = UiKit.El(right, "well");
            well.style.flexGrow = 1;
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
            foreach ((Kind k, Label button) in _filterButtons) button.EnableInClassList("chat-tab--on", k == _filter);
            _sortButton.text = _descending ? "Level: high - low" : "Level: low - high";

            XpCurve curve = Session.Content.XpCurve;
            Session.SkillXp.TryGetValue(skill.Id, out long xp);
            int level = curve.LevelForXp(xp);
            _skillName.text = skill.Name.ToUpperInvariant();
            _skillLevel.text = level.ToString();
            _summary.text = xp.ToString("N0") + " XP" + (level < curve.MaxLevel ? ",  " + (curve.XpForLevel(level + 1) - xp).ToString("N0") + " to level " + (level + 1) : "") +
                            (string.IsNullOrEmpty(skill.Description) ? "" : ".  " + skill.Description);

            List<Unlock> unlocks = UnlocksFor(skill.Id).Where(u => _filter == Kind.All || u.Kind == _filter).ToList();
            unlocks = (_descending ? unlocks.OrderByDescending(u => u.Level) : unlocks.OrderBy(u => u.Level)).ThenBy(u => u.Text).ToList();
            _list.Clear();
            if (unlocks.Count == 0) UiKit.Text(_list, "Nothing here needs this skill yet.", "muted").style.marginLeft = 6f;
            for (int i = 0; i < unlocks.Count; i++)
            {
                Unlock u = unlocks[i];
                bool reached = level >= u.Level;
                VisualElement row = UiKit.El(_list, "row");
                row.style.paddingTop = row.style.paddingBottom = 3f;
                row.style.paddingLeft = 6f;
                row.style.backgroundColor = i % 2 == 0 ? new Color(1f, 1f, 1f, 0.035f) : new Color(0f, 0f, 0f, 0f);
                VisualElement slot = UiKit.El(row, "slot");
                slot.style.width = slot.style.height = 34f;
                VisualElement icon = UiKit.El(slot, "slot__icon");
                icon.style.width = icon.style.height = 26f;
                UiKit.SetIcon(icon, u.Icon, u.Tint);
                icon.style.opacity = reached ? 1f : 0.4f;
                Label lvl = UiKit.Text(row, u.Level.ToString(), "bold");
                lvl.style.width = 34f;
                lvl.style.unityTextAlign = TextAnchor.MiddleCenter;
                lvl.style.color = reached ? UiKit.Good : UiKit.Muted;
                Label text = UiKit.Text(row, u.Text);
                text.style.flexGrow = 1;
                text.style.color = reached ? UiKit.TextColour : UiKit.Muted;
                Label kind = UiKit.Text(row, u.Kind.ToString().TrimEnd('s'), "muted", "small");
                kind.style.marginRight = 8f;
                string tip = u.Tooltip;
                if (tip != null) UiKit.Tooltip(row, () => tip);
            }
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
                            Level = r.Level, Kind = Kind.Abilities, Icon = IconLibrary.ForAbility(a), Tint = IconLibrary.AbilityTint(a),
                            Text = a.Name + "  (" + a.Type.ToString().ToLowerInvariant() + ")", Tooltip = GameText.Describe(a, Session.TickSeconds),
                        });
            foreach (ItemDef item in content.Items)
                if (item.Equipment != null)
                    foreach (SkillRequirement r in item.Equipment.Requirements)
                        if (r.Skill == skill)
                            unlocks.Add(new Unlock
                            {
                                Level = r.Level, Kind = Kind.Equipment, Icon = IconLibrary.ForItem(item), Tint = IconLibrary.ItemTint(item),
                                Text = item.Name, Tooltip = GameText.Describe(item, 1),
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
                .Select(s => new MasterDetail.Entry { Id = s.ToString(), Text = s == CombatStyle.Occult ? "Occultism" : s.ToString(), Icon = "book" }).ToList();
            _view.SetEntries(styles, "Combat styles");
        }

        private void Draw(string styleName)
        {
            VisualElement d = _view.Detail;
            UiKit.Text(d, (styleName == CombatStyle.Occult.ToString() ? "Occultism" : styleName).ToUpperInvariant(), "title-large");
            UiKit.Text(d, "Builders build phlegm; spenders and ultimates spend it. Many push bile one way or the other.", "muted", "prose");
            UiKit.Divider(d);
            foreach (AbilityType type in new[] { AbilityType.Builder, AbilityType.Spender, AbilityType.Ultimate })
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
                    Label described = UiKit.Text(text, GameText.Describe(a, Session.TickSeconds, null), "prose", "small");
                    described.style.fontSize = 13;
                }
            }
        }
    }
}
