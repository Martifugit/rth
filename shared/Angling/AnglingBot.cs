using System.Collections.Generic;
using System.Text;

namespace Rth.Shared.Angling
{
    /// <summary>
    /// A player for the fight, for tests and pacing: it sees what the panel shows (a tell, or the
    /// fish's move once it starts if tells are hidden), reacts after <see cref="ReactionSteps"/>,
    /// counters each move, and reels to keep the tension in the comfortable zone. A share of
    /// <see cref="MistakesPerThousand"/> of its reactions are wrong (seeded, so tests repeat).
    /// </summary>
    public sealed class AnglingBot
    {
        public int ReactionSteps = 6;
        public int MistakesPerThousand;
        /// <summary>Reel when tension is under this, stop over <see cref="ReelStop"/>.</summary>
        public int ReelStart = 560;
        public int ReelStop = 740;

        private readonly Queue<AnglingMove> _seen = new Queue<AnglingMove>();
        private uint _rng;
        private bool _reeling;
        private AnglingMove _acting = AnglingMove.Straight;
        private bool _mistaken;

        public AnglingBot(uint seed = 12345) => _rng = seed == 0 ? 1u : seed;

        private uint Next()
        {
            uint x = _rng;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return _rng = x;
        }

        /// <summary>What the bot does this step, from what it can see of the fight.</summary>
        public byte Decide(AnglingFight fight)
        {
            // What the panel shows: a visible tell is the move to come; otherwise the move as it happens.
            AnglingMove shown = fight.InTell && fight.TellVisible ? fight.Tell : fight.Current;
            _seen.Enqueue(shown);
            AnglingMove reacting = _seen.Count > ReactionSteps ? _seen.Dequeue() : AnglingMove.Straight;
            // A visible tell lets it get ready: it acts on the tell only in the tell's last steps,
            // which the reaction delay gives it naturally.
            if (reacting != _acting)
            {
                _acting = reacting;
                _mistaken = MistakesPerThousand > 0 && Next() % 1000 < (uint)MistakesPerThousand;
            }
            AnglingMove act = _acting;
            byte pull = AnglingInput.PullNone;
            bool mayReel = true;
            switch (act)
            {
                case AnglingMove.Left: pull = AnglingInput.PullRight; break;
                case AnglingMove.Right: pull = AnglingInput.PullLeft; break;
                case AnglingMove.Dive: pull = AnglingInput.PullUp; break;
                case AnglingMove.Run:
                case AnglingMove.Jump:
                case AnglingMove.Latch:
                    mayReel = false;
                    break;
            }
            if (_mistaken)
            {
                // The wrong way: the fish's own side, or reeling into a run.
                if (pull == AnglingInput.PullRight) pull = AnglingInput.PullLeft;
                else if (pull == AnglingInput.PullLeft) pull = AnglingInput.PullRight;
                else if (pull == AnglingInput.PullUp) pull = AnglingInput.PullNone;
            }
            // A mistake on a run, a jump or a latch is reeling straight through it.
            if (_mistaken && (act == AnglingMove.Run || act == AnglingMove.Jump || act == AnglingMove.Latch)) return AnglingInput.Make(true, pull);
            if (!mayReel) _reeling = false;
            else if (fight.Tension < ReelStart) _reeling = true;
            else if (fight.Tension > ReelStop) _reeling = false;
            return AnglingInput.Make(_reeling && mayReel, pull);
        }

        /// <summary>Plays a whole fight; returns the inputs it used.</summary>
        public List<byte> Play(AnglingFight fight)
        {
            var inputs = new List<byte>();
            while (!fight.Over)
            {
                byte input = Decide(fight);
                inputs.Add(input);
                fight.Step(input);
            }
            return inputs;
        }
    }

    /// <summary>
    /// The fight's inputs as text, for the "angling" intent: a start step, then runs of one input
    /// ("12:1x40,3x12,0x5" is steps 12 to 68). Batches are small enough for one intent each.
    /// </summary>
    public static class AnglingInputs
    {
        /// <summary>Encodes inputs from a step on; stops before <paramref name="maxLength"/> characters and says how many it took.</summary>
        public static string Encode(int startStep, IReadOnlyList<byte> inputs, int from, int maxLength, out int taken)
        {
            var text = new StringBuilder();
            text.Append(startStep).Append(':');
            int i = from;
            while (i < inputs.Count)
            {
                byte input = inputs[i];
                int run = 1;
                while (i + run < inputs.Count && inputs[i + run] == input) run++;
                string part = (text[text.Length - 1] == ':' ? "" : ",") + input + "x" + run;
                if (text.Length + part.Length > maxLength) break;
                text.Append(part);
                i += run;
            }
            taken = i - from;
            return text.ToString();
        }

        /// <summary>Decodes a batch; false if it isn't one. Inputs are 0-7; runs at least 1.</summary>
        public static bool TryDecode(string text, out int startStep, List<byte> inputs, int maxSteps = 2000)
        {
            startStep = 0;
            inputs.Clear();
            if (string.IsNullOrEmpty(text)) return false;
            int colon = text.IndexOf(':');
            if (colon <= 0 || !int.TryParse(text.Substring(0, colon), out startStep) || startStep < 0) return false;
            string body = text.Substring(colon + 1);
            if (body.Length == 0) return true;
            foreach (string part in body.Split(','))
            {
                int x = part.IndexOf('x');
                if (x <= 0 || !byte.TryParse(part.Substring(0, x), out byte input) || input > AnglingInput.Max) return false;
                if (!int.TryParse(part.Substring(x + 1), out int run) || run < 1 || inputs.Count + run > maxSteps) return false;
                for (int r = 0; r < run; r++) inputs.Add(input);
            }
            return true;
        }
    }
}
