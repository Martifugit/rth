using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Items;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Items set down as objects for a while (<see cref="PlaceDef"/>): logs lit as a campfire,
    /// which is an ordinary object type, so cooking on it is its recipes and its station. The
    /// object joins <see cref="ObjectSystem"/> on the player's tile and goes after its ticks.
    /// Clients near one get it in the "placed" server state and draw the type's model.
    /// </summary>
    public sealed class PlacingSystem : GameSystem
    {
        private sealed class Placement
        {
            public PlacedObject Object = new PlacedObject();
            public uint Until;
        }

        private sealed class Sent
        {
            public string Last = "";
        }

        private readonly List<Placement> _placed = new List<Placement>();

        protected override void OnAttach() =>
            World.Get<ItemSystem>().RegisterAction(ItemActions.Place, (player, use) => Place(player, use.Def));

        /// <summary>Every object set down now.</summary>
        public IEnumerable<PlacedObject> Placed => _placed.Select(p => p.Object);

        /// <summary>Sets one of the item down as its object on the player's tile, if they have the tool and the tile is free.</summary>
        public PlacedObject? Place(Player player, ItemDef item)
        {
            PlaceDef? def = item.Place;
            ItemSystem items = World.Get<ItemSystem>();
            ObjectSystem objects = World.Get<ObjectSystem>();
            if (def == null || !World.Content.Objects.ContainsKey(def.Object)) return null;
            if (def.Tool != null && !Has(player, def.Tool))
            {
                string name = def.Tool.StartsWith("tag:", StringComparison.Ordinal) ? def.Tool.Substring(4) : World.Content.Item(def.Tool)?.Name.ToLowerInvariant() ?? def.Tool;
                World.Tell(player, TextKind.Warning, "You need a " + name + " to do that.");
                return null;
            }
            if (objects.AllAt(player.Plane, player.Position).Any())
            {
                World.Tell(player, TextKind.Warning, "You can't do that here.");
                return null;
            }
            if (!items.Take(player, item.Id, 1)) return null;
            World.StopInteraction(player);
            var placed = new PlacedObject { Type = def.Object, Plane = player.Plane, X = player.Position.X, Y = player.Position.Y };
            objects.Add(placed);
            _placed.Add(new Placement { Object = placed, Until = World.CurrentTick + (uint)Math.Max(1, def.Ticks) });
            World.Animate(player, "work", null);
            World.Tell(player, TextKind.Info, def.Message);
            return placed;
        }

        private bool Has(Player player, string tool)
        {
            PlayerItems held = World.Get<ItemSystem>().Of(player);
            foreach (ItemContainer c in new[] { held.Inventory, held.Equipment })
                for (int i = 0; i < c.Size; i++)
                {
                    if (c.IsEmpty(i)) continue;
                    if (!tool.StartsWith("tag:", StringComparison.Ordinal) ? c[i].ItemId == tool : World.Content.Item(c[i].ItemId)?.Tags.Contains(tool.Substring(4)) == true) return true;
                }
            return false;
        }

        public override void OnTimers()
        {
            ObjectSystem objects = World.Get<ObjectSystem>();
            foreach (Placement p in _placed.Where(p => World.CurrentTick >= p.Until).ToList())
            {
                _placed.Remove(p);
                objects.Remove(p.Object);
            }
        }

        public override void SendState(Player player)
        {
            var states = _placed.Where(p => World.CanSee(player, p.Object.Plane, p.Object.Tile))
                .Select(p => new PlacedState { Type = p.Object.Type, Plane = p.Object.Plane, X = p.Object.X, Y = p.Object.Y }).ToList();
            JsonStateMessage message = JsonStateMessage.Of(PlacedState.StateKey, states);
            Sent sent = player.Component<Sent>();
            if (message.Json == sent.Last) return;
            sent.Last = message.Json;
            player.Connection.Send(message);
        }
    }
}
