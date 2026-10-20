using System.Collections.Generic;
using Rth.Shared.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// Every skill at a glance, with the total level. Hover for XP; right-click to set a level
    /// target, which shows as a bar under the skill and in Metrics.
    /// </summary>
    public sealed class SkillsPanel : UiPanel
    {
        public override string Id => "skills";
        public override string Title => "Skills";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 4;
        public override Key Hotkey => Key.K;

        private VisualElement _grid;
        private Label _total;
        private int _builtFor = -1;
        private readonly List<Cell> _cells = new List<Cell>();

        private sealed class Cell
        {
            public string Skill;
            public Label Level;
            public VisualElement Progress;
            public VisualElement Target;
        }

        protected override void Build(VisualElement root)
        {
            _grid = UiKit.El(root, "slot-grid");
            _total = UiKit.Text(root, "", "heading");
            _total.style.marginTop = 6f;
            _total.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        public override void Refresh()
        {
            var content = Session.Content;
            if (content == null) return;
            if (content.Skills.Count != _builtFor) Rebuild();

            int total = 0;
            foreach (Cell cell in _cells)
            {
                Session.SkillXp.TryGetValue(cell.Skill, out long xp);
                int level = content.XpCurve.LevelForXp(xp);
                total += level;
                long from = content.XpCurve.XpForLevel(level);
                long to = content.XpCurve.XpForLevel(level + 1);
                cell.Level.text = level.ToString();
                cell.Progress.style.width = Length.Percent(to > from ? (xp - from) * 100f / (to - from) : 100f);

                int target = SkillTargets.Get(Session.PlayerName, cell.Skill);
                cell.Target.style.display = target > level ? DisplayStyle.Flex : DisplayStyle.None;
                if (target > level)
                {
                    long start = content.XpCurve.XpForLevel(level);
                    long goal = content.XpCurve.XpForLevel(target);
                    cell.Target.style.width = Length.Percent(goal > start ? (xp - start) * 100f / (goal - start) : 100f);
                }
            }
            _total.text = "Total level: " + total;
        }

        private void Rebuild()
        {
            _builtFor = Session.Content.Skills.Count;
            _grid.Clear();
            _cells.Clear();
            foreach (SkillDef skill in Session.Content.Skills)
            {
                string id = skill.Id;
                VisualElement box = UiKit.El(_grid);
                box.style.width = 70f;
                box.style.height = 40f;
                box.style.marginLeft = box.style.marginRight = box.style.marginTop = box.style.marginBottom = 1f;
                box.style.paddingLeft = box.style.paddingRight = 3f;
                box.style.backgroundColor = new Color(0.17f, 0.15f, 0.12f);
                box.style.borderTopWidth = box.style.borderBottomWidth = box.style.borderLeftWidth = box.style.borderRightWidth = 1f;
                box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = new Color(0.1f, 0.09f, 0.07f);
                Label name = UiKit.Text(box, skill.Name);
                name.style.fontSize = 9;
                name.style.overflow = Overflow.Hidden;
                Label level = UiKit.Text(box, "1");
                level.style.fontSize = 15;
                level.style.color = new Color(1f, 1f, 0f);
                level.style.unityFontStyleAndWeight = FontStyle.Bold;
                level.style.unityTextAlign = TextAnchor.MiddleRight;
                VisualElement bar = UiKit.El(box);
                bar.style.position = Position.Absolute;
                bar.style.left = bar.style.right = 0f;
                bar.style.bottom = 0f;
                bar.style.height = 3f;
                bar.style.backgroundColor = new Color(0.1f, 0.09f, 0.07f);
                VisualElement progress = UiKit.El(bar);
                progress.style.height = Length.Percent(100f);
                progress.style.backgroundColor = new Color(0.35f, 0.6f, 0.3f);
                VisualElement target = UiKit.El(box);
                target.style.position = Position.Absolute;
                target.style.left = 0f;
                target.style.top = 0f;
                target.style.height = 2f;
                target.style.backgroundColor = new Color(1f, 0.6f, 0.1f);

                box.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button == 1) Ui.Menu(e.position, skill.Name, TargetOptions(id));
                });
                UiKit.Tooltip(box, () => Describe(skill));
                _cells.Add(new Cell { Skill = id, Level = level, Progress = progress, Target = target });
            }
        }

        private List<(string Label, System.Action Action)> TargetOptions(string skill)
        {
            Session.SkillXp.TryGetValue(skill, out long xp);
            int level = Session.Content.XpCurve.LevelForXp(xp);
            int max = Session.Content.XpCurve.MaxLevel;
            string player = Session.PlayerName;
            var options = new List<(string Label, System.Action Action)>();
            foreach (int step in new[] { 1, 5, 10 })
            {
                int goal = Mathf.Min(max, level + step);
                if (goal > level) options.Add(("Set target: level " + goal, () => SkillTargets.Set(player, skill, goal)));
            }
            if (SkillTargets.Get(player, skill) > 0) options.Add(("Clear target", () => SkillTargets.Set(player, skill, 0)));
            return options;
        }

        private string Describe(SkillDef skill)
        {
            var curve = Session.Content.XpCurve;
            Session.SkillXp.TryGetValue(skill.Id, out long xp);
            int level = curve.LevelForXp(xp);
            string text = skill.Name + " " + level + "\n" + xp.ToString("N0") + " xp";
            if (level < curve.MaxLevel) text += "\n" + (curve.XpForLevel(level + 1) - xp).ToString("N0") + " to level " + (level + 1);
            int target = SkillTargets.Get(Session.PlayerName, skill.Id);
            if (target > level) text += "\nTarget " + target + ": " + (curve.XpForLevel(target) - xp).ToString("N0") + " xp to go";
            if (!string.IsNullOrEmpty(skill.Description)) text += "\n\n" + skill.Description;
            return text + "\n\nRight-click to set a target.";
        }
    }
}
