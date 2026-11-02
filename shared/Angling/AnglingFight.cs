using System;

namespace Rth.Shared.Angling
{
    /// <summary>How a fight ended (or None while it goes on).</summary>
    public enum AnglingOutcome : byte
    {
        None = 0,
        Landed = 1,
        /// <summary>Tension stayed in the red too long.</summary>
        Snapped = 2,
        /// <summary>Too much slack for too long, or tight when a jump landed.</summary>
        HookFree = 3,
        /// <summary>It got too far out, or the fight ran too long, or the angler left.</summary>
        Escaped = 4,
    }

    /// <summary>
    /// What the angler does in one step: hold to reel (bit 0), a pull (bits 1-2: none, left, right,
    /// up) and how hard (bits 3-4, stored as weakness so 0 is a full pull: strength 3, 2 or 1).
    /// Packed into a byte, 0-31.
    /// </summary>
    public static class AnglingInput
    {
        public const byte Reel = 1;
        public const byte PullNone = 0, PullLeft = 1, PullRight = 2, PullUp = 3;
        public const int MaxStrength = 3;
        public const byte Max = 31;

        public static byte Make(bool reel, byte pull, int strength = MaxStrength)
        {
            int weakness = MaxStrength - (strength < 1 ? 1 : strength > MaxStrength ? MaxStrength : strength);
            return (byte)((reel ? Reel : 0) | ((pull & 3) << 1) | ((weakness & 3) << 3));
        }

        public static bool Reeling(byte input) => (input & Reel) != 0;
        public static byte Pull(byte input) => (byte)((input >> 1) & 3);
        /// <summary>How hard the pull is, 1 to 3 (3 when there is no pull).</summary>
        public static int Strength(byte input) => MaxStrength - ((input >> 3) & 3) < 1 ? 1 : MaxStrength - ((input >> 3) & 3);
    }

    /// <summary>
    /// The fish a fight is with: its species' fight, its energy (from its size), the seed for its
    /// moves, and whether it is revised (faster, shorter tells). The server rolls it; the client
    /// gets the same numbers, so both run the same fight.
    /// </summary>
    public sealed class AnglingFish
    {
        public AnglingFightDef Fight = new AnglingFightDef();
        public int Energy = 4000;
        public uint Seed = 1;
        public bool Revised;
    }

    /// <summary>
    /// Trophy angling's fight (docs/fishing.md 3.3), a deterministic simulation on fixed 50 ms
    /// steps in whole numbers only, so the server (.NET) and the client (Mono, IL2CPP) step the
    /// same inputs to exactly the same result. The client plays it live; the server steps its own
    /// copy through the client's inputs and decides the outcome.
    ///
    /// Tension runs 0-1000 (slack below <see cref="SlackLine"/>, the red above <see cref="RedLine"/>).
    /// Distance runs from the fish's start out to 0 at the net. Energy is the fight it has left:
    /// correct pulls drain it, and a fresh fish can only be reeled in slowly.
    /// </summary>
    public sealed class AnglingFight
    {
        public const int StepMs = 50;
        public const int StepsPerTick = 12;
        public const int TensionMax = 1000;
        public const int RedLine = 850;
        public const int SlackLine = 80;
        public const int ComfortLow = 300;
        public const int ComfortHigh = 800;
        public const int JumpSteps = 16;
        /// <summary>Six minutes: nothing fights longer.</summary>
        public const int MaxSteps = 7200;
        /// <summary>Tuning: tension per step reeling and giving line; energy a correct pull drains; reeling speed (hundredths of a unit a step) and how much of it reaches a fresh fish (per thousand).</summary>
        public const int TensionReel = 5, TensionGive = 6, DrainCounter = 16, DrainDragged = 2, UnansweredYank = 7, ReelUnits = 300, FreshReel = 150, SpentReelTimes = 3, SlackDrift = 25, RunUnits = 500, RunTire = 7;
        /// <summary>A rod's snapBuffer and a hook's hookHold are counted in these many steps each, so people (not just bots) can keep up.</summary>
        public const int RedStepsPerBuffer = 2, SlackStepsPerHold = 5;

