using System.Collections.Generic;
using Rth.Client.Ui.Modals;
using Rth.Shared.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// Every skill at a glance, as tiles of icon and level, three across, with the combat and
    /// total levels on a footer row. A thin bar under each shows progress to the next level. Click a skill for
    /// its guide (what it unlocks); right-click to set a level or XP target, shown in gold and in
    /// the XP tracker.
    /// </summary>
    public sealed class SkillsPanel : UiPanel
    {
        private const float TileWidth = 86f;
        private const float TileHeight = 38f;

        public override string Id => "skills";
        public override string Title => "Skills";
        public override string Icon => "skills";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 5;
        public override Key Hotkey => Key.K;
        public override Vector2 MinSize => new Vector2(2 * (TileWidth + 4f) + 24f, 160f);

        private VisualElement _grid;
        private Label _total;
        private Label _combat;
        private int _builtFor = -1;
        private readonly List<Tile> _tiles = new List<Tile>();

        private sealed class Tile
        {
            public string Skill;
            public Label Level;
            public VisualElement Progress;
            public VisualElement Target;
        }

        protected override void Build(VisualElement root)
        {
            _grid = UiKit.El(root, "slot-grid");
            _grid.style.width = 3 * (TileWidth + 4f);
            _grid.style.alignSelf = Align.Center;
            // As many columns of tiles as the window is wide.
            root.RegisterCallback<GeometryChangedEvent>(_ =>
                _grid.style.width = Mathf.Max(1, Mathf.FloorToInt(root.contentRect.width / (TileWidth + 4f))) * (TileWidth + 4f));
            UiKit.El(root, "spacer");
            VisualElement total = UiKit.El(root, "footer-row");
            UiKit.Text(total, "Combat", "heading").style.marginTop = total.style.marginBottom = 0f;
            _combat = UiKit.Text(total, "", "value", "bold");
            _combat.style.marginLeft = 6f;
            UiKit.Tooltip(_combat, () => UiKit.Title("Combat level") + "\nYour best of melee, ranged and occultism, with defence and half of religion.");
            UiKit.El(total, "spacer");
            UiKit.Text(total, "Total level", "heading").style.marginTop = 0f;
            _total = UiKit.Text(total, "", "value", "bold");
            _total.style.marginLeft = 6f;
        }

        public override void Refresh()
        {
            var content = Session.Content;
            if (content == null) return;
            if (content.Skills.Count != _builtFor) Rebuild();

            int total = 0;
            foreach (Tile tile in _tiles)
            {
                Session.SkillXp.TryGetValue(tile.Skill, out long xp);
                int cap = content.XpCurve.MaxFor(Session.Skill(tile.Skill));
                int level = Session.Level(tile.Skill);
                total += level;
                long from = content.XpCurve.XpForLevel(level, cap);
                long to = content.XpCurve.XpForLevel(level + 1, cap);
                tile.Level.text = level.ToString();
                tile.Progress.style.width = Length.Percent(to > from ? (xp - from) * 100f / (to - from) : 100f);

                // The target bar runs from the start of this level to the goal.
                long goal = SkillTargets.GoalXp(Session, tile.Skill);
                tile.Target.style.display = goal > xp ? DisplayStyle.Flex : DisplayStyle.None;
                if (goal > xp) tile.Target.style.width = Length.Percent(goal > from ? (xp - from) * 100f / (goal - from) : 100f);
            }
            _total.text = total.ToString();
            _combat.text = Session.CombatLevel.ToString();
        }

        private void Rebuild()
        {
            _builtFor = Session.Content.Skills.Count;
            _grid.Clear();
            _tiles.Clear();
            foreach (SkillDef skill in Session.Content.Skills)
            {
                string id = skill.Id;
                VisualElement tile = UiKit.El(_grid, "card", "row");
                tile.style.width = TileWidth;
                tile.style.height = TileHeight;
                tile.style.marginRight = tile.style.marginBottom = 4f;
                tile.style.paddingTop = tile.style.paddingBottom = 2f;
                tile.style.paddingLeft = 6f;
                UiKit.Icon(tile, IconLibrary.Get(skill.Icon), 24f).style.unityBackgroundImageTintColor = UiKit.IconTint;
                Label level = UiKit.Text(tile, "1", "value", "bold");
                level.style.flexGrow = 1;
                level.style.fontSize = 17;
                level.style.unityTextAlign = TextAnchor.MiddleRight;
                level.pickingMode = PickingMode.Ignore;

                VisualElement track = UiKit.El(tile);
                track.pickingMode = PickingMode.Ignore;
                track.style.position = Position.Absolute;
                track.style.left = 6f;
                track.style.right = 6f;
                track.style.bottom = 3f;
                track.style.height = 3f;
                track.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
                VisualElement progress = UiKit.El(track);
                progress.style.height = Length.Percent(100f);
                progress.style.backgroundColor = UiKit.Good;
                VisualElement target = UiKit.El(tile);
                target.pickingMode = PickingMode.Ignore;
                target.style.position = Position.Absolute;
                target.style.left = 6f;
                target.style.top = 2f;
                target.style.height = 2f;
                target.style.backgroundColor = UiKit.Value;

                tile.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button == 0) Ui.OpenModal<GuideModal>("skills", id);
                    else if (e.button == 1) Ui.Menu(e.position, skill.Name, TargetOptions(skill));
                });
                UiKit.Tooltip(tile, () => Describe(skill));
                _tiles.Add(new Tile { Skill = id, Level = level, Progress = progress, Target = target });
            }
        }

        private List<(string Label, System.Action Action)> TargetOptions(SkillDef skill)
        {
            string id = skill.Id;
            int max = Session.Content.XpCurve.MaxFor(skill);
            var options = new List<(string Label, System.Action Action)>
            {
                ("Set level target", () => Ui.PromptNumber("Target level for " + skill.Name + "?", n =>
                {
                    if (n > Session.Level(id) && n <= max) SkillTargets.Set(Session, id, new SkillTargets.Target { IsLevel = true, Value = n });
                    else Session.Notify("Choose a level above your current one, up to " + max + ".");
                })),
                ("Set exp target", () => Ui.PromptNumber("Target XP for " + skill.Name + "?", n =>
                {
                    Session.SkillXp.TryGetValue(id, out long xp);
                    if (n > xp && n <= XpCurve.MaxXp) SkillTargets.Set(Session, id, new SkillTargets.Target { IsLevel = false, Value = n });
                    else Session.Notify("Choose an amount above the XP you have, up to " + XpCurve.MaxXp.ToString("N0") + ".");
                })),
            };
            if (SkillTargets.Get(Session, id) != null) options.Add(("Clear target", () => SkillTargets.Set(Session, id, null)));
            return options;
        }

        private string Describe(SkillDef skill)
        {
            var curve = Session.Content.XpCurve;
            Session.SkillXp.TryGetValue(skill.Id, out long xp);
            int cap = curve.MaxFor(skill);
            int level = Session.Level(skill.Id);
            var lines = new List<string> { UiKit.Title(skill.Name + " " + level) };
            if (!string.IsNullOrEmpty(skill.Description)) lines.Add(skill.Description);
            lines.Add("");
            lines.Add("XP: " + UiKit.Colour(xp.ToString("N0"), UiKit.Value));
            if (level < cap) lines.Add("Next level: " + UiKit.Colour((curve.XpForLevel(level + 1, cap) - xp).ToString("N0") + " to go", UiKit.Value));
            SkillTargets.Target target = SkillTargets.Get(Session, skill.Id);
            long goal = SkillTargets.GoalXp(Session, skill.Id);
            if (target != null && goal > xp) lines.Add("Target (" + SkillTargets.Describe(target) + "): " + UiKit.Colour((goal - xp).ToString("N0") + " XP to go", UiKit.Value));
            else if (target != null) lines.Add("Target (" + SkillTargets.Describe(target) + "): " + UiKit.Colour("reached", UiKit.Good));
            lines.Add(UiKit.Colour("Click for the skill guide. Right-click to set a target.", UiKit.Muted));
            return string.Join("\n", lines);
        }
    }
}
