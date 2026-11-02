using System.Collections.Generic;
using Rth.Shared.Items;
using Rth.Shared.Skills;

namespace Rth.Shared.Angling
{
    /// <summary>
    /// Trophy angling's content (angling.json, docs/fishing.md): the species, the size classes
    /// trophies come in, and the rules every fight shares.
    /// </summary>
    public sealed class AnglingFile
    {
        public string Skill = "fishing";
        public List<AnglingSpeciesDef> Species = new List<AnglingSpeciesDef>();
        /// <summary>From smallest to largest; a fish's class is the first whose <see cref="AnglingSizeClass.UpTo"/> its size roll is under.</summary>
        public List<AnglingSizeClass> SizeClasses = new List<AnglingSizeClass>();
        /// <summary>The chance a bite is a revised fish (the rare variant).</summary>
        public double RevisedChance = 1.0 / 60;
        /// <summary>A revised fish's energy, in percent of a record fish of its species.</summary>
        public int RevisedEnergyPercent = 180;
        /// <summary>A revised fish's XP, times the species' XP.</summary>
        public double RevisedXp = 3;
        /// <summary>Fish boxes for a revised fish roll this many times.</summary>
        public int RevisedBoxRolls = 25;
        /// <summary>Ticks between casting and a bite, at random.</summary>
        public int BiteMinTicks = 4;
        public int BiteMaxTicks = 14;
        /// <summary>Reeling a little faster for each level above a species' level, up to <see cref="ReelBonusLevels"/> levels.</summary>
        public int ReelBonusPerLevel = 15;   // tenths of a percent: 1.5% a level
        public int ReelBonusLevels = 10;
        /// <summary>Ticks without the client's inputs before a fight ends with the fish gone.</summary>
        public int QuietTicks = 20;
        /// <summary>The rig a player has with nothing in a slot.</summary>
        public AnglingRigStats BareRig = AnglingRigStats.Bare();
        /// <summary>At condition 0 a part gives this percent of its stats (it never breaks).</summary>
        public int WornPercent = 60;
    }

    /// <summary>A size class: the trophy item made for it, its XP and box multipliers.</summary>
    public sealed class AnglingSizeClass
    {
        public string Id = "";
        public string Name = "";
        /// <summary>The size roll (0-1000) this class runs up to, exclusive; the largest class should be 1001.</summary>
        public int UpTo = 1001;
        /// <summary>How many times its fish box rolls.</summary>
        public int BoxRolls = 5;
    }

    /// <summary>
    /// One species of trophy fish: where it lives (its spot object type), how its spots come and
    /// go, how big it grows, how it fights, what it pays and what you learn about it.
    /// </summary>
    public sealed class AnglingSpeciesDef
    {
        public string Id = "";
        public string Name = "";
        public int Tier = 1;
        /// <summary>The Fishing level to hook one.</summary>
        public int Level = 1;
        /// <summary>The placed object type its trophy spots are (needs a "cast" action).</summary>
        public string SpotType = "";
        /// <summary>How many of its spots are active at once, and for how long each.</summary>
        public int Active = 2;
        public int ActiveMinTicks = 2000;
        public int ActiveMaxTicks = 4000;
        /// <summary>Catches at one spot before its fish are spooked for that player, and for how long.</summary>
        public int SpookAfter = 4;
        public int SpookTicks = 1000;
        /// <summary>Weights in grams: the smallest and the largest trophy.</summary>
        public int MinGrams = 500;
        public int MaxGrams = 2000;
        /// <summary>How the size roll is skewed: above 1, big fish are rarer.</summary>
        public double SizeSkew = 2;
        /// <summary>XP for an average fish (size roll 500); smaller and bigger scale from it.</summary>
        public double Xp = 100;
        /// <summary>The thickest line it will bite on (a line's <see cref="AnglingTackleDef.Thickness"/>); 0: any.</summary>
        public int MaxThickness;
        public AnglingFightDef Fight = new AnglingFightDef();
        /// <summary>What its fish boxes hold: each roll picks one by weight.</summary>
        public List<AnglingBoxEntry> Box = new List<AnglingBoxEntry>();
        /// <summary>Lines for the angler's notes, unlocked by catches or by people.</summary>
        public List<AnglingNoteDef> Notes = new List<AnglingNoteDef>();
        /// <summary>Client: the ripple its spots show (a name the client draws).</summary>
        public string Ripple = "";
        public string Color = "#7a8c5a";
        public string Icon = "fish";
    }

    public sealed class AnglingBoxEntry
    {
        public string Item = "";
        public int Min = 1;
        public int Max = 1;
        public int Weight = 1;
    }

    /// <summary>A line in a species' notes: shown after <see cref="Catches"/> catches, or when a dialogue gives it (Catches 0).</summary>
    public sealed class AnglingNoteDef
    {
        public string Id = "";
        public int Catches;
        public string Text = "";
    }

