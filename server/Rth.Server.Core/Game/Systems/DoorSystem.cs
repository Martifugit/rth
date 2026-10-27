using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Doors (objects whose type has a <see cref="DoorDef"/>): each closes one edge of its tile
    /// (<see cref="PlacedObject.Edge"/>). The map is exported with doorways open, so this system
    /// closes every door's edge when it attaches; "open-door" takes the wall away and "close-door"
    /// puts it back, for everyone, and an open door with <see cref="DoorDef.CloseAfter"/> swings shut
    /// by itself. A door is worked from its own tile or the tile across its edge, whichever side of
    /// it the player is on. Clients get the doors standing open near them as the "doors" state.
    /// </summary>
    public sealed class DoorSystem : GameSystem
    {
        public const string OpenAction = DoorDef.OpenAction;
        public const string CloseAction = DoorDef.CloseAction;
        public const string StateKey = DoorDef.StateKey;

        private sealed class Door
        {
            public PlacedObject Placed = null!;
            public WallSide Side;
            public bool Open;
            public uint ClosesAt;
        }

        private sealed class SentDoors
        {
            public string? Last;
        }

        private readonly Dictionary<PlacedObject, Door> _doors = new Dictionary<PlacedObject, Door>();

        protected override void OnAttach()
        {
            ObjectSystem objects = World.Get<ObjectSystem>();
            foreach (PlacedObject placed in objects.Placed)
            {
                if (!World.Content.Objects.TryGetValue(placed.Type, out ObjectDef? def) || def.Door == null) continue;
                if (!(CollisionMap.SideOf(placed.Edge) is WallSide side))
                {
                    World.Log("Door '" + placed.Type + "' at (" + placed.X + ", " + placed.Y + ") has no edge (N, E, S or W); left open.");
                    continue;
                }
                _doors[placed] = new Door { Placed = placed, Side = side };
                World.Map.AddWall(placed.Plane, placed.X, placed.Y, side);
            }
            objects.RegisterAction(OpenAction, (player, placed, _) => Start(player, placed, true));
            objects.RegisterAction(CloseAction, (player, placed, _) => Start(player, placed, false));
        }

        /// <summary>Whether the door at this object stands open.</summary>
        public bool IsOpen(PlacedObject placed) => _doors.TryGetValue(placed, out Door? d) && d.Open;

        private void Start(Player player, PlacedObject placed, bool open)
        {
            if (_doors.TryGetValue(placed, out Door? door)) World.StartInteraction(player, new DoorInteraction(this, door.Placed, door.Side, open));
        }

        /// <summary>Opens or closes a door (nothing if it already is). Returns whether it changed.</summary>
        public bool Set(PlacedObject placed, bool open)
        {
            if (!_doors.TryGetValue(placed, out Door? door) || door.Open == open) return false;
            door.Open = open;
            if (open)
            {
                World.Map.RemoveWall(placed.Plane, placed.X, placed.Y, door.Side);
                int after = World.Content.Objects.TryGetValue(placed.Type, out ObjectDef? def) ? def.Door?.CloseAfter ?? 0 : 0;
                door.ClosesAt = after > 0 ? World.CurrentTick + (uint)after : 0;
            }
            else World.Map.AddWall(placed.Plane, placed.X, placed.Y, door.Side);
            return true;
        }

        public override void OnTimers()
        {
            uint now = World.CurrentTick;
            foreach (Door door in _doors.Values)
                if (door.Open && door.ClosesAt != 0 && now >= door.ClosesAt) Set(door.Placed, false);
        }

        public override void SendState(Player player)
        {
            var open = _doors.Values
                .Where(d => d.Open && World.CanSee(player, d.Placed.Plane, d.Placed.Tile))
                .Select(d => new DoorState { Plane = d.Placed.Plane, X = d.Placed.X, Y = d.Placed.Y, Edge = d.Placed.Edge ?? "" })
                .ToList();
            JsonStateMessage message = JsonStateMessage.Of(StateKey, open);
            SentDoors sent = player.Component<SentDoors>();
            if (message.Json == sent.Last) return;
            sent.Last = message.Json;
            player.Connection.Send(message);
        }
    }

    /// <summary>Walks to a door, to whichever of its two sides is nearer, then opens or closes it.</summary>
    public sealed class DoorInteraction : Interaction
    {
        private readonly DoorSystem _doors;
        private readonly PlacedObject _door;
        private readonly bool _open;
        private readonly TileCoord[] _sides;
        private TileCoord? _goal;

        public DoorInteraction(DoorSystem doors, PlacedObject door, WallSide side, bool open)
        {
            _doors = doors;
            _door = door;
            _open = open;
            (int dx, int dy, _, _) = CollisionMap.Edge(side);
            _sides = new[] { door.Tile, door.Tile.Offset(dx, dy) };
        }

        public override InteractionFocus? Focus(World world) => InteractionFocus.OnObject(_door);

        public override bool Tick(World world, Player player)
        {
            if (player.Plane != _door.Plane) return false;
            if (!_sides.Contains(player.Position))
            {
                if (_goal == null || player.Waypoints.Count == 0)
                {
                    List<TileCoord>? best = null;
                    foreach (TileCoord side in _sides)
                    {
                        if (world.Map.IsBlocked(_door.Plane, side.X, side.Y)) continue;
                        List<TileCoord> path = Pathfinder.FindPath(world.Map, _door.Plane, player.Position, side, world.Config.PathSearchRadius);
                        if (path.Count == 0 || path[path.Count - 1] != side) continue;
                        if (best == null || path.Count < best.Count) best = path;
                    }
                    if (best == null)
                    {
                        world.Tell(player, TextKind.Warning, "You can't reach that.");
                        return false;
                    }
                    _goal = best[best.Count - 1];
                    player.Waypoints.Clear();
                    foreach (TileCoord step in best) player.Waypoints.Enqueue(step);
                }
                world.ProcessMovement(player, () => _sides.Contains(player.Position));
                if (!_sides.Contains(player.Position)) return true;
                player.Waypoints.Clear();
            }
            TileCoord other = player.Position == _sides[0] ? _sides[1] : _sides[0];
            world.Face(player, other);
            if (!_doors.Set(_door, _open))
                world.Tell(player, TextKind.Info, _open ? "It's already open." : "It's already shut.");
            return false;
        }
    }
}
