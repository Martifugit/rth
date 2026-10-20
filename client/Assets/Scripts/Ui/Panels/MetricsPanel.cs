using System.Collections.Generic;
using Rth.Shared.Skills;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// XP gained this session, per skill and per hour, and time to each skill's target at the
    /// current rate. Tracks from the first XP the server reports after logging in.
    /// </summary>
    public sealed class MetricsPanel : UiPanel
    {
        public override string Id => "metrics";
        public override string Title => "Metrics";
        public override string Tab => "XP";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 7;

        private readonly Dictionary<string, long> _baseline = new Dictionary<string, long>();
        private readonly Dictionary<string, float> _firstGainTime = new Dictionary<string, float>();
        private float _sessionStart;
        private string _player;
        private Label _summary;
        private Label _lines;

        protected override void Build(VisualElement root)
        {
            _summary = UiKit.Text(root, "", "heading");
            var scroll = new ScrollView();
            scroll.AddToClassList("scroll");
            root.Add(scroll);
            _lines = UiKit.Text(scroll, "");
            _lines.style.whiteSpace = WhiteSpace.Normal;
            UiKit.Button(root, "Reset", Reset);
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
            var lines = new List<string>();
            foreach (SkillDef skill in content.Skills)
            {
                Session.SkillXp.TryGetValue(skill.Id, out long xp);
                long gained = _baseline.TryGetValue(skill.Id, out long start) ? xp - start : 0;
                if (gained <= 0) continue;
                totalGained += gained;
                float since = _firstGainTime.TryGetValue(skill.Id, out float t) ? t : _sessionStart;
                float hours = Mathf.Max((Time.time - since) / 3600f, 1f / 120f); // at least 30 s, so early rates aren't absurd
                float perHour = gained / hours;
                string line = skill.Name + ": +" + UiKit.FormatXp(gained) + " xp (" + UiKit.FormatXp((long)perHour) + "/h)";

                int level = content.XpCurve.LevelForXp(xp);
                int target = SkillTargets.Get(Session.PlayerName, skill.Id);
                if (target > level && perHour > 0f)
                {
                    float hoursLeft = (content.XpCurve.XpForLevel(target) - xp) / perHour;
                    line += "\n   level " + target + " in " + FormatHours(hoursLeft);
                }
                lines.Add(line);
            }
            float minutes = (Time.time - _sessionStart) / 60f;
            _summary.text = "This session: +" + UiKit.FormatXp(totalGained) + " xp in " + Mathf.FloorToInt(minutes) + " min";
            _lines.text = lines.Count > 0 ? string.Join("\n", lines) : "No XP gained yet.";
        }

        private static string FormatHours(float hours) =>
            hours < 1f ? Mathf.CeilToInt(hours * 60f) + " min" : hours.ToString("0.#") + " h";
    }
}
