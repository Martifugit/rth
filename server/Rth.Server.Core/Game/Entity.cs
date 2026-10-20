using System;
using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    public struct ActiveEffect
    {
        public EffectType Type;
        public int Magnitude;
        public uint EndTick;
    }

    /// <summary>Anything that stands on a tile, moves and can fight: players and NPCs.</summary>
    public abstract class Entity
    {
        protected Entity(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }
        public string Name { get; }
        public abstract EntityKind Kind { get; }

        public int Plane { get; set; }
        public TileCoord Position { get; set; }
        public bool Running { get; set; }

        /// <summary>Tiles still to walk through, in order.</summary>
        public Queue<TileCoord> Waypoints { get; } = new Queue<TileCoord>();

        /// <summary>Tiles stepped through during the current tick, for the client to animate.</summary>
        public List<TileCoord> StepsThisTick { get; } = new List<TileCoord>();

        /// <summary>Moved this tick without walking (login, respawn): viewers get it re-added.</summary>
        public bool Teleported { get; set; }

        // Combat state.
        public int Blood { get; set; }
        public int MaxBlood { get; set; }
        public bool IsDead { get; set; }
        public uint DiedAtTick { get; set; }
        /// <summary>The entity being fought, or 0.</summary>
        public int TargetId { get; set; }
        /// <summary>Where the target stood when the chase path was last planned.</summary>
        public TileCoord? ChaseGoal { get; set; }
        public uint NextAttackTick { get; set; }
        /// <summary>Last tick this entity dealt or took damage; 0 if never.</summary>
        public uint LastCombatTick { get; set; }
        public List<ActiveEffect> Effects { get; } = new List<ActiveEffect>();
        public bool HealthChanged { get; set; }

        public byte HealthPercent => MaxBlood <= 0 ? (byte)0 : (byte)Math.Max(Blood > 0 ? 1 : 0, (int)Math.Round(100.0 * Blood / MaxBlood));

        public bool InCombat(uint tick, int timeoutTicks) => LastCombatTick != 0 && tick - LastCombatTick < timeoutTicks;

        /// <summary>Total magnitude of active effects of one type.</summary>
        public int EffectTotal(EffectType type)
        {
            int total = 0;
            foreach (ActiveEffect e in Effects)
                if (e.Type == type) total += e.Magnitude;
            return total;
        }

        public void ClearCombat()
        {
            TargetId = 0;
            ChaseGoal = null;
        }

        /// <summary>How this entity looks to a client seeing it for the first time.</summary>
        public abstract EntityAdded Describe();
    }
}
