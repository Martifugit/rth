using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rth.Shared.Combat;
using Rth.Shared.World;

namespace Rth.Server.Game.Encounters
{
    /// <summary>
    /// The Unquiet Saint's telegraphs and room effects, as object effects on the crypt's placed
    /// objects (docs/ideas/The Unquiet Saint Religion Skilling Boss Spec.md). The encounter will
    /// call these as its mechanics fire; until it exists they are previewed with <c>::saint</c>.
    /// Every method works on the crypt objects near a tile, so each instance plays its own.
    /// </summary>
    public sealed class UnquietSaintFx
    {
        public const string Symbol = "holy_symbol";
        public const string Fount = "crypt_fount";
        public const string Sigil = "crypt_sigil";
        public const string Saint = "unquiet_saint";
        public const string Altar = "crypt_altar";
        public const string Offertory = "crypt_offertory";

        private readonly World _world;

        public UnquietSaintFx(World world) => _world = world;

        /// <summary>The crypt's objects of one type near a tile, west to east.</summary>
        public List<PlacedObject> Near(string type, int plane, TileCoord at) =>
            _world.Get<ObjectSystem>().Placed
                .Where(o => o.Type == type && o.Plane == plane && o.Tile.ChebyshevDistance(at) <= World.ObjectEffectRadius)
                .OrderBy(o => o.X).ThenBy(o => o.Y).ToList();

        private static string F(float f) => f.ToString(CultureInfo.InvariantCulture);

        /// <summary>Are you a believer?: the symbols light in sequence, each in a style's colour, 0.7 s apart.</summary>
        public void Believer(int plane, TileCoord at, IReadOnlyList<CombatStyle> sequence)
        {
            List<PlacedObject> symbols = Near(Symbol, plane, at);
            if (symbols.Count == 0) return;
            for (int i = 0; i < sequence.Count; i++)
                _world.PlayObjectEffect(symbols[i % symbols.Count], "flare",
                    "colour=" + CombatStyles.Colour(sequence[i]) + ";delay=" + F(i * 0.7f) + ";hold=0.6");
        }

        /// <summary>Let there be light: rings rise round these founts, brighten for 10 s, scorch, and leave them dry for 20 s.</summary>
        public void Light(IEnumerable<PlacedObject> founts)
        {
            foreach (PlacedObject f in founts) _world.PlayObjectEffect(f, "light", "ramp=10;dry=20");
        }

        /// <summary>Pay what you owe: these sigils light as the safe tiles for the delay.</summary>
        public void Sigils(IEnumerable<PlacedObject> sigils, float seconds)
        {
            foreach (PlacedObject s in sigils) _world.PlayObjectEffect(s, "safe", "seconds=" + F(seconds));
        }

        /// <summary>Hold your tongue: the room hushes (fires and choir drop) for the silence.</summary>
        public void Hush(int plane, TileCoord at, float seconds)
        {
            foreach (PlacedObject saint in Near(Saint, plane, at)) _world.PlayObjectEffect(saint, "hush", "seconds=" + F(seconds));
        }

        /// <summary>The Wake bar, 0 to 1: his eyes open, the lamps gutter.</summary>
        public void Wake(int plane, TileCoord at, float level)
        {
            foreach (PlacedObject saint in Near(Saint, plane, at)) _world.PlayObjectEffect(saint, "wake", "level=" + F(Math.Max(0f, Math.Min(1f, level))));
        }

        /// <summary>Owed blood: a marker over the player, and the offertory waiting for this many phials.</summary>
        public void Owed(Player player, int phials, float seconds)
        {
            foreach (PlacedObject o in Near(Offertory, player.Plane, player.Position))
                _world.PlayObjectEffect(o, "owed", "player=" + player.Id + ";count=" + phials + ";seconds=" + F(seconds));
        }

        /// <summary>A cleanse: the channel at the altar, its shaft of light swelling, then a burst.</summary>
        public void Cleanse(int plane, TileCoord at, float seconds)
        {
            foreach (PlacedObject altar in Near(Altar, plane, at)) _world.PlayObjectEffect(altar, "cleanse", "seconds=" + F(seconds));
        }

        /// <summary>
        /// The ::saint preview command: plays one mechanic's effects around the player, as the
        /// encounter would. Returns what to tell them.
        /// </summary>
        public string Preview(Player player, string[] args)
        {
            string mechanic = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            int plane = player.Plane;
            TileCoord at = player.Position;
            Random random = _world.Random;
            if (Near(Saint, plane, at).Count == 0 && mechanic.Length > 0) return "You need to be in the Saint's crypt.";
            switch (mechanic)
            {
                case "believer":
                {
                    int length = args.Length > 1 && int.TryParse(args[1], out int n) ? Math.Max(1, n) : 4;
                    var styles = new[] { CombatStyle.Melee, CombatStyle.Ranged, CombatStyle.Occult };
                    Believer(plane, at, Enumerable.Range(0, length).Select(_ => styles[random.Next(styles.Length)]).ToList());
                    return "Are you a believer? (" + length + " symbols)";
                }
                case "light":
                    Light(Near(Fount, plane, at).OrderBy(_ => random.Next()).Take(3));
                    return "Let there be light: three founts, a 10 s ramp, then the scorch.";
                case "sigils":
                    Sigils(Near(Sigil, plane, at).OrderBy(_ => random.Next()).Take(3), 4f);
                    return "Pay what you owe: three safe sigils for 4 s.";
                case "hush":
                    Hush(plane, at, 3f);
                    return "Hold your tongue: 3 s of silence.";
                case "owed":
                    Owed(player, 12, 15f);
                    return "Owed blood: 12 phials in 15 s.";
                case "wake":
                {
                    float level = args.Length > 1 && float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float w) ? w / 100f : 0.5f;
                    Wake(plane, at, level);
                    return "Wake " + Math.Round(level * 100) + "%.";
                }
                case "cleanse":
                    Cleanse(plane, at, 3f);
                    return "A cleanse: 3 s at the altar.";
                default:
                    return "::saint believer [n] | light | sigils | hush | owed | wake <0-100> | cleanse";
            }
        }
    }
}
