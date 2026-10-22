using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.Religion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// Religion: devotion at the top, then the faith's two halves. <b>Hymns</b> (the church's prayers,
    /// for every style) as a book of icons, in level order; a sung one glows, a locked one is dimmed
    /// with the level it needs. Click one to sing it or stop, or drag it onto an action bar.
    /// <b>Verses</b> (Cruor's, for melee) below them: read in combat with a melee weapon, over
    /// scripture, paid for in blood. They are abilities, so they go on the action bars like any other.
    /// </summary>
    public sealed class ReligionPanel : UiPanel
    {
        public override string Id => "religion";
        public override string Title => "Religion";
        public override string Icon => "religion";
        public override string DefaultWindow => UiDefaultLayout.Side;
        public override int Order => 4;
        public override Key Hotkey => Key.F;

        private UiKit.Gauge _devotion;
        private Label _singing;
        private ScrollView _list;
        private List<HymnDef> _builtFor;
        private int _builtForAbilities = -1;
        private readonly List<(string Id, VisualElement Slot, Label Level)> _slots = new List<(string, VisualElement, Label)>();
        private readonly List<(string Id, VisualElement Slot, Label Level)> _verses = new List<(string, VisualElement, Label)>();

        protected override void Build(VisualElement root)
        {
            VisualElement card = UiKit.El(root, "card");
            card.style.flexShrink = 0;
            _devotion = new UiKit.Gauge(card, "devotion", UiKit.DevotionColour);
            _devotion.Root.style.marginRight = 0f;
            _devotion.Root.style.flexBasis = StyleKeyword.Auto;
            UiKit.Tooltip(_devotion.Root, () => UiKit.Title("Devotion") + "\nHymns drain it while you sing them. Pray at an altar to restore it.");
            _singing = UiKit.Text(card, "", "muted", "small");
            _singing.style.marginTop = 4f;
            _list = UiKit.Scroll(root);

            Label hint = UiKit.Text(root, "Drag a hymn onto an action bar to sing it from there.", "muted", "small");
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.flexShrink = 0;
        }

        private void Rebuild()
        {
            _builtFor = Session.Content.Hymns;
            _builtForAbilities = Session.Content.Abilities.Count;
            _list.Clear();
            _slots.Clear();
            _verses.Clear();

            UiKit.Heading(_list, "Hymns");
            UiKit.Text(_list, "The church's prayers, for every style. One overhead at a time.", "muted", "small");
            VisualElement grid = UiKit.El(_list, "slot-grid");
            foreach (HymnDef hymn in _builtFor.OrderBy(Hymns.LevelNeeded))
            {
                string id = hymn.Id;
                VisualElement slot = UiKit.El(grid, "slot");
                VisualElement icon = UiKit.El(slot, "slot__icon");
                icon.pickingMode = PickingMode.Ignore;
                UiKit.SetIcon(icon, IconLibrary.Get(hymn.Icon), Hymns.Tint);
                Label level = UiKit.Text(slot, "", "slot__level");
                level.pickingMode = PickingMode.Ignore;
                Ui.DragSource(slot,
                    () => Ui.Locked ? null : new UiRoot.Drag { Data = new HymnDrag { HymnId = id }, Icon = IconLibrary.Get(Hymns.Get(Session, id)?.Icon), Tint = Hymns.Tint },
                    () => Hymns.Toggle(Session, id));
                UiKit.Tooltip(slot, () =>
                {
                    HymnDef h = Hymns.Get(Session, id);
                    return h == null ? null : Hymns.Describe(Session, h);
                });
                _slots.Add((id, slot, level));
            }

            UiKit.Heading(_list, "Verses").style.marginTop = 10f;
            UiKit.Text(_list, "Cruor's verses: read in combat with a melee weapon, over scripture, paid for in blood.", "muted", "small");
            VisualElement verses = UiKit.El(_list, "slot-grid");
            foreach (AbilityDef verse in Session.Content.Abilities.Where(a => a.Type == AbilityType.Verse).OrderBy(AbilityBookPanel.LevelNeeded))
                _verses.Add(AbilityBookPanel.AbilitySlot(Ui, Session, verses, verse));
        }

        public override void Refresh()
        {
            PlayerStateMessage state = Session.PlayerState;
            if (state == null || Session.Content == null) return;
            if (!ReferenceEquals(Session.Content.Hymns, _builtFor) || Session.Content.Abilities.Count != _builtForAbilities) Rebuild();

            _devotion.Set(state.Devotion / (float)Mathf.Max(1, state.MaxDevotion), state.Devotion + " / " + state.MaxDevotion);
            _singing.text = state.Hymns.Count == 0
                ? "Singing nothing."
                : "Singing: " + string.Join(", ", state.Hymns.Select(h => Hymns.Get(Session, h)?.Name ?? h)) + ".";
            foreach ((string id, VisualElement slot, Label level) in _slots)
            {
                HymnDef hymn = Hymns.Get(Session, id);
                if (hymn == null) continue;
                bool locked = Hymns.Problem(Session, hymn) != null;
                slot.EnableInClassList("slot--locked", locked);
                slot.EnableInClassList("slot--on", Hymns.IsSung(state, id));
                level.text = locked ? Hymns.LevelNeeded(hymn).ToString() : "";
            }
            foreach ((string id, VisualElement slot, Label level) in _verses)
            {
                AbilityDef verse = Session.Ability(id);
                if (verse == null) continue;
                bool locked = verse.Requirements.Any(r => Session.Level(r.Skill) < r.Level);
                slot.EnableInClassList("slot--locked", locked);
                level.text = locked ? AbilityBookPanel.LevelNeeded(verse).ToString() : "";
            }
        }
    }
}
