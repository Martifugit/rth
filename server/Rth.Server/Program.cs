using System;
using System.IO;
using System.Linq;
using System.Threading;
using Rth.Server;
using Rth.Server.Game;

// Standalone R'th server. Usage:
//   dotnet run --project server/Rth.Server -- [--port N] [--maps dir] [--spawn region] [--saves dir] [--content dir]
//                                             [--tick-ms N] [--no-hot-reload]
// Paths default to server/data/maps (every region in it) and server/saves (found from the working directory).

var config = new ServerConfig
{
    Log = line => Console.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line),
};

string serverDir = FindServerDirectory();
config.MapDirectory = Path.Combine(serverDir, "data", "maps");
config.SaveDirectory = Path.Combine(serverDir, "saves");
config.ContentDirectory = Path.Combine(serverDir, "data", "content");

for (int i = 0; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException("Missing value for " + args[i]);
    switch (args[i])
    {
        case "--port": config.Port = int.Parse(Next()); break;
        case "--maps": config.MapDirectory = Next(); break;
        case "--spawn": config.SpawnRegion = Next(); break;
        case "--saves": config.SaveDirectory = Next(); break;
        case "--content": config.ContentDirectory = Next(); break;
        case "--no-hot-reload": config.HotReloadContent = false; break;
        case "--tick-ms": config.TickMillis = int.Parse(Next()); break;
        default:
            Console.Error.WriteLine("Unknown argument " + args[i]);
            return 1;
    }
}

if (!Directory.Exists(config.MapDirectory) || Directory.GetFiles(config.MapDirectory, "*.rthmap").Length == 0)
{
    Console.Error.WriteLine("No maps in " + Path.GetFullPath(config.MapDirectory));
    Console.Error.WriteLine("Export them from Unity (R'th > Export Map) or pass --maps.");
    return 1;
}

using var host = new ServerHost(config);
host.Start();

var stopped = new ManualResetEventSlim();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stopped.Set();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => host.Stop();

// Simple console commands; when stdin isn't interactive this just waits for Ctrl+C.
var commands = new Thread(() =>
{
    string? line;
    while ((line = Console.ReadLine()) != null)
    {
        switch (line.Trim().ToLowerInvariant())
        {
            case "players":
                // Reads the world from outside the tick thread: fine for a rough debug listing.
                try
                {
                    World? world = host.World;
                    Console.WriteLine(world == null ? "Not running." :
                        world.PlayerCount + " online: " + string.Join(", ", world.Players.ToList().Select(p => p.Name + " " + p.Position)));
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("World changed while listing; try again.");
                }
                break;
            case "stop":
            case "quit":
                stopped.Set();
                return;
            case "":
                break;
            default:
                Console.WriteLine("Commands: players, stop");
                break;
        }
    }
}) { IsBackground = true };
commands.Start();

stopped.Wait();
host.Stop();
return 0;

static string FindServerDirectory()
{
    // Works from the repo root, from /server, or from the build output.
    foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        for (DirectoryInfo? dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "data", "maps"))) return dir.FullName;
            if (Directory.Exists(Path.Combine(dir.FullName, "server", "data", "maps"))) return Path.Combine(dir.FullName, "server");
        }
    }
    return Directory.GetCurrentDirectory();
}
