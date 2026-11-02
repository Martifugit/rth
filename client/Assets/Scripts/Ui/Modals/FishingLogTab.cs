using System.Collections.Generic;
using Rth.Shared.Angling;
using Rth.Shared.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Modals
{
    /// <summary>
    /// The fishing log (docs/fishing.md 5.1), a journal tab: every trophy species, with what the
    /// player has caught (personal best, revised fish, losses), the moves seen, the lures that took
    /// it, the spots seen and a market's lead, and the angler's notes (those for catches made and
    /// those people gave). From the "anglingLog" state and the species in content.
    /// </summary>
    public sealed class FishingLogTab : ModalTab
    {
        public override string Id => "fishinglog";
        public override string Title => "Fishing log";
        public override string Icon => "fishing";

        private MasterDetail _view;
        private int _version = -1;

        protected override void Build(VisualElement root) => _view = new MasterDetail(root, Draw);

        public override void OnShow(string focus)
        {
            _version = -1;
            if (focus != null) _view.Select(focus);
        }

        public override void Refresh()
        {
            if (_version == Session.StateVersion) return;
            _version = Session.StateVersion;
            var entries = new List<MasterDetail.Entry>();
            foreach (AnglingSpeciesDef s in Species())
            {
                AnglingSpeciesLog log = Log(s.Id);
                bool known = log.Caught > 0 || log.Spots.Count > 0;
                entries.Add(new MasterDetail.Entry
                {
                    Id = s.Id,
                    Text = s.Name,
                    Note = log.Caught > 0 ? Weight(log.BestGrams) : "Lv " + s.Level,
                    Icon = s.Icon,
                    IconTint = ColorUtility.TryParseHtmlString(s.Color, out Color c) ? Color.Lerp(c, Color.white, 0.35f) : (Color?)null,
                    Colour = known ? UiKit.TextColour : UiKit.Muted,
                    Section = "Tier " + s.Tier,
                });
            }
            _view.SetEntries(entries);
        }

        private IEnumerable<AnglingSpeciesDef> Species()
        {
            var list = new List<AnglingSpeciesDef>(Session.Content?.Angling?.Species ?? new List<AnglingSpeciesDef>());
            list.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : a.Level.CompareTo(b.Level));
            return list;
        }

        private AnglingSpeciesLog Log(string species)
        {
            AnglingLogState state = Session.ServerState<AnglingLogState>(AnglingLogState.StateKey);
            return state.Species.TryGetValue(species, out AnglingSpeciesLog log) ? log : new AnglingSpeciesLog();
        }

        private void Draw(string id)
        {
            AnglingSpeciesDef species = null;
            foreach (AnglingSpeciesDef s in Species())
                if (s.Id == id) species = s;
            if (species == null) return;
            AnglingSpeciesLog log = Log(id);
            VisualElement d = _view.Detail;

            UiKit.Text(d, UiKit.Title(species.Name));
            UiKit.Text(d, UiKit.Colour("Fishing " + species.Level + " to hook. Trophies from " + Weight(species.MinGrams) + " to " + Weight(species.MaxGrams) + ".", UiKit.Muted), "small");

            UiKit.Heading(d, "Catches").style.marginTop = 8f;
            if (log.Caught == 0 && log.Lost == 0) UiKit.Text(d, UiKit.Colour("You haven't hooked one yet.", UiKit.Muted), "small");
            else
            {
                UiKit.Text(d, "Landed " + UiKit.Colour(log.Caught.ToString(), UiKit.Value) + "   Lost " + UiKit.Colour(log.Lost.ToString(), UiKit.Value)
                              + (log.Revised > 0 ? "   Revised " + UiKit.Colour(log.Revised.ToString(), UiKit.Value) : ""), "small");
                if (log.Caught > 0) UiKit.Text(d, "Personal best " + UiKit.Colour(Weight(log.BestGrams), UiKit.Value), "small");
            }

            UiKit.Heading(d, "How it fights").style.marginTop = 8f;
            var moves = new List<string>();
            foreach (AnglingMove m in System.Enum.GetValues(typeof(AnglingMove)))
                if (m != AnglingMove.Straight && (log.Moves & (1 << (int)m)) != 0) moves.Add(MoveWords(m));
            UiKit.Text(d, moves.Count > 0 ? string.Join("; ", moves) + "." : UiKit.Colour("Hook one to find out.", UiKit.Muted), "small", "prose");

            if (log.Lures.Count > 0)
            {
                var names = new List<string>();
                foreach (string lure in log.Lures) names.Add(Session.Item(lure)?.Name ?? lure);
                UiKit.Text(d, "Taken on: " + string.Join(", ", names), "small", "prose");
            }

            UiKit.Heading(d, "Where").style.marginTop = 8f;
            UiKit.Text(d, log.Spots.Count == 0 ? UiKit.Colour("You haven't seen one rising.", UiKit.Muted)
                : "Spots seen: " + UiKit.Colour(log.Spots.Count.ToString(), UiKit.Value)
                  + (string.IsNullOrEmpty(log.Lead) ? "" : "\nA market's lead: " + UiKit.Colour(Place(log.Lead), UiKit.Value)), "small", "prose");

            UiKit.Heading(d, "Notes").style.marginTop = 8f;
            int shown = 0;
            foreach (AnglingNoteDef note in species.Notes)
            {
                bool have = note.Catches > 0 ? log.Caught >= note.Catches : log.Notes.Contains(note.Id);
                if (have)
                {
                    UiKit.Text(d, note.Text, "small", "prose").style.marginBottom = 4f;
                    shown++;
                }
                else if (note.Catches > 0)
                    UiKit.Text(d, UiKit.Colour("Catch " + note.Catches + " to learn more.", UiKit.Muted), "small").style.marginBottom = 4f;
            }
            if (shown == 0 && species.Notes.Count == 0) UiKit.Text(d, UiKit.Colour("Nothing yet.", UiKit.Muted), "small");
        }

        private static string MoveWords(AnglingMove m) => m switch
        {
            AnglingMove.Left => "turns left (pull right)",
            AnglingMove.Right => "turns right (pull left)",
            AnglingMove.Dive => "dives (pull up)",
            AnglingMove.Run => "runs (give it line)",
            AnglingMove.Jump => "jumps (let go before it lands)",
            AnglingMove.Latch => "latches to stones (go slack)",
            _ => m.ToString(),
        };

        /// <summary>"0:1187:1014" as a tile.</summary>
        private static string Place(string key)
        {
            string[] p = key.Split(':');
            return p.Length == 3 ? p[1] + ", " + p[2] : key;
        }

        private static string Weight(int grams) =>
            grams >= 1000 ? (grams / 1000f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " kg" : grams + " g";
    }
}
