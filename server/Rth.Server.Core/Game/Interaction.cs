using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// What a player is busy doing with something in the world: attacking an NPC, picking up an
    /// item, working at a station, talking to someone. A player has at most one; walking somewhere
    /// cancels it. Each tick the world calls <see cref="Tick"/> instead of plain movement, so the
    /// interaction decides whether to walk closer or act.
    /// </summary>
    public abstract class Interaction
    {
        /// <summary>Advance one tick. Return false when finished (or impossible) to end it.</summary>
        public abstract bool Tick(World world, Player player);

        /// <summary>Called once when the interaction ends for any reason.</summary>
        public virtual void End(World world, Player player) { }
    }

    /// <summary>
    /// "Go and stand beside that, then do something": walks the player to a tile straight beside
    /// the target (re-planning if the target moves), then calls <see cref="Work"/> each tick until
    /// it returns false. Objects, NPCs and later doors, rocks and altars all use this.
    /// </summary>
    public abstract class ApproachInteraction : Interaction
    {
        private TileCoord? _plannedFor;

        /// <summary>Where the target is now, or null if it has gone (which ends the interaction).</summary>
        protected abstract (int Plane, TileCoord Tile)? Target(World world);

        public sealed override bool Tick(World world, Player player)
        {
            (int Plane, TileCoord Tile)? target = Target(world);
            if (target == null) return false;
            (int plane, TileCoord tile) = target.Value;

            if (!world.IsBeside(player, plane, tile))
            {
                if (player.Waypoints.Count == 0 || _plannedFor != tile)
                {
                    _plannedFor = tile;
                    if (!world.PathBeside(player, plane, tile))
                    {
                        world.Tell(player, TextKind.Warning, "You can't reach that.");
                        return false;
                    }
                }
                world.ProcessMovement(player, () => world.IsBeside(player, plane, tile));
                if (!world.IsBeside(player, plane, tile)) return true;
                player.Waypoints.Clear();
            }
            return Work(world, player);
        }

        /// <summary>One tick of work while standing beside the target. Return false when done.</summary>
        protected abstract bool Work(World world, Player player);
    }

    /// <summary>Approaches a fixed world object.</summary>
    public abstract class ObjectInteraction : ApproachInteraction
    {
        protected ObjectInteraction(PlacedObject target)
        {
            Object = target;
        }

        public PlacedObject Object { get; }

        protected override (int Plane, TileCoord Tile)? Target(World world) => (Object.Plane, Object.Tile);
    }

    /// <summary>Approaches an NPC (following it if it moves) and then does one thing.</summary>
    public sealed class NpcInteraction : ApproachInteraction
    {
        private readonly System.Action<Player, Npc> _then;

        public NpcInteraction(int npcId, System.Action<Player, Npc> then)
        {
            NpcId = npcId;
            _then = then;
        }

        public int NpcId { get; }

        protected override (int Plane, TileCoord Tile)? Target(World world) =>
            world.FindEntity(NpcId) is Npc npc && !npc.IsDead ? (npc.Plane, npc.Position) : ((int, TileCoord)?)null;

        protected override bool Work(World world, Player player)
        {
            if (world.FindEntity(NpcId) is Npc npc)
            {
                world.Face(player, npc.Position);
                world.Face(npc, player.Position);
                // Townsfolk stop wandering off while someone deals with them.
                npc.Waypoints.Clear();
                npc.NextWanderTick = world.CurrentTick + 25;
                _then(player, npc);
            }
            return false;
        }
    }
}
