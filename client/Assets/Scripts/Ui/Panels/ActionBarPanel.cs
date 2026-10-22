using Rth.Shared.Combat;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The main action bar, a fixed size: blood, phlegm and devotion as three equal gauges side by
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
        public override Vector2? FixedSize => new Vector2(636f, 110f);

        private UiKit.Gauge _blood;
        private UiKit.Gauge _phlegm;
        private UiKit.Gauge _devotion;
        private BileAxis _bile;
        private ActionBarRow _slots;

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            VisualElement frame = UiKit.El(root, "hud");
            frame.style.flexGrow = 1;

            // The interface lock, at the bar's top right: locks windows, action bars and ability books.
            _lock = UiKit.El(frame, "hud__lock");
            UiKit.SetIcon(_lock, IconLibrary.Get("padlock"));
            _lock.RegisterCallback<ClickEvent>(_ => Ui.Locked = !Ui.Locked);
            UiKit.Tooltip(_lock, () => UiKit.Title(Ui.Locked ? "Interface locked" : "Interface unlocked") + "\n" +
                (Ui.Locked ? "Windows, action bars and ability books stay put. Click to unlock." : "Click to lock windows, action bars and ability books in place."));

            VisualElement gauges = UiKit.El(frame, "row");
            _blood = new UiKit.Gauge(gauges, "blood", UiKit.BloodColour);
            _phlegm = new UiKit.Gauge(gauges, "phlegm", UiKit.PhlegmColour);
            _devotion = new UiKit.Gauge(gauges, "devotion", UiKit.DevotionColour);
            _devotion.Root.style.marginRight = 0f;
            UiKit.Tooltip(_blood.Root, () => UiKit.Title("Blood") + "\nYour life. Food restores it.");
            UiKit.Tooltip(_phlegm.Root, () => UiKit.Title("Phlegm") + "\nBuilt by builder abilities, spent by spenders and ultimates. Fades out of combat.");
            UiKit.Tooltip(_devotion.Root, () => UiKit.Title("Devotion") + "\nHymns drain it while you sing them (F). It doesn't come back by itself: pray at an altar.");

            _bile = new BileAxis(frame);
            _bile.Root.style.marginTop = 5f;

            VisualElement slots = UiKit.El(frame);
            slots.style.marginTop = 5f;
            _slots = new ActionBarRow(Ui, slots, 0);
        }

        private VisualElement _lock;

        public override void Refresh()
        {
            PlayerStateMessage state = Session.PlayerState;
            ClientContent content = Session.Content;
            if (state == null || content == null) return;

            _blood.Set(state.Blood / (float)Mathf.Max(1, state.MaxBlood), state.Blood.ToString("N0") + " / " + state.MaxBlood.ToString("N0"));
            _phlegm.Set(state.Phlegm / (float)Mathf.Max(1, content.MaxPhlegm), state.Phlegm + "%");
            bool singing = state.Hymns.Count > 0;
            _devotion.Set(state.Devotion / (float)Mathf.Max(1, state.MaxDevotion), state.Devotion + " / " + state.MaxDevotion + (singing ? "  singing" : ""));
            _devotion.Fill = singing ? new Color(1f, 0.93f, 0.7f) : UiKit.DevotionColour;
            _bile.Set(state.Bile, content);
            _slots.Refresh();
            _lock.EnableInClassList("hud__lock--on", Ui.Locked);
        }

        /// <summary>
        /// The bile axis between its poles' icons: two half-bars filling outward from a black
        /// stretch of the bar at its centre that holds the value (up to "+100"), coloured by pole. What bile does
        /// right now is in the buff and debuff bars.
        /// </summary>
        private sealed class BileAxis
        {
            private const float CentreWidth = 38f;

            public readonly VisualElement Root;
            private readonly VisualElement _black;
            private readonly VisualElement _yellow;
            private readonly VisualElement _blackFill;
            private readonly VisualElement _yellowFill;
            private readonly Label _value;

            public BileAxis(VisualElement parent)
            {
                Root = UiKit.El(parent, "row");
                VisualElement blackIcon = UiKit.Icon(Root, IconLibrary.Get("bile"), 22f);
                blackIcon.style.unityBackgroundImageTintColor = UiKit.BlackBile;
                blackIcon.style.marginRight = 6f;
                _black = Half(Root, out _blackFill, true);
                VisualElement centre = UiKit.El(Root, "bile-centre");
                centre.style.width = CentreWidth;
                _value = UiKit.Text(centre, "0", "bile-value");
                _yellow = Half(Root, out _yellowFill, false);
                VisualElement yellowIcon = UiKit.Icon(Root, IconLibrary.Get("bile"), 22f);
                yellowIcon.style.unityBackgroundImageTintColor = UiKit.YellowBile;
                yellowIcon.style.marginLeft = 6f;
            }

            /// <summary>One side of the axis; its fill grows away from the centre.</summary>
            private static VisualElement Half(VisualElement parent, out VisualElement fill, bool left)
            {
                VisualElement track = UiKit.El(parent, "bar", left ? "bile-half--left" : "bile-half--right");
                track.style.height = 14f;
                fill = UiKit.El(track, "bar__fill");
                if (left)
                {
                    fill.style.left = StyleKeyword.Auto;
                    fill.style.right = 0f;
                }
                UiKit.El(track, "bar__gloss");
                return track;
            }

            public void Set(int bile, ClientContent content)
            {
                float limit = Mathf.Max(1, content.BileLimit);
                float share = Mathf.Clamp01(Mathf.Abs(bile) / limit) * 100f;
                _blackFill.style.width = Length.Percent(bile < 0 ? share : 0f);
                _yellowFill.style.width = Length.Percent(bile > 0 ? share : 0f);
                _blackFill.style.backgroundColor = UiKit.BlackBile;
                _yellowFill.style.backgroundColor = UiKit.YellowBile;
                _value.text = (bile > 0 ? "+" : "") + bile;
                _value.style.color = bile > 0 ? UiKit.YellowBile : bile < 0 ? new Color(0.75f, 0.6f, 1f) : UiKit.TextColour;
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
