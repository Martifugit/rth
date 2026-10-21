using System.Collections.Generic;
using Rth.Shared.Skills;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// An XP tracker: what you've gained this session, per skill and per hour, and how long to
    /// each skill's target at that rate. Tracks from the XP the server reports after logging in.
    /// </summary>
    public sealed class MetricsPanel : UiPanel
    {
        public override string Id => "metrics";
        public override string Title => "XP tracker";
        public override string Icon => "xp";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 8;

        private readonly Dictionary<string, long> _baseline = new Dictionary<string, long>();
        private readonly Dictionary<string, float> _firstGainTime = new Dictionary<string, float>();
        private readonly Dictionary<string, Row> _rows = new Dictionary<string, Row>();
        private float _sessionStart;
        private string _player;
        private Label _total;
        private Label _time;
        private VisualElement _list;
        private Label _empty;

        private sealed class Row
        {
            public VisualElement Root;
            public Label Gained;
            public Label Rate;
            public UiKit.Bar Target;
        }

        protected override void Build(VisualElement root)
        {
            VisualElement summary = UiKit.El(root, "card", "row");
            UiKit.Icon(summary, IconLibrary.Get("xp"), 30f).style.unityBackgroundImageTintColor = UiKit.IconTint;
            VisualElement text = UiKit.El(summary);
            text.style.marginLeft = 8f;
            _total = UiKit.Text(text, "", "value", "bold");
            _time = UiKit.Text(text, "", "muted", "small");
            UiKit.El(summary, "spacer");
            UiKit.Button(summary, "Reset", Reset);

            VisualElement well = UiKit.El(root, "well");
            well.style.flexGrow = 1;
            well.style.marginTop = 6f;
            ScrollView scroll = UiKit.Scroll(well);
            _list = scroll.contentContainer;
            _empty = UiKit.Text(scroll, "Gain some XP and it shows here.", "muted", "small");
        }

        public override void Observe()
        {
            if (Session.PlayerName != _player)
            {
                _player = Session.PlayerName;
                Reset();
            }
            foreach (KeyValuePair<string, long> s in Session.SkillXp)
            {
                if (!_baseline.ContainsKey(s.Key)) _baseline[s.Key] = s.Value;
                else if (s.Value > _baseline[s.Key] && !_firstGainTime.ContainsKey(s.Key)) _firstGainTime[s.Key] = Time.time;
            }
        }

        private void Reset()
        {
            _baseline.Clear();
            _firstGainTime.Clear();
            _sessionStart = Time.time;
            foreach (KeyValuePair<string, long> s in Session.SkillXp) _baseline[s.Key] = s.Value;
        }

        public override void Refresh()
        {
            var content = Session.Content;
            if (content == null) return;
            long totalGained = 0;
            foreach (SkillDef skill in content.Skills)
            {
                Session.SkillXp.TryGetValue(skill.Id, out long xp);
                long gained = _baseline.TryGetValue(skill.Id, out long start) ? xp - start : 0;
                Row row = RowFor(skill);
                row.Root.style.display = gained > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (gained <= 0) continue;
                totalGained += gained;

                float since = _firstGainTime.TryGetValue(skill.Id, out float t) ? t : _sessionStart;
                float hours = Mathf.Max((Time.time - since) / 3600f, 1f / 120f); // at least 30 s, so early rates aren't absurd
                float perHour = gained / hours;
                row.Gained.text = "+" + UiKit.FormatXp(gained);
                row.Rate.text = UiKit.FormatXp((long)perHour) + "/h";

                int level = Session.Level(skill.Id);
                SkillTargets.Target target = SkillTargets.Get(Session, skill.Id);
                long goal = SkillTargets.GoalXp(Session, skill.Id);
                row.Target.Root.style.display = target != null && goal > xp ? DisplayStyle.Flex : DisplayStyle.None;
                if (target != null && goal > xp)
                {
                    long from = content.XpCurve.XpForLevel(level, content.XpCurve.MaxFor(skill));
                    float hoursLeft = perHour > 0f ? (goal - xp) / perHour : float.PositiveInfinity;
                    row.Target.Set((xp - from) / (float)Mathf.Max(1, goal - from), SkillTargets.Describe(target) + " in " + FormatHours(hoursLeft));
                }
            }
            _empty.style.display = totalGained > 0 ? DisplayStyle.None : DisplayStyle.Flex;
            _total.text = "+" + UiKit.FormatXp(totalGained) + " XP";
            int minutes = Mathf.FloorToInt((Time.time - _sessionStart) / 60f);
            _time.text = "this session, " + (minutes >= 60 ? minutes / 60 + "h " + minutes % 60 + "m" : minutes + " min");
        }

        private Row RowFor(SkillDef skill)
        {
            if (_rows.TryGetValue(skill.Id, out Row row)) return row;
            row = new Row { Root = UiKit.El(_list) };
            row.Root.style.marginBottom = 6f;
            VisualElement line = UiKit.El(row.Root, "row");
            UiKit.Icon(line, IconLibrary.Get(skill.Icon), 18f).style.unityBackgroundImageTintColor = UiKit.IconTint;
            UiKit.Text(line, skill.Name).style.marginLeft = 4f;
            UiKit.El(line, "spacer");
            row.Gained = UiKit.Text(line, "", "value", "bold");
            row.Rate = UiKit.Text(line, "", "muted", "small");
            row.Rate.style.marginLeft = 6f;
            row.Target = new UiKit.Bar(row.Root, UiKit.Value, 14f);
            row.Target.Root.style.marginTop = 2f;
            _rows[skill.Id] = row;
            return row;
        }

        private static string FormatHours(float hours) =>
            float.IsInfinity(hours) ? "-" : hours < 1f ? Mathf.CeilToInt(hours * 60f) + " min" : hours.ToString("0.#") + " h";
    }
}
