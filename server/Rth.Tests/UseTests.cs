using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Server.Items;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Progress;
using Rth.Shared.Skills;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Using an item on an object, an NPC or another item (uses.json).</summary>
public class UseTests
{
    private static readonly TileCoord Spawn = new(5, 5);
    private static readonly PlacedObject Rack = new() { Type = "rack", X = 10, Y = 5 };
    private static readonly PlacedObject Shrine = new() { Type = "shrine", X = 5, Y = 10 };

    private static List<UseDef> Uses() => new()
    {
        new UseDef { Id = "carcass_on_rack", Item = "carcass", OnObject = "rack", Recipe = "rat" },
        new UseDef
        {
            Id = "gem_on_merchant", Item = "gem", OnNpc = "merchant", Consume = true, Message = "Thanks, {player}.",
            Actions = { new DialogueAction { Type = "giveItem", Item = "coins", Count = 10 } },
        },
        new UseDef { Id = "coins_on_merchant", Item = "coins", OnNpc = "merchant", Dialogue = "merchant" },
        new UseDef
        {
            Id = "knife_on_big", Item = "knife", OnItem = "big_carcass", ConsumeTarget = true, FailMessage = "Too tough.",
            Requirements = { new SkillRequirement { Skill = "butchering", Level = 5 } },
            Actions = { new DialogueAction { Type = "giveItem", Item = "gem" } },
        },
        new UseDef
        {
            Id = "vial_on_shrine", Item = "vial", OnObject = "shrine", FailMessage = "Not yet.", Message = "The vial fills.",
            Conditions = { new DialogueCondition { Type = "flag", Key = "blessed" } }, Consume = true,
            Actions = { new DialogueAction { Type = "giveItem", Item = "tonic" } },
        },
        new UseDef { Id = "herb_on_shrine", Item = "tag:herb", OnObject = "shrine", Consume = true, Message = "You leave an offering." },
    };

    private static GameContent Content(List<UseDef>? uses = null, params AchievementDef[] achievements)
    {
        ContentParts parts = TestContent.Parts();
        parts.Items.Add(new ItemDef { Id = "sage", Name = "Sage", Tags = { "herb" } });
        parts.Uses = uses ?? Uses();
        parts.Achievements = achievements.ToList();
        return GameContent.Create(parts);
    }

    private static WorldFixture World(GameContent? content = null) =>
        new(Maps.Open(40, 40, Spawn), content: content ?? Content(),
            spawns: new[] { new NpcSpawn { Npc = "merchant", X = 5, Y = 8 } },
            objects: new[] { Rack, Shrine });

    private static ItemSystem Items(WorldFixture fx) => fx.World.Get<ItemSystem>();
    private static int Count(WorldFixture fx, string item) => Items(fx).CountOf(fx.Player("Alice"), item);
    private static void Give(WorldFixture fx, string item, int count = 1) => Assert.Equal(0, Items(fx).Give(fx.Player("Alice"), item, count));

    private static int SlotOf(WorldFixture fx, string item)
    {
        ItemContainer pack = Items(fx).Of(fx.Player("Alice")).Inventory;
        return Enumerable.Range(0, pack.Size).First(i => pack[i].ItemId == item);
    }

    private static void UseOn(WorldFixture fx, FakeConnection client, string item, PlacedObject o) =>
        client.FromClient(new UseItemMessage { Slot = SlotOf(fx, item), ItemId = item, Target = UseTarget.Object, ObjectType = o.Type, X = o.X, Y = o.Y });

    private static void UseOn(WorldFixture fx, FakeConnection client, string item, Npc npc) =>
        client.FromClient(new UseItemMessage { Slot = SlotOf(fx, item), ItemId = item, Target = UseTarget.Npc, NpcId = npc.Id });

    private static void UseOn(WorldFixture fx, FakeConnection client, string item, string other) =>
        client.FromClient(new UseItemMessage { Slot = SlotOf(fx, item), ItemId = item, Target = UseTarget.Item, TargetSlot = SlotOf(fx, other), TargetItemId = other });

