using Rth.Shared.Angling;
using Xunit.Abstractions;

namespace Rth.Tests;

/// <summary>
/// Trophy angling's fight (docs/fishing.md 3.3, AN1): a good player lands fish in the lengths the
/// design asks for, a careless one or an idle one loses them, the wrong rig costs fish, and the
/// same inputs always give the same fight (the server replays the client's).
/// </summary>
public class AnglingFightTests
{
    private readonly ITestOutputHelper _out;

    public AnglingFightTests(ITestOutputHelper output) => _out = output;

    internal static AnglingFightDef Perch() => new() { Turn = 10, TellSteps = 16, MoveMinSteps = 30, MoveMaxSteps = 55, Energy = 6900, Distance = 650 };
    internal static AnglingFightDef Eel() => new() { Turn = 8, Dive = 5, TellSteps = 14, MoveMinSteps = 28, MoveMaxSteps = 50, Energy = 6800, Distance = 700 };
    internal static AnglingFightDef Pike() => new() { Turn = 10, Dive = 2, Run = 4, TellSteps = 11, MoveMinSteps = 20, MoveMaxSteps = 40, BreatherSteps = 45, Energy = 6400, Distance = 800, RunPull = 120 };
    internal static AnglingFightDef Lamprey() => new() { Turn = 8, Dive = 2, Run = 2, Latch = 3, TellSteps = 12, MoveMinSteps = 24, MoveMaxSteps = 45, Energy = 6500, Distance = 800 };
    internal static AnglingFightDef Char() => new() { Turn = 6, Run = 4, Jump = 4, TellSteps = 11, MoveMinSteps = 24, MoveMaxSteps = 44, Energy = 4200, Distance = 700 };
    internal static AnglingFightDef Sturgeon() => new() { Turn = 6, Dive = 5, Run = 3, Jump = 2, TellSteps = 12, MoveMinSteps = 40, MoveMaxSteps = 80, BreatherSteps = 60, Energy = 7900, Distance = 750 };

    internal static AnglingRigStats GoodRig() => new() { SnapBuffer = 24, Strength = 110, ReelSpeed = 105, RunDrag = 115, HookHold = 40 };

    private static AnglingFight Fight(AnglingFightDef def, int roll, uint seed, bool revised = false, AnglingRigStats? rig = null)
    {
        int energy = revised ? def.Energy * AnglingRules.EnergyPercent(1000) / 100 * 180 / 100 : def.Energy * AnglingRules.EnergyPercent(roll) / 100;
        return new AnglingFight(new AnglingFish { Fight = def, Energy = energy, Seed = seed, Revised = revised }, rig ?? GoodRig());
    }

    /// <summary>Plays many fights with a good bot: the share landed and the average seconds for those landed.</summary>
    private static (double Landed, double Seconds) Run(AnglingFightDef def, int roll, bool revised = false, int count = 200, AnglingRigStats? rig = null, int mistakes = 0)
    {
        int landed = 0;
        long steps = 0;
        for (int i = 0; i < count; i++)
        {
            AnglingFight fight = Fight(def, roll, (uint)(1000 + i * 7919), revised, rig);
            new AnglingBot((uint)(77 + i)) { MistakesPerThousand = mistakes }.Play(fight);
            if (fight.Outcome != AnglingOutcome.Landed) continue;
            landed++;
            steps += fight.Steps;
        }
        return (landed / (double)count, landed == 0 ? 0 : steps / (double)landed * AnglingFight.StepMs / 1000.0);
    }

    public static IEnumerable<object[]> Species() => new[]
    {
        new object[] { "perch" }, new object[] { "eel" }, new object[] { "pike" },
        new object[] { "lamprey" }, new object[] { "char" }, new object[] { "sturgeon" },
    };

    private static AnglingFightDef Def(string name) => name switch
    {
        "perch" => Perch(), "eel" => Eel(), "pike" => Pike(), "lamprey" => Lamprey(), "char" => Char(), _ => Sturgeon(),
    };

