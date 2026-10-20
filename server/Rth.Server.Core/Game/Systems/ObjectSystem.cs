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
        private readonly Dictionary<string, Action<Player, PlacedObject, ObjectDef>> _actions = new Dictionary<string, Action<Player, PlacedObject, ObjectDef>>();

        public ObjectSystem(IEnumerable<PlacedObject> placed)
        {
            _placed = placed.ToList();
        }

        public IReadOnlyList<PlacedObject> Placed => _placed;

        public void RegisterAction(string actionId, Action<Player, PlacedObject, ObjectDef> handler) => _actions[actionId] = handler;

        /// <summary>Whether a flag-gated action is available to this player; tells them why not if it isn't.</summary>
        public static bool Unlocked(World world, Player player, ActionDef action)
        {
            if (action.RequiresFlag == null || world.Find<FlagSystem>()?.IsSet(player, action.RequiresFlag) == true) return true;
            world.Tell(player, TextKind.Warning, action.LockedMessage ?? "You can't do that yet.");
            return false;
        }

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
            if (!Unlocked(World, player, action)) return;
            if (action.Message != null) World.Tell(player, TextKind.Info, action.Message);
            if (_actions.TryGetValue(m.ActionId, out Action<Player, PlacedObject, ObjectDef>? handler)) handler(player, placed, def);
            else if (action.Message == null) World.Log("No handler for object action '" + m.ActionId + "'.");
        }
    }
}
