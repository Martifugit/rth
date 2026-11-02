using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rth.Server.Content;
using Rth.Server.Items;
using Rth.Server.Persistence;
using Rth.Shared.Net;
using Rth.Shared.Progress;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>A player's run at a trial, attached as a component.</summary>
    public sealed class TrialRun
    {
        public TrialDef? Def;
        public uint StartTick;
        /// <summary>Objects whose once-per-attempt effects this run has had (lanterns kindled).</summary>
        public HashSet<PlacedObject> Used = new HashSet<PlacedObject>();
        /// <summary>The last finished run's time, for {trialTime}.</summary>
        public int LastTicks;
        public Dictionary<string, int> Best = new Dictionary<string, int>();
        public bool Dirty = true;

        public bool Running => Def != null;
    }

    /// <summary>
    /// Trials (trials.json): timed runs through regions, begun at a gate's "trial" action. While one
    /// runs, the trial's drain takes a share of max blood and blood doesn't return by itself; objects
    /// whose action says <c>oncePer: "trial"</c> (lanterns) give their effects once an attempt.
    /// Dying, leaving the regions or logging out loses the run; the dialogue action
    /// <c>trialWin</c> wins it. Someone found in a trial's regions with no run is sent to its
    /// <see cref="TrialDef.ExitTo"/>.
    ///
    /// Extension points: dialogue condition <c>trialRunning</c> (id: a trial, or any), actions
    /// <c>trialWin</c> / <c>trialLose</c> (id), tokens <c>{trialTime}</c> and <c>{trialBest:id}</c>;
    /// events <see cref="TrialStarted"/> and <see cref="TrialEnded"/>; the "trial" state.
    /// </summary>
    public sealed class TrialSystem : GameSystem, IStatusSource
    {
        public const string StateKey = "trial";
        private const string SaveSection = "trials";

        public TrialRun Of(Player player) => player.Component<TrialRun>();

        public bool Running(Player player, string? trial = null) =>
            Of(player).Def is TrialDef def && (trial == null || def.Id == trial);

        protected override void OnAttach()
        {
            World.Events.Subscribe<EntityDied>(e =>
            {
                if (e.Victim is Player p && Of(p).Running) End(p, won: false);
            });

            ObjectSystem objects = World.Get<ObjectSystem>();
            objects.RegisterAction("trial", (player, placed, def) =>
                World.StartInteraction(player, new ObjectOnceInteraction(placed, p => Begin(p, def.Id))));
            objects.RegisterOnceScope("trial", (player, placed) => Of(player).Running && Of(player).Used.Add(placed) && MarkDirty(player));

            DialogueSystem dialogue = World.Get<DialogueSystem>();
            dialogue.RegisterCondition("trialRunning", (p, c, _) => Running(p, c.Id));
            dialogue.RegisterAction("trialWin", (p, a, _) => { if (Running(p, a.Id)) End(p, won: true); });
            dialogue.RegisterAction("trialLose", (p, a, _) => { if (Running(p, a.Id)) End(p, won: false); });
            dialogue.RegisterToken("trialTime", (p, _, _) => BossSystem.Duration(Of(p).LastTicks));
            dialogue.RegisterToken("trialBest", (p, arg, _) =>
                arg != null && Of(p).Best.TryGetValue(arg, out int best) ? BossSystem.Duration(best) : "none yet");
        }

        private bool MarkDirty(Player player)
        {
            Of(player).Dirty = true;
            return true;
        }

        /// <summary>The trial whose gate is this object type.</summary>
        public TrialDef? ByGate(string objectType) => World.Content.Trials.Values.FirstOrDefault(t => t.Gate == objectType);

        /// <summary>Begins the trial gated by an object type, if its conditions pass. Returns whether it began.</summary>
        public bool Begin(Player player, string gateType)
        {
            TrialDef? def = ByGate(gateType);
            if (def == null || player.IsDead) return false;
            TrialRun run = Of(player);
            if (run.Running) return false;

            DialogueSystem dialogue = World.Get<DialogueSystem>();
            if (def.Conditions.Any(c => !dialogue.Test(player, c, null)))
            {
                World.Tell(player, TextKind.Warning, def.LockedMessage ?? "You can't go that way.");
                return false;
            }
            if (def.EmptyHanded && !EmptyHanded(player))
            {
                World.Tell(player, TextKind.Warning, def.EmptyHandedMessage ?? "You must go empty-handed.");
                return false;
            }

            run.Def = def;
            run.StartTick = World.CurrentTick;
            run.Used.Clear();
            run.Dirty = true;
            World.Find<DialogueSystem>()?.End(player);
            if (def.Start != null) World.Get<DeathSystem>().Move(player, def.Start.Plane, new TileCoord(def.Start.X, def.Start.Y));
            if (def.StartMessage != null) World.Tell(player, TextKind.Info, def.StartMessage);
            World.Events.Publish(new TrialStarted(player, def.Id));
            return true;
        }

        private bool EmptyHanded(Player player)
        {
            PlayerItems items = World.Get<ItemSystem>().Of(player);
            for (int i = 0; i < items.Inventory.Size; i++) if (!items.Inventory.IsEmpty(i)) return false;
            for (int i = 0; i < items.Equipment.Size; i++) if (!items.Equipment.IsEmpty(i)) return false;
            return true;
        }

        /// <summary>Ends a player's run, won or lost.</summary>
        public void End(Player player, bool won)
        {
            TrialRun run = Of(player);
            if (!(run.Def is TrialDef def)) return;
            int ticks = (int)(World.CurrentTick - run.StartTick);
            run.Def = null;
            run.Used.Clear();
            run.Dirty = true;
            run.LastTicks = ticks;
            if (won)
            {
                if (!run.Best.TryGetValue(def.Id, out int best) || ticks < best) run.Best[def.Id] = ticks;
                if (def.WinMessage != null)
                    World.Tell(player, TextKind.Info, def.WinMessage
                        .Replace("{time}", BossSystem.Duration(ticks))
                        .Replace("{best}", BossSystem.Duration(run.Best[def.Id])));
            }
            else if (def.FailMessage != null) World.Tell(player, TextKind.Warning, def.FailMessage);
            World.Events.Publish(new TrialEnded(player, def.Id, won, ticks));
        }

        public override void BeforePlayer(Player player)
        {
            if (Of(player).Def is TrialDef { WalkOnly: true }) player.Running = false;
        }

        public override void AfterPlayer(Player player)
        {
            if (player.IsDead) return;
            TrialRun run = Of(player);
            string? region = World.Find<RegionSystem>()?.RegionOf(player)?.Name;

            if (!(run.Def is TrialDef def))
            {
                // In a trial's ground with no run (logged back in mid-run): out you go.
                if (region == null) return;
                TrialDef? holding = World.Content.Trials.Values.FirstOrDefault(t => t.Regions.Contains(region));
                if (holding == null) return;
                DeathSystem death = World.Get<DeathSystem>();
                (int plane, TileCoord tile) = holding.ExitTo != null ? death.PlaceOrSpawn(holding.ExitTo) : death.WakePlace;
                death.Move(player, plane, tile);
                return;
            }

            if (region == null || !def.Regions.Contains(region))
            {
                End(player, won: false);
                return;
            }

            if (def.Drain is TrialDrain drain)
            {
                uint now = World.CurrentTick;
                player.BloodRegenHeldUntilTick = now + 2;
                uint elapsed = now - run.StartTick;
                if (elapsed > 0 && elapsed % (uint)drain.EveryTicks == 0 && drain.Percent > 0)
                {
                    int amount = Math.Max(1, (int)Math.Round(player.MaxBlood * drain.Percent / 100.0));
                    World.Get<CombatSystem>().Damage(player, amount, null, HitKind.Bleed);
                }
            }
        }

        public void AddStatuses(Player player, List<StatusState> statuses)
        {
            if (Of(player).Def is TrialDef def && def.Status != null)
                statuses.Add(new StatusState { Id = def.Status, Magnitude = 0, EndTick = 0 });
        }

        public override void SendState(Player player)
        {
            TrialRun run = Of(player);
            if (!run.Dirty) return;
            run.Dirty = false;
            var state = new TrialState();
            if (run.Def is TrialDef def)
            {
                state.Trial = def.Id;
                state.Title = def.Title;
                state.StartTick = run.StartTick;
                state.BestTicks = run.Best.TryGetValue(def.Id, out int best) ? best : 0;
                state.Used = run.Used.Select(o => new[] { o.Plane, o.X, o.Y }).ToList();
            }
            player.Connection.Send(JsonStateMessage.Of(StateKey, state));
        }

        /// <summary>Logging out loses the run (before the final save).</summary>
        public override void OnLeaving(Player player)
        {
            if (Of(player).Running) End(player, won: false);
        }

        private sealed class TrialsSave
        {
            public Dictionary<string, int> Best = new Dictionary<string, int>();
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            TrialsSave? saved = save?.Section<TrialsSave>(SaveSection);
            if (saved?.Best != null) Of(player).Best = saved.Best;
        }

        public override void OnSave(Player player, PlayerSave save)
        {
            TrialRun run = Of(player);
            if (run.Best.Count > 0) save.SetSection(SaveSection, new TrialsSave { Best = run.Best });
        }
    }
}
