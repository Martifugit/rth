using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Rth.Shared.Angling;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Trophy spots (docs/fishing.md 3.1): every placed object of a species' spot type is one of its
    /// spots. A few of each species' spots are active at a time, each for a while; when one ends,
    /// another of its inactive spots becomes active, so the count stays the same, for everyone.
    /// After a few catches at a spot its fish are spooked for that player alone. Players see the
    /// active spots near them that aren't spooked for them, with how long each has left, as the
    /// "anglingSpots" state, and the spots they see go into their fishing log.
    /// </summary>
    public sealed class AnglingSpotSystem : GameSystem
    {
        /// <summary>One placed trophy spot's live state.</summary>
        public sealed class Spot
        {
            public Spot(PlacedObject placed, string species)
            {
                Placed = placed;
                Species = species;
            }

            public PlacedObject Placed { get; }
            public string Species { get; set; }
            public bool Active;
            public uint ActiveFrom;
            public uint ActiveUntil;
            /// <summary>Counts up with each activation, so a player's catches belong to one stretch.</summary>
            public int Stretch;
        }

        /// <summary>A player's catches at spots (this stretch) and the spots spooked for them.</summary>
        public sealed class Spooking
        {
            public readonly Dictionary<Spot, (int Stretch, int Catches)> Catches = new Dictionary<Spot, (int, int)>();
            public readonly Dictionary<Spot, uint> SpookedUntil = new Dictionary<Spot, uint>();
            [JsonIgnore] public string LastSent = "";
        }

        private readonly List<Spot> _spots = new List<Spot>();
        private bool _started;

        public IReadOnlyList<Spot> Spots => _spots;

        protected override void OnAttach() => Collect();

        public override void OnContentChanged()
        {
            // Keep the spots that are still a species' and their timers; add new ones.
            Collect();
        }

        private void Collect()
        {
            Dictionary<string, string> bySpotType = World.Content.Angling.Species.ToDictionary(s => s.SpotType, s => s.Id);
            var keep = _spots.ToDictionary(s => s.Placed);
            _spots.Clear();
            foreach (PlacedObject placed in World.Get<ObjectSystem>().Placed)
            {
                if (!bySpotType.TryGetValue(placed.Type, out string? species)) continue;
                if (keep.TryGetValue(placed, out Spot? had))
                {
                    had.Species = species;
                    _spots.Add(had);
                }
                else _spots.Add(new Spot(placed, species));
            }
        }

        public AnglingSpeciesDef? SpeciesOf(Spot spot) => World.Content.Angling.Species.FirstOrDefault(s => s.Id == spot.Species);

        public Spot? SpotAt(PlacedObject placed) => _spots.FirstOrDefault(s => ReferenceEquals(s.Placed, placed));

        public override void OnTimers()
        {
            uint now = World.CurrentTick;
            foreach (AnglingSpeciesDef species in World.Content.Angling.Species)
            {
                List<Spot> spots = _spots.Where(s => s.Species == species.Id).ToList();
                if (spots.Count == 0) continue;
                foreach (Spot s in spots.Where(s => s.Active && now >= s.ActiveUntil).ToList())
                {
                    s.Active = false;
                    Activate(species, spots, now, exclude: s);
                }
                int want = Math.Min(species.Active, spots.Count);
                while (spots.Count(s => s.Active) < want)
                    if (!Activate(species, spots, now, exclude: null, staggered: !_started)) break;
            }
            _started = true;
        }

        /// <summary>Makes one of the species' inactive spots active (not the one that just ended, if there's another).</summary>
        private bool Activate(AnglingSpeciesDef species, List<Spot> spots, uint now, Spot? exclude, bool staggered = false)
        {
            List<Spot> idle = spots.Where(s => !s.Active && s != exclude).ToList();
            if (idle.Count == 0) idle = spots.Where(s => !s.Active).ToList();
            if (idle.Count == 0) return false;
            Spot chosen = idle[World.Random.Next(idle.Count)];
            int length = World.Random.Next(species.ActiveMinTicks, species.ActiveMaxTicks + 1);
            chosen.Active = true;
            chosen.Stretch++;
            // At startup spots are part-way through, so they don't all end together.
            chosen.ActiveFrom = staggered ? now - (uint)Math.Min(now, (uint)World.Random.Next(0, length)) : now;
            chosen.ActiveUntil = chosen.ActiveFrom + (uint)length;
            if (chosen.ActiveUntil <= now) chosen.ActiveUntil = now + 1;
            return true;
        }

        /// <summary>How long a spot has left: 2 plenty, 1 thinning, 0 nearly gone.</summary>
        public int Phase(Spot spot)
        {
            uint now = World.CurrentTick;
            double total = Math.Max(1, spot.ActiveUntil - spot.ActiveFrom);
            double left = spot.ActiveUntil > now ? (spot.ActiveUntil - now) / total : 0;
            return left > 0.5 ? 2 : left > 0.2 ? 1 : 0;
        }

        public bool IsSpooked(Player player, Spot spot) =>
            player.Component<Spooking>().SpookedUntil.TryGetValue(spot, out uint until) && World.CurrentTick < until;

        /// <summary>Whether the player can fish the spot now: active, and not spooked for them.</summary>
        public bool Workable(Player player, Spot spot) => spot.Active && !IsSpooked(player, spot);

        /// <summary>A catch at a spot; returns true if its fish are now spooked for the player.</summary>
        public bool RecordCatch(Player player, Spot spot)
        {
            AnglingSpeciesDef? species = SpeciesOf(spot);
            if (species == null) return false;
            Spooking spooking = player.Component<Spooking>();
            int catches = spooking.Catches.TryGetValue(spot, out var had) && had.Stretch == spot.Stretch ? had.Catches + 1 : 1;
            if (catches < species.SpookAfter)
            {
                spooking.Catches[spot] = (spot.Stretch, catches);
                return false;
            }
            spooking.Catches.Remove(spot);
            spooking.SpookedUntil[spot] = World.CurrentTick + (uint)species.SpookTicks;
            return true;
        }

        /// <summary>The spot a market names for a species: an active one, the same for everyone during its stretch.</summary>
        public Spot? Lead(string species) =>
            _spots.Where(s => s.Species == species && s.Active).OrderBy(s => s.ActiveFrom).ThenBy(s => s.Placed.X).ThenBy(s => s.Placed.Y).FirstOrDefault();

        public override void SendState(Player player)
        {
            var state = new AnglingSpotsState();
            AnglingLogSystem? log = World.Find<AnglingLogSystem>();
            foreach (Spot s in _spots)
            {
                if (!Workable(player, s) || !World.CanSee(player, s.Placed.Plane, s.Placed.Tile)) continue;
                state.Spots.Add(new AnglingSpotView { Type = s.Placed.Type, Species = s.Species, Plane = s.Placed.Plane, X = s.Placed.X, Y = s.Placed.Y, Phase = Phase(s) });
                log?.SawSpot(player, s.Species, s.Placed);
            }
            Spooking spooking = player.Component<Spooking>();
            JsonStateMessage message = JsonStateMessage.Of(AnglingSpotsState.StateKey, state);
            if (message.Json == spooking.LastSent) return;
            spooking.LastSent = message.Json;
            player.Connection.Send(message);
        }
    }
}
