using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>
/// Gear stats by tier and family (docs/skills.md 2.2, gear.json), and skill boosts from draughts
/// (2.5: requirements check the boosted level; what a player is reads the real one).
/// </summary>
public class GearAndBoostTests
{
    private static GearRules Rules() => new()
    {
        Base = 10, PerTier = 6, LevelsPerTier = 10,
        Families =
        {
            ["plate"] = new GearFamily { Requires = "defence", Slots = { ["Body"] = new() { [StatIds.Ward] = 0.5, [StatIds.MaxBlood] = 6 } } },
        },
    };

    [Fact]
    public void ATieredItemGetsItsStatsAndRequirementFromItsFamily()
    {
        ContentParts parts = TestContent.Parts();
        parts.Gear = Rules();
        parts.Items.Add(new ItemDef { Id = "body1", Name = "Body", Equipment = new EquipmentDef { Slot = EquipSlot.Body, Family = "plate", Tier = 1 } });
        parts.Items.Add(new ItemDef { Id = "body3", Name = "Body", Equipment = new EquipmentDef { Slot = EquipSlot.Body, Family = "plate", Tier = 3, Stats = { [StatIds.Ward] = 1 } } });
        GameContent content = GameContent.Create(parts);
        EquipmentDef one = content.Items["body1"].Equipment!, three = content.Items["body3"].Equipment!;
        Assert.Equal(5, one.Stats[StatIds.Ward]);
        Assert.Equal(60, one.Stats[StatIds.MaxBlood]);
        Assert.Empty(one.Requirements);                        // tier 1 needs nothing
        Assert.Equal(12, three.Stats[StatIds.Ward]);            // 22 x 0.5 = 11, plus its own 1
        Assert.Equal("defence", three.Requirements.Single().Skill);
        Assert.Equal(20, three.Requirements.Single().Level);
        GameContent.Create(parts);                              // building again doesn't add twice
        Assert.Equal(12, content.Items["body3"].Equipment!.Stats[StatIds.Ward]);
    }

    [Fact]
    public void AnUnknownFamilyOrSlotIsAContentProblem()
    {
        ContentParts parts = TestContent.Parts();
        parts.Gear = Rules();
        parts.Items.Add(new ItemDef { Id = "x", Name = "X", Equipment = new EquipmentDef { Slot = EquipSlot.Body, Family = "cloth", Tier = 1 } });
        parts.Items.Add(new ItemDef { Id = "y", Name = "Y", Equipment = new EquipmentDef { Slot = EquipSlot.Head, Family = "plate", Tier = 1 } });
        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("'cloth'", e.Message);
        Assert.Contains("no Head slot", e.Message);
    }

    private static (WorldFixture Fx, FakeConnection Alice) BoostWorld()
    {
        ContentParts parts = TestContent.Parts();
        parts.Items.Add(new ItemDef
        {
            Id = "draught", Name = "Draught",
            Consumable = new ConsumableDef { Verb = "Drink", CooldownGroup = "potion", CooldownTicks = 1, Effects = { new EffectOp { Op = EffectOp.BoostSkill, Skill = "occultism", Amount = 3, Percent = 10, Ticks = 50 } } },
        });
        var fx = new WorldFixture(Maps.Open(10, 10, new TileCoord(5, 5)), new ServerConfig(), GameContent.Create(parts));
        return (fx, fx.Login("Alice"));
    }

    [Fact]
    public void ADraughtBoostsALevelForAWhileAndRequirementsSeeIt()
    {
        var (fx, alice) = BoostWorld();
        SkillSystem skills = fx.World.Get<SkillSystem>();
        Player p = fx.Player("Alice");
        skills.AddXp(p, "occultism", fx.World.Content.XpCurve.XpForLevel(20));
        Assert.Equal(20, skills.Level(p, "occultism"));
        Assert.NotNull(skills.Check(p, new[] { new Rth.Shared.Skills.SkillRequirement { Skill = "occultism", Level = 24 } }, "do it"));

        fx.World.Get<ItemSystem>().Give(p, "draught", 1);
        alice.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = 0, ActionId = ItemActions.Consume });
        fx.Ticks(1);
        Assert.Equal(25, skills.Level(p, "occultism"));          // 20 + 3 + 10% of 20
        Assert.Equal(20, skills.BaseLevel(p, "occultism"));
        Assert.Null(skills.Check(p, new[] { new Rth.Shared.Skills.SkillRequirement { Skill = "occultism", Level = 24 } }, "do it"));
        Assert.Contains(alice.Sent.OfType<JsonStateMessage>(), m => m.Key == SkillSystem.BoostsKey && m.Parse<Dictionary<string, int>>().TryGetValue("occultism", out int b) && b == 5);

        fx.Ticks(55);
        Assert.Equal(20, skills.Level(p, "occultism"));
        Assert.Contains(alice.Texts, t => t.Contains("boost wears off"));
    }

    [Fact]
    public void ASmallerBoostDoesNotReplaceABiggerOne()
    {
        var (fx, _) = BoostWorld();
        SkillSystem skills = fx.World.Get<SkillSystem>();
        Player p = fx.Player("Alice");
        skills.AddBoost(p, "occultism", 6, 0, 100);
        skills.AddBoost(p, "occultism", 2, 0, 100);
        Assert.Equal(7, skills.Level(p, "occultism"));
    }

    [Fact]
    public void TheShippedGearRulesLoad()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "server", "data", "content");
        if (!Directory.Exists(dir)) dir = FindContent();
        GameContent content = GameContent.Load(dir);
        Assert.NotEmpty(content.Items);
    }

    private static string FindContent()
    {
        string? d = AppContext.BaseDirectory;
        while (d != null && !Directory.Exists(Path.Combine(d, "server", "data", "content"))) d = Path.GetDirectoryName(d);
        return Path.Combine(d!, "server", "data", "content");
    }
}