    [Theory]
    [MemberData(nameof(Species))]
    public void A_good_angler_lands_fish_in_the_designed_lengths(string name)
    {
        AnglingFightDef def = Def(name);
        var small = Run(def, 120);
        var average = Run(def, 500);
        var big = Run(def, 950);
        var revised = Run(def, 1000, revised: true);
        _out.WriteLine($"{name}: small {small.Seconds:0}s ({small.Landed:P0}), average {average.Seconds:0}s ({average.Landed:P0}), big {big.Seconds:0}s ({big.Landed:P0}), revised {revised.Seconds:0}s ({revised.Landed:P0})");

        Assert.True(small.Landed > 0.9 && average.Landed > 0.85 && big.Landed > 0.7, "a good angler lands most fish");
        Assert.InRange(small.Seconds, 12, 36);
        Assert.InRange(average.Seconds, 25, 65);
        Assert.InRange(big.Seconds, 55, 140);
        Assert.InRange(revised.Seconds, 70, 250);
    }

    [Theory]
    [MemberData(nameof(Species))]
    public void Once_spent_a_fish_is_reeled_in_within_seconds(string name)
    {
        // The challenge is tiring it; after that you more or less reel it in (owner, 2026-11-02).
        for (uint seed = 1; seed <= 40; seed++)
        {
            AnglingFight fight = Fight(Def(name), 600, seed);
            var bot = new AnglingBot(seed);
            int spentAt = -1;
            while (!fight.Over)
            {
                fight.Step(bot.Decide(fight));
                if (spentAt < 0 && fight.Energy == 0) spentAt = fight.Steps;
            }
            Assert.Equal(AnglingOutcome.Landed, fight.Outcome);
            if (spentAt < 0) continue;     // landed before it was quite spent: fine
            double reelSeconds = (fight.Steps - spentAt) * AnglingFight.StepMs / 1000.0;
            double fightSeconds = spentAt * AnglingFight.StepMs / 1000.0;
            Assert.True(reelSeconds < 12, name + ": reeling in took " + reelSeconds + "s");
            Assert.True(fightSeconds > reelSeconds * 2, name + ": the fight (" + fightSeconds + "s) should be most of it, not the reeling (" + reelSeconds + "s)");
        }
    }

    [Fact]
    public void Reeling_well_but_ignoring_where_it_pulls_never_lands_it()
    {
        // Keeping the line in the green is not enough: the fish must be fought (owner, 2026-11-02).
        int landed = 0;
        for (uint seed = 1; seed <= 60; seed++)
        {
            AnglingFight fight = Fight(Perch(), 400, seed);
            var bot = new AnglingBot(seed);
            while (!fight.Over)
            {
                byte input = bot.Decide(fight);
                fight.Step(AnglingInput.Make(AnglingInput.Reeling(input), AnglingInput.PullNone));
            }
            if (fight.Outcome == AnglingOutcome.Landed) landed++;
        }
        Assert.True(landed < 6, "landed " + landed + " of 60 without ever pulling against the fish");
    }

    [Fact]
    public void Doing_nothing_loses_the_fish()
    {
        for (uint seed = 1; seed < 30; seed++)
        {
            AnglingFight fight = Fight(Perch(), 300, seed);
            while (!fight.Over) fight.Step(0);
            Assert.NotEqual(AnglingOutcome.Landed, fight.Outcome);
        }
    }

    [Fact]
    public void Reeling_without_pulling_rarely_lands_a_pike()
    {
        int landed = 0;
        for (uint seed = 1; seed <= 100; seed++)
        {
            AnglingFight fight = Fight(Pike(), 500, seed);
            while (!fight.Over) fight.Step(AnglingInput.Make(true, AnglingInput.PullNone));
            if (fight.Outcome == AnglingOutcome.Landed) landed++;
        }
        Assert.True(landed < 10, "landed " + landed + " of 100 by holding reel");
    }

