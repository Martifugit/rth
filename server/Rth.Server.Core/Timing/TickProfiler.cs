using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Rth.Server.Timing
{
    /// <summary>
    /// Where each tick's time goes: the eight phases and every system, kept over a rolling window
    /// of recent ticks. Cheap enough to run always (a timestamp either side of each call), so a
    /// slow tick on the live server can be explained after the fact (<c>::perf</c>, the slow-tick
    /// log line). Runs on the tick thread only.
    /// </summary>
    public sealed class TickProfiler
    {
        public static readonly string[] PhaseNames =
        {
            "timers", "input", "npcs", "players", "logouts", "logins", "zones", "updates", "cleanup",
        };

        private readonly int _window;
        private readonly string[] _systemNames;
        private readonly double _msPerTimestamp = 1000.0 / Stopwatch.Frequency;

        // The tick being measured.
        private readonly long[] _phaseNow;
        private readonly long[] _systemNow;
        private long _tickStart;

        // The rolling window: one row per recent tick.
        private readonly long[] _tickTotals;
        private readonly long[][] _phaseHistory;
        private readonly long[][] _systemHistory;
        private int _next;
        private int _filled;

        public TickProfiler(IReadOnlyList<string> systemNames, int window = 100)
        {
            _window = Math.Max(1, window);
            _systemNames = systemNames.ToArray();
            _phaseNow = new long[PhaseNames.Length];
            _systemNow = new long[_systemNames.Length];
            _tickTotals = new long[_window];
            _phaseHistory = new long[_window][];
            _systemHistory = new long[_window][];
            for (int i = 0; i < _window; i++)
            {
                _phaseHistory[i] = new long[PhaseNames.Length];
                _systemHistory[i] = new long[_systemNames.Length];
            }
        }

        public static long Now => Stopwatch.GetTimestamp();

        /// <summary>How long the last finished tick took, in milliseconds.</summary>
        public double LastTickMs { get; private set; }

        public void BeginTick()
        {
            Array.Clear(_phaseNow, 0, _phaseNow.Length);
            Array.Clear(_systemNow, 0, _systemNow.Length);
            _tickStart = Now;
        }

        /// <summary>Adds the time since <paramref name="since"/> to a phase.</summary>
        public void Phase(int phase, long since) => _phaseNow[phase] += Now - since;

        /// <summary>Adds the time since <paramref name="since"/> to a system (summed over every hook and player in the tick).</summary>
        public void System(int system, long since) => _systemNow[system] += Now - since;

        /// <summary>Closes the tick into the window. Returns its length in milliseconds.</summary>
        public double EndTick()
        {
            long total = Now - _tickStart;
            _tickTotals[_next] = total;
            Array.Copy(_phaseNow, _phaseHistory[_next], _phaseNow.Length);
            Array.Copy(_systemNow, _systemHistory[_next], _systemNow.Length);
            _next = (_next + 1) % _window;
            _filled = Math.Min(_window, _filled + 1);
            LastTickMs = total * _msPerTimestamp;
            return LastTickMs;
        }

        /// <summary>The systems that took longest in the last finished tick, as "name 1.2 ms" pieces.</summary>
        public string SlowestInLastTick(int count)
        {
            int last = (_next - 1 + _window) % _window;
            return string.Join(", ", Enumerable.Range(0, _systemNames.Length)
                .OrderByDescending(i => _systemHistory[last][i])
                .Take(count)
                .Where(i => _systemHistory[last][i] > 0)
                .Select(i => _systemNames[i] + " " + Ms(_systemHistory[last][i])));
        }

        /// <summary>A few lines on the recent window: the tick, each phase, and the costliest systems.</summary>
        public List<string> Report(int topSystems = 8)
        {
            var lines = new List<string>();
            if (_filled == 0)
            {
                lines.Add("No ticks measured yet.");
                return lines;
            }
            long sum = 0, max = 0;
            for (int i = 0; i < _filled; i++)
            {
                sum += _tickTotals[i];
                max = Math.Max(max, _tickTotals[i]);
            }
            lines.Add("Last " + _filled + " ticks: average " + Ms(sum / _filled) + ", worst " + Ms(max) + ".");

            var phases = new StringBuilder("Phases (average):");
            for (int p = 0; p < PhaseNames.Length; p++)
            {
                long phaseSum = 0;
                for (int i = 0; i < _filled; i++) phaseSum += _phaseHistory[i][p];
                phases.Append(' ').Append(PhaseNames[p]).Append(' ').Append(Ms(phaseSum / _filled));
                if (p < PhaseNames.Length - 1) phases.Append(',');
            }
            lines.Add(phases.ToString());

            var systems = Enumerable.Range(0, _systemNames.Length)
                .Select(s =>
                {
                    long systemSum = 0, systemMax = 0;
                    for (int i = 0; i < _filled; i++)
                    {
                        systemSum += _systemHistory[i][s];
                        systemMax = Math.Max(systemMax, _systemHistory[i][s]);
                    }
                    return (Name: _systemNames[s], Average: systemSum / _filled, Max: systemMax);
                })
                .Where(s => s.Max > 0)
                .OrderByDescending(s => s.Average)
                .Take(topSystems);
            lines.Add("Costliest systems (average / worst): " +
                      string.Join(", ", systems.Select(s => s.Name + " " + Ms(s.Average) + "/" + Ms(s.Max))) + ".");
            return lines;
        }

        private string Ms(long timestamps) =>
            (timestamps * _msPerTimestamp).ToString(timestamps * _msPerTimestamp < 10 ? "0.00" : "0.0", CultureInfo.InvariantCulture) + " ms";
    }
}
