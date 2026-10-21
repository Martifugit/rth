using System;

namespace Rth.Shared.Combat
{
    /// <summary>
    /// Global combat tuning, loaded from server/data/content/combat.json. The defaults here are the
    /// fallback when a field is missing. The formulas live here too, so client and server agree.
    /// </summary>
    public sealed class CombatRules
    {
        public int GlobalCooldownTicks = 3;
        public int PlayerAbilityDamage = 100;
        public int PlayerMaxBlood = 1000;
        /// <summary>Devotion before Religion levels add to it (each level adds its own, in skills.json).</summary>
        public int PlayerMaxDevotion = 10;
        /// <summary>Chance a player's attack lands before the target's evasion is subtracted.</summary>
        public double PlayerAccuracy = 0.9;
        public int MaxPhlegm = 100;

        // The bile axis.
        public int BileLimit = 100;
        // Any lean from zero brings both of a pole's statuses, scaling up to these at the limit.
        /// <summary>Yellow bile's buff: % more damage dealt.</summary>
        public int InfusedMaxPercent = 20;
        /// <summary>Yellow bile's debuff: % more damage taken.</summary>
        public int CholericMaxPercent = 10;
        /// <summary>Black bile's buff: % less damage taken.</summary>
        public int WardingMaxPercent = 20;
        /// <summary>Black bile's debuff: % less damage dealt.</summary>
        public int MelancholicMaxPercent = 10;

        // Out of combat.
        public int CombatTimeoutTicks = 10;
        public int BileDecayPerTick = 1;
        public int PhlegmDecayPerTick = 2;
        public double BloodRegenPercentPerTick = 0.5;

        public int RespawnDelayTicks = 4;

        public int ClampBile(int bile) => Math.Max(-BileLimit, Math.Min(BileLimit, bile));

        /// <summary>Bile leaning yellow at all: infused and choleric.</summary>
        public bool IsCholeric(int bile) => bile > 0;
        /// <summary>Bile leaning black at all: warding and melancholic.</summary>
        public bool IsMelancholic(int bile) => bile < 0;

        /// <summary>Infused: % more damage dealt, scaling with yellow bile.</summary>
        public int InfusedPercent(int bile) => IsCholeric(bile) ? Scale(bile, InfusedMaxPercent) : 0;

        /// <summary>Choleric: % more damage taken, scaling with yellow bile.</summary>
        public int CholericPercent(int bile) => IsCholeric(bile) ? Scale(bile, CholericMaxPercent) : 0;

        /// <summary>Warding: % less damage taken, scaling with black bile.</summary>
        public int WardingPercent(int bile) => IsMelancholic(bile) ? Scale(-bile, WardingMaxPercent) : 0;

        /// <summary>Melancholic: % less damage dealt, scaling with black bile.</summary>
        public int MelancholicPercent(int bile) => IsMelancholic(bile) ? Scale(-bile, MelancholicMaxPercent) : 0;

        /// <summary>Net % change to damage dealt from bile (infused less melancholic).</summary>
        public int BileDamageDealtPercent(int bile) => InfusedPercent(bile) - MelancholicPercent(bile);

        /// <summary>Net % change to damage taken from bile (choleric less warding).</summary>
        public int BileDamageTakenPercent(int bile) => CholericPercent(bile) - WardingPercent(bile);

        /// <summary>A pole's share of its maximum, in proportion to how far bile leans (at least 1% once it leans).</summary>
        private int Scale(int magnitude, int atLimit)
        {
            double t = Math.Min(1.0, magnitude / (double)Math.Max(1, BileLimit));
            return Math.Max(atLimit > 0 ? 1 : 0, (int)Math.Round(atLimit * t));
        }
    }
}
