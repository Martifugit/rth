using System.Collections.Generic;
using Rth.Shared.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// Every skill at a glance, as raised tiles of icon and level, three across, with a total-level
    /// tile at the end. A thin bar under each level shows progress to the next. Hover for XP;
    /// right-click to set a level target, shown as an orange bar and in Metrics.
    /// </summary>
    public sealed class SkillsPanel : UiPanel
    {
        private const float TileWidth = 74f;
        private const float TileHeight = 34f;

        public override string Id => "skills";
        public override string Title => "Skills";
        public override string Icon => "skills";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 4;
        public override Key Hotkey => Key.K;
        public override Vector2 MinSize => new Vector2(240f, 160f);

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
            _grid.style.width = 3 * (TileWidth + 2f);
            _grid.style.alignSelf = Align.Center;
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
                tile.Level.text = level + "/" + level;
                tile.Progress.style.width = Length.Percent(to > from ? (xp - from) * 100f / (to - from) : 100f);

                int target = SkillTargets.Get(Session.PlayerName, tile.Skill);
                tile.Target.style.display = target > level ? DisplayStyle.Flex : DisplayStyle.None;
                if (target > level)
                {
                    long goal = content.XpCurve.XpForLevel(target);
                    tile.Target.style.width = Length.Percent(goal > from ? (xp - from) * 100f / (goal - from) : 100f);
                }
            }
            _total.text = "Total level:\n" + total;
        }

        private VisualElement NewTile()
        {
            VisualElement tile = UiKit.El(_grid, "raised", "row");
            tile.style.width = TileWidth;
            tile.style.height = TileHeight;
            tile.style.marginRight = tile.style.marginBottom = 2f;
            tile.style.paddingLeft = 3f;
            return tile;
        }

        private void Rebuild()
        {
            _builtFor = Session.Content.Skills.Count;
            _grid.Clear();
            _tiles.Clear();
            foreach (SkillDef skill in Session.Content.Skills)
            {
                string id = skill.Id;
                VisualElement tile = NewTile();
                UiKit.Icon(tile, IconLibrary.Get(skill.Icon), 24f).pickingMode = PickingMode.Ignore;
                Label level = UiKit.Text(tile, "1/1", "value");
                level.pickingMode = PickingMode.Ignore;
                level.style.flexGrow = 1;
                level.style.unityTextAlign = TextAnchor.MiddleRight;
                level.style.paddingRight = 4f;

                VisualElement track = UiKit.El(tile);
                track.pickingMode = PickingMode.Ignore;
                track.style.position = Position.Absolute;
                track.style.left = 30f;
                track.style.right = 4f;
                track.style.bottom = 3f;
                track.style.height = 3f;
                track.style.backgroundColor = new Color(0.09f, 0.07f, 0.05f);
                VisualElement progress = UiKit.El(track);
                progress.style.height = Length.Percent(100f);
                progress.style.backgroundColor = new Color(0.3f, 0.75f, 0.25f);
                VisualElement target = UiKit.El(tile);
                target.pickingMode = PickingMode.Ignore;
                target.style.position = Position.Absolute;
                target.style.left = 0f;
                target.style.top = 0f;
                target.style.height = 2f;
                target.style.backgroundColor = UiKit.Heading;

                tile.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button == 1) Ui.Menu(e.position, skill.Name, TargetOptions(id));
                });
                UiKit.Tooltip(tile, () => Describe(skill));
                _tiles.Add(new Tile { Skill = id, Level = level, Progress = progress, Target = target });
            }

            VisualElement totalTile = NewTile();
            _total = UiKit.Text(totalTile, "", "value", "small");
            _total.style.flexGrow = 1;
            _total.style.unityTextAlign = TextAnchor.MiddleCenter;
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
            if (level < curve.MaxLevel) lines.Add("Next level at: " + UiKit.Colour(curve.XpForLevel(level + 1).ToString("N0"), UiKit.Value) + "\nRemaining: " + UiKit.Colour((curve.XpForLevel(level + 1) - xp).ToString("N0"), UiKit.Value));
            int target = SkillTargets.Get(Session.PlayerName, skill.Id);
            if (target > level) lines.Add("Target " + target + ": " + UiKit.Colour((curve.XpForLevel(target) - xp).ToString("N0") + " to go", UiKit.Heading));
            lines.Add(UiKit.Colour("Right-click to set a target.", UiKit.Muted));
            return string.Join("\n", lines);
        }
    }
}
