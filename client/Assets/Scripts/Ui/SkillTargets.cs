using System.Collections.Generic;
using Newtonsoft.Json;
using Rth.Shared.Skills;

namespace Rth.Client.Ui
{
    /// <summary>
    /// Targets the player sets per skill: a level ("reach 50") or an amount of XP ("reach
    /// 2,000,000"). Kept on the server as client state, so they follow the character.
    /// </summary>
    public static class SkillTargets
    {
        private const string StateKey = "skill.targets";

        public sealed class Target
        {
            /// <summary>True: <see cref="Value"/> is a level. False: an amount of XP.</summary>
            public bool IsLevel;
            public long Value;
        }

        private static string _parsedJson;
        private static Dictionary<string, Target> _parsed = new Dictionary<string, Target>();

        private static Dictionary<string, Target> All(GameSession session)
        {
            session.ClientState.TryGetValue(StateKey, out string json);
            if (json == _parsedJson) return _parsed;
            _parsedJson = json;
            try
            {
                _parsed = string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<Dictionary<string, Target>>(json);
            }
            catch (JsonException)
            {
                _parsed = null;
            }
            _parsed ??= new Dictionary<string, Target>();
            return _parsed;
        }

        /// <summary>The skill's target, or null for none.</summary>
        public static Target Get(GameSession session, string skill) =>
            All(session).TryGetValue(skill, out Target t) ? t : null;

        /// <summary>The XP the target means (a level target in XP), or 0 for none.</summary>
        public static long GoalXp(GameSession session, string skill)
        {
            Target t = Get(session, skill);
            if (t == null || session.Content == null) return 0;
            if (!t.IsLevel) return t.Value;
            XpCurve curve = session.Content.XpCurve;
            return curve.XpForLevel((int)t.Value, curve.MaxFor(session.Skill(skill)));
        }

        /// <summary>"Level 50" or "2.5M XP".</summary>
        public static string Describe(Target t) => t.IsLevel ? "Level " + t.Value : UiKit.FormatXp(t.Value) + " XP";

        /// <summary>Sets a skill's target; null clears it.</summary>
        public static void Set(GameSession session, string skill, Target target)
        {
            var all = new Dictionary<string, Target>(All(session));
            if (target == null) all.Remove(skill);
            else all[skill] = target;
            session.SetClientState(StateKey, JsonConvert.SerializeObject(all));
        }
    }
}
