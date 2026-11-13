using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Server.Persistence;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>The engine under the game: system lookups, the tick profiler, sleeping creatures, saves written behind.</summary>
public class EngineTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static NpcSpawn At(string npc, int x, int y, int wander = -1) => new() { Npc = npc, X = x, Y = y, Wander = wander };

    [Fact]
    public void SystemLookupsAnswerTheSameEveryTime()
    {
        var fx = new WorldFixture(Maps.Open(20, 20, Spawn));
        Assert.Same(fx.World.Get<CombatSystem>(), fx.World.Get<CombatSystem>());
        Assert.Same(fx.World.Find<ItemSystem>(), fx.World.Get<ItemSystem>());
        Assert.Same(fx.World.Implementing<IStatContributor>(), fx.World.Implementing<IStatContributor>());
        Assert.Contains(fx.World.Get<CombatSystem>(), fx.World.Implementing<IStatContributor>().Cast<object>());
    }

    [Fact]
    public void TheProfilerReportsPhasesAndSystems()
    {
        var fx = new WorldFixture(Maps.Open(20, 20, Spawn));
        fx.Login("Alice");
        fx.Ticks(5);
        List<string> report = fx.World.Profiler.Report();
        Assert.StartsWith("Last 6 ticks", report[0]);
        Assert.Contains("players", report[1]);
        Assert.Contains("System", report[2]);
    }

    [Fact]
    public void ASlowTickIsLoggedWithWhatCostTheMost()
    {
        var lines = new List<string>();
        var config = new ServerConfig { SlowTickMillis = 0, Log = lines.Add };
        var fx = new WorldFixture(Maps.Open(20, 20, Spawn), config, null, new[] { At("dummy", 7, 5) });
        FakeConnection client = fx.Login("Alice");
        fx.Ticks(2);
        config.SlowTickMillis = 4; // after the first ticks, which are slow while the code warms up
        // Something slow inside the tick: every blow takes 5 ms to be heard.
        fx.World.Events.Subscribe<DamageDealt>(_ => Thread.Sleep(5));
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(3);
        string slow = Assert.Single(lines, l => l.StartsWith("Slow tick"));
        Assert.Contains("Interactions", slow); // the attack runs as the player's interaction
    }

    [Fact]
    public void CreaturesFarFromEveryPlayerSleep()
    {
        var config = new ServerConfig { NpcActiveRadius = 16 };
        var fx = new WorldFixture(Maps.Open(120, 20, Spawn), config, null, new[] { At("rat", 10, 10, wander: 4), At("rat", 100, 10, wander: 4) });
        fx.Login("Alice");
        Npc near = fx.World.Npcs.First(n => n.Home.X == 10);
        Npc far = fx.World.Npcs.First(n => n.Home.X == 100);

        fx.Ticks(200);
        Assert.Equal(far.Home, far.Position); // never woke: no player came within 16 tiles
        Assert.True(fx.World.Get<NpcSystem>().AwakeCount() >= 1);

        // Walk over: it wakes and wanders.
        Player alice = fx.Player("Alice");
        alice.Position = new TileCoord(95, 10);
        alice.Teleported = true;
        bool moved = false;
        for (int i = 0; i < 200 && !moved; i++)
        {
            fx.Ticks(1);
            moved = far.Position != far.Home;
        }
        Assert.True(moved);
        Assert.NotNull(near); // the near one was awake all along
    }

    [Fact]
    public void ActivityOffKeepsEveryCreatureAwake()
    {
        var config = new ServerConfig { NpcActiveRadius = 0 };
        var fx = new WorldFixture(Maps.Open(120, 20, Spawn), config, null, new[] { At("rat", 100, 10, wander: 4) });
        fx.Login("Alice");
        Npc far = fx.Npc("rat");
        bool moved = false;
        for (int i = 0; i < 200 && !moved; i++)
        {
            fx.Ticks(1);
            moved = far.Position != far.Home;
        }
        Assert.True(moved);
    }

    [Fact]
    public void AnAggressiveCreatureFindsAPlayerThroughTheZones()
    {
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), null, new[] { At("mite", 8, 5, wander: 0) });
        fx.Login("Alice");
        fx.Ticks(3);
        Assert.Equal(fx.Player("Alice").Id, fx.Npc("mite").TargetId);
    }

    [Fact]
    public void SavesWrittenBehindLoadBackAndReachTheDisk()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rth-saves-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var store = new JsonPlayerStore(dir, writeBehind: true))
            {
                for (int i = 0; i < 50; i++) store.Save("alice", new PlayerSave { Name = "Alice", X = i });
                // Whatever has reached the disk, a load sees the newest save.
                Assert.Equal(49, store.Load("alice")!.X);
                store.Flush();
                Assert.True(File.Exists(Path.Combine(dir, "alice.json")));
            }
            using var reopened = new JsonPlayerStore(dir);
            Assert.Equal(49, reopened.Load("alice")!.X);
            Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void DisposingWritesEverythingStillWaiting()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rth-saves-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonPlayerStore(dir, writeBehind: true);
            foreach (string name in new[] { "a", "b", "c", "d" }) store.Save(name, new PlayerSave { Name = name, Y = 7 });
            store.Dispose();
            foreach (string name in new[] { "a", "b", "c", "d" })
                Assert.Equal(7, new JsonPlayerStore(dir).Load(name)!.Y);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
