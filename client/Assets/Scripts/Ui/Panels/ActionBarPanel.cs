using Rth.Shared.Combat;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The main action bar, a fixed size: blood, phlegm and breath as three equal gauges side by
    /// side; the bile axis between its two poles with the value beside it; then 14 slots on keys
    /// 1 to 0, - and =. Secondary bars (<see cref="SecondaryBarPanel"/>) are just slots.
    /// </summary>
    public sealed class ActionBarPanel : UiPanel
    {
        public override string Id => "actionbar";
        public override string Title => "Action bar";
        public override string Icon => "book";
        public override string DefaultWindow => UiDefaultLayout.ActionBar;
        public override bool Bare => true;
        public override Vector2? FixedSize => new Vector2(636f, 124f);

        private UiKit.Gauge _blood;
        private UiKit.Gauge _phlegm;
        private UiKit.Gauge _breath;
        private BileAxis _bile;
        private ActionBarRow _slots;

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            VisualElement frame = UiKit.El(root, "hud");
            frame.style.flexGrow = 1;

            VisualElement gauges = UiKit.El(frame, "row");
            _blood = new UiKit.Gauge(gauges, "blood", UiKit.BloodColour);
            _phlegm = new UiKit.Gauge(gauges, "phlegm", UiKit.PhlegmColour);
            _breath = new UiKit.Gauge(gauges, "breath", UiKit.BreathColour);
            _breath.Root.style.marginRight = 0f;
            UiKit.Tooltip(_blood.Root, () => UiKit.Title("Blood") + "\nYour life. Food restores it.");
            UiKit.Tooltip(_phlegm.Root, () => UiKit.Title("Phlegm") + "\nBuilt by builder abilities, spent by spenders and ultimates. Fades out of combat.");
            UiKit.Tooltip(_breath.Root, () => UiKit.Title("Breath") + "\nHeld focuses spend it, such as deflection (F). Recovers out of combat.");

            _bile = new BileAxis(frame);
            _bile.Root.style.marginTop = 6f;
            UiKit.Tooltip(_bile.Root, () => BileTooltip());

            VisualElement slots = UiKit.El(frame);
            slots.style.marginTop = 7f;
            _slots = new ActionBarRow(Ui, slots, 0);
        }

        public override void Refresh()
        {
            PlayerStateMessage state = Session.PlayerState;
            ClientContent content = Session.Content;
            if (state == null || content == null) return;

            _blood.Set(state.Blood / (float)Mathf.Max(1, state.MaxBlood), state.Blood.ToString("N0") + " / " + state.MaxBlood.ToString("N0"));
            _phlegm.Set(state.Phlegm / (float)Mathf.Max(1, content.MaxPhlegm), state.Phlegm + "%");
            _breath.Set(state.Breath / (float)Mathf.Max(1, state.MaxBreath), state.Breath + " / " + state.MaxBreath + (state.Deflect != CombatStyle.None ? "  deflecting" : ""));
            _breath.Fill = state.Deflect != CombatStyle.None ? new Color(0.6f, 0.82f, 1f) : UiKit.BreathColour;
            _bile.Set(state.Bile, content);
            _slots.Refresh();
        }

        private string BileTooltip()
        {
            PlayerStateMessage state = Session.PlayerState;
            ClientContent content = Session.Content;
            string text = UiKit.Title("Bile") +
                "\n" + UiKit.Colour("Black bile", UiKit.BlackBile) + " to the left: melancholic, warding." +
                "\n" + UiKit.Colour("Yellow bile", UiKit.YellowBile) + " to the right: choleric, harder hitting." +
                "\n" + UiKit.Colour("Pale marks: passives begin. Red marks: fever and torpor.", UiKit.Muted);
            if (state == null || content == null) return text;
            var rules = new CombatRules { BileLimit = content.BileLimit, BilePassiveThreshold = content.BilePassiveThreshold, BileExtreme = content.BileExtreme };
            string now = rules.IsCholeric(state.Bile) ? "Choleric: +" + rules.CholericBonusPercent(state.Bile) + "% damage"
                : rules.IsMelancholic(state.Bile) ? "Melancholic: " + rules.MelancholicWardPercent(state.Bile) + "% less damage taken"
                : "Balanced";
            if (rules.IsFevered(state.Bile)) now += ". Fever: your blood is burning";
            if (rules.IsTorpid(state.Bile)) now += ". Torpor: phlegm builds slowly";
            return text + "\n\n" + UiKit.Colour(now + ".", UiKit.Value);
        }

        /// <summary>The bile axis: no text on it; the value sits in a box beside it, coloured by pole.</summary>
        private sealed class BileAxis
        {
            public readonly VisualElement Root;
            private readonly VisualElement _track;
            private readonly VisualElement _fill;
            private readonly VisualElement _needle;
            private readonly Label _value;
            private int _marksFor = int.MinValue;

            public BileAxis(VisualElement parent)
            {
                Root = UiKit.El(parent, "row");
                VisualElement black = UiKit.Icon(Root, IconLibrary.Get("bile"), 16f);
                black.style.unityBackgroundImageTintColor = UiKit.BlackBile;
                black.style.marginRight = 6f;
                _track = UiKit.El(Root, "bar");
                _track.style.height = 10f;
                _fill = UiKit.El(_track, "bar__fill");
                UiKit.El(_track, "bar__gloss");
                VisualElement centre = UiKit.El(_track);
                centre.style.position = Position.Absolute;
                centre.style.left = Length.Percent(50f);
                centre.style.width = 2f;
                centre.style.marginLeft = -1f;
                centre.style.top = 0f;
                centre.style.bottom = 0f;
                centre.style.backgroundColor = new Color(1f, 1f, 1f, 0.6f);
                _needle = UiKit.El(_track);
                _needle.style.position = Position.Absolute;
                _needle.style.width = 3f;
                _needle.style.marginLeft = -1.5f;
                _needle.style.top = 0f;
                _needle.style.bottom = 0f;
                _needle.style.backgroundColor = Color.white;
                VisualElement yellow = UiKit.Icon(Root, IconLibrary.Get("bile"), 16f);
                yellow.style.unityBackgroundImageTintColor = UiKit.YellowBile;
                yellow.style.marginLeft = 6f;
                _value = UiKit.Text(Root, "0", "bile-value");
            }

            public void Set(int bile, ClientContent content)
            {
                float limit = Mathf.Max(1, content.BileLimit);
                float half = Mathf.Abs(bile) / limit * 50f;
                _fill.style.left = Length.Percent(bile >= 0 ? 50f : 50f - half);
                _fill.style.width = Length.Percent(half);
                _fill.style.backgroundColor = bile > 0 ? UiKit.YellowBile : UiKit.BlackBile;
                _needle.style.left = Length.Percent(50f + bile / limit * 50f);
                _value.text = (bile > 0 ? "+" : "") + bile;
                _value.style.color = bile > 0 ? UiKit.YellowBile : bile < 0 ? new Color(0.75f, 0.6f, 1f) : UiKit.TextColour;

                int key = content.BilePassiveThreshold * 1000 + content.BileExtreme;
                if (key == _marksFor) return;
                _marksFor = key;
                foreach (int mark in new[] { content.BilePassiveThreshold, content.BileExtreme })
                {
                    foreach (int sign in new[] { -1, 1 })
                    {
                        var m = new VisualElement { pickingMode = PickingMode.Ignore };
                        m.style.position = Position.Absolute;
                        m.style.left = Length.Percent(50f + sign * mark / limit * 50f);
                        m.style.width = 1f;
                        m.style.top = 0f;
                        m.style.bottom = 0f;
                        m.style.backgroundColor = mark == content.BileExtreme ? new Color(1f, 0.35f, 0.25f, 0.9f) : new Color(1f, 1f, 1f, 0.35f);
                        _track.Insert(2, m);
                    }
                }
            }
        }
    }

    /// <summary>A secondary action bar: 14 more slots, no keys, a fixed size. Open them from the window menu.</summary>
    public abstract class SecondaryBarPanel : UiPanel
    {
        protected abstract int Bar { get; }
        public override string Icon => "book";
        public override bool Bare => true;
        public override bool OpenByDefault => false;
        public override Vector2? FixedSize => new Vector2(636f, 62f);

        private ActionBarRow _slots;

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            VisualElement frame = UiKit.El(root, "hud");
            frame.style.flexGrow = 1;
            _slots = new ActionBarRow(Ui, frame, Bar);
        }

        public override void Refresh() => _slots.Refresh();
    }

    public sealed class ActionBar2Panel : SecondaryBarPanel
    {
        protected override int Bar => 1;
        public override string Id => "actionbar2";
        public override string Title => "Action bar 2";
        public override string DefaultWindow => UiDefaultLayout.ActionBar2;
    }

    public sealed class ActionBar3Panel : SecondaryBarPanel
    {
        protected override int Bar => 2;
        public override string Id => "actionbar3";
        public override string Title => "Action bar 3";
        public override string DefaultWindow => UiDefaultLayout.ActionBar3;
    }
}
