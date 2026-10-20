using System;
using System.Diagnostics;
using System.Threading;

namespace Rth.Server.Timing
{
    /// <summary>Time source for the tick loop, so tests can run it on simulated time.</summary>
    public interface IClock
    {
        /// <summary>Monotonic time in <see cref="TimeSpan"/> ticks (100 ns).</summary>
        long Now { get; }

        /// <summary>Waits for the given duration, or until <paramref name="cancel"/> is set. Returns true if cancelled.</summary>
        bool Wait(long duration, WaitHandle cancel);
    }

    public sealed class SystemClock : IClock
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public long Now => _stopwatch.Elapsed.Ticks;

        public bool Wait(long duration, WaitHandle cancel)
        {
            if (duration <= 0) return cancel.WaitOne(0);
            return cancel.WaitOne(TimeSpan.FromTicks(duration));
        }
    }
}
