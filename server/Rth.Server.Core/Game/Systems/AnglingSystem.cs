using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Content;
using Rth.Shared.Angling;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;
using Rth.Server.Items;

namespace Rth.Server.Game
{
    /// <summary>
    /// Trophy angling (docs/fishing.md 3, 5, 6): casting at a trophy spot (the "cast" action), the
    /// bite, the fight and the catch. The server rolls the fish and runs its own
    /// <see cref="AnglingFight"/>, stepped through the inputs the client sends as the "angling"
    /// intent (run-length batches, <see cref="AnglingInputs"/>; "quit" lets the line go); the
    /// client plays the same fight live from the "anglingFight" state. A landed fish gives XP by
    /// its size, a trophy of its size class and an entry in the log; every fight wears the rig.
    /// Fish markets take trophies for fish boxes (NPC action "hand-in"), and name a spot (dialogue
    /// token <c>{anglingLead:species}</c>). Items with <c>open</c> (fish boxes) are opened here too.
    /// </summary>
    public sealed class AnglingSystem : GameSystem
    {
        public const string IntentKind = "angling";
        public const string HandInAction = "hand-in";

        /// <summary>A player's fight: the server's copy, the fish, and where it bit.</summary>
        public sealed class Hooked
        {
            public int Id;
            public AnglingFight Fight = null!;
            public AnglingFightState State = new AnglingFightState();
            public AnglingSpeciesDef Species = null!;
            public AnglingSpotSystem.Spot Spot = null!;
            public int Roll;
            public uint Started;
            public uint LastInput;
            public string? Lure;
            public string Rod = "";
        }

        /// <summary>The player's fight now (null: none) and the last fight state sent.</summary>
        public sealed class PlayerAngling
        {
            public Hooked? Fight;
            public int NextId;
            /// <summary>The last fight's state, kept until the next starts so the client can show how it ended.</summary>
            public AnglingFightState Last = new AnglingFightState();
            public string LastSent = "";
        }

        private AnglingFile Rules => World.Content.Angling;
        private readonly List<byte> _batch = new List<byte>();

        public PlayerAngling Of(Player player) => player.Component<PlayerAngling>();

        protected override void OnAttach()
        {
            World.Get<ObjectSystem>().RegisterAction(AnglingContent.CastAction, (player, placed, def) =>
                World.StartInteraction(player, new AnglingCastInteraction(placed)));
            World.Get<IntentSystem>().Register(IntentKind, Intent);
            World.Get<ItemSystem>().RegisterAction(ItemActions.Open, (player, use) => Open(player, use));
            World.Get<NpcSystem>().RegisterAction(HandInAction, (player, npc) => HandIn(player));
            World.Find<DialogueSystem>()?.RegisterToken("anglingLead", (player, arg, npc) => LeadText(player, arg ?? "", npc));
        }

        private AnglingSpotSystem Spots => World.Get<AnglingSpotSystem>();
        private AnglingTackleSystem Tackle => World.Get<AnglingTackleSystem>();

        // ------------------------------------------------------------------ casting

        /// <summary>Why the player can't cast at this spot now, or null.</summary>
        public string? Problem(Player player, AnglingSpotSystem.Spot spot)
        {
            AnglingSpeciesDef? species = Spots.SpeciesOf(spot);
            if (species == null || !Spots.Workable(player, spot))
                return spot.Active ? "The fish here are wary of you. Try elsewhere for a while." : "The water is quiet here.";
            int level = World.Find<SkillSystem>()?.Level(player, Rules.Skill) ?? 1;
            if (level < species.Level) return species.Name + " requires level " + species.Level + " " + World.Content.SkillName(Rules.Skill) + " to hook.";
            ItemDef? rod = Tackle.Rod(player);
            if (rod == null) return "You need a rod, in your hand or your pack, to fish for trophies.";
            ItemDef? lure = Tackle.Part(player, AnglingSlot.Lure);
            if (lure?.Tackle == null) return "Your " + rod.Name.ToLowerInvariant() + " needs a lure: right-click it and choose Rig.";
            if (!AnglingRules.Bites(lure.Tackle, species)) return "Nothing here will take your " + lure.Name.ToLowerInvariant() + ".";
            ItemDef? line = Tackle.Part(player, AnglingSlot.Line);
            int thickness = line?.Tackle?.Thickness ?? 0;
            if (species.MaxThickness > 0 && thickness > species.MaxThickness)
                return "The " + species.Name.ToLowerInvariant() + " see your line and won't come near it. Try a thinner line.";
            if (!World.Get<ItemSystem>().CanGive(player, AnglingContent.TrophyId(species.Id, Rules.SizeClasses.Last().Id), 1))
                return "Your pack is full. Take your trophies to a fish market.";
            return null;
        }