        private readonly AnglingFightDef _def;
        private readonly AnglingRigStats _rig;
        private uint _rng;

        private int _moveLeft;
        private int _tellLeft;
        private AnglingMove _next;
        private bool _lastTurnLeft;
        private int _redRun, _slackRun;
        private bool _spent;

        public AnglingFight(AnglingFish fish, AnglingRigStats rig)
        {
            _def = fish.Fight;
            _rig = rig;
            _rng = fish.Seed == 0 ? 0x9E3779B9u : fish.Seed;
            Revised = fish.Revised;
            Energy0 = Math.Max(1, fish.Energy);
            Energy = Energy0;
            Distance0 = Math.Max(1, _def.Distance);
            _distance = Distance0 * 100;
            Tension = 400;
            Current = AnglingMove.Straight;
            // It bites and swims straight for a moment, then shows its first move.
            _moveLeft = 20;
        }

        public bool Revised { get; }
        public int Steps { get; private set; }
        public AnglingOutcome Outcome { get; private set; }
        public bool Over => Outcome != AnglingOutcome.None;
        public int Tension { get; private set; }
        /// <summary>How far out it is (units; 0 is the net).</summary>
        public int Distance => (_distance + 99) / 100;
        public int Distance0 { get; }
        private int _distance;
        public int Energy { get; private set; }
        public int Energy0 { get; }
        /// <summary>What it is doing now (Straight during a tell).</summary>
        public AnglingMove Current { get; private set; }
        /// <summary>The move it is about to make, while a tell runs.</summary>
        public bool InTell => _tellLeft > 0;
        public AnglingMove Tell => _next;
        /// <summary>Whether the panel shows tells for this fish with this rig.</summary>
        public bool TellVisible => _rig.Listening || !(_def.NoTells || (_def.Dark && !_rig.Lantern));
        /// <summary>Each move it has made, as bits (1 &lt;&lt; move), for the angler's notes.</summary>
        public int Seen { get; private set; }
        /// <summary>Counts of the hard things, for wear: steps in the red, slack, running, jumping, latched.</summary>
        public int RedSteps { get; private set; }
        public int SlackSteps { get; private set; }
        public int RunSteps { get; private set; }
        public int JumpCount { get; private set; }
        public int LatchSteps { get; private set; }

        /// <summary>How close the line is to snapping, per thousand (0: not in the red).</summary>
        public int SnapDanger => _redRun * 1000 / Math.Max(1, Math.Max(1, _rig.SnapBuffer) * RedStepsPerBuffer);

        /// <summary>How close the hook is to pulling free from slack, per thousand (0: the line isn't slack).</summary>
        public int SlackDanger => _slackRun * 1000 / Math.Max(1, Math.Max(1, _rig.HookHold) * SlackStepsPerHold);

        // ------------------------------------------------------------------ randomness (xorshift32: the same everywhere)

        private uint NextRandom()
        {
            uint x = _rng;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rng = x;
            return x;
        }

        /// <summary>A whole number in [min, max].</summary>
        private int Range(int min, int max) => max <= min ? min : min + (int)(NextRandom() % (uint)(max - min + 1));

        /// <summary>Energy left, per thousand.</summary>
        private int Fresh => (int)((long)Energy * 1000 / Energy0);

        private AnglingMove PickMove()
        {
            int fresh = Fresh;
            // A fresh fish runs often; a tired one hardly at all.
            int run = _def.Run * (300 + 700 * fresh / 1000) / 1000;
            int turn = _def.Turn, dive = _def.Dive, jump = _def.Jump, latch = _def.Latch;
            int total = turn + dive + run + jump + latch;
            if (total <= 0) return AnglingMove.Straight;
            int roll = Range(0, total - 1);
            if ((roll -= turn) < 0)
            {
                // Mostly it switches sides.
                bool left = Range(0, 9) < 7 ? !_lastTurnLeft : _lastTurnLeft;
                _lastTurnLeft = left;
                return left ? AnglingMove.Left : AnglingMove.Right;
            }
            if ((roll -= dive) < 0) return AnglingMove.Dive;
            if ((roll -= run) < 0) return AnglingMove.Run;
            if ((roll -= jump) < 0) return AnglingMove.Jump;
            return AnglingMove.Latch;
        }

