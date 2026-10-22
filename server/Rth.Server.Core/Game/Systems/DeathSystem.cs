using Rth.Server.Content;
using Rth.Server.Persistence;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// What happens when someone dies: a dead player wakes in the realm of the dead (death.json
    /// <c>wake</c>) with balanced humours, and <see cref="PlayerRespawned"/> is published; whoever
    /// killed an NPC is told so. What the dead leave behind and how they get back is the
    /// <see cref="FerrySystem"/>'s. Listens for <see cref="EntityDied"/>.
    /// </summary>
    public sealed class DeathSystem : GameSystem
    {
        protected override void OnAttach() => World.Events.Subscribe<EntityDied>(OnDied);

        private void OnDied(EntityDied e)
        {
            if (e.Victim is Player player)
            {
                player.Deaths++;
                player.QueuedAbility = null;
                player.StalledAbility = null;
                player.RespawnAtTick = World.CurrentTick + (uint)World.Rules.RespawnDelayTicks;
                World.Tell(player, TextKind.Info, "Your humours come apart.");
            }
            else if (e.Killer is Player winner)
            {
                World.Tell(winner, TextKind.Info, "You have defeated the " + e.Victim.Name.ToLowerInvariant() + ".");
            }
        }

        public override void BeforePlayer(Player player)
        {
            if (player.IsDead && World.CurrentTick >= player.RespawnAtTick) Respawn(player);
        }

        /// <summary>A place named in data, or the world's spawn if it's missing or not open ground.</summary>
        public (int Plane, TileCoord Tile) PlaceOrSpawn(TilePlace? place)
        {
            if (place != null && World.Map.Contains(place.Plane, place.X, place.Y) && !World.Map.IsBlocked(place.Plane, place.X, place.Y))
                return (place.Plane, new TileCoord(place.X, place.Y));
            return (World.Map.SpawnPlane, World.Map.Spawn);
        }

        /// <summary>Where the dead wake.</summary>
        public (int Plane, TileCoord Tile) WakePlace => PlaceOrSpawn(World.Content.Death.Wake);

        /// <summary>Moves a player instantly (no walk), dropping whatever they were doing.</summary>
        public void Move(Player player, int plane, TileCoord tile)
        {
            World.StopInteraction(player);
            player.Plane = plane;
            player.Position = tile;
            player.Waypoints.Clear();
            player.WalkGoal = null;
            player.Teleported = true;
        }

        private void Respawn(Player player)
        {
            player.IsDead = false;
            (int plane, TileCoord tile) = WakePlace;
            Move(player, plane, tile);
            player.Effects.Clear();
            player.Cooldowns.Clear();
            player.LastCombatTick = 0;
            player.Phlegm = 0;
            player.Bile = 0; // the dead wake balanced
            World.RecalculateStats(player);
            player.Blood = player.MaxBlood;
            player.HealthChanged = true;
            World.Events.Publish(new PlayerRespawned(player));
        }

        public override void OnSave(Player player, PlayerSave save)
        {
            // Logging out while dead counts as having woken.
            if (!player.IsDead) return;
            (int plane, TileCoord tile) = WakePlace;
            save.Plane = plane;
            save.X = tile.X;
            save.Y = tile.Y;
        }
    }
}
