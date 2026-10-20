using Rth.Shared.World;

namespace Rth.Server.Game
{
    public sealed class PlayerRegion
    {
        public string? Current;
    }

    /// <summary>
    /// Knows which region each player is in and publishes <see cref="RegionEntered"/> when that
    /// changes (including on login), for achievements, music, area rules and the like.
    /// </summary>
    public sealed class RegionSystem : GameSystem
    {
        public WorldRegion? RegionOf(Player player) => (World.Map as WorldMap)?.RegionAt(player.Position.X, player.Position.Y);

        public override void AfterPlayer(Player player)
        {
            WorldRegion? region = RegionOf(player);
            PlayerRegion state = player.Component<PlayerRegion>();
            if (region == null || region.Name == state.Current) return;
            state.Current = region.Name;
            World.Events.Publish(new RegionEntered(player, region));
        }
    }
}
