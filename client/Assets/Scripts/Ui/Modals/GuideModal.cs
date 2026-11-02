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
    /// Each skill's guide, master-detail: every skill in a list on the left (icon, name, your
    /// level), and on the right the chosen skill's unlocks as one list in level order, each row an
    /// icon, the level, the name and a small badge for its kind. What you have reached is drawn
    /// plainly and what is still ahead dimmed, so no banner is needed to say where you stand.
    /// Unlocks are gathered from the content itself (abilities, equipment, tools, recipes and
    /// gathering that need the skill), so the guide is always as complete as the game.
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
            /// <summary>The badge's word ("Ability", "Tool"...); its colour comes from the kind.</summary>
            public string Badge;
            public Texture2D Icon;
            public Color Tint;
            public string Text;
            public string Tooltip;
        }

        private MasterDetail _view;

        protected override void Build(VisualElement root) => _view = new MasterDetail(root, Draw, 210f);

        public override void OnShow(string focus)
        {
            if (Session.Content == null) return;
            // Rebuilt each time it opens, so the levels in the list are current.
            _view.SetEntries(Session.Content.Skills.Select(s => new MasterDetail.Entry
            {
                Id = s.Id,
                Text = s.Name,
                Icon = s.Icon,
                Note = Session.Level(s.Id).ToString(),
            }).ToList());
            if (focus != null && Session.Content.Skills.Any(s => s.Id == focus)) _view.Select(focus);
        }

        private void Draw(string skillId)
        {
            SkillDef skill = Session.Content?.Skills.FirstOrDefault(s => s.Id == skillId);
            if (skill == null) return;
            int level = Session.Level(skill.Id);
            VisualElement d = _view.Detail;

            VisualElement head = UiKit.El(d, "row", "guide-head");
            UiKit.Icon(head, IconLibrary.Get(skill.Icon), 26f).style.unityBackgroundImageTintColor = UiKit.IconTint;
            UiKit.Text(head, skill.Name.ToUpperInvariant(), "title-large", "guide-head__name");
            if (!string.IsNullOrEmpty(skill.Description)) UiKit.Text(d, skill.Description, "muted", "small", "prose", "guide-description");

            List<Unlock> unlocks = UnlocksFor(skill.Id).OrderBy(u => u.Level).ThenBy(u => u.Text).ToList();
            VisualElement list = UiKit.El(d, "well", "guide-list");
            if (unlocks.Count == 0) UiKit.Text(list, "Nothing needs this skill yet.", "muted", "guide-list__empty");
            foreach (Unlock u in unlocks)
            {
                VisualElement row = UiKit.El(list, "row", "guide-unlock");
                row.EnableInClassList("guide-unlock--locked", level < u.Level);
                VisualElement slot = UiKit.El(row, "slot", "guide-unlock__slot");
                VisualElement icon = UiKit.El(slot, "slot__icon");
                icon.style.width = icon.style.height = 22f;
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
            {
                Rth.Shared.World.NodeDef node = obj.Node;
                if (node == null || node.Skill != skill) continue;
                Texture2D skillIcon = IconLibrary.Get(content.Skills.FirstOrDefault(s => s.Id == skill)?.Icon ?? skill);
                string how = node.Tool == null ? ""
                    : node.ToolOptional ? "\nA " + node.Tool + " helps."
                    : "\nNeeds " + (node.ToolTier > 1 ? "a tier-" + node.ToolTier + " " : "a ") + node.Tool + ".";
                if (node.Trap != null) how += "\nSet a trap, leave it, and come back to empty it.";
                if (node.Roam != null) how += "\nThe spot moves now and then.";
                if (node.Catches.Count > 0)
                {
                    // One line for each thing the node gives by level (perch at 1, pike at 20).
                    foreach (Rth.Shared.World.NodeCatch c in node.Catches)
                    {
                        ItemDef item = Session.Item(c.Item);
                        unlocks.Add(new Unlock
                        {
                            Level = Mathf.Max(c.Level, node.Level), Kind = Kind.Gathering, Badge = "Gathering",
                            Icon = item != null ? IconLibrary.ForItem(item) : skillIcon, Tint = item != null ? IconLibrary.ItemTint(item) : UiKit.IconTint,
                            Text = Capitalise(node.Verb) + " " + (item?.Name.ToLowerInvariant() ?? c.Item) + " at the " + obj.Name.ToLowerInvariant(),
                            Tooltip = UiKit.Title(obj.Name) + how + "\n" + UiKit.Colour(c.Xp.ToString("0.#") + " XP each", UiKit.Value),
                        });
                    }
                }
                else
                    unlocks.Add(new Unlock
                    {
                        Level = node.Level, Kind = Kind.Gathering, Badge = "Gathering", Icon = skillIcon, Tint = UiKit.IconTint,
                        Text = Capitalise(node.Verb) + " " + obj.Name.ToLowerInvariant(),
                        Tooltip = UiKit.Title(obj.Name) + how + "\n" + UiKit.Colour(node.Xp.ToString("0.#") + " XP each", UiKit.Value),
                    });
                if (node.Tending != null)
                    unlocks.Add(new Unlock
                    {
                        Level = node.Level, Kind = Kind.Gathering, Badge = "Tending", Icon = skillIcon, Tint = UiKit.IconTint,
                        Text = "Tend the " + obj.Name.ToLowerInvariant(),
                        Tooltip = UiKit.Title("Tending") + "\nNow and then one " + obj.Name.ToLowerInvariant() + " of a stand needs its foot cleared. Tend it, and for a while it gives more and is cut back less, for everyone.\n"
                            + UiKit.Colour(node.Tending.Xp.ToString("0.#") + " XP", UiKit.Value),
                    });
            }
            foreach (RecipeInfo recipe in content.Recipes)
                if (recipe.Skill == skill)
                {
                    ItemDef input = recipe.Inputs.Count > 0 ? Session.Item(recipe.Inputs[0].Item) : null;
                    string station = string.IsNullOrEmpty(recipe.Station) ? null : content.Objects.FirstOrDefault(o => o.Id == recipe.Station)?.Name ?? recipe.Station;
                    unlocks.Add(new Unlock
                    {
                        Level = recipe.Level,
                        Kind = Kind.Recipes,
                        Badge = "Recipe",
                        Icon = input != null ? IconLibrary.ForItem(input) : IconLibrary.Get("pouch"),
                        Tint = input != null ? IconLibrary.ItemTint(input) : UiKit.IconTint,
                        Text = Capitalise(recipe.Verb) + " " + recipe.Name,
                        Tooltip = UiKit.Title(Capitalise(recipe.Verb) + " " + recipe.Name) + (station != null ? "\nAt the " + station.ToLowerInvariant() + "." : "\nFrom your pack.") + "\n" + UiKit.Colour(recipe.Xp.ToString("0.#") + " XP each", UiKit.Value),
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
