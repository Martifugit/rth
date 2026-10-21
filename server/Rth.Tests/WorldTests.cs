using Rth.Server;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

public class WorldTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static WorldFixture OpenWorld(ServerConfig? config = null) => new(Maps.Open(40, 40, Spawn), config);

    [Fact]
    public void LoginSpawnsNewPlayerAndTellsTheClient()
    {
        var fx = OpenWorld();
        FakeConnection client = fx.Login("Alice");

        var ok = Assert.IsType<LoginOkMessage>(client.Sent[0]);
        Assert.Equal(600, ok.TickMillis);
        WorldUpdateMessage update = client.Updates.Single();
        EntityAdded self = Assert.Single(update.Added);
        Assert.Equal((ok.PlayerId, "Alice", Spawn), (self.Id, self.Name, self.Position));
    }

    [Fact]
    public void AWalkFurtherThanOneSearchKeepsGoing()
    {
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig { PathSearchRadius = 8 });
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new WalkToMessage { X = 35, Y = 30, Run = true });
        fx.Ticks(30);
        Assert.Equal(new TileCoord(35, 30), fx.Player("Alice").Position);
    }

    [Fact]
    public void AWalkToSomewhereUnreachableStopsAtTheClosestTile()
    {
        var map = Maps.Open(40, 40, Spawn);
        for (int y = 0; y < 40; y++) map.Block(0, 20, y); // a wall right across
        var fx = new WorldFixture(map, new ServerConfig { PathSearchRadius = 8 });
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new WalkToMessage { X = 30, Y = 5, Run = true });
        fx.Ticks(30);
        Assert.Equal(new TileCoord(19, 5), fx.Player("Alice").Position);
        Assert.Null(fx.Player("Alice").WalkGoal);
    }

    [Fact]
    public void WieldingAWeaponShowsItInHandToEveryone()
    {
        var fx = OpenWorld();
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        Rth.Server.Game.Player a = fx.Player("Alice");
        int slot = fx.World.Get<Rth.Server.Game.ItemSystem>().Of(a).Inventory.FirstFreeSlot();
        fx.World.Get<Rth.Server.Game.ItemSystem>().Of(a).Inventory.Set(slot, "sword", 1);
        alice.FromClient(new ItemActionMessage { Container = Rth.Shared.Items.ContainerKind.Inventory, Slot = slot, ActionId = "equip" });
        fx.Ticks(1);
        Assert.Contains(bob.Updates.SelectMany(u => u.Appearances), e => e.Id == a.Id && e.Held == "sword");
        FakeConnection cass = fx.Login("Cass");
        Assert.Contains(cass.Updates.SelectMany(u => u.Added), e => e.Id == a.Id && e.Held == "sword");
    }

    [Fact]
    public void DevCommandsTeleportOnlyWhenTheyAreOn()
    {
        var off = OpenWorld();
        FakeConnection a = off.Login("Alice");
        a.FromClient(new ChatMessage { Channel = ChatChannel.Public, Text = "::tele 30 30" });
        off.Ticks(1);
        Assert.Equal(Spawn, off.Player("Alice").Position);

        var on = OpenWorld(new ServerConfig { DevCommands = true });
        FakeConnection b = on.Login("Alice");
        b.FromClient(new ChatMessage { Channel = ChatChannel.Public, Text = "::tele 30 30" });
        on.Ticks(1);
        Assert.Equal(new TileCoord(30, 30), on.Player("Alice").Position);
        Assert.Empty(b.Sent.OfType<ChatLineMessage>()); // not said out loud
    }

    [Fact]
    public void ObjectEffectsReachViewersFurtherThanTheViewRadius()
    {
        var symbols = new[] { new PlacedObject { Type = "holy_symbol", X = 5, Y = 30 }, new PlacedObject { Type = "holy_symbol", X = 8, Y = 30 } };
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig { DevCommands = true }, null, null, symbols);
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new ChatMessage { Channel = ChatChannel.Public, Text = "::fx holy_symbol flare colour=#ff0000 delay=0.7" });
        fx.Ticks(1);
        var effects = client.Updates.SelectMany(u => u.ObjectEffects).ToList();
        Assert.Equal(2, effects.Count); // 25 tiles away: beyond sight, within the effect radius
        Assert.All(effects, e => Assert.Equal("flare", e.Effect));
        Assert.Equal("colour=#ff0000;delay=0.7", effects[1].Arg);
    }

    [Fact]
    public void SaintPreviewsPlayTheirMechanicsOnTheCryptsObjects()
    {
        var placed = new List<PlacedObject> { new() { Type = "unquiet_saint", X = 20, Y = 30 }, new() { Type = "crypt_altar", X = 20, Y = 18 } };
        for (int i = 0; i < 8; i++) placed.Add(new PlacedObject { Type = "crypt_fount", X = 10 + i, Y = 10 });
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig { DevCommands = true }, null, null, placed);
        FakeConnection client = fx.Login("Alice");
        foreach (string c in new[] { "::saint light", "::saint wake 40", "::saint cleanse" })
            client.FromClient(new ChatMessage { Channel = ChatChannel.Public, Text = c });
        fx.Ticks(1);
        var effects = client.Updates.SelectMany(u => u.ObjectEffects).ToList();
        Assert.Equal(3, effects.Count(e => e.Effect == "light" && e.ObjectType == "crypt_fount"));
        Assert.Contains(effects, e => e.Effect == "wake" && e.Arg == "level=0.4");
        Assert.Contains(effects, e => e.Effect == "cleanse" && e.ObjectType == "crypt_altar");
    }

    [Fact]
    public void WalkingMovesOneTilePerTick()
    {
        var fx = OpenWorld();
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new WalkToMessage { X = 10, Y = 5 });

        for (int i = 1; i <= 5; i++)
        {
            fx.Ticks(1);
            Assert.Equal(new TileCoord(5 + i, 5), fx.Player("Alice").Position);
        }
        fx.Ticks(3);
        Assert.Equal(new TileCoord(10, 5), fx.Player("Alice").Position);
    }

    [Fact]
    public void RunningMovesTwoTilesPerTickAndReportsBothSteps()
    {
        var fx = OpenWorld();
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new WalkToMessage { X = 10, Y = 5, Run = true });

        fx.Ticks(1);
        Assert.Equal(new TileCoord(7, 5), fx.Player("Alice").Position);
        EntityMoved move = client.Updates.Last().Moved.Single();
        Assert.True(move.Running);
        Assert.Equal(new[] { new TileCoord(6, 5), new TileCoord(7, 5) }, move.Steps);

        fx.Ticks(2); // 9, then the last odd tile
        Assert.Equal(new TileCoord(10, 5), fx.Player("Alice").Position);
    }

    [Fact]
    public void ANewWalkOrderReplacesTheOldOne()
    {
        var fx = OpenWorld();
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new WalkToMessage { X = 15, Y = 5 });
        fx.Ticks(2);
        client.FromClient(new WalkToMessage { X = 7, Y = 9 });
        fx.Ticks(10);
        Assert.Equal(new TileCoord(7, 9), fx.Player("Alice").Position);
    }

    [Fact]
    public void WallsBlockMovementAndThePlayerUsesTheDoor()
    {
        // A wall along y = 10 (north edge of row 9) with a door at x = 20.
        MapData map = Maps.Open(40, 40, Spawn);
        for (int x = 0; x < 40; x++)
            if (x != 20) map.AddWall(0, x, 9, WallSide.North);
        var fx = new WorldFixture(map);
        FakeConnection client = fx.Login("Alice");

        client.FromClient(new WalkToMessage { X = 5, Y = 15 });
        TileCoord previous = fx.Player("Alice").Position;
        for (int i = 0; i < 60; i++)
        {
            fx.Ticks(1);
            TileCoord now = fx.Player("Alice").Position;
            if (previous.Y <= 9 && now.Y >= 10)
                Assert.Equal(20, now.X); // crossed the wall line: must be through the door
            previous = now;
        }
        Assert.Equal(new TileCoord(5, 15), fx.Player("Alice").Position);
    }

    [Fact]
    public void LoggingOutAndBackInRestoresPosition()
    {
        var fx = OpenWorld();
        FakeConnection first = fx.Login("Alice");
        first.FromClient(new WalkToMessage { X = 12, Y = 8 });
        fx.Ticks(10);
        first.FromClient(new LogoutMessage());
        fx.Ticks(1);

        Assert.Null(fx.World.FindPlayer("Alice"));
        Assert.False(first.IsOpen);

        fx.Login("alice"); // names are case-insensitive
        Assert.Equal(new TileCoord(12, 8), fx.Player("Alice").Position);
        Assert.Equal("Alice", fx.Player("Alice").Name); // keeps the original spelling
    }

    [Fact]
    public void ASaveWhoseLandIsGoneStartsAtTheSpawn()
    {
        var fx = OpenWorld();
        FakeConnection first = fx.Login("Alice");
        first.FromClient(new WalkToMessage { X = 12, Y = 8 });
        fx.Ticks(10);
        first.FromClient(new LogoutMessage());
        fx.Ticks(1);

        fx.Store.Saves["alice"].X = 5000; // nothing is built out there
        fx.Login("Alice");
        Assert.Equal(Spawn, fx.Player("Alice").Position);
    }

    [Fact]
    public void DroppedConnectionIsLoggedOutAndSaved()
    {
        var fx = OpenWorld();
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new WalkToMessage { X = 8, Y = 5 });
        fx.Ticks(3);
        client.Drop();
        fx.Ticks(1);

        Assert.Equal(0, fx.World.PlayerCount);
        Assert.Equal(8, fx.Store.Saves["alice"].X);
    }

    [Fact]
    public void LoggingInAgainReplacesTheOldSession()
    {
        var fx = OpenWorld();
        FakeConnection first = fx.Login("Alice");
        first.FromClient(new WalkToMessage { X = 9, Y = 5 });
        fx.Ticks(4);

        FakeConnection second = fx.Login("Alice");

        Assert.False(first.IsOpen);
        Assert.IsType<LoginOkMessage>(second.Sent[0]);
        Assert.Equal(1, fx.World.PlayerCount);
        Assert.Equal(new TileCoord(9, 5), fx.Player("Alice").Position);
    }

    [Theory]
    [InlineData("")]
    [InlineData("way too long a name")]
    [InlineData("bad/name")]
    public void InvalidNamesAreRejected(string name)
    {
        var fx = OpenWorld();
        FakeConnection client = fx.Login(name);
        Assert.IsType<LoginRejectedMessage>(Assert.Single(client.Sent));
        Assert.False(client.IsOpen);
        Assert.Equal(0, fx.World.PlayerCount);
    }

    [Fact]
    public void WrongProtocolVersionIsRejected()
    {
        var fx = OpenWorld();
        var client = new FakeConnection();
        fx.World.AcceptConnection(client);
        client.FromClient(new LoginMessage { Name = "Alice", ProtocolVersion = Protocol.Version + 1 });
        fx.Ticks(1);
        Assert.IsType<LoginRejectedMessage>(Assert.Single(client.Sent));
    }

    [Fact]
    public void PlayersSeeEachOtherMoveAndLeave()
    {
        var fx = OpenWorld(new ServerConfig { ViewRadius = 8 });
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        int bobId = fx.Player("Bob").Id;
        int aliceId = fx.Player("Alice").Id;

        Assert.Contains(alice.Updates, u => u.Added.Any(a => a.Id == bobId && a.Name == "Bob"));
        Assert.Contains(bob.Updates, u => u.Added.Any(a => a.Id == aliceId));

        bob.FromClient(new WalkToMessage { X = 7, Y = 5 });
        fx.Ticks(1);
        EntityMoved seen = alice.Updates.Last().Moved.Single(m => m.Id == bobId);
        Assert.Equal(new[] { new TileCoord(6, 5) }, seen.Steps);

        // Bob walks out of Alice's view radius.
        bob.FromClient(new WalkToMessage { X = 30, Y = 5 });
        fx.Ticks(30);
        Assert.Contains(alice.Updates, u => u.Removed.Contains(bobId));

        // ...and back in.
        bob.FromClient(new WalkToMessage { X = 6, Y = 6 });
        fx.Ticks(30);
        Assert.Equal(bobId, alice.Updates.Last(u => u.Added.Count > 0).Added.Single().Id);

        // Logging out removes him too.
        int removalsBefore = alice.Updates.Count(u => u.Removed.Contains(bobId));
        bob.FromClient(new LogoutMessage());
        fx.Ticks(1);
        Assert.Equal(removalsBefore + 1, alice.Updates.Count(u => u.Removed.Contains(bobId)));
    }

    [Fact]
    public void AutosaveSavesOnlinePlayersPeriodically()
    {
        var fx = OpenWorld(new ServerConfig { AutosaveIntervalTicks = 10 });
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new WalkToMessage { X = 9, Y = 5 });
        fx.Ticks(9);

        Assert.Equal(9, fx.Store.Saves["alice"].X);
        Assert.Equal(1, fx.World.PlayerCount);
    }

    [Fact]
    public void ShutdownSavesEveryone()
    {
        var fx = OpenWorld();
        fx.Login("Alice");
        fx.Login("Bob");
        fx.World.Shutdown();

        Assert.Equal(0, fx.World.PlayerCount);
        Assert.Equal(new[] { "alice", "bob" }, fx.Store.Saves.Keys.OrderBy(k => k));
    }

    [Fact]
    public void ThousandTicksOfRandomWalkingNeverEntersBlockedTiles()
    {
        MapData map = Maps.Open(40, 40, Spawn);
        var random = new Random(7);
        for (int i = 0; i < 300; i++) map.Block(0, random.Next(40), random.Next(40));
        for (int i = 0; i < 100; i++) map.AddWall(0, random.Next(40), random.Next(40), (WallSide)random.Next(4));
        map.SetFlags(0, Spawn.X, Spawn.Y, CollisionFlags.None);

        var fx = new WorldFixture(map);
        var clients = new[] { fx.Login("Alice"), fx.Login("Bob"), fx.Login("Carol") };
        var last = new Dictionary<string, TileCoord>();

        for (int tick = 0; tick < 1000; tick++)
        {
            if (tick % 7 == 0)
                foreach (FakeConnection c in clients)
                    c.FromClient(new WalkToMessage { X = random.Next(40), Y = random.Next(40), Run = random.Next(2) == 0 });
            fx.Ticks(1);

            foreach (var player in fx.World.Players)
            {
                Assert.False(map.IsBlocked(0, player.Position.X, player.Position.Y));
                if (last.TryGetValue(player.Name, out TileCoord before))
                    Assert.True(before.ChebyshevDistance(player.Position) <= 2);
                last[player.Name] = player.Position;
            }
        }
    }
}
