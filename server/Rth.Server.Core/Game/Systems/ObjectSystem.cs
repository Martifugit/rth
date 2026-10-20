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
    /// with <see cref="RegisterAction"/> (the recipe system handles "process"). "examine" is built in.
    /// </summary>
    public sealed class ObjectSystem : GameSystem
    {
        public const string Examine = "examine";

        private readonly List<PlacedObject> _placed;
        private readonly Dictionary<string, Action<Player, PlacedObject, ObjectDef>> _actions = new Dictionary<string, Action<Player, PlacedObject, ObjectDef>>();

        public ObjectSystem(IEnumerable<PlacedObject> placed)
        {
            _placed = placed.ToList();
        }

        public IReadOnlyList<PlacedObject> Placed => _placed;

        public void RegisterAction(string actionId, Action<Player, PlacedObject, ObjectDef> handler) => _actions[actionId] = handler;

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
            if (!def.Actions.Any(a => a.Id == m.ActionId)) return;
            if (_actions.TryGetValue(m.ActionId, out Action<Player, PlacedObject, ObjectDef>? handler)) handler(player, placed, def);
            else World.Log("No handler for object action '" + m.ActionId + "'.");
        }
    }

    /// <summary>
    /// Base for "go to that object and work at it": walks the player beside the object, then calls
    /// <see cref="Work"/> each tick until it returns false.
    /// </summary>
    public abstract class ObjectInteraction : Interaction
    {
        protected ObjectInteraction(PlacedObject target)
        {
            Target = target;
        }

        public PlacedObject Target { get; }

        public sealed override bool Tick(World world, Player player)
        {
            if (!world.IsBeside(player, Target.Plane, Target.Tile))
            {
                if (player.Waypoints.Count == 0 && !world.PathBeside(player, Target.Plane, Target.Tile))
                {
                    world.Tell(player, TextKind.Warning, "You can't reach that.");
                    return false;
                }
                world.ProcessMovement(player);
                if (!world.IsBeside(player, Target.Plane, Target.Tile)) return true;
                player.Waypoints.Clear();
            }
            return Work(world, player);
        }

        /// <summary>One tick of work while standing beside the object. Return false when done.</summary>
        protected abstract bool Work(World world, Player player);
    }
}
