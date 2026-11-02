using Rth.Shared.Progress;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// A trial in progress (the long way back): its title, the time against the player's best, and
    /// their blood as a cold bar that pales as it falls. Opens and closes with the run ("trial" state),
    /// in the boss bar's place (the two never run together).
    /// </summary>
    public sealed class TrialTimerPanel : UiPanel
    {
        public override string Id => "trial_timer";
        public override string Title => "Trial";
        public override string Icon => "clock";
        public override string DefaultWindow => UiDefaultLayout.Boss;
        public override bool OpenByDefault => false;
        public override Vector2? FixedSize => new Vector2(460f, 58f);
        public override bool Bare => true;
        public override bool? ServerOpen => !string.IsNullOrEmpty(Run?.Trial);

        private TrialState Run => Session.ServerState<TrialState>("trial");

        private static readonly Color Warm = new Color(0.62f, 0.77f, 0.75f);
        private static readonly Color Cold = new Color(0.86f, 0.93f, 0.95f);

        private Label _name, _time, _best, _bloodLabel;
        private VisualElement _fill;

        protected override void Build(VisualElement root)
        {
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

            VisualElement track = UiKit.El(card, "bar");
            track.style.marginTop = 3f;
            _fill = UiKit.El(track, "bar__fill");
            UiKit.El(track, "bar__gloss");
            _bloodLabel = UiKit.Text(track, "", "bar__label", "bold");
            _bloodLabel.style.fontSize = 12;
        }

        public override void Refresh()
        {
            TrialState run = Run;
            if (string.IsNullOrEmpty(run?.Trial)) return;
            _name.text = run.Title;
            _time.text = BossTimerPanel.Duration(Mathf.Max(0f, Session.EstimatedTick - run.StartTick));
            _best.text = run.BestTicks > 0 ? "best " + BossTimerPanel.Duration(run.BestTicks) : "";

            var state = Session.PlayerState;
            float share = state != null && state.MaxBlood > 0 ? Mathf.Clamp01((float)state.Blood / state.MaxBlood) : 1f;
            _fill.style.width = Length.Percent(share * 100f);
            Color colour = Color.Lerp(Cold, Warm, share);
            // Low on blood, the bar breathes, so the danger is felt before it's read.
            if (share < 0.25f) colour = Color.Lerp(colour, Color.white, 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.time * 6f)));
            _fill.style.backgroundColor = colour;
            _bloodLabel.text = "BLOOD  " + Mathf.FloorToInt(share * 100f) + "%";
        }
    }
}
