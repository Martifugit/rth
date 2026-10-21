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
        public const string MaxDevotion = "maxDevotion";
        /// <summary>Slows the devotion every hymn drains, by 1% a point.</summary>
        public const string DevotionBonus = "devotionBonus";
        /// <summary>Percent of the damage you deal that comes back to you as blood.</summary>
        public const string Lifesteal = "lifesteal";

        // Per combat style: accuracy and damage with that style, and how much less damage you take
        // from it (deflection). Read with ForStyle.
        public const string MeleeAccuracy = "meleeAccuracy";
        public const string RangedAccuracy = "rangedAccuracy";
        public const string OccultAccuracy = "occultAccuracy";
        public const string MeleeDamage = "meleeDamage";
        public const string RangedDamage = "rangedDamage";
        public const string OccultDamage = "occultDamage";
        public const string DeflectMelee = "deflectMelee";
        public const string DeflectRanged = "deflectRanged";
        public const string DeflectOccult = "deflectOccult";

        public static readonly IReadOnlyList<string> All = new[]
        {
            AbilityDamage, Accuracy, DamageBonus, Ward, MaxBlood, MaxDevotion, DevotionBonus, Lifesteal,
            MeleeAccuracy, RangedAccuracy, OccultAccuracy, MeleeDamage, RangedDamage, OccultDamage,
            DeflectMelee, DeflectRanged, DeflectOccult,
        };

        public static bool IsKnown(string id)
        {
            foreach (string s in All)
                if (s == id) return true;
            return false;
        }

        /// <summary>A style's own stat ("meleeAccuracy"), or null for no style.</summary>
        public static string? AccuracyFor(CombatStyle style) => style switch
        {
            CombatStyle.Melee => MeleeAccuracy,
            CombatStyle.Ranged => RangedAccuracy,
            CombatStyle.Occult => OccultAccuracy,
            _ => null,
        };

        public static string? DamageFor(CombatStyle style) => style switch
        {
            CombatStyle.Melee => MeleeDamage,
            CombatStyle.Ranged => RangedDamage,
            CombatStyle.Occult => OccultDamage,
            _ => null,
        };

        /// <summary>The stat that lowers damage taken from a style ("deflectMelee"), or null for no style.</summary>
        public static string? DeflectFor(CombatStyle style) => style switch
        {
            CombatStyle.Melee => DeflectMelee,
            CombatStyle.Ranged => DeflectRanged,
            CombatStyle.Occult => DeflectOccult,
            _ => null,
        };
    }
}
