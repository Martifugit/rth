using System;
using System.Linq;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Building and testing commands, typed in chat after "::" (only when
    /// <see cref="ServerConfig.DevCommands"/> is on, as it is for the Editor's server):
    /// <c>::tele x y</c>, <c>::tele region</c> (a region's middle, instanced ones too),
    /// <c>::level skill n</c>, <c>::give item [count]</c>, <c>::devotion</c>.
    /// </summary>
    public sealed class DevSystem : GameSystem
    {
        public const string Prefix = "::";

        public bool Enabled => World.Config.DevCommands;

        public void Run(Player player, string command)
        {
            string[] args = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0) return;
            switch (args[0].ToLowerInvariant())
            {
                case "tele":
                    Teleport(player, args);
                    break;
                case "level" when args.Length >= 3 && int.TryParse(args[2], out int level):
                    SkillSystem skills = World.Get<SkillSystem>();
                    double want = World.Content.XpCurve.XpForLevel(Math.Max(1, level));
                    skills.AddXp(player, args[1], want - skills.Xp(player, args[1]));
                    Say(player, args[1] + " is level " + skills.Level(player, args[1]) + ".");
                    break;
                case "give" when args.Length >= 2:
                    int count = args.Length >= 3 && int.TryParse(args[2], out int n) ? n : 1;
                    if (World.Content.Item(args[1]) == null) Say(player, "No item '" + args[1] + "'.");
                    else Say(player, "Gave " + World.Get<ItemSystem>().Give(player, args[1], count) + " " + args[1] + ".");
                    break;
                case "devotion":
                    World.Get<ReligionSystem>().Restore(player, player.MaxDevotion);
                    break;
                default:
                    Say(player, "Commands: ::tele x y | ::tele region | ::level skill n | ::give item [count] | ::devotion");
                    break;
            }
        }

        private void Teleport(Player player, string[] args)
        {
            TileCoord tile;
            if (args.Length >= 3 && int.TryParse(args[1], out int x) && int.TryParse(args[2], out int y)) tile = new TileCoord(x, y);
            else if (args.Length == 2 && World.Map is WorldMap world && world.Regions.FirstOrDefault(r => r.Name == args[1]) is WorldRegion region)
                tile = new TileCoord(region.X + region.Width / 2, region.Y + region.Height / 2);
            else
            {
                Say(player, "::tele x y, or ::tele <region name>.");
                return;
            }
            if (World.Map.IsBlocked(player.Plane, tile.X, tile.Y))
            {
                // The middle of an arena may be furniture: take the nearest open tile.
                TileCoord? open = Nearest(player.Plane, tile);
                if (open == null)
                {
                    Say(player, "Nowhere to stand near " + tile + ".");
                    return;
                }
                tile = open.Value;
            }
            World.StopInteraction(player);
            player.Waypoints.Clear();
            player.WalkGoal = null;
            player.Position = tile;
            player.Teleported = true;
            Say(player, "Teleported to " + tile + ".");
        }

        private TileCoord? Nearest(int plane, TileCoord from)
        {
            for (int r = 1; r <= 16; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && !World.Map.IsBlocked(plane, from.X + dx, from.Y + dy))
                            return new TileCoord(from.X + dx, from.Y + dy);
            return null;
        }

        private void Say(Player player, string text) => World.Tell(player, TextKind.Info, text);
    }
}
