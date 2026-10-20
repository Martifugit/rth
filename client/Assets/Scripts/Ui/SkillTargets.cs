using UnityEngine;

namespace Rth.Client.Ui
{
    /// <summary>Level targets the player sets per skill, kept per character on this machine.</summary>
    public static class SkillTargets
    {
        private static string Key(string player, string skill) => "rth.target." + player.ToLowerInvariant() + "." + skill;

        /// <summary>The target level, or 0 for none.</summary>
        public static int Get(string player, string skill) => PlayerPrefs.GetInt(Key(player, skill), 0);

        public static void Set(string player, string skill, int level)
        {
            if (level <= 0) PlayerPrefs.DeleteKey(Key(player, skill));
            else PlayerPrefs.SetInt(Key(player, skill), level);
        }
    }
}