    [Fact]
    public void A_careless_angler_loses_more_fish()
    {
        var careful = Run(Pike(), 700);
        var careless = Run(Pike(), 700, mistakes: 350);
        _out.WriteLine($"careful {careful.Landed:P0}, careless {careless.Landed:P0}");
        Assert.True(careless.Landed < careful.Landed - 0.15);
    }

    [Fact]
    public void A_light_reel_and_weak_line_lose_big_pike()
    {
        var right = Run(Pike(), 950);
        var wrong = Run(Pike(), 950, rig: new AnglingRigStats { SnapBuffer = 12, Strength = 80, ReelSpeed = 100, RunDrag = 70, HookHold = 30 });
        _out.WriteLine($"right rig {right.Landed:P0}, wrong rig {wrong.Landed:P0}");
        Assert.True(wrong.Landed < right.Landed - 0.2);
    }

    [Fact]
    public void Tight_line_on_a_jump_pulls_the_hook()
    {
        int lost = 0;
        for (uint seed = 1; seed <= 60; seed++)
        {
            AnglingFight fight = Fight(Char(), 500, seed);
            var bot = new AnglingBot(seed);
            while (!fight.Over)
            {
                byte input = bot.Decide(fight);
                // Reel hard through every jump.
                if (fight.Current == AnglingMove.Jump) input = AnglingInput.Make(true, AnglingInput.PullNone);
                fight.Step(input);
            }
            if (fight.Outcome == AnglingOutcome.HookFree) lost++;
        }
        Assert.True(lost > 40, "lost " + lost + " of 60 to jumps");
    }

    [Fact]
    public void The_same_inputs_give_the_same_fight()
    {
        AnglingFight first = Fight(Sturgeon(), 800, 4242);
        List<byte> inputs = new AnglingBot(9).Play(first);
        AnglingFight replay = Fight(Sturgeon(), 800, 4242);
        foreach (byte input in inputs) replay.Step(input);
        Assert.Equal(first.Outcome, replay.Outcome);
        Assert.Equal(first.Steps, replay.Steps);
        Assert.Equal(first.Energy, replay.Energy);
        Assert.Equal(first.Seen, replay.Seen);
        Assert.Equal(first.RedSteps, replay.RedSteps);
    }

    [Fact]
    public void Inputs_round_trip_in_batches()
    {
        List<byte> inputs = new AnglingBot(3).Play(Fight(Pike(), 600, 77));
        var decoded = new List<byte>();
        var batch = new List<byte>();
        int at = 0;
        while (at < inputs.Count)
        {
            string text = AnglingInputs.Encode(at, inputs, at, 200, out int taken);
            Assert.True(text.Length <= 200);
            Assert.True(AnglingInputs.TryDecode(text, out int start, batch));
            Assert.Equal(at, start);
            decoded.AddRange(batch);
            at += taken;
        }
        Assert.Equal(inputs, decoded);
        Assert.False(AnglingInputs.TryDecode("5:40x3", out _, batch));
        Assert.False(AnglingInputs.TryDecode("x", out _, batch));
    }

    [Fact]
    public void Tells_hide_in_the_dark_without_a_lantern_and_never_show_for_hushfin()
    {
        var dark = new AnglingFightDef { Dark = true };
        Assert.False(new AnglingFight(new AnglingFish { Fight = dark }, GoodRig()).TellVisible);
        AnglingRigStats lit = GoodRig();
        lit.Lantern = true;
        Assert.True(new AnglingFight(new AnglingFish { Fight = dark }, lit).TellVisible);
        Assert.False(new AnglingFight(new AnglingFish { Fight = new AnglingFightDef { NoTells = true } }, lit).TellVisible);
        AnglingRigStats listening = GoodRig();
        listening.Listening = true;
        Assert.True(new AnglingFight(new AnglingFish { Fight = new AnglingFightDef { NoTells = true } }, listening).TellVisible);
    }
}

