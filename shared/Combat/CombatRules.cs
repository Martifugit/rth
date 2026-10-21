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
        public int PlayerMaxBreath = 300;
        /// <summary>Chance a player's attack lands before the target's evasion is subtracted.</summary>
        public double PlayerAccuracy = 0.9;
        public int MaxPhlegm = 100;

        // The bile axis.
        public int BileLimit = 100;
        /// <summary>At or beyond ±this, the pole's passive bonus applies.</summary>
        public int BilePassiveThreshold = 30;
        /// <summary>At or beyond ±this, the pole's drawback applies too.</summary>
        public int BileExtreme = 80;
        public int CholericMinBonusPercent = 5;
        public int CholericMaxBonusPercent = 20;
        public int MelancholicMinWardPercent = 5;
        public int MelancholicMaxWardPercent = 15;
        /// <summary>Choleric fever: blood lost each tick at the extreme, as % of max (never kills).</summary>
        public double FeverBloodPercentPerTick = 0.5;
        /// <summary>Melancholic torpor: phlegm gains are multiplied by this at the extreme.</summary>
        public double TorporPhlegmMultiplier = 0.5;

        // Out of combat.
        public int CombatTimeoutTicks = 10;
        public int BileDecayPerTick = 1;
        public int PhlegmDecayPerTick = 2;
        public double BloodRegenPercentPerTick = 0.5;
        public int BreathRegenPerTick = 1;

        // Breath and deflection.
        public int BreathDrainPerTick = 1;
        public int DeflectReductionPercent = 50;

        public int RespawnDelayTicks = 4;

        public int ClampBile(int bile) => Math.Max(-BileLimit, Math.Min(BileLimit, bile));

        public bool IsCholeric(int bile) => bile >= BilePassiveThreshold;
        public bool IsMelancholic(int bile) => bile <= -BilePassiveThreshold;
        public bool IsFevered(int bile) => bile >= BileExtreme;
        public bool IsTorpid(int bile) => bile <= -BileExtreme;

        /// <summary>Yellow bile's passive: more damage dealt, scaling from the threshold to the limit.</summary>
        public int CholericBonusPercent(int bile) =>
            IsCholeric(bile) ? Scale(bile, CholericMinBonusPercent, CholericMaxBonusPercent) : 0;

        /// <summary>Black bile's passive: less damage taken, scaling from the threshold to the limit.</summary>
        public int MelancholicWardPercent(int bile) =>
            IsMelancholic(bile) ? Scale(-bile, MelancholicMinWardPercent, MelancholicMaxWardPercent) : 0;

        private int Scale(int magnitude, int atThreshold, int atLimit)
        {
            int span = Math.Max(1, BileLimit - BilePassiveThreshold);
            double t = Math.Min(1.0, (magnitude - BilePassiveThreshold) / (double)span);
            return (int)Math.Round(atThreshold + (atLimit - atThreshold) * t);
        }

        /// <summary>Phlegm actually gained from a builder, after torpor.</summary>
        public int PhlegmGainFor(int baseGain, int bile) =>
            IsTorpid(bile) ? (int)Math.Floor(baseGain * TorporPhlegmMultiplier) : baseGain;
    }
}
