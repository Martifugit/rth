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
    /// objects (docs/ideas/The Unquiet Saint Religion Skilling Boss Spec.md). The encounter calls
    /// these as its mechanics fire (<see cref="UnquietSaintSystem"/> previews them with
    /// <c>::saint</c>). Every method works on the crypt objects near a tile, so each instance plays its own.
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

        /// <summary>Seconds between the symbols lighting, before the first strike, and between strikes.</summary>
        public const float SymbolGap = 0.7f, StrikeDelay = 1.5f, StrikeGap = 0.8f;

        /// <summary>
        /// Are you a believer?: the symbols light in sequence, each in a style's colour, 0.7 s apart;
        /// 1.5 s after the last, a strike flies from each in the same order at the player, in neutral light, 0.8 s
        /// apart. Returns when each strike lands, in seconds from now.
        /// </summary>
        public List<float> Believer(Player player, IReadOnlyList<CombatStyle> sequence)
        {
            var lands = new List<float>();
            List<PlacedObject> symbols = Near(Symbol, player.Plane, player.Position);
            if (symbols.Count == 0) return lands;
            float strikes = (sequence.Count - 1) * SymbolGap + StrikeDelay;
            for (int i = 0; i < sequence.Count; i++)
            {
                PlacedObject symbol = symbols[i % symbols.Count];
                string colour = CombatStyles.Colour(sequence[i]);
                _world.PlayObjectEffect(symbol, "flare", "colour=" + colour + ";delay=" + F(i * SymbolGap) + ";hold=0.6");
                float launch = strikes + i * StrikeGap;
                // The strikes come in the symbols' neutral light: which style each one is must be remembered
                // from the telegraph, not read off the projectile.
                _world.PlayObjectEffect(symbol, "strike", "delay=" + F(launch) + ";player=" + player.Id);
                lands.Add(launch + HolyStrikeFlight);
            }
            return lands;
        }

        /// <summary>How long a strike flies from its symbol (the client's projectile).</summary>
        public const float HolyStrikeFlight = 0.55f;

        /// <summary>
        /// Pay what you owe's judgement: <paramref name="delay"/> seconds after the sigils light, the
        /// floor erupts in <paramref name="pulses"/> blasts everywhere but the lit sigils.
        /// </summary>
        public void Judgement(int plane, TileCoord at, float delay, int pulses, float interval)
        {
            foreach (PlacedObject saint in Near(Saint, plane, at))
                _world.PlayObjectEffect(saint, "judgement", "delay=" + F(delay) + ";pulses=" + pulses + ";interval=" + F(interval));
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
    }
}
