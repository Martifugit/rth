using System.Collections.Generic;
using Rth.Server.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    public sealed class Player
    {
        public Player(int id, string key, string name, IConnection connection)
        {
            Id = id;
            Key = key;
            Name = name;
            Connection = connection;
        }

        public int Id { get; }
        /// <summary>Account key (normalised name) used for saving.</summary>
        public string Key { get; }
        public string Name { get; }
        public IConnection Connection { get; }

        public int Plane { get; set; }
        public TileCoord Position { get; set; }
        public bool Running { get; set; }

        /// <summary>Tiles still to walk through, in order.</summary>
        public Queue<TileCoord> Waypoints { get; } = new Queue<TileCoord>();

        /// <summary>Tiles stepped through during the current tick, for the client to animate.</summary>
        public List<TileCoord> StepsThisTick { get; } = new List<TileCoord>();

        /// <summary>Ids of the entities this player's client currently knows about.</summary>
        public HashSet<int> KnownPlayers { get; } = new HashSet<int>();

        public bool LogoutRequested { get; set; }
    }
}
