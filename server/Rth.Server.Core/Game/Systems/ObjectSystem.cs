using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Interactive objects placed in the map (workstations now; rocks, doors and altars later).
    /// Each object type lists its actions in objects.json; systems register what an action id does
    /// with <see cref="RegisterAction"/> (the recipe system handles "process"). "examine" is built in,
    /// and an action with a <c>message</c> just says it (signposts), so it needs no handler.
    /// </summary>
    public sealed class ObjectSystem : GameSystem
    {
        public const string Examine = "examine";

        private readonly List<PlacedObject> _placed;
        private readonly HashSet<PlacedObject> _present;
        private readonly Dictionary<string, Action<Player, PlacedObject, ObjectDef>> _actions = new Dictionary<string, Action<Player, PlacedObject, ObjectDef>>();

        public ObjectSystem(IEnumerable<PlacedObject> placed)
        {
            _placed = placed.ToList();
            _present = new HashSet<PlacedObject>(_placed);
        }

        /// <summary>Whether the object is in the world now (one added while the game runs may have gone).</summary>
        public bool IsPlaced(PlacedObject placed) => _present.Contains(placed);

        public IReadOnlyList<PlacedObject> Placed => _placed;

        public void RegisterAction(string actionId, Action<Player, PlacedObject, ObjectDef> handler) => _actions[actionId] = handler;

        /// <summary>Whether a flag- or level-gated action is available to this player; tells them why not if it isn't.</summary>
        public static bool Unlocked(World world, Player player, ActionDef action)
        {
            if (action.RequiresFlag != null && world.Find<FlagSystem>()?.IsSet(player, action.RequiresFlag) != true)
            {
                world.Tell(player, TextKind.Warning, action.LockedMessage ?? "You can't do that yet.");
                return false;
            }
            string? problem = world.Find<SkillSystem>()?.Check(player, action.Requirements, action.Label.ToLowerInvariant() + " that");
            if (problem == null) return true;
            world.Tell(player, TextKind.Warning, problem);
            return false;
        }

        /// <summary>Adds an object to the world while the server runs (a campfire); it works like any placed one.</summary>
        public void Add(PlacedObject placed)
        {
            _placed.Add(placed);
            _present.Add(placed);
        }

        /// <summary>Takes away an object added with <see cref="Add"/>; anyone working it stops.</summary>
        public void Remove(PlacedObject placed)
        {
            if (!_placed.Remove(placed)) return;
            _present.Remove(placed);
            foreach (Player p in World.Players)
                if (p.Interaction is ObjectInteraction o && ReferenceEquals(o.Object, placed)) World.StopInteraction(p);
        }

        /// <summary>Every object on a tile.</summary>
        public IEnumerable<PlacedObject> AllAt(int plane, TileCoord tile) => _placed.Where(o => o.Plane == plane && o.X == tile.X && o.Y == tile.Y);

        public PlacedObject? At(int plane, TileCoord tile, string type) =>
            _placed.FirstOrDefault(o => o.Plane == plane && o.X == tile.X && o.Y == tile.Y && o.Type == type);

        protected override void OnAttach()
        {
            foreach (PlacedObject o in _placed)
                if (!World.Content.Objects.ContainsKey(o.Type))
                    World.Log("Map places unknown object type '" + o.Type + "' at (" + o.X + ", " + o.Y + ").");

            World.OnMessage<ObjectActionMessage>(Handle);
        }

        private void Handle(Player player, ObjectActionMessage m)
        {
            if (player.IsDead) return;
            PlacedObject? placed = At(m.Plane, new TileCoord(m.X, m.Y), m.ObjectType);
            if (placed == null || !World.CanSee(player, placed.Plane, placed.Tile)) return;
            if (!World.Content.Objects.TryGetValue(placed.Type, out ObjectDef? def)) return;

            if (m.ActionId == Examine)
            {
                World.Tell(player, TextKind.Info, string.IsNullOrEmpty(def.Examine) ? "It's a " + def.Name.ToLowerInvariant() + "." : def.Examine);
                return;
            }
            ActionDef? action = def.Actions.FirstOrDefault(a => a.Id == m.ActionId);
            if (action == null) return;
            // Systems' handlers walk the player up themselves (a station, a bank, a stair). Anything
            // else (a sign to read, a lock to try, an altar to kneel at) is done standing beside it.
            if (_actions.ContainsKey(action.Id) && action.Animation == null)
            {
                if (Unlocked(World, player, action)) Use(player, placed, def, action);
                return;
            }
            World.StartInteraction(player, new ObjectOnceInteraction(placed, p =>
            {
                if (Unlocked(World, p, action)) Use(p, placed, def, action);
            }));
        }

        private void Use(Player player, PlacedObject placed, ObjectDef def, ActionDef action)
        {
            World.Events.Publish(new ObjectUsed(player, placed, action.Id));
            if (action.Animation != null) World.Animate(player, action.Animation, placed.Tile);
            if (action.Message != null) World.Tell(player, TextKind.Info, action.Message);
            if (_actions.TryGetValue(action.Id, out Action<Player, PlacedObject, ObjectDef>? handler)) handler(player, placed, def);
            else if (action.Message == null && action.Animation == null) World.Log("No handler for object action '" + action.Id + "'.");
        }
    }
}
