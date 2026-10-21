using System.Collections.Generic;
using Rth.Shared.Progress;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The boss bar at the top of the screen while a run is in progress: the boss's name and the
    /// run's time against the player's best, and the encounter's big bars (the Saint's Requiem,
    /// which you fill to win, and his Wake, which fails the run when full; danger bars pulse as
    /// they near the top). Opens and closes with the run.
    /// </summary>
    public sealed class BossTimerPanel : UiPanel
    {
        public override string Id => "boss_timer";
        public override string Title => "Boss";
        public override string Icon => "clock";
        public override string DefaultWindow => UiDefaultLayout.Boss;
        public override bool OpenByDefault => false;
        public override Vector2? FixedSize => new Vector2(460f, 58f);
        public override bool Bare => true;
        public override bool? ServerOpen => !string.IsNullOrEmpty(Run?.Boss);

        private BossRunState Run => Session.ServerState<BossRunState>("bossRun");

        private Label _name, _time, _best;
        private VisualElement _bars;
        private readonly List<(VisualElement Root, Label Label, VisualElement Fill)> _rows = new List<(VisualElement, Label, VisualElement)>();

        protected override void Build(VisualElement root)
        {
            // A slim strip: the boss and the clock on one line, the encounter's bars side by side
            // beneath, each with its name and value written inside it.
            VisualElement card = UiKit.El(root, "card");
            card.style.flexGrow = 1;
            card.style.paddingTop = card.style.paddingBottom = 5f;
            card.style.paddingLeft = card.style.paddingRight = 8f;

            VisualElement top = UiKit.El(card, "row");
            top.style.alignItems = Align.Center;
            _name = UiKit.Text(top, "", "heading");
            _name.style.marginTop = _name.style.marginBottom = 0f;
            _name.style.fontSize = 12;
            UiKit.El(top, "spacer");
            _best = UiKit.Text(top, "", "muted", "small");
            _best.style.marginRight = 8f;
            _time = UiKit.Text(top, "", "value", "bold");
            _time.style.fontSize = 15;

            _bars = UiKit.El(card, "row");
            _bars.style.marginTop = 3f;
        }

        public override void Refresh()
        {
            BossRunState run = Run;
            if (string.IsNullOrEmpty(run?.Boss)) return;
            BossDef boss = Session.Content?.Bosses.Find(b => b.Id == run.Boss);
            _name.text = boss?.Name ?? run.Boss;
            _time.text = Duration(Mathf.Max(0f, Session.EstimatedTick - run.StartTick));
            _best.text = run.BestTicks > 0 ? "best " + Duration(run.BestTicks) : "";

            while (_rows.Count < run.Bars.Count) _rows.Add(Bar());
            for (int i = 0; i < _rows.Count; i++)
            {
                bool shown = i < run.Bars.Count;
                _rows[i].Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown) continue;
                EncounterBar bar = run.Bars[i];
                Color colour = ColorUtility.TryParseHtmlString(bar.Colour, out Color c) ? c : Color.white;
                // A danger bar near the top pulses, so the threat is felt before it's read.
                if (bar.Danger && bar.Value > 0.75f) colour = Color.Lerp(colour, Color.white, 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.time * 8f)));
                _rows[i].Label.text = bar.Label.ToUpperInvariant() + "  " + Mathf.FloorToInt(bar.Value * 100f) + "%";
                _rows[i].Fill.style.width = Length.Percent(Mathf.Clamp01(bar.Value) * 100f);
                _rows[i].Fill.style.backgroundColor = colour;
            }
        }

        private (VisualElement, Label, VisualElement) Bar()
        {
            VisualElement track = UiKit.El(_bars, "bar");
            if (_rows.Count > 0) track.style.marginLeft = 6f;
            VisualElement fill = UiKit.El(track, "bar__fill");
            UiKit.El(track, "bar__gloss");
            Label label = UiKit.Text(track, "", "bar__label", "bold");
            label.style.fontSize = 12;
            return (track, label, fill);
        }

        public static string Duration(float ticks)
        {
            float seconds = ticks * 0.6f;
            int minutes = (int)(seconds / 60f);
            return minutes + ":" + (seconds - minutes * 60).ToString("00.0");
        }
    }
}
