using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Rth.Server.Persistence;
using Rth.Shared.Angling;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    public sealed class PlayerAnglingLog
    {
        public AnglingLogState State = new AnglingLogState();
        [JsonIgnore] public bool Dirty = true;
    }

    /// <summary>
    /// The fishing log (docs/fishing.md 5.1): each species' catches, revised fish, personal best and
    /// losses; the trophy spots seen and a market's lead; and the angler's notes: the moves seen,
    /// the lures landed on, and notes unlocked by people (dialogue action <c>anglingNote</c>, id
    /// "species:note"). Notes for a number of catches are worked out from the count. Dialogue
    /// condition <c>anglingCaught</c> (id: species, count). Sent as "anglingLog".
    /// </summary>
    public sealed class AnglingLogSystem : GameSystem
    {
        private const string SaveSection = "anglingLog";

        public PlayerAnglingLog Of(Player player) => player.Component<PlayerAnglingLog>();

        public AnglingSpeciesLog Species(Player player, string species)
        {
            Dictionary<string, AnglingSpeciesLog> all = Of(player).State.Species;
            if (!all.TryGetValue(species, out AnglingSpeciesLog? log)) all[species] = log = new AnglingSpeciesLog();
            return log;
        }

        public static string SpotKey(PlacedObject spot) => spot.Plane + ":" + spot.X + ":" + spot.Y;

        protected override void OnAttach()
        {
            DialogueSystem? dialogue = World.Find<DialogueSystem>();
            if (dialogue == null) return;
            dialogue.RegisterCondition("anglingCaught", (p, c, _) => Species(p, c.Id ?? "").Caught >= c.Count);
            dialogue.RegisterAction("anglingNote", (p, a, _) =>
            {
                string[] parts = (a.Id ?? "").Split(':');
                if (parts.Length == 2) UnlockNote(p, parts[0], parts[1]);
            });
        }

        /// <summary>A landed fish: returns whether it is a new personal best.</summary>
        public bool RecordCatch(Player player, string species, int grams, bool revised, int moves, string? lure)
        {
            AnglingSpeciesLog log = Species(player, species);
            bool best = grams > log.BestGrams;
            log.Caught++;
            if (revised) log.Revised++;
            if (best) log.BestGrams = grams;
            log.Moves |= moves;
            if (lure != null && !log.Lures.Contains(lure)) log.Lures.Add(lure);
            Of(player).Dirty = true;
            return best;
        }

        public void RecordLoss(Player player, string species, int moves)
        {
            AnglingSpeciesLog log = Species(player, species);
            log.Lost++;
            log.Moves |= moves;
            Of(player).Dirty = true;
        }

        public void SawSpot(Player player, string species, PlacedObject spot)
        {
            AnglingSpeciesLog log = Species(player, species);
            string key = SpotKey(spot);
            if (log.Spots.Contains(key)) return;
            log.Spots.Add(key);
            Of(player).Dirty = true;
        }

        public void SetLead(Player player, string species, PlacedObject spot)
        {
            SawSpot(player, species, spot);
            AnglingSpeciesLog log = Species(player, species);
            string key = SpotKey(spot);
            if (log.Lead == key) return;
            log.Lead = key;
            Of(player).Dirty = true;
        }

        public void UnlockNote(Player player, string species, string note)
        {
            AnglingSpeciesLog log = Species(player, species);
            if (log.Notes.Contains(note)) return;
            log.Notes.Add(note);
            Of(player).Dirty = true;
            AnglingSpeciesDef? def = World.Content.Angling.Species.FirstOrDefault(s => s.Id == species);
            AnglingNoteDef? text = def?.Notes.FirstOrDefault(n => n.Id == note);
            if (def != null && text != null) World.Tell(player, TextKind.Info, "Your fishing log has a new note on " + def.Name.ToLowerInvariant() + ".");
        }

        /// <summary>The notes a player has on a species: those for catches made, and those given.</summary>
        public IEnumerable<AnglingNoteDef> Notes(Player player, AnglingSpeciesDef species)
        {
            AnglingSpeciesLog log = Species(player, species.Id);
            return species.Notes.Where(n => n.Catches > 0 ? log.Caught >= n.Catches : log.Notes.Contains(n.Id));
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            AnglingLogState? saved = save?.Section<AnglingLogState>(SaveSection);
            if (saved != null) Of(player).State = saved;
            Of(player).Dirty = true;
        }

        public override void OnSave(Player player, PlayerSave save) => save.SetSection(SaveSection, Of(player).State);

        public override void OnContentChanged()
        {
            foreach (Player p in World.Players) Of(p).Dirty = true;
        }

        public override void SendState(Player player)
        {
            PlayerAnglingLog log = Of(player);
            if (!log.Dirty) return;
            log.Dirty = false;
            player.Connection.Send(JsonStateMessage.Of(AnglingLogState.StateKey, log.State));
        }
    }
}
