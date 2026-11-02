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
    /// Religion: the faith's two halves (devotion itself is on the main action bar). <b>Hymns</b> (the church's prayers,
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
        public override bool Scrolls => false;

        private ScrollView _list;
        private List<HymnDef> _builtFor;
        private int _builtForAbilities = -1;
        private readonly List<(string Id, VisualElement Slot, Label Level)> _slots = new List<(string, VisualElement, Label)>();
        private readonly List<(string Id, VisualElement Slot, Label Level)> _verses = new List<(string, VisualElement, Label)>();

        protected override void Build(VisualElement root)
        {
            // Devotion is on the main action bar and what is being sung on the buff bar: this panel
            // is only the book.
            _list = UiKit.Scroll(root);
        }

        private void Rebuild()
        {
            _builtFor = Session.Content.Hymns;
            _builtForAbilities = Session.Content.Abilities.Count;
            _list.Clear();
            _slots.Clear();
            _verses.Clear();

            UiKit.Heading(_list, "Hymns").style.marginTop = 0f;
            VisualElement grid = UiKit.El(_list, "slot-grid");
            foreach (HymnDef hymn in _builtFor.OrderBy(Hymns.LevelNeeded))
            {
                string id = hymn.Id;
                VisualElement slot = UiKit.Slots.Make(grid, out VisualElement icon);
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
            VisualElement verses = UiKit.El(_list, "slot-grid");
            foreach (AbilityDef verse in Session.Content.Abilities.Where(a => a.Type == AbilityType.Verse).OrderBy(AbilityBookPanel.LevelNeeded))
                _verses.Add(AbilityBookPanel.AbilitySlot(Ui, Session, verses, verse));
        }

        public override void Refresh()
        {
            PlayerStateMessage state = Session.PlayerState;
            if (state == null || Session.Content == null) return;
            if (!ReferenceEquals(Session.Content.Hymns, _builtFor) || Session.Content.Abilities.Count != _builtForAbilities) Rebuild();

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
