using System.Collections.Generic;

namespace Rth.Shared.Combat
{
    /// <summary>
    /// The named stats gear and effects can change. Content refers to these by string id
    /// ("stats": { "abilityDamage": 15 }), so adding a stat means adding it here and reading it
    /// where it matters; the content loader rejects unknown ids.
    /// </summary>
    public static class StatIds
    {
        /// <summary>Base damage that ability percentages scale (flat).</summary>
        public const string AbilityDamage = "abilityDamage";
        /// <summary>Added to hit chance, in percentage points.</summary>
        public const string Accuracy = "accuracy";
        /// <summary>Extra damage dealt, in percent.</summary>
        public const string DamageBonus = "damageBonus";
        /// <summary>Less damage taken, in percent (capped at 90 in total).</summary>
        public const string Ward = "ward";
        public const string MaxBlood = "maxBlood";
        public const string MaxBreath = "maxBreath";

        public static readonly IReadOnlyList<string> All = new[] { AbilityDamage, Accuracy, DamageBonus, Ward, MaxBlood, MaxBreath };

        public static bool IsKnown(string id)
        {
            foreach (string s in All)
                if (s == id) return true;
            return false;
        }
    }
}
