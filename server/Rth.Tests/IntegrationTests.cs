using System.Diagnostics;
using Rth.Server;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>A real server over TCP with fast ticks, driven by real <see cref="GameClient"/>s.</summary>
public sealed class IntegrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rth-it-" + Guid.NewGuid().ToString("N"));
    private readonly ServerHost _host;
    private readonly List<GameClient> _clients = new();

    public IntegrationTests()
    {
        Directory.CreateDirectory(_dir);
        string mapPath = Path.Combine(_dir, "test.rthmap");
        MapData map = Maps.Open(32, 32, new TileCoord(5, 5));
        map.AddWall(0, 10, 5, WallSide.East);
        map.Save(mapPath);

        _host = new ServerHost(new ServerConfig
        {
            Port = 0,
            TickMillis = 20,
            MapDirectory = _dir,
            SaveDirectory = Path.Combine(_dir, "saves"),
            ContentDirectory = Repo.ContentDirectory,
            HotReloadContent = false,
        });
        _host.Start();
    }

    public void Dispose()
    {
        foreach (GameClient c in _clients) c.Dispose();
        _host.Stop();
        Directory.Delete(_dir, recursive: true);
    }

    private GameClient Connect()
    {
        var client = new GameClient();
        client.Connect("127.0.0.1", _host.Port);
        _clients.Add(client);
        return client;
    }

    /// <summary>Reads messages until one matches, failing after a timeout.</summary>
    private static T WaitFor<T>(GameClient client, Func<T, bool>? match = null, int timeoutMs = 3000) where T : Message
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            while (client.TryReceive(out Message m))
                if (m is T t && (match == null || match(t))) return t;
            Thread.Sleep(5);
        }
        throw new TimeoutException("No " + typeof(T).Name + " arrived.");
    }

    private (GameClient Client, int Id) LoginAs(string name)
    {
        GameClient client = Connect();
        client.Send(new LoginMessage { Name = name });
        LoginOkMessage ok = WaitFor<LoginOkMessage>(client);
        return (client, ok.PlayerId);
    }

    [Fact]
    public void TwoPlayersLogInAndSeeEachOtherWalk()
    {
        var (alice, aliceId) = LoginAs("Alice");
        var (bob, bobId) = LoginAs("Bob");

        WaitFor<WorldUpdateMessage>(alice, u => u.Added.Any(a => a.Id == bobId));

        bob.Send(new WalkToMessage { X = 8, Y = 5 });
        WaitFor<WorldUpdateMessage>(alice, u => u.Moved.Any(m => m.Id == bobId && m.Steps[^1] == new TileCoord(8, 5)));
        WaitFor<WorldUpdateMessage>(bob, u => u.Moved.Any(m => m.Id == bobId && m.Steps[^1] == new TileCoord(8, 5)));
        Assert.True(alice.IsConnected);
        Assert.NotEqual(aliceId, bobId);
    }

    [Fact]
    public void PositionSurvivesLogoutAndReconnect()
    {
        var (alice, aliceId) = LoginAs("Alice");
        alice.Send(new WalkToMessage { X = 12, Y = 7, Run = true });
        WaitFor<WorldUpdateMessage>(alice, u => u.Moved.Any(m => m.Id == aliceId && m.Steps[^1] == new TileCoord(12, 7)));
        alice.Send(new LogoutMessage());

        var sw = Stopwatch.StartNew();
        while (alice.IsConnected && sw.ElapsedMilliseconds < 3000) Thread.Sleep(5);
        Assert.False(alice.IsConnected, "Server should close the connection after logout.");

        var (again, againId) = LoginAs("Alice");
        WorldUpdateMessage first = WaitFor<WorldUpdateMessage>(again, u => u.Added.Any(a => a.Id == againId));
        Assert.Equal(new TileCoord(12, 7), first.Added.Single(a => a.Id == againId).Position);
    }

    [Fact]
    public void AbruptDisconnectStillSaves()
    {
        var (alice, aliceId) = LoginAs("Alice");
        alice.Send(new WalkToMessage { X = 7, Y = 9 });
        WaitFor<WorldUpdateMessage>(alice, u => u.Moved.Any(m => m.Id == aliceId && m.Steps[^1] == new TileCoord(7, 9)));
        alice.Disconnect(); // no logout message

        var (again, againId) = LoginAs("Alice");
        WorldUpdateMessage first = WaitFor<WorldUpdateMessage>(again, u => u.Added.Any(a => a.Id == againId));
        Assert.Equal(new TileCoord(7, 9), first.Added.Single(a => a.Id == againId).Position);
    }

    [Fact]
    public void RejectedLoginDeliversTheReason()
    {
        GameClient client = Connect();
        client.Send(new LoginMessage { Name = "no/slashes" });
        LoginRejectedMessage rejected = WaitFor<LoginRejectedMessage>(client);
        Assert.Contains("Names", rejected.Reason);
    }
}
