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

        /// <summary>
        /// A player can't log out until this long after their last blow given or taken; a dropped
        /// connection keeps them in the world (fighting back) for as long, then saves them.
        /// </summary>
        public int LogoutCombatTicks = 16;

        // Ranged (docs/skills.md 3.4, docs/systems.md "Calibration").
        /// <summary>Chance, in percent, that a shot's ammunition lands on the target's tile to be picked up again.</summary>
        public int AmmoRecoverPercent = 33;
        /// <summary>Calibration gained by a ranged hit on the same target from the same tile.</summary>
        public int CalibrationPerHit = 10;
        /// <summary>Calibration lost by a miss.</summary>
        public int CalibrationMissLoss = 20;
        /// <summary>Accuracy, in percentage points, at full calibration (in proportion below it).</summary>
        public int CalibrationAccuracyMax = 15;
        /// <summary>Percent more ranged damage at full calibration (in proportion below it).</summary>
        public int CalibrationDamageMax = 10;

        /// <summary>A share of a full-calibration amount, in proportion to the calibration held.</summary>
        public static int Calibrated(int calibration, int atFull) => (int)Math.Round(atFull * Math.Max(0, Math.Min(100, calibration)) / 100.0);

        // A creature's combat level, shown beside its name, when npcs.json gives none:
        // the average of its blood and its hardest hit measured against these.
        /// <summary>Blood worth one level.</summary>
        public int NpcLevelBloodPer = 100;
        /// <summary>Max hit worth one level.</summary>
        public int NpcLevelHitPer = 10;

        /// <summary>A creature's combat level from its blood and hardest hit (at least 1).</summary>
        public int NpcLevel(int blood, int maxHit)
        {
            double level = (blood / (double)Math.Max(1, NpcLevelBloodPer) + maxHit / (double)Math.Max(1, NpcLevelHitPer)) / 2;
            return Math.Max(1, (int)Math.Round(level, MidpointRounding.AwayFromZero));
        }

        // Blood (melee and Cruor's verses).
        /// <summary>Damage held back by deferDamage (Death's Dance) bleeds out over this many ticks.</summary>
        public int DeferredBleedTicks = 10;
        /// <summary>Scar stacks (Martyrdom) that make the next ultimate cost no phlegm, consuming them.</summary>
        public int ScarsForFreeUltimate = 10;

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
