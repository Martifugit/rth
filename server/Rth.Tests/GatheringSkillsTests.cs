using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
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
                Roam = new NodeRoamDef { Active = 1, GroupRadius = 6, MinTicks = 1000, MaxTicks = 1000 },
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

    [Fact]
    public void ARoamingGroupHasOneSpotAndTheRestAreDormant()
    {
        var (fx, alice) = World();
        var spots = fx.World.Get<GatheringSystem>().Nodes.Where(n => n.Placed.Type == "spot").ToList();
        Assert.Equal(1, spots.Count(n => !n.Dormant));
        Assert.Equal(2, State(alice).Dormant.Count(d => d.Type == "spot"));
        GatheringSystem.Node asleep = spots.First(n => n.Dormant);
        Act(fx, alice, asleep.Placed, ticks: 3);
        Assert.Contains(alice.Texts, t => t == "There's nothing here now.");
    }

    [Fact]
    public void CatchesFollowLevelAndUsedUpSpotsMove()
    {
        var (fx, alice) = World();
        GatheringSystem.Node spot = fx.World.Get<GatheringSystem>().Nodes.First(n => n.Placed.Type == "spot" && !n.Dormant);
        Act(fx, alice, spot.Placed, ticks: 8);
        Assert.True(Count(fx, "Alice", "perch") > 0);
        Assert.Equal(0, Count(fx, "Alice", "pike"));             // pike needs level 20
        fx.World.Get<SkillSystem>().AddXp(fx.Player("Alice"), "butchering", fx.World.Content.XpCurve.XpForLevel(20));
        fx.Ticks(6);
        Assert.True(Count(fx, "Alice", "pike") > 0);               // the heavy weight wins

        // A spot that is used up moves to another of its group.
        var parts = Parts();
        parts.Objects.Single(o => o.Id == "spot").Node!.DepleteChance = 1;
        var fx2 = new WorldFixture(Maps.Open(30, 30, Spawn), new ServerConfig(), GameContent.Create(parts), null, Placed);
        FakeConnection bob = fx2.Login("Bob");
        GatheringSystem.Node first = fx2.World.Get<GatheringSystem>().Nodes.First(n => n.Placed.Type == "spot" && !n.Dormant);
        Act(fx2, bob, first.Placed, ticks: 4);
        Assert.True(first.Dormant);
        Assert.Equal(1, fx2.World.Get<GatheringSystem>().Nodes.Count(n => n.Placed.Type == "spot" && !n.Dormant));
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
        Assert.Equal(9, fx.World.Get<SkillSystem>().Xp(fx.Player("Alice"), "butchering"));
        Assert.Contains(State(alice).Tended, t => t.X == needy.Placed.X);

        // Base chance 0: only tending (+1) gives logs, and a tended tree isn't cut back (scale 0).
        FakeConnection bob = fx.Login("Bob");
        Act(fx, bob, needy.Placed, ticks: 8);
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