    /// <summary>The moves a fish makes. Each has its counter (docs/fishing.md 3.3).</summary>
    public enum AnglingMove : byte
    {
        /// <summary>Swimming straight and slowly: a breather, or between moves.</summary>
        Straight = 0,
        Left = 1,
        Right = 2,
        Dive = 3,
        Run = 4,
        Jump = 5,
        Latch = 6,
    }

    /// <summary>
    /// How a species fights. Weights pick its next move; lengths are in fight steps (50 ms).
    /// Energy is for an average trophy; a fish's own energy is scaled by its size.
    /// </summary>
    public sealed class AnglingFightDef
    {
        public int Turn = 10;
        public int Dive;
        public int Run;
        public int Jump;
        public int Latch;
        /// <summary>Steps of warning before each move.</summary>
        public int TellSteps = 14;
        /// <summary>How long a move lasts on a fresh fish, at random between these; a tired fish's moves are shorter.</summary>
        public int MoveMinSteps = 30;
        public int MoveMaxSteps = 60;
        /// <summary>How long it swims straight after a run.</summary>
        public int BreatherSteps = 50;
        public int Energy = 4000;
        /// <summary>How far out it bites (distance units; the net is at 0).</summary>
        public int Distance = 1500;
        /// <summary>How hard its runs pull (percent of the base).</summary>
        public int RunPull = 100;
        /// <summary>Tells show only while the angler's lantern is lit (lightfin).</summary>
        public bool Dark;
        /// <summary>Tells are never shown: read the fish's body (hushfin).</summary>
        public bool NoTells;
    }

    /// <summary>
    /// The numbers a rig brings to a fight, each 100 for an ordinary part. Higher is better for
    /// all of them.
    /// </summary>
    public struct AnglingRigStats
    {
        /// <summary>Rod: steps tension may stay in the red before the line snaps.</summary>
        public int SnapBuffer;
        /// <summary>Line: how strong it is (tension rises more slowly on a strong line).</summary>
        public int Strength;
        /// <summary>Reel: how fast it brings the fish in.</summary>
        public int ReelSpeed;
        /// <summary>Reel: how much it holds against a run (less line out, less tension).</summary>
        public int RunDrag;
        /// <summary>Hook: steps of slack before it pulls free, and how tight a jump may land.</summary>
        public int HookHold;
        /// <summary>Reeling faster from Fishing levels above the species', in tenths of a percent.</summary>
        public int ReelBonus;
        /// <summary>Lantern lit (shows a dark fish's tells).</summary>
        public bool Lantern;
        /// <summary>A faint tell for every move, even a fish with none (the listening reel).</summary>
        public bool Listening;

        public static AnglingRigStats Bare() => new AnglingRigStats { SnapBuffer = 20, Strength = 100, ReelSpeed = 100, RunDrag = 100, HookHold = 30 };
    }

    /// <summary>The slots of a rig.</summary>
    public enum AnglingSlot : byte
    {
        Rod = 0,
        Reel = 1,
        Line = 2,
        Hook = 3,
        Lure = 4,
    }

    /// <summary>
    /// A tackle part (<c>tackle</c> on an item): which slot it goes in, what it sets on the rig,
    /// which species bite on it (lures), how it wears and how it's mended.
    /// </summary>
    public sealed class AnglingTackleDef
    {
        public AnglingSlot Slot;
        public int Tier = 1;
        /// <summary>Using it: Fishing at its tier.</summary>
        public List<SkillRequirement> Requirements = new List<SkillRequirement>();
        /// <summary>The rig stats it sets (its slot's own; a rod's snapBuffer, a reel's reelSpeed and runDrag...). 0: the bare rig's.</summary>
        public int SnapBuffer;
        public int Strength;
        public int ReelSpeed;
        public int RunDrag;
        public int HookHold;
        /// <summary>Line: how thick (line-shy fish won't bite above their maxThickness).</summary>
        public int Thickness;
        public bool Lantern;
        public bool Listening;
        /// <summary>Lures: the species that bite on it (ids, or "tier:N" for every species of a tier and below).</summary>
        public List<string> Bites = new List<string>();
        /// <summary>Condition lost per fight, and per 100 steps of the hard thing it suffers (line: in the red; reel: runs; hook: slack and jumps).</summary>
        public double WearPerFight = 1;
        public double WearPerHard = 2;
        /// <summary>Hooks only: don't dull on toothy fish (no extra wear from pike).</summary>
        public bool NoTeethWear;
        /// <summary>Can't be made: only the collector mends it.</summary>
        public bool Collector;
        public AnglingRepairDef? Repair;
    }

    /// <summary>Mending a part yourself at a tackle bench: what it takes, and the skill to do it.</summary>
    public sealed class AnglingRepairDef
    {
        public List<ItemCost> Materials = new List<ItemCost>();
        public List<SkillRequirement> Requirements = new List<SkillRequirement>();
    }
}
