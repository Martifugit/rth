using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Rth.Server.Persistence;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Progress;

namespace Rth.Server.Game
{
    public sealed class PlayerAchievements
    {
        public AchievementProgress State = new AchievementProgress();
        [JsonIgnore] public bool Dirty = true;
    }

    /// <summary>
    /// The achievement diary (achievements.json). Each achievement's objective is counted from
    /// events other systems already publish (kills, recipes, levels, quests, objects used, NPCs
    /// talked to, regions entered, items obtained), so a new achievement is data, not code.
    /// Completing one pays its reward and announces it. Progress is sent as "achievements" state.
    /// </summary>
    public sealed class AchievementSystem : GameSystem
    {
        public const string StateKey = "achievements";
        private const string SaveSection = "achievements";

        public PlayerAchievements Of(Player player) => player.Component<PlayerAchievements>();

        public bool IsDone(Player player, string id) => Of(player).State.Done.Contains(id);

        protected override void OnAttach()
        {
            World.Events.Subscribe<EntityDied>(e =>
            {
                if (e.Killer is Player p && e.Victim is Npc npc) Count(p, "kill", npc.Def.Id);
            });
            World.Events.Subscribe<RecipeCompleted>(e => Count(e.Player, "recipe", e.Recipe.Id));
            World.Events.Subscribe<LevelUp>(e => CheckLevels(e.Player));
            World.Events.Subscribe<QuestStageChanged>(e =>
            {
                if (e.Completed) Count(e.Player, "quest", e.Quest.Id);
            });
            World.Events.Subscribe<ObjectUsed>(e => Count(e.Player, "object", e.Placed.Type + ":" + e.ActionId));
            World.Events.Subscribe<NpcActionUsed>(e =>
            {
                if (e.ActionId == "talk") Count(e.Player, "talk", e.Npc.Def.Id);
            });
            World.Events.Subscribe<RegionEntered>(e => Count(e.Player, "region", e.Region.Name));
            World.Events.Subscribe<ItemsObtained>(e =>
            {
                foreach (Content.Drop d in e.Items) Count(e.Player, "obtain", d.ItemId, d.Count);
            });
        }

        /// <summary>Counts an event towards every unfinished achievement it matches.</summary>
        public void Count(Player player, string kind, string target, int amount = 1)
        {
            foreach (AchievementDef a in World.Content.Achievements.Values)
            {
                if (a.Objective.Kind != kind || a.Objective.Target != target || IsDone(player, a.Id)) continue;
                AchievementProgress state = Of(player).State;
                int done = (state.Progress.TryGetValue(a.Id, out int p) ? p : 0) + amount;
                state.Progress[a.Id] = done;
                Of(player).Dirty = true;
                if (done >= a.Objective.Count) Complete(player, a);
            }
        }

        /// <summary>Level objectives look at the level itself, so they also catch up on login.</summary>
        private void CheckLevels(Player player)
        {
            SkillSystem skills = World.Get<SkillSystem>();
            foreach (AchievementDef a in World.Content.Achievements.Values)
            {
                if (a.Objective.Kind != "level" || IsDone(player, a.Id)) continue;
                int level = skills.Level(player, a.Objective.Target);
                AchievementProgress state = Of(player).State;
                if (!state.Progress.TryGetValue(a.Id, out int was) || was != level)
                {
                    state.Progress[a.Id] = level;
                    Of(player).Dirty = true;
                }
                if (level >= a.Objective.Count) Complete(player, a);
            }
        }

        /// <summary>Quests finished before an achievement existed still count.</summary>
        private void CheckQuests(Player player)
        {
            QuestSystem? quests = World.Find<QuestSystem>();
            if (quests == null) return;
            foreach (AchievementDef a in World.Content.Achievements.Values)
                if (a.Objective.Kind == "quest" && !IsDone(player, a.Id) && quests.IsComplete(player, a.Objective.Target)) Complete(player, a);
        }

        private void Complete(Player player, AchievementDef a)
        {
            AchievementProgress state = Of(player).State;
            if (state.Done.Contains(a.Id)) return;
            state.Done.Add(a.Id);
            state.Progress.Remove(a.Id);
            Of(player).Dirty = true;

            SkillSystem skills = World.Get<SkillSystem>();
            ItemSystem items = World.Get<ItemSystem>();
            foreach (XpReward xp in a.Reward.Xp) skills.AddXp(player, xp.Skill, xp.Amount);
            foreach (ItemCost reward in a.Reward.Items)
            {
                int left = items.Give(player, reward.Item, reward.Count);
                if (left > 0) World.Find<GroundItemSystem>()?.Spawn(reward.Item, left, player.Plane, player.Position, player);
            }
            World.Tell(player, TextKind.Info, "Achievement unlocked: " + a.Name + " (" + a.Area + ", " + a.Tier.ToLowerInvariant() + ").");
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            AchievementProgress? saved = save?.Section<AchievementProgress>(SaveSection);
            if (saved != null) Of(player).State = saved;
            CheckLevels(player);
            CheckQuests(player);
            Of(player).Dirty = true;
        }

        public override void OnSave(Player player, PlayerSave save) => save.SetSection(SaveSection, Of(player).State);

        public override void OnContentChanged()
        {
            foreach (Player player in World.Players) Of(player).Dirty = true;
        }

        public override void SendState(Player player)
        {
            PlayerAchievements a = Of(player);
            if (!a.Dirty) return;
            a.Dirty = false;
            player.Connection.Send(JsonStateMessage.Of(StateKey, a.State));
        }
    }
}
