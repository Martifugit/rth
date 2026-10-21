using System;
using System.Collections.Generic;
using Rth.Shared.Items;
using Rth.Shared.Skills;

namespace Rth.Shared.Combat
{
    public enum CombatStyle : byte
    {
        None = 0,
        Melee = 1,
        Ranged = 2,
        Occult = 3,
    }

    public enum AbilityType : byte
    {
        /// <summary>The free attack made whenever nothing else is used.</summary>
        Auto = 0,
        /// <summary>Builds phlegm.</summary>
        Builder = 1,
        /// <summary>Needs some phlegm (usually 50%) and spends part of it.</summary>
        Spender = 2,
        /// <summary>Needs full phlegm and spends it all.</summary>
        Ultimate = 3,
        /// <summary>
        /// One of Cruor's verses (Religion, for melee): read on yourself on the global cooldown,
        /// paid for with blood and scriptures. It shapes the fight rather than dealing damage.
        /// </summary>
        Verse = 4,
    }

    /// <summary>A timed status effect an ability can put on its user.</summary>
    public enum EffectType : byte
    {
        None = 0,
        /// <summary>Takes Magnitude% less damage.</summary>
        Ward = 1,
        /// <summary>Deals Magnitude% more damage.</summary>
        Fervour = 2,
    }

    /// <summary>
    /// One combat ability, loaded from data (server/data/content/abilities.json) and sent to
    /// clients so they can draw the action bar. Damage is a percentage of the user's ability damage.
    /// </summary>
    public sealed class AbilityDef
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        /// <summary>Icon id (the client's icon library); empty means one picked by the bile pole.</summary>
        public string Icon = "";
        /// <summary>
        /// The character animation played when it's used: a clip or role in the client's
        /// Resources/Characters/animations.json. Empty means the "cast" role.
        /// </summary>
        public string Animation = "";
        public AbilityType Type;
        public CombatStyle Style = CombatStyle.Occult;

        public int CooldownTicks;
        /// <summary>Attack range in tiles (Chebyshev). 1 means melee: orthogonally adjacent.</summary>
        public int Range = 8;
        public int DamageMin;
        public int DamageMax;

        public int PhlegmGain;
        public int PhlegmRequired;
        public int PhlegmCost;

        /// <summary>How far using it pushes the user's bile (+ towards yellow, - towards black).</summary>
        public int BileShift;
        /// <summary>Moves bile this much towards zero instead of (or as well as) shifting it.</summary>
        public int BileTowardZero;
        /// <summary>Bile must be within [BileMin, BileMax] to use it.</summary>
        public int BileMin = -100;
        public int BileMax = 100;

        public int HealPercentOfDamage;
        public int HealPercentOfMaxBlood;

        /// <summary>
        /// Used on yourself where you stand (no target or range needed, no damage), and it doesn't
        /// release or break a stalled ability: using it keeps you in combat, so the stall holds.
        /// </summary>
        public bool KeepsStall;

        public EffectType Effect;
        public int EffectMagnitude;
        public int EffectTicks;

        /// <summary>Blood paid to use it, as a percent of current blood (never the last point).</summary>
        public int BloodCostCurrent;
        /// <summary>Blood paid to use it, as a percent of max blood. It can't be used unless you have more than that.</summary>
        public int BloodCostMax;

        /// <summary>Damage over time on top of the hit: this percent of the damage dealt, spread over <see cref="BleedTicks"/>.</summary>
        public int BleedPercent;
        public int BleedTicks;

        /// <summary>Up to this percent more damage the lower your blood: all of it at no blood, none at full.</summary>
        public int LowBloodBonus;

        /// <summary>A timed buff on the user (stats, maybe conditional), shown as its status.</summary>
        public BuffDef? Buff;

        /// <summary>XP awarded each time it's used (verses train Religion), on top of any damage XP.</summary>
        public List<Rth.Shared.Progress.XpReward> Xp = new List<Rth.Shared.Progress.XpReward>();

        /// <summary>Used on yourself where you stand: keepsStall abilities and every verse.</summary>
        public bool OnSelf => KeepsStall || Type == AbilityType.Verse;

        /// <summary>Skill levels needed to use it at all (e.g. occultism 5 for a spender).</summary>
        public List<SkillRequirement> Requirements = new List<SkillRequirement>();

        /// <summary>Items used up each time it's cast (Distillation's salts and tinctures).</summary>
        public List<ItemCost> Reagents = new List<ItemCost>();

        /// <summary>
        /// Why the ability can't be used with these resources, levels and items, or null if it can.
        /// Level and reagent checks are skipped when their lookups aren't given.
        /// </summary>
        public string? CheckRequirements(int phlegm, int bile, Func<string, int>? levelOf = null, Func<string, string>? skillName = null,
            Func<string, int>? countOf = null, Func<string, string>? itemName = null) =>
            CheckRequirements(new AbilityUser { Phlegm = phlegm, Bile = bile, LevelOf = levelOf, SkillName = skillName, CountOf = countOf, ItemName = itemName });

