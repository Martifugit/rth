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
        Basic = 1,
        /// <summary>Needs some phlegm (usually 50%) and spends part of it.</summary>
        Threshold = 2,
        /// <summary>Needs full phlegm and spends it all.</summary>
        Ultimate = 3,
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

        public EffectType Effect;
        public int EffectMagnitude;
        public int EffectTicks;

        /// <summary>Why the ability can't be used with these resources, or null if it can.</summary>
        public string? CheckRequirements(int phlegm, int bile)
        {
            if (phlegm < PhlegmRequired) return Name + " needs " + PhlegmRequired + "% phlegm.";
            if (bile < BileMin) return Name + " needs your bile at " + Signed(BileMin) + " or higher.";
            if (bile > BileMax) return Name + " needs your bile at " + Signed(BileMax) + " or lower.";
            return null;
        }

        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
    }
}
