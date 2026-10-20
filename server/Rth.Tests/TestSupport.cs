using System.Collections.Concurrent;
using Rth.Server;
using Rth.Server.Game;
using Rth.Server.Net;
using Rth.Server.Persistence;
using Rth.Server.Timing;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>An in-memory connection: tests push client messages in and read what the server sent.</summary>
internal sealed class FakeConnection : IConnection
{
    private readonly ConcurrentQueue<Message> _inbox = new();

    public List<Message> Sent { get; } = new();
    public bool IsOpen { get; private set; } = true;
    public string Description => "fake";

    public void FromClient(Message message) => _inbox.Enqueue(message);
    public bool TryReceive(out Message message) => _inbox.TryDequeue(out message!);

    public void Send(Message message)
    {
        if (IsOpen) Sent.Add(message);
    }

    public void Close() => IsOpen = false;

    /// <summary>Simulates the client vanishing without a logout message.</summary>
    public void Drop() => IsOpen = false;

    public IEnumerable<WorldUpdateMessage> Updates => Sent.OfType<WorldUpdateMessage>();
}

internal sealed class MemoryPlayerStore : IPlayerStore
{
    public Dictionary<string, PlayerSave> Saves { get; } = new();
    public int SaveCount { get; private set; }

    public PlayerSave? Load(string key) => Saves.TryGetValue(key, out PlayerSave? save) ? save : null;

    public void Save(string key, PlayerSave save)
    {
        SaveCount++;
        Saves[key] = save;
    }
}

/// <summary>Simulated time: waiting advances the clock instantly.</summary>
internal sealed class FakeClock : IClock
{
    public long Now { get; set; }

    public bool Wait(long duration, WaitHandle cancel)
    {
        Now += Math.Max(0, duration);
        return cancel.WaitOne(0);
    }
}

internal static class Maps
{
    /// <summary>
    /// Builds a map from rows of text, top row = north. '.' is open, '#' is blocked, 'S' is the
    /// spawn (open). The bottom-left character is tile (0, 0).
    /// </summary>
    public static MapData FromText(params string[] rows)
    {
        int height = rows.Length;
        int width = rows[0].Length;
        var map = new MapData(0, 0, width, height);
        for (int row = 0; row < height; row++)
        {
            int y = height - 1 - row;
            for (int x = 0; x < width; x++)
            {
                char c = rows[row][x];
                if (c == '#') map.Block(0, x, y);
                if (c == 'S') map.Spawn = new TileCoord(x, y);
            }
        }
        return map;
    }

    public static MapData Open(int width, int height, TileCoord spawn)
    {
        return new MapData(0, 0, width, height) { Spawn = spawn };
    }
}

/// <summary>A world with a fake store and helpers for logging in fake clients.</summary>
internal sealed class WorldFixture
{
    public WorldFixture(MapData map, ServerConfig? config = null)
    {
        Config = config ?? new ServerConfig();
        Store = new MemoryPlayerStore();
        World = new World(Config, map, Store);
    }

    public ServerConfig Config { get; }
    public MemoryPlayerStore Store { get; }
    public World World { get; }

    public FakeConnection Login(string name)
    {
        var connection = new FakeConnection();
        World.AcceptConnection(connection);
        connection.FromClient(new LoginMessage { Name = name });
        World.Tick();
        return connection;
    }

    public void Ticks(int count)
    {
        for (int i = 0; i < count; i++) World.Tick();
    }

    public Player Player(string name) => World.FindPlayer(name) ?? throw new InvalidOperationException(name + " is not logged in.");
}
