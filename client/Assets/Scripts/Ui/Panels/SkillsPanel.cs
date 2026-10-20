using System.Collections.Generic;
using Rth.Client.Ui.Modals;
using Rth.Shared.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// Every skill at a glance, as tiles of icon and level, three across, with the total level.
    /// A thin bar under each shows progress to the next level. Click a skill for its guide
    /// (what it unlocks); right-click to set a level target, shown in gold and in the XP tracker.
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
        public override Vector2 MinSize => new Vector2(280f, 160f);

        private VisualElement _grid;
        private Label _total;
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
            VisualElement total = UiKit.El(root, "card", "row");
            total.style.marginTop = 8f;
            UiKit.Text(total, "TOTAL LEVEL", "heading").style.marginTop = total.style.marginBottom = 0f;
            UiKit.El(total, "spacer");
            _total = UiKit.Text(total, "", "value", "bold");
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
                int level = content.XpCurve.LevelForXp(xp);
                total += level;
                long from = content.XpCurve.XpForLevel(level);
                long to = content.XpCurve.XpForLevel(level + 1);
                tile.Level.text = level.ToString();
                tile.Progress.style.width = Length.Percent(to > from ? (xp - from) * 100f / (to - from) : 100f);

                int target = SkillTargets.Get(Session.PlayerName, tile.Skill);
                tile.Target.style.display = target > level ? DisplayStyle.Flex : DisplayStyle.None;
                if (target > level)
                {
                    long goal = content.XpCurve.XpForLevel(target);
                    tile.Target.style.width = Length.Percent(goal > from ? (xp - from) * 100f / (goal - from) : 100f);
                }
            }
            _total.text = total.ToString();
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
                    else if (e.button == 1) Ui.Menu(e.position, skill.Name, TargetOptions(id));
                });
                UiKit.Tooltip(tile, () => Describe(skill));
                _tiles.Add(new Tile { Skill = id, Level = level, Progress = progress, Target = target });
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
            var lines = new List<string> { UiKit.Title(skill.Name + " " + level) };
            if (!string.IsNullOrEmpty(skill.Description)) lines.Add(skill.Description);
            lines.Add("");
            lines.Add("XP: " + UiKit.Colour(xp.ToString("N0"), UiKit.Value));
            if (level < curve.MaxLevel) lines.Add("Next level: " + UiKit.Colour((curve.XpForLevel(level + 1) - xp).ToString("N0") + " to go", UiKit.Value));
            int target = SkillTargets.Get(Session.PlayerName, skill.Id);
            if (target > level) lines.Add("Target " + target + ": " + UiKit.Colour((curve.XpForLevel(target) - xp).ToString("N0") + " to go", UiKit.Value));
            lines.Add(UiKit.Colour("Click for the skill guide. Right-click to set a target.", UiKit.Muted));
            return string.Join("\n", lines);
        }
    }
}
