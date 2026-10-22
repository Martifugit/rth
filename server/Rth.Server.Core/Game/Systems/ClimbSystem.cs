using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Stairs, ladders and trapdoors between floors. An object with a "climb" action and a landing
    /// (<see cref="PlacedObject.Landing"/>, marked in the scene) takes whoever uses it to that tile
    /// on that plane, after they have walked up beside it. One object per end, so every climb up
    /// has a climb down. Nothing else about floors needs rules: each plane has its own collision.
    /// </summary>
    public sealed class ClimbSystem : GameSystem
    {
        public const string ClimbAction = "climb";

        protected override void OnAttach()
        {
            World.Find<ObjectSystem>()?.RegisterAction(ClimbAction, (player, placed, _) =>
                World.StartInteraction(player, new ObjectOnceInteraction(placed, p => Climb(p, placed))));
        }

        /// <summary>Moves the player to the object's landing. Returns false (and says why) if there is nowhere to land.</summary>
        public bool Climb(Player player, PlacedObject placed)
        {
            if (placed.Landing is not (int plane, TileCoord tile))
            {
                World.Log("Climb object '" + placed.Type + "' at (" + placed.X + ", " + placed.Y + ") has no landing.");
                return false;
            }
            if (World.Map.IsBlocked(plane, tile.X, tile.Y))
            {
                World.Log("Climb object '" + placed.Type + "' at (" + placed.X + ", " + placed.Y + ") lands on a blocked tile " + tile + " on plane " + plane + ".");
                World.Tell(player, TextKind.Warning, "You can't get up there.");
                return false;
            }

            World.StopInteraction(player);
            player.Waypoints.Clear();
            player.WalkGoal = null;
            player.Plane = plane;
            player.Position = tile;
            player.Teleported = true;
            return true;
        }
    }
}
