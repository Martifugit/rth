using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Persistence;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Building and testing commands, typed in chat after "::" (only when
    /// <see cref="ServerConfig.DevCommands"/> is on, as it is for the Editor's server):
    /// <c>::tele x y</c>, <c>::tele region</c> (a region's middle, instanced ones too),
    /// <c>::pass name</c> (a pass over the spine, from the layout), <c>::fly</c> (a free camera,
    /// <c>::land x y</c> ends it there),
    /// <c>::level skill n</c>, <c>::give item [count]</c>, <c>::devotion</c>, <c>::die</c>,
    /// <c>::boss id|win|fail</c>, <c>::fx type effect [key=value...]</c> (any object effect on
    /// the objects of a type near you), and each boss's previews under its own word
    /// (<c>::saint light</c>), registered with <see cref="RegisterPreview"/>.
    /// </summary>
    public sealed class DevSystem : GameSystem
    {
        public const string Prefix = "::";

        /// <summary>Whether a player's client is flying its camera (not saved: logging in walks).</summary>
        private sealed class Flying
        {
            public bool On;
        }

        public bool Enabled => World.Config.DevCommands;

        private readonly Dictionary<string, (string About, Func<Player, string[], string> Run)> _previews =
            new Dictionary<string, (string, Func<Player, string[], string>)>();

        /// <summary>A boss's effect previews under one word (<c>::saint ...</c>); the handler returns what to tell the player (or null, having told it).</summary>
        public void RegisterPreview(string word, string about, Func<Player, string[], string> run) => _previews[word] = (about, run);


        public void Run(Player player, string command)
        {
            string[] args = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0) return;
            switch (args[0].ToLowerInvariant())
            {
                case "tele":
                    Teleport(player, args);
                    break;
                case "pass":
                    Pass(player, args);
                    break;
                case "fly":
                    SetFlying(player, !player.Component<Flying>().On);
                    break;
                case "land" when args.Length >= 3 && int.TryParse(args[1], out int lx) && int.TryParse(args[2], out int ly):
                    // Sent by the client's fly camera for a click on the ground: stand there, on the surface.
                    SetFlying(player, false);
                    player.Plane = 0;
                    MoveTo(player, new TileCoord(lx, ly));
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
                    else
                    {
                        int left = World.Get<ItemSystem>().Give(player, args[1], count); // what didn't fit
                        Say(player, "Gave " + (count - left) + " " + args[1] + (left > 0 ? " (" + left + " didn't fit)." : "."));
                    }
                    break;
                case "fx" when args.Length >= 3:
                    Fx(player, args[1], args[2], string.Join(";", args.Skip(3)));
                    break;
                case "boss" when args.Length >= 2:
                    BossSystem bosses = World.Get<BossSystem>();
                    if (args[1] == "win" || args[1] == "fail") bosses.EndRun(player, args[1] == "win");
                    else if (!bosses.StartRun(player, args[1])) Say(player, "No boss '" + args[1] + "'.");
                    break;
                case "devotion":
                    World.Get<ReligionSystem>().Restore(player, player.MaxDevotion);
                    break;
                case "die":
                    // Dies where you stand, with whatever you carry: for trying the ferryman and the long way back.
                    World.Get<CombatSystem>().Damage(player, int.MaxValue / 2, null, Rth.Shared.Net.HitKind.Bleed);
                    break;
                case string word when _previews.TryGetValue(word, out var preview):
                    Say(player, preview.Run(player, args.Skip(1).ToArray()));
                    break;
                default:
                    // One command a line, so the list reads in the chat box.
                    Say(player, "Dev commands:");
                    foreach (string line in Help) Say(player, line);
                    foreach (KeyValuePair<string, (string About, Func<Player, string[], string> Run)> p in _previews)
                        Say(player, "::" + p.Key + "  -  " + p.Value.About);
                    break;
            }
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            if (Enabled) player.Connection.Send(JsonStateMessage.Of(FlyState.Key, new FlyState()));
        }

        private void SetFlying(Player player, bool on)
        {
            player.Component<Flying>().On = on;
            player.Connection.Send(JsonStateMessage.Of(FlyState.Key, new FlyState { On = on }));
            Say(player, on
                ? "Flying: WASD to move, Space and Ctrl to rise and sink, Shift for speed, the wheel sets it, right-drag to look. Click the ground to land there; Escape or ::fly to stop."
                : "Back on your feet.");
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
            MoveTo(player, tile);
        }

        /// <summary>
        /// <c>::pass salt</c>: the middle of a pass over the spine (its id in the continent layout,
        /// with or without "pass-"); <c>::pass</c> lists them. Read from the world map's chart, so
        /// it follows the layout.
        /// </summary>
        private void Pass(Player player, string[] args)
        {
            List<Rth.Shared.World.Layout.ChartLine> passes = World.Chart?.Roads.Where(r => r.Kind == "pass" && r.Points.Length >= 4).ToList()
                ?? new List<Rth.Shared.World.Layout.ChartLine>();
            if (passes.Count == 0)
            {
                Say(player, "No passes: the continent layout isn't loaded.");
                return;
            }
            string want = args.Length >= 2 ? string.Join("-", args.Skip(1)).ToLowerInvariant() : "";
            Rth.Shared.World.Layout.ChartLine? pass = passes.FirstOrDefault(p => p.Id == want || p.Id == "pass-" + want);
            if (pass == null)
            {
                Say(player, "::pass <name>, one of: " + string.Join(", ", passes.Select(ShortName)) + ".");
                return;
            }
            MoveTo(player, Halfway(pass.Points));
        }

        private static string ShortName(Rth.Shared.World.Layout.ChartLine pass) =>
            pass.Id.StartsWith("pass-", StringComparison.Ordinal) ? pass.Id.Substring(5) : pass.Id;

        /// <summary>The tile halfway along a line (flattened x, y pairs) by length: a pass's saddle.</summary>
        private static TileCoord Halfway(float[] points)
        {
            double total = 0;
            for (int i = 2; i < points.Length; i += 2) total += Segment(points, i);
            double left = total / 2;
            for (int i = 2; i < points.Length; i += 2)
            {
                double length = Segment(points, i);
                if (left <= length && length > 0)
                {
                    double t = left / length;
                    return new TileCoord((int)Math.Floor(points[i - 2] + (points[i] - points[i - 2]) * t),
                                         (int)Math.Floor(points[i - 1] + (points[i + 1] - points[i - 1]) * t));
                }
                left -= length;
            }
            return new TileCoord((int)Math.Floor(points[points.Length - 2]), (int)Math.Floor(points[points.Length - 1]));
        }

        private static double Segment(float[] p, int i) => Math.Sqrt(Math.Pow(p[i] - p[i - 2], 2) + Math.Pow(p[i + 1] - p[i - 1], 2));

        private void MoveTo(Player player, TileCoord tile)
        {
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

        private static readonly string[] Help =
        {
            "::tele <region>  -  go to a region (e.g. ::tele saints_crypt)",
            "::tele <x> <y>  -  go to a tile",
            "::pass <name>  -  the middle of a pass over the spine (::pass lists them)",
            "::fly  -  fly the camera free of your body; click the ground to land there",
            "::land <x> <y>  -  stop flying and stand on a surface tile",
            "::level <skill> <level>  -  set a skill's level",
            "::give <item> <count>  -  items in your pack",
            "::devotion  -  refill devotion",
            "::die  -  die where you stand (the ferryman, the long way back)",
            "::boss <id>  -  start a boss run; ::boss win, ::boss fail end it",
            "::fx <object type> <effect>  -  play one object effect on every one near you (tuning)",
        };

        /// <summary>Plays an effect on every object of a type near the player (building and tuning effects).</summary>
        private void Fx(Player player, string type, string effect, string arg)
        {
            int n = 0;
            foreach (PlacedObject o in World.Get<ObjectSystem>().Placed)
                if (o.Type == type && o.Plane == player.Plane && o.Tile.ChebyshevDistance(player.Position) <= World.ObjectEffectRadius)
                {
                    World.PlayObjectEffect(o, effect, arg);
                    n++;
                }
            Say(player, n == 0 ? "No '" + type + "' near you." : "Played '" + effect + "' on " + n + " " + type + ".");
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

        private void Say(Player player, string text)
        {
            if (!string.IsNullOrEmpty(text)) World.Tell(player, TextKind.Info, text);
        }
    }
}
