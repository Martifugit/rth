using System.Collections.Generic;
using Newtonsoft.Json.Linq;
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

        /// <summary>What the player is busy doing (attacking, picking up...), or null.</summary>
        public Interaction? Interaction { get; set; }

        public StatBlock Stats { get; } = new StatBlock();
        /// <summary>Set when stats were recalculated, so the client can be told.</summary>
        public bool StatsChanged { get; set; }

        public int Phlegm { get; set; }
        public int Bile { get; set; }
        /// <summary>What hymns drain (Religion). Fractional, since drains are fractions of a point a tick.</summary>
        public double Devotion { get; set; }
        public int MaxDevotion { get; set; }

        /// <summary>Ability id to fire as soon as the global cooldown allows, or null.</summary>
        public string? QueuedAbility { get; set; }
        /// <summary>
        /// An ability used out of range ("stalled"): its cooldowns already started, and it fires
        /// the moment the player reaches the target. Null if none.
        /// </summary>
        public string? StalledAbility { get; set; }
        public uint GlobalCooldownReadyTick { get; set; }
        /// <summary>Busy with something else (eating, say) until this tick: no attacks before it.</summary>
        public uint ActionDelayUntilTick { get; set; }
        /// <summary>Ability ids (and other cooldown keys, such as "item:food") to the tick they can next be used.</summary>
        public Dictionary<string, uint> Cooldowns { get; } = new Dictionary<string, uint>();

        public uint RespawnAtTick { get; set; }
        public int Deaths { get; set; }

        /// <summary>Ids of the entities this player's client currently knows about.</summary>
        public HashSet<int> KnownEntities { get; } = new HashSet<int>();

        /// <summary>Every save section loaded at login, so ones no system claims survive the next save.</summary>
        public Dictionary<string, JToken> SavedSections { get; } = new Dictionary<string, JToken>();

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
