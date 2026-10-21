using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Stairs and ladders between floors: planes with their own collision, climbing, and who sees whom.</summary>
public class ClimbTests
{
    private static readonly TileCoord Spawn = new(5, 5);
    private static readonly TileCoord Upstairs = new(10, 10);

    /// <summary>
    /// Plane 0 is open. Plane 1 is one room, x and y 10..14, with a stair going down just west of it.
    /// A ladder at (6, 5) on plane 0 climbs to (10, 10); a ladder at (9, 10) on plane 1 climbs to (5, 5).
    /// </summary>
    private static MapData TwoFloors()
    {
        var map = new MapData(0, 0, 24, 24, 2) { Spawn = Spawn };
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
                if (x < 10 || x > 14 || y < 10 || y > 14) map.Block(1, x, y);
        return map;
    }

    private static (WorldFixture Fx, FakeConnection Client, Player Alice) World(params PlacedObject[] placed)
    {
        ContentParts parts = TestContent.Parts();
        parts.Objects.Add(new ObjectDef { Id = "ladder_up", Name = "Ladder", Actions = { new ActionDef { Id = "climb", Label = "Climb up" } } });
        parts.Objects.Add(new ObjectDef { Id = "ladder_down", Name = "Ladder", Actions = { new ActionDef { Id = "climb", Label = "Climb down" } } });
        if (placed.Length == 0)
            placed = new[]
            {
                new PlacedObject { Type = "ladder_up", Plane = 0, X = 6, Y = 5, ToPlane = 1, ToX = Upstairs.X, ToY = Upstairs.Y },
                new PlacedObject { Type = "ladder_down", Plane = 1, X = 9, Y = 10, ToPlane = 0, ToX = Spawn.X, ToY = Spawn.Y },
            };
        var fx = new WorldFixture(TwoFloors(), new ServerConfig(), GameContent.Create(parts), null, placed);
        FakeConnection client = fx.Login("Alice");
        return (fx, client, fx.Player("Alice"));
    }

    private static void Use(WorldFixture fx, FakeConnection client, string type, int plane, int x, int y)
    {
        client.FromClient(new ObjectActionMessage { ObjectType = type, ActionId = "climb", Plane = plane, X = x, Y = y });
        fx.Ticks(4);
    }

    [Fact]
    public void ClimbingMovesThePlayerToTheLandingOnTheOtherFloor()
    {
        var (fx, client, alice) = World();
        Assert.Equal(0, alice.Plane);

        Use(fx, client, "ladder_up", 0, 6, 5);
        Assert.Equal(1, alice.Plane);
        Assert.Equal(Upstairs, alice.Position);
        Assert.Null(alice.Interaction);

        // The client is told it has moved: the entity is added again, now on the new plane.
        EntityAdded self = client.Updates.SelectMany(u => u.Added).Last(a => a.Id == alice.Id);
        Assert.Equal(1, self.Plane);
        Assert.Equal(Upstairs, self.Position);

        Use(fx, client, "ladder_down", 1, 9, 10);
        Assert.Equal(0, alice.Plane);
        Assert.Equal(Spawn, alice.Position);
    }

    [Fact]
    public void ThePlayerWalksUpToTheLadderFirst()
    {
        var (fx, client, alice) = World(new PlacedObject { Type = "ladder_up", Plane = 0, X = 15, Y = 5, ToPlane = 1, ToX = Upstairs.X, ToY = Upstairs.Y });
        client.FromClient(new ObjectActionMessage { ObjectType = "ladder_up", ActionId = "climb", Plane = 0, X = 15, Y = 5 });
        fx.Ticks(2);
        Assert.Equal(0, alice.Plane); // still on the way
        fx.Ticks(12);
        Assert.Equal(1, alice.Plane);
    }

    [Fact]
    public void EachFloorHasItsOwnCollision()
    {
        var (fx, client, alice) = World();
        Use(fx, client, "ladder_up", 0, 6, 5);

        client.FromClient(new WalkToMessage { X = 13, Y = 13 });
        fx.Ticks(8);
        Assert.Equal(new TileCoord(13, 13), alice.Position);

        // Open ground on plane 0 is a wall on plane 1: the walk stops at the room's edge.
        client.FromClient(new WalkToMessage { X = 20, Y = 13 });
        fx.Ticks(10);
        Assert.Equal(1, alice.Plane);
        Assert.True(alice.Position.X <= 14);
    }

    [Fact]
    public void PeopleOnOtherFloorsAreNotSeen()
    {
        var (fx, client, alice) = World();
        FakeConnection bobClient = fx.Login("Bob");
        Player bob = fx.Player("Bob");
        fx.Ticks(1);
        Assert.Contains(client.Updates.SelectMany(u => u.Added), a => a.Id == bob.Id);

        Use(fx, client, "ladder_up", 0, 6, 5);
        Assert.Contains(client.Updates.SelectMany(u => u.Removed), id => id == bob.Id);
        Assert.Contains(bobClient.Updates.SelectMany(u => u.Removed), id => id == alice.Id);

        Use(fx, client, "ladder_down", 1, 9, 10);
        Assert.Contains(client.Updates.SelectMany(u => u.Added), a => a.Id == bob.Id && a.Plane == 0);
    }

