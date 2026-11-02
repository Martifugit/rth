using System.Collections.Generic;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The make window (make-X, docs/skills.md 2.4), while the server has one open: what can be
    /// made at the station (or from the item) in level order under their headings, each with how
    /// many the pack allows. Pick one on the left; the right shows its inputs, XP and how likely it
    /// is to go wrong, and the amount buttons start it (1, 5, 10, X or All). The server does the
    /// making; this only sends the "make" intent.
    /// </summary>
    public sealed class MakePanel : UiPanel
    {
        public override string Id => "make";
        public override string Title => Session?.Make is { IsOpen: true } make ? make.Title : "Make";
        public override string Icon => "smithing";
        public override string DefaultWindow => UiDefaultLayout.Town;
        public override bool OpenByDefault => false;
        public override Vector2? FixedSize => new Vector2(520f, 380f);
        public override bool? ServerOpen => Session.Make.IsOpen;
        public override void OnClosedByPlayer() => Session.Intent(MakeState.CloseIntent);

        private VisualElement _list;
        private VisualElement _detail;
        private int _serial = -1;
        private int _version = -1;
        private string _selected;

        protected override void Build(VisualElement root)
        {
            VisualElement row = UiKit.El(root, "row");
            row.style.flexGrow = 1;
            row.style.alignItems = Align.Stretch;

            VisualElement left = UiKit.El(row, "well");
            left.style.width = 230f;
            left.style.marginRight = 8f;
            _list = UiKit.Scroll(left);

            _detail = UiKit.El(row);
            _detail.style.flexGrow = 1;
        }

        public override void Refresh()
        {
            MakeState make = Session.Make;
            if (!make.IsOpen) return;
            if (make.Serial != _serial)
            {
                _serial = make.Serial;
                _selected = null;
                Window?.Activate(this); // the title follows the station
                // Start on the first thing the pack can make.
                foreach (MakeOption o in make.Options)
                    if (o.Makeable > 0 && o.Problem == null) { _selected = o.Recipe; break; }
                _selected ??= make.Options[0].Recipe;
                _version = -1;
            }
            if (_version == Session.StateVersion) return;
            _version = Session.StateVersion;
            BuildList(make);
            BuildDetail(make);
        }

        private MakeOption Selected(MakeState make)
        {
            foreach (MakeOption o in make.Options)
                if (o.Recipe == _selected) return o;
            return make.Options[0];
        }

        private void BuildList(MakeState make)
        {
            _list.Clear();
            string group = null;
            foreach (MakeOption option in make.Options)
            {
                if (!string.IsNullOrEmpty(option.Group) && option.Group != group)
                {
                    group = option.Group;
                    UiKit.Heading(_list, group).style.marginTop = 4f;
                }
                MakeOption o = option;
                bool can = o.Makeable > 0 && o.Problem == null;
                VisualElement row = UiKit.El(_list, "master-row");
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.EnableInClassList("master-row--selected", o.Recipe == _selected);
                VisualElement icon = UiKit.El(row, "slot__icon");
                icon.style.width = icon.style.height = 24f;
                icon.style.marginRight = 6f;
                icon.pickingMode = PickingMode.Ignore;
                UiKit.SetItem(icon, Session.Item(o.Item));
                Label name = UiKit.Text(row, Capital(o.Name));
                name.style.flexGrow = 1;
                name.style.color = can ? UiKit.TextColour : UiKit.Muted;
                Label count = UiKit.Text(row, o.Makeable > 0 ? "x" + o.Makeable : "Lv " + o.Level, "small");
                count.style.color = o.Problem != null ? UiKit.Bad : can ? UiKit.Value : UiKit.Muted;
                row.RegisterCallback<ClickEvent>(e =>
                {
                    _selected = o.Recipe;
                    _version = -1;
                    if (e.clickCount >= 2 && can) Start(o, 0);
                });
            }
        }

        private void BuildDetail(MakeState make)
        {
            _detail.Clear();
            MakeOption o = Selected(make);
            ItemDef def = Session.Item(o.Item);

            VisualElement head = UiKit.El(_detail, "row");
            head.style.alignItems = Align.Center;
            VisualElement slot = UiKit.Slots.Make(head, out VisualElement icon);
            UiKit.SetItem(icon, def);
            if (def != null) UiKit.Tooltip(slot, () => GameText.Describe(def, o.Count));
            VisualElement words = UiKit.El(head);
            words.style.marginLeft = 8f;
            UiKit.Text(words, UiKit.Title(Capital(o.Name)) + (o.Count > 1 ? UiKit.Colour("  x" + o.Count, UiKit.Value) : ""));
            UiKit.Text(words, SkillName(o.Skill) + " " + o.Level + UiKit.Colour("   " + o.Xp.ToString("0.#") + " XP", UiKit.Value), "small");

            UiKit.Heading(_detail, "Takes").style.marginTop = 8f;
            foreach (ItemCost input in o.Inputs)
            {
                int have = Have(input.Item);
                ItemDef inDef = Session.Item(input.Item);
                VisualElement line = UiKit.El(_detail, "row");
                line.style.alignItems = Align.Center;
                VisualElement inIcon = UiKit.El(line, "slot__icon");
                inIcon.style.width = inIcon.style.height = 20f;
                inIcon.style.marginRight = 6f;
                UiKit.SetItem(inIcon, inDef);
                UiKit.Text(line, input.Count + " x " + (inDef?.Name ?? input.Item) + "  " +
                    UiKit.Colour("(" + have + ")", have >= input.Count ? UiKit.Good : UiKit.Bad), "small");
            }
            if (o.FailChance > 0.005f)
                UiKit.Text(_detail, UiKit.Colour("Chance to go wrong: " + Mathf.RoundToInt((float)o.FailChance * 100f) + "%", UiKit.Muted), "small").style.marginTop = 6f;
            if (o.Problem != null)
                UiKit.Text(_detail, UiKit.Colour(o.Problem, UiKit.Bad), "prose", "small").style.marginTop = 6f;

            VisualElement spacer = UiKit.El(_detail);
            spacer.style.flexGrow = 1;
            bool can = o.Makeable > 0 && o.Problem == null;
            VisualElement buttons = UiKit.El(_detail, "row");
            buttons.style.flexWrap = Wrap.Wrap;
            foreach (int n in new[] { 1, 5, 10 })
            {
                int count = n;
                Amount(buttons, count.ToString(), can && o.Makeable >= 1, () => Start(o, count));
            }
            Amount(buttons, "X...", can, () => Ui.PromptNumber(Capital(make.Verb) + " how many?", n => Start(o, n)));
            Amount(buttons, Capital(make.Verb) + " all (" + o.Makeable + ")", can, () => Start(o, 0));
        }

        private static void Amount(VisualElement parent, string text, bool enabled, System.Action onClick)
        {
            Label b = UiKit.Button(parent, text, () => { if (enabled) onClick(); });
            b.style.marginRight = 4f;
            b.style.marginTop = 4f;
            b.SetEnabled(enabled);
        }

        private void Start(MakeOption o, int count)
        {
            if (count < 0) return;
            Session.Intent(MakeState.Intent, o.Recipe + "|" + count);
        }

        private int Have(string item)
        {
            int n = 0;
            foreach (ItemSlot s in Session.Inventory)
                if (s.ItemId == item) n += s.Count;
            return n;
        }

        private string SkillName(string skill)
        {
            if (Session.Content != null)
                foreach (Rth.Shared.Skills.SkillDef s in Session.Content.Skills)
                    if (s.Id == skill) return s.Name;
            return skill;
        }

        private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
