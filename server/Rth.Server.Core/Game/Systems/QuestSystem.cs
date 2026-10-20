using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Rth.Server.Content;
using Rth.Server.Persistence;
using Rth.Shared.Items;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    public sealed class PlayerQuests
    {
        /// <summary>Quest id to current stage, for quests in progress.</summary>
        public Dictionary<string, int> Stages = new Dictionary<string, int>();
        /// <summary>Counted objective progress, by quest id.</summary>
        public Dictionary<string, int> Progress = new Dictionary<string, int>();
        public HashSet<string> Completed = new HashSet<string>();
        [JsonIgnore] public bool Dirty = true;
    }

    /// <summary>A quest's stage changed (started at its first stage, moved on, or completed with stage 0).</summary>
    public sealed class QuestStageChanged
    {
        public QuestStageChanged(Player player, QuestDef quest, int stage, bool completed)
        {
            Player = player;
            Quest = quest;
            Stage = stage;
            Completed = completed;
        }

        public Player Player { get; }
        public QuestDef Quest { get; }
        public int Stage { get; }
        public bool Completed { get; }
    }

    /// <summary>
    /// Quests from quests.json. Dialogue drives the story ("startQuest", "setQuestStage",
    /// "completeQuest"; conditions "questStage", "questStarted", "questComplete"); a stage's
    /// objective (kill / recipe / item) advances it automatically. Completion pays rewards and
    /// publishes <see cref="QuestStageChanged"/>, so other systems can react.
    /// </summary>
    public sealed class QuestSystem : GameSystem
    {
        private const string SaveSection = "quests";

        public PlayerQuests Of(Player player) => player.Component<PlayerQuests>();

        public int Stage(Player player, string questId) => Of(player).Stages.TryGetValue(questId, out int s) ? s : 0;
        public bool IsComplete(Player player, string questId) => Of(player).Completed.Contains(questId);
        public bool IsStarted(Player player, string questId) => Stage(player, questId) > 0 || IsComplete(player, questId);

        protected override void OnAttach()
        {
            World.Events.Subscribe<EntityDied>(e =>
            {
                if (e.Killer is Player p && e.Victim is Npc npc) Count(p, "kill", npc.Def.Id);
            });
            World.Events.Subscribe<RecipeCompleted>(e => Count(e.Player, "recipe", e.Recipe.Id));

            DialogueSystem dialogue = World.Get<DialogueSystem>();
            dialogue.RegisterCondition("questStage", (p, c, _) => c.Id != null && Stage(p, c.Id) == c.Count);
            dialogue.RegisterCondition("questStarted", (p, c, _) => c.Id != null && IsStarted(p, c.Id));
            dialogue.RegisterCondition("questComplete", (p, c, _) => c.Id != null && IsComplete(p, c.Id));
            dialogue.RegisterAction("startQuest", (p, a, _) =>
            {
                if (a.Id != null) Start(p, a.Id);
            });
            dialogue.RegisterAction("setQuestStage", (p, a, _) =>
            {
                if (a.Id != null) SetStage(p, a.Id, a.Count);
            });
            dialogue.RegisterAction("completeQuest", (p, a, _) =>
            {
                if (a.Id != null) Complete(p, a.Id);
            });
            dialogue.RegisterToken("questProgress", (p, id, _) => id == null ? "" : ProgressText(p, id));
        }

        public bool Start(Player player, string questId)
        {
            if (!World.Content.Quests.TryGetValue(questId, out QuestDef? quest) || IsStarted(player, questId)) return false;
            string? problem = World.Get<SkillSystem>().Check(player, quest.Requirements, "start " + quest.Name);
            if (problem != null)
            {
                World.Tell(player, TextKind.Warning, problem);
                return false;
            }
            World.Tell(player, TextKind.Info, "Quest started: " + quest.Name + ".");
            SetStage(player, questId, quest.Stages.Min(s => s.Stage));
            return true;
        }

        public void SetStage(Player player, string questId, int stage)
        {
            if (!World.Content.Quests.TryGetValue(questId, out QuestDef? quest) || IsComplete(player, questId)) return;
            if (!quest.Stages.Any(s => s.Stage == stage)) return;
            PlayerQuests quests = Of(player);
            quests.Stages[questId] = stage;
            quests.Progress.Remove(questId);
            quests.Dirty = true;
            World.Events.Publish(new QuestStageChanged(player, quest, stage, false));
            CheckItemObjective(player, quest); // the item might already be in the pack
        }

        public void Complete(Player player, string questId)
        {
            if (!World.Content.Quests.TryGetValue(questId, out QuestDef? quest) || IsComplete(player, questId)) return;
            PlayerQuests quests = Of(player);
            quests.Stages.Remove(questId);
            quests.Progress.Remove(questId);
            quests.Completed.Add(questId);
            quests.Dirty = true;

            SkillSystem skills = World.Get<SkillSystem>();
            ItemSystem items = World.Get<ItemSystem>();
            foreach (SkillXp xp in quest.Rewards.Xp) skills.AddXp(player, xp.Skill, xp.Amount);
            foreach (ItemCost reward in quest.Rewards.Items)
            {
                int left = items.Give(player, reward.Item, reward.Count);
                if (left > 0) World.Find<GroundItemSystem>()?.Spawn(reward.Item, left, player.Plane, player.Position, player);
            }
            World.Tell(player, TextKind.Info, "Quest complete: " + quest.Name + "!");
            World.Events.Publish(new QuestStageChanged(player, quest, 0, true));
        }

        private QuestStage? CurrentStage(Player player, QuestDef quest) =>
            Of(player).Stages.TryGetValue(quest.Id, out int stage) ? quest.Stages.FirstOrDefault(s => s.Stage == stage) : null;

        /// <summary>Counts an event towards every quest whose current objective it matches.</summary>
        private void Count(Player player, string kind, string target)
        {
            PlayerQuests quests = Of(player);
            foreach (string id in quests.Stages.Keys.ToList())
            {
                if (!World.Content.Quests.TryGetValue(id, out QuestDef? quest)) continue;
                QuestStage? stage = CurrentStage(player, quest);
                if (stage?.Objective == null || stage.Objective.Kind != kind || stage.Objective.Target != target) continue;
                int done = (quests.Progress.TryGetValue(id, out int p) ? p : 0) + 1;
                quests.Progress[id] = done;
                quests.Dirty = true;
                if (done >= stage.Objective.Count && stage.Next != null) SetStage(player, id, stage.Next.Value);
            }
        }

        private void CheckItemObjective(Player player, QuestDef quest)
        {
            QuestStage? stage = CurrentStage(player, quest);
            if (stage?.Objective == null || stage.Objective.Kind != "item" || stage.Next == null) return;
            if (World.Get<ItemSystem>().CountOf(player, stage.Objective.Target) >= stage.Objective.Count)
                SetStage(player, quest.Id, stage.Next.Value);
        }

        public override void AfterPlayer(Player player)
        {
            // Item objectives are checked as the pack changes; a dictionary walk per player per tick is cheap.
            PlayerQuests quests = Of(player);
            if (quests.Stages.Count == 0) return;
            foreach (string id in quests.Stages.Keys.ToList())
                if (World.Content.Quests.TryGetValue(id, out QuestDef? quest)) CheckItemObjective(player, quest);
        }

        private string ProgressText(Player player, string questId)
        {
            if (!World.Content.Quests.TryGetValue(questId, out QuestDef? quest)) return "";
            QuestStage? stage = CurrentStage(player, quest);
            if (stage?.Objective == null) return "";
            int done = stage.Objective.Kind == "item"
                ? World.Get<ItemSystem>().CountOf(player, stage.Objective.Target)
                : Of(player).Progress.TryGetValue(questId, out int p) ? p : 0;
            return System.Math.Min(done, stage.Objective.Count) + "/" + stage.Objective.Count;
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            PlayerQuests? saved = save?.Section<PlayerQuests>(SaveSection);
            if (saved != null)
            {
                PlayerQuests quests = Of(player);
                quests.Stages = saved.Stages;
                quests.Progress = saved.Progress;
                quests.Completed = saved.Completed;
            }
            Of(player).Dirty = true;
        }

        public override void OnSave(Player player, PlayerSave save) => save.SetSection(SaveSection, Of(player));

        public override void OnContentChanged()
        {
            foreach (Player player in World.Players) Of(player).Dirty = true;
        }

        public override void SendState(Player player)
        {
            PlayerQuests quests = Of(player);
            if (!quests.Dirty) return;
            quests.Dirty = false;
            var message = new QuestsMessage();
            foreach (QuestDef quest in World.Content.Quests.Values)
            {
                QuestStage? stage = CurrentStage(player, quest);
                bool complete = IsComplete(player, quest.Id);

                // The story so far: every stage up to the current one, then the ending.
                var log = new List<string>();
                foreach (QuestStage s in quest.Stages.OrderBy(s => s.Stage))
                    if (complete || (stage != null && s.Stage <= stage.Stage)) log.Add(s.Journal);
                if (complete && !string.IsNullOrEmpty(quest.Completed)) log.Add(quest.Completed);

                message.Quests.Add(new QuestEntry
                {
                    Id = quest.Id,
                    Name = quest.Name,
                    Summary = quest.Summary,
                    State = complete ? QuestState.Complete : stage != null ? QuestState.InProgress : QuestState.NotStarted,
                    Journal = complete ? quest.Completed : stage?.Journal ?? quest.Summary,
                    Progress = stage != null ? ProgressText(player, quest.Id) : "",
                    Log = log,
                    Requirements = quest.Requirements.Select(r => World.Content.SkillName(r.Skill) + " " + r.Level).ToList(),
                    Rewards = quest.Rewards.Xp.Select(x => x.Amount.ToString("N0") + " " + World.Content.SkillName(x.Skill) + " XP")
                        .Concat(quest.Rewards.Items.Select(i => (i.Count > 1 ? i.Count + " x " : "") + (World.Content.Item(i.Item)?.Name ?? i.Item)))
                        .ToList(),
                });
            }
            player.Connection.Send(message);
        }
    }
}