    [Fact]
    public void ALandingThatIsBlockedLeavesThePlayerWhereTheyAre()
    {
        var (fx, client, alice) = World(new PlacedObject { Type = "ladder_up", Plane = 0, X = 6, Y = 5, ToPlane = 1, ToX = 3, ToY = 3 });
        Use(fx, client, "ladder_up", 0, 6, 5);
        Assert.Equal(0, alice.Plane);
        Assert.Contains(client.Texts, t => t == "You can't get up there.");
    }

    [Fact]
    public void ClimbingAndLoggingOutKeepsThePlayerUpstairs()
    {
        var (fx, client, alice) = World();
        Use(fx, client, "ladder_up", 0, 6, 5);
        client.FromClient(new LogoutMessage());
        fx.Ticks(2);

        FakeConnection again = fx.Login("Alice");
        Assert.Equal(1, fx.Player("Alice").Plane);
        Assert.Equal(Upstairs, fx.Player("Alice").Position);
        Assert.NotNull(again);
    }
}

/// <summary>The exported test region (<c>::tele leveltest</c>): the real map the Unity exporter wrote, walked on the server.</summary>
public class LevelTestMapTests
{
    private static readonly string Maps = Path.Combine(Repo.ServerDirectory, "data", "maps");

    private static WorldLoader.Result Load() => WorldLoader.Load(Maps, "tolling");

    [Fact]
    public void TheTestRegionHasTwoFloorsWithTheirOwnCollision()
    {
        MapData map = MapData.Load(Path.Combine(Maps, "leveltest.rthmap"));
        Assert.Equal(2, map.Planes);

        // The hut's upper floor (x 1088..1097, y 1028..1035) is open only where the slab is.
        Assert.False(map.IsBlocked(1, 1092, 1030));
        Assert.True(map.IsBlocked(1, 1092, 1027)); // outside the hut: no floor up there
        Assert.True(map.IsBlocked(1, 1095, 1030) && map.IsBlocked(1, 1095, 1031)); // the bed stands upstairs
        Assert.False(map.IsBlocked(0, 1095, 1030)); // and the room below has no bed

        // The lookout's deck is open on its own floor, and open ground beneath it.
        Assert.False(map.IsBlocked(1, 1111, 1029));
        Assert.False(map.IsBlocked(0, 1111, 1029));
    }

    [Fact]
    public void EveryClimbLandsOnAFloorSomeoneCanStandOn()
    {
        foreach (string path in Directory.GetFiles(Maps, "*.rthmap"))
        {
            MapData own = MapData.Load(path);
            foreach (PlacedObject o in MapPlacements.Load(path).Objects.Where(o => o.Landing != null))
            {
                (int plane, TileCoord tile) = o.Landing!.Value;
                WorldLoader.Result world = Load();
                Assert.False(world.Map.IsBlocked(plane, tile.X, tile.Y), Path.GetFileName(path) + ": " + o.Type + " at (" + o.X + ", " + o.Y + ") lands on a blocked tile " + tile + " on plane " + plane);
                Assert.True(own.Planes > o.Plane);
            }
        }
    }

    [Fact]
    public void TheHillCanOnlyBeClimbedByItsStair()
    {
        WorldMap map = Load().Map;
        // Straight along the stair to the plateau.
        List<TileCoord> up = Pathfinder.FindPath(map, 0, new TileCoord(1086, 1016), new TileCoord(1106, 1016));
        Assert.Equal(new TileCoord(1106, 1016), up[^1]);
        Assert.Equal(20, up.Count);

        // The stair's sides and the plateau's edges are walls.
        Assert.False(map.EdgeOpen(0, 1095, 1013, 0, 1));
        Assert.False(map.EdgeOpen(0, 1106, 1009, 0, 1));
        List<TileCoord> around = Pathfinder.FindPath(map, 0, new TileCoord(1106, 1008), new TileCoord(1106, 1016));
        Assert.Equal(new TileCoord(1106, 1016), around[^1]);
        Assert.True(around.Count > 30); // all the way round to the stair
    }

    [Fact]
    public void ThePlayerClimbsTheHutsStairWalksUpstairsAndComesBackDown()
    {
        WorldLoader.Result world = Load();
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        var fx = new WorldFixture(world.Map, new ServerConfig(), content, null, world.Placements.Objects);
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        alice.Position = new TileCoord(1092, 1031); // inside the hut, ground floor
        alice.Teleported = true;
        fx.Ticks(1);

        client.FromClient(new ObjectActionMessage { ObjectType = "stairs_up", ActionId = "climb", Plane = 0, X = 1089, Y = 1034 });
        fx.Ticks(10);
        Assert.Equal(1, alice.Plane);
        Assert.Equal(new TileCoord(1090, 1034), alice.Position);

        client.FromClient(new WalkToMessage { X = 1096, Y = 1032 });
        fx.Ticks(10);
        Assert.Equal(new TileCoord(1096, 1032), alice.Position);

        // The wall holds on the upper floor too.
        client.FromClient(new WalkToMessage { X = 1104, Y = 1032 });
        fx.Ticks(12);
        Assert.Equal(1, alice.Plane);
        Assert.True(alice.Position.X <= 1097);

        client.FromClient(new ObjectActionMessage { ObjectType = "stairs_down", ActionId = "climb", Plane = 1, X = 1089, Y = 1034 });
        fx.Ticks(14);
        Assert.Equal(0, alice.Plane);
        Assert.Equal(new TileCoord(1090, 1034), alice.Position);
    }
}