        private int TellLength => Revised ? _def.TellSteps * 2 / 3 : _def.TellSteps;

        private int MoveLength(AnglingMove move)
        {
            int length = Range(_def.MoveMinSteps, _def.MoveMaxSteps);
            if (Revised) length = length * 3 / 4;
            switch (move)
            {
                case AnglingMove.Jump: return JumpSteps;
                case AnglingMove.Run: return Math.Max(8, length * 2 / 3);
                case AnglingMove.Left:
                case AnglingMove.Right:
                    return Math.Max(6, length);
                default: return Math.Max(6, length);
            }
        }

        // ------------------------------------------------------------------ one step

        /// <summary>Steps the fight once with the angler's input. Does nothing once it's over.</summary>
        public void Step(byte input)
        {
            if (Over) return;
            Steps++;
            AdvanceFish();
            bool reel = AnglingInput.Reeling(input);
            byte pull = AnglingInput.Pull(input);
            AnglingMove move = Current;

            // Tension.
            bool counter = (move == AnglingMove.Left && pull == AnglingInput.PullRight)
                           || (move == AnglingMove.Right && pull == AnglingInput.PullLeft)
                           || (move == AnglingMove.Dive && pull == AnglingInput.PullUp);
            bool wrongPull = pull != AnglingInput.PullNone && !counter;
            int up = 0, down = 0;
            if (reel) up += TensionReel; else down += TensionGive;
            int pullStrength = AnglingInput.Strength(input);
            if (wrongPull) up += 2;
            if (counter) up += pullStrength - 1;     // pulling harder strains the line a little
            int runScale = _def.RunPull * (Revised ? 125 : 100) / Math.Max(1, _rig.RunDrag);
            switch (move)
            {
                case AnglingMove.Left:
                case AnglingMove.Right:
                    // A turn you don't answer yanks the line; answered, it's held.
                    if (!counter) up += UnansweredYank;
                    break;
                case AnglingMove.Dive:
                    if (pull == AnglingInput.PullUp) up += 2;
                    else down += 4;
                    break;
                case AnglingMove.Run:
                    if (reel) up += 22 * runScale / 100;
                    break;
                case AnglingMove.Jump:
                    if (!reel) down += 18;     // in the air: let go and the line falls slack fast
                    break;
                case AnglingMove.Latch:
                    if (reel) up += 5;
                    break;
            }
            int strength = Math.Max(1, _rig.Strength);
            int tension = Tension + up * 100 / strength - down;
            // Giving line to a running fish: the drag holds it near the middle.
            if (move == AnglingMove.Run && !reel) tension = Tension + (550 * runScale / 100 - Tension) / 10;
            Tension = Clamp(tension, 0, TensionMax);

            // Energy.
            int energy = Energy;
            // It tires only from being fought: pulled against, harder the harder you pull, faster while you
            // reel, and faster the nearer the line is to the red (fighting it close to the limit pays).
            int effort = 400 + 1200 * Math.Min(Tension, RedLine) / RedLine;     // per thousand: 400 slack, 1600 at the red
            if (counter) energy -= DrainCounter * pullStrength / AnglingInput.MaxStrength * (reel ? 1000 : 500) / 1000 * effort / 1000;
            else if (move == AnglingMove.Straight && reel) energy -= DrainDragged * effort / 1000;     // dragged in a straight line, it tires a little
            if (move == AnglingMove.Run && !reel) energy -= RunTire;     // running against the drag tires it
            if (move == AnglingMove.Dive && pull != AnglingInput.PullUp) energy += 8;
            if (move == AnglingMove.Straight && !reel && pull == AnglingInput.PullNone) energy += 2;
            // Spent stays spent: no getting its breath back once it has none.
            Energy = _spent ? 0 : Clamp(energy, 0, Energy0);
            if (Energy == 0 && !_spent)
            {
                _spent = true;
                // Whatever it was doing, it gives up: no more running, leaping or holding on.
                Current = AnglingMove.Straight;
                _tellLeft = 0;
                _moveLeft = SpentSteps;
            }

            // Distance.
            // In hundredths of a unit, so small speeds still add up.
            int distance = _distance;
            if (move == AnglingMove.Run) distance += RunUnits * runScale / 100;
            else if (reel && move != AnglingMove.Latch)
            {
                int spent = 1000 - Fresh;
                int speed = ReelUnits * Math.Max(1, _rig.ReelSpeed) / 100 * (1000 + _rig.ReelBonus) / 1000;
                int factor = Energy == 0 ? 1000 * SpentReelTimes : FreshReel + (1000 - FreshReel) * spent / 1000;      // a fresh fish comes in slowly; a spent one is reeled straight in
                int gain = speed * factor / 1000;
                if (move == AnglingMove.Dive) gain /= 2;
                distance -= Math.Max(1, gain);
            }
            else if (!reel) distance += SlackDrift;     // let go and it drifts back a little: what you reeled in mostly stays in
            _distance = Math.Max(0, distance);

            // Counting, and how it ends.
            if (Tension >= RedLine) { RedSteps++; _redRun++; } else _redRun = 0;
            bool slackExempt = move == AnglingMove.Jump || move == AnglingMove.Latch;
            if (Tension <= SlackLine && !slackExempt) { SlackSteps++; _slackRun++; } else _slackRun = 0;
            if (move == AnglingMove.Run) RunSteps++;
            if (move == AnglingMove.Jump) JumpCount++;
            if (move == AnglingMove.Latch) LatchSteps++;

            if (move == AnglingMove.Jump && _moveLeft == 0 && Tension > 200 + _rig.HookHold * 5) Outcome = AnglingOutcome.HookFree;
            else if (_redRun > Math.Max(1, _rig.SnapBuffer) * RedStepsPerBuffer) Outcome = AnglingOutcome.Snapped;
            else if (_slackRun > Math.Max(1, _rig.HookHold) * SlackStepsPerHold) Outcome = AnglingOutcome.HookFree;
            else if (Distance == 0) Outcome = AnglingOutcome.Landed;
            else if (Distance > Distance0 * 3 || Steps >= MaxSteps) Outcome = AnglingOutcome.Escaped;
        }

