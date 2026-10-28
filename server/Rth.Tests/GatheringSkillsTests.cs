using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>
/// The gathering skills' own ways of working a node (docs/skills.md 7): catches by level, tools
/// that help but aren't needed, spots that roam instead of running out, traps left to fill, and
/// tending (one tree of a cluster at a time needs it, and then gives more for everyone).
/// </summary>
public class GatheringSkillsTests
{
    private static readonly TileCoord Spawn = new(10, 10);

    private static ContentParts Parts()
    {
        ContentParts parts = TestContent.Parts();
        parts.Statuses.Add(new StatusDef { Id = "whisperer", Name = "Whisperer" });
        parts.Items.Add(new ItemDef { Id = "perch", Name = "Perch" });
        parts.Items.Add(new ItemDef { Id = "pike", Name = "Pike" });
        parts.Items.Add(new ItemDef { Id = "herb", Name = "Herb" });
        parts.Items.Add(new ItemDef { Id = "log", Name = "Log" });
        parts.Items.Add(new ItemDef { Id = "sickle", Name = "Sickle", Tool = new ToolDef { Kind = "sickle", Tier = 1, Success = 0.5 } });
        parts.Items.Add(new ItemDef { Id = "eel_trap", Name = "Eel trap", Tags = { "trap" } });
        parts.Items.Add(new ItemDef { Id = "eel", Name = "Eel" });
        parts.Objects.Add(new ObjectDef
        {
            Id = "spot", Name = "Fishing spot", Actions = { new ActionDef { Id = "gather", Label = "Fish" } },
            Node = new NodeDef
            {
                Skill = "butchering", Ticks = 1, BaseChance = 1, MinChance = 1, MaxChance = 1, DepleteChance = 0,
                Catches = { new NodeCatch { Item = "perch", Level = 1, Xp = 5 }, new NodeCatch { Item = "pike", Level = 20, Xp = 20, Weight = 1000 } },
                Roam = new NodeRoamDef { MinTicks = 1000, MaxTicks = 1000, AwayMinTicks = 1000, AwayMaxTicks = 1000, BubbleChance = 0, BubbleTicks = 10,
                    Reading = new NodeReadingDef { Status = "whisperer", PerStack = 0.2, MaxStacks = 3, DecayTicks = 20, Message = "Read it." } },
            },
        });
        parts.Objects.Add(new ObjectDef
        {
            Id = "patch", Name = "Herb patch", Actions = { new ActionDef { Id = "gather", Label = "Pick" } },
            Node = new NodeDef
            {
                Skill = "butchering", Ticks = 1, BaseChance = 0.2, PerLevel = 0, MinChance = 0, MaxChance = 1, Items = { new ItemCost { Item = "herb" } },
                Tool = "sickle", ToolOptional = true, YieldsMin = 1000, YieldsMax = 1000,
            },
        });
        parts.Objects.Add(new ObjectDef
        {
            Id = "weir", Name = "Eel weir", Actions = { new ActionDef { Id = "gather", Label = "Check" } },
            Node = new NodeDef
            {
                Skill = "butchering", BaseChance = 1, MinChance = 1, MaxChance = 1, DepleteChance = 0, Items = { new ItemCost { Item = "eel" } }, Xp = 7,
                Trap = new NodeTrapDef { Item = "tag:trap", Ticks = 10, Max = 3 },
            },
        });
        parts.Objects.Add(new ObjectDef
        {
            Id = "tree", Name = "Tree", Actions = { new ActionDef { Id = "gather", Label = "Chop" }, new ActionDef { Id = "tend", Label = "Tend" } },
            Node = new NodeDef
            {
                Skill = "butchering", Ticks = 1, BaseChance = 0, PerLevel = 0, MinChance = 0, MaxChance = 1, Items = { new ItemCost { Item = "log" } },
                DepleteChance = 1, YieldsMin = 1000, YieldsMax = 1000,
                Tending = new NodeTendingDef { GroupRadius = 6, EveryTicks = 20, NeedTicks = 30, TendedTicks = 50, SuccessBonus = 1, DepleteScale = 0, Xp = 9, NeedMessage = "A tree {where} needs tending." },
            },
        });
        return parts;
    }

    private static readonly PlacedObject[] Placed =
    {
        new() { Type = "spot", X = 11, Y = 10 }, new() { Type = "spot", X = 13, Y = 10 }, new() { Type = "spot", X = 15, Y = 10 },
        new() { Type = "patch", X = 9, Y = 10 },
        new() { Type = "weir", X = 10, Y = 11 },
        new() { Type = "tree", X = 4, Y = 4 }, new() { Type = "tree", X = 6, Y = 4 },
    };

