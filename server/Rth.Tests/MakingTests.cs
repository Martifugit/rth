using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>
/// The making foundation (docs/skills.md 2.4): the make window (a choice of recipe and an amount),
/// recipes that go wrong by level and station, stations that work faster, the forge's shared draw,
/// items processed by themselves, and tools named by tag.
/// </summary>
public class MakingTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static ContentParts Parts()
    {
        ContentParts parts = TestContent.Parts();
        parts.Items.Add(new ItemDef { Id = "bar", Name = "Bar", Stackable = false });
        parts.Items.Add(new ItemDef { Id = "dagger", Name = "Dagger" });
        parts.Items.Add(new ItemDef { Id = "plate", Name = "Plate" });
        parts.Items.Add(new ItemDef { Id = "hammer", Name = "Hammer", Tags = { "hammer" } });
        parts.Items.Add(new ItemDef { Id = "charcoal", Name = "Charcoal", Stackable = true, Tags = { "fuel" } });
        parts.Items.Add(new ItemDef { Id = "raw_fish", Name = "Raw fish" });
        parts.Items.Add(new ItemDef { Id = "fish", Name = "Fish" });
        parts.Items.Add(new ItemDef { Id = "burnt_fish", Name = "Burnt fish" });
        parts.Items.Add(new ItemDef { Id = "grimy", Name = "Grimy herb", Process = new ProcessDef { Verb = "Clean" } });
        parts.Items.Add(new ItemDef { Id = "herb", Name = "Herb" });
        parts.Objects.Add(new ObjectDef
        {
            Id = "forge", Name = "Forge",
            Actions = { new ActionDef { Id = "process", Label = "Smith" }, new ActionDef { Id = "bellows", Label = "Work the bellows" } },
            Station = new StationDef
            {
                Draw = new DrawDef { Skill = "butchering", Max = 100, Bellows = 40, BellowsTicks = 2, HotTickScale = 0.5, Xp = 2, Fuel = { new DrawFuel { Item = "tag:fuel", Ticks = 30 } } },
            },
        });
        parts.Objects.Add(new ObjectDef { Id = "fire", Name = "Fire", Actions = { new ActionDef { Id = "process", Label = "Cook" } } });
        parts.Objects.Add(new ObjectDef { Id = "range", Name = "Range", Actions = { new ActionDef { Id = "process", Label = "Cook" } }, Station = new StationDef { FailScale = 0, TickScale = 0.5 } });
        parts.Recipes.Add(new RecipeDef
        {
            Id = "smith_dagger", Name = "dagger", Verb = "smith", Skill = "butchering", Station = "forge", Group = "Daggers", Ticks = 4, Xp = 10,
            Inputs = { new ItemAmount { Item = "bar" } }, Tools = { "tag:hammer" }, Outputs = { new ItemAmount { Item = "dagger" } },
        });
        parts.Recipes.Add(new RecipeDef
        {
            Id = "smith_plate", Name = "plate", Verb = "smith", Skill = "butchering", Station = "forge", Level = 1, Ticks = 4, Xp = 50,
            Inputs = { new ItemAmount { Item = "bar", Count = 3 } }, Tools = { "tag:hammer" }, Outputs = { new ItemAmount { Item = "plate" } },
        });
        foreach (string station in new[] { "fire", "range" })
            parts.Recipes.Add(new RecipeDef
            {
                Id = "cook_fish_" + station, Name = "fish", Verb = "cook", Skill = "butchering", Station = station, Level = 1, Ticks = 4, Xp = 30,
                Inputs = { new ItemAmount { Item = "raw_fish" } }, Outputs = { new ItemAmount { Item = "fish" } },
                Failure = new RecipeFailure { Chance = 1, SafeLevels = 10, Outputs = { new ItemAmount { Item = "burnt_fish" } }, Message = "You burn the {name}." },
            });
        parts.Recipes.Add(new RecipeDef
        {
            Id = "clean_herb", Name = "grimy herb", Verb = "clean", Skill = "butchering", Ticks = 1, Xp = 2,
            Inputs = { new ItemAmount { Item = "grimy" } }, Outputs = { new ItemAmount { Item = "herb" } },
        });
        return parts;
    }

    private static readonly PlacedObject[] Placed =
    {
        new() { Type = "forge", X = 6, Y = 5 },
        new() { Type = "fire", X = 4, Y = 5 },
        new() { Type = "range", X = 5, Y = 6 },
    };

    private static (WorldFixture Fx, FakeConnection Alice) World(ContentParts? parts = null)
    {
        var fx = new WorldFixture(Maps.Open(20, 20, Spawn), new ServerConfig(), GameContent.Create(parts ?? Parts()), null, Placed);
        return (fx, fx.Login("Alice"));
    }

    private static void Give(WorldFixture fx, string item, int count = 1)
    {
        ItemSystem items = fx.World.Get<ItemSystem>();
        for (int i = 0; i < count; i++) items.Give(fx.Player("Alice"), item, 1);
    }

    private static int Count(WorldFixture fx, string item) => fx.World.Get<ItemSystem>().CountOf(fx.Player("Alice"), item);

    private static void Act(WorldFixture fx, FakeConnection c, string type, string action = "process")
    {
        PlacedObject o = Placed.First(p => p.Type == type);
        c.FromClient(new ObjectActionMessage { ObjectType = type, ActionId = action, Plane = 0, X = o.X, Y = o.Y });
        fx.Ticks(2);
    }

    private static MakeState Window(FakeConnection c) =>
        c.Sent.OfType<JsonStateMessage>().LastOrDefault(m => m.Key == MakeState.StateKey)?.Parse<MakeState>() ?? new MakeState();

    private static void Make(WorldFixture fx, FakeConnection c, string recipe, int count)
    {
        c.FromClient(new IntentMessage { Kind = MakeState.Intent, Arg = recipe + "|" + count });
        fx.Ticks(1);
    }

    private static void SetLevel(WorldFixture fx, int level) =>
        fx.World.Get<SkillSystem>().AddXp(fx.Player("Alice"), "butchering", fx.World.Content.XpCurve.XpForLevel(level));

    [Fact]
    public void AChoiceOpensTheMakeWindowAndTheAmountIsKept()
    {
        var (fx, alice) = World();
        Give(fx, "hammer");
        Give(fx, "bar", 7);
        Act(fx, alice, "forge");
        MakeState window = Window(alice);
        Assert.True(window.IsOpen);
        Assert.Equal("Forge", window.Title);
        Assert.Equal(7, window.Options.Single(o => o.Recipe == "smith_dagger").Makeable);
        Assert.Equal(2, window.Options.Single(o => o.Recipe == "smith_plate").Makeable);

        Make(fx, alice, "smith_dagger", 2);
        Assert.False(Window(alice).IsOpen);                 // closed once work starts
        fx.Ticks(20);
        Assert.Equal(2, Count(fx, "dagger"));
        Assert.Equal(5, Count(fx, "bar"));                  // only the two asked for
    }

    [Fact]
    public void AmountZeroMakesUntilTheInputsRunOut()
    {
        var (fx, alice) = World();
        Give(fx, "hammer");
        Give(fx, "bar", 7);
        Act(fx, alice, "forge");
        Make(fx, alice, "smith_plate", 0);
        fx.Ticks(30);
        Assert.Equal(2, Count(fx, "plate"));
        Assert.Equal(1, Count(fx, "bar"));
        Assert.Contains(alice.Texts, t => t.Contains("nothing left"));
    }

    [Fact]
    public void ARecipeNotInTheOpenWindowIsIgnored()
    {
        var (fx, alice) = World();
        Give(fx, "raw_fish");
        Make(fx, alice, "cook_fish_range", 1);              // no window open
        fx.Ticks(10);
        Assert.Equal(0, Count(fx, "fish") + Count(fx, "burnt_fish"));
    }

    [Fact]
    public void WalkingAwayClosesTheWindow()
    {
        var (fx, alice) = World();
        Give(fx, "hammer");
        Give(fx, "bar", 3);
        Act(fx, alice, "forge");
        Assert.True(Window(alice).IsOpen);
        alice.FromClient(new WalkToMessage { X = 10, Y = 10 });
        fx.Ticks(2);
        Assert.False(Window(alice).IsOpen);
    }

    [Fact]
    public void ATagToolIsAnyItemWithTheTag()
    {
        var (fx, alice) = World();
        Give(fx, "bar", 3);
        Act(fx, alice, "forge");
        Make(fx, alice, "smith_dagger", 1);
        Assert.Contains(alice.Texts, t => t.Contains("need a hammer"));
    }

    [Fact]
    public void FoodBurnsAtItsLevelAndNeverWellAbove()
    {
        var (fx, alice) = World();
        Give(fx, "raw_fish", 3);
        Act(fx, alice, "fire");                             // one recipe touches the pack: straight to work
        fx.Ticks(15);
        Assert.Equal(3, Count(fx, "burnt_fish"));           // a chance of 1 at its level
        Assert.Contains(alice.Texts, t => t == "You burn the fish.");
        Assert.Equal(0, fx.World.Get<SkillSystem>().Xp(fx.Player("Alice"), "butchering"));

        SetLevel(fx, 11);                                   // safeLevels (10) above: never
        Give(fx, "raw_fish", 3);
        Act(fx, alice, "fire");
        fx.Ticks(15);
        Assert.Equal(3, Count(fx, "fish"));
    }

    [Fact]
    public void FailChanceFallsEvenlyWithLevel()
    {
        var recipe = new RecipeDef { Level = 10, Failure = new RecipeFailure { Chance = 0.6, SafeLevels = 15 } };
        Assert.Equal(0.6, recipe.FailChance(10), 6);
        Assert.Equal(0.4, recipe.FailChance(15), 6);
        Assert.Equal(0, recipe.FailChance(25), 6);
        Assert.Equal(0, new RecipeDef().FailChance(1));
    }

    [Fact]
    public void AStationScalesFailureAndTime()
    {
        var (fx, alice) = World();
        Give(fx, "raw_fish", 2);
        Act(fx, alice, "range");                            // failScale 0, tickScale 0.5: 2 ticks a fish
        fx.Ticks(4);
        Assert.Equal(2, Count(fx, "fish"));
        Assert.Equal(0, Count(fx, "burnt_fish"));
        Assert.Equal(0.0, fx.World.Get<RecipeSystem>().FailChance(fx.Player("Alice"), fx.World.Content.Recipes["cook_fish_range"], Placed[2]));
    }

    [Fact]
    public void TheBellowsFillTheDrawWhichBurnsDownAndMakesTheForgeFaster()
    {
        var (fx, alice) = World();
        StationSystem stations = fx.World.Get<StationSystem>();
        RecipeSystem recipes = fx.World.Get<RecipeSystem>();
        RecipeDef dagger = fx.World.Content.Recipes["smith_dagger"];
        PlacedObject forge = Placed[0];
        Assert.Equal(4, recipes.TicksFor(dagger, forge));

        Act(fx, alice, "forge", "bellows");
        fx.Ticks(6);                                        // three pumps of 40 fill 100
        Assert.True(stations.Left(forge) > 90);
        Assert.True(stations.IsHot(forge));
        Assert.Equal(2, recipes.TicksFor(dagger, forge));   // hot: half the time
        Assert.True(fx.World.Get<SkillSystem>().Xp(fx.Player("Alice"), "butchering") >= 6);
        Assert.Contains(alice.Sent.OfType<JsonStateMessage>(), m => m.Key == DrawDef.StateKey && m.Parse<List<DrawState>>().Any(d => d.Hot));

        // It's the station's: Bob, who never touched it, smiths fast too, until it burns out.
        FakeConnection bob = fx.Login("Bob");
        fx.Ticks(1);
        Assert.Contains(bob.Sent.OfType<JsonStateMessage>(), m => m.Key == DrawDef.StateKey && m.Parse<List<DrawState>>().Count == 1);
        fx.Ticks(110);
        Assert.False(stations.IsHot(forge));
        Assert.Equal(4, recipes.TicksFor(dagger, forge));
    }

    [Fact]
    public void FuelFeedsTheDraw()
    {
        var (fx, alice) = World();
        Give(fx, "charcoal", 5);
        StationSystem stations = fx.World.Get<StationSystem>();
        Assert.True(stations.Feed(fx.Player("Alice"), Placed[0], fx.World.Content.Items["charcoal"]));
        Assert.Equal(30, stations.Left(Placed[0]));
        Assert.Equal(4, Count(fx, "charcoal"));
        Assert.False(stations.Feed(fx.Player("Alice"), Placed[0], fx.World.Content.Items["bar"]));  // not fuel
    }

    [Fact]
    public void AnItemCanBeProcessedByItself()
    {
        var (fx, alice) = World();
        Give(fx, "grimy", 3);
        Assert.Contains(ItemActions.For(fx.World.Content.Items["grimy"], ContainerKind.Inventory), a => a.Id == ItemActions.Process && a.Label == "Clean");
        alice.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = 0, ActionId = ItemActions.Process });
        fx.Ticks(8);
        Assert.Equal(3, Count(fx, "herb"));
        Assert.Equal(0, Count(fx, "grimy"));
    }

    [Fact]
    public void ContentChecksStationsAndProcessableItems()
    {
        ContentParts parts = Parts();
        parts.Items.Add(new ItemDef { Id = "orphan", Name = "Orphan", Process = new ProcessDef { Verb = "Fold" } });
        parts.Objects.Single(o => o.Id == "forge").Station!.Draw!.Fuel.Add(new DrawFuel { Item = "nothing", Ticks = 10 });
        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("'orphan'", e.Message);
        Assert.Contains("draw fuel 'nothing'", e.Message);
    }
}
