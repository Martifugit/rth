using System.Collections.Generic;

namespace Rth.Shared.Combat
{
    /// <summary>
    /// tips.json: ranged's power engine (docs/skills.md 3.4), as bile is Occultism's and verses are
    /// melee's. A tip is cut from another skill's material (Mining's humour gems first) and fixed to
    /// ammunition; each shot that lands may set it off. Tipped ammunition is generated, one item per
    /// tip and plain ammunition of a kind it fits, with the recipe that makes it and the "use tips on
    /// ammunition" that starts it, so a new tip is one entry here and its tips item.
    /// Steady aim (calibration) raises the chance, so the two halves of ranged feed each other.
    /// </summary>
    public sealed class TipsFile
    {
        public List<TipDef> Tips = new List<TipDef>();
    }

    public sealed class TipDef
    {
        public string Id = "";
        /// <summary>The word in the ammunition's name: "Garnet" makes "Garnet-tipped copper arrow".</summary>
        public string Name = "";
        /// <summary>The tips item fixed to the ammunition, one per shot's worth.</summary>
        public string Tip = "";
        /// <summary>The kinds of ammunition it fits ("arrow"); every item of those kinds gets a tipped version.</summary>
        public List<string> Ammo = new List<string>();
        /// <summary>The skill and level to fix it, and the XP for each batch.</summary>
        public string Skill = "artifice";
        public int Level = 1;
        public double Xp;
        /// <summary>How many are tipped at once.</summary>
        public int Batch = 5;
        /// <summary>Percent chance a landed shot sets it off...</summary>
        public double Chance;
        /// <summary>...plus this much more at full calibration (in proportion below it).</summary>
        public double ChanceCalibrated;
        public TipEffect Effect = new TipEffect();
    }

    /// <summary>What a tip does when it goes off, on top of the shot's own hit. Any mix of these.</summary>
    public sealed class TipEffect
    {
        /// <summary>Its name, for tooltips and the message ("Bloodletting").</summary>
        public string Name = "";
        /// <summary>Said to the shooter when it goes off.</summary>
        public string Message = "";
        /// <summary>A second hit on the target, as a percent of the shot's damage.</summary>
        public int DamagePercent;
        /// <summary>Heals the shooter by this percent of the shot's damage.</summary>
        public int HealPercent;
        /// <summary>A bleed on the target: a percent of the shot's damage over these ticks.</summary>
        public int BleedPercent;
        public int BleedTicks;
        /// <summary>Roots the target for this many ticks.</summary>
        public int PinTicks;
        /// <summary>Hits every other creature within this many tiles of the target for a percent of the shot's damage.</summary>
        public int SplashRadius;
        public int SplashPercent;
    }
}