    private static (WorldFixture Fx, FakeConnection Alice) World()
    {
        var fx = new WorldFixture(Maps.Open(30, 30, Spawn), new ServerConfig(), GameContent.Create(Parts()), null, Placed);
        return (fx, fx.Login("Alice"));
    }

    private static void Act(WorldFixture fx, FakeConnection c, PlacedObject o, string action = "gather", int ticks = 1)
    {
        c.FromClient(new ObjectActionMessage { ObjectType = o.Type, ActionId = action, Plane = o.Plane, X = o.X, Y = o.Y });
        fx.Ticks(ticks);
    }

    private static int Count(WorldFixture fx, string who, string item) => fx.World.Get<ItemSystem>().CountOf(fx.Player(who), item);

    private static GatheringSystem.Node NodeOf(WorldFixture fx, PlacedObject o) => fx.World.Get<GatheringSystem>().Nodes.Single(n => n.Placed.X == o.X && n.Placed.Y == o.Y);

    private static GatheringWorldState State(FakeConnection c) =>
        c.Sent.OfType<JsonStateMessage>().LastOrDefault(m => m.Key == GatheringWorldState.StateKey)?.Parse<GatheringWorldState>() ?? new GatheringWorldState();

    /// <summary>Sets a spot's state by hand: here (or gone, maybe bubbling), with its next change far off.</summary>
    private static GatheringSystem.Node Spot(WorldFixture fx, int index, bool here, bool bubbling = false, int changeIn = 1000)
    {
        GatheringSystem.Node n = NodeOf(fx, Placed[index]);
        uint now = fx.World.CurrentTick;
        n.Dormant = !here;
        n.NextChange = now + (uint)changeIn;
        n.BubbleFrom = !here && bubbling ? now : 0;
        return n;
    }

    [Fact]
    public void SpotsComeAndGoEachOnItsOwnTimer()
    {
        var parts = Parts();
        NodeRoamDef roam = parts.Objects.Single(o => o.Id == "spot").Node!.Roam!;
        roam.MinTicks = 20; roam.MaxTicks = 60; roam.AwayMinTicks = 15; roam.AwayMaxTicks = 40;
        var fx = new WorldFixture(Maps.Open(30, 30, Spawn), new ServerConfig(), GameContent.Create(parts), null, Placed);
        fx.Login("Alice");
        var spots = fx.World.Get<GatheringSystem>().Nodes.Where(n => n.Placed.Type == "spot").ToList();
        var changes = new int[spots.Count];
        var seen = new HashSet<string>();
        bool[] was = spots.Select(n => n.Dormant).ToArray();
        for (int t = 0; t < 600; t++)
        {
            fx.Ticks(1);
            for (int k = 0; k < spots.Count; k++)
                if (spots[k].Dormant != was[k]) { changes[k]++; was[k] = spots[k].Dormant; }
            seen.Add(string.Concat(spots.Select(n => n.Dormant ? "0" : "1")));
        }
        Assert.All(changes, c => Assert.True(c >= 4, "changed " + c));   // each came and went, many times
        Assert.True(seen.Count >= 5);                                      // in many mixes: no spot waits on another
    }

    [Fact]
    public void AGoneSpotCantBeFishedButABubblingOneCanAndReadsTheWater()
    {
        var (fx, alice) = World();
        Player p = fx.Player("Alice");
        GatheringSystem gathering = fx.World.Get<GatheringSystem>();
        Spot(fx, 0, here: false);
        Act(fx, alice, Placed[0], ticks: 3);
        Assert.Contains(alice.Texts, t => t == "There's nothing here now.");

        GatheringSystem.Node n = Spot(fx, 0, here: false, bubbling: true, changeIn: 8);
        Act(fx, alice, Placed[0], ticks: 3);
        Assert.Contains(alice.Texts, t => t.Contains("bubbling"));
        Assert.Contains(State(alice).Bubbling, b => b.X == Placed[0].X);
        Assert.Equal(0, Count(fx, "Alice", "perch"));              // waiting: nothing yet
        Assert.NotNull(p.Interaction);
        fx.Ticks(8);                                               // the fish arrive while she waits
        Assert.False(n.Dormant);
        Assert.Equal(1, gathering.ReadingStacks(p, "whisperer"));
        Assert.Contains(alice.Texts, t => t == "Read it.");
        fx.Ticks(4);
        Assert.True(Count(fx, "Alice", "perch") > 0);              // and she fishes on
        Assert.Contains(fx.World.StatusesOf(p), st => st.Id == "whisperer" && st.Magnitude == 20);
    }

