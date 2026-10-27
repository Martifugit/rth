using Rth.Server.Game;
using Rth.Server.Content;
using Rth.Shared.Net;
using Rth.Shared.World;
using Xunit;

namespace Rth.Tests;

/// <summary>Doors (<see cref="DoorSystem"/>): shut at startup, opened and closed from either side, for everyone, and swinging shut by themselves.</summary>
public class DoorTests
{
    // A room x 3..5, y 3..5 with its door on the south edge of (4, 3); the doorway is open in the map, as a building's is.
    private static WorldFixture Fixture(int closeAfter = 0)
    {
        MapData map = Maps.Open(10, 10, new TileCoord(4, 1));
        for (int x = 3; x < 6; x++)
        {
            if (x != 4) map.AddWall(0, x, 3, WallSide.South);
            map.AddWall(0, x, 5, WallSide.North);
        }
        for (int y = 3; y < 6; y++)
        {
            map.AddWall(0, 3, y, WallSide.West);
            map.AddWall(0, 5, y, WallSide.East);
        }
        ContentParts parts = TestContent.Parts();
        parts.Objects.Add(new ObjectDef
        {
            Id = "door", Name = "Door", Door = new DoorDef { CloseAfter = closeAfter },
            Actions = { new ActionDef { Id = DoorSystem.OpenAction, Label = "Open" }, new ActionDef { Id = DoorSystem.CloseAction, Label = "Close" } },
        });
        return new WorldFixture(map, content: GameContent.Create(parts),
            objects: new[] { new PlacedObject { Type = "door", Plane = 0, X = 4, Y = 3, Edge = "S" } });
    }

    private static void Use(FakeConnection c, string action) =>
        c.FromClient(new ObjectActionMessage { ObjectType = "door", Plane = 0, X = 4, Y = 3, ActionId = action });

    [Fact]
    public void ADoorIsShutUntilOpenedAndThenLetsEveryoneThrough()
    {
        WorldFixture f = Fixture();
        Assert.False(f.World.Map.CanStep(0, 4, 2, 0, 1));
        FakeConnection a = f.Login("Ada");
        Use(a, DoorSystem.OpenAction);
        f.Ticks(4);
        Assert.True(f.World.Map.CanStep(0, 4, 2, 0, 1));
        Assert.Equal(new TileCoord(4, 2), f.Player("Ada").Position); // worked from the outside, without passing it
        Assert.Contains(a.Sent.OfType<JsonStateMessage>(), m => m.Key == DoorSystem.StateKey && m.Json.Contains("\"S\""));

        Use(a, DoorSystem.CloseAction);
        f.Ticks(2);
        Assert.False(f.World.Map.CanStep(0, 4, 2, 0, 1));
    }

    [Fact]
    public void ADoorIsWorkedFromInsideToo()
    {
        WorldFixture f = Fixture();
        FakeConnection a = f.Login("Ada");
        Player ada = f.Player("Ada");
        ada.Position = new TileCoord(4, 5);
        Use(a, DoorSystem.OpenAction);
        f.Ticks(5);
        Assert.Equal(new TileCoord(4, 3), ada.Position);
        Assert.True(f.World.Map.CanStep(0, 4, 3, 0, -1));
    }

    [Fact]
    public void AnOpenDoorSwingsShutByItself()
    {
        WorldFixture f = Fixture(closeAfter: 10);
        FakeConnection a = f.Login("Ada");
        Use(a, DoorSystem.OpenAction);
        f.Ticks(4);
        Assert.True(f.World.Map.CanStep(0, 4, 2, 0, 1));
        f.Ticks(12);
        Assert.False(f.World.Map.CanStep(0, 4, 2, 0, 1));
    }
}