        /// <summary>A bite: rolls the fish and starts the fight, on the server and (by its state) the client.</summary>
        internal void Hook(Player player, AnglingSpotSystem.Spot spot)
        {
            AnglingSpeciesDef species = Spots.SpeciesOf(spot)!;
            PlayerAngling angling = Of(player);
            bool revised = World.Random.NextDouble() < Rules.RevisedChance;
            int roll = revised ? 1000 : AnglingRules.SizeRoll(World.Random.NextDouble(), species.SizeSkew);
            int level = World.Find<SkillSystem>()?.Level(player, Rules.Skill) ?? 1;
            AnglingRigStats rig = Tackle.Stats(player);
            rig.ReelBonus = AnglingRules.ReelBonus(Rules, level, species);
            var fish = new AnglingFish
            {
                Fight = species.Fight,
                Energy = AnglingRules.Energy(Rules, species, roll, revised),
                Seed = (uint)World.Random.Next(1, int.MaxValue),
                Revised = revised,
            };
            uint now = World.CurrentTick;
            var hooked = new Hooked
            {
                Id = ++angling.NextId,
                Fight = new AnglingFight(fish, rig),
                Species = species,
                Spot = spot,
                Roll = roll,
                Started = now,
                LastInput = now,
                Lure = Tackle.Part(player, AnglingSlot.Lure)?.Id,
                Rod = Tackle.Rod(player)?.Id ?? "",
            };
            hooked.State = new AnglingFightState
            {
                Id = hooked.Id, Species = species.Id, Fight = species.Fight, Energy = fish.Energy, Seed = fish.Seed, Revised = revised, Rig = rig,
                Plane = spot.Placed.Plane, X = spot.Placed.X, Y = spot.Placed.Y,
            };
            angling.Fight = hooked;
            World.Tell(player, TextKind.Info, revised ? "Something takes the lure... something not quite right." : "Something takes the lure!");
            World.Events.Publish(new FishHooked(player, species.Id, revised));
        }

        // ------------------------------------------------------------------ the fight

        private void Intent(Player player, string arg)
        {
            Hooked? hooked = Of(player).Fight;
            if (hooked == null) return;
            if (arg == "quit")
            {
                Resolve(player, AnglingOutcome.Escaped, "You let the line go.");
                return;
            }
            if (!AnglingInputs.TryDecode(arg, out int start, _batch)) return;
            AnglingFight fight = hooked.Fight;
            // Real time is the limit: no more steps than have passed since the bite, with a little room.
            int allowed = (int)(World.CurrentTick - hooked.Started + 2) * AnglingFight.StepsPerTick + 40;
            if (start > fight.Steps) return;     // a gap: wait for the batch that fills it
            for (int i = fight.Steps - start; i < _batch.Count && !fight.Over && fight.Steps < allowed; i++) fight.Step(_batch[i]);
            hooked.LastInput = World.CurrentTick;
            if (fight.Over) Resolve(player, fight.Outcome, null);
        }

        public override void AfterPlayer(Player player)
        {
            Hooked? hooked = Of(player).Fight;
            if (hooked != null && World.CurrentTick - hooked.LastInput > (uint)Math.Max(1, Rules.QuietTicks))
                Resolve(player, AnglingOutcome.Escaped, "The fish swims off with nothing to stop it.");
        }

