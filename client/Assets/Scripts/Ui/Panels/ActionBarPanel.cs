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
        public override Vector2? FixedSize => new Vector2(Width, Height);

        /// <summary>The bar's frame either side of the slots (the hud surface's own padding).</summary>
        public const float SidePadding = 10f;

        /// <summary>
        /// The main bar's width: one row of slots and the frame's padding. Secondary bars take the
        /// same width (or exactly half), so a bar snapped under this one lines up slot for slot.
        /// </summary>
        public static readonly float Width = ActionBarRow.SizeFor(ActionBars.SlotCount).x + 2f * SidePadding;

        /// <summary>The gauges, the bile axis and a row of slots, in the frame's padding.</summary>
        public const float Height = 108f;

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

            // The action bar lock, at the bar's top right: nothing is dragged onto, off or between bars (windows have their own lock).
            _lock = UiKit.El(frame, "hud__lock");
            UiKit.SetIcon(_lock, IconLibrary.Get("padlock"));
            _lock.RegisterCallback<ClickEvent>(_ => Ui.BarsLocked = !Ui.BarsLocked);
            UiKit.Tooltip(_lock, () => UiKit.Title(Ui.BarsLocked ? "Action bars locked" : "Action bars unlocked") + "\n" +
                (Ui.BarsLocked ? "Abilities stay where they are on your bars. Click to unlock." : "Click to stop abilities being dragged on or off your bars by accident."));

            VisualElement gauges = UiKit.El(frame, "row");
            gauges.style.marginRight = 20f; // clear of the lock
            _blood = new UiKit.Gauge(gauges, "blood", UiKit.BloodColour);
            _phlegm = new UiKit.Gauge(gauges, "phlegm", UiKit.PhlegmColour);
            _devotion = new UiKit.Gauge(gauges, "devotion", UiKit.DevotionColour);
            _devotion.Root.style.marginRight = 0f;
            UiKit.Tooltip(_blood.Root, () => UiKit.Title("Blood") + "\nYour life. Food restores it.");
            UiKit.Tooltip(_phlegm.Root, () => UiKit.Title("Phlegm") + "\nBuilt by builder abilities, spent by spenders and ultimates. Fades out of combat.");
            UiKit.Tooltip(_devotion.Root, () => UiKit.Title("Devotion") + "\nHymns drain it while you sing them (F). It doesn't come back by itself: pray at an altar.");

            _bile = new BileAxis(frame);
            _bile.Root.style.marginTop = 5f;

            _slots = new ActionBarRow(Ui, frame, 0);
            _slots.Root.style.marginTop = 5f;
            _slots.Root.style.alignSelf = Align.Center;
            _lock.BringToFront(); // above the gauges, so its own tooltip and click win
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
            _lock.EnableInClassList("hud__lock--on", Ui.BarsLocked);
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

    /// <summary>
    /// A secondary action bar (bars 2 to 5, opened from the window menu): 14 more slots, no keys.
    /// It is a full row as wide as the main bar, or, resized, half that width in two rows of seven,
    /// or two columns of seven standing tall; the window snaps between those sizes
    /// (<see cref="AllowedSizes"/>) and the slots are laid out, centred, to whichever it has.
    /// </summary>
    public abstract class SecondaryBarPanel : UiPanel
    {
        /// <summary>The frame above and below the slots (and either side of the tall layout).</summary>
        private const float EndPadding = 8f;

        /// <summary>The layouts as columns of slots, one for one with <see cref="Sizes"/>.</summary>
        private static readonly int[] Columns = { ActionBars.SlotCount, 7, 2 };

        /// <summary>A full row as wide as the main bar; exactly half its width in two rows; two columns standing as tall as that is wide.</summary>
        private static readonly Vector2[] Sizes =
        {
            new Vector2(ActionBarPanel.Width, ActionBarRow.Pitch + 2f * EndPadding),
            new Vector2(ActionBarPanel.Width / 2f, 2f * ActionBarRow.Pitch + 2f * EndPadding),
            new Vector2(2f * ActionBarRow.Pitch + 2f * EndPadding, ActionBarPanel.Width / 2f),
        };

        protected abstract int Bar { get; }
        public override string Icon => "book";
        public override bool Bare => true;
        public override bool OpenByDefault => false;
        public override Vector2[] AllowedSizes => Sizes;
        public override Vector2 DefaultSize => Sizes[0];
        public override Vector2 MinSize => new Vector2(Sizes[2].x, Sizes[0].y);

        private ActionBarRow _slots;
        private VisualElement _frame;
        private int _columns = -1;

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            // No padding of its own: the block of slots sits centred in whatever size the window has.
            _frame = UiKit.El(root, "hud", "hud--bar");
            _frame.style.flexGrow = 1;
            _slots = new ActionBarRow(Ui, _frame, Bar);
        }

        public override void Refresh()
        {
            int columns = Columns[Nearest(Window != null ? Window.Size : _frame.layout.size)];
            if (columns != _columns)
            {
                _columns = columns;
                _slots.SetColumns(columns);
            }
            _slots.Refresh();
        }

        /// <summary>The layout whose size is nearest the window's, by proportion on each axis.</summary>
        private static int Nearest(Vector2 size)
        {
            if (float.IsNaN(size.x) || float.IsNaN(size.y) || size.x <= 0f || size.y <= 0f) return 0;
            int best = 0;
            float bestScore = float.MaxValue;
            for (int i = 0; i < Sizes.Length; i++)
            {
                float score = Mathf.Abs(Mathf.Log(size.x / Sizes[i].x)) + Mathf.Abs(Mathf.Log(size.y / Sizes[i].y));
                if (score >= bestScore) continue;
                best = i;
                bestScore = score;
            }
            return best;
        }
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

    public sealed class ActionBar4Panel : SecondaryBarPanel
    {
        protected override int Bar => 3;
        public override string Id => "actionbar4";
        public override string Title => "Action bar 4";
        public override string DefaultWindow => UiDefaultLayout.ActionBar4;
    }

    public sealed class ActionBar5Panel : SecondaryBarPanel
    {
        protected override int Bar => 4;
        public override string Id => "actionbar5";
        public override string Title => "Action bar 5";
        public override string DefaultWindow => UiDefaultLayout.ActionBar5;
    }
}