        /// <summary>Why this user can't use the ability now, or null if they can. Checks only what the user says it knows.</summary>
        public string? CheckRequirements(AbilityUser user)
        {
            if (user.WeaponStyle != null && Type != AbilityType.Auto && Style != CombatStyle.None && Style != user.WeaponStyle)
                return Name + " needs " + CombatStyles.WeaponPhrase(Style) + ".";
            if (user.LevelOf != null)
            {
                string? level = SkillRequirement.Check(Requirements, user.LevelOf, user.SkillName ?? (s => s), "use " + Name);
                if (level != null) return level;
            }
            if (user.CountOf != null)
            {
                foreach (ItemCost r in Reagents)
                    if (user.CountOf(r.Item) < r.Count)
                        return Name + " needs " + r.Count + " " + (user.ItemName?.Invoke(r.Item) ?? r.Item).ToLowerInvariant() + ".";
            }
            if (user.MaxBlood > 0 && BloodCostMax > 0 && user.Blood <= user.MaxBlood * BloodCostMax / 100)
                return Name + " needs more than " + BloodCostMax + "% of your blood.";
            if (user.MaxBlood > 0 && BloodCostCurrent > 0 && user.Blood <= 1)
                return Name + " would take your last blood.";
            if (!(user.FreeUltimate && Type == AbilityType.Ultimate) && user.Phlegm < PhlegmRequired) return Name + " needs " + PhlegmRequired + "% phlegm.";
            if (user.Bile < BileMin) return Name + " needs your bile at " + Signed(BileMin) + " or higher.";
            if (user.Bile > BileMax) return Name + " needs your bile at " + Signed(BileMax) + " or lower.";
            return null;
        }

        /// <summary>The blood it costs someone with this much blood (never their last point).</summary>
        public int BloodCost(int blood, int maxBlood)
        {
            int cost = blood * BloodCostCurrent / 100 + maxBlood * BloodCostMax / 100;
            return Math.Max(0, Math.Min(cost, blood - 1));
        }

        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
    }

    /// <summary>What an ability check knows about its user. Anything left unset isn't checked.</summary>
    public sealed class AbilityUser
    {
        public int Phlegm;
        public int Bile;
        public int Blood;
        /// <summary>0 skips the blood cost checks.</summary>
        public int MaxBlood;
        /// <summary>The style of the weapon in hand (<see cref="CombatStyles.OfWeapon"/>); null skips the check.</summary>
        public CombatStyle? WeaponStyle;
        /// <summary>The next ultimate costs no phlegm (Martyrdom's scars).</summary>
        public bool FreeUltimate;
        public Func<string, int>? LevelOf;
        public Func<string, string>? SkillName;
        public Func<string, int>? CountOf;
        public Func<string, string>? ItemName;
    }

    /// <summary>
    /// A timed buff an ability puts on its user: stats while it lasts, shown as a status. Its stats
    /// can be conditional (only against creatures with a tag, only while low on blood), and it can
    /// end on the next hit taken (a block).
    /// </summary>
    public sealed class BuffDef
    {
        /// <summary>The status shown for it (statuses.json). A new buff with the same status replaces the old.</summary>
        public string Status = "";
        public int Ticks;
        /// <summary>Stat id to amount while it lasts (and its conditions hold).</summary>
        public Dictionary<string, int> Stats = new Dictionary<string, int>();
        /// <summary>When set, <see cref="Stats"/> count only against targets with this tag, and <see cref="Otherwise"/> against the rest.</summary>
        public string? TargetTag;
        public Dictionary<string, int> Otherwise = new Dictionary<string, int>();
        /// <summary>When above 0, the stats count only while the user's blood is below this percent of max.</summary>
        public int WhileBloodBelow;
        /// <summary>Ends when the user next takes a hit (after that hit is dealt with).</summary>
        public bool EndsOnHitTaken;

        /// <summary>Whether the stats depend on the moment (the target, the blood) rather than holding throughout.</summary>
        public bool Conditional => TargetTag != null || WhileBloodBelow > 0;
    }

    /// <summary>Which style a weapon fights with.</summary>
    public static class CombatStyles
    {
        /// <summary>A weapon's style; no weapon (or one with no style) fights with <paramref name="unarmed"/>.</summary>
        public static CombatStyle OfWeapon(ItemDef? weapon, CombatStyle unarmed)
        {
            CombatStyle style = weapon?.Equipment?.Style ?? CombatStyle.None;
            return style == CombatStyle.None ? unarmed : style;
        }

        /// <summary>Each style's colour (#RRGGBB) where the world signals it: holy symbols, telegraphs.</summary>
        public static string Colour(CombatStyle style) => style switch
        {
            CombatStyle.Melee => "#ff4a3a",
            CombatStyle.Ranged => "#4fe06a",
            CombatStyle.Occult => "#b06cff",
            _ => "#ffe7b0",
        };

        public static string WeaponPhrase(CombatStyle style) => style switch
        {
            CombatStyle.Melee => "a melee weapon",
            CombatStyle.Ranged => "a ranged weapon",
            CombatStyle.Occult => "an occult weapon or empty hands",
            _ => "a weapon",
        };
    }
}
