using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Net;
using Rth.Shared.World;
using Xunit.Abstractions;

namespace Rth.Tests;

/// <summary>
/// The server at the size it is built for: about 200 players, in clusters as friends gather, on a
/// large map full of creatures, walking and fighting. Prints where the time goes and fails only
/// if a tick comes anywhere near its 600 ms (a debug build on a test machine is slow; the bound
/// is for regressions that make the tick scale badly, not for tuning).
/// </summary>
public class LoadTests
{
    private readonly ITestOutputHelper _out;

    public LoadTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void TwoHundredPlayersAndThreeThousandCreaturesTickWellInsideTheirTime()
    {
        const int size = 1024, players = 200, creatures = 3000;
        var random = new Random(7);
        var spawns = new List<NpcSpawn>();
        for (int i = 0; i < creatures; i++)
            spawns.Add(new NpcSpawn { Npc = i % 3 == 0 ? "mite" : "rat", X = random.Next(4, size - 4), Y = random.Next(4, size - 4), Wander = 3 });
        var fx = new WorldFixture(Maps.Open(size, size, new TileCoord(size / 2, size / 2)), new ServerConfig { SlowTickMillis = 0 }, null, spawns);

        // Players arrive in groups of friends at twenty places round the map.
        var clients = new List<FakeConnection>();
        for (int i = 0; i < players; i++)
        {
            var connection = new FakeConnection();
            fx.World.AcceptConnection(connection);
            connection.FromClient(new LoginMessage { Name = "Load" + i });
            clients.Add(connection);
        }
        fx.Ticks(1);
        int p = 0;
        var camps = new Dictionary<Player, TileCoord>();
        foreach (Player player in fx.World.Players)
        {
            int camp = p++ % 20;
            camps[player] = new TileCoord(80 + camp % 5 * 200, 80 + camp / 5 * 220);
            player.Position = camps[player].Offset(random.Next(-6, 7), random.Next(-6, 7));
            player.Teleported = true;
        }

        // Each tick a few players walk somewhere or pick a fight with something near them.
        var ticks = new List<double>();
        for (int t = 0; t < 120; t++)
        {
            for (int i = 0; i < 15; i++)
            {
                FakeConnection c = clients[random.Next(clients.Count)];
                Player? player = fx.World.Players.FirstOrDefault(x => x.Connection == c);
                if (player == null) continue;
                Npc? near = fx.World.EntitiesNear(player.Plane, player.Position, 8).OfType<Npc>().FirstOrDefault(n => !n.IsDead);
                if (near != null && random.Next(2) == 0) c.FromClient(new AttackMessage { TargetId = near.Id });
                else c.FromClient(new WalkToMessage { X = camps[player].X + random.Next(-12, 13), Y = camps[player].Y + random.Next(-12, 13) }); // about their camp
            }
            fx.Ticks(1);
            ticks.Add(fx.World.Profiler.LastTickMs);
            foreach (FakeConnection c in clients) c.Sent.Clear(); // a real connection forgets what it sent
        }

        foreach (string line in fx.World.Profiler.Report(10)) _out.WriteLine(line);
        double average = ticks.Skip(20).Average(), worst = ticks.Skip(20).Max();
        _out.WriteLine("Average " + average.ToString("0.0") + " ms, worst " + worst.ToString("0.0") + " ms; " +
                       fx.World.Get<NpcSystem>().AwakeCount() + " of " + creatures + " creatures awake, " + fx.World.Zones.ActiveZoneCount + " active zones.");
        Assert.True(fx.World.Get<NpcSystem>().AwakeCount() < creatures / 2, "most of the map should sleep");
        Assert.True(average < 250, "average tick " + average + " ms");
    }
}
