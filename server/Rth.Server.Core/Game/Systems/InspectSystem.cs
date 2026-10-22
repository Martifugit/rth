using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rth.Server.Items;
using Rth.Shared.Net;
using Rth.Shared.Skills;

namespace Rth.Server.Game
{
    /// <summary>
    /// Inspecting a player (right-click, Inspect; or yourself from the worn equipment): their
    /// name, combat and total level, what they wear, and a few highlights. Public information
    /// only. Answers the "inspect" query (argument: the player's entity id); you can inspect
    /// yourself or any player you can see. Other systems add highlights with <see cref="AddHighlights"/>.
    /// </summary>
    public sealed class InspectSystem : GameSystem
    {
        /// <summary>How many of a player's best skills are shown.</summary>
        public const int BestSkills = 3;

        private readonly List<Func<Player, IEnumerable<InspectLine>>> _highlights = new List<Func<Player, IEnumerable<InspectLine>>>();

        protected override void OnAttach()
        {
            World.Get<QuerySystem>().Register(QueryKinds.Inspect, Answer);
            AddHighlights(SkillHighlights);
            AddHighlights(ProgressHighlights);
        }

        /// <summary>Adds lines to every inspect card, after those already added.</summary>
        public void AddHighlights(Func<Player, IEnumerable<InspectLine>> source) => _highlights.Add(source);

        private QueryAnswer Answer(Player asker, string arg)
        {
            if (!int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || !(World.FindEntity(id) is Player target))
                return QueryAnswer.No("They are no longer here.");
            if (target != asker && !World.CanSee(asker, target)) return QueryAnswer.No("They are too far away.");
            return QueryAnswer.Of(Of(target));
        }

        /// <summary>What anyone inspecting <paramref name="player"/> sees.</summary>
        public PlayerInspect Of(Player player)
        {
            var inspect = new PlayerInspect { EntityId = player.Id, Name = player.Name };
            if (World.Find<SkillSystem>() is SkillSystem skills)
            {
                inspect.CombatLevel = World.Content.CombatLevel.Of(s => skills.Level(player, s));
                inspect.TotalLevel = World.Content.Skills.Keys.Sum(s => skills.Level(player, s));
            }
            if (World.Find<ItemSystem>() is ItemSystem items)
            {
                ItemContainer worn = items.Of(player).Equipment;
                for (int i = 0; i < worn.Size; i++) inspect.Equipment.Add(worn[i].IsEmpty ? "" : worn[i].ItemId);
            }
            foreach (Func<Player, IEnumerable<InspectLine>> source in _highlights) inspect.Highlights.AddRange(source(player));
            return inspect;
        }

        /// <summary>The player's best few skills above level 1.</summary>
        private IEnumerable<InspectLine> SkillHighlights(Player player)
        {
            if (!(World.Find<SkillSystem>() is SkillSystem skills)) yield break;
            IEnumerable<SkillDef> best = World.Content.Skills.Values
                .Where(s => skills.Level(player, s.Id) > 1)
                .OrderByDescending(s => skills.Xp(player, s.Id))
                .ThenBy(s => s.Name, StringComparer.Ordinal)
                .Take(BestSkills);
            foreach (SkillDef s in best)
                yield return new InspectLine { Label = s.Name, Value = skills.Level(player, s.Id).ToString(CultureInfo.InvariantCulture), Icon = s.Icon };
        }

        /// <summary>Quests and achievements done, out of how many there are.</summary>
        private IEnumerable<InspectLine> ProgressHighlights(Player player)
        {
            if (World.Content.Quests.Count > 0 && World.Find<QuestSystem>() is QuestSystem quests)
            {
                int done = World.Content.Quests.Keys.Count(q => quests.IsComplete(player, q));
                yield return new InspectLine { Label = "Quests", Value = done + " / " + World.Content.Quests.Count, Icon = "quests" };
            }
            if (World.Content.Achievements.Count > 0 && World.Find<AchievementSystem>() is AchievementSystem achievements)
            {
                int done = World.Content.Achievements.Keys.Count(a => achievements.IsDone(player, a));
                yield return new InspectLine { Label = "Achievements", Value = done + " / " + World.Content.Achievements.Count, Icon = "achievement" };
            }
        }
    }
}
