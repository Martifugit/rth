using System;
using System.Collections.Generic;

namespace Rth.Server.Game
{
    /// <summary>
    /// Synchronous publish/subscribe between systems, so one system can react to another without
    /// either knowing about the other (loot listens for deaths; combat doesn't know loot exists).
    /// Handlers run immediately, on the tick thread, in subscription order.
    /// </summary>
    public sealed class GameEvents
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<T>(Action<T> handler)
        {
            if (!_handlers.TryGetValue(typeof(T), out List<Delegate>? list))
                _handlers[typeof(T)] = list = new List<Delegate>();
            list.Add(handler);
        }

        public void Publish<T>(T e)
        {
            if (!_handlers.TryGetValue(typeof(T), out List<Delegate>? list)) return;
            foreach (Delegate handler in list.ToArray()) ((Action<T>)handler)(e);
        }
    }

    /// <summary>Something died. <see cref="Killer"/> is whoever landed the last hit, if anyone.</summary>
    public sealed class EntityDied
    {
        public EntityDied(Entity victim, Entity? killer)
        {
            Victim = victim;
            Killer = killer;
        }

        public Entity Victim { get; }
        public Entity? Killer { get; }
    }

    /// <summary>Damage was dealt (after reductions). Useful for XP, contribution tracking, achievements.</summary>
    public sealed class DamageDealt
    {
        public DamageDealt(Entity target, Entity? source, int amount)
        {
            Target = target;
            Source = source;
            Amount = amount;
        }

        public Entity Target { get; }
        public Entity? Source { get; }
        public int Amount { get; }
    }
}