    [Fact]
    public void ItemOnObjectWalksOverAndRepeatsTheRecipe()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "knife");
        Give(fx, "carcass", 2);

        UseOn(fx, client, "carcass", Rack);
        fx.Ticks(30);

        Player alice = fx.Player("Alice");
        Assert.True(alice.Position.ChebyshevDistance(Rack.Tile) <= 1);
        Assert.Equal(0, Count(fx, "carcass"));
        Assert.Equal(4, Count(fx, "coins"));
        Assert.Contains("You begin to butcher the rat carcass.", client.Texts);
        Assert.Contains("You have nothing left to butcher.", client.Texts);
        Assert.Null(alice.Interaction);
    }

    [Fact]
    public void TheRecipesOwnChecksStillApply()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "carcass");

        UseOn(fx, client, "carcass", Rack);
        fx.Ticks(10);

        Assert.Equal(1, Count(fx, "carcass"));
        Assert.Contains("You need a knife to butcher the rat carcass.", client.Texts);
    }

    [Fact]
    public void ItemOnNpcRunsActionsAndPublishesTheEvent()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        var seen = new List<ItemUsedOn>();
        fx.World.Events.Subscribe<ItemUsedOn>(seen.Add);
        Give(fx, "gem");

        UseOn(fx, client, "gem", fx.Npc("merchant"));
        fx.Ticks(6);

        Assert.Equal(0, Count(fx, "gem"));
        Assert.Equal(10, Count(fx, "coins"));
        Assert.Contains("Thanks, Alice.", client.Texts);
        ItemUsedOn e = Assert.Single(seen);
        Assert.Equal("gem", e.Item.Id);
        Assert.Equal("npc:merchant", e.Target);
        Assert.Equal("gem_on_merchant", e.Use.Id);
    }

    [Fact]
    public void ItemOnNpcCanStartAConversation()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "coins", 5);

        UseOn(fx, client, "coins", fx.Npc("merchant"));
        fx.Ticks(6);

        Assert.True(fx.World.Get<DialogueSystem>().State(fx.Player("Alice")).Active);
        Assert.Equal("Welcome, Alice.", client.Sent.OfType<DialogueMessage>().Last().Text);
    }

    [Fact]
    public void ItemOnItemWorksEitherWayRoundAndChecksRequirements()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "knife");
        Give(fx, "big_carcass", 2);

        UseOn(fx, client, "knife", "big_carcass");
        fx.Ticks(1);
        Assert.Contains("Too tough.", client.Texts);
        Assert.Equal(2, Count(fx, "big_carcass"));

        fx.World.Get<SkillSystem>().AddXp(fx.Player("Alice"), "butchering", 1000);
        UseOn(fx, client, "big_carcass", "knife");
        fx.Ticks(1);
        Assert.Equal(1, Count(fx, "big_carcass"));
        Assert.Equal(1, Count(fx, "knife"));
        Assert.Equal(1, Count(fx, "gem"));
    }

    [Fact]
    public void ConditionsGateAUseWithItsFailMessage()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "vial");

        UseOn(fx, client, "vial", Shrine);
        fx.Ticks(8);
        Assert.Contains("Not yet.", client.Texts);
        Assert.Equal(1, Count(fx, "vial"));

        fx.World.Get<FlagSystem>().Set(fx.Player("Alice"), "blessed", "1");
        UseOn(fx, client, "vial", Shrine);
        fx.Ticks(2);
        Assert.Contains("The vial fills.", client.Texts);
        Assert.Equal(0, Count(fx, "vial"));
        Assert.Equal(1, Count(fx, "tonic"));
    }

    [Fact]
    public void ATagMatchesAnyItemCarryingIt()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "sage");

        UseOn(fx, client, "sage", Shrine);
        fx.Ticks(8);

        Assert.Contains("You leave an offering.", client.Texts);
        Assert.Equal(0, Count(fx, "sage"));
    }

    [Fact]
    public void AnythingUnlistedIsNothingInteresting()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "bread");
        Give(fx, "gem");

        UseOn(fx, client, "bread", Rack);
        fx.Ticks(8);
        UseOn(fx, client, "bread", "gem");
        fx.Ticks(1);

        Assert.Equal(2, client.Texts.Count(t => t == UseSystem.Nothing));
        Assert.Equal(1, Count(fx, "bread"));
    }

    [Fact]
    public void WalkingAwayCancelsAUse()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "gem");
        var seen = new List<ItemUsedOn>();
        fx.World.Events.Subscribe<ItemUsedOn>(seen.Add);

        client.FromClient(new WalkToMessage { X = 30, Y = 30 });
        fx.Ticks(10);
        UseOn(fx, client, "gem", fx.Npc("merchant"));
        fx.Ticks(1);
        client.FromClient(new WalkToMessage { X = 35, Y = 35 });
        fx.Ticks(30);

        Assert.Empty(seen);
        Assert.Equal(1, Count(fx, "gem"));
    }

    [Fact]
    public void AStaleSlotIsIgnored()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "gem");

        client.FromClient(new UseItemMessage { Slot = SlotOf(fx, "gem"), ItemId = "bread", Target = UseTarget.Npc, NpcId = fx.Npc("merchant").Id });
        fx.Ticks(6);

        Assert.Null(fx.Player("Alice").Interaction);
        Assert.Equal(1, Count(fx, "gem"));
        Assert.DoesNotContain(UseSystem.Nothing, client.Texts);
    }

    [Fact]
    public void UsesCountForAchievements()
    {
        GameContent content = Content(null, new AchievementDef { Id = "gift", Name = "Gift", Area = "Town", Objective = { Kind = "use", Target = "gem_on_merchant" } });
        var fx = World(content);
        FakeConnection client = fx.Login("Alice");
        Give(fx, "gem");

        UseOn(fx, client, "gem", fx.Npc("merchant"));
        fx.Ticks(6);

        Assert.True(fx.World.Get<AchievementSystem>().IsDone(fx.Player("Alice"), "gift"));
    }

    [Fact]
    public void BadUsesAreRefused()
    {
        var bad = new List<UseDef>
        {
            new UseDef { Id = "ghost", Item = "nothing", OnObject = "rack" },
            new UseDef { Id = "two", Item = "gem", OnObject = "rack", OnNpc = "merchant" },
            new UseDef { Id = "none", Item = "gem" },
            new UseDef { Id = "recipe", Item = "gem", OnObject = "rack", Recipe = "missing" },
            new UseDef { Id = "target", Item = "gem", OnNpc = "merchant", ConsumeTarget = true },
        };
        var e = Assert.Throws<ContentException>(() => Content(bad));
        foreach (string id in new[] { "ghost", "two", "none", "recipe", "target" })
            Assert.Contains("Use '" + id + "'", e.Message);
        Assert.Throws<ContentException>(() => Content(null, new AchievementDef { Id = "x", Name = "X", Objective = { Kind = "use", Target = "nope" } }));
    }

    [Fact]
    public void EveryShippedUseUsesOnlyKnownConditionsAndActions()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        var fx = new WorldFixture(Maps.Open(10, 10, new TileCoord(1, 1)), content: content);
        DialogueSystem dialogue = fx.World.Get<DialogueSystem>();
        var conditions = dialogue.ConditionTypes.ToHashSet();
        var actions = dialogue.ActionTypes.ToHashSet();
        foreach (UseDef u in content.Uses)
        {
            foreach (DialogueCondition c in u.Conditions) Assert.True(conditions.Contains(c.Type), u.Id + ": no handler for condition '" + c.Type + "'");
            foreach (DialogueAction a in u.Actions) Assert.True(actions.Contains(a.Type), u.Id + ": no handler for action '" + a.Type + "'");
        }
    }
}