    [Fact]
    public void StacksRaiseTheCatchAndWearOffOneAtATime()
    {
        var parts = Parts();
        parts.Objects.Single(o => o.Id == "spot").Node!.BaseChance = 0.25;
        parts.Objects.Single(o => o.Id == "spot").Node!.MinChance = 0;
        var fx = new WorldFixture(Maps.Open(30, 30, Spawn), new ServerConfig(), GameContent.Create(parts), null, Placed);
        FakeConnection alice = fx.Login("Alice");
        Player p = fx.Player("Alice");
        GatheringSystem gathering = fx.World.Get<GatheringSystem>();
        NodeDef def = parts.Objects.Single(o => o.Id == "spot").Node!;
        for (int i = 0; i < 4; i++)                                 // four reads; the cap is three
        {
            Spot(fx, 0, here: false, bubbling: true, changeIn: 4);
            Act(fx, alice, Placed[0], ticks: 6);
        }
        Assert.Equal(3, gathering.ReadingStacks(p, "whisperer"));
        Assert.Equal(0.6, gathering.ReadingBonus(p, def), 6);       // a fifth more a stack
        alice.FromClient(new WalkToMessage { X = 20, Y = 20 });     // she stops fishing
        fx.Ticks(21);
        Assert.Equal(2, gathering.ReadingStacks(p, "whisperer"));   // one wears off every 20 ticks
        fx.Ticks(20);
        Assert.Equal(1, gathering.ReadingStacks(p, "whisperer"));
        fx.Ticks(20);
        Assert.Equal(0, gathering.ReadingStacks(p, "whisperer"));
        Assert.DoesNotContain(fx.World.StatusesOf(p), st => st.Id == "whisperer");
    }

    [Fact]
    public void CatchesFollowLevelAndAUsedUpSpotGoes()
    {
        var (fx, alice) = World();
        Spot(fx, 0, here: true);
        Act(fx, alice, Placed[0], ticks: 8);
        Assert.True(Count(fx, "Alice", "perch") > 0);
        Assert.Equal(0, Count(fx, "Alice", "pike"));             // pike needs level 20
        fx.World.Get<SkillSystem>().AddXp(fx.Player("Alice"), "butchering", fx.World.Content.XpCurve.XpForLevel(20));
        fx.Ticks(6);
        Assert.True(Count(fx, "Alice", "pike") > 0);               // the heavy weight wins

        var parts = Parts();
        parts.Objects.Single(o => o.Id == "spot").Node!.DepleteChance = 1;
        var fx2 = new WorldFixture(Maps.Open(30, 30, Spawn), new ServerConfig(), GameContent.Create(parts), null, Placed);
        FakeConnection bob = fx2.Login("Bob");
        GatheringSystem.Node first = Spot(fx2, 0, here: true);
        Spot(fx2, 1, here: true);
        Act(fx2, bob, Placed[0], ticks: 4);
        Assert.True(first.Dormant);                                // used up: gone at once
        Assert.False(NodeOf(fx2, Placed[1]).Dormant);              // the others don't care
    }

    [Fact]
    public void AnOptionalToolHelpsButIsNotNeeded()
    {
        var (fx, alice) = World();
        Act(fx, alice, Placed[3], ticks: 60);
        int bare = Count(fx, "Alice", "herb");
        Assert.InRange(bare, 3, 25);                              // ~20% of ~58 attempts
        Assert.DoesNotContain(alice.Texts, t => t.Contains("need a sickle"));
        fx.World.Get<ItemSystem>().Give(fx.Player("Alice"), "sickle", 1);
        Assert.Equal(0.7, fx.World.Get<GatheringSystem>().SuccessChance(fx.Player("Alice"), fx.World.Content.Objects["patch"].Node!), 6);
    }