        /// <summary>The fight ends: the catch (trophy, XP, log, spooking) or the loss; the rig wears either way.</summary>
        public void Resolve(Player player, AnglingOutcome outcome, string? message)
        {
            PlayerAngling angling = Of(player);
            Hooked? hooked = angling.Fight;
            if (hooked == null) return;
            angling.Fight = null;
            AnglingSpeciesDef species = hooked.Species;
            AnglingFight fight = hooked.Fight;
            AnglingFightState state = hooked.State;
            state.Steps = fight.Steps;
            state.Outcome = outcome == AnglingOutcome.None ? AnglingOutcome.Escaped : outcome;
            AnglingLogSystem? log = World.Find<AnglingLogSystem>();
            Tackle.Wear(player, hooked.Rod, fight, species);

            if (state.Outcome == AnglingOutcome.Landed)
            {
                int grams = AnglingRules.Grams(species, hooked.Roll);
                string sizeClass = state.Revised ? AnglingContent.RevisedClass : AnglingRules.ClassOf(Rules, hooked.Roll)?.Id ?? "";
                string trophy = AnglingContent.TrophyId(species.Id, sizeClass);
                ItemSystem items = World.Get<ItemSystem>();
                if (items.Give(player, trophy, 1) > 0) World.Find<GroundItemSystem>()?.Spawn(trophy, 1, player.Plane, player.Position, player);
                World.Find<SkillSystem>()?.AddXp(player, Rules.Skill, AnglingRules.Xp(Rules, species, hooked.Roll, state.Revised));
                bool best = log?.RecordCatch(player, species.Id, grams, state.Revised, fight.Seen, hooked.Lure) ?? false;
                state.Grams = grams;
                state.SizeClass = sizeClass;
                state.PersonalBest = best;
                World.Tell(player, TextKind.Info, "You land " + (state.Revised ? "a revised " : "a ") + species.Name.ToLowerInvariant() + " of " + Weight(grams) + "." + (best ? " New personal best!" : ""));
                World.Events.Publish(new ItemsObtained(player, "angling:" + species.Id, new List<Drop> { new Drop(trophy, 1) }));
                World.Events.Publish(new FishLanded(player, species.Id, grams, sizeClass, state.Revised, best));
                if (Spots.RecordCatch(player, hooked.Spot)) World.Tell(player, TextKind.Info, "The fish here have grown wary of you.");
            }
            else
            {
                log?.RecordLoss(player, species.Id, fight.Seen);
                World.Tell(player, TextKind.Warning, message ?? (state.Outcome == AnglingOutcome.Snapped ? "Your line snaps!"
                    : state.Outcome == AnglingOutcome.HookFree ? "The hook pulls free." : "It gets away."));
                World.Events.Publish(new FishLost(player, species.Id, state.Outcome));
            }
            angling.Last = state;
        }

        /// <summary>"1.25 kg", or "640 g".</summary>
        public static string Weight(int grams) =>
            grams >= 1000 ? (grams / 1000.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " kg" : grams + " g";

        /// <summary>Leaving (or walking off) mid-fight lets the fish go.</summary>
        public override void OnLeaving(Player player)
        {
            if (Of(player).Fight != null) Resolve(player, AnglingOutcome.Escaped, "You let the line go.");
        }

        public override void SendState(Player player)
        {
            PlayerAngling angling = Of(player);
            AnglingFightState state = angling.Fight?.State ?? angling.Last;
            if (angling.Fight != null) state.Steps = angling.Fight.Fight.Steps;
            JsonStateMessage message = JsonStateMessage.Of(AnglingFightState.StateKey, state);
            if (message.Json == angling.LastSent) return;
            angling.LastSent = message.Json;
            player.Connection.Send(message);
        }

        // ------------------------------------------------------------------ markets, boxes, leads

        /// <summary>Hands every ordinary trophy in the pack to a fish market, each for its fish box. Revised fish are kept.</summary>
        public void HandIn(Player player)
        {
            ItemSystem items = World.Get<ItemSystem>();
            ItemContainer pack = items.Of(player).Inventory;
            int given = 0, revised = 0;
            for (int i = 0; i < pack.Size; i++)
            {
                if (pack.IsEmpty(i)) continue;
                ItemDef? def = World.Content.Item(pack[i].ItemId);
                if (def == null || !def.Tags.Contains(AnglingContent.TrophyTag)) continue;
                if (def.Id.EndsWith("_" + AnglingContent.RevisedClass, StringComparison.Ordinal))
                {
                    revised++;
                    continue;
                }
                string box = "fishbox_" + def.Id.Substring("trophy_".Length);
                if (World.Content.Item(box) == null) continue;
                pack.Set(i, box, 1);
                given++;
            }
            if (given > 0) World.Tell(player, TextKind.Info, given == 1 ? "The fishmonger weighs your trophy and gives you a box of fish for it." : "The fishmonger weighs your " + given + " trophies and gives you a box of fish for each.");
            else World.Tell(player, TextKind.Info, "You have no trophies to hand in.");
            if (revised > 0) World.Tell(player, TextKind.Info, "The fishmonger won't touch your revised fish: \"Take that to the collector, not me.\"");
        }

        /// <summary>Opens an item for what's inside: it's used up, and each roll picks an entry by weight.</summary>
        public void Open(Player player, ItemUse use)
        {
            OpenDef? open = use.Def.Open;
            if (open == null) return;
            ItemSystem items = World.Get<ItemSystem>();
            var got = new Dictionary<string, int>();
            int total = open.Entries.Sum(e => Math.Max(0, e.Weight));
            for (int r = 0; r < open.Rolls && total > 0; r++)
            {
                int roll = World.Random.Next(total);
                foreach (OpenEntry e in open.Entries)
                {
                    if ((roll -= Math.Max(0, e.Weight)) >= 0) continue;
                    got[e.Item] = (got.TryGetValue(e.Item, out int had) ? had : 0) + World.Random.Next(e.Min, Math.Max(e.Min, e.Max) + 1);
                    break;
                }
            }
            // Room: the opened item's slot comes free first.
            use.Container.RemoveAt(use.Slot, 1);
            if (got.Any(pair => !items.CanGive(player, pair.Key, pair.Value)))
            {
                items.Give(player, use.Def.Id, 1);
                World.Tell(player, TextKind.Warning, "You need more room in your pack to open it.");
                return;
            }
            var drops = new List<Drop>();
            foreach (var pair in got)
            {
                items.Give(player, pair.Key, pair.Value);
                drops.Add(new Drop(pair.Key, pair.Value));
            }
            World.Tell(player, TextKind.Info, "You " + open.Verb.ToLowerInvariant() + " the " + use.Def.Name.ToLowerInvariant() + ".");
            World.Events.Publish(new ItemsObtained(player, "open:" + use.Def.Id, drops));
        }

        /// <summary>A market's lead for a species: where one of its active spots is from here, and it goes in the log.</summary>
        public string LeadText(Player player, string speciesId, Npc? npc)
        {
            AnglingSpeciesDef? species = Rules.Species.FirstOrDefault(s => s.Id == speciesId);
            if (species == null) return "";
            AnglingSpotSystem.Spot? spot = Spots.Lead(speciesId);
            if (spot == null) return "Nobody's seen " + species.Name.ToLowerInvariant() + " rising lately.";
            World.Find<AnglingLogSystem>()?.SetLead(player, speciesId, spot.Placed);
            TileCoord from = npc?.Position ?? player.Position;
            int far = from.ChebyshevDistance(spot.Placed.Tile);
            string distance = far < 20 ? "close by" : far < 80 ? "a short walk off" : far < 250 ? "a good walk off" : "a long way off";
            return species.Name + " have been rising " + GatheringSystem.Where(from, spot.Placed.Tile).Replace("to your ", "to the ") + " of here, " + distance;
        }
    }

