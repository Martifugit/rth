using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Skills;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Gathering nodes: attempts, yields, depletion, surges, and the founts' cut.</summary>
public class GatheringTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static NodeDef Fount(Action<NodeDef>? tune = null)
    {
        var node = new NodeDef
        {
            Skill = "religion", Verb = "empower", Ticks = 2, BaseChance = 1, MaxChance = 1, Xp = 10,
            Points = "tokens", PointsAmount = 1, PointsChance = 1, YieldsMin = 3, YieldsMax = 3, RespawnTicks = 20,
            StartSay = "Ouch!", StartDamage = 5,
        };
        tune?.Invoke(node);
        return node;
    }

    private static (WorldFixture Fx, FakeConnection Client, Player Alice) World(NodeDef node, params PlacedObject[] placed)
    {
        ContentParts parts = TestContent.Parts();
        parts.Objects.Add(new ObjectDef { Id = "fount", Name = "Fount", Actions = { new ActionDef { Id = "gather", Label = "Empower" } }, Node = node });
        if (placed.Length == 0) placed = new[] { new PlacedObject { Type = "fount", X = 6, Y = 5 } };
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), GameContent.Create(parts), null, placed);
        FakeConnection client = fx.Login("Alice");
        return (fx, client, fx.Player("Alice"));
    }

    private static void Gather(WorldFixture fx, FakeConnection client, int x = 6, int y = 5)
    {
        client.FromClient(new ObjectActionMessage { ObjectType = "fount", ActionId = "gather", Plane = 0, X = x, Y = y });
        fx.Ticks(1);
    }

    [Fact]
    public void WorkingANodeGivesXpAndPointsUntilItDepletes()
    {
        var (fx, client, alice) = World(Fount());
        SkillSystem skills = fx.World.Get<SkillSystem>();
        Gather(fx, client);
        Assert.Contains(client.Sent.OfType<ChatLineMessage>(), l => l.Text == "Ouch!" && l.EntityId == alice.Id);
        Assert.Contains(client.Updates.SelectMany(u => u.Hits), h => h.TargetId == alice.Id && h.Amount == 5); // the cut (regenerated at once out of combat)

        fx.Ticks(10);
        Assert.Equal(30, skills.Xp(alice, "religion"));
        Assert.Equal(3, fx.World.Get<PointsSystem>().Balance(alice, "tokens"));
        Assert.Null(alice.Interaction); // sated: she stopped
        Assert.Contains(client.Texts, t => t == new NodeDef().DepletedMessage);

        Gather(fx, client);
        fx.Ticks(4);
        Assert.Equal(30, skills.Xp(alice, "religion")); // still depleted
    }

    [Fact]
    public void ADepletedNodeIsDepletedForEveryoneAndComesBack()
    {
        var (fx, client, alice) = World(Fount());
        FakeConnection bob = fx.Login("Bob");
        Gather(fx, client);
        fx.Ticks(10);

        Gather(fx, bob);
        Assert.Contains(bob.Texts, t => t == new NodeDef().DepletedMessage);
        JsonStateMessage state = bob.Sent.OfType<JsonStateMessage>().Last(m => m.Key == GatheringSystem.StateKey);
        Assert.True(Assert.Single(state.Parse<List<NodeState>>()).Depleted);

        fx.Ticks(20);
        Gather(fx, bob);
        fx.Ticks(4);
        Assert.True(fx.World.Get<SkillSystem>().Xp(fx.Player("Bob"), "religion") > 0);
        Assert.Empty(bob.Sent.OfType<JsonStateMessage>().Last(m => m.Key == GatheringSystem.StateKey).Parse<List<NodeState>>());
    }

    [Fact]
    public void ANodeNeedsItsLevel()
    {
        var (fx, client, alice) = World(Fount(n => n.Level = 10));
        Gather(fx, client);
        fx.Ticks(4);
        Assert.Contains(client.Texts, t => t.Contains("religion level 10"));
        Assert.Equal(0, fx.World.Get<SkillSystem>().Xp(alice, "religion"));
    }

    [Fact]
    public void ASurgingNodeGivesMore()
    {
        NodeDef node = Fount(n =>
        {
            n.YieldsMin = n.YieldsMax = 100;
            n.PointsChance = 0.25;
            n.Surge = new NodeSurgeDef { EveryTicks = 1000, Ticks = 1000, XpMultiplier = 4, PointsMultiplier = 4 };
        });
        var (fx, client, alice) = World(node);
        GatheringSystem gathering = fx.World.Get<GatheringSystem>();
        fx.Ticks(1001); // long enough for the first surge
        Assert.True(gathering.Nodes[0].IsSurging(fx.World.CurrentTick));

        Gather(fx, client);
        fx.Ticks(2);
        Assert.Equal(40, fx.World.Get<SkillSystem>().Xp(alice, "religion"));
        Assert.Equal(1, fx.World.Get<PointsSystem>().Balance(alice, "tokens")); // a 25% chance made certain
    }

    [Fact]
    public void NodesOfOneTypeCloseTogetherShareOneSurge()
    {
        NodeDef node = Fount(n => n.Surge = new NodeSurgeDef { EveryTicks = 10, Ticks = 5, GroupRadius = 3 });
        var (fx, _, _) = World(node,
            new PlacedObject { Type = "fount", X = 6, Y = 5 }, new PlacedObject { Type = "fount", X = 8, Y = 5 },
            new PlacedObject { Type = "fount", X = 30, Y = 30 });
        GatheringSystem gathering = fx.World.Get<GatheringSystem>();
        Assert.Equal(gathering.Nodes[0].Group, gathering.Nodes[1].Group);
        Assert.NotEqual(gathering.Nodes[0].Group, gathering.Nodes[2].Group);
        for (int i = 0; i < 100; i++)
        {
            fx.Ticks(1);
            Assert.True(gathering.Nodes.Take(2).Count(n => n.IsSurging(fx.World.CurrentTick)) <= 1);
        }
    }

    [Fact]
    public void NodesAreValidated()
    {
        ContentParts parts = TestContent.Parts();
        parts.Objects.Add(new ObjectDef { Id = "bad", Name = "Bad", Node = new NodeDef { Skill = "nope", Points = "nothing", YieldsMin = 0, Items = { new ItemCost { Item = "missing" } } } });
        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("skill 'nope'", e.Message);
        Assert.Contains("points pool 'nothing'", e.Message);
        Assert.Contains("yieldsMin", e.Message);
        Assert.Contains("'missing'", e.Message);
        Assert.Contains("'gather' action", e.Message);
    }

    [Fact]
    public void ShippedMonumentFountsAreAtAboutHalfThePaceTable()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        NodeDef fount = content.Objects["monument_fount"].Node!;
        double attemptsPerHour = 3600 / (fount.Ticks * 0.6);
        double xpPerHour = attemptsPerHour * fount.BaseChance * fount.Xp; // at the fount's own level
        Assert.InRange(xpPerHour, 1500, 3000); // AFK: about half of ~4k/h for levels 1-10 (skills.md 2.1)
    }
}
