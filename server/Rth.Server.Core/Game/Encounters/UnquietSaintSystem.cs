using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game.Encounters
{
    /// <summary>
    /// The Unquiet Saint, as far as it exists: the state its mechanics keep (which founts are dry
    /// after a scorch), and a preview of the encounter run by <c>::saint</c> (dev commands): each
    /// mechanic on its own, or a whole demo run with the Requiem and Wake bars, the timer, the
    /// room's mood and the shouts coming on their own, without anything for the player to do yet.
    /// The real encounter (founts giving phials, pours, hits, deflection) is built on this.
    /// </summary>
    public sealed class UnquietSaintSystem : GameSystem
    {
        public const string BossId = "unquiet_saint";
        /// <summary>A scorched fount is dry for the scorch's 10 s ramp, its flash and 20 s after (in ticks).</summary>
        private const int DryTicks = 52;
        private const int ShoutEveryTicks = 25, OwedEveryTicks = 100;

        private UnquietSaintFx _fx = null!;
        private readonly Dictionary<PlacedObject, uint> _dryUntil = new Dictionary<PlacedObject, uint>();

        /// <summary>A demo run in progress for one player.</summary>
        private sealed class Demo
        {
            public float Requiem;
            public float Wake;
            public float WakeSent = -1f;
            public bool FirstCleanse;
            public uint NextShout;
            public uint NextOwed;
            public string LastShout = "";
        }

        private readonly Dictionary<Player, Demo> _demos = new Dictionary<Player, Demo>();
        private readonly Dictionary<Player, float> _wakeStep = new Dictionary<Player, float>();

        public UnquietSaintFx Fx => _fx;

        protected override void OnAttach()
        {
            _fx = new UnquietSaintFx(World);
            World.Find<DevSystem>()?.RegisterPreview("saint", "the Unquiet Saint: previews of every mechanic, and a demo run", Preview);
        }

        // ------------------------------------------------------------------ mechanics (shared by the demo and the previews)

        private bool InCrypt(Player p) => _fx.Near(UnquietSaintFx.Saint, p.Plane, p.Position).Count > 0;

        private List<CombatStyle> RandomSequence(int length)
        {
            var styles = new[] { CombatStyle.Melee, CombatStyle.Ranged, CombatStyle.Occult };
            return Enumerable.Range(0, length).Select(_ => styles[World.Random.Next(styles.Length)]).ToList();
        }

        /// <summary>Let there be light on up to three founts that aren't dry from an earlier scorch.</summary>
        private int Light(Player p)
        {
            uint now = World.CurrentTick;
            List<PlacedObject> ready = _fx.Near(UnquietSaintFx.Fount, p.Plane, p.Position)
                .Where(f => !_dryUntil.TryGetValue(f, out uint until) || now >= until).OrderBy(_ => World.Random.Next()).Take(3).ToList();
            foreach (PlacedObject f in ready) _dryUntil[f] = now + DryTicks;
            _fx.Light(ready);
            return ready.Count;
        }

        /// <summary>Pay what you owe: three of the sigils light; 4 s later the floor erupts everywhere else.</summary>
        private void PayWhatYouOwe(Player p)
        {
            // The sigils stay lit until the last blast has passed, so standing on one keeps you safe.
            const float delay = 4f, interval = 0.45f;
            const int pulses = 5;
            _fx.Sigils(_fx.Near(UnquietSaintFx.Sigil, p.Plane, p.Position).OrderBy(_ => World.Random.Next()).Take(3), delay + pulses * interval + 0.5f);
            _fx.Judgement(p.Plane, p.Position, delay, pulses, interval);
        }

        private string Shout(Player p, string shout)
        {
            switch (shout)
            {
                case "believer":
                    _fx.Believer(p, RandomSequence(4));
                    return "\"Are you a believer?\"";
                case "light":
                    int lit = Light(p);
                    return lit == 0 ? "Every fount is still dry from the last scorch." : "\"Let there be light.\" (" + lit + " founts)";
                case "sigils":
                    PayWhatYouOwe(p);
                    return "\"Pay what you owe.\"";
                case "hush":
                    _fx.Hush(p.Plane, p.Position, 3f);
                    return "\"Hold your tongue.\"";
                default:
                    return "";
            }
        }

        // ------------------------------------------------------------------ the ::saint command

        private static readonly (string Word, string About)[] Commands =
        {
            ("start", "a demo run: the bars, the timer, the room waking, and the shouts on their own"),
            ("stop", "end the demo run"),
            ("believer", "the symbols light in order, then their strikes come at you"),
            ("light", "Let there be light: three founts (scorched ones rest)"),
            ("sigils", "Pay what you owe: three safe sigils, then the judgement"),
            ("hush", "Hold your tongue: the fires drop for 3 s"),
            ("owed", "owed blood: the marker over you and the offertory"),
            ("wake", "the Wake rises a quarter (0, 25, 50, 75, 100%, then back to 0)"),
            ("cleanse", "the channel at the altar"),
        };

        /// <summary>::saint: one mechanic, or the list one command a line.</summary>
        public string Preview(Player p, string[] args)
        {
            string word = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            if (word.Length == 0 || Commands.All(c => c.Word != word))
            {
                World.Tell(p, TextKind.Info, "The Unquiet Saint (stand in the crypt: ::tele saints_crypt)");
                foreach ((string w, string about) in Commands) World.Tell(p, TextKind.Info, "::saint " + w + "  -  " + about);
                return null;
            }
            if (!InCrypt(p)) return "You need to be in the Saint's crypt: ::tele saints_crypt";
            switch (word)
            {
                case "start":
                    StartDemo(p);
                    return "A demo run of the Unquiet Saint. ::saint stop to end it.";
                case "stop":
                    if (!_demos.ContainsKey(p)) return "No demo run.";
                    EndDemo(p, won: false);
                    return "Demo stopped.";
                case "owed":
                    _fx.Owed(p, 12, 15f);
                    return "Owed blood: 12 phials in 15 s.";
                case "wake":
                {
                    float level = (_wakeStep.TryGetValue(p, out float w) ? w : 0f) + 0.25f;
                    if (level > 1.001f) level = 0f;
                    _wakeStep[p] = level;
                    _fx.Wake(p.Plane, p.Position, level);
                    return "Wake " + Math.Round(level * 100) + "%.";
                }
                case "cleanse":
                    _fx.Cleanse(p.Plane, p.Position, 3f);
                    return "A cleanse: 3 s at the altar.";
                default:
                    return Shout(p, word);
            }
        }

        // ------------------------------------------------------------------ the demo run

        private void StartDemo(Player p)
        {
            BossSystem bosses = World.Get<BossSystem>();
            bosses.StartRun(p, BossId);
            uint now = World.CurrentTick;
            _demos[p] = new Demo { NextShout = now + 12, NextOwed = now + 80 };
        }

        private void EndDemo(Player p, bool won)
        {
            _demos.Remove(p);
            _fx.Wake(p.Plane, p.Position, 0f);
            BossSystem bosses = World.Get<BossSystem>();
            if (bosses.InRun(p)) bosses.EndRun(p, won);
        }

        public override void AfterPlayer(Player p)
        {
            if (!_demos.TryGetValue(p, out Demo? d)) return;
            BossSystem bosses = World.Get<BossSystem>();
            if (!bosses.InRun(p) || !InCrypt(p))
            {
                _demos.Remove(p);
                return;
            }
            uint now = World.CurrentTick;

            // The spec's pace: the Wake fills by itself in about five minutes; here the Requiem
            // rises by itself too (no pours yet), to fill in about three and a half.
            d.Wake = Math.Min(1f, d.Wake + 1f / 500f);
            d.Requiem = Math.Min(1f, d.Requiem + 1f / 350f);
            bosses.SetBar(p, "requiem", "Requiem", d.Requiem, "#e8c070");
            bosses.SetBar(p, "wake", "Wake", d.Wake, "#c0303c", danger: true);
            if (d.Wake - d.WakeSent >= 0.02f)
            {
                d.WakeSent = d.Wake;
                _fx.Wake(p.Plane, p.Position, d.Wake);
            }

            if (!d.FirstCleanse && d.Requiem >= 0.5f)
            {
                d.FirstCleanse = true;
                _fx.Cleanse(p.Plane, p.Position, 3f);
                World.Tell(p, TextKind.Info, "The first cleanse: the Saint stills.");
                d.NextShout = Math.Max(d.NextShout, now + 8);
            }
            if (d.Requiem >= 1f)
            {
                _fx.Cleanse(p.Plane, p.Position, 3f);
                World.Tell(p, TextKind.Info, "The Requiem is sung. The Saint is laid back to rest.");
                EndDemo(p, won: true);
                return;
            }
            if (d.Wake >= 1f)
            {
                World.Tell(p, TextKind.Warning, "The Saint wakes.");
                EndDemo(p, won: false);
                return;
            }

            // One demand at a time: owed blood on its own clock, a shout between, the gap
            // shrinking (25 s here, quicker than the spec's so there's more to watch).
            if (now >= d.NextOwed)
            {
                _fx.Owed(p, 12, 15f);
                World.Tell(p, TextKind.Warning, "The Saint marks you: pay 12 phials at the offertory.");
                d.NextOwed = now + OwedEveryTicks;
                d.NextShout = Math.Max(d.NextShout, now + 26);
                return;
            }
            if (now >= d.NextShout)
            {
                string[] pool = { "believer", "light", "sigils", "hush" };
                string shout;
                do shout = pool[World.Random.Next(pool.Length)];
                while (shout == d.LastShout);
                d.LastShout = shout;
                World.Tell(p, TextKind.Warning, Shout(p, shout));
                int gap = (int)(ShoutEveryTicks * (1f - 0.4f * d.Wake));
                d.NextShout = now + (uint)Math.Max(12, gap);
            }
        }

        public override void OnLeaving(Player p) => _demos.Remove(p);
    }
}