    [Fact]
    public void ATrapIsSetLeftAndCollected()
    {
        var (fx, alice) = World();
        PlacedObject weir = Placed[4];
        Act(fx, alice, weir, ticks: 2);
        Assert.Contains(alice.Texts, t => t.Contains("need a trap"));
        fx.World.Get<ItemSystem>().Give(fx.Player("Alice"), "eel_trap", 1);
        Act(fx, alice, weir, ticks: 2);
        Assert.Equal(0, Count(fx, "Alice", "eel_trap"));
        Assert.Equal("Alice", NodeOf(fx, weir).TrapOwner);
        Assert.Contains(State(alice).Traps, t => t.Mine);

        FakeConnection bob = fx.Login("Bob");
        Act(fx, bob, weir, ticks: 3);
        Assert.Contains(bob.Texts, t => t.Contains("Someone else's trap"));
        Assert.Contains(State(bob).Traps, t => !t.Mine);

        fx.Ticks(60);                                             // full: three catches at most
        Act(fx, alice, weir, ticks: 2);
        Assert.Equal(3, Count(fx, "Alice", "eel"));
        Assert.Equal(21, fx.World.Get<SkillSystem>().Xp(fx.Player("Alice"), "butchering"));
        Assert.Equal(1, Count(fx, "Alice", "eel_trap"));
        Assert.Null(NodeOf(fx, weir).TrapOwner);
    }

    [Theory]
    [InlineData(10, 10, "beside you")]
    [InlineData(11, 11, "beside you")]
    [InlineData(10, 20, "to your north")]
    [InlineData(18, 18, "to your north-east")]
    [InlineData(2, 10, "to your west")]
    [InlineData(12, 2, "to your south")]                         // mostly south: the nearer compass point
    [InlineData(14, 2, "to your south-east")]
    public void TheTendingMessageSaysWhichWayTheTreeLies(int x, int y, string words) =>
        Assert.Equal(words, GatheringSystem.Where(new TileCoord(10, 10), new TileCoord(x, y)));

    [Fact]
    public void OneTreeOfAClusterNeedsTendingAndTendingHelpsEveryone()
    {
        var (fx, alice) = World();
        GatheringSystem gathering = fx.World.Get<GatheringSystem>();
        fx.Ticks(25);
        List<GatheringSystem.Node> trees = gathering.Nodes.Where(n => n.Placed.Type == "tree").ToList();
        GatheringSystem.Node? needy = trees.SingleOrDefault(n => n.NeedsTending(fx.World.CurrentTick));
        Assert.NotNull(needy);                                    // one at a time in the cluster
        Assert.Contains(alice.Texts, t => t == "A tree " + GatheringSystem.Where(fx.Player("Alice").Position, needy!.Placed.Tile) + " needs tending.");
        Assert.Single(State(alice).NeedsTending);

        GatheringSystem.Node other = trees.First(n => !ReferenceEquals(n, needy));
        Act(fx, alice, other.Placed, "tend", 6);
        Assert.Contains(alice.Texts, t => t.Contains("doesn't need tending"));

        Act(fx, alice, needy!.Placed, "tend", 6);
        Assert.True(needy.IsTended(fx.World.CurrentTick));
        Assert.Null(fx.Player("Alice").Interaction);                      // tending is its own act: no chopping after
        Assert.Equal(9, fx.World.Get<SkillSystem>().Xp(fx.Player("Alice"), "butchering"));
        Assert.Contains(State(alice).Tended, t => t.X == needy.Placed.X);

        // Base chance 0: only tending (+1) gives logs, and a tended tree isn't cut back (scale 0).
        FakeConnection bob = fx.Login("Bob");
        Act(fx, bob, needy.Placed, ticks: 12);                    // either tree: time to walk there and chop
        Assert.True(Count(fx, "Bob", "log") >= 3);
        Assert.Equal(1000, needy.YieldsLeft);                     // never cut back while tended
        Act(fx, bob, other.Placed, ticks: 8);
        int before = Count(fx, "Bob", "log");
        fx.Ticks(5);
        Assert.Equal(before, Count(fx, "Bob", "log"));           // the untended tree gives nothing at chance 0
    }

    [Fact]
    public void ContentChecksTheNewNodeParts()
    {
        ContentParts parts = Parts();
        parts.Objects.Single(o => o.Id == "tree").Actions.RemoveAll(a => a.Id == "tend");
        parts.Objects.Single(o => o.Id == "weir").Node!.Trap!.Item = "nothing";
        parts.Objects.Single(o => o.Id == "spot").Node!.Catches.Add(new NodeCatch { Item = "ghost" });
        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("'tend' action", e.Message);
        Assert.Contains("trap 'nothing'", e.Message);
        Assert.Contains("catch 'ghost'", e.Message);
    }
}
