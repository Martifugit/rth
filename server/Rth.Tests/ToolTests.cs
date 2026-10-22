using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Skills;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Gathering tools (docs/skills.md 2.3): needing one, tiers, the best one picked, and each effect.</summary>
public class ToolTests
{
    private static readonly TileCoord Spawn = new(5, 5);
    private const long Level10Xp = 1154;

    private static NodeDef Rock(Action<NodeDef>? tune = null)
    {
        var node = new NodeDef
        {
            Skill = "mining", Verb = "mine", Ticks = 2, BaseChance = 1, MaxChance = 1, Xp = 10, Tool = "pickaxe",
            Items = { new ItemCost { Item = "ore" } }, RespawnTicks = 5,
        };
        tune?.Invoke(node);
        return node;
    }

    private static ItemDef Pick(string id, int tier, double success = 0, int level = 1, Action<ToolDef>? tune = null)
    {
        var tool = new ToolDef { Kind = "pickaxe", Tier = tier, Success = success };
        if (level > 1) tool.Requirements.Add(new SkillRequirement { Skill = "mining", Level = level });
        tune?.Invoke(tool);
        return new ItemDef { Id = id, Name = id == "pick1" ? "Copper pick" : "Iron pick", Tool = tool, Equipment = new EquipmentDef { Slot = EquipSlot.Weapon } };
    }

    private static ContentParts Parts(NodeDef node, params ItemDef[] tools)
    {
        ContentParts parts = TestContent.Parts();
        parts.Skills.Skills.Add(new SkillDef { Id = "mining", Name = "Mining" });
        parts.Skills.Skills.Add(new SkillDef { Id = "smithing", Name = "Smithing" });
        parts.Items.Add(new ItemDef { Id = "ore", Name = "Ore" });
        parts.Items.Add(new ItemDef { Id = "bar", Name = "Bar" });
        if (tools.Length == 0) tools = new[] { Pick("pick1", 1), Pick("pick2", 2, 0.1, 10) };
        parts.Items.AddRange(tools);
        parts.Objects.Add(new ObjectDef { Id = "rock", Name = "Rock", Actions = { new ActionDef { Id = "gather", Label = "Mine" } }, Node = node });
        return parts;
    }

