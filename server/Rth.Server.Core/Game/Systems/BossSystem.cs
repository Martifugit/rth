using System;
using System.Collections.Generic;
using Rth.Server.Content;
using Rth.Shared.Net;
using Rth.Shared.Progress;

namespace Rth.Server.Game
{
    /// <summary>A boss run began.</summary>
    public sealed class BossRunStarted
    {
        public BossRunStarted(Player player, BossDef boss)
        {
            Player = player;
            Boss = boss;
        }

        public Player Player { get; }
        public BossDef Boss { get; }
    }

    /// <summary>A boss run ended: won (a completion) or lost (failed, died, left), after this many ticks.</summary>
    public sealed class BossRunEnded
    {
        public BossRunEnded(Player player, BossDef boss, bool won, int ticks)
        {
            Player = player;
            Boss = boss;
            Won = won;
            Ticks = ticks;
        }

        public Player Player { get; }
        public BossDef Boss { get; }
        public bool Won { get; }
        public int Ticks { get; }
    }

    /// <summary>A player's run in progress.</summary>
    public sealed class PlayerBossRun
    {
        public BossDef? Boss;
        public uint StartTick;
        public List<EncounterBar> Bars = new List<EncounterBar>();
        public bool Dirty = true;
    }

    /// <summary>
    /// Boss runs, for every boss (fights and skilling bosses alike): an encounter starts a run and
    /// ends it, won or lost; this system times it, pays out its rewards on a win and publishes
    /// <see cref="BossRunStarted"/> / <see cref="BossRunEnded"/> (the bestiary keeps completions,
    /// best times and drops). Dying or logging out ends a run as lost. Clients get the run in
    /// progress as the "bossRun" state, for the timer.
    /// </summary>
    public sealed class BossSystem : GameSystem
    {
        public const string StateKey = "bossRun";

        protected override void OnAttach()
        {
            World.Events.Subscribe<EntityDied>(e =>
            {
                if (e.Victim is Player p && Of(p).Boss != null) EndRun(p, won: false);
            });
        }

        public PlayerBossRun Of(Player player) => player.Component<PlayerBossRun>();

        public bool InRun(Player player) => Of(player).Boss != null;

        /// <summary>Starts a run (ending any other as lost). Returns false for an unknown boss.</summary>
        public bool StartRun(Player player, string bossId)
        {
            if (!World.Content.Bosses.TryGetValue(bossId, out BossDef? boss)) return false;
            if (InRun(player)) EndRun(player, won: false);
            PlayerBossRun run = Of(player);
            run.Boss = boss;
            run.StartTick = World.CurrentTick;
            run.Bars = new List<EncounterBar>();
            run.Dirty = true;
            World.Events.Publish(new BossRunStarted(player, boss));
            return true;
        }

        /// <summary>Sets one of the run's bars (adding it the first time); the client is told when it changes.</summary>
        public void SetBar(Player player, string id, string label, float value, string colour, bool danger = false)
        {
            PlayerBossRun run = Of(player);
            if (run.Boss == null) return;
            value = Math.Max(0f, Math.Min(1f, value));
            EncounterBar? bar = run.Bars.Find(b => b.Id == id);
            if (bar == null) run.Bars.Add(bar = new EncounterBar { Id = id });
            if (bar.Label == label && Math.Abs(bar.Value - value) < 0.002f && bar.Colour == colour && bar.Danger == danger) return;
            bar.Label = label;
            bar.Value = value;
            bar.Colour = colour;
            bar.Danger = danger;
            run.Dirty = true;
        }

        /// <summary>Ends the run in progress: a win pays its rewards and counts as a completion.</summary>
        public void EndRun(Player player, bool won)
        {
            PlayerBossRun run = Of(player);
            BossDef? boss = run.Boss;
            if (boss == null) return;
            int ticks = (int)(World.CurrentTick - run.StartTick);
            run.Boss = null;
            run.Dirty = true;

            BossRecord? before = World.Find<BestiarySystem>()?.Boss(player, boss.Id);
            int best = before?.BestTicks ?? 0;
            World.Events.Publish(new BossRunEnded(player, boss, won, ticks));
            if (!won)
            {
                World.Tell(player, TextKind.Warning, boss.Name + ": the run is lost after " + Duration(ticks) + ".");
                return;
            }
            int count = World.Find<BestiarySystem>()?.Boss(player, boss.Id).Completions ?? 1;
            bool record = best == 0 || ticks < best;
            World.Tell(player, TextKind.Info, "Your completion count for " + boss.Name + " is: " + count + ".");
            World.Tell(player, TextKind.Info, "Duration: " + Duration(ticks) + (record ? " (new personal best)" : ". Personal best: " + Duration(best)) + ".");
            if (boss.Rewards != null) PayRewards(player, boss);
        }

        private void PayRewards(Player player, BossDef boss)
        {
            ItemSystem? items = World.Find<ItemSystem>();
            if (items == null) return;
            List<Drop> drops = DropRoller.Roll(World.Content, boss.Rewards!, World.Random);
            var gained = new List<Drop>();
            foreach (Drop d in drops)
            {
                // What doesn't fit falls at the player's feet, like any loot.
                int left = items.Give(player, d.ItemId, d.Count);
                if (left > 0) World.Find<GroundItemSystem>()?.Spawn(d.ItemId, left, player.Plane, player.Position, player);
                gained.Add(d);
            }
            if (gained.Count > 0) World.Events.Publish(new ItemsObtained(player, "boss:" + boss.Id, gained));
        }

        /// <summary>A duration in ticks as "1:23.4".</summary>
        public static string Duration(int ticks)
        {
            double seconds = ticks * 0.6;
            int minutes = (int)(seconds / 60);
            return minutes + ":" + (seconds - minutes * 60).ToString("00.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Leaving loses the run, before the final save so the bestiary records it.</summary>
        public override void OnLeaving(Player player)
        {
            if (InRun(player)) EndRun(player, won: false);
        }

        public override void SendState(Player player)
        {
            PlayerBossRun run = Of(player);
            if (!run.Dirty) return;
            run.Dirty = false;
            var state = new BossRunState();
            if (run.Boss != null)
            {
                state.Boss = run.Boss.Id;
                state.StartTick = run.StartTick;
                state.BestTicks = World.Find<BestiarySystem>()?.Boss(player, run.Boss.Id).BestTicks ?? 0;
                state.Bars = run.Bars;
            }
            player.Connection.Send(JsonStateMessage.Of(StateKey, state));
        }
    }
}
