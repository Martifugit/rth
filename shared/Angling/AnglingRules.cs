using System;
using System.Collections.Generic;

namespace Rth.Shared.Angling
{
    /// <summary>
    /// The plain maths of trophy angling, shared by the server, the client and the tests: a fish's
    /// size from its roll, its class, weight, energy and XP, the reeling bonus, and the rig a set
    /// of parts makes.
    /// </summary>
    public static class AnglingRules
    {
        /// <summary>A size roll (0-1000) from a uniform draw (0-1): skewed so big fish are rare.</summary>
        public static int SizeRoll(double uniform, double skew) =>
            Math.Max(0, Math.Min(1000, (int)(Math.Pow(Math.Max(0, Math.Min(1, uniform)), Math.Max(0.1, skew)) * 1000)));

        public static int Grams(AnglingSpeciesDef species, int roll) =>
            species.MinGrams + (int)((long)(species.MaxGrams - species.MinGrams) * roll / 1000);

        public static AnglingSizeClass? ClassOf(AnglingFile file, int roll)
        {
            foreach (AnglingSizeClass c in file.SizeClasses)
                if (roll < c.UpTo) return c;
            return file.SizeClasses.Count > 0 ? file.SizeClasses[file.SizeClasses.Count - 1] : null;
        }

        /// <summary>
        /// Energy in percent of the species' average: 35% for the smallest, 100% at roll 500,
        /// 300% for a record (so big fish fight two or three times as long).
        /// </summary>
        public static int EnergyPercent(int roll) =>
            roll <= 500 ? 35 + 65 * roll / 500 : 100 + 200 * (roll - 500) / 500;

        public static int Energy(AnglingFile file, AnglingSpeciesDef species, int roll, bool revised) =>
            revised ? species.Fight.Energy * EnergyPercent(1000) / 100 * file.RevisedEnergyPercent / 100
                    : species.Fight.Energy * EnergyPercent(roll) / 100;

        /// <summary>XP for a catch: the species' XP scaled by the fish's energy (its size), times more if revised.</summary>
        public static double Xp(AnglingFile file, AnglingSpeciesDef species, int roll, bool revised) =>
            Math.Round(species.Xp * (revised ? EnergyPercent(1000) / 100.0 * file.RevisedXp : EnergyPercent(roll) / 100.0), 1);

        /// <summary>The reeling bonus (tenths of a percent) for a level above the species' level.</summary>
        public static int ReelBonus(AnglingFile file, int level, AnglingSpeciesDef species) =>
            Math.Max(0, Math.Min(file.ReelBonusLevels, level - species.Level)) * file.ReelBonusPerLevel;

        /// <summary>A part's stat at its condition: it loses its bonus gradually, to <see cref="AnglingFile.WornPercent"/> at 0.</summary>
        public static int Worn(int stat, double condition, int wornPercent) =>
            (int)Math.Round(stat * (wornPercent + (100 - wornPercent) * Math.Max(0, Math.Min(100, condition)) / 100.0) / 100.0);

        /// <summary>
        /// The rig a set of parts makes: each part sets its own stats (worn by its condition); what
        /// no part sets stays the bare rig's.
        /// </summary>
        public static AnglingRigStats Rig(AnglingFile file, IEnumerable<(AnglingTackleDef Part, double Condition)> parts)
        {
            AnglingRigStats rig = file.BareRig;
            foreach (var (part, condition) in parts)
            {
                int worn = file.WornPercent;
                if (part.SnapBuffer > 0) rig.SnapBuffer = Worn(part.SnapBuffer, condition, worn);
                if (part.Strength > 0) rig.Strength = Worn(part.Strength, condition, worn);
                if (part.ReelSpeed > 0) rig.ReelSpeed = Worn(part.ReelSpeed, condition, worn);
                if (part.RunDrag > 0) rig.RunDrag = Worn(part.RunDrag, condition, worn);
                if (part.HookHold > 0) rig.HookHold = Worn(part.HookHold, condition, worn);
                rig.Lantern |= part.Lantern;
                rig.Listening |= part.Listening;
            }
            return rig;
        }

        /// <summary>Whether a lure takes a species: by id, or "tier:N" for every species of tier N and below.</summary>
        public static bool Bites(AnglingTackleDef lure, AnglingSpeciesDef species)
        {
            foreach (string b in lure.Bites)
            {
                if (b == species.Id) return true;
                if (b.StartsWith("tier:", StringComparison.Ordinal) && int.TryParse(b.Substring(5), out int tier) && species.Tier <= tier) return true;
            }
            return false;
        }
    }
}
