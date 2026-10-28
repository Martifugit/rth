using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Items set down as objects for a while: logs lit as a campfire that is a cooking station, then burns out.</summary>
public class PlacingTests
{
    private static (WorldFixture Fx, FakeConnection Alice) World()
    {
        ContentParts parts = TestContent.Parts();
        parts.Items.Add(new ItemDef { Id = "tinderbox", Name = "Tinderbox", Tags = { "tinderbox" } });
        parts.Items.Add(new ItemDef { Id = "logs", Name = "Logs", Place = new PlaceDef { Verb = "Light", Object = "campfire", Ticks = 30, Tool = "tag:tinderbox" } });
        parts.Items.Add(new ItemDef { Id = "raw", Name = "Raw meat" });
        parts.Items.Add(new ItemDef { Id = "meat", Name = "Meat" });
        parts.Objects.Add(new ObjectDef { Id = "campfire", Name = "Campfire", Model = "Nature/Campfire", Actions = { new ActionDef { Id = "process", Label = "Cook" } } });
        parts.Recipes.Add(new RecipeDef
        {
            Id = "cook", Name = "meat", Verb = "cook", Skill = "butchering", Station = "campfire", Ticks = 2, Xp = 5,
            Inputs = { new ItemAmount { Item = "raw" } }, Outputs = { new ItemAmount { Item = "meat" } },
        });
        var fx = new WorldFixture(Maps.Open(20, 20, new TileCoord(5, 5)), new ServerConfig(), GameContent.Create(parts));
        return (fx, fx.Login("Alice"));
    }

    private static void Light(WorldFixture fx, FakeConnection c)
    {
        Rth.Server.Items.ItemContainer pack = fx.World.Get<ItemSystem>().Of(fx.Player("Alice")).Inventory;
        int slot = Enumerable.Range(0, pack.Size).First(i => !pack.IsEmpty(i) && pack[i].ItemId == "logs");
        c.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = slot, ActionId = ItemActions.Place });
        fx.Ticks(1);
    }

    [Fact]
    public void LogsLitAsACampfireCookAndThenBurnOut()
    {
        var (fx, alice) = World();
        ItemSystem items = fx.World.Get<ItemSystem>();
        Player p = fx.Player("Alice");
        items.Give(p, "logs", 1);
        items.Give(p, "logs", 1);
        Light(fx, alice);
        Assert.Contains(alice.Texts, t => t.Contains("need a tinderbox"));
        Assert.Equal(2, items.CountOf(p, "logs"));

        items.Give(p, "tinderbox", 1);
        Light(fx, alice);
        Assert.Equal(1, items.CountOf(p, "logs"));
        PlacedObject fire = fx.World.Get<ObjectSystem>().At(0, new TileCoord(5, 5), "campfire")!;
        Assert.NotNull(fire);
        Assert.Contains(alice.Sent.OfType<JsonStateMessage>(), m => m.Key == PlacedState.StateKey && m.Parse<List<PlacedState>>().Any(s => s.Type == "campfire"));

        Light(fx, alice);                                           // the tile is taken
        Assert.Contains(alice.Texts, t => t.Contains("can't do that here"));

        items.Give(p, "raw", 1);
        alice.FromClient(new ObjectActionMessage { ObjectType = "campfire", ActionId = "process", Plane = 0, X = 5, Y = 5 });
        fx.Ticks(6);
        Assert.Equal(1, items.CountOf(p, "meat"));

        fx.Ticks(30);
        Assert.Null(fx.World.Get<ObjectSystem>().At(0, new TileCoord(5, 5), "campfire"));
        Assert.Empty(alice.Sent.OfType<JsonStateMessage>().Last(m => m.Key == PlacedState.StateKey).Parse<List<PlacedState>>());
    }
}
