using System;
using Rth.Shared.Net;

namespace Rth.Server
{
    public sealed class ServerConfig
    {
        /// <summary>TCP port to listen on. 0 picks a free port (handy in tests).</summary>
        public int Port = Protocol.DefaultPort;

        /// <summary>Length of one game tick. 600 for real play; tests and debugging may go faster.</summary>
        public int TickMillis = 600;

        public string MapPath = "data/maps/test.rthmap";
        public string SaveDirectory = "saves";

        /// <summary>Autosave every this many ticks (300 ticks = 3 minutes at 600 ms).</summary>
        public int AutosaveIntervalTicks = 300;

        /// <summary>Clients receive entities within this many tiles (Chebyshev distance).</summary>
        public int ViewRadius = 15;

        public int PathSearchRadius = 64;
        public int MaxPlayers = 250;

        /// <summary>Per-player cap on messages handled per tick. A sanity limit, not anti-cheat.</summary>
        public int MaxMessagesPerTick = 25;

        /// <summary>Fixed seed for combat rolls and NPC wandering, for repeatable tests. Null = random.</summary>
        public int? RandomSeed;

        public string ContentDirectory = "data/content";

        /// <summary>Reload content files when they change (development).</summary>
        public bool HotReloadContent = true;

        /// <summary>Where log lines go. Defaults to nowhere.</summary>
        public Action<string> Log = _ => { };
    }
}
