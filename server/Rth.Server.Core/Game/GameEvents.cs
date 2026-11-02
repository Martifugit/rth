using System;
using System.Collections.Generic;
using Rth.Shared.Combat;

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

    /// <summary>A player began a trial (trials.json).</summary>
    public sealed class TrialStarted
    {
        public TrialStarted(Player player, string trial)
        {
            Player = player;
            Trial = trial;
        }

        public Player Player { get; }
        public string Trial { get; }
    }

    /// <summary>A player's run at a trial ended: won, or lost (died, left, logged out).</summary>
    public sealed class TrialEnded
    {
        public TrialEnded(Player player, string trial, bool won, int ticks)
        {
            Player = player;
            Trial = trial;
            Won = won;
            Ticks = ticks;
        }

        public Player Player { get; }
        public string Trial { get; }
        public bool Won { get; }
        public int Ticks { get; }
    }

    /// <summary>A tile trigger (grasping hands, a snare) sprang on a player.</summary>
    public sealed class TriggerSprung
    {
        public TriggerSprung(Player player, Rth.Shared.World.PlacedObject trigger)
        {
            Player = player;
            Trigger = trigger;
        }

        public Player Player { get; }
        public Rth.Shared.World.PlacedObject Trigger { get; }
    }

    /// <summary>A dead player has woken (in the realm of the dead): back on their feet, humours restored.</summary>
    public sealed class PlayerRespawned
    {
        public PlayerRespawned(Player player) => Player = player;

        public Player Player { get; }
    }

    /// <summary>
    /// A player received items, and from where: "npc:fen_rat" (its loot), "recipe:butcher_fen_rat"
    /// (a recipe's yield). For drop logs, collection logs and "obtain" objectives.
    /// </summary>
    public sealed class ItemsObtained
    {
        public ItemsObtained(Player player, string source, IReadOnlyList<Content.Drop> items)
        {
            Player = player;
            Source = source;
            Items = items;
        }

        public Player Player { get; }
        public string Source { get; }
        public IReadOnlyList<Content.Drop> Items { get; }
    }

    /// <summary>A player used an action on a placed object (after any flag check passed).</summary>
    public sealed class ObjectUsed
    {
        public ObjectUsed(Player player, Rth.Shared.World.PlacedObject placed, string actionId)
        {
            Player = player;
            Placed = placed;
            ActionId = actionId;
        }

        public Player Player { get; }
        public Rth.Shared.World.PlacedObject Placed { get; }
        public string ActionId { get; }
    }

    /// <summary>A player reached an NPC and did one of its menu actions (talk, trade, bank).</summary>
    public sealed class NpcActionUsed
    {
        public NpcActionUsed(Player player, Npc npc, string actionId)
        {
            Player = player;
            Npc = npc;
            ActionId = actionId;
        }

        public Player Player { get; }
        public Npc Npc { get; }
        public string ActionId { get; }
    }

    /// <summary>
    /// A player used an item on something and a use in uses.json took it (its checks passed).
    /// <see cref="Target"/> is "object:&lt;type&gt;", "npc:&lt;type&gt;" or "item:&lt;id&gt;".
    /// </summary>
    public sealed class ItemUsedOn
    {
        public ItemUsedOn(Player player, Rth.Shared.Items.ItemDef item, string target, Content.UseDef use)
        {
            Player = player;
            Item = item;
            Target = target;
            Use = use;
        }

        public Player Player { get; }
        public Rth.Shared.Items.ItemDef Item { get; }
        public string Target { get; }
        public Content.UseDef Use { get; }
    }

    /// <summary>A player walked (or logged) into a region of the world.</summary>
    public sealed class RegionEntered
    {
        public RegionEntered(Player player, Rth.Shared.World.WorldRegion region)
        {
            Player = player;
            Region = region;
        }

        public Player Player { get; }
        public Rth.Shared.World.WorldRegion Region { get; }
    }

    /// <summary>A player finished one go of a recipe (butchered a carcass, distilled a batch...).</summary>
    public sealed class RecipeCompleted
    {
        public RecipeCompleted(Player player, Content.RecipeDef recipe)
        {
            Player = player;
            Recipe = recipe;
        }

        public Player Player { get; }
        public Content.RecipeDef Recipe { get; }
    }

    /// <summary>Damage was dealt (after reductions). Useful for XP, contribution tracking, achievements.</summary>
    public sealed class DamageDealt
    {
        public DamageDealt(Entity target, Entity? source, int amount, CombatStyle style)
        {
            Target = target;
            Source = source;
            Amount = amount;
            Style = style;
        }

        public Entity Target { get; }
        public Entity? Source { get; }
        public int Amount { get; }
        public CombatStyle Style { get; }
    }
}

namespace Rth.Server.Game
{
    /// <summary>A trophy fish took the player's lure (docs/fishing.md 3).</summary>
    public sealed class FishHooked
    {
        public FishHooked(Player player, string species, bool revised)
        {
            Player = player;
            Species = species;
            Revised = revised;
        }

        public Player Player { get; }
        public string Species { get; }
        public bool Revised { get; }
    }

    /// <summary>A trophy fish was landed: its weight, size class and whether it beat the player's best.</summary>
    public sealed class FishLanded
    {
        public FishLanded(Player player, string species, int grams, string sizeClass, bool revised, bool personalBest)
        {
            Player = player;
            Species = species;
            Grams = grams;
            SizeClass = sizeClass;
            Revised = revised;
            PersonalBest = personalBest;
        }

        public Player Player { get; }
        public string Species { get; }
        public int Grams { get; }
        public string SizeClass { get; }
        public bool Revised { get; }
        public bool PersonalBest { get; }
    }

    /// <summary>A hooked trophy fish got away (snapped, hook free, escaped).</summary>
    public sealed class FishLost
    {
        public FishLost(Player player, string species, Rth.Shared.Angling.AnglingOutcome outcome)
        {
            Player = player;
            Species = species;
            Outcome = outcome;
        }

        public Player Player { get; }
        public string Species { get; }
        public Rth.Shared.Angling.AnglingOutcome Outcome { get; }
    }
}
