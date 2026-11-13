using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// Occultism's spellbooks: every spell by book, the ones the player knows bright, the active
    /// one marked. Clicking a known spell makes it the one occult auto-attacks cast (the "spell"
    /// intent); the server says why if it can't be (a level). Each row says what a cast burns.
    /// </summary>
    public sealed class SpellbookPanel : UiPanel
    {
        public override string Id => "spellbook";
        public override string Title => "Spellbook";
        public override string Icon => "lash";
        public override string DefaultWindow => UiDefaultLayout.Side2;
        public override int Order => 4;
        public override bool OpenByDefault => false;

        private VisualElement _list;
        private string _signature;

        protected override void Build(VisualElement root)
        {
            // The spells first; the hint under them, where a small window can scroll it away.
            _list = UiKit.El(root, "well");
            Label hint = UiKit.Text(root, "Your occult attacks cast the marked spell. Click a known spell to cast it instead.", "muted", "small");
            hint.style.marginTop = 6f;
        }

        public override void Refresh()
        {
            if (Session.Content == null) return;
            SpellsState state = Session.ServerState<SpellsState>(SpellsState.Key);
            string signature = state.Active + "|" + string.Join(",", state.Known) + "|" + Session.Content.Spells.Count;
            if (signature == _signature) return;
            _signature = signature;
            _list.Clear();

            var known = new HashSet<string>(state.Known);
            foreach (SpellbookDef book in Session.Content.Spellbooks)
            {
                List<SpellDef> spells = Session.Content.Spells.Where(s => s.Spellbook == book.Id).OrderBy(s => s.Level).ToList();
                if (spells.Count == 0) continue;
                UiKit.Heading(_list, book.Name);
                foreach (SpellDef spell in spells) Row(spell, known.Contains(spell.Id), state.Active == spell.Id);
            }
            if (Session.Content.Spells.Count == 0) UiKit.Text(_list, "No spellbooks yet.", "muted", "small");
        }

        private void Row(SpellDef spell, bool known, bool active)
        {
            VisualElement row = UiKit.El(_list, "row");
            row.style.marginTop = 3f;
            string label = (active ? "» " : "") + spell.Name;
            Label name = active ? UiKit.Text(row, label, "bold") : UiKit.Text(row, label);
            if (!known) name.style.opacity = 0.45f;
            UiKit.El(row, "spacer");
            UiKit.Text(row, "Lv " + spell.Level, "muted", "small");

            string burns = spell.Reagents.Count == 0
                ? "Burns nothing."
                : "Burns " + string.Join(", ", spell.Reagents.Select(r => r.Count + " " + (Session.Item(r.Item)?.Name ?? r.Item).ToLowerInvariant())) + " a cast.";
            row.tooltip = spell.Description + "\n" + burns + (known ? "" : "\nNot learned yet.");
            if (known && !active)
                row.RegisterCallback<ClickEvent>(_ => Session.Intent(SpellsState.Intent, spell.Id));
        }
    }
}