    private static (WorldFixture Fx, FakeConnection Client, Player Alice) World(NodeDef node, params ItemDef[] tools)
    {
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), GameContent.Create(Parts(node, tools)), null,
            new[] { new PlacedObject { Type = "rock", X = 6, Y = 5 } });
        FakeConnection client = fx.Login("Alice");
        return (fx, client, fx.Player("Alice"));
    }

    private static void Mine(WorldFixture fx, FakeConnection client)
    {
        client.FromClient(new ObjectActionMessage { ObjectType = "rock", ActionId = "gather", Plane = 0, X = 6, Y = 5 });
        fx.Ticks(1);
    }

    private static int Ore(WorldFixture fx, Player p) => fx.World.Get<ItemSystem>().CountOf(p, "ore");

    [Fact]
    public void ANodeThatNeedsAToolSaysSoWithoutOne()
    {
        var (fx, client, alice) = World(Rock());
        Mine(fx, client);
        fx.Ticks(4);
        Assert.Contains(client.Texts, t => t == "You need a pickaxe to mine the rock.");
        Assert.Equal(0, fx.World.Get<SkillSystem>().Xp(alice, "mining"));
        Assert.Equal(0, Ore(fx, alice));
    }

    [Fact]
    public void AToolWorksFromThePackOrWorn()
    {
        var (fx, client, alice) = World(Rock());
        fx.World.Get<ItemSystem>().Give(alice, "pick1", 1);
        Mine(fx, client);
        fx.Ticks(3);
        Assert.Equal(1, Ore(fx, alice));

        var (fx2, client2, bob) = World(Rock());
        fx2.World.Get<ItemSystem>().Of(bob).Equipment.Set((int)EquipSlot.Weapon, "pick1", 1);
        Mine(fx2, client2);
        fx2.Ticks(3);
        Assert.Equal(1, Ore(fx2, bob));
        Assert.Equal(10, fx2.World.Get<SkillSystem>().Xp(bob, "mining"));
    }

    [Fact]
    public void ANodeCanNeedABetterTier()
    {
        var (fx, client, alice) = World(Rock(n => n.ToolTier = 2));
        fx.World.Get<ItemSystem>().Give(alice, "pick1", 1);
        Mine(fx, client);
        fx.Ticks(3);
        Assert.Contains(client.Texts, t => t == "You need a better pickaxe to mine the rock.");
        Assert.Equal(0, Ore(fx, alice));
    }

    [Fact]
    public void AToolNeedsItsLevel()
    {
        var (fx, client, alice) = World(Rock());
        fx.World.Get<ItemSystem>().Give(alice, "pick2", 1);
        Mine(fx, client);
        fx.Ticks(3);
        Assert.Contains(client.Texts, t => t == "You need mining level 10 to use your iron pick.");
        Assert.Equal(0, Ore(fx, alice));
    }

    [Fact]
    public void TheBestUsableToolIsChosen()
    {
        var (fx, _, alice) = World(Rock(n => { n.BaseChance = 0.3; n.PerLevel = 0; }));
        GatheringSystem gathering = fx.World.Get<GatheringSystem>();
        NodeDef def = gathering.Def(gathering.Nodes[0])!;
        ItemSystem items = fx.World.Get<ItemSystem>();
        items.Give(alice, "pick1", 1);
        items.Give(alice, "pick2", 1);

        Assert.Equal("pick1", gathering.BestTool(alice, def)!.Id); // the iron pick needs mining 10
        Assert.Equal(0.3, gathering.SuccessChance(alice, def), 6);

        fx.World.Get<SkillSystem>().AddXp(alice, "mining", Level10Xp);
        Assert.Equal("pick2", gathering.BestTool(alice, def)!.Id);
        Assert.Equal(0.4, gathering.SuccessChance(alice, def), 6); // the higher tier's success bonus
    }

    [Fact]
    public void TheNodeNeedsItsLevelBeforeTheTool()
    {
        var (fx, client, alice) = World(Rock(n => n.Level = 10));
        Mine(fx, client);
        fx.Ticks(3);
        Assert.Contains(client.Texts, t => t == "You need mining level 10 to mine the rock.");
        Assert.Equal(0, fx.World.Get<SkillSystem>().Xp(alice, "mining"));
    }

    [Fact]
    public void LosingTheToolStopsTheWork()
    {
        var (fx, client, alice) = World(Rock(n => n.YieldsMin = n.YieldsMax = 100));
        ItemSystem items = fx.World.Get<ItemSystem>();
        items.Give(alice, "pick1", 1);
        Mine(fx, client);
        fx.Ticks(4);
        int before = Ore(fx, alice);
        Assert.True(before > 0);
        items.Take(alice, "pick1", 1);
        fx.Ticks(6);
        Assert.Equal(before, Ore(fx, alice));
        Assert.Null(alice.Interaction);
        Assert.Contains(client.Texts, t => t == "You need a pickaxe to mine the rock.");
    }

    [Fact]
    public void ConservationKeepsTheNodeStanding()
    {
        var (fx, client, alice) = World(Rock(), Pick("pick1", 1, tune: t => t.Conservation = 1));
        fx.World.Get<ItemSystem>().Give(alice, "pick1", 1);
        Mine(fx, client);
        fx.Ticks(20);
        Assert.Equal(10, Ore(fx, alice)); // an ore rock would deplete on every success
        Assert.False(fx.World.Get<GatheringSystem>().Nodes[0].IsDepleted(fx.World.CurrentTick));
    }

    [Fact]
    public void ATreeFallsByChance()
    {
        var (fx, client, alice) = World(Rock(n => n.DepleteChance = 0.125));
        fx.World.Get<ItemSystem>().Give(alice, "pick1", 1);
        Mine(fx, client);
        fx.Ticks(400);
        int ore = Ore(fx, alice);
        Assert.InRange(ore, 2, 27); // it fell sooner or later, but not on the first success
        Assert.True(fx.World.Get<GatheringSystem>().Nodes[0].IsDepleted(fx.World.CurrentTick) || alice.Interaction == null);
    }

    [Fact]
    public void ATransformationNeedsItsSkillAndGivesItsXp()
    {
        ItemDef charring = Pick("pick1", 1, tune: t => t.Transforms.Add(new ToolTransform { From = "ore", To = "bar", Chance = 1, Skill = "smithing", Level = 5, Xp = 7 }));
        var (fx, client, alice) = World(Rock(n => n.YieldsMin = n.YieldsMax = 100), charring);
        ItemSystem items = fx.World.Get<ItemSystem>();
        SkillSystem skills = fx.World.Get<SkillSystem>();
        items.Give(alice, "pick1", 1);
        Mine(fx, client);
        fx.Ticks(3);
        Assert.Equal(1, Ore(fx, alice)); // smithing 1: no transformation
        Assert.Equal(0, items.CountOf(alice, "bar"));

        skills.AddXp(alice, "smithing", 400); // level 5
        fx.Ticks(2);
        Assert.Equal(1, Ore(fx, alice));
        Assert.Equal(1, items.CountOf(alice, "bar"));
        Assert.Equal(407, skills.Xp(alice, "smithing"));
        Assert.Equal(20, skills.Xp(alice, "mining")); // the node's own XP either way
    }

    [Fact]
    public void AToolCanAddOrReplaceExtras()
    {
        ItemDef lucky = Pick("pick1", 1, tune: t => t.Extras = "gems");
        var (fx, client, alice) = World(Rock(n => { n.YieldsMin = n.YieldsMax = 100; n.Extras = "rat"; }), lucky);
        ItemSystem items = fx.World.Get<ItemSystem>();
        items.Give(alice, "pick1", 1);
        Mine(fx, client);
        fx.Ticks(3);
        Assert.Equal(1, items.CountOf(alice, "gem"));
        Assert.Equal(5, items.CountOf(alice, "coins")); // the node's extras too

        ItemDef replacing = Pick("pick1", 1, tune: t => { t.Extras = "gems"; t.ReplacesExtras = true; });
        var (fx2, client2, bob) = World(Rock(n => { n.YieldsMin = n.YieldsMax = 100; n.Extras = "rat"; }), replacing);
        ItemSystem items2 = fx2.World.Get<ItemSystem>();
        items2.Give(bob, "pick1", 1);
        Mine(fx2, client2);
        fx2.Ticks(3);
        Assert.Equal(1, items2.CountOf(bob, "gem"));
        Assert.Equal(0, items2.CountOf(bob, "coins"));
    }

    [Fact]
    public void AThousandTicksOfMiningGivesOreAndXpInStep()
    {
        var (fx, client, alice) = World(Rock(n => { n.BaseChance = 0.4; n.MaxChance = 0.95; n.Ticks = 4; n.RespawnTicks = 3; }));
        fx.World.Get<ItemSystem>().Give(alice, "pick1", 1);
        for (int i = 0; i < 1000; i += 10)
        {
            if (alice.Interaction == null) Mine(fx, client);
            fx.Ticks(10);
            if (Ore(fx, alice) >= 20) fx.World.Get<ItemSystem>().Take(alice, "ore", Ore(fx, alice));
        }
        double xp = fx.World.Get<SkillSystem>().Xp(alice, "mining");
        Assert.True(xp >= 300, "xp " + xp); // ~250 attempts at 40%+, less the walking and respawns
        Assert.Equal(0, xp % 10);
    }

    [Fact]
    public void ToolsAreValidated()
    {
        ContentParts parts = Parts(Rock(n => { n.Tool = "spoon"; n.DepleteChance = 2; }),
            new ItemDef { Id = "bad", Name = "Bad", Stackable = true, Tool = new ToolDef { Kind = "pickaxe", Conservation = 2, Extras = "nothing",
                Requirements = { new SkillRequirement { Skill = "nope" } }, Transforms = { new ToolTransform { From = "ore", To = "missing", Chance = 1 } } } });
        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("no item is a 'spoon' tool", e.Message);
        Assert.Contains("depleteChance", e.Message);
        Assert.Contains("can't be stackable", e.Message);
        Assert.Contains("conservation", e.Message);
        Assert.Contains("extras table 'nothing'", e.Message);
        Assert.Contains("unknown skill 'nope'", e.Message);
        Assert.Contains("'missing'", e.Message);
    }

    /// <summary>The XP per hour at a node's own level, with the shipped tool of its tier, against the pace table (skills.md 2.1).</summary>
    [Theory]
    [InlineData("copper_seam", 4000)]
    [InlineData("ironstone_seam", 5000)]
    [InlineData("alder_tree", 4000)]
    [InlineData("ash_tree", 5000)]
    public void ShippedRocksAndTreesKeepToThePaceTable(string node, double target)
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        NodeDef def = content.Objects[node].Node!;
        ToolDef tool = content.Items.Values.Select(i => i.Tool).Where(t => t != null && t.Kind == def.Tool
                && t.Requirements.All(r => r.Level <= def.Level))
            .OrderByDescending(t => t!.Tier).First()!;
        double chance = Math.Min(def.MaxChance, def.BaseChance + tool.Success);
        double xpPerHour = 3600 / (def.Ticks * 0.6) * chance * def.Xp;
        Assert.InRange(xpPerHour, target * 0.8, target * 1.5);
    }
}