        /// <summary>The fish's side of a step: the move runs down, a tell, the next move.</summary>
        private void AdvanceFish()
        {
            if (_tellLeft > 0)
            {
                if (--_tellLeft > 0) return;
                Current = _next;
                Seen |= 1 << (int)Current;
                _moveLeft = MoveLength(Current);
                return;
            }
            if (_moveLeft > 0)
            {
                _moveLeft--;
                // A latched fish lets go sooner while the line is slack.
                if (Current == AnglingMove.Latch && Tension <= 300 && _moveLeft > 0) _moveLeft = Math.Max(0, _moveLeft - 2);
                if (_moveLeft > 0) return;
            }
            if (_moveLeft == 0 && Current == AnglingMove.Jump && !_jumpLanded)
            {
                // The landing step itself counts as the jump (checked in Step); the move ends after it.
                _jumpLanded = true;
                return;
            }
            _jumpLanded = false;
            AnglingMove was = Current;
            if (was == AnglingMove.Run)
            {
                // A breather after a run: it swims straight and slowly, and you reel in calmly.
                Current = AnglingMove.Straight;
                _moveLeft = _def.BreatherSteps;
                return;
            }
            Current = AnglingMove.Straight;
            if (Energy == 0)
            {
                // Spent: it has no fight left and swims where it's led. Reel it in.
                _moveLeft = SpentSteps;
                return;
            }
            _next = PickMove();
            _tellLeft = Math.Max(1, TellLength);
        }

        /// <summary>A spent fish swims straight this long, then looks again (it may have got its breath back).</summary>
        private const int SpentSteps = 40;

        private bool _jumpLanded;

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
