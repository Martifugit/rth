using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rth.Client.Ui.Panels;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Modals
{
    /// <summary>
    /// The character window, opened from the buttons under the worn equipment or by inspecting a
    /// player: your combat stats, and the inspect card (what anyone sees of a player).
    /// </summary>
    public sealed class CharacterModal : UiModal
    {
        public override string Id => "character";
        public override string Title => "Character";
        public override string Icon => "character";
        public override Vector2 Size => new Vector2(720f, 540f);

        protected override IEnumerable<ModalTab> CreateTabs() => new ModalTab[] { new StatsTab(), new InspectTab() };
    }

    /// <summary>Every combat stat your gear, levels, hymns and buffs give, grouped: general, per style, and what verses add.</summary>
    public sealed class StatsTab : ModalTab
    {
        public const string TabId = "stats";

        public override string Id => TabId;
        public override string Title => "Stats";
        public override string Icon => "character";

        private static readonly (string Title, string[] Stats)[] Groups =
        {
            ("Offence", new[] { StatIds.AbilityDamage, StatIds.Accuracy, StatIds.DamageBonus, StatIds.Lifesteal }),
            ("Defence", new[] { StatIds.MaxBlood, StatIds.Ward }),
            ("Devotion", new[] { StatIds.MaxDevotion, StatIds.DevotionBonus }),
        };

        private static readonly CombatStyle[] Styles = { CombatStyle.Melee, CombatStyle.Ranged, CombatStyle.Occult };

        private static readonly (string Label, string[] Stats)[] StyleRows =
        {
            ("Power", Styles.Select(s => StatIds.PowerFor(s)).ToArray()),
            ("Accuracy", Styles.Select(s => StatIds.AccuracyFor(s)).ToArray()),
            ("Damage", Styles.Select(s => StatIds.DamageFor(s)).ToArray()),
            ("Deflection", Styles.Select(s => StatIds.DeflectFor(s)).ToArray()),
        };

        /// <summary>Stats that are usually zero and only matter while a verse or buff is up.</summary>
        private static readonly string[] Conditional =
        {
            StatIds.DeferDamage, StatIds.BlockHit, StatIds.BlockedPhlegm, StatIds.ScarOnHit, StatIds.BuilderPhlegm, StatIds.BloodPerAbility,
        };

        private readonly Dictionary<string, Label> _values = new Dictionary<string, Label>();
        private readonly Dictionary<string, VisualElement> _conditionalRows = new Dictionary<string, VisualElement>();
        private Label _noneActive;

        protected override void Build(VisualElement root)
        {
            ScrollView scroll = UiKit.Scroll(root);
            VisualElement columns = UiKit.El(scroll.contentContainer, "character-columns");
            VisualElement left = UiKit.El(columns, "character-column");
            VisualElement right = UiKit.El(columns, "character-column");

            foreach ((string title, string[] stats) in Groups)
            {
                VisualElement card = Card(left, title);
                foreach (string stat in stats) StatRow(card, stat);
            }

            VisualElement table = Card(right, "By style");
            VisualElement head = UiKit.El(table, "stat-row");
            UiKit.Text(head, "", "stat-row__label");
            foreach (CombatStyle style in Styles) UiKit.Text(head, GameText.StyleName(style), "stat-cell", "muted", "small");
            foreach ((string label, string[] stats) in StyleRows)
            {
                VisualElement row = UiKit.El(table, "stat-row");
                UiKit.Text(row, label, "stat-row__label");
                foreach (string stat in stats) _values[stat] = UiKit.Text(row, "", "stat-cell", "value", "bold");
            }

            VisualElement verses = Card(right, "Verses and buffs");
            _noneActive = UiKit.Text(verses, "Nothing active. Verses and some abilities add these for a while.", "muted", "small", "prose");
            foreach (string stat in Conditional) _conditionalRows[stat] = StatRow(verses, stat);

            // Anything new that no group names yet still shows.
            string[] others = StatIds.All.Where(s => !_values.ContainsKey(s)).ToArray();
            if (others.Length > 0)
            {
                VisualElement card = Card(left, "Other");
                foreach (string stat in others) StatRow(card, stat);
            }
        }

        private static VisualElement Card(VisualElement parent, string title)
        {
            VisualElement card = UiKit.El(parent, "card", "character-card");
            UiKit.Heading(card, title);
            return card;
        }

        private VisualElement StatRow(VisualElement parent, string stat)
        {
            VisualElement row = UiKit.El(parent, "stat-row");
            UiKit.Text(row, GameText.StatLabel(stat), "stat-row__label");
            _values[stat] = UiKit.Text(row, "", "stat-cell", "value", "bold");
            return row;
        }

        public override void Refresh()
        {
            foreach (KeyValuePair<string, Label> stat in _values)
            {
                Session.Stats.TryGetValue(stat.Key, out int value);
                stat.Value.text = value + GameText.StatUnit(stat.Key);
                stat.Value.EnableInClassList("stat-cell--zero", value == 0);
            }
            bool any = false;
            foreach (KeyValuePair<string, VisualElement> row in _conditionalRows)
            {
                bool on = Session.Stats.TryGetValue(row.Key, out int value) && value != 0;
                row.Value.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                any |= on;
            }
            _noneActive.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }

    /// <summary>
    /// What anyone sees of a player: name, combat and total level, worn gear and a few
    /// highlights, asked of the server (the "inspect" query). Focus is the player's entity id;
    /// without one it shows you.
    /// </summary>
    public sealed class InspectTab : ModalTab
    {
        public const string TabId = "inspect";

        public override string Id => TabId;
        public override string Title => "Inspect";
        public override string Icon => "eye";

        private string _target;
        private int _version = -1;
        private Label _name;
        private Label _levels;
        private Label _note;
        private VisualElement _you;
        private VisualElement _body;
        private WornPanel.Figure _figure;
        private VisualElement _highlights;

        private string Me => Session.LocalPlayerId.ToString(CultureInfo.InvariantCulture);

        protected override void Build(VisualElement root)
        {
            VisualElement head = UiKit.El(root, "card", "row", "inspect-head");
            UiKit.Icon(head, IconLibrary.Get("character"), 44f).style.unityBackgroundImageTintColor = UiKit.IconTint;
            VisualElement who = UiKit.El(head, "inspect-head__who");
            _name = UiKit.Text(who, "", "title-large");
            _levels = UiKit.Text(who, "", "muted");
            _you = UiKit.Button(head, "Inspect yourself", () => Show(Me));

            _note = UiKit.Text(root, "", "muted", "prose", "inspect-note");

            _body = UiKit.El(root, "inspect-body");
            VisualElement figureCard = UiKit.El(_body, "well", "inspect-figure");
            _figure = new WornPanel.Figure(Ui, Session, figureCard, null, readOnly: true);
            _figure.Fit(64f);
            VisualElement side = UiKit.El(_body, "inspect-side");
            UiKit.Heading(side, "Highlights");
            _highlights = UiKit.El(side, "well");
        }

        public override void OnShow(string focus) => Show(focus ?? _target ?? Me);

        private void Show(string target)
        {
            _target = target;
            _version = -1;
            Session.Query(QueryKinds.Inspect, target);
        }

        public override void Refresh()
        {
            if (_version == Session.QueryVersion) return;
            _version = Session.QueryVersion;

            bool self = _target == Me;
            _you.style.display = self ? DisplayStyle.None : DisplayStyle.Flex;
            QueryResultMessage answer = Session.QueryResult(QueryKinds.Inspect, _target);
            PlayerInspect inspect = answer?.Parse<PlayerInspect>();
            if (inspect == null)
            {
                _name.text = answer == null ? "Looking..." : "Nobody";
                _levels.text = "";
                _note.text = answer?.Problem ?? "";
                _note.style.display = answer == null ? DisplayStyle.None : DisplayStyle.Flex;
                _body.style.display = DisplayStyle.None;
                return;
            }

            _name.text = inspect.Name;
            _levels.text = "Combat level " + inspect.CombatLevel + "     Total level " + inspect.TotalLevel;
            _note.text = self ? "This is what other players see when they inspect you." : "";
            _note.style.display = self ? DisplayStyle.Flex : DisplayStyle.None;
            _body.style.display = DisplayStyle.Flex;

            List<string> worn = inspect.Equipment;
            _figure.Show(slot => (int)slot < worn.Count ? Session.Item(worn[(int)slot]) : null);

            _highlights.Clear();
            if (inspect.Highlights.Count == 0) UiKit.Text(_highlights, "Nothing to show yet.", "muted", "small");
            foreach (InspectLine line in inspect.Highlights)
            {
                VisualElement row = UiKit.El(_highlights, "stat-row");
                if (!string.IsNullOrEmpty(line.Icon))
                    UiKit.Icon(row, IconLibrary.Get(line.Icon), 20f, "inspect-line__icon").style.unityBackgroundImageTintColor = UiKit.IconTint;
                UiKit.Text(row, line.Label, "stat-row__label");
                UiKit.Text(row, line.Value, "stat-cell", "value", "bold");
            }
        }
    }
}
