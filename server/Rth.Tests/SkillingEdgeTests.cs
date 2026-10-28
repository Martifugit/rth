using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>The skilling loops' edge cases on the shipped content: things that go away, full packs, leaving, the order of the make window.</summary>
public class SkillingEdgeTests
{
    private static readonly PlacedObject[] Placed =
    {
        new() { Type = "furnace", X = 10, Y = 13 },
        new() { Type = "forge", X = 13, Y = 13 },
        new() { Type = "eel_weir", X = 10, Y = 7 },
    };

    private static (WorldFixture Fx, FakeConnection Alice, Player P) World()
    {
        var fx = new WorldFixture(Maps.Open(24, 24, new TileCoord(10, 10)), new ServerConfig(), GameContent.Load(Repo.ContentDirectory), null, Placed);
        FakeConnection alice = fx.Login("Alice");
        return (fx, alice, fx.Player("Alice"));
    }

    private static int Slot(WorldFixture fx, Player p, string item)
    {
        Rth.Server.Items.ItemContainer pack = fx.World.Get<ItemSystem>().Of(p).Inventory;
        return Enumerable.Range(0, pack.Size).First(i => !pack.IsEmpty(i) && pack[i].ItemId == item);
    }

    private static MakeState Window(FakeConnection c) =>
        c.Sent.OfType<JsonStateMessage>().LastOrDefault(m => m.Key == MakeState.StateKey)?.Parse<MakeState>() ?? new MakeState();

    [Fact]
    public void CookingStopsAndTheWindowClosesWhenTheFireBurnsOut()
    {
        var (fx, alice, p) = World();
        ItemSystem items = fx.World.Get<ItemSystem>();
        while (items.CountOf(p, "riverbread") > 0) items.Take(p, "riverbread", 1);   // room in the pack
        items.Give(p, "alder_log", 1);
        alice.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = Slot(fx, p, "alder_log"), ActionId = ItemActions.Place });
        fx.Ticks(1);
        PlacedObject fire = fx.World.Get<PlacingSystem>().Placed.Single();
        for (int i = 0; i < 10; i++) items.Give(p, "raw_perch", 1);
        items.Give(p, "raw_rat_haunch", 1);
        alice.FromClient(new ObjectActionMessage { ObjectType = "campfire", ActionId = "process", Plane = 0, X = fire.X, Y = fire.Y });
        fx.Ticks(3);
        Assert.True(Window(alice).IsOpen, string.Join(" | ", alice.Texts.TakeLast(4)));   // perch or haunch: a choice
        fx.Ticks(160);                                             // the fire burns out (150 ticks)
        Assert.False(fx.World.Get<ObjectSystem>().IsPlaced(fire));
        Assert.False(Window(alice).IsOpen);
        alice.FromClient(new IntentMessage { Kind = MakeState.Intent, Arg = "cook_perch_fire|0" });
        fx.Ticks(20);
        Assert.Equal(10, items.CountOf(p, "raw_perch"));          // nothing cooked on a fire that isn't there
    }

    [Fact]
    public void CharcoalOnTheFurnaceOffersSteelNotFuel()
    {
        var (fx, alice, p) = World();
        ItemSystem items = fx.World.Get<ItemSystem>();
        items.Give(p, "charcoal", 1);
        items.Give(p, "hematite", 1);
        items.Give(p, "raw_copper", 1);
        alice.FromClient(new UseItemMessage { Slot = Slot(fx, p, "charcoal"), ItemId = "charcoal", Target = UseTarget.Object, ObjectType = "furnace", Plane = 0, X = 10, Y = 13 });
        fx.Ticks(5);
        Assert.Equal(1, items.CountOf(p, "charcoal"));
        Assert.Equal(0, fx.World.Get<StationSystem>().Left(Placed[0]));
    }

    [Fact]
    public void AFullPackLeavesTheTrapSet()
    {
        var (fx, alice, p) = World();
        ItemSystem items = fx.World.Get<ItemSystem>();
        fx.World.Get<SkillSystem>().AddXp(p, "fishing", fx.World.Content.XpCurve.XpForLevel(10));
        items.Give(p, "eel_trap", 1);
        alice.FromClient(new ObjectActionMessage { ObjectType = "eel_weir", ActionId = "gather", Plane = 0, X = 10, Y = 7 });
        fx.Ticks(4);
        GatheringSystem.Node weir = fx.World.Get<GatheringSystem>().Nodes.Single(n => n.Placed.Type == "eel_weir");
        Assert.Equal("Alice", weir.TrapOwner);
        fx.Ticks(300);
        while (items.Give(p, "smiths_hammer", 1) == 0) { }       // fill the pack
        alice.FromClient(new ObjectActionMessage { ObjectType = "eel_weir", ActionId = "gather", Plane = 0, X = 10, Y = 7 });
        fx.Ticks(2);
        Assert.Equal("Alice", weir.TrapOwner);
        Assert.Contains(alice.Texts, t => t.Contains("don't have room"));
    }

    [Fact]
    public void LeavingTheWorldLiftsYourTrapsAndTakesThemHome()
    {
        var (fx, alice, p) = World();
        ItemSystem items = fx.World.Get<ItemSystem>();
        fx.World.Get<SkillSystem>().AddXp(p, "fishing", fx.World.Content.XpCurve.XpForLevel(10));
        items.Give(p, "eel_trap", 1);
        alice.FromClient(new ObjectActionMessage { ObjectType = "eel_weir", ActionId = "gather", Plane = 0, X = 10, Y = 7 });
        fx.Ticks(4);
        alice.FromClient(new LogoutMessage());
        fx.Ticks(2);
        GatheringSystem.Node weir = fx.World.Get<GatheringSystem>().Nodes.Single(n => n.Placed.Type == "eel_weir");
        Assert.Null(weir.TrapOwner);
        FakeConnection again = fx.Login("Alice");
        Assert.Equal(1, fx.World.Get<ItemSystem>().CountOf(fx.Player("Alice"), "eel_trap"));
    }

    [Fact]
    public void TheForgeWindowShowsEachHeadingOnce()
    {
        var (fx, alice, p) = World();
        fx.World.Get<ItemSystem>().Give(p, "copper_bar", 1);
        alice.FromClient(new ObjectActionMessage { ObjectType = "forge", ActionId = "process", Plane = 0, X = 13, Y = 13 });
        fx.Ticks(5);
        List<string> groups = Window(alice).Options.Select(o => o.Group).ToList();
        Assert.NotEmpty(groups);
        List<string> runs = groups.Where((g, i) => i == 0 || groups[i - 1] != g).ToList();
        Assert.Equal(runs.Distinct().Count(), runs.Count);
    }
}
