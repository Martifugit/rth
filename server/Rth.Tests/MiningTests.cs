using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>
/// Mining's progress nodes and fire-setting (docs/skills.md, Mining): strikes fill a bar towards
/// the ore with a little XP each, a fire set against the rock burns for everyone, and a douse at
/// the stone's peak heat cracks the face for the one who doused it alone.
/// </summary>
public class MiningTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static (WorldFixture Fx, FakeConnection Alice) World()
    {
        ContentParts parts = TestContent.Parts();
        parts.Items.Add(new ItemDef { Id = "ore", Name = "Ore", Value = 1 });
        parts.Items.Add(new ItemDef { Id = "log", Name = "Log", Value = 1, Stackable = true, Tags = { "log" } });
        parts.Items.Add(new ItemDef { Id = "skin", Name = "Waterskin", Value = 1 });
        parts.Items.Add(new ItemDef { Id = "skin_full", Name = "Waterskin (1)", Value = 1, Water = new WaterDef { Leaves = "skin" } });
        parts.Objects.Add(new ObjectDef
        {
            Id = "rock", Name = "Rock",
            Actions = { new ActionDef { Id = "gather", Label = "Mine" } },
            Node = new NodeDef
            {
                Skill = "religion", Ticks = 2, Xp = 10, Items = { new ItemCost { Item = "ore" } }, YieldsMin = 100, YieldsMax = 100, DepleteChance = 0,
                Progress = new NodeProgressDef { Needed = 100, PerStrike = 20, PerLevel = 0, StrikeXp = 1 },
                Fire = new FireSettingDef { LogCount = 2, BurnTicks = 400, RiseTicks = 10, PeakTicks = 4, FallTicks = 6, CrackBonus = 1, CrackStrikes = 10, DouseXp = 8, LightXp = 0 },
            },
        });
        var fx = new WorldFixture(Maps.Open(30, 30, Spawn), new ServerConfig(), GameContent.Create(parts), null,
            new[] { new PlacedObject { Type = "rock", X = 6, Y = 5 } });
        FakeConnection alice = fx.Login("Alice");
        return (fx, alice);
    }

    private static void Act(WorldFixture fx, FakeConnection c, string action)
    {
        c.FromClient(new ObjectActionMessage { ObjectType = "rock", ActionId = action, Plane = 0, X = 6, Y = 5 });
        fx.Ticks(1);
    }

    /// <summary>Uses a pack item on the rock (fire-setting is done this way: logs lay a fire, water douses it).</summary>
    private static void Use(WorldFixture fx, FakeConnection c, string who, string item)
    {
        Rth.Server.Items.ItemContainer pack = fx.World.Get<ItemSystem>().Of(fx.Player(who)).Inventory;
        int slot = Enumerable.Range(0, pack.Size).First(i => !pack.IsEmpty(i) && pack[i].ItemId == item);
        c.FromClient(new UseItemMessage { Slot = slot, ItemId = item, Target = UseTarget.Object, ObjectType = "rock", Plane = 0, X = 6, Y = 5 });
        fx.Ticks(1);
    }

    private static double Xp(WorldFixture fx, string who) => fx.World.Get<SkillSystem>().Xp(fx.Player(who), "religion");
    private static int Ore(WorldFixture fx, string who) => fx.World.Get<ItemSystem>().CountOf(fx.Player(who), "ore");

    [Fact]
    public void StrikesFillTheBarWithALittleXpEachAndTheOreComesWhenItIsFull()
    {
        var (fx, alice) = World();
        Act(fx, alice, "gather");
        fx.Ticks(8);                       // four strikes: 80 of 100
        Assert.Equal(0, Ore(fx, "Alice"));
        Assert.Equal(4, Xp(fx, "Alice"));
        fx.Ticks(2);                       // the fifth fills it
        Assert.Equal(1, Ore(fx, "Alice"));
        Assert.Equal(15, Xp(fx, "Alice")); // five strikes and the ore
        JsonStateMessage state = alice.Sent.OfType<JsonStateMessage>().Last(m => m.Key == MiningState.StateKey);
        Assert.Equal("rock", state.Parse<MiningState>().Type);
    }

    [Fact]
    public void AFireBurnsForEveryoneAndCostsTheLogs()
    {
        var (fx, alice) = World();
        fx.World.Get<ItemSystem>().Give(fx.Player("Alice"), "log", 1);
        Use(fx, alice, "Alice", "log");
        Assert.Contains(alice.Texts, t => t.Contains("need 2 logs"));
        fx.World.Get<ItemSystem>().Give(fx.Player("Alice"), "log", 2);
        Use(fx, alice, "Alice", "log");
        Assert.Equal(1, fx.World.Get<ItemSystem>().CountOf(fx.Player("Alice"), "log"));
        fx.Ticks(3);
        Assert.Null(fx.Player("Alice").Interaction);                      // laying it is its own act: no mining after
        FakeConnection bob = fx.Login("Bob");
        fx.Ticks(1);
        Assert.True(bob.Sent.OfType<JsonStateMessage>().Last(m => m.Key == GatheringSystem.StateKey).Parse<List<NodeState>>().Single().Burning);
    }

    [Fact]
    public void ADouseAtThePeakCracksTheFaceFullyForTheDouserAlone()
    {
        var (fx, alice) = World();
        ItemSystem items = fx.World.Get<ItemSystem>();
        items.Give(fx.Player("Alice"), "log", 2);
        items.Give(fx.Player("Alice"), "skin_full", 1);
        Use(fx, alice, "Alice", "log");
        GatheringSystem gathering = fx.World.Get<GatheringSystem>();
        GatheringSystem.Node node = gathering.Nodes.Single();
        NodeDef def = gathering.Def(node)!;
        while (gathering.HeatFor(fx.Player("Alice"), node, def) < 1) fx.Ticks(1);  // wait for the glow's peak
        double before = Xp(fx, "Alice");
        Use(fx, alice, "Alice", "skin_full");
        Assert.Equal(1, items.CountOf(fx.Player("Alice"), "skin"));      // the skin is empty now
        Assert.True(Xp(fx, "Alice") - before >= 8);                       // the douse's XP, at full heat
        GatheringSystem.Miner miner = fx.Player("Alice").Component<GatheringSystem.Miner>();
        Assert.Equal(1, miner.Crack, 6);
        Assert.Null(fx.Player("Alice").Interaction);                      // dousing is its own act: it doesn't mine
        Act(fx, alice, "gather");                                         // she works the rock again
        miner.Progress = 0;                                               // count from here
        fx.Ticks(4);                                                      // two cracked strikes
        Assert.True(miner.Progress > 2 * 20);                              // more than two plain strikes would give
        Assert.True(gathering.HeatFor(fx.Player("Alice"), node, def) < 0.7); // her side cooled when she doused it, and is climbing again

        FakeConnection bob = fx.Login("Bob");
        fx.Ticks(1);
        Assert.Equal(0, fx.Player("Bob").Component<GatheringSystem.Miner>().Crack);
    }

    [Fact]
    public void AnEarlyDouseCracksItLess()
    {
        var (fx, alice) = World();
        ItemSystem items = fx.World.Get<ItemSystem>();
        items.Give(fx.Player("Alice"), "log", 2);
        items.Give(fx.Player("Alice"), "skin_full", 1);
        Use(fx, alice, "Alice", "log");
        fx.Ticks(2);
        Use(fx, alice, "Alice", "skin_full");
        double crack = fx.Player("Alice").Component<GatheringSystem.Miner>().Crack;
        Assert.InRange(crack, 0.01, 0.6);
        Assert.Contains(alice.Texts, t => t.Contains("hisses") || t.Contains("cracks"));
    }

    [Fact]
    public void DousingNeedsAFireAndAnUncrackedFace()
    {
        var (fx, alice) = World();
        ItemSystem items = fx.World.Get<ItemSystem>();
        items.Give(fx.Player("Alice"), "skin_full", 2);
        Use(fx, alice, "Alice", "skin_full");
        Assert.Contains(alice.Texts, t => t.Contains("no fire"));
        items.Give(fx.Player("Alice"), "log", 2);
        Use(fx, alice, "Alice", "log");
        fx.Ticks(12);
        Use(fx, alice, "Alice", "skin_full");                                       // cracks it
        Assert.Equal(1, items.CountOf(fx.Player("Alice"), "skin_full"));
        Use(fx, alice, "Alice", "skin_full");                                       // still cracked: refused, no water wasted
        Assert.Contains(alice.Texts, t => t.Contains("still cracked"));
        Assert.Equal(1, items.CountOf(fx.Player("Alice"), "skin_full"));
        GatheringSystem gathering = fx.World.Get<GatheringSystem>();
        GatheringSystem.Node node = gathering.Nodes.Single();
        Assert.Equal(0, gathering.HeatFor(fx.Player("Alice"), node, gathering.Def(node)!)); // a cracked face doesn't heat
    }

    [Fact]
    public void TheFireBurnsAtTheFaceItWasLaidFrom()
    {
        var (fx, alice) = World();
        fx.World.Get<ItemSystem>().Give(fx.Player("Alice"), "log", 2);
        Use(fx, alice, "Alice", "log");
        NodeState state = alice.Sent.OfType<JsonStateMessage>().Last(m => m.Key == GatheringSystem.StateKey).Parse<List<NodeState>>().Single();
        Assert.True(state.Burning);
        Assert.Equal(fx.Player("Alice").Position, new TileCoord(state.FireX, state.FireY));
    }
}