    /// <summary>
    /// Walks to a trophy spot and fishes it: a cast, a wait for the bite, the fight. One fish a cast:
    /// when the fight ends, landed or lost, so does this (cast again to fish on). Walking away ends
    /// it (and lets a hooked fish go).
    /// </summary>
    public sealed class AnglingCastInteraction : ObjectInteraction
    {
        private bool _started;
        private bool _hooked;
        private uint _biteAt;

        public AnglingCastInteraction(PlacedObject spot) : base(spot) { }

        public override void End(World world, Player player)
        {
            AnglingSystem angling = world.Get<AnglingSystem>();
            if (angling.Of(player).Fight != null) angling.Resolve(player, AnglingOutcome.Escaped, "You let the line go.");
            world.RecalculateStats(player);
        }

        protected override bool Work(World world, Player player)
        {
            AnglingSystem angling = world.Get<AnglingSystem>();
            AnglingSpotSystem spots = world.Get<AnglingSpotSystem>();
            AnglingSpotSystem.Spot? spot = spots.SpotAt(Object);
            if (spot == null) return false;
            if (angling.Of(player).Fight != null) return true;     // fighting: the client's inputs drive it
            if (_hooked) return false;                              // the fight is over: one fish a cast
            AnglingFile rules = world.Content.Angling;
            if (!_started || _biteAt == 0)
            {
                string? problem = angling.Problem(player, spot);
                if (problem != null)
                {
                    world.Tell(player, _started ? TextKind.Info : TextKind.Warning, problem);
                    return false;
                }
                if (!_started)
                {
                    world.Tell(player, TextKind.Info, "You cast your line and wait.");
                    // The rod in hand while fishing (as a gathering tool is); the weapon comes back after.
                    string? rod = world.Get<AnglingTackleSystem>().Rod(player)?.Id;
                    if (rod != null) player.SetHeld(rod);
                }
                _started = true;
                _biteAt = world.CurrentTick + (uint)world.Random.Next(rules.BiteMinTicks, rules.BiteMaxTicks + 1);
                world.Animate(player, "fish_rod", Object.Tile);
                return true;
            }
            if (!spots.Workable(player, spot))
            {
                world.Tell(player, TextKind.Info, spot.Active ? "The fish here have grown wary of you." : "The ripples die away. The fish have moved on.");
                return false;
            }
            if (world.CurrentTick < _biteAt) return true;
            _hooked = true;
            angling.Hook(player, spot);
            return true;
        }
    }
}
