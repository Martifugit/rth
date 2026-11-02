using Rth.Server.Content;
using Rth.Shared.Angling;
using Rth.Shared.Items;
using Rth.Shared.World;
using Xunit.Abstractions;

namespace Rth.Tests;

/// <summary>
/// The shipped trophy species against the pace table (docs/skills.md 2.1, docs/fishing.md 3.2):
/// at about 20 attempts an hour, a good angler with the best shipped rig of the fish's tier earns
/// more than the table (trophy angling is the engaged method, 1.0-1.6x), every species can be
/// rigged for, and a fish box holds about what AFK fishing gives in the time a trophy takes.
/// </summary>
public class AnglingPaceTests
{
    private static readonly GameContent Content = GameContent.Load(Repo.ContentDirectory);
    private readonly ITestOutputHelper _out;

    public AnglingPaceTests(ITestOutputHelper output) => _out = output;

    private const double AttemptsPerHour = 20;

    private static double Target(int level) => level switch
    {
        < 10 => 4000, < 20 => 5000, < 30 => 6000, < 40 => 10000, < 50 => 18000, < 60 => 38000, < 70 => 77000, < 80 => 155000, < 90 => 335000, _ => 700000,
    };

    public static IEnumerable<object[]> Species() => Content.Angling.Species.Select(s => new object[] { s.Id });

    private static AnglingSpeciesDef Def(string id) => Content.Angling.Species.First(s => s.Id == id);

    /// <summary>The best ordinary (not collector) part for each slot a player of the species' level can use and that suits the fish.</summary>
    internal static List<(AnglingTackleDef Part, double Condition)> BestRig(AnglingSpeciesDef species)
    {
        var parts = new List<(AnglingTackleDef, double)>();
        List<AnglingTackleDef> usable = Content.Items.Values.Select(i => i.Tackle).Where(t => t != null && !t.Collector
            && t.Requirements.All(r => r.Level <= species.Level)).Select(t => t!).ToList();
        foreach (AnglingSlot slot in Enum.GetValues<AnglingSlot>())
        {
            IEnumerable<AnglingTackleDef> mine = usable.Where(t => t.Slot == slot);
            if (slot == AnglingSlot.Lure) mine = mine.Where(t => AnglingRules.Bites(t, species));
            if (slot == AnglingSlot.Line && species.MaxThickness > 0) mine = mine.Where(t => t.Thickness <= species.MaxThickness);
            if (slot == AnglingSlot.Hook && species.Teeth) mine = mine.OrderByDescending(t => t.NoTeethWear);
            AnglingTackleDef? best = mine.OrderByDescending(t => t.Tier).ThenByDescending(t => t.SnapBuffer + t.Strength + t.ReelSpeed + t.RunDrag + t.HookHold).FirstOrDefault();
            if (best != null) parts.Add((best, 100));
        }
        return parts;
    }

    [Theory]
    [MemberData(nameof(Species))]
    public void Every_species_can_be_rigged_for(string id)
    {
        List<(AnglingTackleDef Part, double)> rig = BestRig(Def(id));
        Assert.Contains(rig, p => p.Part.Slot == AnglingSlot.Lure);
        Assert.Contains(rig, p => p.Part.Slot == AnglingSlot.Rod);
        Assert.True(Content.Objects.ContainsKey(Def(id).SpotType));
    }

    [Theory]
    [MemberData(nameof(Species))]
    public void A_good_angler_beats_the_table_at_the_fishes_level(string id)
    {
        AnglingSpeciesDef species = Def(id);
        AnglingRigStats rig = AnglingRules.Rig(Content.Angling, BestRig(species));
        var random = new Random(11);
        int landed = 0, fights = 400;
        double xp = 0, seconds = 0;
        for (int i = 0; i < fights; i++)
        {
            bool revised = random.NextDouble() < Content.Angling.RevisedChance;
            int roll = revised ? 1000 : AnglingRules.SizeRoll(random.NextDouble(), species.SizeSkew);
            var fight = new AnglingFight(new AnglingFish
            {
                Fight = species.Fight, Energy = AnglingRules.Energy(Content.Angling, species, roll, revised), Seed = (uint)random.Next(1, int.MaxValue), Revised = revised,
            }, rig);
            new AnglingBot((uint)(i + 1)).Play(fight);
            seconds += fight.Steps * AnglingFight.StepMs / 1000.0;
            if (fight.Outcome != AnglingOutcome.Landed) continue;
            landed++;
            xp += AnglingRules.Xp(Content.Angling, species, roll, revised);
        }
        double perHour = AttemptsPerHour * xp / fights;
        double target = Target(species.Level);
        _out.WriteLine($"{species.Name}: landed {landed * 100 / fights}%, fights average {seconds / fights:0}s, {perHour:0} XP/h against {target:0} ({perHour / target:0.00}x)");
        Assert.InRange(perHour, target * 1.0, target * 1.6);
    }

    [Theory]
    [MemberData(nameof(Species))]
    public void A_fish_box_holds_about_what_afk_fishing_gives_in_the_same_time(string id)
    {
        AnglingSpeciesDef species = Def(id);
        // AFK at the river spot: its chance at the fish's level, an attempt every few ticks.
        NodeDef river = Content.Objects["river_spot"].Node!;
        double chance = Math.Min(river.MaxChance, river.BaseChance + river.PerLevel * (species.Level - river.Level));
        double afkFishPerHour = 3600 / (river.Ticks * 0.6) * chance;
        double afkPerTrophy = afkFishPerHour / AttemptsPerHour;
        // A box by the size classes' shares (the size roll's distribution), each roll's average count.
        double perRoll = species.Box.Sum(e => e.Weight * (e.Min + e.Max) / 2.0) / species.Box.Sum(e => e.Weight);
        var random = new Random(3);
        double rolls = 0;
        for (int i = 0; i < 2000; i++) rolls += AnglingRules.ClassOf(Content.Angling, AnglingRules.SizeRoll(random.NextDouble(), species.SizeSkew))!.BoxRolls;
        double perBox = rolls / 2000 * perRoll;
        _out.WriteLine($"{species.Name}: a box holds {perBox:0.0} on average; AFK gives {afkPerTrophy:0.0} in a trophy's time");
        Assert.InRange(perBox, afkPerTrophy * 0.6, afkPerTrophy * 1.6);
    }

    [Fact]
    public void Tackle_made_at_the_gear_bench_keeps_to_the_table()
    {
        foreach (var r in Content.Recipes.Values.Where(r => r.Station == "net_bench"))
        {
            double perHour = 3600 / (r.Ticks * 0.6) * r.Xp;
            Assert.True(perHour >= Target(r.Level) * 0.6 && perHour <= Target(r.Level) * 1.5, r.Id + " " + perHour);
        }
    }

    [Fact]
    public void Every_lure_and_collector_trade_points_at_real_things()
    {
        foreach (ItemDef item in Content.Items.Values.Where(i => i.Tackle?.Slot == AnglingSlot.Lure))
            Assert.Contains(Content.Angling.Species, s => AnglingRules.Bites(item.Tackle!, s));
        foreach (AnglingSpeciesDef s in Content.Angling.Species)
            Assert.Contains(Content.Uses, u => u.Item == AnglingContent.TrophyId(s.Id, AnglingContent.RevisedClass) && u.OnNpc == "collector");
    }
}
