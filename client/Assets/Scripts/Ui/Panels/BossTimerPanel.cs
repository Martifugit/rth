using Rth.Shared.Progress;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The boss timer: while a boss run is in progress, the boss's name, how long the run has
    /// taken and the player's best, under the target bar. Opens and closes with the run.
    /// </summary>
    public sealed class BossTimerPanel : UiPanel
    {
        public override string Id => "boss_timer";
        public override string Title => "Boss";
        public override string Icon => "clock";
        public override string DefaultWindow => UiDefaultLayout.Boss;
        public override bool OpenByDefault => false;
        public override Vector2? FixedSize => new Vector2(240f, 84f);
        public override bool Bare => true;
        public override bool? ServerOpen => !string.IsNullOrEmpty(Run?.Boss);

        private BossRunState Run => Session.ServerState<BossRunState>("bossRun");

        private Label _name, _time, _best;

        protected override void Build(VisualElement root)
        {
            VisualElement card = UiKit.El(root, "card");
            card.style.flexGrow = 1;
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.Center;
            _name = UiKit.Text(card, "", "heading");
            _time = UiKit.Text(card, "", "value", "bold");
            _time.style.fontSize = 20;
            _best = UiKit.Text(card, "", "muted", "small");
        }

        public override void Refresh()
        {
            BossRunState run = Run;
            if (string.IsNullOrEmpty(run?.Boss)) return;
            BossDef boss = Session.Content?.Bosses.Find(b => b.Id == run.Boss);
            _name.text = boss?.Name ?? run.Boss;
            float ticks = Mathf.Max(0f, Session.EstimatedTick - run.StartTick);
            _time.text = Duration(ticks);
            _best.text = run.BestTicks > 0 ? "Personal best " + Duration(run.BestTicks) : "No completions yet";
        }

        public static string Duration(float ticks)
        {
            float seconds = ticks * 0.6f;
            int minutes = (int)(seconds / 60f);
            return minutes + ":" + (seconds - minutes * 60).ToString("00.0");
        }
    }
}
