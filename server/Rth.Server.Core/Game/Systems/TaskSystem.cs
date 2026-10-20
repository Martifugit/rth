using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Rth.Server.Content;
using Rth.Server.Persistence;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>A player's current task and their streaks with each task-giver.</summary>
    public sealed class PlayerTask
    {
        public string Giver = "";
        public string Task = "";
        public int Remaining;
        public int Total;
        public Dictionary<string, int> Streaks = new Dictionary<string, int>();
        [JsonIgnore] public bool Dirty = true;

        [JsonIgnore] public bool Active => Remaining > 0 && Giver.Length > 0;
    }

    /// <summary>
    /// Task-givers (tasks.json) hand out one task at a time: butcher N of a carcass, kill N of a
    /// creature. Progress comes from events (<see cref="RecipeCompleted"/>, <see cref="EntityDied"/>),
    /// so a new kind of task only needs a new event subscription. Finishing pays the giver's
    /// points, with a bonus every few tasks in a row. Driven from dialogue: conditions "hasTask",
    /// "canTakeTask"; actions "assignTask", "cancelTask"; tokens {task}, {taskGiver}.
    /// </summary>
    public sealed class TaskSystem : GameSystem
    {
        private const string SaveSection = "task";

        public PlayerTask Of(Player player) => player.Component<PlayerTask>();

        private PointsSystem Points => World.Get<PointsSystem>();

        protected override void OnAttach()
        {
            World.Events.Subscribe<RecipeCompleted>(e => Progress(e.Player, "recipe", e.Recipe.Id));
            World.Events.Subscribe<EntityDied>(e =>
            {
                if (e.Killer is Player p && e.Victim is Npc npc) Progress(p, "kill", npc.Def.Id);
            });

            DialogueSystem dialogue = World.Get<DialogueSystem>();
            dialogue.RegisterCondition("hasTask", (p, c, _) => Of(p).Active && (c.Id == null || Of(p).Giver == c.Id));
            dialogue.RegisterCondition("canTakeTask", (p, c, _) => c.Id != null && WhyNot(p, c.Id) == null);
            dialogue.RegisterAction("assignTask", (p, a, _) =>
            {
                if (a.Id != null) Assign(p, a.Id);
            });
            dialogue.RegisterAction("cancelTask", (p, _, _) => Cancel(p));
            dialogue.RegisterToken("task", (p, _, _) => Describe(p));
            dialogue.RegisterToken("taskGiver", (p, _, _) => World.Content.TaskGivers.TryGetValue(Of(p).Giver, out var g) ? g.Name : "nobody");
            dialogue.RegisterToken("whyNoTask", (p, giver, _) => giver == null ? "" : WhyNot(p, giver) ?? "");
        }

        /// <summary>Why this giver won't hand the player a task right now, or null if they will.</summary>
        public string? WhyNot(Player player, string giverId)
        {
            if (!World.Content.TaskGivers.TryGetValue(giverId, out TaskGiverDef? giver)) return "Nobody gives tasks like that.";
            if (Of(player).Active) return "You already have a task.";
            SkillSystem skills = World.Get<SkillSystem>();
            string? level = skills.Check(player, giver.Requirements, "take tasks from " + giver.Name);
            if (level != null) return level;
            if (!giver.Tasks.Any(t => skills.Check(player, t.Requirements, "") == null)) return "There's nothing here you're able for yet.";
            return null;
        }

        public bool Assign(Player player, string giverId)
        {
            if (WhyNot(player, giverId) != null) return false;
            TaskGiverDef giver = World.Content.TaskGivers[giverId];
            SkillSystem skills = World.Get<SkillSystem>();
            List<TaskDef> options = giver.Tasks.Where(t => skills.Check(player, t.Requirements, "") == null).ToList();

            int pick = World.Random.Next(options.Sum(t => System.Math.Max(1, t.Weight)));
            TaskDef task = options[0];
            foreach (TaskDef t in options)
            {
                pick -= System.Math.Max(1, t.Weight);
                if (pick < 0)
                {
                    task = t;
                    break;
                }
            }

            PlayerTask state = Of(player);
            state.Giver = giver.Id;
            state.Task = task.Id;
            state.Total = state.Remaining = World.Random.Next(task.Min, task.Max + 1);
            state.Dirty = true;
            World.Tell(player, TextKind.Info, "New task: " + Describe(player));
            return true;
        }

        public void Cancel(Player player)
        {
            PlayerTask state = Of(player);
            if (!state.Active || !World.Content.TaskGivers.TryGetValue(state.Giver, out TaskGiverDef? giver)) return;
            if (!Points.TrySpend(player, giver.Pool, giver.CancelCost))
            {
                World.Tell(player, TextKind.Warning, "You need " + giver.CancelCost + " " + Points.PoolName(giver.Pool).ToLowerInvariant() + " to drop a task.");
                return;
            }
            state.Remaining = 0;
            state.Streaks[giver.Id] = 0;
            state.Dirty = true;
            World.Tell(player, TextKind.Info, "Task dropped.");
        }

        private TaskDef? Current(Player player, out TaskGiverDef? giver)
        {
            PlayerTask state = Of(player);
            giver = null;
            if (!state.Active || !World.Content.TaskGivers.TryGetValue(state.Giver, out giver)) return null;
            return giver.Tasks.FirstOrDefault(t => t.Id == state.Task);
        }

        public string Describe(Player player)
        {
            TaskDef? task = Current(player, out _);
            if (task == null) return "no task";
            PlayerTask state = Of(player);
            return task.Description.Replace("{count}", state.Total.ToString()) + " (" + (state.Total - state.Remaining) + "/" + state.Total + ")";
        }

        private void Progress(Player player, string kind, string target)
        {
            TaskDef? task = Current(player, out TaskGiverDef? giver);
            if (task == null || giver == null || task.Kind != kind || task.Target != target) return;
            PlayerTask state = Of(player);
            state.Remaining--;
            state.Dirty = true;
            if (state.Remaining > 0) return;

            int streak = state.Streaks.TryGetValue(giver.Id, out int s) ? s + 1 : 1;
            state.Streaks[giver.Id] = streak;
            int points = task.Points * (giver.StreakEvery > 0 && streak % giver.StreakEvery == 0 ? giver.StreakMultiplier : 1);
            Points.Add(player, giver.Pool, points);
            World.Tell(player, TextKind.Info, "Task complete (" + streak + " in a row). " + giver.Name + " owes you " + points + " " + Points.PoolName(giver.Pool).ToLowerInvariant() + ".");
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            PlayerTask? saved = save?.Section<PlayerTask>(SaveSection);
            PlayerTask state = Of(player);
            if (saved != null)
            {
                state.Giver = saved.Giver;
                state.Task = saved.Task;
                state.Remaining = saved.Remaining;
                state.Total = saved.Total;
                state.Streaks = saved.Streaks;
            }
            state.Dirty = true;
        }

        public override void OnSave(Player player, PlayerSave save) => save.SetSection(SaveSection, Of(player));

        public override void SendState(Player player)
        {
            PlayerTask state = Of(player);
            PlayerPoints points = Points.Of(player);
            if (!state.Dirty && !points.Dirty) return;
            state.Dirty = false;
            points.Dirty = false;
            var message = new TaskMessage
            {
                Description = state.Active ? Describe(player) : "",
                GiverName = World.Content.TaskGivers.TryGetValue(state.Giver, out var g) ? g.Name : "",
                Remaining = state.Remaining,
                Total = state.Total,
            };
            foreach (KeyValuePair<string, int> p in points.Pools) message.Points[Points.PoolName(p.Key)] = p.Value;
            player.Connection.Send(message);
        }
    }
}
