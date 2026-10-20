using System.Collections.Generic;
using Rth.Server.Net;
using Rth.Shared.Combat;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    public sealed class Player : Entity
    {
        public Player(int id, string key, string name, IConnection connection) : base(id, name)
        {
            Key = key;
            Connection = connection;
        }

        public override EntityKind Kind => EntityKind.Player;

        /// <summary>Account key (normalised name) used for saving.</summary>
        public string Key { get; }
        public IConnection Connection { get; }

        public int Phlegm { get; set; }
        public int Bile { get; set; }
        public int Breath { get; set; }
        public int MaxBreath { get; set; }
        public CombatStyle Deflect { get; set; }

        /// <summary>Ability id to fire as soon as the global cooldown allows, or null.</summary>
        public string? QueuedAbility { get; set; }
        public uint GlobalCooldownReadyTick { get; set; }
        /// <summary>Ability id to the tick it can next be used.</summary>
        public Dictionary<string, uint> Cooldowns { get; } = new Dictionary<string, uint>();

        public uint RespawnAtTick { get; set; }
        public int Deaths { get; set; }

        /// <summary>Ids of the entities this player's client currently knows about.</summary>
        public HashSet<int> KnownEntities { get; } = new HashSet<int>();

        public bool LogoutRequested { get; set; }

        public override EntityAdded Describe() => new EntityAdded
        {
            Id = Id,
            Kind = EntityKind.Player,
            Name = Name,
            Plane = Plane,
            Position = Position,
            Shape = EntityShape.Capsule,
            HealthPercent = HealthPercent,
        };
    }
}
